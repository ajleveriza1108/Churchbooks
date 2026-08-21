# Privacy-Safe Smart Import Design

ChurchBooks will inspect XLS/XLSX/CSV locally. Confidential financial records do not need to be uploaded to an external AI service for core import.

Pipeline: read-only source -> workbook/sheet/header detection -> column profiling -> mapping suggestions -> structure fingerprint -> duplicate/anomaly checks -> preview -> user approval -> import session -> accounting validation -> posting.

A future Privacy-Safe Structure Report may contain sheet names, header names (optionally anonymized), row/column counts, detected data types, date/currency formats, formula presence, mapping decisions and parser errors, but no cell values by default.
