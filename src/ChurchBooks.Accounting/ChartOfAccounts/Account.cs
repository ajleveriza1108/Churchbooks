namespace ChurchBooks.Accounting.ChartOfAccounts;

public sealed record Account
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public AccountType Type { get; }
    public AccountStatus Status { get; }
    public Guid? ParentAccountId { get; }
    public bool AllowDirectPosting { get; }

    public AccountNormalBalance NormalBalance => Type is AccountType.Asset or AccountType.Expense
        ? AccountNormalBalance.Debit
        : AccountNormalBalance.Credit;

    public Account(
        Guid id,
        string code,
        string name,
        AccountType type,
        AccountStatus status = AccountStatus.Active,
        Guid? parentAccountId = null,
        bool allowDirectPosting = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Account ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Type = type;
        Status = status;
        ParentAccountId = parentAccountId;
        AllowDirectPosting = allowDirectPosting;
    }
}
