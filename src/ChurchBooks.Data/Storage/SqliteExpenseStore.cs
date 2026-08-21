using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Expenses;
using ChurchBooks.Core.Finance;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteExpenseStore : IExpenseStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteExpenseStore(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task AddVendorAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO vendors(vendor_id,vendor_code,vendor_name,tax_id,email,phone,vendor_status,created_utc,archived_utc)
            VALUES($id,$code,$name,$tax,$email,$phone,$status,$created,$archived);
            """;
        BindVendor(command, vendor);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Vendor code must be unique.", ex);
        }
    }

    public async Task UpdateVendorAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE vendors SET vendor_code=$code,vendor_name=$name,tax_id=$tax,email=$email,phone=$phone,
                vendor_status=$status,archived_utc=$archived
            WHERE vendor_id=$id;
            """;
        BindVendor(command, vendor);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Vendor could not be updated.");
    }

    public async Task<Vendor?> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT vendor_id,vendor_code,vendor_name,tax_id,email,phone,vendor_status,created_utc,archived_utc
            FROM vendors WHERE vendor_id=$id;
            """;
        command.Parameters.AddWithValue("$id", vendorId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadVendor(reader) : null;
    }

    public async Task<IReadOnlyList<Vendor>> GetVendorsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT vendor_id,vendor_code,vendor_name,tax_id,email,phone,vendor_status,created_utc,archived_utc
            FROM vendors
            WHERE $include_archived=1 OR vendor_status='Active'
            ORDER BY vendor_name COLLATE NOCASE, vendor_code COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$include_archived", includeArchived ? 1 : 0);
        var result = new List<Vendor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadVendor(reader));
        return result;
    }

    public async Task SaveDirectExpenseAsync(DirectExpense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        if (expense.Status != DirectExpenseStatus.Draft)
            throw new InvalidOperationException("Only draft direct expenses can be inserted.");

        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO direct_expenses(
                    expense_id,vendor_id,bank_account_id,expense_date,currency,reference,memo,journal_entry_id,
                    expense_status,total_amount,created_utc,posted_utc)
                VALUES($id,$vendor,$bank,$date,$currency,$reference,$memo,$journal,$status,$total,$created,NULL);
                """;
            command.Parameters.AddWithValue("$id", expense.Id.ToString("D"));
            command.Parameters.AddWithValue("$vendor", expense.VendorId.HasValue ? (object)expense.VendorId.Value.ToString("D") : DBNull.Value);
            command.Parameters.AddWithValue("$bank", expense.BankAccountId.ToString("D"));
            command.Parameters.AddWithValue("$date", expense.ExpenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$currency", expense.Currency.Value);
            command.Parameters.AddWithValue("$reference", expense.Reference);
            command.Parameters.AddWithValue("$memo", expense.Memo);
            command.Parameters.AddWithValue("$journal", expense.JournalEntryId.ToString("D"));
            command.Parameters.AddWithValue("$status", expense.Status.ToString());
            command.Parameters.AddWithValue("$total", FormatDecimal(expense.TotalAmount));
            command.Parameters.AddWithValue("$created", expense.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var lineNumber = 0;
        foreach (var line in expense.Lines)
        {
            lineNumber++;
            await using var lineCommand = connection.CreateCommand();
            lineCommand.Transaction = transaction;
            lineCommand.CommandText = """
                INSERT INTO direct_expense_lines(expense_line_id,expense_id,line_number,expense_account_id,fund_id,description,amount)
                VALUES($id,$expense,$line,$account,$fund,$description,$amount);
                """;
            lineCommand.Parameters.AddWithValue("$id", line.Id.ToString("D"));
            lineCommand.Parameters.AddWithValue("$expense", expense.Id.ToString("D"));
            lineCommand.Parameters.AddWithValue("$line", lineNumber);
            lineCommand.Parameters.AddWithValue("$account", line.ExpenseAccountId.ToString("D"));
            lineCommand.Parameters.AddWithValue("$fund", line.FundId.ToString("D"));
            lineCommand.Parameters.AddWithValue("$description", line.Description);
            lineCommand.Parameters.AddWithValue("$amount", FormatDecimal(line.Amount));
            await lineCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<DirectExpense?> GetDirectExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var header = await GetExpenseHeaderAsync(connection, expenseId, cancellationToken);
        return header is null ? null : await LoadExpenseAsync(connection, header.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<DirectExpense>> GetDirectExpensesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ExpenseHeaderSelect + " ORDER BY expense_date DESC, created_utc DESC;";
        var headers = new List<ExpenseHeader>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) headers.Add(ReadHeader(reader));
        }

        var result = new List<DirectExpense>(headers.Count);
        foreach (var header in headers) result.Add(await LoadExpenseAsync(connection, header, cancellationToken));
        return result;
    }

    public async Task MarkDirectExpensePostedAsync(
        Guid expenseId,
        Guid journalEntryId,
        DateTimeOffset postedUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE direct_expenses
            SET expense_status='Posted', posted_utc=$posted
            WHERE expense_id=$id AND journal_entry_id=$journal AND expense_status='Draft';
            """;
        command.Parameters.AddWithValue("$posted", postedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", expenseId.ToString("D"));
        command.Parameters.AddWithValue("$journal", journalEntryId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Direct expense could not be marked posted.");
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

    private static void BindVendor(SqliteCommand command, Vendor vendor)
    {
        command.Parameters.AddWithValue("$id", vendor.Id.ToString("D"));
        command.Parameters.AddWithValue("$code", vendor.Code);
        command.Parameters.AddWithValue("$name", vendor.Name);
        command.Parameters.AddWithValue("$tax", vendor.TaxId);
        command.Parameters.AddWithValue("$email", vendor.Email);
        command.Parameters.AddWithValue("$phone", vendor.Phone);
        command.Parameters.AddWithValue("$status", vendor.Status.ToString());
        command.Parameters.AddWithValue("$created", vendor.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$archived", vendor.ArchivedUtc.HasValue ? (object)vendor.ArchivedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
    }

    private static Vendor ReadVendor(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        Enum.Parse<VendorStatus>(reader.GetString(6), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private async Task<ExpenseHeader?> GetExpenseHeaderAsync(SqliteConnection connection, Guid expenseId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ExpenseHeaderSelect + " WHERE expense_id=$id;";
        command.Parameters.AddWithValue("$id", expenseId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHeader(reader) : null;
    }

    private static ExpenseHeader ReadHeader(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
        Guid.Parse(reader.GetString(2)),
        DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        new CurrencyCode(reader.GetString(4)),
        reader.GetString(5),
        reader.GetString(6),
        Guid.Parse(reader.GetString(7)),
        Enum.Parse<DirectExpenseStatus>(reader.GetString(8), ignoreCase: false),
        DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static async Task<DirectExpense> LoadExpenseAsync(SqliteConnection connection, ExpenseHeader header, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT expense_line_id,expense_account_id,fund_id,description,amount
            FROM direct_expense_lines
            WHERE expense_id=$expense
            ORDER BY line_number;
            """;
        command.Parameters.AddWithValue("$expense", header.Id.ToString("D"));
        var lines = new List<DirectExpenseLine>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new DirectExpenseLine(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                ParseDecimal(reader.GetString(4)),
                reader.GetString(3)));
        }

        return new DirectExpense(
            header.Id,
            header.VendorId,
            header.BankAccountId,
            header.ExpenseDate,
            header.Currency,
            lines,
            header.Reference,
            header.Memo,
            header.JournalEntryId,
            header.Status,
            header.CreatedUtc,
            header.PostedUtc);
    }

    private const string ExpenseHeaderSelect = """
        SELECT expense_id,vendor_id,bank_account_id,expense_date,currency,reference,memo,journal_entry_id,
               expense_status,total_amount,created_utc,posted_utc
        FROM direct_expenses
        """;

    private static string FormatDecimal(decimal value) => value.ToString("0.00############################", CultureInfo.InvariantCulture);
    private static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private readonly record struct ExpenseHeader(
        Guid Id,
        Guid? VendorId,
        Guid BankAccountId,
        DateOnly ExpenseDate,
        CurrencyCode Currency,
        string Reference,
        string Memo,
        Guid JournalEntryId,
        DirectExpenseStatus Status,
        DateTimeOffset CreatedUtc,
        DateTimeOffset? PostedUtc);
}
