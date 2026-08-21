using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Reconciliation;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteBankReconciliationStore : IBankReconciliationStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteBankReconciliationStore(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task SaveStatementImportAsync(
        Guid bankAccountId,
        Guid importSessionId,
        IReadOnlyList<BankStatementLine> statementLines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statementLines);
        if (statementLines.Count == 0) throw new ArgumentException("At least one statement line is required.", nameof(statementLines));
        if (statementLines.Any(line => line.BankAccountId != bankAccountId || line.ImportSessionId != importSessionId))
            throw new ArgumentException("Every statement line must belong to the selected bank and import session.", nameof(statementLines));

        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var link = connection.CreateCommand())
        {
            link.Transaction = transaction;
            link.CommandText = """
                INSERT INTO bank_statement_import_links(import_session_id, bank_account_id, linked_utc)
                VALUES($session, $bank, strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            link.Parameters.AddWithValue("$session", importSessionId.ToString("D"));
            link.Parameters.AddWithValue("$bank", bankAccountId.ToString("D"));
            await link.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var line in statementLines)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO bank_statement_lines(
                    statement_line_id, bank_account_id, import_session_id, transaction_date,
                    signed_amount, description, reference, fingerprint, source_row_number, created_utc)
                VALUES(
                    $id, $bank, $session, $date,
                    $amount, $description, $reference, $fingerprint, $row, $created);
                """;
            command.Parameters.AddWithValue("$id", line.Id.ToString("D"));
            command.Parameters.AddWithValue("$bank", line.BankAccountId.ToString("D"));
            command.Parameters.AddWithValue("$session", line.ImportSessionId.ToString("D"));
            command.Parameters.AddWithValue("$date", FormatDate(line.TransactionDate));
            command.Parameters.AddWithValue("$amount", FormatDecimal(line.Amount));
            command.Parameters.AddWithValue("$description", line.Description);
            command.Parameters.AddWithValue("$reference", line.Reference);
            command.Parameters.AddWithValue("$fingerprint", line.Fingerprint);
            command.Parameters.AddWithValue("$row", line.SourceRowNumber);
            command.Parameters.AddWithValue("$created", line.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<bool> IsImportSessionLinkedAsync(
        Guid importSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM bank_statement_import_links WHERE import_session_id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", importSessionId.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task<IReadOnlyList<BankStatementLine>> GetStatementLinesAsync(
        Guid bankAccountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate) throw new ArgumentException("Statement end date cannot precede start date.");

        var result = new List<BankStatementLine>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT statement_line_id, bank_account_id, import_session_id, transaction_date,
                   signed_amount, description, reference, fingerprint, source_row_number, created_utc
            FROM bank_statement_lines
            WHERE bank_account_id = $bank
              AND transaction_date >= $from
              AND transaction_date <= $to
            ORDER BY transaction_date, source_row_number, statement_line_id;
            """;
        command.Parameters.AddWithValue("$bank", bankAccountId.ToString("D"));
        command.Parameters.AddWithValue("$from", FormatDate(fromDate));
        command.Parameters.AddWithValue("$to", FormatDate(toDate));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadStatementLine(reader));
        }
        return result;
    }

    public async Task SaveReconciliationAsync(
        BankReconciliation reconciliation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reconciliation);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO bank_reconciliations(
                reconciliation_id, bank_account_id, statement_start_date, statement_end_date,
                statement_ending_balance, reconciliation_status, created_utc, completed_utc)
            VALUES(
                $id, $bank, $start, $end,
                $balance, $status, $created, $completed);
            """;
        BindReconciliation(command, reconciliation);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BankReconciliation?> GetReconciliationAsync(
        Guid reconciliationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ReconciliationSelect + " WHERE reconciliation_id = $id;";
        command.Parameters.AddWithValue("$id", reconciliationId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadReconciliation(reader) : null;
    }

    public async Task<IReadOnlyList<BankReconciliation>> GetReconciliationsAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken = default)
    {
        var result = new List<BankReconciliation>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ReconciliationSelect + """
             WHERE bank_account_id = $bank
             ORDER BY statement_end_date DESC, created_utc DESC;
            """;
        command.Parameters.AddWithValue("$bank", bankAccountId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadReconciliation(reader));
        }
        return result;
    }

    public async Task<IReadOnlyList<ReconciliationMatchGroup>> GetMatchGroupsAsync(
        Guid reconciliationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var headers = new List<(Guid Id, DateTimeOffset CreatedUtc)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT match_group_id, created_utc
                FROM bank_reconciliation_match_groups
                WHERE reconciliation_id = $reconciliation
                ORDER BY created_utc, match_group_id;
                """;
            command.Parameters.AddWithValue("$reconciliation", reconciliationId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                headers.Add((
                    Guid.Parse(reader.GetString(0)),
                    DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
            }
        }

        var groups = new List<ReconciliationMatchGroup>(headers.Count);
        foreach (var header in headers)
        {
            var statementIds = await LoadIdsAsync(
                connection,
                "SELECT statement_line_id FROM bank_reconciliation_match_statement_lines WHERE match_group_id = $group ORDER BY statement_line_id;",
                header.Id,
                cancellationToken);
            var journalIds = await LoadIdsAsync(
                connection,
                "SELECT journal_entry_id FROM bank_reconciliation_match_journals WHERE match_group_id = $group ORDER BY journal_entry_id;",
                header.Id,
                cancellationToken);
            groups.Add(new ReconciliationMatchGroup(
                header.Id,
                reconciliationId,
                statementIds,
                journalIds,
                header.CreatedUtc));
        }

        return groups;
    }

    public async Task SaveMatchGroupAsync(
        ReconciliationMatchGroup group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await EnsureDraftAsync(connection, transaction, group.ReconciliationId, cancellationToken);
        var bankAccountId = await ValidateMatchOwnershipAndBalanceAsync(connection, transaction, group, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO bank_reconciliation_match_groups(match_group_id, reconciliation_id, created_utc)
                VALUES($id, $reconciliation, $created);
                """;
            command.Parameters.AddWithValue("$id", group.Id.ToString("D"));
            command.Parameters.AddWithValue("$reconciliation", group.ReconciliationId.ToString("D"));
            command.Parameters.AddWithValue("$created", group.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var statementLineId in group.StatementLineIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO bank_reconciliation_match_statement_lines(match_group_id, statement_line_id)
                VALUES($group, $statement);
                """;
            command.Parameters.AddWithValue("$group", group.Id.ToString("D"));
            command.Parameters.AddWithValue("$statement", statementLineId.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var journalEntryId in group.JournalEntryIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO bank_reconciliation_match_journals(match_group_id, bank_account_id, journal_entry_id)
                VALUES($group, $bank, $journal);
                """;
            command.Parameters.AddWithValue("$group", group.Id.ToString("D"));
            command.Parameters.AddWithValue("$bank", bankAccountId.ToString("D"));
            command.Parameters.AddWithValue("$journal", journalEntryId.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task DeleteMatchGroupAsync(
        Guid matchGroupId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        Guid reconciliationId;
        await using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT reconciliation_id FROM bank_reconciliation_match_groups WHERE match_group_id = $id;";
            lookup.Parameters.AddWithValue("$id", matchGroupId.ToString("D"));
            var value = await lookup.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("The match group does not exist.");
            reconciliationId = Guid.Parse((string)value);
        }

        await EnsureDraftAsync(connection, transaction, reconciliationId, cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM bank_reconciliation_match_groups WHERE match_group_id = $id;";
        command.Parameters.AddWithValue("$id", matchGroupId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("The match group could not be removed.");

        transaction.Commit();
    }

    public async Task<IReadOnlySet<Guid>> GetUnavailableJournalIdsAsync(
        Guid bankAccountId,
        Guid? excludingReconciliationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new HashSet<Guid>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT journals.journal_entry_id
            FROM bank_reconciliation_match_journals journals
            INNER JOIN bank_reconciliation_match_groups groups
                ON groups.match_group_id = journals.match_group_id
            INNER JOIN bank_reconciliations reconciliations
                ON reconciliations.reconciliation_id = groups.reconciliation_id
            WHERE reconciliations.bank_account_id = $bank
              AND ($exclude = '' OR reconciliations.reconciliation_id <> $exclude);
            """;
        command.Parameters.AddWithValue("$bank", bankAccountId.ToString("D"));
        command.Parameters.AddWithValue("$exclude", excludingReconciliationId?.ToString("D") ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Guid.Parse(reader.GetString(0)));
        }
        return result;
    }

    public async Task<IReadOnlySet<Guid>> GetUnavailableStatementLineIdsAsync(
        Guid bankAccountId,
        Guid? excludingReconciliationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new HashSet<Guid>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT statement.statement_line_id
            FROM bank_reconciliation_match_statement_lines statement
            INNER JOIN bank_reconciliation_match_groups groups
                ON groups.match_group_id = statement.match_group_id
            INNER JOIN bank_reconciliations reconciliations
                ON reconciliations.reconciliation_id = groups.reconciliation_id
            WHERE reconciliations.bank_account_id = $bank
              AND ($exclude = '' OR reconciliations.reconciliation_id <> $exclude);
            """;
        command.Parameters.AddWithValue("$bank", bankAccountId.ToString("D"));
        command.Parameters.AddWithValue("$exclude", excludingReconciliationId?.ToString("D") ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Guid.Parse(reader.GetString(0)));
        }
        return result;
    }

    public async Task MarkCompletedAsync(
        Guid reconciliationId,
        DateTimeOffset completedUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bank_reconciliations
            SET reconciliation_status = 'Completed', completed_utc = $completed
            WHERE reconciliation_id = $id AND reconciliation_status = 'Draft';
            """;
        command.Parameters.AddWithValue("$completed", completedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", reconciliationId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Only a draft reconciliation can be completed.");
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<Guid> ValidateMatchOwnershipAndBalanceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ReconciliationMatchGroup group,
        CancellationToken cancellationToken)
    {
        Guid bankAccountId;
        Guid bankLedgerAccountId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT reconciliations.bank_account_id, banks.ledger_account_id
                FROM bank_reconciliations reconciliations
                INNER JOIN bank_accounts banks
                    ON banks.bank_account_id = reconciliations.bank_account_id
                WHERE reconciliations.reconciliation_id = $reconciliation;
                """;
            command.Parameters.AddWithValue("$reconciliation", group.ReconciliationId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("The reconciliation bank account does not exist.");
            bankAccountId = Guid.Parse(reader.GetString(0));
            bankLedgerAccountId = Guid.Parse(reader.GetString(1));
        }

        decimal statementTotal = 0m;
        foreach (var statementLineId in group.StatementLineIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT bank_account_id, signed_amount
                FROM bank_statement_lines
                WHERE statement_line_id = $id;
                """;
            command.Parameters.AddWithValue("$id", statementLineId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("A selected statement line does not exist.");
            if (Guid.Parse(reader.GetString(0)) != bankAccountId)
                throw new InvalidOperationException("A selected statement line belongs to another bank account.");
            statementTotal += decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
        }

        decimal journalTotal = 0m;
        foreach (var journalEntryId in group.JournalEntryIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT debit, credit
                FROM journal_lines
                WHERE journal_entry_id = $journal
                  AND account_id = $account;
                """;
            command.Parameters.AddWithValue("$journal", journalEntryId.ToString("D"));
            command.Parameters.AddWithValue("$account", bankLedgerAccountId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var found = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                found = true;
                journalTotal +=
                    decimal.Parse(reader.GetString(0), CultureInfo.InvariantCulture) -
                    decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
            }
            if (!found)
                throw new InvalidOperationException("A selected journal does not contain activity for this bank account.");
        }

        if (statementTotal != journalTotal)
            throw new InvalidOperationException("Stored reconciliation matches must have exactly equal signed totals.");

        return bankAccountId;
    }

    private static async Task EnsureDraftAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid reconciliationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT reconciliation_status FROM bank_reconciliations WHERE reconciliation_id = $id;";
        command.Parameters.AddWithValue("$id", reconciliationId.ToString("D"));
        var status = await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new InvalidOperationException("The reconciliation does not exist.");
        if (!string.Equals(status, "Draft", StringComparison.Ordinal))
            throw new InvalidOperationException("Completed reconciliations are locked.");
    }

    private static async Task<IReadOnlyList<Guid>> LoadIdsAsync(
        SqliteConnection connection,
        string sql,
        Guid matchGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<Guid>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$group", matchGroupId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Guid.Parse(reader.GetString(0)));
        }
        return result;
    }

    private static BankStatementLine ReadStatementLine(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        Guid.Parse(reader.GetString(2)),
        DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.GetInt32(8),
        DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static BankReconciliation ReadReconciliation(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
        Enum.Parse<BankReconciliationStatus>(reader.GetString(5), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(7)
            ? null
            : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static void BindReconciliation(SqliteCommand command, BankReconciliation reconciliation)
    {
        command.Parameters.AddWithValue("$id", reconciliation.Id.ToString("D"));
        command.Parameters.AddWithValue("$bank", reconciliation.BankAccountId.ToString("D"));
        command.Parameters.AddWithValue("$start", FormatDate(reconciliation.StatementStartDate));
        command.Parameters.AddWithValue("$end", FormatDate(reconciliation.StatementEndDate));
        command.Parameters.AddWithValue("$balance", FormatDecimal(reconciliation.StatementEndingBalance));
        command.Parameters.AddWithValue("$status", reconciliation.Status.ToString());
        command.Parameters.AddWithValue("$created", reconciliation.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "$completed",
            reconciliation.CompletedUtc.HasValue
                ? reconciliation.CompletedUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);
    }

    private static string FormatDate(DateOnly value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private const string ReconciliationSelect = """
        SELECT reconciliation_id, bank_account_id, statement_start_date, statement_end_date,
               statement_ending_balance, reconciliation_status, created_utc, completed_utc
        FROM bank_reconciliations
        """;
}
