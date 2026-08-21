using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ChurchBooks.App.Behaviors;

public static class DateInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(DateInputBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DatePicker picker) return;
        if ((bool)e.NewValue) picker.Loaded += Picker_Loaded;
        else picker.Loaded -= Picker_Loaded;
    }

    private static void Picker_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DatePicker picker) return;
        picker.ApplyTemplate();
        if (picker.Template.FindName("PART_TextBox", picker) is not DatePickerTextBox textBox) return;

        textBox.ToolTip = "Type MM/DD/YYYY. ChurchBooks inserts the slashes automatically, or use the calendar button.";
        textBox.PreviewTextInput -= TextBox_PreviewTextInput;
        textBox.PreviewTextInput += TextBox_PreviewTextInput;
        textBox.LostKeyboardFocus -= TextBox_LostKeyboardFocus;
        textBox.LostKeyboardFocus += TextBox_LostKeyboardFocus;
        DataObject.RemovePastingHandler(textBox, TextBox_Pasting);
        DataObject.AddPastingHandler(textBox, TextBox_Pasting);
    }

    private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not DatePickerTextBox textBox) return;
        if (e.Text.Any(static ch => !char.IsDigit(ch) && ch != '/'))
        {
            e.Handled = true;
            return;
        }

        var candidate = ReplaceSelection(textBox, e.Text);
        var digits = new string(candidate.Where(char.IsDigit).Take(8).ToArray());
        textBox.Text = FormatDigits(digits);
        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }

    private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not DatePickerTextBox textBox || !e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        var digits = new string(pasted.Where(char.IsDigit).Take(8).ToArray());
        if (digits.Length == 0)
        {
            e.CancelCommand();
            return;
        }

        textBox.Text = FormatDigits(digits);
        textBox.CaretIndex = textBox.Text.Length;
        e.CancelCommand();
    }

    private static void TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not DatePickerTextBox textBox || textBox.TemplatedParent is not DatePicker picker) return;
        if (string.IsNullOrWhiteSpace(textBox.Text)) return;

        if (DateTime.TryParseExact(textBox.Text, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            picker.SelectedDate = parsed.Date;
            textBox.Text = parsed.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        }
    }

    private static string ReplaceSelection(TextBox textBox, string insertion)
    {
        var original = textBox.Text ?? string.Empty;
        var start = Math.Max(0, Math.Min(textBox.SelectionStart, original.Length));
        var length = Math.Max(0, Math.Min(textBox.SelectionLength, original.Length - start));
        return original.Remove(start, length).Insert(start, insertion);
    }

    private static string FormatDigits(string digits)
    {
        if (digits.Length <= 2) return digits;
        if (digits.Length <= 4) return digits[..2] + "/" + digits[2..];
        return digits[..2] + "/" + digits[2..4] + "/" + digits[4..];
    }
}
