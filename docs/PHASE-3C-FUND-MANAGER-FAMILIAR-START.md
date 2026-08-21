# Phase 3C - Fund Manager and Familiar Start

Status: current Phase 3C implementation.

## Purpose
Phase 3C turns the verified Phase 3B fund kernel into a usable native-WPF Fund Manager without changing the frozen Phase 2 journal/ledger invariants.

## User experience
- Fund Manager is a real local database workspace, not sample-only UI.
- Add, edit, archive, reactivate, search, filter, and refresh funds.
- Balances are read from the Phase 3B fund-aware ledger and are never editable shadow totals.
- Archiving never deletes history. A non-zero balance requires a second explicit archive action as an inline confirmation.
- New users can create a General Fund from the empty state, but ChurchBooks never silently creates or posts financial activity.
- DataGrid row and column virtualization are enabled for scalable accounting lists.

## Familiar Start
Workspace Mode and Help Level are intentionally separate:
- Workspace Mode: Simple, Pro, Accountant.
- Help Level: Beginner, Guided, Experienced, Accountant.

The defaults are Simple + Guided and are stored locally under the user's LocalAppData ChurchBooks folder. No cloud is required.

Global search recognizes familiar terminology including QuickBooks Class and Xero Tracking Category and routes those terms to the native ChurchBooks Funds workspace. ChurchBooks then explains that:
- Bank Account = where money is held.
- Fund = what money is designated for.
- Account/category = why money moved.

## Validation
FluentValidation 12.1.1 is introduced only at the WPF input boundary. It validates fund code/name/purpose before a domain Fund is created. Duplicate fund code/name enforcement remains in FundManagementService and is case-insensitive.

## Dependency decision
WPF UI 4.3.0 was researched but not adopted in this phase. The project is active and MIT licensed, but its 2026 issue tracker contains open theme/control regressions. ChurchBooks keeps its proven native WPF shell while adopting Microsoft WPF binding, commanding, virtualization, keyboard, and accessibility guidance directly.

## Phase boundaries
Phase 3C does not implement donor restriction release accounting, reports, member giving, bank reconciliation, or licensing. The verified internal fund-transfer engine remains available in Phase 3B, but its end-user transfer screen is intentionally deferred until ChurchBooks has safe bank-account, posting-period, and interfund-clearing-account selection workflows instead of inventing placeholder accounting choices. These remain later phases.
