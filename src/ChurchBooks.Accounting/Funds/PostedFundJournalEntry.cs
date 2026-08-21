namespace ChurchBooks.Accounting.Funds;

public sealed class PostedFundJournalEntry
{
    public FundJournalEntry Entry { get; }
    public DateTimeOffset PostedUtc { get; }
    public IReadOnlyList<string> Warnings { get; }

    internal PostedFundJournalEntry(FundJournalEntry entry, DateTimeOffset postedUtc, IEnumerable<string> warnings)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        ArgumentNullException.ThrowIfNull(warnings);
        PostedUtc = postedUtc;
        Warnings = warnings.ToArray();
    }
}
