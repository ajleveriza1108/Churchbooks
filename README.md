# ChurchBooks

ChurchBooks is a local-first native Windows accounting application designed to combine spreadsheet familiarity with accountant-grade double-entry safeguards and church fund-accounting workflows.

## Current phase
**Phase 9 R1 - Adaptive Import Templates + Identity Resolution**

Phase 8 R1.1 Bank Reconciliation is the frozen Windows-verified predecessor. Phase 9 adds optional ChurchBooks templates, adaptive CSV/XLS/XLSX interpretation, multiple saved source profiles per organization, conservative person identity resolution, and explicit safe registration. Unknown or ambiguous layouts remain review-first; Smart Import does not automatically post journals, deposits, giving, reconciliation adjustments, or merge people.

## Phase 9 capabilities
- Optional ChurchBooks standard templates for People Directory, Giving, Bank Statement, and General Ledger.
- Adaptive worksheet/header detection for common tabular CSV/XLS/XLSX exports.
- Header aliases and purpose detection for People, Giving, Bank Statement, and General Ledger sources.
- Multiple source profiles per church, automatically narrowed by source signature so users are not shown an overwhelming template list.
- External person IDs remembered per source profile.
- Exact member/envelope number, email, and normalized phone resolution before any name similarity review.
- Name-only similarity never auto-merges people.
- Conflicting strong identifiers fail closed and require review.
- New people are registered only through an explicit safe-registration action; existing profiles and households are not silently overwritten or created.
- Schema v9 remains local-first and additive.

## AI development guidance
- `AGENTS.md` - cross-tool master rules and routing.
- `.clinerules/` - persistent Cline workspace rules.
- `.cline/skills/` - focused on-demand skills for accounting, releases, funds, UX, imports, and phase research.
- `.github/copilot-instructions.md` - lightweight cross-tool bridge.
- `docs/research/RESEARCH-REGISTER.md` - current research decisions that must be refreshed each phase/update.

## Release architecture
ChurchBooks uses transactional staging: complete candidate -> PowerShell 5.1 parser -> NuGet vulnerability audit -> Release x64 build -> machine-readable TRX tests -> visible WPF smoke -> transactional managed-file overlay with manifest last -> complete live re-verification -> receipt. Staging failure leaves live untouched; commit/live-verification failure rolls back.

## Canonical project location
`D:\Windows Projects\ChurchBooks`

## Important
Licensing/activation remains intentionally out of scope until the final licensing phase. Dedicated donor-restriction release behavior and jurisdiction-specific compliance claims remain deferred until explicit CPA/regulatory validation.
