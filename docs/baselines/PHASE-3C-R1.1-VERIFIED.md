# Phase 3C R1.1 Verified Baseline

Verified on Windows: 2026-08-19.

- Windows PowerShell 5.1 parser gate: PASS
- NuGet restore/vulnerability audit: PASS
- Release x64 build: PASS
- ChurchBooks.Accounting.Tests: 39/39 PASS
- ChurchBooks.Core.Tests: 4/4 PASS
- ChurchBooks.Data.Tests: 14/14 PASS
- ChurchBooks.App.Tests: 9/9 PASS
- Total regression: 66/66 from machine-readable TRX
- Visible WPF main-window smoke: PASS
- Staged candidate: PASS before live mutation
- Transactional live overlay: PASS
- Manifest-last verification: PASS
- Live re-verification: PASS
- Rollback architecture: preserved

Phase 3D may extend reporting/integrity surfaces but must preserve the verified Phase 3B fund engine and Phase 3C Fund Manager/Familiar Start behavior.
