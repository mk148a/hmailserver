param(
    [int]$Iterations = 25,
    [int]$DurationSeconds = 90,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$OutputDirectory = "",
    [string]$BenchmarkStagingRoot = "",
    [string]$BenchmarkDatabase = "",
    [string]$BenchmarkServiceExecutable = "",
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$serviceExe = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe"
$stagingRoot = "C:\hmail-perf-net10-ascii-20260810"
$database = "hmail_perf_net_sql_20260810_152708"
$argumentList = [string]$DurationSeconds

if ($Implementation -eq "cpp") {
    $serviceExe = "C:\hmail-perf-cpp-ascii-20260810\Bin\hMailServer.exe"
    $stagingRoot = "C:\hmail-perf-cpp-ascii-20260810"
    $database = "hmail_perf_cpp_sql_20260810_152708"
    $argumentList = "/Debug"
}

if (-not [string]::IsNullOrWhiteSpace($BenchmarkStagingRoot)) { $stagingRoot = [IO.Path]::GetFullPath($BenchmarkStagingRoot) }
if (-not [string]::IsNullOrWhiteSpace($BenchmarkDatabase)) { $database = $BenchmarkDatabase }
if (-not [string]::IsNullOrWhiteSpace($BenchmarkServiceExecutable)) { $serviceExe = [IO.Path]::GetFullPath($BenchmarkServiceExecutable) }

if ($database -notmatch '^hmail_perf_[a-z0-9_]+$') {
    throw "Refusing non-disposable benchmark database: $database"
}
if ([IO.Path]::GetFullPath($stagingRoot) -notmatch '(?i)^C:\\hmail-perf-') {
    throw "Refusing non-disposable benchmark root: $stagingRoot"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) {
    throw "Live listener host is missing: $serviceExe"
}
if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot "Data") -PathType Container)) {
    throw "Disposable Data directory is missing: $stagingRoot\Data"
}

function Read-UntilTag {
    param([IO.StreamReader]$Reader, [string]$Tag)

    $lines = [System.Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt 40; $index++) {
        $line = $Reader.ReadLine()
        if ($null -eq $line) {
            break
        }
        $lines.Add($line)
        if ($line.StartsWith($Tag, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
    }
    return $lines.ToArray()
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

function New-ClientResult {
    param([Diagnostics.Stopwatch]$Stopwatch, [bool]$Success, [string]$Error)

    $Stopwatch.Stop()
    [pscustomobject]@{
        ok = $Success
        ms = [math]::Round($Stopwatch.Elapsed.TotalMilliseconds, 3)
        error = $Error
    }
}

function Invoke-SmtpScenario {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 2525)
        $client.ReceiveTimeout = 3000
        $client.SendTimeout = 3000
        $reader = [IO.StreamReader]::new($client.GetStream())
        $writer = [IO.StreamWriter]::new($client.GetStream())
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true
        $greeting = $reader.ReadLine()
        $writer.WriteLine("EHLO perf.test")
        $ehlo = Read-UntilTag $reader "250 "
        $writer.WriteLine("QUIT")
        $quit = $reader.ReadLine()
        $reader.Dispose()
        $writer.Dispose()
        $client.Dispose()
        New-ClientResult $stopwatch ($greeting -like "220*" -and $ehlo.Count -gt 0 -and $quit -like "221*") $null
    }
    catch {
        New-ClientResult $stopwatch $false $_.Exception.Message
    }
}

function Invoke-ImapScenario {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 1143)
        $client.ReceiveTimeout = 5000
        $client.SendTimeout = 5000
        $reader = [IO.StreamReader]::new($client.GetStream())
        $writer = [IO.StreamWriter]::new($client.GetStream())
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true
        $greeting = $reader.ReadLine()
        $writer.WriteLine("a001 LOGIN test@perf.test test")
        $login = Read-UntilTag $reader "a001"
        $writer.WriteLine("a002 SELECT INBOX")
        $select = Read-UntilTag $reader "a002"
        $writer.WriteLine("a003 SEARCH TEXT needle")
        $search = Read-UntilTag $reader "a003"
        $writer.WriteLine("a004 SORT (DATE) UTF-8 ALL")
        $sort = Read-UntilTag $reader "a004"
        $writer.WriteLine("a005 LOGOUT")
        $logout = Read-UntilTag $reader "a005"
        $all = (($login + $select + $search + $sort + $logout) -join " ")
        $reader.Dispose()
        $writer.Dispose()
        $client.Dispose()
        New-ClientResult $stopwatch ($greeting -like "*OK*" -and $all -like "*OK*") $null
    }
    catch {
        New-ClientResult $stopwatch $false $_.Exception.Message
    }
}

function Invoke-Pop3Scenario {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 25110)
        $client.ReceiveTimeout = 5000
        $client.SendTimeout = 5000
        $reader = [IO.StreamReader]::new($client.GetStream())
        $writer = [IO.StreamWriter]::new($client.GetStream())
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true
        $greeting = $reader.ReadLine()
        $writer.WriteLine("USER test@perf.test")
        $user = $reader.ReadLine()
        $writer.WriteLine("PASS test")
        $password = $reader.ReadLine()
        $writer.WriteLine("STAT")
        $stat = $reader.ReadLine()
        $writer.WriteLine("LIST")
        while (($line = $reader.ReadLine()) -ne "." -and $null -ne $line) { }
        $writer.WriteLine("QUIT")
        $quit = $reader.ReadLine()
        $reader.Dispose()
        $writer.Dispose()
        $client.Dispose()
        New-ClientResult $stopwatch ($greeting -like "+OK*" -and $user -like "+OK*" -and $password -like "+OK*" -and $stat -like "+OK*" -and $quit -like "+OK*") $null
    }
    catch {
        New-ClientResult $stopwatch $false $_.Exception.Message
    }
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

function Get-ProcessMetrics {
    param([int]$ProcessId)

    $target = [Diagnostics.Process]::GetProcessById($ProcessId)
    try {
        $target.Refresh()
        return [pscustomobject]@{
            privateBytes = $target.PrivateMemorySize64
            handles = $target.HandleCount
            threads = $target.Threads.Count
        }
    }
    finally {
        $target.Dispose()
    }
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
    # The installed Application AppID rejects this listener-only benchmark host.
    # Keep COM enabled by default in production; this opt-in prevents the host
    # from stopping after the protocol listeners become ready.
    $env:HMAILSERVER_COM_LOCAL_SERVER_ENABLED = "false"
}

$process = $null
$startUtc = [DateTimeOffset]::UtcNow
$samples = [System.Collections.Generic.List[object]]::new()
$readinessFailures = @()
$shutdownFailures = @()
$before = $null
$after = $null
$preflight = $null
$provenance = $null

if ($Implementation -eq "cpp") {
    $preflight = Get-CppIsolationPreflight -TargetExecutable $serviceExe -ExpectedStagingRoot $stagingRoot -ExpectedDatabase $database
    $provenance = Get-CppExecutableProvenance -TargetExecutable $serviceExe
    $readinessFailures = @($preflight.failures)
}

if ($null -eq $preflight -or $preflight.passed) {
    $process = Start-Process -FilePath $serviceExe -ArgumentList $argumentList -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
}
try {
    if ($null -ne $process) {
        $readinessFailures = @(Wait-ForReadiness $process.Id)
        if ($readinessFailures.Count -eq 0) {
            Start-Sleep -Milliseconds 500
            $before = Get-ProcessMetrics $process.Id
            foreach ($scenario in @("smtp", "imap", "pop3")) {
                for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
                    $result = switch ($scenario) {
                        "smtp" { Invoke-SmtpScenario }
                        "imap" { Invoke-ImapScenario }
                        "pop3" { Invoke-Pop3Scenario }
                    }
                    $samples.Add([pscustomobject]@{
                        scenario = $scenario
                        iteration = $iteration
                        ok = $result.ok
                        ms = $result.ms
                        error = $result.error
                    })
                }
            }
            if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
                $after = Get-ProcessMetrics $process.Id
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

$summary = foreach ($scenario in @("smtp", "imap", "pop3")) {
    $rows = @($samples | Where-Object scenario -eq $scenario)
    $successful = @($rows | Where-Object ok)
    [pscustomobject]@{
        scenario = $scenario
        iterations = $rows.Count
        successes = $successful.Count
        errors = $rows.Count - $successful.Count
        p50_ms = Get-Percentile ($successful | ForEach-Object ms) 50
        p95_ms = Get-Percentile ($successful | ForEach-Object ms) 95
        p99_ms = Get-Percentile ($successful | ForEach-Object ms) 99
    }
}

$report = [pscustomobject]@{
    schema = "live-protocol-v1"
    implementation = $Implementation
    status = if (($summary | Where-Object errors -gt 0).Count -eq 0 -and $readinessFailures.Count -eq 0 -and $shutdownFailures.Count -eq 0) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $database
    dataRoot = Join-Path $stagingRoot "Data"
    bind = "127.0.0.1"
    ports = "SMTP 2525, IMAP 1143, POP3 25110"
    messageCount = 1000
    summary = $summary
    readinessFailures = @($readinessFailures)
    shutdownFailures = @($shutdownFailures)
    processBefore = if ($null -ne $before) { @{ privateBytes = $before.privateBytes; handles = $before.handles; threads = $before.threads } } else { $null }
    processAfter = if ($null -ne $after) { @{ privateBytes = $after.privateBytes; handles = $after.handles; threads = $after.threads } } else { $null }
    isolationPreflight = $preflight
    executableProvenance = $provenance
    samples = $samples
    comHostedService = if ($Implementation -eq "net10") { "not started; installed AppID preserved" } else { "legacy /Debug path; AppID hash checked separately" }
    productionSafety = if ($Implementation -eq "cpp") {
        "loopback-only; legacy registry/config resolution and executable provenance were preflighted; disposable SQL/Data roots required"
    } else {
        "service stopped/disabled; production DB/Data not used"
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "net10-live-protocol.json"
$csvPath = Join-Path $OutputDirectory "net10-live-protocol.csv"
$markdownPath = Join-Path $OutputDirectory "net10-live-protocol.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
$markdown = @(
    "# .NET 10 live protocol benchmark",
    "",
    "Status: $($report.status)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Bind/ports: $($report.bind) / $($report.ports)",
    "Corpus files: $($report.messageCount)",
    "",
    "| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |",
    "| --- | ---: | ---: | ---: | ---: | ---: |"
)
$markdown += $summary | ForEach-Object { "| $($_.scenario) | $($_.successes) | $($_.errors) | $($_.p50_ms) | $($_.p95_ms) | $($_.p99_ms) |" }
$markdown += "", "COM local-server registration was intentionally omitted because the installed AppID rejects the rewrite caller with 0x80004015.", "This is live .NET 10 listener evidence, not a C++ comparison; no speed-up ratio is valid."
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

$summary | Format-Table -AutoSize
Write-Output "JSON: $jsonPath"
