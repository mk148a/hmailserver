param(
    [string]$BenchmarkScript = (Join-Path $PSScriptRoot "benchmark-paired-tcp451-retry-throughput.ps1"),
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-Contains {
    param([string]$Text, [string]$Needle)
    Assert-True ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) "Benchmark static contract is missing '$Needle'."
}

$scriptPath = [IO.Path]::GetFullPath($BenchmarkScript)
Assert-True (Test-Path -LiteralPath $scriptPath -PathType Leaf) "Benchmark script is missing: $scriptPath"
$scriptText = Get-Content -LiteralPath $scriptPath -Raw
$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors) | Out-Null
Assert-True ($errors.Count -eq 0) "Benchmark script has PowerShell parse errors: $((@($errors | ForEach-Object Message)) -join '; ')"
Assert-True ($scriptText -match '\[ValidateRange\(25,\s*25\)\]') "Benchmark must be fixed to exactly 25 messages."

foreach ($required in @(
        "live-benchmark-provenance.ps1",
        "Read-LiveBenchmarkFixtureManifest",
        "test-disposable-cpp-tcp451-retry.ps1",
        "DisposableDeliveryQueueRealTcp451Then250CompletesMessage",
        "HMAILSERVER_NET10_LIVE_SQL_DELIVERY_RECOVERY_REPORT",
        "MessageCount",
        "451",
        "250",
        "retained retry state",
        "p50_ms",
        "p95_ms",
        "p99_ms",
        "throughput_messages_per_second",
        "resource",
        "cleanup",
        "Start-Process",
        "ConvertTo-Json",
        "Export-Csv",
        "NO_WINNER",
        "productionSafety")) {
    Assert-Contains $scriptText $required
}
foreach ($forbidden in @(
        "hmaildb_test5700",
        "HMAILSERVER_NET10_LIVE_SQL_TCP451_RECOVERY_REPORT",
        "Program Files",
        "RegisterTypeLib",
        "Register-Com",
        "git commit",
        "git push",
        "Invoke-Sql",
        "Remove-Item -Recurse -Force")) {
    Assert-True ($scriptText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -lt 0) "Static isolation contract forbids '$forbidden'."
}

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportPath = [IO.Path]::GetFullPath($ReportPath)
    Assert-True (Test-Path -LiteralPath $reportPath -PathType Leaf) "Throughput report is missing: $reportPath"
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    Assert-True ($report.schema -eq "paired-cpp-net10-tcp451-retry-throughput-v1") "Unexpected throughput report schema."
    Assert-True ($report.status -eq "PASS") "Throughput report status is not PASS."
    Assert-True ($report.messageCount -eq 25) "Throughput report must contain exactly 25 messages."
    Assert-True ($report.decision -match "NO_WINNER") "Throughput report must retain the no-winner claim."
    Assert-True ($report.fixture.manifestSha256 -match "^[0-9A-Fa-f]{64}$") "Manifest hash is missing."
    foreach ($implementation in @("cpp", "net10")) {
        $result = $report.results.$implementation
        Assert-True ($result.requestedMessages -eq 25) "$implementation requested message count is invalid."
        Assert-True ($result.completedMessages -eq 25) "$implementation completed message count is invalid."
        Assert-True ($result.errors -eq 0) "$implementation has reported errors."
        Assert-True ($report.results.$implementation.cleanup.allSamplesClean -eq $true) "$implementation cleanup is not proven."
        Assert-True (@($result.samples).Count -eq 25) "$implementation sample count is invalid."
        Assert-True (@($result.samples | Where-Object { $_.status -ne "PASS" -or $_.cleanup -ne $true }).Count -eq 0) "$implementation has a failed sample or cleanup."
        Assert-True ($result.p50_ms -le $result.p95_ms -and $result.p95_ms -le $result.p99_ms) "$implementation percentile ordering is invalid."
        Assert-True ($result.total_ms -gt 0 -and $result.throughput_messages_per_second -gt 0) "$implementation aggregate timing is invalid."
        $expectedThroughput = 25 / ($result.total_ms / 1000)
        Assert-True ([math]::Abs($expectedThroughput - $result.throughput_messages_per_second) -lt 0.001) "$implementation throughput does not match aggregate duration."
        foreach ($sample in @($result.samples)) {
            Assert-True (Test-Path -LiteralPath $sample.evidencePath -PathType Leaf) "$implementation evidence file is missing: $($sample.evidencePath)"
        }
    }
    Assert-True ($report.sink.firstReply -eq 451 -and $report.sink.recoveryReply -eq 250) "SMTP sink replies are invalid."
    Assert-True (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($reportPath, ".csv")) -PathType Leaf) "CSV sidecar is missing."
    Assert-True (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($reportPath, ".md")) -PathType Leaf) "Markdown sidecar is missing."
    Write-Output "PASS: paired TCP 451 retry/defer throughput report validated: $reportPath"
}
else {
    Write-Output "PASS: paired TCP 451 retry/defer throughput static contract validated."
}
