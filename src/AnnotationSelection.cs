// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Coat;
public partial class EditorCanvas
{
    private bool CanSelectAnnotation(Annotation note) => note.Level <= Plan.ActiveLevel && CanSelectLevel(note.Level) && FocusIncludesAnnotation(note);
    internal Rect AnnotationScreenBounds(Annotation note)
    {
        var bounds = note.WorldBounds();
        return new Rect(Screen(new V2(bounds.Left, -bounds.Top)), new Size(bounds.Width*Zoom, bounds.Height*Zoom));
    }
}
public partial class MainWindow
{
    internal static bool HandleAnnotationKey(TextBox input,Key key,ModifierKeys modifiers,Action apply)
    {
        if(key!=Key.Enter)return false;
        if((modifiers&ModifierKeys.Shift)!=0)
        {
            if(input.MaxLength > 0 && input.Text.Length-input.SelectionLength+Environment.NewLine.Length > input.MaxLength)return true;
            int caret=input.SelectionStart;input.SelectedText=Environment.NewLine;input.CaretIndex=caret+Environment.NewLine.Length;input.SelectionLength=0;
        }
        else apply();
        return true;
    }
}
