using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Journals;

public sealed record JournalEntry
{
    public Guid Id { get; }
    public string EntryNumber { get; }
    public Guid PeriodId { get; }
    public DateOnly PostingDate { get; }
    public string Description { get; }
    public string Reference { get; }
    public CurrencyCode BaseCurrency { get; }
    public IReadOnlyList<JournalLine> Lines { get; }

    public decimal TotalDebit => Lines.Sum(static line => line.Debit);
    public decimal TotalCredit => Lines.Sum(static line => line.Credit);
    public bool IsBalanced => Lines.Count >= 2 && TotalDebit == TotalCredit && TotalDebit > 0m;

    public JournalEntry(
        Guid id,
        string entryNumber,
        Guid periodId,
        DateOnly postingDate,
        string description,
        CurrencyCode baseCurrency,
        IEnumerable<JournalLine> lines,
        string? reference = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Journal entry ID cannot be empty.", nameof(id));
        }

        if (periodId == Guid.Empty)
        {
            throw new ArgumentException("Accounting period ID cannot be empty.", nameof(periodId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(entryNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(lines);

        if (string.IsNullOrWhiteSpace(baseCurrency.Value))
        {
            throw new ArgumentException("Base currency must be a valid three-letter currency code.", nameof(baseCurrency));
        }

        Id = id;
        EntryNumber = entryNumber.Trim();
        PeriodId = periodId;
        PostingDate = postingDate;
        Description = description.Trim();
        Reference = reference?.Trim() ?? string.Empty;
        BaseCurrency = baseCurrency;
        Lines = lines.ToArray();
    }
}
