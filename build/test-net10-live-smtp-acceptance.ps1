param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [switch]$AllowFailedReport
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

$reports = @(Get-ChildItem -LiteralPath $InputDirectory -Filter "*-smtp-message-acceptance.json" -File)
if ($reports.Count -ne 1) {
    throw "Expected exactly one SMTP acceptance JSON report under $InputDirectory; found $($reports.Count)."
}

$report = Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
$reportBaseName = [IO.Path]::GetFileNameWithoutExtension($reports[0].Name)
Assert-LiveBenchmarkManifestBoundArtifact -Report $report -CsvPath (Join-Path $InputDirectory "$reportBaseName.csv") -MarkdownPath (Join-Path $InputDirectory "$reportBaseName.md")
if ($report.schema -ne "live-smtp-message-acceptance-v1") {
    throw "Unexpected SMTP acceptance report schema: $($report.schema)"
}
if ($report.implementation -notin @("net10", "cpp")) {
    throw "Unexpected implementation: $($report.implementation)"
}
if ($report.database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $report.database -match '(?i)hmaildb_test5700|production') {
    throw "The report database is not an isolated benchmark database: $($report.database)"
}
if ($report.dataRoot -notmatch '^C:\\hmail-perf-' -or $report.dataRoot -match '(?i)hmailserver57|production') {
    throw "The report Data root is not an isolated benchmark root: $($report.dataRoot)"
}
if ($report.implementation -eq "cpp") {
    if ($null -eq $report.isolationPreflight) {
        throw "C++ acceptance reports must include the legacy registry/config isolation preflight."
    }
    if ($null -eq $report.executableProvenance) {
        throw "C++ acceptance reports must include executable provenance."
    }
    if ($report.executableProvenance.sha256 -notmatch "^[0-9A-Fa-f]{64}$" -or [int64]$report.executableProvenance.length -le 0) {
        throw "C++ executable provenance is incomplete or invalid."
    }
    if ($report.status -eq "PASS" -and $report.isolationPreflight.passed -ne $true) {
        throw "A passing C++ acceptance report must have a passing isolation preflight."
    }
}
if ($null -eq $report.fixture -or $null -eq $report.postRunAccounting) {
    throw "SMTP acceptance reports must include fixture and post-run accounting evidence."
}
if ([string]::IsNullOrWhiteSpace($report.fixture.identity) -or $report.fixture.database -ne $report.database -or $report.fixture.dataRoot -ne $report.dataRoot) {
    throw "SMTP acceptance fixture identity or target roots are invalid."
}
if ($null -eq $report.fixture.before -or $null -eq $report.fixture.after -or $null -eq $report.fixture.before.sql -or $null -eq $report.fixture.after.sql -or $null -eq $report.fixture.before.data -or $null -eq $report.fixture.after.data) {
    throw "SMTP acceptance reports must include complete before/after SQL and Data fixture snapshots."
}
if (@($report.PSObject.Properties.Name) -notcontains "acceptedMessageStates") {
    throw "SMTP acceptance reports must include bounded accepted-message state evidence."
}
if ($report.status -eq "PASS" -and $report.postRunAccounting.valid -ne $true) {
    throw "A passing SMTP acceptance report must have valid SQL/Data post-run accounting."
}
if ($report.status -eq "PASS" -and (
        [int64]$report.postRunAccounting.messageRowDelta -ne [int64]$report.acceptedMessages -or
        [int64]$report.postRunAccounting.dataFileDelta -ne [int64]$report.acceptedMessages)) {
    throw "A passing SMTP acceptance report must have exact SQL message-row and Data-file deltas."
}
if ($report.status -eq "PASS" -and @($report.acceptedMessageStates | Where-Object observed).Count -ne [int]$report.acceptedMessages) {
    throw "A passing SMTP acceptance report must observe every accepted message in SQL queue/delivery state."
}
if ([int]$report.requestedMessages -lt 1) {
    throw "The report requested no messages."
}
if (@($report.samples).Count -ne [int]$report.requestedMessages -and @($report.readinessFailures).Count -eq 0) {
    throw "The report sample count does not match requestedMessages after successful readiness."
}
if ([int]$report.acceptedMessages -lt 0 -or [int]$report.acceptedMessages -gt [int]$report.requestedMessages) {
    throw "acceptedMessages is outside the requested range."
}
if ([int]$report.errors -ne ([int]$report.requestedMessages - [int]$report.acceptedMessages)) {
    throw "errors does not reconcile with requestedMessages and acceptedMessages."
}
if ($report.status -eq "PASS" -and (
        [int]$report.acceptedMessages -ne [int]$report.requestedMessages -or
        [int]$report.errors -ne 0 -or
        @($report.readinessFailures).Count -ne 0 -or
        @($report.shutdownFailures).Count -ne 0)) {
    throw "A PASS report must have complete acceptance and clean readiness/shutdown."
}
if ($report.status -eq "PASS") {
    $workloadStart = [DateTimeOffset]$report.workloadStartedUtc
    $workloadEnd = [DateTimeOffset]$report.workloadEndedUtc
    $exactWorkloadSeconds = ($workloadEnd - $workloadStart).TotalSeconds
    if ($exactWorkloadSeconds -le 0 -or [double]$report.workloadSeconds -le 0) {
        throw "A passing SMTP acceptance report must include a positive workload-only duration."
    }
    $expectedThroughput = [math]::Round([double]$report.acceptedMessages / $exactWorkloadSeconds, 3)
    if ([math]::Abs($expectedThroughput - [double]$report.throughput_messages_per_second) -gt 0.01) {
        throw "SMTP acceptance throughput does not reconcile with the workload-only duration."
    }
}
if (@($report.samples).Count -gt 0 -and (
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
    throw "SMTP acceptance workload artifacts must include numeric process metric snapshots."
}
if (@($report.samples).Count -gt 0 -and [int]$report.postWorkloadSettleSeconds -lt 1) {
    throw "SMTP acceptance evidence must include a positive post-workload settle interval."
}
if ($report.status -notin @("PASS", "FAIL")) {
    throw "Unexpected report status: $($report.status)"
}
if ($report.status -ne "PASS" -and -not $AllowFailedReport) {
    throw "SMTP acceptance report status is FAIL."
}

Write-Output "Validated $($reports[0].FullName): status=$($report.status), accepted=$($report.acceptedMessages)/$($report.requestedMessages), errors=$($report.errors)."
