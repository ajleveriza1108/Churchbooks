namespace ChurchBooks.Accounting.Journals;

public sealed record JournalLine
{
    public Guid Id { get; }
    public Guid AccountId { get; }
    public decimal Debit { get; }
    public decimal Credit { get; }
    public string Memo { get; }

    public JournalLine(Guid id, Guid accountId, decimal debit, decimal credit, string? memo = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Journal line ID cannot be empty.", nameof(id));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID cannot be empty.", nameof(accountId));
        }

        if (debit < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(debit), "Debit cannot be negative.");
        }

        if (credit < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(credit), "Credit cannot be negative.");
        }

        if ((debit == 0m && credit == 0m) || (debit > 0m && credit > 0m))
        {
            throw new ArgumentException("A journal line must contain exactly one positive debit or credit amount.");
        }

        Id = id;
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Memo = memo?.Trim() ?? string.Empty;
    }
}
