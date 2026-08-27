param(
    [ValidateRange(1, 300)]
    [int]$Iterations = 25,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$OutputDirectory = "",
    [string]$BenchmarkStagingRoot = "",
    [string]$BenchmarkDatabase = "",
    [string]$BenchmarkServiceExecutable = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")
$serviceExe = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe"
$stagingRoot = "C:\hmail-perf-net10-ascii-20260810"
$database = "hmail_perf_net_sql_20260810_152708"

if (-not [string]::IsNullOrWhiteSpace($BenchmarkStagingRoot)) { $stagingRoot = [IO.Path]::GetFullPath($BenchmarkStagingRoot) }
if (-not [string]::IsNullOrWhiteSpace($BenchmarkDatabase)) { $database = $BenchmarkDatabase }
if (-not [string]::IsNullOrWhiteSpace($BenchmarkServiceExecutable)) {
    Assert-ApprovedBenchmarkExecutable -Path $BenchmarkServiceExecutable -Implementation net10 -RepositoryRoot $repoRoot
    $serviceExe = [IO.Path]::GetFullPath($BenchmarkServiceExecutable)
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\net10-pop3-acceptance"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) { throw "Live listener host is missing: $serviceExe" }
$dataRoot = Join-Path $stagingRoot "Data"
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) { throw "Disposable Data root is missing: $dataRoot" }
if ($database -notmatch '^hmail_perf_[a-z0-9_]+$') { throw "Refusing non-disposable benchmark database: $database" }
if ([IO.Path]::GetFullPath($stagingRoot) -notmatch '(?i)^C:\\hmail-perf-') { throw "Refusing non-disposable benchmark Data root: $stagingRoot" }

function Get-ListenerState {
    @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort 25110 -ErrorAction SilentlyContinue)
}

function Read-Pop3Line {
    param([IO.StreamReader]$Reader)
    $line = $Reader.ReadLine()
    if ($null -eq $line) { throw "POP3 server closed the connection before a response line." }
    return $line
}

function Invoke-Pop3Scenario {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $client = $null
    $reader = $null
    $writer = $null
    $stage = "connect"
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 25110)
        $client.ReceiveTimeout = 5000
        $client.SendTimeout = 5000
        $stream = $client.GetStream()
        $reader = [IO.StreamReader]::new($stream)
        $writer = [IO.StreamWriter]::new($stream)
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true

        $stage = "greeting"
        $greeting = Read-Pop3Line $reader
        $stage = "USER"
        $writer.WriteLine("USER test@perf.test")
        $user = Read-Pop3Line $reader
        $stage = "PASS"
        $writer.WriteLine("PASS test")
        $password = Read-Pop3Line $reader
        $stage = "STAT"
        $writer.WriteLine("STAT")
        $stat = Read-Pop3Line $reader
        $stage = "LIST"
        $writer.WriteLine("LIST")
        $list = Read-Pop3Line $reader
        if ($list -notlike "+OK*") { throw "POP3 LIST failed: [$list]" }
        while ($true) {
            $line = Read-Pop3Line $reader
            if ($line -eq ".") { break }
        }
        $stage = "QUIT"
        $writer.WriteLine("QUIT")
        $quit = Read-Pop3Line $reader
        $ok = $greeting -like "+OK*" -and $user -like "+OK*" -and $password -like "+OK*" -and $stat -like "+OK*" -and $quit -like "+OK*"
        $stopwatch.Stop()
        [pscustomobject]@{ ok = [bool]$ok; ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3); error = if ($ok) { $null } else { "Unexpected POP3 response sequence." } }
    }
    catch {
        $stopwatch.Stop()
        [pscustomobject]@{ ok = $false; ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3); error = "${stage}: $($_.Exception.Message)" }
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        if ($null -ne $writer) { $writer.Dispose() }
        if ($null -ne $client) { $client.Dispose() }
    }
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percent)
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    $rank = ($Percent / 100) * ($sorted.Count - 1)
    $lower = [math]::Floor($rank)
    $upper = [math]::Ceiling($rank)
    if ($lower -eq $upper) { return [math]::Round([double]$sorted[$lower], 3) }
    return [math]::Round(([double]$sorted[$lower] + (([double]$sorted[$upper] - [double]$sorted[$lower]) * ($rank - $lower))), 3)
}

$env:HMAILSERVER_SQLSERVER_CONNECTION = "Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True;"
$env:HMAILSERVER_DATA_DIRECTORY = $dataRoot
$env:HMAILSERVER_INITIALIZATION_FILE = Join-Path $stagingRoot "hMailServer.ini"
$env:HMAILSERVER_SMTP_ENABLED = "false"
$env:HMAILSERVER_IMAP_ENABLED = "false"
$env:HMAILSERVER_POP3_ENABLED = "true"
$env:HMAILSERVER_POP3_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_POP3_PORT = "25110"
$env:HMAILSERVER_EXTERNAL_FETCH_ENABLED = "false"
$env:HMAILSERVER_COM_LOCAL_SERVER_ENABLED = "false"

$process = $null
$startUtc = [DateTimeOffset]::UtcNow
$samples = [System.Collections.Generic.List[object]]::new()
$readinessFailures = [System.Collections.Generic.List[string]]::new()
$shutdownFailures = [System.Collections.Generic.List[string]]::new()
try {
    $process = Start-Process -FilePath $serviceExe -ArgumentList "90" -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    do {
        if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { $readinessFailures.Add("Live listener host exited before POP3 readiness."); break }
        $listeners = @(Get-ListenerState)
        if ($listeners.Count -gt 0) {
            try {
                $probe = [Net.Sockets.TcpClient]::new("127.0.0.1", 25110)
                $probe.ReceiveTimeout = 3000
                $probeReader = [IO.StreamReader]::new($probe.GetStream())
                $banner = $probeReader.ReadLine()
                $probeReader.Dispose(); $probe.Dispose()
                if ($banner -like "+OK*") { break }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ($readinessFailures.Count -eq 0 -and $listeners.Count -eq 0) { $readinessFailures.Add("POP3 listener is not listening on 127.0.0.1:25110.") }
    if ($readinessFailures.Count -eq 0) {
        Start-Sleep -Milliseconds 500
        for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
            $result = Invoke-Pop3Scenario
            $samples.Add([pscustomobject]@{ scenario = "pop3"; iteration = $iteration; ok = $result.ok; ms = $result.ms; error = $result.error })
        }
    }
}
finally {
    if ($null -ne $process -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        try { Stop-Process -Id $process.Id -Force } catch { $shutdownFailures.Add($_.Exception.Message) }
    }
}
$endUtc = [DateTimeOffset]::UtcNow
$successful = @($samples | Where-Object ok)
$latencies = @($successful | ForEach-Object ms)
$report = [pscustomobject]@{
    schema = "live-pop3-acceptance-v1"
    implementation = "net10"
    status = if ($readinessFailures.Count -eq 0 -and $shutdownFailures.Count -eq 0 -and $successful.Count -eq $Iterations) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $database
    dataRoot = $dataRoot
    bind = "127.0.0.1"
    port = 25110
    iterations = $Iterations
    successes = $successful.Count
    errors = $samples.Count - $successful.Count
    p50_ms = Get-Percentile $latencies 50
    p95_ms = Get-Percentile $latencies 95
    p99_ms = Get-Percentile $latencies 99
    throughput_sessions_per_second = if ($successful.Count -gt 0) { [math]::Round($successful.Count / (($endUtc - $startUtc).TotalSeconds), 3) } else { 0 }
    readinessFailures = @($readinessFailures)
    shutdownFailures = @($shutdownFailures)
    samples = $samples
    productionSafety = "loopback-only; disposable SQL/Data roots required; production service/DB/Data are not used"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "net10-live-pop3.json"
$csvPath = Join-Path $OutputDirectory "net10-live-pop3.csv"
$markdownPath = Join-Path $OutputDirectory "net10-live-pop3.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
@(
    "# .NET 10 live POP3 acceptance",
    "",
    "Status: $($report.status)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Endpoint: $($report.bind):$($report.port)",
    "Iterations: $($report.iterations); successes: $($report.successes); errors: $($report.errors)",
    "p50/p95/p99: $($report.p50_ms) / $($report.p95_ms) / $($report.p99_ms) ms",
    "Throughput: $($report.throughput_sessions_per_second) sessions/s",
    "",
    "This is Net10-only evidence. No C++ ratio or winner is valid until the same isolated fixture and scenario run on the legacy server."
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "status=$($report.status); successes=$($report.successes); errors=$($report.errors); p50=$($report.p50_ms); p95=$($report.p95_ms); p99=$($report.p99_ms)"
Write-Output "JSON: $jsonPath"
