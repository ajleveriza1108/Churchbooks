using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;

namespace ChurchBooks.Accounting.Services;

public sealed class BankingManagementService
{
    private readonly IAccountingStore _accountingStore;
    private readonly IFundAccountingStore _fundStore;
    private readonly IOfferingStore _offeringStore;
    private readonly IBankingStore _bankingStore;
    private readonly FundAccountingEngine _fundEngine;

    public BankingManagementService(
        IAccountingStore accountingStore,
        IFundAccountingStore fundStore,
        IOfferingStore offeringStore,
        IBankingStore bankingStore)
    {
        _accountingStore = accountingStore ?? throw new ArgumentNullException(nameof(accountingStore));
        _fundStore = fundStore ?? throw new ArgumentNullException(nameof(fundStore));
        _offeringStore = offeringStore ?? throw new ArgumentNullException(nameof(offeringStore));
        _bankingStore = bankingStore ?? throw new ArgumentNullException(nameof(bankingStore));
        _fundEngine = new FundAccountingEngine(_accountingStore, _fundStore);
    }

    public async Task AddBankAccountAsync(
        BankAccount bankAccount,
        Account ledgerAccount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        ArgumentNullException.ThrowIfNull(ledgerAccount);

        if (ledgerAccount.Id != bankAccount.LedgerAccountId)
        {
            throw new BankingManagementException(
                "The bank account must reference the same ledger account being created.");
        }

        EnsurePostingBankLedger(ledgerAccount);
        await _bankingStore.AddBankAccountAsync(bankAccount, ledgerAccount, cancellationToken);
    }

    public async Task<BankDeposit> CreateDepositAsync(
        Guid bankAccountId,
        DateOnly depositDate,
        IEnumerable<Guid> contributionIds,
        string reference,
        string memo,
        CancellationToken cancellationToken = default)
    {
        var bank = await _bankingStore.GetBankAccountAsync(bankAccountId, cancellationToken)
            ?? throw new BankingManagementException("The selected bank account does not exist.");

        if (bank.Status != BankAccountStatus.Active)
        {
            throw new BankingManagementException("The selected bank account is archived.");
        }

        var ids = contributionIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        if (ids.Length == 0)
        {
            throw new BankingManagementException("Select at least one contribution for the deposit.");
        }

        var allContributions = await _offeringStore.GetContributionsAsync(
            cancellationToken: cancellationToken);
        var contributionsById = allContributions.ToDictionary(item => item.Id);
        var allocations = new List<DepositContributionAllocation>();

        foreach (var id in ids)
        {
            if (!contributionsById.TryGetValue(id, out var contribution))
            {
                throw new BankingManagementException($"Contribution {id:D} does not exist.");
            }

            if (contribution.Currency != bank.Currency)
            {
                throw new BankingManagementException(
                    "Every contribution in a deposit must use the bank account currency.");
            }

            if (contribution.ReceivedDate > depositDate)
            {
                throw new BankingManagementException(
                    "A deposit cannot include a contribution received after the deposit date.");
            }

            if (await _bankingStore.IsContributionAlreadyDepositedAsync(id, cancellationToken))
            {
                throw new BankingManagementException(
                    "One of the selected contributions is already assigned to another deposit.");
            }

            allocations.Add(new DepositContributionAllocation(id, contribution.TotalAmount));
        }

        var deposit = new BankDeposit(
            Guid.NewGuid(),
            bank.Id,
            depositDate,
            bank.Currency,
            allocations,
            reference,
            memo,
            Guid.NewGuid());

        await _bankingStore.SaveDepositAsync(deposit, cancellationToken);
        return deposit;
    }

    public async Task<DepositPostingPreview> PreviewPostingAsync(
        Guid depositId,
        CancellationToken cancellationToken = default)
    {
        var deposit = await _bankingStore.GetDepositAsync(depositId, cancellationToken)
            ?? throw new BankingManagementException("The deposit does not exist.");
        var bank = await _bankingStore.GetBankAccountAsync(deposit.BankAccountId, cancellationToken)
            ?? throw new BankingManagementException("The deposit bank account does not exist.");

        if (bank.Status != BankAccountStatus.Active)
        {
            throw new BankingManagementException("The selected bank account is archived.");
        }

        var period = await _accountingStore.GetOpenPeriodForDateAsync(
            deposit.DepositDate,
            cancellationToken)
            ?? throw new BankingManagementException(
                "There is no open accounting period for the deposit date.");

        var mapping = await _bankingStore.GetGivingCategoryIncomeMappingsAsync(cancellationToken);
        var allContributions = await _offeringStore.GetContributionsAsync(
            cancellationToken: cancellationToken);
        var contributionsById = allContributions.ToDictionary(item => item.Id);
        var contributions = deposit.Contributions
            .Select(item => contributionsById.TryGetValue(item.ContributionId, out var contribution)
                ? contribution
                : throw new BankingManagementException(
                    "A contribution referenced by the deposit no longer exists."))
            .ToArray();

        var requestedAccountIds = mapping.Values
            .Append(bank.LedgerAccountId)
            .Distinct()
            .ToArray();
        var accounts = await _accountingStore.GetAccountsAsync(
            requestedAccountIds,
            cancellationToken);

        if (!accounts.TryGetValue(bank.LedgerAccountId, out var bankLedger))
        {
            throw new BankingManagementException(
                "The bank account ledger mapping does not exist.");
        }

        EnsurePostingBankLedger(bankLedger);

        var missingCategories = contributions
            .SelectMany(item => item.Lines)
            .Select(line => line.GivingCategoryId)
            .Distinct()
            .Where(categoryId => !mapping.ContainsKey(categoryId))
            .ToArray();
        if (missingCategories.Length > 0)
        {
            throw new BankingManagementException(
                "Every giving category in the deposit must have an Income account mapping before posting.");
        }

        foreach (var accountId in contributions
                     .SelectMany(item => item.Lines)
                     .Select(line => mapping[line.GivingCategoryId])
                     .Distinct())
        {
            if (!accounts.TryGetValue(accountId, out var income)
                || income.Type != AccountType.Income
                || income.Status != AccountStatus.Active
                || !income.AllowDirectPosting)
            {
                throw new BankingManagementException(
                    "Every giving-category mapping must reference an active, direct-posting Income account.");
            }
        }

        var lines = new List<JournalLine>();
        var assignments = new List<FundAssignment>();

        foreach (var fundGroup in contributions
                     .SelectMany(item => item.Lines)
                     .GroupBy(line => line.FundId)
                     .OrderBy(group => group.Key))
        {
            var journalLineId = Guid.NewGuid();
            lines.Add(
                new JournalLine(
                    journalLineId,
                    bank.LedgerAccountId,
                    fundGroup.Sum(line => line.Amount),
                    0m,
                    "Deposit to " + bank.Name));
            assignments.Add(new FundAssignment(journalLineId, fundGroup.Key));
        }

        foreach (var group in contributions
                     .SelectMany(item => item.Lines)
                     .GroupBy(line => new { line.GivingCategoryId, line.FundId })
                     .OrderBy(group => group.Key.FundId)
                     .ThenBy(group => group.Key.GivingCategoryId))
        {
            var journalLineId = Guid.NewGuid();
            lines.Add(
                new JournalLine(
                    journalLineId,
                    mapping[group.Key.GivingCategoryId],
                    0m,
                    group.Sum(line => line.Amount),
                    "Contribution income"));
            assignments.Add(new FundAssignment(journalLineId, group.Key.FundId));
        }

        var entryNumber =
            "DEP-"
            + deposit.DepositDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)
            + "-"
            + deposit.Id.ToString("N")[..8].ToUpperInvariant();
        var journal = new JournalEntry(
            deposit.JournalEntryId,
            entryNumber,
            period.Id,
            deposit.DepositDate,
            "Bank deposit - " + bank.Name,
            deposit.Currency,
            lines,
            deposit.Reference);

        return new DepositPostingPreview(
            deposit,
            new FundJournalEntry(journal, assignments));
    }

    public async Task<PostedFundJournalEntry> PostDepositAsync(
        Guid depositId,
        CancellationToken cancellationToken = default)
    {
        var deposit = await _bankingStore.GetDepositAsync(depositId, cancellationToken)
            ?? throw new BankingManagementException("The deposit does not exist.");

        if (deposit.Status == BankDepositStatus.Posted)
        {
            throw new BankingManagementException("This deposit is already posted.");
        }

        if (await _accountingStore.JournalExistsAsync(deposit.JournalEntryId, cancellationToken))
        {
            var healedAt = DateTimeOffset.UtcNow;
            await _bankingStore.MarkDepositPostedAsync(
                deposit.Id,
                deposit.JournalEntryId,
                healedAt,
                cancellationToken);
            throw new BankingManagementException(
                "The journal already exists, so ChurchBooks repaired the deposit status. Refresh before continuing.");
        }

        var preview = await PreviewPostingAsync(depositId, cancellationToken);
        var posted = await _fundEngine.PostJournalAsync(
            preview.FundJournal,
            cancellationToken: cancellationToken);
        await _bankingStore.MarkDepositPostedAsync(
            deposit.Id,
            deposit.JournalEntryId,
            posted.PostedUtc,
            cancellationToken);

        return posted;
    }

    private static void EnsurePostingBankLedger(Account ledgerAccount)
    {
        if (ledgerAccount.Type != AccountType.Asset)
        {
            throw new BankingManagementException(
                "A bank account must map to an Asset ledger account.");
        }

        if (ledgerAccount.Status != AccountStatus.Active)
        {
            throw new BankingManagementException(
                "The bank ledger account must be active.");
        }

        if (!ledgerAccount.AllowDirectPosting)
        {
            throw new BankingManagementException(
                "The bank ledger account must allow direct posting.");
        }
    }
}
