// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Coat;

public enum TransformKind { Translation, Rotation, Scale }
public static class SelectionTransform
{
    public static V2 Center(IEnumerable<V2> source)
    {
        var points = source.ToList(); return points.Count == 0 ? new() : new((points.Min(p => p.X) + points.Max(p => p.X)) / 2, (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2);
    }
    public static V2 Rotate(V2 point, V2 pivot, double degrees)
    {
        double angle = (degrees % 360) * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
        if (Math.Abs(c) < 1e-12) c = 0; if (Math.Abs(s) < 1e-12) s = 0;
        var p = point - pivot; return pivot + new V2(p.X * c - p.Y * s, p.X * s + p.Y * c);
    }
    public static V2 Scale(V2 point, V2 pivot, double x, double y) { var p = point - pivot; return pivot + new V2(p.X * x, p.Y * y); }
    public static bool Valid(V2 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && Math.Abs(p.X) <= 1e8 && Math.Abs(p.Y) <= 1e8;
}
public partial class EditorCanvas
{
    public TransformKind Transform { get; set; }
    private record AnnotationPose(V2 Position, double Size, double Rotation)
    {
        internal AnnotationPose(Annotation note) : this(note.Position, note.Size, note.Rotation) { }
    }
    private Dictionary<int,AnnotationPose> originalAnnotations = new();
    private V2 transformPivot;
    private double transformAmount;
    public V2 SelectionCenter => SelectionTransform.Center(SelectionPoints().Select(id => VertexAt(id).P).Concat(Plan.Annotations.Where(a => SelectedAnnotations.Contains(a.Id)).Select(a => new V2(a.X,a.Y))));
    public void SetTransform(TransformKind transform) { Transform = transform; CancelGesture(); InvalidateVisual(); Focus(); }
    private void BeginTransformDrag()
    {
        originalAnnotations = Plan.Annotations.Where(a => SelectedAnnotations.Contains(a.Id)).ToDictionary(a => a.Id,a => new AnnotationPose(a));
        scaleHandle = null; dragging = true; original = SelectionPoints().ToDictionary(id => id, id => VertexAt(id).P); transformPivot = SelectionTransform.Center(original.Values.Concat(originalAnnotations.Values.Select(a => a.Position))); transformAmount = Transform == TransformKind.Scale ? 1 : 0;
    }
    private void UpdateTransformDrag(bool shift)
    {
        if (Transform == TransformKind.Scale && scaleHandle != null) { UpdateScaleHandle(shift); return; }
        var initial = downWorld - transformPivot; var current = mouseWorld - transformPivot;
        Func<V2, V2> map;
        if (Transform == TransformKind.Rotation)
        {
            double degrees = initial.Length * Zoom < 10 ? mouseScreen.X - downScreen.X : Math.Atan2(V2.Cross(initial, current), V2.Dot(initial, current)) * 180 / Math.PI;
            double step = shift ? 15 : 1; transformAmount = Math.Round(degrees / step) * step;
            map = p => SelectionTransform.Rotate(p, transformPivot, transformAmount);
        }
        else
        {
            double scale = initial.Length * Zoom < 10 ? Math.Exp((mouseScreen.X - downScreen.X - mouseScreen.Y + downScreen.Y) / 140) : current.Length / initial.Length;
            transformAmount = Math.Clamp(shift ? Math.Round(scale * 10) / 10 : scale, .01, 1000);
            scaleFactorX = scaleFactorY = transformAmount;
            map = p => SelectionTransform.Scale(p, transformPivot, transformAmount, transformAmount);
        }
        var changes = original.ToDictionary(pair => pair.Key, pair => map(pair.Value));
        var noteChanges = originalAnnotations.ToDictionary(pair => pair.Key, pair => map(pair.Value.Position));
        if (!changes.Values.Concat(noteChanges.Values).All(SelectionTransform.Valid)) return;
        foreach (var (id,position) in changes) VertexAt(id).P = position;
        foreach (var note in Plan.Annotations.Where(a => noteChanges.ContainsKey(a.Id)))
        {
            var originalNote = originalAnnotations[note.Id];
            ApplyAnnotationTransform(note, noteChanges[note.Id], originalNote.Size, originalNote.Rotation,
                Transform == TransformKind.Scale ? transformAmount : 1,
                Transform == TransformKind.Rotation ? transformAmount : 0);
        }
    }
    internal static void ApplyAnnotationTransform(Annotation note,V2 position,double size,double rotation,double scale,double angle)
    {
        note.X = position.X;
        note.Y = position.Y;
        note.Size = Math.Clamp(size * scale,4,256);
        note.Rotation = (rotation + angle % 360) % 360;
    }
    public void NormalizeSelection()
    {
        var annotations = SelectedAnnotations.ToHashSet();
        bool individual = IndividualSelection, hadFaces = SelectedFaces.Count > 0;
        var track = new GeometryGroup { Id = Plan.NextId++, VertexIds = SelectedVertices.ToList(), EdgeIds = SelectionEdges().ToList() };
        Plan.Groups.Add(track); Plan.Normalize(); Plan.Groups.Remove(track);
        ClearSelection(); IndividualSelection = individual; SelectedAnnotations.UnionWith(annotations); SelectedVertices.UnionWith(track.VertexIds); SelectedEdges.UnionWith(track.EdgeIds); RefreshGeometry();
        if (individual && hadFaces) SelectedFaces.UnionWith(faces.Where(f => CachedFaceEdges(f).IsSubsetOf(SelectedEdges)).Select(f => f.Key));
        ExpandGroups();
    }
    private void DrawTransformGuide(DrawingContext dc)
    {
        if (Tool != EditorTool.Select || Transform == TransformKind.Translation || (SelectedAnnotations.Count == 0 && SelectionPoints().Count == 0)) return;
        if (Transform == TransformKind.Scale && SelectedAnnotations.Count == 0) { DrawScaleHandles(dc); return; }
        var center = Screen(dragging ? transformPivot : SelectionCenter);
        var pen = new Pen(Accent, 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(pen, new(center.X - 10, center.Y), new(center.X + 10, center.Y)); dc.DrawLine(pen, new(center.X, center.Y - 10), new(center.X, center.Y + 10));
        dc.DrawEllipse(null, pen, center, 16, 16);
        if (dragging && moved) { dc.DrawLine(pen, center, mouseScreen); Label(dc, Transform == TransformKind.Rotation ? L.F($"Rotation {transformAmount:0.#}°") : L.F($"Échelle ×{transformAmount:0.##}"), new Point(mouseScreen.X + 14, mouseScreen.Y - 24), 12, Accent); }
    }
}
public partial class MainWindow
{
    private string rotationText = "90", scaleXText = "1", scaleYText = "1";
    private bool uniformScale = true;
    public void RotateSelection(double degrees)
    {
        if (!double.IsFinite(degrees)) { Notify(L.T("Angle invalide.")); return; }
        var pivot = Canvas.SelectionCenter; TransformSelection(p => SelectionTransform.Rotate(p, pivot, degrees), angle: degrees);
    }
    public void ScaleSelection(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) < .001 || Math.Abs(y) < .001 || Math.Abs(x) > 1000 || Math.Abs(y) > 1000) { Notify(L.T("Les facteurs doivent être compris entre 0,001 et 1000 en valeur absolue ; zéro est interdit.")); return; }
        var pivot = Canvas.SelectionCenter; TransformSelection(p => SelectionTransform.Scale(p, pivot, x, y), scale: Math.Sqrt(Math.Abs(x*y)));
    }
    private void TransformSelection(Func<V2, V2> transform, double scale = 1, double angle = 0)
    {
        Canvas.CancelGesture();
        var vertices = Document.Vertices.ToDictionary(v => v.Id);
        var changes = Canvas.SelectionPoints().ToDictionary(id => id, id => transform(vertices[id].P));
        var notes = Document.Annotations.Where(a => Canvas.SelectedAnnotations.Contains(a.Id)).ToDictionary(a => a.Id);
        var noteChanges = notes.ToDictionary(a => a.Key,a => transform(new V2(a.Value.X,a.Value.Y)));
        if(changes.Count + noteChanges.Count == 0)return;
        if (!changes.Values.Concat(noteChanges.Values).All(SelectionTransform.Valid)) { Notify(L.T("La transformation dépasse les limites du plan.")); return; }
        Mutate(() =>
        {
            foreach (var (id, position) in changes) vertices[id].P = position;
            foreach (var (id, position) in noteChanges)
            {
                var note = notes[id];
                EditorCanvas.ApplyAnnotationTransform(note, position, note.Size, note.Rotation, scale, angle);
            }
            if (changes.Count > 0) Canvas.NormalizeSelection();
        });
        Canvas.Focus();
    }

    private void TransformInspector()
    {
        Heading(L.T("TRANSFORMATIONS"));
        var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        string[] ids = { "Translation", "Rotation", "Échelle" };
        for (int i = 0; i < 3; i++)
        {
            var mode = (TransformKind)i; var button = Button("", () => Canvas.SetTransform(mode), ids[i]);
            string title = L.T(ids[i]);
            button.Content = new Image { Source = TransformIcon(mode), Width = 22, Height = 22 };
            CompactButton(button, title);
            SetHint(button, title, L.T(mode switch
            {
                TransformKind.Translation => "Déplacez la sélection sur le plan.",
                TransformKind.Rotation => "Tournez la sélection autour de son centre.",
                _ => "Redimensionnez la sélection sur un ou deux axes."
            }));
            SetActive(button, Canvas.Transform == mode);
            buttons.Children.Add(button);
        }
        inspector.Children.Add(buttons);
        bool selected = Canvas.SelectionPoints().Count > 0 || Canvas.SelectedAnnotations.Count > 0;
        if (!selected) { ToolHint(L.T("Sélectionnez des points, arêtes ou zones pour les transformer.")); return; }
        if (Canvas.Transform == TransformKind.Translation)
        {
            var (dx, dy) = AxisFields(L.T("Décalage"), moveXText, moveYText, L.T("Décalage X"), L.T("Décalage Y"));
            dx.TextChanged += (_, _) => moveXText = dx.Text; dy.TextChanged += (_, _) => moveYText = dy.Text;
            var actions = new WrapPanel { Margin = new Thickness(0,0,0,6) };
            actions.Children.Add(ConfirmButton(L.T("Déplacer la sélection"), () =>
            {
                if (Number(dx, out var x) && Number(dy, out var y)) MoveSelection(x, y);
            }, dx, dy));
            inspector.Children.Add(actions);
            if (Canvas.Mode == SelectionMode.Edge && Canvas.SelectedEdges.Count > 0)
            {
                var ex = Button(L.T("Extruder avec ce décalage"), () => { if (Number(dx, out var x) && Number(dy, out var y)) Mutate(() => { var next = Document.Extrude(Canvas.SelectedEdges.ToArray(), new V2(x, y)); Canvas.SelectedEdges.Clear(); Canvas.SelectedEdges.UnionWith(next); }); }); actions.Children.Add(ex);
            }
        }
        else if (Canvas.Transform == TransformKind.Rotation)
        {
            var angle = Field("Angle (°)", rotationText); angle.TextChanged += (_, _) => rotationText = angle.Text;
            inspector.Children.Add(ConfirmButton(L.T("Appliquer la rotation"), () => { if (Number(angle, out var value)) RotateSelection(value); }, angle));
            ToolHint(L.T("\nPivot au centre de la sélection. Angle positif : sens antihoraire. Glissez pour tourner ; Maj règle le pas à 15°."));
        }
        else
        {
            var uniform = new CheckBox { Content = L.T("Échelle uniforme"), IsChecked = uniformScale || Canvas.SelectedAnnotations.Count > 0 && Canvas.SelectionPoints().Count == 0 }; uniform.IsEnabled = Canvas.SelectedAnnotations.Count == 0 || Canvas.SelectionPoints().Count > 0; uniform.Click += (_, _) => { uniformScale = uniform.IsChecked == true; UpdateInspector(); }; inspector.Children.Add(uniform);
            TextBox x; TextBox? y = null;
            if (uniform.IsChecked == true) x = Field(L.T("Facteur"), scaleXText);
            else { (x, y) = AxisFields(L.T("Facteur"), scaleXText, scaleYText, L.T("Facteur X"), L.T("Facteur Y")); y.TextChanged += (_, _) => scaleYText = y.Text; }
            x.TextChanged += (_, _) => scaleXText = x.Text;
            inspector.Children.Add(ConfirmButton(L.T("Appliquer l’échelle"), () => { if (!Number(x, out double sx)) return; double sy = sx; if (y != null && !Number(y, out sy)) return; ScaleSelection(sx, sy); }, y == null ? new[] { x } : new[] { x, y }));
            ToolHint(L.T("Les poignées latérales redimensionnent un axe ; les coins redimensionnent X et Y. Maj sur un coin conserve les proportions. Le côté opposé reste fixe. Les facteurs numériques utilisent le centre ; un facteur négatif crée un miroir."));
        }
    }
    private ImageSource TransformIcon(TransformKind kind)
    {
        var drawing = new DrawingGroup(); var pen = new Pen(B("#B8E8DD"), 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        using (var dc = drawing.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
            void Line(double x, double y, double u, double v) => dc.DrawLine(pen, new Point(x, y), new Point(u, v));
            void Arrow(double x, double y, double u, double v)
            {
                Line(x, y, u, v); var d = new Vector(u - x, v - y); d.Normalize(); var n = new Vector(-d.Y, d.X); var end = new Point(u, v);
                dc.DrawLine(pen, end, end - d * 4 + n * 3); dc.DrawLine(pen, end, end - d * 4 - n * 3);
            }
            if (kind == TransformKind.Translation) { Arrow(16, 16, 16, 3); Arrow(16, 16, 29, 16); Arrow(16, 16, 16, 29); Arrow(16, 16, 3, 16); }
            else if (kind == TransformKind.Rotation)
            {
                dc.DrawEllipse(null, pen, new Point(16, 16), 12, 12); dc.DrawEllipse(null, pen, new Point(16, 16), 5, 12); dc.DrawEllipse(null, pen, new Point(16, 16), 12, 5); Line(23, 4, 28, 7); Line(28, 7, 29, 2);
            }
            else
            {
                void Cube(double x, double y, double s) { double d = s * .35; Line(x, y, x + s, y); Line(x + s, y, x + s, y + s); Line(x + s, y + s, x, y + s); Line(x, y + s, x, y); Line(x, y, x + d, y - d); Line(x + s, y, x + s + d, y - d); Line(x + d, y - d, x + s + d, y - d); Line(x + s + d, y - d, x + s + d, y + s - d); Line(x + s + d, y + s - d, x + s, y + s); }
                Cube(2, 24, 5); Cube(19, 6, 9); Arrow(10, 22, 18, 14);
            }
        }
        drawing.Freeze(); return new DrawingImage(drawing);
    }
}
