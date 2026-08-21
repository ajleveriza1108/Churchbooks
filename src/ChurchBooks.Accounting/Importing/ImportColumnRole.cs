namespace ChurchBooks.Accounting.Importing;

public enum ImportColumnRole
{
    Ignore = 0,
    Date = 1,
    Amount = 2,
    Debit = 3,
    Credit = 4,
    Description = 5,
    Reference = 6,
    Payee = 7,
    PersonName = 8,
    MemberNumber = 9,
    FundCode = 10,
    GivingCategoryCode = 11,
    BankAccount = 12,
    AccountCode = 13,
    Memo = 14,
    ExternalPersonId = 15,
    FirstName = 16,
    MiddleName = 17,
    LastName = 18,
    PreferredName = 19,
    Email = 20,
    Phone = 21,
    HouseholdName = 22,
    IsMember = 23,
    IsDonor = 24
}
