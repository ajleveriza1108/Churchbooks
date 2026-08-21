# Accounting Invariants

- Posted journal: total debit must equal total credit exactly in base currency.
- Posted history is append-only from the user's perspective; correction is by reversal/void/adjustment with traceability.
- Closed periods reject ordinary posting unless an explicit governed reopen workflow exists.
- Inactive/non-posting accounts reject new direct postings.
- Monetary persistence must not use binary floating point.
- Transfers do not create income or expense.
- Future fund subledgers must reconcile to the GL.
- Future offering/member detail totals must reconcile to service batch totals.
- Future bank reconciliation must preserve book, bank, and reconciled balances distinctly.
- Property-based tests should express laws, not only examples.

## Phase 3B fund invariants
- Fund-aware journals must balance independently inside every fund represented in the journal.
- Every fund-aware journal line has exactly one fund assignment.
- Fund balance is derived from balance-sheet movement of fund-tagged journal lines; never maintain an independently editable fund balance.
- Internal fund transfers must preserve consolidated cash/net assets while moving fund-level custody/net assets explicitly.
- Archived funds keep history but reject new ordinary postings.
- Donor restriction release is explicit and auditable; never silently relabel a restricted fund as unrestricted.
