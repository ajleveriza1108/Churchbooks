namespace ChurchBooks.Accounting.Expenses;

public sealed class ExpenseManagementException : Exception
{
    public ExpenseManagementException(string message) : base(message)
    {
    }
}
