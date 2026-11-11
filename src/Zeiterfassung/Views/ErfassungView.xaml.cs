using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zeiterfassung.Views;

public partial class ErfassungView : UserControl
{
    // Eine Uhrzeit im Format HH:mm: nur Ziffern und ein Doppelpunkt, höchstens 5 Zeichen.
    private static readonly Regex TimeCharacters = new(@"^[0-9:]*$", RegexOptions.CultureInvariant);
    private const int TimeLength = 5;

    public ErfassungView()
    {
        InitializeComponent();
    }

    private void TimeInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        var resulting = ResultingText(box, e.Text);
        e.Handled = !IsValidTimeText(resulting);
    }

    private void TimeInput_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box || !e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var pasted = (string)e.DataObject.GetData(DataFormats.UnicodeText)!;
        if (!IsValidTimeText(ResultingText(box, pasted)))
        {
            e.CancelCommand();
        }
    }

    private static string ResultingText(TextBox box, string inserted)
    {
        var text = box.Text ?? string.Empty;
        var start = Math.Min(box.SelectionStart, text.Length);
        var length = Math.Min(box.SelectionLength, text.Length - start);
        return text.Remove(start, length).Insert(start, inserted);
    }

    private static bool IsValidTimeText(string text) =>
        text.Length <= TimeLength && TimeCharacters.IsMatch(text);
}
