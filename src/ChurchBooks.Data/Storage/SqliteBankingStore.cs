using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Core.Finance;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteBankingStore : IBankingStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteBankingStore(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task AddBankAccountAsync(
        BankAccount bankAccount,
        Account ledgerAccount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        ArgumentNullException.ThrowIfNull(ledgerAccount);

        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var accountCommand = connection.CreateCommand())
        {
            accountCommand.Transaction = transaction;
            accountCommand.CommandText = """
                INSERT INTO accounts(
                    account_id, code, name, account_type, account_status,
                    parent_account_id, allow_direct_posting, created_utc)
                VALUES($id, $code, $name, $type, $status, NULL, 1, $created);
                """;
            accountCommand.Parameters.AddWithValue("$id", ledgerAccount.Id.ToString("D"));
            accountCommand.Parameters.AddWithValue("$code", ledgerAccount.Code);
            accountCommand.Parameters.AddWithValue("$name", ledgerAccount.Name);
            accountCommand.Parameters.AddWithValue("$type", ledgerAccount.Type.ToString());
            accountCommand.Parameters.AddWithValue("$status", ledgerAccount.Status.ToString());
            accountCommand.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await accountCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var bankCommand = connection.CreateCommand())
        {
            bankCommand.Transaction = transaction;
            bankCommand.CommandText = """
                INSERT INTO bank_accounts(
                    bank_account_id, name, institution_name, account_last_four, currency_code,
                    ledger_account_id, bank_status, created_utc, archived_utc)
                VALUES($id, $name, $institution, $last_four, $currency, $ledger, $status, $created, NULL);
                """;
            BindBankInsert(bankCommand, bankAccount);
            await bankCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task UpdateBankAccountAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bank_accounts
            SET name = $name,
                institution_name = $institution,
                account_last_four = $last_four,
                bank_status = $status,
                archived_utc = $archived
            WHERE bank_account_id = $id;
            """;
        command.Parameters.AddWithValue("$id", bankAccount.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", bankAccount.Name);
        command.Parameters.AddWithValue("$institution", bankAccount.InstitutionName);
        command.Parameters.AddWithValue("$last_four", bankAccount.AccountLastFour);
        command.Parameters.AddWithValue("$status", bankAccount.Status.ToString());
        command.Parameters.AddWithValue(
            "$archived",
            bankAccount.ArchivedUtc.HasValue
                ? bankAccount.ArchivedUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Bank account does not exist.");
    }

    public async Task<BankAccount?> GetBankAccountAsync(Guid bankAccountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BankSelect + " WHERE bank_account_id = $id;";
        command.Parameters.AddWithValue("$id", bankAccountId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBank(reader) : null;
    }

    public async Task<IReadOnlyList<BankAccount>> GetBankAccountsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BankSelect + " WHERE $include_archived = 1 OR bank_status = 'Active' ORDER BY name;";
        command.Parameters.AddWithValue("$include_archived", includeArchived ? 1 : 0);

        var items = new List<BankAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadBank(reader));
        return items;
    }

    public async Task SetGivingCategoryIncomeMappingAsync(
        GivingCategoryIncomeMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO giving_category_income_accounts(giving_category_id, income_account_id, updated_utc)
            VALUES($category, $account, $updated)
            ON CONFLICT(giving_category_id) DO UPDATE SET
                income_account_id = excluded.income_account_id,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$category", mapping.GivingCategoryId.ToString("D"));
        command.Parameters.AddWithValue("$account", mapping.IncomeAccountId.ToString("D"));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetGivingCategoryIncomeMappingsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT giving_category_id, income_account_id FROM giving_category_income_accounts;";

        var mappings = new Dictionary<Guid, Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            mappings[Guid.Parse(reader.GetString(0))] = Guid.Parse(reader.GetString(1));
        return mappings;
    }

    public async Task SaveDepositAsync(BankDeposit deposit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var depositCommand = connection.CreateCommand())
        {
            depositCommand.Transaction = transaction;
            depositCommand.CommandText = """
                INSERT INTO bank_deposits(
                    deposit_id, bank_account_id, deposit_date, currency_code, total_amount,
                    reference, memo, journal_entry_id, deposit_status, created_utc, posted_utc)
                VALUES($id, $bank, $date, $currency, $total, $reference, $memo, $journal, $status, $created, NULL);
                """;
            BindDeposit(depositCommand, deposit);
            await depositCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var allocation in deposit.Contributions)
        {
            await using var allocationCommand = connection.CreateCommand();
            allocationCommand.Transaction = transaction;
            allocationCommand.CommandText = """
                INSERT INTO bank_deposit_contributions(deposit_id, contribution_id, amount)
                VALUES($deposit, $contribution, $amount);
                """;
            allocationCommand.Parameters.AddWithValue("$deposit", deposit.Id.ToString("D"));
            allocationCommand.Parameters.AddWithValue("$contribution", allocation.ContributionId.ToString("D"));
            allocationCommand.Parameters.AddWithValue("$amount", FormatDecimal(allocation.Amount));
            await allocationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<BankDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        DepositHeader? header;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = DepositSelect + " WHERE deposit_id = $id;";
            command.Parameters.AddWithValue("$id", depositId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            header = await reader.ReadAsync(cancellationToken) ? ReadDepositHeader(reader) : null;
        }

        return header is null ? null : await LoadDepositAsync(connection, header, cancellationToken);
    }

    public async Task<IReadOnlyList<BankDeposit>> GetDepositsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var headers = new List<DepositHeader>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = DepositSelect + " ORDER BY deposit_date DESC, created_utc DESC;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) headers.Add(ReadDepositHeader(reader));
        }

        var deposits = new List<BankDeposit>(headers.Count);
        foreach (var header in headers) deposits.Add(await LoadDepositAsync(connection, header, cancellationToken));
        return deposits;
    }

    public async Task<bool> IsContributionAlreadyDepositedAsync(
        Guid contributionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM bank_deposit_contributions WHERE contribution_id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", contributionId.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task MarkDepositPostedAsync(
        Guid depositId,
        Guid journalEntryId,
        DateTimeOffset postedUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bank_deposits
            SET deposit_status = 'Posted', posted_utc = $posted
            WHERE deposit_id = $id AND journal_entry_id = $journal;
            """;
        command.Parameters.AddWithValue("$posted", postedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", depositId.ToString("D"));
        command.Parameters.AddWithValue("$journal", journalEntryId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Deposit could not be marked posted.");
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static void BindBankInsert(SqliteCommand command, BankAccount bank)
    {
        command.Parameters.AddWithValue("$id", bank.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", bank.Name);
        command.Parameters.AddWithValue("$institution", bank.InstitutionName);
        command.Parameters.AddWithValue("$last_four", bank.AccountLastFour);
        command.Parameters.AddWithValue("$currency", bank.Currency.Value);
        command.Parameters.AddWithValue("$ledger", bank.LedgerAccountId.ToString("D"));
        command.Parameters.AddWithValue("$status", bank.Status.ToString());
        command.Parameters.AddWithValue("$created", bank.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static BankAccount ReadBank(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        new CurrencyCode(reader.GetString(4)),
        Guid.Parse(reader.GetString(5)),
        Enum.Parse<BankAccountStatus>(reader.GetString(6), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(8)
            ? null
            : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static void BindDeposit(SqliteCommand command, BankDeposit deposit)
    {
        command.Parameters.AddWithValue("$id", deposit.Id.ToString("D"));
        command.Parameters.AddWithValue("$bank", deposit.BankAccountId.ToString("D"));
        command.Parameters.AddWithValue("$date", deposit.DepositDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$currency", deposit.Currency.Value);
        command.Parameters.AddWithValue("$total", FormatDecimal(deposit.TotalAmount));
        command.Parameters.AddWithValue("$reference", deposit.Reference);
        command.Parameters.AddWithValue("$memo", deposit.Memo);
        command.Parameters.AddWithValue("$journal", deposit.JournalEntryId.ToString("D"));
        command.Parameters.AddWithValue("$status", deposit.Status.ToString());
        command.Parameters.AddWithValue("$created", deposit.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static DepositHeader ReadDepositHeader(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        new CurrencyCode(reader.GetString(3)),
        reader.GetString(5),
        reader.GetString(6),
        Guid.Parse(reader.GetString(7)),
        Enum.Parse<BankDepositStatus>(reader.GetString(8), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(10)
            ? null
            : DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static async Task<BankDeposit> LoadDepositAsync(
        SqliteConnection connection,
        DepositHeader header,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT contribution_id, amount
            FROM bank_deposit_contributions
            WHERE deposit_id = $id
            ORDER BY contribution_id;
            """;
        command.Parameters.AddWithValue("$id", header.Id.ToString("D"));

        var allocations = new List<DepositContributionAllocation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            allocations.Add(new DepositContributionAllocation(
                Guid.Parse(reader.GetString(0)),
                decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture)));
        }

        return new BankDeposit(
            header.Id,
            header.BankAccountId,
            header.Date,
            header.Currency,
            allocations,
            header.Reference,
            header.Memo,
            header.JournalEntryId,
            header.Status,
            header.CreatedUtc,
            header.PostedUtc);
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private const string BankSelect =
        "SELECT bank_account_id, name, institution_name, account_last_four, currency_code, ledger_account_id, bank_status, created_utc, archived_utc FROM bank_accounts";

    private const string DepositSelect =
        "SELECT deposit_id, bank_account_id, deposit_date, currency_code, total_amount, reference, memo, journal_entry_id, deposit_status, created_utc, posted_utc FROM bank_deposits";

    private sealed record DepositHeader(
        Guid Id,
        Guid BankAccountId,
        DateOnly Date,
        CurrencyCode Currency,
        string Reference,
        string Memo,
        Guid JournalEntryId,
        BankDepositStatus Status,
        DateTimeOffset CreatedUtc,
        DateTimeOffset? PostedUtc);
}
