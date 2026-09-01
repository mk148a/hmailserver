param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureManifest,
    [ValidateSet("Admission", "AuthSelect", "Search", "Sort", "Full")]
    [string]$Profile = "Full",
    [ValidateRange(1, 5000)]
    [int]$Concurrency = 500,
    [ValidateRange(1, 30000)]
    [int]$TimeoutMilliseconds = 5000,
    [ValidateRange(0, 300)]
    [int]$WarmupSeconds = 5,
    [ValidateRange(0, 1000)]
    [int]$LaunchStaggerMilliseconds = 0,
    [ValidateRange(50, 5000)]
    [int]$SampleIntervalMilliseconds = 100,
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

function Assert-DisposableMonitorInput {
    param([object]$Fixture)

    foreach ($path in @([string]$Fixture.executable, [string]$Fixture.dataRoot)) {
        $fullPath = [IO.Path]::GetFullPath($path)
        if ($fullPath -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+(?:\\cpp)?(?:\\Bin\\hMailServer\.exe|\\Data)$') {
            throw "Fixture path is outside the approved disposable C++ roots: $fullPath"
        }
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "Fixture path does not exist: $fullPath"
        }
    }
    if ([string]$Fixture.database -notmatch '^hmail_perf_pair_cpp_[a-z0-9_]+$') {
        throw "Fixture database is not disposable: $($Fixture.database)"
    }
}

function Get-ProductionService {
    Get-CimInstance Win32_Service -Filter "Name='hMailServer'" -ErrorAction SilentlyContinue
}

function Get-WorkerSnapshot {
    param([string]$Executable)

    $fullExecutable = [IO.Path]::GetFullPath($Executable)
    $workers = @(Get-CimInstance Win32_Process -Filter "Name='hMailServer.exe'" -ErrorAction SilentlyContinue |
        Where-Object { [string]::Equals([IO.Path]::GetFullPath([string]$_.ExecutablePath), $fullExecutable, [StringComparison]::OrdinalIgnoreCase) })
    if ($workers.Count -eq 0) {
        return [pscustomobject]@{ pids = @(); privateBytes = $null; handles = $null; threads = $null }
    }

    $processes = @($workers | ForEach-Object { Get-Process -Id ([int]$_.ProcessId) -ErrorAction SilentlyContinue } | Where-Object { $null -ne $_ })
    [pscustomobject]@{
        pids = @($workers | ForEach-Object { [int]$_.ProcessId })
        privateBytes = if ($processes.Count -eq 0) { $null } else { [long](($processes | Measure-Object PrivateMemorySize64 -Sum).Sum) }
        handles = if ($processes.Count -eq 0) { $null } else { [int](($processes | Measure-Object Handles -Sum).Sum) }
        threads = if ($processes.Count -eq 0) { $null } else { [int](($processes | ForEach-Object { $_.Threads.Count } | Measure-Object -Sum).Sum) }
    }
}

function Get-TcpSnapshot {
    $connections = @(Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort 1143 -ErrorAction SilentlyContinue)
    [pscustomobject]@{
        listener = @($connections | Where-Object State -eq Listen).Count
        established = @($connections | Where-Object State -eq Established).Count
        synReceived = @($connections | Where-Object State -eq SynReceived).Count
        closeWait = @($connections | Where-Object State -eq CloseWait).Count
        total = $connections.Count
    }
}

function Get-SqlSnapshot {
    param([string]$Database)

    $query = @"
SET NOCOUNT ON;
SELECT CONCAT(
    COALESCE(CONVERT(varchar(20), (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id = DB_ID(N'$Database'))), '0'), '|',
    COALESCE(CONVERT(varchar(20), (SELECT COUNT(*) FROM sys.dm_os_waiting_tasks wt INNER JOIN sys.dm_exec_requests r ON r.session_id = wt.session_id WHERE r.database_id = DB_ID(N'$Database'))), '0'), '|',
    COALESCE((SELECT TOP 1 COALESCE(wait_type, '') FROM sys.dm_os_waiting_tasks ORDER BY wait_duration_ms DESC), '')
);
"@
    $value = (@(& sqlcmd.exe -S localhost -E -b -d master -h-1 -W -Q $query 2>&1) -join " ").Trim()
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^(\d+)\|(\d+)\|(.*)$') {
        return [pscustomobject]@{ available = $false; activeRequests = $null; waitingTasks = $null; longestWaitType = $null; error = $value }
    }
    [pscustomobject]@{
        available = $true
        activeRequests = [int]$Matches[1]
        waitingTasks = [int]$Matches[2]
        longestWaitType = $Matches[3]
        error = $null
    }
}

function Write-MonitorArtifacts {
    param(
        [object]$Report,
        [string]$Directory
    )

    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $jsonPath = Join-Path $Directory "cpp-imap-capacity-monitor.json"
    $csvPath = Join-Path $Directory "cpp-imap-capacity-monitor.csv"
    $markdownPath = Join-Path $Directory "cpp-imap-capacity-monitor.md"
    $Report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
    $Report.samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
    $last = @($Report.samples | Select-Object -Last 1)
    @(
        "# Disposable C++ IMAP Capacity Monitor",
        "",
        "- Status: **$($Report.status)**",
        "- Fixture: $($Report.fixtureId) ($($Report.fixtureManifestSha256))",
        "- Database: $($Report.database)",
        "- Profile/concurrency: $($Report.profile) / $($Report.concurrency)",
        "- Sample interval: $($Report.sampleIntervalMilliseconds) ms",
        "- Launch stagger: $($Report.launchStaggerMilliseconds) ms",
        "- Samples: $($Report.samples.Count)",
        "- Wrapper exit code: $($Report.wrapperExitCode)",
        "- Workload report: $($Report.workloadReportPath)",
        "- Production service untouched: $($Report.productionServiceUntouched)",
        "",
        "| Metric | First | Peak | Last |",
        "| --- | ---: | ---: | ---: |",
        "| Worker private bytes | $($Report.summary.privateBytesFirst) | $($Report.summary.privateBytesPeak) | $($Report.summary.privateBytesLast) |",
        "| Worker handles | $($Report.summary.handlesFirst) | $($Report.summary.handlesPeak) | $($Report.summary.handlesLast) |",
        "| Worker threads | $($Report.summary.threadsFirst) | $($Report.summary.threadsPeak) | $($Report.summary.threadsLast) |",
        "| Established TCP 1143 | $($Report.summary.establishedFirst) | $($Report.summary.establishedPeak) | $($Report.summary.establishedLast) |",
        "| Active SQL requests | $($Report.summary.activeRequestsFirst) | $($Report.summary.activeRequestsPeak) | $($Report.summary.activeRequestsLast) |",
        "",
        "This is diagnostic evidence only. It does not change the legacy C++ server or establish a release-gate PASS."
    ) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
    [pscustomobject]@{ json = $jsonPath; csv = $csvPath; markdown = $markdownPath }
}

$fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation cpp -RepositoryRoot $repoRoot
Assert-DisposableMonitorInput $fixture
$productionService = Get-ProductionService
if ($null -ne $productionService -and [string]$productionService.State -ne "Stopped") {
    throw "Refusing monitor run while production-named hMailServer service is not stopped."
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\disposable-cpp-capacity-monitor"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory -notmatch '(?i)\\artifacts\\benchmarks(?:\\[^\\]+)*$') {
    throw "Output directory is outside the repository benchmark artifact boundary: $OutputDirectory"
}

$runDirectory = Join-Path $OutputDirectory ("{0}-{1}" -f $Concurrency, (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss"))
$wrapperOutput = Join-Path $repoRoot ("artifacts\benchmarks\paired-cpp-net10-capacity-monitor-{0}-{1}" -f $Concurrency, (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss"))
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
$wrapperLog = Join-Path $runDirectory "wrapper.stdout.log"
$wrapperError = Join-Path $runDirectory "wrapper.stderr.log"
$wrapperArguments = @(
    "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "benchmark-disposable-cpp-service-protocol.ps1"),
    "-FixtureManifest", [IO.Path]::GetFullPath($FixtureManifest),
    "-OutputDirectory", $wrapperOutput,
    "-Workload", "concurrent-imap", "-Profile", $Profile,
    "-Concurrency", $Concurrency, "-Waves", 1,
        "-TimeoutMilliseconds", $TimeoutMilliseconds, "-WarmupSeconds", $WarmupSeconds,
    "-PostWorkloadSettleSeconds", 1, "-LaunchStaggerMilliseconds", $LaunchStaggerMilliseconds,
    "-CorpusMessageCount", [int]$fixture.expectedMessageFingerprint.rowCount,
    "-ServiceName", ("hMailServerPerfMonitor{0}" -f (Get-Random -Minimum 10000 -Maximum 99999))
)
$process = Start-Process -FilePath "powershell.exe" -ArgumentList $wrapperArguments -WorkingDirectory $repoRoot -RedirectStandardOutput $wrapperLog -RedirectStandardError $wrapperError -PassThru -WindowStyle Hidden
$samples = [System.Collections.Generic.List[object]]::new()
$startUtc = [DateTimeOffset]::UtcNow
try {
    do {
        $worker = Get-WorkerSnapshot ([string]$fixture.executable)
        $tcp = Get-TcpSnapshot
        $sql = Get-SqlSnapshot ([string]$fixture.database)
        $samples.Add([pscustomobject]@{
            observedUtc = [DateTimeOffset]::UtcNow.ToString("o")
            workerPids = ($worker.pids -join ",")
            privateBytes = $worker.privateBytes
            handles = $worker.handles
            threads = $worker.threads
            tcpListener = $tcp.listener
            tcpEstablished = $tcp.established
            tcpSynReceived = $tcp.synReceived
            tcpCloseWait = $tcp.closeWait
            tcpTotal = $tcp.total
            sqlAvailable = $sql.available
            sqlActiveRequests = $sql.activeRequests
            sqlWaitingTasks = $sql.waitingTasks
            sqlLongestWaitType = $sql.longestWaitType
            sqlError = $sql.error
        })
        if (-not $process.HasExited) { Start-Sleep -Milliseconds $SampleIntervalMilliseconds }
    } while (-not $process.HasExited)
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
$process.WaitForExit()
$process.Refresh()
$endUtc = [DateTimeOffset]::UtcNow
$workloadJson = Join-Path $wrapperOutput "disposable-cpp-service-concurrent-imap.json"
$childReport = $null
if (Test-Path -LiteralPath $workloadJson -PathType Leaf) {
    $childReport = Get-Content -LiteralPath $workloadJson -Raw | ConvertFrom-Json
}
$numeric = @($samples | Where-Object { $null -ne $_.privateBytes })
function Get-Metric { param([object[]]$Rows, [string]$Name, [string]$Mode)
    if ($Rows.Count -eq 0) { return $null }
    $values = @($Rows | ForEach-Object { $_.$Name } | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ })
    if ($values.Count -eq 0) { return $null }
    if ($Mode -eq "first") { return $values[0] }
    if ($Mode -eq "last") { return $values[$values.Count - 1] }
    return [math]::Round(($values | Measure-Object -Maximum).Maximum, 3)
}
$summary = [ordered]@{}
foreach ($metric in @("privateBytes", "handles", "threads", "tcpEstablished", "sqlActiveRequests")) {
    $summary["${metric}First"] = Get-Metric $numeric $metric "first"
    $summary["${metric}Peak"] = Get-Metric $numeric $metric "peak"
    $summary["${metric}Last"] = Get-Metric $numeric $metric "last"
}
$report = [ordered]@{
    schema = "disposable-cpp-imap-capacity-monitor-v1"
    status = if ($null -ne $childReport -and $childReport.status -eq "PASS") { "WORKLOAD_PASS" } else { "WORKLOAD_FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    fixtureId = [string]$fixture.fixtureId
    fixtureManifestSha256 = (Get-FileHash -LiteralPath $FixtureManifest -Algorithm SHA256).Hash.ToUpperInvariant()
    database = [string]$fixture.database
    executable = [string]$fixture.executable
    profile = $Profile
    concurrency = $Concurrency
    timeoutMilliseconds = $TimeoutMilliseconds
    warmupSeconds = $WarmupSeconds
    launchStaggerMilliseconds = $LaunchStaggerMilliseconds
    sampleIntervalMilliseconds = $SampleIntervalMilliseconds
    wrapperExitCode = $process.ExitCode
    wrapperOutputDirectory = [IO.Path]::GetFullPath($wrapperOutput)
    workloadReportPath = if (Test-Path -LiteralPath $workloadJson) { [IO.Path]::GetFullPath($workloadJson) } else { $null }
    childStatus = if ($null -eq $childReport) { $null } else { [string]$childReport.status }
    productionServiceUntouched = $true
    summary = [pscustomobject]$summary
    samples = @($samples)
}
$paths = Write-MonitorArtifacts ([pscustomobject]$report) $runDirectory
$report.artifacts = $paths
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $paths.json -Encoding UTF8
Write-Output ($report | ConvertTo-Json -Depth 12)
