// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;

internal static partial class InterfaceLayoutTests
{
    internal static void Run(MainWindow window,Action<bool,string> check,string output)
    {
        window.Shortcuts.Theme="classic"; window.ChangeLanguage("en");
        Measure(window,output);
        window.SetTool(EditorTool.Draw); window.Canvas.Fit(); Layout(window);
        var decorations = Children<MenuDecoration>(window).ToArray();
        check(decorations.Length == 2 && decorations.All(d => d.IsVisible && !d.IsHitTestVisible && !d.Focusable),
            "The upper bars share one non-interactive decoration and the inspector has its own");
        var root = (Grid)window.Content;
        var topDecoration = decorations.Single(d => d.Surface == MenuSurface.Top);
        var inspectorDecoration = decorations.Single(d => d.Surface == MenuSurface.Inspector);
        check(Grid.GetRowSpan(topDecoration) == 2 && topDecoration.ActualWidth == root.ActualWidth &&
            root.Children.OfType<DockPanel>().Where(d => Grid.GetRow(d) < 2).All(d => root.Children.IndexOf(d) > root.Children.IndexOf(topDecoration)) &&
            inspectorDecoration.ActualHeight > 450 && inspectorDecoration.ActualWidth > 240 &&
            inspectorDecoration.Parent is Grid inspectorLayer && inspectorLayer.Children[0] == inspectorDecoration,
            "Decorations cover their full area and sit behind the controls at the minimum window size");
        var tiles = Children<Image>(inspectorDecoration).ToArray();
        check(tiles.Length == 2 && ReferenceEquals(tiles[0].Source, tiles[1].Source) && tiles[0].Source.IsFrozen &&
            tiles[0].Parent is Canvas { RenderTransform: TranslateTransform } && ReferenceEquals(tiles[0].Parent, tiles[1].Parent),
            "Inspector scrolling translates one strip that reuses a frozen bitmap for both tiles");
        check(decorations.All(d => d.IsAnimating == SystemParameters.ClientAreaAnimation),
            "Menu decoration motion follows the Windows animation setting");
        foreach (var decoration in decorations) decoration.SetValue(MenuDecoration.MotionAllowedProperty, false);
        check(decorations.All(d => !d.IsAnimating && d.Offset == 0 && d.IsVisible),
            "Reduced motion holds the menu circuits and inspector facets still");
        MainWindow.SaveVisual((FrameworkElement)window.Content,Path.Combine(output,"coat-menu-classic.png"));
        string project=window.Document.Serialize();
        string map=MapExporter.Export(window.Document).Text, svg=MainWindow.Svg(window.Document);
        string ImagePath(string name)=>Path.Combine(output,name+".png");
        window.ExportPngTo(ImagePath("classic-plan"));
        var path=Path.Combine(output,"appearance-preferences.json");
        var preferences=window.CreatePreferencesWindow(path);
        preferences.Show(); Layout(preferences);
        Children<ComboBox>(preferences).Single(c=>AutomationProperties.GetName(c)=="Theme").SelectedIndex=1;
        Layout(preferences);
        check(window.IsBordeaux && ShortcutSettings.Load(path).Theme=="bordeaux","Choosing Bordeaux applies immediately and persists in preferences");
        check(preferences.Background==window.MenuBack,"Preferences preview the selected palette");
        preferences.Close(); Layout(window);
        var bordeauxTile = Children<Image>(Children<MenuDecoration>(window).Single(d => d.Surface == MenuSurface.Inspector)).First().Source;
        check(!ReferenceEquals(bordeauxTile, tiles[0].Source),"Switching to Bordeaux picks the inspector tile for that theme");
        window.ExportPngTo(ImagePath("bordeaux-plan"));
        check(File.ReadAllBytes(ImagePath("classic-plan")).SequenceEqual(File.ReadAllBytes(ImagePath("bordeaux-plan"))) &&
            MapExporter.Export(window.Document).Text==map && MainWindow.Svg(window.Document)==svg && window.Document.Serialize()==project,
            "Switching themes preserves project data and produces identical PNG, SVG and .map exports");
        foreach(var language in new[] {"en","fr"})
        {
            window.ChangeLanguage(language); window.SetTool(EditorTool.Draw); Layout(window);
            var controls=Children<Button>(window).Where(b=>b.Content is System.Windows.Shapes.Path).ToList();
            var toolbar=controls.Where(b=>b.TranslatePoint(new Point(),window).Y<100).ToList();
            check(toolbar.Count==10 && toolbar.All(b=>b.ActualWidth>=30 && b.TranslatePoint(new Point(b.ActualWidth,0),window).X<=window.ActualWidth &&
                AutomationProperties.GetName(b).Length>0 && AutomationProperties.GetHelpText(b).Length>0),language+" toolbar icons fit at minimum size and expose name and help");
            var dimension=toolbar.Single(b=>AutomationProperties.GetName(b)==L.T("Cotation"));
            dimension.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.Canvas.Tool==EditorTool.Dimension && dimension.Background!=toolbar.First(b=>b!=dimension).Background,"The selected tool keeps a distinct background");
        }
        window.ChangeLanguage("en"); window.SetTool(EditorTool.Paint); Layout(window);
        var advanced=Children<Expander>(window).Single(e=>Equals(e.Header,"MAP EXPORT"));
        advanced.IsExpanded=false; window.SetTool(EditorTool.Select); window.SetTool(EditorTool.Paint); Layout(window);
        check(!Children<Expander>(window).Single(e=>Equals(e.Header,"MAP EXPORT")).IsExpanded,"Secondary section stays collapsed after switching tools");

        window.Shortcuts.Bindings["Enregistrer"] = new(Key.F6, ModifierKeys.Control);
        var edgeButton = Children<Button>(window).Single(b=>AutomationProperties.GetName(b)=="Save");
        var source = PresentationSource.FromVisual(window)!;
        void Shift(bool pressed) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.LeftShift) {
            RoutedEvent=pressed ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent });
        void AwaitHint(bool hover=true)
        {
            if(hover) edgeButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0) { RoutedEvent=Mouse.MouseEnterEvent });
            var frame=new DispatcherFrame();
            var delay=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(600) };
            delay.Tick += (_,_)=>{ delay.Stop(); frame.Continue=false; };
            delay.Start(); Dispatcher.PushFrame(frame); Layout(window);
        }
        Border Hint(string title = "Save (Ctrl+F6)") => Children<Border>(window).Single(b => b.Child is StackPanel panel &&
            panel.Children.Count==2 && panel.Children[0] is TextBlock t && t.Text==title && panel.Children[1] is TextBlock);
        var movingDecoration = Children<MenuDecoration>(window).First();
        inspectorDecoration = Children<MenuDecoration>(window).Single(d => d.Surface == MenuSurface.Inspector);
        var offset = movingDecoration.Offset;
        var inspectorOffset = inspectorDecoration.Offset;
        AwaitHint();
        check(!SystemParameters.ClientAreaAnimation || (Math.Abs(movingDecoration.Offset - offset) > .1 &&
            inspectorDecoration.Offset < inspectorOffset - .001),
            "The menu chevrons drift and the background facets scroll up while the window is visible");
        var tip=Hint(); var detail=(TextBlock)((StackPanel)tip.Child).Children[1];
        check(tip.IsVisible && !detail.IsVisible && !tip.IsHitTestVisible,"Hover shows the command name and its current custom shortcut in parentheses");
        Shift(true); Layout(window);
        check(detail.IsVisible && detail.Text=="Save the current plan." && Hint()==tip,"Shift expands the visible hint in place");
        Shift(false); Layout(window); check(!detail.IsVisible,"Releasing Shift collapses the description");
        edgeButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0) { RoutedEvent=Mouse.MouseLeaveEvent });
        check(!tip.IsVisible,"Leaving the button dismisses the hint");
        Shift(true); AwaitHint(); check(detail.IsVisible,"Holding Shift before hover opens the expanded hint");
        var layer=(FrameworkElement)VisualTreeHelper.GetParent(tip);
        var bounds=tip.TransformToAncestor(layer).TransformBounds(new Rect(tip.RenderSize));
        check(bounds.Left>=0 && bounds.Right<=layer.ActualWidth+1 && bounds.Bottom<=layer.ActualHeight+1,"Expanded hint stays inside the client area");
        edgeButton.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
        check(!tip.IsVisible,"Pressing a button dismisses the hint before the command runs"); Shift(false);
        window.Activate(); window.Canvas.Focus(); bool hasFocus=edgeButton.Focus(); AwaitHint(false); Shift(true); Layout(window);
        check(tip.IsVisible && detail.IsVisible && tip.TransformToAncestor(layer).TransformBounds(new Rect(tip.RenderSize)).Right<=layer.ActualWidth+1,
            $"Keyboard focus reveals the description at the window edge (focused={hasFocus}, visible={tip.IsVisible}, expanded={detail.IsVisible}, right={tip.TransformToAncestor(layer).TransformBounds(new Rect(tip.RenderSize)).Right}, width={layer.ActualWidth})"); Shift(false);
        window.Shortcuts.Bindings["Enregistrer"] = ShortcutSettings.Unassigned;
        AwaitHint();
        check(Hint("Save").IsVisible,"Unassigned buttons show the name alone");
        window.Shortcuts.Bindings["Shapes"] = new(Key.R, ModifierKeys.None);
        window.SetTool(EditorTool.Select);
        window.Canvas.SelectAll();
        check(window.ButtonShortcut("Rotation") == "R" && window.ButtonShortcut("Shapes") == "",
            "Hint displays the contextual transform shortcut when it supersedes a tool shortcut");
        var fileMenu = Children<Menu>(window).Single().Items.OfType<MenuItem>().First();
        fileMenu.ApplyTemplate();
        var dropdown = (System.Windows.Controls.Primitives.Popup)fileMenu.Template.FindName("PART_Popup",fileMenu);
        var command = (MenuItem)fileMenu.Items[0];
        check(dropdown.Child is Border { Background: SolidColorBrush paper } && paper.Color == Colors.White &&
            command.Foreground is SolidColorBrush ink && ink.Color == Colors.Black &&
            fileMenu.Foreground is SolidColorBrush label && label.Color != Colors.Black,
            "Bordeaux keeps its themed header and uses a white dropdown with black text");
        window.Canvas.ClearSelection(); window.Width=1280; window.Height=800; window.SetTool(EditorTool.Draw); Layout(window); window.Canvas.Fit(); Layout(window);
        MainWindow.SaveVisual((FrameworkElement)window.Content,ImagePath("coat-interface"));
        preferences = window.CreatePreferencesWindow(path);
        preferences.Show(); Layout(preferences);
        var animatedMenus = Children<CheckBox>(preferences).Single(c => Equals(c.Content, "Animated menu decorations"));
        check(animatedMenus.IsChecked == true,"The menu decoration preference is enabled by default");
        animatedMenus.IsChecked = false;
        var savePreferences = Children<Button>(preferences).Single(b => b.IsDefault);
        savePreferences.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(window);
        check(!Children<MenuDecoration>(window).Any() && !ShortcutSettings.Load(path).AnimatedMenus,
            "Turning off menu decorations clears the shared upper backdrop and the inspector and saves the preference");
        MainWindow restarted=new() { TestMode=true, Shortcuts=ShortcutSettings.Load(path) };
        restarted.ChangeLanguage(restarted.Shortcuts.Language);
        check(restarted.IsBordeaux && restarted.Background==window.Background,"A new window restores the saved Bordeaux palette");
        restarted.Show(); Layout(restarted);
        check(!Children<MenuDecoration>(restarted).Any(),"A new window restores disabled menu decorations");
        restarted.Close();
        check(L.Missing.Count==0,"Appearance and toolbar text are localized");
    }
}
