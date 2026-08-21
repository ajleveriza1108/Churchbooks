using System.IO;
using ChurchBooks.App.Models;
using ChurchBooks.App.Preferences;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R110BackupRecoveryUxTests
{
    [Fact]
    public void Settings_ContainsDedicatedBackupRecoveryTabAndExplicitActions()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Header=\"Backup &amp; Recovery\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Back Up Now\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Verify Backup File...\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Restore Backup...\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Restore is replacement, not merge.", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FooterAndDashboard_UseRealBackupStatusBindings()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Text=\"{Binding BackupFooterDisplay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BackupAlert}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Backup: Not configured", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void UiPreferences_RoundTripBackupFolderAndLastVerifiedBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks-UiPrefs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "prefs.json");
            var store = new UiPreferenceStore(path);
            var timestamp = DateTimeOffset.UtcNow;
            var expected = new UiPreferences(WorkspaceMode.Accountant, HelpLevel.Experienced)
            {
                BackupDirectory = Path.Combine(root, "Backups"),
                LastBackupPath = Path.Combine(root, "Backups", "ChurchBooks-BFBC-test.cbbackup"),
                LastBackupUtc = timestamp
            };

            store.Save(expected);
            var actual = store.Load();

            Assert.Equal(expected.BackupDirectory, actual.BackupDirectory);
            Assert.Equal(expected.LastBackupPath, actual.LastBackupPath);
            Assert.Equal(timestamp, actual.LastBackupUtc);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RestoreWorkflow_RequiresValidationConfirmationAndPreRestoreSafetyBackup()
    {
        var source = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml.cs");
        Assert.Contains("ValidateBackupAsync(dialog.FileName)", source, StringComparison.Ordinal);
        Assert.Contains("MessageBoxButton.YesNo", source, StringComparison.Ordinal);
        Assert.Contains("pre-restore safety backup", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RestoreBackupAsync", source, StringComparison.Ordinal);
        Assert.Contains("await _viewModel.InitializeAsync(_database)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_DeclaresBackupRecoveryWithoutSchemaAccountingOrLicensingChanges()
    {
        var manifest = ReadSource("churchbooks.manifest.json");
        Assert.Contains("\"sqliteOnlineBackup\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"preRestoreSafetyBackupRequired\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"restoreMergesDatabases\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"schemaChanged\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"accountingBehaviorChanged\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"licensingChanged\": false", manifest, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
