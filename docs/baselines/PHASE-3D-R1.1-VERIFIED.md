# Phase 3D R1.1 Verified Baseline

Verified on Windows: 2026-08-19.

- Windows PowerShell 5.1 parser gate: PASS
- NuGet restore/vulnerability audit: PASS
- Release x64 build: PASS
- ChurchBooks.Accounting.Tests: 49/49 PASS
- ChurchBooks.Core.Tests: 4/4 PASS
- ChurchBooks.Data.Tests: 20/20 PASS
- ChurchBooks.App.Tests: 13/13 PASS
- Total regression: 86/86 from machine-readable TRX
- Visible WPF main-window smoke: PASS
- Staged candidate: PASS before live mutation
- Transactional live overlay: PASS
- Manifest-last verification: PASS
- Live re-verification: PASS
- Rollback architecture: preserved

Phase 4 may add people, households, donors, and giving categories, but must not reopen the verified accounting, fund-reporting, or Integrity Center behavior.
