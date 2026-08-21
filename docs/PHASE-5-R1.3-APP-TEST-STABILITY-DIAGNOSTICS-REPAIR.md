# ChurchBooks Phase 5 R1.3 — App-test stability and diagnostics repair

R1.2 proved the Phase 5 product candidate in isolated staging with a clean Release build, 125/125 TRX tests, and WPF smoke. The immediate live re-verification then produced a one-run-only failure in `GivingWorkspace_InitializesSchemaFiveAndCommonChartDefaults` and correctly rolled back.

This revision does not change ChurchBooks accounting, offerings, analytics, approved GUI, Giving Categories, Funds, or schema-v5 product behavior.

It hardens only the App test/release proof path:

- App.Tests run without xUnit parallelization because they exercise WPF-facing ViewModels plus native SQLite resources.
- the offering workspace integration test uses bounded Windows-safe retry cleanup for temporary SQLite files;
- a failed test run prints the failing TRX test name, message, and stack trace before the verifier exits nonzero;
- all prior fail-closed build/TRX/WPF smoke and transactional rollback rules remain mandatory;
- authoritative gate remains 125/125 plus WPF smoke PASS in both staged and live verification.

Phase 6 remains blocked until the complete R1.3 gate passes twice: staged candidate and installed live project.
