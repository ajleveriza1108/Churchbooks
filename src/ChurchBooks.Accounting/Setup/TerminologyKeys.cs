namespace ChurchBooks.Accounting.Setup;

public static class TerminologyKeys
{
    public const string Member = "Member";
    public const string Donor = "Donor";
    public const string Household = "Household";
    public const string Service = "Service";
    public const string Offering = "Offering";
    public const string GivingCategory = "GivingCategory";
    public const string Fund = "Fund";
    public const string BankAccount = "BankAccount";
    public const string Deposit = "Deposit";
    public const string Ministry = "Ministry";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Member, Donor, Household, Service, Offering, GivingCategory, Fund, BankAccount, Deposit, Ministry
    };
}
