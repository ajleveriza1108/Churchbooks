# ChurchBooks Phase 5 R1.2 — GUID / foreign-key round-trip repair

## Root cause

Phase 5 R1 seeded the starter `Tithes` giving category with SQLite `lower(hex(randomblob(16)))`. SQLite stored 32 compact hexadecimal characters, while .NET `Guid` persistence elsewhere uses canonical `D` format with hyphens. The domain model could parse the compact value, but saving a contribution line wrote the same Guid in canonical form. Because SQLite TEXT foreign keys compare the stored strings, the contribution line could not reference the compact starter key.

## Repair

- Fresh databases seed Tithes with a canonical dashed Guid.
- Existing compact `TITHE` storage keys are normalized transactionally to canonical Guid text.
- Any existing `contribution_lines` references are updated in the same transaction with deferred foreign-key enforcement.
- `PRAGMA foreign_key_check` must return no rows before migration commit.
- The existing `Store_RoundTripsContributionAndBreakdown` test now recreates the legacy compact-ID condition, reruns the migrator, then proves contribution round-trip. The total test count remains 125.

## Unchanged

- Approved ChurchBooks Pro GUI contract.
- Data-driven giving categories and funds; no hard-coded Missions/Building/Youth/etc.
- Phase 2 accounting kernel and Phase 3 fund/reporting invariants.
- Phase 5 remains a subsidiary giving ledger; Phase 6 alone owns bank/deposit-to-GL posting.
- Licensing remains deferred.
