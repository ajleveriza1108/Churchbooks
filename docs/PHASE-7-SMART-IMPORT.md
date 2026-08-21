# Phase 7 - Smart Spreadsheet Import

Phase 7 adds preview-first local import for CSV, legacy XLS, and XLSX while keeping Phase 2-6 accounting/banking behavior frozen.

## Safety contract
- Reading/analyzing a spreadsheet never posts a journal, bank deposit, contribution, fund transfer, or reconciliation adjustment.
- The user reviews inferred column mappings and can change any mapping before approval.
- Mapping templates are remembered by normalized header signature.
- Duplicate fingerprints include mapped roles and values, so signed Amount and separate Debit/Credit conventions are not conflated.
- Approved rows go only to `import_staged_rows`; conversion/posting is a later explicit workflow.
- Files are read locally. Spreadsheet limits are 10,000 data rows, 100 columns, and 4,000 characters per cell.
- Church-specific Giving Categories and Funds remain data-driven and are never hard-coded by Smart Import.

## Supported mapping roles
Date, Amount, Debit, Credit, Description, Reference, Payee, Person Name, Member Number, Fund Code, Giving Category Code, Bank Account, Account Code, Memo, Ignore.

## GUI
Smart Import follows the approved ChurchBooks Pro compact desktop baseline. Mapping and preview share the available window; scrolling is limited to data grids where dense rows require it.
