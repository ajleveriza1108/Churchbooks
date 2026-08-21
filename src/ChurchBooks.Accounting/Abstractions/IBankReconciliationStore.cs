using ChurchBooks.Accounting.Reconciliation;

namespace ChurchBooks.Accounting.Abstractions;

public interface IBankReconciliationStore
{
    Task SaveStatementImportAsync(
        Guid bankAccountId,
        Guid importSessionId,
        IReadOnlyList<BankStatementLine> statementLines,
        CancellationToken cancellationToken = default);

    Task<bool> IsImportSessionLinkedAsync(Guid importSessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankStatementLine>> GetStatementLinesAsync(
        Guid bankAccountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task SaveReconciliationAsync(BankReconciliation reconciliation, CancellationToken cancellationToken = default);
    Task<BankReconciliation?> GetReconciliationAsync(Guid reconciliationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankReconciliation>> GetReconciliationsAsync(Guid bankAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationMatchGroup>> GetMatchGroupsAsync(
        Guid reconciliationId,
        CancellationToken cancellationToken = default);

    Task SaveMatchGroupAsync(ReconciliationMatchGroup group, CancellationToken cancellationToken = default);
    Task DeleteMatchGroupAsync(Guid matchGroupId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetUnavailableJournalIdsAsync(
        Guid bankAccountId,
        Guid? excludingReconciliationId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetUnavailableStatementLineIdsAsync(
        Guid bankAccountId,
        Guid? excludingReconciliationId = null,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid reconciliationId,
        DateTimeOffset completedUtc,
        CancellationToken cancellationToken = default);
}
