using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Expenses;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;

namespace ChurchBooks.Accounting.Services;

public sealed class ExpenseManagementService
{
    private readonly IAccountingStore _accountingStore;
    private readonly IFundAccountingStore _fundStore;
    private readonly IBankingStore _bankingStore;
    private readonly IExpenseStore _expenseStore;
    private readonly FundAccountingEngine _fundEngine;

    public ExpenseManagementService(
        IAccountingStore accountingStore,
        IFundAccountingStore fundStore,
        IBankingStore bankingStore,
        IExpenseStore expenseStore)
    {
        _accountingStore = accountingStore ?? throw new ArgumentNullException(nameof(accountingStore));
        _fundStore = fundStore ?? throw new ArgumentNullException(nameof(fundStore));
        _bankingStore = bankingStore ?? throw new ArgumentNullException(nameof(bankingStore));
        _expenseStore = expenseStore ?? throw new ArgumentNullException(nameof(expenseStore));
        _fundEngine = new FundAccountingEngine(_accountingStore, _fundStore);
    }

    public async Task AddVendorAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        var existing = await _expenseStore.GetVendorsAsync(includeArchived: true, cancellationToken);
        if (existing.Any(item => item.Code.Equals(vendor.Code, StringComparison.OrdinalIgnoreCase)))
            throw new ExpenseManagementException("Vendor code is already in use.");
        await _expenseStore.AddVendorAsync(vendor, cancellationToken);
    }

    public async Task ArchiveVendorAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = await _expenseStore.GetVendorAsync(vendorId, cancellationToken)
            ?? throw new ExpenseManagementException("The vendor does not exist.");
        if (vendor.Status == VendorStatus.Archived) return;
        await _expenseStore.UpdateVendorAsync(vendor.Archive(DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task RestoreVendorAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = await _expenseStore.GetVendorAsync(vendorId, cancellationToken)
            ?? throw new ExpenseManagementException("The vendor does not exist.");
        if (vendor.Status == VendorStatus.Active) return;
        await _expenseStore.UpdateVendorAsync(vendor.Reactivate(), cancellationToken);
    }

    public async Task<DirectExpense> CreateDirectExpenseAsync(
        Guid? vendorId,
        Guid bankAccountId,
        DateOnly expenseDate,
        IEnumerable<DirectExpenseLine> lines,
        string reference,
        string memo,
        CancellationToken cancellationToken = default)
    {
        var bank = await _bankingStore.GetBankAccountAsync(bankAccountId, cancellationToken)
            ?? throw new ExpenseManagementException("The selected bank account does not exist.");
        if (bank.Status != BankAccountStatus.Active)
            throw new ExpenseManagementException("The selected bank account is archived.");

        if (vendorId.HasValue)
        {
            var vendor = await _expenseStore.GetVendorAsync(vendorId.Value, cancellationToken)
                ?? throw new ExpenseManagementException("The selected vendor does not exist.");
            if (vendor.Status != VendorStatus.Active)
                throw new ExpenseManagementException("Archived vendors cannot be used for new expenses.");
        }

        var lineArray = lines?.ToArray() ?? throw new ArgumentNullException(nameof(lines));
        var expense = new DirectExpense(
            Guid.NewGuid(),
            vendorId,
            bankAccountId,
            expenseDate,
            bank.Currency,
            lineArray,
            reference,
            memo,
            Guid.NewGuid());

        await ValidatePostingReferencesAsync(bank, expense.Lines, cancellationToken);
        await _expenseStore.SaveDirectExpenseAsync(expense, cancellationToken);
        return expense;
    }

    public async Task<ExpensePostingPreview> PreviewPostingAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await _expenseStore.GetDirectExpenseAsync(expenseId, cancellationToken)
            ?? throw new ExpenseManagementException("The expense does not exist.");
        if (expense.Status != DirectExpenseStatus.Draft)
            throw new ExpenseManagementException("Only draft expenses can be posted.");

        var bank = await _bankingStore.GetBankAccountAsync(expense.BankAccountId, cancellationToken)
            ?? throw new ExpenseManagementException("The selected bank account does not exist.");
        if (bank.Status != BankAccountStatus.Active)
            throw new ExpenseManagementException("The selected bank account is archived.");
        if (bank.Currency != expense.Currency)
            throw new ExpenseManagementException("Expense currency must match the bank account currency.");

        await ValidatePostingReferencesAsync(bank, expense.Lines, cancellationToken);
        var period = await _accountingStore.GetOpenPeriodForDateAsync(expense.ExpenseDate, cancellationToken)
            ?? throw new ExpenseManagementException("There is no open accounting period for the expense date.");

        var journalLines = new List<JournalLine>();
        var assignments = new List<FundAssignment>();
        foreach (var expenseLine in expense.Lines)
        {
            var journalLineId = Guid.NewGuid();
            journalLines.Add(new JournalLine(
                journalLineId,
                expenseLine.ExpenseAccountId,
                expenseLine.Amount,
                0m,
                expenseLine.Description));
            assignments.Add(new FundAssignment(journalLineId, expenseLine.FundId));
        }

        foreach (var fundGroup in expense.Lines.GroupBy(static line => line.FundId).OrderBy(static group => group.Key))
        {
            var journalLineId = Guid.NewGuid();
            journalLines.Add(new JournalLine(
                journalLineId,
                bank.LedgerAccountId,
                0m,
                fundGroup.Sum(static line => line.Amount),
                "Payment from " + bank.Name));
            assignments.Add(new FundAssignment(journalLineId, fundGroup.Key));
        }

        var entryNumber =
            "EXP-"
            + expense.ExpenseDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)
            + "-"
            + expense.Id.ToString("N")[..8].ToUpperInvariant();
        var journal = new JournalEntry(
            expense.JournalEntryId,
            entryNumber,
            period.Id,
            expense.ExpenseDate,
            "Direct expense",
            expense.Currency,
            journalLines,
            expense.Reference);

        return new ExpensePostingPreview(expense, new FundJournalEntry(journal, assignments));
    }

    public async Task<PostedFundJournalEntry> PostDirectExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewPostingAsync(expenseId, cancellationToken);
        var posted = await _fundEngine.PostJournalAsync(preview.FundJournal, cancellationToken: cancellationToken);
        await _expenseStore.MarkDirectExpensePostedAsync(
            preview.Expense.Id,
            preview.Expense.JournalEntryId,
            posted.PostedUtc,
            cancellationToken);
        return posted;
    }

    private async Task ValidatePostingReferencesAsync(
        BankAccount bank,
        IReadOnlyCollection<DirectExpenseLine> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0) throw new ExpenseManagementException("A direct expense requires at least one line.");

        var accountIds = lines.Select(static line => line.ExpenseAccountId)
            .Append(bank.LedgerAccountId)
            .Distinct()
            .ToArray();
        var accounts = await _accountingStore.GetAccountsAsync(accountIds, cancellationToken);

        if (!accounts.TryGetValue(bank.LedgerAccountId, out var bankLedger)
            || bankLedger.Type != AccountType.Asset
            || bankLedger.Status != AccountStatus.Active
            || !bankLedger.AllowDirectPosting)
            throw new ExpenseManagementException("The bank account must map to an active direct-posting Asset account.");

        foreach (var accountId in lines.Select(static line => line.ExpenseAccountId).Distinct())
        {
            if (!accounts.TryGetValue(accountId, out var account)
                || account.Type != AccountType.Expense
                || account.Status != AccountStatus.Active
                || !account.AllowDirectPosting)
                throw new ExpenseManagementException("Every expense line must use an active direct-posting Expense account.");
        }

        var fundIds = lines.Select(static line => line.FundId).Distinct().ToArray();
        var funds = await _fundStore.GetFundsAsync(fundIds, cancellationToken);
        if (funds.Count != fundIds.Length || funds.Values.Any(static fund => fund.Status != FundStatus.Active))
            throw new ExpenseManagementException("Every expense line must use an active Fund.");
    }
}
