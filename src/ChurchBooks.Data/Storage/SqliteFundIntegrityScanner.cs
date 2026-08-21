using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Integrity;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteFundIntegrityScanner : IFundIntegrityScanner
{
    private readonly ChurchBooksDatabase _database;
    private readonly IFundAccountingStore _fundStore;

    public SqliteFundIntegrityScanner(ChurchBooksDatabase database, IFundAccountingStore fundStore)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fundStore = fundStore ?? throw new ArgumentNullException(nameof(fundStore));
    }

    public async Task<FundIntegrityReport> ScanAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<FundIntegrityFinding>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnableForeignKeysAsync(connection, cancellationToken);

        await ScanForeignKeysAsync(connection, findings, cancellationToken);
        await ScanFundAwareAssignmentsAsync(connection, findings, cancellationToken);
        await ScanPerFundJournalBalanceAsync(connection, findings, cancellationToken);
        await ScanFundMasterWarningsAsync(findings, cancellationToken);

        return new FundIntegrityReport(DateTimeOffset.UtcNow, findings);
    }

    private static async Task EnableForeignKeysAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ScanForeignKeysAsync(
        SqliteConnection connection,
        ICollection<FundIntegrityFinding> findings,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            var rowId = reader.IsDBNull(1) ? "unknown" : reader.GetValue(1).ToString() ?? "unknown";
            findings.Add(new FundIntegrityFinding(
                "FK_VIOLATION",
                FundIntegritySeverity.Critical,
                $"Foreign-key integrity failed in table '{table}' at row {rowId}."));
        }
    }

    private static async Task ScanFundAwareAssignmentsAsync(
        SqliteConnection connection,
        ICollection<FundIntegrityFinding> findings,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT je.journal_entry_id, je.entry_number,
                   COUNT(jl.journal_line_id) AS line_count,
                   COUNT(jlf.journal_line_id) AS assigned_count
            FROM journal_entries AS je
            INNER JOIN journal_lines AS jl ON jl.journal_entry_id = je.journal_entry_id
            LEFT JOIN journal_line_funds AS jlf ON jlf.journal_line_id = jl.journal_line_id
            WHERE EXISTS (
                SELECT 1
                FROM journal_lines AS fund_line
                INNER JOIN journal_line_funds AS fund_assignment
                    ON fund_assignment.journal_line_id = fund_line.journal_line_id
                WHERE fund_line.journal_entry_id = je.journal_entry_id
            )
            GROUP BY je.journal_entry_id, je.entry_number
            HAVING COUNT(jl.journal_line_id) <> COUNT(jlf.journal_line_id);
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var journalEntryId = Guid.Parse(reader.GetString(0));
            var entryNumber = reader.GetString(1);
            var lineCount = reader.GetInt32(2);
            var assignedCount = reader.GetInt32(3);
            findings.Add(new FundIntegrityFinding(
                "FUND_ASSIGNMENT_GAP",
                FundIntegritySeverity.Critical,
                $"Fund-aware journal '{entryNumber}' has {assignedCount} fund assignments for {lineCount} journal lines.",
                JournalEntryId: journalEntryId));
        }
    }

    private static async Task ScanPerFundJournalBalanceAsync(
        SqliteConnection connection,
        ICollection<FundIntegrityFinding> findings,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT je.journal_entry_id, je.entry_number, jlf.fund_id, jl.debit, jl.credit
            FROM journal_entries AS je
            INNER JOIN journal_lines AS jl ON jl.journal_entry_id = je.journal_entry_id
            INNER JOIN journal_line_funds AS jlf ON jlf.journal_line_id = jl.journal_line_id
            ORDER BY je.journal_entry_id, jlf.fund_id, jl.line_number;
            """;

        var totals = new Dictionary<(Guid JournalId, Guid FundId, string EntryNumber), decimal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = (
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(2)),
                reader.GetString(1));
            var debit = decimal.Parse(reader.GetString(3), NumberStyles.Number, CultureInfo.InvariantCulture);
            var credit = decimal.Parse(reader.GetString(4), NumberStyles.Number, CultureInfo.InvariantCulture);
            totals[key] = totals.TryGetValue(key, out var current) ? current + debit - credit : debit - credit;
        }

        foreach (var item in totals.Where(static item => item.Value != 0m))
        {
            findings.Add(new FundIntegrityFinding(
                "FUND_JOURNAL_IMBALANCE",
                FundIntegritySeverity.Critical,
                $"Journal '{item.Key.EntryNumber}' is out of balance within fund {item.Key.FundId:D} by {item.Value:N2}.",
                item.Key.FundId,
                item.Key.JournalId));
        }
    }

    private async Task ScanFundMasterWarningsAsync(
        ICollection<FundIntegrityFinding> findings,
        CancellationToken cancellationToken)
    {
        var funds = await _fundStore.GetAllFundsAsync(includeArchived: true, cancellationToken);
        var balances = await _fundStore.GetFundBalancesAsync(funds.Select(static fund => fund.Id), cancellationToken);

        foreach (var fund in funds)
        {
            var balance = balances.TryGetValue(fund.Id, out var current) ? current : 0m;
            if (fund.Status == FundStatus.Archived && balance != 0m)
            {
                findings.Add(new FundIntegrityFinding(
                    "ARCHIVED_NONZERO_BALANCE",
                    FundIntegritySeverity.Warning,
                    $"Archived fund '{fund.Name}' still has a balance of {balance:N2}. Historical activity is preserved, but the balance should be reviewed.",
                    fund.Id));
            }

            if (fund.Restriction is FundRestriction.DonorRestricted or FundRestriction.Endowment)
            {
                if (string.IsNullOrWhiteSpace(fund.Purpose))
                {
                    findings.Add(new FundIntegrityFinding(
                        "RESTRICTED_PURPOSE_MISSING",
                        FundIntegritySeverity.Warning,
                        $"Restricted fund '{fund.Name}' has no documented purpose.",
                        fund.Id));
                }

                if (balance < 0m)
                {
                    findings.Add(new FundIntegrityFinding(
                        "RESTRICTED_NEGATIVE_BALANCE",
                        fund.OverspendPolicy == FundOverspendPolicy.Block
                            ? FundIntegritySeverity.Critical
                            : FundIntegritySeverity.Warning,
                        $"Restricted fund '{fund.Name}' has a negative balance of {balance:N2}.",
                        fund.Id));
                }
            }
        }
    }
}
