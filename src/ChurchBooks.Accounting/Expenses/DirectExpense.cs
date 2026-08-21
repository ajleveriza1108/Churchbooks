using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Expenses;

public sealed record DirectExpense
{
    public Guid Id { get; }
    public Guid? VendorId { get; }
    public Guid BankAccountId { get; }
    public DateOnly ExpenseDate { get; }
    public CurrencyCode Currency { get; }
    public string Reference { get; }
    public string Memo { get; }
    public Guid JournalEntryId { get; }
    public IReadOnlyList<DirectExpenseLine> Lines { get; }
    public DirectExpenseStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? PostedUtc { get; }
    public decimal TotalAmount => Lines.Sum(static line => line.Amount);

    public DirectExpense(
        Guid id,
        Guid? vendorId,
        Guid bankAccountId,
        DateOnly expenseDate,
        CurrencyCode currency,
        IEnumerable<DirectExpenseLine> lines,
        string? reference = null,
        string? memo = null,
        Guid? journalEntryId = null,
        DirectExpenseStatus status = DirectExpenseStatus.Draft,
        DateTimeOffset? createdUtc = null,
        DateTimeOffset? postedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Expense ID cannot be empty.", nameof(id));
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor ID cannot be empty when supplied.", nameof(vendorId));
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Bank account ID cannot be empty.", nameof(bankAccountId));
        var lineArray = lines?.ToArray() ?? throw new ArgumentNullException(nameof(lines));
        if (lineArray.Length == 0) throw new ArgumentException("A direct expense requires at least one line.", nameof(lines));
        if (lineArray.Select(static line => line.Id).Distinct().Count() != lineArray.Length)
            throw new ArgumentException("Expense line IDs must be unique.", nameof(lines));
        if (status == DirectExpenseStatus.Draft && postedUtc.HasValue)
            throw new ArgumentException("A draft expense cannot have a posted timestamp.", nameof(postedUtc));
        if (status == DirectExpenseStatus.Posted && !postedUtc.HasValue)
            throw new ArgumentException("A posted expense requires a posted timestamp.", nameof(postedUtc));

        Id = id;
        VendorId = vendorId;
        BankAccountId = bankAccountId;
        ExpenseDate = expenseDate;
        Currency = currency;
        Lines = lineArray;
        Reference = reference?.Trim() ?? string.Empty;
        Memo = memo?.Trim() ?? string.Empty;
        JournalEntryId = journalEntryId is null || journalEntryId == Guid.Empty ? Guid.NewGuid() : journalEntryId.Value;
        Status = status;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
        PostedUtc = postedUtc;
    }
}
