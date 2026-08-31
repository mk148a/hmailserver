param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory
)

$ErrorActionPreference = "Stop"
$summaryPath = Join-Path $InputDirectory "profile-summary.json"
$markdownPath = Join-Path $InputDirectory "PROFILE_DIAGNOSTIC.md"
if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) { throw "Profile summary JSON is missing." }
if (-not (Test-Path -LiteralPath $markdownPath -PathType Leaf)) { throw "Profile diagnostic Markdown is missing." }

$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
if ($summary.schema -ne "paired-imap-profile-diagnostic-v1") { throw "Unexpected profile diagnostic schema." }
if ($summary.status -ne "RED") { throw "The current diagnostic must remain RED until all profiles pass." }
if ($summary.concurrency -ne 1000 -or $summary.waves -ne 1) { throw "Unexpected diagnostic concurrency or wave count." }
if (@($summary.rows).Count -ne 6) { throw "Expected six implementation/profile rows." }
if ($summary.claims.listenerAdmissionIsolated -ne $true) { throw "Admission isolation was not proven." }
if ($summary.claims.fullSearchSortPassedForBoth -ne $false) { throw "Full SEARCH/SORT must not be reported as passing." }
if ($summary.claims.speedRatioPermitted -ne $false) { throw "A ratio must not be permitted while a profile fails." }

foreach ($row in @($summary.rows)) {
    if ($row.implementation -notin @("cpp", "net10") -or $row.profile -notin @("Admission", "AuthSelect", "Full")) {
        throw "Unexpected row identity."
    }
    if ([int]$row.successes + [int]$row.errors -ne 1000) { throw "Row accounting does not reconcile." }
    if ($row.profile -eq "Admission") {
        if ($row.ratio_valid -ne $true -or $null -eq $row.p95_ratio_cpp_over_net10) { throw "The passing admission ratio is missing." }
    } elseif ($row.ratio_valid -ne $false -or $null -ne $row.p95_ratio_cpp_over_net10) {
        throw "Invalid ratio was published."
    }
}

$text = Get-Content -LiteralPath $markdownPath -Raw
if ($text -match "(?i)C:\\|hmail_perf_|hmail-perf-") { throw "Diagnostic Markdown contains local or database paths." }
foreach ($name in @("profile-success-count.png", "profile-p95-latency.png", "profile-throughput.png")) {
    $path = Join-Path $InputDirectory $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) {
        throw "Diagnostic chart is missing or empty: $name"
    }
}

Write-Output "Validated paired IMAP profile diagnostic: admission isolated, full gate RED, no invalid ratio published."
