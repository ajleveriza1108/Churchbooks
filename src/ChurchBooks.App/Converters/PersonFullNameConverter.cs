using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using ChurchBooks.App.Behaviors;

namespace ChurchBooks.App.Converters;

public sealed class PersonFullNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;

        var firstName = ReadString(value, "FirstName");
        var middleName = FirstNonBlank(ReadString(value, "MiddleName"), ReadString(value, "SecondName"));
        var lastName = ReadString(value, "LastName");
        var suffix = ReadString(value, "Suffix");

        var parts = new[] { firstName, middleName, lastName, suffix }
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Select(PersonNameTextBoxBehavior.NormalizeName)
            .ToArray();

        if (parts.Length > 0) return string.Join(" ", parts);

        var displayName = ReadString(value, "DisplayName");
        return string.IsNullOrWhiteSpace(displayName) ? value.ToString() ?? string.Empty : displayName.Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    private static string ReadString(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return property?.GetValue(value)?.ToString()?.Trim() ?? string.Empty;
    }

    private static string FirstNonBlank(params string[] values) => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
