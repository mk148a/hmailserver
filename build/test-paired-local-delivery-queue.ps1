param(
    [string]$BenchmarkScript = (Join-Path $PSScriptRoot 'benchmark-paired-local-delivery-queue.ps1'),
    [string]$ReportPath = "",
    [ValidateRange(1, 5000)]
    [int]$ExpectedMessageCount = 100
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Name)
    Assert-True ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) "$Name is missing '$Needle'."
}

function Assert-NotContains {
    param([string]$Text, [string]$Needle, [string]$Name)
    Assert-True ($Text.IndexOf($Needle, [StringComparison]::OrdinalIgnoreCase) -lt 0) "$Name must not contain '$Needle'."
}

function Assert-Report {
    param([string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-True (Test-Path -LiteralPath $fullPath -PathType Leaf) "Paired local-delivery report is missing: $fullPath"
    $report = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    Assert-True ($report.schema -eq 'paired-local-delivery-queue-v1') "Unexpected report schema: $($report.schema)"
    Assert-True ($report.status -in @('PASS', 'FAIL', 'BLOCKED')) "Invalid report status: $($report.status)"
    Assert-True ($report.decision -notmatch '(?i)winner|faster|speed-up') "Reports must not claim a winner or speed-up."
    Assert-True ($report.fixture.manifestSha256 -match '^[0-9A-Fa-f]{64}$') 'Fixture manifest hash is missing.'
    Assert-True ($report.fixture.dataParity.exact -eq $true) 'Fixture Data parity is not exact.'
    Assert-True ($report.fixture.messageParity.exact -eq $true) 'Fixture message parity is not exact.'
    Assert-True ($report.seed.messageCount -eq $ExpectedMessageCount) "Seed message count is not $ExpectedMessageCount."
    Assert-True ($report.seed.cpp.fileSha256 -eq $report.seed.net10.fileSha256) 'Seed file hashes are not byte-matched.'
    Assert-True ($report.seed.cpp.bytes -eq $report.seed.net10.bytes) 'Seed file byte counts are not matched.'

    foreach ($implementation in @('cpp', 'net10')) {
        $result = $report.results.$implementation
        Assert-True ($null -ne $result) "Missing $implementation result."
        Assert-True ($result.status -in @('PASS', 'FAIL', 'BLOCKED')) "Invalid $implementation result status."
        Assert-True ($result.database -match '^hmail_perf_pair_(cpp|net10)_[a-z0-9_]+$') "$implementation database is not disposable."
        Assert-True ([IO.Path]::GetFullPath($result.dataRoot) -match '(?i)^C:\\hmail-perf-pair-[a-z0-9_-]+\\(cpp|net10)\\Data$') "$implementation Data root is not paired-disposable."
        Assert-True ($result.executableSha256 -match '^[0-9A-Fa-f]{64}$') "$implementation executable hash is missing."
        Assert-True ($result.metrics.sampleCount -eq $ExpectedMessageCount -or $result.metrics.sampleCount -eq 0) "$implementation sample count is invalid."
        Assert-True ($result.cleanup.messageRowsAbsent -eq $true) "$implementation queue message cleanup is not proven."
        Assert-True ($result.cleanup.recipientRowsAbsent -eq $true) "$implementation recipient cleanup is not proven."
        Assert-True ($result.cleanup.inboxRowsAbsent -eq $true) "$implementation Inbox cleanup is not proven."
        Assert-True ($result.cleanup.dataFilesAbsent -eq $true) "$implementation Data cleanup is not proven."
        Assert-True ($result.cleanup.serviceAbsent -eq $true) "$implementation service cleanup is not proven."
    }

    $csvPath = [IO.Path]::ChangeExtension($fullPath, '.csv')
    $markdownPath = [IO.Path]::ChangeExtension($fullPath, '.md')
    Assert-True (Test-Path -LiteralPath $csvPath -PathType Leaf) "CSV sidecar is missing: $csvPath"
    Assert-True (Test-Path -LiteralPath $markdownPath -PathType Leaf) "Markdown sidecar is missing: $markdownPath"
    $csvRows = @(Import-Csv -LiteralPath $csvPath)
    Assert-True ($csvRows.Count -eq 2) 'CSV must contain one row for each implementation.'
    Assert-True (@($csvRows | Where-Object { $_.implementation -notin @('cpp', 'net10') }).Count -eq 0) 'CSV implementation set is invalid.'
    $markdown = Get-Content -LiteralPath $markdownPath -Raw
    foreach ($required in @('Paired local-delivery queue drain', 'Fixture manifest SHA-256', 'No winner claim')) {
        Assert-Contains $markdown $required 'Markdown report'
    }
    if ($report.status -ne 'PASS') {
        Assert-True ($markdown -match '(?i)incomplete|blocked|no winner') 'Incomplete report must explain why no comparison is valid.'
    }
}

$scriptPath = [IO.Path]::GetFullPath($BenchmarkScript)
Assert-True (Test-Path -LiteralPath $scriptPath -PathType Leaf) "Benchmark script is missing: $scriptPath"
$scriptText = Get-Content -LiteralPath $scriptPath -Raw
$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors) | Out-Null
Assert-True ($errors.Count -eq 0) "Benchmark script has PowerShell parse errors: $((@($errors | ForEach-Object Message)) -join '; ')"

foreach ($required in @(
        'live-benchmark-provenance.ps1',
        'live-cpp-isolation-preflight.ps1',
        'Read-LiveBenchmarkFixtureManifest',
        'Assert-LiveBenchmarkRunStartAttestation',
        'Get-CppIsolationPreflight',
        'Wait-ForServiceReadiness',
        'messagetype',
        'hm_messagerecipients',
        'p50_ms',
        'p95_ms',
        'p99_ms',
        'throughput_messages_per_second',
        'ConvertTo-Json',
        'Export-Csv',
        'No winner claim')) {
    Assert-Contains $scriptText $required 'Benchmark static contract'
}
foreach ($forbidden in @(
        'hmaildb_test5700',
        'Program Files',
        'RegisterTypeLib',
        'Register-Com',
        'git commit',
        'git reset',
        'Remove-Item -Recurse -Force $repoRoot')) {
    Assert-NotContains $scriptText $forbidden 'Benchmark static isolation contract'
}
Assert-NotContains $scriptText 'Winner = $true' 'Benchmark winner contract'

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    Assert-Report $ReportPath
    Write-Output "PASS: paired local-delivery queue report validated: $([IO.Path]::GetFullPath($ReportPath))"
}
else {
    Write-Output 'PASS: paired local-delivery queue benchmark static contract validated.'
}
