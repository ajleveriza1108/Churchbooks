namespace ChurchBooks.Accounting.Offerings;

public sealed class OfferingManagementException : InvalidOperationException
{
    public OfferingManagementException(string message) : base(message) { }
}
