---
name: church-fund-accounting
description: Design or implement ChurchBooks church fund accounting, restricted/designated funds, fund transfers, fund balances, ministry/project allocations, or donor restrictions.
---

# Church Fund Accounting Skill

Use native ChurchBooks fund dimensions; do not emulate funds by creating fake bank accounts, Chart-of-Accounts clones, or QuickBooks-style Class workarounds.

Core distinctions:
- Bank Account: physical custody/location of cash.
- Fund: designation/restriction/purpose of resources.
- Ministry/Department: organizational responsibility or reporting dimension.
- Project/Campaign: time- or goal-bounded activity.

Phase 3B laws:
- Every line in a fund-aware posted journal has exactly one Fund assignment.
- A fund-aware journal must balance overall AND independently inside every Fund represented in the journal.
- One bank may contain many funds; one fund may span many banks.
- Fund balance is derived from fund-tagged balance-sheet movement, not from a second editable balance field.
- Internal fund transfers use explicit balanced journal lines and have zero consolidated bank movement and zero consolidated net-asset change.
- Archived funds reject new ordinary postings while preserving history.
- Restricted/designated fund negative-balance behavior is explicit: Allow, Warn, or Block. Defaults are conservative and user-configurable later.
- Donor restriction releases are NOT ordinary fund transfers. They require a dedicated, explicit, auditable workflow and CPA/jurisdiction validation before release behavior is finalized.
- Restrictions are never silently removed or converted.

Persistence laws:
- Keep Phase 2 journal tables intact.
- Store fund assignment additively in `journal_line_funds` so the verified Phase 2 journal kernel remains frozen.
- Persist a journal and all of its fund assignments in one SQLite transaction.
- Use decimal TEXT parsing in .NET; do not introduce SQLite REAL for accounting amounts.
- Use `schema_migrations` for future schema progression; contradictory migration key/version history must fail closed before the new phase is advertised.

Before implementing or changing fund behavior, update current research in `docs/research/RESEARCH-REGISTER.md`.


## Phase 3D reporting/integrity rules
- Fund reports must be derived from posted journal/fund assignments, never editable shadow balances.
- Integrity scans are read-only unless a later explicit repair workflow is separately approved and transactionally tested.
- A restriction-release review is not a normal fund transfer. In Phase 3D it may validate evidence/amount/readiness but must not post a journal.
- Keep Bank Account != Fund and preserve per-fund journal balancing.
