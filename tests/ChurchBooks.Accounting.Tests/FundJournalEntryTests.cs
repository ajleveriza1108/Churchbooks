using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundJournalEntryTests
{
    [Fact]
    public void Constructor_ExactOneFundPerLine_IsAccepted()
    {
        var fundId = Guid.NewGuid();
        var journal = CreateJournal();
        var entry = new FundJournalEntry(
            journal,
            journal.Lines.Select(line => new FundAssignment(line.Id, fundId)));

        Assert.Equal(journal.Lines.Count, entry.Assignments.Count);
        Assert.Single(entry.FundIds);
        Assert.All(journal.Lines, line => Assert.Equal(fundId, entry.GetFundId(line.Id)));
    }

    [Fact]
    public void Constructor_MissingLineAssignment_IsRejected()
    {
        var journal = CreateJournal();
        var assignments = new[] { new FundAssignment(journal.Lines[0].Id, Guid.NewGuid()) };

        var error = Assert.Throws<ArgumentException>(() => new FundJournalEntry(journal, assignments));
        Assert.Contains("Every journal line must have exactly one fund assignment", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DuplicateLineAssignment_IsRejected()
    {
        var journal = CreateJournal();
        var assignments = new[]
        {
            new FundAssignment(journal.Lines[0].Id, Guid.NewGuid()),
            new FundAssignment(journal.Lines[0].Id, Guid.NewGuid())
        };

        var error = Assert.Throws<ArgumentException>(() => new FundJournalEntry(journal, assignments));
        Assert.Contains("cannot have more than one fund assignment", error.Message, StringComparison.Ordinal);
    }

    private static JournalEntry CreateJournal() => new(
        Guid.NewGuid(),
        "FUND-JE-1",
        Guid.NewGuid(),
        new DateOnly(2026, 8, 19),
        "Fund assignment test",
        CurrencyCode.Php,
        new[]
        {
            new JournalLine(Guid.NewGuid(), Guid.NewGuid(), 100m, 0m),
            new JournalLine(Guid.NewGuid(), Guid.NewGuid(), 0m, 100m)
        });
}
