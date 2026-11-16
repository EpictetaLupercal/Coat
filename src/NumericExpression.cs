// Coat — Epicteta Lupercal
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

namespace Coat;

// A bounded arithmetic parser: no scripting, reflection or executable input.
public static class NumericExpression
{
    public static bool TryEvaluate(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 256) return false;
        try { var parser = new Parser(text.Replace(',', '.')); value = parser.Expression(); parser.Space(); return parser.End && double.IsFinite(value); }
        catch (FormatException) { return false; }
    }
    public static bool TryInteger(string text, out int value)
    {
        value = 0;
        if (!TryEvaluate(text, out double n) || n != Math.Truncate(n) || n < int.MinValue || n > int.MaxValue) return false;
        value = (int)n; return true;
    }
    private sealed class Parser(string text)
    {
        private int at, depth;
        public bool End => at == text.Length;
        public void Space() { while (!End && char.IsWhiteSpace(text[at])) at++; }
        private bool Take(char c) { Space(); if (End || text[at] != c) return false; at++; return true; }
        public double Expression()
        {
            double value = Product();
            while (true) { if (Take('+')) value += Product(); else if (Take('-')) value -= Product(); else return value; }
        }
        private double Product()
        {
            double value = Atom();
            while (true)
            {
                if (Take('*')) value *= Atom();
                else if (Take('/')) { double divisor = Atom(); if (divisor == 0) throw new FormatException(); value /= divisor; }
                else return value;
            }
        }
        private double Atom()
        {
            if (++depth > 32) throw new FormatException();
            try
            {
                if (Take('+')) return Atom(); if (Take('-')) return -Atom();
                if (Take('(')) { double n = Expression(); if (!Take(')')) throw new FormatException(); return n; }
                Space(); int start = at;
                while (!End && (char.IsAsciiDigit(text[at]) || text[at] == '.')) at++;
                if (!End && (text[at] is 'e' or 'E'))
                { at++; if (!End && (text[at] is '+' or '-')) at++; while (!End && char.IsAsciiDigit(text[at])) at++; }
                if (!double.TryParse(text[start..at], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) throw new FormatException();
                return value;
            }
            finally { depth--; }
        }
    }
}

public partial class MainWindow
{
    private record NumericRange(double Minimum, double Maximum, bool Integer);
    private readonly Dictionary<TextBox, Action> confirmations = new();
    private void NumericInput(TextBox input)
    {
        input.PreviewMouseWheel += (_, e) =>
        {
            if (!input.IsKeyboardFocusWithin) return;
            e.Handled = true;
            if (!NumericExpression.TryEvaluate(input.Text, out var value)) return;
            var limits = input.Tag as NumericRange;
            value = Math.Clamp(value + Math.Sign(e.Delta), limits?.Minimum ?? -1e8, limits?.Maximum ?? 1e8);
            if (limits?.Integer == true) value = Math.Round(value);
            input.Text = value.ToString("G15", CultureInfo.InvariantCulture);
        };
        input.LostKeyboardFocus += (_, _) => EvaluateInput(input);
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || input.AcceptsReturn) return;
            e.Handled = true;
            if (EvaluateInput(input) && confirmations.TryGetValue(input, out var apply)) apply();
        };
    }
    private bool EvaluateInput(TextBox input)
    {
        if (NumericExpression.TryEvaluate(input.Text, out double value) && Math.Abs(value) <= 1e8 && (input.Tag is not NumericRange range || value >= range.Minimum && value <= range.Maximum && (!range.Integer || value == Math.Truncate(value))))
        { if (!double.TryParse(input.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) input.Text = value.ToString("G15", CultureInfo.InvariantCulture); input.ClearValue(TextBox.BorderBrushProperty); return true; }
        input.BorderBrush = System.Windows.Media.Brushes.IndianRed;
        Notify(L.T("Expression numérique invalide.")); return false;
    }
    private Button ConfirmButton(string label, Action action, params TextBox[] fields)
    {
        foreach (var field in fields)
        {
            confirmations[field] = action;
            // Text and colour fields also validate with Enter; multiline notes keep line breaks.
            field.KeyDown += (_, e) => { if (e.Key == Key.Enter && !field.AcceptsReturn) { e.Handled = true; action(); } };
        }
        return Button(label, action);
    }
}
