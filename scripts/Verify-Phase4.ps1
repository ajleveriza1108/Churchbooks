[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
    [switch]$SkipWpfSmoke,
    [switch]$StaticOnly
)

$target = Join-Path $PSScriptRoot 'Verify-Phase5.ps1'
$arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $target, '-ProjectRoot', $ProjectRoot)
if ($SkipWpfSmoke) { $arguments += '-SkipWpfSmoke' }
if ($StaticOnly) { $arguments += '-StaticOnly' }
& powershell.exe @arguments
exit $LASTEXITCODE
