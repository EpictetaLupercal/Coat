// Coat — Epicteta Lupercal
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
namespace Coat;
internal static class StrokeDecoration
{
    private static readonly Brush Ink = EditorCanvas.Brush("#DFE8F2");
    internal static void Head(DrawingContext dc, Point tip, Point other, double size, ArrowKind kind, Brush color)
    {
        var delta = other - tip; if (delta.Length < 1e-8) return; size = Math.Min(size, delta.Length * .45);
        if (kind == ArrowKind.Round) { delta.Normalize(); double radius = size * .45; dc.DrawEllipse(color, null, tip + delta * radius, radius, radius); return; }
        var points = ArrowGeometry.Points(new(tip.X,tip.Y),new(other.X,other.Y),size,kind); if (points.Count == 0) return;
        var shape = new StreamGeometry(); using(var path=shape.Open()) { path.BeginFigure(new(points[0].X,points[0].Y),kind!=ArrowKind.Open,kind!=ArrowKind.Open); path.PolyLineTo(points.Skip(1).Select(p=>new Point(p.X,p.Y)).ToArray(),true,false); }
        dc.DrawGeometry(kind==ArrowKind.Open?null:color,new Pen(color,1.8),shape);
    }
    internal static string Distance(V2 a,V2 b) => (b-a).Length.ToString("0.##",CultureInfo.InvariantCulture)+" u";
    internal static void Draw(DrawingContext dc,Point a,Point b,V2 worldA,V2 worldB,Edge edge,double zoom,double dpi)
    {
        var color=EditorCanvas.Brush(edge.Color); double size=edge.ArrowSize*zoom;
        if(edge.Dimension || edge.ArrowStart) Head(dc,a,b,size,edge.Dimension?ArrowKind.Triangle:edge.HeadStyle(true),color);
        if(edge.Dimension || edge.ArrowEnd) Head(dc,b,a,size,edge.Dimension?ArrowKind.Triangle:edge.HeadStyle(false),color);
        if(!edge.Dimension || (b-a).Length<1e-8) return;
        var d=b-a; d.Normalize(); var n=new Vector(-d.Y,d.X)*Math.Clamp(size*.6,4,16); var pen=new Pen(color,1.5);
        dc.DrawLine(pen,a-n,a+n); dc.DrawLine(pen,b-n,b+n);
        var text=new FormattedText(Distance(worldA,worldB),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),edge.DimensionFontSize,color,dpi);
        var mid=new Point((a.X+b.X)/2,(a.Y+b.Y)/2); var box=new Rect(mid.X-text.Width/2-5,mid.Y-text.Height/2-2,text.Width+10,text.Height+4);
        dc.DrawRoundedRectangle(EditorCanvas.Brush("#181F2A"),null,box,3,3); dc.DrawText(text,new Point(mid.X-text.Width/2,mid.Y-text.Height/2));
    }
    internal static ImageSource Icon(ArrowKind? kind,bool start)
    {
        var drawing=new DrawingGroup(); using(var dc=drawing.Open())
        { dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,68,20)); dc.DrawLine(new Pen(Ink,1.5),new Point(5,10),new Point(63,10)); if(kind.HasValue) Head(dc,start?new Point(5,10):new Point(63,10),start?new Point(63,10):new Point(5,10),15,kind.Value,Ink); }
        drawing.Freeze(); return new DrawingImage(drawing);
    }
    internal static DrawingGroup DimensionIcon()
    {
        var drawing=new DrawingGroup(); using(var dc=drawing.Open())
        { dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,100,24)); dc.DrawLine(new Pen(Ink,1.5),new Point(5,12),new Point(95,12)); Draw(dc,new(5,12),new(95,12),new(0,0),new(64,0),new Edge{Dimension=true},1,1); } return drawing;
    }
    internal static string Svg(V2 a,V2 b,Edge edge)
    {
        string F(double n)=>n.ToString("0.#########",CultureInfo.InvariantCulture);
        string E(string s)=>System.Security.SecurityElement.Escape(s)??"";
        string P(V2 p)=>F(p.X)+","+F(-p.Y);
        var s=new StringBuilder(); string color=E(edge.Color); double size=Math.Min(edge.ArrowSize,(b-a).Length*.45);
        if((b-a).Length<1e-8) return "";
        foreach(bool start in new[]{true,false})
        {
            if(!edge.Dimension && !(start?edge.ArrowStart:edge.ArrowEnd))continue;
            var tip=start?a:b; var other=start?b:a; var kind=edge.Dimension?ArrowKind.Triangle:edge.HeadStyle(start);
            if(kind==ArrowKind.Round)
            { double radius=size*.45; var center=tip+(other-tip)*(radius/(other-tip).Length); s.Append($"<circle cx=\"{F(center.X)}\" cy=\"{F(-center.Y)}\" r=\"{F(radius)}\" fill=\"{color}\"/>"); }
            else { var points=ArrowGeometry.Points(tip,other,size,kind); s.Append($"<{(kind==ArrowKind.Open?"polyline":"polygon")} points=\"{string.Join(" ",points.Select(P))}\" fill=\"{(kind==ArrowKind.Open?"none":color)}\" stroke=\"{color}\" stroke-width=\"2\"/>"); }
        }
        if(edge.Dimension)
        {
            var d=b-a; var n=new V2(-d.Y,d.X)*(Math.Clamp(edge.ArrowSize*.6,4,16)/d.Length);
            foreach(var p in new[]{a,b})s.Append($"<polyline points=\"{P(p-n)} {P(p+n)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            var mid=(a+b)*.5; string label=Distance(a,b); var layout = new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),edge.DimensionFontSize,Ink,1); double width=layout.Width+10, height=layout.Height+4;
            s.Append($"<g data-dimension=\"true\"><rect x=\"{F(mid.X-width/2)}\" y=\"{F(-mid.Y-height/2)}\" width=\"{F(width)}\" height=\"{F(height)}\" rx=\"3\" fill=\"#181F2A\"/><text x=\"{F(mid.X)}\" y=\"{F(-mid.Y)}\" text-anchor=\"middle\" dominant-baseline=\"central\" font-family=\"Segoe UI, sans-serif\" font-size=\"{F(edge.DimensionFontSize)}\" fill=\"{color}\">{label}</text></g>");
        }
        return s.ToString();
    }
}
