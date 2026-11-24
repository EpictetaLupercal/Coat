// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;
public partial class MainWindow
{
    private readonly Dictionary<string, Action> menuCommands = new(), inspectorCommands = new();
    private readonly Dictionary<MenuItem, string> featureMenus = new();
    private string? RegisterFeature(string title, Action action, bool menu = false)
    {
        var id = ShortcutSettings.FeatureLabels.FirstOrDefault(label => L.T(label) == title);
        if (id != null) (menu ? menuCommands : inspectorCommands)[id] = action;
        return id;
    }
    internal bool ExecuteFeature(string id)
    {
        void Line(LineStyle style) { Canvas.PaintLineStyle = style; if (Canvas.Tool == EditorTool.Draw) Canvas.DrawAppearance.Style = style; var ids = Canvas.SelectionEdges(); if (ids.Count > 0) Mutate(() => { foreach (var e in Document.Edges.Where(e => ids.Contains(e.Id))) e.Style = style; }); else { if (Canvas.Tool != EditorTool.Draw) SetTool(EditorTool.Paint); Canvas.PaintLines = true; UpdateInspector(); } }
        void Pattern(FillPattern pattern) { Canvas.GradientMode = false; Canvas.PaintPattern = pattern; if (Canvas.SelectedFaces.Count > 0) Mutate(() => { foreach (string key in Canvas.SelectedFaces) { Document.Fills.TryAdd(key, Canvas.PaintColor); Document.FillPatterns[key] = pattern; Document.Gradients.Remove(key); } }); else { SetTool(EditorTool.Paint); Canvas.PaintLines = false; UpdateInspector(); } }
        switch (id)
        {
            case "Ping : repère": return ArmProjectPing(PingKind.Landmark);
            case "Ping : à revoir": return ArmProjectPing(PingKind.Review);
            case "Ping : à tester": return ArmProjectPing(PingKind.Test);
            case ShortcutSettings.FocusAction: Canvas.ToggleSelectionFocus(); break;
            case "Miroir": SetTool(EditorTool.Mirror); break;
            case "Modificateurs": SetTool(EditorTool.Modifiers); break;
            case "Ignorer les groupes": SetTool(EditorTool.Select); Canvas.ToggleIgnoreGroups(); break;
            case "Cote": SetTool(EditorTool.Dimension); break;
            case "Dissoudre les sommets": Canvas.DissolveSelection(true); break;
            case "Dissoudre les arêtes": Canvas.DissolveSelection(false); break;
            case "Jeton": SetTool(EditorTool.Token); break;
            case "Dégradé": case "Remplissage": SetTool(EditorTool.Paint); Canvas.PaintLines = false; Canvas.GradientMode = id == "Dégradé"; UpdateInspector(); break;
            case "Confirmer": Canvas.ConfirmGesture(); break;
            case "Rectangle": ChooseShape(ShapeKind.Rectangle); break;
            case "Cercle": ChooseShape(ShapeKind.Circle); break;
            case "Escaliers droits": ChooseShape(ShapeKind.Stairs, StairKind.Straight); break;
            case "Escaliers en L": ChooseShape(ShapeKind.Stairs, StairKind.L); break;
            case "Escaliers en U": ChooseShape(ShapeKind.Stairs, StairKind.U); break;
            case "Escaliers en colimaçon": ChooseShape(ShapeKind.Stairs, StairKind.Spiral); break;
            case "Tourne à gauche": case "Tourne à droite": Canvas.StairRight = id == "Tourne à droite"; UpdateInspector(); break;
            case "Lignes continues": Line(LineStyle.Solid); break;
            case "Tirets longs": Line(LineStyle.LongDash); break;
            case "Tirets courts": Line(LineStyle.ShortDash); break;
            case "Remplissage uni": Pattern(FillPattern.Solid); break;
            case "Remplissage à bandes": Pattern(FillPattern.Stripes); break;
            case "Remplissage à points": Pattern(FillPattern.Dots); break;
            case "Remplir une zone": case "Colorer les lignes": SetTool(EditorTool.Paint); Canvas.PaintLines = id == "Colorer les lignes"; UpdateInspector(); break;
            case "Sélection sur plusieurs niveaux": Canvas.SelectAcrossLevels = !Canvas.SelectAcrossLevels; UpdateInspector(); break;
            case "Définir la couleur des murs": SetWallColor(Canvas.PaintColor); break;
            case "Utiliser cette couleur": ChooseColor(Canvas.PaintColor, value => Canvas.PaintColor=value); ActiveColorPicker?.HexInput.Focus(); break;
            case "Couleur des murs…": ChooseColor(Document.WallColor, value => Document.WallColor=value); break;
            case "◉  Roue de couleurs…": ChooseColor(Canvas.PaintColor, color => Canvas.PaintColor = color); break;
            case "Organisation": ShowOrganization(); break;
            case "Sélectionner le groupe suivant":
                var groups = Document.Groups; if (groups.Count == 0) break;
                var current = Document.MatchingGroups(Canvas.SelectedVertices, Canvas.SelectionEdges()).FirstOrDefault();
                SelectGroup(groups[(groups.IndexOf(current!) + 1) % groups.Count]); break;
            case "Échelle uniforme": uniformScale = !uniformScale; UpdateInspector(); break;
            case "Afficher sous la grille": if (Document.Background is { } bg) Mutate(() => bg.Visible = !bg.Visible); break;
            case "Zoom avant": Canvas.Zoom = Math.Clamp(Canvas.Zoom * 1.15, .01, 20); break;
            case "Zoom arrière": Canvas.Zoom = Math.Clamp(Canvas.Zoom / 1.15, .01, 20); break;
            case "Aucun outil": SetTool(EditorTool.None); break;
            case "Modifier la hauteur": ShowPlanInspector(); UpdateLayout(); FocusField(L.T("Hauteur du niveau")); break;
            case "Modifier le nombre de points": ChooseShape(ShapeKind.Circle); UpdateLayout(); FocusField(L.T("Points")); break;
            case "Modifier le nombre de marches": ChooseShape(ShapeKind.Stairs); UpdateLayout(); FocusField(L.T("Marches")); break;
            case "Modifier l’angle": FocusField("Angle (°)"); break;
            case "Modifier la largeur": FocusField(L.T("Largeur (u)")); break;
            case "Ouvert récemment": recentMenu.IsSubmenuOpen = true; break;
            case "Effacer la liste": ClearRecent(); break;
            default:
                if (menuCommands.TryGetValue(id, out var menu)) menu();
                else if (inspectorCommands.TryGetValue(id, out var action)) action();
                else { Notify(L.T("Cette action n’est pas disponible dans le contexte actuel.")); return false; }
                break;
        }
        Canvas.InvalidateVisual(); return true;
    }
    private void FocusField(string name)
    {
        foreach (var field in Descendants<TextBox>(this))
            if (System.Windows.Automation.AutomationProperties.GetName(field) == name) { field.Focus(); field.SelectAll(); break; }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) yield return match;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants<T>(System.Windows.Media.VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
}
public partial class EditorCanvas
{
    internal void ConfirmGesture()
    {
        if (shapeStart != null) FinishShape(mouseWorld);
        else if (dragging || extruding || marquee) PointerUp(MouseButton.Left);
        else if (Anchor != null) { Anchor = chainStart = null; InvalidateVisual(); }
    }
}
