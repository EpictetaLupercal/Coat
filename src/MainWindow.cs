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
    private StackPanel levelsPanel = new() { Margin = new Thickness(12, 9, 12, 0) };
    private TextBlock statusLeft = new(), statusRight = new();
    private readonly PresentationMonitor presentationMonitor = new();
    private readonly Dictionary<EditorTool, Button> toolButtons = new();
    private ComboBox gridChoice = new();
    private readonly int[] steps = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 };
    private PlanSnapshot saved;
    private string file = "";
    private string moveXText = "0", moveYText = "0";
    private (long Length, DateTime Written)? openFileVersion;
    private bool updating;
    private bool compactLevels;
    private MenuDecoration? topDecoration, inspectorDecoration;
    private double inspectorWidth = 276;
    private readonly CommandHints hints;
    public bool TestMode { get; set; }
    internal Brush ChromeBrush(string color) => B(color);
    private Brush B(string c) => EditorCanvas.Brush(ChromeColor(c));
    public MainWindow()
    {
        hints = new CommandHints(this);
        Title = AppVersion.Title; Width = 1440; Height = 900; MinWidth = 1050; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = B("#141B25"); Foreground = B("#DCE4ED"); FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        SetTheme(); Icon = OfficialLogo; Canvas = new EditorCanvas(this); Language = XmlLanguage.GetLanguage(L.Culture.Name);
        BuildInterface();
        saved = PlanSnapshot.Capture(Document); Canvas.RefreshGeometry(); UpdateInspector(); UpdateToolButtons(); UpdateStatus();
        hints.Listen(this,this); PreviewKeyDown += OnKey; PreviewKeyUp += (_, _) => Canvas.InvalidateVisual();
        PreviewTextInput += OnFocusTextInput;
        Closing += (_, e) => { Canvas.CancelGesture(); if (!TestMode && !CheckSave()) e.Cancel = true; };
        Closed += (_, _) => { Canvas.GlPlan.Close(); presentationMonitor.Dispose(); editorSounds.Dispose(); };
        Loaded += (_, _) => { if (!TestMode) presentationMonitor.Start(); Canvas.Focus(); };
        SizeChanged += (_, _) => { bool compact = ActualHeight < 760; if (compact != compactLevels) { compactLevels = compact; if (inspectorPage == InspectorPage.Plan) UpdateLevels(); } };
    }
    private void BuildInterface()
    {
        if (VisualTreeHelper.GetParent(Canvas.GlPlan) is Panel glParent) glParent.Children.Remove(Canvas.GlPlan);
        if (VisualTreeHelper.GetParent(Canvas) is Panel parent) parent.Children.Remove(Canvas);
        if (VisualTreeHelper.GetParent(Canvas.Feedback) is Panel feedbackParent) feedbackParent.Children.Remove(Canvas.Feedback);
        inspector = new(); levelsPanel = new() { Margin = new Thickness(0, 0, 0, 10) };
        statusLeft = new(); statusRight = new(); gridChoice = new(); toolButtons.Clear(); shortcutMenus.Clear(); menuCommands.Clear(); featureMenus.Clear();
        var root = new Grid { Background = B("#141B25") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
        Content = root; hints.Install(root);
        var toolbarBorder = new Border { Background = B("#1B2430"), BorderBrush = B("#2B3645"), BorderThickness = new Thickness(0, 1, 0, 1) };
        Grid.SetRow(toolbarBorder, 1);
        root.Children.Add(toolbarBorder);
        topDecoration = Shortcuts.AnimatedMenus ? new MenuDecoration(MenuSurface.Top, IsBordeaux) : null;
        if (topDecoration != null)
        {
            Grid.SetRowSpan(topDecoration, 2);
            root.Children.Add(topDecoration);
        }
        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(14, 0, 16, 0) };
        var brand = new Image
        {
            Source = HeaderLogo, Width = 48, Height = 34, Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0)
        };
        System.Windows.Automation.AutomationProperties.SetName(brand, "Coat");
        header.Children.Add(brand);
        var menu = new Menu { Background = MenuBack, Foreground = MenuInk, VerticalAlignment = VerticalAlignment.Center };
        var fm = Menu(L.T("Fichier")); Add(fm, L.T("Nouveau plan"), "Ctrl+N", New); Add(fm, L.T("Ouvrir…"), "Ctrl+O", Open); recentMenu = DropDownItem(L.T("Ouvert récemment")); fm.Items.Add(recentMenu); UpdateRecentMenu(); Add(fm, L.T("Enregistrer"), "Ctrl+S", () => Save()); Add(fm, L.T("Enregistrer sous…"), "Ctrl+Maj+S", () => Save(true)); fm.Items.Add(new Separator()); Add(fm, L.T("Importer une image…"), "Ctrl+I", ImportImage); Add(fm, L.T("Exporter en PNG…"), "", ExportPng); Add(fm, L.T("Exporter en SVG…"), "", ExportSvg); Add(fm, L.T("Exporter en .map…"), "", ExportMap); fm.Items.Add(new Separator()); Add(fm, L.T("Charger le plan d’exemple"), "", LoadDemo); Add(fm, L.T("Quitter"), "Alt+F4", Close); menu.Items.Add(fm);
        var em = Menu(L.T("Édition")); Add(em, L.Undo, "Ctrl+Z", Undo); Add(em, L.T("Rétablir"), "Ctrl+Y", Redo); em.Items.Add(new Separator()); Add(em, L.T("Tout sélectionner"), "Ctrl+A", Canvas.SelectAll); Add(em, L.T("Dupliquer la sélection"), "Ctrl+D", Duplicate); Add(em, L.T("Supprimer la sélection"), L.T("Suppr"), Canvas.DeleteSelection); menu.Items.Add(em);
        Add(em, L.T("Grouper la sélection"), "Ctrl+G", GroupSelection); Add(em, L.T("Dissocier les groupes"), "Ctrl+Maj+G", UngroupSelection);
        Add(em, L.T("Copier"), "Ctrl+C", () => CopySelection()); Add(em, L.T("Couper"), "Ctrl+X", CutSelection); Add(em, L.T("Coller"), "Ctrl+V", PasteSelection);
        Add(em, L.T("Préférences…"), "Ctrl+OemComma", ShowPreferences);
        var vm = Menu(L.T("Affichage")); Add(vm, L.T("Cadrer le plan"), L.T("Origine"), Canvas.Fit); Add(vm, L.T("Revenir à l’origine"), "", () => { Canvas.Center = new(); Canvas.Zoom = 1; Canvas.InvalidateVisual(); UpdateStatus(); }); Add(vm, L.T("Afficher / masquer la grille"), "G", () => { Canvas.ShowGrid = !Canvas.ShowGrid; Canvas.InvalidateVisual(); }); Add(vm, L.T("Afficher / masquer les longueurs"), "M", () => { Canvas.ShowLengths = !Canvas.ShowLengths; Canvas.InvalidateVisual(); }); Add(vm, L.T("Afficher les niveaux supérieurs"), "H", ToggleUpperLevels); upperLevelsMenu = (MenuItem)vm.Items[vm.Items.Count - 1]; upperLevelsMenu.IsCheckable = true; Add(vm, L.T("Afficher les niveaux inférieurs"), "Shift+H", ToggleLowerLevels); lowerLevelsMenu = (MenuItem)vm.Items[vm.Items.Count - 1]; lowerLevelsMenu.IsCheckable = true; lowerLevelsMenu.IsChecked = Canvas.ShowLowerLevels; Add(vm, L.T("Organisation"), "", ShowOrganization); menu.Items.Add(vm);
        var options = Menu(L.T("Options"));
        Add(vm, L.T(ShortcutSettings.FocusAction), "", Canvas.ToggleSelectionFocus);
        focusMenu = (MenuItem)vm.Items[^1];
        focusMenu.IsCheckable = true;
        Add(options, L.T("Vue 3D"), "", Open3DView);
        Add(options, L.T("Sons de l’éditeur"), "", ShowSoundPreferences);
        menu.Items.Add(options);
        menu.Items.Add(BuildPluginsMenu());
        var hm = Menu(L.T("Aide")); Add(hm, L.T("Raccourcis et prise en main"), "F1", Help); Add(hm, L.T("À propos de Coat"), "", () => AppDialog.Show(this, AppVersion.Title + "\n\nEpicteta Lupercal\n\n" + L.T("Éditeur de plans et blueprints pour level design."), L.T("À propos de Coat"), MessageBoxButton.OK, MessageBoxImage.Information)); menu.Items.Add(hm); header.Children.Add(menu);
        var version = new TextBlock { Text = "EPICTETA LUPERCAL  /  " + AppVersion.Number, FontSize = 10, Foreground = B("#728196"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(version, Dock.Right); header.Children.Add(version);
        root.Children.Add(header);
        var toolbar = new DockPanel { Margin = new Thickness(12, 5, 12, 5), LastChildFill = false };
        Grid.SetRow(toolbar, 1);
        root.Children.Add(toolbar);
        var contextBar = CreateContextBar();
        Grid.SetRow(contextBar, 2);
        root.Children.Add(contextBar);
        AddTool(toolbar, EditorTool.Select, L.T("Sélection"));
        AddTool(toolbar, EditorTool.Mirror, L.T("Miroir"));
        AddToolbarDivider(toolbar);
        AddTool(toolbar, EditorTool.Draw, L.T("Dessin")); AddTool(toolbar, EditorTool.Dimension, L.T("Cotation")); AddTool(toolbar, EditorTool.Erase, L.T("Gomme")); AddTool(toolbar, EditorTool.Paint, L.T("Couleur"));
        AddTool(toolbar, EditorTool.Shapes, L.T("Shapes"));
        AddToolbarDivider(toolbar);
        AddTool(toolbar, EditorTool.Annotation, L.T("Texte"));
        AddTool(toolbar, EditorTool.Token, L.T("Jetons"));
        toolbar.Children.Add(new Border { Background = B("#354151"), Width = 1, Margin = new Thickness(12, 5, 14, 5) });
        toolbar.Children.Add(new TextBlock { Text = L.T("GRILLE"), FontSize = 10, Foreground = B("#96A4B6"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        gridChoice.Width = 108; gridChoice.Height = 32; gridChoice.VerticalAlignment = VerticalAlignment.Center;
        foreach (var step in steps) gridChoice.Items.Add(step + L.T(" unités")); gridChoice.SelectedIndex = 5;
        gridChoice.SelectionChanged += (_, _) => { if (!updating && gridChoice.SelectedIndex >= 0) SetGrid(steps[gridChoice.SelectedIndex]); }; toolbar.Children.Add(gridChoice);
        var save = Button(L.T("Enregistrer"), () => Save(), "Enregistrer"); IconButton(save,L.T("Enregistrer"),SaveGlyph,L.T("Enregistrez le plan en cours.")); DockPanel.SetDock(save, Dock.Right); toolbar.Children.Add(save);
        var fit = Button("", Canvas.Fit, "Cadrer"); IconButton(fit,L.T("Cadrer le plan"),FitGlyph,L.T("Ajustez la vue pour afficher tout le plan.")); DockPanel.SetDock(fit, Dock.Right); toolbar.Children.Add(fit);
        save.Background = ToolbarSurface("#263241");
        fit.Background = ToolbarSurface("#263241");
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 420 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        var inspectorColumn = new ColumnDefinition { Width = new GridLength(inspectorWidth), MinWidth = 264, MaxWidth = 540 };
        body.ColumnDefinitions.Add(inspectorColumn);
        Grid.SetRow(body, 3);
        root.Children.Add(body);
        body.Children.Add(Canvas.GlPlan);
        body.Children.Add(Canvas);
        body.Children.Add(Canvas.Feedback);
        rts = new RtsHud(this, rts?.IsMapCollapsed == true);
        body.Children.Add(rts);
        var splitter = new GridSplitter
        {
            Width = 8, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext, Background = B("#141B25")
        };
        System.Windows.Automation.AutomationProperties.SetName(splitter, L.T("Largeur du panneau droit"));
        splitter.DragCompleted += (_,_) => inspectorWidth = inspectorColumn.Width.Value;
        splitter.KeyUp += (_,_) => inspectorWidth = inspectorColumn.Width.Value;
        Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);
        var sidebar = new Border { Background = B(IsBordeaux ? "#302632" : "#1D2A39"), BorderBrush = B("#303B49"), BorderThickness = new Thickness(1, 0, 0, 0) };
        Grid.SetColumn(sidebar, 2);
        body.Children.Add(sidebar);
        var sideLayout = new DockPanel();
        inspectorDecoration = Shortcuts.AnimatedMenus ? new MenuDecoration(MenuSurface.Inspector, IsBordeaux) : null;
        sidebar.Child = inspectorDecoration != null
            ? new Grid { Children = { inspectorDecoration, sideLayout } }
            : sideLayout;
        AddInspectorTabs(sideLayout);
        inspector.Margin = new Thickness(12, 9, 12, 12);
        var status = new DockPanel { Margin = new Thickness(14, 0, 14, 0) };
        statusLeft.FontSize = statusRight.FontSize = 11; statusLeft.Foreground = statusRight.Foreground = B("#9BA9BA"); statusLeft.VerticalAlignment = statusRight.VerticalAlignment = VerticalAlignment.Center;
        var fps = new PresentedFps(presentationMonitor)
        {
            FontSize = 11, Foreground = statusRight.Foreground, VerticalAlignment = VerticalAlignment.Center,
            Width = 52, Margin = new Thickness(12, 0, 0, 0), TextAlignment = TextAlignment.Right
        };
        DockPanel.SetDock(fps, Dock.Right);
        status.Children.Add(fps);
        statusRight.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(statusRight, Dock.Right); status.Children.Add(statusRight); status.Children.Add(statusLeft); Grid.SetRow(status, 4); root.Children.Add(status);

    }

    internal void SetMenuPanPaused(bool paused)
    {
        topDecoration?.PauseForPan(paused);
        inspectorDecoration?.PauseForPan(paused);
    }
    internal void ChangeLanguage(string language)
    {
        Canvas.CancelGesture(); L.SetLanguage(language); SetTheme(); Background=B("#141B25"); Foreground=B("#DCE4ED");
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.Name);
        if (view3DWindow != null) view3DWindow.Title = L.T("Vue 3D");
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
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="#263241" BorderBrush="#3B485A" BorderThickness="1" CornerRadius="4"><TextBlock Text="⌄" Foreground="#B9C7D8" HorizontalAlignment="Right" Margin="0,0,9,0" VerticalAlignment="Center"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin="9,0,25,0" VerticalAlignment="Center" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"/><Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False"><Border Background="#263241" BorderBrush="#4A5B70" BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"><ScrollViewer MaxHeight="360"><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="#DFE8F2"/><Setter Property="Padding" Value="10,6"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="b" Padding="{TemplateBinding Padding}" Background="#263241"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="b" Property="Background" Value="#436155"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="MenuItem"><Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="Black"/><Setter Property="Padding" Value="9,5"/></Style>
 <Style TargetType="CheckBox"><Setter Property="Foreground" Value="#BCC9D9"/><Setter Property="Margin" Value="0,6,0,6"/></Style>
 <Style TargetType="ToolTip"><Setter Property="Background" Value="#263241"/><Setter Property="Foreground" Value="#E1EBF5"/></Style>
 <Style TargetType="ScrollBar"><Setter Property="Background" Value="#1B2430"/><Setter Property="Width" Value="8"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="{TemplateBinding Background}"><Track x:Name="PART_Track" IsDirectionReversed="True"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#445368" CornerRadius="3" Margin="2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
""";
        Resources = (ResourceDictionary)XamlReader.Parse(ThemeMarkup(xaml));
        if (IsBordeaux)
        {
            var menus = new ResourceDictionary { Source=new Uri("/Coat;component/BordeauxMenu.xaml",UriKind.Relative) };
            Resources[typeof(MenuItem)] = menus[typeof(MenuItem)];
        }
        Resources[SystemColors.MenuBrushKey] = MenuBack; Resources[SystemColors.ControlBrushKey] = MenuBack; Resources[SystemColors.MenuTextBrushKey] = MenuInk;
    }
    private static readonly ImageSource OfficialLogo = LoadOfficialLogo();
    private static readonly ImageSource HeaderLogo = LoadHeaderLogo();
    private static BitmapImage LoadOfficialLogo(int width = 128)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Coat;component/Assets/CoatLogo.png", UriKind.Absolute);
        image.DecodePixelWidth = width;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
    private static BitmapSource LoadHeaderLogo()
    {
        // Composite the official artwork onto either header theme, without its black matte.
        var source = new FormatConvertedBitmap(LoadOfficialLogo(256), PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * stride + x * 4;
            int brightness = Math.Max(pixels[index], Math.Max(pixels[index+1], pixels[index+2]));
            pixels[index+3] = (byte)(pixels[index+3] * Math.Clamp((brightness - 12) / 64.0, 0, 1));
            if (pixels[index+3] == 0) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        var transparent = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        if (right < left) return OfficialLogo as BitmapSource ?? source;
        var cropped = new CroppedBitmap(transparent, new Int32Rect(left, top, right-left+1, bottom-top+1));
        cropped.Freeze();
        return cropped;
    }
    private MenuItem Menu(string title) => new() { Header = title, Background = MenuBack, Foreground = MenuInk };
    private static MenuItem DropDownItem(string title) => new() { Header = title, Background = Brushes.White, Foreground = Brushes.Black };
    private void Add(MenuItem parent, string title, string shortcut, Action action)
    { var feature = RegisterFeature(title, action, menu: true); var item = DropDownItem(title); item.InputGestureText = shortcut; if (feature != null) featureMenus[item] = feature; item.Click += (_, _) => action(); parent.Items.Add(item); var original = ParseHint(shortcut); if (original != null) { shortcutMenus.Add((item, original)); item.InputGestureText = CurrentHint(original); } }
    private Button Button(string text, Action action, string? shortcutId = null)
    {
        var featureId = RegisterFeature(text, action);
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => action();
        hints.Bind(button, shortcutId ?? featureId);
        ApplyCommandIcon(button, text);
        return button;
    }
    private void AddTool(DockPanel bar, EditorTool tool, string title)
    {
        string shortcutId = tool switch
        {
            EditorTool.Select => "Sélection",
            EditorTool.Draw => "Dessin",
            EditorTool.Erase => "Gomme",
            EditorTool.Paint => "Coloriage",
            EditorTool.Shapes => "Shapes",
            EditorTool.Annotation => "Annotations",
            EditorTool.Dimension => "Cote",
            EditorTool.Token => "Jeton",
            EditorTool.Mirror => "Miroir",
            _ => throw new ArgumentOutOfRangeException(nameof(tool))
        };
        var button = Button("", () => SetTool(Canvas.Tool == tool ? EditorTool.None : tool), shortcutId);
        var (path, description) = ToolDesign(tool);
        IconButton(button, title, path, L.T(description));
        toolButtons[tool] = button;
        bar.Children.Add(button);
    }
    public void SetTool(EditorTool tool) => ActivateTool(tool,
        tool == EditorTool.None ? InspectorPage.Plan : tool == EditorTool.Modifiers ? InspectorPage.Modifiers : InspectorPage.Selection);
    private void UpdateToolButtons()
    {
        foreach (var (tool,button) in toolButtons)
        {
            bool active = tool == Canvas.Tool;
            SetActive(button, active);
            button.Background = ToolbarSurface(active ? "#304E48" : "#263241");
        }
    }
    private Brush ToolbarSurface(string color) => Shortcuts.AnimatedMenus
        ? EditorCanvas.Brush("#DF" + ChromeColor(color)[1..]) : B(color);
    private void Heading(string title)
    { inspector.Children.Add(new TextBlock { Text = title, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = B("#8596AB"), Margin = new Thickness(0, 0, 0, 6) }); }
    private void Description(string text)
    { inspector.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = B("#96A7BC"), LineHeight = 19, Margin = new Thickness(0, 0, 0, 6) }); }
    private readonly List<string> inspectorHints = new();
    private Button? inspectorHelp;
    private void InspectorTitle(string title)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
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
    private void Divider() { inspector.Children.Add(new Border { Height = 1, Background = B("#303D4C"), Margin = new Thickness(0, 7, 0, 8) }); }
    private TextBox Field(string title, string value, bool numeric = true)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = title, Foreground = B("#A7B6C9"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var input = new TextBox { Text = value, MinWidth = 60 }; System.Windows.Automation.AutomationProperties.SetName(input, title); Grid.SetColumn(input, 1); row.Children.Add(input); inspector.Children.Add(row); if (numeric) NumericInput(input); return input;
    }
    private void ModeButtons(string[] labels, int active, Action<int> choose, string[]? shortcutIds = null)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        bool patterns = labels.SequenceEqual(new[] { L.T("Uni"), L.T("Bandes"), L.Dots });
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var button = Button(labels[i], () => choose(index), shortcutIds?[i]);
            if (patterns)
            {
                button.Content = PatternIcon(i);
                CompactButton(button, labels[i]);
                SetHint(button, labels[i], L.T("Appliquez ce motif aux remplissages."));
            }
            SetActive(button, active == i);
            row.Children.Add(button);
        }
        inspector.Children.Add(row);
    }
    public void UpdateInspector()
    {
        CloseColorPicker(false); confirmations.Clear(); inspectorCommands.Clear(); inspector.Children.Clear(); inspectorHints.Clear(); inspectorHelp = null;
        UpdateContextBar();
        rts?.RefreshCommands();
        foreach (var (page, button) in inspectorTabs) SetActive(button, inspectorPage == page);
        foreach (var (mode, button) in selectionModeButtons)
        {
            bool active = Canvas.Tool == EditorTool.Select && Canvas.Mode == mode;
            SetActive(button, active);
            button.Background = ToolbarSurface(active ? "#304E48" : "#263241");
        }
        if (inspectorPage == InspectorPage.Modifiers) { BooleanInspector(); UpdateTitle(); return; }
        if (inspectorPage == InspectorPage.Selection)
        {
        string[] names = { L.T("Sélection"), L.T("Dessin de contours"), L.T("Gomme"), L.T("Coloriage"), "", L.T("Shapes"), L.T("Annotations"), L.T("Cotation"), L.T("Jetons"), L.T("Miroir") };
        InspectorTitle(names[(int)Canvas.Tool]);
        if (Canvas.Tool == EditorTool.Select)
        {
            var across = new CheckBox { Content = L.T("Sélection sur plusieurs niveaux"), IsChecked = Canvas.SelectAcrossLevels }; across.Click += (_, _) => { Canvas.SelectAcrossLevels = across.IsChecked == true; Canvas.ClearSelection(); Canvas.InvalidateVisual(); }; inspector.Children.Add(across);
            var dimensionEdges = Canvas.SelectionEdges();
            var dimensions = Document.Edges.Where(e => dimensionEdges.Contains(e.Id) && e.Dimension).ToList();
            if (dimensions.Count > 0) DimensionControls(dimensions, false);
            TransformDetails();
            if (Canvas.Transform == TransformKind.Translation)
                ToolHint(Canvas.Mode == SelectionMode.Edge ? L.T("Glissez une arête ou l’intérieur d’une forme entièrement sélectionnée pour la déplacer. Maintenez Maj et glissez pour extruder une épaisseur.") : Canvas.Mode == SelectionMode.Vertex ? L.T("Glissez un point ou saisissez ses coordonnées. Une forme entièrement sélectionnée se déplace aussi depuis son intérieur.") : L.T("Cliquez à l’intérieur d’un contour fermé pour sélectionner toute sa zone."));
            ToolHint(L.T("Ctrl + clic : sélection multiple. Glissez dans le vide pour sélectionner par rectangle."));
            ToolHint(L.T("Double-cliquez un point, une arête ou une zone pour modifier un élément du groupe. La sélection individuelle reste active jusqu’à la désélection ; les groupes sont conservés."));
            StyleInspector();
            if (Canvas.SelectedAnnotation != null) AnnotationInspector();
            var ids = Canvas.SelectionPoints();
            if (ids.Count == 1)
            {
                var point = Document.Get(ids.First()); var (x, y) = AxisFields(L.T("Position"), F(point.X), F(point.Y), "X", "Y");
                inspector.Children.Add(ConfirmButton(L.T("Appliquer les coordonnées"), () => { if (Number(x, out var nx) && Number(y, out var ny)) { Mutate(() => { point.P = new V2(nx, ny); Document.Normalize(); }); Canvas.Focus(); } }, x, y));
            }
            else if (ids.Count > 0)
            {
                Description(L.F($"{ids.Count} points dans la sélection"));
                if (Canvas.SelectedEdges.Count == 1) { var edge = Document.Edges.First(e => Canvas.SelectedEdges.Contains(e.Id)); Description(L.F($"Longueur : {(Document.Get(edge.A).P - Document.Get(edge.B).P).Length:0.##} unités")); }
                if (Canvas.SelectedFaces.Count > 0) Description(L.F($"Surface : {Document.Faces().Where(f => Canvas.SelectedFaces.Contains(f.Key)).Sum(f => f.Area):0.##} u²"));

            }
            else if (Canvas.SelectedAnnotations.Count == 0) Description(L.T("Aucune sélection"));
            if (Canvas.SelectedAnnotations.Count > 0 && Canvas.SelectedAnnotation == null) Description(L.F($"{Canvas.SelectedAnnotations.Count} textes et jetons sélectionnés"));
            FoldSection("groups", L.T("GROUPES"), () =>
            {
                var ignoreGroups = Button(L.T("Ignorer les groupes"), Canvas.ToggleIgnoreGroups);
                ignoreGroups.ToolTip = L.T("Sélectionnez les points, arêtes et zones séparément sans dissocier les groupes. Recliquez pour réactiver la sélection des groupes.");
                System.Windows.Automation.AutomationProperties.SetName(ignoreGroups,L.T("Ignorer les groupes"));
                System.Windows.Automation.AutomationProperties.SetItemStatus(ignoreGroups,L.T(Canvas.GroupsIgnored ? "Activé" : "Désactivé"));
                SetActive(ignoreGroups, Canvas.GroupsIgnored);
                ignoreGroups.Margin = new Thickness(0,0,0,10); inspector.Children.Add(ignoreGroups);
                if (ids.Count > 0)
                {
                    var groups = Document.MatchingGroups(Canvas.SelectedVertices, Canvas.SelectionEdges());
                    if (groups.Count > 0) Description(string.Join(" · ", groups.Select(g => g.Name)));
                    else ToolHint(L.T("Sélectionnez plusieurs éléments puis regroupez-les pour les manipuler ensemble."));
                    if (groups.Count == 1) GroupNameControls(groups[0]);
                    var actions = new WrapPanel();
                    actions.Children.Add(Button(L.T("Grouper"), GroupSelection, "Grouper"));
                    if (groups.Count > 0) { var ungroup = Button(L.T("Dissocier"), UngroupSelection, "Dissocier"); ungroup.Margin = new Thickness(6, 0, 0, 0); actions.Children.Add(ungroup); }
                    inspector.Children.Add(actions);
                }
            }, initiallyOpen: true);
        }
        else if (Canvas.Tool == EditorTool.Mirror) MirrorInspector();
        else if (Canvas.Tool == EditorTool.Dimension) DimensionInspector();
        else if (Canvas.Tool is EditorTool.Annotation or EditorTool.Token) AnnotationInspector();
        else if (Canvas.Tool == EditorTool.Draw)
        {
            DrawInspector();
            ToolHint(L.T("Placez les points sur les intersections de la grille. Chaque clic prolonge le contour."));
            ToolHint(L.T("Revenez au premier point pour fermer une zone. Échap ou clic droit termine le tracé."));
            var card = new Border { Background = B("#243B38"), CornerRadius = new CornerRadius(5), Padding = new Thickness(12), Child = new TextBlock { Text = L.F($"Aimantation active  ·  {Document.Grid} u"), Foreground = B("#94DCC8"), FontSize = 12 } }; inspector.Children.Add(card);
        }
        else if (Canvas.Tool == EditorTool.Shapes)
        {
            if (Canvas.Shape == ShapeKind.Stairs) StairInspector();
            else ToolHint(Canvas.Shape == ShapeKind.Rectangle ? L.T("Placez deux coins opposés, ou cliquez-glissez pour créer un rectangle de quatre points.") : L.T("Placez le centre, puis le rayon, ou cliquez-glissez. Le contour est un polygone régulier."));
            if (Canvas.Shape == ShapeKind.Circle)
                ToolHint(L.T("De 3 à 128 points. Le centre et le rayon suivent la grille ; les sommets conservent un cercle régulier."));
            ToolHint(L.T("Échap annule la forme en cours. Une forme complète s’annule en une seule étape avec Ctrl+Z."));
        }
        else if (Canvas.Tool == EditorTool.Erase) ToolHint(L.T("Cliquez sur une arête pour la supprimer, ou à l’intérieur d’une zone pour effacer son remplissage. Ctrl+Z annule l’opération."));
        else
        {
            ModeButtons(new[] { L.T("Remplir une zone"), L.T("Colorer les lignes") }, Canvas.PaintLines ? 1 : 0, i => { Canvas.PaintLines = i == 1; UpdateInspector(); });
            ToolHint(Canvas.PaintLines ? L.T("Cliquez sur une arête pour lui appliquer la couleur.") : L.T("Cliquez dans un contour fermé pour remplir la zone."));
            PaintStyleInspector();
            var palette = new PaletteView(SavedColors, color => { Canvas.PaintColor = color; UpdateInspector(); }, () => Canvas.PaintColor, Notify, this,hints) { Margin = new Thickness(0, 1, 0, 12) };
            inspector.Children.Add(palette);
            var wheel = ColorButton(() => Canvas.PaintColor, color => { Canvas.PaintColor = color; palette.Refresh(); });
            wheel.Margin = new Thickness(0, 0, 0, 10); inspector.Children.Add(wheel);
            if (Canvas.SelectionPoints().Count > 0)
            {
                var apply = Button(L.T("Colorer la sélection"), () => Mutate(() => { if (Canvas.PaintLines) foreach (var e in Document.Edges.Where(e => Canvas.SelectedEdges.Contains(e.Id))) { e.Color = Canvas.PaintColor; e.Style = Canvas.PaintLineStyle; } else foreach (var f in Canvas.SelectedFaces) { Canvas.PaintFace(f); } })); apply.Margin = new Thickness(0, 8, 0, 0); inspector.Children.Add(apply);
            }
            FoldSection("map", L.T("EXPORT .MAP"), WallColorControls);
        }
        UpdateTitle(); return;
        }
        UpdateLevels();
        inspector.Children.Add(levelsPanel);
        InspectorTitle(L.T("IMAGE DE RÉFÉRENCE"));
        if (Document.Background is { } bg)
        {
            Description(bg.Name);
            var visible = new CheckBox { Content = L.T("Afficher sous la grille"), IsChecked = bg.Visible }; visible.Click += (_, _) => Mutate(() => bg.Visible = visible.IsChecked == true); inspector.Children.Add(visible);
            var op = Field(L.T("Opacité %"), F(bg.Opacity * 100)); var x = Field(L.T("Origine X"), F(bg.X)); var y = Field(L.T("Origine Y"), F(bg.Y)); var w = Field(L.T("Largeur (u)"), F(bg.Width));
            inspector.Children.Add(ConfirmButton(L.T("Appliquer à l’image"), () => { if (Number(op, out var opacity) && Number(x, out var nx) && Number(y, out var ny) && Number(w, out var width) && width > 0) Mutate(() => { bg.Height *= width / bg.Width; bg.Width = width; bg.X = nx; bg.Y = ny; bg.Opacity = Math.Clamp(opacity / 100, 0, 1); }); }, op, x, y, w));
            var remove = Button(L.T("Retirer l’image"), () => Mutate(() => Document.Background = null)); remove.Margin = new Thickness(0, 7, 0, 0); inspector.Children.Add(remove);
        }
        else
        {
            ToolHint(L.T("Superposez votre plan à une esquisse, une carte ou une capture de niveau.")); inspector.Children.Add(Button(L.T("＋  Importer une image"), ImportImage, "Importer une image")); ToolHint(L.T("\nPNG, JPG, BMP · image intégrée au projet"));
        }
        ToolHint(L.T("Molette   Zoom\nClic milieu / droit   Déplacer la vue\nEspace + glisser   Déplacer la vue\nFlèches   Déplacer la vue\n1–0   Changer le pas de grille\nA   Cadrer le plan\nCtrl+S   Enregistrer   ·   Ctrl+Z   Annuler"));
        UpdateTitle();
    }
    private void UpdateLevels()
    {
        levelsPanel.Children.Clear();
        levelsPanel.Children.Add(new TextBlock { Text = L.T("NIVEAUX"), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = B("#8596AB"), Margin = new Thickness(0, 0, 0, 10) });
        var preview = new Expander
        {
            Header = L.T("Aperçu des niveaux"), Foreground = B("#DCE4ED"),
            IsExpanded = openSections.GetValueOrDefault("level-preview", true),
            Content = new LevelPreview(Document.Levels, Document.ActiveLevel) { Height = compactLevels || Canvas.Tool == EditorTool.Select ? 78 : 102 }
        };
        preview.Expanded += (_, _) => openSections["level-preview"] = true;
        preview.Collapsed += (_, _) => openSections["level-preview"] = false;
        levelsPanel.Children.Add(preview);
        var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); row.ColumnDefinitions.Add(new ColumnDefinition()); for (int i=0; i<3; i++) row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(32)});
        var down = Button(L.T("Un niveau plus bas"), () => ChangeAdjacentLevel(-1), "Niveau inférieur"); down.Margin = new Thickness(0); down.IsEnabled = Document.ActiveLevel > -999; down.ToolTip = L.T("Passer au niveau inférieur (créé si nécessaire) · Page ↓"); SetHint(down,L.T("Un niveau plus bas"),L.T("Passer au niveau inférieur (créé si nécessaire) · Page ↓").Split('·')[0].Trim()); row.Children.Add(down);
        var choice = new ComboBox { Margin = new Thickness(6, 0, 6, 0), Height = 32, SelectedValuePath = "Tag" };
        foreach (int level in Document.Levels) choice.Items.Add(new ComboBoxItem { Content = L.T("Niveau ") + level, Tag = level });
        choice.SelectedValue = Document.ActiveLevel; choice.SelectionChanged += (_, _) => { if (choice.SelectedValue is int level && level != Document.ActiveLevel) ChangeLevel(level); }; Grid.SetColumn(choice, 1); row.Children.Add(choice);
        var up = Button(L.T("Un niveau plus haut"), () => ChangeAdjacentLevel(1), "Niveau supérieur"); up.Margin = new Thickness(0); up.IsEnabled = Document.ActiveLevel < 999; up.ToolTip = L.T("Passer au niveau supérieur (créé si nécessaire) · Page ↑"); SetHint(up,L.T("Un niveau plus haut"),L.T("Passer au niveau supérieur (créé si nécessaire) · Page ↑").Split('·')[0].Trim()); Grid.SetColumn(up, 2); row.Children.Add(up); levelsPanel.Children.Add(row);
        var delete = Button(L.T("Supprimer ce niveau"), DeleteActiveLevel); delete.Margin = new Thickness(0); delete.Foreground = B("#E3A0A9"); Grid.SetColumn(delete,4); row.Children.Add(delete);
        LevelHeightControls();
        var organize = Button(L.T("Organisation"), ShowOrganization); organize.Margin = new Thickness(0); Grid.SetColumn(organize,3); row.Children.Add(organize);
        if (Canvas.SelectionPoints().Count > 0 || Canvas.SelectedAnnotations.Count > 0)
        {
            levelsPanel.Children.Add(new TextBlock { Text = L.T("AFFECTER LA SÉLECTION AU NIVEAU"), FontSize = 9, Foreground = B("#A9B9CC"), Margin = new Thickness(0, 14, 0, 7) });
            var assign = new DockPanel();
            var input = new TextBox { Text = Document.ActiveLevel.ToString(CultureInfo.InvariantCulture), MinWidth = 60, ToolTip = L.T("Numéro de niveau, de -999 à 999") };
            NumericInput(input);
            var apply = ConfirmButton(L.T("Affecter"), () =>
            {
                if (NumericExpression.TryInteger(input.Text, out int target) && target >= -999 && target <= 999) AssignSelectionToLevel(target);
                else Notify(L.T("Le niveau doit être un entier compris entre -999 et 999."));
            }, input); apply.Margin = new Thickness(7, 0, 0, 0); DockPanel.SetDock(apply, Dock.Right); assign.Children.Add(apply); assign.Children.Add(input); levelsPanel.Children.Add(assign);
            if (Canvas.SelectedVertices.Count > 0) apply.ToolTip = L.T("Les points emportent leurs arêtes incidentes.");
        }
        levelsPanel.Children.Add(new Border { Height = 1, Background = B("#303D4C"), Margin = new Thickness(0, 7, 0, 0) });
    }
    public void ChangeLevel(int level)
    {
        if (level < -999 || level > 999 || level == Document.ActiveLevel) return;
        Canvas.Feedback.Clear(); Canvas.CancelGesture(); if (!Canvas.SelectAcrossLevels) Canvas.ClearSelection(); Document.EnsureLevel(level); Document.ActiveLevel = level;
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
        if ((Canvas.SelectionPoints().Count + Canvas.SelectedAnnotations.Count == 0) || (x == 0 && y == 0)) return;
        var origin = Canvas.CaptureMoveFeedback();
        if (TransformSelection(p => p + new V2(x, y))) Canvas.FinishMoveFeedback(origin);
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
        var notes = Canvas.SelectedAnnotations.ToHashSet();
        if(Canvas.SelectionPoints().Count+notes.Count==0)return;
        if(Canvas.SelectionPoints().All(id=>Document.Get(id).Level==level)&&Document.Annotations.Where(a=>notes.Contains(a.Id)).All(a=>a.Level==level))return;
        Canvas.CancelGesture();
        Mutate(() =>
        {
            var result = Document.TransferToLevel(Canvas.SelectedVertices, Canvas.SelectedEdges, Canvas.SelectedFaces, level);
            foreach(var note in Document.Annotations.Where(a=>notes.Contains(a.Id)))note.Level=level;
            Document.ActiveLevel = level; Canvas.ClearSelection(); Canvas.SelectedAnnotations.UnionWith(notes);
            if (Canvas.Mode == SelectionMode.Vertex) Canvas.SelectedVertices.UnionWith(result.Points);
            else if (Canvas.Mode == SelectionMode.Face) Canvas.SelectedFaces.UnionWith(result.Faces);
            else Canvas.SelectedEdges.UnionWith(result.Edges);
        }); Canvas.Focus();
    }

    public void Escape()
    {
        if (rts?.CancelPing() == true) return;
        Canvas.CancelGesture(); Canvas.ClearSelection();
        SetTool(EditorTool.Select);
    }
    private static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private bool Number(TextBox box, out double n)
    {
        if (NumericExpression.TryEvaluate(box.Text, out n) && Math.Abs(n) <= 1e8) { if (!double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) box.Text = n.ToString("G15", CultureInfo.InvariantCulture); return true; }
        Notify(L.T("Saisissez une coordonnée numérique valide.")); box.Focus(); box.SelectAll(); return false;
    }
    public void Mutate(Action action, bool createsObjects = false, bool createsFaces = false)
    {
        CloseColorPicker(false);
        Canvas.Anchor = null;
        var before = PlanSnapshot.Capture(Document);
        int objects = Document.Annotations.Count + Document.Edges.Count + Document.Vertices.Count;
        action();
        Commit(before, createsFaces, createsObjects && Document.Annotations.Count + Document.Edges.Count + Document.Vertices.Count > objects);
    }
    public void Commit(string before, bool createsFaces = false, bool createdObject = false) =>
        Commit(PlanSnapshot.Capture(Plan.Parse(before)), createsFaces, createdObject);

    internal void Commit(PlanSnapshot before, bool createsFaces = false, bool createdObject = false)
    {
        CloseColorPicker(false);
        bool changed = before != PlanSnapshot.Capture(Document);
        int previousFaces = Canvas.ClosedFaceCount;
        if (changed) History.Push(before);
        Canvas.RefreshGeometry();
        if (changed)
        {
            rts?.InvalidateMapGeometry();
            if (Progress.CreationXp < 20 && (createdObject || (createsFaces && Canvas.ClosedFaceCount > previousFaces)))
                ApplyProgress(Progress with { CreationXp = Progress.CreationXp + 1 });
        }
        UpdateInspector(); UpdateStatus();
    }
    private bool Dirty => saved != PlanSnapshot.Capture(Document);
    private void UpdateTitle() => Title = AppVersion.Title + " — " + (string.IsNullOrEmpty(file) ? L.T("Sans titre") : Path.GetFileName(file)) + (Dirty ? " *" : "");
    public void UpdateStatus(V2? p = null)
    {
        bool focused = Canvas.IsSelectionFocused;
        if (focusMenu != null) focusMenu.IsChecked = focused;
        statusLeft.Text = rts?.PendingPing != null ? L.T("Cliquez dans le plan ou la minicarte.") + " · " + L.T("Échap annule la pose")
            : focused ? L.T("Mode Focus") : L.T("Édition → Préférences : raccourcis clavier");
        statusRight.Text = (p != null ? L.F($"X {p.Value.X:0.##}   Y {p.Value.Y:0.##}     |     ") : "") + L.F($"Niveau {Document.ActiveLevel}     |     Pas {Document.Grid} u     |     {Canvas.Zoom * 100:0}%");
    }
    public void Notify(string text) { statusLeft.Text = text; }
    private void SetGrid(int step)
    {
        Document.Grid = step;
        SyncGridControl();
        Canvas.InvalidateVisual();
        UpdateInspector();
        UpdateStatus();
    }

    private void SyncGridControl()
    {
        updating = true;
        gridChoice.SelectedIndex = Array.IndexOf(steps, Document.Grid);
        updating = false;
    }
    public void Undo() { Canvas.CancelGesture(); var p = History.Undo(Document); if (p != null) ReplaceFromHistory(p); }
    public void Redo() { Canvas.CancelGesture(); var p = History.Redo(Document); if (p != null) ReplaceFromHistory(p); }
    private void ReplaceFromHistory(Plan p)
    {
        Canvas.Feedback.Clear();
        p.Progression = Document.Progression;
        Document = p;
        Canvas.ClearSelection(); Canvas.RefreshGeometry(); SetGrid(p.Grid);
        rts?.InvalidateMapGeometry();
    }
    public void Duplicate()
    {
        var notes = Document.Annotations.Where(a=>Canvas.SelectedAnnotations.Contains(a.Id)).ToList();
        var ids = Canvas.SelectionPoints(); if (ids.Count + notes.Count == 0) return;
        Mutate(() =>
        {
            var old = Document.Edges.Where(e => ids.Contains(e.A) && ids.Contains(e.B)).ToList();
            var oldFaces = Document.Faces().Where(f => f.Vertices.All(ids.Contains) && Document.Fills.ContainsKey(f.Key)).Select(f => (f.Vertices, Color: Document.Fills[f.Key], Pattern: Document.FillPatterns.GetValueOrDefault(f.Key), Gradient: Document.Gradients.GetValueOrDefault(f.Key))).ToList();
            var map = ids.ToDictionary(id => id, id => Document.Point(Document.Get(id).P + new V2(Document.Grid, -Document.Grid), Document.Get(id).Level));
            var edgeMap = old.ToDictionary(e => e.Id, e => Document.AddEdge(map[e.A], map[e.B], e.Color));
            foreach (var e in old) { Document.Edges.First(x => x.Id == edgeMap[e.Id]).Style = e.Style; Document.Edges.First(x => x.Id == edgeMap[e.Id]).CopyArrows(e); }
            Document.CopyGroups(map, edgeMap);
            foreach (var id in map.Values.Where(id => !Document.Edges.Any(e => e.A == id || e.B == id))) Document.Get(id).KeepPoint = true;
            Canvas.ClearSelection(); Canvas.SelectedEdges.UnionWith(edgeMap.Values); if (edgeMap.Count == 0) Canvas.SelectedVertices.UnionWith(map.Values);
            foreach (var face in oldFaces) { var key = string.Join("-", face.Vertices.Select(id => map[id]).Order()); Document.Fills[key] = face.Color; Document.FillPatterns[key] = face.Pattern; if (face.Gradient != null) Document.Gradients[key] = face.Gradient; }
            foreach(var note in notes){var copy=CloneNote(note,Document.NextId++,new V2(Document.Grid,-Document.Grid),note.Level);Document.Annotations.Add(copy);Canvas.SelectedAnnotations.Add(copy.Id);}
            Document.Normalize();
        }, createsObjects: true, createsFaces: true);
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        focusTextRepeating = e.IsRepeat;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && rts?.CancelPing() == true) { e.Handled = true; return; }
        if (Canvas.IsColorSampling && key == Key.Escape) { Canvas.CancelColorSampling(); e.Handled=true; return; }
        if (e.OriginalSource is TextBox && key != Key.Escape) return;
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None && key is (Key.S or Key.R or Key.T) && HasTransformSelection)
        {
            var (_, transform) = selectionShortcuts.Values.First(value => value.Key == key);
            if (!e.IsRepeat)
            {
                if (Canvas.Tool != EditorTool.Select) SetTool(EditorTool.Select);
                Canvas.SetTransform(transform);
            }
            e.Handled = true;
            return;
        }
        var binding = new Shortcut(key, modifiers);
        var command = ShortcutSettings.Actions.FirstOrDefault(a => !a.IsView3D && Shortcuts.Bindings.GetValueOrDefault(a.Label) == binding);
        if (command?.Label == ShortcutSettings.FocusAction && UsesDefaultFocusShortcut) return;
        if (command == null && key == Key.Divide && modifiers == ModifierKeys.None && UsesDefaultFocusShortcut)
        { if (!e.IsRepeat) Canvas.ToggleSelectionFocus(); e.Handled = true; return; }
        if (command != null && (command.Command.Key == Key.None || command.Label == "Confirmer")) { if (!e.IsRepeat) ExecuteFeature(command.Label); e.Handled = true; return; }
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
            case Key.P: SetSelectionMode(SelectionMode.Vertex); break;
            case Key.L: SetSelectionMode(SelectionMode.Edge); break;
            case Key.F: SetSelectionMode(SelectionMode.Face); break;
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
        var dlg = new OpenFileDialog { Filter = L.T("Plan Coat (*.coat)|*.coat"), Title = L.T("Ouvrir un plan") };
        if (dlg.ShowDialog(this) == true) OpenPath(dlg.FileName);
    }
    public void OpenPath(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var info = new FileInfo(path);
            var version = (info.Length, info.LastWriteTimeUtc);
            if (string.Equals(file, path, StringComparison.OrdinalIgnoreCase) && openFileVersion == version)
                return;

            Canvas.CancelGesture();
            CloseColorPicker(false);
            if (!TestMode && !CheckSave()) return;
            info.Refresh();
            version = (info.Length, info.LastWriteTimeUtc);
            if (version.Length > 70 * 1024 * 1024) throw new InvalidDataException(L.T("Ce plan dépasse la limite de 70 Mo."));
            var plan = Plan.Parse(File.ReadAllText(path));
            var image = Canvas.ReadReference(plan.Background?.Data);
            LoadDocument(plan, path, image);
            openFileVersion = version;
            RememberRecent(path);
        }
        catch (Exception ex) { Error(L.T("Impossible d’ouvrir le plan"), ex); }
    }
    public void LoadDocument(Plan plan, string path) => LoadDocument(plan, path, Canvas.ReadReference(plan.Background?.Data));

    private void LoadDocument(Plan plan, string path, BitmapImage? image)
    {
        Canvas.Feedback.Clear();
        Canvas.CancelGesture();
        CloseColorPicker(false);
        Canvas.ClearSelectionFocus(restoreView: false);
        Canvas.ResetGroupSelectionMode();
        Document = plan;
        file = path;
        openFileVersion = null;
        saved = PlanSnapshot.Capture(plan);
        History.Clear();
        Canvas.ClearSelection();
        Canvas.Center = new V2(plan.ViewX, plan.ViewY);
        Canvas.Zoom = plan.Zoom;
        RestoreView();
        Canvas.RefreshGeometry(image);
        SyncGridControl();
        rts?.ResetProject();
        UpdateInspector();
        UpdateStatus();
    }

    private void RememberFileVersion()
    {
        openFileVersion = null;
        try
        {
            var info = new FileInfo(file);
            openFileVersion = (info.Length, info.LastWriteTimeUtc);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A later open can reload the file when its current version is unknown.
        }
    }
    private void LoadDemo() { Canvas.CancelGesture(); if (!CheckSave()) return; LoadDocument(Plan.Demo(), ""); saved = PlanSnapshot.Capture(new Plan()); Canvas.Fit(); UpdateTitle(); }
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
            SaveProjectTo(path);
            return true;
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
        var regions = new ContourRegions(exportFaces, plan.Vertices.ToDictionary(v => v.Id, v => v.P));
        foreach (int level in plan.Levels.Where(l => l == plan.ActiveLevel || (showLowerLevels && l < plan.ActiveLevel)).Order())
        {
            s.Append($"<g data-level=\"{level}\" opacity=\"{F(plan.LevelOpacity(level))}\">");
            foreach (var f in exportFaces.Where(f => f.Level == level).OrderByDescending(f => f.Area)) if (plan.Fills.TryGetValue(f.Key, out var color))
            {
                var points = string.Join(" ", f.Vertices.Select(id => F(plan.Get(id).X) + "," + F(-plan.Get(id).Y)));
                string path = string.Join(" ", regions.Loops(f.Key).Select(loop => "M " + string.Join(" L ", loop.Select(p => F(p.X) + "," + F(-p.Y))) + " Z"));
                string clip = "clip-" + f.Key; s.Append($"<defs><clipPath id=\"{clip}\"><path d=\"{path}\" clip-rule=\"evenodd\" fill-rule=\"evenodd\"/></clipPath></defs><g clip-path=\"url(#{clip})\">");
                if (plan.Gradients.TryGetValue(f.Key, out var gradient))
                {
                    string id = "gradient-" + f.Key; double angle = gradient.Angle * Math.PI / 180;
                    s.Append($"<defs><linearGradient id=\"{id}\" x1=\"{F(.5 - .5 * Math.Cos(angle))}\" y1=\"{F(.5 + .5 * Math.Sin(angle))}\" x2=\"{F(.5 + .5 * Math.Cos(angle))}\" y2=\"{F(.5 - .5 * Math.Sin(angle))}\"><stop offset=\"0\" stop-color=\"{Escape(color)}\"/><stop offset=\"1\" stop-color=\"{Escape(gradient.EndColor)}\"/></linearGradient></defs><polygon points=\"{points}\" fill=\"url(#{id})\" opacity=\"{F(plan.FillOpacity(level))}\"/>");
                }
                else s.Append(DecorationSvg.Polygon(points, color, plan.FillOpacity(level), plan.FillPatterns.GetValueOrDefault(f.Key)));
                s.Append("</g>");
            }
            foreach (var e in plan.Edges.Where(e => plan.LevelOf(e) == level)) { var a = plan.Get(e.A); var endpoint = plan.Get(e.B); s.Append($"<line x1=\"{F(a.X)}\" y1=\"{F(-a.Y)}\" x2=\"{F(endpoint.X)}\" y2=\"{F(-endpoint.Y)}\" stroke=\"{Escape(e.Color)}\" stroke-width=\"2\"{DecorationSvg.Dash(e.Style)}/>"); s.Append(ArrowGeometry.Svg(plan, e)); }
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
    private void Help()
    {
        var assigned = ShortcutSettings.Actions.Where(a => Shortcuts.Bindings[a.Label].Key != Key.None)
            .Select(a => L.ShortcutLabel(a.Label) + " : " + Shortcuts.Bindings[a.Label]);
        AppDialog.Show(this, AppVersion.Title + " — Epicteta Lupercal\n\n" + L.T("Choisissez un outil dans la barre supérieure et réglez ses options à droite. Molette : zoom. Clic milieu/droit ou Espace+glisser : déplacer la vue. Ctrl+clic : sélection multiple. Maj+glisser une arête : extrusion.\n\nLes champs numériques acceptent +, -, *, / et les parenthèses. Entrée valide ; les annotations multilignes conservent leurs retours à la ligne.\n\nÉdition → Préférences permet d’attribuer ou de supprimer chaque raccourci.\n\nRACCOURCIS ATTRIBUÉS") + "\n" + string.Join("\n", assigned), L.T("Prise en main de Coat"), MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
