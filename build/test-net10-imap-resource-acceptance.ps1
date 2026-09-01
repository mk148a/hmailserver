param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [ValidateRange(1, 100)]
    [int]$ExpectedWaves = 5,
    [ValidateRange(1, 5000)]
    [int]$ExpectedConcurrency = 100,
    [ValidateRange(1, 1000000)]
    [int]$ExpectedMessageCount = 100000,
    [ValidateRange(1, 1024)]
    [int]$MaxPrivateBytesGrowthMiB = 64,
    [ValidateRange(1, 1000)]
    [int]$MaxHandlesGrowth = 100,
    [ValidateRange(1, 100)]
    [int]$MaxThreadsGrowth = 20
)

$ErrorActionPreference = "Stop"
$fullDirectory = [IO.Path]::GetFullPath($InputDirectory)
$jsonPath = Join-Path $fullDirectory "live-concurrent-imap.json"
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
    throw "Concurrent IMAP report is missing: $jsonPath"
}

$validator = Join-Path $PSScriptRoot "test-net10-live-concurrent-imap.ps1"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $validator `
    -InputDirectory $fullDirectory `
    -ExpectedConcurrency $ExpectedConcurrency `
    -ExpectedWaves $ExpectedWaves `
    -ExpectedMessageCount $ExpectedMessageCount
if ($LASTEXITCODE -ne 0) {
    throw "The standard concurrent IMAP validator failed with exit code $LASTEXITCODE."
}

$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
if ($report.status -ne "PASS" -or $report.implementation -ne "net10" -or $report.profile -ne "Admission") {
    throw "Resource acceptance requires a passing Net10 Admission report."
}
if ($report.summary.successes -ne ($ExpectedConcurrency * $ExpectedWaves) -or
    $report.summary.errors -ne 0 -or $report.summary.timeouts -ne 0 -or
    @($report.readinessFailures).Count -ne 0 -or @($report.shutdownFailures).Count -ne 0 -or
    @($report.runtimeFailures).Count -ne 0) {
    throw "Resource acceptance report contains workload or lifecycle failures."
}

function Require-Metric([object]$Metric, [string]$Name) {
    if ($null -eq $Metric -or [long]$Metric.privateBytes -le 0 -or
        [int]$Metric.handles -le 0 -or [int]$Metric.threads -le 0) {
        throw "Missing or invalid process metric: $Name"
    }
}

Require-Metric $report.processBefore "processBefore"
Require-Metric $report.processAfterImmediate "processAfterImmediate"
Require-Metric $report.processAfter "processAfter"

$privateGrowth = ([long]$report.processAfter.privateBytes - [long]$report.processBefore.privateBytes) / 1MB
$handlesGrowth = [int]$report.processAfter.handles - [int]$report.processBefore.handles
$threadsGrowth = [int]$report.processAfter.threads - [int]$report.processBefore.threads
if ($privateGrowth -gt $MaxPrivateBytesGrowthMiB -or $handlesGrowth -gt $MaxHandlesGrowth -or $threadsGrowth -gt $MaxThreadsGrowth) {
    throw "Settled process growth exceeded the acceptance limits: private=$privateGrowth MiB, handles=$handlesGrowth, threads=$threadsGrowth."
}

$waves = @($report.waveMetrics)
if ($waves.Count -ne $ExpectedWaves) {
    throw "Expected $ExpectedWaves wave metrics, got $($waves.Count)."
}
foreach ($wave in $waves) {
    Require-Metric $wave.processBefore "wave $($wave.wave) processBefore"
    Require-Metric $wave.processAfterSettle "wave $($wave.wave) processAfterSettle"
    if ($wave.successes -ne $ExpectedConcurrency -or $wave.errors -ne 0) {
        throw "Wave $($wave.wave) did not complete exactly $ExpectedConcurrency successful sessions."
    }
}

$wavePrivateValues = @($waves | ForEach-Object { [long]$_.processAfterSettle.privateBytes })
$waveHandleValues = @($waves | ForEach-Object { [int]$_.processAfterSettle.handles })
$waveThreadValues = @($waves | ForEach-Object { [int]$_.processAfterSettle.threads })
if ((($wavePrivateValues | Measure-Object -Maximum).Maximum - ($wavePrivateValues | Measure-Object -Minimum).Minimum) -gt ($MaxPrivateBytesGrowthMiB * 1MB) -or
    (($waveHandleValues | Measure-Object -Maximum).Maximum - ($waveHandleValues | Measure-Object -Minimum).Minimum) -gt $MaxHandlesGrowth -or
    (($waveThreadValues | Measure-Object -Maximum).Maximum - ($waveThreadValues | Measure-Object -Minimum).Minimum) -gt $MaxThreadsGrowth) {
    throw "Settled wave-to-wave process growth exceeded the acceptance limits."
}

Write-Host "Net10 IMAP resource acceptance is valid: $($report.summary.successes)/$($ExpectedConcurrency * $ExpectedWaves) sessions, $ExpectedWaves waves, growth $([math]::Round($privateGrowth, 3)) MiB/$handlesGrowth handles/$threadsGrowth threads."
