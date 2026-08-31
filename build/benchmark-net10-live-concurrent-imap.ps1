param(
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10",
    [ValidateSet("Admission", "AuthSelect", "Search", "Sort", "Full")]
    [string]$Profile = "Full",
    [ValidateRange(1, 5000)]
    [int]$Concurrency = 1000,
    [ValidateRange(1, 100)]
    [int]$Waves = 1,
    [ValidateRange(500, 30000)]
    [int]$TimeoutMilliseconds = 5000,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [ValidateRange(0, 60)]
    [int]$PostWorkloadSettleSeconds = 5,
    [ValidateRange(0, 1000)]
    [int]$LaunchStaggerMilliseconds = 0,
    [string]$OutputDirectory = "",
    [string]$BenchmarkStagingRoot = "",
    [string]$BenchmarkDatabase = "",
    [string]$BenchmarkServiceExecutable = "",
    [string]$FixtureManifest = "",
    [string]$RunId = "",
    [ValidateRange(1, 5000)]
    [int]$SqlMaxPoolSize = 100
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

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

if (-not [string]::IsNullOrWhiteSpace($BenchmarkStagingRoot)) { $stagingRoot = [IO.Path]::GetFullPath($BenchmarkStagingRoot) }
if (-not [string]::IsNullOrWhiteSpace($BenchmarkDatabase)) { $database = $BenchmarkDatabase }
if (-not [string]::IsNullOrWhiteSpace($BenchmarkServiceExecutable)) {
    Assert-ApprovedBenchmarkExecutable -Path $BenchmarkServiceExecutable -Implementation $Implementation -RepositoryRoot $repoRoot
    $serviceExe = [IO.Path]::GetFullPath($BenchmarkServiceExecutable)
}

$fixtureBinding = $null
if (-not [string]::IsNullOrWhiteSpace($FixtureManifest)) {
    $fixtureBinding = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation $Implementation -RepositoryRoot $repoRoot
    if (-not [string]::IsNullOrWhiteSpace($BenchmarkDatabase) -and $BenchmarkDatabase -cne $fixtureBinding.database) {
        throw "BenchmarkDatabase does not match the fixture manifest."
    }
    $fixtureStagingRoot = Split-Path -Parent $fixtureBinding.dataRoot
    if (-not [string]::IsNullOrWhiteSpace($BenchmarkStagingRoot) -and [IO.Path]::GetFullPath($BenchmarkStagingRoot) -ine $fixtureStagingRoot) {
        throw "BenchmarkStagingRoot does not match the fixture manifest."
    }
    if ($null -ne $fixtureBinding.executable -and [IO.Path]::GetFullPath($serviceExe) -ine $fixtureBinding.executable) {
        throw "BenchmarkServiceExecutable does not match the fixture manifest."
    }
    $database = $fixtureBinding.database
    $stagingRoot = $fixtureStagingRoot
}
Assert-ApprovedBenchmarkExecutable -Path $serviceExe -Implementation $Implementation -RepositoryRoot $repoRoot

if ($database -notmatch '^hmail_perf_[a-z0-9_]+$') {
    throw "Refusing non-disposable benchmark database: $database"
}
if ([IO.Path]::GetFullPath($stagingRoot) -notmatch '(?i)^C:\\hmail-perf-') {
    throw "Refusing non-disposable benchmark root: $stagingRoot"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\$Implementation-concurrent-imap"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) {
    throw "Live listener host is missing: $serviceExe"
}
$dataRoot = Join-Path $stagingRoot "Data"
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
    throw "Disposable Data directory is missing: $stagingRoot\Data"
}

$expectedListeners = @(
    [pscustomobject]@{ protocol = "smtp"; port = 2525; banner = "220" },
    [pscustomobject]@{ protocol = "imap"; port = 1143; banner = "OK" },
    [pscustomobject]@{ protocol = "pop3"; port = 25110; banner = "+OK" }
)

function Get-ListenerState {
    param([int]$Port)

    [pscustomobject]@{
        connections = @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort $Port -ErrorAction SilentlyContinue)
        queryError = $null
    }
}

function Test-BannerProbe {
    param([string]$Protocol, [int]$Port, [string]$Expected)

    $client = $null
    $reader = $null
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", $Port)
        $client.ReceiveTimeout = 3000
        $reader = [IO.StreamReader]::new($client.GetStream())
        $response = $reader.ReadLine()
        $valid = switch ($Protocol) {
            "smtp" { $response -like "$Expected*" }
            "imap" { $response -like "*$Expected*" }
            "pop3" { $response -like "$Expected*" }
        }
        [pscustomobject]@{ ok = [bool]$valid; response = $response; error = if ($valid) { $null } else { "Unexpected $Protocol banner: [$response]" } }
    }
    catch {
        [pscustomobject]@{ ok = $false; response = $null; error = $_.Exception.Message }
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        if ($null -ne $client) { $client.Dispose() }
    }
}

function Wait-ForReadiness {
    param([int]$ProcessId)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    $lastFailures = @()
    do {
        $failures = [System.Collections.Generic.List[string]]::new()
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            $failures.Add("Launched process $ProcessId exited before readiness completed.")
        }
        foreach ($listener in $expectedListeners) {
            $state = Get-ListenerState $listener.port
            if ($null -ne $state.queryError) {
                $failures.Add("$($listener.protocol) listener query failed: $($state.queryError)")
                continue
            }
            if ($state.connections.Count -eq 0) {
                $failures.Add("$($listener.protocol) listener is not listening on 127.0.0.1:$($listener.port).")
                continue
            }
            $owners = @($state.connections | ForEach-Object { [int]$_.OwningProcess })
            if ($owners.Count -eq 0) {
                $failures.Add("$($listener.protocol) listener ownership was unavailable on port $($listener.port).")
            }
            elseif ($owners -notcontains $ProcessId) {
                $failures.Add("$($listener.protocol) listener on port $($listener.port) is owned by PID(s) $($owners -join ',') instead of launched PID $ProcessId.")
            }
        }
        if ($failures.Count -eq 0) {
            foreach ($listener in $expectedListeners) {
                $probe = Test-BannerProbe $listener.protocol $listener.port $listener.banner
                if (-not $probe.ok) {
                    $failures.Add("$($listener.protocol) banner probe failed on port $($listener.port): $($probe.error)")
                }
            }
        }
        if ($failures.Count -eq 0) {
            return @()
        }
        $lastFailures = $failures.ToArray()
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    return $lastFailures
}

function Wait-ForShutdown {
    param([int]$ProcessId)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    $lastFailures = @()
    do {
        $remaining = [System.Collections.Generic.List[string]]::new()
        if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
            $remaining.Add("Launched process $ProcessId is still running.")
        }
        foreach ($listener in $expectedListeners) {
            $state = Get-ListenerState $listener.port
            if ($null -ne $state.queryError) {
                $remaining.Add("$($listener.protocol) shutdown listener query failed: $($state.queryError)")
            }
            elseif ($state.connections.Count -gt 0) {
                $remaining.Add("$($listener.protocol) listener still present on 127.0.0.1:$($listener.port).")
            }
        }
        if ($remaining.Count -eq 0) {
            return @()
        }
        $lastFailures = $remaining.ToArray()
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    return $lastFailures
}

if (-not ("HMailServerLiveImapProbe" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class HMailServerLiveImapProbeResult
{
    public bool Success { get; set; }
    public bool TimedOut { get; set; }
    public double Milliseconds { get; set; }
    public string Error { get; set; }
    public bool SearchResultValid { get; set; }
    public int SearchResultCount { get; set; }
    public bool SearchExactSequence { get; set; }
    public bool SortResultValid { get; set; }
    public int SortResultCount { get; set; }
    public bool SortExactSequence { get; set; }
    public string ResultError { get; set; }
}

internal sealed class HMailServerLiveImapResultValidation
{
    public bool Valid { get; set; }
    public int Count { get; set; }
    public bool ExactSequence { get; set; }
    public string Error { get; set; }
}

internal sealed class HMailServerLiveImapTagResponse
{
    public string Tag { get; set; }
    public string[] Untagged { get; set; }
}

public static class HMailServerLiveImapProbe
{
    public static HMailServerLiveImapProbeResult[] RunMany(int count, int timeoutMilliseconds, string profile, int launchStaggerMilliseconds)
    {
        int originalMinWorkerThreads;
        int originalMinCompletionPortThreads;
        ThreadPool.GetMinThreads(out originalMinWorkerThreads, out originalMinCompletionPortThreads);
        ThreadPool.SetMinThreads(
            Math.Max(originalMinWorkerThreads, count),
            originalMinCompletionPortThreads);
        try
        {
            var tasks = new Task<HMailServerLiveImapProbeResult>[count];
            using (var startBarrier = new ManualResetEventSlim(false))
            {
                var ready = 0;
                for (var index = 0; index < count; index++)
                {
                    var sessionIndex = index;
                    tasks[index] = Task.Run(() =>
                    {
                        if (Interlocked.Increment(ref ready) == count)
                        {
                            startBarrier.Set();
                        }
                        startBarrier.Wait();
                        if (launchStaggerMilliseconds > 0)
                        {
                            Thread.Sleep(sessionIndex * launchStaggerMilliseconds);
                        }
                        return RunOne(timeoutMilliseconds, profile);
                    });
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
        }
        finally
        {
            ThreadPool.SetMinThreads(originalMinWorkerThreads, originalMinCompletionPortThreads);
        }
    }

    private static HMailServerLiveImapProbeResult RunOne(int timeoutMilliseconds, string profile)
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
                    HMailServerLiveImapTagResponse login = null;
                    HMailServerLiveImapTagResponse select = null;
                    HMailServerLiveImapTagResponse search = null;
                    HMailServerLiveImapTagResponse sort = null;
                    HMailServerLiveImapTagResponse logout;
                    if (profile == "Admission")
                    {
                        writer.WriteLine("a001 LOGOUT");
                        logout = ReadTag(reader, "a001");
                    }
                    else
                    {
                        writer.WriteLine("a001 LOGIN test@perf.test test");
                        login = ReadTag(reader, "a001");
                        writer.WriteLine("a002 SELECT INBOX");
                        select = ReadTag(reader, "a002");
                        if (profile == "Search" || profile == "Full")
                        {
                            writer.WriteLine("a003 SEARCH TEXT needle");
                            search = ReadTag(reader, "a003");
                        }
                        if (profile == "Sort" || profile == "Full")
                        {
                            var sortTag = profile == "Full" ? "a004" : "a003";
                            writer.WriteLine(sortTag + " SORT (DATE) UTF-8 ALL");
                            sort = ReadTag(reader, sortTag);
                        }
                        var logoutTag = profile == "Full" || profile == "Search" || profile == "Sort" ? "a005" : "a003";
                        if (profile == "Search" || profile == "Sort")
                        {
                            logoutTag = "a004";
                        }
                        writer.WriteLine(logoutTag + " LOGOUT");
                        logout = ReadTag(reader, logoutTag);
                    }

                    var searchValidation = profile == "Search" || profile == "Full"
                        ? ValidateResult(search == null ? null : search.Untagged, "SEARCH", 1000)
                        : null;
                    var sortValidation = profile == "Sort" || profile == "Full"
                        ? ValidateResult(sort == null ? null : sort.Untagged, "SORT", 1000)
                        : null;

                    var success = greeting != null
                        && greeting.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0
                        && (profile == "Admission" || (IsOk(login) && IsOk(select)))
                        && (!(profile == "Search" || profile == "Full") || (IsOk(search) && searchValidation.Valid))
                        && (!(profile == "Sort" || profile == "Full") || (IsOk(sort) && sortValidation.Valid))
                        && IsOk(logout);
                    return success
                        ? Success(stopwatch, searchValidation, sortValidation)
                        : Failure(
                            stopwatch,
                            false,
                            "IMAP response failure: greetingOk=" + (greeting != null)
                            + " profile=" + profile + " loginOk=" + IsOk(login) + " selectOk=" + IsOk(select)
                            + " searchOk=" + IsOk(search) + " sortOk=" + IsOk(sort)
                            + " logoutOk=" + IsOk(logout)
                            + " resultError=" + CombineValidationErrors(searchValidation, sortValidation),
                            searchValidation,
                            sortValidation);
                }
            }
        }
        catch (AggregateException exception)
        {
            var innerErrors = exception.Flatten().InnerExceptions
                .Select(item => item.Message)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
            return Failure(
                stopwatch,
                exception.Flatten().InnerExceptions.Any(item => item is TimeoutException),
                innerErrors.Length == 0 ? exception.Message : string.Join(" | ", innerErrors));
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

    private static HMailServerLiveImapTagResponse ReadTag(StreamReader reader, string tag)
    {
        var untagged = new List<string>();
        for (var index = 0; index < 512; index++)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                return null;
            }

            if (line.StartsWith(tag + " ", StringComparison.OrdinalIgnoreCase))
            {
                return new HMailServerLiveImapTagResponse
                {
                    Tag = line,
                    Untagged = untagged.ToArray()
                };
            }
            untagged.Add(line);
        }

        return null;
    }

    private static bool IsOk(HMailServerLiveImapTagResponse response)
    {
        return response != null
            && response.Tag != null
            && response.Tag.IndexOf(" OK", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static HMailServerLiveImapResultValidation ValidateResult(
        string[] lines,
        string identifier,
        int expectedCount)
    {
        var prefix = "* " + identifier;
        string candidate = null;
        if (lines != null)
        {
            foreach (var line in lines)
            {
                if (line != null
                    && line.StartsWith(prefix, StringComparison.Ordinal)
                    && (line.Length == prefix.Length || line[prefix.Length] == ' '))
                {
                    candidate = line;
                }
            }
        }

        if (candidate == null)
        {
            return InvalidResult("No untagged * " + identifier + " result line was found.");
        }

        if (candidate.Length == prefix.Length)
        {
            return new HMailServerLiveImapResultValidation
            {
                Valid = expectedCount == 0,
                ExactSequence = expectedCount == 0,
                Error = expectedCount == 0 ? null : "Expected " + expectedCount + " result values, got zero."
            };
        }

        if (candidate[prefix.Length] != ' ')
        {
            return InvalidResult("Result values must follow the command name with one space.");
        }

        var payload = candidate.Substring(prefix.Length + 1);
        if (payload.Length == 0 || payload[0] == ' ' || payload[payload.Length - 1] == ' ')
        {
            return InvalidResult("A zero-result line must have no trailing space and nonzero values must use single spaces.");
        }

        var tokens = payload.Split(new[] { ' ' }, StringSplitOptions.None);
        var count = 0;
        var exact = true;
        foreach (var token in tokens)
        {
            int value;
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                return InvalidResult("Result contains a nonnumeric token.", count);
            }
            count++;
            if (value != count)
            {
                exact = false;
            }
        }

        exact = exact && count == expectedCount;
        return new HMailServerLiveImapResultValidation
        {
            Valid = exact,
            Count = count,
            ExactSequence = exact,
            Error = exact
                ? null
                : count == expectedCount
                    ? "Result values are not in exact 1.." + expectedCount + " order."
                    : "Expected " + expectedCount + " result values, got " + count + "."
        };
    }

    private static HMailServerLiveImapResultValidation InvalidResult(string error, int count = 0)
    {
        return new HMailServerLiveImapResultValidation
        {
            Valid = false,
            Count = count,
            ExactSequence = false,
            Error = error
        };
    }

    private static string CombineValidationErrors(
        HMailServerLiveImapResultValidation search,
        HMailServerLiveImapResultValidation sort)
    {
        var errors = new List<string>();
        if (search != null && !string.IsNullOrEmpty(search.Error))
        {
            errors.Add("SEARCH: " + search.Error);
        }
        if (sort != null && !string.IsNullOrEmpty(sort.Error))
        {
            errors.Add("SORT: " + sort.Error);
        }
        return string.Join("; ", errors);
    }

    private static HMailServerLiveImapProbeResult Success(
        Stopwatch stopwatch,
        HMailServerLiveImapResultValidation search,
        HMailServerLiveImapResultValidation sort)
    {
        return Finish(stopwatch, true, false, null, search, sort);
    }

    private static HMailServerLiveImapProbeResult Failure(
        Stopwatch stopwatch,
        bool timedOut,
        string error,
        HMailServerLiveImapResultValidation search = null,
        HMailServerLiveImapResultValidation sort = null)
    {
        return Finish(stopwatch, false, timedOut, error, search, sort);
    }

    private static HMailServerLiveImapProbeResult Finish(
        Stopwatch stopwatch,
        bool success,
        bool timedOut,
        string error,
        HMailServerLiveImapResultValidation search,
        HMailServerLiveImapResultValidation sort)
    {
        stopwatch.Stop();
        return new HMailServerLiveImapProbeResult
        {
            Success = success,
            TimedOut = timedOut,
            Milliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
            Error = error ?? (success ? null : "Unknown IMAP probe failure."),
            SearchResultValid = search != null && search.Valid,
            SearchResultCount = search == null ? 0 : search.Count,
            SearchExactSequence = search != null && search.ExactSequence,
            SortResultValid = sort != null && sort.Valid,
            SortResultCount = sort == null ? 0 : sort.Count,
            SortExactSequence = sort != null && sort.ExactSequence,
            ResultError = CombineValidationErrors(search, sort)
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
    $env:HMAILSERVER_SQLSERVER_CONNECTION = "Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True;Max Pool Size=$SqlMaxPoolSize;"
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
    $env:HMAILSERVER_COM_LOCAL_SERVER_ENABLED = "false"
}

$sqlConnectionSettings = if ($Implementation -eq "net10") {
    [pscustomobject]@{
        appliesTo = "net10"
        provider = "Microsoft.Data.SqlClient"
        server = "localhost"
        database = $database
        integratedSecurity = $true
        trustServerCertificate = $true
        pooling = $true
        maxPoolSize = $SqlMaxPoolSize
        connectionTimeoutSeconds = 15
    }
}
else {
    [pscustomobject]@{
        appliesTo = "cpp"
        provider = "legacy native hMailServer SQL layer"
        server = "localhost"
        database = $database
        integratedSecurity = $null
        trustServerCertificate = $null
        pooling = $null
        maxPoolSize = $null
        connectionTimeoutSeconds = $null
    }
}
$probeConfiguration = [pscustomobject]@{
    scheduler = "Task.Run with a ManualResetEventSlim start barrier"
    profile = $Profile
    perSessionCommands = switch ($Profile) {
        "Admission" { "greeting; LOGOUT"; break }
        "AuthSelect" { "greeting; LOGIN; SELECT INBOX; LOGOUT"; break }
        "Search" { "greeting; LOGIN; SELECT INBOX; SEARCH; LOGOUT"; break }
        "Sort" { "greeting; LOGIN; SELECT INBOX; SORT; LOGOUT"; break }
        default { "greeting; LOGIN; SELECT INBOX; SEARCH; SORT; LOGOUT" }
    }
    concurrentSessionsPerWave = $Concurrency
    waves = $Waves
    socketTimeoutMilliseconds = $TimeoutMilliseconds
    launchStaggerMilliseconds = $LaunchStaggerMilliseconds
    fanOut = "one TCP client and one sequential IMAP session per sample"
}

$process = $null
$startUtc = [DateTimeOffset]::UtcNow
$before = $null
$afterImmediate = $null
$after = $null
$readinessFailures = @()
$shutdownFailures = @()
$results = [System.Collections.Generic.List[object]]::new()
$waveMetrics = [System.Collections.Generic.List[object]]::new()
$runtimeFailures = [System.Collections.Generic.List[string]]::new()
$preflight = $null
$provenance = $null
$runStartAttestation = $null
$workloadStartedUtc = $null
$workloadEndedUtc = $null
$workloadSeconds = 0.0

$provenance = Get-LiveBenchmarkProvenance -FixtureManifest $FixtureManifest -RunId $RunId -Implementation $Implementation -RepositoryRoot $repoRoot -Database $database -DataRoot $dataRoot -ServiceExecutable $serviceExe -Ports ([ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 })

if ($Implementation -eq "cpp") {
    $preflight = Get-CppIsolationPreflight -TargetExecutable $serviceExe -ExpectedStagingRoot $stagingRoot -ExpectedDatabase $database
    $readinessFailures = @($preflight.failures)
}

if ($null -eq $preflight -or $preflight.passed) {
    if ($provenance.manifestBound) {
        $runStartAttestation = Assert-LiveBenchmarkRunStartAttestation -FixtureManifest $FixtureManifest -Implementation $Implementation -RepositoryRoot $repoRoot -Database $database -DataRoot $dataRoot -ServiceExecutable $serviceExe
    }
    $process = Start-Process -FilePath $serviceExe -ArgumentList $argumentList -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
}
try {
    if ($null -ne $process) {
        $readinessFailures = @(Wait-ForReadiness $process.Id)
        if ($readinessFailures.Count -eq 0) {
            $metricProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
            if ($null -eq $metricProcess) {
                $runtimeFailures.Add("Launched process $($process.Id) exited before workload start.")
            } else {
                $before = [pscustomobject]@{
                    privateBytes = [long]$metricProcess.PrivateMemorySize64
                    handles = [int]$metricProcess.Handles
                    threads = [int]$metricProcess.Threads.Count
                }
            }
            if ($null -ne $metricProcess) {
                for ($wave = 1; $wave -le $Waves; $wave++) {
                $metricProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
                if ($null -eq $metricProcess) {
                    $runtimeFailures.Add("Launched process $($process.Id) exited before wave $wave started.")
                    break
                }
                $waveBefore = [pscustomobject]@{
                    privateBytes = [long]$metricProcess.PrivateMemorySize64
                    handles = [int]$metricProcess.Handles
                    threads = [int]$metricProcess.Threads.Count
                }
                $waveStartedUtc = [DateTimeOffset]::UtcNow
                if ($null -eq $workloadStartedUtc) {
                    $workloadStartedUtc = $waveStartedUtc
                }
                $waveResults = @([HMailServerLiveImapProbe]::RunMany($Concurrency, $TimeoutMilliseconds, $Profile, $LaunchStaggerMilliseconds))
                $waveEndedUtc = [DateTimeOffset]::UtcNow
                $workloadEndedUtc = $waveEndedUtc
                $workloadSeconds += ($waveEndedUtc - $waveStartedUtc).TotalSeconds
                $session = 0
                foreach ($waveResult in $waveResults) {
                    $session++
                    $results.Add([pscustomobject]@{
                        Wave = $wave
                        Session = $session
                        Success = $waveResult.Success
                        TimedOut = $waveResult.TimedOut
                        Milliseconds = $waveResult.Milliseconds
                        Error = $waveResult.Error
                        SearchResultValid = $waveResult.SearchResultValid
                        SearchResultCount = $waveResult.SearchResultCount
                        SearchExactSequence = $waveResult.SearchExactSequence
                        SortResultValid = $waveResult.SortResultValid
                        SortResultCount = $waveResult.SortResultCount
                        SortExactSequence = $waveResult.SortExactSequence
                        ResultError = $waveResult.ResultError
                    })
                }
                $metricProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
                $afterImmediate = if ($null -ne $metricProcess) {
                    [pscustomobject]@{
                        privateBytes = [long]$metricProcess.PrivateMemorySize64
                        handles = [int]$metricProcess.Handles
                        threads = [int]$metricProcess.Threads.Count
                    }
                } else { $null }
                if ($PostWorkloadSettleSeconds -gt 0) {
                    Start-Sleep -Seconds $PostWorkloadSettleSeconds
                }
                $metricProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
                $after = if ($null -ne $metricProcess) {
                    [pscustomobject]@{
                        privateBytes = [long]$metricProcess.PrivateMemorySize64
                        handles = [int]$metricProcess.Handles
                        threads = [int]$metricProcess.Threads.Count
                    }
                } else { $null }
                $waveSuccessful = @($waveResults | Where-Object Success)
                $waveMetrics.Add([pscustomobject]@{
                    wave = $wave
                    startedUtc = $waveStartedUtc.ToString("o")
                    endedUtc = $waveEndedUtc.ToString("o")
                    workloadSeconds = [math]::Round(($waveEndedUtc - $waveStartedUtc).TotalSeconds, 6)
                    successes = $waveSuccessful.Count
                    errors = $waveResults.Count - $waveSuccessful.Count
                    processBefore = $waveBefore
                    processAfterImmediate = $afterImmediate
                    processAfterSettle = $after
                })
                }
            }
        }
    }
}
finally {
    if ($null -ne $process -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        try { Stop-Process -Id $process.Id -Force } catch { $shutdownFailures += "Unable to stop launched process $($process.Id): $($_.Exception.Message)" }
    }
    if ($null -ne $process) {
        $shutdownFailures += @(Wait-ForShutdown $process.Id)
    }
}

$endUtc = [DateTimeOffset]::UtcNow
$successful = @($results | Where-Object Success)
$timedOut = @($results | Where-Object TimedOut)
$errors = @($results | Where-Object { -not $_.Success })
$requestedSessions = $Concurrency * $Waves
$summary = [pscustomobject]@{
    scenario = "imap-concurrent"
    concurrency = $Concurrency
    waves = $Waves
    requested = $requestedSessions
    completed = $results.Count
    successes = $successful.Count
    errors = $errors.Count
    timeouts = $timedOut.Count
    p50_ms = Get-Percentile ($successful | ForEach-Object Milliseconds) 50
    p95_ms = Get-Percentile ($successful | ForEach-Object Milliseconds) 95
    p99_ms = Get-Percentile ($successful | ForEach-Object Milliseconds) 99
    workload_seconds = [math]::Round($workloadSeconds, 6)
    throughput_sessions_per_second = if ($workloadSeconds -gt 0) { [math]::Round($successful.Count / $workloadSeconds, 3) } else { 0 }
}

$report = [pscustomobject]@{
    schema = "live-concurrent-imap-v2"
    implementation = $Implementation
    profile = $Profile
    status = if ($summary.errors -eq 0 -and $summary.completed -eq $requestedSessions -and $readinessFailures.Count -eq 0 -and $shutdownFailures.Count -eq 0 -and $runtimeFailures.Count -eq 0) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    workloadStartedUtc = if ($null -ne $workloadStartedUtc) { $workloadStartedUtc.ToString("o") } else { $null }
    workloadEndedUtc = if ($null -ne $workloadEndedUtc) { $workloadEndedUtc.ToString("o") } else { $null }
    runId = $provenance.runId
    provenanceStatus = if ($provenance.manifestBound) { "MANIFEST_BOUND" } else { "UNBOUND" }
    fixtureId = $provenance.fixtureId
    manifestSha256 = $provenance.manifestSha256
    database = $database
    dataRoot = $dataRoot
    bind = "127.0.0.1"
    port = 1143
    ports = $provenance.ports
    messageCount = 1000
    concurrency = $Concurrency
    waves = $Waves
    requestedSessions = $requestedSessions
    timeoutMilliseconds = $TimeoutMilliseconds
    postWorkloadSettleSeconds = $PostWorkloadSettleSeconds
    summary = $summary
    sqlConnectionSettings = $sqlConnectionSettings
    probeConfiguration = $probeConfiguration
    readinessFailures = @($readinessFailures)
    shutdownFailures = @($shutdownFailures)
    processBefore = $before
    processAfterImmediate = $afterImmediate
    processAfter = $after
    runtimeFailures = @($runtimeFailures)
    waveMetrics = $waveMetrics
    isolationPreflight = $preflight
    executableProvenance = $provenance.executableProvenance
    runStartAttestation = $runStartAttestation
    samples = @($results | ForEach-Object {
        [pscustomobject]@{
            wave = $_.Wave
            session = $_.Session
            ok = $_.Success
            timedOut = $_.TimedOut
            ms = $_.Milliseconds
            error = $_.Error
            searchResultValid = $_.SearchResultValid
            searchResultCount = $_.SearchResultCount
            searchExactSequence = $_.SearchExactSequence
            sortResultValid = $_.SortResultValid
            sortResultCount = $_.SortResultCount
            sortExactSequence = $_.SortExactSequence
            resultError = $_.ResultError
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
$csvSamples = $report.samples | ForEach-Object {
    [pscustomobject]@{
        runId = $report.runId
        provenanceStatus = $report.provenanceStatus
        fixtureId = $report.fixtureId
        manifestSha256 = $report.manifestSha256
        implementation = $report.implementation
        database = $report.database
        dataRoot = $report.dataRoot
        executableSha256 = $report.executableProvenance.sha256
        sqlMaxPoolSize = $report.sqlConnectionSettings.maxPoolSize
        sqlConnectionTimeoutSeconds = $report.sqlConnectionSettings.connectionTimeoutSeconds
        probeFanOut = $report.probeConfiguration.fanOut
        launchStaggerMilliseconds = $report.probeConfiguration.launchStaggerMilliseconds
        runStartAttestationStatus = if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.status } else { "UNBOUND" }
        runStartDataSha256 = if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.dataSha256 } else { $null }
        runStartMessageSha256 = if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.messageSha256 } else { $null }
        wave = $_.wave
        session = $_.session
        ok = $_.ok
        timedOut = $_.timedOut
        ms = $_.ms
        error = $_.error
        searchResultValid = $_.searchResultValid
        searchResultCount = $_.searchResultCount
        searchExactSequence = $_.searchExactSequence
        sortResultValid = $_.sortResultValid
        sortResultCount = $_.sortResultCount
        sortExactSequence = $_.sortExactSequence
    }
}
$csvSamples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
$markdown = @(
    "# Live concurrent IMAP benchmark",
    "",
    "Implementation: $($report.implementation)",
    "Status: $($report.status)",
    "Implementation: $($report.implementation)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Bind/port: $($report.bind):$($report.port)",
    "Corpus files: $($report.messageCount)",
    "Run ID: $($report.runId)",
    "Provenance: $($report.provenanceStatus)",
    "Fixture ID: $($report.fixtureId)",
    "Fixture manifest SHA-256: $($report.manifestSha256)",
    "Executable SHA-256: $($report.executableProvenance.sha256)",
    "SQL provider/server/database: $($report.sqlConnectionSettings.provider) / $($report.sqlConnectionSettings.server) / $($report.sqlConnectionSettings.database)",
    "SQL pooling/max pool/timeout: $($report.sqlConnectionSettings.pooling) / $($report.sqlConnectionSettings.maxPoolSize) / $($report.sqlConnectionSettings.connectionTimeoutSeconds) seconds",
    "IMAP probe fan-out: $($report.probeConfiguration.fanOut)",
    "Run-start attestation: $(if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.status } else { 'UNBOUND' })",
    "Run-start Data SHA-256: $(if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.dataSha256 } else { '' })",
    "Run-start message SHA-256: $(if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.messageSha256 } else { '' })",
    "Concurrency: $($report.concurrency)",
    "Waves / requested sessions: $($report.waves) / $($report.requestedSessions)",
    "Timeout: $($report.timeoutMilliseconds) ms",
    "Launch stagger: $($report.probeConfiguration.launchStaggerMilliseconds) ms per session index",
    "Post-workload settle: $($report.postWorkloadSettleSeconds) seconds",
    "",
    "| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Workload s | Throughput/s |",
    "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    "| $($summary.scenario) | $($summary.completed) | $($summary.successes) | $($summary.errors) | $($summary.timeouts) | $($summary.p50_ms) | $($summary.p95_ms) | $($summary.p99_ms) | $($summary.workload_seconds) | $($summary.throughput_sessions_per_second) |",
    "",
    "No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.",
    "COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed."
)
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

$summary | Format-Table -AutoSize
Write-Output "JSON: $jsonPath"
