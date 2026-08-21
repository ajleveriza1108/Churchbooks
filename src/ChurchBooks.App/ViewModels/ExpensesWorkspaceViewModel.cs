using System.Collections.ObjectModel;
using System.Globalization;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Expenses;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Services;
using ChurchBooks.App.Models;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class ExpensesWorkspaceViewModel : ObservableObject
{
    private SqliteExpenseStore? _expenseStore;
    private SqliteAccountingStore? _accountingStore;
    private SqliteFundAccountingStore? _fundStore;
    private SqliteBankingStore? _bankingStore;
    private ExpenseManagementService? _service;

    public ObservableCollection<Vendor> Vendors { get; } = new();
    public ObservableCollection<DirectExpense> Expenses { get; } = new();
    public ObservableCollection<BankAccount> BankAccounts { get; } = new();
    public ObservableCollection<Account> ExpenseAccounts { get; } = new();
    public ObservableCollection<Fund> Funds { get; } = new();
    public ObservableCollection<ExpenseLineDraft> DraftLines { get; } = new();

    [ObservableProperty] private Vendor? _selectedVendor;
    [ObservableProperty] private DirectExpense? _selectedExpense;
    [ObservableProperty] private BankAccount? _selectedBankAccount;
    [ObservableProperty] private Account? _selectedExpenseAccount;
    [ObservableProperty] private Fund? _selectedFund;
    [ObservableProperty] private string _vendorCode = string.Empty;
    [ObservableProperty] private string _vendorName = string.Empty;
    [ObservableProperty] private string _vendorTaxId = string.Empty;
    [ObservableProperty] private string _vendorEmail = string.Empty;
    [ObservableProperty] private string _vendorPhone = string.Empty;
    [ObservableProperty] private DateTime? _expenseDate = DateTime.Today;
    [ObservableProperty] private string _expenseReference = string.Empty;
    [ObservableProperty] private string _expenseMemo = string.Empty;
    [ObservableProperty] private string _lineDescription = string.Empty;
    [ObservableProperty] private string _lineAmount = string.Empty;
    [ObservableProperty] private string _baseCurrency = "PHP";
    [ObservableProperty] private string _statusMessage = "Record vendors and direct bank-paid expenses. Nothing posts until you explicitly choose Post Expense.";
    [ObservableProperty] private string _postingPreview = "Select a draft expense to preview its Fund-aware journal.";

    public int ActiveVendorCount => Vendors.Count(static vendor => vendor.Status == VendorStatus.Active);
    public int DraftExpenseCount => Expenses.Count(static expense => expense.Status == DirectExpenseStatus.Draft);
    public int PostedExpenseCount => Expenses.Count(static expense => expense.Status == DirectExpenseStatus.Posted);
    public decimal PostedExpenseTotal => Expenses
        .Where(static expense => expense.Status == DirectExpenseStatus.Posted)
        .Sum(static expense => expense.TotalAmount);

    public string PostedExpenseTotalDisplay
    {
        get
        {
            var posted = Expenses.Where(static expense => expense.Status == DirectExpenseStatus.Posted).ToArray();
            if (posted.Length == 0) return $"{BaseCurrency} 0.00";
            var currencies = posted.Select(static expense => expense.Currency.Value).Distinct(StringComparer.Ordinal).ToArray();
            if (currencies.Length != 1) return "Multiple currencies";
            return $"{currencies[0]} {posted.Sum(static expense => expense.TotalAmount):N2}";
        }
    }

    public decimal DraftLineTotal => DraftLines.Sum(static line => line.Amount);
    public string DraftLineTotalDisplay => $"{SelectedBankAccount?.Currency.Value ?? BaseCurrency} {DraftLineTotal:N2}";
    public string VendorActionLabel => SelectedVendor?.Status == VendorStatus.Archived ? "Restore Vendor" : "Archive Vendor";

    public void ApplyPersonalization(string baseCurrency)
    {
        BaseCurrency = string.IsNullOrWhiteSpace(baseCurrency) ? "PHP" : baseCurrency.Trim().ToUpperInvariant();
        RaiseStats();
    }

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new Phase10DatabaseMigrator(database).InitializeAsync(cancellationToken);
        _expenseStore = new SqliteExpenseStore(database);
        _accountingStore = new SqliteAccountingStore(database);
        _fundStore = new SqliteFundAccountingStore(database);
        _bankingStore = new SqliteBankingStore(database);
        _service = new ExpenseManagementService(_accountingStore, _fundStore, _bankingStore, _expenseStore);
        await EnsureStarterExpenseAccountAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_expenseStore is null || _accountingStore is null || _fundStore is null || _bankingStore is null) return;
        var selectedVendorId = SelectedVendor?.Id;
        var selectedExpenseId = SelectedExpense?.Id;
        var selectedBankId = SelectedBankAccount?.Id;

        Vendors.Clear();
        foreach (var vendor in await _expenseStore.GetVendorsAsync(includeArchived: true, cancellationToken)) Vendors.Add(vendor);

        Expenses.Clear();
        foreach (var expense in await _expenseStore.GetDirectExpensesAsync(cancellationToken)) Expenses.Add(expense);

        BankAccounts.Clear();
        foreach (var bank in await _bankingStore.GetBankAccountsAsync(false, cancellationToken))
            if (bank.Status == BankAccountStatus.Active) BankAccounts.Add(bank);

        ExpenseAccounts.Clear();
        foreach (var account in await _accountingStore.GetAccountsByTypeAsync(AccountType.Expense, cancellationToken))
            if (account.Status == AccountStatus.Active && account.AllowDirectPosting) ExpenseAccounts.Add(account);

        Funds.Clear();
        foreach (var fund in await _fundStore.GetAllFundsAsync(false, cancellationToken))
            if (fund.Status == FundStatus.Active) Funds.Add(fund);

        SelectedVendor = selectedVendorId.HasValue ? Vendors.FirstOrDefault(item => item.Id == selectedVendorId.Value) : Vendors.FirstOrDefault(item => item.Status == VendorStatus.Active);
        SelectedExpense = selectedExpenseId.HasValue ? Expenses.FirstOrDefault(item => item.Id == selectedExpenseId.Value) : Expenses.FirstOrDefault();
        SelectedBankAccount = selectedBankId.HasValue ? BankAccounts.FirstOrDefault(item => item.Id == selectedBankId.Value) : BankAccounts.FirstOrDefault();
        SelectedExpenseAccount ??= ExpenseAccounts.FirstOrDefault();
        SelectedFund ??= Funds.FirstOrDefault();
        RaiseStats();
    }

    [RelayCommand]
    private async Task AddVendorAsync()
    {
        if (_service is null) return;
        try
        {
            var vendorId = Guid.NewGuid();
            var vendorCode = string.IsNullOrWhiteSpace(VendorCode)
                ? $"V-{vendorId:N}"[..10].ToUpperInvariant()
                : VendorCode.Trim();
            var vendor = new Vendor(vendorId, vendorCode, VendorName, VendorTaxId, VendorEmail, VendorPhone);
            await _service.AddVendorAsync(vendor);
            VendorCode = VendorName = VendorTaxId = VendorEmail = VendorPhone = string.Empty;
            StatusMessage = "Vendor added. Vendor records never post accounting entries by themselves.";
            await RefreshAsync();
            SelectedVendor = Vendors.FirstOrDefault(item => item.Id == vendor.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ToggleVendorStatusAsync()
    {
        if (_service is null || SelectedVendor is null) return;
        try
        {
            if (SelectedVendor.Status == VendorStatus.Active)
            {
                await _service.ArchiveVendorAsync(SelectedVendor.Id);
                StatusMessage = "Vendor archived. Historical expenses remain intact.";
            }
            else
            {
                await _service.RestoreVendorAsync(SelectedVendor.Id);
                StatusMessage = "Vendor restored for new expenses.";
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void AddExpenseLine()
    {
        if (SelectedExpenseAccount is null || SelectedFund is null)
        {
            StatusMessage = "Choose an Expense account and Fund first.";
            return;
        }
        if (!decimal.TryParse(LineAmount, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && !decimal.TryParse(LineAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            StatusMessage = "Enter a valid positive expense amount.";
            return;
        }
        try
        {
            DraftLines.Add(new ExpenseLineDraft(SelectedExpenseAccount, SelectedFund, amount, LineDescription));
            LineAmount = string.Empty;
            LineDescription = string.Empty;
            StatusMessage = "Expense line added to the draft. It has not posted.";
            OnPropertyChanged(nameof(DraftLineTotal));
            OnPropertyChanged(nameof(DraftLineTotalDisplay));
        }
        catch (ArgumentOutOfRangeException)
        {
            StatusMessage = "Expense amount must be greater than zero.";
        }
    }

    [RelayCommand]
    private void RemoveExpenseLine(ExpenseLineDraft? line)
    {
        if (line is null) return;
        DraftLines.Remove(line);
        OnPropertyChanged(nameof(DraftLineTotal));
        OnPropertyChanged(nameof(DraftLineTotalDisplay));
    }

    [RelayCommand]
    private async Task CreateExpenseAsync()
    {
        if (_service is null || SelectedBankAccount is null || ExpenseDate is null) return;
        if (SelectedVendor is { Status: VendorStatus.Active } && string.IsNullOrWhiteSpace(ExpenseReference))
        {
            StatusMessage = "Enter the vendor receipt / invoice / reference number before creating this expense.";
            return;
        }
        try
        {
            var lines = DraftLines.Select(line => new DirectExpenseLine(
                line.Id,
                line.ExpenseAccount.Id,
                line.Fund.Id,
                line.Amount,
                line.Description)).ToArray();
            Guid? vendorId = SelectedVendor is { Status: VendorStatus.Active } activeVendor ? activeVendor.Id : null;
            var expense = await _service.CreateDirectExpenseAsync(
                vendorId,
                SelectedBankAccount.Id,
                DateOnly.FromDateTime(ExpenseDate.Value.Date),
                lines,
                ExpenseReference,
                ExpenseMemo);
            DraftLines.Clear();
            ExpenseReference = ExpenseMemo = string.Empty;
            StatusMessage = $"Draft expense created for {expense.Currency.Value} {expense.TotalAmount:N2}. Preview before posting.";
            await RefreshAsync();
            SelectedExpense = Expenses.FirstOrDefault(item => item.Id == expense.Id);
            await PreviewExpenseAsync();
            OnPropertyChanged(nameof(DraftLineTotal));
            OnPropertyChanged(nameof(DraftLineTotalDisplay));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PreviewExpenseAsync()
    {
        if (_service is null || SelectedExpense is null) return;
        try
        {
            var preview = await _service.PreviewPostingAsync(SelectedExpense.Id);
            PostingPreview = $"Journal {preview.FundJournal.Entry.EntryNumber}: {preview.FundJournal.Entry.Lines.Count} lines; Debit = Credit = {preview.Expense.Currency.Value} {preview.Expense.TotalAmount:N2}; every journal line has a Fund assignment.";
        }
        catch (Exception ex)
        {
            PostingPreview = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PostExpenseAsync()
    {
        if (_service is null || SelectedExpense is null) return;
        try
        {
            var posted = await _service.PostDirectExpenseAsync(SelectedExpense.Id);
            StatusMessage = $"Expense posted as {posted.Entry.Entry.EntryNumber}. Double-post protection is active.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedVendorChanged(Vendor? value) => OnPropertyChanged(nameof(VendorActionLabel));

    partial void OnSelectedBankAccountChanged(BankAccount? value)
    {
        OnPropertyChanged(nameof(DraftLineTotalDisplay));
    }

    private async Task EnsureStarterExpenseAccountAsync(CancellationToken cancellationToken)
    {
        if (_accountingStore is null) return;
        var existing = await _accountingStore.GetAccountsByTypeAsync(AccountType.Expense, cancellationToken);
        if (existing.Any(static account => account.Status == AccountStatus.Active && account.AllowDirectPosting)) return;

        for (var code = 6000; code <= 6990; code += 10)
        {
            var codeText = code.ToString(CultureInfo.InvariantCulture);
            if (await _accountingStore.GetAccountByCodeAsync(codeText, cancellationToken) is not null) continue;
            await _accountingStore.AddAccountAsync(new Account(Guid.NewGuid(), codeText, "General Operating Expense", AccountType.Expense), cancellationToken);
            StatusMessage = "A starter General Operating Expense account was created. You can add more Expense accounts later.";
            return;
        }

        throw new InvalidOperationException("No available starter Expense account code was found between 6000 and 6990.");
    }
    private void RaiseStats()
    {
        OnPropertyChanged(nameof(ActiveVendorCount));
        OnPropertyChanged(nameof(DraftExpenseCount));
        OnPropertyChanged(nameof(PostedExpenseCount));
        OnPropertyChanged(nameof(PostedExpenseTotal));
        OnPropertyChanged(nameof(PostedExpenseTotalDisplay));
        OnPropertyChanged(nameof(VendorActionLabel));
    }
}
