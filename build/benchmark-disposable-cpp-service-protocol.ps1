param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureManifest,
    [string]$OutputDirectory = "",
    [ValidateSet("protocol", "concurrent-imap", "smtp")]
    [string]$Workload = "protocol",
    [int]$Iterations = 25,
    [ValidateSet("Admission", "AuthSelect", "Search", "Sort", "Full")]
    [string]$Profile = "Full",
    [ValidateRange(1, 5000)]
    [int]$Concurrency = 1000,
    [ValidateRange(1, 100)]
    [int]$Waves = 1,
    [ValidateRange(500, 30000)]
    [int]$TimeoutMilliseconds = 5000,
    [ValidateRange(0, 60)]
    [int]$PostWorkloadSettleSeconds = 5,
    [ValidateRange(0, 1000)]
    [int]$LaunchStaggerMilliseconds = 0,
    [ValidateRange(1, 5000)]
    [int]$SqlMaxPoolSize = 100,
    [ValidateRange(1, 100000)]
    [int]$MessageCount = 100,
    [switch]$VerifyLocalDeliveryReadback,
    [ValidateRange(1, 1000000)]
    [int]$CorpusMessageCount = 1000,
    [ValidateRange(1, 60)]
    [int]$PostAcceptanceTimeoutSeconds = 10,
    [ValidateRange(5, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$RunId = "",
    [string]$ServiceName = "hMailServerPerfCppProtocol",
    [string]$ServiceAccount = "NT AUTHORITY\LocalService"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")
. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")

function Assert-DisposableServiceName {
    if ($ServiceName -eq "hMailServer" -or $ServiceName -notmatch '^[A-Za-z0-9_.-]{1,255}$') {
        throw "ServiceName must be a non-production disposable SCM name."
    }
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

function Invoke-SqlStrict {
    param([string]$Query, [string]$Database = "master")

    $output = @(& sqlcmd.exe -S localhost -E -b -d $Database -Q $Query 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed against disposable database '$Database' with exit code ${LASTEXITCODE}: $($output -join ' ')"
    }
    return $output
}

function Get-SqlScalar {
    param([string]$Query, [string]$Database = "master")

    $value = (@(& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -Q $Query 2>&1) -join " ").Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd scalar query failed against disposable database '$Database'."
    }
    return $value
}

function Get-ListeningPids {
    param([int]$Port)

    @(Get-NetTCPConnection -State Listen -LocalAddress 127.0.0.1 -LocalPort $Port -ErrorAction SilentlyContinue |
        ForEach-Object { [int]$_.OwningProcess })
}

function Get-TcpIpPortPreflight {
    param([string]$Database)

    $query = "SET NOCOUNT ON; SELECT CONCAT(portprotocol, '|', portnumber, '|', portaddress1, '|', COALESCE(CONVERT(varchar(32), portaddress2), 'NULL')) FROM hm_tcpipports ORDER BY portprotocol, portnumber, portaddress1;"
    $observed = @(& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -s '|' -Q $query 2>&1 |
        ForEach-Object { ([string]$_).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read hm_tcpipports from disposable database '$Database'."
    }
    $expected = @('1|2525|2130706433|NULL', '3|25110|2130706433|NULL', '5|1143|2130706433|NULL')
    $observedSorted = @($observed | Sort-Object)
    $expectedSorted = @($expected | Sort-Object)
    [pscustomobject]@{
        status = if (($observedSorted -join "`n") -ceq ($expectedSorted -join "`n")) { 'PASS' } else { 'FAIL' }
        expected = $expectedSorted
        observed = $observedSorted
        database = $Database
        bind = '127.0.0.1'
        ports = [ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 }
    }
}

function Wait-ForServiceReadiness {
    param([string]$Name, [int]$ExpectedProcessId)

    $deadline = [DateTime]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    do {
        $service = Get-ServiceRecord $Name
        $portsReady = @(2525, 1143, 25110 | Where-Object { (Get-ListeningPids $_).Count -gt 0 })
        $ownersValid = @($portsReady | Where-Object { (Get-ListeningPids $_) -notcontains $ExpectedProcessId }).Count -eq 0
        if ($null -ne $service -and $service.state -eq "Running" -and $service.processId -eq $ExpectedProcessId -and $portsReady.Count -eq 3 -and $ownersValid) {
            return @()
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    $service = Get-ServiceRecord $Name
    $missing = @(2525, 1143, 25110 | Where-Object { (Get-ListeningPids $_).Count -eq 0 })
    @("Service readiness failed: state=$($service.state), pid=$($service.processId), missingPorts=$($missing -join ',')")
}

Assert-DisposableServiceName
if ($ServiceAccount -ne "NT AUTHORITY\LocalService") {
    throw "This bounded runner permits only NT AUTHORITY\LocalService."
}

$fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation cpp -RepositoryRoot $repoRoot
$serviceExe = [IO.Path]::GetFullPath($fixture.executable)
$stagingRoot = Split-Path -Parent $fixture.dataRoot
$database = $fixture.database
if ($serviceExe -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+(?:\\cpp)?\\Bin\\hMailServer\.exe$') {
    throw "Fixture C++ executable is outside the approved disposable service roots: $serviceExe"
}
if ($stagingRoot -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+(?:\\cpp)?$') {
    throw "Fixture C++ staging root is outside the approved disposable roots: $stagingRoot"
}
if ($database -notmatch '^hmail_perf_pair_cpp_[a-z0-9_]+$') {
    throw "Fixture C++ database is not disposable: $database"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot ("artifacts\benchmarks\paired-cpp-net10-20260901-service\{0}-cpp-service" -f $Workload)
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory -notmatch '(?i)\\artifacts\\benchmarks\\paired-cpp-net10-[a-z0-9_-]+(?:\\[^\\]+)?$') {
    throw "OutputDirectory is outside the repository benchmark artifact boundary: $OutputDirectory"
}
$workloadScript = if ($Workload -eq "protocol") {
    Join-Path $PSScriptRoot "benchmark-net10-live-protocol.ps1"
} elseif ($Workload -eq "concurrent-imap") {
    Join-Path $PSScriptRoot "benchmark-net10-live-concurrent-imap.ps1"
} else {
    Join-Path $PSScriptRoot "benchmark-net10-live-smtp-acceptance.ps1"
}
$childOutputDirectory = Join-Path $OutputDirectory $Workload
$childJsonName = switch ($Workload) {
    "protocol" { "net10-live-protocol.json"; break }
    "concurrent-imap" { "live-concurrent-imap.json"; break }
    default { "cpp-smtp-message-acceptance.json" }
}

$productionService = Get-ServiceRecord "hMailServer"
if ($null -ne $productionService -and $productionService.state -ne "Stopped") {
    throw "The production-named hMailServer service is not stopped; refusing disposable service benchmark."
}
if ($null -ne (Get-ServiceRecord $ServiceName)) {
    throw "Refusing to reuse an existing disposable service: $ServiceName"
}
$preflight = Get-CppIsolationPreflight -TargetExecutable $serviceExe -ExpectedStagingRoot $stagingRoot -ExpectedDatabase $database -DisposableRegistrationGuarded
if (-not $preflight.passed) { throw (($preflight.failures) -join [Environment]::NewLine) }
$portPreflight = Get-TcpIpPortPreflight $database
if ($portPreflight.status -ne 'PASS') {
    throw "Disposable hm_tcpipports rows do not exactly match loopback SMTP/IMAP/POP3: $($portPreflight.observed -join ', ')"
}

$principal = "NT AUTHORITY\LOCAL SERVICE"
$principalSql = $principal.Replace("'", "''")
$loginExists = [int](Get-SqlScalar "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.server_principals WHERE name = N'$principalSql';")
if ($loginExists -ne 0) {
    throw "Disposable SQL login already exists; refusing to change an existing principal."
}
$sqlPrincipalCreated = $false
$serviceCreated = $false
$serviceStarted = $false
$servicePid = 0
$childExitCode = $null
$childReport = $null
$readinessFailures = [System.Collections.Generic.List[string]]::new()
$cleanupFailures = [System.Collections.Generic.List[string]]::new()
$startUtc = [DateTimeOffset]::UtcNow
$binPath = '"{0}" /DisposableBenchmark /ServiceName={1} RunAsService' -f $serviceExe, $ServiceName

try {
    Invoke-SqlStrict "CREATE LOGIN [$principal] FROM WINDOWS;" | Out-Null
    $sqlPrincipalCreated = $true
    Invoke-SqlStrict "CREATE USER [$principal] FOR LOGIN [$principal]; ALTER ROLE [db_owner] ADD MEMBER [$principal];" $database | Out-Null

    $createArgs = @('create', $ServiceName, 'binPath=', $binPath, 'start=', 'demand', 'type=', 'own', 'DisplayName=', $ServiceName, 'obj=', $ServiceAccount)
    & sc.exe @createArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with exit code $LASTEXITCODE" }
    $serviceCreated = $true
    & sc.exe start $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe start failed with exit code $LASTEXITCODE" }
    $serviceStarted = $true

    $deadline = [DateTime]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $record = Get-ServiceRecord $ServiceName
        if ($null -ne $record -and $record.state -eq "Running" -and $record.processId -gt 0) {
            $servicePid = $record.processId
            break
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($servicePid -le 0) {
        $readinessFailures.Add("Disposable service did not expose a running worker PID.")
    }
    else {
        foreach ($failure in @(Wait-ForServiceReadiness $ServiceName $servicePid)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$failure)) { $readinessFailures.Add([string]$failure) }
        }
    }

    if ($readinessFailures.Count -eq 0) {
        $childArgs = @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $workloadScript,
            '-Implementation', 'cpp',
            '-FixtureManifest', $FixtureManifest,
            '-OutputDirectory', $childOutputDirectory,
            '-BenchmarkStagingRoot', $stagingRoot,
            '-BenchmarkDatabase', $database,
            '-BenchmarkServiceExecutable', $serviceExe,
            '-ReadinessTimeoutSeconds', $ReadinessTimeoutSeconds,
            '-ExternalServiceProcessId', $servicePid,
            '-ExternalServiceName', $ServiceName
        )
        if ($Workload -eq "protocol") {
            $childArgs += @('-Iterations', $Iterations)
        } elseif ($Workload -eq "concurrent-imap") {
            $childArgs += @(
                '-Profile', $Profile,
                '-Concurrency', $Concurrency,
                '-Waves', $Waves,
                '-TimeoutMilliseconds', $TimeoutMilliseconds,
                '-PostWorkloadSettleSeconds', $PostWorkloadSettleSeconds,
                '-LaunchStaggerMilliseconds', $LaunchStaggerMilliseconds,
                '-SqlMaxPoolSize', $SqlMaxPoolSize,
                '-ExpectedMessageCount', $CorpusMessageCount
            )
        } else {
            $childArgs += @(
                '-MessageCount', $MessageCount,
                '-PostAcceptanceTimeoutSeconds', $PostAcceptanceTimeoutSeconds,
                '-PostWorkloadSettleSeconds', $PostWorkloadSettleSeconds
            )
            if ($VerifyLocalDeliveryReadback) { $childArgs += '-VerifyLocalDeliveryReadback' }
        }
        if (-not [string]::IsNullOrWhiteSpace($RunId)) { $childArgs += @('-RunId', $RunId) }
        & powershell.exe @childArgs 2>&1 | Out-Host
        $childExitCode = $LASTEXITCODE
        $childJsonPath = Join-Path $childOutputDirectory $childJsonName
        if (Test-Path -LiteralPath $childJsonPath -PathType Leaf) {
            $childReport = Get-Content -LiteralPath $childJsonPath -Raw | ConvertFrom-Json
        }
        if ($childExitCode -ne 0) { $readinessFailures.Add("Protocol child exited with code $childExitCode.") }
    }
}
catch {
    $readinessFailures.Add($_.Exception.Message)
}
finally {
    if ($serviceCreated) {
        & sc.exe stop $ServiceName | Out-Null
        if ($LASTEXITCODE -ne 0 -and $serviceStarted) { $cleanupFailures.Add("sc.exe stop failed with exit code $LASTEXITCODE") }
        $stopDeadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $record = Get-ServiceRecord $ServiceName
        } while ($null -ne $record -and $record.state -ne "Stopped" -and [DateTime]::UtcNow -lt $stopDeadline)
        if ($null -ne $record -and $record.state -ne "Stopped") { $cleanupFailures.Add("Disposable service did not stop: $ServiceName") }
        & sc.exe delete $ServiceName | Out-Null
        if ($LASTEXITCODE -ne 0) { $cleanupFailures.Add("sc.exe delete failed with exit code $LASTEXITCODE") }
        $deleteDeadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $record = Get-ServiceRecord $ServiceName
        } while ($null -ne $record -and [DateTime]::UtcNow -lt $deleteDeadline)
        if ($null -ne $record) { $cleanupFailures.Add("Disposable service was not deleted: $ServiceName") }
    }
    if ($sqlPrincipalCreated) {
        try {
            Invoke-SqlStrict "IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$principalSql') DROP USER [$principal];" $database | Out-Null
            Invoke-SqlStrict "IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$principalSql') DROP LOGIN [$principal];" | Out-Null
        }
        catch { $cleanupFailures.Add("SQL principal cleanup failed: $($_.Exception.Message)") }
    }
}

$endUtc = [DateTimeOffset]::UtcNow
$productionAfter = Get-ServiceRecord "hMailServer"
$productionUntouched = ($null -eq $productionAfter -and $null -eq $productionService) -or ($null -ne $productionService -and $null -ne $productionAfter -and $productionService.state -eq $productionAfter.state -and $productionService.processId -eq $productionAfter.processId)
$reportStem = switch ($Workload) {
    "protocol" { "disposable-cpp-service-protocol"; break }
    "concurrent-imap" { "disposable-cpp-service-concurrent-imap"; break }
    default { "disposable-cpp-service-smtp" }
}
$report = [pscustomobject]@{
    schema = switch ($Workload) {
        "protocol" { "disposable-cpp-service-protocol-v1"; break }
        "concurrent-imap" { "disposable-cpp-service-concurrent-imap-v1"; break }
        default { "disposable-cpp-service-smtp-v1" }
    }
    workload = $Workload
    status = if ($readinessFailures.Count -eq 0 -and $cleanupFailures.Count -eq 0 -and $null -ne $childReport -and $childReport.status -eq "PASS") { "PASS" } else { "FAIL" }
    startedUtc = $startUtc.ToString("o")
    endedUtc = $endUtc.ToString("o")
    fixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
    fixtureId = $fixture.fixtureId
    fixtureManifestSha256 = $fixture.sha256
    executable = $serviceExe
    executableSha256 = (Get-FileHash -LiteralPath $serviceExe -Algorithm SHA256).Hash
    stagingRoot = $stagingRoot
    database = $database
    corpusMessageCount = $CorpusMessageCount
    verifyLocalDeliveryReadback = [bool]$VerifyLocalDeliveryReadback
    serviceName = $ServiceName
    serviceAccount = $ServiceAccount
    commandLine = $binPath
    workerPid = $servicePid
    listenerBind = "127.0.0.1"
    ports = @(2525, 1143, 25110)
    childReport = $childReport
    childExitCode = $childExitCode
    preflight = $preflight
    tcpipPortPreflight = $portPreflight
    readinessFailures = @($readinessFailures)
    cleanupFailures = @($cleanupFailures)
    sqlPrincipalCreatedAndRemoved = $sqlPrincipalCreated -and [int](Get-SqlScalar "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.server_principals WHERE name = N'$principalSql';") -eq 0
    productionServiceUntouched = $productionUntouched
}
$jsonPath = Join-Path $OutputDirectory ($reportStem + ".json")
$markdownPath = Join-Path $OutputDirectory ($reportStem + ".md")
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
@(
    "# Disposable C++ Service $Workload Benchmark",
    "",
    "- Status: **$($report.status)**",
    "- Fixture: $($report.fixtureId) ($($report.fixtureManifestSha256))",
    "- Service: $ServiceName (created, exercised, stopped, and deleted)",
    "- Worker PID: $servicePid",
    "- Database: $database",
    "- Data root: $stagingRoot\Data",
    "- Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110",
    "- Workload report: $(Join-Path $childOutputDirectory $childJsonName)",
    "- Production service untouched: $productionUntouched",
    "- SQL principal removed: $($report.sqlPrincipalCreatedAndRemoved)",
    "- Readiness failures: $($readinessFailures.Count)",
    "- Cleanup failures: $($cleanupFailures.Count)"
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8

if ($report.status -ne "PASS") { throw "Disposable C++ service $Workload benchmark failed. See $jsonPath" }
Write-Output ($report | ConvertTo-Json -Depth 12)
