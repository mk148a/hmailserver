param(
    [string]$InputDirectory = "",
    [string]$OutputDirectory = "",
    [string]$Net10ReportPath = "",
    [string]$CppReportPath = "",
    [string]$CorpusPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
if ([string]::IsNullOrWhiteSpace($InputDirectory)) {
    $InputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = $InputDirectory
}
if ([string]::IsNullOrWhiteSpace($Net10ReportPath)) {
    $Net10ReportPath = Join-Path $InputDirectory "net10-live-protocol.json"
}
if ([string]::IsNullOrWhiteSpace($CppReportPath)) {
    $CppReportPath = Join-Path $InputDirectory "cpp-live-protocol\net10-live-protocol.json"
}
if ([string]::IsNullOrWhiteSpace($CorpusPath)) {
    $CorpusPath = Join-Path $InputDirectory "corpus-equality.json"
}

$net10 = Get-Content -LiteralPath $Net10ReportPath -Raw | ConvertFrom-Json
$cpp = Get-Content -LiteralPath $CppReportPath -Raw | ConvertFrom-Json
$corpus = Get-Content -LiteralPath $CorpusPath -Raw | ConvertFrom-Json

if ($net10.schema -ne "live-protocol-v1" -or $net10.implementation -ne "net10") {
    throw "The .NET 10 input is not a live-protocol-v1 net10 report: $Net10ReportPath"
}
if ($cpp.schema -ne "live-protocol-v1" -or $cpp.implementation -ne "cpp") {
    throw "The C++ input is not a live-protocol-v1 cpp report: $CppReportPath"
}
if ($null -eq $cpp.isolationPreflight) {
    throw "The C++ input is missing the required registry/config isolation preflight."
}
if ($null -eq $cpp.executableProvenance) {
    throw "The C++ input is missing executable provenance."
}
if ($cpp.executableProvenance.sha256 -notmatch "^[0-9A-Fa-f]{64}$" -or [int64]$cpp.executableProvenance.length -le 0) {
    throw "The C++ executable provenance is incomplete or invalid."
}
if ($cpp.status -eq "PASS" -and $cpp.isolationPreflight.passed -ne $true) {
    throw "A passing C++ input must have a passing isolation preflight."
}
if ($corpus.identical -ne $true) {
    throw "The input corpus-equality evidence is not identical; no paired comparison report is allowed."
}

$blockers = [System.Collections.Generic.List[string]]::new()
if ($cpp.isolationPreflight.passed -ne $true) {
    $blockers.Add("C++ launch was refused by the registry/config isolation preflight; no C++ workload result exists.")
}
else {
    $cppImap = @($cpp.summary | Where-Object scenario -eq "imap")
    $cppPop3 = @($cpp.summary | Where-Object scenario -eq "pop3")
    if ($cppImap.Count -eq 1 -and $cppImap[0].errors -gt 0) {
        $blockers.Add("C++ IMAP protocol did not complete its requested matrix.")
    }
    if ($cppPop3.Count -eq 1 -and $cppPop3[0].errors -gt 0) {
        $blockers.Add("C++ POP3 protocol did not complete its requested matrix.")
    }
}
if ($cpp.status -ne "PASS") {
    $blockers.Add("The C++ live protocol report is FAIL, so no paired performance result is valid.")
}
if ($net10.status -ne "PASS") {
    $blockers.Add("The .NET 10 live protocol report is FAIL, so no paired performance result is valid.")
}
$blockers.Add("The C++ executable is a temporary /Debug probe and not a normal reproducible release build.")
$blockers.Add("Net10 production host COM local-server registration is blocked by existing AppID security identity 0x80004015; listener-only helper was used.")
$blockers.Add("SQL row-count equality was not supplied to this comparison generator and is not asserted.")
$blockers.Add("No SMTP message-acceptance, delivery-queue, 1000-concurrent IMAP, or 24-hour soak was completed.")

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
    sameSqlRowCounts = $false
    loopback = "127.0.0.1"
    ports = "SMTP 2525, IMAP 1143, POP3 25110"
    sourceReports = [ordered]@{
        net10 = [IO.Path]::GetFullPath($Net10ReportPath)
        cpp = [IO.Path]::GetFullPath($CppReportPath)
        corpus = [IO.Path]::GetFullPath($CorpusPath)
    }
    cppExecutableProvenance = $cpp.executableProvenance
    scenarios = $rows
    blockers = $blockers.ToArray()
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
    "**RED. No performance winner or speed-up ratio is valid.** The input Data corpus-equality evidence is identical, but the legacy run did not complete the same protocol matrix.",
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
    "Artifacts: the input report paths are recorded in paired-live-comparison.json; C++ preflight and executable provenance are mandatory.",
    "",
    "Required next environment step: obtain a normal legacy binary that can open SMTP/IMAP/POP3 from the isolated configuration, then repeat the identical workload and add message acceptance, queue, concurrency, and soak scenarios."
)
$markdown | Set-Content (Join-Path $OutputDirectory "paired-live-comparison.md") -Encoding UTF8
Write-Output "Comparison report: $(Join-Path $OutputDirectory 'paired-live-comparison.md')"
