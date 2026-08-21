using ChurchBooks.App.Models;

namespace ChurchBooks.App.Preferences;

public sealed record UiPreferences(WorkspaceMode WorkspaceMode, HelpLevel HelpLevel)
{
    public AppearanceTheme AppearanceTheme { get; init; } = AppearanceTheme.ChurchBooksLight;
    public string OrganizationLogoPath { get; init; } = string.Empty;
    public string BackupDirectory { get; init; } = string.Empty;
    public string LastBackupPath { get; init; } = string.Empty;
    public DateTimeOffset? LastBackupUtc { get; init; }

    public static UiPreferences Default { get; } = new(WorkspaceMode.Simple, HelpLevel.Guided)
    {
        AppearanceTheme = AppearanceTheme.ChurchBooksLight,
        OrganizationLogoPath = string.Empty,
        BackupDirectory = string.Empty,
        LastBackupPath = string.Empty,
        LastBackupUtc = null
    };
}
