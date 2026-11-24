// Coat — Epicteta Lupercal
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Coat;

internal static class InteractionFeedbackTests
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
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, Width = 1050, Height = 680 };
        window.Shortcuts.AnimatedMenus = false;
        window.ChangeLanguage("en");
        window.Show();
        var canvas = window.Canvas;
        var layer = canvas.Feedback;
        InterfaceLayoutTests.Layout(window);
        Check(layer.CanPlay == SystemParameters.ClientAreaAnimation && !layer.IsHitTestVisible,
            "The non-interactive overlay inherits the Windows animation preference");
        layer.SetValue(InteractionFeedbackLayer.MotionAllowedProperty, true);

        void Click(V2 p)
        {
            canvas.PointerDown(canvas.Screen(p), MouseButton.Left);
            canvas.PointerUp(MouseButton.Left);
        }
        void Drag(V2 a, V2 b)
        {
            canvas.PointerDown(canvas.Screen(a), MouseButton.Left);
            canvas.PointerMove(canvas.Screen(b));
            canvas.PointerUp(MouseButton.Left);
        }
        void Load(Plan plan, EditorTool tool)
        {
            window.LoadDocument(plan, "");
            window.SetTool(tool);
            canvas.Center = new V2(); canvas.Zoom = 1;
            InterfaceLayoutTests.Layout(window);
        }
        bool Active(FeedbackCue cue) => layer.ActiveCues.Contains(cue);
        RenderTargetBitmap FeedbackFrameAt(long clickTime, int milliseconds)
        {
            layer.DrawFrame(clickTime + milliseconds * Stopwatch.Frequency / 1000);
            var frame = new RenderTargetBitmap((int)layer.ActualWidth, (int)layer.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            frame.Render(layer);
            return frame;
        }

        var plan = new Plan { ShowGrid = false };
        RectPath(plan, -128, -96, 128, 96);
        RectPath(plan, -32, -32, 32, 32);
        Load(plan, EditorTool.Select);
        var serial = plan.Serialize();
        canvas.Mode = SelectionMode.Vertex;
        Click(new(-128, -96));
        Check(Active(FeedbackCue.Vertex) && plan.Serialize() == serial, "Clicking a vertex queues a halo without changing the project");
        Wait(230);
        Check(!layer.IsAnimating && !layer.ActiveCues.Any(), "The vertex halo expires and detaches the frame callback");
        int idlePasses = layer.RenderPasses;
        Wait(60);
        Check(layer.RenderPasses == idlePasses, "The idle overlay performs no render passes");
        canvas.ClearSelection(); canvas.Mode = SelectionMode.Edge;
        Click(new(-106, -91));
        Check(Active(FeedbackCue.Edge), "An edge click triggers an outline halo");
        long edgeClick = Stopwatch.GetTimestamp();
        var edgeFrame = FeedbackFrameAt(edgeClick, 24);
        Check(HasInkNear(edgeFrame, canvas.Screen(new(-128, -96))) &&
            HasInkNear(edgeFrame, canvas.Screen(new(0, -96))) && HasInkNear(edgeFrame, canvas.Screen(new(128, -96))),
            "The halo covers the entire edge immediately even when clicked near one endpoint");
        Check(HasInkNear(edgeFrame, canvas.Screen(new(70, -88))) && !HasInkNear(edgeFrame, canvas.Screen(new(-106, -78))),
            "The glow follows the edge away from the click without drawing a cursor halo");
        edgeFrame = FeedbackFrameAt(edgeClick, 185);
        Check(!HasInkNear(edgeFrame, canvas.Screen(new(70, -88))) && HasInkNear(edgeFrame, canvas.Screen(new(70, -96))),
            "The edge halo narrows along its length as it fades");
        canvas.ClearSelection(); layer.Clear(); canvas.Mode = SelectionMode.Face;
        Click(new(-90, 0));
        Check(Active(FeedbackCue.Face) && canvas.SelectedFaces.Count == 1, "Selecting an outer region with a hole triggers its contour effect");
        long faceClick = Stopwatch.GetTimestamp();
        var faceFrame = FeedbackFrameAt(faceClick, 36);
        Check(new V2[] { new(-128, 0), new(128, 0), new(0, -96), new(0, 96) }.All(point => HasInkNear(faceFrame, canvas.Screen(point))) &&
            !HasInkNear(faceFrame, canvas.Screen(new(-90, 13))),
            "The face highlights its full perimeter immediately and leaves the point of the click clear");
        Check(HasInkNear(faceFrame, canvas.Screen(new(-32, 0))) && !HasInkNear(faceFrame, canvas.Screen(new(0, 0))),
            "The face animation renders the hole boundary and leaves its center clear");
        Check(HasInkNear(faceFrame, canvas.Screen(new(138, 0))), "The face halo extends visibly beyond the contour opposite the click");
        faceFrame = FeedbackFrameAt(faceClick, 235);
        Check(!HasInkNear(faceFrame, canvas.Screen(new(138, 0))) && HasInkNear(faceFrame, canvas.Screen(new(128, 0))),
            "The face halo narrows towards the whole contour as it fades");
        Wait(375);
        Check(!layer.IsAnimating && canvas.SelectedFaces.Count == 1, "The face pulse expires while the selection remains active");
        layer.Clear(); canvas.ClearSelection(); canvas.Mode = SelectionMode.Vertex;
        Drag(new(-170, 130), new(170, -130));
        Check(layer.ActiveCues.SequenceEqual(new[] { FeedbackCue.Marquee }) && canvas.SelectedVertices.Count > 1, "A marquee with multiple points produces exactly one pulse");
        layer.Clear(); window.MoveSelection(32, 0);
        Check(Active(FeedbackCue.Move) && plan.Serialize() != serial, "A validated move queues its origin silhouette");
        window.Undo();
        Check(!layer.IsAnimating && window.Document.Serialize() == serial, "Undo removes the overlay and restores geometry in one step");
        canvas.Mode = SelectionMode.Edge;
        canvas.PointerDown(canvas.Screen(new V2(-80, -96)), MouseButton.Left);
        canvas.PointerMove(canvas.Screen(new V2(-80, -160)));
        canvas.CancelGesture();
        Check(!Active(FeedbackCue.Move) && window.Document.Serialize() == serial, "Canceling a drag restores the geometry without a move cue");
        Drag(new(-80, -96), new(-80, -160));
        Check(Active(FeedbackCue.Move), "Releasing a moved edge shows its origin silhouette");

        Plan ExtrusionFixture(bool withHole = false)
        {
            var fixture = new Plan();
            fixture.Segment(new(-128, 0), new(128, 0));
            if (withHole) RectPath(fixture, -24, 48, 24, 80);
            Load(fixture, EditorTool.Select);
            canvas.Mode = SelectionMode.Edge;
            canvas.Transform = TransformKind.Translation;
            return fixture;
        }
        void StartExtrusion(V2 a, V2 b)
        {
            canvas.PointerDown(canvas.Screen(a), MouseButton.Left, shift: true);
            canvas.PointerMove(canvas.Screen(b));
        }

        var extrusion = ExtrusionFixture(withHole: true);
        var beforeExtrusion = Plan.Parse(extrusion.Serialize());
        StartExtrusion(new(0, 0), new(0, 128));
        Check(!Active(FeedbackCue.Extrusion) && extrusion.Faces().Count == 1, "The extrusion preview has no construction pulse");
        canvas.PointerUp(MouseButton.Left);
        Check(Active(FeedbackCue.Extrusion) && extrusion.Faces().Count == 2, "A committed Shift drag animates the newly enclosed area");
        layer.DrawFrame(Stopwatch.GetTimestamp() + (long)(.17 * Stopwatch.Frequency));
        var extrusionFrame = new RenderTargetBitmap((int)layer.ActualWidth, (int)layer.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        extrusionFrame.Render(layer);
        Check(HasInkNear(extrusionFrame, canvas.Screen(new(60, 58))) && !HasInkNear(extrusionFrame, canvas.Screen(new(0, 64))),
            "The extrusion pulse lights the new surface and leaves the existing hole clear");
        layer.DrawFrame(Stopwatch.GetTimestamp() + (long)(.35 * Stopwatch.Frequency));
        int passes = layer.RenderPasses;
        Wait(55);
        Check(!layer.IsAnimating && layer.RenderPasses == passes, "Extrusion feedback stops rendering after the pulse");
        beforeExtrusion.Progression = window.Document.Progression;
        window.Undo();
        Check(window.Document.Serialize() == beforeExtrusion.Serialize() && window.Progress.CreationXp == 1,
            "Undo restores the extrusion in one step and retains the earned XP");
        StartExtrusion(new(0, 0), new(0, 128));
        canvas.CancelGesture();
        Check(!Active(FeedbackCue.Extrusion) && window.Document.Serialize() == beforeExtrusion.Serialize(), "Canceling extrusion creates neither a face nor a pulse");

        extrusion = ExtrusionFixture();
        StartExtrusion(new(0, 0), new(64, 0));
        canvas.PointerUp(MouseButton.Left);
        Check(!Active(FeedbackCue.Extrusion) && extrusion.Faces().Count == 0 && extrusion.Edges.Count > 1,
            "Extending an edge without enclosing an area produces no construction pulse");

        extrusion = new Plan();
        RectPath(extrusion, -128, -64, 128, 64);
        string originalFace = extrusion.Faces().Single().Key;
        Load(extrusion, EditorTool.Select);
        StartExtrusion(new(0, -64), new(64, -64));
        canvas.PointerUp(MouseButton.Left);
        Check(!Active(FeedbackCue.Extrusion) && extrusion.Faces().Single().Key != originalFace,
            "Splitting an unchanged closed boundary does not count as creating a face");

        extrusion = ExtrusionFixture();
        Click(new(0, 0));
        InterfaceLayoutTests.Layout(window);
        var offset = InterfaceLayoutTests.Children<TextBox>(window).Single(box => AutomationProperties.GetName(box) == "Offset Y");
        offset.Text = "128";
        var extrudeButton = InterfaceLayoutTests.Children<Button>(window).Single(button => AutomationProperties.GetName(button) == "Extrude by offset");
        extrudeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Active(FeedbackCue.Extrusion) && extrusion.Faces().Count == 1, "The inspector offset button triggers the same construction effect");
        Wait(420);
        Check(!layer.IsAnimating, "The extrusion pulse expires without requiring another interaction");

        window.Shortcuts.ConstructionFeedback = false;
        extrusion = ExtrusionFixture();
        StartExtrusion(new(0, 0), new(0, 128));
        canvas.PointerUp(MouseButton.Left);
        Check(!Active(FeedbackCue.Extrusion) && extrusion.Faces().Count == 1, "Disabling construction feedback suppresses the pulse without blocking extrusion");
        window.Shortcuts.ConstructionFeedback = true;

        Load(new Plan(), EditorTool.Draw);
        Click(new(-96, -64));
        Check(Active(FeedbackCue.DrawPoint) && !Active(FeedbackCue.DrawClosed), "The first snapped point only triggers a point halo");
        Click(new(96, -64)); Click(new(96, 64)); Click(new(-96, 64));
        Check(!Active(FeedbackCue.DrawClosed), "An open path has no construction pulse");
        Click(new(-96, -64));
        Check(Active(FeedbackCue.DrawClosed) && window.Document.Faces().Count == 1 && window.Document.Fills.Count == 0,
            "Closing Draw creates an impulse for the unpainted enclosed area");
        var full = (FrameworkElement)window.Content;
        Wait(65);
        MainWindow.SaveVisual(full, Path.Combine(output, "coat-construction-pulse.png"));
        var during = Path.Combine(output, "export-during.png");
        var after = Path.Combine(output, "export-after.png");
        window.ExportPngTo(during);
        layer.Clear();
        window.ExportPngTo(after);
        Check(File.ReadAllBytes(during).SequenceEqual(File.ReadAllBytes(after)), "Canvas PNG exports are identical with or without the feedback layer");

        foreach (var (shape, cue) in new[] { (ShapeKind.Rectangle, FeedbackCue.Rectangle), (ShapeKind.Circle, FeedbackCue.Circle), (ShapeKind.Stairs, FeedbackCue.Stairs) })
        {
            var fixture = new Plan();
            if (shape == ShapeKind.Rectangle) RectPath(fixture, -32, -32, 32, 32);
            Load(fixture, EditorTool.Shapes);
            canvas.Shape = shape; canvas.Stair = StairKind.L;
            Click(new(-128, -96));
            Check(!Active(cue), $"{shape} does not animate during placement");
            Click(new(128, 96));
            Check(Active(cue) && fixture.Faces().Count > 0, $"{shape} queues its construction effect after completion");
            if (shape == ShapeKind.Rectangle)
            {
                layer.DrawFrame(Stopwatch.GetTimestamp() + (long)(.17 * Stopwatch.Frequency));
                var frame = new RenderTargetBitmap((int)layer.ActualWidth, (int)layer.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                frame.Render(layer);
                bool surface = HasInkNear(frame, canvas.Screen(new(-64, 85)));
                bool hole = HasInkNear(frame, canvas.Screen(new(0, 0)));
                MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-rectangle-pulse.png"));
                Check(surface && !hole,
                    "A rectangle construction sweep lights its surface and preserves the existing hole");
            }
        }

        Load(Plan.Parse(serial), EditorTool.Select);
        canvas.Mode = SelectionMode.Face; Click(new(-90, 0));
        canvas.ToggleSelectionFocus(); Click(new(0, 0));
        Check(canvas.IsSelectionFocused && !layer.ActiveCues.Any(), "Focus clears old effects and clicking the hidden interior starts no new cue");
        Click(new(-90, 0)); window.ChangeLevel(1);
        Check(!layer.IsAnimating, "Changing levels stops visual feedback from the previous view");

        window.Shortcuts.SelectionFeedback = false;
        Load(Plan.Parse(serial), EditorTool.Select); canvas.Mode = SelectionMode.Vertex;
        Click(new(-128, -96));
        Check(!layer.IsAnimating, "Disabling selection suppresses the halo");
        canvas.Mode = SelectionMode.Edge; Click(new(-80, -96));
        canvas.Mode = SelectionMode.Face; Click(new(-90, 0));
        Check(!layer.IsAnimating, "Disabling selection also suppresses edge and face feedback");
        window.SetTool(EditorTool.Draw); Click(new(256, 96));
        Check(Active(FeedbackCue.DrawPoint), "Selection can be disabled while construction remains active");
        window.Shortcuts.SelectionFeedback = true; window.Shortcuts.ConstructionFeedback = false;
        Load(Plan.Parse(serial), EditorTool.Draw); Click(new(256, 96));
        Check(!layer.IsAnimating, "Disabling construction suppresses drawing feedback");
        window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Vertex; Click(new(-128, -96));
        Check(Active(FeedbackCue.Vertex), "Construction can be disabled while selection remains active");
        layer.SetValue(InteractionFeedbackLayer.MotionAllowedProperty, false);
        Check(!layer.IsAnimating, "Reduced motion stops a running cue immediately");
        canvas.ClearSelection(); Click(new(-128, -96));
        Check(!layer.IsAnimating, "Reduced motion blocks new cues");

        var settingsPath = Path.Combine(output, "feedback-preferences.json");
        var dialog = window.CreatePreferencesWindow(settingsPath);
        dialog.Show(); InterfaceLayoutTests.Layout(dialog);
        var expander = InterfaceLayoutTests.Children<Expander>(dialog).Single(e => Equals(e.Header, "Interaction Feedback"));
        expander.IsExpanded = true; InterfaceLayoutTests.Layout(dialog);
        InterfaceLayoutTests.Children<CheckBox>(expander).Single(b => Equals(b.Content, "Selection effects")).IsChecked = false;
        InterfaceLayoutTests.Children<CheckBox>(expander).Single(b => Equals(b.Content, "Construction effects")).IsChecked = true;
        InterfaceLayoutTests.Children<Button>(dialog).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var settings = ShortcutSettings.Load(settingsPath);
        Check(!settings.SelectionFeedback && settings.ConstructionFeedback, "The preferences dialog saves each feedback switch independently");

        window.Close(); app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }

    private static bool HasInkNear(BitmapSource image, Point point)
    {
        var pixels = new byte[7 * 7 * 4];
        image.CopyPixels(new Int32Rect((int)point.X - 3, (int)point.Y - 3, 7, 7), pixels, 7 * 4, 0);
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 5) return true;
        return false;
    }

    private static void RectPath(Plan plan, double left, double bottom, double right, double top) =>
        plan.Polygon(new V2[] { new(left, bottom), new(right, bottom), new(right, top), new(left, top) });

    private static void Wait(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
