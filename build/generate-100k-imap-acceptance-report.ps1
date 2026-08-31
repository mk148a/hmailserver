param(
    [Parameter(Mandatory = $true)]
    [string]$InputRoot
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($InputRoot)
$cppPath = Join-Path $root 'concurrent-cpp-1-rerun\concurrent-imap\live-concurrent-imap.json'
$net10Path = Join-Path $root 'concurrent-net10-1-rerun\live-concurrent-imap.json'
$cpp = Get-Content -LiteralPath $cppPath -Raw | ConvertFrom-Json
$net10 = Get-Content -LiteralPath $net10Path -Raw | ConvertFrom-Json

foreach ($report in @($cpp, $net10)) {
    if ($report.status -ne 'PASS' -or $report.messageCount -ne 100000 -or
        $report.samples[0].searchResultCount -ne 100000 -or
        $report.samples[0].sortResultCount -ne 100000) {
        throw "100k acceptance input is incomplete or failed."
    }
}

$rows = @(
    [pscustomobject]@{
        implementation = 'cpp'
        status = $cpp.status
        messageCount = $cpp.messageCount
        searchCount = $cpp.samples[0].searchResultCount
        sortCount = $cpp.samples[0].sortResultCount
        p50_ms = $cpp.summary.p50_ms
        p95_ms = $cpp.summary.p95_ms
        p99_ms = $cpp.summary.p99_ms
        workloadSeconds = $cpp.summary.workload_seconds
        executableSha256 = $cpp.executableProvenance.sha256
    },
    [pscustomobject]@{
        implementation = 'net10'
        status = $net10.status
        messageCount = $net10.messageCount
        searchCount = $net10.samples[0].searchResultCount
        sortCount = $net10.samples[0].sortResultCount
        p50_ms = $net10.summary.p50_ms
        p95_ms = $net10.summary.p95_ms
        p99_ms = $net10.summary.p99_ms
        workloadSeconds = $net10.summary.workload_seconds
        executableSha256 = $net10.executableProvenance.sha256
    }
)
$rows | Export-Csv -LiteralPath (Join-Path $root 'imap-100k-comparison.csv') -NoTypeInformation
$manifestPath = 'C:\hmail-perf-pair-100k-20260901\paired-fixture.json'
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$ratio = [math]::Round([double]$cpp.summary.p50_ms / [double]$net10.summary.p50_ms, 3)
$markdown = @(
    '# Paired 100k IMAP SEARCH/SORT acceptance',
    '',
    'Status: PASS for both implementations (one session, Full profile)',
    "Fixture: $($cpp.fixtureId)",
    "Manifest SHA-256: $manifestHash",
    'Corpus: 100000 SQL messages and 100000 byte-matched Data files per side',
    'Database versions: C++ 5708 / Net10 6000',
    'Listener: 127.0.0.1:1143',
    '',
    '| Implementation | Acceptance | p50 ms | p95 ms | p99 ms | Search | Sort |',
    '| --- | --- | ---: | ---: | ---: | ---: | ---: |'
)
foreach ($row in $rows) {
    $markdown += "| $($row.implementation) | $($row.status) | $($row.p50_ms) | $($row.p95_ms) | $($row.p99_ms) | $($row.searchCount)/$($row.messageCount) | $($row.sortCount)/$($row.messageCount) |"
}
$markdown += @(
    '',
    '## Interpretation',
    '',
    "The bounded single-session p50 ratio is $ratio (C++ divided by Net10). This is a mailbox acceptance measurement, not a general performance winner claim.",
    'The release gate remains RED because 500/1000-session C++ capacity, SMTP/delivery/queue, restore/installer, COM lifecycle, and 24-hour soak remain open.',
    '',
    'Raw run reports remain outside the repository under C:\\hmail-perf-pair-100k-20260901; this committed evidence contains compact summaries only.'
)
$markdown | Set-Content -LiteralPath (Join-Path $root 'imap-100k-comparison.md') -Encoding UTF8

$max = [math]::Max([double]$cpp.summary.p50_ms, [double]$net10.summary.p50_ms)
$cppWidth = [math]::Max(1, [math]::Round(560 * [double]$cpp.summary.p50_ms / $max))
$net10Width = [math]::Max(1, [math]::Round(560 * [double]$net10.summary.p50_ms / $max))
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="760" height="260" viewBox="0 0 760 260">
<title>Paired 100k IMAP SEARCH/SORT p50 latency</title>
<rect width="760" height="260" fill="white"/>
<text x="30" y="34" font-family="Arial" font-size="20" font-weight="bold">100k IMAP SEARCH + SORT p50 latency</text>
<text x="30" y="58" font-family="Arial" font-size="13">One session, manifest-bound disposable fixture</text>
<text x="30" y="105" font-family="Arial" font-size="14">C++</text>
<rect x="105" y="84" width="$cppWidth" height="32" fill="#b4472e"/>
<text x="680" y="106" text-anchor="end" font-family="Arial" font-size="14">$($cpp.summary.p50_ms) ms</text>
<text x="30" y="165" font-family="Arial" font-size="14">Net10</text>
<rect x="105" y="144" width="$net10Width" height="32" fill="#267a68"/>
<text x="680" y="166" text-anchor="end" font-family="Arial" font-size="14">$($net10.summary.p50_ms) ms</text>
<text x="30" y="220" font-family="Arial" font-size="12" fill="#444">Lower is better; this chart does not establish release readiness.</text>
</svg>
"@
$svg | Set-Content -LiteralPath (Join-Path $root 'imap-100k-p50.svg') -Encoding UTF8
Write-Output "Generated 100k IMAP evidence under $root"
