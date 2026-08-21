namespace ChurchBooks.Accounting.Funds;

public sealed record Fund
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public FundRestriction Restriction { get; }
    public FundOverspendPolicy OverspendPolicy { get; }
    public string Purpose { get; }
    public FundStatus Status { get; }
    public DateTimeOffset? ArchivedUtc { get; }

    public Fund(
        Guid id,
        string code,
        string name,
        FundRestriction restriction = FundRestriction.Unrestricted,
        FundOverspendPolicy? overspendPolicy = null,
        string? purpose = null,
        FundStatus status = FundStatus.Active,
        DateTimeOffset? archivedUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Fund ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (status == FundStatus.Active && archivedUtc.HasValue)
        {
            throw new ArgumentException("An active fund cannot have an archived timestamp.", nameof(archivedUtc));
        }

        if (status == FundStatus.Archived && !archivedUtc.HasValue)
        {
            throw new ArgumentException("An archived fund requires an archived timestamp.", nameof(archivedUtc));
        }

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Restriction = restriction;
        OverspendPolicy = overspendPolicy ?? DefaultOverspendPolicy(restriction);
        Purpose = purpose?.Trim() ?? string.Empty;
        Status = status;
        ArchivedUtc = archivedUtc;
    }

    public Fund Archive(DateTimeOffset archivedUtc) => new(
        Id,
        Code,
        Name,
        Restriction,
        OverspendPolicy,
        Purpose,
        FundStatus.Archived,
        archivedUtc);

    public Fund Reactivate() => new(
        Id,
        Code,
        Name,
        Restriction,
        OverspendPolicy,
        Purpose,
        FundStatus.Active,
        archivedUtc: null);

    private static FundOverspendPolicy DefaultOverspendPolicy(FundRestriction restriction) => restriction switch
    {
        FundRestriction.Unrestricted => FundOverspendPolicy.Allow,
        FundRestriction.BoardDesignated => FundOverspendPolicy.Warn,
        FundRestriction.DonorRestricted => FundOverspendPolicy.Block,
        FundRestriction.Endowment => FundOverspendPolicy.Block,
        _ => throw new ArgumentOutOfRangeException(nameof(restriction), restriction, "Unknown fund restriction.")
    };
}
