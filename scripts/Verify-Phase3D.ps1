[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
    [switch]$SkipWpfSmoke
)
$ErrorActionPreference = 'Stop'
Write-Host '[REDIRECT] Phase 3D verifier is historical. Running current Phase 4 verifier.' -ForegroundColor Yellow
$current = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'Verify-Phase4.ps1'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $current -ProjectRoot $ProjectRoot -SkipWpfSmoke:$SkipWpfSmoke
exit $LASTEXITCODE
