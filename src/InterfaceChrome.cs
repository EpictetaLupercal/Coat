// Coat — Epicteta Lupercal
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Coat;

public partial class MainWindow
{
    internal bool IsBordeaux => Shortcuts.Theme == "bordeaux";
    internal Brush MenuBack => IsBordeaux ? B("#1B2430") : Brushes.White;
    internal Brush MenuInk => IsBordeaux ? B("#DFE8F2") : Brushes.Black;

    // This palette belongs to the window chrome. Document colors bypass it.
    private string ChromeColor(string color) => !IsBordeaux ? color : color switch
    {
        "#141B25" or "#141D28" => "#24161D",
        "#1B2430" => "#35212A",
        "#263241" => "#4A2D38",
        "#304E48" or "#436155" or "#437D70" => "#77394D",
        "#243B38" => "#512D39",
        "#78DCC1" or "#6DC8B0" => "#D5B578",
        "#364455" or "#3B485A" or "#4A5B70" or "#2B3645" or "#303B49" or "#303D4C" or "#354151" => "#957749",
        "#445368" => "#916F62",
        "#13251F" or "#15281F" => "#332027",
        "#DFE8F2" or "#DCE4ED" or "#D9E3EC" or "#E1EBF5" or "#9BEBD4" or "#B8E8DD" => "#FFF1D9",
        "#BCC9D9" or "#B9C7D8" or "#94DCC8" => "#E2CFAF",
        "#728196" or "#8596AB" or "#96A4B6" or "#9BA9BA" or "#96A7BC" or "#A7B6C9" or "#A9B9CC" => "#CFBBA7",
        _ => color
    };
    private string ThemeMarkup(string xaml)
    {
        xaml = xaml.Replace("Value=\"12,7\"", "Value=\"9,4\"");
        const string marker = "<Trigger Property=\"IsPressed\"";
        var focus = "<Trigger Property=\"IsKeyboardFocused\" Value=\"True\"><Setter TargetName=\"b\" Property=\"BorderBrush\" Value=\"#FFF1D9\"/><Setter TargetName=\"b\" Property=\"BorderThickness\" Value=\"2\"/></Trigger>";
        xaml = xaml.Replace(marker,focus + marker);
        if (!IsBordeaux) return xaml;
        xaml = Regex.Replace(xaml, "#[A-Fa-f0-9]{6}", match => ChromeColor(match.Value));
        return xaml.Replace("Value=\"White\"", "Value=\"#35212A\"").Replace("Value=\"Black\"", "Value=\"#FFF1D9\"")
            .Replace("<Setter TargetName=\"b\" Property=\"BorderBrush\" Value=\"#D5B578\"/>",
                     "<Setter TargetName=\"b\" Property=\"Background\" Value=\"#653847\"/><Setter TargetName=\"b\" Property=\"BorderBrush\" Value=\"#D5B578\"/>");
    }

    private readonly Dictionary<string,bool> openSections = new();
    private void FoldSection(string key, string title, Action build, bool initiallyOpen = false)
    {
        var parent = inspector;
        var contents = new StackPanel { Margin=new Thickness(2,5,2,0) };
        inspector = contents;
        try { build(); } finally { inspector = parent; }
        var section = new Expander { Header=title, Content=contents, Foreground=Foreground,
            Margin=new Thickness(0,6,0,4), IsExpanded=openSections.GetValueOrDefault(key,initiallyOpen) };
        section.Expanded += (_,_) => openSections[key]=true;
        section.Collapsed += (_,_) => openSections[key]=false;
        parent.Children.Add(section);
    }

    private const string SaveGlyph = "M3,3 H16 L21,8 V21 H3 Z M7,3 V10 H16 V3 M7,21 V14 H17 V21";
    private const string FitGlyph = "M3,9 V3 H9 M15,3 H21 V9 M21,15 V21 H15 M9,21 H3 V15 M8,8 H16 V16 H8 Z";
    private const string DeleteGlyph = "M4,6 H20 M9,6 V3 H15 V6 M6,8 L7,21 H17 L18,8 M10,10 V18 M14,10 V18";
    private const string TreeGlyph = "M3,3 H10 V8 H3 Z M6,8 V18 H13 M6,12 H13 M13,10 H21 V14 H13 Z M13,16 H21 V20 H13 Z";

    private static (string Path,string Help) ToolDesign(EditorTool tool) => tool switch
    {
        EditorTool.Select => ("M4,2 L19,13 L12,14 L9,21 Z","Sélectionnez des points, des arêtes ou des zones pour les modifier."),
        EditorTool.Draw => ("M3,20 L4,15 L15,4 L20,9 L9,20 Z M12,7 L17,12 M4,15 L9,20","Placez des points successifs pour tracer un contour."),
        EditorTool.Dimension => ("M2,5 V19 M22,5 V19 M3,12 H21 M8,8 L3,12 L8,16 M16,8 L21,12 L16,16","Placez deux points pour afficher la distance qui les sépare."),
        EditorTool.Erase => ("M3,14 L14,3 L21,10 L10,21 H8 Z M7,10 L14,17 M10,21 H22","Effacez une arête ou le remplissage d’une zone."),
        EditorTool.Paint => ("M3,12 L12,3 L20,11 L11,20 Z M4,13 H17 M8,2 L13,7 M20,15 Q25,21 20,21 Q16,21 20,15 Z","Appliquez une couleur, un motif ou un dégradé."),
        EditorTool.Shapes => ("M3,4 H15 V16 H3 Z M16,9 A6,6 0 1 1 10,17","Tracez un rectangle, un cercle ou un escalier."),
        EditorTool.Annotation => ("M3,7 V3 H21 V7 M12,3 V21 M7,21 H17","Placez ou modifiez une annotation sur le plan."),
        EditorTool.Token => ("M12,2 A10,10 0 1 1 11.99,2 Z M12,6 L14,10 L19,11 L15,14 L16,18 L12,16 L8,18 L9,14 L5,11 L10,10 Z","Choisissez un symbole et placez un jeton sur le plan."),
        _ => throw new ArgumentOutOfRangeException(nameof(tool))
    };

    private void IconButton(Button button,string title,string geometry,string description)
    {
        SetButtonGlyph(button, title, geometry);
        SetHint(button, title, description);
    }

    private void SetButtonGlyph(Button button, string title, string geometry)
    {
        if (!glyphs.TryGetValue(geometry, out var shape))
        {
            shape = Geometry.Parse(geometry);
            shape.Freeze();
            glyphs.Add(geometry, shape);
        }
        var path = new Path { Data=shape, Stretch=Stretch.Uniform, Width=20, Height=20,
            StrokeThickness=1.65, StrokeLineJoin=PenLineJoin.Round, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round };
        path.SetBinding(Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){Source=button});
        button.Content = path;
        CompactButton(button, title);
    }

    private static readonly Dictionary<string, Geometry> glyphs = new();
    private static void CompactButton(Button button, string title)
    {
        button.Width = 32;
        button.Height = 32;
        button.Padding = new Thickness(4);
        button.Margin = new Thickness(0,0,3,0);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(button, title);
    }
    private void SetActive(Button button, bool active)
    {
        button.Background = B(active ? "#304E48" : "#263241");
        button.BorderBrush = B(active ? "#78DCC1" : "#364455");
        button.Foreground = B(active ? "#9BEBD4" : "#D9E3EC");
    }

    private const string CheckGlyph = "M3,12 L9,18 L21,5";
    private const string FillGlyph = "M3,3 H21 V21 H3 Z M4,17 L17,4 M7,21 L21,7 M13,21 L21,13";
    private static readonly (string Label, string Glyph)[] commandGlyphs =
    {
        ("Appliquer les coordonnées",CheckGlyph), ("Appliquer la hauteur",CheckGlyph),
        ("Appliquer la rotation",CheckGlyph), ("Appliquer l’échelle",CheckGlyph),
        ("Appliquer la cote",CheckGlyph), ("Appliquer le texte",CheckGlyph),
        ("Appliquer le jeton",CheckGlyph), ("Appliquer le dégradé",CheckGlyph),
        ("Appliquer à l’image",CheckGlyph), ("Utiliser les réglages du jeton",CheckGlyph),
        ("Affecter","M4,4 H20 M4,20 H20 M12,5 V16 M7,11 L12,16 L17,11"),
        ("Supprimer ce niveau",DeleteGlyph), ("Supprimer le texte",DeleteGlyph), ("Supprimer le jeton",DeleteGlyph),
        ("Retirer l’image",DeleteGlyph), ("Organisation",TreeGlyph),
        ("Un niveau plus bas","M12,4 V20 M6,14 L12,20 L18,14"),
        ("Un niveau plus haut","M12,20 V4 M6,10 L12,4 L18,10"),
        ("Déplacer la sélection","M3,12 H21 M12,3 V21 M3,12 L7,8 M3,12 L7,16 M21,12 L17,8 M21,12 L17,16 M12,3 L8,7 M12,3 L16,7 M12,21 L8,17 M12,21 L16,17"),
        ("Extruder avec ce décalage","M3,9 H15 V21 H3 Z M3,9 L9,3 H21 V15 L15,21 M15,9 L21,3"),
        ("Ignorer les groupes","M8,3 H3 V8 M16,3 H21 V8 M21,16 V21 H16 M3,16 V21 H8 M9,9 H15 V15 H9 Z"),
        ("Grouper","M3,3 H21 V21 H3 Z M6,6 H11 V11 H6 Z M13,13 H18 V18 H13 Z"),
        ("Dissocier","M3,3 H10 V10 H3 Z M14,14 H21 V21 H14 Z M11,15 L15,11"),
        ("Renommer le groupe","M3,5 H21 V19 H3 Z M12,3 V21 M9,3 H15 M9,21 H15"),
        ("Colorer la sélection",FillGlyph), ("Remplir une zone",FillGlyph), ("Remplissage",FillGlyph),
        ("Dégradé","M2,4 H22 V20 H2 Z M6,5 V19 M9,5 V19 M13,5 V19 M18,5 V19"),
        ("Colorer les lignes","M3,19 L13,9 L17,13 L7,23 Z M13,9 L18,4 L22,8 L17,13"),
        ("Définir la couleur actuelle comme mur","M2,4 H22 V20 H2 Z M2,12 H22 M9,4 V12 M15,12 V20"),
        ("＋  Importer une image","M2,4 H22 V20 H2 Z M3,17 L9,10 L15,17 M15,8 H21 M18,5 V11"),
        ("•  Points","M12,4 A8,8 0 1 1 11.99,4 Z M12,10 A2,2 0 1 1 11.99,10 Z"),
        ("╱  Arêtes","M3,20 L21,4 M3,17 V22 H8 M16,3 H22 V8"),
        ("▱  Zones",FillGlyph), ("Rectangle","M2,5 H22 V19 H2 Z"),
        ("Cercle","M12,3 A9,9 0 1 1 11.99,3 Z"),
        ("Escaliers","M2,21 V16 H8 V10 H14 V4 H22"),
        ("Tourne à gauche","M21,20 V9 H4 M4,9 L10,3 M4,9 L10,15"),
        ("Tourne à droite","M3,20 V9 H20 M20,9 L14,3 M20,9 L14,15"),
        ("Continu","M2,12 H22"),
        ("Long","M2,12 H10 M14,12 H22"),
        ("Court","M2,12 H5 M8,12 H11 M14,12 H17 M20,12 H23")
    };
    private void ApplyCommandIcon(Button button, string title)
    {
        foreach (var (label,geometry) in commandGlyphs)
        {
            var display = L.Language == "en" && L.English.TryGetValue(label, out var english) ? english : label;
            if (title != display) continue;
            SetButtonGlyph(button, title, geometry);
            return;
        }
    }
    private static void SetHint(Button button,string name,string help)
    {
        AutomationProperties.SetName(button,name);
        AutomationProperties.SetHelpText(button,help);
    }
}
