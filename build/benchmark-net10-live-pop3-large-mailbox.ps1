param(
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10",
    [ValidateRange(1, 25)]
    [int]$Iterations = 5,
    [ValidateRange(1, 100000)]
    [int]$ExpectedMessages = 1000,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$OutputDirectory = "",
    [string]$BenchmarkStagingRoot = "C:\hmail-perf-pair-20260811_1748\net10",
    [string]$BenchmarkDatabase = "hmail_perf_pair_net10_20260811_1748",
    [string]$BenchmarkServiceExecutable = "",
    [string]$FixtureManifest = "",
    [string]$RunId = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")
$provenanceScript = Join-Path $PSScriptRoot "live-benchmark-provenance.ps1"
if (Test-Path -LiteralPath $provenanceScript -PathType Leaf) { . $provenanceScript }
$serviceExe = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe"
$argumentList = "90"
if ($Implementation -eq "cpp") {
    $serviceExe = "C:\hmail-perf-cpp-ascii-20260810\Bin\hMailServer.exe"
    $BenchmarkStagingRoot = "C:\hmail-perf-cpp-ascii-20260810"
    $BenchmarkDatabase = "hmail_perf_cpp_sql_20260810_152708"
    $argumentList = "/Debug"
}
if (-not [string]::IsNullOrWhiteSpace($BenchmarkServiceExecutable)) {
    Assert-ApprovedBenchmarkExecutable -Path $BenchmarkServiceExecutable -Implementation $Implementation -RepositoryRoot $repoRoot
    $serviceExe = [IO.Path]::GetFullPath($BenchmarkServiceExecutable)
}
if (-not [string]::IsNullOrWhiteSpace($FixtureManifest)) {
    $fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation $Implementation -RepositoryRoot $repoRoot
    $BenchmarkDatabase = $fixture.database
    $BenchmarkStagingRoot = Split-Path -Parent $fixture.dataRoot
    if ($null -ne $fixture.executable) { $serviceExe = [IO.Path]::GetFullPath($fixture.executable) }
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\net10-pop3-large-mailbox"
}
$dataRoot = Join-Path $BenchmarkStagingRoot "Data"

if ($BenchmarkDatabase -notmatch '^hmail_perf_[a-z0-9_]+$') { throw "Refusing non-disposable benchmark database: $BenchmarkDatabase" }
if ([IO.Path]::GetFullPath($BenchmarkStagingRoot) -notmatch '(?i)^C:\\hmail-perf-') { throw "Refusing non-disposable benchmark root: $BenchmarkStagingRoot" }
if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) { throw "Live listener host is missing: $serviceExe" }
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) { throw "Disposable Data root is missing: $dataRoot" }

function Read-Pop3Line {
    param([IO.StreamReader]$Reader)
    $line = $Reader.ReadLine()
    if ($null -eq $line) { throw "POP3 server closed the connection before a response line." }
    return $line
}

function Read-Pop3Block {
    param([IO.StreamReader]$Reader)
    $lines = [System.Collections.Generic.List[string]]::new()
    while ($true) {
        $line = Read-Pop3Line $Reader
        if ($line -eq ".") { return $lines.ToArray() }
        $lines.Add($line)
    }
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percent)
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return 0 }
    $rank = ($Percent / 100) * ($sorted.Count - 1)
    $lower = [math]::Floor($rank)
    $upper = [math]::Ceiling($rank)
    if ($lower -eq $upper) { return [math]::Round([double]$sorted[$lower], 3) }
    return [math]::Round(([double]$sorted[$lower] + (([double]$sorted[$upper] - [double]$sorted[$lower]) * ($rank - $lower))), 3)
}

function Invoke-Pop3LargeMailboxScenario {
    $total = [Diagnostics.Stopwatch]::StartNew()
    $client = $null
    $reader = $null
    $writer = $null
    $stage = "connect"
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 25110)
        $client.ReceiveTimeout = 10000
        $client.SendTimeout = 10000
        $stream = $client.GetStream()
        $reader = [IO.StreamReader]::new($stream)
        $writer = [IO.StreamWriter]::new($stream)
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true

        $stage = "greeting"
        $greeting = Read-Pop3Line $reader
        $writer.WriteLine("USER test@perf.test")
        $user = Read-Pop3Line $reader
        $writer.WriteLine("PASS test")
        $password = Read-Pop3Line $reader
        if ($greeting -notlike "+OK*" -or $user -notlike "+OK*" -or $password -notlike "+OK*") {
            throw "POP3 authentication failed: [$greeting] [$user] [$password]"
        }

        $stage = "STAT"
        $statWatch = [Diagnostics.Stopwatch]::StartNew()
        $writer.WriteLine("STAT")
        $stat = Read-Pop3Line $reader
        $statWatch.Stop()
        if ($stat -notmatch '^\+OK\s+(\d+)\s+(\d+)') { throw "Unexpected STAT response: [$stat]" }
        $statCount = [int]$Matches[1]
        $statSize = [int64]$Matches[2]

        $stage = "LIST"
        $listWatch = [Diagnostics.Stopwatch]::StartNew()
        $writer.WriteLine("LIST")
        $listHeader = Read-Pop3Line $reader
        $listRows = if ($listHeader -like "+OK*") { @(Read-Pop3Block $reader) } else { @() }
        $listWatch.Stop()
        $listCount = @($listRows | Where-Object { $_ -match '^\d+\s+\d+$' }).Count
        if ($listHeader -notlike "+OK*" -or $listCount -ne $ExpectedMessages) { throw "LIST returned $listCount rows, expected $ExpectedMessages." }

        $stage = "UIDL"
        $uidlWatch = [Diagnostics.Stopwatch]::StartNew()
        $writer.WriteLine("UIDL")
        $uidlHeader = Read-Pop3Line $reader
        $uidlRows = if ($uidlHeader -like "+OK*") { @(Read-Pop3Block $reader) } else { @() }
        $uidlWatch.Stop()
        $uidlCount = @($uidlRows | Where-Object { $_ -match '^\d+\s+\S+$' }).Count
        if ($uidlHeader -notlike "+OK*" -or $uidlCount -ne $ExpectedMessages) { throw "UIDL returned $uidlCount rows, expected $ExpectedMessages." }

        $stage = "RETR"
        $retrWatch = [Diagnostics.Stopwatch]::StartNew()
        $writer.WriteLine("RETR 1")
        $retrHeader = Read-Pop3Line $reader
        $retrRows = if ($retrHeader -like "+OK*") { @(Read-Pop3Block $reader) } else { @() }
        $retrWatch.Stop()
        if ($retrHeader -notlike "+OK*" -or $retrRows.Count -eq 0) { throw "RETR 1 returned no message data." }

        $stage = "QUIT"
        $writer.WriteLine("QUIT")
        $quit = Read-Pop3Line $reader
        if ($quit -notlike "+OK*") { throw "Unexpected QUIT response: [$quit]" }
        $total.Stop()
        [pscustomobject]@{
            ok = $true
            total_ms = [math]::Round($total.Elapsed.TotalMilliseconds, 3)
            stat_ms = [math]::Round($statWatch.Elapsed.TotalMilliseconds, 3)
            list_ms = [math]::Round($listWatch.Elapsed.TotalMilliseconds, 3)
            uidl_ms = [math]::Round($uidlWatch.Elapsed.TotalMilliseconds, 3)
            retr_ms = [math]::Round($retrWatch.Elapsed.TotalMilliseconds, 3)
            stat_count = $statCount
            stat_size = $statSize
            list_count = $listCount
            uidl_count = $uidlCount
            retr_lines = $retrRows.Count
            error = $null
        }
    }
    catch {
        $total.Stop()
        [pscustomobject]@{
            ok = $false
            total_ms = [math]::Round($total.Elapsed.TotalMilliseconds, 3)
            stat_ms = 0
            list_ms = 0
            uidl_ms = 0
            retr_ms = 0
            stat_count = 0
            stat_size = 0
            list_count = 0
            uidl_count = 0
            retr_lines = 0
            error = "${stage}: $($_.Exception.Message)"
        }
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        if ($null -ne $writer) { $writer.Dispose() }
        if ($null -ne $client) { $client.Dispose() }
    }
}

function Get-MailboxRowCount {
    $connectionString = "Server=localhost;Database=$BenchmarkDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
    Add-Type -AssemblyName System.Data
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT COUNT_BIG(*) FROM hm_messages WHERE messageaccountid = 1 AND messagefolderid = 1 AND messagetype = 2;"
        return [int64]$command.ExecuteScalar()
    }
    finally { if ($null -ne $connection) { $connection.Dispose() } }
}

$env:HMAILSERVER_SQLSERVER_CONNECTION = "Server=localhost;Database=$BenchmarkDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$env:HMAILSERVER_DATA_DIRECTORY = $dataRoot
$env:HMAILSERVER_INITIALIZATION_FILE = Join-Path $BenchmarkStagingRoot "hMailServer.ini"
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
    $process = Start-Process -FilePath $serviceExe -ArgumentList $argumentList -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { $readinessFailures.Add("Live listener host exited before POP3 readiness."); break }
        $listeners = @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort 25110 -ErrorAction SilentlyContinue)
        if ($listeners.Count -gt 0) {
            try {
                $probe = [Net.Sockets.TcpClient]::new("127.0.0.1", 25110)
                $probe.ReceiveTimeout = 3000
                $probeReader = [IO.StreamReader]::new($probe.GetStream())
                $banner = $probeReader.ReadLine()
                $probeReader.Dispose(); $probe.Dispose()
                if ($banner -like "+OK*") { break }
            } catch { }
        }
        Start-Sleep -Milliseconds 250
    }
    if ($readinessFailures.Count -eq 0 -and $listeners.Count -eq 0) { $readinessFailures.Add("POP3 listener is not listening on 127.0.0.1:25110.") }
    if ($readinessFailures.Count -eq 0) {
        for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
            $result = Invoke-Pop3LargeMailboxScenario
            $samples.Add([pscustomobject]@{ scenario = "pop3-large-mailbox"; iteration = $iteration; ok = $result.ok; total_ms = $result.total_ms; stat_ms = $result.stat_ms; list_ms = $result.list_ms; uidl_ms = $result.uidl_ms; retr_ms = $result.retr_ms; stat_count = $result.stat_count; list_count = $result.list_count; uidl_count = $result.uidl_count; retr_lines = $result.retr_lines; error = $result.error })
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
$mailboxRows = Get-MailboxRowCount
$report = [pscustomobject]@{
    schema = "live-pop3-large-mailbox-v2"
    implementation = $Implementation
    runId = $RunId
    fixtureManifest = if ([string]::IsNullOrWhiteSpace($FixtureManifest)) { $null } else { [IO.Path]::GetFullPath($FixtureManifest) }
    status = if ($readinessFailures.Count -eq 0 -and $shutdownFailures.Count -eq 0 -and $successful.Count -eq $Iterations -and $mailboxRows -eq $ExpectedMessages) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $BenchmarkDatabase
    dataRoot = $dataRoot
    endpoint = "127.0.0.1:25110"
    expectedMessages = $ExpectedMessages
    mailboxRowsAfterRun = $mailboxRows
    iterations = $Iterations
    successes = $successful.Count
    errors = $samples.Count - $successful.Count
    total_p50_ms = Get-Percentile @($successful | ForEach-Object total_ms) 50
    total_p95_ms = Get-Percentile @($successful | ForEach-Object total_ms) 95
    total_p99_ms = Get-Percentile @($successful | ForEach-Object total_ms) 99
    list_p50_ms = Get-Percentile @($successful | ForEach-Object list_ms) 50
    list_p95_ms = Get-Percentile @($successful | ForEach-Object list_ms) 95
    uidl_p50_ms = Get-Percentile @($successful | ForEach-Object uidl_ms) 50
    retr_p50_ms = Get-Percentile @($successful | ForEach-Object retr_ms) 50
    readinessFailures = @($readinessFailures)
    shutdownFailures = @($shutdownFailures)
    samples = $samples
    productionSafety = "loopback-only; disposable SQL/Data roots required; no production service/DB/Data is used"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "net10-live-pop3-large-mailbox.json"
$csvPath = Join-Path $OutputDirectory "net10-live-pop3-large-mailbox.csv"
$markdownPath = Join-Path $OutputDirectory "net10-live-pop3-large-mailbox.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
@(
    "# $Implementation live POP3 large-mailbox acceptance",
    "",
    "Status: $($report.status)",
    "Mailbox: $($report.mailboxRowsAfterRun)/$($report.expectedMessages) rows",
    "Iterations: $($report.iterations); successes: $($report.successes); errors: $($report.errors)",
    "STAT/LIST/UIDL/RETR p50: $($report.total_p50_ms) / $($report.list_p50_ms) / $($report.uidl_p50_ms) / $($report.retr_p50_ms) ms",
    "",
    "This is Net10-only large-mailbox evidence. No C++ ratio or winner is valid until the same isolated fixture and commands run on the legacy server."
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "status=$($report.status); mailbox=$($report.mailboxRowsAfterRun)/$($report.expectedMessages); successes=$($report.successes); total_p50=$($report.total_p50_ms); list_p50=$($report.list_p50_ms); uidl_p50=$($report.uidl_p50_ms); retr_p50=$($report.retr_p50_ms)"
Write-Output "JSON: $jsonPath"
