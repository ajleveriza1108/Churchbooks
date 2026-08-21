# Security and Privacy

- Financial records are confidential by default.
- Core workbook/import analysis must run locally and work without uploading source records.
- Logs/diagnostics must avoid donor/member names, bank numbers, full transaction descriptions, and cell values unless explicitly opted in.
- Future Privacy-Safe Structure Reports expose schema/headers/types/counts/mapping decisions but no private cell values by default.
- Secrets/credentials never belong in source control or diagnostic ZIPs.
- Least privilege: normal operation and installers should not require Administrator unless a future feature genuinely requires narrowly scoped elevation.
