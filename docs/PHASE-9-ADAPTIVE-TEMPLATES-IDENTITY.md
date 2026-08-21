# Phase 9 - Adaptive Import Templates + Identity Resolution

Phase 9 builds on the Windows-verified Phase 8 R1.1 baseline. It does not change the frozen accounting, fund, offering, banking, Smart Import staging, or reconciliation rules.

## Standard templates without lock-in

ChurchBooks ships four optional CSV starting templates: People Directory, Giving, Bank Statement, and General Ledger. A church can ignore them and import an existing CSV, XLS, or XLSX instead.

Adaptive Smart Import scans common tabular layouts, can detect a header row within the first 25 rows, selects the strongest worksheet candidate when a workbook contains multiple sheets, normalizes common header aliases, suggests mappings, and classifies the file as People Directory, Giving, Bank Statement, General Ledger, or Unknown.

No claim is made that every arbitrary spreadsheet can be interpreted without review. If a layout is unfamiliar, ambiguous, merged/non-tabular, or uses unknown terminology, ChurchBooks stays review-first and asks the user to map the columns rather than guessing silently.

## Multiple source profiles without clutter

A church can save many named source profiles. Profiles remember the exact normalized header signature, approved column mapping, detected purpose, and default role for new people. When a file is opened, ChurchBooks shows only profiles whose signature matches that file. One exact match can be applied automatically; multiple matches are presented as a small relevant choice instead of a global template list.

## Person identity resolution

Every ChurchBooks person retains a stable internal Person ID. Names are never used as automatic identity keys.

Resolution order is conservative:
1. remembered external person ID within the selected source profile;
2. exact member/envelope number;
3. exact email;
4. normalized phone, including country-prefix/leading-zero equivalents when the final 10 digits agree;
5. name-only similarity is review-only.

If strong identifiers point to different people, the row is Ambiguous and no merge or update occurs. If the same strong identifier is repeated on multiple imported rows, those rows are also Ambiguous.

New people require a usable first and last name plus Member, Donor, or Both status, either from mapped columns or a user-selected source default. Register Safe People is an explicit action. Existing person profiles are never overwritten automatically. Existing households can be linked by one exact household-name match; Phase 9 does not silently create households.

## Safety boundary

Adaptive Smart Import remains staging-first. It does not automatically post journals, bank transactions, giving, reconciliation adjustments, or person merges. Phase 8 exact-zero bank reconciliation remains frozen and protected. Licensing remains deferred.
