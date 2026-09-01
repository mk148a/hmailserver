param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [ValidateSet("net10", "cpp")]
    [string]$ExpectedImplementation = "net10",
    [ValidateRange(1, 25)]
    [int]$ExpectedIterations = 5,
    [ValidateRange(1, 100000)]
    [int]$ExpectedMessages = 1000
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")
$jsonPath = Join-Path $InputDirectory "net10-live-pop3-large-mailbox.json"
$csvPath = Join-Path $InputDirectory "net10-live-pop3-large-mailbox.csv"
$markdownPath = Join-Path $InputDirectory "net10-live-pop3-large-mailbox.md"
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
    throw "POP3 large-mailbox JSON report is missing: $jsonPath"
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
if ($report.schema -ne "live-pop3-large-mailbox-v2") {
    throw "Unexpected POP3 large-mailbox report schema: $($report.schema)"
}
if ($report.implementation -ne $ExpectedImplementation) {
    throw "Expected $ExpectedImplementation report, got $($report.implementation)."
}
if ($report.endpoint -ne "127.0.0.1:25110") {
    throw "POP3 large-mailbox endpoint is not loopback-only: $($report.endpoint)"
}
if ($report.database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $report.database -match '(?i)hmaildb_test5700|production') {
    throw "The POP3 report database is not disposable: $($report.database)"
}
if ($report.dataRoot -notmatch '^C:\\hmail-perf-' -or $report.dataRoot -match '(?i)hmailserver57|production') {
    throw "The POP3 report Data root is not disposable: $($report.dataRoot)"
}
if ([int]$report.expectedMessages -ne $ExpectedMessages -or
    [int]$report.mailboxRowsAfterRun -ne $ExpectedMessages -or
    [int]$report.iterations -ne $ExpectedIterations) {
    throw "POP3 mailbox/iteration accounting is invalid."
}
$samples = @($report.samples)
if ($samples.Count -ne $ExpectedIterations) {
    throw "Expected $ExpectedIterations POP3 samples, got $($samples.Count)."
}
if (@($samples | Where-Object { $_.ok -ne $true }).Count -ne 0 -or
    [int]$report.successes -ne $ExpectedIterations -or
    [int]$report.errors -ne 0 -or
    $report.status -ne "PASS") {
    throw "POP3 large-mailbox acceptance did not pass all iterations."
}
foreach ($sample in $samples) {
    if ([int]$sample.stat_count -ne $ExpectedMessages -or
        [int]$sample.list_count -ne $ExpectedMessages -or
        [int]$sample.uidl_count -ne $ExpectedMessages -or
        [int]$sample.retr_lines -le 0) {
        throw "POP3 sample $($sample.iteration) has invalid STAT/LIST/UIDL/RETR accounting."
    }
}
if (@($report.readinessFailures).Count -ne 0 -or @($report.shutdownFailures).Count -ne 0) {
    throw "POP3 report contains readiness or shutdown failures."
}
if ($report.provenanceStatus -eq "MANIFEST_BOUND") {
    Assert-LiveBenchmarkManifestBoundArtifact -Report $report -CsvPath $csvPath -MarkdownPath $markdownPath
    Assert-LiveBenchmarkRunStartArtifact -Report $report -CsvPath $csvPath -MarkdownPath $markdownPath
}
Write-Output "Validated $ExpectedImplementation POP3 large-mailbox artifact: $($report.successes)/$ExpectedIterations; mailbox $($report.mailboxRowsAfterRun)/$ExpectedMessages; status $($report.status)."
