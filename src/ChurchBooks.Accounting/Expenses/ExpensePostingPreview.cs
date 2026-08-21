using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Expenses;

public sealed record ExpensePostingPreview(DirectExpense Expense, FundJournalEntry FundJournal);
