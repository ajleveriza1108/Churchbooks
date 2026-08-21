# Phase 2 FINAL - Transactional Finalization

Phase 2 FINAL replaces repair-chain installers with a staged transactional updater.

## Guarantees

- No live source mutation before the canonical payload passes restore, build, tests, and WPF smoke in staging.
- No predecessor-hash matrix is required for ordinary managed files.
- Existing managed files are backed up before replacement.
- New managed files are tracked so rollback can remove them.
- Unknown/unmanaged files are preserved.
- Post-commit hash equality and full live verification are required.
- Canonical manifest is written last.
- A failed commit triggers automatic source rollback.
- Legacy Phase 1/Phase 2 BAT/PS1 launchers are replaced by inert retired stubs only after staged verification succeeds.

This architecture is intended to remain the release baseline for later ChurchBooks phases.
