namespace ChurchBooks.Accounting.Banking;

public sealed class BankingManagementException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public BankingManagementException(IEnumerable<string> errors) : base(string.Join(" ", errors))
    {
        Errors = errors.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
    }
    public BankingManagementException(string error) : this(new[] { error }) { }
}
