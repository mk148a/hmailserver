param(
    [ValidateRange(2, 5)]
    [int]$Cycles = 2,
    [ValidateRange(10, 180)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$OutputDirectory = "",
    [string]$BenchmarkStagingRoot = "C:\hmail-perf-pair-20260811_1748\net10",
    [string]$BenchmarkDatabase = "hmail_perf_pair_net10_20260811_1748",
    [string]$BenchmarkServiceExecutable = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")
$serviceExe = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe"
if (-not [string]::IsNullOrWhiteSpace($BenchmarkServiceExecutable)) {
    Assert-ApprovedBenchmarkExecutable -Path $BenchmarkServiceExecutable -Implementation net10 -RepositoryRoot $repoRoot
    $serviceExe = [IO.Path]::GetFullPath($BenchmarkServiceExecutable)
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\net10-restart-lifecycle"
}
$dataRoot = Join-Path $BenchmarkStagingRoot "Data"

if ($BenchmarkDatabase -notmatch '^hmail_perf_[a-z0-9_]+$') { throw "Refusing non-disposable benchmark database: $BenchmarkDatabase" }
if ([IO.Path]::GetFullPath($BenchmarkStagingRoot) -notmatch '(?i)^C:\\hmail-perf-') { throw "Refusing non-disposable benchmark root: $BenchmarkStagingRoot" }
if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) { throw "Live listener host is missing: $serviceExe" }
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) { throw "Disposable Data root is missing: $dataRoot" }

$listeners = @(
    [pscustomobject]@{ protocol = "smtp"; port = 2525; prefix = "220" },
    [pscustomobject]@{ protocol = "imap"; port = 1143; prefix = "* OK" },
    [pscustomobject]@{ protocol = "pop3"; port = 25110; prefix = "+OK" }
)

function Get-PortOwners {
    param([int]$Port)
    @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort $Port -ErrorAction SilentlyContinue | ForEach-Object { [int]$_.OwningProcess })
}

function Test-Banners {
    param([int]$ProcessId)
    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($listener in $listeners) {
        $owners = @(Get-PortOwners $listener.port)
        if ($owners -notcontains $ProcessId) {
            $failures.Add("$($listener.protocol) port $($listener.port) is not owned by PID $($ProcessId): $($owners -join ',')")
            continue
        }
        $client = $null
        $reader = $null
        try {
            $client = [Net.Sockets.TcpClient]::new("127.0.0.1", $listener.port)
            $client.ReceiveTimeout = 3000
            $reader = [IO.StreamReader]::new($client.GetStream())
            $banner = $reader.ReadLine()
            if ($listener.protocol -eq "imap") {
                $valid = $banner -like "* OK*"
            }
            else {
                $valid = $banner -like "$($listener.prefix)*"
            }
            if (-not $valid) { $failures.Add("Unexpected $($listener.protocol) banner: [$banner]") }
        }
        catch { $failures.Add("$($listener.protocol) banner probe failed: $($_.Exception.Message)") }
        finally {
            if ($null -ne $reader) { $reader.Dispose() }
            if ($null -ne $client) { $client.Dispose() }
        }
    }
    return $failures.ToArray()
}

function Wait-Ready {
    param([int]$ProcessId)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    $last = @()
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            return @("Launched process $ProcessId exited before readiness.")
        }
        $last = @(Test-Banners $ProcessId)
        if ($last.Count -eq 0) { return @() }
        Start-Sleep -Milliseconds 250
    }
    return $last
}

function Stop-AndVerify {
    param([System.Diagnostics.Process]$Process)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $failures = [System.Collections.Generic.List[string]]::new()
    try {
        if (-not $Process.HasExited) { Stop-Process -Id $Process.Id -Force }
        $Process.WaitForExit(10000)
        if (-not $Process.HasExited) { $failures.Add("PID $($Process.Id) did not exit.") }
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            $remaining = @($listeners | ForEach-Object { Get-PortOwners $_.port } | Where-Object { $_ -eq $Process.Id })
            if ($remaining.Count -eq 0) { break }
            Start-Sleep -Milliseconds 100
        }
        foreach ($listener in $listeners) {
            if ((Get-PortOwners $listener.port) -contains $Process.Id) {
                $failures.Add("PID $($Process.Id) still owns $($listener.protocol) port $($listener.port).")
            }
        }
    }
    catch { $failures.Add($_.Exception.Message) }
    finally { $watch.Stop() }
    [pscustomobject]@{ ok = ($failures.Count -eq 0); ms = [math]::Round($watch.Elapsed.TotalMilliseconds, 3); failures = $failures.ToArray() }
}

$env:HMAILSERVER_SQLSERVER_CONNECTION = "Server=localhost;Database=$BenchmarkDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$env:HMAILSERVER_DATA_DIRECTORY = $dataRoot
$env:HMAILSERVER_INITIALIZATION_FILE = Join-Path $BenchmarkStagingRoot "hMailServer.ini"
$env:HMAILSERVER_SMTP_ENABLED = "true"
$env:HMAILSERVER_SMTP_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_SMTP_PORT = "2525"
$env:HMAILSERVER_IMAP_ENABLED = "true"
$env:HMAILSERVER_IMAP_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_IMAP_PORT = "1143"
$env:HMAILSERVER_POP3_ENABLED = "true"
$env:HMAILSERVER_POP3_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_POP3_PORT = "25110"
$env:HMAILSERVER_EXTERNAL_FETCH_ENABLED = "false"
$env:HMAILSERVER_COM_LOCAL_SERVER_ENABLED = "false"

$samples = [System.Collections.Generic.List[object]]::new()
$allFailures = [System.Collections.Generic.List[string]]::new()
$startUtc = [DateTimeOffset]::UtcNow
for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
    $startWatch = [Diagnostics.Stopwatch]::StartNew()
    $process = $null
    $readiness = @()
    $stop = $null
    try {
        $process = Start-Process -FilePath $serviceExe -ArgumentList "90" -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
        $readiness = @(Wait-Ready $process.Id)
        $startWatch.Stop()
        if ($readiness.Count -eq 0) {
            $stop = Stop-AndVerify $process
        }
        else {
            $stop = Stop-AndVerify $process
        }
    }
    catch {
        $startWatch.Stop()
        $readiness = @($_.Exception.Message)
        if ($null -ne $process) { try { Stop-Process -Id $process.Id -Force } catch { } }
        $stop = [pscustomobject]@{ ok = $false; ms = 0; failures = @("cleanup after cycle failure") }
    }
    $ok = $readiness.Count -eq 0 -and $stop.ok
    if (-not $ok) { $allFailures.Add("cycle $cycle readiness: $($readiness -join '; '); stop: $($stop.failures -join '; ')") }
    $samples.Add([pscustomobject]@{ cycle = $cycle; pid = if ($null -ne $process) { $process.Id } else { 0 }; ok = $ok; start_ready_ms = [math]::Round($startWatch.Elapsed.TotalMilliseconds, 3); stop_ms = $stop.ms; readiness_failures = [string[]]@($readiness | Where-Object { $null -ne $_ }); shutdown_failures = [string[]]@($stop.failures | Where-Object { $null -ne $_ }) })
}
$endUtc = [DateTimeOffset]::UtcNow
$report = [pscustomobject]@{
    schema = "live-net10-restart-lifecycle-v1"
    implementation = "net10"
    status = if ($allFailures.Count -eq 0 -and @($samples | Where-Object ok).Count -eq $Cycles) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $BenchmarkDatabase
    dataRoot = $dataRoot
    loopbackPorts = "127.0.0.1:2525,1143,25110"
    cycles = $Cycles
    successes = @($samples | Where-Object ok).Count
    errors = $allFailures.Count
    start_ready_p50_ms = [math]::Round((@($samples.start_ready_ms | Sort-Object)[[math]::Floor(($samples.Count - 1) * 0.5)]), 3)
    stop_p50_ms = [math]::Round((@($samples.stop_ms | Sort-Object)[[math]::Floor(($samples.Count - 1) * 0.5)]), 3)
    failures = $allFailures.ToArray()
    samples = $samples
    comLocalServerEnabled = $false
    productionSafety = "only disposable LiveListenerHost PID and loopback ports were used; no Windows service, registry, COM registration, or DCOM ACL was changed"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "net10-restart-lifecycle.json"
$csvPath = Join-Path $OutputDirectory "net10-restart-lifecycle.csv"
$markdownPath = Join-Path $OutputDirectory "net10-restart-lifecycle.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
@(
    "# .NET 10 disposable restart lifecycle",
    "",
    "Status: $($report.status)",
    "Cycles: $($report.successes)/$($report.cycles)",
    "Loopback ports: $($report.loopbackPorts)",
    "Start-ready p50 / stop p50: $($report.start_ready_p50_ms) / $($report.stop_p50_ms) ms",
    "",
    "COM local server was disabled; this evidence does not register or authorize COM and does not prove Windows service or out-of-process COM lifecycle."
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "status=$($report.status); cycles=$($report.successes)/$($report.cycles); start_ready_p50=$($report.start_ready_p50_ms); stop_p50=$($report.stop_p50_ms)"
Write-Output "JSON: $jsonPath"
