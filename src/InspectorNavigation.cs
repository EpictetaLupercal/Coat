// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Coat;

public partial class MainWindow
{
    private enum InspectorPage { Plan, Selection, Modifiers }

    private InspectorPage inspectorPage;
    private EditorTool toolBeforeModifiers;
    private readonly Dictionary<InspectorPage, Button> inspectorTabs = new();
    private readonly Dictionary<SelectionMode, Button> selectionModeButtons = new();
    private ScrollViewer inspectorScroll = null!;

    private void AddInspectorTabs(DockPanel panel)
    {
        inspectorTabs.Clear();
        var tabs = new Grid { Margin = new Thickness(12, 10, 12, 0) };
        foreach (var (page, label, weight) in new[]
        {
            (InspectorPage.Plan, "Plan", .8), (InspectorPage.Selection, "Sélection", 1.02),
            (InspectorPage.Modifiers, "Modificateurs", 1.25)
        })
        {
            var button = Button("", () => ShowInspectorPage(page), page == InspectorPage.Modifiers ? "Modificateurs" : null);
            var title = L.T(label);
            button.Content = new TextBlock { Text = title };
            button.Padding = new Thickness(4, 5, 4, 5);
            button.Margin = new Thickness(tabs.Children.Count == 0 ? 0 : 3, 0, 0, 0);
            AutomationProperties.SetName(button, title);
            inspectorTabs.Add(page, button);
            Grid.SetColumn(button, tabs.Children.Count);
            tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
            tabs.Children.Add(button);
        }
        DockPanel.SetDock(tabs, Dock.Top);
        panel.Children.Add(tabs);
        inspectorScroll = new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.Children.Add(inspectorScroll);
    }

    private void ShowInspectorPage(InspectorPage page)
    {
        if (inspectorPage == page) return;
        if (page == InspectorPage.Modifiers) { SetTool(EditorTool.Modifiers); return; }
        if (Canvas.Tool == EditorTool.Modifiers)
        {
            ActivateTool(page == InspectorPage.Selection && toolBeforeModifiers == EditorTool.None ? EditorTool.Select : toolBeforeModifiers, page);
            return;
        }
        if (page == InspectorPage.Selection && Canvas.Tool == EditorTool.None) { SetTool(EditorTool.Select); return; }
        SetInspectorPage(page);
        UpdateInspector();
    }

    internal void ShowPlanInspector() => ShowInspectorPage(InspectorPage.Plan);

    private void SetInspectorPage(InspectorPage page)
    {
        if (inspectorPage != page) inspectorScroll.ScrollToTop();
        inspectorPage = page;
    }

    private void ActivateTool(EditorTool tool, InspectorPage page)
    {
        if (tool == EditorTool.Modifiers && Canvas.Tool != EditorTool.Modifiers) toolBeforeModifiers = Canvas.Tool;
        if (tool != EditorTool.Modifiers) CancelBooleanPick();
        SetInspectorPage(page);
        Canvas.SetTool(tool);
        UpdateToolButtons();
        UpdateStatus();
        Canvas.Focus();
    }

    private void AddSelectionModeButtons(Panel row)
    {
        foreach (var (mode, label, glyph, detail) in new[]
        {
            (SelectionMode.Vertex, "Sélection de points", "M4,4 H20 V20 H4 Z M2,2 H6 V6 H2 Z M18,2 H22 V6 H18 Z M18,18 H22 V22 H18 Z M2,18 H6 V22 H2 Z", "Sélectionnez les sommets du plan."),
            (SelectionMode.Edge, "Sélection d’arêtes", "M4,20 L20,4 M3,18 H7 V22 H3 Z M17,2 H21 V6 H17 Z", "Sélectionnez les segments du plan."),
            (SelectionMode.Face, "Sélection de zones", "M3,8 L14,3 21,8 10,20 3,16 Z M7,10 L15,7 M8,15 L17,10", "Sélectionnez les zones fermées du plan.")
        })
        {
            var button = Button("", () =>
            {
                if (Canvas.Tool == EditorTool.Select && Canvas.Mode == mode) ShowInspectorPage(InspectorPage.Selection);
                else SetSelectionMode(mode);
                Canvas.Focus();
            }, label);
            IconButton(button, L.T(label), glyph, L.T(detail));
            selectionModeButtons.Add(mode, button);
            row.Children.Add(button);
        }
    }

    private void SetSelectionMode(SelectionMode mode)
    {
        Canvas.CancelGesture();
        Canvas.ClearSelection();
        Canvas.Mode = mode;
        SetTool(EditorTool.Select);
    }

    private void AddToolbarDivider(Panel toolbar) => toolbar.Children.Add(new Border
    {
        Background = B("#354151"), Width = 1, Margin = new Thickness(8, 5, 8, 5)
    });
}
