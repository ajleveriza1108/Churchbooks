namespace ChurchBooks.Accounting.Funds;

public sealed record FundPostingAssessment
{
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool CanPost => Errors.Count == 0;

    public FundPostingAssessment(IEnumerable<string> errors, IEnumerable<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);
        Errors = errors.ToArray();
        Warnings = warnings.ToArray();
    }
}
