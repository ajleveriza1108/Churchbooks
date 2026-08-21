using System.IO;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.Data.Importing;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class Phase7ImportDataTests
{
    [Fact]
    public async Task SchemaSeven_CreatesImportTables()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();

            foreach (var table in new[]
                     {
                         "import_mapping_templates",
                         "import_sessions",
                         "import_staged_rows"
                     })
            {
                Assert.True(await TableExists(database, table));
            }
        });
    }

    [Fact]
    public async Task SchemaSeven_IsIdempotent()
    {
        await WithDb(async database =>
        {
            var migrator = new Phase7DatabaseMigrator(database);

            await migrator.InitializeAsync();
            await migrator.InitializeAsync();

            Assert.True(await TableExists(database, "import_sessions"));
        });
    }

    [Fact]
    public async Task CsvReader_ReadsHeadersAndRows()
    {
        await WithFile("Date,Description,Amount\n2026-08-20,Gift,10\n", async path =>
        {
            var document = await new SpreadsheetImportReader().ReadAsync(path);

            Assert.Equal(3, document.Headers.Count);
            Assert.Single(document.Rows);
        });
    }

    [Fact]
    public async Task CsvReader_HandlesQuotedComma()
    {
        await WithFile("Description,Amount\n\"Gift, special\",10\n", async path =>
        {
            var document = await new SpreadsheetImportReader().ReadAsync(path);

            Assert.Equal("Gift, special", document.Rows[0][0]);
        });
    }

    [Fact]
    public async Task CsvReader_DetectsSemicolon()
    {
        await WithFile("Date;Amount\n2026-08-20;10\n", async path =>
        {
            var document = await new SpreadsheetImportReader().ReadAsync(path);

            Assert.Equal("10", document.Rows[0][1]);
        });
    }

    [Fact]
    public async Task Reader_RejectsUnsupportedExtension()
    {
        await WithNamedFile("test.txt", "x", async path =>
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => new SpreadsheetImportReader().ReadAsync(path));
        });
    }

    [Fact]
    public async Task Store_RoundTripsTemplate()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSmartImportStore(database);
            var template = new ImportMappingTemplate(
                Guid.NewGuid(),
                "Bank",
                "DATE|AMOUNT",
                [
                    new(0, "Date", ImportColumnRole.Date),
                    new(1, "Amount", ImportColumnRole.Amount)
                ]);

            await store.SaveTemplateAsync(template);

            Assert.NotNull(await store.FindTemplateAsync("DATE|AMOUNT"));
        });
    }

    [Fact]
    public async Task Store_UpdatesTemplateBySignature()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSmartImportStore(database);

            await store.SaveTemplateAsync(
                new ImportMappingTemplate(
                    Guid.NewGuid(),
                    "A",
                    "SIG",
                    [new(0, "A", ImportColumnRole.Ignore)]));

            await store.SaveTemplateAsync(
                new ImportMappingTemplate(
                    Guid.NewGuid(),
                    "B",
                    "SIG",
                    [new(0, "A", ImportColumnRole.Amount)]));

            Assert.Equal("B", (await store.FindTemplateAsync("SIG"))!.Name);
        });
    }

    [Fact]
    public async Task Store_StagesSessionTransactionally()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSmartImportStore(database);
            var row = new ImportStagedRow(Guid.NewGuid(), 2, "fp", "{}", false);
            var session = new ImportSession(
                Guid.NewGuid(),
                "a.csv",
                "hash",
                string.Empty,
                ImportSourceKind.Csv,
                1,
                [row]);

            await store.StageSessionAsync(session);

            Assert.Contains("fp", await store.FindExistingFingerprintsAsync(["fp"]));
        });
    }

    [Fact]
    public async Task Store_FindsOnlyExistingFingerprints()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSmartImportStore(database);
            var row = new ImportStagedRow(Guid.NewGuid(), 2, "known", "{}", false);
            var session = new ImportSession(
                Guid.NewGuid(),
                "a.csv",
                "hash",
                string.Empty,
                ImportSourceKind.Csv,
                1,
                [row]);

            await store.StageSessionAsync(session);
            var fingerprints = await store.FindExistingFingerprintsAsync(["known", "missing"]);

            Assert.Contains("known", fingerprints);
            Assert.DoesNotContain("missing", fingerprints);
        });
    }

    [Fact]
    public async Task Stage_DoesNotCreateJournalEntries()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSmartImportStore(database);
            var session = new ImportSession(
                Guid.NewGuid(),
                "a.csv",
                "hash",
                string.Empty,
                ImportSourceKind.Csv,
                0,
                []);

            await store.StageSessionAsync(session);

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM journal_entries;";

            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        });
    }

    [Fact]
    public async Task SchemaVersion_IsSeven()
    {
        await WithDb(async database =>
        {
            await new Phase7DatabaseMigrator(database).InitializeAsync();

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key='schema_version';";

            Assert.Equal("7", (string)(await command.ExecuteScalarAsync())!);
        });
    }

    private static async Task<bool> TableExists(ChurchBooksDatabase database, string name)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n;";
        command.Parameters.AddWithValue("$n", name);

        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase7.DataTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(root, "test.db"));
            await database.InitializeAsync();
            await action(database);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(root);
        }
    }

    private static Task WithFile(string content, Func<string, Task> action) =>
        WithNamedFile("test.csv", content, action);

    private static async Task WithNamedFile(string name, string content, Func<string, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase7.Files",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var path = Path.Combine(root, name);
            await File.WriteAllTextAsync(path, content);
            await action(path);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(root);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string root)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(80);
            }
        }
    }
}
