param(
    [ValidateRange(1, 100000)]
    [int]$MessageCount = 100,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [ValidateRange(1, 60)]
    [int]$PostAcceptanceTimeoutSeconds = 10,
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
if ($database -notmatch '^hmail_perf_(net|cpp)_sql_[a-z0-9_]+$') {
    throw "Refusing non-disposable benchmark database: $database"
}
if ([IO.Path]::GetFullPath($stagingRoot) -notmatch '(?i)^C:\\hmail-perf-(net10|cpp)-') {
    throw "Refusing non-disposable benchmark Data root: $stagingRoot"
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

function Get-SqlFixtureSnapshot {
    param([string]$Database, [string]$DataRoot)

    try {
        $dataRootSql = $DataRoot.Replace("'", "''")
        $query = @"
SET NOCOUNT ON;
SELECT
    (SELECT COUNT_BIG(*) FROM hm_messages),
    (SELECT COUNT_BIG(*) FROM hm_messages WHERE messagetype = 1),
    (SELECT COUNT_BIG(*) FROM hm_messages WHERE messagetype = 2),
    (SELECT COUNT_BIG(*) FROM hm_message_metadata),
    (SELECT COUNT_BIG(*) FROM hm_messagerecipients),
    (SELECT COUNT_BIG(*) FROM hm_tcpipports),
    (SELECT COUNT_BIG(*) FROM hm_tcpipports WHERE
        (portprotocol = 1 AND portnumber = 2525 AND portaddress1 = 2130706433) OR
        (portprotocol = 5 AND portnumber = 1143 AND portaddress1 = 2130706433) OR
        (portprotocol = 3 AND portnumber = 25110 AND portaddress1 = 2130706433)),
    (SELECT COUNT_BIG(*) FROM hm_domains WHERE domainname = N'perf.test' AND domainactive <> 0),
    (SELECT COUNT_BIG(*) FROM hm_accounts WHERE accountaddress = N'test@perf.test' AND accountactive <> 0),
    (SELECT COUNT_BIG(*) FROM hm_imapfolders WHERE folderaccountid = 1 AND folderparentid = -1 AND LOWER(foldername) = N'inbox'),
    (SELECT COUNT_BIG(*) FROM hm_messages WHERE LEFT(messagefilename, LEN(N'$dataRootSql')) = N'$dataRootSql'),
    (SELECT COUNT_BIG(*) FROM hm_messages WHERE LEFT(messagefilename, LEN(N'$dataRootSql')) <> N'$dataRootSql');
"@
        $lines = @(sqlcmd -S localhost -E -d $Database -W -s '|' -h-1 -b -Q $query)
        if ($LASTEXITCODE -ne 0 -or $lines.Count -ne 1) {
            throw "sqlcmd returned no single fixture snapshot row (exit code $LASTEXITCODE)."
        }
        $parts = $lines[0].Trim().Split('|')
        if ($parts.Count -ne 12) {
            throw "sqlcmd fixture snapshot returned $($parts.Count) fields instead of 12."
        }
        $tcpipPorts = [int64]$parts[5].Trim()
        $matchingLoopbackPorts = [int64]$parts[6].Trim()
        $domainMatches = [int64]$parts[7].Trim()
        $accountMatches = [int64]$parts[8].Trim()
        $inboxMatches = [int64]$parts[9].Trim()
        $messageFilesWithinDataRoot = [int64]$parts[10].Trim()
        $messageFilesOutsideDataRoot = [int64]$parts[11].Trim()
        [pscustomobject]@{
            available = $true
            messages = [int64]$parts[0].Trim()
            queuedMessages = [int64]$parts[1].Trim()
            deliveredMessages = [int64]$parts[2].Trim()
            metadata = [int64]$parts[3].Trim()
            recipients = [int64]$parts[4].Trim()
            tcpipPorts = $tcpipPorts
            matchingLoopbackPorts = $matchingLoopbackPorts
            domainMatches = $domainMatches
            accountMatches = $accountMatches
            inboxMatches = $inboxMatches
            messageFilesWithinDataRoot = $messageFilesWithinDataRoot
            messageFilesOutsideDataRoot = $messageFilesOutsideDataRoot
            loopbackFixtureValid = ($tcpipPorts -eq 3 -and $matchingLoopbackPorts -eq 3)
            fixtureValid = ($tcpipPorts -eq 3 -and $matchingLoopbackPorts -eq 3 -and $domainMatches -eq 1 -and $accountMatches -eq 1 -and $inboxMatches -eq 1 -and $messageFilesOutsideDataRoot -eq 0)
            error = $null
        }
    }
    catch {
        [pscustomobject]@{
            available = $false
            messages = $null
            queuedMessages = $null
            deliveredMessages = $null
            metadata = $null
            recipients = $null
            tcpipPorts = $null
            matchingLoopbackPorts = $null
            domainMatches = $null
            accountMatches = $null
            inboxMatches = $null
            messageFilesWithinDataRoot = $null
            messageFilesOutsideDataRoot = $null
            loopbackFixtureValid = $false
            fixtureValid = $false
            error = $_.Exception.Message
        }
    }
}

function Get-DataFixtureSnapshot {
    param([string]$Root)

    try {
        $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
        $entries = @(
            Get-ChildItem -LiteralPath $fullRoot -Recurse -File |
                ForEach-Object {
                    $relative = $_.FullName.Substring($fullRoot.Length).TrimStart('\')
                    "$relative|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
                } |
                Sort-Object
        )
        $bytes = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n"))
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            $digest = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
        }
        finally {
            $sha.Dispose()
        }
        [pscustomobject]@{
            available = $true
            fileCount = $entries.Count
            sha256 = $digest
            error = $null
        }
    }
    catch {
        [pscustomobject]@{
            available = $false
            fileCount = $null
            sha256 = $null
            error = $_.Exception.Message
        }
    }
}

function Get-FixtureIdentity {
    param([object]$Sql, [object]$Data, [int]$RequestedMessages)

    $canonical = [pscustomobject]@{
        requestedMessages = $RequestedMessages
        sql = $Sql
        data = $Data
        protocolPorts = "SMTP:2525;IMAP:1143;POP3:25110;bind:127.0.0.1"
    } | ConvertTo-Json -Compress -Depth 8
    $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Wait-ForAcceptedMessageState {
    param([int64]$ExpectedNewMessages)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($PostAcceptanceTimeoutSeconds)
    $last = $null
    do {
        $last = Get-SqlFixtureSnapshot -Database $database -DataRoot $dataRoot
        if ($last.available -and $sqlBefore.available) {
            $newMessages = $last.messages - $sqlBefore.messages
            $newQueued = $last.queuedMessages - $sqlBefore.queuedMessages
            $newDelivered = $last.deliveredMessages - $sqlBefore.deliveredMessages
            if ($newMessages -ge $ExpectedNewMessages -and ($newQueued + $newDelivered) -ge $ExpectedNewMessages) {
                return [pscustomobject]@{
                    observed = $true
                    expectedNewMessages = $ExpectedNewMessages
                    messages = $newMessages
                    queuedMessages = $newQueued
                    deliveredMessages = $newDelivered
                    snapshot = $last
                }
            }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    [pscustomobject]@{
        observed = $false
        expectedNewMessages = $ExpectedNewMessages
        messages = if ($null -ne $last -and $last.available -and $sqlBefore.available) { $last.messages - $sqlBefore.messages } else { $null }
        queuedMessages = if ($null -ne $last -and $last.available -and $sqlBefore.available) { $last.queuedMessages - $sqlBefore.queuedMessages } else { $null }
        deliveredMessages = if ($null -ne $last -and $last.available -and $sqlBefore.available) { $last.deliveredMessages - $sqlBefore.deliveredMessages } else { $null }
        snapshot = $last
    }
}

. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")

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

$process = $null
$startUtc = [DateTimeOffset]::UtcNow
$readinessFailures = @()
$shutdownFailures = @()
$samples = [System.Collections.Generic.List[object]]::new()
$acceptedStates = [System.Collections.Generic.List[object]]::new()
$before = $null
$after = $null
$preflight = $null
$provenance = $null

$sqlBefore = Get-SqlFixtureSnapshot -Database $database -DataRoot $dataRoot
$dataBefore = Get-DataFixtureSnapshot -Root $dataRoot
$fixtureIdentity = Get-FixtureIdentity -Sql $sqlBefore -Data $dataBefore -RequestedMessages $MessageCount

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
            $before = Get-Process -Id $process.Id
            for ($sequence = 1; $sequence -le $MessageCount; $sequence++) {
                $result = Invoke-SmtpAcceptance $sequence
                if ($result.ok) {
                    $acceptedStates.Add((Wait-ForAcceptedMessageState -ExpectedNewMessages ($acceptedStates.Count + 1)))
                }
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
}
finally {
    if ($null -ne $process -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        try { Stop-Process -Id $process.Id -Force } catch { $shutdownFailures += $_.Exception.Message }
    }
    if ($null -ne $process) {
        $shutdownFailures += @(Wait-ForShutdown $process.Id)
    }
}
$endUtc = [DateTimeOffset]::UtcNow
$sqlAfter = Get-SqlFixtureSnapshot -Database $database -DataRoot $dataRoot
$dataAfter = Get-DataFixtureSnapshot -Root $dataRoot

$successful = @($samples | Where-Object ok)
$durationSeconds = ($endUtc - $startUtc).TotalSeconds
$postRunAccounting = [pscustomobject]@{
    sqlAvailable = $sqlBefore.available -and $sqlAfter.available
    dataAvailable = $dataBefore.available -and $dataAfter.available
    fixtureValidBefore = $sqlBefore.fixtureValid
    fixtureValidAfter = $sqlAfter.fixtureValid
    messageRowDelta = if ($sqlBefore.available -and $sqlAfter.available) { $sqlAfter.messages - $sqlBefore.messages } else { $null }
    metadataRowDelta = if ($sqlBefore.available -and $sqlAfter.available) { $sqlAfter.metadata - $sqlBefore.metadata } else { $null }
    recipientRowDelta = if ($sqlBefore.available -and $sqlAfter.available) { $sqlAfter.recipients - $sqlBefore.recipients } else { $null }
    dataFileDelta = if ($dataBefore.available -and $dataAfter.available) { $dataAfter.fileCount - $dataBefore.fileCount } else { $null }
    acceptedStatesObserved = @($acceptedStates | Where-Object observed).Count
    valid = ($sqlBefore.available -and $sqlAfter.available -and $dataBefore.available -and $dataAfter.available -and $sqlBefore.fixtureValid -and $sqlAfter.fixtureValid -and (($sqlAfter.messages - $sqlBefore.messages) -ge $successful.Count) -and (@($acceptedStates | Where-Object observed).Count -eq $successful.Count))
}
$report = [pscustomobject]@{
    schema = "live-smtp-message-acceptance-v1"
    implementation = $Implementation
    status = if ($readinessFailures.Count -eq 0 -and $shutdownFailures.Count -eq 0 -and $successful.Count -eq $MessageCount -and $postRunAccounting.valid) { "PASS" } else { "FAIL" }
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
    isolationPreflight = $preflight
    executableProvenance = $provenance
    fixture = [pscustomobject]@{
        identity = $fixtureIdentity
        database = $database
        dataRoot = $dataRoot
        messageCountRequested = $MessageCount
        before = [pscustomobject]@{ sql = $sqlBefore; data = $dataBefore }
        after = [pscustomobject]@{ sql = $sqlAfter; data = $dataAfter }
    }
    postRunAccounting = $postRunAccounting
    acceptedMessageStates = @($acceptedStates)
    samples = $samples
    productionSafety = if ($Implementation -eq "cpp") {
        "loopback-only; legacy registry/config resolution was preflighted; disposable SQL/Data roots required"
    } else {
        "loopback-only; disposable SQL/Data roots required; production service/DB/Data are not used"
    }
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
    "Fixture identity: $($report.fixture.identity)",
    "Fixture valid before/after: $($report.fixture.before.sql.fixtureValid) / $($report.fixture.after.sql.fixtureValid)",
    "Post-run accounting: $($report.postRunAccounting.valid); message/data deltas $($report.postRunAccounting.messageRowDelta) / $($report.postRunAccounting.dataFileDelta)",
    "",
    "This is a loopback disposable-target measurement. A C++/.NET 10 ratio is valid only when both implementations pass the same readiness, SQL/Data, message, and cleanup gates."
)
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

$report | Select-Object status, implementation, requestedMessages, acceptedMessages, errors, p50_ms, p95_ms, p99_ms, throughput_messages_per_second | Format-List
Write-Output "JSON: $jsonPath"
Write-Output "CSV: $csvPath"
Write-Output "Markdown: $markdownPath"
