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
Write-Host 'ChurchBooks Phase 8 - Bank Reconciliation Verification'
Write-Host '============================================================'
Write-Host ('Project: ' + $ProjectRoot)
Write-Host 'Phase 2 accounting kernel: FROZEN / protected'
Write-Host 'Phase 3B fund-accounting kernel: VERIFIED / protected'
Write-Host 'Phase 3C Fund Manager + Familiar Start: VERIFIED / protected'
Write-Host 'Phase 3D Fund Reports + Integrity: VERIFIED / protected'
Write-Host 'Phase 4 People + Giving Directory: VERIFIED / protected'
Write-Host 'Phase 5 Individual Offerings + Analytics: FROZEN at R1.3 / 125 of 125 plus staged+live WPF smoke'
Write-Host 'Phase 6 Personalized Setup + Banking: FROZEN / 155 of 155 plus staged+live WPF smoke'
Write-Host 'Phase 7 Smart Import: FROZEN at R1.3 / 189 of 189 plus staged+live WPF smoke'
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
$parserGate = Require-File -RelativePath 'scripts\Validate-Phase8PowerShell51.ps1'
Invoke-NativeChecked -FilePath 'powershell.exe' -Arguments @(
    '-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$parserGate,'-ProjectRoot',$ProjectRoot
) -DisplayName 'Phase 8 Windows PowerShell 5.1 parser preflight' -TimeoutSeconds 60 `
  -RequiredOutputText @('[PASS] All ChurchBooks Phase 8 PowerShell files passed the Windows PowerShell 5.1 parser gate.') `
  -RejectedOutputText @('[PARSER ERROR]','[FATAL]')

Write-Host '[1/19] Verifying Phase 8 manifest and frozen Phase 7 predecessor identity...'
$manifest = Get-Content -LiteralPath (Require-File -RelativePath 'churchbooks.manifest.json') -Raw | ConvertFrom-Json
if ($manifest.product -ne 'ChurchBooks') { throw 'Manifest product must be ChurchBooks.' }
if ([int]$manifest.phase -ne 8 -or $manifest.subphase -ne 'R1.1') { throw 'Manifest phase/subphase must be Phase 8 R1.1.' }
if ($manifest.release -ne 'Phase8-Bank-Reconciliation') { throw 'Phase 8 release identity is invalid.' }
if ([int]$manifest.schemaVersion -ne 8) { throw 'Phase 8 must use additive schema version 8.' }
if (-not [bool]$manifest.bankReconciliation.explicitAmountConvention) { throw 'Explicit statement amount convention is required.' }
if (-not [bool]$manifest.bankReconciliation.manualManyToManyMatching) { throw 'Many-to-many matching must be enabled.' }
if (-not [bool]$manifest.bankReconciliation.exactZeroDifferenceRequired) { throw 'Exact zero-difference completion must be required.' }
if ([bool]$manifest.bankReconciliation.automaticAdjustmentJournals) { throw 'Automatic reconciliation adjustments must remain disabled.' }
if ([bool]$manifest.smartImport.automaticPosting) { throw 'Smart Import automatic posting must remain disabled.' }
if ([bool]$manifest.licensingImplemented) { throw 'Licensing must remain deferred in Phase 8.' }
if ([int]$manifest.verificationTarget.accounting -ne 95 -or
    [int]$manifest.verificationTarget.core -ne 4 -or
    [int]$manifest.verificationTarget.data -ne 69 -or
    [int]$manifest.verificationTarget.app -ne 53 -or
    [int]$manifest.verificationTarget.total -ne 221) {
    throw 'Phase 8 verification target must be Accounting 95, Core 4, Data 69, App 53, Total 221.'
}
Write-Host '[PASS] Phase 8 manifest and reconciliation safety boundaries are correct.' -ForegroundColor Green

Write-Host '[2/19] Proving exact verified Phase 7 R1.3 files outside the authorized Phase 8 change set...'
$baseline = Get-Content -LiteralPath (Require-File -RelativePath 'docs\baselines\PHASE-7-R1.3-MANAGED-SHA256.json') -Raw | ConvertFrom-Json
if ($baseline.product -ne 'ChurchBooks' -or
    $baseline.release -ne 'Phase7-Smart-Import' -or
    $baseline.subphase -ne 'R1.3' -or
    [int]$baseline.schemaVersion -ne 7) {
    throw 'Phase 7 R1.3 baseline identity is invalid.'
}
if (@($baseline.files).Count -ne 300) { throw ('Phase 7 baseline inventory must contain exactly 300 files; found ' + @($baseline.files).Count) }
$authorizedPhase8Changes = @(
    'churchbooks.manifest.json',
    'docs/PHASES.md',
    'src/ChurchBooks.Core/ProductInfo.cs',
    'src/ChurchBooks.Accounting/Abstractions/ISmartImportStore.cs',
    'src/ChurchBooks.Data/Storage/SqliteSmartImportStore.cs',
    'src/ChurchBooks.App/ViewModels/ImportWorkspaceViewModel.cs',
    'src/ChurchBooks.App/ViewModels/BankingWorkspaceViewModel.cs',
    'src/ChurchBooks.App/Views/BankingWorkspaceView.xaml',
    'src/ChurchBooks.App/ViewModels/MainWindowViewModel.cs',
    'src/ChurchBooks.App/Services/TerminologyAliasService.cs'
)
$authorizedLookup = @{}
foreach ($item in $authorizedPhase8Changes) { $authorizedLookup[$item.ToLowerInvariant()] = $true }
$protectedCount = 0
foreach ($entry in @($baseline.files)) {
    $relative = [string]$entry.path
    if ($authorizedLookup.ContainsKey($relative.ToLowerInvariant())) { continue }
    $full = Join-Path $ProjectRoot $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $full)) { throw ('Frozen Phase 7 file is missing: ' + $relative) }
    if ((Get-Sha256Lower -Path $full) -ne ([string]$entry.sha256).ToLowerInvariant()) {
        throw ('Frozen Phase 7 file changed unexpectedly: ' + $relative)
    }
    $protectedCount++
}
if ($protectedCount -ne 290) { throw ('Frozen Phase 7 predecessor proof expected 290 protected files; checked ' + $protectedCount) }
Write-Host ('[PASS] Frozen Phase 7 R1.3 files are byte-identical outside the authorized Phase 8 scope: ' + $protectedCount + ' checked.') -ForegroundColor Green

Write-Host '[3/19] Rechecking first-run personalization and stable internal terminology...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Setup\TerminologyKeys.cs' -ExpectedText 'public const string BankAccount = "BankAccount";'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\SetupWorkspaceView.xaml' -ExpectedText 'Internal accounting keys remain stable'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\SetupWorkspaceViewModel.cs' -ExpectedText 'You can change these terms later in Settings.'
Require-Text -RelativePath 'src\ChurchBooks.App\Services\TerminologyAliasService.cs' -ExpectedText '"bank reconciliation"'
Write-Host '[PASS] Personalized labels remain editable while canonical accounting keys stay stable.' -ForegroundColor Green

Write-Host '[4/19] Verifying additive schema v8 reconciliation storage...'
$schemaText = Get-Content -LiteralPath (Require-File -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs') -Raw
foreach ($table in @(
    'bank_statement_import_links',
    'bank_statement_lines',
    'bank_reconciliations',
    'bank_reconciliation_match_groups',
    'bank_reconciliation_match_statement_lines',
    'bank_reconciliation_match_journals'
)) {
    if ($schemaText.IndexOf(('CREATE TABLE IF NOT EXISTS ' + $table), [System.StringComparison]::Ordinal) -lt 0) {
        throw ('Schema v8 table is missing: ' + $table)
    }
}
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs' -ExpectedText "VALUES('schema_version','8'"
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs' -ExpectedText 'PRAGMA foreign_key_check;'
Write-Host '[PASS] Schema v8 is additive, foreign-key guarded, and stores statement/reconciliation evidence locally.' -ForegroundColor Green

Write-Host '[5/19] Verifying Smart Import remains staging-only and provides retrievable sessions...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Abstractions\ISmartImportStore.cs' -ExpectedText 'GetSessionsAsync'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Abstractions\ISmartImportStore.cs' -ExpectedText 'GetSessionAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'No journal or bank posting was created.'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'new Phase8DatabaseMigrator(database)'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText '".csv" => ImportSourceKind.Csv'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText '".xls" => ImportSourceKind.Xls'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText '".xlsx" => ImportSourceKind.Xlsx'
Write-Host '[PASS] Phase 7 Smart Import remains preview/staging-only and can safely supply statement sessions.' -ForegroundColor Green

Write-Host '[6/19] Verifying explicit statement amount semantics and duplicate safety...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankStatementAmountConvention.cs' -ExpectedText 'DebitIncreasesBalance'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankStatementAmountConvention.cs' -ExpectedText 'CreditIncreasesBalance'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankStatementImportConverter.cs' -ExpectedText 'includePotentialDuplicates = false'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankStatementImportConverter.cs' -ExpectedText 'var date = ParseDate(GetText(root, "Date"))'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankStatementImportConverter.cs' -RejectedText 'date.Value'
Require-Text -RelativePath 'tests\ChurchBooks.Accounting.Tests\Phase8ReconciliationTests.cs' -ExpectedText 'Assert.Equal(new DateOnly(2026, 8, 20), line.TransactionDate);'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs' -ExpectedText 'UNIQUE(bank_account_id, fingerprint)'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs' -ExpectedText 'import_session_id TEXT NOT NULL PRIMARY KEY'
Write-Host '[PASS] Bank-specific debit/credit semantics are explicit; duplicate/session reuse is fail-closed.' -ForegroundColor Green

Write-Host '[7/19] Verifying many-to-many matching and exact signed-total boundary...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\ReconciliationMatchGroup.cs' -ExpectedText 'StatementLineIds'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\ReconciliationMatchGroup.cs' -ExpectedText 'JournalEntryIds'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -ExpectedText 'statementTotal != bookTotal'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs' -ExpectedText 'UNIQUE(bank_account_id, journal_entry_id)'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase8DatabaseMigrator.cs' -ExpectedText 'statement_line_id TEXT NOT NULL UNIQUE'
Write-Host '[PASS] Manual one-to-one/one-to-many/many-to-one matching requires exact signed totals and single-use evidence.' -ForegroundColor Green

Write-Host '[8/19] Verifying conservative exact auto-match refuses ambiguity...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\ReconciliationMatcher.cs' -ExpectedText 'candidates.Length != 1'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\ReconciliationMatcher.cs' -ExpectedText 'reverseCandidates.Length != 1'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -ExpectedText 'ApplyExactAutoMatchesAsync'
Write-Host '[PASS] Auto-match is opt-in and limited to unambiguous equal signed amounts in a bounded date window.' -ForegroundColor Green

Write-Host '[9/19] Verifying reconciliation calculator and completion gate...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationCalculator.cs' -ExpectedText 'var outstandingBook = unmatchedBooks.Sum'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationCalculator.cs' -ExpectedText 'var expectedStatement = bookEnding - outstandingBook'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationSummary.cs' -ExpectedText 'Difference == 0m'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationSummary.cs' -ExpectedText 'UnmatchedStatementLines.Count == 0'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -ExpectedText 'Completed reconciliations are locked.'
Write-Host '[PASS] Completion requires every statement line matched plus exact zero difference; completed work is locked.' -ForegroundColor Green

Write-Host '[10/19] Verifying no automatic balancing journal can originate from reconciliation...'
foreach ($relative in @(
    'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs',
    'src\ChurchBooks.App\ViewModels\BankReconciliationViewModel.cs',
    'src\ChurchBooks.App\Views\BankReconciliationView.xaml'
)) {
    Reject-Text -RelativePath $relative -RejectedText 'AccountingEngine('
    Reject-Text -RelativePath $relative -RejectedText 'PostJournalAsync('
}
Require-Text -RelativePath 'src\ChurchBooks.App\Views\BankReconciliationView.xaml' -ExpectedText 'never creates an automatic balancing adjustment'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankReconciliationViewModel.cs' -ExpectedText 'No balancing journal was generated.'
Write-Host '[PASS] Reconciliation matches evidence only; bank fees/interest/corrections require explicit accounting entry elsewhere.' -ForegroundColor Green

Write-Host '[11/19] Verifying bank-ledger aggregation and prior-clear exclusion...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -ExpectedText '.GroupBy(line => new'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankReconciliationService.cs' -ExpectedText 'group.Sum(line => line.Debit - line.Credit)'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteBankReconciliationStore.cs' -ExpectedText 'GetUnavailableJournalIdsAsync'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteBankReconciliationStore.cs' -ExpectedText 'GetUnavailableStatementLineIdsAsync'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationCalculator.cs' -ExpectedText '!previouslyCleared.Contains'
Write-Host '[PASS] Fund-aware deposit journals aggregate to one bank transaction and previously cleared journals stay out of future outstanding lists.' -ForegroundColor Green

Write-Host '[12/19] Verifying approved compact Banking GUI and personalized navigation...'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'WindowState="Maximized"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinWidth="1180"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinHeight="700"'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\BankingWorkspaceView.xaml' -ExpectedText 'Header="Reconciliation"'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\BankingWorkspaceView.xaml' -ExpectedText 'BankReconciliationView'
Require-Text -RelativePath 'src\ChurchBooks.App\Services\TerminologyAliasService.cs' -ExpectedText '"reconcile bank"'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\MainWindowViewModel.cs' -ExpectedText 'case WorkspaceSection.Banking:'
Write-Host '[PASS] Reconciliation stays inside the existing Banking workspace and the approved one-window shell remains binding.' -ForegroundColor Green

Write-Host '[13/19] Rechecking data-driven categories/funds, personalized currency, and protected posting...'
foreach ($relative in @(
    'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs',
    'src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs',
    'src\ChurchBooks.App\Views\GivingWorkspaceView.xaml',
    'src\ChurchBooks.App\Views\BankingWorkspaceView.xaml'
)) {
    foreach ($name in @('Missions Fund','Building Fund','Love Offering','Youth Ministry')) {
        Reject-Text -RelativePath $relative -RejectedText $name
    }
}
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'FundAccountingEngine'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'PostJournalAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs' -ExpectedText 'Reconciliation.ApplyPersonalization(BaseCurrency);'
Write-Host '[PASS] Dynamic church configuration, fund-aware deposit posting, and personalized currency remain intact.' -ForegroundColor Green

Write-Host '[14/19] Rechecking dependency, exact-balance, and Smart Import package safeguards...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Journals\JournalEntry.cs' -ExpectedText 'TotalDebit == TotalCredit'
Require-Text -RelativePath 'Directory.Packages.props' -ExpectedText 'ExcelDataReader'
Reject-Text -RelativePath 'Directory.Packages.props' -RejectedText 'System.Text.Encoding.CodePages'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);'
Require-File -RelativePath 'src\ChurchBooks.App\Assets\Brand\ChurchBooks.ico' | Out-Null
Write-Host '[PASS] Frozen accounting/dependency/branding safeguards remain present.' -ForegroundColor Green

Write-Host '[15/19] Verifying clean focused Phase 8 tests and integration coverage...'
$phase8FocusedTests = @(
    'tests\ChurchBooks.Accounting.Tests\Phase8ReconciliationTests.cs',
    'tests\ChurchBooks.Data.Tests\Phase8ReconciliationDataTests.cs',
    'tests\ChurchBooks.App.Tests\Phase8ReconciliationUxTests.cs'
)
foreach ($relativeTest in $phase8FocusedTests) {
    $testPath = Require-File -RelativePath $relativeTest
    $testLines = @(Get-Content -LiteralPath $testPath)
    foreach ($line in $testLines) {
        if ($line -match '^\s*\[Fact\]\s+public') {
            throw ('Compressed same-line [Fact] declaration is not allowed in clean Phase 8 tests: ' + $relativeTest)
        }
        if ($line.Length -gt 180) {
            throw ('Phase 8 focused test line exceeds 180 characters: ' + $relativeTest)
        }
    }
}
Require-Text -RelativePath 'tests\ChurchBooks.Accounting.Tests\Phase8ReconciliationTests.cs' -ExpectedText 'Matcher_RefusesAmbiguousExactMatch'
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\Phase8ReconciliationDataTests.cs' -ExpectedText 'CompletedReconciliation_LocksMatchesAndReturnsClearedJournal'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase8ReconciliationUxTests.cs' -ExpectedText 'ReconciliationView_ExplainsNoAutomaticAdjustment'
Write-Host '[PASS] Phase 8 has readable domain, SQLite, and WPF integration regression coverage.' -ForegroundColor Green

Write-Host '[16/19] Verifying Phase 8 roadmap boundary and fail-closed release runner...'
Require-Text -RelativePath 'docs\PHASE-8-BANK-RECONCILIATION.md' -ExpectedText 'Completed reconciliations are locked.'
Require-Text -RelativePath 'docs\PHASE-8-BANK-RECONCILIATION.md' -ExpectedText 'never creates an automatic balancing journal'
Require-Text -RelativePath 'LICENSE-NOT-IN-SCOPE.md' -ExpectedText 'Licensing'
Require-Text -RelativePath 'scripts\Verify-Phase8.ps1' -ExpectedText '$LASTEXITCODE'
Require-Text -RelativePath 'scripts\Verify-Phase8.ps1' -ExpectedText 'Build succeeded.'
Require-Text -RelativePath 'scripts\Verify-Phase8.ps1' -ExpectedText 'Build FAILED.'
Require-Text -RelativePath 'scripts\Verify-Phase8.ps1' -ExpectedText '--disable-build-servers'
Write-Host '[PASS] Phase 8 scope and fail-closed Windows verification contract are explicit.' -ForegroundColor Green

if ($StaticOnly) {
    Write-Host '[PASS] ChurchBooks Phase 8 R1.1 static verifier contract passed against canonical payload.' -ForegroundColor Green
    Write-Host '[INFO] Windows restore/build, 221/221 TRX verification, and fail-closed WPF smoke remain authoritative during staged verification.'
    exit 0
}

Write-Host '[17/19] Restoring NuGet packages with complete vulnerability audit...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @(
    'restore','ChurchBooks.sln','--disable-build-servers','-p:NuGetAudit=true','-p:NuGetAuditMode=all'
) -DisplayName 'dotnet restore + complete NuGet audit' -TimeoutSeconds 180 -RejectedOutputText @('error NU','Restore failed')

Write-Host '[18/19] Building Release x64...'
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

$testResultsRoot = Join-Path $env:TEMP ('ChurchBooks-Phase8-TRX-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testResultsRoot -Force | Out-Null
try {
    Write-Host '[19/19] Running complete protected regression + Phase 8 tests with machine-readable TRX verification...'
    $verifiedPassed = 0
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Accounting.Tests\ChurchBooks.Accounting.Tests.csproj' -DisplayName 'ChurchBooks.Accounting.Tests' -TrxFileName 'accounting.trx' -ExpectedTotal 95 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Core.Tests\ChurchBooks.Core.Tests.csproj' -DisplayName 'ChurchBooks.Core.Tests' -TrxFileName 'core.trx' -ExpectedTotal 4 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Data.Tests\ChurchBooks.Data.Tests.csproj' -DisplayName 'ChurchBooks.Data.Tests' -TrxFileName 'data.trx' -ExpectedTotal 69 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.App.Tests\ChurchBooks.App.Tests.csproj' -DisplayName 'ChurchBooks.App.Tests' -TrxFileName 'app.trx' -ExpectedTotal 53 -ResultsDirectory $testResultsRoot
    if ($verifiedPassed -ne 221) { throw ('Unexpected aggregate Phase 8 TRX result. Passed=' + $verifiedPassed + ' Expected=221') }
    Write-Host '[PASS] Protected regression plus Phase 8 coverage: 221/221 verified from machine-readable TRX results.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testResultsRoot) { Remove-Item -LiteralPath $testResultsRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host '[19/19 - dependency] Checking resolved dependencies and Phase 8 identity...'
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
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase8-Bank-Reconciliation";'
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string ReleaseRevision = "R1.1";'
Write-Host '[PASS] Dependency resolution, security boundary, and Phase 8 identity are correct.' -ForegroundColor Green

Write-Host '[19/19 - smoke] Running fail-closed WPF main-window smoke test...'
if ($SkipWpfSmoke) {
    Write-Host '[SKIP] WPF smoke was explicitly skipped.' -ForegroundColor Yellow
}
else {
    $appExe = Join-Path $ProjectRoot 'src\ChurchBooks.App\bin\Release\net10.0-windows\ChurchBooks.App.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw ('Built WPF executable was not found: ' + $appExe) }

    $appProcess = $null
    $smokeRoot = Join-Path $env:TEMP ('ChurchBooks-Phase8-WpfSmoke-' + [guid]::NewGuid().ToString('N'))
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

        $databaseDeadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $databaseDeadline -and -not (Test-Path -LiteralPath $smokeDatabasePath)) {
            Start-Sleep -Milliseconds 250
            $appProcess.Refresh()
            if ($appProcess.HasExited) { throw ('ChurchBooks.App exited while waiting for isolated database initialization. ExitCode=' + $appProcess.ExitCode) }
        }
        if (-not (Test-Path -LiteralPath $smokeDatabasePath)) { throw 'WPF smoke did not create the isolated verification database within 20 seconds.' }
        if ((Get-Item -LiteralPath $smokeDatabasePath).Length -le 0) { throw 'WPF smoke created an empty verification database.' }

        Write-Host ('[PASS] ChurchBooks WPF main window and isolated schema-v8 database created. PID=' + $appProcess.Id + ' HWND=' + $appProcess.MainWindowHandle) -ForegroundColor Green
    }
    finally {
        if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue }
        $env:CHURCHBOOKS_DATABASE_PATH = $priorDatabasePath
        if (Test-Path -LiteralPath $smokeRoot) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host '[19/19 - boundary] Rechecking Smart Import no-auto-post and Phase 9 deferred boundaries...'
if (-not [bool]$manifest.smartImport.previewFirst -or [bool]$manifest.smartImport.automaticPosting) {
    throw 'Smart Import must remain preview-first with automatic posting disabled.'
}
if ([bool]$manifest.bankReconciliation.automaticAdjustmentJournals) {
    throw 'Bank reconciliation automatic adjustment journals must remain disabled.'
}
if ([bool]$manifest.licensingImplemented) {
    throw 'Licensing must remain deferred after Phase 8.'
}
Write-Host '[PASS] Phase 8 reconciles statement-to-book evidence without automatic adjustment posting; later bank feeds/licensing remain separate.' -ForegroundColor Green
Write-Host ''
Write-Host '[PASS] ChurchBooks Phase 8 R1.1 Bank Reconciliation verification passed.' -ForegroundColor Green
exit 0
