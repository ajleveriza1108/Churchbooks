using ChurchBooks.Accounting.Expenses;
using ChurchBooks.Core.Finance;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class Phase10ExpenseTests
{
    [Fact]
    public void Vendor_RequiresNonEmptyIdentity()
    {
        Assert.Throws<ArgumentException>(() => new Vendor(Guid.Empty, "V1", "Vendor"));
    }

    [Fact]
    public void Vendor_ArchiveAndRestorePreserveIdentity()
    {
        var vendor = new Vendor(Guid.NewGuid(), "V1", "Vendor");
        var archived = vendor.Archive(DateTimeOffset.UtcNow);
        var restored = archived.Reactivate();
        Assert.Equal(vendor.Id, restored.Id);
        Assert.Equal(VendorStatus.Active, restored.Status);
        Assert.Null(restored.ArchivedUtc);
    }

    [Fact]
    public void Vendor_ArchivedStatusRequiresTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new Vendor(Guid.NewGuid(), "V1", "Vendor", status: VendorStatus.Archived));
    }

    [Fact]
    public void DirectExpenseLine_RequiresPositiveAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DirectExpenseLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m));
    }

    [Fact]
    public void DirectExpense_RequiresAtLeastOneLine()
    {
        Assert.Throws<ArgumentException>(() => new DirectExpense(
            Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 8, 20), new CurrencyCode("PHP"), Array.Empty<DirectExpenseLine>()));
    }

    [Fact]
    public void DirectExpense_RejectsDuplicateLineIds()
    {
        var lineId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var fundId = Guid.NewGuid();
        var lines = new[]
        {
            new DirectExpenseLine(lineId, accountId, fundId, 10m),
            new DirectExpenseLine(lineId, accountId, fundId, 20m)
        };
        Assert.Throws<ArgumentException>(() => new DirectExpense(
            Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 8, 20), new CurrencyCode("PHP"), lines));
    }

    [Fact]
    public void DirectExpense_TotalIsExactLineSum()
    {
        var lines = new[]
        {
            new DirectExpenseLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.25m),
            new DirectExpenseLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50.75m)
        };
        var expense = new DirectExpense(
            Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 8, 20), new CurrencyCode("PHP"), lines);
        Assert.Equal(151.00m, expense.TotalAmount);
    }

    [Fact]
    public void DirectExpense_DraftCannotHavePostedTimestamp()
    {
        var line = new DirectExpenseLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m);
        Assert.Throws<ArgumentException>(() => new DirectExpense(
            Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 8, 20), new CurrencyCode("PHP"), new[] { line },
            postedUtc: DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DirectExpense_PostedRequiresPostedTimestamp()
    {
        var line = new DirectExpenseLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m);
        Assert.Throws<ArgumentException>(() => new DirectExpense(
            Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 8, 20), new CurrencyCode("PHP"), new[] { line },
            status: DirectExpenseStatus.Posted));
    }

    [Fact]
    public void DirectExpense_GeneratesJournalIdentityWhenNotSupplied()
    {
        var line = new DirectExpenseLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m);
        var expense = new DirectExpense(
            Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 8, 20), new CurrencyCode("PHP"), new[] { line });
        Assert.NotEqual(Guid.Empty, expense.JournalEntryId);
    }
}
