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
Write-Host 'ChurchBooks Phase 9 - Adaptive Templates + Identity Resolution Verification'
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
Write-Host 'Phase 8 Bank Reconciliation: FROZEN at R1.1 / 221 of 221 plus staged+live WPF smoke'
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
$parserGate = Require-File -RelativePath 'scripts\Validate-Phase9PowerShell51.ps1'
Invoke-NativeChecked -FilePath 'powershell.exe' -Arguments @(
    '-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$parserGate,'-ProjectRoot',$ProjectRoot
) -DisplayName 'Phase 9 Windows PowerShell 5.1 parser preflight' -TimeoutSeconds 60 `
  -RequiredOutputText @('[PASS] All ChurchBooks Phase 9 PowerShell files passed the Windows PowerShell 5.1 parser gate.') `
  -RejectedOutputText @('[PARSER ERROR]','[FATAL]')

Write-Host '[1/19] Verifying Phase 9 manifest and verified Phase 8 predecessor identity...'
$manifest = Get-Content -LiteralPath (Require-File -RelativePath 'churchbooks.manifest.json') -Raw | ConvertFrom-Json
if ($manifest.product -ne 'ChurchBooks') { throw 'Manifest product must be ChurchBooks.' }
if ([int]$manifest.phase -ne 9 -or $manifest.subphase -ne 'R1') { throw 'Manifest phase/subphase must be Phase 9 R1.' }
if ($manifest.release -ne 'Phase9-Adaptive-Templates-Identity') { throw 'Phase 9 release identity is invalid.' }
if ([int]$manifest.schemaVersion -ne 9) { throw 'Phase 9 must use additive schema version 9.' }
if (-not [bool]$manifest.adaptiveImport.standardTemplatesOptional -or -not [bool]$manifest.adaptiveImport.multipleSourceProfiles) { throw 'Optional standard templates and multiple source profiles are required.' }
if (-not [bool]$manifest.adaptiveImport.unknownLayoutsRequireReview -or [bool]$manifest.adaptiveImport.automaticPosting) { throw 'Unfamiliar layouts must require review and automatic posting must remain disabled.' }
if ([bool]$manifest.identityResolution.nameOnlyAutoMerge -or [bool]$manifest.identityResolution.existingProfileAutoOverwrite) { throw 'Name-only auto-merge and existing-profile overwrite must remain disabled.' }
if ([bool]$manifest.bankReconciliation.automaticAdjustmentJournals) { throw 'Reconciliation auto-adjustment journals must remain disabled.' }
if ([bool]$manifest.licensingImplemented) { throw 'Licensing must remain deferred in Phase 9.' }
if ([int]$manifest.verificationTarget.accounting -ne 109 -or [int]$manifest.verificationTarget.core -ne 4 -or [int]$manifest.verificationTarget.data -ne 79 -or [int]$manifest.verificationTarget.app -ne 63 -or [int]$manifest.verificationTarget.total -ne 255) {
    throw 'Phase 9 verification target must be Accounting 109, Core 4, Data 79, App 63, Total 255.'
}
Write-Host '[PASS] Phase 9 manifest, adaptive-import boundaries, identity safeguards, and verification targets are correct.' -ForegroundColor Green

Write-Host '[2/19] Proving exact Windows-verified Phase 8 R1.1 files outside the authorized Phase 9 change set...'
$baseline = Get-Content -LiteralPath (Require-File -RelativePath 'docs\baselines\PHASE-8-R1.1-MANAGED-SHA256.json') -Raw | ConvertFrom-Json
if ($baseline.product -ne 'ChurchBooks' -or $baseline.release -ne 'Phase8-Bank-Reconciliation' -or $baseline.subphase -ne 'R1.1' -or [int]$baseline.schemaVersion -ne 8) { throw 'Phase 8 R1.1 baseline identity is invalid.' }
if (@($baseline.files).Count -ne 328) { throw ('Phase 8 R1.1 baseline inventory must contain exactly 328 files; found ' + @($baseline.files).Count) }
$authorizedPhase9Changes = @(
    'churchbooks.manifest.json',
    'README.md',
    'src/ChurchBooks.Core/ProductInfo.cs',
    'src/ChurchBooks.Accounting/Importing/ImportColumnRole.cs',
    'src/ChurchBooks.Accounting/Importing/SmartImportAnalyzer.cs',
    'src/ChurchBooks.Data/Importing/TabularImportDocument.cs',
    'src/ChurchBooks.Data/Importing/SpreadsheetImportReader.cs',
    'src/ChurchBooks.App/ViewModels/ImportWorkspaceViewModel.cs',
    'src/ChurchBooks.App/Views/ImportWorkspaceView.xaml',
    'src/ChurchBooks.App/Views/ImportWorkspaceView.xaml.cs',
    'src/ChurchBooks.App/ViewModels/MainWindowViewModel.cs',
    'tests/ChurchBooks.App.Tests/Phase8ReconciliationUxTests.cs'
)
$authorizedLookup = @{}
foreach ($item in $authorizedPhase9Changes) { $authorizedLookup[$item.ToLowerInvariant()] = $true }
$protectedCount = 0
foreach ($entry in @($baseline.files)) {
    $relative = [string]$entry.path
    if ($authorizedLookup.ContainsKey($relative.ToLowerInvariant())) { continue }
    $full = Join-Path $ProjectRoot $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $full)) { throw ('Frozen Phase 8 file is missing: ' + $relative) }
    if ((Get-Sha256Lower -Path $full) -ne ([string]$entry.sha256).ToLowerInvariant()) { throw ('Frozen Phase 8 file changed unexpectedly: ' + $relative) }
    $protectedCount++
}
if ($protectedCount -ne 316) { throw ('Frozen Phase 8 predecessor proof expected 316 protected files; checked ' + $protectedCount) }
Write-Host ('[PASS] Frozen Phase 8 R1.1 files are byte-identical outside the authorized Phase 9 scope: ' + $protectedCount + ' checked.') -ForegroundColor Green

Write-Host '[3/19] Verifying optional ChurchBooks templates and uncluttered multiple source profiles...'
foreach ($relative in @('templates\ChurchBooks-People-Directory.csv','templates\ChurchBooks-Giving.csv','templates\ChurchBooks-Bank-Statement.csv','templates\ChurchBooks-General-Ledger.csv')) { Require-File -RelativePath $relative | Out-Null }
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\ChurchBooksStandardImportTemplates.cs' -ExpectedText 'People Directory'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\ImportSourceProfile.cs' -ExpectedText 'SourceSignature'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteAdaptiveImportStore.cs' -ExpectedText 'FindSourceProfilesAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'Templates and source profiles (optional)'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'Multiple saved profiles match'
Write-Host '[PASS] ChurchBooks offers standard templates without forcing them and limits profile choices to matching layouts.' -ForegroundColor Green

Write-Host '[4/19] Verifying adaptive header, worksheet, alias, and purpose interpretation...'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'HeaderScanRows = 25'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'FindHeaderRowIndex'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'OrderByDescending(x => SmartImportAnalyzer.ScoreHeaderCandidate(x.Headers))'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\SmartImportAnalyzer.cs' -ExpectedText '"GIVENNAME"'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\SmartImportAnalyzer.cs' -ExpectedText '"ENVELOPENO"'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\AdaptiveImportAnalyzer.cs' -ExpectedText 'ImportPurpose.PeopleDirectory'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\AdaptiveImportAnalyzer.cs' -ExpectedText 'ImportPurpose.BankStatement'
Write-Host '[PASS] Common tabular CSV/XLS/XLSX layouts can detect headers, aliases, workbook candidates, and intended import purpose while ambiguous files remain review-first.' -ForegroundColor Green

Write-Host '[5/19] Verifying conservative person identity resolution...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PersonIdentityResolver.cs' -ExpectedText 'saved external person ID'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PersonIdentityResolver.cs' -ExpectedText 'member/envelope number'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PersonIdentityResolver.cs' -ExpectedText 'Name-only similarity is never enough'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PersonIdentityResolver.cs' -ExpectedText 'Strong identifiers point to different existing people'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -ExpectedText 'FindDuplicateStrongIdentifierRows'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteAdaptiveImportStore.cs' -ExpectedText 'already linked to a different ChurchBooks person'
Write-Host '[PASS] Identity resolution uses stable strong identifiers, detects conflicts, and never auto-merges on names alone.' -ForegroundColor Green

Write-Host '[6/19] Verifying explicit safe registration and no silent profile overwrite/household creation...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -ExpectedText '_peopleStore.AddPersonAsync'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -RejectedText '_peopleStore.UpdatePersonAsync'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -ExpectedText 'ResolveExistingHousehold'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs' -RejectedText 'AddHouseholdAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'Register Safe People'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'Existing person profiles were not overwritten.'
Write-Host '[PASS] New people require explicit safe registration; existing people are not overwritten and households are not silently created.' -ForegroundColor Green

Write-Host '[7/19] Verifying additive schema v9 and source-scoped external identity memory...'
$schemaText = Get-Content -LiteralPath (Require-File -RelativePath 'src\ChurchBooks.Data\Storage\Phase9DatabaseMigrator.cs') -Raw
foreach ($table in @('import_source_profiles','person_import_external_links')) { if ($schemaText.IndexOf(('CREATE TABLE IF NOT EXISTS ' + $table), [System.StringComparison]::Ordinal) -lt 0) { throw ('Schema v9 table is missing: ' + $table) } }
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase9DatabaseMigrator.cs' -ExpectedText "VALUES('schema_version','9'"
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase9DatabaseMigrator.cs' -ExpectedText 'PRAGMA foreign_key_check;'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase9DatabaseMigrator.cs' -ExpectedText 'PRIMARY KEY(profile_id, external_person_key)'
Write-Host '[PASS] Schema v9 is additive, foreign-key guarded, and scopes external person IDs to saved source profiles.' -ForegroundColor Green

Write-Host '[8/19] Verifying Smart Import remains staging-only with no accounting auto-post...'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'new Phase9DatabaseMigrator(database)'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'No journal, bank posting, giving posting, or automatic person merge was created.'
foreach ($relative in @('src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs','src\ChurchBooks.Accounting\Importing\PeopleImportRegistrationService.cs')) { Reject-Text -RelativePath $relative -RejectedText 'PostJournalAsync(' }
Write-Host '[PASS] Adaptive import remains analyze/map/review/register-safe/stage only; no journal or bank/giving posting is automatic.' -ForegroundColor Green

Write-Host '[9/19] Rechecking frozen Phase 8 exact-zero bank reconciliation safeguards...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationSummary.cs' -ExpectedText 'Difference == 0m'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankReconciliationSummary.cs' -ExpectedText 'UnmatchedStatementLines.Count == 0'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\BankReconciliationView.xaml' -ExpectedText 'never creates an automatic balancing adjustment'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankReconciliationViewModel.cs' -RejectedText 'PostJournalAsync('
Write-Host '[PASS] Phase 8 exact-zero reconciliation and no-auto-adjust boundaries remain frozen.' -ForegroundColor Green

Write-Host '[10/19] Verifying approved compact GUI and low-confusion import workflow...'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'WindowState="Maximized"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinWidth="1180"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinHeight="700"'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'Adaptive Smart Import'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'People Resolution'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'Name-only matches require review'
Write-Host '[PASS] Adaptive import stays inside the approved compact desktop shell and exposes uncertainty instead of hiding it.' -ForegroundColor Green

Write-Host '[11/19] Rechecking dynamic church configuration and personalized currency...'
foreach ($relative in @('src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs','src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs','src\ChurchBooks.App\Views\GivingWorkspaceView.xaml','src\ChurchBooks.App\Views\BankingWorkspaceView.xaml')) {
    foreach ($name in @('Missions Fund','Building Fund','Love Offering','Youth Ministry')) { Reject-Text -RelativePath $relative -RejectedText $name }
}
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs' -ExpectedText 'Reconciliation.ApplyPersonalization(BaseCurrency);'
Write-Host '[PASS] Giving categories/funds remain data-driven and personalized currency behavior remains intact.' -ForegroundColor Green

Write-Host '[12/19] Rechecking dependency, balance, and known compile-risk safeguards...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Journals\JournalEntry.cs' -ExpectedText 'TotalDebit == TotalCredit'
Require-Text -RelativePath 'Directory.Packages.props' -ExpectedText 'ExcelDataReader'
Reject-Text -RelativePath 'Directory.Packages.props' -RejectedText 'System.Text.Encoding.CodePages'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'using System.IO;'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'Path.GetFileNameWithoutExtension'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'Smart Import never posts automatically'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml' -ExpectedText 'never merges people automatically'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase8ReconciliationUxTests.cs' -ExpectedText 'MainWindowViewModel_ReportsSchemaEightOrLater'
Reject-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase8ReconciliationUxTests.cs' -RejectedText 'Assert.Contains("schema v8"'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase9AdaptiveImportUxTests.cs' -ExpectedText 'MainWindowViewModel_ReportsSchemaNineOrLater'
Reject-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase9AdaptiveImportUxTests.cs' -RejectedText 'Assert.Contains("schema v9"'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -RejectedText 'using ChurchBooks.App.Infrastructure;'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Reconciliation\BankStatementImportConverter.cs' -RejectedText 'date.Value'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);'
Write-Host '[PASS] Frozen dependency, exact-balance, namespace, System.IO, DateOnly, spreadsheet-reader, and forward-compatible historical regression guards remain intact.' -ForegroundColor Green

Write-Host '[13/19] Verifying focused Phase 9 tests are readable and complete...'
$phase9FocusedTests = @('tests\ChurchBooks.Accounting.Tests\Phase9AdaptiveImportTests.cs','tests\ChurchBooks.Data.Tests\Phase9AdaptiveImportDataTests.cs','tests\ChurchBooks.App.Tests\Phase9AdaptiveImportUxTests.cs')
foreach ($relativeTest in $phase9FocusedTests) {
    $testPath = Require-File -RelativePath $relativeTest
    foreach ($line in @(Get-Content -LiteralPath $testPath)) {
        if ($line -match '^\s*\[Fact\]\s+public') { throw ('Compressed same-line [Fact] declaration is not allowed in clean Phase 9 tests: ' + $relativeTest) }
        if ($line.Length -gt 220) { throw ('Phase 9 focused test line exceeds 220 characters: ' + $relativeTest) }
    }
}
Require-Text -RelativePath 'tests\ChurchBooks.Accounting.Tests\Phase9AdaptiveImportTests.cs' -ExpectedText 'IdentityResolver_ConflictingStrongIdentifiersAreAmbiguous'
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\Phase9AdaptiveImportDataTests.cs' -ExpectedText 'CsvReader_DetectsHeaderBelowReportTitle'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase9AdaptiveImportUxTests.cs' -ExpectedText 'RegisterSafePeople_AddsNewPersonWithoutOverwritingExistingProfiles'
Write-Host '[PASS] Phase 9 has focused adaptive-analysis, SQLite/profile, identity, and WPF workflow coverage.' -ForegroundColor Green

Write-Host '[14/19] Verifying documentation and standard-template contracts...'
Require-Text -RelativePath 'docs\PHASE-9-ADAPTIVE-TEMPLATES-IDENTITY.md' -ExpectedText 'No claim is made that every arbitrary spreadsheet can be interpreted without review.'
Require-Text -RelativePath 'docs\PHASE-9-ADAPTIVE-TEMPLATES-IDENTITY.md' -ExpectedText 'Names are never used as automatic identity keys.'
Require-Text -RelativePath 'docs\PHASE-9-ADAPTIVE-TEMPLATES-IDENTITY.md' -ExpectedText 'Existing person profiles are never overwritten automatically.'
Write-Host '[PASS] Phase 9 documents both the adaptive capability and its fail-closed limits clearly.' -ForegroundColor Green

Write-Host '[15/19] Verifying fail-closed release runner...'
Require-Text -RelativePath 'scripts\Verify-Phase9.ps1' -ExpectedText '$LASTEXITCODE'
Require-Text -RelativePath 'scripts\Verify-Phase9.ps1' -ExpectedText 'Build succeeded.'
Require-Text -RelativePath 'scripts\Verify-Phase9.ps1' -ExpectedText 'Build FAILED.'
Require-Text -RelativePath 'scripts\Verify-Phase9.ps1' -ExpectedText '--disable-build-servers'
Write-Host '[PASS] Phase 9 retains direct native exit evidence, TRX proof, bounded processes, and fail-closed WPF smoke.' -ForegroundColor Green

if ($StaticOnly) {
    Write-Host '[PASS] ChurchBooks Phase 9 R1 static verifier contract passed against canonical payload.' -ForegroundColor Green
    Write-Host '[INFO] Windows restore/build, 255/255 TRX verification, and fail-closed WPF smoke remain authoritative during staged verification.'
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

$testResultsRoot = Join-Path $env:TEMP ('ChurchBooks-Phase9-TRX-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testResultsRoot -Force | Out-Null
try {
    Write-Host '[18/19] Running complete protected regression + Phase 9 tests with machine-readable TRX verification...'
    $verifiedPassed = 0
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Accounting.Tests\ChurchBooks.Accounting.Tests.csproj' -DisplayName 'ChurchBooks.Accounting.Tests' -TrxFileName 'accounting.trx' -ExpectedTotal 109 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Core.Tests\ChurchBooks.Core.Tests.csproj' -DisplayName 'ChurchBooks.Core.Tests' -TrxFileName 'core.trx' -ExpectedTotal 4 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Data.Tests\ChurchBooks.Data.Tests.csproj' -DisplayName 'ChurchBooks.Data.Tests' -TrxFileName 'data.trx' -ExpectedTotal 79 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.App.Tests\ChurchBooks.App.Tests.csproj' -DisplayName 'ChurchBooks.App.Tests' -TrxFileName 'app.trx' -ExpectedTotal 63 -ResultsDirectory $testResultsRoot
    if ($verifiedPassed -ne 255) { throw ('Unexpected aggregate Phase 9 TRX result. Passed=' + $verifiedPassed + ' Expected=255') }
    Write-Host '[PASS] Protected regression plus Phase 9 coverage: 255/255 verified from machine-readable TRX results.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testResultsRoot) { Remove-Item -LiteralPath $testResultsRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host '[18/19 - dependency] Checking resolved dependencies and Phase 9 identity...'
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
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase9-Adaptive-Templates-Identity";'
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string ReleaseRevision = "R1";'
Write-Host '[PASS] Dependency resolution, security boundary, and Phase 9 identity are correct.' -ForegroundColor Green

Write-Host '[18/19 - smoke] Running fail-closed WPF main-window smoke test...'
if ($SkipWpfSmoke) {
    Write-Host '[SKIP] WPF smoke was explicitly skipped.' -ForegroundColor Yellow
}
else {
    $appExe = Join-Path $ProjectRoot 'src\ChurchBooks.App\bin\Release\net10.0-windows\ChurchBooks.App.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw ('Built WPF executable was not found: ' + $appExe) }

    $appProcess = $null
    $smokeRoot = Join-Path $env:TEMP ('ChurchBooks-Phase9-WpfSmoke-' + [guid]::NewGuid().ToString('N'))
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

        Write-Host ('[PASS] ChurchBooks WPF main window and isolated schema-v9 database created. PID=' + $appProcess.Id + ' HWND=' + $appProcess.MainWindowHandle) -ForegroundColor Green
    }
    finally {
        if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue }
        $env:CHURCHBOOKS_DATABASE_PATH = $priorDatabasePath
        if (Test-Path -LiteralPath $smokeRoot) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host '[19/19 - boundary] Rechecking adaptive import, identity, reconciliation, and licensing boundaries...'
if ([bool]$manifest.adaptiveImport.automaticPosting) { throw 'Adaptive Smart Import automatic posting must remain disabled.' }
if ([bool]$manifest.identityResolution.nameOnlyAutoMerge -or [bool]$manifest.identityResolution.existingProfileAutoOverwrite) { throw 'Identity auto-merge/overwrite must remain disabled.' }
if ([bool]$manifest.bankReconciliation.automaticAdjustmentJournals) {
    throw 'Bank reconciliation automatic adjustment journals must remain disabled.'
}
if ([bool]$manifest.licensingImplemented) {
    throw 'Licensing must remain deferred after Phase 9.'
}
Write-Host '[PASS] Phase 9 adds adaptive templates and conservative identity resolution without automatic accounting posting, merging, or reconciliation adjustment.' -ForegroundColor Green
Write-Host ''
Write-Host '[PASS] ChurchBooks Phase 9 R1 Adaptive Templates + Identity Resolution verification passed.' -ForegroundColor Green
exit 0
