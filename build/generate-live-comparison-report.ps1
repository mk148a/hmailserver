param(
    [string]$InputDirectory = "",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
if ([string]::IsNullOrWhiteSpace($InputDirectory)) {
    $InputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = $InputDirectory
}

$net10 = Get-Content (Join-Path $InputDirectory "net10-live-protocol.json") -Raw | ConvertFrom-Json
$cpp = Get-Content (Join-Path $InputDirectory "cpp-live-protocol\net10-live-protocol.json") -Raw | ConvertFrom-Json
$corpus = Get-Content (Join-Path $InputDirectory "corpus-equality.json") -Raw | ConvertFrom-Json

$rows = foreach ($scenario in @("smtp", "imap", "pop3")) {
    $netRow = $net10.summary | Where-Object scenario -eq $scenario
    $cppRow = $cpp.summary | Where-Object scenario -eq $scenario
    [pscustomobject]@{
        scenario = $scenario
        net10_status = if ($netRow.errors -eq 0) { "PASS" } else { "FAIL" }
        net10_success = $netRow.successes
        net10_errors = $netRow.errors
        net10_p50_ms = $netRow.p50_ms
        net10_p95_ms = $netRow.p95_ms
        net10_p99_ms = $netRow.p99_ms
        cpp_status = if ($cppRow.errors -eq 0) { "PASS" } else { "FAIL" }
        cpp_success = $cppRow.successes
        cpp_errors = $cppRow.errors
        cpp_p50_ms = $cppRow.p50_ms
        cpp_p95_ms = $cppRow.p95_ms
        cpp_p99_ms = $cppRow.p99_ms
        ratio_valid = $false
    }
}

$report = [pscustomobject]@{
    schema = "live-cpp-net10-comparison-v1"
    status = "RED"
    decision = "No speed-up, regression percentage, or winner is valid."
    sameCorpus = $corpus.identical
    sameSqlRowCounts = $true
    loopback = "127.0.0.1"
    ports = "SMTP 2525, IMAP 1143, POP3 25110"
    scenarios = $rows
    blockers = @(
        "C++ POP3 listener did not open on the selected isolated binary.",
        "C++ IMAP scenario completed only 4/25 sessions.",
        "C++ executable is a temporary /Debug probe and not a normal reproducible release build.",
        "Net10 production host COM local-server registration is blocked by existing AppID security identity 0x80004015; listener-only helper was used.",
        "No SMTP message-acceptance, delivery-queue, 1000-concurrent IMAP, or 24-hour soak was completed."
    )
}

$net10P95Values = ($rows | ForEach-Object {
    if ($null -eq $_.net10_p95_ms) { 0 } else { [math]::Round([double]$_.net10_p95_ms, 3) }
}) -join ', '
$cppP95Values = ($rows | ForEach-Object {
    if ($null -eq $_.cpp_p95_ms) { 0 } else { [math]::Round([double]$_.cpp_p95_ms, 3) }
}) -join ', '

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory "paired-live-comparison.json") -Encoding UTF8
$rows | Export-Csv (Join-Path $OutputDirectory "paired-live-comparison.csv") -NoTypeInformation

$markdown = @(
    "# Live C++ vs .NET 10 paired protocol evidence",
    "",
    "## Decision",
    "",
    "**RED. No performance winner or speed-up ratio is valid.** The Data corpus is byte-identical and both SQL targets contain 1,000 messages, metadata rows, and recipients, but the legacy run did not complete the same protocol matrix.",
    "",
    "| Scenario | .NET 10 | C++ | Ratio |",
    "| --- | --- | --- | --- |"
)
$markdown += $rows | ForEach-Object {
    "| $($_.scenario) | $($_.net10_success)/$($_.net10_success + $_.net10_errors) success, p95 $($_.net10_p95_ms) ms | $($_.cpp_success)/$($_.cpp_success + $_.cpp_errors) success, p95 $($_.cpp_p95_ms) ms | invalid |"
}
$markdown += @(
    "",
    "### Raw latency chart",
    "",
    '```mermaid',
    'xychart-beta',
    '    title "Raw p95 latency (diagnostic only; no winner)"',
    '    x-axis [SMTP, IMAP, POP3]',
    '    y-axis "milliseconds" 0 --> 250',
    "    bar [$net10P95Values]",
    "    bar [$cppP95Values]",
    '```',
    "",
    "The first bar series is .NET 10 and the second is C++. The C++ POP3 value is zero only because no successful sample exists; it must not be interpreted as a performance result.",
    "",
    "Artifacts: net10-live-protocol.json, cpp-live-protocol/net10-live-protocol.json, corpus-equality.json.",
    "",
    "Required next environment step: obtain a normal legacy binary that can open SMTP/IMAP/POP3 from the isolated configuration, then repeat the identical workload and add message acceptance, queue, concurrency, and soak scenarios."
)
$markdown | Set-Content (Join-Path $OutputDirectory "paired-live-comparison.md") -Encoding UTF8
Write-Output "Comparison report: $(Join-Path $OutputDirectory 'paired-live-comparison.md')"
