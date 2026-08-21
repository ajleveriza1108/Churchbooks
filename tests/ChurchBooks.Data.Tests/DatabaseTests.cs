using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public async Task InitializeAsync_CreatesDatabaseFile_AndReleasesItForCleanup()
    {
        var testRoot = CreateTestRoot();
        var databasePath = Path.Combine(testRoot, "test.db");

        try
        {
            var database = new ChurchBooksDatabase(databasePath);
            await database.InitializeAsync();

            Assert.True(File.Exists(databasePath));

            Directory.Delete(testRoot, recursive: true);
            Assert.False(Directory.Exists(testRoot));
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesPhase2AccountingSchema()
    {
        var testRoot = CreateTestRoot();
        var databasePath = Path.Combine(testRoot, "test.db");

        try
        {
            var database = new ChurchBooksDatabase(databasePath);
            await database.InitializeAsync();

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            var tables = await ReadTableNamesAsync(connection);

            Assert.Contains("accounts", tables);
            Assert.Contains("accounting_periods", tables);
            Assert.Contains("journal_entries", tables);
            Assert.Contains("journal_lines", tables);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task AccountingEngine_PostBalancedJournal_PersistsLedgerExactly()
    {
        var testRoot = CreateTestRoot();
        var databasePath = Path.Combine(testRoot, "test.db");

        try
        {
            var database = new ChurchBooksDatabase(databasePath);
            await database.InitializeAsync();
            var store = new SqliteAccountingStore(database);
            var engine = new AccountingEngine(store);

            var cash = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
            var giving = new Account(Guid.NewGuid(), "4000", "Contribution Income", AccountType.Income);
            var period = new AccountingPeriod(Guid.NewGuid(), "August 2026", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

            await store.AddAccountAsync(cash);
            await store.AddAccountAsync(giving);
            await store.AddPeriodAsync(period);

            var journal = new JournalEntry(
                Guid.NewGuid(),
                "JE-000001",
                period.Id,
                new DateOnly(2026, 8, 16),
                "Sunday offering",
                CurrencyCode.Php,
                new[]
                {
                    new JournalLine(Guid.NewGuid(), cash.Id, 12345.67m, 0m, "Offering received"),
                    new JournalLine(Guid.NewGuid(), giving.Id, 0m, 12345.67m, "Contribution income")
                },
                "SERVICE-2026-08-16-AM");

            var postedAt = new DateTimeOffset(2026, 8, 16, 13, 0, 0, TimeSpan.FromHours(8));
            var posted = await engine.PostJournalAsync(journal, postedAt);
            var cashLedger = await store.GetLedgerAsync(cash.Id);
            var givingLedger = await store.GetLedgerAsync(giving.Id);
            var cashLedgerFromDate = await store.GetLedgerAsync(cash.Id, new DateOnly(2026, 8, 16));
            var cashLedgerToDate = await store.GetLedgerAsync(cash.Id, toDate: new DateOnly(2026, 8, 16));
            var cashLedgerRange = await store.GetLedgerAsync(
                cash.Id,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31));
            var cashLedgerOutsideRange = await store.GetLedgerAsync(
                cash.Id,
                new DateOnly(2026, 8, 17),
                new DateOnly(2026, 8, 31));

            Assert.Equal(postedAt, posted.PostedUtc);
            Assert.Single(cashLedger);
            Assert.Single(givingLedger);
            Assert.Single(cashLedgerFromDate);
            Assert.Single(cashLedgerToDate);
            Assert.Single(cashLedgerRange);
            Assert.Empty(cashLedgerOutsideRange);
            Assert.Equal(12345.67m, cashLedger[0].Debit);
            Assert.Equal(0m, cashLedger[0].Credit);
            Assert.Equal(0m, givingLedger[0].Debit);
            Assert.Equal(12345.67m, givingLedger[0].Credit);
            Assert.Equal("SERVICE-2026-08-16-AM", cashLedger[0].Reference);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task AccountingEngine_UnbalancedJournal_IsRejectedBeforePersistence()
    {
        var testRoot = CreateTestRoot();
        var databasePath = Path.Combine(testRoot, "test.db");

        try
        {
            var database = new ChurchBooksDatabase(databasePath);
            await database.InitializeAsync();
            var store = new SqliteAccountingStore(database);
            var engine = new AccountingEngine(store);

            var cash = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
            var giving = new Account(Guid.NewGuid(), "4000", "Contribution Income", AccountType.Income);
            var period = new AccountingPeriod(Guid.NewGuid(), "August 2026", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

            await store.AddAccountAsync(cash);
            await store.AddAccountAsync(giving);
            await store.AddPeriodAsync(period);

            var journal = new JournalEntry(
                Guid.NewGuid(),
                "JE-000002",
                period.Id,
                new DateOnly(2026, 8, 16),
                "Invalid offering",
                CurrencyCode.Php,
                new[]
                {
                    new JournalLine(Guid.NewGuid(), cash.Id, 1000m, 0m),
                    new JournalLine(Guid.NewGuid(), giving.Id, 0m, 999m)
                });

            await Assert.ThrowsAsync<JournalPostingException>(() => engine.PostJournalAsync(journal));
            Assert.False(await store.JournalExistsAsync(journal.Id));
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    private static string CreateTestRoot() => Path.Combine(Path.GetTempPath(), "ChurchBooks.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
