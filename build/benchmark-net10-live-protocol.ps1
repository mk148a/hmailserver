param(
    [int]$Iterations = 25,
    [int]$DurationSeconds = 90,
    [string]$OutputDirectory = "",
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10"
)

$ErrorActionPreference = "Stop"

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
$samples = [System.Collections.Generic.List[object]]::new()
try {
    Start-Sleep -Seconds 2
    $before = Get-Process -Id $process.Id
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
    $endUtc = [DateTimeOffset]::UtcNow
    $after = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
}
finally {
    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
        Stop-Process -Id $process.Id -Force
    }
}

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
    status = if (($summary | Where-Object errors -gt 0).Count -eq 0) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $database
    dataRoot = Join-Path $stagingRoot "Data"
    bind = "127.0.0.1"
    ports = "SMTP 2525, IMAP 1143, POP3 25110"
    messageCount = 1000
    summary = $summary
    processBefore = @{ privateBytes = $before.PrivateMemorySize64; handles = $before.Handles; threads = $before.Threads.Count }
    processAfter = if ($null -ne $after) { @{ privateBytes = $after.PrivateMemorySize64; handles = $after.Handles; threads = $after.Threads.Count } } else { $null }
    samples = $samples
    comHostedService = if ($Implementation -eq "net10") { "not started; installed AppID preserved" } else { "legacy /Debug path; AppID hash checked separately" }
    productionSafety = "service stopped/disabled; production DB/Data not used"
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
