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
Write-Host 'ChurchBooks Phase 5 - Individual Offerings + Analytics Verification'
Write-Host '============================================================'
Write-Host ('Project: ' + $ProjectRoot)
Write-Host 'Phase 2 accounting kernel: FROZEN / protected'
Write-Host 'Phase 3B fund-accounting kernel: VERIFIED / protected'
Write-Host 'Phase 3C Fund Manager + Familiar Start: VERIFIED / protected'
Write-Host 'Phase 3D Fund Reports + Integrity: VERIFIED / protected'
Write-Host 'Phase 4 People + Giving Directory: PRODUCT TESTS 108/108 / predecessor protected'
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

Write-Host '[0/17] Running Windows PowerShell 5.1 parser preflight...'
$parserGate = Require-File -RelativePath 'scripts\Validate-Phase5PowerShell51.ps1'
Invoke-NativeChecked -FilePath 'powershell.exe' -Arguments @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$parserGate,'-ProjectRoot',$ProjectRoot) -DisplayName 'Phase 5 Windows PowerShell 5.1 parser preflight' -TimeoutSeconds 60 -RequiredOutputText @('[PASS] All ChurchBooks Phase 5 PowerShell files passed the Windows PowerShell 5.1 parser gate.') -RejectedOutputText @('[PARSER ERROR]','[FATAL]')

Write-Host '[1/17] Verifying Phase 5 manifest and phase boundaries...'
$manifest = Get-Content -LiteralPath (Require-File -RelativePath 'churchbooks.manifest.json') -Raw | ConvertFrom-Json
if ($manifest.product -ne 'ChurchBooks') { throw 'Manifest product identity is invalid.' }
if ([int]$manifest.phase -ne 5 -or $manifest.subphase -ne 'R1.3') { throw 'Manifest phase/subphase must be Phase 5 R1.3.' }
if ($manifest.release -ne 'Phase5-Individual-Offerings-Analytics') { throw 'Manifest release identity is invalid.' }
if ([int]$manifest.schemaVersion -ne 5) { throw 'Phase 5 must use additive schema version 5.' }
foreach ($flag in @('accountingKernel','fundAccounting','peopleDirectory','givingCategories','givingTransactions','serviceOfferingWorkflow','individualOfferingBreakdown','weeklyOfferingTotals','monthlyOfferingTotals','yearlyOfferingTotals','approvedGuiBaseline','dataDrivenGivingCategories','dataDrivenFunds','starterTithesCategory','otherGivingCategoryNamesDataDriven','fundNamesDataDriven')) {
    if (-not [bool]$manifest.$flag) { throw ('Manifest flag must be true: ' + $flag) }
}
if ([bool]$manifest.bankDepositPosting -or [bool]$manifest.generalLedgerContributionPosting) { throw 'Phase 5 must not post bank deposits or GL contribution journals before Phase 6.' }
if ([bool]$manifest.licensingImplemented) { throw 'Licensing must remain deferred.' }
if ($manifest.verificationRunner -ne 'phase5-direct-native-exit-evidence-v3') { throw 'Phase 5 verifier identity is invalid.' }
Write-Host '[PASS] Phase 5 manifest and boundaries are correct.' -ForegroundColor Green

Write-Host '[2/17] Proving protected Phase 2-4 product baselines...'
$protectedHashes = @{
    'src\ChurchBooks.Accounting\Journals\JournalEntry.cs' = 'b2fa616c079735133c34fe6428cefa3f02541b37edba346ffe12df545efe49f5'
    'src\ChurchBooks.Accounting\Journals\JournalLine.cs' = 'ea4ccf4bb96a3c17bb0e7d0beba413cfc1c8accdcd5b7edba3256a5e749671eb'
    'src\ChurchBooks.Accounting\Journals\JournalEntryValidator.cs' = '2b152a964879d7589e21638c7f4acebecd4b766a01069542088a89468fd4d14c'
    'src\ChurchBooks.Accounting\Services\AccountingEngine.cs' = 'f99c9261920a85d9ba1cfeca38dee7d59c80aa68d57ec1745a2ca002cd8674a1'
    'src\ChurchBooks.Data\Storage\SqliteAccountingStore.cs' = '5c72dffbda03cb1822d816fbc25616f5d65d7667a1678f538216c36b8bfcbf4b'
    'src\ChurchBooks.Accounting\Funds\Fund.cs' = '60ee94c37c6d062a92f80e47c390ed97ad77147b807511b39e2e9692e6efa6a7'
    'src\ChurchBooks.Accounting\Funds\FundJournalEntry.cs' = 'b727696368b7fb9afd773595d0cd2fbbe4b83b9d32b455dd3ea875ddbec26404'
    'src\ChurchBooks.Accounting\Funds\FundJournalValidator.cs' = 'd1ee1742a07ef43c4c4f4924962fe976f932afe48397f5acd6f9a8fa65c1f258'
    'src\ChurchBooks.Accounting\Services\FundAccountingEngine.cs' = 'c6ff72a04dab5fb793c1648c00c6171c856cc6384f36901c7711f62b3e1000fe'
    'src\ChurchBooks.Data\Storage\FundAccountingDatabaseMigrator.cs' = 'b54417560da9dc1dca22de48da0cb7d36c8e169db2f1f136a06199f2cc93d491'
    'src\ChurchBooks.Accounting\People\PersonProfile.cs' = '1ace2f59d93095e78bb3c63fa180e63deb549cbc71bc3911e79230f8d28c6602'
    'src\ChurchBooks.Accounting\People\Household.cs' = '24c6044aa6872ccbf85dd126ef924abbbde726f25e4487e6d4ecaa293085b4d6'
    'src\ChurchBooks.Accounting\Giving\GivingCategory.cs' = '5d161b53a99461d3bf42549184fa2bd5be803c99735867b589b14f81e7279f03'
    'src\ChurchBooks.Accounting\Services\PeopleGivingManagementService.cs' = 'a11682e7f12f625ad58b2b4be00f0a3fb13713194b70b1233a1b884fad41b8cc'
    'src\ChurchBooks.Data\Storage\PeopleGivingDatabaseMigrator.cs' = '020d55fa72a68fecafe129b3844892b9cb2c3b1bface17b4c351b993e3629bcc'
    'src\ChurchBooks.Data\Storage\SqlitePeopleGivingStore.cs' = 'e2abe7def4a1d4caf7e2dc4df64a2fd8dc8c6d3540801bf07b276c65b28bbf01'
    'src\ChurchBooks.Accounting\Reporting\FundReportingService.cs' = 'f9f07d1a1cf2b2e34e98cb02427a5f593cc5eb38619266c311294edee97eec05'
    'src\ChurchBooks.Data\Storage\SqliteFundIntegrityScanner.cs' = 'ee4f32ab8474a7f35a9e1e5bae6bf48431decc4d2c5c4cff9ec9317850604053'
    'src\ChurchBooks.App\Services\CsvReportExportService.cs' = '812b44d90b74cfcfe599978e688d9b310853e80ad2ccdde1dfc2f7a5b10cc110'
}
foreach ($relativePath in $protectedHashes.Keys) {
    $actual = Get-Sha256Lower -Path (Require-File -RelativePath $relativePath)
    if ($actual -ne $protectedHashes[$relativePath]) { throw ('Protected product baseline drift: ' + $relativePath + ' actual=' + $actual) }
}
Require-Text -RelativePath 'docs\baselines\PHASE-3D-R1.1-VERIFIED.md' -ExpectedText 'Total regression: 86/86 from machine-readable TRX'
Require-File -RelativePath 'docs\baselines\PHASE-4-R1.4-MANAGED-SHA256.json' | Out-Null
Write-Host ('[PASS] Protected accounting, fund, people, and reporting files are byte-identical: ' + $protectedHashes.Count + ' checked.') -ForegroundColor Green

Write-Host '[3/17] Verifying Phase 5 offering domain invariants...'
foreach ($path in @('src\ChurchBooks.Accounting\Abstractions\IOfferingStore.cs','src\ChurchBooks.Accounting\Offerings\OfferingBatch.cs','src\ChurchBooks.Accounting\Offerings\Contribution.cs','src\ChurchBooks.Accounting\Offerings\ContributionLine.cs','src\ChurchBooks.Accounting\Offerings\OfferingAnalyticsCalculator.cs')) { Require-File -RelativePath $path | Out-Null }
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Offerings\Contribution.cs' -ExpectedText 'A contribution requires at least one breakdown line.'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Offerings\Contribution.cs' -ExpectedText 'Combine duplicate giving-category and fund breakdown rows before saving.'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Offerings\ContributionLine.cs' -ExpectedText 'Contribution amount must be greater than zero.'
Write-Host '[PASS] Contribution identity, breakdown, and money invariants are present.' -ForegroundColor Green

Write-Host '[4/17] Verifying additive schema v5 and offering persistence...'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText '005_individual_offerings_analytics'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS offering_batches'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS contributions'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS contribution_lines'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteOfferingStore.cs' -ExpectedText 'PRAGMA foreign_keys = ON;'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteOfferingStore.cs' -ExpectedText 'using var transaction = connection.BeginTransaction();'
Write-Host '[PASS] Schema v5 is additive and offering persistence is transactional/foreign-key guarded.' -ForegroundColor Green

Write-Host '[5/17] Verifying offering management and analytics boundaries...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -ExpectedText 'Mark the person as a donor before recording an offering'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -ExpectedText 'Every contribution line must use an active giving category.'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -ExpectedText 'Every contribution line must use an active fund.'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -RejectedText 'PostJournal'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -RejectedText 'SavePostedJournal'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Offerings\OfferingAnalyticsCalculator.cs' -ExpectedText 'StartOfWeek'
Write-Host '[PASS] Phase 5 records the contribution subsidiary ledger without premature bank/GL posting.' -ForegroundColor Green

Write-Host '[6/17] Verifying Giving WPF workspace and selectable graph options...'
foreach ($path in @('src\ChurchBooks.App\Views\GivingWorkspaceView.xaml','src\ChurchBooks.App\Views\GivingWorkspaceView.xaml.cs','src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs','src\ChurchBooks.App\Controls\OfferingChartControl.cs','src\ChurchBooks.App\Models\OfferingChartKind.cs')) { Require-File -RelativePath $path | Out-Null }
Require-Text -RelativePath 'src\ChurchBooks.App\Models\OfferingChartKind.cs' -ExpectedText 'Pie = 4'
Require-Text -RelativePath 'src\ChurchBooks.App\Models\OfferingChartKind.cs' -ExpectedText 'Line = 1'
Require-Text -RelativePath 'src\ChurchBooks.App\Models\OfferingChartKind.cs' -ExpectedText 'Donut = 5'
Require-Text -RelativePath 'src\ChurchBooks.App\Models\OfferingChartKind.cs' -ExpectedText 'Area = 6'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\GivingWorkspaceView.xaml' -ExpectedText 'This Week'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\GivingWorkspaceView.xaml' -ExpectedText 'This Month'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\GivingWorkspaceView.xaml' -ExpectedText 'This Year'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'Open Giving and individual offering analytics'
Write-Host '[PASS] Individual totals and Line/Column/Bar/Pie/Donut/Area selection are wired into native WPF.' -ForegroundColor Green

Write-Host '[7/17] Verifying roadmap and durable accounting boundary documentation...'
Require-Text -RelativePath 'docs\PHASE-5-INDIVIDUAL-OFFERINGS-ANALYTICS.md' -ExpectedText 'People directory remains the only identity authority'
Require-Text -RelativePath 'docs\PHASE-5-INDIVIDUAL-OFFERINGS-ANALYTICS.md' -ExpectedText 'week, month, year, and all time'
Require-Text -RelativePath 'docs\PHASE-5-INDIVIDUAL-OFFERINGS-ANALYTICS.md' -ExpectedText 'Line, Column, Bar, Pie, Donut, or Area'
Require-Text -RelativePath 'docs\ROADMAP.md' -ExpectedText 'individual contribution breakdowns, weekly/monthly/yearly totals, selectable charts: CURRENT'
Write-Host '[PASS] Requested offering and visualization requirements are durable roadmap contracts.' -ForegroundColor Green

Write-Host '[8/17] Rechecking People directory remains the single registration authority...'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'SqlitePeopleGivingStore'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -RejectedText 'AddPersonAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\GivingWorkspaceView.xaml' -ExpectedText 'Register or edit the person once in People'
Write-Host '[PASS] Phase 5 references People identities instead of creating a second person-registration system.' -ForegroundColor Green

Write-Host '[9/17] Rechecking fund reports and Integrity Center remain protected...'
Require-File -RelativePath 'src\ChurchBooks.Accounting\Reporting\FundReportingService.cs' | Out-Null
Require-File -RelativePath 'src\ChurchBooks.Data\Storage\SqliteFundIntegrityScanner.cs' | Out-Null
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Reporting\FundReportingService.cs' -ExpectedText 'BuildBalanceReportAsync'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteFundIntegrityScanner.cs' -ExpectedText 'ScanAsync'
Write-Host '[PASS] Existing fund reports and integrity scanner remain present and hash protected.' -ForegroundColor Green

Write-Host '[10/17] Running compile-risk namespace preflight...'
$appCsFiles = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src\ChurchBooks.App') -Filter '*.cs' -File -Recurse
foreach ($appCsFile in $appCsFiles) {
    $content = Get-Content -LiteralPath $appCsFile.FullName -Raw
    if (($content.IndexOf('Path.', [System.StringComparison]::Ordinal) -ge 0 -or $content.IndexOf('Directory.', [System.StringComparison]::Ordinal) -ge 0 -or $content.IndexOf('IOException', [System.StringComparison]::Ordinal) -ge 0) -and
        $content.IndexOf('using System.IO;', [System.StringComparison]::Ordinal) -lt 0 -and $content.IndexOf('System.IO.', [System.StringComparison]::Ordinal) -lt 0) {
        throw ('App source uses System.IO without explicit namespace: ' + $appCsFile.FullName)
    }
}
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteOfferingStore.cs' -ExpectedText 'using System.Globalization;'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'using System.Globalization;'
Write-Host '[PASS] Explicit compile-risk namespaces are present.' -ForegroundColor Green

Write-Host '[11/17] Verifying focused Phase 5 tests and analyzer-safe patterns...'
$newTests = @('tests\ChurchBooks.Accounting.Tests\OfferingAnalyticsTests.cs','tests\ChurchBooks.Data.Tests\OfferingDataTests.cs','tests\ChurchBooks.App.Tests\OfferingUxTests.cs')
foreach ($relativePath in $newTests) {
    $text = Get-Content -LiteralPath (Require-File -RelativePath $relativePath) -Raw
    if ($text.IndexOf('Assert.True(', [System.StringComparison]::Ordinal) -ge 0 -and $text.IndexOf('.Any(', [System.StringComparison]::Ordinal) -ge 0) { throw ('Potential xUnit collection analyzer trap: ' + $relativePath) }
}
Write-Host '[PASS] Focused offering/domain/data/UI tests are present.' -ForegroundColor Green

Write-Host '[12/17] Rechecking branding, exact-balance, SQLite, and dependency pins...'
Require-File -RelativePath 'src\ChurchBooks.App\Assets\Brand\ChurchBooks.ico' | Out-Null
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Journals\JournalEntryValidator.cs' -ExpectedText 'Total debits must equal total credits exactly in the base currency.'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\ChurchBooksDatabase.cs' -ExpectedText 'Pooling = false'
Require-Text -RelativePath 'Directory.Packages.props' -ExpectedText 'SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.12"'
Write-Host '[PASS] Frozen accounting/storage safeguards and branding remain intact.' -ForegroundColor Green

Write-Host '[13/17] Verifying fail-closed WPF smoke and release-runner contract...'
$verifyText = Get-Content -LiteralPath (Require-File -RelativePath 'scripts\Verify-Phase5.ps1') -Raw
if ($verifyText.IndexOf('trap { Write-Host (''[FATAL] '' + $_.Exception.Message)', [System.StringComparison]::Ordinal) -lt 0) { throw 'Top-level fail-closed trap is missing.' }
Require-Text -RelativePath 'scripts\Verify-Phase5.ps1' -ExpectedText 'WPF smoke did not create the isolated verification database within 20 seconds.'
Require-Text -RelativePath 'scripts\Verify-Phase5.ps1' -ExpectedText '$LASTEXITCODE'
Require-Text -RelativePath 'scripts\Verify-Phase5.ps1' -ExpectedText 'Build succeeded.'
Require-Text -RelativePath 'scripts\Verify-Phase5.ps1' -ExpectedText 'Build FAILED.'
Require-Text -RelativePath 'scripts\Verify-Phase5.ps1' -ExpectedText '--disable-build-servers'
Write-Host '[PASS] Verifier explicitly converts smoke/runtime exceptions to nonzero process exit and waits for async database initialization.' -ForegroundColor Green
Write-Host '[13/17 - config] Verifying approved GUI and data-driven category/fund contract...'
Require-File -RelativePath 'docs\reference\ChurchBooks-Approved-Dashboard.png' | Out-Null
Require-Text -RelativePath 'docs\APPROVED-GUI-BASELINE.md' -ExpectedText 'Operational code must never special-case'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'GetGivingCategoriesAsync'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'GetAllFundsAsync'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText "'TITHE', 'Tithes'"
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'Other giving categories and all ministry/designation names are created by the user.'
$operationalSourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Recurse -File | Where-Object { $_.Extension -in @('.cs','.xaml') })
foreach ($name in @('Missions','Building Fund','Love Offering','Youth Ministry')) {
    foreach ($sourceFile in $operationalSourceFiles) {
        $sourceText = Get-Content -LiteralPath $sourceFile.FullName -Raw
        if ($sourceText.IndexOf($name, [System.StringComparison]::Ordinal) -ge 0) {
            throw ('Operational source contains a fixed giving/fund name that must be data-driven: ' + $name + ' in ' + $sourceFile.FullName)
        }
    }
}
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'WindowState="Maximized"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinWidth="1180"'
Write-Host '[PASS] Approved desktop GUI baseline and dynamic category/fund lookups are enforced.' -ForegroundColor Green

Write-Host '[13/17 - compile-risk] Verifying OfferingUxTests imports System.IO for Path/Directory usage...'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\OfferingUxTests.cs' -ExpectedText 'using System.IO;'
Write-Host '[PASS] Phase 5 App test compile-risk namespace repair is present.' -ForegroundColor Green
Write-Host '[13/17 - app-stability] Verifying deterministic App test isolation and retry-safe SQLite cleanup...'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\AssemblyInfo.cs' -ExpectedText 'DisableTestParallelization = true'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\OfferingUxTests.cs' -ExpectedText 'DeleteDirectoryWithRetry'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\OfferingUxTests.cs' -ExpectedText 'GC.WaitForPendingFinalizers();'
Write-Host '[PASS] App tests are serialized and temporary SQLite cleanup is bounded/retry-safe.' -ForegroundColor Green
Write-Host '[13/17 - data-repair] Verifying canonical starter-GUID and legacy compact-ID migration repair...'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText "'d8cb9944-f3e1-4ff0-9fbe-7ee9eb521001', 'TITHE', 'Tithes'"
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'PRAGMA defer_foreign_keys = ON;'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'length(giving_category_id) = 32'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\OfferingDatabaseMigrator.cs' -ExpectedText 'PRAGMA foreign_key_check;'
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\OfferingDataTests.cs' -ExpectedText 'RewriteStarterCategoryAsLegacyCompactIdAsync'
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\OfferingDataTests.cs' -ExpectedText 'Assert.Equal(36L'
Write-Host '[PASS] Starter category uses canonical Guid storage and the legacy compact-ID round trip is regression-covered.' -ForegroundColor Green

if ($StaticOnly) {
    Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase5-Individual-Offerings-Analytics";'
    Write-Host '[PASS] Phase 5 R1.3 static verifier contract passed against canonical payload.' -ForegroundColor Green
    Write-Host '[INFO] Windows build, 125/125 TRX verification, and fail-closed WPF smoke remain authoritative during staged verification.' -ForegroundColor Yellow
    exit 0
}

Write-Host '[14/17] Restoring NuGet packages with complete vulnerability audit...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @('restore','ChurchBooks.sln','--disable-build-servers','-p:NuGetAudit=true','-p:NuGetAuditMode=all') -DisplayName 'dotnet restore + complete NuGet audit' -TimeoutSeconds 180 -RejectedOutputText @('error NU','Restore failed')

Write-Host '[15/17] Building Release x64...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @('build','ChurchBooks.sln','-c','Release','-p:Platform=x64','--no-restore','--disable-build-servers') -DisplayName 'Release x64 build' -TimeoutSeconds 180 -RequiredOutputText @('Build succeeded.') -RejectedOutputText @('Build FAILED.',' error CS',' error MSB')

Write-Host '[16/17] Running complete regression + Phase 5 offering tests with machine-readable TRX verification...'
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

$testResultsRoot = Join-Path $env:TEMP ('ChurchBooks-Phase5-TRX-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testResultsRoot -Force | Out-Null
try {
    $verifiedPassed = 0
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Accounting.Tests\ChurchBooks.Accounting.Tests.csproj' -DisplayName 'ChurchBooks.Accounting.Tests' -TrxFileName 'accounting.trx' -ExpectedTotal 65 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Core.Tests\ChurchBooks.Core.Tests.csproj' -DisplayName 'ChurchBooks.Core.Tests' -TrxFileName 'core.trx' -ExpectedTotal 4 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Data.Tests\ChurchBooks.Data.Tests.csproj' -DisplayName 'ChurchBooks.Data.Tests' -TrxFileName 'data.trx' -ExpectedTotal 33 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.App.Tests\ChurchBooks.App.Tests.csproj' -DisplayName 'ChurchBooks.App.Tests' -TrxFileName 'app.trx' -ExpectedTotal 23 -ResultsDirectory $testResultsRoot
    if ($verifiedPassed -ne 125) { throw ('Unexpected aggregate Phase 5 TRX result. Passed=' + $verifiedPassed + ' Expected=125') }
    Write-Host '[PASS] Protected regression plus Phase 5 coverage: 125/125 verified from machine-readable TRX results.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testResultsRoot) { Remove-Item -LiteralPath $testResultsRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host '[17/17] Checking dependency resolution, fail-closed WPF smoke, and Phase 6 boundary...'
$assetFiles = Get-ChildItem -LiteralPath $ProjectRoot -Recurse -Filter 'project.assets.json' -ErrorAction SilentlyContinue
$mvvm = $false
$fs = $false
$fluent = $false
foreach ($assetFile in $assetFiles) {
    $assetText = Get-Content -LiteralPath $assetFile.FullName -Raw
    if ($assetText.IndexOf('SQLitePCLRaw.lib.e_sqlite3/2.1.11', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw ('Vulnerable SQLite package resolved: ' + $assetFile.FullName)
    }
    if ($assetText.IndexOf('CommunityToolkit.Mvvm/8.4.2', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $mvvm = $true }
    if ($assetText.IndexOf('FsCheck.Xunit/3.3.4', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $fs = $true }
    if ($assetText.IndexOf('FluentValidation/12.1.1', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $fluent = $true }
}
if (-not $mvvm -or -not $fs -or -not $fluent) { throw 'Expected pinned dependencies were not all resolved.' }
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase5-Individual-Offerings-Analytics";'
Write-Host '[PASS] Dependency resolution, security boundary, and Phase 5 identity are correct.' -ForegroundColor Green

Write-Host '[17/17 - smoke] Running fail-closed WPF main-window smoke test...'
if ($SkipWpfSmoke) {
    Write-Host '[SKIP] WPF smoke was explicitly skipped.' -ForegroundColor Yellow
}
else {
    $appExe = Join-Path $ProjectRoot 'src\ChurchBooks.App\bin\Release\net10.0-windows\ChurchBooks.App.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw ('Built WPF executable was not found: ' + $appExe) }

    $appProcess = $null
    $smokeRoot = Join-Path $env:TEMP ('ChurchBooks-Phase5-WpfSmoke-' + [guid]::NewGuid().ToString('N'))
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
            if ($appProcess.MainWindowHandle -ne 0) {
                $windowFound = $true
                break
            }
        }
        if (-not $windowFound) { throw 'ChurchBooks.App did not create a visible WPF main window within 20 seconds.' }

        # MainWindow.Loaded initializes SQLite asynchronously.  Do not race the window handle.
        $databaseDeadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $databaseDeadline -and -not (Test-Path -LiteralPath $smokeDatabasePath)) {
            Start-Sleep -Milliseconds 250
            $appProcess.Refresh()
            if ($appProcess.HasExited) { throw ('ChurchBooks.App exited while waiting for isolated database initialization. ExitCode=' + $appProcess.ExitCode) }
        }
        if (-not (Test-Path -LiteralPath $smokeDatabasePath)) { throw 'WPF smoke did not create the isolated verification database within 20 seconds.' }
        if ((Get-Item -LiteralPath $smokeDatabasePath).Length -le 0) { throw 'WPF smoke created an empty verification database.' }

        Write-Host ('[PASS] ChurchBooks WPF main window and isolated schema-v5 database created. PID=' + $appProcess.Id + ' HWND=' + $appProcess.MainWindowHandle) -ForegroundColor Green
    }
    finally {
        if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue }
        $env:CHURCHBOOKS_DATABASE_PATH = $priorDatabasePath
        if (Test-Path -LiteralPath $smokeRoot) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host '[17/17 - boundary] Verifying Phase 6 banking/posting boundary remains explicit...'
Require-Text -RelativePath 'docs\PHASE-5-INDIVIDUAL-OFFERINGS-ANALYTICS.md' -ExpectedText 'Phase 6 owns bank/financial accounts and the deposit-to-ledger bridge.'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -RejectedText 'AccountingEngine'
Write-Host '[PASS] Phase 6 remains the only future bank/deposit-to-GL bridge.' -ForegroundColor Green
Write-Host ''
Write-Host '[PASS] ChurchBooks Phase 5 R1.3 Individual Offerings + Analytics verification passed.' -ForegroundColor Green
exit 0
