# Phase 6 — Personalized first-run setup + banking/deposit bridge

Phase 6 starts only from the Windows-verified Phase 5 R1.3 baseline (125/125 tests plus staged/live WPF smoke).

## First-run personalization
- First launch opens Setup instead of a blocking modal.
- Organization display/legal name, base currency, fiscal-year start month, country, and tax identifier are local settings.
- Canonical internal keys stay stable; users can rename visible singular/plural terms such as Member/Partner, Service/Gathering, Offering/Contribution, Giving Category/Offering Type, Fund/Ministry Fund, Bank Account, and Deposit.
- Extra search aliases can be added/removed without changing accounting data.
- Settings remains available later; setup is not a one-way wizard.
- Completing setup creates an open fiscal period if needed and a starter Contribution Income account only when no Income account exists. Active Giving Categories receive a safe starter mapping that accountants can revise.

## Banking
- Multiple local bank accounts, each mapped to one Asset ledger account.
- Recorded Phase 5 contributions can be grouped into a draft bank deposit exactly once.
- Every Giving Category must map to an Income account.
- Deposit preview creates a balanced Fund-aware journal: debit bank Asset by Fund, credit mapped Income by Giving Category + Fund.
- Posting uses the protected FundAccountingEngine; duplicate Journal IDs are rejected and deposit status can recover from a journal-written/status-not-written interruption.
- Full statement reconciliation remains a later roadmap phase; Phase 6 does not pretend draft deposit grouping is bank reconciliation.

## GUI
The approved ChurchBooks Pro desktop reference remains binding: compact/maximized, no clipped/hidden essential controls, one-window preference, scrolling only as fallback.
