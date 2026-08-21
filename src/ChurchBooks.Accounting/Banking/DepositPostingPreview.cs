using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Banking;

public sealed record DepositPostingPreview(BankDeposit Deposit, FundJournalEntry FundJournal);
