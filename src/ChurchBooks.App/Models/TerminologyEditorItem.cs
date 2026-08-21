using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChurchBooks.App.Models;

public sealed partial class TerminologyEditorItem : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string DisplayKey
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Key)) return string.Empty;
            var builder = new StringBuilder(Key.Length + 4);
            for (var index = 0; index < Key.Length; index++)
            {
                var ch = Key[index];
                if (index > 0 && char.IsUpper(ch) && !char.IsWhiteSpace(Key[index - 1])) builder.Append(' ');
                builder.Append(ch);
            }
            return builder.ToString();
        }
    }

    [ObservableProperty] private string _singular = string.Empty;
    [ObservableProperty] private string _plural = string.Empty;
}
