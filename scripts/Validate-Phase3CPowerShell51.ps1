[CmdletBinding()]
param([string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))
$ErrorActionPreference = 'Stop'
Write-Host '[REDIRECT] Phase 3C parser gate is historical. Running current Phase 3D parser gate.' -ForegroundColor Yellow
$current = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'Validate-Phase3DPowerShell51.ps1'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $current -ProjectRoot $ProjectRoot
exit $LASTEXITCODE
