namespace ChurchBooks.Accounting.Reconciliation;

public static class BankReconciliationCalculator
{
    public static BankReconciliationSummary BuildSummary(
        BankReconciliation reconciliation,
        IEnumerable<BankStatementLine> statementLines,
        IEnumerable<ReconciliationBookItem> bookItems,
        IEnumerable<ReconciliationMatchGroup> matchGroups,
        IEnumerable<Guid>? previouslyClearedJournalIds = null)
    {
        ArgumentNullException.ThrowIfNull(reconciliation);
        ArgumentNullException.ThrowIfNull(statementLines);
        ArgumentNullException.ThrowIfNull(bookItems);
        ArgumentNullException.ThrowIfNull(matchGroups);

        var statements = statementLines.ToArray();
        var books = bookItems.ToArray();
        var groups = matchGroups.ToArray();
        var matchedStatementIds = groups.SelectMany(group => group.StatementLineIds).ToHashSet();
        var matchedJournalIds = groups.SelectMany(group => group.JournalEntryIds).ToHashSet();
        var previouslyCleared = (previouslyClearedJournalIds ?? Array.Empty<Guid>()).ToHashSet();

        var unmatchedStatements = statements.Where(line => !matchedStatementIds.Contains(line.Id)).ToArray();
        var unmatchedBooks = books
            .Where(item => !matchedJournalIds.Contains(item.JournalEntryId) && !previouslyCleared.Contains(item.JournalEntryId))
            .ToArray();

        var bookEnding = books.Sum(item => item.Amount);
        var outstandingBook = unmatchedBooks.Sum(item => item.Amount);
        var expectedStatement = bookEnding - outstandingBook;
        var difference = reconciliation.StatementEndingBalance - expectedStatement;

        return new BankReconciliationSummary(
            reconciliation,
            bookEnding,
            outstandingBook,
            expectedStatement,
            difference,
            unmatchedStatements,
            unmatchedBooks,
            groups);
    }
}
