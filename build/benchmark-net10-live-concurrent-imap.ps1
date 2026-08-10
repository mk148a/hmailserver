param(
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10",
    [ValidateRange(1, 5000)]
    [int]$Concurrency = 1000,
    [ValidateRange(500, 30000)]
    [int]$TimeoutMilliseconds = 5000,
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$serviceExe = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe"
$stagingRoot = "C:\hmail-perf-net10-ascii-20260810"
$database = "hmail_perf_net_sql_20260810_152708"
$argumentList = [string]30

if ($Implementation -eq "cpp") {
    $serviceExe = "C:\hmail-perf-cpp-ascii-20260810\Bin\hMailServer.exe"
    $stagingRoot = "C:\hmail-perf-cpp-ascii-20260810"
    $database = "hmail_perf_cpp_sql_20260810_152708"
    $argumentList = "/Debug"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\$Implementation-concurrent-imap"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) {
    throw "Live listener host is missing: $serviceExe"
}
if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot "Data") -PathType Container)) {
    throw "Disposable Data directory is missing: $stagingRoot\Data"
}

if (-not ("HMailServerLiveImapProbe" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed class HMailServerLiveImapProbeResult
{
    public bool Success { get; set; }
    public bool TimedOut { get; set; }
    public double Milliseconds { get; set; }
    public string Error { get; set; }
}

public static class HMailServerLiveImapProbe
{
    public static HMailServerLiveImapProbeResult[] RunMany(int count, int timeoutMilliseconds)
    {
        var tasks = new Task<HMailServerLiveImapProbeResult>[count];
        for (var index = 0; index < count; index++)
        {
            tasks[index] = Task.Run(() => RunOne(timeoutMilliseconds));
        }

        var completed = Task.WaitAll(tasks, timeoutMilliseconds + 30000);
        var results = new HMailServerLiveImapProbeResult[count];
        for (var index = 0; index < count; index++)
        {
            results[index] = completed && tasks[index].IsCompleted
                ? tasks[index].GetAwaiter().GetResult()
                : new HMailServerLiveImapProbeResult
                {
                    Success = false,
                    TimedOut = true,
                    Milliseconds = timeoutMilliseconds + 30000,
                    Error = "The concurrent IMAP probe did not complete before the batch timeout."
                };
        }

        return results;
    }

    private static HMailServerLiveImapProbeResult RunOne(int timeoutMilliseconds)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using (var client = new TcpClient())
            {
                var connect = client.ConnectAsync("127.0.0.1", 1143);
                if (!connect.Wait(timeoutMilliseconds))
                {
                    return Failure(stopwatch, true, "IMAP connection timed out.");
                }

                client.ReceiveTimeout = timeoutMilliseconds;
                client.SendTimeout = timeoutMilliseconds;
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(stream) { NewLine = "\r\n", AutoFlush = true })
                {
                    var greeting = reader.ReadLine();
                    writer.WriteLine("a001 LOGIN test@perf.test test");
                    var login = ReadTag(reader, "a001");
                    writer.WriteLine("a002 SELECT INBOX");
                    var select = ReadTag(reader, "a002");
                    writer.WriteLine("a003 SEARCH TEXT needle");
                    var search = ReadTag(reader, "a003");
                    writer.WriteLine("a004 SORT (DATE) UTF-8 ALL");
                    var sort = ReadTag(reader, "a004");
                    writer.WriteLine("a005 LOGOUT");
                    var logout = ReadTag(reader, "a005");

                    var success = greeting != null
                        && greeting.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0
                        && IsOk(login)
                        && IsOk(select)
                        && IsOk(search)
                        && IsOk(sort)
                        && IsOk(logout);
                    return success
                        ? Success(stopwatch)
                        : Failure(
                            stopwatch,
                            false,
                            "IMAP response failure: greeting=[" + greeting + "] login=[" + login
                            + "] select=[" + select + "] search=[" + search + "] sort=[" + sort
                            + "] logout=[" + logout + "]");
                }
            }
        }
        catch (AggregateException exception)
        {
            return Failure(stopwatch, exception.InnerException is TimeoutException, exception.Message);
        }
        catch (IOException exception)
        {
            return Failure(stopwatch, false, exception.Message);
        }
        catch (SocketException exception)
        {
            return Failure(stopwatch, false, exception.Message);
        }
        catch (Exception exception)
        {
            return Failure(stopwatch, false, exception.Message);
        }
    }

    private static string ReadTag(StreamReader reader, string tag)
    {
        for (var index = 0; index < 512; index++)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                return null;
            }

            if (line.StartsWith(tag + " ", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return null;
    }

    private static bool IsOk(string line)
    {
        return line != null && line.IndexOf(" OK", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static HMailServerLiveImapProbeResult Success(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new HMailServerLiveImapProbeResult
        {
            Success = true,
            Milliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3)
        };
    }

    private static HMailServerLiveImapProbeResult Failure(
        Stopwatch stopwatch,
        bool timedOut,
        string error)
    {
        stopwatch.Stop();
        return new HMailServerLiveImapProbeResult
        {
            Success = false,
            TimedOut = timedOut,
            Milliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
            Error = error ?? "Unknown IMAP probe failure."
        };
    }
}
'@
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percent)

    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) {
        return $null
    }
    $rank = ($Percent / 100) * ($sorted.Count - 1)
    $lower = [math]::Floor($rank)
    $upper = [math]::Ceiling($rank)
    if ($lower -eq $upper) {
        return [math]::Round([double]$sorted[$lower], 3)
    }
    return [math]::Round(([double]$sorted[$lower] + (([double]$sorted[$upper] - [double]$sorted[$lower]) * ($rank - $lower))), 3)
}

if ($Implementation -eq "net10") {
    $env:HMAILSERVER_SQLSERVER_CONNECTION = "Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True;"
    $env:HMAILSERVER_DATA_DIRECTORY = Join-Path $stagingRoot "Data"
    $env:HMAILSERVER_INITIALIZATION_FILE = Join-Path $stagingRoot "hMailServer.ini"
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
}

$process = Start-Process -FilePath $serviceExe -ArgumentList $argumentList -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
$startUtc = [DateTimeOffset]::UtcNow
$before = $null
$after = $null
$results = @()
try {
    Start-Sleep -Seconds 2
    $before = Get-Process -Id $process.Id
    $results = @([HMailServerLiveImapProbe]::RunMany($Concurrency, $TimeoutMilliseconds))
    $after = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
}
finally {
    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
        Stop-Process -Id $process.Id -Force
    }
}

$endUtc = [DateTimeOffset]::UtcNow
$successful = @($results | Where-Object Success)
$timedOut = @($results | Where-Object TimedOut)
$errors = @($results | Where-Object { -not $_.Success })
$summary = [pscustomobject]@{
    scenario = "imap-concurrent"
    concurrency = $Concurrency
    completed = $results.Count
    successes = $successful.Count
    errors = $errors.Count
    timeouts = $timedOut.Count
    p50_ms = Get-Percentile ($successful | ForEach-Object Milliseconds) 50
    p95_ms = Get-Percentile ($successful | ForEach-Object Milliseconds) 95
    p99_ms = Get-Percentile ($successful | ForEach-Object Milliseconds) 99
    throughput_sessions_per_second = if (($endUtc - $startUtc).TotalSeconds -gt 0) { [math]::Round($successful.Count / ($endUtc - $startUtc).TotalSeconds, 3) } else { 0 }
}

$report = [pscustomobject]@{
    schema = "live-concurrent-imap-v1"
    implementation = $Implementation
    status = if ($summary.errors -eq 0 -and $summary.completed -eq $Concurrency) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $database
    dataRoot = Join-Path $stagingRoot "Data"
    bind = "127.0.0.1"
    port = 1143
    messageCount = 1000
    concurrency = $Concurrency
    timeoutMilliseconds = $TimeoutMilliseconds
    summary = $summary
    processBefore = if ($null -ne $before) { @{ privateBytes = $before.PrivateMemorySize64; handles = $before.Handles; threads = $before.Threads.Count } } else { $null }
    processAfter = if ($null -ne $after) { @{ privateBytes = $after.PrivateMemorySize64; handles = $after.Handles; threads = $after.Threads.Count } } else { $null }
    samples = @($results | ForEach-Object {
        [pscustomobject]@{
            ok = $_.Success
            timedOut = $_.TimedOut
            ms = $_.Milliseconds
            error = $_.Error
        }
    })
    ratioValid = $false
    productionSafety = "service stopped/disabled; production DB/Data not used"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "live-concurrent-imap.json"
$csvPath = Join-Path $OutputDirectory "live-concurrent-imap.csv"
$markdownPath = Join-Path $OutputDirectory "live-concurrent-imap.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$report.samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
$markdown = @(
    "# Live concurrent IMAP benchmark",
    "",
    "Status: $($report.status)",
    "Implementation: $($report.implementation)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Bind/port: $($report.bind):$($report.port)",
    "Corpus files: $($report.messageCount)",
    "Concurrency: $($report.concurrency)",
    "Timeout: $($report.timeoutMilliseconds) ms",
    "",
    "| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Throughput/s |",
    "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    "| $($summary.scenario) | $($summary.completed) | $($summary.successes) | $($summary.errors) | $($summary.timeouts) | $($summary.p50_ms) | $($summary.p95_ms) | $($summary.p99_ms) | $($summary.throughput_sessions_per_second) |",
    "",
    "No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.",
    "COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed."
)
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

$summary | Format-Table -AutoSize
Write-Output "JSON: $jsonPath"
