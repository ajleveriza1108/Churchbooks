using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Accounting.Reconciliation;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class Phase8ReconciliationDataTests
{
    [Fact]
    public async Task Migrator_SetsSchemaVersionEight()
    {
        await WithContextAsync(async context =>
        {
            await using var connection = context.Database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key='schema_version';";

            Assert.Equal("8", (string?)await command.ExecuteScalarAsync());
        });
    }

    [Fact]
    public async Task Migrator_CreatesReconciliationTables()
    {
        await WithContextAsync(async context =>
        {
            await using var connection = context.Database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type='table'
                  AND name IN (
                    'bank_statement_lines',
                    'bank_reconciliations',
                    'bank_reconciliation_match_groups');
                """;

            Assert.Equal(3L, (long)(await command.ExecuteScalarAsync() ?? 0L));
        });
    }

    [Fact]
    public async Task Migrator_IsIdempotent()
    {
        await WithContextAsync(async context =>
        {
            await new Phase8DatabaseMigrator(context.Database).InitializeAsync();
            await new Phase8DatabaseMigrator(context.Database).InitializeAsync();

            Assert.NotNull(await new SqliteBankingStore(context.Database).GetBankAccountAsync(context.Bank.Id));
        });
    }

    [Fact]
    public async Task SmartImportStore_RoundTripsSessionForReconciliation()
    {
        await WithContextAsync(async context =>
        {
            var session = CreateImportSession();
            var store = new SqliteSmartImportStore(context.Database);
            await store.StageSessionAsync(session);

            var loaded = await store.GetSessionAsync(session.Id);

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Rows);
            Assert.Equal(session.FileName, loaded.FileName);
        });
    }

    [Fact]
    public async Task StatementStore_RoundTripsStatementLine()
    {
        await WithContextAsync(async context =>
        {
            var session = await StageSessionAsync(context);
            var line = CreateStatement(context.Bank.Id, session.Id, "fp-1", 100m);
            var store = new SqliteBankReconciliationStore(context.Database);
            await store.SaveStatementImportAsync(context.Bank.Id, session.Id, new[] { line });

            var loaded = await store.GetStatementLinesAsync(
                context.Bank.Id,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31));

            Assert.Equal(100m, Assert.Single(loaded).Amount);
        });
    }

    [Fact]
    public async Task StatementStore_RejectsImportSessionReuse()
    {
        await WithContextAsync(async context =>
        {
            var session = await StageSessionAsync(context);
            var store = new SqliteBankReconciliationStore(context.Database);
            await store.SaveStatementImportAsync(
                context.Bank.Id,
                session.Id,
                new[] { CreateStatement(context.Bank.Id, session.Id, "fp-2", 100m) });

            await Assert.ThrowsAnyAsync<Exception>(() =>
                store.SaveStatementImportAsync(
                    context.Bank.Id,
                    session.Id,
                    new[] { CreateStatement(context.Bank.Id, session.Id, "fp-3", 200m) }));
        });
    }

    [Fact]
    public async Task StatementStore_RejectsDuplicateFingerprintForSameBank()
    {
        await WithContextAsync(async context =>
        {
            var firstSession = await StageSessionAsync(context, "first.csv", "hash-1");
            var secondSession = await StageSessionAsync(context, "second.csv", "hash-2");
            var store = new SqliteBankReconciliationStore(context.Database);

            await store.SaveStatementImportAsync(
                context.Bank.Id,
                firstSession.Id,
                new[] { CreateStatement(context.Bank.Id, firstSession.Id, "same-fingerprint", 100m) });

            await Assert.ThrowsAnyAsync<Exception>(() =>
                store.SaveStatementImportAsync(
                    context.Bank.Id,
                    secondSession.Id,
                    new[] { CreateStatement(context.Bank.Id, secondSession.Id, "same-fingerprint", 100m) }));
        });
    }

    [Fact]
    public async Task ReconciliationStore_RoundTripsDraft()
    {
        await WithContextAsync(async context =>
        {
            var reconciliation = CreateReconciliation(context.Bank.Id);
            var store = new SqliteBankReconciliationStore(context.Database);
            await store.SaveReconciliationAsync(reconciliation);

            var loaded = await store.GetReconciliationAsync(reconciliation.Id);

            Assert.NotNull(loaded);
            Assert.Equal(BankReconciliationStatus.Draft, loaded!.Status);
            Assert.Equal(100m, loaded.StatementEndingBalance);
        });
    }

    [Fact]
    public async Task MatchStore_RoundTripsGroup()
    {
        await WithContextAsync(async context =>
        {
            var seeded = await SeedMatchableDataAsync(context);
            var store = new SqliteBankReconciliationStore(context.Database);
            var group = new ReconciliationMatchGroup(
                Guid.NewGuid(),
                seeded.Reconciliation.Id,
                new[] { seeded.Statement.Id },
                new[] { seeded.JournalId });
            await store.SaveMatchGroupAsync(group);

            var loaded = Assert.Single(await store.GetMatchGroupsAsync(seeded.Reconciliation.Id));

            Assert.Equal(seeded.Statement.Id, Assert.Single(loaded.StatementLineIds));
            Assert.Equal(seeded.JournalId, Assert.Single(loaded.JournalEntryIds));
        });
    }

    [Fact]
    public async Task MatchStore_DeletesDraftGroup()
    {
        await WithContextAsync(async context =>
        {
            var seeded = await SeedMatchableDataAsync(context);
            var store = new SqliteBankReconciliationStore(context.Database);
            var group = new ReconciliationMatchGroup(
                Guid.NewGuid(),
                seeded.Reconciliation.Id,
                new[] { seeded.Statement.Id },
                new[] { seeded.JournalId });
            await store.SaveMatchGroupAsync(group);
            await store.DeleteMatchGroupAsync(group.Id);

            Assert.Empty(await store.GetMatchGroupsAsync(seeded.Reconciliation.Id));
        });
    }

    [Fact]
    public async Task MatchStore_RejectsJournalReuse()
    {
        await WithContextAsync(async context =>
        {
            var seeded = await SeedMatchableDataAsync(context);
            var store = new SqliteBankReconciliationStore(context.Database);
            await store.SaveMatchGroupAsync(new ReconciliationMatchGroup(
                Guid.NewGuid(),
                seeded.Reconciliation.Id,
                new[] { seeded.Statement.Id },
                new[] { seeded.JournalId }));

            var session2 = await StageSessionAsync(context, "statement2.csv", "hash-2");
            var statement2 = CreateStatement(context.Bank.Id, session2.Id, "fp-reuse-2", 100m);
            await store.SaveStatementImportAsync(context.Bank.Id, session2.Id, new[] { statement2 });

            await Assert.ThrowsAnyAsync<Exception>(() =>
                store.SaveMatchGroupAsync(new ReconciliationMatchGroup(
                    Guid.NewGuid(),
                    seeded.Reconciliation.Id,
                    new[] { statement2.Id },
                    new[] { seeded.JournalId })));
        });
    }

    [Fact]
    public async Task CompletedReconciliation_LocksMatchesAndReturnsClearedJournal()
    {
        await WithContextAsync(async context =>
        {
            var seeded = await SeedMatchableDataAsync(context);
            var store = new SqliteBankReconciliationStore(context.Database);
            var group = new ReconciliationMatchGroup(
                Guid.NewGuid(),
                seeded.Reconciliation.Id,
                new[] { seeded.Statement.Id },
                new[] { seeded.JournalId });
            await store.SaveMatchGroupAsync(group);
            await store.MarkCompletedAsync(seeded.Reconciliation.Id, DateTimeOffset.UtcNow);

            var cleared = await store.GetUnavailableJournalIdsAsync(context.Bank.Id);

            Assert.Contains(seeded.JournalId, cleared);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DeleteMatchGroupAsync(group.Id));
        });
    }

    private static async Task WithContextAsync(Func<TestContext, Task> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase8.Data.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(directory, "phase8.db"));
            await new Phase8DatabaseMigrator(database).InitializeAsync();

            var ledger = new Account(Guid.NewGuid(), "1018", "Reconciliation Bank", AccountType.Asset);
            var bank = new BankAccount(
                Guid.NewGuid(),
                "Operating Bank",
                "Test Bank",
                "1234",
                CurrencyCode.Php,
                ledger.Id);
            await new SqliteBankingStore(database).AddBankAccountAsync(bank, ledger);

            await action(new TestContext(database, bank));
        }
        finally
        {
            DeleteDirectoryWithRetry(directory);
        }
    }

    private static async Task<ImportSession> StageSessionAsync(
        TestContext context,
        string fileName = "statement.csv",
        string fileHash = "statement-hash")
    {
        var session = CreateImportSession(fileName, fileHash);
        await new SqliteSmartImportStore(context.Database).StageSessionAsync(session);
        return session;
    }

    private static ImportSession CreateImportSession(
        string fileName = "statement.csv",
        string fileHash = "statement-hash") =>
        new(
            Guid.NewGuid(),
            fileName,
            fileHash,
            string.Empty,
            ImportSourceKind.Csv,
            1,
            new[]
            {
                new ImportStagedRow(
                    Guid.NewGuid(),
                    2,
                    Guid.NewGuid().ToString("N"),
                    """{"Date":"2026-08-20","Amount":"100","Description":"Deposit"}""",
                    false)
            });

    private static BankStatementLine CreateStatement(
        Guid bankAccountId,
        Guid sessionId,
        string fingerprint,
        decimal amount) =>
        new(
            Guid.NewGuid(),
            bankAccountId,
            sessionId,
            new DateOnly(2026, 8, 20),
            amount,
            "Deposit",
            "R",
            fingerprint,
            2);

    private static BankReconciliation CreateReconciliation(Guid bankAccountId) =>
        new(
            Guid.NewGuid(),
            bankAccountId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            100m);

    private static async Task<SeededMatchData> SeedMatchableDataAsync(TestContext context)
    {
        var session = await StageSessionAsync(context);
        var statement = CreateStatement(context.Bank.Id, session.Id, "fp-match", 100m);
        var reconciliation = CreateReconciliation(context.Bank.Id);
        var reconciliationStore = new SqliteBankReconciliationStore(context.Database);
        await reconciliationStore.SaveStatementImportAsync(context.Bank.Id, session.Id, new[] { statement });
        await reconciliationStore.SaveReconciliationAsync(reconciliation);

        var accountingStore = new SqliteAccountingStore(context.Database);
        var period = new AccountingPeriod(
            Guid.NewGuid(),
            "August 2026",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));
        await accountingStore.AddPeriodAsync(period);

        var offset = new Account(Guid.NewGuid(), "4018", "Test Income", AccountType.Income);
        await accountingStore.AddAccountAsync(offset);

        var journalId = Guid.NewGuid();
        var journal = new JournalEntry(
            journalId,
            "R8-TEST-" + Guid.NewGuid().ToString("N")[..8],
            period.Id,
            new DateOnly(2026, 8, 20),
            "Deposit",
            CurrencyCode.Php,
            new[]
            {
                new JournalLine(Guid.NewGuid(), context.Bank.LedgerAccountId, 100m, 0m),
                new JournalLine(Guid.NewGuid(), offset.Id, 0m, 100m)
            });

        await new AccountingEngine(accountingStore).PostJournalAsync(journal);
        return new SeededMatchData(reconciliation, statement, journalId);
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            if (!Directory.Exists(directory)) return;
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 20)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
        }

        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed record TestContext(ChurchBooksDatabase Database, BankAccount Bank);
    private sealed record SeededMatchData(
        BankReconciliation Reconciliation,
        BankStatementLine Statement,
        Guid JournalId);
}
