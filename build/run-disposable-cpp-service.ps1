param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [Parameter(Mandatory = $true)]
    [string]$StagingRoot,
    [Parameter(Mandatory = $true)]
    [string]$Database,
    [string]$OutputDirectory = "",
    [string]$ServiceName = "hMailServerPerfCpp",
    [string]$ServiceAccount = "LocalSystem",
    [string]$ServicePassword = "",
    [ValidateRange(5, 300)]
    [int]$ReadinessTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $repoRoot 'build\live-cpp-isolation-preflight.ps1')

function Get-RegistryState {
    $paths = @(
        'HKLM:\SOFTWARE\Classes\AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}',
        'HKLM:\SOFTWARE\WOW6432Node\Classes\AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}',
        'HKLM:\SYSTEM\CurrentControlSet\Services\hMailServer'
    )
    $state = [ordered]@{}
    foreach ($path in $paths) {
        $item = Get-ItemProperty -LiteralPath $path -ErrorAction SilentlyContinue
        if ($null -eq $item) {
            $state[$path] = $null
            continue
        }
        $properties = [ordered]@{}
        foreach ($property in $item.PSObject.Properties | Where-Object Name -notmatch '^PS') {
            $properties[$property.Name] = [string]$property.Value
        }
        $state[$path] = $properties
    }
    return [pscustomobject]$state
}

function Get-ServiceRecord {
    param([string]$Name)
    $service = Get-CimInstance Win32_Service -Filter "Name='$Name'" -ErrorAction SilentlyContinue
    if ($null -eq $service) { return $null }
    return [pscustomobject]@{
        name = [string]$service.Name
        state = [string]$service.State
        processId = [int]$service.ProcessId
        startName = [string]$service.StartName
        pathName = [string]$service.PathName
    }
}

function Assert-DisposableInputs {
    $fullExecutable = [IO.Path]::GetFullPath($Executable)
    $fullRoot = [IO.Path]::GetFullPath($StagingRoot)
    if ($fullExecutable -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+(?:\\cpp)?\\Bin\\hMailServer\.exe$') {
        throw "Executable is outside the approved disposable C++ roots: $fullExecutable"
    }
    if ($fullRoot -notmatch '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+(?:\\cpp)?$') {
        throw "Staging root is outside the approved disposable roots: $fullRoot"
    }
    if ($Database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $Database -match '(?i)test5700|production') {
        throw "Database is not disposable: $Database"
    }
    if ($ServiceName -eq 'hMailServer' -or $ServiceName -notmatch '^[A-Za-z0-9_.-]{1,255}$') {
        throw "ServiceName must be a non-production disposable SCM name: $ServiceName"
    }
    $builtInAccounts = @('NT AUTHORITY\LocalService', 'NT AUTHORITY\NetworkService')
    if ($ServiceAccount -ne 'LocalSystem' -and $ServiceAccount -notin $builtInAccounts -and $ServiceAccount -notmatch '^(?:\.\\)?[A-Za-z0-9_.-]{1,104}$') {
        throw "ServiceAccount is not a supported disposable local account: $ServiceAccount"
    }
    if ($ServiceAccount -ne 'LocalSystem' -and $ServiceAccount -notin $builtInAccounts -and [string]::IsNullOrWhiteSpace($ServicePassword)) {
        throw 'ServicePassword is required for a non-LocalSystem disposable account.'
    }
    if (-not (Test-Path -LiteralPath $fullExecutable -PathType Leaf)) { throw "Executable is missing: $fullExecutable" }
    $iniPath = Join-Path $fullRoot 'Bin\hMailServer.ini'
    if (-not (Test-Path -LiteralPath $iniPath -PathType Leaf)) { throw "Disposable INI is missing: $iniPath" }
    $ini = Get-Content -LiteralPath $iniPath -Raw
    if ($ini -notmatch "(?m)^Database=$([regex]::Escape($Database))\s*$") { throw "INI database does not match $Database" }
    $expectedData = [regex]::Escape((Join-Path $fullRoot 'Data'))
    if ($ini -notmatch "(?mi)^DataFolder=$expectedData\\?\s*$") { throw "INI DataFolder is not disposable: $fullRoot\Data" }
}

Assert-DisposableInputs
$Executable = [IO.Path]::GetFullPath($Executable)
$StagingRoot = [IO.Path]::GetFullPath($StagingRoot)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\benchmarks\paired-cpp-net10-20260901-service'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory -notmatch '(?i)\\artifacts\\benchmarks\\paired-cpp-net10-[a-z0-9_-]+(?:\\service)?$') {
    throw "Output directory is outside the repository benchmark artifact boundary: $OutputDirectory"
}

$existingLegacyService = Get-ServiceRecord 'hMailServer'
if ($null -ne $existingLegacyService -and $existingLegacyService.state -ne 'Stopped') {
    throw "The production-named hMailServer service is not stopped; isolated staging is required."
}
$existingTargetService = Get-ServiceRecord $ServiceName
if ($null -ne $existingTargetService) {
    throw "Refusing to reuse an existing disposable service: $ServiceName"
}

$preflight = Get-CppIsolationPreflight -TargetExecutable $Executable -ExpectedStagingRoot $StagingRoot -ExpectedDatabase $Database -DisposableRegistrationGuarded
if (-not $preflight.passed) { throw (($preflight.failures) -join [Environment]::NewLine) }

$beforeRegistry = Get-RegistryState
$serviceCreated = $false
$serviceStarted = $false
$worker = $null
$workerProcessRecord = $null
$readinessFailures = [System.Collections.Generic.List[string]]::new()
$cleanupFailures = [System.Collections.Generic.List[string]]::new()
$startUtc = [DateTimeOffset]::UtcNow
$binPath = '"{0}" /DisposableBenchmark /ServiceName={1} RunAsService' -f $Executable, $ServiceName

try {
    $createArguments = @('create', $ServiceName, 'binPath=', $binPath, 'start=', 'demand', 'type=', 'own', 'DisplayName=', $ServiceName)
    if ($ServiceAccount -ne 'LocalSystem') {
        $createArguments += @('obj=', $ServiceAccount)
        if ($ServiceAccount -notin @('NT AUTHORITY\LocalService', 'NT AUTHORITY\NetworkService')) {
            $createArguments += @('password=', $ServicePassword)
        }
    }
    & sc.exe @createArguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with exit code $LASTEXITCODE" }
    $serviceCreated = $true

    & sc.exe start $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe start failed with exit code $LASTEXITCODE" }
    $serviceStarted = $true

    $deadline = [DateTime]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        $record = Get-ServiceRecord $ServiceName
        if ($null -ne $record -and $record.state -eq 'Running') {
            if ($record.processId -gt 0) {
                $worker = Get-Process -Id $record.processId -ErrorAction SilentlyContinue
                $workerProcessRecord = Get-CimInstance Win32_Process -Filter "ProcessId=$($record.processId)" -ErrorAction SilentlyContinue
            }
            $ports = @(2525, 1143, 25110) | Where-Object {
                @(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort $_ -State Listen -ErrorAction SilentlyContinue).Count -gt 0
            }
            if ($ports.Count -eq 3) { break }
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    $record = Get-ServiceRecord $ServiceName
    if ($null -eq $record -or $record.state -ne 'Running') { $readinessFailures.Add("Disposable service did not reach Running: $($record | ConvertTo-Json -Compress)") }
    $ports = @(2525, 1143, 25110) | Where-Object {
        @(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort $_ -State Listen -ErrorAction SilentlyContinue).Count -gt 0
    }
    if ($ports.Count -ne 3) { $readinessFailures.Add("Expected loopback listeners 2525, 1143, 25110; found $($ports -join ',')") }
    if ($null -eq $worker -and $null -ne $record -and $record.processId -gt 0) { $worker = Get-Process -Id $record.processId -ErrorAction SilentlyContinue }
    if ($null -eq $workerProcessRecord -and $null -ne $record -and $record.processId -gt 0) {
        $workerProcessRecord = Get-CimInstance Win32_Process -Filter "ProcessId=$($record.processId)" -ErrorAction SilentlyContinue
    }
}
catch {
    $readinessFailures.Add($_.Exception.Message)
}
finally {
    if ($serviceCreated) {
        & sc.exe stop $ServiceName | Out-Null
        $stopDeadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $record = Get-ServiceRecord $ServiceName
        } while ($null -ne $record -and $record.state -ne 'Stopped' -and [DateTime]::UtcNow -lt $stopDeadline)
        if ($null -ne $record -and $record.state -ne 'Stopped') { $cleanupFailures.Add("Disposable service did not stop: $ServiceName") }
        & sc.exe delete $ServiceName | Out-Null
        $deleteDeadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $record = Get-ServiceRecord $ServiceName
        } while ($null -ne $record -and [DateTime]::UtcNow -lt $deleteDeadline)
        if ($null -ne $record) { $cleanupFailures.Add("Disposable service was not deleted: $ServiceName") }
    }
}

$afterRegistry = Get-RegistryState
$registryUnchanged = ($beforeRegistry | ConvertTo-Json -Depth 8 -Compress) -ceq ($afterRegistry | ConvertTo-Json -Depth 8 -Compress)
if (-not $registryUnchanged) { $cleanupFailures.Add('Installed Application/service registry state changed during the disposable run.') }
$endUtc = [DateTimeOffset]::UtcNow

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$workerRecord = if ($null -ne $worker -or $null -ne $workerProcessRecord) {
    [pscustomobject]@{
        pid = if ($null -ne $worker) { $worker.Id } else { [int]$workerProcessRecord.ProcessId }
        executable = if ($null -ne $workerProcessRecord) { [string]$workerProcessRecord.ExecutablePath } else { [string]$worker.Path }
        processName = if ($null -ne $workerProcessRecord) { [string]$workerProcessRecord.Name } else { [string]$worker.ProcessName }
        commandLine = if ($null -ne $workerProcessRecord) { [string]$workerProcessRecord.CommandLine } else { $null }
    }
} else { $null }
$report = [pscustomobject]@{
    schema = 'disposable-cpp-service-start-v1'
    status = if ($readinessFailures.Count -eq 0 -and $cleanupFailures.Count -eq 0) { 'PASS' } else { 'FAIL' }
    startedUtc = $startUtc.ToString('o')
    endedUtc = $endUtc.ToString('o')
    executable = $Executable
    executableSha256 = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
    stagingRoot = $StagingRoot
    database = $Database
    serviceName = $ServiceName
    serviceAccount = $ServiceAccount
    commandLine = $binPath
    listenerBind = '127.0.0.1'
    ports = @(2525, 1143, 25110)
    worker = $workerRecord
    preflight = $preflight
    readinessFailures = @($readinessFailures)
    cleanupFailures = @($cleanupFailures)
    installedRegistryUnchanged = $registryUnchanged
    productionServiceUntouched = $null -eq (Get-ServiceRecord 'hMailServer') -or $existingLegacyService.state -eq 'Stopped'
}
$jsonPath = Join-Path $OutputDirectory 'disposable-cpp-service.json'
$markdownPath = Join-Path $OutputDirectory 'disposable-cpp-service.md'
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
@(
    '# Disposable Legacy C++ Service Evidence',
    '',
    "- Status: **$($report.status)**",
    "- Service: $ServiceName (created, exercised, and deleted)",
    "- Executable: $Executable",
    "- Executable SHA-256: $($report.executableSha256)",
    "- Database: $Database",
    "- Data root: $StagingRoot\Data",
    "- Bind: 127.0.0.1 ports 2525, 1143, 25110",
    "- Worker: $($workerRecord | ConvertTo-Json -Compress)",
    "- Installed registry unchanged: $registryUnchanged",
    "- Readiness failures: $($readinessFailures.Count)",
    "- Cleanup failures: $($cleanupFailures.Count)"
) | Set-Content -LiteralPath $markdownPath -Encoding UTF8

if ($report.status -ne 'PASS') { throw "Disposable C++ service evidence failed. See $jsonPath" }
Write-Output ($report | ConvertTo-Json -Depth 10)
