param(
    [int]$Iterations = 25,
    [int]$DurationSeconds = 90,
    [ValidateRange(1, 300)]
    [int]$ReadinessTimeoutSeconds = 60,
    [string]$OutputDirectory = "",
    [string]$BenchmarkStagingRoot = "",
    [string]$BenchmarkDatabase = "",
    [string]$BenchmarkServiceExecutable = "",
    [string]$FixtureManifest = "",
    [string]$RunId = "",
    [ValidateSet("net10", "cpp")]
    [string]$Implementation = "net10",
    [int]$ExternalServiceProcessId = 0,
    [string]$ExternalServiceName = ""
)

$ErrorActionPreference = "Stop"

if ($ExternalServiceProcessId -gt 0 -and $Implementation -ne "cpp") {
    throw "ExternalServiceProcessId is supported only for the legacy C++ implementation."
}
if ($ExternalServiceProcessId -gt 0 -and $ExternalServiceName -notmatch '^[A-Za-z0-9_.-]{1,255}$') {
    throw "ExternalServiceName must be supplied and must be a disposable SCM service name."
}

. (Join-Path $PSScriptRoot "live-cpp-isolation-preflight.ps1")
. (Join-Path $PSScriptRoot "live-imap-result-validation.ps1")
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

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
    $OutputDirectory = Join-Path $repoRoot "artifacts\benchmarks\live-cpp-net10-20260810_152708"
}

if (-not (Test-Path -LiteralPath $serviceExe -PathType Leaf)) {
    throw "Live listener host is missing: $serviceExe"
}
$dataRoot = Join-Path $stagingRoot "Data"
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
    throw "Disposable Data directory is missing: $stagingRoot\Data"
}

function Read-UntilTag {
    param([IO.StreamReader]$Reader, [string]$Tag)

    $lines = [System.Collections.Generic.List[string]]::new()
    $tagPrefix = $Tag.TrimEnd() + " "
    for ($index = 0; $index -lt 40; $index++) {
        $line = $Reader.ReadLine()
        if ($null -eq $line) {
            break
        }
        $lines.Add($line)
        if ($line.StartsWith($tagPrefix, [StringComparison]::OrdinalIgnoreCase)) {
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

function Get-CompletionTag {
    param([string[]]$Lines, [string]$Tag)

    $matching = @($Lines | Where-Object { $_ -like "$Tag *" } | Select-Object -Last 1)
    if ($matching.Count -eq 0) {
        return $null
    }
    return [string]$matching[0]
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
        $searchValidation = Test-ImapResultSequence -Lines $search -Command SEARCH -ExpectedCount 1000
        $sortValidation = Test-ImapResultSequence -Lines $sort -Command SORT -ExpectedCount 1000
        $searchCompletionTag = Get-CompletionTag $search "a003"
        $sortCompletionTag = Get-CompletionTag $sort "a004"

        # IMAPCommandSEARCH::ExecuteCommand emits this non-UID completion text;
        # IMAPCommandUID::ExecuteCommand uses "UID completed" only for UID commands.
        $legacyCompletionTagExpected = [pscustomobject]@{
            search = "a003 OK Search completed"
            sort = "a004 OK Search completed"
        }
        $legacyCompletionTagMatches = [pscustomobject]@{
            search = [bool]($searchCompletionTag -ceq $legacyCompletionTagExpected.search)
            sort = [bool]($sortCompletionTag -ceq $legacyCompletionTagExpected.sort)
        }
        $searchResponseIdentifierValid = [bool]($searchValidation.found -and $searchValidation.command -ceq "SEARCH")
        $sortResponseIdentifierValid = [bool]($sortValidation.found -and $sortValidation.command -ceq "SORT")
        $resultValidationError = @(
            $searchValidation.error
            $sortValidation.error
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $all = (($login + $select + $search + $sort + $logout) -join " ")
        $reader.Dispose()
        $writer.Dispose()
        $client.Dispose()
        $ok = $greeting -like "*OK*" `
            -and $all -like "*OK*" `
            -and $searchResponseIdentifierValid `
            -and $sortResponseIdentifierValid `
            -and $searchValidation.exactSequence `
            -and $sortValidation.exactSequence `
            -and $legacyCompletionTagMatches.search `
            -and $legacyCompletionTagMatches.sort
        $stopwatch.Stop()
        [pscustomobject]@{
            ok = [bool]$ok
            ms = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            error = if ($ok) { $null } else { "IMAP result/completion validation failed: $($resultValidationError -join '; ')" }
            searchResponseIdentifier = $searchValidation.command
            searchResponseIdentifierValid = $searchResponseIdentifierValid
            searchResultCount = $searchValidation.count
            searchResultFirst = $searchValidation.first
            searchResultLast = $searchValidation.last
            searchExactSequence = [bool]$searchValidation.exactSequence
            searchResultShape = $searchValidation.shape
            sortResponseIdentifier = $sortValidation.command
            sortResponseIdentifierValid = $sortResponseIdentifierValid
            sortResultCount = $sortValidation.count
            sortResultFirst = $sortValidation.first
            sortResultLast = $sortValidation.last
            sortExactSequence = [bool]$sortValidation.exactSequence
            sortResultShape = $sortValidation.shape
            currentCompletionTag = [pscustomobject]@{
                search = $searchCompletionTag
                sort = $sortCompletionTag
            }
            legacyCompletionTagExpected = $legacyCompletionTagExpected
            legacyCompletionTagMatches = $legacyCompletionTagMatches
        }
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
$runStartAttestation = $null
$externalService = $ExternalServiceProcessId -gt 0

$provenance = Get-LiveBenchmarkProvenance -FixtureManifest $FixtureManifest -RunId $RunId -Implementation $Implementation -RepositoryRoot $repoRoot -Database $database -DataRoot $dataRoot -ServiceExecutable $serviceExe -Ports ([ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 })

if ($Implementation -eq "cpp") {
    $preflight = Get-CppIsolationPreflight -TargetExecutable $serviceExe -ExpectedStagingRoot $stagingRoot -ExpectedDatabase $database -DisposableRegistrationGuarded:$externalService
    $readinessFailures = @($preflight.failures)
}

if ($null -eq $preflight -or $preflight.passed) {
    if ($provenance.manifestBound) {
        $runStartAttestation = Assert-LiveBenchmarkRunStartAttestation -FixtureManifest $FixtureManifest -Implementation $Implementation -RepositoryRoot $repoRoot -Database $database -DataRoot $dataRoot -ServiceExecutable $serviceExe
    }
    if ($externalService) {
        $process = Get-Process -Id $ExternalServiceProcessId -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            $readinessFailures += "External disposable C++ service worker PID $ExternalServiceProcessId is not running."
        }
        else {
            $workerRecord = Get-CimInstance Win32_Process -Filter "ProcessId=$ExternalServiceProcessId" -ErrorAction SilentlyContinue
            if ($null -eq $workerRecord -or
                -not [string]::Equals([IO.Path]::GetFullPath([string]$workerRecord.ExecutablePath), [IO.Path]::GetFullPath($serviceExe), [StringComparison]::OrdinalIgnoreCase)) {
                $readinessFailures += "External service worker PID $ExternalServiceProcessId is not the approved legacy executable."
            }
        }
    }
    else {
        $process = Start-Process -FilePath $serviceExe -ArgumentList $argumentList -WorkingDirectory (Split-Path -Parent $serviceExe) -PassThru -WindowStyle Hidden
    }
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
                        searchResponseIdentifier = $result.searchResponseIdentifier
                        searchResponseIdentifierValid = $result.searchResponseIdentifierValid
                        searchResultCount = $result.searchResultCount
                        searchResultFirst = $result.searchResultFirst
                        searchResultLast = $result.searchResultLast
                        searchExactSequence = $result.searchExactSequence
                        searchResultShape = $result.searchResultShape
                        sortResponseIdentifier = $result.sortResponseIdentifier
                        sortResponseIdentifierValid = $result.sortResponseIdentifierValid
                        sortResultCount = $result.sortResultCount
                        sortResultFirst = $result.sortResultFirst
                        sortResultLast = $result.sortResultLast
                        sortExactSequence = $result.sortExactSequence
                        sortResultShape = $result.sortResultShape
                        currentCompletionTag = $result.currentCompletionTag
                        legacyCompletionTagExpected = $result.legacyCompletionTagExpected
                        legacyCompletionTagMatches = $result.legacyCompletionTagMatches
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
    if (-not $externalService -and $null -ne $process -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        try { Stop-Process -Id $process.Id -Force } catch { $shutdownFailures += "Unable to stop launched process $($process.Id): $($_.Exception.Message)" }
    }
    if (-not $externalService -and $null -ne $process) {
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
    runId = $provenance.runId
    provenanceStatus = if ($provenance.manifestBound) { "MANIFEST_BOUND" } else { "UNBOUND" }
    fixtureId = $provenance.fixtureId
    manifestSha256 = $provenance.manifestSha256
    dataRoot = $dataRoot
    bind = "127.0.0.1"
    ports = "SMTP 2525, IMAP 1143, POP3 25110"
    messageCount = 1000
    summary = $summary
    readinessFailures = @($readinessFailures)
    shutdownFailures = @($shutdownFailures)
    processBefore = if ($null -ne $before) { @{ privateBytes = $before.privateBytes; handles = $before.handles; threads = $before.threads } } else { $null }
    processAfter = if ($null -ne $after) { @{ privateBytes = $after.privateBytes; handles = $after.handles; threads = $after.threads } } else { $null }
    isolationPreflight = $preflight
    executableProvenance = $provenance.executableProvenance
    runStartAttestation = $runStartAttestation
    samples = $samples
    serviceBacked = $externalService
    externalServiceName = if ($externalService) { $ExternalServiceName } else { $null }
    externalServiceProcessId = if ($externalService) { $ExternalServiceProcessId } else { $null }
    comHostedService = if ($Implementation -eq "net10") { "not started; installed AppID preserved" } elseif ($externalService) { "disposable SCM service; service wrapper owns cleanup" } else { "legacy /Debug path; AppID hash checked separately" }
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
$csvSamples = $samples | ForEach-Object {
    [pscustomobject]@{
        runId = $report.runId
        provenanceStatus = $report.provenanceStatus
        fixtureId = $report.fixtureId
        manifestSha256 = $report.manifestSha256
        implementation = $report.implementation
        database = $report.database
        dataRoot = $report.dataRoot
        executableSha256 = $report.executableProvenance.sha256
        runStartAttestationStatus = if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.status } else { "UNBOUND" }
        runStartDataSha256 = if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.dataSha256 } else { $null }
        runStartMessageSha256 = if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.messageSha256 } else { $null }
        scenario = $_.scenario
        iteration = $_.iteration
        ok = $_.ok
        ms = $_.ms
        error = $_.error
        searchResponseIdentifier = $_.searchResponseIdentifier
        searchResultCount = $_.searchResultCount
        searchExactSequence = $_.searchExactSequence
        sortResponseIdentifier = $_.sortResponseIdentifier
        sortResultCount = $_.sortResultCount
        sortExactSequence = $_.sortExactSequence
    }
}
$csvSamples | Export-Csv -LiteralPath $csvPath -NoTypeInformation
$markdown = @(
    "# Live protocol benchmark",
    "",
    "Implementation: $($report.implementation)",
    "Status: $($report.status)",
    "Database: $($report.database)",
    "Data root: $($report.dataRoot)",
    "Bind/ports: $($report.bind) / $($report.ports)",
    "Corpus files: $($report.messageCount)",
    "Run ID: $($report.runId)",
    "Provenance: $($report.provenanceStatus)",
    "Fixture ID: $($report.fixtureId)",
    "Fixture manifest SHA-256: $($report.manifestSha256)",
    "Executable SHA-256: $($report.executableProvenance.sha256)",
    "Run-start attestation: $(if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.status } else { 'UNBOUND' })",
    "Run-start Data SHA-256: $(if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.dataSha256 } else { '' })",
    "Run-start message SHA-256: $(if ($null -ne $report.runStartAttestation) { $report.runStartAttestation.messageSha256 } else { '' })",
    "",
    "| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |",
    "| --- | ---: | ---: | ---: | ---: | ---: |"
)
$markdown += $summary | ForEach-Object { "| $($_.scenario) | $($_.successes) | $($_.errors) | $($_.p50_ms) | $($_.p95_ms) | $($_.p99_ms) |" }
$markdown += "", "COM local-server registration was intentionally omitted; the installed Application registration was not changed.", "This single-implementation artifact does not calculate a C++/.NET 10 speed ratio."
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

$summary | Format-Table -AutoSize
Write-Output "JSON: $jsonPath"
