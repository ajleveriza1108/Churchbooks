namespace ChurchBooks.Accounting.Journals;

public sealed class JournalPostingException : InvalidOperationException
{
    public IReadOnlyList<string> Errors { get; }

    public JournalPostingException(IEnumerable<string> errors)
        : this(errors?.ToArray() ?? throw new ArgumentNullException(nameof(errors)))
    {
    }

    private JournalPostingException(string[] errors)
        : base(errors.Length == 0
            ? "Journal entry failed posting validation."
            : string.Join(" ", errors))
    {
        Errors = errors;
    }
}
