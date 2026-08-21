namespace ChurchBooks.Accounting.Reconciliation;

public sealed class ReconciliationMatcher
{
    public IReadOnlyList<(Guid StatementLineId, Guid JournalEntryId)> SuggestExactMatches(
        IEnumerable<BankStatementLine> statementLines,
        IEnumerable<ReconciliationBookItem> bookItems,
        int dateToleranceDays = 3)
    {
        if (dateToleranceDays < 0) throw new ArgumentOutOfRangeException(nameof(dateToleranceDays));
        ArgumentNullException.ThrowIfNull(statementLines);
        ArgumentNullException.ThrowIfNull(bookItems);

        var availableBooks = bookItems.ToArray();
        var result = new List<(Guid StatementLineId, Guid JournalEntryId)>();
        var usedJournalIds = new HashSet<Guid>();

        foreach (var statement in statementLines.OrderBy(line => line.TransactionDate).ThenBy(line => line.Id))
        {
            var candidates = availableBooks
                .Where(book => !usedJournalIds.Contains(book.JournalEntryId))
                .Where(book => book.Amount == statement.Amount)
                .Where(book => Math.Abs(book.PostingDate.DayNumber - statement.TransactionDate.DayNumber) <= dateToleranceDays)
                .ToArray();

            if (candidates.Length != 1) continue;

            var candidate = candidates[0];
            var reverseCandidates = statementLines
                .Where(other => other.Amount == candidate.Amount)
                .Where(other => Math.Abs(other.TransactionDate.DayNumber - candidate.PostingDate.DayNumber) <= dateToleranceDays)
                .ToArray();
            if (reverseCandidates.Length != 1) continue;

            result.Add((statement.Id, candidate.JournalEntryId));
            usedJournalIds.Add(candidate.JournalEntryId);
        }

        return result;
    }
}
