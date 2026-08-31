param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory
)

$ErrorActionPreference = 'Stop'
$summaryPath = Join-Path $InputDirectory 'threshold-summary.json'
$markdownPath = Join-Path $InputDirectory 'IMAP_QUERY_THRESHOLD_DIAGNOSTIC.md'
foreach ($path in @($summaryPath, $markdownPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Threshold artifact is missing: $path" }
}
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
if ($summary.schema -ne 'paired-imap-threshold-v1' -or $summary.status -ne 'RED') { throw 'Unexpected threshold schema or status.' }
if (@($summary.rows).Count -ne 12) { throw 'Expected twelve threshold rows.' }
if ($summary.claims.net10AllIndexedProfilesPassed -ne $true -or $summary.claims.cppLowAndMediumLevelsPassed -ne $true) { throw 'Expected indexed Net10 and low/medium C++ claims.' }
if ($summary.claims.speedRatioPermitted -ne $true) { throw 'Expected at least one valid paired ratio.' }
$failedRows = @($summary.rows | Where-Object status -eq 'FAIL')
if ($failedRows.Count -eq 0) { throw 'Expected failed C++ high-load rows to remain visible.' }
foreach ($row in @($summary.rows)) {
    if ($row.implementation -notin @('cpp', 'net10') -or $row.profile -notin @('Search', 'Full') -or [int]$row.concurrency -notin @(100, 500, 1000)) { throw 'Unexpected threshold row identity.' }
    if ([int]$row.successes + [int]$row.errors -ne [int]$row.concurrency) { throw 'Threshold accounting does not reconcile.' }
    if ($row.ratio_valid -eq $true -and $null -eq $row.p95_ratio_cpp_over_net10) { throw 'A valid threshold ratio is missing.' }
    if ($row.status -eq 'FAIL' -and $row.ratio_valid -eq $true) { throw 'A failed threshold row cannot publish a valid ratio.' }
}
$text = Get-Content -LiteralPath $markdownPath -Raw
if ($text -match '(?i)C:\\|hmail_perf_|hmail-perf-|password|secret|token') { throw 'Threshold Markdown contains local or sensitive identifiers.' }
foreach ($name in @('threshold-success-count.png', 'threshold-p95-latency.png', 'threshold-throughput.png')) {
    $path = Join-Path $InputDirectory $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) { throw "Threshold chart is missing or empty: $name" }
}
Write-Output 'Validated paired IMAP threshold diagnostic: RED gate with visible failures and valid ratios only for paired PASS rows.'
