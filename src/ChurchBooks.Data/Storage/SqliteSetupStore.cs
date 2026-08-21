using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Setup;
using ChurchBooks.Core.Finance;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteSetupStore : ISetupStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteSetupStore(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<OrganizationProfile?> GetOrganizationProfileAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT display_name, legal_name, base_currency, fiscal_year_start_month,
                   country_code, tax_identifier, setup_complete, updated_utc
            FROM organization_profile
            WHERE profile_key = 'PRIMARY';
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new OrganizationProfile(
            reader.GetString(0),
            reader.GetString(1),
            new CurrencyCode(reader.GetString(2)),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6) == 1,
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    public async Task SaveOrganizationProfileAsync(OrganizationProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO organization_profile(
                profile_key, display_name, legal_name, base_currency, fiscal_year_start_month,
                country_code, tax_identifier, setup_complete, updated_utc)
            VALUES('PRIMARY', $display, $legal, $currency, $month, $country, $tax, $complete, $updated)
            ON CONFLICT(profile_key) DO UPDATE SET
                display_name = excluded.display_name,
                legal_name = excluded.legal_name,
                base_currency = excluded.base_currency,
                fiscal_year_start_month = excluded.fiscal_year_start_month,
                country_code = excluded.country_code,
                tax_identifier = excluded.tax_identifier,
                setup_complete = excluded.setup_complete,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$display", profile.DisplayName);
        command.Parameters.AddWithValue("$legal", profile.LegalName);
        command.Parameters.AddWithValue("$currency", profile.BaseCurrency.Value);
        command.Parameters.AddWithValue("$month", profile.FiscalYearStartMonth);
        command.Parameters.AddWithValue("$country", profile.CountryCode);
        command.Parameters.AddWithValue("$tax", profile.TaxIdentifier);
        command.Parameters.AddWithValue("$complete", profile.SetupComplete ? 1 : 0);
        command.Parameters.AddWithValue("$updated", profile.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerminologyDefinition>> GetTerminologyOverridesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT term_key, singular_label, plural_label FROM terminology_overrides ORDER BY term_key;";

        var items = new List<TerminologyDefinition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TerminologyDefinition(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        return items;
    }

    public async Task SaveTerminologyAsync(TerminologyDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO terminology_overrides(term_key, singular_label, plural_label, updated_utc)
            VALUES($key, $singular, $plural, $updated)
            ON CONFLICT(term_key) DO UPDATE SET
                singular_label = excluded.singular_label,
                plural_label = excluded.plural_label,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$key", definition.Key);
        command.Parameters.AddWithValue("$singular", definition.Singular);
        command.Parameters.AddWithValue("$plural", definition.Plural);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomSearchAlias>> GetCustomSearchAliasesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT alias_id, area_key, alias_text FROM custom_search_aliases ORDER BY area_key, alias_text;";

        var items = new List<CustomSearchAlias>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CustomSearchAlias(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
        }
        return items;
    }

    public async Task AddCustomSearchAliasAsync(CustomSearchAlias alias, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alias);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO custom_search_aliases(alias_id, area_key, alias_text, created_utc)
            VALUES($id, $area, $text, $created);
            """;
        command.Parameters.AddWithValue("$id", alias.Id.ToString("D"));
        command.Parameters.AddWithValue("$area", alias.AreaKey);
        command.Parameters.AddWithValue("$text", alias.AliasText);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteCustomSearchAliasAsync(Guid aliasId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM custom_search_aliases WHERE alias_id = $id;";
        command.Parameters.AddWithValue("$id", aliasId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
