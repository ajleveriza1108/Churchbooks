# ChurchBooks Architecture

## Platform
Native Windows WPF on .NET 10, C#, MVVM, SQLite, local-first. No Electron/Chromium and no always-on cloud requirement.

## Layers
- `ChurchBooks.Core`: cross-cutting primitives such as money/currency/product identity.
- `ChurchBooks.Accounting`: accountant-grade domain rules, journals, Chart of Accounts, periods, GL calculations.
- `ChurchBooks.Data`: SQLite persistence and schema ownership.
- `ChurchBooks.App`: WPF presentation and application composition.
- `tests/*`: unit, integration, property/invariant, and release-regression coverage.

## Phase 3A development intelligence
Persistent instructions live in `AGENTS.md` and `.clinerules/`; focused on-demand Cline skills live in `.cline/skills/`. Research decisions are recorded in `docs/research/RESEARCH-REGISTER.md`.

## Dependency policy
UI-specific MVVM infrastructure may depend on CommunityToolkit.Mvvm. Accounting core remains independent of UI frameworks. Validation libraries belong at application/input boundaries, not inside mathematical accounting invariants. Property testing is test-only.

## Release model
Every phase is staged, security-audited, Release-x64-built, tested with machine-readable TRX verification, WPF-smoked, transactionally overlaid with manifest last, and reverified live. Failure before commit does not touch live; failure during/after commit rolls back managed files.


## Phase 5 contribution boundary

`People` owns identity. `Offerings` owns service batches and immutable donor-level contribution breakdowns. `Funds` owns designation/restriction. Phase 5 reporting derives totals from contribution records. No Phase 5 UI writes directly to journal tables; the bank/deposit posting bridge remains Phase 6.
