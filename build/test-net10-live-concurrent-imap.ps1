param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [ValidateRange(1, 5000)]
    [int]$ExpectedConcurrency = 1000
)

$ErrorActionPreference = "Stop"
$jsonPath = Join-Path $InputDirectory "live-concurrent-imap.json"
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
    throw "Concurrent IMAP JSON report is missing: $jsonPath"
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
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
if ($report.readinessFailures.Count -eq 0) {
    if ($report.summary.completed -ne $ExpectedConcurrency) {
        throw "Expected $ExpectedConcurrency completed samples, got $($report.summary.completed)."
    }
    if (($report.summary.successes + $report.summary.errors) -ne $ExpectedConcurrency) {
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
if ($report.database -notmatch '^hmail_perf_(cpp|net)_sql_') {
    throw "The report database is not an isolated benchmark database: $($report.database)"
}
if ($report.dataRoot -notmatch '^C:\\hmail-perf-(cpp|net10)-') {
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
if ($report.status -notin @("PASS", "FAIL")) {
    throw "Unexpected concurrent IMAP report status: $($report.status)"
}

Write-Output "Validated $($report.implementation) concurrent IMAP artifact: $($report.summary.successes)/$ExpectedConcurrency success, $($report.summary.timeouts) timeouts, status $($report.status)."
