using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.Accounting.Reconciliation;

namespace ChurchBooks.Accounting.Services;

public sealed class BankReconciliationService
{
    private readonly IBankingStore _bankingStore;
    private readonly IAccountingStore _accountingStore;
    private readonly ISmartImportStore _importStore;
    private readonly IBankReconciliationStore _reconciliationStore;
    private readonly BankStatementImportConverter _converter = new();
    private readonly ReconciliationMatcher _matcher = new();

    public BankReconciliationService(
        IBankingStore bankingStore,
        IAccountingStore accountingStore,
        ISmartImportStore importStore,
        IBankReconciliationStore reconciliationStore)
    {
        _bankingStore = bankingStore ?? throw new ArgumentNullException(nameof(bankingStore));
        _accountingStore = accountingStore ?? throw new ArgumentNullException(nameof(accountingStore));
        _importStore = importStore ?? throw new ArgumentNullException(nameof(importStore));
        _reconciliationStore = reconciliationStore ?? throw new ArgumentNullException(nameof(reconciliationStore));
    }

    public async Task<int> ImportStatementSessionAsync(
        Guid bankAccountId,
        Guid importSessionId,
        BankStatementAmountConvention convention,
        CancellationToken cancellationToken = default)
    {
        var bank = await RequireActiveBankAsync(bankAccountId, cancellationToken);
        var session = await _importStore.GetSessionAsync(importSessionId, cancellationToken)
            ?? throw new InvalidOperationException("The selected Smart Import session does not exist.");

        if (await _reconciliationStore.IsImportSessionLinkedAsync(importSessionId, cancellationToken))
            throw new InvalidOperationException("That Smart Import session is already linked to a bank statement.");

        var statementLines = _converter.Convert(session, bank.Id, convention);
        if (statementLines.Count == 0)
            throw new InvalidOperationException("The selected import session has no usable non-duplicate statement rows.");

        await _reconciliationStore.SaveStatementImportAsync(bank.Id, session.Id, statementLines, cancellationToken);
        return statementLines.Count;
    }

    public async Task<BankReconciliation> StartAsync(
        Guid bankAccountId,
        DateOnly statementStartDate,
        DateOnly statementEndDate,
        decimal statementEndingBalance,
        CancellationToken cancellationToken = default)
    {
        await RequireActiveBankAsync(bankAccountId, cancellationToken);
        var existing = await _reconciliationStore.GetReconciliationsAsync(bankAccountId, cancellationToken);
        if (existing.Any(item =>
            item.Status == BankReconciliationStatus.Draft &&
            item.StatementStartDate == statementStartDate &&
            item.StatementEndDate == statementEndDate))
        {
            throw new InvalidOperationException("A draft reconciliation already exists for that statement period.");
        }

        var reconciliation = new BankReconciliation(
            Guid.NewGuid(),
            bankAccountId,
            statementStartDate,
            statementEndDate,
            statementEndingBalance);

        await _reconciliationStore.SaveReconciliationAsync(reconciliation, cancellationToken);
        return reconciliation;
    }

    public async Task<BankReconciliationSummary> BuildSummaryAsync(
        Guid reconciliationId,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireReconciliationAsync(reconciliationId, cancellationToken);
        var bank = await _bankingStore.GetBankAccountAsync(reconciliation.BankAccountId, cancellationToken)
            ?? throw new InvalidOperationException("The reconciliation bank account no longer exists.");

        var statementLines = await _reconciliationStore.GetStatementLinesAsync(
            bank.Id,
            reconciliation.StatementStartDate,
            reconciliation.StatementEndDate,
            cancellationToken);
        var unavailableStatementIds = await _reconciliationStore.GetUnavailableStatementLineIdsAsync(
            bank.Id,
            reconciliation.Id,
            cancellationToken);
        statementLines = statementLines
            .Where(line => !unavailableStatementIds.Contains(line.Id))
            .ToArray();

        var bookItems = await BuildBookItemsAsync(bank, reconciliation.StatementEndDate, cancellationToken);
        var matchGroups = await _reconciliationStore.GetMatchGroupsAsync(reconciliation.Id, cancellationToken);
        var unavailableJournalIds = await _reconciliationStore.GetUnavailableJournalIdsAsync(
            bank.Id,
            reconciliation.Id,
            cancellationToken);

        return BankReconciliationCalculator.BuildSummary(
            reconciliation,
            statementLines,
            bookItems,
            matchGroups,
            unavailableJournalIds);
    }

    public async Task<ReconciliationMatchGroup> MatchAsync(
        Guid reconciliationId,
        IEnumerable<Guid> statementLineIds,
        IEnumerable<Guid> journalEntryIds,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireDraftAsync(reconciliationId, cancellationToken);
        var summary = await BuildSummaryAsync(reconciliation.Id, cancellationToken);

        var requestedStatementIds = statementLineIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        var requestedJournalIds = journalEntryIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        var availableStatements = summary.UnmatchedStatementLines.ToDictionary(item => item.Id);
        var availableBooks = summary.UnmatchedBookItems.ToDictionary(item => item.JournalEntryId);

        if (requestedStatementIds.Length == 0 || requestedJournalIds.Length == 0)
            throw new InvalidOperationException("Select at least one statement line and one book entry.");

        if (requestedStatementIds.Any(id => !availableStatements.ContainsKey(id)))
            throw new InvalidOperationException("A selected statement line is already matched or unavailable.");
        if (requestedJournalIds.Any(id => !availableBooks.ContainsKey(id)))
            throw new InvalidOperationException("A selected book entry is already cleared, matched, or unavailable.");

        var statementTotal = requestedStatementIds.Sum(id => availableStatements[id].Amount);
        var bookTotal = requestedJournalIds.Sum(id => availableBooks[id].Amount);
        if (statementTotal != bookTotal)
            throw new InvalidOperationException("Selected statement and book entries must have exactly equal signed totals.");

        var group = new ReconciliationMatchGroup(
            Guid.NewGuid(),
            reconciliation.Id,
            requestedStatementIds,
            requestedJournalIds);

        await _reconciliationStore.SaveMatchGroupAsync(group, cancellationToken);
        return group;
    }

    public async Task<int> ApplyExactAutoMatchesAsync(
        Guid reconciliationId,
        CancellationToken cancellationToken = default)
    {
        var reconciliation = await RequireDraftAsync(reconciliationId, cancellationToken);
        var summary = await BuildSummaryAsync(reconciliation.Id, cancellationToken);
        var suggestions = _matcher.SuggestExactMatches(summary.UnmatchedStatementLines, summary.UnmatchedBookItems);

        var applied = 0;
        foreach (var (statementLineId, journalEntryId) in suggestions)
        {
            await MatchAsync(
                reconciliation.Id,
                new[] { statementLineId },
                new[] { journalEntryId },
                cancellationToken);
            applied++;
        }

        return applied;
    }

    public async Task RemoveMatchAsync(Guid reconciliationId, Guid matchGroupId, CancellationToken cancellationToken = default)
    {
        await RequireDraftAsync(reconciliationId, cancellationToken);
        var groups = await _reconciliationStore.GetMatchGroupsAsync(reconciliationId, cancellationToken);
        if (groups.All(group => group.Id != matchGroupId))
            throw new InvalidOperationException("The match group does not belong to this reconciliation.");

        await _reconciliationStore.DeleteMatchGroupAsync(matchGroupId, cancellationToken);
    }

    public async Task CompleteAsync(Guid reconciliationId, CancellationToken cancellationToken = default)
    {
        await RequireDraftAsync(reconciliationId, cancellationToken);
        var summary = await BuildSummaryAsync(reconciliationId, cancellationToken);
        if (!summary.CanComplete)
            throw new InvalidOperationException(
                "Reconciliation cannot complete until every statement line is matched and the difference is exactly zero.");

        await _reconciliationStore.MarkCompletedAsync(reconciliationId, DateTimeOffset.UtcNow, cancellationToken);
    }

    private async Task<IReadOnlyList<ReconciliationBookItem>> BuildBookItemsAsync(
        BankAccount bank,
        DateOnly statementEndDate,
        CancellationToken cancellationToken)
    {
        var ledger = await _accountingStore.GetLedgerAsync(
            bank.LedgerAccountId,
            toDate: statementEndDate,
            cancellationToken: cancellationToken);

        return ledger
            .GroupBy(line => new
            {
                line.JournalEntryId,
                line.EntryNumber,
                line.PostingDate,
                line.Description,
                line.Reference
            })
            .Select(group => new ReconciliationBookItem(
                group.Key.JournalEntryId,
                group.Key.EntryNumber,
                group.Key.PostingDate,
                group.Sum(line => line.Debit - line.Credit),
                group.Key.Description,
                group.Key.Reference))
            .Where(item => item.Amount != 0m)
            .OrderBy(item => item.PostingDate)
            .ThenBy(item => item.EntryNumber, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<BankAccount> RequireActiveBankAsync(Guid bankAccountId, CancellationToken cancellationToken)
    {
        var bank = await _bankingStore.GetBankAccountAsync(bankAccountId, cancellationToken)
            ?? throw new InvalidOperationException("The selected bank account does not exist.");
        if (bank.Status != BankAccountStatus.Active)
            throw new InvalidOperationException("The selected bank account is archived.");
        return bank;
    }

    private async Task<BankReconciliation> RequireReconciliationAsync(Guid reconciliationId, CancellationToken cancellationToken) =>
        await _reconciliationStore.GetReconciliationAsync(reconciliationId, cancellationToken)
        ?? throw new InvalidOperationException("The reconciliation does not exist.");

    private async Task<BankReconciliation> RequireDraftAsync(Guid reconciliationId, CancellationToken cancellationToken)
    {
        var reconciliation = await RequireReconciliationAsync(reconciliationId, cancellationToken);
        if (reconciliation.Status != BankReconciliationStatus.Draft)
            throw new InvalidOperationException("Completed reconciliations are locked.");
        return reconciliation;
    }
}
