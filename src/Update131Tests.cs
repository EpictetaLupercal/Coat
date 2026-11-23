// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Coat;

internal static class Update131Tests
{
    internal static void Run(MainWindow w, Action<bool,string> check, string output)
    {
        var canvas = w.Canvas;
        w.Shortcuts = new ShortcutSettings();
        w.ChangeLanguage("en");
        var plan = new Plan();
        plan.Polygon(new V2[] { new(160,0), new(224,0), new(224,64), new(160,64) });
        var text = new Annotation { Id=plan.NextId++, Text="Entrance", Size=24 };
        var token = new Annotation { Id=plan.NextId++, IsToken=true, TokenShape=TokenKind.Heart, X=-160, Size=32 };
        plan.Annotations.AddRange(new[] { text,token });
        w.LoadDocument(plan,"");
        canvas.Zoom = 1;
        canvas.Center = new V2();
        w.SetTool(EditorTool.Select);
        InterfaceLayoutTests.Layout(w);
        void KeyPress(Key key, UIElement? target = null) => (target ?? w).RaiseEvent(
            new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(w)!,0,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
        void Drag(V2 from,V2 to)
        {
            canvas.PointerDown(canvas.Screen(from), MouseButton.Left);
            canvas.PointerMove(canvas.Screen(to));
            canvas.PointerUp(MouseButton.Left);
        }
        foreach (var note in w.Document.Annotations)
        {
            canvas.SelectedAnnotation = note.Id;
            var anchor = note.Position;
            var from = anchor + (note.IsToken ? new V2(12,0) : new V2(12,-12));
            KeyPress(Key.S);
            Drag(from,anchor+(from-anchor)*2);
            check(canvas.Transform==TransformKind.Scale && Math.Abs(note.Size-(note.IsToken ? 64 : 48))<1e-7 && note.Position==anchor,
                "S scales the selected annotation around its anchor");
            KeyPress(Key.R);
            Drag(from,anchor+SelectionTransform.Rotate(from-anchor,new V2(),90));
            check(canvas.Transform==TransformKind.Rotation && note.Rotation==90 && canvas.SelectedAnnotation==note.Id,
                "R rotates the selected annotation without dropping selection");
            check(MainWindow.Svg(w.Document).Contains("rotate(-90 ") && Plan.Parse(w.Document.Serialize()).Annotations.Single(a=>a.Id==note.Id).Rotation==90,
                "Rotated annotations survive project loading and SVG export");
            KeyPress(Key.T);
            var start = anchor+SelectionTransform.Rotate(from-anchor,new V2(),90);
            Drag(start,start+new V2(64,64));
            check(canvas.Transform==TransformKind.Translation && note.Position==anchor+new V2(64,64),
                "T moves the annotation when picked in its rotated bounds");
        }
        w.Undo();
        check(w.Document.Annotations.Single(a=>a.IsToken).Position==new V2(-160,0),"Undo restores the original token position");
        canvas.ClearSelection();
        canvas.SelectedEdges.UnionWith(w.Document.Edges.Select(e=>e.Id));
        KeyPress(Key.S); check(canvas.Transform==TransformKind.Scale && canvas.ScaleHandles().Count==8,"S enables the geometry scale gizmo");
        KeyPress(Key.R); check(canvas.Transform==TransformKind.Rotation,"R enables geometry rotation");
        KeyPress(Key.T); check(canvas.Transform==TransformKind.Translation,"T enables geometry translation");

        var splitter = InterfaceLayoutTests.Children<GridSplitter>(w).Single();
        var body = (Grid)splitter.Parent;
        var originalWidth = body.ColumnDefinitions[2].ActualWidth;
        splitter.RaiseEvent(new DragStartedEventArgs(0,0) { RoutedEvent=Thumb.DragStartedEvent });
        splitter.RaiseEvent(new DragDeltaEventArgs(-80,0) { RoutedEvent=Thumb.DragDeltaEvent });
        splitter.RaiseEvent(new DragCompletedEventArgs(-80,0,false) { RoutedEvent=Thumb.DragCompletedEvent });
        InterfaceLayoutTests.Layout(w);
        check(body.ColumnDefinitions[2].ActualWidth >= originalWidth+79,"Dragging the divider left widens the inspector");
        check(InterfaceLayoutTests.Children<LevelPreview>(w).Single().ActualHeight>=66,"Level preview keeps its taller layout");
        var sidebar = (Border)body.Children.Cast<UIElement>().Single(e=>Grid.GetColumn(e)==2);
        var buttons = InterfaceLayoutTests.Children<Button>(sidebar).ToList();
        check(buttons.Any(b=>AutomationProperties.GetName(b)==L.T("Déplacer la sélection")) &&
            buttons.All(b=>b.Content is not string text || text.Length==1),"Inspector commands display icons and retain accessible names");
        MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-131-inspector.png"));
        check(L.Missing.Count==0,"Inspector resizing and toolbar changes remain localized");
    }
}
