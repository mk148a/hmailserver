param(
    [ValidateRange(1, 100000)]
    [int]$MessageCount = 100,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$OutputDirectory = "",
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$serviceExe = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe"
$stagingRoot = "C:\hmail-perf-net10-ascii-20260810"
$database = "hmail_perf_net_sql_20260810_152708"
$argumentList = "90"

if ($Implementation -eq "cpp") {
    $serviceExe = "C:\hmail-perf-cpp-ascii-20260810\Bin\hMailServer.exe"
    $stagingRoot = "C:\hmail-perf-cpp-ascii-20260810"
    $database = "hmail_perf_cpp_sql_20260810_152708"
    $argumentList = "/Debug"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260811\$Implementation-smtp-acceptance"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) {
    throw "The isolated benchmark executable is missing: $serviceExe"
}

$dataRoot = Join-Path $stagingRoot "Data"
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
    throw "The isolated benchmark Data root is missing: $dataRoot"
}

function Read-SmtpResponse {
    param([IO.StreamReader]$Reader)

    $lines = [System.Collections.Generic.List[string]]::new()
    do {
        $line = $Reader.ReadLine()
        if ($null -eq $line) {
            break
        }
        $lines.Add($line)
    } while ($line -match '^[0-9]{3}-')
    return $lines.ToArray()
}

function Get-ListenerState {
    @(
        Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort 2525 -ErrorAction SilentlyContinue
    )
}

function Wait-ForReadiness {
    param([int]$ProcessId)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    $lastFailure = ""
    do {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            $lastFailure = "Launched process $ProcessId exited before SMTP readiness."
        }
        elseif ((Get-ListenerState).Count -eq 0) {
            $lastFailure = "SMTP listener is not listening on 127.0.0.1:2525."
        }
        else {
            try {
                $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 2525)
                $client.ReceiveTimeout = 3000
                $reader = [IO.StreamReader]::new($client.GetStream())
                $banner = $reader.ReadLine()
                $reader.Dispose()
                $client.Dispose()
                if ($banner -like "220*") {
                    return @()
                }
                $lastFailure = "Unexpected SMTP banner: [$banner]"
            }
            catch {
                $lastFailure = $_.Exception.Message
            }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    return @($lastFailure)
}

function Wait-ForShutdown {
    param([int]$ProcessId)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    do {
        $processAlive = $null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
        if (-not $processAlive -and (Get-ListenerState).Count -eq 0) {
            return @()
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    return @("SMTP process or listener remained after shutdown.")
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
    return [math]::Round(([double]$sorted[$lower] + (($sorted[$upper] - $sorted[$lower]) * ($rank - $lower))), 3)
}

function Invoke-SmtpAcceptance {
    param([int]$Sequence)

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $client = $null
    $reader = $null
    $writer = $null
    $responses = [System.Collections.Generic.List[string]]::new()
    try {
        $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 2525)
        $client.ReceiveTimeout = 5000
        $client.SendTimeout = 5000
        $reader = [IO.StreamReader]::new($client.GetStream())
        $writer = [IO.StreamWriter]::new($client.GetStream())
        $writer.NewLine = "`r`n"
        $writer.AutoFlush = $true

        $greeting = $reader.ReadLine()
        $responses.Add("greeting: $greeting")
        $writer.WriteLine("EHLO perf.test")
        $ehlo = Read-SmtpResponse $reader
        $responses.Add("ehlo: " + ($ehlo -join " | "))
        $writer.WriteLine("MAIL FROM:<sender$Sequence@perf.test>")
        $mailFrom = $reader.ReadLine()
        $responses.Add("mail-from: $mailFrom")
        $writer.WriteLine("RCPT TO:<test@perf.test>")
        $recipient = $reader.ReadLine()
        $responses.Add("recipient: $recipient")
        $writer.WriteLine("DATA")
        $dataReady = $reader.ReadLine()
        $responses.Add("data: $dataReady")
        $writer.WriteLine("From: sender$Sequence@perf.test")
        $writer.WriteLine("To: test@perf.test")
        $writer.WriteLine("Subject: paired acceptance $Sequence")
        $writer.WriteLine("Message-ID: <paired-$Sequence@perf.test>")
        $writer.WriteLine("")
        $writer.WriteLine("paired SMTP acceptance message $Sequence")
        $writer.WriteLine(".")
        $accepted = $reader.ReadLine()
        $responses.Add("accepted: $accepted")
        $writer.WriteLine("QUIT")
        $quit = $reader.ReadLine()
        $responses.Add("quit: $quit")

        $ok = ($greeting -like "220*" -and $ehlo.Count -gt 0 -and $mailFrom -like "250*" -and $recipient -like "250*" -and $dataReady -like "354*" -and $accepted -like "250*" -and $quit -like "221*")
        [pscustomobject]@{
            ok = [bool]$ok
            ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            error = if ($ok) { $null } else { "SMTP acceptance response sequence was incomplete." }
            responses = $responses.ToArray()
        }
    }
    catch {
        [pscustomobject]@{
            ok = $false
            ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            error = $_.Exception.Message
            responses = $responses.ToArray()
        }
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        if ($null -ne $writer) { $writer.Dispose() }
        if ($null -ne $client) { $client.Dispose() }
        $stopwatch.Stop()
    }
}

if ($Implementation -eq "net10") {
    $env:HMAILSERVER_SQLSERVER_CONNECTION = "Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True;"
    $env:HMAILSERVER_DATA_DIRECTORY = $dataRoot
    $env:HMAILSERVER_INITIALIZATION_FILE = Join-Path $stagingRoot "hMailServer.ini"
    $env:HMAILSERVER_SMTP_ENABLED = "true"
    $env:HMAILSERVER_SMTP_BIND_ADDRESS = "127.0.0.1"
    $env:HMAILSERVER_SMTP_PORT = "2525"
    $env:HMAILSERVER_IMAP_ENABLED = "false"
    $env:HMAILSERVER_POP3_ENABLED = "false"
    $env:HMAILSERVER_EXTERNAL_FETCH_ENABLED = "false"
    $env:HMAILSERVER_COM_LOCAL_SERVER_ENABLED = "false"
}

$process = Start-Process -FilePath $serviceExe -ArgumentList $argumentList -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
$startUtc = [DateTimeOffset]::UtcNow
$readinessFailures = @()
$shutdownFailures = @()
$samples = [System.Collections.Generic.List[object]]::new()
$before = $null
$after = $null
try {
    $readinessFailures = @(Wait-ForReadiness $process.Id)
    if ($readinessFailures.Count -eq 0) {
        $before = Get-Process -Id $process.Id
        for ($sequence = 1; $sequence -le $MessageCount; $sequence++) {
            $result = Invoke-SmtpAcceptance $sequence
            $samples.Add([pscustomobject]@{
                scenario = "smtp-message-acceptance"
                sequence = $sequence
                ok = $result.ok
                ms = $result.ms
                error = $result.error
                responses = $result.responses
            })
        }
        $after = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    }
}
finally {
    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
        try { Stop-Process -Id $process.Id -Force } catch { $shutdownFailures += $_.Exception.Message }
    }
    $shutdownFailures += @(Wait-ForShutdown $process.Id)
}
$endUtc = [DateTimeOffset]::UtcNow

$successful = @($samples | Where-Object ok)
$durationSeconds = ($endUtc - $startUtc).TotalSeconds
$report = [pscustomobject]@{
    schema = "live-smtp-message-acceptance-v1"
    implementation = $Implementation
    status = if ($readinessFailures.Count -eq 0 -and $shutdownFailures.Count -eq 0 -and $successful.Count -eq $MessageCount) { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    database = $database
    dataRoot = $dataRoot
    bind = "127.0.0.1"
    port = 2525
    requestedMessages = $MessageCount
    acceptedMessages = $successful.Count
    errors = $MessageCount - $successful.Count
    p50_ms = Get-Percentile ($successful | ForEach-Object ms) 50
    p95_ms = Get-Percentile ($successful | ForEach-Object ms) 95
    p99_ms = Get-Percentile ($successful | ForEach-Object ms) 99
    throughput_messages_per_second = if ($durationSeconds -gt 0) { [math]::Round($successful.Count / $durationSeconds, 3) } else { 0 }
    readinessFailures = @($readinessFailures)
    shutdownFailures = @($shutdownFailures)
    processBefore = if ($null -ne $before) { @{ privateBytes = $before.PrivateMemorySize64; handles = $before.Handles; threads = $before.Threads.Count } } else { $null }
    processAfter = if ($null -ne $after) { @{ privateBytes = $after.PrivateMemorySize64; handles = $after.Handles; threads = $after.Threads.Count } } else { $null }
    samples = $samples
    productionSafety = "loopback-only; disposable SQL/Data roots required; production service/DB/Data are not used"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory "$Implementation-smtp-message-acceptance.json"
$csvPath = Join-Path $OutputDirectory "$Implementation-smtp-message-acceptance.csv"
$markdownPath = Join-Path $OutputDirectory "$Implementation-smtp-message-acceptance.md"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$samples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
$markdown = @(
    "# SMTP message acceptance benchmark",
    "",
    "Status: $($report.status)",
    "Implementation: $($report.implementation)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Bind/port: $($report.bind):$($report.port)",
    "Requested/accepted: $($report.requestedMessages) / $($report.acceptedMessages)",
    "p50/p95/p99: $($report.p50_ms) / $($report.p95_ms) / $($report.p99_ms) ms",
    "Throughput: $($report.throughput_messages_per_second) messages/s",
    "",
    "This is a loopback disposable-target measurement. A C++/.NET 10 ratio is valid only when both implementations pass the same readiness, SQL/Data, message, and cleanup gates."
)
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

$report | Select-Object status, implementation, requestedMessages, acceptedMessages, errors, p50_ms, p95_ms, p99_ms, throughput_messages_per_second | Format-List
Write-Output "JSON: $jsonPath"
Write-Output "CSV: $csvPath"
Write-Output "Markdown: $markdownPath"
