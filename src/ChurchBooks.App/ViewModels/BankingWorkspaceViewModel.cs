using System.Collections.ObjectModel;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Accounting.Services;
using ChurchBooks.App.Models;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class BankingWorkspaceViewModel : ObservableObject
{
    public BankReconciliationViewModel Reconciliation { get; } = new();

    private SqliteBankingStore? _bankingStore;
    private SqliteAccountingStore? _accountingStore;
    private SqliteOfferingStore? _offeringStore;
    private SqlitePeopleGivingStore? _peopleStore;
    private BankingManagementService? _service;

    public ObservableCollection<BankAccountListItemViewModel> BankAccounts { get; } = new();
    public ObservableCollection<BankDeposit> Deposits { get; } = new();
    public ObservableCollection<ContributionDepositItem> AvailableContributions { get; } = new();
    public ObservableCollection<GivingCategory> GivingCategories { get; } = new();
    public ObservableCollection<Account> IncomeAccounts { get; } = new();

    [ObservableProperty] private BankAccountListItemViewModel? _selectedBankAccount;
    [ObservableProperty] private BankDeposit? _selectedDeposit;
    [ObservableProperty] private string _bankName = string.Empty;
    [ObservableProperty] private string _institutionName = string.Empty;
    [ObservableProperty] private string _lastFour = string.Empty;
    [ObservableProperty] private string _ledgerCode = "1010";
    [ObservableProperty] private string _ledgerName = "Operating Bank";
    [ObservableProperty] private DateTime? _depositDate = DateTime.Today;
    [ObservableProperty] private string _depositReference = string.Empty;
    [ObservableProperty] private string _depositMemo = string.Empty;
    [ObservableProperty] private GivingCategory? _selectedCategory;
    [ObservableProperty] private Account? _selectedIncomeAccount;
    [ObservableProperty] private string _baseCurrency = "PHP";
    [ObservableProperty] private string _bankAccountPluralLabel = "Bank Accounts";
    [ObservableProperty] private string _depositPluralLabel = "Deposits";
    [ObservableProperty] private string _givingCategorySingularLabel = "Giving Category";
    [ObservableProperty] private string _statusMessage = "Add a bank account, confirm category mappings, then group recorded contributions into deposits.";
    [ObservableProperty] private string _postingPreview = "Select a draft deposit to preview the journal bridge.";

    public int ActiveBankCount => BankAccounts.Count(item => item.Account.Status == BankAccountStatus.Active);
    public decimal TotalBookBalance => BankAccounts.Sum(item => item.BookBalance);
    public string TotalBookBalanceDisplay => $"{BaseCurrency} {TotalBookBalance:N2}";
    public int DraftDepositCount => Deposits.Count(deposit => deposit.Status == BankDepositStatus.Draft);
    public int PostedDepositCount => Deposits.Count(deposit => deposit.Status == BankDepositStatus.Posted);

    public void ApplyPersonalization(string baseCurrency, ChurchBooks.Accounting.Setup.TerminologyCatalog terminology)
    {
        BaseCurrency = string.IsNullOrWhiteSpace(baseCurrency) ? "PHP" : baseCurrency.Trim().ToUpperInvariant();
        BankAccountPluralLabel = terminology.Plural(ChurchBooks.Accounting.Setup.TerminologyKeys.BankAccount);
        DepositPluralLabel = terminology.Plural(ChurchBooks.Accounting.Setup.TerminologyKeys.Deposit);
        GivingCategorySingularLabel = terminology.Singular(ChurchBooks.Accounting.Setup.TerminologyKeys.GivingCategory);
        Reconciliation.ApplyPersonalization(BaseCurrency);
        OnPropertyChanged(nameof(TotalBookBalanceDisplay));
    }

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new Phase8DatabaseMigrator(database).InitializeAsync(cancellationToken);

        _bankingStore = new SqliteBankingStore(database);
        _accountingStore = new SqliteAccountingStore(database);
        _offeringStore = new SqliteOfferingStore(database);
        _peopleStore = new SqlitePeopleGivingStore(database);
        var fundStore = new SqliteFundAccountingStore(database);
        _service = new BankingManagementService(_accountingStore, fundStore, _offeringStore, _bankingStore);
        Reconciliation.ApplyPersonalization(BaseCurrency);
        await Reconciliation.InitializeAsync(database, cancellationToken);

        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_bankingStore is null || _accountingStore is null || _offeringStore is null || _peopleStore is null) return;

        var selectedBankId = SelectedBankAccount?.Account.Id;
        var selectedDepositId = SelectedDeposit?.Id;

        BankAccounts.Clear();
        foreach (var bank in await _bankingStore.GetBankAccountsAsync(false, cancellationToken))
        {
            var ledger = await _accountingStore.GetLedgerAsync(bank.LedgerAccountId, cancellationToken: cancellationToken);
            var balance = ledger.Sum(line => line.Debit - line.Credit);
            BankAccounts.Add(new BankAccountListItemViewModel(bank, balance));
        }

        Deposits.Clear();
        foreach (var deposit in await _bankingStore.GetDepositsAsync(cancellationToken)) Deposits.Add(deposit);

        GivingCategories.Clear();
        var categories = await _peopleStore.GetGivingCategoriesAsync(cancellationToken: cancellationToken);
        foreach (var category in categories.Where(item => item.Status == GivingCategoryStatus.Active)) GivingCategories.Add(category);

        IncomeAccounts.Clear();
        var incomeAccounts = await _accountingStore.GetAccountsByTypeAsync(AccountType.Income, cancellationToken);
        foreach (var account in incomeAccounts.Where(item => item.Status == AccountStatus.Active)) IncomeAccounts.Add(account);

        AvailableContributions.Clear();
        var contributions = await _offeringStore.GetContributionsAsync(cancellationToken: cancellationToken);
        foreach (var contribution in contributions)
        {
            if (await _bankingStore.IsContributionAlreadyDepositedAsync(contribution.Id, cancellationToken)) continue;
            AvailableContributions.Add(new ContributionDepositItem
            {
                ContributionId = contribution.Id,
                Amount = contribution.TotalAmount,
                Display = $"{contribution.ReceivedDate:yyyy-MM-dd}  {contribution.Reference}  {contribution.Currency.Value} {contribution.TotalAmount:N2}"
            });
        }

        SelectedBankAccount = selectedBankId.HasValue
            ? BankAccounts.FirstOrDefault(item => item.Account.Id == selectedBankId.Value)
            : BankAccounts.FirstOrDefault();
        SelectedDeposit = selectedDepositId.HasValue
            ? Deposits.FirstOrDefault(item => item.Id == selectedDepositId.Value)
            : Deposits.FirstOrDefault();
        SelectedCategory ??= GivingCategories.FirstOrDefault();
        SelectedIncomeAccount ??= IncomeAccounts.FirstOrDefault();
        RaiseStats();
    }

    [RelayCommand]
    private async Task AddBankAccountAsync()
    {
        if (_service is null) return;
        try
        {
            var ledgerId = Guid.NewGuid();
            var ledger = new Account(
                ledgerId,
                $"BANK-{ledgerId:N}"[..13].ToUpperInvariant(),
                string.IsNullOrWhiteSpace(LedgerName) ? (string.IsNullOrWhiteSpace(BankName) ? "Operating Bank" : BankName.Trim()) : LedgerName.Trim(),
                AccountType.Asset);
            var bank = new BankAccount(
                Guid.NewGuid(),
                string.IsNullOrWhiteSpace(BankName) ? "Operating Bank" : BankName.Trim(),
                string.IsNullOrWhiteSpace(InstitutionName) ? "Bank" : InstitutionName.Trim(),
                LastFour,
                new CurrencyCode(BaseCurrency),
                ledgerId);

            await _service.AddBankAccountAsync(bank, ledger);
            StatusMessage = "Bank account added with its linked Asset ledger account.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveCategoryMappingAsync()
    {
        if (_bankingStore is null || SelectedCategory is null || SelectedIncomeAccount is null) return;
        try
        {
            await _bankingStore.SetGivingCategoryIncomeMappingAsync(
                new GivingCategoryIncomeMapping(SelectedCategory.Id, SelectedIncomeAccount.Id));
            StatusMessage = $"{GivingCategorySingularLabel} mapped to Income account.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CreateDepositAsync()
    {
        if (_service is null || SelectedBankAccount is null || DepositDate is null) return;
        try
        {
            var contributionIds = AvailableContributions.Where(item => item.IsSelected).Select(item => item.ContributionId).ToArray();
            var deposit = await _service.CreateDepositAsync(
                SelectedBankAccount.Account.Id,
                DateOnly.FromDateTime(DepositDate.Value.Date),
                contributionIds,
                DepositReference,
                DepositMemo);

            StatusMessage = $"Draft deposit created for {deposit.Currency.Value} {deposit.TotalAmount:N2}. Review the posting preview before posting.";
            await RefreshAsync();
            SelectedDeposit = Deposits.FirstOrDefault(item => item.Id == deposit.Id);
            await PreviewDepositAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PreviewDepositAsync()
    {
        if (_service is null || SelectedDeposit is null) return;
        try
        {
            var preview = await _service.PreviewPostingAsync(SelectedDeposit.Id);
            PostingPreview = $"Journal {preview.FundJournal.Entry.EntryNumber}: {preview.FundJournal.Entry.Lines.Count} lines, Debit = Credit = {preview.Deposit.Currency.Value} {preview.Deposit.TotalAmount:N2}. Every line carries a Fund assignment.";
        }
        catch (Exception ex)
        {
            PostingPreview = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PostDepositAsync()
    {
        if (_service is null || SelectedDeposit is null) return;
        try
        {
            var posted = await _service.PostDepositAsync(SelectedDeposit.Id);
            StatusMessage = $"Deposit posted to the General Ledger as {posted.Entry.Entry.EntryNumber}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RaiseStats()
    {
        OnPropertyChanged(nameof(ActiveBankCount));
        OnPropertyChanged(nameof(TotalBookBalance));
        Reconciliation.ApplyPersonalization(BaseCurrency);
        OnPropertyChanged(nameof(TotalBookBalanceDisplay));
        OnPropertyChanged(nameof(DraftDepositCount));
        OnPropertyChanged(nameof(PostedDepositCount));
    }
}
