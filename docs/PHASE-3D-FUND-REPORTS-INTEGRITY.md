# Phase 3D - Fund Reports + Integrity Center

## Purpose
Phase 3D turns the verified Phase 3C fund workspace into a reviewable reporting and integrity surface without rewriting the protected accounting kernel.

## Delivered
- Ledger-derived Fund Balance report with an as-of date.
- Fund Activity report with opening balance, period increases/decreases, closing balance, and posted activity detail.
- UTF-8 CSV export to the user's Documents\ChurchBooks\Reports folder.
- Read-only Integrity Center that checks foreign keys, fund-assignment completeness, per-fund journal balancing, archived non-zero funds, missing restricted-purpose documentation, and negative restricted balances.
- Restriction Release Review preview. It validates applicability, amount, available balance, evidence, and endowment special-review requirements but posts **no journal**.
- Search aliases for Reports, Statement of Activities, Audit, Health Check, and Integrity Center.

## Accounting boundaries
- Reports are derived from posted ledger/fund assignments; report rows are not shadow balances.
- Integrity Center never repairs or rewrites posted data automatically.
- Phase 3D does not implement a jurisdiction-specific donor-restriction release journal. That remains a CPA/accounting-validation item.
- General Balance Sheet, full Statement of Activities, budget reporting, PDF packages, and immutable finalized reports remain later reporting phases.

## Upgrade safety
- Database schema version remains 3.
- Phase 2 exact-balance kernel remains frozen.
- Critical Phase 3B fund engine/migrator files remain frozen.
- Phase 3C verified Fund Manager/Familiar Start remains the protected UI ancestor.
- Staging/build/test/WPF smoke must pass before live mutation.
- Live verification must pass after the manifest-last transaction or rollback runs.
