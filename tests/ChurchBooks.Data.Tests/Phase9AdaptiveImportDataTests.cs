using System.IO;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.Accounting.People;
using ChurchBooks.Data.Importing;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class Phase9AdaptiveImportDataTests
{
    [Fact]
    public async Task SchemaNine_CreatesAdaptiveImportTables()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            Assert.True(await TableExists(database, "import_source_profiles"));
            Assert.True(await TableExists(database, "person_import_external_links"));
        });
    }

    [Fact]
    public async Task SchemaVersion_IsNine()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key='schema_version';";
            Assert.Equal("9", (string)(await command.ExecuteScalarAsync())!);
        });
    }

    [Fact]
    public async Task SourceProfile_RoundTripsMappings()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteAdaptiveImportStore(database);
            var profile = Profile("People A", "FIRSTNAME|LASTNAME");
            await store.SaveSourceProfileAsync(profile);
            var loaded = Assert.Single(await store.FindSourceProfilesAsync(profile.SourceSignature));
            Assert.Equal(ImportPurpose.PeopleDirectory, loaded.Purpose);
            Assert.Equal(2, loaded.Mappings.Count);
        });
    }

    [Fact]
    public async Task MultipleProfiles_CanShareSameLayoutWithoutConfusingNames()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteAdaptiveImportStore(database);
            await store.SaveSourceProfileAsync(Profile("Sunday Directory", "SIG"));
            await store.SaveSourceProfileAsync(Profile("Annual Directory", "SIG"));
            Assert.Equal(2, (await store.FindSourceProfilesAsync("SIG")).Count);
        });
    }

    [Fact]
    public async Task ExternalPersonLink_RoundTripsWithinSourceProfile()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteAdaptiveImportStore(database);
            var profile = Profile("Directory", "SIG");
            await store.SaveSourceProfileAsync(profile);
            var person = new PersonProfile(Guid.NewGuid(), "Ana", "Cruz", true, false, memberNumber: "M1");
            await new SqlitePeopleGivingStore(database).AddPersonAsync(person);
            await store.SavePersonExternalLinkAsync(profile.Id, "EXT-1", person.Id);
            Assert.Equal(person.Id, await store.FindLinkedPersonIdAsync(profile.Id, "ext-1"));
        });
    }

    [Fact]
    public async Task ExternalPersonLink_RefusesSilentReassignment()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteAdaptiveImportStore(database);
            var profile = Profile("Directory", "SIG");
            await store.SaveSourceProfileAsync(profile);
            var people = new SqlitePeopleGivingStore(database);
            var first = new PersonProfile(Guid.NewGuid(), "Ana", "Cruz", true, false);
            var second = new PersonProfile(Guid.NewGuid(), "Bea", "Santos", true, false);
            await people.AddPersonAsync(first);
            await people.AddPersonAsync(second);
            await store.SavePersonExternalLinkAsync(profile.Id, "EXT-1", first.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SavePersonExternalLinkAsync(profile.Id, "EXT-1", second.Id));
        });
    }

    [Fact]
    public async Task CsvReader_DetectsHeaderBelowReportTitle()
    {
        await WithFile("Member Directory\nGenerated 2026-08-20\nGiven Name,Surname,Email\nAna,Cruz,ana@example.test\n", async path =>
        {
            var document = await new SpreadsheetImportReader().ReadAsync(path);
            Assert.Equal(3, document.HeaderRowNumber);
            Assert.Equal("Given Name", document.Headers[0]);
            Assert.Single(document.Rows);
        });
    }

    [Fact]
    public async Task CsvReader_DetectsDelimiterBeyondTitleRow()
    {
        await WithFile("Giving Export\nDate;Gift Amount;Fund\n2026-08-20;100;GENERAL\n", async path =>
        {
            var document = await new SpreadsheetImportReader().ReadAsync(path);
            Assert.Equal("Gift Amount", document.Headers[1]);
            Assert.Equal("100", document.Rows[0][1]);
        });
    }

    [Fact]
    public async Task StandardTemplateWriter_WritesRecommendedHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase9.TemplateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "people.csv");
            var template = ChurchBooksStandardImportTemplates.All.Single(x => x.Purpose == ImportPurpose.PeopleDirectory);
            await new StandardImportTemplateWriter().WriteCsvAsync(template, path);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("External Person ID", text, StringComparison.Ordinal);
            Assert.Contains("First Name", text, StringComparison.Ordinal);
        }
        finally { await DeleteDirectoryWithRetryAsync(root); }
    }

    [Fact]
    public async Task SchemaNine_PreservesPhaseEightReconciliationTables()
    {
        await WithDb(async database =>
        {
            await new Phase9DatabaseMigrator(database).InitializeAsync();
            Assert.True(await TableExists(database, "bank_reconciliations"));
            Assert.True(await TableExists(database, "bank_reconciliation_match_groups"));
        });
    }

    private static ImportSourceProfile Profile(string name, string signature) =>
        new(Guid.NewGuid(), name, ImportPurpose.PeopleDirectory, signature,
        [new ImportColumnMapping(0, "First", ImportColumnRole.FirstName), new ImportColumnMapping(1, "Last", ImportColumnRole.LastName)],
        PersonImportDefaultRole.Member);

    private static async Task<bool> TableExists(ChurchBooksDatabase database, string name)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", name);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase9.DataTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(root, "test.db"));
            await database.InitializeAsync();
            await action(database);
        }
        finally { await DeleteDirectoryWithRetryAsync(root); }
    }

    private static async Task WithFile(string content, Func<string, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase9.FileTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "test.csv");
            await File.WriteAllTextAsync(path, content);
            await action(path);
        }
        finally { await DeleteDirectoryWithRetryAsync(root); }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string root)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 7)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);
            }
        }
    }
}
