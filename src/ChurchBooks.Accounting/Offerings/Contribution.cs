using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Offerings;

public sealed record Contribution
{
    public Guid Id { get; }
    public Guid BatchId { get; }
    public Guid PersonId { get; }
    public DateOnly ReceivedDate { get; }
    public CurrencyCode Currency { get; }
    public string Reference { get; }
    public string Memo { get; }
    public DateTimeOffset CreatedUtc { get; }
    public IReadOnlyList<ContributionLine> Lines { get; }
    public decimal TotalAmount => Lines.Sum(static line => line.Amount);

    public Contribution(
        Guid id,
        Guid batchId,
        Guid personId,
        DateOnly receivedDate,
        CurrencyCode currency,
        IEnumerable<ContributionLine> lines,
        string? reference = null,
        string? memo = null,
        DateTimeOffset? createdUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Contribution ID cannot be empty.", nameof(id));
        if (batchId == Guid.Empty) throw new ArgumentException("Offering batch ID cannot be empty.", nameof(batchId));
        if (personId == Guid.Empty) throw new ArgumentException("Person ID cannot be empty.", nameof(personId));
        ArgumentNullException.ThrowIfNull(lines);
        var lineArray = lines.ToArray();
        if (lineArray.Length == 0) throw new ArgumentException("A contribution requires at least one breakdown line.", nameof(lines));
        if (lineArray.Select(static line => line.Id).Distinct().Count() != lineArray.Length)
            throw new ArgumentException("Contribution line IDs must be unique.", nameof(lines));
        if (lineArray.GroupBy(static line => (line.GivingCategoryId, line.FundId)).Any(group => group.Count() > 1))
            throw new ArgumentException("Combine duplicate giving-category and fund breakdown rows before saving.", nameof(lines));

        Id = id;
        BatchId = batchId;
        PersonId = personId;
        ReceivedDate = receivedDate;
        Currency = currency;
        Reference = reference?.Trim() ?? string.Empty;
        Memo = memo?.Trim() ?? string.Empty;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
        Lines = lineArray;
    }
}
