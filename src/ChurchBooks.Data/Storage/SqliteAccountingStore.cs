using System.Data.Common;
using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.GeneralLedger;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteAccountingStore : IAccountingStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteAccountingStore(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task AddAccountAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounts(
                account_id, code, name, account_type, account_status,
                parent_account_id, allow_direct_posting, created_utc)
            VALUES (
                $account_id, $code, $name, $account_type, $account_status,
                $parent_account_id, $allow_direct_posting, $created_utc);
            """;
        command.Parameters.AddWithValue("$account_id", account.Id.ToString("D"));
        command.Parameters.AddWithValue("$code", account.Code);
        command.Parameters.AddWithValue("$name", account.Name);
        command.Parameters.AddWithValue("$account_type", account.Type.ToString());
        command.Parameters.AddWithValue("$account_status", account.Status.ToString());
        object parentAccountValue = account.ParentAccountId.HasValue
            ? account.ParentAccountId.Value.ToString("D")
            : DBNull.Value;
        command.Parameters.AddWithValue("$parent_account_id", parentAccountValue);
        command.Parameters.AddWithValue("$allow_direct_posting", account.AllowDirectPosting ? 1 : 0);
        command.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddPeriodAsync(AccountingPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounting_periods(
                period_id, name, start_date, end_date, period_status, created_utc)
            VALUES (
                $period_id, $name, $start_date, $end_date, $period_status, $created_utc);
            """;
        command.Parameters.AddWithValue("$period_id", period.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", period.Name);
        command.Parameters.AddWithValue("$start_date", FormatDate(period.StartDate));
        command.Parameters.AddWithValue("$end_date", FormatDate(period.EndDate));
        command.Parameters.AddWithValue("$period_status", period.Status.ToString());
        command.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AccountingPeriod?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT period_id, name, start_date, end_date, period_status
            FROM accounting_periods
            WHERE period_id = $period_id;
            """;
        command.Parameters.AddWithValue("$period_id", periodId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AccountingPeriod(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            ParseDate(reader.GetString(2)),
            ParseDate(reader.GetString(3)),
            Enum.Parse<AccountingPeriodStatus>(reader.GetString(4), ignoreCase: false));
    }


    public async Task<AccountingPeriod?> GetOpenPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT period_id, name, start_date, end_date, period_status
            FROM accounting_periods
            WHERE period_status='Open' AND start_date <= $date AND end_date >= $date
            ORDER BY start_date DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AccountingPeriod(Guid.Parse(reader.GetString(0)), reader.GetString(1), ParseDate(reader.GetString(2)), ParseDate(reader.GetString(3)), Enum.Parse<AccountingPeriodStatus>(reader.GetString(4), false))
            : null;
    }

    public async Task<Account?> GetAccountByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT account_id, code, name, account_type, account_status, parent_account_id, allow_direct_posting FROM accounts WHERE code=$code COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("$code", code.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    public async Task<IReadOnlyList<Account>> GetAccountsByTypeAsync(AccountType accountType, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT account_id, code, name, account_type, account_status, parent_account_id, allow_direct_posting FROM accounts WHERE account_type=$type ORDER BY code;";
        command.Parameters.AddWithValue("$type", accountType.ToString());
        var result = new List<Account>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadAccount(reader));
        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, Account>> GetAccountsAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        var ids = accountIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, Account>();
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var parameterNames = new string[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            var parameterName = "$id" + index.ToString(CultureInfo.InvariantCulture);
            parameterNames[index] = parameterName;
            command.Parameters.AddWithValue(parameterName, ids[index].ToString("D"));
        }

        command.CommandText = $"""
            SELECT account_id, code, name, account_type, account_status, parent_account_id, allow_direct_posting
            FROM accounts
            WHERE account_id IN ({string.Join(", ", parameterNames)});
            """;

        var results = new Dictionary<Guid, Account>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var account = ReadAccount(reader);
            results[account.Id] = account;
        }

        return results;
    }

    public async Task<bool> JournalExistsAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM journal_entries WHERE journal_entry_id = $journal_entry_id LIMIT 1;";
        command.Parameters.AddWithValue("$journal_entry_id", journalEntryId.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task SavePostedJournalAsync(PostedJournalEntry postedEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postedEntry);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

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
            command.Parameters.AddWithValue("$journal_entry_id", postedEntry.Entry.Id.ToString("D"));
            command.Parameters.AddWithValue("$entry_number", postedEntry.Entry.EntryNumber);
            command.Parameters.AddWithValue("$period_id", postedEntry.Entry.PeriodId.ToString("D"));
            command.Parameters.AddWithValue("$posting_date", FormatDate(postedEntry.Entry.PostingDate));
            command.Parameters.AddWithValue("$description", postedEntry.Entry.Description);
            command.Parameters.AddWithValue("$reference", postedEntry.Entry.Reference);
            command.Parameters.AddWithValue("$base_currency", postedEntry.Entry.BaseCurrency.Value);
            command.Parameters.AddWithValue("$total_debit", FormatDecimal(postedEntry.Entry.TotalDebit));
            command.Parameters.AddWithValue("$total_credit", FormatDecimal(postedEntry.Entry.TotalCredit));
            command.Parameters.AddWithValue("$posted_utc", postedEntry.PostedUtc.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var index = 0; index < postedEntry.Entry.Lines.Count; index++)
        {
            var line = postedEntry.Entry.Lines[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO journal_lines(
                    journal_line_id, journal_entry_id, line_number, account_id, debit, credit, memo)
                VALUES (
                    $journal_line_id, $journal_entry_id, $line_number, $account_id, $debit, $credit, $memo);
                """;
            command.Parameters.AddWithValue("$journal_line_id", line.Id.ToString("D"));
            command.Parameters.AddWithValue("$journal_entry_id", postedEntry.Entry.Id.ToString("D"));
            command.Parameters.AddWithValue("$line_number", index + 1);
            command.Parameters.AddWithValue("$account_id", line.AccountId.ToString("D"));
            command.Parameters.AddWithValue("$debit", FormatDecimal(line.Debit));
            command.Parameters.AddWithValue("$credit", FormatDecimal(line.Credit));
            command.Parameters.AddWithValue("$memo", line.Memo);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<LedgerLine>> GetLedgerAsync(
        Guid accountId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                je.journal_entry_id,
                je.entry_number,
                je.posting_date,
                jl.account_id,
                jl.debit,
                jl.credit,
                je.description,
                je.reference,
                jl.memo
            FROM journal_lines AS jl
            INNER JOIN journal_entries AS je ON je.journal_entry_id = jl.journal_entry_id
            WHERE jl.account_id = $account_id
              AND ($from_date IS NULL OR je.posting_date >= $from_date)
              AND ($to_date IS NULL OR je.posting_date <= $to_date)
            ORDER BY je.posting_date, je.entry_number, jl.line_number;
            """;
        command.Parameters.AddWithValue("$account_id", accountId.ToString("D"));
        command.Parameters.AddWithValue(
            "$from_date",
            fromDate.HasValue ? FormatDate(fromDate.Value) : DBNull.Value);
        command.Parameters.AddWithValue(
            "$to_date",
            toDate.HasValue ? FormatDate(toDate.Value) : DBNull.Value);

        var lines = new List<LedgerLine>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new LedgerLine(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                ParseDate(reader.GetString(2)),
                Guid.Parse(reader.GetString(3)),
                ParseDecimal(reader.GetString(4)),
                ParseDecimal(reader.GetString(5)),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8)));
        }

        return lines;
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

    private static Account ReadAccount(DbDataReader reader)
    {
        Guid? parentAccountId = reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5));
        return new Account(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<AccountType>(reader.GetString(3), ignoreCase: false),
            Enum.Parse<AccountStatus>(reader.GetString(4), ignoreCase: false),
            parentAccountId,
            reader.GetInt32(6) == 1);
    }

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string FormatDecimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
