using ChurchBooks.Accounting.Journals;

namespace ChurchBooks.Accounting.Funds;

public sealed record FundJournalEntry
{
    private readonly IReadOnlyDictionary<Guid, Guid> _fundByLineId;

    public JournalEntry Entry { get; }
    public IReadOnlyList<FundAssignment> Assignments { get; }
    public IReadOnlyCollection<Guid> FundIds { get; }

    public FundJournalEntry(JournalEntry entry, IEnumerable<FundAssignment> assignments)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        ArgumentNullException.ThrowIfNull(assignments);

        var assignmentArray = assignments.ToArray();
        if (assignmentArray.Length != entry.Lines.Count)
        {
            throw new ArgumentException("Every journal line must have exactly one fund assignment.", nameof(assignments));
        }

        if (assignmentArray.Select(static assignment => assignment.JournalLineId).Distinct().Count() != assignmentArray.Length)
        {
            throw new ArgumentException("A journal line cannot have more than one fund assignment.", nameof(assignments));
        }

        var lineIds = entry.Lines.Select(static line => line.Id).ToHashSet();
        if (assignmentArray.Any(assignment => !lineIds.Contains(assignment.JournalLineId)))
        {
            throw new ArgumentException("A fund assignment references a journal line outside the entry.", nameof(assignments));
        }

        _fundByLineId = assignmentArray.ToDictionary(static assignment => assignment.JournalLineId, static assignment => assignment.FundId);
        Assignments = assignmentArray;
        FundIds = assignmentArray.Select(static assignment => assignment.FundId).Distinct().ToArray();
    }

    public Guid GetFundId(Guid journalLineId)
    {
        if (!_fundByLineId.TryGetValue(journalLineId, out var fundId))
        {
            throw new KeyNotFoundException($"Journal line {journalLineId:D} has no fund assignment.");
        }

        return fundId;
    }
}
