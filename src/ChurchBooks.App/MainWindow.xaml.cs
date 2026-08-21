using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Backup;
using ChurchBooks.Data.Storage;
using Microsoft.Win32;

namespace ChurchBooks.App;

public partial class MainWindow : Window
{
    private const string VerificationDatabasePathEnvironmentVariable = "CHURCHBOOKS_DATABASE_PATH";
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private readonly MainWindowViewModel _viewModel = new();
    private ChurchBooksDatabase? _database;
    private ChurchBooksBackupService? _backupService;

    public MainWindow()
    {
        InitializeComponent();
        ThemeManager.Apply(_viewModel.SelectedAppearanceTheme);
        DataContext = _viewModel;
        SourceInitialized += OnSourceInitialized;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyNativeTitleBarTheme();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.SelectedAppearanceTheme), StringComparison.Ordinal))
        {
            Dispatcher.BeginInvoke(ApplyNativeTitleBarTheme);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void ApplyNativeTitleBarTheme()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = _viewModel.SelectedAppearanceTheme == AppearanceTheme.ChurchBooksDark ? 1 : 0;
        try
        {
            var result = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
            if (result != 0)
            {
                _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // Older/unsupported Windows shells keep their system title-bar appearance.
        }
        catch (EntryPointNotFoundException)
        {
            // Older/unsupported Windows shells keep their system title-bar appearance.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            var verificationDatabasePath = Environment.GetEnvironmentVariable(VerificationDatabasePathEnvironmentVariable);
            _database = string.IsNullOrWhiteSpace(verificationDatabasePath)
                ? new ChurchBooksDatabase()
                : new ChurchBooksDatabase(verificationDatabasePath);
            await _viewModel.InitializeAsync(_database);
            _backupService = new ChurchBooksBackupService(_database);
        }
        catch (Exception ex)
        {
            _viewModel.DatabaseStatus = "Database initialization failed: " + ex.Message;
            _viewModel.StatusMessage = "ChurchBooks could not initialize the local database. No accounting data was changed.";
        }
    }

    private void ChooseOrganizationLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose church or organization logo",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|PNG files (*.png)|*.png|JPEG files (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SetOrganizationLogo(dialog.FileName);
        }
    }

    private void ChooseBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose ChurchBooks backup folder",
            InitialDirectory = _viewModel.BackupDirectory,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SetBackupDirectory(dialog.FolderName);
        }
    }

    private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_viewModel.BackupDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _viewModel.BackupDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _viewModel.RecordBackupVerification("The backup folder could not be opened: " + ex.Message);
        }
    }

    private async void BackUpNow_Click(object sender, RoutedEventArgs e)
    {
        if (_backupService is null)
        {
            _viewModel.RecordBackupVerification("Backup is not ready until the local database finishes initializing.");
            return;
        }

        try
        {
            var result = await _backupService.CreateBackupAsync(
                _viewModel.BackupDirectory,
                _viewModel.SetupWorkspace.OrganizationAcronym);
            _viewModel.RecordBackupSuccess(result.BackupPath, result.CreatedUtc, result.Sha256);
        }
        catch (Exception ex)
        {
            _viewModel.RecordBackupVerification("Backup failed without changing the books: " + ex.Message);
        }
    }

    private async void VerifyBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_backupService is null)
        {
            _viewModel.RecordBackupVerification("Backup verification is not ready until the local database finishes initializing.");
            return;
        }

        var dialog = CreateBackupOpenDialog("Choose a ChurchBooks backup to verify");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var validation = await _backupService.ValidateBackupAsync(dialog.FileName);
        var organization = string.IsNullOrWhiteSpace(validation.OrganizationName) ? string.Empty : " Organization: " + validation.OrganizationName + ".";
        _viewModel.RecordBackupVerification(validation.IsValid
            ? "Backup verified successfully." + organization + " " + validation.Message
            : validation.Message);
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_backupService is null || _database is null)
        {
            _viewModel.RecordBackupVerification("Restore is not ready until the local database finishes initializing.");
            return;
        }

        var dialog = CreateBackupOpenDialog("Choose a verified ChurchBooks backup to restore");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var validation = await _backupService.ValidateBackupAsync(dialog.FileName);
        if (!validation.IsValid)
        {
            _viewModel.RecordBackupVerification(validation.Message);
            MessageBox.Show(this, validation.Message, "Restore blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var organization = string.IsNullOrWhiteSpace(validation.OrganizationName) ? "this ChurchBooks backup" : validation.OrganizationName;
        var answer = MessageBox.Show(
            this,
            "Restore " + organization + "?\n\nChurchBooks will create and verify a pre-restore safety backup first. The selected backup must pass SQLite integrity checks. Restore never merges databases; it replaces the local books with the selected snapshot.",
            "Confirm ChurchBooks restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            _viewModel.RecordBackupVerification("Restore cancelled. No accounting data was changed.");
            return;
        }

        try
        {
            var result = await _backupService.RestoreBackupAsync(
                dialog.FileName,
                _viewModel.BackupDirectory,
                _viewModel.SetupWorkspace.OrganizationAcronym);
            await _viewModel.InitializeAsync(_database);
            _viewModel.RecordRestoreSuccess(result.RestoredFromPath, result.SafetyBackupPath, result.RestoredUtc);
            MessageBox.Show(
                this,
                "Restore completed and ChurchBooks reloaded the restored books.\n\nPre-restore safety backup:\n" + result.SafetyBackupPath,
                "Restore complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _viewModel.RecordBackupVerification("Restore failed. ChurchBooks attempted to preserve the pre-restore books: " + ex.Message);
            MessageBox.Show(this, _viewModel.BackupOperationStatus, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static OpenFileDialog CreateBackupOpenDialog(string title) => new()
    {
        Title = title,
        Filter = "ChurchBooks backup (*.cbbackup)|*.cbbackup|SQLite database (*.db)|*.db|All files (*.*)|*.*",
        CheckFileExists = true,
        Multiselect = false
    };
}
