// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coat;

internal sealed class CommandHints
{
    private readonly MainWindow host;
    internal CommandHints(MainWindow host) { this.host=host; }
    private readonly DispatcherTimer hintDelay = new() { Interval=TimeSpan.FromMilliseconds(450) };
    private System.Windows.Controls.Canvas hintLayer = new();
    private Border hintBox = new();
    private readonly TextBlock hintName = new();
    private readonly TextBlock hintDetail = new() { MaxWidth=235, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,3,0,0) };
    private Button? hintTarget;
    private Point hintAnchor;
    private bool keyboardHint;
    private bool hintShiftDown;

    internal void Install(Grid root)
    {
        HideHint();
        if (hintBox.Child is StackPanel old) old.Children.Clear();
        var text = new StackPanel(); text.Children.Add(hintName); text.Children.Add(hintDetail);
        hintBox = new Border { Child=text, Background=host.ChromeBrush("#263241"), BorderBrush=host.ChromeBrush("#364455"), BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(3), Padding=new Thickness(8,4,8,4), Visibility=Visibility.Collapsed };
        hintName.Foreground=host.ChromeBrush("#DFE8F2"); hintDetail.Foreground=host.ChromeBrush("#BCC9D9");
        hintBox.SizeChanged += (_,e) => { if(hintBox.IsVisible) PositionHint(e.NewSize); };
        hintLayer = new System.Windows.Controls.Canvas { IsHitTestVisible=false, ClipToBounds=true, Focusable=false };
        hintLayer.Children.Add(hintBox); Grid.SetRowSpan(hintLayer,4); Panel.SetZIndex(hintLayer,99); root.Children.Add(hintLayer);
    }
    internal void RefreshPalette()
    {
        hintBox.Background=host.ChromeBrush("#263241"); hintBox.BorderBrush=host.ChromeBrush("#364455");
        hintName.Foreground=host.ChromeBrush("#DFE8F2"); hintDetail.Foreground=host.ChromeBrush("#BCC9D9");
    }
    internal void Listen(UIElement root,Window window)
    {
        hintDelay.Tick += (_,_) => {
            hintDelay.Stop();
            if(hintTarget == null || !hintTarget.IsVisible) { HideHint(); return; }
            hintAnchor = keyboardHint ? hintTarget.TranslatePoint(new Point(4,hintTarget.ActualHeight),hintLayer) : Mouse.GetPosition(hintLayer);
            ShowHint(hintShiftDown || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        };
        void Shift(object sender, KeyEventArgs e)
        {
            if(e.Key is not (Key.LeftShift or Key.RightShift)) return;
            hintShiftDown = e.RoutedEvent == Keyboard.PreviewKeyDownEvent || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if(hintBox.IsVisible) ShowHint(hintShiftDown);
        }
        root.AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler(Shift),true);
        root.AddHandler(Keyboard.PreviewKeyUpEvent,new KeyEventHandler(Shift),true);
        root.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((_,_)=>HideHint()),true);
        void Reset(object? sender,EventArgs e) { hintShiftDown=false; HideHint(); }
        void Resize(object sender,SizeChangedEventArgs e) => HideHint();
        root.AddHandler(FrameworkElement.LoadedEvent,new RoutedEventHandler((_,_) => { window.Deactivated += Reset; window.SizeChanged += Resize; }));
        root.AddHandler(FrameworkElement.UnloadedEvent,new RoutedEventHandler((_,_) => { window.Deactivated -= Reset; window.SizeChanged -= Resize; Reset(null,EventArgs.Empty); }));
    }
    private static readonly (string Label, string Help)[] CommandHelp =
    {
        ("Par défaut", "Réinitialisez les raccourcis et la langue dans ce formulaire."),
        ("Ergonomique", "Chargez la disposition ergonomique des raccourcis."),
        ("Classique", "Chargez la disposition classique des raccourcis."),
        ("Annuler", "Fermez les préférences sans appliquer les raccourcis saisis."),
        ("Enregistrer", "Enregistrez la langue et les raccourcis choisis."),
        ("Tourne à gauche", "Orientez les marches vers la gauche."),
        ("Tourne à droite", "Orientez les marches vers la droite."),
        ("Supprimer ce niveau", "Supprimez le niveau actif et son contenu."),
        ("Organisation", "Ouvrez la liste des niveaux, groupes et formes."),
        ("Déplacer la sélection", "Appliquez le décalage X et Y aux éléments sélectionnés."),
        ("Extruder avec ce décalage", "Prolongez les arêtes sélectionnées selon le décalage indiqué."),
        ("Appliquer la rotation", "Tournez la sélection de l’angle indiqué."),
        ("Appliquer l’échelle", "Appliquez les facteurs de taille à la sélection."),
        ("Appliquer les coordonnées", "Placez le point aux coordonnées X et Y indiquées."),
        ("Grouper", "Réunissez la sélection dans un groupe."),
        ("Dissocier", "Rendez les membres du groupe indépendants."),
        ("Affecter", "Transférez la sélection au niveau indiqué."),
        ("Colorer la sélection", "Appliquez la couleur active à la sélection."),
        ("Définir la couleur actuelle comme mur", "Extrudez les zones de cette couleur en murs à l’export .map."),
        ("Appliquer la hauteur", "Réglez la hauteur Z du niveau actif."),
        ("Appliquer la cote", "Réglez la taille des flèches et de la mesure."),
        ("Appliquer le texte", "Enregistrez le texte et ses propriétés."),
        ("Appliquer le jeton", "Appliquez les propriétés au jeton sélectionné."),
        ("Supprimer le texte", "Retirez l’annotation du plan."),
        ("Supprimer le jeton", "Retirez le jeton du plan."),
        ("Appliquer le dégradé", "Appliquez les couleurs et l’angle du dégradé."),
        ("Utiliser les réglages du jeton", "Utilisez cette taille pour les prochains jetons."),
        ("Appliquer à l’image", "Ajustez la position, la taille et l’opacité de l’image."),
        ("Retirer l’image", "Retirez l’image de référence du plan."),
        ("Renommer le groupe", "Attribuez le nom saisi au groupe sélectionné."),
        ("＋  Importer une image", "Placez une image de référence sous la grille."),
        ("Continu", "Tracez les lignes sans interruption."),
        ("Long", "Espacez les traits longs de la ligne."),
        ("Court", "Dessinez la ligne en tirets courts."),
        ("•  Points", "Sélectionnez les sommets du contour."),
        ("╱  Arêtes", "Sélectionnez les segments entre deux points."),
        ("▱  Zones", "Sélectionnez des surfaces fermées."),
        ("Remplir une zone", "Cliquez dans un contour fermé pour le remplir."),
        ("Colorer les lignes", "Cliquez sur une arête pour la peindre."),
        ("Rectangle", "Placez deux coins opposés du rectangle."),
        ("Cercle", "Placez le centre puis définissez le rayon."),
        ("Escaliers", "Glissez pour orienter et dimensionner l’escalier."),
        ("Dégradé", "Mélangez deux couleurs sur la zone."),
        ("Remplissage", "Remplissez la zone avec la couleur et le motif actifs."),
    };

    internal void Bind(Button button, string? shortcutId = null)
    {
        if(button.Content is string label && label.Length > 0)
        {
            foreach (var item in CommandHelp)
            {
                if (label != item.Label && (!L.English.TryGetValue(item.Label,out var english) || label != english)) continue;
                AutomationProperties.SetHelpText(button,L.T(item.Help));
                break;
            }
        }
        ToolTipService.SetIsEnabled(button,false);
        button.MouseEnter += (_,_)=> StartHint(button,false,shortcutId);
        button.GotKeyboardFocus += (_,_)=> { if(KeyboardNavigation.GetIsTabStop(button) && Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released) StartHint(button,true,shortcutId); };
        void Clear() { if(hintTarget == button) HideHint(); }
        button.MouseLeave += (_,_)=>Clear();
        button.LostKeyboardFocus += (_,_)=>Clear();
        button.Unloaded += (_,_)=>Clear();
    }
    internal static CommandHints Wrap(MainWindow host,Window window,FrameworkElement content,out Grid shell)
    {
        var hints=new CommandHints(host);
        shell=new Grid(); shell.Children.Add(content); hints.Install(shell); hints.Listen(shell,window);
        return hints;
    }
    private void StartHint(Button button, bool keyboard, string? shortcutId)
    {
        HideHint();
        var name = AutomationProperties.GetName(button);
        if (string.IsNullOrWhiteSpace(name)) name = button.Content as string ?? button.ToolTip as string;
        if (string.IsNullOrWhiteSpace(name)) return;
        string help = AutomationProperties.GetHelpText(button);
        if (string.IsNullOrWhiteSpace(help)) help = button.ToolTip is TextBlock block ? block.Text.Split('\n')[0] : button.ToolTip as string ?? "";
        if (help == name) help="";
        string shortcut = shortcutId == null ? "" : host.ButtonShortcut(shortcutId);
        hintName.Text = shortcut.Length == 0 ? name : $"{name} ({shortcut})";
        hintDetail.Text = help;
        hintTarget=button; keyboardHint=keyboard; hintDelay.Start();
    }
    private void ShowHint(bool expanded)
    {
        if(hintTarget == null) return;
        hintDetail.Visibility=expanded && hintDetail.Text.Length>0 ? Visibility.Visible : Visibility.Collapsed;
        hintBox.Visibility=Visibility.Visible; hintBox.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        PositionHint(hintBox.DesiredSize);
    }
    private void PositionHint(Size size)
    {
        double left=Math.Clamp(hintAnchor.X+12,0,Math.Max(0,hintLayer.ActualWidth-size.Width));
        double below=hintAnchor.Y+15;
        double top=below+size.Height<=hintLayer.ActualHeight ? below : Math.Clamp(hintAnchor.Y-8-size.Height,0,Math.Max(0,hintLayer.ActualHeight-size.Height));
        System.Windows.Controls.Canvas.SetLeft(hintBox,left); System.Windows.Controls.Canvas.SetTop(hintBox,top);
    }
    private void HideHint()
    {
        hintDelay.Stop(); hintTarget=null; hintBox.Visibility=Visibility.Collapsed;
    }
}
