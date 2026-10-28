// Coat — Epicteta Lupercal
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Coat;

public partial class MainWindow : Window
{
    public Plan Document { get; set; } = new();
    public EditorCanvas Canvas { get; }
    public History History { get; } = new();
    private StackPanel inspector = new();
    private StackPanel levelsPanel = new() { Margin = new Thickness(20, 18, 20, 0) };
    private TextBlock statusLeft = new(), statusRight = new();
    private readonly Dictionary<EditorTool, Button> toolButtons = new();
    private ComboBox gridChoice = new();
    private readonly int[] steps = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 };
    private string saved = "", file = "";
    private string moveXText = "0", moveYText = "0";
    private bool updating;
    private bool compactLevels;
    public bool TestMode { get; set; }
    private static Brush B(string c) => EditorCanvas.Brush(c);
    public MainWindow()
    {
        Title = L.T("Coat — Sans titre"); Width = 1440; Height = 900; MinWidth = 1050; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = B("#141B25"); Foreground = B("#DCE4ED"); FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        SetTheme(); Icon = MakeIcon(); Canvas = new EditorCanvas(this); Language = XmlLanguage.GetLanguage(L.Culture.Name);
        BuildInterface();
        saved = Document.Serialize(); Canvas.RefreshGeometry(); UpdateInspector(); UpdateToolButtons(); UpdateStatus();
        PreviewKeyDown += OnKey; PreviewKeyUp += (_, _) => Canvas.InvalidateVisual();
        Closing += (_, e) => { Canvas.CancelGesture(); if (!TestMode && !CheckSave()) e.Cancel = true; };
        Loaded += (_, _) => Canvas.Focus();
        SizeChanged += (_, _) => { bool compact = ActualHeight < 760; if (compact != compactLevels) { compactLevels = compact; UpdateLevels(); } };
    }
    private void BuildInterface()
    {
        if (VisualTreeHelper.GetParent(Canvas) is Panel parent) parent.Children.Remove(Canvas);
        inspector = new(); levelsPanel = new() { Margin = new Thickness(20, 18, 20, 0) };
        statusLeft = new(); statusRight = new(); gridChoice = new(); toolButtons.Clear(); shortcutMenus.Clear();
        var root = new Grid { Background = B("#141B25") }; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); Content = root;
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(14, 0, 16, 0) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 28, 0) };
        brand.Children.Add(new Border { Background = B("#78DCC1"), Width = 24, Height = 24, CornerRadius = new CornerRadius(5), Child = new TextBlock { Text = "C", Foreground = B("#13251F"), FontWeight = FontWeights.Bold, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center } });
        brand.Children.Add(new TextBlock { Text = "coat", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(9, -4, 0, 0) }); header.Children.Add(brand);
        var menu = new Menu { Background = Brushes.White, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Center };
        var fm = Menu(L.T("Fichier")); Add(fm, L.T("Nouveau plan"), "Ctrl+N", New); Add(fm, L.T("Ouvrir…"), "Ctrl+O", Open); Add(fm, L.T("Enregistrer"), "Ctrl+S", () => Save()); Add(fm, L.T("Enregistrer sous…"), "Ctrl+Maj+S", () => Save(true)); fm.Items.Add(new Separator()); Add(fm, L.T("Importer une image…"), "Ctrl+I", ImportImage); Add(fm, L.T("Exporter en PNG…"), "", ExportPng); Add(fm, L.T("Exporter en SVG…"), "", ExportSvg); Add(fm, L.T("Exporter en .map…"), "", ExportMap); fm.Items.Add(new Separator()); Add(fm, L.T("Charger le plan d’exemple"), "", LoadDemo); Add(fm, L.T("Quitter"), "Alt+F4", Close); menu.Items.Add(fm);
        var em = Menu(L.T("Édition")); Add(em, L.Undo, "Ctrl+Z", Undo); Add(em, L.T("Rétablir"), "Ctrl+Y", Redo); em.Items.Add(new Separator()); Add(em, L.T("Tout sélectionner"), "Ctrl+A", Canvas.SelectAll); Add(em, L.T("Dupliquer la sélection"), "Ctrl+D", Duplicate); Add(em, L.T("Supprimer la sélection"), L.T("Suppr"), Canvas.DeleteSelection); menu.Items.Add(em);
        Add(em, L.T("Grouper la sélection"), "Ctrl+G", GroupSelection); Add(em, L.T("Dissocier les groupes"), "Ctrl+Maj+G", UngroupSelection);
        Add(em, L.T("Copier"), "Ctrl+C", () => CopySelection()); Add(em, L.T("Couper"), "Ctrl+X", CutSelection); Add(em, L.T("Coller"), "Ctrl+V", PasteSelection);
        Add(em, L.T("Préférences…"), "Ctrl+OemComma", ShowPreferences);
        var vm = Menu(L.T("Affichage")); Add(vm, L.T("Cadrer le plan"), L.T("Origine"), Canvas.Fit); Add(vm, L.T("Revenir à l’origine"), "", () => { Canvas.Center = new(); Canvas.Zoom = 1; Canvas.InvalidateVisual(); UpdateStatus(); }); Add(vm, L.T("Afficher / masquer la grille"), "G", () => { Canvas.ShowGrid = !Canvas.ShowGrid; Canvas.InvalidateVisual(); }); Add(vm, L.T("Afficher / masquer les longueurs"), "M", () => { Canvas.ShowLengths = !Canvas.ShowLengths; Canvas.InvalidateVisual(); }); Add(vm, L.T("Afficher les niveaux supérieurs"), "H", ToggleUpperLevels); upperLevelsMenu = (MenuItem)vm.Items[vm.Items.Count - 1]; upperLevelsMenu.IsCheckable = true; Add(vm, L.T("Afficher les niveaux inférieurs"), "Shift+H", ToggleLowerLevels); lowerLevelsMenu = (MenuItem)vm.Items[vm.Items.Count - 1]; lowerLevelsMenu.IsCheckable = true; lowerLevelsMenu.IsChecked = Canvas.ShowLowerLevels; menu.Items.Add(vm);
        var hm = Menu(L.T("Aide")); Add(hm, L.T("Raccourcis et prise en main"), "F1", Help); Add(hm, L.T("À propos de Coat"), "", () => AppDialog.Show(this, L.T("Coat 1.0.5\n\nEpicteta Lupercal\n\nÉditeur de plans et blueprints pour level design."), L.T("À propos de Coat"), MessageBoxButton.OK, MessageBoxImage.Information)); menu.Items.Add(hm); header.Children.Add(menu);
        var version = new TextBlock { Text = "EPICTETA LUPERCAL  /  1.0.5", FontSize = 10, Foreground = B("#728196"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(version, Dock.Right); header.Children.Add(version); root.Children.Add(header);
        var toolbar = new DockPanel { Margin = new Thickness(12, 8, 12, 8), LastChildFill = false };
        var toolbarBorder = new Border { Background = B("#1B2430"), BorderBrush = B("#2B3645"), BorderThickness = new Thickness(0, 1, 0, 1), Child = toolbar }; Grid.SetRow(toolbarBorder, 1); root.Children.Add(toolbarBorder);
        AddTool(toolbar, EditorTool.Select, "↖", L.T("Sélection"), "V"); AddTool(toolbar, EditorTool.Draw, "╱", L.T("Dessin"), "D"); AddTool(toolbar, EditorTool.Erase, "⌫", L.T("Gomme"), "E"); AddTool(toolbar, EditorTool.Paint, "◐", L.T("Couleur"), "C");
        AddTool(toolbar, EditorTool.Shapes, "▱", L.T("Shapes"), "R");
        AddTool(toolbar, EditorTool.Annotation, "T", L.T("Texte"), "T");
        toolbar.Children.Add(new Border { Background = B("#354151"), Width = 1, Margin = new Thickness(12, 5, 14, 5) });
        toolbar.Children.Add(new TextBlock { Text = L.T("GRILLE"), FontSize = 10, Foreground = B("#96A4B6"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        gridChoice.Width = 108; gridChoice.Height = 32; gridChoice.VerticalAlignment = VerticalAlignment.Center;
        foreach (var step in steps) gridChoice.Items.Add(step + L.T(" unités")); gridChoice.SelectedIndex = 5;
        gridChoice.SelectionChanged += (_, _) => { if (!updating && gridChoice.SelectedIndex >= 0) SetGrid(steps[gridChoice.SelectedIndex]); }; toolbar.Children.Add(gridChoice);
        var save = Button(L.T("Enregistrer"), () => Save()); save.Margin = new Thickness(10, 0, 0, 0); save.ToolTip = L.T("Enregistrer le plan · Ctrl+S"); DockPanel.SetDock(save, Dock.Right); toolbar.Children.Add(save);
        var fit = Button(L.T("⊞  Cadrer"), Canvas.Fit); fit.ToolTip = L.T("Cadrer le plan") + " · " + CurrentHint(new(Key.Home, ModifierKeys.None)); DockPanel.SetDock(fit, Dock.Right); toolbar.Children.Add(fit);
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(294) }); Grid.SetRow(body, 2); root.Children.Add(body); body.Children.Add(Canvas);
        var sidebar = new Border { Background = B("#1B2430"), BorderBrush = B("#303B49"), BorderThickness = new Thickness(1, 0, 0, 0) };
        Grid.SetColumn(sidebar, 1); body.Children.Add(sidebar);
        var sideLayout = new DockPanel(); sidebar.Child = sideLayout;
        DockPanel.SetDock(levelsPanel, Dock.Top); sideLayout.Children.Add(levelsPanel);
        sideLayout.Children.Add(new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        inspector.Margin = new Thickness(20, 16, 20, 16);
        var status = new DockPanel { Margin = new Thickness(14, 0, 14, 0) };
        statusLeft.FontSize = statusRight.FontSize = 11; statusLeft.Foreground = statusRight.Foreground = B("#9BA9BA"); statusLeft.VerticalAlignment = statusRight.VerticalAlignment = VerticalAlignment.Center;
        statusRight.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(statusRight, Dock.Right); status.Children.Add(statusRight); status.Children.Add(statusLeft); Grid.SetRow(status, 3); root.Children.Add(status);
    }
    internal void ChangeLanguage(string language)
    {
        Canvas.CancelGesture(); L.SetLanguage(language);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.Name);
        BuildInterface();
        updating = true; gridChoice.SelectedIndex = Array.IndexOf(steps, Document.Grid); updating = false;
        if (upperLevelsMenu != null) upperLevelsMenu.IsChecked = Canvas.ShowUpperLevels;
        UpdateInspector(); UpdateToolButtons(); RefreshShortcutHints(); Canvas.InvalidateVisual(); Canvas.Focus();
    }
    private void SetTheme()
    {
        var xaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style TargetType="Button">
  <Setter Property="Foreground" Value="#D9E3EC"/><Setter Property="Background" Value="#263241"/><Setter Property="BorderBrush" Value="#364455"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="12,7"/><Setter Property="Cursor" Value="Hand"/><Setter Property="VerticalContentAlignment" Value="Center"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="b" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="BorderBrush" Value="#78DCC1"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="b" Property="Opacity" Value="0.65"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="b" Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="TextBox"><Setter Property="Background" Value="#141D28"/><Setter Property="Foreground" Value="#DFE8F2"/><Setter Property="CaretBrush" Value="#78DCC1"/><Setter Property="BorderBrush" Value="#3B485A"/><Setter Property="Padding" Value="8,6"/><Setter Property="SelectionBrush" Value="#437D70"/></Style>
 <Style TargetType="ComboBox"><Setter Property="Foreground" Value="#DFE8F2"/><Setter Property="Background" Value="#263241"/><Setter Property="BorderBrush" Value="#3B485A"/><Setter Property="Padding" Value="8,6"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="#263241" BorderBrush="#3B485A" BorderThickness="1" CornerRadius="4"><TextBlock Text="⌄" Foreground="#B9C7D8" HorizontalAlignment="Right" Margin="0,0,9,0" VerticalAlignment="Center"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin="9,0,25,0" VerticalAlignment="Center" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}"/><Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False"><Border Background="#263241" BorderBrush="#4A5B70" BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"><ScrollViewer MaxHeight="360"><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="#DFE8F2"/><Setter Property="Padding" Value="10,6"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="b" Padding="{TemplateBinding Padding}" Background="#263241"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="b" Property="Background" Value="#436155"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="MenuItem"><Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="Black"/><Setter Property="Padding" Value="9,5"/></Style>
 <Style TargetType="CheckBox"><Setter Property="Foreground" Value="#BCC9D9"/><Setter Property="Margin" Value="0,6,0,6"/></Style>
 <Style TargetType="ToolTip"><Setter Property="Background" Value="#263241"/><Setter Property="Foreground" Value="#E1EBF5"/></Style>
 <Style TargetType="ScrollBar"><Setter Property="Background" Value="#1B2430"/><Setter Property="Width" Value="8"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="{TemplateBinding Background}"><Track x:Name="PART_Track" IsDirectionReversed="True"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#445368" CornerRadius="3" Margin="2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
""";
        Resources = (ResourceDictionary)XamlReader.Parse(xaml);
        Resources[SystemColors.MenuBrushKey] = Brushes.White; Resources[SystemColors.MenuTextBrushKey] = Brushes.Black;
    }
    private static ImageSource MakeIcon()
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRoundedRectangle(B("#78DCC1"), null, new Rect(0, 0, 32, 32), 7, 7); var pen = new Pen(B("#15281F"), 4); dc.DrawLine(pen, new Point(23, 9), new Point(10, 9)); dc.DrawLine(pen, new Point(10, 9), new Point(10, 23)); dc.DrawLine(pen, new Point(10, 23), new Point(23, 23)); } var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual); return bmp;
    }
    private MenuItem Menu(string title) => new() { Header = title, Background = Brushes.White, Foreground = Brushes.Black };
    private void Add(MenuItem parent, string title, string shortcut, Action action)
    { var item = new MenuItem { Header = title, InputGestureText = shortcut }; item.Click += (_, _) => action(); parent.Items.Add(item); var original = ParseHint(shortcut); if (original != null) shortcutMenus.Add((item, original)); }
    private Button Button(string text, Action action)
    { var b = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch }; b.Click += (_, _) => action(); return b; }
    private void AddTool(DockPanel bar, EditorTool tool, string icon, string title, string key)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal }; content.Children.Add(new TextBlock { Text = icon, FontSize = 18, Margin = new Thickness(0, -2, 7, 0) }); content.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
        var button = Button("", () => SetTool(Canvas.Tool == tool ? EditorTool.None : tool)); button.Content = content; button.Margin = new Thickness(0, 0, 5, 0); button.Padding = new Thickness(11, 7, 11, 7); button.ToolTip = title + " · " + key; toolButtons[tool] = button; bar.Children.Add(button);
    }
    public void SetTool(EditorTool tool) { Canvas.SetTool(tool); UpdateToolButtons(); UpdateStatus(); Canvas.Focus(); }
    private void UpdateToolButtons()
    {
        foreach (var pair in toolButtons)
        { bool active = pair.Key == Canvas.Tool; pair.Value.Background = B(active ? "#304E48" : "#263241"); pair.Value.BorderBrush = B(active ? "#78DCC1" : "#364455"); pair.Value.Foreground = B(active ? "#9BEBD4" : "#D9E3EC"); }
    }
    private void Heading(string title)
    { inspector.Children.Add(new TextBlock { Text = title, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = B("#8596AB"), Margin = new Thickness(0, 0, 0, 12) }); }
    private void Description(string text)
    { inspector.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = B("#96A7BC"), LineHeight = 19, Margin = new Thickness(0, 0, 0, 12) }); }
    private readonly List<string> inspectorHints = new();
    private Button? inspectorHelp;
    private void InspectorTitle(string title)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        inspectorHelp = Button("?", () => AppDialog.Show(this, string.Join("\n\n", inspectorHints), title, MessageBoxButton.OK, MessageBoxImage.Information));
        inspectorHelp.Width = 24; inspectorHelp.Height = 24; inspectorHelp.Padding = new Thickness(0); inspectorHelp.Visibility = Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(inspectorHelp, L.T("Aide"));
        DockPanel.SetDock(inspectorHelp, Dock.Right); row.Children.Add(inspectorHelp);
        row.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        inspector.Children.Add(row);
    }
    private void ToolHint(string text)
    {
        text = text.Trim(); if (!inspectorHints.Contains(text)) inspectorHints.Add(text);
        if (inspectorHelp == null) return;
        inspectorHelp.Visibility = Visibility.Visible;
        inspectorHelp.ToolTip = new TextBlock { Text = string.Join("\n\n", inspectorHints), TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
    }
    private void Divider() { inspector.Children.Add(new Border { Height = 1, Background = B("#303D4C"), Margin = new Thickness(0, 18, 0, 20) }); }
    private TextBox Field(string title, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = title, Foreground = B("#A7B6C9"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var input = new TextBox { Text = value, MinWidth = 60 }; System.Windows.Automation.AutomationProperties.SetName(input, title); Grid.SetColumn(input, 1); row.Children.Add(input); inspector.Children.Add(row); return input;
    }
    private void ModeButtons(string[] labels, int active, Action<int> choose)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 12) };
        for (int i = 0; i < labels.Length; i++) { int index = i; var b = Button(labels[i], () => choose(index)); b.FontSize = 11; b.Padding = new Thickness(4, 8, 4, 8); b.Margin = new Thickness(0, 0, i == labels.Length - 1 ? 0 : 4, 0); if (active == i) { b.Background = B("#304E48"); b.BorderBrush = B("#6DC8B0"); b.Foreground = B("#9BEBD4"); } grid.Children.Add(b); } inspector.Children.Add(grid);
    }
    public void UpdateInspector()
    {
        UpdateLevels(); inspector.Children.Clear(); inspectorHints.Clear(); inspectorHelp = null;
        if (Canvas.Tool != EditorTool.None)
        {
        string[] names = { L.T("Sélection"), L.T("Dessin de contours"), L.T("Gomme"), L.T("Coloriage"), "", L.T("Shapes"), L.T("Annotations") };
        InspectorTitle(names[(int)Canvas.Tool]);
        if (Canvas.Tool == EditorTool.Select)
        {
            ModeButtons(new[] { L.T("•  Points"), L.T("╱  Arêtes"), L.T("▱  Zones") }, (int)Canvas.Mode, i => { Canvas.CancelGesture(); Canvas.ClearSelection(); Canvas.Mode = (SelectionMode)i; UpdateInspector(); Canvas.InvalidateVisual(); Canvas.Focus(); });
            TransformInspector();
            if (Canvas.Transform == TransformKind.Translation)
                ToolHint(Canvas.Mode == SelectionMode.Edge ? L.T("Glissez une arête ou l’intérieur d’une forme entièrement sélectionnée pour la déplacer. Maintenez Maj et glissez pour extruder une épaisseur.") : Canvas.Mode == SelectionMode.Vertex ? L.T("Glissez un point ou saisissez ses coordonnées. Une forme entièrement sélectionnée se déplace aussi depuis son intérieur.") : L.T("Cliquez à l’intérieur d’un contour fermé pour sélectionner toute sa zone."));
            ToolHint(L.T("Ctrl + clic : sélection multiple. Glissez dans le vide pour sélectionner par rectangle."));
            StyleInspector();
            if (Canvas.SelectedAnnotation != null) AnnotationInspector();
            var ids = Canvas.SelectionPoints();
            if (ids.Count == 1)
            {
                var point = Document.Get(ids.First()); var x = Field("X", F(point.X)); var y = Field("Y", F(point.Y));
                inspector.Children.Add(Button(L.T("Appliquer les coordonnées"), () => { if (Number(x, out var nx) && Number(y, out var ny)) { Mutate(() => { point.P = new V2(nx, ny); Document.Normalize(); }); Canvas.Focus(); } }));
            }
            else if (ids.Count > 0)
            {
                Description(L.F($"{ids.Count} points dans la sélection"));
                if (Canvas.SelectedEdges.Count == 1) { var edge = Document.Edges.First(e => Canvas.SelectedEdges.Contains(e.Id)); Description(L.F($"Longueur : {(Document.Get(edge.A).P - Document.Get(edge.B).P).Length:0.##} unités")); }
                if (Canvas.SelectedFaces.Count > 0) Description(L.F($"Surface : {Document.Faces().Where(f => Canvas.SelectedFaces.Contains(f.Key)).Sum(f => f.Area):0.##} u²"));

            }
            else Description(L.T("Aucune sélection"));
            if (ids.Count > 0)
            {
                Divider(); Heading(L.T("GROUPES"));
                var groups = Document.MatchingGroups(Canvas.SelectedVertices, Canvas.SelectionEdges());
                if (groups.Count > 0) Description(string.Join(" · ", groups.Select(g => g.Name)));
                else ToolHint(L.T("Sélectionnez plusieurs éléments puis regroupez-les pour les manipuler ensemble."));
                inspector.Children.Add(Button(L.T("Grouper  ·  Ctrl+G"), GroupSelection));
                if (groups.Count > 0) { var ungroup = Button(L.T("Dissocier  ·  Ctrl+Maj+G"), UngroupSelection); ungroup.Margin = new Thickness(0, 6, 0, 0); inspector.Children.Add(ungroup); }
            }
        }
        else if (Canvas.Tool == EditorTool.Annotation) AnnotationInspector();
        else if (Canvas.Tool == EditorTool.Draw)
        {
            ToolHint(L.T("Placez les points sur les intersections de la grille. Chaque clic prolonge le contour."));
            ToolHint(L.T("Revenez au premier point pour fermer une zone. Échap ou clic droit termine le tracé."));
            var card = new Border { Background = B("#243B38"), CornerRadius = new CornerRadius(5), Padding = new Thickness(12), Child = new TextBlock { Text = L.F($"Aimantation active  ·  {Document.Grid} u"), Foreground = B("#94DCC8"), FontSize = 12 } }; inspector.Children.Add(card);
        }
        else if (Canvas.Tool == EditorTool.Shapes)
        {
            ModeButtons(new[] { "Rectangle", L.T("Cercle"), L.T("Escaliers") }, (int)Canvas.Shape, i => { Canvas.CancelGesture(); Canvas.Shape = (ShapeKind)i; UpdateInspector(); Canvas.Focus(); });
            if (Canvas.Shape == ShapeKind.Stairs) StairInspector();
            else ToolHint(Canvas.Shape == ShapeKind.Rectangle ? L.T("Placez deux coins opposés, ou cliquez-glissez pour créer un rectangle de quatre points.") : L.T("Placez le centre, puis le rayon, ou cliquez-glissez. Le contour est un polygone régulier."));
            if (Canvas.Shape == ShapeKind.Circle)
            {
                var count = Field(L.T("Points"), Canvas.CirclePoints.ToString(CultureInfo.InvariantCulture));
                count.TextChanged += (_, _) => { if (int.TryParse(count.Text, out int n) && n >= 3 && n <= 128) { Canvas.CirclePoints = n; Canvas.InvalidateVisual(); } };
                count.LostKeyboardFocus += (_, _) => { if (!int.TryParse(count.Text, out int n) || n < 3 || n > 128) { count.Text = Canvas.CirclePoints.ToString(CultureInfo.InvariantCulture); Notify(L.T("Le cercle doit avoir entre 3 et 128 points.")); } };
                AddStepper(count, L.T("Points"), 3, 128, () => Canvas.CirclePoints, integer: true);
                ToolHint(L.T("De 3 à 128 points. Le centre et le rayon suivent la grille ; les sommets conservent un cercle régulier."));
            }
            ToolHint(L.T("Échap annule la forme en cours. Une forme complète s’annule en une seule étape avec Ctrl+Z."));
        }
        else if (Canvas.Tool == EditorTool.Erase) ToolHint(L.T("Cliquez sur une arête pour la supprimer, ou à l’intérieur d’une zone pour effacer son remplissage. Ctrl+Z annule l’opération."));
        else
        {
            ModeButtons(new[] { L.T("Remplir une zone"), L.T("Colorer les lignes") }, Canvas.PaintLines ? 1 : 0, i => { Canvas.PaintLines = i == 1; UpdateInspector(); });
            ToolHint(Canvas.PaintLines ? L.T("Cliquez sur une arête pour lui appliquer la couleur.") : L.T("Cliquez dans un contour fermé pour remplir la zone."));
            PaintStyleInspector();
            var palette = new PaletteView(SavedColors, color => { Canvas.PaintColor = color; UpdateInspector(); }, () => Canvas.PaintColor, Notify) { Margin = new Thickness(0, 1, 0, 12) };
            inspector.Children.Add(palette);
            var wheel = Button(L.T("◉  Roue de couleurs…"), () => ChooseColor(Canvas.PaintColor, color => { Canvas.PaintColor = color; UpdateInspector(); }));
            wheel.Margin = new Thickness(0, 0, 0, 10); inspector.Children.Add(wheel);
            var hex = Field(L.T("Couleur"), Canvas.PaintColor);
            inspector.Children.Add(Button(L.T("Utiliser cette couleur"), () => { try { var c = (Color)ColorConverter.ConvertFromString(hex.Text.Trim()); Canvas.PaintColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; UpdateInspector(); } catch { Notify(L.T("Couleur invalide. Exemple : #66C5AD")); } }));
            if (Canvas.SelectionPoints().Count > 0)
            {
                var apply = Button(L.T("Colorer la sélection"), () => Mutate(() => { if (Canvas.PaintLines) foreach (var e in Document.Edges.Where(e => Canvas.SelectedEdges.Contains(e.Id))) { e.Color = Canvas.PaintColor; e.Style = Canvas.PaintLineStyle; } else foreach (var f in Canvas.SelectedFaces) { Document.Fills[f] = Canvas.PaintColor; Document.FillPatterns[f] = Canvas.PaintPattern; } })); apply.Margin = new Thickness(0, 8, 0, 0); inspector.Children.Add(apply);
            }
            WallColorControls();
        }
        UpdateTitle(); return;
        }
        InspectorTitle(L.T("IMAGE DE RÉFÉRENCE"));
        if (Document.Background is { } bg)
        {
            Description(bg.Name);
            var visible = new CheckBox { Content = L.T("Afficher sous la grille"), IsChecked = bg.Visible }; visible.Click += (_, _) => Mutate(() => bg.Visible = visible.IsChecked == true); inspector.Children.Add(visible);
            var op = Field(L.T("Opacité %"), F(bg.Opacity * 100)); var x = Field(L.T("Origine X"), F(bg.X)); var y = Field(L.T("Origine Y"), F(bg.Y)); var w = Field(L.T("Largeur (u)"), F(bg.Width));
            inspector.Children.Add(Button(L.T("Appliquer à l’image"), () => { if (Number(op, out var opacity) && Number(x, out var nx) && Number(y, out var ny) && Number(w, out var width) && width > 0) Mutate(() => { bg.Height *= width / bg.Width; bg.Width = width; bg.X = nx; bg.Y = ny; bg.Opacity = Math.Clamp(opacity / 100, 0, 1); }); }));
            var remove = Button(L.T("Retirer l’image"), () => Mutate(() => Document.Background = null)); remove.Margin = new Thickness(0, 7, 0, 0); inspector.Children.Add(remove);
        }
        else
        {
            ToolHint(L.T("Superposez votre plan à une esquisse, une carte ou une capture de niveau.")); inspector.Children.Add(Button(L.T("＋  Importer une image"), ImportImage)); ToolHint(L.T("\nPNG, JPG, BMP · image intégrée au projet"));
        }
        ToolHint(L.T("Molette   Zoom\nClic milieu / droit   Déplacer la vue\nEspace + glisser   Déplacer la vue\nFlèches   Déplacer la vue\n1–0   Changer le pas de grille\nA   Cadrer le plan\nCtrl+S   Enregistrer   ·   Ctrl+Z   Annuler"));
        UpdateTitle();
    }
    private void UpdateLevels()
    {
        levelsPanel.Children.Clear();
        levelsPanel.Children.Add(new TextBlock { Text = L.T("NIVEAUX"), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = B("#8596AB"), Margin = new Thickness(0, 0, 0, 10) });
        levelsPanel.Children.Add(new LevelPreview(Document.Levels, Document.ActiveLevel) { Height = compactLevels ? 96 : 132 });
        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        var down = Button("↓", () => ChangeAdjacentLevel(-1)); down.Padding = new Thickness(0); down.IsEnabled = Document.ActiveLevel > -999; down.ToolTip = L.T("Passer au niveau inférieur (créé si nécessaire) · Page ↓"); row.Children.Add(down);
        var choice = new ComboBox { Margin = new Thickness(6, 0, 6, 0), Height = 32, SelectedValuePath = "Tag" };
        foreach (int level in Document.Levels) choice.Items.Add(new ComboBoxItem { Content = L.T("Niveau ") + level, Tag = level });
        choice.SelectedValue = Document.ActiveLevel; choice.SelectionChanged += (_, _) => { if (choice.SelectedValue is int level && level != Document.ActiveLevel) ChangeLevel(level); }; Grid.SetColumn(choice, 1); row.Children.Add(choice);
        var up = Button("↑", () => ChangeAdjacentLevel(1)); up.Padding = new Thickness(0); up.IsEnabled = Document.ActiveLevel < 999; up.ToolTip = L.T("Passer au niveau supérieur (créé si nécessaire) · Page ↑"); Grid.SetColumn(up, 2); row.Children.Add(up); levelsPanel.Children.Add(row);
        var delete = Button(L.T("Supprimer ce niveau"), DeleteActiveLevel); delete.FontSize = 11; delete.Padding = new Thickness(5); delete.Margin = new Thickness(0, 7, 0, 0); delete.Foreground = B("#E3A0A9"); levelsPanel.Children.Add(delete);
        LevelHeightControls();
        if (Canvas.SelectionPoints().Count > 0 || Canvas.SelectedAnnotation != null)
        {
            levelsPanel.Children.Add(new TextBlock { Text = L.T("AFFECTER LA SÉLECTION AU NIVEAU"), FontSize = 9, Foreground = B("#A9B9CC"), Margin = new Thickness(0, 14, 0, 7) });
            var assign = new DockPanel();
            var input = new TextBox { Text = Document.ActiveLevel.ToString(CultureInfo.InvariantCulture), MinWidth = 60, ToolTip = L.T("Numéro de niveau, de -999 à 999") };
            var apply = Button(L.T("Affecter"), () =>
            {
                if (int.TryParse(input.Text, out int target) && target >= -999 && target <= 999) AssignSelectionToLevel(target);
                else Notify(L.T("Le niveau doit être un entier compris entre -999 et 999."));
            }); apply.Margin = new Thickness(7, 0, 0, 0); DockPanel.SetDock(apply, Dock.Right); assign.Children.Add(apply); assign.Children.Add(input); levelsPanel.Children.Add(assign);
            if (Canvas.SelectedVertices.Count > 0) apply.ToolTip = L.T("Les points emportent leurs arêtes incidentes.");
        }
        levelsPanel.Children.Add(new Border { Height = 1, Background = B("#303D4C"), Margin = new Thickness(0, 16, 0, 0) });
    }
    public void ChangeLevel(int level)
    {
        if (level < -999 || level > 999 || level == Document.ActiveLevel) return;
        Canvas.CancelGesture(); Canvas.ClearSelection(); Document.EnsureLevel(level); Document.ActiveLevel = level;
        Canvas.RefreshGeometry(); UpdateInspector(); UpdateStatus(); Canvas.Focus();
    }
    public void ChangeAdjacentLevel(int direction)
    {
        var next = Document.Levels.Where(l => direction > 0 ? l > Document.ActiveLevel : l < Document.ActiveLevel).OrderBy(l => Math.Abs(l - Document.ActiveLevel)).Cast<int?>().FirstOrDefault();
        ChangeLevel(next ?? Document.ActiveLevel + Math.Sign(direction));
    }
    public void DeleteActiveLevel()
    {
        Canvas.CancelGesture(); int level = Document.ActiveLevel; int count = Document.Edges.Count(e => Document.LevelOf(e) == level);
        if ((count > 0 || Document.Annotations.Any(a => a.Level == level)) && !TestMode && AppDialog.Show(this, L.F($"Supprimer le niveau {level} et ses {count} arêtes, ainsi que ses remplissages et annotations ?\n\nCtrl+Z permet d’annuler cette suppression."), L.T("Supprimer un niveau"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        Mutate(() => { Canvas.ClearSelection(); Document.DeleteLevel(level); }); Canvas.Focus();
    }
    public void MoveSelection(double x, double y)
    {
        if (Canvas.SelectedAnnotation is int noteId) { Mutate(() => { var note = Document.Annotations.First(a => a.Id == noteId); note.X += x; note.Y += y; }); return; }
        var ids = Canvas.SelectionPoints(); if (ids.Count == 0 || (x == 0 && y == 0)) return;
        TransformSelection(p => p + new V2(x, y));
    }
    public void GroupSelection()
    {
        Canvas.CancelGesture();
        if (Canvas.SelectionPoints().Count == 0) { Notify(L.T("Sélectionnez les éléments à grouper.")); return; }
        Mutate(() => { Document.GroupSelection(Canvas.SelectedVertices, Canvas.SelectionEdges()); Canvas.ExpandGroups(); }); Canvas.Focus();
    }
    public void UngroupSelection()
    {
        Canvas.CancelGesture(); var groups = Document.MatchingGroups(Canvas.SelectedVertices, Canvas.SelectionEdges());
        if (groups.Count == 0) return;
        Mutate(() => Document.Groups.RemoveAll(g => groups.Contains(g))); Canvas.Focus();
    }
    public void AssignSelectionToLevel(int level)
    {
        if (level == Document.ActiveLevel) return;
        if (Canvas.SelectedAnnotation is int noteId) { Canvas.CancelGesture(); Mutate(() => { Document.EnsureLevel(level); Document.Annotations.First(a => a.Id == noteId).Level = level; Document.ActiveLevel = level; }); return; }
        if (Canvas.SelectionPoints().Count == 0) return;
        Canvas.CancelGesture();
        Mutate(() =>
        {
            var result = Document.TransferToLevel(Canvas.SelectedVertices, Canvas.SelectedEdges, Canvas.SelectedFaces, level);
            Document.ActiveLevel = level; Canvas.ClearSelection();
            if (Canvas.Mode == SelectionMode.Vertex) Canvas.SelectedVertices.UnionWith(result.Points);
            else if (Canvas.Mode == SelectionMode.Face) Canvas.SelectedFaces.UnionWith(result.Faces);
            else Canvas.SelectedEdges.UnionWith(result.Edges);
        }); Canvas.Focus();
    }
    public void Escape()
    {
        Canvas.CancelGesture(); Canvas.ClearSelection();
        SetTool(EditorTool.Select);
    }
    private static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private bool Number(TextBox box, out double n)
    {
        if (double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out n) && double.IsFinite(n) && Math.Abs(n) <= 1e7) return true;
        Notify(L.T("Saisissez une coordonnée numérique valide.")); box.Focus(); box.SelectAll(); return false;
    }
    public void Mutate(Action action) { Canvas.Anchor = null; var before = Document.Serialize(); action(); Commit(before); }
    public void Commit(string before)
    {
        if (before != Document.Serialize()) History.Push(before);
        Canvas.RefreshGeometry(); UpdateInspector(); UpdateStatus();
    }
    private bool Dirty => saved != Document.Serialize();
    private void UpdateTitle() => Title = "Coat — " + (string.IsNullOrEmpty(file) ? L.T("Sans titre") : Path.GetFileName(file)) + (Dirty ? " *" : "");
    public void UpdateStatus(V2? p = null)
    {
        string[] hints = { L.T("V  Sélection  ·  Translation / Rotation / Échelle à droite  ·  Ctrl+G  Grouper"), L.T("D  Dessin  ·  Clic  Placer un point  ·  Échap  Terminer"), L.T("E  Gomme  ·  Clic  Effacer  ·  Ctrl+Z  Annuler"), L.T("C  Couleur  ·  Clic  Appliquer la couleur"), L.T("Aucun outil  ·  D  Dessin  ·  V  Sélection  ·  Molette  Zoom"), L.T("R  Shapes  ·  Deux clics ou glisser  ·  Échap  Annuler"), L.T("T  Annotation  ·  Clic  Placer ou sélectionner  ·  Glisser  Déplacer") };
        statusLeft.Text = hints[(int)Canvas.Tool];
        if (Shortcuts.Bindings.Any(pair => pair.Value != ShortcutSettings.Actions.First(a => a.Label == pair.Key).Default)) statusLeft.Text = L.T("Raccourcis personnalisés · Édition → Préférences pour consulter les touches"); statusRight.Text = (p != null ? L.F($"X {p.Value.X:0.##}   Y {p.Value.Y:0.##}     |     ") : "") + L.F($"Niveau {Document.ActiveLevel}     |     Pas {Document.Grid} u     |     {Canvas.Zoom * 100:0}%");
    }
    public void Notify(string text) { statusLeft.Text = text; }
    private void SetGrid(int step)
    {
        Document.Grid = step; updating = true; gridChoice.SelectedIndex = Array.IndexOf(steps, step); updating = false; Canvas.InvalidateVisual(); UpdateInspector(); UpdateStatus();
    }
    public void Undo() { Canvas.CancelGesture(); var p = History.Undo(Document); if (p != null) ReplaceFromHistory(p); }
    public void Redo() { Canvas.CancelGesture(); var p = History.Redo(Document); if (p != null) ReplaceFromHistory(p); }
    private void ReplaceFromHistory(Plan p) { Document = p; Canvas.ClearSelection(); Canvas.RefreshGeometry(); SetGrid(p.Grid); }
    public void Duplicate()
    {
        if (Canvas.SelectedAnnotation is int noteId) { Mutate(() => { var a = Document.Annotations.First(n => n.Id == noteId); var copy = new Annotation { Id = Document.NextId++, Text = a.Text, X = a.X + Document.Grid, Y = a.Y - Document.Grid, Size = a.Size, Color = a.Color, Level = a.Level }; Document.Annotations.Add(copy); Canvas.SelectedAnnotation = copy.Id; }); return; }
        var ids = Canvas.SelectionPoints(); if (ids.Count == 0) return;
        Mutate(() =>
        {
            var old = Document.Edges.Where(e => ids.Contains(e.A) && ids.Contains(e.B)).ToList();
            var oldFaces = Document.Faces().Where(f => f.Vertices.All(ids.Contains) && Document.Fills.ContainsKey(f.Key)).Select(f => (f.Vertices, Color: Document.Fills[f.Key], Pattern: Document.FillPatterns.GetValueOrDefault(f.Key))).ToList();
            var map = ids.ToDictionary(id => id, id => Document.Point(Document.Get(id).P + new V2(Document.Grid, -Document.Grid)));
            var edgeMap = old.ToDictionary(e => e.Id, e => Document.AddEdge(map[e.A], map[e.B], e.Color));
            foreach (var e in old) Document.Edges.First(x => x.Id == edgeMap[e.Id]).Style = e.Style;
            Document.CopyGroups(map, edgeMap);
            foreach (var id in map.Values.Where(id => !Document.Edges.Any(e => e.A == id || e.B == id))) Document.Get(id).KeepPoint = true;
            Canvas.ClearSelection(); Canvas.SelectedEdges.UnionWith(edgeMap.Values); if (edgeMap.Count == 0) Canvas.SelectedVertices.UnionWith(map.Values);
            foreach (var face in oldFaces) { var key = string.Join("-", face.Vertices.Select(id => map[id]).Order()); Document.Fills[key] = face.Color; Document.FillPatterns[key] = face.Pattern; }
            Document.Normalize();
        });
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (e.OriginalSource is TextBox && key != Key.Escape) return;
        var modifiers = Keyboard.Modifiers;
        if (!Shortcuts.Translate(ref key, ref modifiers)) return;
        bool ctrl = (modifiers & ModifierKeys.Control) != 0, shift = (modifiers & ModifierKeys.Shift) != 0;
        if (ctrl)
        {
            switch (key)
            {
                case Key.S: Canvas.CancelGesture(); Save(shift); break;
                case Key.Z: if (shift) Redo(); else Undo(); break;
                case Key.Y: Redo(); break;
                case Key.O: Open(); break;
                case Key.N: New(); break;
                case Key.I: ImportImage(); break;
                case Key.A: Canvas.SelectAll(); break;
                case Key.D: Duplicate(); break;
                case Key.C: CopySelection(); break;
                case Key.X: CutSelection(); break;
                case Key.V: PasteSelection(); break;
                case Key.G: if (shift) UngroupSelection(); else GroupSelection(); break;
                case Key.OemComma: ShowPreferences(); break;
                default: return;
            }
        }
        else if (key >= Key.D0 && key <= Key.D9) SetGrid(steps[key == Key.D0 ? 9 : (int)key - (int)Key.D1]);
        else if (key >= Key.NumPad0 && key <= Key.NumPad9) SetGrid(steps[key == Key.NumPad0 ? 9 : (int)key - (int)Key.NumPad1]);
        else switch (key)
        {
            case Key.V: SetTool(EditorTool.Select); break;
            case Key.D: SetTool(EditorTool.Draw); break;
            case Key.E: SetTool(EditorTool.Erase); break;
            case Key.C: SetTool(EditorTool.Paint); break;
            case Key.R: if (shift) { SetTool(EditorTool.Select); Canvas.SetTransform(TransformKind.Rotation); } else SetTool(EditorTool.Shapes); break;
            case Key.W: SetTool(EditorTool.Select); Canvas.SetTransform(TransformKind.Translation); break;
            case Key.S: SetTool(EditorTool.Select); Canvas.SetTransform(TransformKind.Scale); break;
            case Key.T: SetTool(EditorTool.Annotation); break;
            case Key.P: Canvas.Mode = SelectionMode.Vertex; Canvas.ClearSelection(); SetTool(EditorTool.Select); break;
            case Key.L: Canvas.Mode = SelectionMode.Edge; Canvas.ClearSelection(); SetTool(EditorTool.Select); break;
            case Key.F: Canvas.Mode = SelectionMode.Face; Canvas.ClearSelection(); SetTool(EditorTool.Select); break;
            case Key.Escape: if (!e.IsRepeat) Escape(); break;
            case Key.PageUp: ChangeAdjacentLevel(1); break;
            case Key.PageDown: ChangeAdjacentLevel(-1); break;
            case Key.Delete: case Key.Back: Canvas.DeleteSelection(); break;
            case Key.Home: Canvas.Fit(); break;
            case Key.G: Canvas.ShowGrid = !Canvas.ShowGrid; break;
            case Key.M: Canvas.ShowLengths = !Canvas.ShowLengths; break;
            case Key.H: if (!e.IsRepeat) { if (shift) ToggleLowerLevels(); else ToggleUpperLevels(); } break;
            case Key.Left: Canvas.Center += new V2(-80 / Canvas.Zoom * (shift ? 3 : 1), 0); break;
            case Key.Right: Canvas.Center += new V2(80 / Canvas.Zoom * (shift ? 3 : 1), 0); break;
            case Key.Up: Canvas.Center += new V2(0, 80 / Canvas.Zoom * (shift ? 3 : 1)); break;
            case Key.Down: Canvas.Center += new V2(0, -80 / Canvas.Zoom * (shift ? 3 : 1)); break;
            case Key.F1: Help(); break;
            case Key.LeftShift: case Key.RightShift: Canvas.InvalidateVisual(); return;
            default: return;
        }
        Canvas.InvalidateVisual(); UpdateStatus(); e.Handled = true;
    }
    private bool CheckSave()
    {
        if (!Dirty) return true;
        var answer = AppDialog.Show(this, L.T("Enregistrer les modifications du plan avant de continuer ?"), "Coat", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || (answer == MessageBoxResult.Yes && Save());
    }
    private void New() { Canvas.CancelGesture(); if (!CheckSave()) return; LoadDocument(new Plan(), ""); }
    private void Open()
    {
        Canvas.CancelGesture(); if (!CheckSave()) return;
        var dlg = new OpenFileDialog { Filter = L.T("Plan Coat (*.coat)|*.coat"), Title = L.T("Ouvrir un plan") };
        if (dlg.ShowDialog(this) == true) OpenPath(dlg.FileName);
    }
    public void OpenPath(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 70 * 1024 * 1024) throw new InvalidDataException(L.T("Ce plan dépasse la limite de 70 Mo."));
            var plan = Plan.Parse(File.ReadAllText(path)); if (plan.Background != null) EditorCanvas.DecodeImage(plan.Background.Data);
            LoadDocument(plan, path);
        }
        catch (Exception ex) { Error(L.T("Impossible d’ouvrir le plan"), ex); }
    }
    public void LoadDocument(Plan plan, string path)
    {
        Canvas.CancelGesture(); Document = plan; file = path; saved = plan.Serialize(); History.Clear(); Canvas.ClearSelection(); Canvas.Center = new V2(plan.ViewX, plan.ViewY); Canvas.Zoom = plan.Zoom; Canvas.RefreshGeometry(); SetGrid(plan.Grid); UpdateInspector(); UpdateStatus();
    }
    private void LoadDemo() { Canvas.CancelGesture(); if (!CheckSave()) return; LoadDocument(Plan.Demo(), ""); saved = new Plan().Serialize(); Canvas.Fit(); UpdateTitle(); }
    public bool Save(bool saveAs = false)
    {
        Canvas.CancelGesture(); string path = file;
        if (saveAs || string.IsNullOrEmpty(path))
        {
            var dlg = new SaveFileDialog { Filter = L.T("Plan Coat (*.coat)|*.coat"), DefaultExt = ".coat", FileName = string.IsNullOrEmpty(file) ? L.T("Sans titre.coat") : Path.GetFileName(file), Title = L.T("Enregistrer le plan") };
            if (dlg.ShowDialog(this) != true) return false; path = dlg.FileName;
        }
        try
        {
            Document.ViewX = Canvas.Center.X; Document.ViewY = Canvas.Center.Y; Document.Zoom = Canvas.Zoom;
            WriteAtomic(path, Document.Serialize()); file = path; saved = Document.Serialize(); UpdateInspector(); Notify(L.T("Plan enregistré : ") + Path.GetFileName(path)); return true;
        }
        catch (Exception ex) { Error(L.T("Impossible d’enregistrer le plan"), ex); return false; }
    }
    public static void WriteAtomic(string path, string contents)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, contents, new UTF8Encoding(false)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private void ImportImage()
    {
        Canvas.CancelGesture();
        var dlg = new OpenFileDialog { Filter = L.T("Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"), Title = L.T("Importer une image de référence") };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dlg.FileName).Length > 25 * 1024 * 1024) throw new InvalidDataException(L.T("Choisissez une image de moins de 25 Mo."));
            string data = Convert.ToBase64String(File.ReadAllBytes(dlg.FileName)); var bmp = EditorCanvas.DecodeImage(data);
            if ((long)bmp.PixelWidth * bmp.PixelHeight > 64000000) throw new InvalidDataException(L.T("Choisissez une image de moins de 64 mégapixels."));
            var width = Math.Max(256, Math.Round(1024.0 / Document.Grid) * Document.Grid); double height = width * bmp.PixelHeight / bmp.PixelWidth;
            Mutate(() => Document.Background = new ReferenceImage { Data = data, Name = Path.GetFileName(dlg.FileName), X = Canvas.Center.X - width / 2, Y = Canvas.Center.Y + height / 2, Width = width, Height = height });
        }
        catch (Exception ex) { Error(L.T("Impossible d’importer l’image"), ex); }
    }
    private void ExportPng()
    {
        var dlg = new SaveFileDialog { Filter = L.T("Vue du plan avec échelle (*.png)|*.png"), DefaultExt = ".png", FileName = "Plan.png", Title = L.T("Exporter la grille et le plan avec l’échelle") }; if (dlg.ShowDialog(this) != true) return;
        try { ExportPngTo(dlg.FileName); Notify(L.T("Grille et plan exportés en PNG avec l’échelle.")); }
        catch (Exception ex) { Error(L.T("Impossible d’exporter l’image"), ex); }
    }
    public void ExportPngTo(string path)
    {
        Canvas.CancelGesture();
        bool exporting = Canvas.Exporting, guides = Canvas.ExportGuides;
        try
        {
            Canvas.Exporting = true; Canvas.ExportGuides = true; Canvas.InvalidateVisual(); UpdateLayout(); Canvas.UpdateLayout();
            SaveVisual(Canvas, path, 2);
        }
        finally { Canvas.Exporting = exporting; Canvas.ExportGuides = guides; Canvas.InvalidateVisual(); UpdateStatus(); }
    }
    public static void SaveVisual(FrameworkElement visual, string path, double scale = 1)
    {
        visual.UpdateLayout(); var target = new RenderTargetBitmap(Math.Max(1, (int)(visual.ActualWidth * scale)), Math.Max(1, (int)(visual.ActualHeight * scale)), 96 * scale, 96 * scale, PixelFormats.Pbgra32); target.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target)); using var stream = File.Create(path); encoder.Save(stream);
    }
    private void ExportSvg()
    {
        var dlg = new SaveFileDialog { Filter = L.T("Plan vectoriel SVG (*.svg)|*.svg"), FileName = "Plan.svg" }; if (dlg.ShowDialog(this) != true) return;
        try { File.WriteAllText(dlg.FileName, Svg(Document, Canvas.ShowUpperLevels, Canvas.ShowLowerLevels), Encoding.UTF8); Notify(L.T("Plan vectoriel SVG exporté.")); } catch (Exception ex) { Error(L.T("Impossible d’exporter le SVG"), ex); }
    }
    public static string Svg(Plan plan, bool showUpperLevels = false, bool showLowerLevels = true)
    {
        var pts = plan.Vertices.Where(v => v.Level == plan.ActiveLevel || (v.Level < plan.ActiveLevel ? showLowerLevels : showUpperLevels)).Select(v => v.P).Concat(plan.AnnotationBounds(showLowerLevels)).ToList(); if (plan.Background is { Visible: true } bg) { pts.Add(new V2(bg.X, bg.Y)); pts.Add(new V2(bg.X + bg.Width, bg.Y - bg.Height)); } if (pts.Count == 0) { pts.Add(new(-256, -256)); pts.Add(new(256, 256)); }
        double x = pts.Min(p => p.X) - 64, y = -pts.Max(p => p.Y) - 64, w = pts.Max(p => p.X) - x + 64, h = -pts.Min(p => p.Y) - y + 64;
        string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? "";
        var s = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{F(x)} {F(y)} {F(w)} {F(h)}\"><title>Coat plan</title><rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#181F2A\"/>");
        if (plan.Background is { Visible: true } b)
        {
            var bytes = Convert.FromBase64String(b.Data); string mime = bytes.Length > 2 && bytes[0] == 0xFF ? "image/jpeg" : bytes.Length > 2 && bytes[0] == 0x42 ? "image/bmp" : "image/png";
            s.Append($"<image href=\"data:{mime};base64,{b.Data}\" x=\"{F(b.X)}\" y=\"{F(-b.Y)}\" width=\"{F(b.Width)}\" height=\"{F(b.Height)}\" opacity=\"{F(b.Opacity)}\"/>");
        }
        s.Append(DecorationSvg.PatternDefinitions());
        var exportFaces = plan.Faces();
        foreach (int level in plan.Levels.Where(l => l == plan.ActiveLevel || (showLowerLevels && l < plan.ActiveLevel)).Order())
        {
            s.Append($"<g data-level=\"{level}\" opacity=\"{F(plan.LevelOpacity(level))}\">");
            foreach (var f in exportFaces.Where(f => f.Level == level).OrderByDescending(f => f.Area)) if (plan.Fills.TryGetValue(f.Key, out var color))
            {
                var points = string.Join(" ", f.Vertices.Select(id => F(plan.Get(id).X) + "," + F(-plan.Get(id).Y)));
                s.Append(DecorationSvg.Polygon(points, color, plan.FillOpacity(level), plan.FillPatterns.GetValueOrDefault(f.Key)));
            }
            foreach (var e in plan.Edges.Where(e => plan.LevelOf(e) == level)) { var a = plan.Get(e.A); var endpoint = plan.Get(e.B); s.Append($"<line x1=\"{F(a.X)}\" y1=\"{F(-a.Y)}\" x2=\"{F(endpoint.X)}\" y2=\"{F(-endpoint.Y)}\" stroke=\"{Escape(e.Color)}\" stroke-width=\"2\"{DecorationSvg.Dash(e.Style)}/>"); }
            foreach (var note in plan.Annotations.Where(a => a.Level == level)) s.Append(DecorationSvg.Text(note));
            s.Append("</g>");
        }
        if (showUpperLevels)
        {
            s.Append("<g data-upper-levels=\"true\" fill=\"none\" stroke=\"#FF2020\" stroke-width=\"0.75\">");
            foreach (var edge in plan.Edges.Where(e => plan.LevelOf(e) > plan.ActiveLevel))
            {
                var a = plan.Get(edge.A); var upperEnd = plan.Get(edge.B);
                s.Append($"<line x1=\"{F(a.X)}\" y1=\"{F(-a.Y)}\" x2=\"{F(upperEnd.X)}\" y2=\"{F(-upperEnd.Y)}\" vector-effect=\"non-scaling-stroke\"/>");
            }
            s.Append("</g>");
        }
        return s.Append("</svg>").ToString();
    }
    private void Error(string title, Exception ex) { if (TestMode) throw new InvalidOperationException(title, ex); AppDialog.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); }
    private void Help() => AppDialog.Show(this,
        L.T("Coat 1.0.5 — Epicteta Lupercal\n\nRACCOURCIS PAR DÉFAUT\nV : Sélection · D : Dessin · R : Shapes\nC : Couleur · E : Gomme · T : Annotation\nZ : Points · X : Arêtes · F : Zones\nW : Translation · Maj+R : Rotation · Maj+S : Échelle\n\nÉchap annule le geste, retire la sélection et revient à Sélection.\nClic droit termine un tracé en conservant l’outil.\nCtrl+clic ajoute à la sélection. Maj+glisser une arête crée une extrusion.\nCtrl+C / X / V : copier / couper / coller\nCtrl+G : grouper · Ctrl+Maj+G : dissocier\nCtrl+Z : annuler · Ctrl+Y : rétablir\n\nVUE ET NIVEAUX\nMolette : zoom · Flèches : déplacer la vue\nClic milieu/droit ou Espace+glisser : déplacer la vue\nA : cadrer · G : grille · M : longueurs\n1–0 : pas de grille de 1 à 512 unités\nPage Up / Down : changer de niveau\nH : niveaux supérieurs · Maj+H : niveaux inférieurs\n\nFORMES ET PRÉFÉRENCES\nCercles et escaliers : saisie numérique, boutons ▴/▾ ou touches Haut/Bas dans le champ.\nCtrl+, ou Édition → Préférences : langue, recherche de raccourcis et préréglages Ergonomique / Classique.\n\nCtrl+S : enregistrer · Ctrl+O : ouvrir · Ctrl+N : nouveau\nFichier : importer une image, exporter en PNG, SVG ou .map.\nCliquez à nouveau sur l’outil actif pour afficher les réglages de l’image de référence."),
        L.T("Prise en main de Coat"), MessageBoxButton.OK, MessageBoxImage.Information);
}
