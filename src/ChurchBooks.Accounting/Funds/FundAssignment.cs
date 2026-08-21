namespace ChurchBooks.Accounting.Funds;

public sealed record FundAssignment
{
    public Guid JournalLineId { get; }
    public Guid FundId { get; }

    public FundAssignment(Guid journalLineId, Guid fundId)
    {
        if (journalLineId == Guid.Empty)
        {
            throw new ArgumentException("Journal line ID cannot be empty.", nameof(journalLineId));
        }

        if (fundId == Guid.Empty)
        {
            throw new ArgumentException("Fund ID cannot be empty.", nameof(fundId));
        }

        JournalLineId = journalLineId;
        FundId = fundId;
    }
}
