param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [switch]$AllowFailedReport
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

$reports = @(Get-ChildItem -LiteralPath $InputDirectory -Filter "net10-live-protocol.json" -File)
if ($reports.Count -ne 1) {
    throw "Expected exactly one live protocol JSON report under $InputDirectory; found $($reports.Count)."
}

$report = Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
Assert-LiveBenchmarkManifestBoundArtifact -Report $report -CsvPath (Join-Path $InputDirectory "net10-live-protocol.csv") -MarkdownPath (Join-Path $InputDirectory "net10-live-protocol.md")
Assert-LiveBenchmarkRunStartArtifact -Report $report -CsvPath (Join-Path $InputDirectory "net10-live-protocol.csv") -MarkdownPath (Join-Path $InputDirectory "net10-live-protocol.md")
if ($report.schema -ne "live-protocol-v1") {
    throw "Unexpected live protocol report schema: $($report.schema)"
}
if ($report.implementation -notin @("net10", "cpp")) {
    throw "Unexpected implementation: $($report.implementation)"
}
if ($report.bind -ne "127.0.0.1") {
    throw "Live protocol reports must bind to loopback; found '$($report.bind)'."
}
if ($report.ports -notmatch "SMTP 2525, IMAP 1143, POP3 25110") {
    throw "Unexpected protocol ports: $($report.ports)"
}

if ($report.database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $report.database -match '(?i)hmaildb_test5700|production') {
    throw "Unexpected disposable benchmark database: $($report.database)"
}
if ($report.dataRoot -notmatch '^C:\\hmail-perf-' -or $report.dataRoot -match '(?i)hmailserver57|production') {
    throw "Unexpected disposable benchmark data root: $($report.dataRoot)"
}

$readinessFailures = @($report.readinessFailures)
$shutdownFailures = @($report.shutdownFailures)
$samples = @($report.samples)
$scenarios = @("smtp", "imap", "pop3")
$summary = @($report.summary)
if ($summary.Count -ne $scenarios.Count -or @($summary | Where-Object { $_.scenario -notin $scenarios }).Count -ne 0) {
    throw "Protocol summary must contain exactly smtp, imap, and pop3."
}
if ($readinessFailures.Count -gt 0 -and $samples.Count -ne 0) {
    throw "A report with readiness failures must not contain benchmark samples."
}
foreach ($scenario in $scenarios) {
    $summaryRow = @($summary | Where-Object scenario -eq $scenario)
    if ($summaryRow.Count -ne 1) {
        throw "Missing or duplicate summary row for $scenario."
    }
    $sampleRows = @($samples | Where-Object scenario -eq $scenario)
    if ([int]$summaryRow[0].iterations -ne $sampleRows.Count) {
        throw "Summary/sample iteration count does not reconcile for $scenario."
    }
    $successes = @($sampleRows | Where-Object ok).Count
    $errors = $sampleRows.Count - $successes
    if ([int]$summaryRow[0].successes -ne $successes -or [int]$summaryRow[0].errors -ne $errors) {
        throw "Summary success/error counts do not reconcile for $scenario."
    }
}

if ($report.implementation -eq "cpp") {
    if ($null -eq $report.isolationPreflight) {
        throw "C++ protocol reports must include the legacy registry/config isolation preflight."
    }
    if ($null -eq $report.executableProvenance) {
        throw "C++ protocol reports must include executable provenance."
    }
    if ($report.executableProvenance.path -notmatch "(?i)\\hMailServer\.exe$") {
        throw "C++ executable provenance path is not hMailServer.exe: $($report.executableProvenance.path)"
    }
    if ($report.executableProvenance.sha256 -notmatch "^[0-9A-Fa-f]{64}$" -or [int64]$report.executableProvenance.length -le 0) {
        throw "C++ executable provenance is incomplete or invalid."
    }
    if ($report.status -eq "PASS" -and $report.isolationPreflight.passed -ne $true) {
        throw "A passing C++ protocol report must have a passing isolation preflight."
    }
}

if ($report.status -notin @("PASS", "FAIL")) {
    throw "Unexpected report status: $($report.status)"
}
if ($report.status -ne "PASS" -and -not $AllowFailedReport) {
    throw "Live protocol acceptance report status is FAIL."
}
if ($report.status -eq "PASS" -and ($readinessFailures.Count -ne 0 -or $shutdownFailures.Count -ne 0 -or ($summary | Where-Object errors -gt 0).Count -ne 0)) {
    throw "A PASS protocol report must have clean readiness, samples, and shutdown."
}

Write-Output "Validated $($reports[0].FullName): status=$($report.status), samples=$($samples.Count), readinessFailures=$($readinessFailures.Count)."
