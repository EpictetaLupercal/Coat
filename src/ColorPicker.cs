// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Coat;

internal static class ColorMath
{
    public static Color FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360; saturation = Math.Clamp(saturation, 0, 1); value = Math.Clamp(value, 0, 1);
        double c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
        var (r, g, b) = hue switch { < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x), < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x) };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
    public static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double hue = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        return ((hue + 360) % 360, max == 0 ? 0 : d / max, max);
    }
    public static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    public static bool TryHex(string text, out Color color)
    {
        text = text.Trim().TrimStart('#'); color = Colors.Black;
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); return true;
    }
}

internal sealed class ColorWheel : FrameworkElement
{
    private static readonly BitmapSource disk = MakeDisk();
    public double Hue { get; set; }
    public double Saturation { get; set; }
    public double Value { get; set; } = 1;
    public event Action? Changed;
    private double Radius => Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 8);
    public ColorWheel()
    {
        Width = Height = 244; Cursor = Cursors.Cross; Focusable = true;
        System.Windows.Automation.AutomationProperties.SetName(this, L.T("Roue de couleurs : teinte et saturation"));
        MouseLeftButtonDown += (_, e) => { Focus(); CaptureMouse(); Pick(e.GetPosition(this)); e.Handled = true; };
        MouseMove += (_, e) => { if (IsMouseCaptured) Pick(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, e) => { if (IsMouseCaptured) { Pick(e.GetPosition(this)); ReleaseMouseCapture(); } e.Handled = true; };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Left) Hue -= 2; else if (e.Key == Key.Right) Hue += 2;
            else if (e.Key == Key.Up) Saturation += .02; else if (e.Key == Key.Down) Saturation -= .02; else return;
            Hue = (Hue + 360) % 360; Saturation = Math.Clamp(Saturation, 0, 1); Changed?.Invoke(); InvalidateVisual(); e.Handled = true;
        };
    }
    internal void Pick(Point point)
    {
        double x = point.X - ActualWidth / 2, y = ActualHeight / 2 - point.Y;
        if (Math.Abs(x) + Math.Abs(y) > .001) Hue = (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
        Saturation = Math.Clamp(Math.Sqrt(x * x + y * y) / Radius, 0, 1);
        Changed?.Invoke(); InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        var center = new Point(ActualWidth / 2, ActualHeight / 2); double radius = Radius;
        dc.DrawImage(disk, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)Math.Round((1 - Value) * 255), 0, 0, 0)), null, center, radius, radius);
        var marker = new Point(center.X + Math.Cos(Hue * Math.PI / 180) * Saturation * radius, center.Y - Math.Sin(Hue * Math.PI / 180) * Saturation * radius);
        dc.DrawEllipse(null, new Pen(Brushes.Black, 4), marker, 5, 5); dc.DrawEllipse(null, new Pen(Brushes.White, 2), marker, 5, 5);
    }
    private static BitmapSource MakeDisk()
    {
        const int size = 480; var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            double dx = (x + .5 - size / 2.0) / (size / 2.0), dy = (size / 2.0 - y - .5) / (size / 2.0), radius = Math.Sqrt(dx * dx + dy * dy);
            if (radius > 1) continue;
            var c = ColorMath.FromHsv(Math.Atan2(dy, dx) * 180 / Math.PI, radius, 1); int i = (y * size + x) * 4;
            pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R; pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4); bitmap.Freeze(); return bitmap;
    }
}

internal sealed class ColorPicker : Border
{
    private readonly MainWindow owner;
    internal ColorWheel Wheel { get; } = new() { Width = 200, Height = 200 };
    internal TextBox HexInput { get; } = new() { MaxLength = 7, Width = 112 };
    private readonly Slider hue = new() { Minimum = 0, Maximum = 359.99 }, saturation = new() { Minimum = 0, Maximum = 100 }, brightness = new() { Minimum = 0, Maximum = 100 };
    private readonly Border preview = new() { Width = 44, Height = 30, CornerRadius = new CornerRadius(4) };
    private readonly TextBlock rgb = new(), error = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
    private readonly PaletteView palette;
    private readonly List<(Slider Slider, TextBox Input)> fields = new();
    private bool updating;
    internal event Action<string>? Changed;
    internal string SelectedHex => ColorMath.Hex(ColorMath.FromHsv(Wheel.Hue, Wheel.Saturation, Wheel.Value));
    internal ColorPicker(MainWindow owner, string initial)
    {
        this.owner = owner; Width = 370; Padding = new Thickness(12); CornerRadius = new CornerRadius(9); BorderBrush = EditorCanvas.Brush("#647387"); BorderThickness = new Thickness(1);
        Background = EditorCanvas.Brush("#1B2430"); Resources = owner.Resources; Language = owner.Language;
        TextElement.SetForeground(this,EditorCanvas.Brush("#DCE4ED")); TextElement.SetFontFamily(this,owner.FontFamily); TextElement.SetFontSize(this,12);
        System.Windows.Automation.AutomationProperties.SetName(this,L.T("Palette de couleurs — Coat"));
        var root = new StackPanel(); Child = root;
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(206) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.Children.Add(Wheel); root.Children.Add(row);
        var controls = new StackPanel { Margin = new Thickness(8,2,0,0) }; Grid.SetColumn(controls,1); row.Children.Add(controls);
        void SliderRow(string title, Slider slider)
        {
            var heading = new DockPanel(); controls.Children.Add(heading);
            var input = new TextBox { Width = 46, Padding = new Thickness(3,2,3,2), FontSize = 11 }; DockPanel.SetDock(input,Dock.Right); heading.Children.Add(input);
            heading.Children.Add(new TextBlock { Text = title, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            slider.Margin = new Thickness(0,4,0,10); controls.Children.Add(slider); fields.Add((slider,input));
            System.Windows.Automation.AutomationProperties.SetName(slider,title); System.Windows.Automation.AutomationProperties.SetName(input,title);
            slider.ValueChanged += (_,_) => { if (updating) return; Wheel.Hue = hue.Value; Wheel.Saturation = saturation.Value / 100; Wheel.Value = brightness.Value / 100; Sync(); };
            input.TextChanged += (_,_) => { if (!updating && NumericExpression.TryEvaluate(input.Text,out double n) && n >= slider.Minimum && n <= slider.Maximum) slider.Value = n; };
            input.PreviewKeyDown += (_,e) => { if (e.Key == Key.Enter) { if (NumericExpression.TryEvaluate(input.Text,out double n) && n >= slider.Minimum && n <= slider.Maximum) { slider.Value=n; input.Text=slider.Value.ToString("0.#",CultureInfo.InvariantCulture); } else error.Text=L.T("Expression numérique invalide."); e.Handled=true; } };
            input.PreviewMouseWheel += (_,e) => { if (!input.IsKeyboardFocusWithin) return; slider.Value=Math.Clamp(slider.Value+Math.Sign(e.Delta),slider.Minimum,slider.Maximum); e.Handled=true; };
        }
        SliderRow(L.T("Teinte"),hue); SliderRow(L.T("Saturation"),saturation); SliderRow(L.T("Luminosité"),brightness);
        var samples = new DockPanel { Margin = new Thickness(0,4,0,10) }; root.Children.Add(samples); samples.Children.Add(preview);
        HexInput.Margin = new Thickness(8,0,8,0); samples.Children.Add(HexInput); samples.Children.Add(rgb);
        System.Windows.Automation.AutomationProperties.SetName(HexInput,L.T("Couleur hexadécimale"));
        HexInput.TextChanged += (_,_) => { if (updating) return; if (ColorMath.TryHex(HexInput.Text,out var color)) { SetColor(color,false); error.Text=""; } else error.Text=L.T("Utilisez six chiffres hexadécimaux : #66C5AD."); };
        root.Children.Add(new TextBlock { Text=L.T("Couleurs enregistrées"), Margin=new Thickness(0,0,0,6) });
        var paletteRow = new StackPanel { Orientation=Orientation.Horizontal }; root.Children.Add(paletteRow);
        palette = new PaletteView(owner.SavedColors,color=> { ColorMath.TryHex(color,out var chosen); SetColor(chosen); },()=>SelectedHex,message=>error.Text=message) { Width=230 }; paletteRow.Children.Add(palette);
        var add = new Button { Content="+", FontSize=22, Width=34, Height=34, Padding=new Thickness(0), Margin=new Thickness(5,0,5,0), VerticalAlignment=VerticalAlignment.Top, ToolTip=L.T("Enregistrer dans le premier emplacement vide") };
        System.Windows.Automation.AutomationProperties.SetName(add,L.T("Enregistrer la couleur dans la palette"));
        add.Click += (_,_) => { if (!ColorMath.TryHex(HexInput.Text,out var color)) { error.Text=L.T("Couleur invalide : utilisez le format #RRGGBB."); return; } try { owner.SavedColors.Add(ColorMath.Hex(color)); error.Text=""; } catch(Exception ex) { error.Text=ex.Message; } }; paletteRow.Children.Add(add);
        var pick = new Button { Content=new Image { Source=ColorUi.EyedropperIcon(),Width=20,Height=20 }, Width=34,Height=34,Padding=new Thickness(4),VerticalAlignment=VerticalAlignment.Top,ToolTip=L.T("Prélever une couleur sur le plan ou son image de référence. Échap ou clic droit pour annuler.") };
        System.Windows.Automation.AutomationProperties.SetName(pick,L.T("Pipette")); pick.Click += (_,_) => owner.BeginColorSampling(this); paletteRow.Children.Add(pick);
        root.Children.Add(error);
        Wheel.Changed += () => Sync(); ColorMath.TryHex(initial,out var initialColor); SetColor(initialColor);
        PreviewKeyDown += (_,e) => { if (e.Key == Key.Escape) { Close(); e.Handled=true; } };
    }
    internal void Show() => owner.ShowColorPopup(this,owner.Canvas);
    internal void Close() => owner.CloseColorPicker();
    internal void SetColor(Color color,bool updateHex=true) { (Wheel.Hue,Wheel.Saturation,Wheel.Value)=ColorMath.ToHsv(color); Sync(updateHex); }
    private void Sync(bool updateHex=true)
    {
        updating=true;
        try { hue.Value=Wheel.Hue; saturation.Value=Wheel.Saturation*100; brightness.Value=Wheel.Value*100; foreach(var field in fields) if(!field.Input.IsKeyboardFocusWithin)field.Input.Text=field.Slider.Value.ToString("0.#",CultureInfo.InvariantCulture); var color=ColorMath.FromHsv(Wheel.Hue,Wheel.Saturation,Wheel.Value); preview.Background=new SolidColorBrush(color); rgb.Text=L.F($"R {color.R}  ·  V {color.G}  ·  B {color.B}"); rgb.TextWrapping=TextWrapping.Wrap; if(updateHex){HexInput.Text=ColorMath.Hex(color);error.Text="";} Wheel.InvalidateVisual();palette?.Refresh(); }
        finally { updating=false; }
        Changed?.Invoke(SelectedHex);
    }
}
