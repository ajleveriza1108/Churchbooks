using System.IO;
using System.Text.Json;

namespace ChurchBooks.App.Preferences;

public sealed class UiPreferenceStore
{
    private readonly string _path;

    public UiPreferenceStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChurchBooks",
            "ui-preferences.json");
    }

    public UiPreferences Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return UiPreferences.Default;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<UiPreferences>(json) ?? UiPreferences.Default;
        }
        catch (JsonException)
        {
            return UiPreferences.Default;
        }
        catch (IOException)
        {
            return UiPreferences.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return UiPreferences.Default;
        }
    }

    public void Save(UiPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("UI preference path is invalid.");
        }

        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporary, json);
        File.Move(temporary, _path, overwrite: true);
    }
}
