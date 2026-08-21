using System.Data.Common;
using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteFundAccountingStore : IFundAccountingStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteFundAccountingStore(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task AddFundAsync(Fund fund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fund);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO funds(
                fund_id, code, name, restriction_class, overspend_policy, purpose,
                fund_status, created_utc, archived_utc)
            VALUES (
                $fund_id, $code, $name, $restriction_class, $overspend_policy, $purpose,
                $fund_status, $created_utc, $archived_utc);
            """;
        BindFund(command, fund, includeCreatedUtc: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateFundAsync(Fund fund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fund);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE funds
            SET code = $code,
                name = $name,
                restriction_class = $restriction_class,
                overspend_policy = $overspend_policy,
                purpose = $purpose,
                fund_status = $fund_status,
                archived_utc = $archived_utc
            WHERE fund_id = $fund_id;
            """;
        BindFund(command, fund, includeCreatedUtc: false);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed != 1)
        {
            throw new InvalidOperationException($"Fund {fund.Id:D} does not exist.");
        }
    }

    public async Task<Fund?> GetFundAsync(Guid fundId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT fund_id, code, name, restriction_class, overspend_policy, purpose, fund_status, archived_utc
            FROM funds
            WHERE fund_id = $fund_id;
            """;
        command.Parameters.AddWithValue("$fund_id", fundId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadFund(reader) : null;
    }

    public async Task<IReadOnlyList<Fund>> GetAllFundsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT fund_id, code, name, restriction_class, overspend_policy, purpose, fund_status, archived_utc
            FROM funds
            WHERE $include_archived = 1 OR fund_status = 'Active'
            ORDER BY code, name;
            """;
        command.Parameters.AddWithValue("$include_archived", includeArchived ? 1 : 0);

        var funds = new List<Fund>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            funds.Add(ReadFund(reader));
        }

        return funds;
    }

    public async Task<IReadOnlyDictionary<Guid, Fund>> GetFundsAsync(
        IEnumerable<Guid> fundIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundIds);

        var ids = fundIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, Fund>();
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = BindGuidList(command, ids, "$fund");
        command.CommandText = $"""
            SELECT fund_id, code, name, restriction_class, overspend_policy, purpose, fund_status, archived_utc
            FROM funds
            WHERE fund_id IN ({string.Join(", ", parameterNames)});
            """;

        var results = new Dictionary<Guid, Fund>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fund = ReadFund(reader);
            results[fund.Id] = fund;
        }

        return results;
    }

    public Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsync(
        IEnumerable<Guid> fundIds,
        CancellationToken cancellationToken = default) =>
        GetFundBalancesInternalAsync(fundIds, asOfDate: null, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsOfAsync(
        IEnumerable<Guid> fundIds,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default) =>
        GetFundBalancesInternalAsync(fundIds, asOfDate, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesInternalAsync(
        IEnumerable<Guid> fundIds,
        DateOnly? asOfDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fundIds);

        var ids = fundIds.Distinct().ToArray();
        var balances = ids.ToDictionary(static id => id, static _ => 0m);
        if (ids.Length == 0)
        {
            return balances;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = BindGuidList(command, ids, "$fund");
        command.CommandText = $"""
            SELECT jlf.fund_id, a.account_type, jl.debit, jl.credit
            FROM journal_line_funds AS jlf
            INNER JOIN journal_lines AS jl ON jl.journal_line_id = jlf.journal_line_id
            INNER JOIN journal_entries AS je ON je.journal_entry_id = jl.journal_entry_id
            INNER JOIN accounts AS a ON a.account_id = jl.account_id
            WHERE jlf.fund_id IN ({string.Join(", ", parameterNames)})
              AND ($as_of_date IS NULL OR je.posting_date <= $as_of_date)
            ORDER BY jlf.fund_id, je.posting_date, je.entry_number, jl.line_number;
            """;
        command.Parameters.AddWithValue(
            "$as_of_date",
            asOfDate.HasValue ? FormatDate(asOfDate.Value) : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fundId = Guid.Parse(reader.GetString(0));
            var accountType = Enum.Parse<AccountType>(reader.GetString(1), ignoreCase: false);
            if (accountType is not (AccountType.Asset or AccountType.Liability))
            {
                continue;
            }

            balances[fundId] += ParseDecimal(reader.GetString(2)) - ParseDecimal(reader.GetString(3));
        }

        return balances;
    }

    public async Task SavePostedFundJournalAsync(PostedFundJournalEntry postedEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postedEntry);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var journal = postedEntry.Entry.Entry;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO journal_entries(
                    journal_entry_id, entry_number, period_id, posting_date, description,
                    reference, base_currency, total_debit, total_credit, posted_utc)
                VALUES (
                    $journal_entry_id, $entry_number, $period_id, $posting_date, $description,
                    $reference, $base_currency, $total_debit, $total_credit, $posted_utc);
                """;
            command.Parameters.AddWithValue("$journal_entry_id", journal.Id.ToString("D"));
            command.Parameters.AddWithValue("$entry_number", journal.EntryNumber);
            command.Parameters.AddWithValue("$period_id", journal.PeriodId.ToString("D"));
            command.Parameters.AddWithValue("$posting_date", FormatDate(journal.PostingDate));
            command.Parameters.AddWithValue("$description", journal.Description);
            command.Parameters.AddWithValue("$reference", journal.Reference);
            command.Parameters.AddWithValue("$base_currency", journal.BaseCurrency.Value);
            command.Parameters.AddWithValue("$total_debit", FormatDecimal(journal.TotalDebit));
            command.Parameters.AddWithValue("$total_credit", FormatDecimal(journal.TotalCredit));
            command.Parameters.AddWithValue("$posted_utc", postedEntry.PostedUtc.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var index = 0; index < journal.Lines.Count; index++)
        {
            var line = journal.Lines[index];
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO journal_lines(
                        journal_line_id, journal_entry_id, line_number, account_id, debit, credit, memo)
                    VALUES (
                        $journal_line_id, $journal_entry_id, $line_number, $account_id, $debit, $credit, $memo);
                    """;
                command.Parameters.AddWithValue("$journal_line_id", line.Id.ToString("D"));
                command.Parameters.AddWithValue("$journal_entry_id", journal.Id.ToString("D"));
                command.Parameters.AddWithValue("$line_number", index + 1);
                command.Parameters.AddWithValue("$account_id", line.AccountId.ToString("D"));
                command.Parameters.AddWithValue("$debit", FormatDecimal(line.Debit));
                command.Parameters.AddWithValue("$credit", FormatDecimal(line.Credit));
                command.Parameters.AddWithValue("$memo", line.Memo);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO journal_line_funds(journal_line_id, fund_id, assigned_utc)
                    VALUES ($journal_line_id, $fund_id, $assigned_utc);
                    """;
                command.Parameters.AddWithValue("$journal_line_id", line.Id.ToString("D"));
                command.Parameters.AddWithValue("$fund_id", postedEntry.Entry.GetFundId(line.Id).ToString("D"));
                command.Parameters.AddWithValue("$assigned_utc", postedEntry.PostedUtc.ToString("O", CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<FundActivityLine>> GetFundActivityAsync(
        Guid fundId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                je.journal_entry_id,
                jl.journal_line_id,
                je.entry_number,
                je.posting_date,
                jlf.fund_id,
                jl.account_id,
                a.account_type,
                jl.debit,
                jl.credit,
                je.description,
                je.reference,
                jl.memo
            FROM journal_line_funds AS jlf
            INNER JOIN journal_lines AS jl ON jl.journal_line_id = jlf.journal_line_id
            INNER JOIN journal_entries AS je ON je.journal_entry_id = jl.journal_entry_id
            INNER JOIN accounts AS a ON a.account_id = jl.account_id
            WHERE jlf.fund_id = $fund_id
              AND ($from_date IS NULL OR je.posting_date >= $from_date)
              AND ($to_date IS NULL OR je.posting_date <= $to_date)
            ORDER BY je.posting_date, je.entry_number, jl.line_number;
            """;
        command.Parameters.AddWithValue("$fund_id", fundId.ToString("D"));
        command.Parameters.AddWithValue("$from_date", fromDate.HasValue ? FormatDate(fromDate.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$to_date", toDate.HasValue ? FormatDate(toDate.Value) : DBNull.Value);

        var activity = new List<FundActivityLine>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            activity.Add(new FundActivityLine(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                ParseDate(reader.GetString(3)),
                Guid.Parse(reader.GetString(4)),
                Guid.Parse(reader.GetString(5)),
                Enum.Parse<AccountType>(reader.GetString(6), ignoreCase: false),
                ParseDecimal(reader.GetString(7)),
                ParseDecimal(reader.GetString(8)),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11)));
        }

        return activity;
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

    private static void BindFund(SqliteCommand command, Fund fund, bool includeCreatedUtc)
    {
        command.Parameters.AddWithValue("$fund_id", fund.Id.ToString("D"));
        command.Parameters.AddWithValue("$code", fund.Code);
        command.Parameters.AddWithValue("$name", fund.Name);
        command.Parameters.AddWithValue("$restriction_class", fund.Restriction.ToString());
        command.Parameters.AddWithValue("$overspend_policy", fund.OverspendPolicy.ToString());
        command.Parameters.AddWithValue("$purpose", fund.Purpose);
        command.Parameters.AddWithValue("$fund_status", fund.Status.ToString());
        object archivedValue = fund.ArchivedUtc.HasValue
            ? fund.ArchivedUtc.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value;
        command.Parameters.AddWithValue("$archived_utc", archivedValue);
        if (includeCreatedUtc)
        {
            command.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static string[] BindGuidList(SqliteCommand command, IReadOnlyList<Guid> ids, string prefix)
    {
        var parameterNames = new string[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            var parameterName = prefix + index.ToString(CultureInfo.InvariantCulture);
            parameterNames[index] = parameterName;
            command.Parameters.AddWithValue(parameterName, ids[index].ToString("D"));
        }

        return parameterNames;
    }

    private static Fund ReadFund(DbDataReader reader)
    {
        var archivedUtc = reader.IsDBNull(7)
            ? (DateTimeOffset?)null
            : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new Fund(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<FundRestriction>(reader.GetString(3), ignoreCase: false),
            Enum.Parse<FundOverspendPolicy>(reader.GetString(4), ignoreCase: false),
            reader.GetString(5),
            Enum.Parse<FundStatus>(reader.GetString(6), ignoreCase: false),
            archivedUtc);
    }

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string FormatDecimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
