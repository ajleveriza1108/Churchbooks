# Release and Transaction Safety

- Build a complete canonical candidate in isolated staging.
- Parser, restore/audit, Release x64 build, tests, and WPF smoke must pass before live mutation.
- Overlay only managed files; preserve unrelated local files, `.git`, databases, and backup history.
- Back up every replaced managed file before copy.
- Canonical manifest participates in the rollback-capable transaction and is copied last.
- Reverify the complete live project before final success receipt.
- Roll back created/replaced managed files on commit/live-verification failure.
- Test projects run from `.csproj`; verify TRX/XML counters; do not scrape human-readable summaries or guess DLL paths.
- Phase N must not own or reinstall obsolete Phase N-1 installers.
