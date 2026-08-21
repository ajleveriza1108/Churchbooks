namespace ChurchBooks.Accounting.Journals;

public sealed class PostedJournalEntry
{
    public JournalEntry Entry { get; }
    public DateTimeOffset PostedUtc { get; }

    internal PostedJournalEntry(JournalEntry entry, DateTimeOffset postedUtc)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        PostedUtc = postedUtc;
    }
}
