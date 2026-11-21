// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
namespace Coat;
internal static class Update116Tests
{
    internal static void Run(MainWindow w,Action<bool,string> check,string output)
    {
        var p=new Plan();var note=new Annotation{Id=p.NextId++,Text="Original",X=0,Y=0};p.Annotations.Add(note);w.LoadDocument(p,"");w.SetTool(EditorTool.Annotation);var c=w.Canvas;c.SelectedAnnotation=note.Id;w.UpdateInspector();w.UpdateLayout();Pump();
        var input=Desc<TextBox>(w).Single(t=>AutomationProperties.GetName(t)=="Annotation text");input.Text="FirstSecond";input.CaretIndex=5;
        check(MainWindow.HandleAnnotationKey(input,Key.Enter,ModifierKeys.Shift,()=>throw new Exception("Shift+Enter applied text"))&&input.Text.Replace("\r","")=="First\nSecond"&&note.Text=="Original","Shift+Enter inserts a line break at the caret without applying text");
        var enter=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};input.RaiseEvent(enter);
        check(enter.Handled&&note.Text.Replace("\r","")=="First\nSecond","Enter in the annotation editor confirms its current text");w.Undo();check(w.Document.Annotations.Single().Text=="Original","Text confirmation is undoable in one step");
        input=new TextBox{Text="abcd",MaxLength=4,CaretIndex=2};MainWindow.HandleAnnotationKey(input,Key.Enter,ModifierKeys.Shift,()=>{});check(input.Text=="abcd","Shift+Enter respects the text length limit");
        Plan Fixture()
        {
            var plan=new Plan();plan.Polygon(new V2[]{new(-64,-64),new(64,-64),new(64,64),new(-64,64)});
            plan.Annotations.Add(new Annotation{Id=plan.NextId++,Text="Objective",X=-64,Y=160,Size=18});plan.Annotations.Add(new Annotation{Id=plan.NextId++,IsToken=true,TokenShape=TokenKind.Skull,X=160,Y=0,Size=32,Color="#FFCC00"});
            plan.Annotations.Add(new Annotation{Id=plan.NextId++,IsToken=true,TokenShape=TokenKind.Heart,X=400,Y=0,Size=32});return plan;
        }
        void Load(Plan plan){w.LoadDocument(plan,"");w.SetTool(EditorTool.Select);c.Mode=SelectionMode.Edge;c.Transform=TransformKind.Translation;c.Zoom=1;c.Center=new(128,64);c.ShowLowerLevels=true;c.ShowUpperLevels=false;c.SelectAcrossLevels=false;}
        void Box(V2 a,V2 b,bool ctrl=false){c.PointerDown(c.Screen(a),MouseButton.Left,ctrl:ctrl);c.PointerMove(c.Screen(b));c.PointerUp(MouseButton.Left);}
        Load(Fixture());Box(new(-128,224),new(224,-128));
        check(c.SelectedEdges.Count==4&&c.SelectedAnnotations.Count==2&&c.SelectedAnnotation==null,"Selection rectangle includes geometry, a text annotation and a token together");
        check(!c.SelectedAnnotations.Contains(w.Document.Annotations.Last().Id),"Annotations outside the selection rectangle stay unselected");
        var selectedNotes=w.Document.Annotations.Where(a=>c.SelectedAnnotations.Contains(a.Id)).ToDictionary(a=>a.Id,a=>new V2(a.X,a.Y));var before=w.Document.Serialize();
        c.PointerDown(c.Screen(new(160,0)),MouseButton.Left);c.PointerMove(c.Screen(new(192,-32)));c.PointerUp(MouseButton.Left);
        check(w.Document.Annotations.Where(a=>selectedNotes.ContainsKey(a.Id)).All(a=>new V2(a.X,a.Y)==selectedNotes[a.Id]+new V2(32,-32))&&w.Document.Vertices.Min(v=>v.X)==-32&&c.SelectedAnnotations.Count==2,"Dragging a selected token moves the whole mixed selection and preserves it");w.Undo();check(w.Document.Serialize()==before,"Mixed-selection drag undoes geometry and annotations together");
        Box(new(-128,224),new(224,-128));w.MoveSelection(64,32);
        check(w.Document.Annotations.Where(a=>selectedNotes.ContainsKey(a.Id)).All(a=>new V2(a.X,a.Y)==selectedNotes[a.Id]+new V2(64,32))&&w.Document.Vertices.Min(v=>v.X)==0,"Numeric Move applies to both geometry and selected annotations");w.Undo();Box(new(-128,224),new(224,-128));
        var fragment=w.CaptureSelection();check(fragment.Edges.Count==4&&fragment.Annotations.Count==2&&fragment.Annotations.Any(a=>a.IsToken&&a.TokenShape==TokenKind.Skull),"Clipboard capture includes all selected text, tokens and geometry");
        var previousClipboard=w.PlanClipboard;w.PlanClipboard=new MemoryClipboard();check(w.CopySelection(),"Mixed selection can be copied to the clipboard");
        w.LoadDocument(new Plan(),"");w.PasteSelection();check(w.Document.Edges.Count==4&&w.Document.Annotations.Count==2&&c.SelectedAnnotations.Count==2,"Paste restores all items of a mixed selection");
        w.CutSelection();check(w.Document.Edges.Count==0&&w.Document.Annotations.Count==0,"Cut removes all selected geometry, text and tokens");w.Undo();check(w.Document.Edges.Count==4&&w.Document.Annotations.Count==2,"Undo restores a cut mixed selection");
        w.PlanClipboard=previousClipboard;Load(Fixture());Box(new(-128,224),new(224,-128));
        int notes=w.Document.Annotations.Count,edges=w.Document.Edges.Count;w.Duplicate();check(w.Document.Annotations.Count==notes+2&&w.Document.Edges.Count>edges&&c.SelectedAnnotations.Count==2,"Duplicate copies the complete mixed selection");w.Undo();
        Box(new(-128,224),new(224,-128));c.DeleteSelection();check(w.Document.Edges.Count==0&&w.Document.Annotations.Count==1,"Delete removes every item in a mixed selection");w.Undo();check(w.Document.Serialize()==before,"Delete mixed selection is undone in one step");
        Box(new(-128,224),new(224,-128));w.AssignSelectionToLevel(2);check(w.Document.Annotations.Where(a=>selectedNotes.ContainsKey(a.Id)).All(a=>a.Level==2)&&w.Document.Vertices.All(v=>v.Level==2)&&c.SelectedAnnotations.Count==2,"Assign Level transfers text, tokens and geometry together");w.Undo();
        Load(Fixture());foreach(var mode in Enum.GetValues<SelectionMode>()){c.ClearSelection();c.Mode=mode;Box(new(-128,224),new(224,-128));check(c.SelectedAnnotations.Count==2,"Marquee includes annotations in "+mode+" mode");}
        c.ClearSelection();Box(new(128,32),new(160,-32));check(c.SelectedAnnotations.Count==0,"A partially enclosed token is not selected");
        Box(new(128,32),new(192,-32));check(c.SelectedAnnotations.Count==1&&c.SelectionPoints().Count==0,"Rectangle can select a token without geometry");Box(new(-96,192),new(96,96),ctrl:true);check(c.SelectedAnnotations.Count==2,"Ctrl-drag adds another annotation to the selection");
        c.PointerDown(c.Screen(new(160,0)),MouseButton.Left,ctrl:true);c.PointerUp(MouseButton.Left);check(c.SelectedAnnotations.Count==1,"Ctrl-click removes a selected token from a multi-selection");
        var layered=Fixture();layered.EnsureLevel(1);layered.ActiveLevel=1;Load(layered);Box(new(-128,224),new(224,-128));check(c.SelectedAnnotations.Count==0,"Annotations on another level are excluded when cross-level selection is disabled");
        c.SelectAcrossLevels=true;Box(new(-128,224),new(224,-128));check(c.SelectedAnnotations.Count==2,"Visible lower-level annotations participate in cross-level marquee selection");c.ShowLowerLevels=false;c.ClearSelection();Box(new(-128,224),new(224,-128));check(c.SelectedAnnotations.Count==0,"Hidden annotations cannot be marquee-selected");
        Load(Fixture());c.SelectAll();check(c.SelectedAnnotations.Count==3,"Select All includes all visible texts and tokens");w.Escape();check(c.SelectedAnnotations.Count==0&&!c.HasSelectionOrGesture,"Escape clears every annotation from the selection");
        Box(new(-128,224),new(224,-128));c.Fit();w.UpdateInspector();w.UpdateLayout();Pump();MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-116-mixed-selection.png"));
        check(L.Missing.Count==0,"Text validation and multi-annotation selection are fully localized");
    }
    private sealed class MemoryClipboard:IPlanClipboard { private string? value;public void Write(string data)=>value=data;public string? Read()=>value; }
    private static IEnumerable<T> Desc<T>(DependencyObject p)where T:DependencyObject{if(p is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i)))yield return c;}
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}
