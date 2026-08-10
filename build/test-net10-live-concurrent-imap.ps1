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
if ($report.concurrency -ne $ExpectedConcurrency) {
    throw "Expected concurrency $ExpectedConcurrency, got $($report.concurrency)."
}
if ($report.summary.completed -ne $ExpectedConcurrency) {
    throw "Expected $ExpectedConcurrency completed samples, got $($report.summary.completed)."
}
if (($report.summary.successes + $report.summary.errors) -ne $ExpectedConcurrency) {
    throw "Success/error accounting does not equal the requested concurrency."
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
if ($report.status -eq "PASS" -and $report.summary.errors -ne 0) {
    throw "A passing concurrent IMAP artifact cannot contain errors."
}

Write-Output "Validated $($report.implementation) concurrent IMAP artifact: $($report.summary.successes)/$ExpectedConcurrency success, $($report.summary.timeouts) timeouts, status $($report.status)."
