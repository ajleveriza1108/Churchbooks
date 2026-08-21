# ChurchBooks Phase 2 - Accounting Engine

Phase 2 establishes the accountant-grade double-entry kernel beneath the church-first user interface.

## Implemented in Phase 2

- Chart of Accounts domain model with Asset, Liability, Equity, Income, and Expense types.
- Debit or credit normal-balance rules.
- Active and inactive accounts.
- Parent accounts and direct-posting control for future account hierarchies.
- Open and closed accounting periods with posting-date boundaries.
- Draft journal entries and journal lines.
- Exact debit-equals-credit validation in the company base currency.
- Rejection of unknown, inactive, or non-posting accounts.
- Rejection of closed-period and out-of-period postings.
- Duplicate journal-entry protection.
- Transactional SQLite persistence of posted journal headers and lines.
- General Ledger retrieval by account and optional date range.
- Decimal values stored as invariant decimal text rather than floating-point values.
- Automated domain and SQLite end-to-end accounting tests.
- Existing SQLite security audit and file-release regression protections retained.

## Deliberately not implemented yet

Phase 2 does not create offering, donor, fund, banking, expense, invoice, tax, or report workflows. Those later modules will post through this kernel rather than inventing separate accounting logic.

Multi-currency source amounts and exchange-rate accounting are planned above this base-currency posting kernel. The debit-equals-credit invariant always remains in the company base currency.

Licensing remains out of scope until the final testing and licensing phase.
