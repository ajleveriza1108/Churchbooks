using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChurchBooks.App.Behaviors;

public static class PersonNameTextBoxBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(PersonNameTextBoxBehavior),
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
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsAllowedNameText(e.Text);
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        if (!IsAllowedNameText(pasted)) e.CancelCommand();
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        textBox.Text = NormalizeName(textBox.Text);
        textBox.CaretIndex = textBox.Text.Length;
    }

    public static bool IsAllowedNameText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        foreach (var character in text)
        {
            if (char.IsLetter(character) || char.IsWhiteSpace(character) || character is '-' or '\'' or '.') continue;
            return false;
        }
        return true;
    }

    public static string NormalizeName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var collapsed = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var culture = CultureInfo.CurrentCulture;
        var result = new StringBuilder(collapsed.Length);
        var capitalizeNext = true;

        foreach (var character in collapsed)
        {
            if (char.IsLetter(character))
            {
                result.Append(capitalizeNext ? char.ToUpper(character, culture) : char.ToLower(character, culture));
                capitalizeNext = false;
            }
            else
            {
                result.Append(character);
                capitalizeNext = character is ' ' or '-' or '\'';
            }
        }

        return result.ToString();
    }
}
