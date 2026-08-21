[CmdletBinding()]
param([string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)), [switch]$SkipWpfSmoke)
$ErrorActionPreference = 'Stop'
Write-Host '[REDIRECT] Phase 3C verifier is historical. Running current Phase 3D verifier.' -ForegroundColor Yellow
$current = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'Verify-Phase3D.ps1'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $current -ProjectRoot $ProjectRoot -SkipWpfSmoke:$SkipWpfSmoke
exit $LASTEXITCODE
