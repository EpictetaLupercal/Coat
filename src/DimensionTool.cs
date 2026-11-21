// Coat — Epicteta Lupercal
namespace Coat;
public partial class MainWindow
{
    private void DimensionInspector()
    {
        var ids = Canvas.SelectionEdges();
        DimensionControls(Document.Edges.Where(e => e.Dimension && ids.Contains(e.Id)).ToList(), true);
        ToolHint(L.T("Placez deux points pour créer une cote. Sélectionnez une cote pour régler son texte et ses flèches. Chaque cote affiche la distance entre ses extrémités."));
    }
    private void DimensionControls(List<Edge> selected, bool includeDefault)
    {
        if (!includeDefault) Heading(L.T("COTATION"));
        var targets = selected.ToList(); if (includeDefault || targets.Count == 0) targets.Add(Canvas.DimensionAppearance);
        var first = targets[0];
        var font = Field(L.T("Taille de police"), F(first.DimensionFontSize)); font.Tag = new NumericRange(4,256,false);
        var arrows = Field(L.T("Taille des flèches"), F(first.ArrowSize)); arrows.Tag = new NumericRange(1,1024,false);
        inspector.Children.Add(ConfirmButton(L.T("Appliquer la cote"), () =>
        {
            if (!Number(font,out var size) || size < 4 || size > 256 || !Number(arrows,out var arrowSize) || arrowSize < 1 || arrowSize > 1024) return;
            void Apply() { foreach (var edge in targets) { edge.DimensionFontSize = size; edge.ArrowSize = arrowSize; } }
            if (selected.Count > 0) Mutate(Apply); else { Apply(); Canvas.InvalidateVisual(); }
        },font,arrows));
    }
}
