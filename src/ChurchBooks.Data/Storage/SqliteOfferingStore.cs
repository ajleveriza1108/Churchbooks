using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Core.Finance;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteOfferingStore : IOfferingStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteOfferingStore(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task AddBatchAsync(OfferingBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO offering_batches(offering_batch_id, service_date, name, reference, batch_status, created_utc, closed_utc)
            VALUES ($id, $date, $name, $reference, $status, $created, $closed);
            """;
        BindBatch(command, batch);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(OfferingBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE offering_batches SET service_date=$date, name=$name, reference=$reference, batch_status=$status, created_utc=$created, closed_utc=$closed
            WHERE offering_batch_id=$id;
            """;
        BindBatch(command, batch);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("The offering batch could not be updated because it no longer exists.");
    }

    public async Task<OfferingBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (batchId == Guid.Empty) return null;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BatchSelect + " WHERE offering_batch_id=$id;";
        command.Parameters.AddWithValue("$id", batchId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBatch(reader) : null;
    }

    public async Task<IReadOnlyList<OfferingBatch>> GetBatchesAsync(DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = new List<string>();
        if (fromDate.HasValue) { where.Add("service_date >= $from"); command.Parameters.AddWithValue("$from", fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
        if (toDate.HasValue) { where.Add("service_date <= $to"); command.Parameters.AddWithValue("$to", toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
        command.CommandText = BatchSelect + (where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where)) + " ORDER BY service_date DESC, name, offering_batch_id;";
        var result = new List<OfferingBatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadBatch(reader));
        return result;
    }

    public async Task<bool> ContributionExistsAsync(Guid contributionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM contributions WHERE contribution_id=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", contributionId.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task SaveContributionAsync(Contribution contribution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO contributions(contribution_id, offering_batch_id, person_id, received_date, currency_code, total_amount, reference, memo, created_utc)
                VALUES ($id, $batch, $person, $date, $currency, $total, $reference, $memo, $created);
                """;
            command.Parameters.AddWithValue("$id", contribution.Id.ToString("D"));
            command.Parameters.AddWithValue("$batch", contribution.BatchId.ToString("D"));
            command.Parameters.AddWithValue("$person", contribution.PersonId.ToString("D"));
            command.Parameters.AddWithValue("$date", contribution.ReceivedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$currency", contribution.Currency.Value);
            command.Parameters.AddWithValue("$total", contribution.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$reference", contribution.Reference);
            command.Parameters.AddWithValue("$memo", contribution.Memo);
            command.Parameters.AddWithValue("$created", contribution.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var line in contribution.Lines)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO contribution_lines(contribution_line_id, contribution_id, giving_category_id, fund_id, amount)
                VALUES ($line, $contribution, $category, $fund, $amount);
                """;
            command.Parameters.AddWithValue("$line", line.Id.ToString("D"));
            command.Parameters.AddWithValue("$contribution", contribution.Id.ToString("D"));
            command.Parameters.AddWithValue("$category", line.GivingCategoryId.ToString("D"));
            command.Parameters.AddWithValue("$fund", line.FundId.ToString("D"));
            command.Parameters.AddWithValue("$amount", line.Amount.ToString("0.00", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        transaction.Commit();
    }

    public async Task<IReadOnlyList<Contribution>> GetContributionsAsync(Guid? personId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = new List<string>();
        if (personId.HasValue) { where.Add("person_id=$person"); command.Parameters.AddWithValue("$person", personId.Value.ToString("D")); }
        if (fromDate.HasValue) { where.Add("received_date >= $from"); command.Parameters.AddWithValue("$from", fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
        if (toDate.HasValue) { where.Add("received_date <= $to"); command.Parameters.AddWithValue("$to", toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
        command.CommandText = """
            SELECT contribution_id, offering_batch_id, person_id, received_date, currency_code, reference, memo, created_utc
            FROM contributions
            """ + (where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where)) + " ORDER BY received_date, contribution_id;";
        var headers = new List<(Guid Id, Guid BatchId, Guid PersonId, DateOnly Date, CurrencyCode Currency, string Reference, string Memo, DateTimeOffset CreatedUtc)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                headers.Add((
                    Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2)),
                    DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture), new CurrencyCode(reader.GetString(4)),
                    reader.GetString(5), reader.GetString(6), DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
            }
        }
        var result = new List<Contribution>(headers.Count);
        foreach (var header in headers)
        {
            var lines = new List<ContributionLine>();
            await using var lineCommand = connection.CreateCommand();
            lineCommand.CommandText = """
                SELECT contribution_line_id, giving_category_id, fund_id, amount
                FROM contribution_lines WHERE contribution_id=$id ORDER BY contribution_line_id;
                """;
            lineCommand.Parameters.AddWithValue("$id", header.Id.ToString("D"));
            await using var reader = await lineCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new ContributionLine(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2)), decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
            }
            result.Add(new Contribution(header.Id, header.BatchId, header.PersonId, header.Date, header.Currency, lines, header.Reference, header.Memo, header.CreatedUtc));
        }
        return result;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static void BindBatch(SqliteCommand command, OfferingBatch batch)
    {
        command.Parameters.AddWithValue("$id", batch.Id.ToString("D"));
        command.Parameters.AddWithValue("$date", batch.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$name", batch.Name);
        command.Parameters.AddWithValue("$reference", batch.Reference);
        command.Parameters.AddWithValue("$status", batch.Status.ToString());
        command.Parameters.AddWithValue("$created", batch.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$closed", batch.ClosedUtc.HasValue ? batch.ClosedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
    }

    private static OfferingBatch ReadBatch(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        reader.GetString(2), reader.GetString(3), Enum.Parse<OfferingBatchStatus>(reader.GetString(4), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private const string BatchSelect = "SELECT offering_batch_id, service_date, name, reference, batch_status, created_utc, closed_utc FROM offering_batches";
}
