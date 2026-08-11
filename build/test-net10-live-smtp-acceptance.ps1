param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory
)

$ErrorActionPreference = "Stop"

$reports = @(Get-ChildItem -LiteralPath $InputDirectory -Filter "*-smtp-message-acceptance.json" -File)
if ($reports.Count -ne 1) {
    throw "Expected exactly one SMTP acceptance JSON report under $InputDirectory; found $($reports.Count)."
}

$report = Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
if ($report.schema -ne "live-smtp-message-acceptance-v1") {
    throw "Unexpected SMTP acceptance report schema: $($report.schema)"
}
if ($report.implementation -notin @("net10", "cpp")) {
    throw "Unexpected implementation: $($report.implementation)"
}
if ($report.implementation -eq "cpp") {
    if ($null -eq $report.isolationPreflight) {
        throw "C++ acceptance reports must include the legacy registry/config isolation preflight."
    }
    if ($report.status -eq "PASS" -and $report.isolationPreflight.passed -ne $true) {
        throw "A passing C++ acceptance report must have a passing isolation preflight."
    }
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
if ($report.status -notin @("PASS", "FAIL")) {
    throw "Unexpected report status: $($report.status)"
}

Write-Output "Validated $($reports[0].FullName): status=$($report.status), accepted=$($report.acceptedMessages)/$($report.requestedMessages), errors=$($report.errors)."
