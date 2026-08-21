using System.IO;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Expenses;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class Phase10ExpenseDataTests
{
    [Fact]
    public async Task SchemaTen_CreatesVendorAndExpenseTables()
    {
        await WithDb(async database =>
        {
            await new Phase10DatabaseMigrator(database).InitializeAsync();
            Assert.True(await TableExists(database, "vendors"));
            Assert.True(await TableExists(database, "direct_expenses"));
            Assert.True(await TableExists(database, "direct_expense_lines"));
        });
    }

    [Fact]
    public async Task SchemaVersion_IsTen()
    {
        await WithDb(async database =>
        {
            await new Phase10DatabaseMigrator(database).InitializeAsync();
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key='schema_version';";
            Assert.Equal("10", (string)(await command.ExecuteScalarAsync())!);
        });
    }

    [Fact]
    public async Task Vendor_RoundTripsAndArchivePersists()
    {
        await WithReadyDb(async context =>
        {
            var vendor = new Vendor(Guid.NewGuid(), "V001", "Local Supplier", "TIN-1", "vendor@example.test", "09170000000");
            await context.ExpenseStore.AddVendorAsync(vendor);
            await context.ExpenseStore.UpdateVendorAsync(vendor.Archive(DateTimeOffset.UtcNow));
            var loaded = await context.ExpenseStore.GetVendorAsync(vendor.Id);
            Assert.NotNull(loaded);
            Assert.Equal(VendorStatus.Archived, loaded!.Status);
            Assert.Equal("Local Supplier", loaded.Name);
        });
    }

    [Fact]
    public async Task VendorCode_IsCaseInsensitiveUnique()
    {
        await WithReadyDb(async context =>
        {
            await context.ExpenseStore.AddVendorAsync(new Vendor(Guid.NewGuid(), "V001", "First"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.ExpenseStore.AddVendorAsync(new Vendor(Guid.NewGuid(), "v001", "Second")));
        });
    }

    [Fact]
    public async Task DirectExpense_RoundTripsMultipleLines()
    {
        await WithReadyDb(async context =>
        {
            var vendor = new Vendor(Guid.NewGuid(), "V100", "Vendor");
            await context.ExpenseStore.AddVendorAsync(vendor);
            var expense = new DirectExpense(
                Guid.NewGuid(),
                vendor.Id,
                context.Bank.Id,
                new DateOnly(2026, 8, 20),
                context.Bank.Currency,
                new[]
                {
                    new DirectExpenseLine(Guid.NewGuid(), context.ExpenseAccount.Id, context.Fund.Id, 125.50m, "Supplies"),
                    new DirectExpenseLine(Guid.NewGuid(), context.ExpenseAccount.Id, context.Fund.Id, 24.50m, "Delivery")
                },
                "OR-100",
                "Office supplies",
                Guid.NewGuid());
            await context.ExpenseStore.SaveDirectExpenseAsync(expense);
            var loaded = await context.ExpenseStore.GetDirectExpenseAsync(expense.Id);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Lines.Count);
            Assert.Equal(150.00m, loaded.TotalAmount);
            Assert.Equal("OR-100", loaded.Reference);
        });
    }

    [Fact]
    public async Task DraftExpense_CanBeMarkedPostedOnlyOnce()
    {
        await WithReadyDb(async context =>
        {
            var expense = Expense(context, 50m);
            await context.ExpenseStore.SaveDirectExpenseAsync(expense);
            var postedUtc = DateTimeOffset.UtcNow;
            await context.ExpenseStore.MarkDirectExpensePostedAsync(expense.Id, expense.JournalEntryId, postedUtc);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.ExpenseStore.MarkDirectExpensePostedAsync(expense.Id, expense.JournalEntryId, postedUtc));
        });
    }

    [Fact]
    public async Task ExpenseListing_PreservesDraftAndPostedHistory()
    {
        await WithReadyDb(async context =>
        {
            var first = Expense(context, 10m);
            var second = Expense(context, 20m);
            await context.ExpenseStore.SaveDirectExpenseAsync(first);
            await context.ExpenseStore.SaveDirectExpenseAsync(second);
            await context.ExpenseStore.MarkDirectExpensePostedAsync(first.Id, first.JournalEntryId, DateTimeOffset.UtcNow);
            var rows = await context.ExpenseStore.GetDirectExpensesAsync();
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, item => item.Status == DirectExpenseStatus.Posted);
            Assert.Contains(rows, item => item.Status == DirectExpenseStatus.Draft);
        });
    }

    [Fact]
    public async Task SchemaTen_PreservesAdaptiveImportAndReconciliationTables()
    {
        await WithDb(async database =>
        {
            await new Phase10DatabaseMigrator(database).InitializeAsync();
            Assert.True(await TableExists(database, "import_source_profiles"));
            Assert.True(await TableExists(database, "bank_reconciliations"));
        });
    }

    private static DirectExpense Expense(ReadyContext context, decimal amount) => new(
        Guid.NewGuid(),
        null,
        context.Bank.Id,
        new DateOnly(2026, 8, 20),
        context.Bank.Currency,
        new[] { new DirectExpenseLine(Guid.NewGuid(), context.ExpenseAccount.Id, context.Fund.Id, amount, "Expense") },
        "REF",
        "Memo",
        Guid.NewGuid());

    private static async Task WithReadyDb(Func<ReadyContext, Task> action)
    {
        await WithDb(async database =>
        {
            await new Phase10DatabaseMigrator(database).InitializeAsync();
            var accountingStore = new SqliteAccountingStore(database);
            var fundStore = new SqliteFundAccountingStore(database);
            var bankingStore = new SqliteBankingStore(database);
            var expenseStore = new SqliteExpenseStore(database);

            var expenseAccount = new Account(Guid.NewGuid(), "6100", "Office Expense", AccountType.Expense);
            await accountingStore.AddAccountAsync(expenseAccount);
            var fund = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
            await fundStore.AddFundAsync(fund);
            var bankLedger = new Account(Guid.NewGuid(), "1010", "Operating Bank", AccountType.Asset);
            var bank = new BankAccount(Guid.NewGuid(), "Operating", "Bank", "1234", new CurrencyCode("PHP"), bankLedger.Id);
            await bankingStore.AddBankAccountAsync(bank, bankLedger);

            await action(new ReadyContext(database, expenseStore, expenseAccount, fund, bank));
        });
    }

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
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase10.DataTests", Guid.NewGuid().ToString("N"));
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

    private sealed record ReadyContext(
        ChurchBooksDatabase Database,
        SqliteExpenseStore ExpenseStore,
        Account ExpenseAccount,
        Fund Fund,
        BankAccount Bank);
}
