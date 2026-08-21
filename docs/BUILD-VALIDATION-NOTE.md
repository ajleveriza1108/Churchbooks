# Build Validation Note

ChurchBooks Phase 2 R1 is packaged as a guarded forward update over the verified Phase 1 R1.3 foundation.

The Windows verification gate performs:

1. Windows PowerShell 5.1 parser validation of all installer and verification scripts.
2. ChurchBooks branding/resource checks, including the WPF application icon and navigation logo.
3. Phase 2 accounting-kernel source and project-boundary checks.
4. SQLite bootstrap checks, including non-pooled/non-shared-cache connections.
5. NuGet restore with vulnerability auditing enabled for transitive packages.
6. Release x64 build of the complete solution.
7. Full xUnit test execution, including double-entry domain validation and SQLite end-to-end posting.
8. Resolution check that vulnerable `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 is absent.
9. Guard check that licensing remains outside the implementation scope.

The package is intentionally not configured to push to GitHub automatically. The canonical repository is `ajleveriza1108/Churchbooks`; publication should happen only after the Windows verification gate passes on the target development machine.
