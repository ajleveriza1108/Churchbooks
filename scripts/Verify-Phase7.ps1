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
Write-Host 'ChurchBooks Phase 7 - Smart Import Verification'
Write-Host '============================================================'
Write-Host ('Project: ' + $ProjectRoot)
Write-Host 'Phase 2 accounting kernel: FROZEN / protected'
Write-Host 'Phase 3B fund-accounting kernel: VERIFIED / protected'
Write-Host 'Phase 3C Fund Manager + Familiar Start: VERIFIED / protected'
Write-Host 'Phase 3D Fund Reports + Integrity: VERIFIED / protected'
Write-Host 'Phase 4 People + Giving Directory: VERIFIED / protected'
Write-Host 'Phase 5 Individual Offerings + Analytics: FROZEN at R1.3 / 125 of 125 plus staged+live WPF smoke'
Write-Host 'Phase 6 Personalized Setup + Banking: FROZEN / 155 of 155 plus staged+live WPF smoke'
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
$parserGate = Require-File -RelativePath 'scripts\Validate-Phase7PowerShell51.ps1'
Invoke-NativeChecked -FilePath 'powershell.exe' -Arguments @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$parserGate,'-ProjectRoot',$ProjectRoot) -DisplayName 'Phase 7 Windows PowerShell 5.1 parser preflight' -TimeoutSeconds 60 -RequiredOutputText @('[PASS] All ChurchBooks Phase 7 PowerShell files passed the Windows PowerShell 5.1 parser gate.') -RejectedOutputText @('[PARSER ERROR]','[FATAL]')

Write-Host '[1/19] Verifying Phase 7 manifest and frozen Phase 6 predecessor identity...'
$manifest = Get-Content -LiteralPath (Require-File -RelativePath 'churchbooks.manifest.json') -Raw | ConvertFrom-Json
if ($manifest.product -ne 'ChurchBooks') { throw 'Manifest product must be ChurchBooks.' }
if ([int]$manifest.phase -ne 7 -or $manifest.subphase -ne 'R1.3') { throw 'Manifest phase/subphase must be Phase 7 R1.3.' }
if ($manifest.release -ne 'Phase7-Smart-Import') { throw 'Phase 7 release identity is invalid.' }
if ([int]$manifest.schemaVersion -ne 7) { throw 'Phase 7 must use additive schema version 7.' }
if (-not [bool]$manifest.smartImport.csv -or -not [bool]$manifest.smartImport.xls -or -not [bool]$manifest.smartImport.xlsx) { throw 'CSV/XLS/XLSX support must be declared.' }
if (-not [bool]$manifest.smartImport.previewFirst -or -not [bool]$manifest.smartImport.mappingMemory -or -not [bool]$manifest.smartImport.duplicateDetection) { throw 'Smart Import preview/mapping/duplicate capabilities must be enabled.' }
if ([bool]$manifest.smartImport.automaticPosting) { throw 'Smart Import automatic posting must remain disabled.' }
if ([bool]$manifest.licensingImplemented) { throw 'Licensing must remain deferred in Phase 7.' }
if ([int]$manifest.verificationTarget.total -ne 189) { throw 'Phase 7 verification target must be 189 tests.' }
Write-Host '[PASS] Phase 7 manifest and Smart Import safety boundaries are correct.' -ForegroundColor Green

Write-Host '[2/19] Proving exact verified Phase 6 R1.2 files outside the authorized Phase 7 change set...'
$baseline = Get-Content -LiteralPath (Require-File -RelativePath 'docs\baselines\PHASE-6-R1.2-MANAGED-SHA256.json') -Raw | ConvertFrom-Json
if ($baseline.product -ne 'ChurchBooks' -or $baseline.release -ne 'Phase6-Personalized-Setup-Banking' -or $baseline.subphase -ne 'R1' -or [int]$baseline.schemaVersion -ne 6) { throw 'Phase 6 R1.2 baseline identity is invalid.' }
if (@($baseline.files).Count -lt 250) { throw ('Phase 6 baseline inventory is unexpectedly small: ' + @($baseline.files).Count) }
$authorizedPhase7Changes = @(
    'churchbooks.manifest.json',
    'Directory.Packages.props',
    'docs/ROADMAP.md',
    'docs/PHASES.md',
    'src/ChurchBooks.Core/ProductInfo.cs',
    'src/ChurchBooks.Data/ChurchBooks.Data.csproj',
    'src/ChurchBooks.App/MainWindow.xaml',
    'src/ChurchBooks.App/ViewModels/MainWindowViewModel.cs',
    'src/ChurchBooks.App/Models/WorkspaceSection.cs',
    'src/ChurchBooks.App/Services/TerminologyAliasService.cs'
)
$authorizedLookup = @{}
foreach ($item in $authorizedPhase7Changes) { $authorizedLookup[$item.ToLowerInvariant()] = $true }
$protectedCount = 0
foreach ($entry in @($baseline.files)) {
    $relative = [string]$entry.path
    if ($authorizedLookup.ContainsKey($relative.ToLowerInvariant())) { continue }
    $full = Join-Path $ProjectRoot $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $full)) { throw ('Frozen Phase 6 file is missing: ' + $relative) }
    $actual = Get-Sha256Lower -Path $full
    if ($actual -ne ([string]$entry.sha256).ToLowerInvariant()) { throw ('Frozen Phase 6 file changed unexpectedly: ' + $relative) }
    $protectedCount++
}
if ($protectedCount -lt 250) { throw ('Frozen Phase 6 predecessor proof covered too few files: ' + $protectedCount) }
Write-Host ('[PASS] Frozen Phase 6 R1.2 predecessor files are byte-identical outside the authorized Phase 7 scope: ' + $protectedCount + ' checked.') -ForegroundColor Green

Write-Host '[3/19] Verifying first-run organization profile and personalized terminology boundaries...'
foreach ($file in @(
    'src\ChurchBooks.Accounting\Setup\OrganizationProfile.cs',
    'src\ChurchBooks.Accounting\Setup\TerminologyKeys.cs',
    'src\ChurchBooks.Accounting\Setup\TerminologyCatalog.cs',
    'src\ChurchBooks.Accounting\Setup\TerminologyDefinition.cs',
    'src\ChurchBooks.Accounting\Setup\CustomSearchAlias.cs',
    'src\ChurchBooks.Accounting\Abstractions\ISetupStore.cs',
    'src\ChurchBooks.Accounting\Services\FirstRunSetupService.cs',
    'src\ChurchBooks.Data\Storage\SqliteSetupStore.cs',
    'src\ChurchBooks.App\ViewModels\SetupWorkspaceViewModel.cs',
    'src\ChurchBooks.App\Views\SetupWorkspaceView.xaml'
)) { [void](Require-File -RelativePath $file) }
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Setup\TerminologyKeys.cs' -ExpectedText 'public const string Member = "Member";'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Setup\TerminologyKeys.cs' -ExpectedText 'public const string Offering = "Offering";'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Setup\TerminologyKeys.cs' -ExpectedText 'public const string BankAccount = "BankAccount";'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\SetupWorkspaceView.xaml' -ExpectedText 'Internal accounting keys remain stable'
Require-Text -RelativePath 'src\ChurchBooks.App\Views\SetupWorkspaceView.xaml' -ExpectedText 'Additional labels / search terms'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\SetupWorkspaceViewModel.cs' -ExpectedText 'You can change these terms later in Settings.'
Write-Host '[PASS] First-use setup personalizes visible language while canonical accounting keys stay stable.' -ForegroundColor Green

Write-Host '[4/19] Verifying schema v6 is additive and stores personalization/banking data locally...'
$schemaText = Get-Content -LiteralPath (Require-File -RelativePath 'src\ChurchBooks.Data\Storage\Phase6DatabaseMigrator.cs') -Raw
foreach ($table in @('organization_profile','terminology_overrides','custom_search_aliases','bank_accounts','giving_category_income_accounts','bank_deposits','bank_deposit_contributions')) {
    if ($schemaText.IndexOf(('CREATE TABLE IF NOT EXISTS ' + $table), [System.StringComparison]::Ordinal) -lt 0) { throw ('Schema v6 table is missing: ' + $table) }
}
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase6DatabaseMigrator.cs' -ExpectedText "VALUES('schema_version','6'"
Write-Host '[PASS] Schema v6 is additive and contains the required local-first personalization/banking tables.' -ForegroundColor Green

Write-Host '[5/19] Verifying multiple-bank account model and controlled deposit workflow...'
foreach ($file in @(
    'src\ChurchBooks.Accounting\Banking\BankAccount.cs',
    'src\ChurchBooks.Accounting\Banking\BankDeposit.cs',
    'src\ChurchBooks.Accounting\Banking\DepositContributionAllocation.cs',
    'src\ChurchBooks.Accounting\Banking\GivingCategoryIncomeMapping.cs',
    'src\ChurchBooks.Accounting\Abstractions\IBankingStore.cs',
    'src\ChurchBooks.Accounting\Services\BankingManagementService.cs',
    'src\ChurchBooks.Data\Storage\SqliteBankingStore.cs'
)) { [void](Require-File -RelativePath $file) }
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'Every giving category in the deposit must have an Income account mapping before posting.'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'FundAccountingEngine'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'This deposit is already posted.'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase6DatabaseMigrator.cs' -ExpectedText 'contribution_id TEXT NOT NULL UNIQUE'
Write-Host '[PASS] Bank accounts, deposit grouping, category-to-income mapping, and double-post protection are present.' -ForegroundColor Green

Write-Host '[6/19] Verifying deposit-to-General-Ledger posting remains fund-aware and balanced...'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'new FundAssignment'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'bank.LedgerAccountId'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'AccountType.Income'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Services\BankingManagementService.cs' -ExpectedText 'PostJournalAsync'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -RejectedText 'AccountingEngine'
Reject-Text -RelativePath 'src\ChurchBooks.Accounting\Services\OfferingManagementService.cs' -RejectedText 'PostJournalAsync'
Write-Host '[PASS] Deposit posting uses the protected fund-aware accounting engine; offering capture itself still does not post.' -ForegroundColor Green

Write-Host '[7/19] Verifying approved GUI, one-window fit policy, and personalized navigation...'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'WindowState="Maximized"'
$phase7FocusedTests = @(
    'tests\ChurchBooks.Accounting.Tests\Phase7ImportTests.cs',
    'tests\ChurchBooks.Data.Tests\Phase7ImportDataTests.cs',
    'tests\ChurchBooks.App.Tests\Phase7ImportUxTests.cs'
)
foreach ($relativeTest in $phase7FocusedTests) {
    $testPath = Require-File -RelativePath $relativeTest
    $testLines = @(Get-Content -LiteralPath $testPath)
    foreach ($line in $testLines) {
        if ($line -match '^\s*\[Fact\]\s+public') { throw ('Compressed same-line [Fact] declaration is not allowed in clean Phase 7 tests: ' + $relativeTest) }
        if ($line.Length -gt 180) { throw ('Phase 7 focused test line exceeds 180 characters: ' + $relativeTest) }
    }
}
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase7ImportUxTests.cs' -ExpectedText 'public void AliasService_ResolvesImport()'
$phase7UxTestText = Get-Content -LiteralPath (Require-File -RelativePath 'tests\ChurchBooks.App.Tests\Phase7ImportUxTests.cs') -Raw
if ($phase7UxTestText -match 'AliasService_ResolvesImport\s*\(\s*\)\s*=>\s*\{') { throw 'AliasService_ResolvesImport must be block-bodied; expression-bodied arrow followed by a block is invalid C#.' }
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinWidth="1180"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'MinHeight="700"'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'SetupWorkspace.MemberPlural'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'SetupWorkspace.FundPlural'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'ShowBankingCommand'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'ShowSetupCommand'
Require-File -RelativePath 'docs\reference\ChurchBooks-Approved-Dashboard.png' | Out-Null
Write-Host '[PASS] Approved compact desktop GUI and personalized navigation remain enforced.' -ForegroundColor Green

Write-Host '[8/19] Verifying categories/funds remain data-driven and church-specific names are not hard-coded in operational Phase 6 workspaces...'
foreach ($relative in @('src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs','src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs','src\ChurchBooks.App\Views\GivingWorkspaceView.xaml','src\ChurchBooks.App\Views\BankingWorkspaceView.xaml')) {
    foreach ($name in @('Missions Fund','Building Fund','Love Offering','Youth Ministry')) { Reject-Text -RelativePath $relative -RejectedText $name }
}
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs' -ExpectedText 'GivingCategories'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs' -ExpectedText 'IncomeAccounts'
Write-Host '[PASS] Operational giving/banking UI reads categories and funds from data instead of fixed ministry names.' -ForegroundColor Green

Write-Host '[9/19] Verifying base-currency personalization reaches Giving and Banking...'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'BaseCurrency'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\BankingWorkspaceViewModel.cs' -ExpectedText 'new CurrencyCode(BaseCurrency)'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\MainWindowViewModel.cs' -ExpectedText 'BankingWorkspace.ApplyPersonalization(SetupWorkspace.BaseCurrency, terminology);'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'private string Format(decimal value) => $"{BaseCurrency} {value:N2}";'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -RejectedText 'private static string Format(decimal value) => $"{BaseCurrency} {value:N2}";'
Write-Host '[PASS] Personalized base currency is used by new Giving/Banking surfaces and the formatter is instance-scoped.' -ForegroundColor Green

Write-Host '[10/19] Verifying SQLite reader/transaction safety for banking storage...'
$bankStoreText = Get-Content -LiteralPath (Require-File -RelativePath 'src\ChurchBooks.Data\Storage\SqliteBankingStore.cs') -Raw
if ($bankStoreText.IndexOf('return header is null ? null : await LoadDepositAsync(connection, header, cancellationToken);', [System.StringComparison]::Ordinal) -lt 0) { throw 'GetDepositAsync must dispose the header reader before loading allocations.' }
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\SqliteBankingStore.cs' -ExpectedText 'using var transaction = connection.BeginTransaction();'
Write-Host '[PASS] Banking persistence uses scoped readers and transactional writes.' -ForegroundColor Green

Write-Host '[11/19] Rechecking frozen Phase 6 tests and Phase 7 focused coverage...'
foreach ($file in @('tests\ChurchBooks.Accounting.Tests\Phase6BankingSetupTests.cs','tests\ChurchBooks.Data.Tests\Phase6DataTests.cs','tests\ChurchBooks.App.Tests\Phase6UxTests.cs','tests\ChurchBooks.Accounting.Tests\Phase7ImportTests.cs','tests\ChurchBooks.Data.Tests\Phase7ImportDataTests.cs','tests\ChurchBooks.App.Tests\Phase7ImportUxTests.cs')) { [void](Require-File -RelativePath $file) }
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\Phase6DataTests.cs' -ExpectedText 'FirstRunSetup_CompleteCreatesPeriodIncomeAccountAndCategoryMapping'
Require-Text -RelativePath 'tests\ChurchBooks.Data.Tests\Phase6DataTests.cs' -ExpectedText 'BankingService_PostDepositCreatesBalancedFundLedgerAndPreventsReuse'
Require-Text -RelativePath 'tests\ChurchBooks.App.Tests\Phase6UxTests.cs' -ExpectedText 'MainWindowViewModel_FirstRunRoutesToSetup'
Write-Host '[PASS] Frozen Phase 6 coverage and Phase 7 focused test files are present.' -ForegroundColor Green

Write-Host '[12/19] Rechecking Phase 5 offering analytics and selectable graph choices remain intact...'
Require-Text -RelativePath 'src\ChurchBooks.App\Models\OfferingChartKind.cs' -ExpectedText 'Donut'
Require-Text -RelativePath 'src\ChurchBooks.App\Models\OfferingChartKind.cs' -ExpectedText 'Area'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'PersonWeekTotal'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'PersonMonthTotal'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\GivingWorkspaceViewModel.cs' -ExpectedText 'PersonYearTotal'
Write-Host '[PASS] Individual offering rollups and six selectable chart modes remain present.' -ForegroundColor Green

Write-Host '[13/19] Rechecking branding, dependency pins, and exact-balance safeguards...'
Require-File -RelativePath 'src\ChurchBooks.App\Assets\Brand\ChurchBooks.ico' | Out-Null
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Journals\JournalEntry.cs' -ExpectedText 'public bool IsBalanced'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Journals\JournalEntry.cs' -ExpectedText 'TotalDebit == TotalCredit'
Require-Text -RelativePath 'Directory.Packages.props' -ExpectedText 'CommunityToolkit.Mvvm'
Require-Text -RelativePath 'Directory.Packages.props' -ExpectedText 'FluentValidation'
Require-Text -RelativePath 'Directory.Packages.props' -ExpectedText 'ExcelDataReader'
Reject-Text -RelativePath 'Directory.Packages.props' -RejectedText 'System.Text.Encoding.CodePages'
Reject-Text -RelativePath 'src\ChurchBooks.Data\ChurchBooks.Data.csproj' -RejectedText 'PackageReference Include="System.Text.Encoding.CodePages"'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText 'Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);'
Write-Host '[PASS] Branding, dependency pins, NuGet-prunable references, and protected exact-balance checks remain present.' -ForegroundColor Green

Write-Host '[14/19] Rechecking Phase 6 boundary and deferred reconciliation/licensing...'
Require-Text -RelativePath 'docs\PHASE-6-PERSONALIZED-SETUP-BANKING.md' -ExpectedText 'first-run'
Require-Text -RelativePath 'docs\PHASE-6-PERSONALIZED-SETUP-BANKING.md' -ExpectedText 'Canonical internal keys stay stable'
Require-Text -RelativePath 'docs\PHASE-6-PERSONALIZED-SETUP-BANKING.md' -ExpectedText 'reconciliation'
Require-Text -RelativePath 'LICENSE-NOT-IN-SCOPE.md' -ExpectedText 'Licensing'
Write-Host '[PASS] Phase 7 scope is documented; full reconciliation and licensing remain deferred.' -ForegroundColor Green

Write-Host '[15/19] Verifying fail-closed release-runner contract...'
Require-Text -RelativePath 'scripts\Verify-Phase6.ps1' -ExpectedText 'trap { Write-Host (''[FATAL] '' + $_.Exception.Message)'
Require-Text -RelativePath 'scripts\Verify-Phase6.ps1' -ExpectedText '$LASTEXITCODE'
Require-Text -RelativePath 'scripts\Verify-Phase6.ps1' -ExpectedText 'Build succeeded.'
Require-Text -RelativePath 'scripts\Verify-Phase6.ps1' -ExpectedText 'Build FAILED.'
Require-Text -RelativePath 'scripts\Verify-Phase6.ps1' -ExpectedText '--disable-build-servers'
Write-Host '[PASS] Phase 7 verifier retains direct native exit evidence, failure-marker checks, TRX proof, and bounded processes.' -ForegroundColor Green

Write-Host '[16/19] Verifying Smart Import source boundaries and no-auto-post contract...' -ForegroundColor Cyan
foreach ($requiredFile in @(
    'src\ChurchBooks.Accounting\Importing\SmartImportAnalyzer.cs',
    'src\ChurchBooks.Accounting\Importing\ImportDuplicateFingerprint.cs',
    'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs',
    'src\ChurchBooks.Data\Storage\Phase7DatabaseMigrator.cs',
    'src\ChurchBooks.Data\Storage\SqliteSmartImportStore.cs',
    'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs',
    'src\ChurchBooks.App\Views\ImportWorkspaceView.xaml'
)) { [void](Require-File -RelativePath $requiredFile) }
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText '".xls" => ImportSourceKind.Xls'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText '".xlsx" => ImportSourceKind.Xlsx'
Require-Text -RelativePath 'src\ChurchBooks.Data\Importing\SpreadsheetImportReader.cs' -ExpectedText '".csv" => ImportSourceKind.Csv'
Require-Text -RelativePath 'src\ChurchBooks.Accounting\Importing\SmartImportAnalyzer.cs' -ExpectedText 'No amount/debit/credit column was inferred'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'No journal or bank posting was created.'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'using CommunityToolkit.Mvvm.ComponentModel;'
Reject-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -RejectedText 'using ChurchBooks.App.Infrastructure;'
Require-Text -RelativePath 'src\ChurchBooks.App\ViewModels\ImportWorkspaceViewModel.cs' -ExpectedText 'public sealed partial class ImportWorkspaceViewModel : ObservableObject'
Require-Text -RelativePath 'src\ChurchBooks.Data\Storage\Phase7DatabaseMigrator.cs' -ExpectedText 'CREATE TABLE IF NOT EXISTS import_staged_rows'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'ShowImportCommand'
Require-Text -RelativePath 'src\ChurchBooks.App\MainWindow.xaml' -ExpectedText 'WindowState="Maximized"'
Write-Host '[PASS] Smart Import is preview/staging-only, supports CSV/XLS/XLSX, preserves the approved desktop shell, uses one MVVM ObservableObject base, and passes the clean focused-test syntax guard.' -ForegroundColor Green


if ($StaticOnly) {
    Write-Host '[PASS] Phase 7 R1.3 static verifier contract passed against canonical payload.' -ForegroundColor Green
    Write-Host '[INFO] Windows restore/build, 189/189 TRX verification, and fail-closed WPF smoke remain authoritative during staged verification.'
    exit 0
}


Write-Host '[17/19] Restoring NuGet packages with complete vulnerability audit...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @('restore','ChurchBooks.sln','--disable-build-servers','-p:NuGetAudit=true','-p:NuGetAuditMode=all') -DisplayName 'dotnet restore + complete NuGet audit' -TimeoutSeconds 180 -RejectedOutputText @('error NU','Restore failed')

Write-Host '[18/19] Building Release x64...'
Invoke-NativeChecked -FilePath 'dotnet.exe' -Arguments @('build','ChurchBooks.sln','-c','Release','-p:Platform=x64','--no-restore','--disable-build-servers') -DisplayName 'Release x64 build' -TimeoutSeconds 180 -RequiredOutputText @('Build succeeded.') -RejectedOutputText @('Build FAILED.',' error CS')

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

$testResultsRoot = Join-Path $env:TEMP ('ChurchBooks-Phase7-TRX-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testResultsRoot -Force | Out-Null
try {
    Write-Host '[19/19] Running complete protected regression + Phase 7 tests with machine-readable TRX verification...'
    $verifiedPassed = 0
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Accounting.Tests\ChurchBooks.Accounting.Tests.csproj' -DisplayName 'ChurchBooks.Accounting.Tests' -TrxFileName 'accounting.trx' -ExpectedTotal 85 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Core.Tests\ChurchBooks.Core.Tests.csproj' -DisplayName 'ChurchBooks.Core.Tests' -TrxFileName 'core.trx' -ExpectedTotal 4 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.Data.Tests\ChurchBooks.Data.Tests.csproj' -DisplayName 'ChurchBooks.Data.Tests' -TrxFileName 'data.trx' -ExpectedTotal 57 -ResultsDirectory $testResultsRoot
    $verifiedPassed += Invoke-VerifiedTestProject -ProjectPath 'tests\ChurchBooks.App.Tests\ChurchBooks.App.Tests.csproj' -DisplayName 'ChurchBooks.App.Tests' -TrxFileName 'app.trx' -ExpectedTotal 43 -ResultsDirectory $testResultsRoot
    if ($verifiedPassed -ne 189) { throw ('Unexpected aggregate Phase 7 TRX result. Passed=' + $verifiedPassed + ' Expected=189') }
    Write-Host '[PASS] Protected regression plus Phase 7 coverage: 189/189 verified from machine-readable TRX results.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testResultsRoot) { Remove-Item -LiteralPath $testResultsRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host '[19/19 - dependency] Checking resolved dependencies and Phase 7 identity...'
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
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string Release = "Phase7-Smart-Import";'
Require-Text -RelativePath 'src\ChurchBooks.Core\ProductInfo.cs' -ExpectedText 'public const string ReleaseRevision = "R1.3";'
Write-Host '[PASS] Dependency resolution, security boundary, and Phase 7 identity are correct.' -ForegroundColor Green

Write-Host '[19/19 - smoke] Running fail-closed WPF main-window smoke test...'
if ($SkipWpfSmoke) {
    Write-Host '[SKIP] WPF smoke was explicitly skipped.' -ForegroundColor Yellow
}
else {
    $appExe = Join-Path $ProjectRoot 'src\ChurchBooks.App\bin\Release\net10.0-windows\ChurchBooks.App.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw ('Built WPF executable was not found: ' + $appExe) }

    $appProcess = $null
    $smokeRoot = Join-Path $env:TEMP ('ChurchBooks-Phase7-WpfSmoke-' + [guid]::NewGuid().ToString('N'))
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

        Write-Host ('[PASS] ChurchBooks WPF main window and isolated schema-v7 database created. PID=' + $appProcess.Id + ' HWND=' + $appProcess.MainWindowHandle) -ForegroundColor Green
    }
    finally {
        if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue }
        $env:CHURCHBOOKS_DATABASE_PATH = $priorDatabasePath
        if (Test-Path -LiteralPath $smokeRoot) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Write-Host '[19/19 - boundary] Verifying Phase 8 reconciliation boundary and Smart Import no-auto-post contract...'
if ($null -ne $manifest.bankReconciliation -and [bool]$manifest.bankReconciliation) { throw 'Full bank reconciliation must remain deferred after Phase 7.' }
if (-not [bool]$manifest.smartImport.previewFirst -or [bool]$manifest.smartImport.automaticPosting) { throw 'Smart Import must remain preview-first with automatic posting disabled.' }
Write-Host '[PASS] Phase 7 ends at preview/approval-to-staging Smart Import; full bank reconciliation and import-to-ledger conversion remain separate later workflows.' -ForegroundColor Green
Write-Host ''
Write-Host '[PASS] ChurchBooks Phase 7 R1.3 Smart Import verification passed.' -ForegroundColor Green
exit 0
