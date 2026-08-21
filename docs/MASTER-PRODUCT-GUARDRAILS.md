# ChurchBooks master product guardrails

## Product character

ChurchBooks is church-first, accountant-grade, lightweight, local-first, spreadsheet-native, premium, highly automated, smart, and auditable.

## Core principles

- A non-accountant must be able to use Simple mode safely.
- Accountants must have a professional Accountant mode with full journals, ledgers, adjustments, reconciliation, and audit detail.
- The application may suggest; it must not silently invent financial facts.
- Finalized reports are permanent snapshots. Corrections create adjustments or later revisions, not silent historical replacement.
- Multiple bank accounts maintain separate registers and reconciliations while posting to one unified General Ledger.
- Internal bank transfers never become fake income or fake expense.
- Bank account and fund are separate dimensions: where money sits is not necessarily what the money is designated for.
- Giving categories are unlimited and user-configurable.
- Detailed member giving can roll into annual member/household giving reports.
- XLS/XLSX/CSV imports must be previewed, mapped, validated, duplicate-checked, and approved before posting.
- The system learns approved mappings per source without silently guessing ambiguous data.
- Multi-currency support belongs in the core model, not as a late patch.
- Philippine BIR reporting is profile-driven and must be validated against current BIR requirements before compliance claims.
- UI text must be grammatically correct and consistent.
- UI controls must be compact but readable, with no cropped actions or unreachable totals.
- No mandatory cloud account is required to open local books.
- Licensing is postponed until late testing is complete.


## Approved GUI and configurable giving/funds contract

- `docs/reference/ChurchBooks-Approved-Dashboard.png` is the approved desktop GUI baseline. Prefer a complete one-window dashboard at normal desktop sizes, with intentional scrolling only as a small-screen fallback; controls must never be cropped or hidden.
- Giving Categories and Funds are data-driven. Operational forms, reports, and charts must load active records from storage and must not hard-code ministry names such as Missions, Building Fund, Love Offering, or Youth Ministry.
- Add/edit is direct. “Remove” means remove from active use while preserving historical references; restoration is supported.
- Tithes may be a starter/default setup record, but calculations and charts must not special-case it.
