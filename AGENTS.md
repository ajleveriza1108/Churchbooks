# ChurchBooks Agent Instructions

ChurchBooks is a local-first native Windows accounting application. This repository contains financial/accounting logic; correctness and auditability outrank convenience.

## Mandatory routing
- Before accounting changes, read `.clinerules/20-accounting-invariants.md` and use `.cline/skills/accounting-invariants/SKILL.md`.
- Before installer, release, PowerShell, package, or rollback changes, read `.clinerules/30-release-safety.md` and use `.cline/skills/release-safety/SKILL.md`.
- Before fund-accounting work, use `.cline/skills/church-fund-accounting/SKILL.md`.
- Before onboarding/navigation/terminology/UI work, read `.clinerules/40-familiar-start-ux.md` and use `.cline/skills/familiar-start-ux/SKILL.md`.
- Before XLS/XLSX/CSV import work, use `.cline/skills/privacy-safe-import/SKILL.md`.
- At the start of every new phase or material phase update, use `.cline/skills/phase-research/SKILL.md` and update `docs/research/RESEARCH-REGISTER.md`.

## Frozen verified baseline
Phase 2 FINAL is the protected accounting-kernel ancestor: 19/19 tests, Release x64 build, WPF visible-window smoke, staged verification, transactional live update, and rollback architecture all passed on Windows on 2026-08-19. Phase 3A R1 is the verified development-intelligence ancestor: 23/23 total regression tests, rules/skills integration, MVVM/FsCheck wiring, WPF smoke, and transactional live verification passed on the same date. See `docs/baselines/PHASE-3A-R1-VERIFIED.md`. Phase 3C R1.1 is the verified UI/application ancestor at 66/66 with live WPF smoke and transactional verification; see `docs/baselines/PHASE-3C-R1.1-VERIFIED.md`.

## Non-negotiable rules
- Never weaken exact debit = credit enforcement.
- Never silently edit or delete posted accounting history; corrections use reversal/void/adjustment workflows.
- Bank Account is not Fund. Keep physical custody/location separate from designation/restriction.
- Do not introduce licensing/activation until the licensing phase.
- Do not require confidential financial files to leave the user's PC for core import/analysis.
- Preserve NuGet vulnerability auditing; never suppress security warnings to make a build pass.
- Windows installer/release PowerShell must pass Windows PowerShell 5.1 parser preflight.
- Every phase must pass its staged verifier before live mutation, then pass the same verifier against the live project.
- Failed staging leaves the live project untouched; failed commit/live verification rolls back.
- Tests must use machine-readable results (TRX/XML), not console-text scraping.
- Preserve ChurchBooks branding and native WPF/local-first architecture.
- Keep UI progressive: Simple / Pro / Accountant modes and Beginner / Guided / Experienced / Accountant help levels are separate concepts.
- Research current primary repos/docs/apps/programs every phase; record what was adopted, deferred, or rejected and why.
- A later phase may improve earlier-phase behavior when the change is research-backed, explicitly scoped, regression-tested, and preserves frozen accounting/release invariants.

## Current phase
Phase 3D extends the verified Phase 3C (66/66) Fund Manager/Familiar Start baseline with local fund reports and a read-only Integrity Center. Reports remain ledger-derived. Integrity scans may identify problems but must never auto-repair posted accounting. Restriction release is preview/review only in this phase and must not post a journal until the accounting treatment is explicitly validated. General financial statements/PDF packages remain later reporting work.
