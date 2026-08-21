---
name: privacy-safe-import
description: Design or implement local XLS/XLSX/CSV smart import, workbook analysis, mapping, diagnostics, privacy-safe reports, duplicate detection, or migration from spreadsheets/accounting apps.
---

# Privacy-Safe Import Skill

- Source workbooks are read-only inputs.
- Default processing is local/offline.
- Detect sheets, headers, types, dates, amount/debit/credit conventions, totals/subtotals, formulas, blanks, repeated IDs, and likely donor/fund/account fields.
- Never post directly from raw inference. Flow: inspect -> map -> preview -> user approval -> import session -> accounting validation -> post.
- Remember approved mappings by structural fingerprint, not confidential cell values.
- Duplicate/anomaly detection happens before posting.
- Diagnostics default to schema/header/type/count/parser metadata without private values.
- Preserve unrelated notes/metadata and the original source file.
