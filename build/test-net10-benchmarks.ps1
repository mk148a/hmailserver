Param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$localDotnet = Join-Path $workspaceRoot 'tools\dotnet10\dotnet.exe'
$benchmarkProject = Join-Path $repoRoot 'hmailserver\source\Server.Net10\benchmarks\HMailServer.Net10.Benchmarks\HMailServer.Net10.Benchmarks.csproj'

if (Test-Path $localDotnet) {
    $dotnet = (Get-Item $localDotnet).FullName
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) ("hmailserver-net10-benchmark-" + [Guid]::NewGuid().ToString("N"))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitCommit)) {
    throw "Unable to determine the repository commit."
}

& $dotnet build $benchmarkProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnet run --project $benchmarkProject --configuration $Configuration --no-build -- --mode search-sort --count 100000 --seed 5700 --output $OutputDirectory --git-commit $gitCommit
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$jsonPath = Join-Path $OutputDirectory 'offline-imap-search-sort.json'
$csvPath = Join-Path $OutputDirectory 'offline-imap-search-sort.csv'
$markdownPath = Join-Path $OutputDirectory 'offline-imap-search-sort.md'
foreach ($artifactPath in @($jsonPath, $csvPath, $markdownPath)) {
    if (-not (Test-Path $artifactPath -PathType Leaf)) {
        throw "Required benchmark artifact is missing: $artifactPath"
    }
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
if ($report.Scenario -ne 'offline-imap-search-sort-100k') {
    throw "Unexpected benchmark scenario: $($report.Scenario)"
}
if ($report.MessageCount -ne 100000) {
    throw "Unexpected benchmark message count: $($report.MessageCount)"
}
if ($report.Seed -ne 5700) {
    throw "Unexpected benchmark seed: $($report.Seed)"
}
if ($report.SearchTerm -ne 'needle') {
    throw "Unexpected benchmark search term: $($report.SearchTerm)"
}
if ($report.SortOrder -ne 'DATE DESC, UID ASC') {
    throw "Unexpected benchmark sort order: $($report.SortOrder)"
}
if ($report.Correct -ne $true) {
    throw "Benchmark correctness check failed."
}
if ($report.ThresholdPassed -ne $true) {
    throw "Benchmark threshold check failed."
}
if ($report.ExpectedMatchCount -ne $report.ActualMatchCount) {
    throw "Benchmark expected and actual match counts differ."
}
if ([string]::IsNullOrWhiteSpace([string]$report.GitCommit) -or [string]$report.GitCommit -eq 'unknown') {
    throw "Benchmark commit metadata is missing."
}
if ([string]::IsNullOrWhiteSpace([string]$report.RuntimeDescription)) {
    throw "Benchmark runtime metadata is missing."
}

if ($report.GitCommit -ne $gitCommit) {
    throw "Benchmark commit metadata does not match the pre-build HEAD."
}

$startedUtc = [DateTimeOffset]::MinValue
$endedUtc = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse([string]$report.StartedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$startedUtc) -or
    -not [DateTimeOffset]::TryParse([string]$report.EndedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$endedUtc)) {
    throw "Benchmark timestamps are not parseable."
}
if ($endedUtc -lt $startedUtc) {
    throw "Benchmark ended before it started."
}

$csvRows = @(Import-Csv -LiteralPath $csvPath)
if ($csvRows.Count -ne 1) {
    throw "Expected exactly one benchmark CSV row, found $($csvRows.Count)."
}
$csvRow = $csvRows[0]
$csvValues = @{
    scenario = [string]$report.Scenario
    git_commit = [string]$report.GitCommit
    message_count = [string]$report.MessageCount
    seed = [string]$report.Seed
    expected_matches = [string]$report.ExpectedMatchCount
    actual_matches = [string]$report.ActualMatchCount
    correct = ([bool]$report.Correct).ToString().ToLowerInvariant()
    threshold_passed = ([bool]$report.ThresholdPassed).ToString().ToLowerInvariant()
}
foreach ($column in $csvValues.Keys) {
    if (-not $csvRow.PSObject.Properties.Name.Contains($column) -or [string]$csvRow.$column -ne $csvValues[$column]) {
        throw "CSV column '$column' does not match the JSON report."
    }
}

$markdown = Get-Content -LiteralPath $markdownPath -Raw
$datasetMarker = "$($report.MessageCount.ToString('N0', [Globalization.CultureInfo]::InvariantCulture)) messages, seed ``$($report.Seed)``"
foreach ($marker in @(
        [string]$report.Scenario,
        [string]$report.GitCommit,
        $datasetMarker,
        "Correctness | ``$($report.Correct)``",
        "p95 threshold | ",
        "``$($report.ThresholdPassed)``")) {
    if (-not $markdown.Contains($marker)) {
        throw "Markdown benchmark marker is missing: $marker"
    }
}

Write-Host "Benchmark gate passed. Artifacts: $OutputDirectory"
