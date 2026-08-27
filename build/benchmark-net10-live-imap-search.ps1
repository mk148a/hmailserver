param(
    [ValidateRange(1, 300)]
    [int]$Iterations = 25,
    [ValidateRange(10, 180)]
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
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\net10-live-imap-search"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) { throw "Live listener host is missing: $serviceExe" }
$dataRoot = Join-Path $stagingRoot "Data"
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) { throw "Disposable Data root is missing: $dataRoot" }
if ($database -notmatch '^hmail_perf_[a-z0-9_]+$') { throw "Refusing non-disposable benchmark database: $database" }
if ([IO.Path]::GetFullPath($stagingRoot) -notmatch '(?i)^C:\\hmail-perf-') { throw "Refusing non-disposable benchmark root: $stagingRoot" }

$connectionString = "Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"

function Invoke-SqlScalar {
    param([string]$Sql)
    Add-Type -AssemblyName System.Data
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Sql
        return $command.ExecuteScalar()
    }
    finally {
        if ($null -ne $connection) { $connection.Dispose() }
    }
}

function Clear-SearchFixture {
    Add-Type -AssemblyName System.Data
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = @"
UPDATE hm_settings SET settinginteger = 0 WHERE settingname = N'MessageIndexing';
DELETE FROM hm_message_search_queue;
DELETE FROM hm_message_search_documents;
"@
        [void]$command.ExecuteNonQuery()
    }
    finally {
        if ($null -ne $connection) { $connection.Dispose() }
    }
}

function Read-ImapLine {
    param([IO.StreamReader]$Reader)
    $line = $Reader.ReadLine()
    if ($null -eq $line) { throw "IMAP server closed the connection before a response line." }
    return $line
}

function Read-ImapTag {
    param([IO.StreamReader]$Reader, [string]$Tag)
    $untagged = [System.Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt 2048; $index++) {
        $line = Read-ImapLine $Reader
        if ($line.StartsWith("$Tag ", [StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject]@{ tag = $line; untagged = $untagged.ToArray() }
        }
        $untagged.Add($line)
    }
    throw "IMAP response did not terminate with tag $Tag."
}

function Get-SearchMatchCount {
    param([string[]]$Lines)
    $search = @($Lines | Where-Object { $_ -like "* SEARCH*" } | Select-Object -Last 1)
    if ($search.Count -eq 0) { return 0 }
    $line = [string]$search[0]
    $payload = $line -replace '^\* SEARCH\s*', ''
    if ([string]::IsNullOrWhiteSpace($payload)) { return 0 }
    return @($payload.Trim().Split(' ', [StringSplitOptions]::RemoveEmptyEntries)).Count
}

function Invoke-ImapSearchScenario {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $searchStopwatch = [Diagnostics.Stopwatch]::new()
    $client = $null
    $reader = $null
    $writer = $null
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 1143)
        $client.ReceiveTimeout = 10000
        $client.SendTimeout = 10000
        $stream = $client.GetStream()
        $reader = [IO.StreamReader]::new($stream)
        $writer = [IO.StreamWriter]::new($stream)
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true

        $greeting = Read-ImapLine $reader
        $writer.WriteLine("a001 LOGIN test@perf.test test")
        $login = Read-ImapTag $reader "a001"
        $writer.WriteLine("a002 SELECT INBOX")
        $select = Read-ImapTag $reader "a002"
        $searchStopwatch.Start()
        $writer.WriteLine("a003 SEARCH TEXT needle")
        $search = Read-ImapTag $reader "a003"
        $searchStopwatch.Stop()
        $writer.WriteLine("a004 LOGOUT")
        $logout = Read-ImapTag $reader "a004"
        $matches = Get-SearchMatchCount $search.untagged
        $ok = ($greeting -like "* OK*") `
            -and ($login.tag -like "a001 OK*") `
            -and ($select.tag -like "a002 OK*") `
            -and ($search.tag -like "a003 OK*") `
            -and ($logout.tag -like "a004 OK*") `
            -and ($matches -eq 1000)
        $stopwatch.Stop()
        [pscustomobject]@{
            ok = [bool]$ok
            matches = $matches
            ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            search_ms = [math]::Round($searchStopwatch.Elapsed.TotalMilliseconds, 3)
            error = if ($ok) { $null } else { "IMAP SEARCH response did not return 1000 matches: [$($search.tag)]" }
        }
    }
    catch {
        $stopwatch.Stop()
        $searchStopwatch.Stop()
        [pscustomobject]@{
            ok = $false
            matches = 0
            ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            search_ms = [math]::Round($searchStopwatch.Elapsed.TotalMilliseconds, 3)
            error = $_.Exception.Message
        }
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

$env:HMAILSERVER_NET10_LIVE_SQL_FTS_DIAGNOSTIC = "1"
$env:HMAILSERVER_NET10_LIVE_SQL_FTS_KEEP = "1"
$env:HMAILSERVER_NET10_LIVE_SQL_CONNECTION = $connectionString
$env:HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT = $dataRoot
$testProject = Join-Path $repoRoot "hmailserver\source\Server.Net10\tests\HMailServer.Net10.Tests\HMailServer.Net10.Tests.csproj"
$process = $null
$preparationPassed = $false
$shutdownPassed = $true
$samples = [System.Collections.Generic.List[object]]::new()
$startUtc = [DateTimeOffset]::UtcNow
try {
    & dotnet test $testProject --configuration Release --filter "FullyQualifiedName~DisposableFullTextBackfillAndSearchAreUsable" --logger "console;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) { throw "Disposable Full-Text preparation test failed with exit code $LASTEXITCODE." }
    $preparationPassed = $true

    $env:HMAILSERVER_SQLSERVER_CONNECTION = $connectionString
    $env:HMAILSERVER_DATA_DIRECTORY = $dataRoot
    $env:HMAILSERVER_INITIALIZATION_FILE = Join-Path $stagingRoot "hMailServer.ini"
    $env:HMAILSERVER_SMTP_ENABLED = "false"
    $env:HMAILSERVER_IMAP_ENABLED = "true"
    $env:HMAILSERVER_IMAP_BIND_ADDRESS = "127.0.0.1"
    $env:HMAILSERVER_IMAP_PORT = "1143"
    $env:HMAILSERVER_POP3_ENABLED = "false"
    $env:HMAILSERVER_EXTERNAL_FETCH_ENABLED = "false"
    $env:HMAILSERVER_COM_LOCAL_SERVER_ENABLED = "false"

    $process = Start-Process -FilePath $serviceExe -ArgumentList "90" -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    $ready = $false
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { break }
        $listener = @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort 1143 -ErrorAction SilentlyContinue)
        if ($listener.Count -gt 0 -and @($listener | Where-Object { $_.OwningProcess -eq $process.Id }).Count -gt 0) {
            try {
                $probe = [Net.Sockets.TcpClient]::new("127.0.0.1", 1143)
                $probe.ReceiveTimeout = 3000
                $probeReader = [IO.StreamReader]::new($probe.GetStream())
                $banner = $probeReader.ReadLine()
                $probeReader.Dispose(); $probe.Dispose()
                if ($banner -like "* OK*") { $ready = $true; break }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw "IMAP listener did not become ready on 127.0.0.1:1143." }
    Start-Sleep -Milliseconds 500
    for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
        $result = Invoke-ImapSearchScenario
        $samples.Add([pscustomobject]@{
            scenario = "imap-search"
            iteration = $iteration
            ok = $result.ok
            matches = $result.matches
            ms = $result.ms
            search_ms = $result.search_ms
            error = $result.error
        })
    }
}
finally {
    if ($null -ne $process -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        try { Stop-Process -Id $process.Id -Force } catch { $shutdownPassed = $false }
    }
    try { Clear-SearchFixture } catch { $shutdownPassed = $false }
}

$endUtc = [DateTimeOffset]::UtcNow
$successful = @($samples | Where-Object ok)
$latencies = @($successful | ForEach-Object ms)
$searchLatencies = @($successful | ForEach-Object search_ms)
$report = [pscustomobject]@{
    schema = "live-imap-search-acceptance-v1"
    implementation = "net10"
    status = if ($preparationPassed -and $shutdownPassed -and $successful.Count -eq $Iterations) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $database
    dataRoot = $dataRoot
    bind = "127.0.0.1"
    port = 1143
    search = "TEXT needle"
    indexedMessages = 1000
    iterations = $Iterations
    successes = $successful.Count
    errors = $samples.Count - $successful.Count
    p50_ms = Get-Percentile $latencies 50
    p95_ms = Get-Percentile $latencies 95
    p99_ms = Get-Percentile $latencies 99
    search_p50_ms = Get-Percentile $searchLatencies 50
    search_p95_ms = Get-Percentile $searchLatencies 95
    search_p99_ms = Get-Percentile $searchLatencies 99
    preparationPassed = $preparationPassed
    shutdownPassed = $shutdownPassed
    samples = $samples
    productionSafety = "loopback-only; disposable SQL/Data roots required; search documents and queues are cleared after the run"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "net10-live-imap-search.json"
$csvPath = Join-Path $OutputDirectory "net10-live-imap-search.csv"
$markdownPath = Join-Path $OutputDirectory "net10-live-imap-search.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
@(
    "# .NET 10 live IMAP Full-Text SEARCH acceptance",
    "",
    "Status: $($report.status)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Endpoint: $($report.bind):$($report.port)",
    "Query: $($report.search); indexed messages: $($report.indexedMessages)",
    "Iterations: $($report.iterations); successes: $($report.successes); errors: $($report.errors)",
    "Session p50/p95/p99: $($report.p50_ms) / $($report.p95_ms) / $($report.p99_ms) ms",
    "SEARCH p50/p95/p99: $($report.search_p50_ms) / $($report.search_p95_ms) / $($report.search_p99_ms) ms",
    "",
    "This is Net10-only evidence. No C++ ratio or winner is valid until the same isolated fixture and SEARCH scenario run on the legacy server."
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "status=$($report.status); successes=$($report.successes); errors=$($report.errors); search_p50=$($report.search_p50_ms); search_p95=$($report.search_p95_ms); search_p99=$($report.search_p99_ms)"
Write-Output "JSON: $jsonPath"
