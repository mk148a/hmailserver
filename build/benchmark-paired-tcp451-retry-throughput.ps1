param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureManifest,
    [string]$OutputDirectory = "",
    [ValidateRange(25, 25)]
    [int]$MessageCount = 25,
    [ValidateRange(25000, 29900)]
    [int]$SinkPort = 26045,
    [ValidateRange(30, 300)]
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

function Get-ProcessResourceSnapshot {
    param([int]$ProcessId)

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return [ordered]@{ privateBytes = 0; handles = 0; threads = 0 }
    }
    return [ordered]@{
        privateBytes = [int64]$process.PrivateMemorySize64
        handles = [int]$process.Handles
        threads = [int]$process.Threads.Count
    }
}

function Get-ServiceProcessId {
    param([string]$Name)

    $service = Get-CimInstance Win32_Service -Filter "Name='$Name'" -ErrorAction SilentlyContinue
    if ($null -eq $service -or [int]$service.ProcessId -le 0) { return 0 }
    return [int]$service.ProcessId
}

function Invoke-BenchmarkChild {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$ServiceName = "",
        [string]$OutputPath
    )

    $errorPath = "$OutputPath.err"
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -PassThru -RedirectStandardOutput $OutputPath -RedirectStandardError $errorPath
    $before = Get-ProcessResourceSnapshot -ProcessId $process.Id
    $peak = $before
    $start = [DateTimeOffset]::UtcNow
    while (-not $process.HasExited) {
        $samplePid = if ([string]::IsNullOrWhiteSpace($ServiceName)) { $process.Id } else { Get-ServiceProcessId $ServiceName }
        if ($samplePid -le 0) { $samplePid = $process.Id }
        $sample = Get-ProcessResourceSnapshot -ProcessId $samplePid
        if ($sample.privateBytes -gt $peak.privateBytes) { $peak.privateBytes = $sample.privateBytes }
        if ($sample.handles -gt $peak.handles) { $peak.handles = $sample.handles }
        if ($sample.threads -gt $peak.threads) { $peak.threads = $sample.threads }
        Start-Sleep -Milliseconds 100
    }
    $process.WaitForExit()
    $process.Refresh()
    $end = [DateTimeOffset]::UtcNow
    [pscustomobject]@{
        exitCode = [int]$process.ExitCode
        durationMs = [math]::Round(($end - $start).TotalMilliseconds, 3)
        before = $before
        peak = $peak
        after = Get-ProcessResourceSnapshot -ProcessId $process.Id
    }
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)

    if ($Values.Count -eq 0) { return 0 }
    $ordered = @($Values | Sort-Object)
    $rank = ($Percentile / 100) * ($ordered.Count - 1)
    $lower = [int][math]::Floor($rank)
    $upper = [int][math]::Ceiling($rank)
    if ($lower -eq $upper) { return [math]::Round($ordered[$lower], 3) }
    return [math]::Round($ordered[$lower] + (($ordered[$upper] - $ordered[$lower]) * ($rank - $lower)), 3)
}

function Get-AggregateResult {
    param([string]$Implementation, [object[]]$Samples)

    $durations = @($Samples | ForEach-Object { [double]$_.durationMs })
    $completed = @($Samples | Where-Object { $_.status -eq "PASS" -and $_.cleanup -eq $true })
    $totalMs = [double](($durations | Measure-Object -Sum).Sum)
    $totalSeconds = $totalMs / 1000
    [ordered]@{
        implementation = $Implementation
        status = if ($completed.Count -eq $MessageCount) { "PASS" } else { "FAIL" }
        requestedMessages = $MessageCount
        completedMessages = $completed.Count
        errors = $MessageCount - $completed.Count
        total_ms = [math]::Round($totalMs, 3)
        p50_ms = Get-Percentile $durations 50
        p95_ms = Get-Percentile $durations 95
        p99_ms = Get-Percentile $durations 99
        throughput_messages_per_second = if ($totalSeconds -gt 0) { [math]::Round($MessageCount / $totalSeconds, 3) } else { 0 }
        resource = [ordered]@{
            peakPrivateBytes = [int64](($Samples | ForEach-Object { [int64]$_.resource.peak.privateBytes } | Measure-Object -Maximum).Maximum)
            peakHandles = [int](($Samples | ForEach-Object { [int]$_.resource.peak.handles } | Measure-Object -Maximum).Maximum)
            peakThreads = [int](($Samples | ForEach-Object { [int]$_.resource.peak.threads } | Measure-Object -Maximum).Maximum)
        }
        cleanup = [ordered]@{
            allSamplesClean = $completed.Count -eq $MessageCount
            failedSamples = @($Samples | Where-Object { $_.cleanup -ne $true } | ForEach-Object sequence)
        }
        samples = @($Samples)
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\paired-cpp-net10-20260902-tcp451-retry-throughput"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory -notmatch '(?i)\\artifacts\\benchmarks\\paired-cpp-net10-[a-z0-9_-]+(?:\\[^\\]+)*$') {
    throw "OutputDirectory is outside the repository benchmark artifact boundary: $OutputDirectory"
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$cppFixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation cpp -RepositoryRoot $repoRoot
$net10Fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation net10 -RepositoryRoot $repoRoot
if ($cppFixture.sha256 -ne $net10Fixture.sha256) { throw "C++ and Net10 fixture manifest hashes differ." }
if ($null -eq $net10Fixture.executable) { throw "The fixture manifest must provide a Net10 executable for this paired runner." }

$testProject = Join-Path $repoRoot "hmailserver\source\Server.Net10\tests\HMailServer.Net10.Tests\HMailServer.Net10.Tests.csproj"
if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) { throw "Net10 test project is missing: $testProject" }
$localDotnet = Join-Path ((Get-Item $repoRoot).Parent.FullName) "tools\dotnet10\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet -PathType Leaf) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$cppScript = Join-Path $PSScriptRoot "test-disposable-cpp-tcp451-retry.ps1"
$runId = [Guid]::NewGuid().ToString("N")
$net10Samples = [System.Collections.Generic.List[object]]::new()
$cppSamples = [System.Collections.Generic.List[object]]::new()
$environmentNames = @(
    "HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC",
    "HMAILSERVER_NET10_LIVE_SQL_CONNECTION",
    "HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT",
    "HMAILSERVER_NET10_LIVE_SQL_DELIVERY_RECOVERY_REPORT"
)
$originalEnvironment = @{}
foreach ($name in $environmentNames) { $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process") }

try {
    for ($sequence = 1; $sequence -le $MessageCount; $sequence++) {
        $iterationDirectory = Join-Path $OutputDirectory ("iteration-{0:D2}" -f $sequence)
        New-Item -ItemType Directory -Force -Path $iterationDirectory | Out-Null
        $net10ReportPath = Join-Path $iterationDirectory "net10-tcp451-recovery.json"
        $net10LogPath = Join-Path $iterationDirectory "net10-test.log"
        $cppDirectory = Join-Path $iterationDirectory "cpp"
        $cppReportPath = Join-Path $cppDirectory "paired-cpp-net10-tcp451-recovery.json"
        $cppLogPath = Join-Path $iterationDirectory "cpp-test.log"
        $serviceName = "hMailPerfTcp451Throughput-$runId-$sequence"
        $port = $SinkPort + $sequence - 1

        [Environment]::SetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_DIAGNOSTIC", "1", "Process")
        [Environment]::SetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_CONNECTION", "Server=localhost;Database=$($net10Fixture.database);Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10", "Process")
        [Environment]::SetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT", $net10Fixture.dataRoot, "Process")
        [Environment]::SetEnvironmentVariable("HMAILSERVER_NET10_LIVE_SQL_DELIVERY_RECOVERY_REPORT", $net10ReportPath, "Process")

        $net10Child = Invoke-BenchmarkChild -FilePath $dotnet -Arguments @(
            "test", $testProject, "--configuration", "Release",
            "--filter", "FullyQualifiedName~DisposableDeliveryQueueRealTcp451Then250CompletesMessage",
            "--logger", "console;verbosity=minimal"
        ) -OutputPath $net10LogPath
        $net10Evidence = if (Test-Path -LiteralPath $net10ReportPath -PathType Leaf) { Get-Content -LiteralPath $net10ReportPath -Raw | ConvertFrom-Json } else { $null }
        $net10Cleanup = $null -ne $net10Evidence -and $net10Evidence.status -eq "PASS" -and $net10Evidence.finalState.QueuedCount -eq 0 -and $net10Evidence.finalState.RecipientCount -eq 0 -and $net10Evidence.messageFileAbsent -eq $true
        $net10Status = $net10Cleanup -and $net10Child.exitCode -eq 0 -and $net10Evidence.firstAttempt.smtpReply -eq 451 -and $net10Evidence.recoveryAttempt.smtpReply -eq 250
        $net10Samples.Add([pscustomobject]@{
            sequence = $sequence
            status = if ($net10Status) { "PASS" } else { "FAIL" }
            cleanup = $net10Cleanup
            durationMs = $net10Child.durationMs
            resource = [ordered]@{ before = $net10Child.before; peak = $net10Child.peak; after = $net10Child.after }
            evidencePath = $net10ReportPath
            error = if ($net10Status) { $null } elseif ($null -eq $net10Evidence) { "Net10 recovery evidence was not emitted." } else { "Net10 recovery or cleanup assertions failed." }
        })

        $cppChild = Invoke-BenchmarkChild -FilePath "powershell.exe" -Arguments @(
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $cppScript,
            "-FixtureManifest", $FixtureManifest,
            "-Net10EvidencePath", $net10ReportPath,
            "-OutputDirectory", $cppDirectory,
            "-ServiceName", $serviceName,
            "-SinkPort", $port,
            "-TimeoutSeconds", $TimeoutSeconds,
            "-Recovery"
        ) -ServiceName $serviceName -OutputPath $cppLogPath
        $cppEvidence = if (Test-Path -LiteralPath $cppReportPath -PathType Leaf) { Get-Content -LiteralPath $cppReportPath -Raw | ConvertFrom-Json } else { $null }
        $cppCleanup = $null -ne $cppEvidence -and $cppEvidence.status -eq "PASS" -and $cppEvidence.cleanup.serviceAbsent -eq $true -and $cppEvidence.cleanup.routeAbsent -eq $true -and $cppEvidence.cleanup.messageAbsent -eq $true -and $cppEvidence.cleanup.recipientAbsent -eq $true -and $cppEvidence.cleanup.dataFileAbsent -eq $true
        $cppStatus = $cppCleanup -and $cppChild.exitCode -eq 0 -and $cppEvidence.cpp.evidence.initial.sink.saw451 -eq $true -and $cppEvidence.cpp.evidence.sink.sawRecovery -eq $true
        $cppSamples.Add([pscustomobject]@{
            sequence = $sequence
            status = if ($cppStatus) { "PASS" } else { "FAIL" }
            cleanup = $cppCleanup
            durationMs = $cppChild.durationMs
            resource = [ordered]@{ before = $cppChild.before; peak = $cppChild.peak; after = $cppChild.after }
            evidencePath = $cppReportPath
            error = if ($cppStatus) { $null } elseif ($null -eq $cppEvidence) { "C++ recovery evidence was not emitted." } else { "C++ recovery or cleanup assertions failed." }
        })
    }
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], "Process") }
}

$cppResult = Get-AggregateResult -Implementation cpp -Samples $cppSamples.ToArray()
$net10Result = Get-AggregateResult -Implementation net10 -Samples $net10Samples.ToArray()
$status = if ($cppResult.status -eq "PASS" -and $net10Result.status -eq "PASS") { "PASS" } else { "FAIL" }
$report = [ordered]@{
    schema = "paired-cpp-net10-tcp451-retry-throughput-v1"
    status = $status
    decision = "NO_WINNER: bounded retry/defer evidence is descriptive and does not establish a performance winner."
    generatedUtc = [DateTimeOffset]::UtcNow.ToString("o")
    runId = $runId
    messageCount = $MessageCount
    fixture = [ordered]@{
        manifest = $cppFixture.path
        manifestSha256 = $cppFixture.sha256
        fixtureId = $cppFixture.fixtureId
        cppDatabase = $cppFixture.database
        cppDataRoot = $cppFixture.dataRoot
        cppExecutableSha256 = $cppFixture.expectedExecutableSha256
        net10Database = $net10Fixture.database
        net10DataRoot = $net10Fixture.dataRoot
        net10ExecutableSha256 = $net10Fixture.expectedExecutableSha256
    }
    sink = [ordered]@{ host = "127.0.0.1"; firstReply = 451; recoveryReply = 250; deterministic = $true }
    results = [ordered]@{ cpp = $cppResult; net10 = $net10Result }
    productionSafety = "test-only; loopback sink; disposable SQL/Data roots; no production service, DB, Data, COM, registry, protocol, delivery, or schema changes"
}
$jsonPath = Join-Path $OutputDirectory "paired-cpp-net10-tcp451-retry-throughput.json"
$csvPath = Join-Path $OutputDirectory "paired-cpp-net10-tcp451-retry-throughput.csv"
$mdPath = Join-Path $OutputDirectory "paired-cpp-net10-tcp451-retry-throughput.md"
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
@($cppResult, $net10Result) | ForEach-Object {
    [pscustomobject]@{
        implementation = $_.implementation
        status = $_.status
        requested_messages = $_.requestedMessages
        completed_messages = $_.completedMessages
        errors = $_.errors
        total_ms = $_.total_ms
        p50_ms = $_.p50_ms
        p95_ms = $_.p95_ms
        p99_ms = $_.p99_ms
        throughput_messages_per_second = $_.throughput_messages_per_second
        cleanup_all_samples_clean = $_.cleanup.allSamplesClean
        peak_private_bytes = $_.resource.peakPrivateBytes
        peak_handles = $_.resource.peakHandles
        peak_threads = $_.resource.peakThreads
    }
} | Export-Csv -LiteralPath $csvPath -NoTypeInformation
@(
    "# Paired C++ / .NET 10 TCP 451 retry/defer throughput",
    "",
    "Status: $($report.status)",
    "Decision: $($report.decision)",
    "Messages per implementation: $($report.messageCount)",
    "Fixture manifest SHA-256: $($report.fixture.manifestSha256)",
    "Sink: $($report.sink.host), first reply $($report.sink.firstReply), recovery reply $($report.sink.recoveryReply)",
    "",
    "| Implementation | Status | p50 ms | p95 ms | p99 ms | Throughput/s | Cleanup |",
    "| --- | --- | ---: | ---: | ---: | ---: | --- |",
    "| C++ | $($cppResult.status) | $($cppResult.p50_ms) | $($cppResult.p95_ms) | $($cppResult.p99_ms) | $($cppResult.throughput_messages_per_second) | $($cppResult.cleanup.allSamplesClean) |",
    "| .NET 10 | $($net10Result.status) | $($net10Result.p50_ms) | $($net10Result.p95_ms) | $($net10Result.p99_ms) | $($net10Result.throughput_messages_per_second) | $($net10Result.cleanup.allSamplesClean) |",
    "",
    "Resource evidence is recorded per child and aggregated as peak private bytes, handles, and threads.",
    "Each sample reads retained retry state after 451, verifies 250 recovery, and requires final message, recipient, Data, route, and temporary-service cleanup.",
    "No winner claim is made; this is bounded disposable evidence only.",
    "",
    "JSON: $jsonPath"
) | Set-Content -LiteralPath $mdPath -Encoding UTF8
if ($report.status -ne "PASS") { throw "Paired TCP 451 retry/defer throughput failed. See $jsonPath" }
Write-Output ($report | ConvertTo-Json -Depth 20)
