# ChurchBooks roadmap

## Verified / frozen foundation
- Phase 1 - Windows foundation: verified.
- Phase 2 - Double-entry accounting kernel: frozen.
- Phase 3A-3D - Development foundation, fund accounting, Fund Manager, fund reports and Integrity foundations: verified/protected.
- Phase 4 - People, donors, households, configurable giving categories: verified/protected.
- Phase 5 - Offering/service batches, individual contribution breakdowns and analytics: frozen R1.3.
- Phase 6 - Personalized setup, multiple bank accounts and controlled deposit-to-GL bridge: frozen R1.2.
- Phase 7 - Smart CSV/XLS/XLSX import staging: frozen R1.3.
- Phase 8 - Bank reconciliation: frozen R1.1.
- Phase 9 - Adaptive import templates + conservative identity resolution: frozen R1.2.

## Current implementation
- **Phase 10 - Vendors + direct bank-paid expenses: CURRENT.**
  - Vendor master data with archive/restore history protection.
  - Multi-line direct expenses.
  - Active Expense-account + Fund validation.
  - Bank-currency validation.
  - Fund-aware balanced journal preview and explicit posting.
  - No Smart Import auto-post and no silent vendor creation.

## Planned accounting modules
- Phase 11 - Bills, accounts payable, partial payments and purchasing workflow.
- Phase 12 - Income, invoices/receivables and customer/organization receivables.
- Phase 13 - Budgets and fixed assets.
- Phase 14 - Ready service, monthly, quarterly, annual and custom report packages.
- Phase 15 - Excel-style analysis workspace and report builder.
- Phase 16 - Smart automation and anomaly detection.
- Phase 17 - Accountant and auditor tools.
- Phase 18 - Security, privacy, approvals and segregation of duties.
- Phase 19 - Backup, recovery and immutable report snapshots.
- Phase 20 - Performance and premium UI hardening.
- Phase 21 - Optional external integrations.
- Phase 22 - CPA/auditor/accounting validation gate.
- Phase 23 - Installer/updater hardening and only then licensing.
- Phase 24 - Advanced optional modules.

## Durable GUI and accounting contracts
- `docs/reference/ChurchBooks-Approved-Dashboard.png` remains the approved desktop GUI baseline.
- Important controls must not be cropped or hidden; normal desktop use should fit in one window with scrolling only when useful.
- Giving Categories and Funds remain data-driven; ministry names are not hard-coded.
- Smart Import analyzes, maps, reviews and stages. It never posts accounting automatically.
- Accounting posting is explicit, balanced, auditable and protected from duplicates.
