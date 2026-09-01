param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureManifest,
    [Parameter(Mandatory = $true)]
    [string]$Net10EvidencePath,
    [string]$OutputDirectory = "",
    [string]$ServiceName = "",
    [ValidateRange(25000, 29999)]
    [int]$SinkPort = 26045,
    [ValidateRange(10, 300)]
    [int]$TimeoutSeconds = 90,
    [switch]$Recovery,
    [switch]$MixedRecipients
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")
. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")

function Invoke-SqlStrict {
    param([string]$Database, [string]$Query)
    $result = @(& sqlcmd.exe -S localhost -E -b -d $Database -Q $Query 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed for disposable database '$Database': $($result -join ' ')" }
    return $result
}

function Get-SqlScalar {
    param([string]$Database, [string]$Query)
    $result = (@(& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -Q $Query 2>&1) -join " ").Trim()
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd scalar query failed for disposable database '$Database'." }
    return $result
}

function Get-ServiceRecord {
    param([string]$Name)
    $service = Get-CimInstance Win32_Service -Filter "Name='$Name'" -ErrorAction SilentlyContinue
    if ($null -eq $service) { return $null }
    [pscustomobject]@{
        name = [string]$service.Name
        state = [string]$service.State
        processId = [int]$service.ProcessId
        startName = [string]$service.StartName
        pathName = [string]$service.PathName
    }
}

function Get-SinkJobSnapshot {
    param([System.Management.Automation.Job]$Job, [int]$Port, [string]$ReadyPath, [string]$FirstStatePath, [string]$StatePath)
    $currentJob = if ($null -ne $Job) { Get-Job -Id $Job.Id -ErrorAction SilentlyContinue } else { $null }
    $reason = if ($null -ne $currentJob) { @($currentJob.ChildJobs | ForEach-Object { $_.JobStateInfo.Reason } | Where-Object { $null -ne $_ } | Select-Object -First 1) } else { @() }
    $errors = if ($null -ne $currentJob) { @($currentJob.ChildJobs | ForEach-Object { $_.Error | ForEach-Object { $_.ToString() } } | Select-Object -First 4) } else { @() }
    $listener = @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1)
    [ordered]@{
        present = $null -ne $currentJob
        state = if ($null -ne $currentJob) { [string]$currentJob.State } else { $null }
        hasMoreData = if ($null -ne $currentJob) { [bool]$currentJob.HasMoreData } else { $false }
        childStates = @($currentJob.ChildJobs | ForEach-Object { [string]$_.State })
        failureType = if ($reason.Count -gt 0) { $reason[0].GetType().FullName } else { $null }
        errors = $errors
        listenerReady = $listener.Count -gt 0
        readyMarkerPresent = Test-Path -LiteralPath $ReadyPath -PathType Leaf
        firstStatePresent = Test-Path -LiteralPath $FirstStatePath -PathType Leaf
        statePresent = Test-Path -LiteralPath $StatePath -PathType Leaf
    }
}

function Get-SinkJobFailureMessage {
    param([System.Management.Automation.Job]$Job, [string]$ExpectedStatePath = "")
    $snapshot = Get-SinkJobSnapshot -Job $Job -Port $SinkPort -ReadyPath $sinkReadyPath -FirstStatePath $sinkFirstStatePath -StatePath $sinkStatePath
    if ($snapshot.state -in @('Failed', 'Stopped', 'Disconnected')) {
        $details = if ($snapshot.errors.Count -gt 0) { ($snapshot.errors -join ' | ') } else { 'no error stream details' }
        return "Disposable SMTP sink Start-Job failed (state=$($snapshot.state); details=$details)."
    }
    if ($snapshot.state -eq 'Completed' -and -not [string]::IsNullOrWhiteSpace($ExpectedStatePath) -and -not (Test-Path -LiteralPath $ExpectedStatePath -PathType Leaf)) {
        $details = if ($snapshot.errors.Count -gt 0) { ($snapshot.errors -join ' | ') } else { 'no error stream details' }
        return "Disposable SMTP sink ended before expected state (details=$details)."
    }
    return $null
}

function Wait-SinkReady {
    param([System.Management.Automation.Job]$Job, [int]$Port, [string]$ReadyPath, [DateTime]$Deadline)
    do {
        $failure = Get-SinkJobFailureMessage -Job $Job
        if ($null -ne $failure) { throw $failure }
        if ((Test-Path -LiteralPath $ReadyPath -PathType Leaf) -and @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort $Port -ErrorAction SilentlyContinue).Count -gt 0) { return }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $Deadline)
    throw "Disposable SMTP sink did not become ready before timeout."
}

function Wait-SinkStateFile {
    param([System.Management.Automation.Job]$Job, [int]$Port, [string]$StatePath, [DateTime]$Deadline, [string]$TimeoutMessage)
    do {
        $failure = Get-SinkJobFailureMessage -Job $Job -ExpectedStatePath $StatePath
        if ($null -ne $failure) { throw $failure }
        if (Test-Path -LiteralPath $StatePath -PathType Leaf) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $Deadline)
    throw $TimeoutMessage
}

function Get-DisposableSqlRetrySnapshot {
    param([string]$Database, [string]$MessageFromSql)
    $snapshot = [ordered]@{
        available = $false
        messageCount = $null
        messageType = $null
        locked = $null
        retryCount = $null
        nextTryTimePresent = $null
        recipientCount = $null
        errorType = $null
    }
    try {
        $query = "SET NOCOUNT ON; SELECT COUNT_BIG(*),MAX(CAST(messagetype AS int)),MAX(CAST(messagelocked AS int)),MAX(messagecurnooftries),CASE WHEN MAX(messagenexttrytime) IS NULL THEN 0 ELSE 1 END,COUNT_BIG(r.recipientmessageid) FROM hm_messages m LEFT JOIN hm_messagerecipients r ON r.recipientmessageid=m.messageid WHERE m.messagefrom=N'$MessageFromSql';"
        $row = @(& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -s '|' -Q $query 2>&1) | Where-Object { $_ -match '\|' } | Select-Object -Last 1
        if ($LASTEXITCODE -ne 0) { throw [InvalidOperationException]::new('sqlcmd failed') }
        $parts = ([string]$row).Trim().Split('|')
        if ($parts.Count -ne 6) { throw [System.IO.InvalidDataException]::new('unexpected SQL snapshot shape') }
        $snapshot.messageCount = [int64]$parts[0].Trim()
        $snapshot.messageType = [int]$parts[1].Trim()
        $snapshot.locked = [int]$parts[2].Trim()
        $snapshot.retryCount = [int]$parts[3].Trim()
        $snapshot.nextTryTimePresent = [int]$parts[4].Trim() -eq 1
        $snapshot.recipientCount = [int64]$parts[5].Trim()
        $snapshot.available = $true
    }
    catch {
        $snapshot.errorType = $_.Exception.GetType().FullName
    }
    return $snapshot
}

function Get-DataFileSnapshot {
    param([string]$Path)
    try {
        $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
        [ordered]@{
            exists = $null -ne $item -and $item.PSIsContainer -eq $false
            sizeBytes = if ($null -ne $item -and $item.PSIsContainer -eq $false) { [int64]$item.Length } else { $null }
            contentCaptured = $false
        }
    }
    catch {
        [ordered]@{ exists = $false; sizeBytes = $null; contentCaptured = $false; errorType = $_.Exception.GetType().FullName }
    }
}

function Write-TimeoutDiagnostic {
    param([string]$Path, [string]$ReasonCode, [string]$ServiceName, [int]$Port, [System.Management.Automation.Job]$Job, [string]$ReadyPath, [string]$FirstStatePath, [string]$StatePath, [string]$Database, [string]$MessageFromSql, [string]$DataPath)
    $service = Get-ServiceRecord $ServiceName
    $diagnostic = [ordered]@{
        schema = 'paired-cpp-net10-tcp451-timeout-diagnostic-v1'
        generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        reasonCode = $ReasonCode
        service = [ordered]@{
            present = $null -ne $service
            state = if ($null -ne $service) { $service.state } else { $null }
            processId = if ($null -ne $service) { $service.processId } else { 0 }
            startName = if ($null -ne $service) { $service.startName } else { $null }
        }
        sinkJob = Get-SinkJobSnapshot -Job $Job -Port $Port -ReadyPath $ReadyPath -FirstStatePath $FirstStatePath -StatePath $StatePath
        sql = Get-DisposableSqlRetrySnapshot -Database $Database -MessageFromSql $MessageFromSql
        dataFile = Get-DataFileSnapshot -Path $DataPath
    }
    $diagnostic | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding UTF8
    return $Path
}

function Assert-DisposableFixture {
    param($Fixture)
    if ($Fixture.database -notmatch '^hmail_perf_pair_cpp_[a-z0-9_]+$') { throw "C++ fixture database is not disposable." }
    if ($Fixture.dataRoot -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+\\cpp\\Data$') { throw "C++ Data root is outside the disposable fixture boundary." }
    if ($Fixture.executable -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+\\cpp\\Bin\\hMailServer\.exe$') { throw "C++ executable is outside the disposable fixture boundary." }
    if (-not (Test-Path -LiteralPath $Fixture.executable -PathType Leaf)) { throw "C++ executable is missing: $($Fixture.executable)" }
    if (-not (Test-Path -LiteralPath $Fixture.dataRoot -PathType Container)) { throw "C++ Data root is missing: $($Fixture.dataRoot)" }
}

function Start-TransientSink {
    param([int]$Port, [string]$ReadyPath, [string]$StatePath)
    try {
        $job = Start-Job -ArgumentList $Port, $ReadyPath, $StatePath -ScriptBlock {
        param($SinkPort, $SinkReadyPath, $SinkStatePath)
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $SinkPort)
        $lines = [System.Collections.Generic.List[string]]::new()
        $saw451 = $false
        $sawData = $false
        $listener.Start()
        [ordered]@{ startedUtc = [DateTimeOffset]::UtcNow.ToString('o'); port = $SinkPort } | ConvertTo-Json | Set-Content -LiteralPath $SinkReadyPath -Encoding UTF8
        try {
            $client = $listener.AcceptTcpClient()
            try {
                $stream = $client.GetStream()
                $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 4096, $true)
                $writer = [IO.StreamWriter]::new($stream, [Text.Encoding]::ASCII, 4096, $true)
                $writer.NewLine = "`r`n"
                $writer.AutoFlush = $true
                $writer.WriteLine("220 disposable retry sink")
                while ($null -ne ($line = $reader.ReadLine())) {
                    $lines.Add($line)
                    if ($line -match '^(EHLO|HELO)') { $writer.WriteLine("250 disposable retry sink") }
                    elseif ($line -match '^MAIL FROM:') { $writer.WriteLine("250 sender accepted") }
                    elseif ($line -match '^RCPT TO:') {
                        $writer.WriteLine("451 temporary recipient failure")
                        $saw451 = $true
                        break
                    }
                    elseif ($line -match '^DATA') {
                        $sawData = $true
                        $writer.WriteLine("354 unexpected data")
                    }
                    else { $writer.WriteLine("250 ok") }
                }
                $reader.Dispose()
                $writer.Dispose()
            }
            finally { $client.Dispose() }
        }
        finally {
            $listener.Stop()
            [pscustomobject]@{
                saw451 = $saw451
                sawData = $sawData
                lines = $lines.ToArray()
            } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $SinkStatePath -Encoding UTF8
        }
        } -ErrorAction Stop
        if ($null -eq $job) { throw [InvalidOperationException]::new('Start-Job returned no job') }
        return $job
    }
    catch {
        throw "Start-Job failed to create disposable transient sink ($($_.Exception.GetType().FullName))."
    }
}

function Start-RecoverySink {
    param([int]$Port, [string]$ReadyPath, [string]$StatePath, [string]$FirstStatePath, [switch]$MixedRecipients)
    try {
        $job = Start-Job -ArgumentList $Port, $ReadyPath, $StatePath, $FirstStatePath, $MixedRecipients.IsPresent -ScriptBlock {
        param($SinkPort, $SinkReadyPath, $SinkStatePath, $SinkFirstStatePath, $UseMixedRecipients)
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $SinkPort)
        $lines = [System.Collections.Generic.List[string]]::new()
        $saw451 = $false
        $sawData = $false
        $sawRecovery = $false
        $sawDataAfterRecovery = $false
        $firstRecipientCount = 0
        $recoveryRecipientCount = 0
        $sawFirstAccepted = $false
        $listener.Start()
        [ordered]@{ startedUtc = [DateTimeOffset]::UtcNow.ToString('o'); port = $SinkPort } | ConvertTo-Json | Set-Content -LiteralPath $SinkReadyPath -Encoding UTF8
        try {
            $client = $listener.AcceptTcpClient()
            try {
                $stream = $client.GetStream()
                $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 4096, $true)
                $writer = [IO.StreamWriter]::new($stream, [Text.Encoding]::ASCII, 4096, $true)
                $writer.NewLine = "`r`n"
                $writer.AutoFlush = $true
                $writer.WriteLine("220 disposable retry sink")
                while ($null -ne ($line = $reader.ReadLine())) {
                    $lines.Add($line)
                    if ($line -match '^(EHLO|HELO)') { $writer.WriteLine("250 disposable retry sink") }
                    elseif ($line -match '^MAIL FROM:') { $writer.WriteLine("250 sender accepted") }
                    elseif ($line -match '^RCPT TO:') {
                        $firstRecipientCount++
                        if ($UseMixedRecipients -and $firstRecipientCount -eq 1) {
                            $writer.WriteLine("250 recipient accepted")
                            $sawFirstAccepted = $true
                        }
                        else {
                            $writer.WriteLine("451 temporary recipient failure")
                            $saw451 = $true
                            if (-not $UseMixedRecipients) { break }
                        }
                    }
                    elseif ($line -match '^DATA') {
                        $sawData = $true
                        $writer.WriteLine("354 start mail input")
                        while ($null -ne ($dataLine = $reader.ReadLine()) -and $dataLine -ne ".") { $lines.Add($dataLine) }
                        $writer.WriteLine("250 message accepted")
                        break
                    }
                    else { $writer.WriteLine("250 ok") }
                }
                $reader.Dispose()
                $writer.Dispose()
            }
            finally { $client.Dispose() }

            [pscustomobject]@{
                saw451 = $saw451
                sawFirstAccepted = $sawFirstAccepted
                firstRecipientCount = $firstRecipientCount
                sawData = $sawData
                lines = $lines.ToArray()
            } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $SinkFirstStatePath -Encoding UTF8

            $client = $listener.AcceptTcpClient()
            try {
                $stream = $client.GetStream()
                $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 4096, $true)
                $writer = [IO.StreamWriter]::new($stream, [Text.Encoding]::ASCII, 4096, $true)
                $writer.NewLine = "`r`n"
                $writer.AutoFlush = $true
                $writer.WriteLine("220 disposable retry sink")
                while ($null -ne ($line = $reader.ReadLine())) {
                    $lines.Add($line)
                    if ($line -match '^(EHLO|HELO)') { $writer.WriteLine("250 disposable retry sink") }
                    elseif ($line -match '^MAIL FROM:') { $writer.WriteLine("250 sender accepted") }
                    elseif ($line -match '^RCPT TO:') { $recoveryRecipientCount++; $writer.WriteLine("250 recipient accepted"); $sawRecovery = $true }
                    elseif ($line -match '^DATA') {
                        $sawDataAfterRecovery = $true
                        $writer.WriteLine("354 start mail input")
                        while ($null -ne ($dataLine = $reader.ReadLine()) -and $dataLine -ne ".") { $lines.Add($dataLine) }
                        $writer.WriteLine("250 message accepted")
                        break
                    }
                    else { $writer.WriteLine("250 ok") }
                }
                $reader.Dispose()
                $writer.Dispose()
            }
            finally { $client.Dispose() }
        }
        finally {
            $listener.Stop()
            [pscustomobject]@{
                saw451 = $saw451
                sawData = $sawData
                sawRecovery = $sawRecovery
                recoveryRecipientCount = $recoveryRecipientCount
                sawDataAfterRecovery = $sawDataAfterRecovery
                lines = $lines.ToArray()
            } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $SinkStatePath -Encoding UTF8
        }
        } -ErrorAction Stop
        if ($null -eq $job) { throw [InvalidOperationException]::new('Start-Job returned no job') }
        return $job
    }
    catch {
        throw "Start-Job failed to create disposable recovery sink ($($_.Exception.GetType().FullName))."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\paired-cpp-net10-20260901-delivery\cpp-tcp451-retry"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory -notmatch '(?i)\\artifacts\\benchmarks\\paired-cpp-net10-[a-z0-9_-]+(?:\\[^\\]+)*$') {
    throw "OutputDirectory is outside the repository benchmark artifact boundary: $OutputDirectory"
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation cpp -RepositoryRoot $repoRoot
$net10Evidence = Get-Content -LiteralPath $Net10EvidencePath -Raw | ConvertFrom-Json
Assert-DisposableFixture $fixture
if ($MixedRecipients -and -not $Recovery) { throw "MixedRecipients requires Recovery because the bounded slice includes the second successful delivery attempt." }
if ($Recovery) {
    if ($MixedRecipients) {
        if ($net10Evidence.status -ne "PASS" -or $net10Evidence.mixedRecipients -ne $true -or $net10Evidence.firstAttempt.smtpReply -ne 451 -or -not $net10Evidence.firstAttempt.sawFirstRecipientAccepted -or -not $net10Evidence.firstAttempt.saw451ResponseSent -or -not $net10Evidence.firstAttempt.sawData -or $net10Evidence.firstAttempt.acceptedRecipientPresent -or -not $net10Evidence.firstAttempt.transientRecipientPresent -or $net10Evidence.recoveryAttempt.smtpReply -ne 250 -or $net10Evidence.recoveryAttempt.recipientCount -ne 1 -or -not $net10Evidence.recoveryAttempt.sawRecoveryResponse -or -not $net10Evidence.recoveryAttempt.sawData) {
            throw "Net10 mixed-recipient TCP 451 recovery evidence is not a PASS artifact with accepted and transient recipient state transitions."
        }
    }
    elseif ($net10Evidence.status -ne "PASS" -or $net10Evidence.firstAttempt.smtpReply -ne 451 -or -not $net10Evidence.firstAttempt.saw451ResponseSent -or $net10Evidence.recoveryAttempt.smtpReply -ne 250 -or -not $net10Evidence.recoveryAttempt.sawRecoveryResponse) {
        throw "Net10 TCP 451 recovery evidence is not a PASS artifact with real 451 and 250 attempts."
    }
}
elseif ($net10Evidence.status -ne "PASS" -or $net10Evidence.smtpReply -ne 451 -or -not $net10Evidence.saw451ResponseSent) {
    throw "Net10 TCP 451 evidence is not a PASS artifact with a real 451 response."
}
if ([string]::IsNullOrWhiteSpace($ServiceName)) { $ServiceName = "hMailPerfTcp451Cpp-$([Guid]::NewGuid().ToString('N').Substring(0, 12))" }
if ($ServiceName -eq "hMailServer" -or $ServiceName -notmatch '^[A-Za-z0-9_.-]{1,255}$') { throw "ServiceName must be a disposable SCM name." }
if (Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort $SinkPort -ErrorAction SilentlyContinue) { throw "Sink port $SinkPort is already in use." }
if ($null -ne (Get-ServiceRecord $ServiceName)) { throw "Refusing to reuse service '$ServiceName'." }
$preflight = Get-CppIsolationPreflight -TargetExecutable $fixture.executable -ExpectedStagingRoot (Split-Path -Parent $fixture.dataRoot) -ExpectedDatabase $fixture.database -DisposableRegistrationGuarded
if (-not $preflight.passed) { throw (($preflight.failures) -join [Environment]::NewLine) }

$runId = [Guid]::NewGuid().ToString('N')
$from = "tcp451-cpp-$runId@perf.test"
$fromSql = $from.Replace("'", "''")
$routeName = "retry-$runId.test"
$routeSql = $routeName.Replace("'", "''")
$seedPath = Join-Path $fixture.dataRoot "perf.test\test\tcp451-$runId.eml"
$seedPathSql = $seedPath.Replace("'", "''")
$reportStem = if ($MixedRecipients) { "paired-cpp-net10-tcp451-mixed-recovery" } elseif ($Recovery) { "paired-cpp-net10-tcp451-recovery" } else { "paired-cpp-net10-tcp451-retry" }
$sinkStatePath = Join-Path $OutputDirectory "cpp-tcp451-sink-$runId.json"
$sinkFirstStatePath = Join-Path $OutputDirectory "cpp-tcp451-first-$runId.json"
$sinkReadyPath = Join-Path $OutputDirectory "cpp-tcp451-ready-$runId.json"
$serviceCreated = $false
$serviceStarted = $false
$routeCreated = $false
$messageSeeded = $false
$sqlPrincipalCreated = $false
$sinkJob = $null
$runError = $null
$evidence = $null
$cleanupFailures = [System.Collections.Generic.List[string]]::new()
$startUtc = [DateTimeOffset]::UtcNow
$deadline = $null
$timeoutDiagnosticPath = $null

try {
    $existingRoute = Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT COUNT(*) FROM hm_routes WHERE routedomainname=N'$routeSql';"
    if ([int]$existingRoute -ne 0) { throw "Generated route name already exists." }
    $retryMinutes = if ($MixedRecipients) { 0 } elseif ($Recovery) { 0 } else { 1 }
    $routeInsert = "INSERT INTO hm_routes (routedomainname,routedescription,routetargetsmthost,routetargetsmtport,routenooftries,routeminutesbetweentry,routealladdresses,routeuseauthentication,routeauthenticationusername,routeauthenticationpassword,routetreatsecurityaslocal,routeconnectionsecurity,routetreatsenderaslocaldomain) VALUES (N'$routeSql',N'disposable TCP 451',N'127.0.0.1',$SinkPort,4,$retryMinutes,1,0,N'',N'',0,0,0);"
    Invoke-SqlStrict $fixture.database $routeInsert | Out-Null
    $routeCreated = $true
    [IO.Directory]::CreateDirectory((Split-Path $seedPath)) | Out-Null
    $acceptedAddress = "accepted@$routeName"
    $transientAddress = "transient@$routeName"
    $acceptedAddressSql = $acceptedAddress.Replace("'", "''")
    $transientAddressSql = $transientAddress.Replace("'", "''")
    $payload = if ($MixedRecipients) {
        "From: $from`r`nTo: $acceptedAddress, $transientAddress`r`nSubject: disposable TCP 451 mixed recipients`r`n`r`nmixed recipient recovery body`r`n"
    }
    else {
        "From: $from`r`nTo: retry@$routeName`r`nSubject: disposable TCP 451 retry`r`n`r`nretry body`r`n"
    }
    [IO.File]::WriteAllText($seedPath, $payload, [Text.Encoding]::ASCII)
    $size = [IO.File]::ReadAllBytes($seedPath).Length
    $recipientInsert = if ($MixedRecipients) {
        "INSERT INTO hm_messagerecipients (recipientmessageid,recipientaddress,recipientlocalaccountid,recipientoriginaladdress) VALUES (@id,N'$acceptedAddressSql',0,N'$acceptedAddressSql'),(@id,N'$transientAddressSql',0,N'$transientAddressSql');"
    }
    else {
        "INSERT INTO hm_messagerecipients (recipientmessageid,recipientaddress,recipientlocalaccountid,recipientoriginaladdress) VALUES (@id,N'retry@$routeSql',0,N'retry@$routeSql');"
    }
    $seed = "INSERT INTO hm_messages (messageaccountid,messagefolderid,messagefilename,messagetype,messagefrom,messagesize,messagecurnooftries,messagenexttrytime,messageflags,messagecreatetime,messagelocked,messageuid) VALUES (0,0,N'$seedPathSql',1,N'$fromSql',$size,0,GETDATE(),0,GETDATE(),0,0); DECLARE @id bigint=SCOPE_IDENTITY(); $recipientInsert"
    Invoke-SqlStrict $fixture.database $seed | Out-Null
    $messageSeeded = $true
    if ([int](Get-SqlScalar master "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.server_principals WHERE name=N'NT AUTHORITY\LOCAL SERVICE';") -ne 0) { throw "LocalService SQL login already exists; refusing to reuse it." }
    Invoke-SqlStrict master "CREATE LOGIN [NT AUTHORITY\LOCAL SERVICE] FROM WINDOWS;" | Out-Null
    Invoke-SqlStrict $fixture.database "CREATE USER [NT AUTHORITY\LOCAL SERVICE] FOR LOGIN [NT AUTHORITY\LOCAL SERVICE]; ALTER ROLE [db_owner] ADD MEMBER [NT AUTHORITY\LOCAL SERVICE];" | Out-Null
    $sqlPrincipalCreated = $true
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $sinkJob = if ($Recovery) {
        Start-RecoverySink -Port $SinkPort -ReadyPath $sinkReadyPath -StatePath $sinkStatePath -FirstStatePath $sinkFirstStatePath -MixedRecipients:$MixedRecipients
    }
    else {
        Start-TransientSink -Port $SinkPort -ReadyPath $sinkReadyPath -StatePath $sinkStatePath
    }
    Wait-SinkReady -Job $sinkJob -Port $SinkPort -ReadyPath $sinkReadyPath -Deadline $deadline
    $binPath = '"{0}" /DisposableBenchmark /ServiceName={1} RunAsService' -f $fixture.executable, $ServiceName
    & sc.exe create $ServiceName binPath= $binPath start= demand type= own obj= 'NT AUTHORITY\LocalService' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed." }
    $serviceCreated = $true
    & sc.exe start $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe start failed." }
    $serviceStarted = $true
    do {
        Start-Sleep -Milliseconds 500
        $service = Get-ServiceRecord $ServiceName
        if ($null -ne $service -and $service.state -eq "Running") { break }
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -eq $service -or $service.state -ne "Running") { throw "Disposable C++ service did not reach Running before timeout." }
    $firstStateToWait = if ($Recovery) { $sinkFirstStatePath } else { $sinkStatePath }
    Wait-SinkStateFile -Job $sinkJob -Port $SinkPort -StatePath $firstStateToWait -Deadline $deadline -TimeoutMessage "C++ service did not reach the transient sink before timeout."
    $firstSinkEvidence = Get-Content -LiteralPath $firstStateToWait -Raw | ConvertFrom-Json
    $row = @(& sqlcmd.exe -S localhost -E -b -d $fixture.database -h-1 -W -s '|' -Q "SET NOCOUNT ON; SELECT COUNT_BIG(*),MAX(CAST(messagetype AS int)),MAX(CAST(messagelocked AS int)),MAX(messagecurnooftries),MAX(messagenexttrytime),COUNT_BIG(r.recipientmessageid) FROM hm_messages m LEFT JOIN hm_messagerecipients r ON r.recipientmessageid=m.messageid WHERE m.messagefrom=N'$fromSql';" 2>&1) | Where-Object { $_ -match '\|' } | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) { throw "C++ SQL evidence query failed." }
    $parts = ([string]$row).Trim().Split('|')
    if ($parts.Count -ne 6) { throw "C++ SQL evidence returned an unexpected shape: $row" }
    $nextTryUtc = ([DateTime]::Parse($parts[4].Trim())).ToUniversalTime()
    $acceptedRecipientPresent = $false
    $transientRecipientPresent = $false
    if ($MixedRecipients) {
        $acceptedRecipientPresent = [int](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT CASE WHEN EXISTS (SELECT 1 FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom=N'$fromSql' AND r.recipientaddress=N'$acceptedAddressSql') THEN 1 ELSE 0 END;") -eq 1
        $transientRecipientPresent = [int](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT CASE WHEN EXISTS (SELECT 1 FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom=N'$fromSql' AND r.recipientaddress=N'$transientAddressSql') THEN 1 ELSE 0 END;") -eq 1
    }
    $initialEvidence = [ordered]@{
        mixedRecipients = [bool]$MixedRecipients
        queuedCount = [int64]$parts[0].Trim()
        messageType = [int]$parts[1].Trim()
        locked = [int]$parts[2].Trim()
        retryCount = [int]$parts[3].Trim()
        nextTryUtc = $nextTryUtc.ToString('o')
        recipientCount = [int]$parts[5].Trim()
        acceptedRecipientPresent = $acceptedRecipientPresent
        transientRecipientPresent = $transientRecipientPresent
        dataFileExists = Test-Path -LiteralPath $seedPath -PathType Leaf
        sink = $firstSinkEvidence
    }
    if ($Recovery) {
        Wait-SinkStateFile -Job $sinkJob -Port $SinkPort -StatePath $sinkStatePath -Deadline $deadline -TimeoutMessage "C++ service did not complete the recovery attempt before timeout."
        $sinkEvidence = Get-Content -LiteralPath $sinkStatePath -Raw | ConvertFrom-Json
        $finalMessageCount = [int64](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_messages WHERE messagefrom=N'$fromSql';")
        $finalRecipientCount = [int64](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom=N'$fromSql';")
        $evidence = [ordered]@{
            queuedCount = $initialEvidence.queuedCount
            messageType = $initialEvidence.messageType
            locked = $initialEvidence.locked
            retryCount = $initialEvidence.retryCount
            nextTryUtc = $initialEvidence.nextTryUtc
            recipientCount = $initialEvidence.recipientCount
            dataFileExists = $initialEvidence.dataFileExists
            initial = $initialEvidence
            final = [ordered]@{
                queuedCount = $finalMessageCount
                recipientCount = $finalRecipientCount
                dataFileExists = Test-Path -LiteralPath $seedPath -PathType Leaf
            }
            sink = $sinkEvidence
        }
        Remove-Item -LiteralPath $sinkFirstStatePath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $sinkStatePath -Force -ErrorAction SilentlyContinue
        $recoveryStateFailed = -not $firstSinkEvidence.saw451 -or $firstSinkEvidence.sawData -or -not $sinkEvidence.sawRecovery -or -not $sinkEvidence.sawDataAfterRecovery -or $initialEvidence.queuedCount -ne 1 -or $initialEvidence.messageType -ne 1 -or $initialEvidence.locked -ne 0 -or $initialEvidence.retryCount -ne 1 -or $initialEvidence.recipientCount -ne 1 -or -not $initialEvidence.dataFileExists -or $finalMessageCount -ne 0 -or $finalRecipientCount -ne 0 -or (Test-Path -LiteralPath $seedPath -PathType Leaf)
        if ($MixedRecipients) {
            $recoveryStateFailed = -not $firstSinkEvidence.sawFirstAccepted -or -not $firstSinkEvidence.saw451 -or -not $firstSinkEvidence.sawData -or $firstSinkEvidence.firstRecipientCount -ne 2 -or $acceptedRecipientPresent -or -not $transientRecipientPresent -or $sinkEvidence.recoveryRecipientCount -ne 1 -or -not $sinkEvidence.sawRecovery -or -not $sinkEvidence.sawDataAfterRecovery -or $initialEvidence.recipientCount -ne 1
        }
        if ($recoveryStateFailed) {
            throw "C++ TCP 451 recovery-state assertions failed."
        }
    }
    else {
        $sinkEvidence = $firstSinkEvidence
        $evidence = [ordered]@{
            queuedCount = $initialEvidence.queuedCount
            messageType = $initialEvidence.messageType
            locked = $initialEvidence.locked
            retryCount = $initialEvidence.retryCount
            nextTryUtc = $initialEvidence.nextTryUtc
            recipientCount = $initialEvidence.recipientCount
            dataFileExists = $initialEvidence.dataFileExists
            sink = $sinkEvidence
        }
        Remove-Item -LiteralPath $sinkStatePath -Force -ErrorAction SilentlyContinue
        if (-not $sinkEvidence.saw451 -or $sinkEvidence.sawData -or $evidence.queuedCount -ne 1 -or $evidence.messageType -ne 1 -or $evidence.locked -ne 0 -or $evidence.retryCount -ne 1 -or $evidence.recipientCount -ne 1 -or -not $evidence.dataFileExists) {
            throw "C++ TCP 451 retry-state assertions failed."
        }
    }
}
catch {
    $runError = $_.Exception.Message
    if ($runError -match '(?i)before timeout' -and $null -ne $deadline) {
        $reasonCode = if ($runError -match 'sink did not become ready') { 'sink-readiness-timeout' } elseif ($runError -match 'service did not reach Running') { 'service-start-timeout' } elseif ($runError -match 'complete the recovery') { 'recovery-timeout' } else { 'sink-state-timeout' }
        $timeoutDiagnosticPath = Join-Path $OutputDirectory "cpp-tcp451-timeout-$runId.json"
        try {
            Write-TimeoutDiagnostic -Path $timeoutDiagnosticPath -ReasonCode $reasonCode -ServiceName $ServiceName -Port $SinkPort -Job $sinkJob -ReadyPath $sinkReadyPath -FirstStatePath $sinkFirstStatePath -StatePath $sinkStatePath -Database $fixture.database -MessageFromSql $fromSql -DataPath $seedPath | Out-Null
        }
        catch {
            $cleanupFailures.Add("Timeout diagnostic write failed: $($_.Exception.GetType().FullName)")
            $timeoutDiagnosticPath = $null
        }
    }
}
finally {
    if ($serviceCreated) {
        & sc.exe stop $ServiceName | Out-Null
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do { Start-Sleep -Milliseconds 500; $service = Get-ServiceRecord $ServiceName } while ($null -ne $service -and $service.state -ne "Stopped" -and [DateTime]::UtcNow -lt $deadline)
        if ($null -ne $service -and $service.state -ne "Stopped") { $cleanupFailures.Add("Disposable C++ service did not stop.") }
        & sc.exe delete $ServiceName | Out-Null
        if ($LASTEXITCODE -ne 0) { $cleanupFailures.Add("sc.exe delete failed.") }
    }
    if ($null -ne $sinkJob) { Stop-Job $sinkJob -ErrorAction SilentlyContinue; Remove-Job $sinkJob -Force -ErrorAction SilentlyContinue }
    if ($messageSeeded) {
        try { Invoke-SqlStrict $fixture.database "DELETE r FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom=N'$fromSql'; DELETE FROM hm_messages WHERE messagefrom=N'$fromSql';" | Out-Null } catch { $cleanupFailures.Add("Message cleanup failed: $($_.Exception.Message)") }
    }
    if ($routeCreated) { try { Invoke-SqlStrict $fixture.database "DELETE FROM hm_routes WHERE routedomainname=N'$routeSql';" | Out-Null } catch { $cleanupFailures.Add("Route cleanup failed: $($_.Exception.Message)") } }
    if ($sqlPrincipalCreated) {
        try { Invoke-SqlStrict $fixture.database "IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'NT AUTHORITY\LOCAL SERVICE') DROP USER [NT AUTHORITY\LOCAL SERVICE];" | Out-Null; Invoke-SqlStrict master "IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'NT AUTHORITY\LOCAL SERVICE') DROP LOGIN [NT AUTHORITY\LOCAL SERVICE];" | Out-Null } catch { $cleanupFailures.Add("SQL principal cleanup failed: $($_.Exception.Message)") }
    }
    Remove-Item -LiteralPath $seedPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $sinkReadyPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $sinkFirstStatePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $sinkStatePath -Force -ErrorAction SilentlyContinue
}

$cleanupState = [ordered]@{
    serviceAbsent = $null -eq (Get-ServiceRecord $ServiceName)
    routeAbsent = [int](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT COUNT(*) FROM hm_routes WHERE routedomainname=N'$routeSql';") -eq 0
    messageAbsent = [int](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT COUNT(*) FROM hm_messages WHERE messagefrom=N'$fromSql';") -eq 0
    recipientAbsent = [int](Get-SqlScalar $fixture.database "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom=N'$fromSql';") -eq 0
    dataFileAbsent = -not (Test-Path -LiteralPath $seedPath -PathType Leaf)
}
$cleanupPass = $cleanupFailures.Count -eq 0 -and @($cleanupState.Values | Where-Object { -not $_ }).Count -eq 0
$endUtc = [DateTimeOffset]::UtcNow
$report = [ordered]@{
    schema = if ($MixedRecipients) { "paired-cpp-net10-tcp-451-mixed-recovery-v1" } elseif ($Recovery) { "paired-cpp-net10-tcp-451-recovery-v1" } else { "paired-cpp-net10-tcp-451-retry-v1" }
    status = if ($null -eq $runError -and $cleanupPass) { "PASS" } else { "FAIL" }
    generatedUtc = $endUtc.ToString('o')
    gitCommit = (git rev-parse HEAD).Trim()
    fixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
    fixtureManifestSha256 = $fixture.sha256
    sink = [ordered]@{ host = "127.0.0.1"; port = $SinkPort; smtpReply = 451; sameProtocolPhase = $true }
    cpp = [ordered]@{ database = $fixture.database; dataRoot = $fixture.dataRoot; executable = $fixture.executable; executableSha256 = (Get-FileHash -LiteralPath $fixture.executable -Algorithm SHA256).Hash; serviceName = $ServiceName; evidence = $evidence }
    net10 = [ordered]@{ evidencePath = [IO.Path]::GetFullPath($Net10EvidencePath); database = $net10Evidence.database; dataRoot = $net10Evidence.dataRoot; evidence = $net10Evidence }
    cleanup = $cleanupState
    cleanupFailures = @($cleanupFailures)
    timeoutDiagnosticPath = if ($null -ne $timeoutDiagnosticPath) { [IO.Path]::GetFullPath($timeoutDiagnosticPath) } else { $null }
    error = $runError
}
$jsonPath = Join-Path $OutputDirectory "$reportStem.json"
$csvPath = Join-Path $OutputDirectory "$reportStem.csv"
$mdPath = Join-Path $OutputDirectory "$reportStem.md"
$report | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
if ($Recovery) {
    if ($MixedRecipients) {
        "implementation,status,first_smtp_reply,recovery_smtp_reply,initial_queued_count,initial_retry_count,initial_recipient_count,accepted_recipient_present,transient_recipient_present,final_queued_count,final_recipient_count,final_data_file_exists`ncpp,$($report.status),451,250,$($evidence.initial.queuedCount),$($evidence.initial.retryCount),$($evidence.initial.recipientCount),$($evidence.initial.acceptedRecipientPresent),$($evidence.initial.transientRecipientPresent),$($evidence.final.queuedCount),$($evidence.final.recipientCount),$($evidence.final.dataFileExists)`nnet10,$($net10Evidence.status),451,250,$($net10Evidence.firstAttempt.queuedCount),$($net10Evidence.firstAttempt.retryCount),$($net10Evidence.firstAttempt.recipientCount),$($net10Evidence.firstAttempt.acceptedRecipientPresent),$($net10Evidence.firstAttempt.transientRecipientPresent),$($net10Evidence.finalState.QueuedCount),$($net10Evidence.finalState.RecipientCount),$($net10Evidence.messageFileAbsent)" | Set-Content -LiteralPath $csvPath -Encoding UTF8
    }
    else {
        "implementation,status,first_smtp_reply,recovery_smtp_reply,initial_queued_count,initial_retry_count,initial_recipient_count,final_queued_count,final_recipient_count,final_data_file_exists`ncpp,$($report.status),451,250,$($evidence.initial.queuedCount),$($evidence.initial.retryCount),$($evidence.initial.recipientCount),$($evidence.final.queuedCount),$($evidence.final.recipientCount),$($evidence.final.dataFileExists)`nnet10,$($net10Evidence.status),451,250,$($net10Evidence.firstAttempt.queuedCount),$($net10Evidence.firstAttempt.retryCount),$($net10Evidence.firstAttempt.recipientCount),$($net10Evidence.finalState.QueuedCount),$($net10Evidence.finalState.RecipientCount),$($net10Evidence.messageFileAbsent)" | Set-Content -LiteralPath $csvPath -Encoding UTF8
    }
}
else {
    "implementation,status,smtp_reply,queued_count,message_type,locked,retry_count,recipient_count,data_file_exists`ncpp,$($report.status),451,$($evidence.queuedCount),$($evidence.messageType),$($evidence.locked),$($evidence.retryCount),$($evidence.recipientCount),$($evidence.dataFileExists)`nnet10,$($net10Evidence.status),451,$($net10Evidence.queuedCount),$($net10Evidence.messageType),$($net10Evidence.locked),$($net10Evidence.retryCount),$($net10Evidence.recipientCount),$(-not $net10Evidence.sawData)" | Set-Content -LiteralPath $csvPath -Encoding UTF8
}
if ($Recovery) {
    if ($MixedRecipients) {
        @(
            "# Paired C++ / .NET 10 TCP 451 mixed-recipient recovery acceptance",
            "",
            "- Status: **$($report.status)**",
            "- Sink: 127.0.0.1:$SinkPort, first RCPT replies 250 then 451, DATA observed, recovery RCPT reply 250 and DATA observed",
            "- C++ initial state: queued=$($evidence.initial.queuedCount), retry=$($evidence.initial.retryCount), recipients=$($evidence.initial.recipientCount), accepted present=$($evidence.initial.acceptedRecipientPresent), transient present=$($evidence.initial.transientRecipientPresent), Data file=$($evidence.initial.dataFileExists)",
            "- C++ final state: queued=$($evidence.final.queuedCount), recipients=$($evidence.final.recipientCount), Data file=$($evidence.final.dataFileExists)",
            "- Net10 initial state: queued=$($net10Evidence.firstAttempt.queuedCount), retry=$($net10Evidence.firstAttempt.retryCount), recipients=$($net10Evidence.firstAttempt.recipientCount), accepted present=$($net10Evidence.firstAttempt.acceptedRecipientPresent), transient present=$($net10Evidence.firstAttempt.transientRecipientPresent)",
            "- Net10 final state: queued=$($net10Evidence.finalState.QueuedCount), recipients=$($net10Evidence.finalState.RecipientCount), Data file absent=$($net10Evidence.messageFileAbsent)",
            "- Cleanup: service=$($cleanupState.serviceAbsent), route=$($cleanupState.routeAbsent), message=$($cleanupState.messageAbsent), recipient=$($cleanupState.recipientAbsent), Data file=$($cleanupState.dataFileAbsent)",
            "",
            "This is bounded mixed-recipient retry-recovery parity evidence.",
            "",
            "JSON: $jsonPath"
        ) | Set-Content -LiteralPath $mdPath -Encoding UTF8
    }
    else {
    @(
        "# Paired C++ / .NET 10 TCP 451 recovery acceptance",
        "",
        "- Status: **$($report.status)**",
        "- Sink: 127.0.0.1:$SinkPort, first RCPT reply 451, recovery RCPT reply 250, DATA expected only after recovery",
        "- C++ initial state: queued=$($evidence.initial.queuedCount), retry=$($evidence.initial.retryCount), recipients=$($evidence.initial.recipientCount), Data file=$($evidence.initial.dataFileExists)",
        "- C++ final state: queued=$($evidence.final.queuedCount), recipients=$($evidence.final.recipientCount), Data file=$($evidence.final.dataFileExists)",
        "- Net10 initial state: queued=$($net10Evidence.firstAttempt.queuedCount), retry=$($net10Evidence.firstAttempt.retryCount), recipients=$($net10Evidence.firstAttempt.recipientCount), DATA before recovery=$($net10Evidence.firstAttempt.sawData)",
        "- Net10 final state: queued=$($net10Evidence.finalState.QueuedCount), recipients=$($net10Evidence.finalState.RecipientCount), Data file absent=$($net10Evidence.messageFileAbsent)",
        "- Cleanup: service=$($cleanupState.serviceAbsent), route=$($cleanupState.routeAbsent), message=$($cleanupState.messageAbsent), recipient=$($cleanupState.recipientAbsent), Data file=$($cleanupState.dataFileAbsent)",
        "",
        "This is bounded retry-recovery parity evidence. It is not throughput, soak, or release clearance.",
        "",
        "JSON: $jsonPath"
    ) | Set-Content -LiteralPath $mdPath -Encoding UTF8
    }
}
else {
@(
    "# Paired C++ / .NET 10 TCP 451 retry acceptance",
    "",
    "- Status: **$($report.status)**",
    "- Sink: 127.0.0.1:$SinkPort, RCPT reply 451, DATA expected on first attempt: false",
    "- C++: queued=$($evidence.queuedCount), type=$($evidence.messageType), locked=$($evidence.locked), retry=$($evidence.retryCount), recipients=$($evidence.recipientCount), Data file=$($evidence.dataFileExists)",
    "- Net10: queued=$($net10Evidence.queuedCount), type=$($net10Evidence.messageType), locked=$($net10Evidence.locked), retry=$($net10Evidence.retryCount), recipients=$($net10Evidence.recipientCount), no DATA=$(-not $net10Evidence.sawData)",
    "- Cleanup: service=$($cleanupState.serviceAbsent), route=$($cleanupState.routeAbsent), message=$($cleanupState.messageAbsent), recipient=$($cleanupState.recipientAbsent), Data file=$($cleanupState.dataFileAbsent)",
    "",
    "This is bounded transient-state parity evidence. It is not retry recovery, throughput, soak, or release clearance.",
    "",
    "JSON: $jsonPath"
) | Set-Content -LiteralPath $mdPath -Encoding UTF8
}
if ($report.status -ne "PASS") { throw "Paired TCP 451 retry acceptance failed. See $jsonPath" }
Write-Output ($report | ConvertTo-Json -Depth 16)
