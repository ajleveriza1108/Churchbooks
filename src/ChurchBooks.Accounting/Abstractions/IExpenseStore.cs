using ChurchBooks.Accounting.Expenses;

namespace ChurchBooks.Accounting.Abstractions;

public interface IExpenseStore
{
    Task AddVendorAsync(Vendor vendor, CancellationToken cancellationToken = default);
    Task UpdateVendorAsync(Vendor vendor, CancellationToken cancellationToken = default);
    Task<Vendor?> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vendor>> GetVendorsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task SaveDirectExpenseAsync(DirectExpense expense, CancellationToken cancellationToken = default);
    Task<DirectExpense?> GetDirectExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectExpense>> GetDirectExpensesAsync(CancellationToken cancellationToken = default);
    Task MarkDirectExpensePostedAsync(Guid expenseId, Guid journalEntryId, DateTimeOffset postedUtc, CancellationToken cancellationToken = default);
}
