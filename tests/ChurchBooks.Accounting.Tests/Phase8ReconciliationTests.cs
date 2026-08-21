using ChurchBooks.Accounting.Importing;
using ChurchBooks.Accounting.Reconciliation;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class Phase8ReconciliationTests
{
    [Fact]
    public void StatementLine_RejectsZeroAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateStatement(amount: 0m));
    }

    [Fact]
    public void MatchGroup_RequiresBothSides()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReconciliationMatchGroup(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new[] { Guid.NewGuid() },
                Array.Empty<Guid>()));
    }

    [Fact]
    public void Calculator_TreatsUnmatchedDepositAsDepositInTransit()
    {
        var reconciliation = CreateReconciliation(statementEndingBalance: 0m);
        var book = new ReconciliationBookItem(Guid.NewGuid(), "J1", new DateOnly(2026, 8, 20), 100m, "Deposit", "");

        var summary = BankReconciliationCalculator.BuildSummary(
            reconciliation,
            Array.Empty<BankStatementLine>(),
            new[] { book },
            Array.Empty<ReconciliationMatchGroup>());

        Assert.Equal(100m, summary.OutstandingBookAmount);
        Assert.Equal(0m, summary.ExpectedStatementBalance);
        Assert.Equal(0m, summary.Difference);
    }

    [Fact]
    public void Calculator_TreatsUnmatchedWithdrawalAsOutstandingCheck()
    {
        var reconciliation = CreateReconciliation(statementEndingBalance: 0m);
        var book = new ReconciliationBookItem(Guid.NewGuid(), "J2", new DateOnly(2026, 8, 20), -75m, "Check", "");

        var summary = BankReconciliationCalculator.BuildSummary(
            reconciliation,
            Array.Empty<BankStatementLine>(),
            new[] { book },
            Array.Empty<ReconciliationMatchGroup>());

        Assert.Equal(-75m, summary.OutstandingBookAmount);
        Assert.Equal(0m, summary.ExpectedStatementBalance);
        Assert.Equal(0m, summary.Difference);
    }

    [Fact]
    public void Calculator_AllMatchedAndZeroDifferenceCanComplete()
    {
        var statement = CreateStatement(amount: 100m);
        var book = new ReconciliationBookItem(Guid.NewGuid(), "J3", statement.TransactionDate, 100m, "Deposit", "");
        var reconciliation = CreateReconciliation(statementEndingBalance: 100m);
        var group = new ReconciliationMatchGroup(
            Guid.NewGuid(),
            reconciliation.Id,
            new[] { statement.Id },
            new[] { book.JournalEntryId });

        var summary = BankReconciliationCalculator.BuildSummary(
            reconciliation,
            new[] { statement },
            new[] { book },
            new[] { group });

        Assert.True(summary.CanComplete);
        Assert.Equal(0m, summary.Difference);
    }

    [Fact]
    public void Calculator_UnmatchedStatementBlocksCompletion()
    {
        var statement = CreateStatement(amount: 100m);
        var summary = BankReconciliationCalculator.BuildSummary(
            CreateReconciliation(statementEndingBalance: 0m),
            new[] { statement },
            Array.Empty<ReconciliationBookItem>(),
            Array.Empty<ReconciliationMatchGroup>());

        Assert.False(summary.CanComplete);
        Assert.Single(summary.UnmatchedStatementLines);
    }

    [Fact]
    public void Calculator_PreviouslyClearedJournalIsNotOutstanding()
    {
        var journalId = Guid.NewGuid();
        var book = new ReconciliationBookItem(journalId, "J4", new DateOnly(2026, 7, 1), 100m, "Old", "");
        var summary = BankReconciliationCalculator.BuildSummary(
            CreateReconciliation(statementEndingBalance: 100m),
            Array.Empty<BankStatementLine>(),
            new[] { book },
            Array.Empty<ReconciliationMatchGroup>(),
            new[] { journalId });

        Assert.Empty(summary.UnmatchedBookItems);
        Assert.Equal(0m, summary.OutstandingBookAmount);
    }

    [Fact]
    public void Matcher_SuggestsUniqueExactMatch()
    {
        var statement = CreateStatement(amount: 125m);
        var book = new ReconciliationBookItem(
            Guid.NewGuid(),
            "J5",
            statement.TransactionDate.AddDays(1),
            125m,
            "Deposit",
            "");

        var matches = new ReconciliationMatcher().SuggestExactMatches(new[] { statement }, new[] { book });

        var match = Assert.Single(matches);
        Assert.Equal(statement.Id, match.StatementLineId);
        Assert.Equal(book.JournalEntryId, match.JournalEntryId);
    }

    [Fact]
    public void Matcher_RefusesAmbiguousExactMatch()
    {
        var statement = CreateStatement(amount: 125m);
        var books = new[]
        {
            new ReconciliationBookItem(Guid.NewGuid(), "J6", statement.TransactionDate, 125m, "A", ""),
            new ReconciliationBookItem(Guid.NewGuid(), "J7", statement.TransactionDate, 125m, "B", "")
        };

        var matches = new ReconciliationMatcher().SuggestExactMatches(new[] { statement }, books);

        Assert.Empty(matches);
    }

    [Fact]
    public void ImportConverter_UsesExplicitCreditIncreasesConvention()
    {
        var row = new ImportStagedRow(
            Guid.NewGuid(),
            2,
            "fingerprint",
            """{"Date":"2026-08-20","Debit":"10","Credit":"20","Description":"Bank activity"}""",
            false);
        var session = new ImportSession(
            Guid.NewGuid(),
            "statement.csv",
            "hash",
            string.Empty,
            ImportSourceKind.Csv,
            1,
            new[] { row });

        var lines = new BankStatementImportConverter().Convert(
            session,
            Guid.NewGuid(),
            BankStatementAmountConvention.CreditIncreasesBalance);

        var line = Assert.Single(lines);
        Assert.Equal(new DateOnly(2026, 8, 20), line.TransactionDate);
        Assert.Equal(10m, line.Amount);
    }

    private static BankStatementLine CreateStatement(decimal amount) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 20),
            amount,
            "Statement",
            string.Empty,
            Guid.NewGuid().ToString("N"),
            2);

    private static BankReconciliation CreateReconciliation(decimal statementEndingBalance) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            statementEndingBalance);
}
