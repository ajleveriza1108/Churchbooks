# ChurchBooks Research Register

Research date: 2026-08-19. Recheck current versions and behavior at the start of every phase/update.

| Source | Current finding | ChurchBooks decision |
|---|---|---|
| Microsoft .NET Community Toolkit / CommunityToolkit.Mvvm | Microsoft/.NET Foundation maintained, WPF-compatible, platform-agnostic MVVM primitives and source generators. NuGet stable 8.4.2 (2026-03-25). | **Adopt now** in WPF presentation; migrate incrementally rather than rewrite the shell. |
| FsCheck / FsCheck.Xunit | Property-based testing integrates with xUnit; 3.3.4 (2026-07-25) includes a C# record-generation fix and works with xUnit 2.9-era integration. | **Adopt now, test-only** for accounting laws. |
| FluentValidation | v12 supports .NET 8+ including .NET 10; strongly typed rules. | **Approved/defer** until a clear application/input command boundary exists; do not add unused dependency to accounting core. |
| Cline official Rules docs | Workspace `.clinerules/` and `AGENTS.md` are supported persistent instruction formats. | **Adopt now**. |
| Cline official Skills docs | Workspace skills under `.cline/skills/<name>/SKILL.md` load on demand using progressive disclosure. | **Adopt now** for focused accounting/release/fund/UX/import/research skills. |
| GitHub Copilot custom instructions | `.github/copilot-instructions.md` and `AGENTS.md` are supported repository instructions. | **Adopt lightweight bridge** for cross-tool portability. |
| QuickBooks nonprofit fund accounting (Intuit, updated 2026-05-26) | QBO suggests Classes and bank subaccounts to track nonprofit funds. | **Adapt concept, improve model**: ChurchBooks uses a native Fund dimension and keeps Bank Account != Fund; migration assistant can interpret QB Classes rather than copying the workaround. |
| Xero Tracking Categories | Tracking categories represent business dimensions; current limits include four categories total and two active at once. | **Reference only**: support familiar alias/migration concepts but do not inherit artificial dimension limits. |
| ChurchTrac accounting support | Strong beginner terminology distinguishes Bank Accounts, Funds, and Categories; funds can share a bank account. ChurchTrac states its accounting is single-entry. | **Adapt UX terminology only**; ChurchBooks remains true double-entry. |
| PowerChurch Plus | Church-focused workflows include accounting funds, donor restrictions/release, transfers between funds, posted corrections, audit log, budgets, reconciliation, and previewing unposted transactions in reports. | **Reference for Phase 3B-3D requirements**; implement independently. |
| ChurchCRM GitHub | Active MIT church-management project covering membership, groups, events and finances; release 7.5.1 in July 2026. | **Reference later** for Phase 4 member/household domain; do not turn Phase 3 into a full ChMS. |
| FASB NFP guidance / ASU 2016-14 | NFP reporting distinguishes resources with and without donor restrictions; authoritative requirements belong to FASB Codification and jurisdiction/context matters. | **Design terminology with CPA-validation flags**; never claim blanket US-GAAP/BIR compliance without validation. |

## Primary URLs
- https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- https://github.com/CommunityToolkit/dotnet
- https://www.nuget.org/packages/CommunityToolkit.Mvvm/
- https://fscheck.github.io/FsCheck/
- https://github.com/fscheck/FsCheck
- https://www.nuget.org/packages/FsCheck.Xunit/
- https://docs.fluentvalidation.net/en/latest/
- https://docs.cline.bot/customization/cline-rules
- https://docs.cline.bot/customization/skills
- https://docs.github.com/en/copilot/how-tos/configure-custom-instructions-in-your-ide/add-repository-instructions-in-your-ide
- https://quickbooks.intuit.com/learn-support/en-us/help-article/accounting-standards/fund-accounting-non-profits/L833HXXTo_US_en_US
- https://central.xero.com/0/article/Set-up-tracking-categories
- https://www.churchtrac.com/support/accounting/using-the-accounting-screen
- https://www.powerchurch.com/training/videos/series_detail.php?id=15
- https://github.com/ChurchCRM/CRM
- https://fasb.org/standards/accounting-standard-updates?year=2016


## Phase 3B update - 2026-08-19

| Source | Current finding | ChurchBooks Phase 3B decision |
|---|---|---|
| QuickBooks Online nonprofit fund accounting (Intuit, updated 2026-05-26) | QBO recommends Classes and bank subaccounts to track nonprofit funds and describes blocking spending after a fund is empty. | **Adapt familiarity, reject workaround architecture.** Native Fund dimension; Bank Account != Fund; explicit overspend policy. |
| Xero Tracking Categories | Xero models dimensions using tracking categories/options, currently with four total categories and only two active at once. | **Migration/search alias reference only.** ChurchBooks does not inherit artificial fund-dimension limits. |
| ChurchTrac accounting terminology | Distinguishes Bank Accounts, Funds, and Categories and explicitly states a fund can span bank accounts; its accounting is single-entry. | **Adopt mental model, improve engine.** Keep beginner terminology while using true double-entry and per-fund balancing. |
| Aplos fund accounting | Emphasizes restricted/unrestricted balances, fund-specific statements, program/grant/project views, and audit-ready fund tagging. | **Adopt reporting requirements later.** Phase 3B establishes reliable fund tags/balances that Phase 3D reports can consume. |
| PowerChurch donor restrictions + support forum | Uses dedicated donor-restriction setup and explicit release-from-restriction workflows; forum cases show setup errors can produce incorrect releases. | **Do not overload ordinary transfers.** Dedicated restriction-release workflow stays explicit and requires CPA/jurisdiction validation. |
| PowerChurch Fund Accounting v15 training | Covers accounting funds, transfers between funds, posted corrections, donor restrictions/releases, reconciliation, budgets and audit log. | **Reference workflow coverage.** Phase 3B implements fund kernel/transfer persistence; later phases add UI, reports, corrections and audit UX. |

Phase 3B implementation hardening: migration history is treated as audit evidence. Known migration keys must retain their exact schema versions; contradictory history stops and rolls back rather than being ignored.

### Phase 3B source URLs
- https://quickbooks.intuit.com/learn-support/en-us/help-article/accounting-standards/fund-accounting-non-profits/L833HXXTo_US_en_US
- https://central.xero.com/0/article/Set-up-tracking-categories
- https://www.churchtrac.com/support/accounting/basic-terminology-and-examples
- https://www.aplos.com/fund-accounting-software
- https://www.powerchurch.com/support/554/1/setting-up-and-tracking-donor-restrictions
- https://www.powerchurch.com/forum/viewtopic.php?t=36764
- https://www.powerchurch.com/training/videos/watch.php?id=25


## Phase 3C update - 2026-08-19

| Source | Current finding | ChurchBooks Phase 3C decision |
|---|---|---|
| Microsoft WPF data binding / commanding | Native WPF provides separation between UI and logic through data binding and command semantics. | **Adopt directly.** Continue CommunityToolkit.Mvvm commands/bindings; keep code-behind limited to window startup/bootstrap. |
| Microsoft WPF DataGrid virtualization | Row virtualization is on by default; column virtualization is opt-in and improves large-grid efficiency. | **Enable both explicitly** on Fund Manager and use recycling virtualization. |
| Microsoft Windows accessibility guidance | Keyboard access, visible focus, programmatic names/UI Automation, DPI scaling, and non-color-only meaning are core accessibility practices. | **Adopt progressively.** Phase 3C adds automation names and keyboard-friendly standard controls; continue later audits. |
| WPF UI 4.3.0 repository (2026-05-04) | Current MIT Fluent-style WPF library with .NET 10 support, but 2026 open issues include theme/control regressions. | **Evaluate/defer.** Do not add framework risk during the first live accounting workspace; preserve ChurchBooks-owned native theme abstractions. |
| FluentValidation 12.1.1 | Current stable package, Apache-2.0, supports .NET 8+ including .NET 10. | **Adopt now at input boundary only.** Do not move accounting invariants out of ChurchBooks.Accounting. |
| QuickBooks nonprofit fund accounting | QuickBooks uses Class tracking and bank subaccounts for nonprofit fund tracking. | **Familiarity alias only.** Search for Class routes to native Funds; do not copy the workaround architecture. |
| Xero tracking categories | Tracking categories are dimensions used for departments/cost centres/locations. | **Familiarity alias only.** Search for Tracking Category routes to Funds when appropriate, with explanation rather than silent equivalence. |
| ChurchTrac terminology and setup | Separates Bank Accounts, Funds, and Categories and teaches these concepts explicitly. | **Adopt the teaching pattern.** ChurchBooks explains where-held vs designated-for vs why-moved while retaining true double-entry. |
| Aplos | Emphasizes fund-filtered statements and board-ready nonprofit reporting. | **Phase 3D input.** Phase 3C exposes clean fund master data and derived balances that later reports consume. |

### Phase 3C source URLs
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.datagrid.enablerowvirtualization?view=windowsdesktop-10.0
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.datagrid.enablecolumnvirtualization?view=windowsdesktop-10.0
- https://learn.microsoft.com/en-us/windows/apps/develop/accessibility
- https://github.com/lepoco/wpfui
- https://github.com/lepoco/wpfui/releases/tag/4.3.0
- https://www.nuget.org/packages/FluentValidation/12.1.1
- https://docs.fluentvalidation.net/en/latest/
- https://quickbooks.intuit.com/learn-support/en-us/help-article/accounting-standards/fund-accounting-non-profits/L833HXXTo_US_en_US
- https://central.xero.com/0/article/Set-up-tracking-categories
- https://www.churchtrac.com/support/accounting/basic-terminology-and-examples
- https://www.aplos.com/


## Phase 3D update - 2026-08-19

| Source | Current finding | ChurchBooks Phase 3D decision |
|---|---|---|
| Aplos nonprofit accounting | Current product emphasizes fund-filtered Balance Sheet/Income Statement views, custom nonprofit reporting, and board-ready exports. | **Adopt the reporting expectation, not the cloud dependency.** Phase 3D adds local ledger-derived fund balance/activity reports and CSV export; broader statements remain Phase 12. |
| ChurchTrac Accounting Reports | Fund/category reports, transaction search, CSV export, budget reports, and reconciliation/audit-support views are prominent. | **Adapt discoverability.** Reports and Integrity become first-class navigation/search destinations while ChurchBooks remains true double-entry. |
| GnuCash stable report code/repository | Mature reports expose explicit report dates, account selection, display options, zero-balance handling, and detail links. | **Reference architecture.** Keep report inputs explicit and deterministic; do not copy GPL implementation. |
| PowerChurch Fund Accounting v15 training | Current training separates reports, bank reconciliation, donor restrictions/releases, posted corrections, audit log, and unposted workflows. | **Preserve workflow separation.** Restriction release is not treated as an ordinary fund transfer or silently posted by Phase 3D. |
| PowerChurch support forum | Real support cases recommend dedicated donor-restriction release workflows and audit trail rather than ad-hoc reclassification. | **Use as failure-mode research.** ChurchBooks preview requires evidence and accountant review; no automatic release journal. |
| FASB NFP contribution/restriction guidance | Donor restrictions, conditions, and release treatment depend on the underlying agreement and applicable accounting policy. | **Fail safe.** Phase 3D reviews readiness only; authoritative release posting stays behind CPA/framework validation. |

### Phase 3D source URLs
- https://www.aplos.com/
- https://www.churchtrac.com/support/accounting/accounting-reports
- https://www.churchtrac.com/support/accounting/search-transactions
- https://github.com/Gnucash/gnucash
- https://github.com/Gnucash/gnucash/blob/stable/gnucash/report/reports/standard/balance-sheet.scm
- https://www.powerchurch.com/training/videos/watch.php?id=25
- https://www.powerchurch.com/forum/viewtopic.php?t=18680
- https://fasb.org/page/PageContent?bcpath=tff&pageId=%2Farchive%2Ffasb-staff-issuances%2Ffifjune2018asu-201808notforprofit-entities-topic-958.html
