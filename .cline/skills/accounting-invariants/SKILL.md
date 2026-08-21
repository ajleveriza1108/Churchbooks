---
name: accounting-invariants
description: Protect ChurchBooks double-entry and ledger correctness. Use for journals, GL, periods, transfers, funds, reconciliation, posting, balances, or accounting tests.
---

# Accounting Invariants Skill

1. Read `.clinerules/20-accounting-invariants.md`.
2. Identify the invariant(s) affected before editing code.
3. Preserve the Phase 2 exact-balance, closed-period, inactive-account, and parameterized-ledger protections unless the task explicitly extends them.
4. Add both example regression tests and property/law tests where mathematics is involved.
5. Never replace audit-safe correction with destructive editing.
6. For fund accounting, prove fund dimensions reconcile to the GL and that internal fund transfers do not create income/expense.
7. Report any CPA/regulatory assumption that requires external validation rather than presenting it as certified compliance.
