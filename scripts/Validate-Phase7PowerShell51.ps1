[CmdletBinding()]
param([string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path

Write-Host '============================================================'
Write-Host 'ChurchBooks Phase 7 - Windows PowerShell 5.1 Parser Gate'
Write-Host '============================================================'

$relativeFiles = @(
    'Install-Phase1.ps1',
    'Install-Phase2.ps1',
    'scripts\Run-ChurchBooks.ps1',
    'scripts\Validate-Phase2FinalPowerShell51.ps1',
    'scripts\Verify-Phase2Final.ps1',
    'scripts\Validate-Phase3APowerShell51.ps1',
    'scripts\Verify-Phase3A.ps1',
    'scripts\Validate-Phase3BPowerShell51.ps1',
    'scripts\Verify-Phase3B.ps1',
    'scripts\Validate-Phase3CPowerShell51.ps1',
    'scripts\Verify-Phase3C.ps1',
    'scripts\Validate-Phase3DPowerShell51.ps1',
    'scripts\Verify-Phase3D.ps1',
    'scripts\Validate-Phase4PowerShell51.ps1',
    'scripts\Verify-Phase4.ps1',
    'scripts\Validate-Phase5PowerShell51.ps1',
    'scripts\Verify-Phase5.ps1',
    'scripts\Validate-Phase6PowerShell51.ps1',
    'scripts\Verify-Phase6.ps1',
    'scripts\Validate-Phase7PowerShell51.ps1',
    'scripts\Verify-Phase7.ps1'
)

foreach ($relativePath in $relativeFiles) {
    $path = Join-Path $ProjectRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) { throw ('Required PowerShell file is missing: ' + $relativePath) }
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
    if ($errors -and $errors.Count -gt 0) {
        foreach ($parseError in $errors) {
            Write-Host ('[PARSER ERROR] ' + $relativePath + ' line ' + $parseError.Extent.StartLineNumber + ', column ' + $parseError.Extent.StartColumnNumber + ': ' + $parseError.Message) -ForegroundColor Red
        }
        throw ('Windows PowerShell 5.1 parser gate failed: ' + $relativePath)
    }
    Write-Host ('[PASS] ' + $path) -ForegroundColor Green
}

Write-Host '[PASS] All ChurchBooks Phase 7 PowerShell files passed the Windows PowerShell 5.1 parser gate.' -ForegroundColor Green
exit 0
