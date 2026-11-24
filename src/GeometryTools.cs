// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Coat;

public partial class MainWindow
{
    private bool mirrorHorizontal, mirrorCopy;
    private double? mirrorAxis;
    private enum BooleanPick { Target, Operand, Rectangle }
    private BooleanPick booleanPick;
    private GeometryCombineMode booleanOperation = GeometryCombineMode.Exclude;
    private Plan? booleanDocument;
    private int booleanLevel;
    private string? booleanTarget, booleanOperand;
    private V2? cutterStart;
    private List<V2>? booleanCutter;
    private BooleanRegions? booleanPreview;

    private double MirrorCoordinate => mirrorAxis ?? (mirrorHorizontal ? Canvas.SelectionCenter.Y : Canvas.SelectionCenter.X);

    internal void PickMirrorAxis(V2 point)
    {
        mirrorAxis = mirrorHorizontal ? point.Y : point.X;
        UpdateInspector(); Canvas.InvalidateVisual();
    }

    private void MirrorInspector()
    {
        ToolHint(L.T("Faites une sélection, puis cliquez sur le plan pour placer l’axe du miroir."));
        ModeButtons(new[] { L.T("Vertical"), L.T("Horizontal") }, mirrorHorizontal ? 1 : 0,
            i => { mirrorHorizontal = i == 1; mirrorAxis = null; UpdateInspector(); Canvas.InvalidateVisual(); });
        var axis = Field(L.T(mirrorHorizontal ? "Axe Y" : "Axe X"), F(MirrorCoordinate));
        var copy = new CheckBox { Content = L.T("Conserver l’original"), IsChecked = mirrorCopy };
        copy.Click += (_, _) => mirrorCopy = copy.IsChecked == true;
        inspector.Children.Add(copy);
        inspector.Children.Add(ConfirmButton(L.T("Appliquer le miroir"), () =>
        {
            if (Number(axis, out double coordinate)) ApplyMirror(mirrorHorizontal, coordinate, mirrorCopy);
        }, axis));
    }

    internal bool ApplyMirror(bool horizontal, double coordinate, bool copy)
    {
        V2 Reflect(V2 p) => horizontal ? new(p.X, 2 * coordinate - p.Y) : new(2 * coordinate - p.X, p.Y);
        GradientFill ReflectGradient(GradientFill g) => g with { Angle = (horizontal ? -g.Angle : 180 - g.Angle) % 360 };
        var positions = Canvas.SelectionPoints().Select(id => Document.Get(id).P)
            .Concat(Document.Annotations.Where(n => Canvas.SelectedAnnotations.Contains(n.Id)).Select(n => n.Position)).ToArray();
        if (positions.Length == 0) { Notify(L.T("Sélectionnez les éléments du miroir.")); return false; }
        if (positions.Any(p => !SelectionTransform.Valid(Reflect(p)))) { Notify(L.T("La transformation dépasse les limites du plan.")); return false; }
        if (!copy)
        {
            TransformSelection(Reflect, gradientTransform: ReflectGradient);
            return true;
        }

        var fragment = CaptureSelection();
        if (Document.Vertices.Count + fragment.Vertices.Count > 20000 || Document.Edges.Count + fragment.Edges.Count > 30000 ||
            Document.Annotations.Count + fragment.Annotations.Count > 5000)
        { Notify(L.T("La transformation dépasse les limites du plan.")); return false; }
        Mutate(() =>
        {
            var vertices = fragment.Vertices.ToDictionary(v => v.Id, v => Document.Point(Reflect(v.P), v.Level));
            foreach (var vertex in fragment.Vertices.Where(v => v.KeepPoint)) Document.Get(vertices[vertex.Id]).KeepPoint = true;
            var edges = new Dictionary<int, int>();
            foreach (var edge in fragment.Edges)
            {
                int count = Document.Edges.Count;
                int id = Document.AddEdge(vertices[edge.A], vertices[edge.B], edge.Color);
                edges[edge.Id] = id;
                if (Document.Edges.Count == count) continue;
                Document.Edges[^1].Style = edge.Style;
                Document.Edges[^1].CopyArrows(edge);
            }
            foreach (var key in fragment.Gradients.Keys.ToArray()) fragment.Gradients[key] = ReflectGradient(fragment.Gradients[key]);
            CopyFaceStyles(fragment, Document, vertices, edges.Keys.ToHashSet());
            CopyFragmentGroups(fragment, Document, vertices, edges);
            foreach (var note in fragment.Annotations)
                Document.Annotations.Add(CloneNote(note, Document.NextId++, Reflect(note.Position) - note.Position, note.Level));
            Document.Normalize();
        }, createsObjects: true);
        Canvas.Focus();
        return true;
    }

    private void CancelBooleanPick()
    {
        booleanDocument = null;
        booleanTarget = booleanOperand = null;
        cutterStart = null;
        booleanCutter = null;
        booleanPreview = null;
        booleanPick = BooleanPick.Target;
    }

    private void BooleanInspector()
    {
        if (booleanDocument != Document || booleanLevel != Document.ActiveLevel)
        {
            CancelBooleanPick();
            booleanDocument = Document;
            booleanLevel = Document.ActiveLevel;
        }
        InspectorTitle(L.T("Booléen"));
        GeometryCombineMode[] operations = { GeometryCombineMode.Union, GeometryCombineMode.Exclude, GeometryCombineMode.Intersect };
        var operation = new ComboBox { ItemsSource = new[] { L.T("Union"), L.T("Différence"), L.T("Intersection") }, SelectedIndex = Array.IndexOf(operations, booleanOperation), Margin = new Thickness(0, 4, 0, 6) };
        System.Windows.Automation.AutomationProperties.SetName(operation, L.T("Opération booléenne"));
        operation.SelectionChanged += (_, _) =>
        {
            if (operation.SelectedIndex < 0) return;
            booleanOperation = operations[operation.SelectedIndex];
            RefreshBoolean();
        };
        inspector.Children.Add(operation);
        ToolHint(L.T("Choisissez A et B sur le niveau actif. En différence, B est retiré de A."));
        PickButton(booleanTarget == null ? "Choisir A" : "Changer A", BooleanPick.Target);
        PickButton(booleanOperand == null ? "Choisir B" : "Changer B", BooleanPick.Operand);
        PickButton(booleanCutter == null ? "Tracer un rectangle B" : "Retracer le rectangle B", BooleanPick.Rectangle);
        if (booleanPick == BooleanPick.Rectangle)
            ToolHint(L.T(cutterStart == null ? "Cliquez sur le premier coin du rectangle B." : "Cliquez sur le coin opposé du rectangle B."));
        if (booleanPreview is { IsEmpty: true }) ToolHint(L.T("Le résultat est vide."));
        var apply = Button(L.T("Appliquer le booléen"), ApplyBoolean);
        apply.IsEnabled = booleanPreview != null;
        inspector.Children.Add(apply);

        void PickButton(string label, BooleanPick pick)
        {
            var button = Button(L.T(label), () => { booleanPick = pick; cutterStart = null; UpdateInspector(); Canvas.Focus(); });
            SetActive(button, booleanPick == pick);
            button.Margin = new Thickness(0, 0, 0, 4);
            inspector.Children.Add(button);
        }
    }

    internal void PickBooleanOperand(V2 point, Func<Face?> hit)
    {
        if (booleanPick == BooleanPick.Rectangle)
        {
            var snapped = point.Snap(Document.Grid);
            if (cutterStart is not { } start) { cutterStart = snapped; UpdateInspector(); Canvas.InvalidateVisual(); return; }
            var contour = Shapes.Contour(ShapeKind.Rectangle, start, snapped, 4, Document.Grid);
            if (contour.Count == 0) return;
            booleanCutter = contour;
            booleanOperand = null;
            cutterStart = null;
            booleanPick = booleanTarget == null ? BooleanPick.Target : BooleanPick.Operand;
        }
        else if (hit() is { } face)
        {
            if (booleanPick == BooleanPick.Target && face.Key != booleanOperand)
            { booleanTarget = face.Key; booleanPick = BooleanPick.Operand; }
            else if (booleanPick == BooleanPick.Operand && face.Key != booleanTarget)
            { booleanOperand = face.Key; booleanCutter = null; }
            else return;
        }
        else return;
        RefreshBoolean();
    }

    private BooleanRegions? BuildBoolean()
    {
        if (booleanTarget == null || (booleanOperand == null && booleanCutter == null) || booleanDocument != Document || booleanLevel != Document.ActiveLevel) return null;
        var faces = Document.Faces().Where(f => f.Level == booleanLevel).ToArray();
        var target = faces.FirstOrDefault(f => f.Key == booleanTarget);
        var operand = faces.FirstOrDefault(f => f.Key == booleanOperand);
        if (target == null || (operand == null && booleanCutter == null)) return null;
        return new BooleanRegions(target, operand, booleanCutter, booleanOperation, faces, Document.Vertices.ToDictionary(v => v.Id, v => v.P));
    }

    private void RefreshBoolean()
    {
        booleanPreview = BuildBoolean();
        UpdateInspector(); Canvas.InvalidateVisual();
    }

    private void ApplyBoolean()
    {
        var edit = BuildBoolean();
        if (edit == null) return;
        var result = Plan.Parse(Document.Serialize());
        if (!edit.Apply(result)) { Notify(L.T("La transformation dépasse les limites du plan.")); return; }
        CancelBooleanPick();
        Mutate(() => { Document = result; Canvas.ClearSelection(); }, createsFaces: true);
        Canvas.Focus();
    }

    internal void DrawGeometryToolGuide(DrawingContext drawing, V2 pointer)
    {
        if (Canvas.Tool == EditorTool.Mirror)
        {
            var point = Canvas.Screen(mirrorHorizontal ? new(0, MirrorCoordinate) : new(MirrorCoordinate, 0));
            var pen = new Pen(B("#78DCC1"), 1) { DashStyle = DashStyles.Dash };
            if (mirrorHorizontal) drawing.DrawLine(pen, new(0, point.Y), new(Canvas.ActualWidth, point.Y));
            else drawing.DrawLine(pen, new(point.X, 0), new(point.X, Canvas.ActualHeight));
        }
        if (Canvas.Tool != EditorTool.Modifiers) return;
        if (booleanPreview is { } preview)
        {
            drawing.PushTransform(new MatrixTransform(Canvas.Zoom, 0, 0, Canvas.Zoom,
                Canvas.ActualWidth / 2 - Canvas.Center.X * Canvas.Zoom, Canvas.ActualHeight / 2 + Canvas.Center.Y * Canvas.Zoom));
            drawing.DrawGeometry(null, new Pen(B("#728196"), 1 / Canvas.Zoom) { DashStyle = DashStyles.Dot }, preview.A);
            drawing.DrawGeometry(null, new Pen(B("#B9C7D8"), 1 / Canvas.Zoom) { DashStyle = DashStyles.Dash }, preview.B);
            drawing.DrawGeometry(EditorCanvas.Brush("#3378DCC1"), new Pen(B("#78DCC1"), 2 / Canvas.Zoom), preview.Result);
            drawing.Pop();
        }
        if (cutterStart is { } start)
        {
            var corner = Canvas.Screen(start);
            drawing.DrawRectangle(null, new Pen(B("#78DCC1"), 1) { DashStyle = DashStyles.Dash },
                new Rect(corner, Canvas.Screen(pointer.Snap(Document.Grid))));
            drawing.DrawEllipse(B("#78DCC1"), null, corner, 4, 4);
        }
    }
}
