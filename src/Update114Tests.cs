// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Xml.Linq;
namespace Coat;
internal static class Update114Tests
{
    internal static void Run(MainWindow w,Action<bool,string> check,string output)
    {
        w.LoadDocument(new Plan(),"");w.SetTool(EditorTool.Token);var c=w.Canvas;w.UpdateLayout();Pump();
        var text=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Text");var tokens=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Tokens");
        check(tokens.TranslatePoint(new(),w).X>text.TranslatePoint(new(),w).X,"Tokens toolbar button is placed next to Text");
        check(Desc<Button>(w).Count(b=>b.Content is Image { Width:24 })==22,"Token palette exposes all 22 choices as images without letter labels");
        check(!Desc<Button>(w).Any(b=>Equals(b.Content,"Text")||Equals(b.Content,"Token")),"Text and Token mode switches are removed from the right panel");
        c.TokenShape=TokenKind.Skull;c.TokenSize=48;c.PaintColor="#FFBB00";
        void Click(V2 p){c.PointerDown(c.Screen(p),MouseButton.Left);c.PointerUp(MouseButton.Left);}
        Click(new());int id=w.Document.Annotations.Single().Id;
        check(w.Document.Annotations.Single() is {IsToken:true,TokenShape:TokenKind.Skull,Size:48,Color:"#FFBB00"},"Token tool places the configured shape, color and size");
        w.SetTool(EditorTool.Annotation);Click(new(192,192));check(w.Document.Annotations.Last().IsToken==false,"Text tool creates text independently of the token tool");
        w.SetTool(EditorTool.Token);c.SelectedAnnotation=id;w.UpdateInspector();
        Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Armor").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(w.Document.Annotations.Single(a=>a.Id==id).TokenShape==TokenKind.Armor&&c.TokenShape==TokenKind.Armor,"Selecting an icon updates the selected token and next placement shape");
        check(!Desc<TextBox>(w).Any(b=>b.AcceptsReturn),"Selected token inspector has no multiline text editor");
        w.Undo();check(w.Document.Annotations.Single(a=>a.Id==id).TokenShape==TokenKind.Skull,"Token shape changes are undoable");w.Redo();c.SelectedAnnotation=id;w.Duplicate();
        check(w.Document.Annotations.Last().TokenShape==TokenKind.Armor,"Token duplication retains its shape");
        check(w.CaptureSelection().Annotations.Single().TokenShape==TokenKind.Armor,"Clipboard capture retains token shape");
        var note=w.Document.Annotations.Single(a=>a.Id==id);var legacy=new Plan();legacy.Annotations.Add(new Annotation{Id=1,IsToken=true});legacy.NextId=2;
        check(Plan.Parse(legacy.Serialize().Replace("\"TokenShape\": 0,"," ")).Annotations[0].TokenShape==TokenKind.Circle,"Legacy tokens default to a circle when shape is absent");
        var gallery=new Plan();int index=0;string[] colors={"#78DCC1","#7EBBFF","#F0C470","#EC909F"};
        foreach(var kind in Enum.GetValues<TokenKind>())
        {
            var shape=TokenShapes.Shape(kind);check(!shape.Bounds.IsEmpty&&shape.Bounds.Width>0&&shape.Bounds.Height>0&&shape.Bounds.Left>=-1&&shape.Bounds.Top>=-1&&shape.Bounds.Right<=33&&shape.Bounds.Bottom<=33,kind+" has bounded vector artwork");
            if(kind>=TokenKind.Number0)
            {
                var symbol=XDocument.Parse("<svg>"+TokenShapes.Svg(new Annotation{IsToken=true,TokenShape=kind,Size=32})+"</svg>");var data=symbol.Descendants("path").Single().Attribute("d")!.Value;var bounds=Geometry.Parse(data).Bounds;
                check(bounds.Left>=-1&&bounds.Top>=-1&&bounds.Right<=33&&bounds.Bottom<=33,kind+" SVG bakes the digit transform into its outline");
            }
            gallery.Annotations.Add(new Annotation{Id=gallery.NextId++,IsToken=true,TokenShape=kind,X=index%6*96-240,Y=192-index/6*112,Size=56,Color=colors[index%4]});index++;
        }
        var parsed=Plan.Parse(gallery.Serialize());check(parsed.Annotations.Select(a=>a.TokenShape).SequenceEqual(Enum.GetValues<TokenKind>()),"All token shapes survive project save and reload");
        var xml=XDocument.Parse(MainWindow.Svg(gallery));check(xml.Descendants().Count(e=>e.Name.LocalName=="g"&&e.Attribute("data-token")!=null)==21&&xml.Descendants().Any(e=>e.Name.LocalName=="circle"),"SVG exports every symbolic token as vector paths and the legacy circle");
        check(!xml.Descendants().Any(e=>e.Name.LocalName=="text"),"Token SVG uses artwork instead of letter or font placeholders");
        w.LoadDocument(gallery,"");w.SetTool(EditorTool.Token);c.ShowGrid=false;c.Fit();w.UpdateInspector();w.UpdateLayout();Pump();
        MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-114-tokens.png"));w.ExportPngTo(Path.Combine(output,"coat-114-gallery.png"));File.WriteAllText(Path.Combine(output,"coat-114-gallery.svg"),MainWindow.Svg(gallery));
        w.Width=1050;w.Height=680;w.UpdateLayout();Pump();var toolbar=Desc<Button>(w).Single(b=>AutomationProperties.GetName(b)=="Tokens");check(toolbar.TranslatePoint(new(),w).X+toolbar.ActualWidth<w.ActualWidth,"Tokens toolbar button remains visible at minimum window size");MainWindow.SaveVisual((FrameworkElement)w.Content,Path.Combine(output,"coat-114-compact.png"));w.Width=1440;w.Height=900;
        check(new ShortcutSettings().Bindings["Jeton"]==ShortcutSettings.Unassigned,"Tokens tool has no new default shortcut");
        check(L.Missing.Count==0,"Token names and controls are fully localized");
    }
    private static IEnumerable<T> Desc<T>(DependencyObject p)where T:DependencyObject{if(p is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++)foreach(var c in Desc<T>(VisualTreeHelper.GetChild(p,i)))yield return c;}
    private static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false));Dispatcher.PushFrame(f);}
}
