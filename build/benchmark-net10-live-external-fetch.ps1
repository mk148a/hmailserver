param(
    [string]$SqlServerConnection = "Server=localhost;Database=hmail_perf_pair_net10_20260811_1748;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10",
    [string]$BenchmarkDatabase = "hmail_perf_pair_net10_20260811_1748",
    [string]$BenchmarkDataRoot = "C:\hmail-perf-pair-20260811_1748\net10\Data",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\net10-external-fetch"
}

if ($BenchmarkDatabase -notmatch '^hmail_perf_[a-z0-9_]+$') {
    throw "Refusing non-disposable benchmark database: $BenchmarkDatabase"
}
$fullDataRoot = [IO.Path]::GetFullPath($BenchmarkDataRoot)
if ($fullDataRoot -notmatch '(?i)^C:\\hmail-perf-' -or $fullDataRoot -match '(?i)hmailserver57') {
    throw "Refusing non-disposable benchmark Data root: $fullDataRoot"
}
if (-not (Test-Path -LiteralPath $fullDataRoot -PathType Container)) {
    throw "Disposable benchmark Data root is missing: $fullDataRoot"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "net10-live-external-fetch.json"
$csvPath = Join-Path $OutputDirectory "net10-live-external-fetch.csv"
$markdownPath = Join-Path $OutputDirectory "net10-live-external-fetch.md"

$env:HMAILSERVER_NET10_LIVE_EXTERNAL_FETCH = "1"
$env:HMAILSERVER_NET10_LIVE_SQL_CONNECTION = $SqlServerConnection
$env:HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT = $fullDataRoot
$env:HMAILSERVER_NET10_LIVE_EXTERNAL_FETCH_REPORT = $jsonPath

$testProject = Join-Path $repoRoot "hmailserver\source\Server.Net10\tests\HMailServer.Net10.Tests\HMailServer.Net10.Tests.csproj"
& dotnet test $testProject --no-restore --configuration Debug --filter "FullyQualifiedName~LiveExternalFetchIntegrationTests" --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { throw "Live external-fetch acceptance failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) { throw "The acceptance test did not emit $jsonPath." }

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
if ($report.status -ne "PASS") { throw "Unexpected external-fetch status: $($report.status)" }
[pscustomobject]@{
    implementation = $report.implementation
    database = $report.database
    cycles = $report.cycles
    messages_downloaded = $report.messagesDownloaded
    messages_accepted = $report.messagesAccepted
    final_known_uids = $report.knownUids
    cycle_p50_ms = $report.cycleP50Ms
    cycle_p95_ms = $report.cycleP95Ms
    cycle_p99_ms = $report.cycleP99Ms
} | Export-Csv -LiteralPath $csvPath -NoTypeInformation
@(
    "# .NET 10 disposable external-fetch acceptance",
    "",
    "Status: **$($report.status)**",
    "",
    "- Cycles: $($report.cycles)",
    "- Messages downloaded/accepted: $($report.messagesDownloaded)/$($report.messagesAccepted)",
    "- Final known UID snapshot: $($report.knownUids)",
    "- Cycle p50/p95/p99: $($report.cycleP50Ms)/$($report.cycleP95Ms)/$($report.cycleP99Ms) ms",
    "- Endpoint: $($report.loopbackEndpoint)",
    "- Egress: $($report.egressPolicy)",
    "- Cleanup: fetch-account and UID rows removed after the run",
    "",
    "This is isolated Net10 external POP3/SQL acceptance. It is not a C++ comparison and does not claim a speed-up ratio."
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "status=$($report.status); downloaded=$($report.messagesDownloaded); accepted=$($report.messagesAccepted); p50=$($report.cycleP50Ms)ms"
Write-Output "JSON: $jsonPath"
Write-Output "CSV: $csvPath"
Write-Output "Markdown: $markdownPath"
