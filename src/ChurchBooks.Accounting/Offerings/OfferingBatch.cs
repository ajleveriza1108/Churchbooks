namespace ChurchBooks.Accounting.Offerings;

public sealed record OfferingBatch
{
    public Guid Id { get; }
    public DateOnly ServiceDate { get; }
    public string Name { get; }
    public string Reference { get; }
    public OfferingBatchStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? ClosedUtc { get; }

    public OfferingBatch(
        Guid id,
        DateOnly serviceDate,
        string name,
        string? reference = null,
        OfferingBatchStatus status = OfferingBatchStatus.Open,
        DateTimeOffset? createdUtc = null,
        DateTimeOffset? closedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Offering batch ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (status == OfferingBatchStatus.Open && closedUtc.HasValue)
            throw new ArgumentException("An open offering batch cannot have a closed timestamp.", nameof(closedUtc));
        if (status == OfferingBatchStatus.Closed && !closedUtc.HasValue)
            throw new ArgumentException("A closed offering batch requires a closed timestamp.", nameof(closedUtc));

        Id = id;
        ServiceDate = serviceDate;
        Name = name.Trim();
        Reference = reference?.Trim() ?? string.Empty;
        Status = status;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
        ClosedUtc = closedUtc;
    }

    public OfferingBatch Close(DateTimeOffset closedUtc) =>
        Status == OfferingBatchStatus.Closed
            ? this
            : new OfferingBatch(Id, ServiceDate, Name, Reference, OfferingBatchStatus.Closed, CreatedUtc, closedUtc);
}
