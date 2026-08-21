namespace ChurchBooks.Accounting.Reconciliation;

public sealed record ReconciliationMatchGroup
{
    public Guid Id { get; }
    public Guid ReconciliationId { get; }
    public IReadOnlyList<Guid> StatementLineIds { get; }
    public IReadOnlyList<Guid> JournalEntryIds { get; }
    public DateTimeOffset CreatedUtc { get; }

    public ReconciliationMatchGroup(
        Guid id,
        Guid reconciliationId,
        IEnumerable<Guid> statementLineIds,
        IEnumerable<Guid> journalEntryIds,
        DateTimeOffset? createdUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Match group ID is required.", nameof(id));
        if (reconciliationId == Guid.Empty) throw new ArgumentException("Reconciliation ID is required.", nameof(reconciliationId));
        ArgumentNullException.ThrowIfNull(statementLineIds);
        ArgumentNullException.ThrowIfNull(journalEntryIds);

        var statementIds = statementLineIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        var journalIds = journalEntryIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (statementIds.Length == 0) throw new ArgumentException("Select at least one statement line.", nameof(statementLineIds));
        if (journalIds.Length == 0) throw new ArgumentException("Select at least one book journal.", nameof(journalEntryIds));

        Id = id;
        ReconciliationId = reconciliationId;
        StatementLineIds = statementIds;
        JournalEntryIds = journalIds;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
    }
}
