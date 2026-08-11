param(
    [ValidateRange(1, 500)]
    [int]$MessageCount = 50,
    [string]$BenchmarkStagingRoot = "C:\hmail-perf-pair-20260811_1748\net10",
    [string]$BenchmarkDatabase = "hmail_perf_pair_net10_20260811_1748",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$dataRoot = Join-Path $BenchmarkStagingRoot "Data"
$testProject = Join-Path $repoRoot "hmailserver\source\Server.Net10\tests\HMailServer.Net10.Tests\HMailServer.Net10.Tests.csproj"

if ($BenchmarkDatabase -notmatch '^hmail_perf_[a-z0-9_]+$') { throw "Refusing non-disposable benchmark database: $BenchmarkDatabase" }
if ([IO.Path]::GetFullPath($BenchmarkStagingRoot) -notmatch '(?i)^C:\\hmail-perf-') { throw "Refusing non-disposable benchmark root: $BenchmarkStagingRoot" }
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) { throw "Disposable Data root is missing: $dataRoot" }
if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) { throw "Test project is missing: $testProject" }

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\net10-live-delivery-queue"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputPath = Join-Path $OutputDirectory "net10-live-delivery-queue.json"

$env:HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC = "1"
$env:HMAILSERVER_NET10_LIVE_SQL_DELIVERY_COUNT = [string]$MessageCount
$env:HMAILSERVER_NET10_LIVE_SQL_CONNECTION = "Server=localhost;Database=$BenchmarkDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$env:HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT = $dataRoot
$env:HMAILSERVER_NET10_LIVE_SQL_DELIVERY_OUTPUT = $outputPath

& dotnet test $testProject --configuration Release --filter "FullyQualifiedName~DisposableDeliveryQueueLocalDeliveryAndRetryAreUsable" --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { throw "Disposable delivery queue diagnostic failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) { throw "Delivery queue diagnostic did not emit JSON: $outputPath" }
Write-Host "Delivery queue diagnostic passed: $outputPath"
