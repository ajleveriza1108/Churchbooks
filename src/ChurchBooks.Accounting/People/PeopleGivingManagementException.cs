namespace ChurchBooks.Accounting.People;

public sealed class PeopleGivingManagementException : Exception
{
    public PeopleGivingManagementException(string message) : base(message)
    {
    }

    public PeopleGivingManagementException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
