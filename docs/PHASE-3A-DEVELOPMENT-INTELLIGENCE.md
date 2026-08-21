# Phase 3A - Development Intelligence and Project Rules Foundation

## Goal
Make ChurchBooks safer and easier to evolve before implementing Phase 3B fund accounting.

## Added
- Native Cline workspace rules under `.clinerules/`.
- On-demand Cline skills under `.cline/skills/`.
- Cross-tool `AGENTS.md` and GitHub Copilot instruction bridge.
- Mandatory per-phase research register and dependency decision process.
- Microsoft CommunityToolkit.Mvvm adoption in the WPF shell.
- FsCheck.Xunit property-based accounting invariant tests (1,000 generated cases across four properties per test run at MaxTest=250 each).
- Frozen Phase 2 critical-source SHA baseline for this subphase.
- Phase 3B fund-accounting and Familiar Start design boundaries.

## Intentionally deferred
- FluentValidation: approved for the first clear application/input command boundary, not added unused to accounting core.
- Fund accounting database/schema changes: Phase 3B.
- Excel/CSV packages: Phase 7.
- PDF/report/chart dependencies: later reporting/UX phases.
- Licensing: final licensing phase only.

## Acceptance
Staged and live verification must pass Windows PowerShell 5.1 parser gate, dependency audit, frozen Phase 2 hash proof, Release x64 build, 23/23 tests, dependency resolution checks, and visible WPF smoke. Transactional installer semantics from Phase 2 FINAL remain mandatory.
