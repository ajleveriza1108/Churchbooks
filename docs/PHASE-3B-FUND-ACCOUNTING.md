# Phase 3B - Church Fund Accounting Engine and Persistence

Status target: Phase 3B R1.

## Purpose
Add a native fund dimension to the verified Phase 2 double-entry journal without rewriting the frozen journal kernel.

## Model
A **Bank Account** says where money is held. A **Fund** says what resources are designated or restricted for. A bank account can contain many funds, and a fund can span many bank accounts.

Fund-aware journal entries assign every journal line to exactly one fund. The journal must balance globally and independently for each fund. This permits reliable fund-specific balance sheets/activity while retaining one canonical General Ledger.

## Fund restriction classes
- Unrestricted
- Board Designated
- Donor Restricted
- Endowment

These are application classifications, not a claim of jurisdiction-specific GAAP/BIR compliance. Final regulatory/reporting terminology remains subject to CPA/regulatory validation.

## Overspend policy
Each fund carries an explicit policy:
- Allow
- Warn
- Block

Defaults:
- Unrestricted -> Allow
- Board Designated -> Warn
- Donor Restricted -> Block
- Endowment -> Block

Future UI will allow authorized configuration with clear warnings.

## Fund balance
ChurchBooks does not store a manually editable running fund balance. It derives fund net assets from fund-tagged Asset/Liability journal movement using exact decimal arithmetic in .NET.

Income and expense lines are not counted again in the balance calculation because doing so would double-count the same economic event.

## Internal transfers
Internal fund transfers create a four-line, per-fund-balanced journal using an Equity interfund-transfer clearing account and asset/bank lines. The source fund decreases, destination fund increases, and consolidated bank/net assets do not change.

A donor restriction release is NOT automatically treated as an ordinary internal transfer. A dedicated release workflow will be designed with audit trail and CPA/jurisdiction validation.

## Persistence
Phase 3B adds:
- `schema_migrations`
- `funds`
- `journal_line_funds`

Existing Phase 2 `journal_entries` and `journal_lines` remain structurally unchanged. Fund assignments are additive and saved in the same SQLite transaction as the base journal.

## Startup migration
The WPF app now runs `FundAccountingDatabaseMigrator` at startup. The migrator initializes Phase 2 first, then transactionally ensures migration records 001, 002, and 003 plus the Phase 3B fund tables/markers. Migration keys and schema versions are verified before commit; contradictory migration history fails closed and rolls back instead of advertising Phase 3B.

## Phase boundary
Phase 3B is engine/persistence. Full fund-management screens, Guided/Familiar Start presentation, dedicated donor-restriction release UI, and financial reports remain Phase 3C/3D work.
