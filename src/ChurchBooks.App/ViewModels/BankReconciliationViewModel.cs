using System.Collections.ObjectModel;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.Accounting.Reconciliation;
using ChurchBooks.Accounting.Services;
using ChurchBooks.App.Models;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class BankReconciliationViewModel : ObservableObject
{
    private SqliteBankingStore? _bankingStore;
    private SqliteSmartImportStore? _importStore;
    private SqliteBankReconciliationStore? _reconciliationStore;
    private BankReconciliationService? _service;

    public ObservableCollection<BankAccount> BankAccounts { get; } = new();
    public ObservableCollection<ImportSession> StagedImportSessions { get; } = new();
    public ObservableCollection<BankReconciliation> Reconciliations { get; } = new();
    public ObservableCollection<StatementLineSelectionItem> StatementLines { get; } = new();
    public ObservableCollection<ReconciliationBookSelectionItem> BookItems { get; } = new();
    public ObservableCollection<ReconciliationMatchGroupListItem> MatchGroups { get; } = new();

    public IReadOnlyList<BankStatementAmountConvention> AmountConventions { get; } =
        Enum.GetValues<BankStatementAmountConvention>();

    [ObservableProperty] private BankAccount? _selectedBankAccount;
    [ObservableProperty] private ImportSession? _selectedImportSession;
    [ObservableProperty] private BankStatementAmountConvention _selectedAmountConvention = BankStatementAmountConvention.SignedAmount;
    [ObservableProperty] private BankReconciliation? _selectedReconciliation;
    [ObservableProperty] private ReconciliationMatchGroupListItem? _selectedMatchGroup;
    [ObservableProperty] private DateTime? _statementStartDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _statementEndDate = DateTime.Today;
    [ObservableProperty] private decimal _statementEndingBalance;
    [ObservableProperty] private string _baseCurrency = "PHP";
    [ObservableProperty] private string _statusMessage =
        "Import a staged bank statement, start a statement period, then match statement lines to book entries.";
    [ObservableProperty] private string _differenceDisplay = "PHP 0.00";
    [ObservableProperty] private string _bookEndingDisplay = "PHP 0.00";
    [ObservableProperty] private string _outstandingDisplay = "PHP 0.00";
    [ObservableProperty] private string _expectedStatementDisplay = "PHP 0.00";
    [ObservableProperty] private bool _canComplete;

    public void ApplyPersonalization(string baseCurrency)
    {
        BaseCurrency = string.IsNullOrWhiteSpace(baseCurrency)
            ? "PHP"
            : baseCurrency.Trim().ToUpperInvariant();
        ResetSummaryDisplays();
    }

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new Phase8DatabaseMigrator(database).InitializeAsync(cancellationToken);

        _bankingStore = new SqliteBankingStore(database);
        _importStore = new SqliteSmartImportStore(database);
        _reconciliationStore = new SqliteBankReconciliationStore(database);
        _service = new BankReconciliationService(
            _bankingStore,
            new SqliteAccountingStore(database),
            _importStore,
            _reconciliationStore);

        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_bankingStore is null || _importStore is null || _reconciliationStore is null) return;

        var selectedBankId = SelectedBankAccount?.Id;
        var selectedImportId = SelectedImportSession?.Id;
        var selectedReconciliationId = SelectedReconciliation?.Id;

        BankAccounts.Clear();
        foreach (var bank in await _bankingStore.GetBankAccountsAsync(false, cancellationToken))
        {
            BankAccounts.Add(bank);
        }

        StagedImportSessions.Clear();
        foreach (var session in await _importStore.GetSessionsAsync(cancellationToken))
        {
            if (!await _reconciliationStore.IsImportSessionLinkedAsync(session.Id, cancellationToken))
            {
                StagedImportSessions.Add(session);
            }
        }

        SelectedBankAccount = selectedBankId.HasValue
            ? BankAccounts.FirstOrDefault(bank => bank.Id == selectedBankId.Value)
            : BankAccounts.FirstOrDefault();
        SelectedImportSession = selectedImportId.HasValue
            ? StagedImportSessions.FirstOrDefault(session => session.Id == selectedImportId.Value)
            : StagedImportSessions.FirstOrDefault();

        await RefreshReconciliationsAsync(selectedReconciliationId, cancellationToken);
    }

    [RelayCommand]
    private async Task ImportStatementAsync()
    {
        if (_service is null || SelectedBankAccount is null || SelectedImportSession is null) return;
        try
        {
            var imported = await _service.ImportStatementSessionAsync(
                SelectedBankAccount.Id,
                SelectedImportSession.Id,
                SelectedAmountConvention);
            StatusMessage =
                $"{imported} statement row(s) linked to {SelectedBankAccount.Name}. No journal entries were created.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StartReconciliationAsync()
    {
        if (_service is null || SelectedBankAccount is null || StatementStartDate is null || StatementEndDate is null) return;
        try
        {
            var reconciliation = await _service.StartAsync(
                SelectedBankAccount.Id,
                DateOnly.FromDateTime(StatementStartDate.Value.Date),
                DateOnly.FromDateTime(StatementEndDate.Value.Date),
                StatementEndingBalance);
            SelectedReconciliation = reconciliation;
            StatusMessage = "Draft reconciliation created. Matching does not create adjustment journals.";
            await RefreshReconciliationsAsync(reconciliation.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RefreshSummaryAsync()
    {
        if (_service is null || SelectedReconciliation is null)
        {
            ClearSummary();
            return;
        }

        try
        {
            var summary = await _service.BuildSummaryAsync(SelectedReconciliation.Id);
            PopulateSummary(summary);
            StatusMessage = summary.CanComplete
                ? "Reconciliation balances exactly and every statement line is matched. It can be completed."
                : "Continue matching. ChurchBooks never creates an automatic balancing adjustment.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MatchSelectedAsync()
    {
        if (_service is null || SelectedReconciliation is null) return;
        try
        {
            var statementIds = StatementLines.Where(item => item.IsSelected).Select(item => item.Line.Id).ToArray();
            var journalIds = BookItems.Where(item => item.IsSelected).Select(item => item.Item.JournalEntryId).ToArray();
            await _service.MatchAsync(SelectedReconciliation.Id, statementIds, journalIds);
            StatusMessage = "Selected statement and book entries matched with exact signed totals.";
            await RefreshSummaryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AutoMatchExactAsync()
    {
        if (_service is null || SelectedReconciliation is null) return;
        try
        {
            var count = await _service.ApplyExactAutoMatchesAsync(SelectedReconciliation.Id);
            StatusMessage = count == 0
                ? "No unambiguous exact matches were found. Nothing was changed."
                : $"{count} unambiguous exact match(es) applied. Review before completion.";
            await RefreshSummaryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedMatchAsync()
    {
        if (_service is null || SelectedReconciliation is null || SelectedMatchGroup is null) return;
        try
        {
            await _service.RemoveMatchAsync(SelectedReconciliation.Id, SelectedMatchGroup.Group.Id);
            StatusMessage = "Draft match removed.";
            await RefreshSummaryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CompleteReconciliationAsync()
    {
        if (_service is null || SelectedReconciliation is null) return;
        try
        {
            await _service.CompleteAsync(SelectedReconciliation.Id);
            StatusMessage = "Reconciliation completed and locked. No balancing journal was generated.";
            await RefreshReconciliationsAsync(SelectedReconciliation.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RefreshReconciliationsAsync(
        Guid? preferredReconciliationId = null,
        CancellationToken cancellationToken = default)
    {
        if (_reconciliationStore is null || SelectedBankAccount is null)
        {
            Reconciliations.Clear();
            ClearSummary();
            return;
        }

        var requestedId = preferredReconciliationId ?? SelectedReconciliation?.Id;
        Reconciliations.Clear();
        foreach (var reconciliation in await _reconciliationStore.GetReconciliationsAsync(
            SelectedBankAccount.Id,
            cancellationToken))
        {
            Reconciliations.Add(reconciliation);
        }

        SelectedReconciliation = requestedId.HasValue
            ? Reconciliations.FirstOrDefault(item => item.Id == requestedId.Value)
            : Reconciliations.FirstOrDefault();

        if (SelectedReconciliation is null) ClearSummary();
    }

    private void PopulateSummary(BankReconciliationSummary summary)
    {
        StatementLines.Clear();
        foreach (var line in summary.UnmatchedStatementLines)
        {
            StatementLines.Add(new StatementLineSelectionItem(line));
        }

        BookItems.Clear();
        foreach (var item in summary.UnmatchedBookItems)
        {
            BookItems.Add(new ReconciliationBookSelectionItem(item));
        }

        MatchGroups.Clear();
        foreach (var group in summary.MatchGroups)
        {
            MatchGroups.Add(new ReconciliationMatchGroupListItem(group));
        }

        BookEndingDisplay = Format(summary.BookEndingBalance);
        OutstandingDisplay = Format(summary.OutstandingBookAmount);
        ExpectedStatementDisplay = Format(summary.ExpectedStatementBalance);
        DifferenceDisplay = Format(summary.Difference);
        CanComplete = summary.CanComplete &&
            summary.Reconciliation.Status == BankReconciliationStatus.Draft;
    }

    private void ClearSummary()
    {
        StatementLines.Clear();
        BookItems.Clear();
        MatchGroups.Clear();
        CanComplete = false;
        ResetSummaryDisplays();
    }

    private Task ClearSummaryAsync()
    {
        ClearSummary();
        return Task.CompletedTask;
    }

    private void ResetSummaryDisplays()
    {
        BookEndingDisplay = Format(0m);
        OutstandingDisplay = Format(0m);
        ExpectedStatementDisplay = Format(0m);
        DifferenceDisplay = Format(0m);
    }

    private string Format(decimal amount) => $"{BaseCurrency} {amount:N2}";
}
