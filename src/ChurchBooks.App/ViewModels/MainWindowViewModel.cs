using System.IO;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Expenses;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Integrity;
using ChurchBooks.Accounting.Reporting;
using ChurchBooks.Accounting.Services;
using ChurchBooks.App.Models;
using ChurchBooks.App.Preferences;
using ChurchBooks.App.Services;
using ChurchBooks.App.Validation;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly FundDraftValidator _fundDraftValidator = new();
    private readonly TerminologyAliasService _terminologyAliasService = new();
    private readonly UiPreferenceStore _preferenceStore = new();
    private readonly CsvReportExportService _csvReportExportService = new();
    private readonly RestrictionReleaseReviewService _restrictionReleaseReviewService = new();
    private readonly List<FundListItemViewModel> _allFunds = new();
    private FundManagementService? _fundManagementService;
    private FundReportingService? _fundReportingService;
    private AccountantReportingService? _accountantReportingService;
    private SqliteAccountingStore? _accountingStore;
    private SqliteFundAccountingStore? _fundStore;
    private SqliteFundIntegrityScanner? _integrityScanner;
    private FundBalanceReport? _currentBalanceReport;
    private FundActivityReport? _currentActivityReport;
    private TrialBalanceReport? _currentTrialBalanceReport;
    private IncomeExpenseReport? _currentIncomeExpenseReport;
    private GeneralLedgerReport? _currentGeneralLedgerReport;
    private Guid? _editingFundId;
    private Guid? _pendingArchiveFundId;
    private readonly Stack<WorkspaceSection> _backHistory = new();
    private readonly Stack<WorkspaceSection> _forwardHistory = new();
    private bool _historyNavigation;

    public MainWindowViewModel()
    {
        SetupWorkspace.TerminologyChanged += (_, _) => ApplyPersonalization();
        var preferences = _preferenceStore.Load();
        _selectedWorkspaceMode = preferences.WorkspaceMode;
        _selectedHelpLevel = preferences.HelpLevel;
        _selectedAppearanceTheme = preferences.AppearanceTheme;
        _organizationLogoPath = File.Exists(preferences.OrganizationLogoPath) ? preferences.OrganizationLogoPath : string.Empty;
        _backupDirectory = string.IsNullOrWhiteSpace(preferences.BackupDirectory) ? GetDefaultBackupDirectory() : preferences.BackupDirectory;
        _lastBackupPath = File.Exists(preferences.LastBackupPath) ? preferences.LastBackupPath : string.Empty;
        _lastBackupUtc = preferences.LastBackupUtc;
        _backupOperationStatus = _lastBackupUtc.HasValue
            ? "Last verified backup: " + _lastBackupUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "No verified ChurchBooks backup has been created from this profile yet.";
    }

    public string ChurchName => SetupWorkspace.OrganizationDisplayName;
    public string CurrentPeriod
    {
        get
        {
            var today = DateTime.Today;
            var start = new DateTime(today.Year, today.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            return $"{start:MMMM d} - {end:MMMM d, yyyy}";
        }
    }
    public string BaseCurrency => SetupWorkspace.BaseCurrency;
    public string ProductStatus => "ChurchBooks Pro";
    public string FiscalYearDisplay
    {
        get
        {
            var start = Math.Clamp(SetupWorkspace.FiscalYearStartMonth, 1, 12);
            var end = start == 1 ? 12 : start - 1;
            var startName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(start);
            var endName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(end);
            return $"Fiscal Year: {startName} - {endName}";
        }
    }
    public string TaxIdentifierDisplay => string.IsNullOrWhiteSpace(SetupWorkspace.TaxIdentifier)
        ? "TIN: Not set"
        : "TIN: " + SetupWorkspace.TaxIdentifier.Trim();
    public string DatabaseConnectionDisplay => DatabaseStatus.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
        ? "Database: Connected"
        : "Database: Attention";
    public string SystemStatusDisplay => DatabaseStatus.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
        ? "System Status: Ready"
        : "System Status: Initializing";
    public string BackupFooterDisplay => LastBackupUtc.HasValue
        ? "Backup: " + LastBackupUtc.Value.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.CurrentCulture)
        : "Backup: Not yet created";
    public string LastBackupDisplay => LastBackupUtc.HasValue
        ? LastBackupUtc.Value.ToLocalTime().ToString("F", CultureInfo.CurrentCulture)
        : "No verified backup yet";
    public string BackupAlert => LastBackupUtc.HasValue
        ? "Last verified backup: " + LastBackupUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : "Create and verify a backup before production use.";

    public SetupWorkspaceViewModel SetupWorkspace { get; } = new();
    public PeopleGivingWorkspaceViewModel PeopleWorkspace { get; } = new();
    public GivingWorkspaceViewModel GivingWorkspace { get; } = new();
    public BankingWorkspaceViewModel BankingWorkspace { get; } = new();
    public ImportWorkspaceViewModel ImportWorkspace { get; } = new();
    public ExpensesWorkspaceViewModel ExpensesWorkspace { get; } = new();

    public ObservableCollection<FundListItemViewModel> Funds { get; } = new();
    public ObservableCollection<FundBalanceReportRow> FundBalanceReportRows { get; } = new();
    public ObservableCollection<FundActivityLine> FundActivityReportLines { get; } = new();
    public ObservableCollection<Account> ReportAccounts { get; } = new();
    public ObservableCollection<TrialBalanceRow> TrialBalanceRows { get; } = new();
    public ObservableCollection<IncomeExpenseRow> IncomeReportRows { get; } = new();
    public ObservableCollection<IncomeExpenseRow> ExpenseReportRows { get; } = new();
    public ObservableCollection<GeneralLedgerReportRow> GeneralLedgerRows { get; } = new();
    public ObservableCollection<FundIntegrityFinding> IntegrityFindings { get; } = new();
    public ObservableCollection<DashboardTransactionItem> DashboardTransactions { get; } = new();
    public ObservableCollection<FundListItemViewModel> DashboardFundBalances { get; } = new();
    public IReadOnlyList<WorkspaceMode> WorkspaceModes { get; } = Enum.GetValues<WorkspaceMode>();
    public IReadOnlyList<HelpLevel> HelpLevels { get; } = Enum.GetValues<HelpLevel>();
    public IReadOnlyList<AppearanceTheme> AppearanceThemes { get; } = Enum.GetValues<AppearanceTheme>();
    public IReadOnlyList<FundRestriction> FundRestrictions { get; } = Enum.GetValues<FundRestriction>();
    public IReadOnlyList<FundOverspendPolicy> OverspendPolicies { get; } = Enum.GetValues<FundOverspendPolicy>();

    [ObservableProperty]
    private string _databaseStatus = "Initializing local database";

    [ObservableProperty]
    private string _statusMessage = "ChurchBooks is ready.";

    [ObservableProperty]
    private WorkspaceSection _currentSection = WorkspaceSection.Dashboard;

    [ObservableProperty]
    private int _peopleTabIndex;

    [ObservableProperty]
    private int _settingsTabIndex;

    [ObservableProperty]
    private WorkspaceMode _selectedWorkspaceMode;

    [ObservableProperty]
    private HelpLevel _selectedHelpLevel;

    [ObservableProperty]
    private AppearanceTheme _selectedAppearanceTheme;

    [ObservableProperty]
    private string _organizationLogoPath = string.Empty;

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private string _lastBackupPath = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? _lastBackupUtc;

    [ObservableProperty]
    private string _backupOperationStatus = "No verified ChurchBooks backup has been created from this profile yet.";

    [ObservableProperty]
    private string _globalSearchText = string.Empty;

    [ObservableProperty]
    private string _fundSearchText = string.Empty;

    [ObservableProperty]
    private bool _includeArchived;

    [ObservableProperty]
    private FundListItemViewModel? _selectedFund;

    [ObservableProperty]
    private bool _isFundEditorOpen;

    [ObservableProperty]
    private string _editorTitle = "Add Fund";

    [ObservableProperty]
    private string _editorCode = string.Empty;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private string _editorPurpose = string.Empty;

    [ObservableProperty]
    private FundRestriction _editorRestriction = FundRestriction.Unrestricted;

    [ObservableProperty]
    private FundOverspendPolicy _editorOverspendPolicy = FundOverspendPolicy.Allow;

    [ObservableProperty]
    private string _editorCodeError = string.Empty;

    [ObservableProperty]
    private string _editorNameError = string.Empty;

    [ObservableProperty]
    private string _editorPurposeError = string.Empty;

    [ObservableProperty]
    private string _editorGeneralError = string.Empty;

    [ObservableProperty]
    private DateTime? _reportAsOfDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _activityFromDate = new DateTime(DateTime.Today.Year, 1, 1);

    [ObservableProperty]
    private DateTime? _activityToDate = DateTime.Today;

    [ObservableProperty]
    private FundListItemViewModel? _reportFund;

    [ObservableProperty]
    private string _balanceReportSummary = "Generate a fund balance snapshot from the ledger.";

    [ObservableProperty]
    private string _activityReportSummary = "Select a fund and date range to review fund activity.";

    [ObservableProperty]
    private DateTime? _financialReportAsOfDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _financialFromDate = new DateTime(DateTime.Today.Year, 1, 1);

    [ObservableProperty]
    private DateTime? _financialToDate = DateTime.Today;

    [ObservableProperty]
    private Account? _generalLedgerAccount;

    [ObservableProperty]
    private string _trialBalanceSummary = "Generate the Trial Balance from posted journal activity.";

    [ObservableProperty]
    private string _incomeExpenseSummary = "Choose a date range to generate the Income & Expense Statement.";

    [ObservableProperty]
    private string _generalLedgerSummary = "Choose an account and date range to generate its General Ledger.";

    [ObservableProperty]
    private string _integritySummary = "Run Integrity Center to verify fund assignments, per-fund balance, restrictions, and database relationships.";

    [ObservableProperty]
    private string _releaseAmountText = string.Empty;

    [ObservableProperty]
    private string _releaseEvidence = string.Empty;

    [ObservableProperty]
    private string _releaseReviewResult = "No restriction-release review has been run. Phase 3D never posts a release automatically.";

    public bool IsDashboardVisible => CurrentSection == WorkspaceSection.Dashboard;
    public bool IsServicesVisible => CurrentSection == WorkspaceSection.Services;
    public bool IsFundsVisible => CurrentSection == WorkspaceSection.Funds;
    public bool IsReportsVisible => CurrentSection == WorkspaceSection.Reports;
    public bool IsIntegrityVisible => CurrentSection == WorkspaceSection.Integrity;
    public bool IsPeopleVisible => CurrentSection == WorkspaceSection.People;
    public bool IsGivingVisible => CurrentSection == WorkspaceSection.Giving;
    public bool IsBankingVisible => CurrentSection == WorkspaceSection.Banking;
    public bool IsSetupVisible => CurrentSection == WorkspaceSection.Setup;
    public bool IsImportVisible => CurrentSection == WorkspaceSection.Import;
    public bool IsExpensesVisible => CurrentSection == WorkspaceSection.Expenses;
    public bool CanNavigateBack => _backHistory.Count > 0;
    public bool CanNavigateForward => _forwardHistory.Count > 0;
    public string LatestServiceDisplay
    {
        get
        {
            var batch = GivingWorkspace.Batches.OrderByDescending(static item => item.ServiceDate).FirstOrDefault();
            return batch is null ? "No service financial package yet" : $"{batch.Name} - {batch.ServiceDate:MMMM d, yyyy}";
        }
    }
    public string LatestServiceCaption
    {
        get
        {
            var batch = GivingWorkspace.Batches.OrderByDescending(static item => item.ServiceDate).FirstOrDefault();
            return batch is null
                ? "Create a service batch and record giving to begin the after-service financial trail."
                : $"Service batch status: {batch.Status}. Deposits and expenses remain separately reviewable until reporting is finalized.";
        }
    }
    public string OrganizationSetupAlert => SetupWorkspace.IsSetupComplete
        ? "Organization profile is complete and remains editable in Settings."
        : "Complete organization identity and terminology before production use.";
    public string BankingAlert => BankingWorkspace.ActiveBankCount == 0
        ? "No active bank account is configured yet."
        : BankingWorkspace.DraftDepositCount > 0
            ? $"{BankingWorkspace.DraftDepositCount} draft deposit(s) need review."
            : "No draft deposits are waiting for review.";
    public bool ShowGuidance => SelectedHelpLevel is HelpLevel.Beginner or HelpLevel.Guided;
    public bool ShowAdvancedFundDetails => SelectedWorkspaceMode is WorkspaceMode.Pro or WorkspaceMode.Accountant;
    public bool HasFunds => Funds.Count > 0;
    public bool HasNoFunds => !HasFunds;
    public int ActiveFundCount => _allFunds.Count(static fund => fund.Status == FundStatus.Active);
    public int RestrictedFundCount => _allFunds.Count(static fund => fund.Restriction is FundRestriction.DonorRestricted or FundRestriction.Endowment);
    public decimal TotalFundBalance => _allFunds.Where(static fund => fund.Status == FundStatus.Active).Sum(static fund => fund.Balance);
    public string TotalFundBalanceDisplay => $"{BaseCurrency} {TotalFundBalance:N2}";
    public string SelectedFundActionLabel => SelectedFund?.Status == FundStatus.Archived ? "Restore Fund" : "Remove Fund";
    public string ModeExplanation => SelectedWorkspaceMode switch
    {
        WorkspaceMode.Simple => "Simple mode uses familiar bookkeeping language and keeps advanced accounting detail out of the way.",
        WorkspaceMode.Pro => "Pro mode adds operational accounting detail for treasurers and bookkeepers.",
        WorkspaceMode.Accountant => "Accountant mode exposes the most accounting context while preserving the same underlying books.",
        _ => string.Empty
    };
    public string FundTerminologyHint => "Bank Account tells you where money is held. Fund tells you what money is designated for. Category or account tells you why money moved.";
    public int IntegrityCriticalCount => IntegrityFindings.Count(static finding => finding.Severity == FundIntegritySeverity.Critical);
    public int IntegrityWarningCount => IntegrityFindings.Count(static finding => finding.Severity == FundIntegritySeverity.Warning);

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await SetupWorkspace.InitializeAsync(database, cancellationToken);
        ApplyPersonalization();
        await PeopleWorkspace.InitializeAsync(database, cancellationToken);
        await GivingWorkspace.InitializeAsync(database, cancellationToken);
        await BankingWorkspace.InitializeAsync(database, cancellationToken);
        await ImportWorkspace.InitializeAsync(database, cancellationToken);
        await ExpensesWorkspace.InitializeAsync(database, cancellationToken);
        _accountingStore = new SqliteAccountingStore(database);
        _accountantReportingService = new AccountantReportingService(_accountingStore);
        _fundStore = new SqliteFundAccountingStore(database);
        _fundManagementService = new FundManagementService(_fundStore);
        _fundReportingService = new FundReportingService(_fundStore);
        _integrityScanner = new SqliteFundIntegrityScanner(database, _fundStore);
        DatabaseStatus = "Connected - schema v10 - personalized setup, people, offerings, banking/deposits, adaptive Smart Import, bank reconciliation, vendors/direct expenses, fund reports, accountant reports, backup/recovery, and integrity ready - " + database.DatabasePath;
        await LoadFundsAsync(cancellationToken);
        BuildDashboardSnapshot();
        NotifyDashboardSummaryChanged();
        if (!SetupWorkspace.IsSetupComplete) CurrentSection = WorkspaceSection.Setup;
    }

    [RelayCommand]
    private async Task ShowDashboardAsync()
    {
        CurrentSection = WorkspaceSection.Dashboard;
        await RefreshDashboardAsync();
        StatusMessage = "Dashboard ready.";
    }

    [RelayCommand]
    private async Task RefreshDashboardAsync()
    {
        await GivingWorkspace.RefreshCommand.ExecuteAsync(null);
        await BankingWorkspace.RefreshCommand.ExecuteAsync(null);
        await ExpensesWorkspace.RefreshCommand.ExecuteAsync(null);
        await LoadFundsAsync();
        BuildDashboardSnapshot();
        NotifyDashboardSummaryChanged();
    }

    [RelayCommand]
    private void SelectSimpleMode() => SelectedWorkspaceMode = WorkspaceMode.Simple;

    [RelayCommand]
    private void SelectProMode() => SelectedWorkspaceMode = WorkspaceMode.Pro;

    [RelayCommand]
    private void SelectAccountantMode() => SelectedWorkspaceMode = WorkspaceMode.Accountant;

    [RelayCommand]
    private void SelectChurchBooksLightTheme() => SelectedAppearanceTheme = AppearanceTheme.ChurchBooksLight;

    [RelayCommand]
    private void SelectClassicWhiteTheme() => SelectedAppearanceTheme = AppearanceTheme.ClassicWhite;

    [RelayCommand]
    private void SelectChurchBooksDarkTheme() => SelectedAppearanceTheme = AppearanceTheme.ChurchBooksDark;

    [RelayCommand]
    private async Task ShowFundsAsync()
    {
        CurrentSection = WorkspaceSection.Funds;
        await LoadFundsAsync();
        StatusMessage = "Fund Manager ready.";
    }

    [RelayCommand]
    private async Task ShowServicesAsync()
    {
        CurrentSection = WorkspaceSection.Services;
        await GivingWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "Services and offering batches ready.";
    }

    [RelayCommand]
    private async Task ShowPeopleAsync()
    {
        CurrentSection = WorkspaceSection.People;
        await PeopleWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "People, households, and giving categories ready.";
    }

    [RelayCommand]
    private async Task ShowMembersAsync()
    {
        PeopleTabIndex = 0;
        CurrentSection = WorkspaceSection.People;
        await PeopleWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "Members and donors ready. Register, edit, archive, restore, or permanently delete only unused people here.";
    }

    [RelayCommand]
    private async Task ShowGivingSetupAsync()
    {
        SettingsTabIndex = 1;
        CurrentSection = WorkspaceSection.Setup;
        await PeopleWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "Giving Setup ready. Add, edit, remove, or restore giving categories here; Giving refreshes them automatically.";
    }

    [RelayCommand]
    private async Task ShowGivingAsync()
    {
        CurrentSection = WorkspaceSection.Giving;
        await GivingWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "Giving entry and individual analytics ready.";
    }

    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        if (_backHistory.Count == 0) return;
        var target = _backHistory.Pop();
        _forwardHistory.Push(CurrentSection);
        _historyNavigation = true;
        try
        {
            CurrentSection = target;
            await RefreshHistoryTargetAsync(target);
        }
        finally
        {
            _historyNavigation = false;
            NotifyNavigationHistoryChanged();
        }
        StatusMessage = "Returned to the previous ChurchBooks section.";
    }

    [RelayCommand]
    private async Task NavigateForwardAsync()
    {
        if (_forwardHistory.Count == 0) return;
        var target = _forwardHistory.Pop();
        _backHistory.Push(CurrentSection);
        _historyNavigation = true;
        try
        {
            CurrentSection = target;
            await RefreshHistoryTargetAsync(target);
        }
        finally
        {
            _historyNavigation = false;
            NotifyNavigationHistoryChanged();
        }
        StatusMessage = "Moved forward to the next ChurchBooks section.";
    }

    private async Task RefreshHistoryTargetAsync(WorkspaceSection section)
    {
        switch (section)
        {
            case WorkspaceSection.Dashboard:
                await RefreshDashboardAsync();
                break;
            case WorkspaceSection.Services:
            case WorkspaceSection.Giving:
                await GivingWorkspace.RefreshCommand.ExecuteAsync(null);
                break;
            case WorkspaceSection.People:
                await PeopleWorkspace.RefreshCommand.ExecuteAsync(null);
                break;
            case WorkspaceSection.Funds:
            case WorkspaceSection.Reports:
                await LoadFundsAsync();
                if (section == WorkspaceSection.Reports) await RefreshReportsAsync();
                break;
            case WorkspaceSection.Banking:
                await BankingWorkspace.RefreshCommand.ExecuteAsync(null);
                break;
            case WorkspaceSection.Expenses:
                await ExpensesWorkspace.RefreshCommand.ExecuteAsync(null);
                break;
            case WorkspaceSection.Integrity:
                await RunIntegrityScanAsync();
                break;
        }
    }

    private void NotifyNavigationHistoryChanged()
    {
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
    }

    [RelayCommand]
    private async Task ShowBankingAsync()
    {
        CurrentSection = WorkspaceSection.Banking;
        await BankingWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "Banking, deposit posting, and reconciliation ready.";
    }

    [RelayCommand]
    private void ShowSetup()
    {
        CurrentSection = WorkspaceSection.Setup;
        StatusMessage = "Organization setup and terminology personalization ready.";
    }

    [RelayCommand]
    private void ShowImport()
    {
        CurrentSection = WorkspaceSection.Import;
        StatusMessage = "Adaptive Smart Import ready for CSV/XLS/XLSX interpretation, reusable source profiles, identity review, and staging.";
    }

    [RelayCommand]
    private async Task ShowExpensesAsync()
    {
        CurrentSection = WorkspaceSection.Expenses;
        await ExpensesWorkspace.RefreshCommand.ExecuteAsync(null);
        StatusMessage = "Vendors and direct expenses ready. Posting remains explicit and Fund-aware.";
    }

    [RelayCommand]
    private async Task ShowReportsAsync()
    {
        CurrentSection = WorkspaceSection.Reports;
        await LoadFundsAsync();
        await RefreshReportsAsync();
        StatusMessage = "Reports Center ready.";
    }

    [RelayCommand]
    private async Task ShowIntegrityAsync()
    {
        CurrentSection = WorkspaceSection.Integrity;
        await RunIntegrityScanAsync();
        StatusMessage = "Audit and integrity scan completed.";
    }

    [RelayCommand]
    private async Task RunGlobalSearchAsync()
    {
        if (!_terminologyAliasService.TryResolve(GlobalSearchText, out var section))
        {
            StatusMessage = "No matching feature is available yet. Try Service, Member, Donor, Offering, Giving, Household, Giving Category, Fund, Banking, Expense, Vendor, Smart Import, Report, Audit, or Integrity.";
            return;
        }

        switch (section)
        {
            case WorkspaceSection.Giving:
                await ShowGivingAsync();
                StatusMessage = "Opened Giving. ChurchBooks recognized your offering or contribution term.";
                break;
            case WorkspaceSection.People:
                await ShowPeopleAsync();
                StatusMessage = "Opened People. ChurchBooks recognized your member, donor, household, or giving-category term.";
                break;
            case WorkspaceSection.Funds:
                CurrentSection = WorkspaceSection.Funds;
                await LoadFundsAsync();
                StatusMessage = "Opened Funds. ChurchBooks recognized your familiar accounting term.";
                break;
            case WorkspaceSection.Reports:
                await ShowReportsAsync();
                StatusMessage = "Opened Reports Center. ChurchBooks recognized your reporting term.";
                break;
            case WorkspaceSection.Integrity:
                await ShowIntegrityAsync();
                StatusMessage = "Opened Integrity Center. ChurchBooks recognized your audit or health-check term.";
                break;
            case WorkspaceSection.Banking:
                await ShowBankingAsync();
                StatusMessage = "Opened Banking. ChurchBooks recognized your bank or reconciliation term.";
                break;
            case WorkspaceSection.Import:
                ShowImport();
                StatusMessage = "Opened Smart Import. ChurchBooks recognized your spreadsheet or import term.";
                break;
            case WorkspaceSection.Expenses:
                await ShowExpensesAsync();
                StatusMessage = "Opened Expenses. ChurchBooks recognized your vendor, purchase, payment, or expense term.";
                break;
            default:
                CurrentSection = WorkspaceSection.Dashboard;
                break;
        }
    }

    [RelayCommand]
    private async Task RefreshFundsAsync()
    {
        await LoadFundsAsync();
        StatusMessage = "Fund balances refreshed from the ledger.";
    }

    [RelayCommand]
    private async Task RefreshReportsAsync()
    {
        if (_fundReportingService is null)
        {
            BalanceReportSummary = "Fund reporting is not ready yet.";
            return;
        }

        var asOfDate = DateOnly.FromDateTime((ReportAsOfDate ?? DateTime.Today).Date);
        _currentBalanceReport = await _fundReportingService.BuildBalanceReportAsync(asOfDate, includeArchived: true);
        FundBalanceReportRows.Clear();
        foreach (var row in _currentBalanceReport.Rows)
        {
            FundBalanceReportRows.Add(row);
        }
        BalanceReportSummary = $"As of {asOfDate:yyyy-MM-dd}: total {BaseCurrency} {_currentBalanceReport.TotalBalance:N2}; restricted {BaseCurrency} {_currentBalanceReport.RestrictedBalance:N2}.";

        ReportFund ??= SelectedFund ?? Funds.FirstOrDefault();
        if (ReportFund is null)
        {
            FundActivityReportLines.Clear();
            _currentActivityReport = null;
            ActivityReportSummary = "Create or select a fund before generating fund activity.";
            await RefreshAccountantReportsAsync();
            return;
        }

        var fromDate = DateOnly.FromDateTime((ActivityFromDate ?? new DateTime(DateTime.Today.Year, 1, 1)).Date);
        var toDate = DateOnly.FromDateTime((ActivityToDate ?? DateTime.Today).Date);
        try
        {
            _currentActivityReport = await _fundReportingService.BuildActivityReportAsync(ReportFund.Id, fromDate, toDate);
            FundActivityReportLines.Clear();
            foreach (var line in _currentActivityReport.Lines)
            {
                FundActivityReportLines.Add(line);
            }
            ActivityReportSummary =
                $"{_currentActivityReport.Fund.Name}: opening {BaseCurrency} {_currentActivityReport.OpeningBalance:N2}, " +
                $"increases {_currentActivityReport.Increases:N2}, decreases {_currentActivityReport.Decreases:N2}, " +
                $"closing {_currentActivityReport.ClosingBalance:N2}.";
        }
        catch (ArgumentException ex)
        {
            FundActivityReportLines.Clear();
            _currentActivityReport = null;
            ActivityReportSummary = ex.Message;
        }

        await RefreshAccountantReportsAsync();
    }

    private async Task RefreshAccountantReportsAsync()
    {
        if (_accountantReportingService is null || _accountingStore is null)
        {
            TrialBalanceSummary = "Accountant reporting is not ready yet.";
            IncomeExpenseSummary = "Accountant reporting is not ready yet.";
            GeneralLedgerSummary = "Accountant reporting is not ready yet.";
            return;
        }

        await LoadReportAccountsAsync();
        var asOfDate = DateOnly.FromDateTime((FinancialReportAsOfDate ?? DateTime.Today).Date);
        _currentTrialBalanceReport = await _accountantReportingService.BuildTrialBalanceAsync(asOfDate);
        TrialBalanceRows.Clear();
        foreach (var row in _currentTrialBalanceReport.Rows)
        {
            TrialBalanceRows.Add(row);
        }
        TrialBalanceSummary = _currentTrialBalanceReport.IsBalanced
            ? $"As of {asOfDate:yyyy-MM-dd}: debits and credits agree at {BaseCurrency} {_currentTrialBalanceReport.TotalDebit:N2}."
            : $"Attention: Trial Balance difference is {BaseCurrency} {_currentTrialBalanceReport.Difference:N2}. No automatic correction was made.";

        var fromDate = DateOnly.FromDateTime((FinancialFromDate ?? new DateTime(DateTime.Today.Year, 1, 1)).Date);
        var toDate = DateOnly.FromDateTime((FinancialToDate ?? DateTime.Today).Date);
        try
        {
            _currentIncomeExpenseReport = await _accountantReportingService.BuildIncomeExpenseAsync(fromDate, toDate);
            IncomeReportRows.Clear();
            ExpenseReportRows.Clear();
            foreach (var row in _currentIncomeExpenseReport.IncomeRows) IncomeReportRows.Add(row);
            foreach (var row in _currentIncomeExpenseReport.ExpenseRows) ExpenseReportRows.Add(row);
            IncomeExpenseSummary =
                $"{fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}: income {BaseCurrency} {_currentIncomeExpenseReport.TotalIncome:N2}; " +
                $"expenses {BaseCurrency} {_currentIncomeExpenseReport.TotalExpenses:N2}; net income {BaseCurrency} {_currentIncomeExpenseReport.NetIncome:N2}.";
        }
        catch (ArgumentException ex)
        {
            _currentIncomeExpenseReport = null;
            IncomeReportRows.Clear();
            ExpenseReportRows.Clear();
            IncomeExpenseSummary = ex.Message;
        }

        GeneralLedgerAccount ??= ReportAccounts.FirstOrDefault();
        if (GeneralLedgerAccount is null)
        {
            _currentGeneralLedgerReport = null;
            GeneralLedgerRows.Clear();
            GeneralLedgerSummary = "Create a ledger account before generating the General Ledger.";
            return;
        }

        try
        {
            _currentGeneralLedgerReport = await _accountantReportingService.BuildGeneralLedgerAsync(GeneralLedgerAccount.Id, fromDate, toDate);
            GeneralLedgerRows.Clear();
            foreach (var row in _currentGeneralLedgerReport.Rows) GeneralLedgerRows.Add(row);
            GeneralLedgerSummary =
                $"{GeneralLedgerAccount.Code} - {GeneralLedgerAccount.Name}: opening {BaseCurrency} {_currentGeneralLedgerReport.OpeningBalance:N2}; " +
                $"closing {BaseCurrency} {_currentGeneralLedgerReport.ClosingBalance:N2}.";
        }
        catch (ArgumentException ex)
        {
            _currentGeneralLedgerReport = null;
            GeneralLedgerRows.Clear();
            GeneralLedgerSummary = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            _currentGeneralLedgerReport = null;
            GeneralLedgerRows.Clear();
            GeneralLedgerSummary = ex.Message;
        }
    }

    private async Task LoadReportAccountsAsync()
    {
        if (_accountingStore is null) return;
        var selectedId = GeneralLedgerAccount?.Id;
        var accounts = new List<Account>();
        foreach (var type in Enum.GetValues<AccountType>())
        {
            accounts.AddRange(await _accountingStore.GetAccountsByTypeAsync(type));
        }

        ReportAccounts.Clear();
        foreach (var account in accounts
            .GroupBy(static account => account.Id)
            .Select(static group => group.First())
            .OrderBy(static account => account.Code, StringComparer.OrdinalIgnoreCase))
        {
            ReportAccounts.Add(account);
        }

        GeneralLedgerAccount = selectedId.HasValue
            ? ReportAccounts.FirstOrDefault(account => account.Id == selectedId.Value) ?? ReportAccounts.FirstOrDefault()
            : ReportAccounts.FirstOrDefault();
    }

    [RelayCommand]
    private void ExportFundBalanceCsv()
    {
        if (_currentBalanceReport is null)
        {
            StatusMessage = "Generate the Fund Balance report before exporting.";
            return;
        }

        try
        {
            var path = _csvReportExportService.ExportFundBalanceReport(_currentBalanceReport, GetReportsDirectory());
            StatusMessage = "Fund Balance CSV exported: " + path;
        }
        catch (IOException ex)
        {
            StatusMessage = "CSV export failed: " + ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = "CSV export failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ExportFundActivityCsv()
    {
        if (_currentActivityReport is null)
        {
            StatusMessage = "Generate the Fund Activity report before exporting.";
            return;
        }

        try
        {
            var path = _csvReportExportService.ExportFundActivityReport(_currentActivityReport, GetReportsDirectory());
            StatusMessage = "Fund Activity CSV exported: " + path;
        }
        catch (IOException ex)
        {
            StatusMessage = "CSV export failed: " + ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = "CSV export failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ExportTrialBalanceCsv()
    {
        if (_currentTrialBalanceReport is null)
        {
            StatusMessage = "Generate the Trial Balance before exporting.";
            return;
        }

        ExportCsv(() => _csvReportExportService.ExportTrialBalanceReport(_currentTrialBalanceReport, GetReportsDirectory()), "Trial Balance");
    }

    [RelayCommand]
    private void ExportIncomeExpenseCsv()
    {
        if (_currentIncomeExpenseReport is null)
        {
            StatusMessage = "Generate the Income & Expense Statement before exporting.";
            return;
        }

        ExportCsv(() => _csvReportExportService.ExportIncomeExpenseReport(_currentIncomeExpenseReport, GetReportsDirectory()), "Income & Expense Statement");
    }

    [RelayCommand]
    private void ExportGeneralLedgerCsv()
    {
        if (_currentGeneralLedgerReport is null)
        {
            StatusMessage = "Generate the General Ledger before exporting.";
            return;
        }

        ExportCsv(() => _csvReportExportService.ExportGeneralLedgerReport(_currentGeneralLedgerReport, GetReportsDirectory()), "General Ledger");
    }

    private void ExportCsv(Func<string> exportAction, string reportName)
    {
        try
        {
            var path = exportAction();
            StatusMessage = reportName + " CSV exported: " + path;
        }
        catch (IOException ex)
        {
            StatusMessage = "CSV export failed: " + ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = "CSV export failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task RunIntegrityScanAsync()
    {
        if (_integrityScanner is null)
        {
            IntegritySummary = "Integrity Center is not ready yet.";
            return;
        }

        var report = await _integrityScanner.ScanAsync();
        IntegrityFindings.Clear();
        foreach (var finding in report.Findings)
        {
            IntegrityFindings.Add(finding);
        }
        OnPropertyChanged(nameof(IntegrityCriticalCount));
        OnPropertyChanged(nameof(IntegrityWarningCount));
        IntegritySummary = report.IsClean
            ? "No fund-accounting integrity findings were detected."
            : $"Integrity scan found {report.CriticalCount} critical and {report.WarningCount} warning item(s). No automatic corrections were made.";
    }

    [RelayCommand]
    private async Task ReviewRestrictionReleaseAsync()
    {
        if (_fundStore is null)
        {
            ReleaseReviewResult = "Fund storage is not ready yet.";
            return;
        }

        var target = ReportFund ?? SelectedFund;
        if (target is null)
        {
            ReleaseReviewResult = "Select a fund before reviewing a restriction release.";
            return;
        }

        if (!decimal.TryParse(ReleaseAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount))
        {
            ReleaseReviewResult = "Enter a valid numeric release amount.";
            return;
        }

        var fund = await _fundStore.GetFundAsync(target.Id);
        if (fund is null)
        {
            ReleaseReviewResult = "The selected fund no longer exists.";
            return;
        }

        var balances = await _fundStore.GetFundBalancesAsync(new[] { fund.Id });
        var balance = balances.TryGetValue(fund.Id, out var current) ? current : 0m;
        var review = _restrictionReleaseReviewService.Review(fund, balance, amount, ReleaseEvidence);
        ReleaseReviewResult = review.Message + " No journal was posted.";
    }

    private static string GetReportsDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ChurchBooks",
        "Reports");

    private static string BuildInternalCode(string prefix, string name)
    {
        var letters = new string((name ?? string.Empty)
            .Where(static ch => char.IsLetterOrDigit(ch))
            .Take(10)
            .ToArray())
            .ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(letters)) letters = "ITEM";
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var code = $"{prefix}-{letters}-{suffix}";
        return code.Length <= 20 ? code : code[..20];
    }

    [RelayCommand]
    private void AddFund()
    {
        _editingFundId = null;
        ClearEditorErrors();
        EditorTitle = "Add Fund";
        EditorCode = string.Empty;
        EditorName = string.Empty;
        EditorPurpose = string.Empty;
        EditorRestriction = FundRestriction.Unrestricted;
        EditorOverspendPolicy = FundOverspendPolicy.Allow;
        IsFundEditorOpen = true;
        StatusMessage = "Enter the fund details. Nothing is saved until you choose Save Fund.";
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedFund))]
    private void EditFund()
    {
        if (SelectedFund is null)
        {
            return;
        }

        _editingFundId = SelectedFund.Id;
        ClearEditorErrors();
        EditorTitle = "Edit Fund";
        EditorCode = SelectedFund.Code;
        EditorName = SelectedFund.Name;
        EditorPurpose = SelectedFund.Purpose;
        EditorRestriction = SelectedFund.Restriction;
        EditorOverspendPolicy = SelectedFund.OverspendPolicy;
        IsFundEditorOpen = true;
        StatusMessage = "Editing fund details. Posted accounting history will not be rewritten.";
    }

    [RelayCommand]
    private void CancelFundEdit()
    {
        IsFundEditorOpen = false;
        _editingFundId = null;
        ClearEditorErrors();
        StatusMessage = "Fund edit cancelled.";
    }

    [RelayCommand]
    private async Task SaveFundAsync()
    {
        if (_fundManagementService is null)
        {
            EditorGeneralError = "Fund storage is not ready yet.";
            return;
        }

        ClearEditorErrors();
        var internalCode = _editingFundId.HasValue
            ? EditorCode.Trim()
            : BuildInternalCode("FUND", EditorName);
        var draft = new FundDraft
        {
            Code = internalCode,
            Name = EditorName.Trim(),
            Purpose = EditorPurpose.Trim(),
            Restriction = EditorRestriction,
            OverspendPolicy = EditorOverspendPolicy
        };
        var validation = await _fundDraftValidator.ValidateAsync(draft);
        if (!validation.IsValid)
        {
            EditorCodeError = validation.Errors.FirstOrDefault(static error => error.PropertyName == nameof(FundDraft.Code))?.ErrorMessage ?? string.Empty;
            EditorNameError = validation.Errors.FirstOrDefault(static error => error.PropertyName == nameof(FundDraft.Name))?.ErrorMessage ?? string.Empty;
            EditorPurposeError = validation.Errors.FirstOrDefault(static error => error.PropertyName == nameof(FundDraft.Purpose))?.ErrorMessage ?? string.Empty;
            EditorGeneralError = "Review the highlighted fields before saving.";
            return;
        }

        try
        {
            Fund saved;
            if (_editingFundId.HasValue)
            {
                var current = await _fundStore!.GetFundAsync(_editingFundId.Value)
                    ?? throw new FundManagementException("The selected fund no longer exists.");
                saved = await _fundManagementService.UpdateFundAsync(new Fund(
                    current.Id,
                    draft.Code,
                    draft.Name,
                    draft.Restriction,
                    draft.OverspendPolicy,
                    draft.Purpose,
                    current.Status,
                    current.ArchivedUtc));
            }
            else
            {
                saved = await _fundManagementService.CreateFundAsync(new Fund(
                    Guid.NewGuid(),
                    draft.Code,
                    draft.Name,
                    draft.Restriction,
                    draft.OverspendPolicy,
                    draft.Purpose));
            }

            IsFundEditorOpen = false;
            _editingFundId = null;
            await LoadFundsAsync();
            SelectedFund = Funds.FirstOrDefault(item => item.Id == saved.Id);
            StatusMessage = $"Fund '{saved.Name}' saved.";
        }
        catch (FundManagementException ex)
        {
            EditorGeneralError = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            EditorGeneralError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CreateGeneralFundAsync()
    {
        if (_fundManagementService is null || _fundStore is null)
        {
            StatusMessage = "Fund storage is not ready yet. Please wait for ChurchBooks to finish opening.";
            return;
        }

        try
        {
            var existingFunds = await _fundManagementService.GetFundsAsync(includeArchived: true);
            var existing = existingFunds.FirstOrDefault(fund =>
                fund.Code.Equals("GENERAL", StringComparison.OrdinalIgnoreCase) ||
                fund.Code.Equals("GEN", StringComparison.OrdinalIgnoreCase) ||
                fund.Name.Equals("General Fund", StringComparison.OrdinalIgnoreCase));

            FundSearchText = string.Empty;

            if (existing is not null)
            {
                var wasArchived = existing.Status == FundStatus.Archived;
                if (wasArchived)
                {
                    await _fundManagementService.ReactivateFundAsync(existing.Id);
                }

                await LoadFundsAsync();
                SelectedFund = Funds.FirstOrDefault(item => item.Id == existing.Id)
                    ?? _allFunds.FirstOrDefault(item => item.Id == existing.Id);
                IsFundEditorOpen = false;
                StatusMessage = wasArchived
                    ? "General Fund restored and selected. No accounting entry was created."
                    : "General Fund already exists and is now selected. No duplicate fund or accounting entry was created.";
                return;
            }

            var created = await _fundManagementService.CreateFundAsync(new Fund(
                Guid.NewGuid(),
                "GENERAL",
                "General Fund",
                FundRestriction.Unrestricted,
                FundOverspendPolicy.Allow,
                "Unrestricted resources available for the church's general ministry and operations."));
            await LoadFundsAsync();
            SelectedFund = Funds.FirstOrDefault(item => item.Id == created.Id)
                ?? _allFunds.FirstOrDefault(item => item.Id == created.Id);
            IsFundEditorOpen = false;
            StatusMessage = "General Fund created and selected. No accounting entry was created.";
        }
        catch (FundManagementException ex)
        {
            StatusMessage = "General Fund was not changed: " + ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = "General Fund was not changed: " + ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedFund))]
    private async Task ArchiveOrReactivateFundAsync()
    {
        if (SelectedFund is null || _fundManagementService is null)
        {
            return;
        }

        if (SelectedFund.Status == FundStatus.Archived)
        {
            await _fundManagementService.ReactivateFundAsync(SelectedFund.Id);
            _pendingArchiveFundId = null;
            await LoadFundsAsync();
            StatusMessage = $"Fund '{SelectedFund?.Name ?? ""}' reactivated.";
            return;
        }

        if (SelectedFund.Balance != 0m && _pendingArchiveFundId != SelectedFund.Id)
        {
            _pendingArchiveFundId = SelectedFund.Id;
            StatusMessage = $"This fund still has {SelectedFund.BalanceDisplay}. Review the balance, then choose Remove Fund again to confirm.";
            return;
        }

        var name = SelectedFund.Name;
        await _fundManagementService.ArchiveFundAsync(SelectedFund.Id);
        _pendingArchiveFundId = null;
        await LoadFundsAsync();
        StatusMessage = $"Fund '{name}' removed from active use. Historical activity remains intact and the fund can be restored later.";
    }

    private bool CanEditSelectedFund() => SelectedFund is not null;

    private async Task LoadFundsAsync(CancellationToken cancellationToken = default)
    {
        if (_fundManagementService is null || _fundStore is null)
        {
            return;
        }

        var selectedId = SelectedFund?.Id;
        var funds = await _fundManagementService.GetFundsAsync(includeArchived: true, cancellationToken);
        var balances = await _fundStore.GetFundBalancesAsync(funds.Select(static fund => fund.Id), cancellationToken);

        _allFunds.Clear();
        foreach (var fund in funds)
        {
            _allFunds.Add(new FundListItemViewModel(
                fund.Id,
                fund.Code,
                fund.Name,
                fund.Purpose,
                fund.Restriction,
                fund.OverspendPolicy,
                fund.Status,
                balances.TryGetValue(fund.Id, out var balance) ? balance : 0m));
        }

        ApplyFundFilter();
        SelectedFund = selectedId.HasValue ? Funds.FirstOrDefault(item => item.Id == selectedId.Value) : Funds.FirstOrDefault();
        if (ReportFund is null || !_allFunds.Any(item => item.Id == ReportFund.Id))
        {
            ReportFund = SelectedFund ?? _allFunds.FirstOrDefault();
        }
        NotifyFundSummaryChanged();
    }

    private void ApplyFundFilter()
    {
        var search = FundSearchText.Trim();
        var filtered = _allFunds.Where(fund =>
            (IncludeArchived || fund.Status == FundStatus.Active) &&
            (string.IsNullOrWhiteSpace(search) ||
             fund.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             fund.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             fund.Purpose.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             fund.RestrictionLabel.Contains(search, StringComparison.OrdinalIgnoreCase)));

        Funds.Clear();
        foreach (var fund in filtered)
        {
            Funds.Add(fund);
        }

        OnPropertyChanged(nameof(HasFunds));
        OnPropertyChanged(nameof(HasNoFunds));
    }

    private void NotifyFundSummaryChanged()
    {
        OnPropertyChanged(nameof(ActiveFundCount));
        OnPropertyChanged(nameof(RestrictedFundCount));
        OnPropertyChanged(nameof(TotalFundBalance));
        OnPropertyChanged(nameof(TotalFundBalanceDisplay));
        OnPropertyChanged(nameof(HasFunds));
        OnPropertyChanged(nameof(HasNoFunds));
    }

    private void SavePreferencesSafe()
    {
        try
        {
            _preferenceStore.Save(new UiPreferences(SelectedWorkspaceMode, SelectedHelpLevel)
            {
                AppearanceTheme = SelectedAppearanceTheme,
                OrganizationLogoPath = OrganizationLogoPath,
                BackupDirectory = BackupDirectory,
                LastBackupPath = LastBackupPath,
                LastBackupUtc = LastBackupUtc
            });
        }
        catch (IOException)
        {
            StatusMessage = "Your display preference could not be saved, but ChurchBooks can continue normally.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Your display preference could not be saved, but ChurchBooks can continue normally.";
        }
    }

    private void BuildDashboardSnapshot()
    {
        var bankNames = BankingWorkspace.BankAccounts
            .ToDictionary(static item => item.Account.Id, static item => item.Name);

        var rows = new List<DashboardTransactionItem>();
        rows.AddRange(ExpensesWorkspace.Expenses
            .Where(static item => item.Status == DirectExpenseStatus.Posted)
            .Select(item => new DashboardTransactionItem(
                item.ExpenseDate,
                string.IsNullOrWhiteSpace(item.Reference) ? "Direct expense" : item.Reference,
                "Expense",
                $"-{item.Currency.Value} {item.TotalAmount:N2}")));

        rows.AddRange(BankingWorkspace.Deposits
            .Where(static item => item.Status == BankDepositStatus.Posted)
            .Select(item => new DashboardTransactionItem(
                item.DepositDate,
                string.IsNullOrWhiteSpace(item.Reference)
                    ? $"Bank deposit - {bankNames.GetValueOrDefault(item.BankAccountId, "Bank account")}"
                    : item.Reference,
                "Deposit",
                $"+{item.Currency.Value} {item.TotalAmount:N2}")));

        DashboardTransactions.Clear();
        foreach (var item in rows.OrderByDescending(static item => item.Date).ThenBy(static item => item.Description).Take(6))
        {
            DashboardTransactions.Add(item);
        }

        DashboardFundBalances.Clear();
        foreach (var fund in _allFunds
            .Where(static item => item.Status == FundStatus.Active)
            .OrderByDescending(static item => Math.Abs(item.Balance))
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5))
        {
            DashboardFundBalances.Add(fund);
        }
    }

    private void NotifyDashboardSummaryChanged()
    {
        OnPropertyChanged(nameof(LatestServiceDisplay));
        OnPropertyChanged(nameof(LatestServiceCaption));
        OnPropertyChanged(nameof(OrganizationSetupAlert));
        OnPropertyChanged(nameof(BankingAlert));
        OnPropertyChanged(nameof(FiscalYearDisplay));
        OnPropertyChanged(nameof(TaxIdentifierDisplay));
        OnPropertyChanged(nameof(DatabaseConnectionDisplay));
        OnPropertyChanged(nameof(SystemStatusDisplay));
        OnPropertyChanged(nameof(CurrentPeriod));
    }

    private void ClearEditorErrors()
    {
        EditorCodeError = string.Empty;
        EditorNameError = string.Empty;
        EditorPurposeError = string.Empty;
        EditorGeneralError = string.Empty;
    }

    partial void OnDatabaseStatusChanged(string value)
    {
        OnPropertyChanged(nameof(DatabaseConnectionDisplay));
        OnPropertyChanged(nameof(SystemStatusDisplay));
    }

    partial void OnCurrentSectionChanging(WorkspaceSection value)
    {
        if (_historyNavigation || value == CurrentSection) return;
        _backHistory.Push(CurrentSection);
        _forwardHistory.Clear();
        NotifyNavigationHistoryChanged();
    }

    partial void OnCurrentSectionChanged(WorkspaceSection value)
    {
        OnPropertyChanged(nameof(IsDashboardVisible));
        OnPropertyChanged(nameof(IsServicesVisible));
        OnPropertyChanged(nameof(IsFundsVisible));
        OnPropertyChanged(nameof(IsReportsVisible));
        OnPropertyChanged(nameof(IsIntegrityVisible));
        OnPropertyChanged(nameof(IsPeopleVisible));
        OnPropertyChanged(nameof(IsGivingVisible));
        OnPropertyChanged(nameof(IsBankingVisible));
        OnPropertyChanged(nameof(IsSetupVisible));
        OnPropertyChanged(nameof(IsImportVisible));
        OnPropertyChanged(nameof(IsExpensesVisible));
        NotifyNavigationHistoryChanged();
    }

    partial void OnSelectedWorkspaceModeChanged(WorkspaceMode value)
    {
        OnPropertyChanged(nameof(ShowAdvancedFundDetails));
        OnPropertyChanged(nameof(ModeExplanation));
        SavePreferencesSafe();
    }

    partial void OnSelectedHelpLevelChanged(HelpLevel value)
    {
        OnPropertyChanged(nameof(ShowGuidance));
        SavePreferencesSafe();
    }

    partial void OnSelectedAppearanceThemeChanged(AppearanceTheme value)
    {
        ThemeManager.Apply(value);
        SavePreferencesSafe();
        StatusMessage = value switch
        {
            AppearanceTheme.ChurchBooksLight => "ChurchBooks Light theme applied.",
            AppearanceTheme.ClassicWhite => "Classic White theme applied.",
            AppearanceTheme.ChurchBooksDark => "ChurchBooks Dark theme applied.",
            _ => "Theme updated."
        };
    }

    partial void OnOrganizationLogoPathChanged(string value) => SavePreferencesSafe();

    partial void OnBackupDirectoryChanged(string value) => SavePreferencesSafe();

    partial void OnLastBackupPathChanged(string value) => SavePreferencesSafe();

    partial void OnLastBackupUtcChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(BackupFooterDisplay));
        OnPropertyChanged(nameof(LastBackupDisplay));
        OnPropertyChanged(nameof(BackupAlert));
        SavePreferencesSafe();
    }

    public void SetOrganizationLogo(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            StatusMessage = "Choose an existing PNG or JPEG image for the organization logo.";
            return;
        }

        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Length > 10 * 1024 * 1024)
            {
                StatusMessage = "The organization logo must be 10 MB or smaller.";
                return;
            }

            // Validate that WPF can decode the selected image before copying it.
            using (var stream = File.OpenRead(sourcePath))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0)
                {
                    StatusMessage = "The selected file is not a readable image.";
                    return;
                }
            }

            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension is not ".png" and not ".jpg" and not ".jpeg")
            {
                StatusMessage = "Use a PNG or JPEG image for the organization logo.";
                return;
            }

            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChurchBooks", "Branding");
            Directory.CreateDirectory(directory);
            foreach (var old in Directory.EnumerateFiles(directory, "organization-logo.*"))
            {
                File.Delete(old);
            }

            var destination = Path.Combine(directory, "organization-logo" + extension);
            File.Copy(sourcePath, destination, overwrite: true);
            OrganizationLogoPath = destination;
            StatusMessage = "Organization logo saved. It now appears in ChurchBooks organization branding.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            StatusMessage = "The organization logo could not be saved: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RemoveOrganizationLogo()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(OrganizationLogoPath) && File.Exists(OrganizationLogoPath))
            {
                File.Delete(OrganizationLogoPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "The saved logo file could not be removed: " + ex.Message;
            return;
        }

        OrganizationLogoPath = string.Empty;
        StatusMessage = "Organization logo removed. ChurchBooks will use the neutral organization fallback.";
    }

    public void SetBackupDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            StatusMessage = "Choose a valid backup folder.";
            return;
        }
        BackupDirectory = Path.GetFullPath(directory);
        BackupOperationStatus = "Backup folder ready: " + BackupDirectory;
        StatusMessage = "Backup folder updated. No accounting data was changed.";
    }

    public void RecordBackupSuccess(string backupPath, DateTimeOffset createdUtc, string sha256)
    {
        LastBackupPath = backupPath;
        LastBackupUtc = createdUtc;
        BackupOperationStatus = "Verified backup created: " + backupPath + " (SHA-256 " + sha256 + ")";
        StatusMessage = "Verified ChurchBooks backup created successfully.";
        OnPropertyChanged(nameof(BackupFooterDisplay));
        OnPropertyChanged(nameof(LastBackupDisplay));
        OnPropertyChanged(nameof(BackupAlert));
    }

    public void RecordBackupVerification(string message)
    {
        BackupOperationStatus = message;
        StatusMessage = message;
    }

    public void RecordRestoreSuccess(string sourcePath, string safetyBackupPath, DateTimeOffset restoredUtc)
    {
        BackupOperationStatus = "Restore completed from " + sourcePath + ". Pre-restore safety backup: " + safetyBackupPath;
        StatusMessage = "ChurchBooks restored the selected verified backup and reloaded the local books.";
        OnPropertyChanged(nameof(BackupFooterDisplay));
        OnPropertyChanged(nameof(BackupAlert));
    }

    private static string GetDefaultBackupDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ChurchBooks",
        "Backups");

    partial void OnFundSearchTextChanged(string value) => ApplyFundFilter();
    partial void OnIncludeArchivedChanged(bool value) => ApplyFundFilter();

    partial void OnSelectedFundChanged(FundListItemViewModel? value)
    {
        _pendingArchiveFundId = null;
        OnPropertyChanged(nameof(SelectedFundActionLabel));
        EditFundCommand.NotifyCanExecuteChanged();
        ArchiveOrReactivateFundCommand.NotifyCanExecuteChanged();
    }

    partial void OnReportFundChanged(FundListItemViewModel? value)
    {
        ReleaseReviewResult = "No restriction-release review has been run. Phase 3D never posts a release automatically.";
    }

    partial void OnEditorRestrictionChanged(FundRestriction value)
    {
        if (!_editingFundId.HasValue)
        {
            EditorOverspendPolicy = value switch
            {
                FundRestriction.Unrestricted => FundOverspendPolicy.Allow,
                FundRestriction.BoardDesignated => FundOverspendPolicy.Warn,
                FundRestriction.DonorRestricted => FundOverspendPolicy.Block,
                FundRestriction.Endowment => FundOverspendPolicy.Block,
                _ => FundOverspendPolicy.Warn
            };
        }
    }
    private void ApplyPersonalization()
    {
        _terminologyAliasService.SetCustomAliases(SetupWorkspace.CustomAliases);
        OnPropertyChanged(nameof(ChurchName));
        OnPropertyChanged(nameof(BaseCurrency));
        OnPropertyChanged(nameof(FiscalYearDisplay));
        OnPropertyChanged(nameof(TaxIdentifierDisplay));
        OnPropertyChanged(nameof(OrganizationSetupAlert));
        var terminology = SetupWorkspace.GetCatalog();
        GivingWorkspace.ApplyTerminology(terminology, SetupWorkspace.BaseCurrency);
        BankingWorkspace.ApplyPersonalization(SetupWorkspace.BaseCurrency, terminology);
        ExpensesWorkspace.ApplyPersonalization(SetupWorkspace.BaseCurrency);
        OnPropertyChanged(nameof(TotalFundBalanceDisplay));
    }

}

public sealed record DashboardTransactionItem(DateOnly Date, string Description, string Type, string AmountDisplay)
{
    public string DateDisplay => Date.ToString("MMM d", CultureInfo.CurrentCulture);
}
