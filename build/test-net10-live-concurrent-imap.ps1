param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [ValidateRange(1, 5000)]
    [int]$ExpectedConcurrency = 1000,
    [ValidateRange(1, 100)]
    [int]$ExpectedWaves = 1,
    [switch]$AllowFailedReport
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")
$jsonPath = Join-Path $InputDirectory "live-concurrent-imap.json"
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
    throw "Concurrent IMAP JSON report is missing: $jsonPath"
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
Assert-LiveBenchmarkManifestBoundArtifact -Report $report -CsvPath (Join-Path $InputDirectory "live-concurrent-imap.csv") -MarkdownPath (Join-Path $InputDirectory "live-concurrent-imap.md")
Assert-LiveBenchmarkRunStartArtifact -Report $report -CsvPath (Join-Path $InputDirectory "live-concurrent-imap.csv") -MarkdownPath (Join-Path $InputDirectory "live-concurrent-imap.md")
if ($report.schema -ne "live-concurrent-imap-v1") {
    throw "Unexpected concurrent IMAP report schema: $($report.schema)"
}
if ($report.implementation -notin @("net10", "cpp")) {
    throw "Unexpected implementation: $($report.implementation)"
}
if ($report.implementation -eq "cpp") {
    if ($null -eq $report.isolationPreflight) {
        throw "C++ concurrent IMAP reports must include the legacy registry/config isolation preflight."
    }
    if ($null -eq $report.executableProvenance) {
        throw "C++ concurrent IMAP reports must include executable provenance."
    }
    if ($report.executableProvenance.path -notmatch "(?i)\\hMailServer\.exe$") {
        throw "C++ executable provenance path is not hMailServer.exe: $($report.executableProvenance.path)"
    }
    if ($report.executableProvenance.sha256 -notmatch "^[0-9A-Fa-f]{64}$" -or [int64]$report.executableProvenance.length -le 0) {
        throw "C++ executable provenance is incomplete or invalid."
    }
    if ($report.status -eq "PASS" -and $report.isolationPreflight.passed -ne $true) {
        throw "A passing C++ concurrent IMAP report must have a passing isolation preflight."
    }
}
if ($report.concurrency -ne $ExpectedConcurrency) {
    throw "Expected concurrency $ExpectedConcurrency, got $($report.concurrency)."
}
$waves = if (@($report.PSObject.Properties.Name) -contains "waves") { [int]$report.waves } else { 1 }
if ($waves -ne $ExpectedWaves) {
    throw "Expected $ExpectedWaves wave(s), got $waves."
}
$expectedSessions = $ExpectedConcurrency * $ExpectedWaves
if ($report.readinessFailures.Count -eq 0) {
    if ($report.summary.completed -ne $expectedSessions) {
        throw "Expected $expectedSessions completed samples, got $($report.summary.completed)."
    }
    if (($report.summary.successes + $report.summary.errors) -ne $expectedSessions) {
        throw "Success/error accounting does not equal the requested concurrency."
    }
}
elseif ($report.summary.completed -ne 0 -or $report.summary.successes -ne 0 -or $report.summary.errors -ne 0) {
    throw "A readiness failure must prevent workload samples from starting."
}
if ($report.messageCount -ne 1000) {
    throw "Expected the paired 1,000-message corpus, got $($report.messageCount)."
}
if ($report.bind -ne "127.0.0.1" -or $report.port -ne 1143) {
    throw "Concurrent IMAP must run on 127.0.0.1:1143."
}
if ($report.database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $report.database -match '(?i)hmaildb_test5700|production') {
    throw "The report database is not an isolated benchmark database: $($report.database)"
}
if ($report.dataRoot -notmatch '^C:\\hmail-perf-' -or $report.dataRoot -match '(?i)hmailserver57|production') {
    throw "The report Data root is not an isolated benchmark root: $($report.dataRoot)"
}
if ($report.ratioValid -ne $false) {
    throw "Concurrent IMAP artifact must not claim a ratio."
}
if (@($report.PSObject.Properties.Name) -notcontains "readinessFailures" -or @($report.PSObject.Properties.Name) -notcontains "shutdownFailures") {
    throw "Concurrent IMAP artifact must record readiness and shutdown failures explicitly."
}
if ($report.status -eq "PASS" -and ($report.readinessFailures.Count -ne 0 -or $report.shutdownFailures.Count -ne 0)) {
    throw "A passing concurrent IMAP artifact cannot contain readiness or shutdown failures."
}
if ($report.status -eq "PASS" -and $report.summary.errors -ne 0) {
    throw "A passing concurrent IMAP artifact cannot contain errors."
}
if ($report.status -eq "PASS") {
    $workloadStart = [DateTimeOffset]$report.workloadStartedUtc
    $workloadEnd = [DateTimeOffset]$report.workloadEndedUtc
    if ($workloadEnd -le $workloadStart -or [double]$report.summary.workload_seconds -le 0) {
        throw "A passing concurrent IMAP artifact must include a positive workload-only duration."
    }
    $exactWorkloadSeconds = if ($ExpectedWaves -eq 1 -or $null -eq $report.waveMetrics) {
        ($workloadEnd - $workloadStart).TotalSeconds
    } else {
        $waveRows = @($report.waveMetrics)
        if ($waveRows.Count -ne $ExpectedWaves) {
            throw "Expected $ExpectedWaves wave metric rows, got $($waveRows.Count)."
        }
        ($waveRows | Measure-Object workloadSeconds -Sum).Sum
    }
    $expectedThroughput = [math]::Round([double]$report.summary.successes / $exactWorkloadSeconds, 3)
    if ([math]::Abs($expectedThroughput - [double]$report.summary.throughput_sessions_per_second) -gt 0.01) {
        throw "Concurrent IMAP throughput does not reconcile with the workload-only duration."
    }
}
if ([int]$report.summary.completed -gt 0 -and (
        $null -eq $report.processBefore -or
        $null -eq $report.processAfterImmediate -or
        $null -eq $report.processAfter -or
        [long]$report.processBefore.privateBytes -le 0 -or
        [long]$report.processAfterImmediate.privateBytes -le 0 -or
        [long]$report.processAfter.privateBytes -le 0 -or
        [int]$report.processBefore.handles -le 0 -or
        [int]$report.processAfterImmediate.handles -le 0 -or
        [int]$report.processAfter.handles -le 0 -or
        [int]$report.processBefore.threads -le 0 -or
        [int]$report.processAfterImmediate.threads -le 0 -or
        [int]$report.processAfter.threads -le 0)) {
    throw "Concurrent IMAP workload artifacts must include numeric process metric snapshots."
}
if ([int]$report.summary.completed -gt 0 -and [int]$report.postWorkloadSettleSeconds -lt 1) {
    throw "Concurrent IMAP evidence must include a positive post-workload settle interval."
}
if ($report.status -notin @("PASS", "FAIL")) {
    throw "Unexpected concurrent IMAP report status: $($report.status)"
}
if ($report.status -ne "PASS" -and -not $AllowFailedReport) {
    throw "Concurrent IMAP acceptance report status is FAIL."
}

Write-Output "Validated $($report.implementation) concurrent IMAP artifact: $($report.summary.successes)/$expectedSessions success across $ExpectedWaves wave(s), $($report.summary.timeouts) timeouts, status $($report.status)."
