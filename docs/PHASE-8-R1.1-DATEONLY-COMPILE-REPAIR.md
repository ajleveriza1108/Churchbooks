# Phase 8 R1.1 DateOnly Compile Repair

Phase 8 R1 stopped during isolated staged compilation before any live overlay.

The converter resolves `DateOnly?` through `?? throw`, so the resulting local is already a non-nullable `DateOnly`. R1 incorrectly dereferenced that value with `.Value`. R1.1 passes the resolved `DateOnly` directly to `BankStatementLine`.

The existing converter regression test now also verifies the converted transaction date. Package preflight and the authoritative Phase 8 verifier reject the stale `date.Value` pattern before the Windows build.

No reconciliation behavior, schema v8 behavior, GUI contract, Smart Import boundary, accounting totals, or licensing scope changed.
