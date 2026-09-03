param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureManifest,
    [ValidateRange(1, 5000)]
    [int]$MessageCount = 100,
    [ValidateRange(5, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [ValidateRange(5, 600)]
    [int]$DrainTimeoutSeconds = 120,
    [ValidateRange(25, 1000)]
    [int]$PollMilliseconds = 100,
    [string]$OutputDirectory = "",
    [string]$RunId = "",
    [string]$CppServiceName = "hMailServerPerfLocalDeliveryQueue"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot 'live-benchmark-provenance.ps1')
. (Join-Path $PSScriptRoot 'live-cpp-isolation-preflight.ps1')

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-SafeGuid {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return [Guid]::NewGuid().ToString('N') }
    try { $guid = [Guid]::Parse($Value) } catch { throw 'RunId must be a valid GUID.' }
    if ($guid -eq [Guid]::Empty) { throw 'RunId must not be empty.' }
    return $guid.ToString('N')
}

function Invoke-SqlStrict {
    param([string]$Database, [string]$Query)
    $output = @(& sqlcmd.exe -S localhost -E -b -d $Database -Q $Query 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed against disposable database '$Database' with exit code ${LASTEXITCODE}: $($output -join ' ')"
    }
    return $output
}

function Get-SqlRow {
    param([string]$Database, [string]$Query, [int]$FieldCount)
    $rows = @(& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -s '|' -Q $Query 2>&1 |
        ForEach-Object { ([string]$_).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and ($FieldCount -eq 1 -or $_ -match '\|') })
    if ($LASTEXITCODE -ne 0 -or $rows.Count -eq 0) { throw "Could not read disposable SQL state from '$Database'." }
    $parts = ([string]$rows[-1]).Split('|')
    if ($parts.Count -ne $FieldCount) { throw "Unexpected SQL state shape from '$Database': $($rows[-1])" }
    return $parts
}

function Get-SqlSnapshot {
    param([string]$Database, [string]$MarkerPrefix)
    $prefixSql = $MarkerPrefix.Replace("'", "''")
    $query = @"
SET NOCOUNT ON;
DECLARE @prefix nvarchar(200)=N'$prefixSql';
SELECT
 (SELECT COUNT_BIG(*) FROM hm_messages),
 (SELECT COUNT_BIG(*) FROM hm_message_metadata),
 (SELECT COUNT_BIG(*) FROM hm_messagerecipients),
 (SELECT COUNT_BIG(*) FROM hm_messages WHERE messagefrom LIKE @prefix + N'-%@perf.test' AND messagetype = 1),
 (SELECT COUNT_BIG(*) FROM hm_messages WHERE messagefrom LIKE @prefix + N'-%@perf.test' AND messagetype = 2),
 (SELECT COUNT_BIG(*) FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom LIKE @prefix + N'-%@perf.test'),
 (SELECT COUNT_BIG(*) FROM hm_messages m INNER JOIN hm_imapfolders f ON f.folderid=m.messagefolderid WHERE m.messagefrom LIKE @prefix + N'-%@perf.test' AND m.messagetype=2 AND UPPER(f.foldername)=N'INBOX');
"@
    $parts = Get-SqlRow -Database $Database -Query $query -FieldCount 7
    [pscustomobject]@{
        messageCount = [int64]$parts[0]
        metadataCount = [int64]$parts[1]
        recipientCount = [int64]$parts[2]
        markedQueueRows = [int64]$parts[3]
        markedInboxRows = [int64]$parts[4]
        markedRecipientRows = [int64]$parts[5]
        markedInboxFolderRows = [int64]$parts[6]
    }
}

function Get-DeliveredMarkers {
    param([string]$Database, [string]$MarkerPrefix)
    $prefixSql = $MarkerPrefix.Replace("'", "''")
    $query = @"
SET NOCOUNT ON;
DECLARE @prefix nvarchar(200)=N'$prefixSql';
SELECT m.messagefrom, f.folderaccountid, f.folderparentid, UPPER(f.foldername),
       (SELECT COUNT_BIG(*) FROM hm_messagerecipients r WHERE r.recipientmessageid=m.messageid)
FROM hm_messages m
INNER JOIN hm_imapfolders f ON f.folderid=m.messagefolderid
WHERE m.messagefrom LIKE @prefix + N'-%@perf.test' AND m.messagetype=2
ORDER BY m.messagefrom;
"@
    $rows = @(& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -s '|' -Q $query 2>&1 |
        ForEach-Object { ([string]$_).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -match '\|' })
    if ($LASTEXITCODE -ne 0) { throw "Could not read terminal local-delivery rows from '$Database'." }
    foreach ($row in $rows) {
        $parts = ([string]$row).Split('|')
        if ($parts.Count -ne 5) { throw "Unexpected local-delivery row shape from '$Database': $row" }
        [pscustomobject]@{
            messageFrom = $parts[0]
            folderAccountId = [int]$parts[1]
            folderParentId = [int]$parts[2]
            folderName = $parts[3]
            recipientRows = [int64]$parts[4]
        }
    }
}

function Get-SeedFileFingerprint {
    param([string]$DataRoot, [string]$MarkerPrefix)
    $files = @(Get-ChildItem -LiteralPath $DataRoot -File -Recurse -Force |
        Where-Object { [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($_.FullName)).IndexOf($MarkerPrefix, [StringComparison]::Ordinal) -ge 0 } |
        Sort-Object FullName)
    $root = $DataRoot.TrimEnd('\')
    $rows = foreach ($file in $files) {
        $relative = $file.FullName.Substring($root.Length).TrimStart('\').Replace('/', '\')
        "$relative|$($file.Length)|$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToUpperInvariant())"
    }
    $payload = [Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $digest = ([BitConverter]::ToString($sha.ComputeHash($payload))).Replace('-', '') }
    finally { $sha.Dispose() }
    [pscustomobject]@{ fileCount = $files.Count; bytes = [long](($files | Measure-Object Length -Sum).Sum); sha256 = $digest; files = @($files.FullName) }
}

function Get-ResourceSnapshot {
    param([int]$ProcessId)
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { return $null }
    [pscustomobject]@{
        processId = $process.Id
        privateBytes = [int64]$process.PrivateMemorySize64
        workingSetBytes = [int64]$process.WorkingSet64
        handles = [int]$process.Handles
        threads = [int]$process.Threads.Count
        cpuTimeMs = [int64]$process.TotalProcessorTime.TotalMilliseconds
    }
}

function Get-ServiceRecord {
    param([string]$Name)
    $service = Get-CimInstance Win32_Service -Filter "Name='$Name'" -ErrorAction SilentlyContinue
    if ($null -eq $service) { return $null }
    [pscustomobject]@{ name = [string]$service.Name; state = [string]$service.State; processId = [int]$service.ProcessId; startName = [string]$service.StartName; pathName = [string]$service.PathName }
}

function Get-PortPids {
    param([int]$Port)
    @(Get-NetTCPConnection -State Listen -LocalAddress '127.0.0.1' -LocalPort $Port -ErrorAction SilentlyContinue | ForEach-Object { [int]$_.OwningProcess })
}

function Wait-ForServiceReadiness {
    param([string]$Name, [int]$ExpectedProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    do {
        $service = Get-ServiceRecord $Name
        if ($null -ne $service -and $service.state -eq 'Running' -and $service.processId -eq $ExpectedProcessId) { return @() }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    $service = Get-ServiceRecord $Name
    return @("Service readiness failed: state=$($service.state), pid=$($service.processId)")
}

function Wait-ForProcessReadiness {
    param([System.Diagnostics.Process]$Process)
    if (-not $Process.HasExited) { return @() }
    return @("Net10 readiness failed for PID $($Process.Id).")
}

function Remove-MarkerFiles {
    param([string]$DataRoot, [string]$MarkerPrefix)
    $files = @(Get-SeedFileFingerprint -DataRoot $DataRoot -MarkerPrefix $MarkerPrefix).files
    foreach ($file in $files) { Remove-Item -LiteralPath $file -Force -ErrorAction Stop }
}

function Remove-MarkerRows {
    param([string]$Database, [string]$MarkerPrefix)
    $prefixSql = $MarkerPrefix.Replace("'", "''")
    Invoke-SqlStrict -Database $Database -Query @"
SET NOCOUNT ON;
DECLARE @prefix nvarchar(200)=N'$prefixSql';
BEGIN TRANSACTION;
DELETE r FROM hm_messagerecipients r INNER JOIN hm_messages m ON m.messageid=r.recipientmessageid WHERE m.messagefrom LIKE @prefix + N'-%@perf.test';
DELETE FROM hm_messages WHERE messagefrom LIKE @prefix + N'-%@perf.test';
COMMIT TRANSACTION;
"@ | Out-Null
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percent)
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $index = [math]::Max(0, [math]::Ceiling($sorted.Count * $Percent) - 1)
    return [math]::Round([double]$sorted[$index], 3)
}

function New-SeedCorpus {
    param([object]$Fixture, [string]$Implementation, [string]$MarkerPrefix, [string]$Token)
    $dataRoot = [string]$Fixture.dataRoot
    $database = [string]$Fixture.database
    $messageDirectory = Join-Path $dataRoot 'perf.test\test'
    $files = [System.Collections.Generic.List[object]]::new()
    for ($sequence = 1; $sequence -le $MessageCount; $sequence++) {
        $marker = '{0}-{1:D6}' -f $MarkerPrefix, $sequence
        $fileName = 'paired-local-delivery-{0}-{1:D6}.eml' -f $Token, $sequence
        $path = Join-Path $messageDirectory $fileName
        $payload = "From: $marker@perf.test`r`nTo: test@perf.test`r`nMessage-ID: <$marker@perf.test>`r`nSubject: paired local delivery queue`r`nDate: Mon, 01 Jan 2024 00:00:00 +0000`r`n`r`nDisposable local-delivery benchmark message $sequence.`r`n"
        $bytes = [Text.Encoding]::ASCII.GetBytes($payload)
        [IO.File]::WriteAllBytes($path, $bytes)
        $files.Add([pscustomobject]@{ sequence = $sequence; marker = $marker; path = $path; bytes = $bytes.Length })
    }
    $seedFingerprint = Get-SeedFileFingerprint -DataRoot $dataRoot -MarkerPrefix $MarkerPrefix
    Assert-True ($seedFingerprint.fileCount -eq $MessageCount) "$Implementation seed file count is not $MessageCount."
    $rootSql = $dataRoot.Replace("'", "''")
    $prefixSql = $MarkerPrefix.Replace("'", "''")
    $fileSize = [int]$files[0].bytes
    Invoke-SqlStrict -Database $database -Query @"
SET NOCOUNT ON;
DECLARE @prefix nvarchar(200)=N'$prefixSql';
DECLARE @root nvarchar(4000)=N'$rootSql';
;WITH n AS
(
    SELECT TOP ($MessageCount) CONVERT(bigint, ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) AS sequence
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO hm_messages
    (messageaccountid,messagefolderid,messagefilename,messagetype,messagefrom,messagesize,
     messagecurnooftries,messagenexttrytime,messageflags,messagecreatetime,messagelocked,messageuid)
SELECT 0,0,
       @root + N'\perf.test\test\paired-local-delivery-$Token-' + RIGHT(N'000000' + CONVERT(nvarchar(20), n.sequence), 6) + N'.eml',
       1, @prefix + N'-' + RIGHT(N'000000' + CONVERT(nvarchar(20), n.sequence), 6) + N'@perf.test',
       $fileSize, 0, SYSUTCDATETIME(), 0, SYSUTCDATETIME(), 0, 0
FROM n;
INSERT INTO hm_messagerecipients
    (recipientmessageid,recipientaddress,recipientlocalaccountid,recipientoriginaladdress)
SELECT m.messageid, N'test@perf.test', 1, N'test@perf.test'
FROM hm_messages m
WHERE m.messagefrom LIKE @prefix + N'-%@perf.test' AND m.messagetype=1;
"@ | Out-Null
    $snapshot = Get-SqlSnapshot -Database $database -MarkerPrefix $MarkerPrefix
    Assert-True ($snapshot.markedQueueRows -eq $MessageCount -and $snapshot.markedRecipientRows -eq $MessageCount) "$Implementation SQL seed did not produce exactly $MessageCount type=1 rows and recipients."
    [pscustomobject]@{ implementation = $Implementation; markerPrefix = $MarkerPrefix; files = $files.ToArray(); fingerprint = $seedFingerprint; database = $database; dataRoot = $dataRoot }
}

function Invoke-SideBenchmark {
    param([ValidateSet('cpp', 'net10')][string]$Implementation, [object]$Fixture, [object]$Seed, [object]$Baseline)
    $database = [string]$Fixture.database
    $dataRoot = [string]$Fixture.dataRoot
    $executable = [string]$Fixture.executable
    $serviceName = if ($Implementation -eq 'cpp') { $CppServiceName } else { $null }
    $serviceCreated = $false
    $serviceStarted = $false
    $sqlPrincipalCreated = $false
    $process = $null
    $workerPid = 0
    $readinessFailures = [System.Collections.Generic.List[string]]::new()
    $cleanupFailures = [System.Collections.Generic.List[string]]::new()
    $beforeSnapshot = $Baseline.sql
    $dataBefore = $Baseline.data
    $resourceBefore = $null
    $resourceAfter = $null
    $drainStartedUtc = $null
    $drainEndedUtc = $null
    $latencies = [System.Collections.Generic.List[double]]::new()
    $observed = @{}
    $runError = $null
    $startUtc = [DateTimeOffset]::UtcNow
    try {
        if ($Implementation -eq 'cpp') {
            $preflight = Get-CppIsolationPreflight -TargetExecutable $executable -ExpectedStagingRoot (Split-Path -Parent $dataRoot) -ExpectedDatabase $database -DisposableRegistrationGuarded
            if (-not $preflight.passed) { throw (($preflight.failures) -join [Environment]::NewLine) }
            $principal = 'NT AUTHORITY\LOCAL SERVICE'
            $principalSql = $principal.Replace("'", "''")
            $principalCount = @(& sqlcmd.exe -S localhost -E -b -d master -h-1 -W -Q "SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'$principalSql') SELECT 1 ELSE SELECT 0;" 2>&1 |
                ForEach-Object { ([string]$_).Trim() } |
                Where-Object { $_ -match '^\d+$' })
            if ($LASTEXITCODE -ne 0 -or $principalCount.Count -ne 1) { throw 'Could not determine whether the disposable SQL LocalService login exists.' }
            if ([int]$principalCount[0] -ne 0) {
                throw 'Disposable SQL LocalService login already exists; refusing to reuse it.'
            }
            Invoke-SqlStrict -Database 'master' -Query "CREATE LOGIN [$principal] FROM WINDOWS;" | Out-Null
            $sqlPrincipalCreated = $true
            Invoke-SqlStrict -Database $database -Query "CREATE USER [$principal] FOR LOGIN [$principal]; ALTER ROLE [db_owner] ADD MEMBER [$principal];" | Out-Null
            if ($null -ne (Get-ServiceRecord 'hMailServer') -and (Get-ServiceRecord 'hMailServer').state -ne 'Stopped') { throw 'Production-named hMailServer service is running.' }
            if ($null -ne (Get-ServiceRecord $serviceName)) { throw "Refusing to reuse disposable service '$serviceName'." }
            $binPath = '"{0}" /DisposableBenchmark /ServiceName={1} RunAsService' -f $executable, $serviceName
            & sc.exe create $serviceName binPath= $binPath start= demand type= own DisplayName= $serviceName obj= 'NT AUTHORITY\LocalService' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with exit code $LASTEXITCODE." }
            $serviceCreated = $true
            & sc.exe start $serviceName | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "sc.exe start failed with exit code $LASTEXITCODE." }
            $serviceStarted = $true
            $deadline = [DateTime]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
            do {
                Start-Sleep -Milliseconds 500
                $service = Get-ServiceRecord $serviceName
                if ($null -ne $service -and $service.state -eq 'Running' -and $service.processId -gt 0) { $workerPid = $service.processId; break }
            } while ([DateTime]::UtcNow -lt $deadline)
            if ($workerPid -le 0) { $readinessFailures.Add('Disposable C++ service did not expose a worker PID.') }
            else { foreach ($failure in @(Wait-ForServiceReadiness $serviceName $workerPid)) { $readinessFailures.Add([string]$failure) } }
        }
        else {
            $processStartInfo = [Diagnostics.ProcessStartInfo]::new()
            $processStartInfo.FileName = $executable
            $processStartInfo.WorkingDirectory = Split-Path -Parent $executable
            $processStartInfo.UseShellExecute = $false
            $processStartInfo.Environment['HMAILSERVER_SQLSERVER_CONNECTION'] = "Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True;"
            $processStartInfo.Environment['HMAILSERVER_DATA_DIRECTORY'] = $dataRoot
            $processStartInfo.Environment['HMAILSERVER_COM_LOCAL_SERVER_ENABLED'] = 'false'
            $process = [Diagnostics.Process]::Start($processStartInfo)
            $workerPid = $process.Id
            foreach ($failure in @(Wait-ForProcessReadiness $process)) { $readinessFailures.Add([string]$failure) }
        }
        if ($readinessFailures.Count -gt 0) { throw (($readinessFailures) -join [Environment]::NewLine) }
        $resourceBefore = Get-ResourceSnapshot -ProcessId $workerPid
        $drainStartedUtc = [DateTimeOffset]::UtcNow
        $deadline = $drainStartedUtc.AddSeconds($DrainTimeoutSeconds)
        do {
            foreach ($row in @(Get-DeliveredMarkers -Database $database -MarkerPrefix $Seed.markerPrefix)) {
                $key = [string]$row.messageFrom
                if (-not $observed.ContainsKey($key) -and $row.folderAccountId -eq 1 -and $row.folderParentId -eq -1 -and $row.folderName -ceq 'INBOX' -and $row.recipientRows -eq 0) {
                    $observed[$key] = $true
                    $latencies.Add(([DateTimeOffset]::UtcNow - $drainStartedUtc).TotalMilliseconds)
                }
            }
            if ($observed.Count -eq $MessageCount) { break }
            Start-Sleep -Milliseconds $PollMilliseconds
        } while ([DateTimeOffset]::UtcNow -lt $deadline)
        $drainEndedUtc = [DateTimeOffset]::UtcNow
        $resourceAfter = Get-ResourceSnapshot -ProcessId $workerPid
        if ($observed.Count -ne $MessageCount) { $runError = "Terminal local-delivery drain timed out after $DrainTimeoutSeconds seconds: observed $($observed.Count)/$MessageCount Inbox rows." }
    }
    catch {
        if ($null -eq $runError) { $runError = $_.Exception.Message }
    }
    finally {
        if ($Implementation -eq 'net10' -and $null -ne $process) {
            try { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force } } catch { $cleanupFailures.Add("Net10 process stop failed: $($_.Exception.Message)") }
            try { $process.WaitForExit(30000) | Out-Null } catch { $cleanupFailures.Add("Net10 process wait failed: $($_.Exception.Message)") }
        }
        if ($serviceCreated) {
            & sc.exe stop $serviceName | Out-Null
            $stopDeadline = [DateTime]::UtcNow.AddSeconds(30)
            do { Start-Sleep -Milliseconds 500; $service = Get-ServiceRecord $serviceName } while ($null -ne $service -and $service.state -ne 'Stopped' -and [DateTime]::UtcNow -lt $stopDeadline)
            if ($null -ne $service -and $service.state -ne 'Stopped') { $cleanupFailures.Add("Disposable C++ service did not stop: $serviceName") }
            & sc.exe delete $serviceName | Out-Null
            if ($LASTEXITCODE -ne 0) { $cleanupFailures.Add("sc.exe delete failed with exit code $LASTEXITCODE") }
            $deleteDeadline = [DateTime]::UtcNow.AddSeconds(30)
            do { Start-Sleep -Milliseconds 500; $service = Get-ServiceRecord $serviceName } while ($null -ne $service -and [DateTime]::UtcNow -lt $deleteDeadline)
            if ($null -ne $service) { $cleanupFailures.Add("Disposable C++ service was not deleted: $serviceName") }
        }
        if ($sqlPrincipalCreated) {
            try {
                Invoke-SqlStrict -Database $database -Query "IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name=N'NT AUTHORITY\LOCAL SERVICE') DROP USER [NT AUTHORITY\LOCAL SERVICE];" | Out-Null
                Invoke-SqlStrict -Database 'master' -Query "IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'NT AUTHORITY\LOCAL SERVICE') DROP LOGIN [NT AUTHORITY\LOCAL SERVICE];" | Out-Null
            } catch { $cleanupFailures.Add("SQL principal cleanup failed: $($_.Exception.Message)") }
        }
        try { Remove-MarkerRows -Database $database -MarkerPrefix $Seed.markerPrefix } catch { $cleanupFailures.Add("SQL marker cleanup failed: $($_.Exception.Message)") }
        try { Remove-MarkerFiles -DataRoot $dataRoot -MarkerPrefix $Seed.markerPrefix } catch { $cleanupFailures.Add("Data marker cleanup failed: $($_.Exception.Message)") }
    }
    $afterSnapshot = Get-SqlSnapshot -Database $database -MarkerPrefix $Seed.markerPrefix
    $dataAfter = Get-LiveBenchmarkDirectoryFingerprint $dataRoot
    $cleanup = [ordered]@{
        messageRowsAbsent = $afterSnapshot.markedQueueRows -eq 0 -and $afterSnapshot.markedInboxRows -eq 0
        recipientRowsAbsent = $afterSnapshot.markedRecipientRows -eq 0
        inboxRowsAbsent = $afterSnapshot.markedInboxFolderRows -eq 0
        dataFilesAbsent = (Get-SeedFileFingerprint -DataRoot $dataRoot -MarkerPrefix $Seed.markerPrefix).fileCount -eq 0
        serviceAbsent = if ($Implementation -eq 'cpp') { $null -eq (Get-ServiceRecord $serviceName) } else { $null -eq (Get-Process -Id $workerPid -ErrorAction SilentlyContinue) }
        baselineSqlRestored = $beforeSnapshot.messageCount -eq $afterSnapshot.messageCount -and $beforeSnapshot.metadataCount -eq $afterSnapshot.metadataCount -and $beforeSnapshot.recipientCount -eq $afterSnapshot.recipientCount
        baselineDataRestored = $dataBefore.fileCount -eq $dataAfter.fileCount -and $dataBefore.bytes -eq $dataAfter.bytes -and $dataBefore.sha256 -eq $dataAfter.sha256
    }
    $cleanupPass = $cleanupFailures.Count -eq 0 -and @($cleanup.Values | Where-Object { $_ -ne $true }).Count -eq 0
    $completed = $null -eq $runError -and $latencies.Count -eq $MessageCount
    $durationSeconds = if ($null -ne $drainStartedUtc -and $null -ne $drainEndedUtc) { ($drainEndedUtc - $drainStartedUtc).TotalSeconds } else { 0 }
    [pscustomobject]@{
        implementation = $Implementation
        status = if ($completed -and $cleanupPass) { 'PASS' } elseif ($readinessFailures.Count -gt 0) { 'BLOCKED' } else { 'FAIL' }
        database = $database
        dataRoot = $dataRoot
        executable = $executable
        executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToUpperInvariant()
        serviceName = $serviceName
        workerPid = $workerPid
        startedUtc = $startUtc.ToString('o')
        drainStartedUtc = if ($null -ne $drainStartedUtc) { $drainStartedUtc.ToString('o') } else { $null }
        drainEndedUtc = if ($null -ne $drainEndedUtc) { $drainEndedUtc.ToString('o') } else { $null }
        endedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        readinessFailures = @($readinessFailures)
        error = $runError
        metrics = [ordered]@{
            sampleCount = $latencies.Count
            terminal = $completed
            p50_ms = Get-Percentile -Values $latencies.ToArray() -Percent 0.50
            p95_ms = Get-Percentile -Values $latencies.ToArray() -Percent 0.95
            p99_ms = Get-Percentile -Values $latencies.ToArray() -Percent 0.99
            total_ms = if ($completed) { [math]::Round(($drainEndedUtc - $drainStartedUtc).TotalMilliseconds, 3) } else { $null }
            throughput_messages_per_second = if ($completed -and $durationSeconds -gt 0) { [math]::Round($MessageCount / $durationSeconds, 3) } else { $null }
            latencies_ms = @($latencies | ForEach-Object { [math]::Round($_, 3) })
        }
        resourceBefore = $resourceBefore
        resourceAfter = $resourceAfter
        resourceDelta = if ($null -ne $resourceBefore -and $null -ne $resourceAfter) { [ordered]@{ privateBytes = $resourceAfter.privateBytes - $resourceBefore.privateBytes; handles = $resourceAfter.handles - $resourceBefore.handles; threads = $resourceAfter.threads - $resourceBefore.threads; cpuTimeMs = $resourceAfter.cpuTimeMs - $resourceBefore.cpuTimeMs } } else { $null }
        cleanup = $cleanup
        cleanupFailures = @($cleanupFailures)
        sqlBefore = $beforeSnapshot
        sqlAfter = $afterSnapshot
        dataBefore = $dataBefore
        dataAfter = $dataAfter
    }
}

$runToken = Get-SafeGuid $RunId
$startUtc = [DateTimeOffset]::UtcNow
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\paired-local-delivery-queue-$($startUtc.ToString('yyyyMMdd_HHmmss'))" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
$benchmarkArtifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\benchmarks')).TrimEnd('\')
if (-not $OutputDirectory.StartsWith($benchmarkArtifactRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "OutputDirectory must be under the repository benchmark artifacts directory: $benchmarkArtifactRoot" }
if (Test-Path -LiteralPath $OutputDirectory) { throw "Refusing to overwrite existing benchmark output: $OutputDirectory" }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$report = $null
$fixture = $null
$cppFixture = $null
$net10Fixture = $null
$cppSeed = $null
$net10Seed = $null
$blocker = $null
$results = [ordered]@{ cpp = $null; net10 = $null }
$manifestFullPath = [IO.Path]::GetFullPath($FixtureManifest)
try {
    $cppFixture = Read-LiveBenchmarkFixtureManifest -Path $manifestFullPath -Implementation cpp -RepositoryRoot $repoRoot
    $net10Fixture = Read-LiveBenchmarkFixtureManifest -Path $manifestFullPath -Implementation net10 -RepositoryRoot $repoRoot
    Assert-True ($cppFixture.fixtureId -eq $net10Fixture.fixtureId -and $cppFixture.sha256 -eq $net10Fixture.sha256) 'C++ and Net10 fixture identity is not shared.'
    Assert-True ($cppFixture.database -ne $net10Fixture.database -and $cppFixture.dataRoot -ne $net10Fixture.dataRoot) 'C++ and Net10 fixture targets must be separate.'
    Assert-True ($null -ne $cppFixture.expectedDataFingerprint -and $null -ne $cppFixture.expectedMessageFingerprint -and
        $null -ne $net10Fixture.expectedDataFingerprint -and $null -ne $net10Fixture.expectedMessageFingerprint) 'Fixture does not prove exact Data/message parity.'
    Assert-True ([int]$cppFixture.expectedDatabaseVersion -eq 5708 -and [int]$net10Fixture.expectedDatabaseVersion -eq 6000) 'Fixture schema versions do not preserve C++ 5708 and Net10 6000.'
    Assert-True ($CppServiceName -ne 'hMailServer' -and $CppServiceName -match '^[A-Za-z0-9_.-]{1,255}$') 'CppServiceName is not a disposable SCM name.'
    $ports = [ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 }
    $cppProvenance = Get-LiveBenchmarkProvenance -FixtureManifest $manifestFullPath -Implementation cpp -RepositoryRoot $repoRoot -Database $cppFixture.database -DataRoot $cppFixture.dataRoot -ServiceExecutable $cppFixture.executable -Ports $ports
    $net10Provenance = Get-LiveBenchmarkProvenance -FixtureManifest $manifestFullPath -Implementation net10 -RepositoryRoot $repoRoot -Database $net10Fixture.database -DataRoot $net10Fixture.dataRoot -ServiceExecutable $net10Fixture.executable -Ports $ports
    $cppAttestation = Assert-LiveBenchmarkRunStartAttestation -FixtureManifest $manifestFullPath -Implementation cpp -RepositoryRoot $repoRoot -Database $cppFixture.database -DataRoot $cppFixture.dataRoot -ServiceExecutable $cppFixture.executable
    $net10Attestation = Assert-LiveBenchmarkRunStartAttestation -FixtureManifest $manifestFullPath -Implementation net10 -RepositoryRoot $repoRoot -Database $net10Fixture.database -DataRoot $net10Fixture.dataRoot -ServiceExecutable $net10Fixture.executable
    $markerPrefix = "paired-local-delivery-$runToken"
    $cppBaseline = [pscustomobject]@{ sql = Get-SqlSnapshot -Database $cppFixture.database -MarkerPrefix $markerPrefix; data = Get-LiveBenchmarkDirectoryFingerprint $cppFixture.dataRoot }
    $net10Baseline = [pscustomobject]@{ sql = Get-SqlSnapshot -Database $net10Fixture.database -MarkerPrefix $markerPrefix; data = Get-LiveBenchmarkDirectoryFingerprint $net10Fixture.dataRoot }
    $cppSeed = New-SeedCorpus -Fixture $cppFixture -Implementation cpp -MarkerPrefix $markerPrefix -Token $runToken
    $net10Seed = New-SeedCorpus -Fixture $net10Fixture -Implementation net10 -MarkerPrefix $markerPrefix -Token $runToken
    Assert-True ($cppSeed.fingerprint.fileCount -eq $net10Seed.fingerprint.fileCount -and $cppSeed.fingerprint.bytes -eq $net10Seed.fingerprint.bytes -and $cppSeed.fingerprint.sha256 -eq $net10Seed.fingerprint.sha256) 'C++ and Net10 seed files are not byte-matched.'
    $results.cpp = Invoke-SideBenchmark -Implementation cpp -Fixture $cppFixture -Seed $cppSeed -Baseline $cppBaseline
    $results.net10 = Invoke-SideBenchmark -Implementation net10 -Fixture $net10Fixture -Seed $net10Seed -Baseline $net10Baseline
}
catch {
    $blocker = $_.Exception.Message
}
finally {
    foreach ($entry in @(@{ fixture = $cppFixture; seed = $cppSeed }, @{ fixture = $net10Fixture; seed = $net10Seed })) {
        if ($null -ne $entry.fixture -and $null -ne $entry.seed) {
            try { Remove-MarkerRows -Database $entry.fixture.database -MarkerPrefix $entry.seed.markerPrefix } catch { $blocker = if ($null -eq $blocker) { "Final SQL cleanup failed: $($_.Exception.Message)" } else { "$blocker; Final SQL cleanup failed: $($_.Exception.Message)" } }
            try { Remove-MarkerFiles -DataRoot $entry.fixture.dataRoot -MarkerPrefix $entry.seed.markerPrefix } catch { $blocker = if ($null -eq $blocker) { "Final Data cleanup failed: $($_.Exception.Message)" } else { "$blocker; Final Data cleanup failed: $($_.Exception.Message)" } }
        }
    }
    $endedUtc = [DateTimeOffset]::UtcNow
    $status = if ($null -eq $blocker -and $null -ne $results.cpp -and $null -ne $results.net10 -and $results.cpp.status -eq 'PASS' -and $results.net10.status -eq 'PASS') { 'PASS' } elseif ($null -ne $blocker) { 'BLOCKED' } else { 'FAIL' }
    $fixtureSummary = if ($null -ne $cppFixture) {
        [ordered]@{
            fixtureId = $cppFixture.fixtureId
            manifestPath = $manifestFullPath
            manifestSha256 = $cppFixture.sha256
            cppDatabase = $cppFixture.database
            net10Database = if ($null -ne $net10Fixture) { $net10Fixture.database } else { $null }
            cppDataRoot = $cppFixture.dataRoot
            net10DataRoot = if ($null -ne $net10Fixture) { $net10Fixture.dataRoot } else { $null }
            cppDatabaseVersion = $cppFixture.expectedDatabaseVersion
            net10DatabaseVersion = if ($null -ne $net10Fixture) { $net10Fixture.expectedDatabaseVersion } else { $null }
            dataParity = [ordered]@{ fileCount = $cppFixture.expectedDataFingerprint.fileCount; bytes = $cppFixture.expectedDataFingerprint.bytes; sha256 = $cppFixture.expectedDataFingerprint.sha256; exact = $true }
            messageParity = [ordered]@{ rowCount = $cppFixture.expectedMessageFingerprint.rowCount; sha256 = $cppFixture.expectedMessageFingerprint.sha256; exact = $true }
        }
    } else {
        [ordered]@{ fixtureId = $null; manifestPath = $manifestFullPath; manifestSha256 = $null; cppDatabase = $null; net10Database = $null; cppDataRoot = $null; net10DataRoot = $null; cppDatabaseVersion = $null; net10DatabaseVersion = $null; dataParity = $null; messageParity = $null }
    }
    $seedSummary = if ($null -ne $cppSeed -and $null -ne $net10Seed) { [ordered]@{ messageCount = $MessageCount; markerPrefix = $cppSeed.markerPrefix; cpp = [ordered]@{ fileCount = $cppSeed.fingerprint.fileCount; bytes = $cppSeed.fingerprint.bytes; fileSha256 = $cppSeed.fingerprint.sha256 }; net10 = [ordered]@{ fileCount = $net10Seed.fingerprint.fileCount; bytes = $net10Seed.fingerprint.bytes; fileSha256 = $net10Seed.fingerprint.sha256 } } } else { [ordered]@{ messageCount = $MessageCount; markerPrefix = $null; cpp = [ordered]@{ fileCount = 0; bytes = 0; fileSha256 = $null }; net10 = [ordered]@{ fileCount = 0; bytes = 0; fileSha256 = $null } } }
    $report = [ordered]@{
        schema = 'paired-local-delivery-queue-v1'
        status = $status
        startedUtc = $startUtc.ToString('o')
        endedUtc = $endedUtc.ToString('o')
        runId = $runToken
        decision = if ($status -eq 'PASS') { 'Paired drain evidence only; no release decision.' } else { "Paired drain run incomplete or blocked. Blocker: $blocker" }
        blocker = $blocker
        fixture = $fixtureSummary
        seed = $seedSummary
        configuration = [ordered]@{ messageCount = $MessageCount; readinessTimeoutSeconds = $ReadinessTimeoutSeconds; drainTimeoutSeconds = $DrainTimeoutSeconds; pollMilliseconds = $PollMilliseconds; bind = '127.0.0.1'; ports = $ports; cppServiceName = $CppServiceName; sqlAuthentication = 'Integrated Security'; isolation = 'manifest-bound disposable paired C++/Net10 roots'; productionCodeModified = $false }
        resources = [ordered]@{ computerName = $env:COMPUTERNAME; os = (Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue | Select-Object Caption,Version,BuildNumber); processorCount = [Environment]::ProcessorCount; powershell = $PSVersionTable.PSVersion.ToString(); utc = $endedUtc.ToString('o') }
        provenance = [ordered]@{ cpp = $cppProvenance; net10 = $net10Provenance; cppRunStartAttestation = $cppAttestation; net10RunStartAttestation = $net10Attestation }
        results = $results
    }
    $jsonPath = Join-Path $OutputDirectory 'paired-local-delivery-queue.json'
    $csvPath = Join-Path $OutputDirectory 'paired-local-delivery-queue.csv'
    $markdownPath = Join-Path $OutputDirectory 'paired-local-delivery-queue.md'
    $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
    @($results.cpp, $results.net10) | Where-Object { $null -ne $_ } | ForEach-Object {
        [pscustomobject]@{ implementation = $_.implementation; status = $_.status; sample_count = $_.metrics.sampleCount; p50_ms = $_.metrics.p50_ms; p95_ms = $_.metrics.p95_ms; p99_ms = $_.metrics.p99_ms; total_ms = $_.metrics.total_ms; throughput_messages_per_second = $_.metrics.throughput_messages_per_second; cleanup_pass = (@($_.cleanup.Values | Where-Object { $_ -ne $true }).Count -eq 0); executable_sha256 = $_.executableSha256; data_before_sha256 = $_.dataBefore.sha256; data_after_sha256 = $_.dataAfter.sha256; started_utc = $_.startedUtc; ended_utc = $_.endedUtc }
    } | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
    $markdownRows = foreach ($row in @($results.cpp, $results.net10)) {
        if ($null -ne $row) { "| $($row.implementation) | $($row.status) | $($row.metrics.sampleCount) | $($row.metrics.p50_ms) | $($row.metrics.p95_ms) | $($row.metrics.p99_ms) | $($row.metrics.throughput_messages_per_second) | $((@($row.cleanup.Values | Where-Object { $_ -ne $true }).Count -eq 0)) |" }
    }
    @(
        '# Paired local-delivery queue drain',
        '',
        "- Status: **$status**",
        "- Run ID: $runToken",
        "- Fixture ID: $($fixtureSummary.fixtureId)",
        "- Fixture manifest SHA-256: $($fixtureSummary.manifestSha256)",
        "- Seed messages: $MessageCount; C++/Net10 byte-matched seed SHA-256: $($seedSummary.cpp.fileSha256)",
        '- No winner claim: paired drain evidence is not a release decision.',
        "- Configuration: bind=$($ports.bind); SMTP=$($ports.smtp); IMAP=$($ports.imap); POP3=$($ports.pop3); poll=${PollMilliseconds}ms",
        "- Blocker: $blocker",
        '',
        '| implementation | status | samples | p50 ms | p95 ms | p99 ms | throughput msg/s | cleanup |',
        '|---|---:|---:|---:|---:|---:|---:|---:|',
        $markdownRows
    ) | Set-Content -LiteralPath $markdownPath -Encoding UTF8
    Write-Output ($report | ConvertTo-Json -Depth 20)
    if ($status -ne 'PASS') { throw "Paired local-delivery queue benchmark did not complete: $OutputDirectory" }
}
