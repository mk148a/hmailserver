param([Parameter(Mandatory = $true)][string]$InputDirectory)
$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($InputDirectory)
$required = @("PERFORMANCE_DIAGNOSTIC.md", "diagnostic-summary.json", "diagnostic-summary.csv", "protocol-p95.png", "concurrent-imap.png", "smtp-acceptance.png")
foreach ($name in $required) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) { throw "Missing diagnostic artifact: $name" }
}
$summary = Get-Content (Join-Path $root "diagnostic-summary.json") -Raw | ConvertFrom-Json
if ($summary.schema -ne "paired-cpp-net10-performance-diagnostic-v1" -or $summary.gate -ne "RED") { throw "Diagnostic summary schema/gate invalid." }
$concurrent = @($summary.concurrentImap)
if ($concurrent.Count -ne 3) { throw "Expected three IMAP concurrency levels." }
foreach ($row in $concurrent) {
    if ([int]$row.concurrency -gt 100 -and $null -ne $row.cppOverNet10P95Ratio) { throw "Failed high-load scenario must not publish a ratio." }
}
$markdown = Get-Content (Join-Path $root "PERFORMANCE_DIAGNOSTIC.md") -Raw
foreach ($text in @("Decision: RED", "100,000-message", "24-hour soak", "standalone", "/Debug")) {
    if ($markdown -notlike "*$text*") { throw "Diagnostic report is missing: $text" }
}
if ($markdown -match "(?i)C:\\|E:\\|Users\\|hmail_perf_") { throw "Diagnostic report contains a local path or database name." }
Write-Output "Validated diagnostic C++/.NET 10 report with explicit failed-load limitations."
