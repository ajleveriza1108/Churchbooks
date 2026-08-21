namespace ChurchBooks.Accounting.Periods;

public sealed record AccountingPeriod
{
    public Guid Id { get; }
    public string Name { get; }
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }
    public AccountingPeriodStatus Status { get; }

    public AccountingPeriod(
        Guid id,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        AccountingPeriodStatus status = AccountingPeriodStatus.Open)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Accounting period ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (endDate < startDate)
        {
            throw new ArgumentException("Accounting period end date cannot be earlier than its start date.", nameof(endDate));
        }

        Id = id;
        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
    }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}
