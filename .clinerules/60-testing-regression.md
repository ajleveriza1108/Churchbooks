# Testing and Regression

- All previous regression tests continue to run in every later phase.
- Add example tests for known workflows and property-based tests for mathematical/accounting laws.
- Use machine-readable TRX results for release gates.
- A verifier is not allowed to declare success solely from static source markers when a runtime test can prove the behavior.
- If a verifier itself fails after tests pass, fix the verifier; do not weaken the product tests.
- New phase tests must include negative/error paths and rerun/idempotency where applicable.
- Static source audits must never be described as compiler verification; only a successful `dotnet build` proves C# namespace/type resolution.
- New C# files that use external-package types must use explicit namespaces (or fully qualified types), and release preflight should guard the known external symbols introduced by that phase.


## Explicit framework namespace rule for WPF App code

Do not rely on WPF App implicit usings for System.IO. Any App source that uses Path, File, Directory, IOException, or related IO types must include `using System.IO;` or fully qualify `System.IO.*`. Static text audits do not count as compiler verification; the Windows `dotnet build` gate remains authoritative.
