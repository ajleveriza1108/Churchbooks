namespace ChurchBooks.Accounting.Importing;

public static class ChurchBooksStandardImportTemplates
{
    public static IReadOnlyList<StandardImportTemplateDefinition> All { get; } =
    [
        new(
            "People Directory",
            ImportPurpose.PeopleDirectory,
            ["External Person ID", "Member Number", "First Name", "Middle Name", "Last Name", "Preferred Name", "Email", "Phone", "Member", "Donor", "Household"],
            "Recommended format for member/donor directories. Existing church spreadsheets are still supported."),
        new(
            "Giving",
            ImportPurpose.Giving,
            ["Date", "External Person ID", "Member Number", "Person Name", "Giving Category", "Fund", "Amount", "Reference", "Memo"],
            "Recommended contribution detail format. It stages for review and never posts automatically."),
        new(
            "Bank Statement",
            ImportPurpose.BankStatement,
            ["Date", "Description", "Reference", "Debit", "Credit"],
            "Recommended bank statement format for later reconciliation."),
        new(
            "General Ledger",
            ImportPurpose.GeneralLedger,
            ["Date", "Reference", "Account Code", "Debit", "Credit", "Fund", "Memo"],
            "Recommended general-ledger exchange format. Import remains review-first.")
    ];
}
