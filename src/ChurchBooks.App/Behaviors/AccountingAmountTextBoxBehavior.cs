using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChurchBooks.App.Behaviors;

public static class AccountingAmountTextBoxBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(AccountingAmountTextBoxBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox) return;

        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
        DataObject.RemovePastingHandler(textBox, OnPaste);

        if (e.NewValue is true)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
            DataObject.AddPastingHandler(textBox, OnPaste);
            InputMethod.SetIsInputMethodEnabled(textBox, false);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var candidate = ReplaceSelection(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, e.Text);
        e.Handled = !IsPotentialAmountText(candidate);
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var candidate = ReplaceSelection(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, pasted);
        if (!IsPotentialAmountText(candidate)) e.CancelCommand();
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox || string.IsNullOrWhiteSpace(textBox.Text)) return;
        if (!TryParseAmount(textBox.Text, out var amount)) return;
        textBox.Text = FormatAmount(amount);
        textBox.CaretIndex = textBox.Text.Length;
    }

    public static bool IsPotentialAmountText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        var decimalPointSeen = false;
        var digitsAfterDecimal = 0;
        foreach (var character in text)
        {
            if (char.IsDigit(character))
            {
                if (decimalPointSeen && ++digitsAfterDecimal > 2) return false;
                continue;
            }

            if (character == ',')
            {
                if (decimalPointSeen) return false;
                continue;
            }

            if (character == '.')
            {
                if (decimalPointSeen) return false;
                decimalPointSeen = true;
                continue;
            }

            return false;
        }

        return true;
    }

    public static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out amount) && amount >= 0m;
    }

    public static string FormatAmount(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

    private static string ReplaceSelection(string source, int selectionStart, int selectionLength, string replacement)
    {
        source ??= string.Empty;
        var safeStart = Math.Clamp(selectionStart, 0, source.Length);
        var safeLength = Math.Clamp(selectionLength, 0, source.Length - safeStart);
        return source.Remove(safeStart, safeLength).Insert(safeStart, replacement ?? string.Empty);
    }
}
