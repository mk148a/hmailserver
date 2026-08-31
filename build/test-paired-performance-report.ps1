param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($InputDirectory)
$required = @(
    "PERFORMANCE_COMPARISON.md",
    "performance-summary.json",
    "performance-summary.csv",
    "protocol-samples.csv",
    "concurrent-imap-samples.csv",
    "smtp-acceptance-samples.csv",
    "net10-imap-soak-waves.csv",
    "protocol-p95.png",
    "imap-concurrency.png",
    "smtp-acceptance.png",
    "net10-imap-soak-resources.png"
)
foreach ($name in $required) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) {
        throw "Required performance report artifact is missing or empty: $path"
    }
}

$report = Get-Content -LiteralPath (Join-Path $root "performance-summary.json") -Raw | ConvertFrom-Json
if ($report.schema -ne "paired-cpp-net10-performance-v1") {
    throw "Unexpected performance summary schema: $($report.schema)"
}
if ($report.gate -ne "RED") {
    throw "The incomplete release benchmark matrix must remain RED."
}
if ($report.fixture.cppDatabaseVersion -ne 5708 -or $report.fixture.net10DatabaseVersion -ne 6000) {
    throw "The report must preserve the legacy 5708 and Net10 6000 schema boundary."
}
if ($report.fixture.dataParity.exact -ne $true -or $report.fixture.messageParity.exact -ne $true) {
    throw "The report cannot claim a paired comparison without exact Data and logical message parity."
}
if ($report.fixture.dataParity.fileCount -ne 1000 -or $report.fixture.messageParity.rowCount -ne 1000) {
    throw "The paired fixture must contain the expected 1,000 files/messages."
}
if ($report.source.cpp.postBuildRegistrationDisabled -ne $true) {
    throw "The legacy benchmark build must disable post-build registration."
}
if ($report.source.cpp.sha256 -notmatch "^[0-9A-F]{64}$" -or $report.source.net10.sha256 -notmatch "^[0-9A-F]{64}$") {
    throw "Executable provenance hashes are incomplete."
}
if ($report.source.runDescriptorStatus -ne "SEALED" -or
    $report.source.runDescriptorSha256 -notmatch "^[0-9A-F]{64}$") {
    throw "The paired report must be bound to a sealed run descriptor."
}
if ($null -eq $report.source.runDescriptorArtifacts -or
    @($report.source.runDescriptorArtifacts.PSObject.Properties).Count -ne 11) {
    throw "The paired report must retain all sealed artifact-slot hashes."
}

$protocol = @($report.protocol.findings)
if ($protocol.Count -ne 3) {
    throw "Expected SMTP, IMAP, and POP3 protocol findings."
}
foreach ($row in $protocol) {
    $expectedRatio = [math]::Round([double]$row.cppP95Ms / [double]$row.net10P95Ms, 6)
    if ([math]::Abs($expectedRatio - [double]$row.cppOverNet10Ratio) -gt 0.000001) {
        throw "Protocol ratio does not reconcile for $($row.scenario)."
    }
}

$concurrent = @($report.concurrentImap.findings)
if ($concurrent.Count -ne 3) {
    throw "Expected 100, 500, and 1,000-session IMAP findings."
}
$oneThousand = @($concurrent | Where-Object concurrency -eq 1000)
if ($oneThousand.Count -ne 1 -or $oneThousand[0].net10Status -ne "PASS" -or $oneThousand[0].net10Successes -ne 1000) {
    throw "Net10 1,000-session acceptance is missing or incomplete."
}
if ($oneThousand[0].cppStatus -eq "PASS") {
    if ($oneThousand[0].cppSuccesses -ne 1000 -or $null -eq $oneThousand[0].cppOverNet10Ratio) {
        throw "A passing legacy 1,000-session artifact must publish a reconciled ratio."
    }
}
elseif ($null -ne $oneThousand[0].cppOverNet10Ratio) {
    throw "A failed legacy 1,000-session artifact must not publish a performance ratio."
}

if ($report.smtpAcceptance.cpp.accepted -ne 500 -or $report.smtpAcceptance.net10.accepted -ne 500) {
    throw "SMTP acceptance must be 500/500 for both implementations."
}
if ($report.smtpAcceptance.cpp.messageRowDelta -ne 500 -or
    $report.smtpAcceptance.cpp.dataFileDelta -ne 500 -or
    $report.smtpAcceptance.net10.messageRowDelta -ne 500 -or
    $report.smtpAcceptance.net10.dataFileDelta -ne 500) {
    throw "SMTP SQL/Data accounting must be exactly +500 for both implementations."
}
$expectedSmtpLatencyRatio = [math]::Round(
    [double]$report.smtpAcceptance.cpp.p95Ms / [double]$report.smtpAcceptance.net10.p95Ms,
    6)
if ([math]::Abs($expectedSmtpLatencyRatio - [double]$report.smtpAcceptance.p95LatencyCppOverNet10Ratio) -gt 0.000001) {
    throw "SMTP p95 latency ratio does not reconcile."
}
$expectedSmtpThroughputRatio = [math]::Round(
    [double]$report.smtpAcceptance.net10.throughputMessagesPerSecond /
        [double]$report.smtpAcceptance.cpp.throughputMessagesPerSecond,
    6)
if ([math]::Abs($expectedSmtpThroughputRatio - [double]$report.smtpAcceptance.throughputNet10OverCppRatio) -gt 0.000001) {
    throw "SMTP throughput ratio does not reconcile."
}

if ($report.net10ShortSoak.status -ne "PASS" -or
    $report.net10ShortSoak.waves -ne 20 -or
    $report.net10ShortSoak.sessions -ne 20000 -or
    $report.net10ShortSoak.errors -ne 0) {
    throw "The Net10 20-wave short-soak evidence is incomplete."
}

$markdown = Get-Content -LiteralPath (Join-Path $root "PERFORMANCE_COMPARISON.md") -Raw
foreach ($requiredText in @(
        "overall performance release gate remains **RED**",
        "no C++/Net10 latency or throughput ratio is published at that load",
        "Mandatory 24-hour memory/handle/thread/socket soak remains open",
        "POP3 is materially slower in Net10")) {
    if ($markdown -notlike "*$requiredText*") {
        throw "Performance report is missing the required limitation: $requiredText"
    }
}
if ($markdown -match "(?i)C:\\|E:\\|Users\\|hmail_perf_pair_") {
    throw "Committed performance report contains an unsanitized local path or disposable database name."
}

$csvRows = @(Import-Csv -LiteralPath (Join-Path $root "performance-summary.csv"))
if ($csvRows.Count -lt 8) {
    throw "Performance summary CSV is unexpectedly incomplete."
}
$soakRows = @(Import-Csv -LiteralPath (Join-Path $root "net10-imap-soak-waves.csv"))
if ($soakRows.Count -ne 20 -or @($soakRows | Where-Object errors -ne "0").Count -ne 0) {
    throw "Short-soak CSV must contain 20 clean wave rows."
}

Write-Output "Validated paired C++/.NET 10 report: protocol, concurrency, SMTP accounting, charts, and 20,000-session short soak."
