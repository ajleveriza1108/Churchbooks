# Accounting Model

ChurchBooks uses true double-entry accounting. Posted journal debits and credits must balance exactly in base currency. Posted history is corrected through governed reversal/void/adjustment rather than silent deletion.

## Dimensions are not accounts
A bank account answers **where the money is held**. A fund answers **what the money is designated or restricted for**. A ministry/department answers **who or what organizational area is responsible**. A project/campaign answers **which bounded initiative is involved**.

One bank can contain multiple funds. One fund can span multiple banks.

## Phase 3B native fund dimension
ChurchBooks does not emulate funds with bank subaccounts, duplicate Chart-of-Accounts branches, or a generic Class field. Fund-aware journal lines are assigned to one native Fund while continuing to post to the single canonical General Ledger.

A fund-aware journal is valid only when:
1. the base journal balances exactly;
2. every journal line has exactly one fund assignment;
3. each represented fund balances exactly inside the journal;
4. every referenced fund exists and is active;
5. configured negative-balance policy is satisfied.

## Fund balance
Fund balance/net assets are derived from fund-tagged balance-sheet movement. Asset and liability movements determine the fund balance; income and expense activity is reported separately and is not double-counted into the balance calculation.

## Internal fund transfers
An internal transfer moves fund-level net assets using explicit per-fund-balanced lines. It must have zero consolidated cash movement when the same physical bank is used and zero consolidated net-asset change. Transfer lines remain auditable.

## Restrictions
ChurchBooks distinguishes unrestricted, board-designated, donor-restricted, and endowment fund classifications. Donor restriction releases/reclassifications are explicit workflows and are never silently inferred from spending. Final compliance/report treatment is subject to CPA/jurisdiction validation.

## Persistence
Phase 3B adds `funds` and `journal_line_funds` without altering the frozen Phase 2 journal schema. A `schema_migrations` ledger starts deterministic database migration history for future phases. Known migration keys must match their exact schema versions; conflicting history aborts the migration transaction.
