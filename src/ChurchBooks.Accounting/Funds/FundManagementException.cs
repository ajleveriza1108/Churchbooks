namespace ChurchBooks.Accounting.Funds;

public sealed class FundManagementException : Exception
{
    public FundManagementException(string message) : base(message)
    {
    }
}
