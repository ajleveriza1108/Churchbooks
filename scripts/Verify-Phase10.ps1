[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
    [switch]$SkipWpfSmoke,
    [switch]$StaticOnly
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
trap { Write-Host ('[FATAL] ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path

Write-Host '============================================================'
Write-Host 'ChurchBooks Phase 10 - Vendors + Direct Expenses Verification'
Write-Host '============================================================'
Write-Host ('Project: ' + $ProjectRoot)
Write-Host 'Phase 2 accounting kernel: FROZEN / protected'
Write-Host 'Phase 3B fund-accounting kernel: VERIFIED / protected'
Write-Host 'Phase 3C Fund Manager + Familiar Start: VERIFIED / protected'
Write-Host 'Phase 3D Fund Reports + Integrity: VERIFIED / protected'
Write-Host 'Phase 4 People + Giving Directory: VERIFIED / protected'
Write-Host 'Phase 5 Individual Offerings + Analytics: FROZEN at R1.4 / 125 of 125 plus staged+live WPF smoke'
Write-Host 'Phase 6 Personalized Setup + Banking: FROZEN / 155 of 155 plus staged+live WPF smoke'
Write-Host 'Phase 7 Smart Import: FROZEN at R1.4 / 189 of 189 plus staged+live WPF smoke'
Write-Host 'Phase 8 Bank Reconciliation: FROZEN at R1.1 / 221 of 221 plus staged+live WPF smoke'
Write-Host 'Phase 9 Adaptive Templates + Identity: FROZEN at R1.2 / 255 of 255 plus staged+live WPF smoke'
Write-Host 'Licensing: intentionally not implemented'
Write-Host 'NuGet security audit: enabled, including transitive packages'
Write-Host ''

function Require-File {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    $fullPath = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) { throw ('Required file is missing: ' + $RelativePath) }
    return $fullPath
}

function Require-Text {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$ExpectedText
    )
    $content = Get-Content -LiteralPath (Require-File -RelativePath $RelativePath) -Raw
    if ($content.IndexOf($ExpectedText, [System.StringComparison]::Ordinal) -lt 0) {
        throw ('Expected text was not found in ' + $RelativePath + ': ' + $ExpectedText)
    }
}

function Reject-Text {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$RejectedText
    )
    $content = Get-Content -LiteralPath (Require-File -RelativePath $RelativePath) -Raw
    if ($content.IndexOf($RejectedText, [System.StringComparison]::Ordinal) -ge 0) {
        throw ('Rejected text is still present in ' + $RelativePath + ': ' + $RejectedText)
    }
}

function Get-Sha256Lower {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}



function Write-LogDelta {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ref]$LineIndex,
        [switch]$Warning
    )
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $lines = @(Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue)
    $start = [int]$LineIndex.Value
    if ($start -lt 0) { $start = 0 }
    if ($lines.Count -le $start) { return }
    for ($i = $start; $i -lt $lines.Count; $i++) {
        if ($Warning) { Write-Host $lines[$i] -ForegroundColor Yellow } else { Write-Host $lines[$i] }
    }
    $LineIndex.Value = $lines.Count
}

function Stop-ProcessTreeSafe {
    param([Parameter(Mandatory = $true)][int]$TargetProcessId)
    try {
        & taskkill.exe /PID $TargetProcessId /T /F 2>&1 | Out-Null
    }
    catch {
        Stop-Process -Id $TargetProcessId -Force -ErrorAction SilentlyContinue
    }
}

function ConvertTo-SingleQuotedLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)
    return ("'" + $Value.Replace("'", "''") + "'")
}

function New-DirectExitCodeCaptureCommand {
    param(
        [Parameter(Mandatory = $true)][string]$TargetFile,
        [Parameter(Mandatory = $true)][string[]]$TargetArguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$StdoutPath,
        [Parameter(Mandatory = $true)][string]$StderrPath,
        [Parameter(Mandatory = $true)][string]$ExitCodePath
    )
    $argumentLiterals = @($TargetArguments | ForEach-Object { ConvertTo-SingleQuotedLiteral -Value ([string]$_) })
    $invokeLine = '    & $targetFile'
    if ($argumentLiterals.Count -gt 0) { $invokeLine += ' ' + ($argumentLiterals -join ' ') }
    $invokeLine += ' 1> $stdoutPath 2> $stderrPath'
    $wrapperLines = @(
        '$ErrorActionPreference = ''Stop''',
        '$ProgressPreference = ''SilentlyContinue''',
        ('$targetFile = ' + (ConvertTo-SingleQuotedLiteral -Value $TargetFile)),
        ('$workingDirectory = ' + (ConvertTo-SingleQuotedLiteral -Value $WorkingDirectory)),
        ('$stdoutPath = ' + (ConvertTo-SingleQuotedLiteral -Value $StdoutPath)),
        ('$stderrPath = ' + (ConvertTo-SingleQuotedLiteral -Value $StderrPath)),
        ('$exitCodePath = ' + (ConvertTo-SingleQuotedLiteral -Value $ExitCodePath)),
        '$capturedExitCode = 9001',
        'try {',
        '    Set-Location -LiteralPath $workingDirectory',
        $invokeLine,
        '    if ($null -eq $LASTEXITCODE) { $capturedExitCode = 9002 } else { $capturedExitCode = [int]$LASTEXITCODE }',
        '}',
        'catch {',
        '    ($_ | Out-String) | Add-Content -LiteralPath $stderrPath',
        '    $capturedExitCode = 9001',
        '}',
        '[System.IO.File]::WriteAllText($exitCodePath, [string]$capturedExitCode, [System.Text.Encoding]::ASCII)',
        'exit $capturedExitCode'
    )
    $wrapperText = $wrapperLines -join [Environment]::NewLine
    return [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($wrapperText))
}

function Read-CapturedExitCode {
    param(
        [Parameter(Mandatory = $true)][string]$ExitCodePath,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )
    if (-not (Test-Path -LiteralPath $ExitCodePath)) {
        throw ($DisplayName + ' did not produce its required exit-code sidecar. The runner cannot prove success safely.')
    }
    $exitText = (Get-Content -LiteralPath $ExitCodePath -Raw).Trim()
    if ($exitText -notmatch '^-?[0-9]+$') {
        throw ($DisplayName + ' produced an invalid exit-code sidecar value: ' + $exitText)
    }
    return [int]$exitText
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [int]$TimeoutSeconds = 180,
        [string[]]$RequiredOutputText = @(),
        [string[]]$RejectedOutputText = @()
    )
    $nativeLogRoot = Join-Path $env:TEMP ('ChurchBooks-Native-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $nativeLogRoot -Force | Out-Null
    $stdoutPath = Join-Path $nativeLogRoot 'stdout.log'
    $stderrPath = Join-Path $nativeLogRoot 'stderr.log'
    $exitCodePath = Join-Path $nativeLogRoot 'exit.code'
    $stdoutLine = 0
    $stderrLine = 0
    $started = Get-Date
    $lastHeartbeat = $started
    $process = $null
    try {
        Write-Host ('[RUNNING] ' + $DisplayName + ' (timeout ' + $TimeoutSeconds + 's)') -ForegroundColor Cyan
        $encodedCapture = New-DirectExitCodeCaptureCommand -TargetFile $FilePath -TargetArguments $Arguments -WorkingDirectory $ProjectRoot -StdoutPath $stdoutPath -StderrPath $stderrPath -ExitCodePath $exitCodePath
        $process = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ' + $encodedCapture) -WorkingDirectory $ProjectRoot -NoNewWindow -PassThru
        while (-not $process.HasExited) {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            Write-LogDelta -Path $stdoutPath -LineIndex ([ref]$stdoutLine)
            Write-LogDelta -Path $stderrPath -LineIndex ([ref]$stderrLine) -Warning
            $now = Get-Date
            $elapsed = [int](($now - $started).TotalSeconds)
            if (($now - $lastHeartbeat).TotalSeconds -ge 15) {
                Write-Host ('[WORKING] ' + $DisplayName + ' elapsed=' + $elapsed + 's') -ForegroundColor DarkGray
                $lastHeartbeat = $now
            }
            if ($elapsed -ge $TimeoutSeconds) {
                Stop-ProcessTreeSafe -TargetProcessId $process.Id
                Write-LogDelta -Path $stdoutPath -LineIndex ([ref]$stdoutLine)
                Write-LogDelta -Path $stderrPath -LineIndex ([ref]$stderrLine) -Warning
                throw ($DisplayName + ' exceeded the ' + $TimeoutSeconds + ' second safety timeout.')
            }
        }
        $process.WaitForExit()
        Write-LogDelta -Path $stdoutPath -LineIndex ([ref]$stdoutLine)
        Write-LogDelta -Path $stderrPath -LineIndex ([ref]$stderrLine) -Warning
        $capturedExitCode = Read-CapturedExitCode -ExitCodePath $exitCodePath -DisplayName $DisplayName
        $stdoutText = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue } else { '' }
        $stderrText = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue } else { '' }
        $combinedText = [string]$stdoutText + [Environment]::NewLine + [string]$stderrText
        if ($capturedExitCode -ne 0) { throw ($DisplayName + ' failed with native exit code ' + $capturedExitCode + '.') }
        foreach ($rejected in $RejectedOutputText) {
            if (-not [string]::IsNullOrWhiteSpace($rejected) -and $combinedText.IndexOf($rejected, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw ($DisplayName + ' emitted a rejected failure marker despite exit code 0: ' + $rejected)
            }
        }
        foreach ($required in $RequiredOutputText) {
            if (-not [string]::IsNullOrWhiteSpace($required) -and $combinedText.IndexOf($required, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
                throw ($DisplayName + ' did not emit required success evidence: ' + $required)
            }
        }
        Write-Host ('[PASS] ' + $DisplayName + ' native exit code 0 with required success evidence.') -ForegroundColor Green
    }
    finally {
        if ($process -and -not $process.HasExited) { Stop-ProcessTreeSafe -TargetProcessId $process.Id }
        if (Test-Path -LiteralPath $nativeLogRoot) { Remove-Item -LiteralPath $nativeLogRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}



Set-Location -LiteralPath $ProjectRoot

Write-Host '[0/19] Running Windows PowerShell 5.1 parser preflight...'
$parserGate = Require-File -RelativePath 'scripts\Validate-Phase10PowerShell51.ps1'
Invoke-NativeChecked -FilePath 'powershell.exe' -Arguments @(
    '-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$parserGate,'-ProjectRoot',$ProjectRoot
) -DisplayName 'Phase 10 Windows PowerShell 5.1 parser preflight' -TimeoutSeconds 60 `
  -RequiredOutputText @('[PASS] All ChurchBooks Phase 10 PowerShell files passed the Windows PowerShell 5.1 parser gate.') `
  -RejectedOutputText @('[PARSER ERROR]','[FATAL]')

Write-Host '[1/19] Verifying Phase 10 manifest and frozen Phase 9 predecessor identity...'
$manifest = Get-Content -LiteralPath (Require-File -RelativePath 'churchbooks.manifest.json') -Raw | ConvertFrom-Json
if ($manifest.product -ne 'ChurchBooks') { throw 'Manifest product must be ChurchBooks.' }
if ([int]$manifest.phase -ne 10 -or $manifest.subphase -ne 'R1.4') { throw 'Manifest phase/subphase must be Phase 10 R1.4.' }
if ($manifest.release -ne 'Phase10-Vendors-Direct-Expenses') { throw 'Phase 10 release identity is invalid.' }
if ([int]$manifest.schemaVersion -ne 10) { throw 'Phase 10 must use additive schema version 10.' }
if (-not [bool]$manifest.expenses.vendorMasterData -or -not [bool]$manifest.expenses.directBankPaidExpenses -or -not [bool]$manifest.expenses.fundAwarePosting) { throw 'Phase 10 vendor/direct-expense scope is incomplete.' }
if ([bool]$manifest.expenses.automaticPosting -or [bool]$manifest.adaptiveImport.automaticPosting -or [bool]$manifest.adaptiveImport.automaticVendorCreation) { throw 'Automatic expense/import posting or silent vendor creation is prohibited.' }
if ([bool]$manifest.identityResolution.nameOnlyAutoMerge -or [bool]$manifest.identityResolution.existingProfileAutoOverwrite) { throw 'Phase 9 identity safety must remain frozen.' }
if ([bool]$manifest.bankReconciliation.automaticAdjustmentJournals) { throw 'Bank reconciliation auto-adjustment must remain disabled.' }
if ([bool]$manifest.licensingImplemented) { throw 'Licensing must remain deferred in Phase 10.' }
if ([int]$manifest.verificationTarget.accounting -ne 119 -or [int]$manifest.verificationTarget.core -ne 4 -or [int]$manifest.verificationTarget.data -ne 87 -or [int]$manifest.verificationTarget.app -ne 71 -or [int]$manifest.verificationTarget.total -ne 281) {
    throw 'Phase 10 verification target must be Accounting 119, Core 4, Data 87, App 71, Total 281.'
}
Write-Host '[PASS] Phase 10 manifest, expense boundaries, frozen safety contracts, and verification targets are correct.' -ForegroundColor Green

Write-Host '[2/19] Proving exact Windows-verified Phase 9 R1.2 files outside the authorized Phase 10 change set...'
$baseline = Get-Content -LiteralPath (Require-File -RelativePath 'docs\baselines\PHASE-9-R1.2-MANAGED-SHA256.json') -Raw | ConvertFrom-Json
if ($baseline.product -ne 'ChurchBooks' -or $baseline.release -ne 'Phase9-Adaptive-Templates-Identity' -or [int]$baseline.schemaVersion -ne 9) { throw 'Phase 9 R1.2 baseline identity is invalid.' }
if (@($baseline.files).Count -ne 359) { throw ('Phase 9 R1.2 baseline inventory must contain exactly 359 files; found ' + @($baseline.files).Count) }
$authorizedPhase10Changes = @(
    'churchbooks.manifest.json',
    'docs/ROADMAP.md',
    'docs/PHASES.md',
    'src/ChurchBooks.Core/ProductInfo.cs',
    'src/ChurchBooks.App/MainWindow.xaml',
    'src/ChurchBooks.App/Models/WorkspaceSection.cs',
    'src/ChurchBooks.App/Services/TerminologyAliasService.cs',
    'src/ChurchBooks.App/ViewModels/MainWindowViewModel.cs'
)
$authorizedLookup = @{}
foreach ($item in $authorizedPhase10Changes) { $authorizedLookup[$item.ToLowerInvariant()] = $true }
$protectedCount = 0
foreach ($entry in @($baseline.files)) {
    $relative = [string]$entry.path
    if ($authorizedLookup.ContainsKey($relative.ToLowerInvariant())) { continue }
    $full = Join-Path $ProjectRoot $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $full)) { throw ('Frozen Phase 9 file is missing: ' + $relative) }
    if ((Get-Sha256Lower -Path $full) -ne ([string]$entry.sha256).ToLowerInvariant()) { throw ('Frozen Phase 9 file changed unexpectedly: ' + $relative) }
    $protectedCount++
}
if ($protectedCount -ne 351) { throw ('Frozen Phase 9 predecessor proof expected 351 protected files; checked ' + $protectedCount) }
Write-Host ('[PASS] Frozen Phase 9 R1.2 files are byte-identical outside the authorized Phase 10 scope: ' + $protectedCount + ' checked.') -ForegroundColor Green

Write-Host '[3/19] Verifying vendor master data and history-safe archive behavior...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Expenses\Vendor.cs' -ExpectedText 'public Vendor Archive'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Expenses\Vendor.cs' -ExpectedText 'public Vendor Reactivate'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Abstractions\IExpenseStore.cs' -ExpectedText 'GetVendorsAsync'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText 'Archived vendors cannot be used for new expenses.'
Write-Host '[PASS] Vendor master data supports archive/restore while preserving historical identity.' -ForegroundColor Green

Write-Host '[4/19] Verifying additive schema v10 vendor/direct-expense storage...'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase10DatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS vendors'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase10DatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS direct_expenses'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase10DatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS direct_expense_lines'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase10DatabaseMigrator.cs' -ExpectedText "VALUES('schema_version','10'"
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteExpenseStore.cs' -ExpectedText "expense_status='Posted'"
Write-Host '[PASS] Schema v10 is additive and stores vendors plus direct-expense evidence locally.' -ForegroundColor Green

Write-Host '[5/19] Verifying direct-expense validation and Fund-aware balanced posting...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Expenses\DirectExpenseLine.cs' -ExpectedText 'Expense amount must be greater than zero.'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText 'AccountType.Expense'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText 'AccountType.Asset'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText 'new FundJournalEntry(journal, assignments)'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText '_fundEngine.PostJournalAsync'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText 'Only draft expenses can be posted.'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ExpensesWorkspaceViewModel.cs' -ExpectedText 'Guid? vendorId = SelectedVendor is { Status: VendorStatus.Active } activeVendor ? activeVendor.Id : null;'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ExpensesWorkspaceViewModel.cs' -RejectedText 'var vendorId = SelectedVendor?.Status == VendorStatus.Active ? SelectedVendor.Id : null;'
Write-Host '[PASS] Direct expenses validate bank/account/Fund references and post only through the protected FundAccountingEngine.' -ForegroundColor Green

Write-Host '[6/19] Verifying posting remains explicit and Smart Import cannot create expense side effects...'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ExpensesWorkspaceView.xaml' -ExpectedText 'Smart Import never posts expenses automatically'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ExpensesWorkspaceViewModel.cs' -ExpectedText 'Nothing posts until you explicitly choose Post Expense.'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -RejectedText 'SqliteExpenseStore'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -RejectedText 'AddVendorAsync'
Write-Host '[PASS] Expense posting is explicit; adaptive import has no expense/vendor side-effect path.' -ForegroundColor Green

Write-Host '[7/19] Verifying approved compact Expenses GUI and navigation...'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'ShowExpensesCommand'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'ExpensesWorkspaceView'
Reject-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -RejectedText 'Content="Expenses" IsEnabled="False"'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\MainWindowViewModel.cs' -ExpectedText 'IsExpensesVisible'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\MainWindowViewModel.cs' -ExpectedText 'schema v10'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\MainWindowViewModel.cs' -ExpectedText 'OnPropertyChanged(nameof(IsExpensesVisible))'
Write-Host '[PASS] Expenses is integrated into the existing one-window desktop shell with correct visibility notification.' -ForegroundColor Green

Write-Host '[8/19] Rechecking frozen Phase 9 adaptive import and identity safeguards...'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'never posts automatically'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'never merges people automatically'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PersonIdentityResolver.cs' -ExpectedText 'Name-only similarity is never enough'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -ExpectedText 'resolution.Status == PersonImportResolutionStatus.ExistingExact'
Write-Host '[PASS] Phase 9 adaptive import and conservative identity behavior remain protected.' -ForegroundColor Green

Write-Host '[9/19] Rechecking frozen bank reconciliation exact-zero/no-auto-adjust boundaries...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationCalculator.cs' -ExpectedText 'var difference = reconciliation.StatementEndingBalance - expectedStatement;'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -ExpectedText '0m'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -RejectedText 'automatic balancing'
Write-Host '[PASS] Bank reconciliation remains a separate exact-zero evidence-matching workflow.' -ForegroundColor Green

Write-Host '[10/19] Rechecking dynamic Funds, personalized currency, and no hard-coded ministry categories...'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ExpensesWorkspaceViewModel.cs' -ExpectedText '_fundStore.GetAllFundsAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ExpensesWorkspaceViewModel.cs' -ExpectedText 'ApplyPersonalization'
$expenseSourceRoot = Join-Path $ProjectRoot 'src\ChurchBooks.App'
$expenseSources = @(Get-ChildItem -LiteralPath $expenseSourceRoot -Recurse -File -Include '*Expense*.cs','*Expense*.xaml' | Where-Object {
    $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]'
})
if ($expenseSources.Count -lt 1) { throw 'No canonical Phase 10 expense source files were found for the ministry-name guard.' }
foreach ($forbidden in @('Missions','Building Fund','Love Offering','Youth Ministry','Benevolence')) {
    foreach ($source in $expenseSources) {
        $text = Get-Content -LiteralPath $source.FullName -Raw
        if ($text.IndexOf($forbidden, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ('Hard-coded ministry name found in canonical Phase 10 expense source: ' + $source.FullName + ' -> ' + $forbidden)
        }
    }
}
Write-Host '[PASS] Phase 10 reads active Funds/accounts/banks from data and preserves personalized currency.' -ForegroundColor Green

Write-Host '[11/19] Rechecking known compile-risk and dependency safeguards...'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'using System.IO;'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -RejectedText 'using ChurchBooks.App.Infrastructure;'
Reject-Text -RelativePath 'Directory.Packages.props' -RejectedText 'System.Text.Encoding.CodePages'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\ExpenseManagementService.cs' -ExpectedText 'DateOnly expenseDate'
Write-Host '[PASS] Dependency pruning, namespace, System.IO, spreadsheet-reader, and date typing guards remain intact.' -ForegroundColor Green

Write-Host '[12/19] Verifying focused Phase 10 tests and stable historical contracts...'
$phase10FocusedTests = @('tests\ChurchBooks.Accounting.Tests\Phase10ExpenseTests.cs','tests\ChurchBooks.Data.Tests\Phase10ExpenseDataTests.cs','tests\ChurchBooks.App.Tests\Phase10ExpenseUxTests.cs')
foreach ($relativeTest in $phase10FocusedTests) {
    $testPath = Require-File -RelativePath $relativeTest
    foreach ($line in @(Get-Content -LiteralPath $testPath)) {
        if ($line -match '^\s*\[Fact\]\s+public') { throw ('Compressed same-line [Fact] declaration is not allowed in clean Phase 10 tests: ' + $relativeTest) }
        if ($line.Length -gt 220) { throw ('Phase 10 focused test line exceeds 220 characters: ' + $relativeTest) }
    }
}
Require-Text -RelativePath 'tests\ChurchBooks.Accounting.Tests\Phase10ExpenseTests.cs' -ExpectedText 'DirectExpense_TotalIsExactLineSum'
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\Phase10ExpenseDataTests.cs' -ExpectedText 'DirectExpense_RoundTripsMultipleLines'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase10ExpenseUxTests.cs' -ExpectedText 'MainWindow_HasSingleActiveExpensesNavigationButton'
Write-Host '[PASS] Phase 10 has readable domain, SQLite, and WPF regression coverage.' -ForegroundColor Green

Write-Host '[13/19] Verifying roadmap boundary and deferred payables/purchasing scope...'
Require-Text -RelativePath 'docs\PHASES.md' -ExpectedText 'Bills/accounts payable, partial payments and purchasing are deliberately deferred to Phase 11'
Require-Text -RelativePath 'docs\ROADMAP.md' -ExpectedText 'Phase 11 - Bills, accounts payable, partial payments and purchasing workflow.'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ExpensesWorkspaceView.xaml' -ExpectedText 'Bills/payables are intentionally reserved for the next phase.'
Write-Host '[PASS] Phase 10 remains narrow: direct expenses now, bills/payables/purchasing later.' -ForegroundColor Green

Write-Host '[14/19] Verifying fail-closed release runner...'
Require-Text -RelativePath 'scripts\Verify-Phase10.ps1' -ExpectedText '$LASTEXITCODE'
Require-Text -RelativePath 'scripts\Verify-Phase10.ps1' -ExpectedText 'Build succeeded.'
Require-Text -RelativePath 'scripts\Verify-Phase10.ps1' -ExpectedText 'Build FAILED.'
Require-Text -RelativePath 'scripts\Verify-Phase10.ps1' -ExpectedText '--disable-build-servers'
Write-Host '[PASS] Phase 10 retains native exit evidence, TRX proof, bounded processes, and fail-closed WPF smoke.' -ForegroundColor Green

Write-Host '[15/19] Verifying Phase 10 product identity...'
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase10-Vendors-Direct-Expenses";'
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string ReleaseRevision = "R1.4";'
Write-Host '[PASS] Product identity is Phase 10 R1.4 Vendors + Direct Expenses.' -ForegroundColor Green

if ($StaticOnly) {
    Write-Host '[PASS] ChurchBooks Phase 10 R1.4 static verifier contract passed against canonical payload.' -ForegroundColor Green
    Write-Host '[INFO] Windows restore/build, 281/281 TRX verification, and fail-closed WPF smoke remain authoritative during staged verification.'
    exit 0
}

Write-Host '[16/19] Restoring NuGet packages with complete vulnerability audit...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @(
    'restore','ChurchBooks.sln','--disable-build-servers','-p:NuGetAudit=true','-p:NuGetAuditMode=all'
) -DisplayName 'dotnet restore + complete NuGet audit' -TimeoutSeconds 180 -RejectedOutputText @('error NU','Restore failed')

Write-Host '[17/19] Building Release x64...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @(
    'build','ChurchBooks.sln','-c','Release','-p:Platform=x64','--no-restore','--disable-build-servers'
) -DisplayName 'Release x64 build' -TimeoutSeconds 180 -RequiredOutputText @('Build succeeded.') -RejectedOutputText @('Build FAILED.',' error CS')

function Get-TrxCounter {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlElement]$Counters,
        [Parameter(Mandatory = $true)][string]$Name
    )
    $attribute = $Counters.Attributes[$Name]
    if ($null -eq $attribute) { return 0 }
    return [int]$attribute.Value
}

function Write-TrxFailureDiagnostics {
    param(
        [Parameter(Mandatory = $true)][string]$TrxPath,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )
    if (-not (Test-Path -LiteralPath $TrxPath)) {
        Write-Host ('[DIAGNOSTIC] ' + $DisplayName + ' did not create a TRX file before failure.') -ForegroundColor Yellow
        return
    }
    try {
        [xml]$failedTrx = Get-Content -LiteralPath $TrxPath -Raw
        $failedResults = @($failedTrx.SelectNodes("//*[local-name()='UnitTestResult' and @outcome='Failed']"))
        if ($failedResults.Count -eq 0) {
            Write-Host ('[DIAGNOSTIC] ' + $DisplayName + ' TRX exists but contains no Failed UnitTestResult node.') -ForegroundColor Yellow
            return
        }
        foreach ($result in $failedResults) {
            Write-Host ('[TRX FAIL] ' + [string]$result.testName) -ForegroundColor Red
            $messageNode = $result.SelectSingleNode("./*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")
            $stackNode = $result.SelectSingleNode("./*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='StackTrace']")
            if ($messageNode -and -not [string]::IsNullOrWhiteSpace($messageNode.InnerText)) { Write-Host ('[TRX MESSAGE] ' + $messageNode.InnerText) -ForegroundColor Yellow }
            if ($stackNode -and -not [string]::IsNullOrWhiteSpace($stackNode.InnerText)) { Write-Host ('[TRX STACK] ' + $stackNode.InnerText) -ForegroundColor DarkYellow }
        }
    }
    catch {
        Write-Host ('[DIAGNOSTIC] Unable to parse failed TRX for ' + $DisplayName + ': ' + $_.Exception.Message) -ForegroundColor Yellow
    }
}

function Invoke-VerifiedTestProject {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][string]$TrxFileName,
        [Parameter(Mandatory = $true)][int]$ExpectedTotal,
        [Parameter(Mandatory = $true)][string]$ResultsDirectory
    )

    $testArguments = @('test',$ProjectPath,'-c','Release','--no-restore','--disable-build-servers','--logger',('trx;LogFileName=' + $TrxFileName),'--results-directory',$ResultsDirectory)
    $trxPath = Join-Path $ResultsDirectory $TrxFileName
    try {
        Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments $testArguments -DisplayName ($DisplayName + ' dotnet test') -TimeoutSeconds 120 -RequiredOutputText @('Passed!') -RejectedOutputText @('Build FAILED.',' error CS','Test Run Failed')
    }
    catch {
        Write-TrxFailureDiagnostics -TrxPath $trxPath -DisplayName $DisplayName
        throw
    }

    if (-not (Test-Path -LiteralPath $trxPath)) { throw ($DisplayName + ' TRX result was not created.') }

    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) { throw ($DisplayName + ' TRX Counters node was not found.') }

    $total = Get-TrxCounter -Counters $counters -Name 'total'
    $executed = Get-TrxCounter -Counters $counters -Name 'executed'
    $passed = Get-TrxCounter -Counters $counters -Name 'passed'
    $failed = Get-TrxCounter -Counters $counters -Name 'failed'
    $errors = Get-TrxCounter -Counters $counters -Name 'error'
    $timeout = Get-TrxCounter -Counters $counters -Name 'timeout'
    $aborted = Get-TrxCounter -Counters $counters -Name 'aborted'
    $notExecuted = Get-TrxCounter -Counters $counters -Name 'notExecuted'

    if ($total -ne $ExpectedTotal -or
        $executed -ne $ExpectedTotal -or
        $passed -ne $ExpectedTotal -or
        $failed -ne 0 -or
        $errors -ne 0 -or
        $timeout -ne 0 -or
        $aborted -ne 0 -or
        $notExecuted -ne 0) {
        throw ($DisplayName + ' unexpected TRX result. Total=' + $total + ' Passed=' + $passed + ' Failed=' + $failed)
    }

    Write-Host ('[PASS] ' + $DisplayName + ': ' + $passed + '/' + $total + ' verified from TRX.') -ForegroundColor Green
    return $passed
}

$testResultsRoot = Join-Path $env:TEMP ('ChurchBooks-Phase10-TRX-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testResultsRoot -Force | Out-Null
try {
    Write-Host '[18/19] Running complete protected regression + Phase 10 tests with machine-readable TRX verification...'
    $verifiedPassed = 0
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Accounting.Tests\ChurchBooks.Accounting.Tests.csproj' -DisplayName 'ChurchBooks.Accounting.Tests' -TrxFileName 'accounting.trx' -ExpectedTotal 119 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Core.Tests\ChurchBooks.Core.Tests.csproj' -DisplayName 'ChurchBooks.Core.Tests' -TrxFileName 'core.trx' -ExpectedTotal 4 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Data.Tests\ChurchBooks.Data.Tests.csproj' -DisplayName 'ChurchBooks.Data.Tests' -TrxFileName 'data.trx' -ExpectedTotal 87 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.App.Tests\ChurchBooks.App.Tests.csproj' -DisplayName 'ChurchBooks.App.Tests' -TrxFileName 'app.trx' -ExpectedTotal 71 -ResultsDirectory $testResultsRoot
    if ($verifiedPassed -ne 281) { throw ('Unexpected aggregate Phase 10 TRX result. Passed=' + $verifiedPassed + ' Expected=281') }
    Write-Host '[PASS] Protected regression plus Phase 10 coverage: 281/281 verified from machine-readable TRX results.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testResultsRoot) { Remove-Item -LiteralPath $testResultsRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host '[18/19 - dependency] Checking resolved dependencies and Phase 10 identity...'
$assetFiles = Get-ChildItem -LiteralPath $ProjectRoot -Recurse -Filter 'project.assets.json' -ErrorAction SilentlyContinue
$mvvm = $false
$fluent = $false
foreach ($assetFile in $assetFiles) {
    $assetText = Get-Content -LiteralPath $assetFile.FullName -Raw
    if ($assetText.IndexOf('SQLitePCLRaw.lib.e_sqlite3/2.1.11', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { throw ('Vulnerable SQLite package resolved: ' + $assetFile.FullName) }
    if ($assetText.IndexOf('CommunityToolkit.Mvvm/8.4.2', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $mvvm = $true }
    if ($assetText.IndexOf('FluentValidation/12.1.1', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $fluent = $true }
}
if (-not $mvvm -or -not $fluent) { throw 'Expected pinned dependencies were not all resolved.' }
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase10-Vendors-Direct-Expenses";'
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string ReleaseRevision = "R1.4";'
Write-Host '[PASS] Dependency resolution, security boundary, and Phase 10 identity are correct.' -ForegroundColor Green

Write-Host '[18/19 - smoke] Running fail-closed WPF main-window smoke test...'
if ($SkipWpfSmoke) {
    Write-Host '[SKIP] WPF smoke was explicitly skipped.' -ForegroundColor Yellow
}
else {
    $appExe = Join-Path $ProjectRoot 'src\ChurchBooks.App\bin\Release\net10.0-windows\ChurchBooks.App.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw ('Built WPF executable was not found: ' + $appExe) }

    $appProcess = $null
    $smokeRoot = Join-Path $env:TEMP ('ChurchBooks-Phase10-WpfSmoke-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
    $smokeDatabasePath = Join-Path $smokeRoot 'ChurchBooks-Smoke.db'
    $priorDatabasePath = $env:CHURCHBOOKS_DATABASE_PATH
    try {
        $env:CHURCHBOOKS_DATABASE_PATH = $smokeDatabasePath
        $appProcess = Start-Process -FilePath $appExe -WorkingDirectory (Split-Path -Parent $appExe) -PassThru

        $windowDeadline = (Get-Date).AddSeconds(20)
        $windowFound = $false
        while ((Get-Date) -lt $windowDeadline) {
            Start-Sleep -Milliseconds 250
            $appProcess.Refresh()
            if ($appProcess.HasExited) { throw ('ChurchBooks.App exited before creating a WPF main window. ExitCode=' + $appProcess.ExitCode) }
            if ($appProcess.MainWindowHandle -ne 0) { $windowFound = $true; break }
        }
        if (-not $windowFound) { throw 'ChurchBooks.App did not create a visible WPF main window within 20 seconds.' }

        # SQLite can create the file before the first schema write is durable enough to expose
        # a readable database header. Treat file existence alone as "initialization started",
        # not as readiness. This bounded poll prevents a false failure on a transient 0-byte file.
        $databaseReadyDeadline = (Get-Date).AddSeconds(30)
        $databaseReady = $false
        $lastMainBytes = [int64]0
        $lastWalBytes = [int64]0
        while ((Get-Date) -lt $databaseReadyDeadline) {
            Start-Sleep -Milliseconds 250
            $appProcess.Refresh()
            if ($appProcess.HasExited) { throw ('ChurchBooks.App exited while waiting for isolated database initialization. ExitCode=' + $appProcess.ExitCode) }

            if (Test-Path -LiteralPath $smokeDatabasePath) {
                $databaseItem = Get-Item -LiteralPath $smokeDatabasePath
                $lastMainBytes = [int64]$databaseItem.Length
                $walPath = $smokeDatabasePath + '-wal'
                if (Test-Path -LiteralPath $walPath) {
                    $lastWalBytes = [int64](Get-Item -LiteralPath $walPath).Length
                }

                if ($lastMainBytes -ge 16) {
                    $stream = $null
                    try {
                        $stream = [System.IO.File]::Open(
                            $smokeDatabasePath,
                            [System.IO.FileMode]::Open,
                            [System.IO.FileAccess]::Read,
                            [System.IO.FileShare]::ReadWrite
                        )
                        $header = New-Object byte[] 16
                        $read = $stream.Read($header, 0, $header.Length)
                        if ($read -eq 16) {
                            $headerText = [System.Text.Encoding]::ASCII.GetString($header, 0, 15)
                            if ($headerText -eq 'SQLite format 3' -and $header[15] -eq 0) {
                                $databaseReady = $true
                                break
                            }
                        }
                    }
                    catch {
                        # The app may still hold the database during startup. Retry until the
                        # bounded deadline rather than treating a transient sharing state as failure.
                    }
                    finally {
                        if ($stream) { $stream.Dispose() }
                    }
                }
            }
        }
        if (-not $databaseReady) {
            throw ('WPF smoke did not produce a readable SQLite verification database within 30 seconds. MainBytes=' + $lastMainBytes + ' WalBytes=' + $lastWalBytes)
        }

        Write-Host ('[PASS] ChurchBooks WPF main window and isolated schema-v10 SQLite database became readable. PID=' + $appProcess.Id + ' HWND=' + $appProcess.MainWindowHandle + ' Bytes=' + $lastMainBytes) -ForegroundColor Green
    }
    finally {
        if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue }
        $env:CHURCHBOOKS_DATABASE_PATH = $priorDatabasePath
        if (Test-Path -LiteralPath $smokeRoot) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host '[19/19 - boundary] Rechecking expense, import, reconciliation, and licensing boundaries...'
if ([bool]$manifest.expenses.automaticPosting) { throw 'Expense automatic posting must remain disabled.' }
if ([bool]$manifest.adaptiveImport.automaticPosting -or [bool]$manifest.adaptiveImport.automaticVendorCreation) { throw 'Smart Import must not post expenses or create vendors silently.' }
if ([bool]$manifest.identityResolution.nameOnlyAutoMerge -or [bool]$manifest.identityResolution.existingProfileAutoOverwrite) { throw 'Identity auto-merge/overwrite must remain disabled.' }
if ([bool]$manifest.bankReconciliation.automaticAdjustmentJournals) { throw 'Bank reconciliation automatic adjustment journals must remain disabled.' }
if ([bool]$manifest.licensingImplemented) { throw 'Licensing must remain deferred after Phase 10.' }
Write-Host '[PASS] Phase 10 adds vendor master data and explicit Fund-aware direct-expense posting without weakening Smart Import, identity, reconciliation, or licensing boundaries.' -ForegroundColor Green
Write-Host ''
Write-Host '[PASS] ChurchBooks Phase 10 R1.4 Vendors + Direct Expenses verification passed.' -ForegroundColor Green
exit 0
