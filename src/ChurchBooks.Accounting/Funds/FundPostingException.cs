namespace ChurchBooks.Accounting.Funds;

public sealed class FundPostingException : InvalidOperationException
{
    public IReadOnlyList<string> Errors { get; }

    public FundPostingException(IEnumerable<string> errors)
        : this(errors?.ToArray() ?? throw new ArgumentNullException(nameof(errors)))
    {
    }

    private FundPostingException(string[] errors)
        : base(errors.Length == 0
            ? "Fund-aware journal entry failed posting validation."
            : string.Join(" ", errors))
    {
        Errors = errors;
    }
}
