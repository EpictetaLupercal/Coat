// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Coat;
public partial class EditorCanvas
{
    public bool SelectAcrossLevels { get; set; }
    private bool CanSelectLevel(int level) => level == Plan.ActiveLevel || (Tool == EditorTool.Select && SelectAcrossLevels && IsLevelVisible(level));
    private bool IsLevelVisible(int level) => level == Plan.ActiveLevel || (level < Plan.ActiveLevel ? ShowLowerLevels : ShowUpperLevels);
}
public partial class MainWindow
{
    internal void SelectGroup(GeometryGroup group)
    {
        SetTool(EditorTool.Select); Canvas.ClearSelection();
        var ids = Document.GroupPoints(group); var levels = Document.Vertices.Where(v => ids.Contains(v.Id)).Select(v => v.Level).Distinct().ToList();
        // Selecting a named group is an explicit request to manipulate every member.
        if (levels.Count > 1 || levels.Any(l => l != Document.ActiveLevel)) Canvas.SelectAcrossLevels = true;
        if (levels.Any(l => l < Document.ActiveLevel)) Canvas.ShowLowerLevels = true;
        if (levels.Any(l => l > Document.ActiveLevel)) Canvas.ShowUpperLevels = true;
        upperLevelsMenu.IsChecked = Canvas.ShowUpperLevels; lowerLevelsMenu.IsChecked = Canvas.ShowLowerLevels;
        Canvas.SelectedVertices.UnionWith(group.VertexIds); Canvas.SelectedEdges.UnionWith(group.EdgeIds);
        Canvas.ExpandGroups(); UpdateInspector(); Canvas.InvalidateVisual();
    }
    private void GroupNameControls(GeometryGroup group)
    {
        var name = Field(L.T("Nom du groupe"), group.Name, numeric: false);
        inspector.Children.Add(ConfirmButton(L.T("Renommer le groupe"), () => RenameGroup(group.Id, name.Text), name));
    }
    internal void RenameGroup(int id, string name)
    {
        name = name.Trim(); if (name.Length == 0 || name.Length > 100) { Notify(L.T("Le nom doit contenir entre 1 et 100 caractères.")); return; }
        var group = Document.Groups.FirstOrDefault(g => g.Id == id); if (group == null || group.Name == name) return;
        Mutate(() => group.Name = name);
    }
    private void ShowOrganization()
    {
        var dialog = new Window { Owner = this, Title = L.T("Organisation"), Width = 470, Height = 600, Background = Brushes.White, Foreground = Brushes.Black, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel { Margin = new Thickness(16) }; dialog.Content = root;
        var done = new Button { Content = "OK", IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 12, 0, 0) };
        done.Click += (_, _) => dialog.Close(); DockPanel.SetDock(done, Dock.Bottom); root.Children.Add(done);
        var title = new TextBlock { Text = L.T("Niveaux → Groupes → Formes"), Margin = new Thickness(0, 0, 0, 12), FontWeight = FontWeights.SemiBold };
        DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var tree = new TreeView { Background = Brushes.White, Foreground = Brushes.Black }; root.Children.Add(tree);
        var allFaces = Document.Faces(); var vertices = Document.Vertices.ToDictionary(v => v.Id);
        var groupPoints = Document.Groups.ToDictionary(g => g.Id, Document.GroupPoints);
        TreeViewItem Node(string label, Action select) { var n = new TreeViewItem { Header = label, Foreground = Brushes.Black }; n.Selected += (_, e) => { e.Handled = true; select(); }; return n; }
        void AddFaces(TreeViewItem parent, IEnumerable<Face> source)
        {
            foreach (var face in source)
            {
                var n = Node(L.F($"Forme · {face.Vertices.Count} points · {face.Area:0.#} u²"), () =>
                {
                    SetTool(EditorTool.Select); Canvas.SelectAcrossLevels = true;
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) Canvas.ClearSelection();
                    if (face.Level > Document.ActiveLevel) Canvas.ShowUpperLevels = true; if (face.Level < Document.ActiveLevel) Canvas.ShowLowerLevels = true;
                    Canvas.SelectedFaces.Add(face.Key); Canvas.ExpandGroups(); UpdateInspector(); Canvas.InvalidateVisual();
                }); parent.Items.Add(n);
            }
        }
        foreach (int level in Document.Levels)
        {
            var node = Node(L.T("Niveau ") + level, () => ChangeLevel(level)); tree.Items.Add(node);
            node.Items.Add(new TreeViewItem { Header = "…" }); bool populated = false;
            node.Expanded += (_, e) =>
            {
                if (e.OriginalSource != node || populated) return; populated = true; node.Items.Clear();
                var grouped = new HashSet<string>();
                foreach (var group in Document.Groups.Where(g => groupPoints[g.Id].Any(id => vertices[id].Level == level)))
                {
                    var child = Node(group.Name, () => SelectGroup(group)); node.Items.Add(child);
                    var members = allFaces.Where(f => f.Level == level && f.Vertices.All(groupPoints[group.Id].Contains)).ToList();
                    grouped.UnionWith(members.Select(f => f.Key)); AddFaces(child, members);
                    if (members.Count == 0) child.Items.Add(new TreeViewItem { Header = L.F($"{groupPoints[group.Id].Count} points"), Foreground = Brushes.Gray });
                }
                AddFaces(node, allFaces.Where(f => f.Level == level && !grouped.Contains(f.Key)));
                var loose = Node(L.T("Tous les éléments du niveau"), () => { ChangeLevel(level); SetTool(EditorTool.Select); Canvas.SelectAcrossLevels = false; Canvas.SelectAll(); }); node.Items.Add(loose);
            };
        }
        dialog.ShowDialog();
    }
}
