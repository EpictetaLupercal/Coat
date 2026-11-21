// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace Coat;
internal static class ColorUi
{
    internal static ImageSource ButtonIcon(string hex)
    {
        var drawing=new DrawingGroup();using(var dc=drawing.Open())
        {
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,64,30));var center=new Point(15,15);
            for(int i=0;i<12;i++)
            {
                double a=i*Math.PI/6,b=(i+1)*Math.PI/6;var shape=new StreamGeometry();using(var c=shape.Open()){c.BeginFigure(center,true,true);c.LineTo(new Point(15+13*Math.Cos(a),15+13*Math.Sin(a)),true,false);c.ArcTo(new Point(15+13*Math.Cos(b),15+13*Math.Sin(b)),new Size(13,13),0,false,SweepDirection.Clockwise,true,false);}dc.DrawGeometry(new SolidColorBrush(ColorMath.FromHsv(i*30,1,1)),null,shape);
            }
            dc.DrawEllipse(EditorCanvas.Brush(hex),new Pen(Brushes.White,1),center,7,7);
            dc.DrawRoundedRectangle(EditorCanvas.Brush(hex),new Pen(EditorCanvas.Brush("#A9B9CC"),1),new Rect(35,3,26,24),3,3);
        }drawing.Freeze();return new DrawingImage(drawing);
    }
    internal static ImageSource EyedropperIcon()
    {
        var drawing=new DrawingGroup();using(var dc=drawing.Open())
        { dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,24,24));dc.DrawGeometry(null,new Pen(EditorCanvas.Brush("#C5EEE3"),2),Geometry.Parse("M 5,17 L 15,7 18,10 8,20 3,21 Z M 12,5 L 20,13 M 15,6 L 18,3 Q 20,1 22,3 Q 24,5 21,8 L 19,10")); }drawing.Freeze();return new DrawingImage(drawing);
    }
}
public partial class EditorCanvas
{
    internal void InvalidateColors(){cachedScene=null;navigationBrush=null;InvalidateVisual();}
}
public partial class MainWindow
{
    private Popup? colorPopup;
    internal Popup? ColorPopup => colorPopup;
    internal ColorPicker? ActiveColorPicker { get; private set; }
    private Plan? colorDocument;
    private string? colorBefore;
    private bool suspendColorPopup,closingColorPopup,refreshColorPopup=true;
    private Button ColorButton(Func<string> current,Action<string> apply,string label="◉  Roue de couleurs…")
    {
        Button? button=null;
        void Refresh(string value){button!.Content=new Image {Source=ColorUi.ButtonIcon(value),Width=64,Height=30};button.ToolTip=L.T(label)+" · "+value;}
        void Open(){ShowColorPopup(new ColorPicker(this,current()),button!,hex=>{apply(hex);Refresh(hex);});}
        button=Button("",Open);button.HorizontalAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(6,3,6,3);Refresh(current());
        System.Windows.Automation.AutomationProperties.SetName(button,L.T(label));RegisterFeature(L.T(label),Open);return button;
    }
    internal void ShowColorPopup(ColorPicker picker,FrameworkElement anchor,Action<string>? apply=null)
    {
        CloseColorPicker(false);Canvas.Anchor=null;
        ActiveColorPicker=picker;colorDocument=Document;colorBefore=Document.Serialize();
        var popup=new Popup {PlacementTarget=anchor,Placement=PlacementMode.Left,HorizontalOffset=-8,AllowsTransparency=true,StaysOpen=false};
        colorPopup=popup;popup.Child=new ScrollViewer {Content=picker,MaxHeight=Math.Max(250,Math.Min(570,ActualHeight-64)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        string last=picker.SelectedHex;
        picker.Changed+=hex=>{if(ActiveColorPicker!=picker||hex==last)return;last=hex;apply?.Invoke(hex);Canvas.InvalidateColors();};
        popup.Closed+=(_,_)=>{if(!popup.IsOpen&&!suspendColorPopup&&ReferenceEquals(colorPopup,popup))FinishColorPopup();};popup.IsOpen=true;picker.Wheel.Focus();
    }
    internal void CloseColorPicker(bool refresh=true)
    {
        if(colorPopup==null)return;
        refreshColorPopup=refresh;closingColorPopup=true;
        try {if(Canvas.IsColorSampling)Canvas.CancelColorSampling();suspendColorPopup=false;if(colorPopup?.IsOpen==true)colorPopup.IsOpen=false;FinishColorPopup();}
        finally {closingColorPopup=false;}
    }
    private void FinishColorPopup()
    {
        var popup=colorPopup;if(popup==null)return;
        bool refresh=refreshColorPopup;refreshColorPopup=true;
        var before=colorBefore;var document=colorDocument;
        colorPopup=null;ActiveColorPicker=null;colorBefore=null;colorDocument=null;popup.Child=null;
        if(before!=null&&document==Document&&before!=Document.Serialize())History.Push(before);
        if(refresh){UpdateInspector();UpdateStatus();}else UpdateTitle();
    }
    internal void BeginColorSampling(ColorPicker picker)
    {
        if(ActiveColorPicker!=picker||colorPopup==null)return;
        try
        {
            var frame=ColorSampling.FromVisual(Canvas);suspendColorPopup=true;colorPopup.IsOpen=false;
            Canvas.BeginColorSampling(frame,color=>
            {
                if(closingColorPopup||ActiveColorPicker!=picker)return;
                if(color.HasValue)picker.SetColor(color.Value);
                suspendColorPopup=false;if(colorPopup!=null){colorPopup.IsOpen=true;picker.Wheel.Focus();}UpdateStatus();
            });
        }
        catch(Exception ex){suspendColorPopup=false;Notify(L.T("Prélèvement impossible : ")+ex.Message);if(colorPopup!=null)colorPopup.IsOpen=true;}
    }
    private void ChooseColor(string initial,Action<string> apply)=>ShowColorPopup(new ColorPicker(this,initial),Canvas,apply);
}
