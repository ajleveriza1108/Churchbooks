# Phase 1 - Windows Foundation

## Objective

Create a clean, lightweight Windows foundation that reuses the same general C#/.NET/WPF development stack as QuietShield Windows without importing QuietShield-specific networking, DNS, WFP, filtering, or service code.

## Phase 1 deliverables

- Traditional Visual Studio solution: `ChurchBooks.sln`
- Native WPF desktop shell targeting .NET 10 Windows
- Core domain project with multi-currency-safe value types
- SQLite data project and local database bootstrap
- Premium compact dashboard shell based on the approved ChurchBooks concept
- Responsive layout baseline and theme tokens
- Branding package: PNG, SVG, ICO
- Unit test projects
- Build and verification scripts
- Product guardrails and phase roadmap
- Licensing explicitly excluded

## Phase 1 acceptance gate

1. .NET 10 SDK detected.
2. `dotnet restore` succeeds.
3. `dotnet build -c Release` succeeds.
4. `dotnet test -c Release` succeeds.
5. WPF app opens without Administrator elevation.
6. Local database initializes under `%LOCALAPPDATA%\ChurchBooks\Data`.
7. Logo and Windows icon display correctly.
8. No licensing project or activation code exists.
9. No accounting transactions can yet be posted; Phase 1 is a foundation only.
10. Main window remains usable at the initial supported desktop baseline and is ready for the dedicated UI regression phase later.
