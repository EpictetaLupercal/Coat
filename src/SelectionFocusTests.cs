// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Coat;

internal static class SelectionFocusTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            results.Add("PASS: " + name);
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false };
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        var canvas = window.Canvas;
        var plan = new Plan { ShowGrid = false, ViewX = 120, ViewY = 55, Zoom = .8 };
        Rectangle(0, 0, 96, 96, "#D76A60");
        Rectangle(128, 0, 224, 96, "#48C17B");
        var note = new Annotation { Id = plan.NextId++, X = 280, Y = 64, IsToken = true, Size = 32 };
        plan.Annotations.Add(note);
        window.LoadDocument(plan, "");
        window.SetTool(EditorTool.Select);
        canvas.Mode = SelectionMode.Face;
        InterfaceLayoutTests.Layout(window);
        var original = canvas.PersistentView;
        var snapshot = plan.Serialize();

        void KeyPress(Key key) => canvas.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(canvas)!, 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        void Slash() => canvas.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, canvas, "/"))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
        void Click(V2 world)
        {
            canvas.PointerDown(canvas.Screen(world), MouseButton.Left);
            canvas.PointerUp(MouseButton.Left);
        }
        Slash();
        Check(!canvas.IsSelectionFocused && canvas.PersistentView == original, "Slash with no selection leaves the camera and display unchanged");
        Click(new(48, 48));
        Slash();
        Check(canvas.IsSelectionFocused && canvas.Center == new V2(48, 48) && canvas.Zoom > original.Zoom, "Typing slash centers and frames the selected face");
        Check(canvas.PersistentView == original && plan.Serialize() == snapshot, "Isolation preserves the project and its saved camera");

        // Put both rooms in view to verify that the unselected room is actually hidden.
        canvas.Center = original.Center;
        canvas.Zoom = original.Zoom;
        canvas.InvalidateVisual();
        var focusImage = Capture("focus-isolated.png");
        Check(Pixel(focusImage, new(48, 48)) != Pixel(focusImage, new(176, 48)) && Pixel(focusImage, new(176, 48)) == Pixel(focusImage, new(104, 48)),
            "The rendered canvas shows the selected room and hides the other room");
        Click(new(176, 48));
        Check(canvas.SelectedFaces.Count == 0 && canvas.IsSelectionFocused, "A hidden room is not clickable and deselection keeps Focus active");
        canvas.SelectAll();
        Check(canvas.SelectedFaces.SetEquals(new[] { plan.HitFace(new(48, 48))!.Key }) && canvas.SelectedAnnotations.Count == 0,
            "Select All is restricted to visible content while focused");
        canvas.Center = new(15, 25);
        canvas.Zoom = 2.5;
        KeyPress(Key.Divide);
        Check(!canvas.IsSelectionFocused && canvas.Center == original.Center && canvas.Zoom == original.Zoom, "Numpad divide restores the exact camera even after navigation");
        var normalImage = Capture("focus-restored.png");
        Check(Pixel(normalImage, new(176, 48)) != Pixel(focusImage, new(176, 48)), "Leaving Focus makes the other room visible again");

        canvas.ClearSelection(); Click(note.Position); KeyPress(Key.Divide);
        Check(canvas.IsSelectionFocused && canvas.Center == note.Position && canvas.Zoom > original.Zoom, "An isolated token uses its bounds for framing");
        Slash();
        window.Shortcuts.Bindings[ShortcutSettings.FocusAction] = new(Key.F8, ModifierKeys.None);
        Slash(); KeyPress(Key.Divide);
        Check(!canvas.IsSelectionFocused, "Changing the binding disables the default slash aliases");
        KeyPress(Key.F8);
        Check(canvas.IsSelectionFocused, "The custom shortcut toggles Focus");
        window.LoadDocument(new Plan(), "");
        Check(!canvas.IsSelectionFocused && canvas.Zoom == 1 && canvas.Center == new V2(), "Opening another plan clears the temporary view");

        var path = Path.Combine(output, "older-shortcuts.json");
        var oldBindings = ShortcutSettings.UnassignedBindings();
        oldBindings.Remove(ShortcutSettings.FocusAction);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(oldBindings));
        Check(ShortcutSettings.Load(path).Bindings[ShortcutSettings.FocusAction] == ShortcutSettings.FocusDefault, "Old preferences gain the slash binding");
        oldBindings["Dessin"] = ShortcutSettings.FocusDefault;
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(oldBindings));
        var migrated = ShortcutSettings.Load(path);
        Check(migrated.Bindings["Dessin"] == ShortcutSettings.FocusDefault && migrated.Bindings[ShortcutSettings.FocusAction] == ShortcutSettings.Unassigned,
            "Migration preserves an existing shortcut that already uses slash");
        var view = InterfaceLayoutTests.Children<Menu>(window).Single().Items.OfType<MenuItem>().Single(item => Equals(item.Header, "View"));
        var item = view.Items.OfType<MenuItem>().Single(menu => Equals(menu.Header, "Focus Mode"));
        item.IsChecked = true;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!item.IsChecked && !canvas.IsSelectionFocused, "The View menu stays unchecked when no selection can be isolated");

        window.Close(); app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));

        void Rectangle(double left, double bottom, double right, double top, string fill)
        {
            plan.Polygon(new V2[] { new(left, bottom), new(right, bottom), new(right, top), new(left, top) });
            plan.Fills[plan.HitFace(new((left + right) / 2, (bottom + top) / 2))!.Key] = fill;
        }
        BitmapSource Capture(string filename)
        {
            InterfaceLayoutTests.Layout(window);
            var path = Path.Combine(output, filename);
            MainWindow.SaveVisual((FrameworkElement)window.Content, path);
            var bitmap = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        }
        int Pixel(BitmapSource image, V2 world)
        {
            var location = canvas.TranslatePoint(canvas.Screen(world), (FrameworkElement)window.Content);
            var pixel = new byte[4];
            image.CopyPixels(new Int32Rect((int)location.X, (int)location.Y, 1, 1), pixel, 4, 0);
            return BitConverter.ToInt32(pixel);
        }
    }
}
