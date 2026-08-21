[CmdletBinding()]
param([string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))

& (Join-Path $PSScriptRoot 'Validate-Phase5PowerShell51.ps1') -ProjectRoot $ProjectRoot
exit $LASTEXITCODE
