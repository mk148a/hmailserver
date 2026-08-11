param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory
)

$ErrorActionPreference = "Stop"
$jsonPath = Join-Path $InputDirectory "paired-live-comparison.json"
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
    throw "Paired comparison JSON is missing: $jsonPath"
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
if ($report.schema -ne "live-cpp-net10-comparison-v1") {
    throw "Unexpected paired comparison schema: $($report.schema)"
}
if ($report.status -ne "RED" -or $report.decision -notmatch "No speed-up") {
    throw "Paired comparison must remain an explicit RED/no-speed-up decision."
}
if ($report.sameCorpus -ne $true -or $report.sameSqlRowCounts -ne $false) {
    throw "Paired comparison must prove Data equality and must not claim unprovided SQL row-count equality."
}
if ($report.loopback -ne "127.0.0.1" -or $report.ports -notmatch "SMTP 2525, IMAP 1143, POP3 25110") {
    throw "Paired comparison loopback/port contract is invalid."
}

$sourceNames = @("net10", "cpp", "corpus")
foreach ($name in $sourceNames) {
    $sourcePath = $report.sourceReports.$name
    if ([string]::IsNullOrWhiteSpace($sourcePath) -or -not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Paired comparison source report is missing for '$name': $sourcePath"
    }
}
$cpp = Get-Content -LiteralPath $report.sourceReports.cpp -Raw | ConvertFrom-Json
if ($cpp.schema -ne "live-protocol-v1" -or $cpp.implementation -ne "cpp") {
    throw "Paired comparison C++ source report is invalid."
}
if ($null -eq $cpp.isolationPreflight -or $null -eq $cpp.executableProvenance) {
    throw "Paired comparison C++ source report lacks isolation/provenance evidence."
}
if ($cpp.executableProvenance.sha256 -notmatch "^[0-9A-Fa-f]{64}$") {
    throw "Paired comparison C++ executable provenance SHA-256 is invalid."
}

$rows = @($report.scenarios)
if ($rows.Count -ne 3 -or @($rows | Where-Object scenario -notin @("smtp", "imap", "pop3")).Count -ne 0) {
    throw "Paired comparison must contain exactly SMTP, IMAP, and POP3 rows."
}
if (@($rows | Where-Object ratio_valid -ne $false).Count -ne 0) {
    throw "Paired comparison must not mark any scenario ratio as valid."
}

Write-Output "Validated paired comparison: RED, sameCorpus=$($report.sameCorpus), sameSqlRowCounts=$($report.sameSqlRowCounts), ratios=invalid."
