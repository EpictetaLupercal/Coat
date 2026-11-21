// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace Coat;
public enum TokenKind { Circle, Square, Triangle, Cross, Check, Skull, Star, Heart, Armor, Explosion, Ammo, Rocket, Number0, Number1, Number2, Number3, Number4, Number5, Number6, Number7, Number8, Number9 }
internal static class TokenShapes
{
    private static readonly Dictionary<TokenKind,Geometry> shapes = new();
    internal static string Name(TokenKind kind) => kind switch {
        TokenKind.Circle=>L.T("Rond"),TokenKind.Square=>L.T("Carré"),TokenKind.Triangle=>L.T("Triangle"),TokenKind.Cross=>L.T("Croix"),TokenKind.Check=>L.T("Valider"),TokenKind.Skull=>L.T("Crâne"),TokenKind.Star=>L.T("Étoile"),TokenKind.Heart=>L.T("Cœur"),TokenKind.Armor=>L.T("Armure"),TokenKind.Explosion=>L.T("Explosion"),TokenKind.Ammo=>L.T("Munitions"),TokenKind.Rocket=>L.T("Fusée"),_=>((int)kind-(int)TokenKind.Number0).ToString(CultureInfo.InvariantCulture) };
    internal static Geometry Shape(TokenKind kind)
    {
        if(shapes.TryGetValue(kind,out var cached))return cached;
        string path=kind switch {
            TokenKind.Circle=>"M 16,0 A 16,16 0 1 1 16,32 A 16,16 0 1 1 16,0 Z",
            TokenKind.Square=>"M 3,3 L 29,3 29,29 3,29 Z",
            TokenKind.Triangle=>"M 16,2 L 31,29 1,29 Z",
            TokenKind.Cross=>"M 4,1 L 16,13 28,1 31,4 19,16 31,28 28,31 16,19 4,31 1,28 13,16 1,4 Z",
            TokenKind.Check=>"M 1,17 L 7,11 13,17 25,3 31,8 13,30 Z",
            TokenKind.Skull=>"M 7,23 C -2,17 2,2 16,2 C 30,2 34,17 25,23 L 25,29 7,29 Z M 10,10 A 4,5 0 1 0 10,20 A 4,5 0 1 0 10,10 Z M 22,10 A 4,5 0 1 0 22,20 A 4,5 0 1 0 22,10 Z M 16,19 L 13,23 19,23 Z M 11,25 L 13,25 13,29 11,29 Z M 19,25 L 21,25 21,29 19,29 Z",
            TokenKind.Star=>Star(5,14,6),
            TokenKind.Heart=>"M 16,30 C 11,25 1,19 1,10 C 1,0 12,-1 16,7 C 20,-1 31,0 31,10 C 31,19 21,25 16,30 Z",
            TokenKind.Armor=>"M 16,1 L 29,6 27,20 Q 25,27 16,31 Q 7,27 5,20 L 3,6 Z M 14,7 L 18,7 18,14 24,14 24,18 18,18 18,25 14,25 14,18 8,18 8,14 14,14 Z",
            TokenKind.Explosion=>"M 16,0 L 20,9 29,3 25,13 32,17 23,20 29,29 18,25 14,32 11,23 1,28 7,18 0,13 10,12 5,2 14,8 Z",
            TokenKind.Ammo=>"M 2,28 L 2,12 Q 2,7 6,3 Q 10,7 10,12 L 10,28 Z M 12,28 L 12,10 Q 12,5 16,1 Q 20,5 20,10 L 20,28 Z M 22,28 L 22,12 Q 22,7 26,3 Q 30,7 30,12 L 30,28 Z M 2,24 L 10,24 10,26 2,26 Z M 12,24 L 20,24 20,26 12,26 Z M 22,24 L 30,24 30,26 22,26 Z",
            TokenKind.Rocket=>"M 16,1 C 24,6 25,13 22,23 L 10,23 C 7,13 8,6 16,1 Z M 10,15 L 3,22 3,28 10,24 Z M 22,15 L 29,22 29,28 22,24 Z M 13,25 L 19,25 16,32 Z M 16,7 A 3,3 0 1 0 16,13 A 3,3 0 1 0 16,7 Z",
            _=>""
        };
        Geometry shape;
        if(kind>=TokenKind.Number0)
        {
            var text=new FormattedText(((int)kind-(int)TokenKind.Number0).ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.Bold,FontStretches.Normal),30,Brushes.White,1);
            var raw=text.BuildGeometry(new Point());var bounds=raw.Bounds;double scale=28/Math.Max(bounds.Width,bounds.Height);var group=new System.Windows.Media.GeometryGroup();group.Children.Add(raw);group.Transform=new MatrixTransform(scale,0,0,scale,16-(bounds.Left+bounds.Width/2)*scale,16-(bounds.Top+bounds.Height/2)*scale);shape=group;
        }
        else shape=Geometry.Parse("F0 "+path);
        shape.Freeze();shapes[kind]=shape;return shape;
    }
    private static string Star(int count,double outer,double inner)
    {
        var points=Enumerable.Range(0,count*2).Select(i=>{double a=-Math.PI/2+i*Math.PI/count,r=i%2==0?outer:inner;return new Point(16+Math.Cos(a)*r,16+Math.Sin(a)*r);});
        return "M "+string.Join(" L ",points.Select(p=>p.X.ToString("G17",CultureInfo.InvariantCulture)+","+p.Y.ToString("G17",CultureInfo.InvariantCulture)))+" Z";
    }
    internal static void Draw(DrawingContext dc,Annotation note,double zoom)
    {
        dc.PushTransform(new TranslateTransform(-note.Size/2,-note.Size/2));dc.PushTransform(new ScaleTransform(note.Size/32,note.Size/32));
        dc.DrawGeometry(EditorCanvas.Brush(note.Color),new Pen(Brushes.White,32/note.Size/zoom),Shape(note.TokenShape));dc.Pop();dc.Pop();
    }
    internal static ImageSource Icon(TokenKind kind)
    {
        var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));dc.DrawGeometry(EditorCanvas.Brush("#C5EEE3"),null,Shape(kind));}drawing.Freeze();return new DrawingImage(drawing);
    }
    internal static string Svg(Annotation note)
    {
        string F(double n)=>n.ToString("0.#########",CultureInfo.InvariantCulture);
        string color=System.Security.SecurityElement.Escape(note.Color)??"#FFFFFF";
        if(note.TokenShape==TokenKind.Circle)return $"<circle cx=\"{F(note.X)}\" cy=\"{F(-note.Y)}\" r=\"{F(note.Size/2)}\" fill=\"{color}\" stroke=\"white\"/>";
        var geometry=Shape(note.TokenShape).GetFlattenedPathGeometry(.01,ToleranceType.Absolute);
        string P(Point p) { p=geometry.Transform?.Transform(p) ?? p;return F(p.X)+","+F(p.Y); }
        string path=string.Join(" ",geometry.Figures.Select(figure=>"M "+P(figure.StartPoint)+" "+string.Join(" ",figure.Segments.Select(segment=>segment switch { PolyLineSegment line=>"L "+string.Join(" ",line.Points.Select(P)),LineSegment line=>"L "+P(line.Point),_=>throw new InvalidOperationException("Unflattened token geometry.") }))+(figure.IsClosed?" Z":"")));

        return $"<g data-token=\"{note.TokenShape}\" transform=\"translate({F(note.X-note.Size/2)} {F(-note.Y-note.Size/2)}) scale({F(note.Size/32)})\"><path d=\"{path}\" fill=\"{color}\" fill-rule=\"evenodd\" stroke=\"white\" stroke-width=\"{F(32/note.Size)}\"/></g>";
    }
}
