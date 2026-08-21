---
name: release-safety
description: Build or repair ChurchBooks installers, release gates, PowerShell, staging, rollback, manifests, or verification. Use whenever packaging or upgrading ChurchBooks.
---

# Release Safety Skill

1. Read `.clinerules/30-release-safety.md`.
2. Windows PowerShell scripts must target Windows PowerShell 5.1 parser compatibility.
3. Stage first; never mutate the live project to discover whether the candidate builds.
4. Use TRX/XML for test counts; invoke tests from project files.
5. Preserve NuGet audit.
6. Self-test transaction success, idempotent rerun, injected post-manifest failure, and rollback.
7. Copy the canonical manifest last *inside* the rollback-capable transaction, then reverify live.
8. Failed staging = live untouched. Failed commit/live verification = automatic rollback.
9. Do not maintain an ever-growing matrix of predecessor hashes for ordinary evolving files.
