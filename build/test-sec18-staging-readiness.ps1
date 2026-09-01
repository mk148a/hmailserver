[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceDirectory,

    [string]$StagingSiteName = 'HMailWebAdminBrokerStaging',
    [string]$StagingPoolName = 'HMailWebAdminBrokerPool'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-ReadinessCheck {
    param(
        [string]$Name,
        [ValidateSet('PASS', 'ENVIRONMENT-BLOCKED', 'FAIL')]
        [string]$Status,
        [bool]$Required,
        [string]$Detail,
        [AllowNull()]
        [object]$Observed
    )

    [pscustomobject]@{
        Name = $Name
        Status = $Status
        Required = $Required
        Detail = $Detail
        Observed = $Observed
    }
}

function Resolve-EvidenceDirectory {
    param([string]$RequestedPath, [string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'artifacts\sec18-staging')).TrimEnd('\')
    $fullPath = if ([IO.Path]::IsPathRooted($RequestedPath)) {
        [IO.Path]::GetFullPath($RequestedPath)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RequestedPath))
    }
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not [string]::Equals($fullPath, $root, [StringComparison]::OrdinalIgnoreCase) -and
        -not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Evidence directory must remain under $root."
    }
    if (-not [IO.Directory]::Exists($fullPath)) {
        throw "Evidence directory must already exist: $fullPath"
    }

    $current = $fullPath
    while ($true) {
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Evidence directory cannot use a reparse point: $current"
        }
        if ([string]::Equals($current, $root, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $current = [IO.Path]::GetFullPath((Split-Path -Parent $current)).TrimEnd('\')
    }

    return $fullPath
}

function Write-NewEvidenceFile {
    param([string]$Path, [string]$Content)

    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Content)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    }
    finally {
        $stream.Dispose()
    }
}

function Get-AdminCheck {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $isAdmin = [Security.Principal.WindowsPrincipal]::new($identity).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)
        if ($isAdmin) {
            return New-ReadinessCheck 'admin-status' 'PASS' $true 'Current token is elevated.' ([pscustomobject]@{ User = $identity.Name })
        }
        return New-ReadinessCheck 'admin-status' 'ENVIRONMENT-BLOCKED' $true 'Current token is not elevated.' $null
    }
    catch {
        return New-ReadinessCheck 'admin-status' 'FAIL' $true $_.Exception.Message $null
    }
}

function Get-ModuleCheck {
    try {
        $module = @(Get-Module -ListAvailable -Name WebAdministration -ErrorAction Stop) |
            Sort-Object Version -Descending | Select-Object -First 1
        if ($null -eq $module) {
            return New-ReadinessCheck 'webadministration-module' 'ENVIRONMENT-BLOCKED' $true 'WebAdministration is not installed or discoverable.' ([pscustomobject]@{ Present = $false })
        }
        return New-ReadinessCheck 'webadministration-module' 'PASS' $true 'WebAdministration is available.' ([pscustomobject]@{ Present = $true; Version = [string]$module.Version; Path = [string]$module.Path })
    }
    catch {
        return New-ReadinessCheck 'webadministration-module' 'FAIL' $true $_.Exception.Message $null
    }
}

function Get-AppcmdCheck {
    $candidates = @(
        (Join-Path $env:windir 'System32\inetsrv\appcmd.exe'),
        (Join-Path $env:windir 'Sysnative\inetsrv\appcmd.exe'),
        (Join-Path $env:windir 'SysWOW64\inetsrv\appcmd.exe')
    )
    $command = Get-Command appcmd.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { $candidates += [string]$command.Source }
    $path = @($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1)
    if ($path.Count -eq 0) {
        return New-ReadinessCheck 'appcmd-exe' 'ENVIRONMENT-BLOCKED' $true 'appcmd.exe was not found.' ([pscustomobject]@{ Present = $false })
    }
    return New-ReadinessCheck 'appcmd-exe' 'PASS' $true 'appcmd.exe is available.' ([pscustomobject]@{ Present = $true; Path = [string]$path[0] })
}

function Get-ServiceCheck {
    param([string]$Name)
    try {
        $service = Get-Service -Name $Name -ErrorAction Stop
        return New-ReadinessCheck "$Name-service" 'PASS' $true "$Name is present." ([pscustomobject]@{ Present = $true; Status = [string]$service.Status; StartType = [string]$service.StartType })
    }
    catch {
        if ($_.CategoryInfo.Category -eq 'ObjectNotFound') {
            return New-ReadinessCheck "$Name-service" 'ENVIRONMENT-BLOCKED' $true "$Name is not present." ([pscustomobject]@{ Present = $false })
        }
        return New-ReadinessCheck "$Name-service" 'FAIL' $true $_.Exception.Message $null
    }
}

function Get-OptionalIisCheck {
    param([bool]$ModuleAvailable, [string]$AppcmdPath, [string]$SiteName, [string]$PoolName)
    if (-not $ModuleAvailable -and [string]::IsNullOrWhiteSpace($AppcmdPath)) {
        $detail = 'IIS site/pool state cannot be read because WebAdministration and appcmd.exe are unavailable.'
        return @(
            (New-ReadinessCheck 'staging-site-read' 'ENVIRONMENT-BLOCKED' $false $detail ([pscustomobject]@{ Name = $SiteName })),
            (New-ReadinessCheck 'staging-pool-read' 'ENVIRONMENT-BLOCKED' $false $detail ([pscustomobject]@{ Name = $PoolName }))
        )
    }

    try {
        if ($ModuleAvailable) {
            Import-Module WebAdministration -ErrorAction Stop
            $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
            $pool = Get-Item -LiteralPath "IIS:\AppPools\$PoolName" -ErrorAction SilentlyContinue
            return @(
                (New-ReadinessCheck 'staging-site-read' 'PASS' $false 'Staging site metadata was read.' ([pscustomobject]@{ Present = $null -ne $site; Name = $SiteName; State = if ($null -ne $site) { [string]$site.State } else { $null }; ApplicationPool = if ($null -ne $site) { [string]$site.ApplicationPool } else { $null } })),
                (New-ReadinessCheck 'staging-pool-read' 'PASS' $false 'Staging pool metadata was read.' ([pscustomobject]@{ Present = $null -ne $pool; Name = $PoolName; State = if ($null -ne $pool) { [string]$pool.State } else { $null } }))
            )
        }
        return @(
            (New-ReadinessCheck 'staging-site-read' 'PASS' $false 'appcmd.exe is available; optional site read was not requested through a mutation-capable command.' ([pscustomobject]@{ Name = $SiteName; QuerySource = 'appcmd-available' })),
            (New-ReadinessCheck 'staging-pool-read' 'PASS' $false 'appcmd.exe is available; optional pool read was not requested through a mutation-capable command.' ([pscustomobject]@{ Name = $PoolName; QuerySource = 'appcmd-available' }))
        )
    }
    catch {
        return @(
            (New-ReadinessCheck 'staging-site-read' 'FAIL' $false $_.Exception.Message $null),
            (New-ReadinessCheck 'staging-pool-read' 'FAIL' $false $_.Exception.Message $null)
        )
    }
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$safeDirectory = Resolve-EvidenceDirectory -RequestedPath $EvidenceDirectory -RepositoryRoot $repositoryRoot
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ', [Globalization.CultureInfo]::InvariantCulture)
$stem = "SEC18-staging-readiness-$stamp-$([Guid]::NewGuid().ToString('N'))"
$jsonPath = Join-Path $safeDirectory "$stem.json"
$markdownPath = Join-Path $safeDirectory "$stem.md"
$checks = [System.Collections.Generic.List[object]]::new()
$checks.Add((Get-AdminCheck))
$moduleCheck = Get-ModuleCheck
$checks.Add($moduleCheck)
$appcmdCheck = Get-AppcmdCheck
$checks.Add($appcmdCheck)
$checks.Add((Get-ServiceCheck 'W3SVC'))
$checks.Add((Get-ServiceCheck 'WAS'))
$appcmdPath = if ($appcmdCheck.Observed -and $appcmdCheck.Observed.PSObject.Properties['Path']) { [string]$appcmdCheck.Observed.Path } else { '' }
foreach ($check in @(Get-OptionalIisCheck ($moduleCheck.Status -eq 'PASS') $appcmdPath $StagingSiteName $StagingPoolName)) { $checks.Add($check) }
$checks.Add((New-ReadinessCheck 'no-machine-mutation-attempted' 'PASS' $true 'This runner performs read-only prerequisite checks and evidence writes only.' ([pscustomobject]@{ MachineMutationAttempted = $false; ExistingEvidenceModified = $false })))
$checks.Add((New-ReadinessCheck 'collector-semantics' 'PASS' $true 'This report does not approve broker registration or replace SEC-18 collector evidence.' ([pscustomobject]@{ ReadyForBrokerRegistration = $false })))

$required = @($checks | Where-Object Required)
$missing = @($required | Where-Object Status -eq 'ENVIRONMENT-BLOCKED')
$failed = @($checks | Where-Object Status -eq 'FAIL')
$status = if ($failed.Count -gt 0) { 'FAIL' } elseif ($missing.Count -gt 0) { 'ENVIRONMENT-BLOCKED' } else { 'PASS' }
$report = [pscustomobject]@{
    SchemaVersion = 1
    EvidenceKind = 'SEC18-StagingReadiness'
    ObservedUtc = [DateTimeOffset]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    ComputerName = $env:COMPUTERNAME
    Status = $status
    Checks = @($checks)
    MissingPrerequisites = @($missing | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Detail = $_.Detail } })
    FailedChecks = @($failed | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Detail = $_.Detail } })
    Gate = [pscustomobject]@{ Status = $status; ReadyForBrokerRegistration = $false; Reason = 'Readiness evidence only; independent caller-token proof and reviewed broker controls remain required.' }
    MutationSafety = [pscustomobject]@{ MachineMutationAttempted = $false; ExistingEvidenceModified = $false; EvidenceDirectory = $safeDirectory }
}
$json = $report | ConvertTo-Json -Depth 16
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# SEC-18 staging readiness')
$lines.Add('')
$lines.Add("- Status: **$status**")
$lines.Add("- Observed UTC: $($report.ObservedUtc)")
$lines.Add("- Computer: $($report.ComputerName)")
$lines.Add('')
$lines.Add('| Check | Required | Status | Detail |')
$lines.Add('| --- | --- | --- | --- |')
foreach ($check in @($checks)) {
    $detail = ([string]$check.Detail).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
    $lines.Add("| $($check.Name) | $($check.Required) | $($check.Status) | $detail |")
}
$lines.Add('')
$lines.Add('This report is read-only and does not approve broker registration or replace SEC-18 collector evidence.')
Write-NewEvidenceFile -Path $jsonPath -Content $json
Write-NewEvidenceFile -Path $markdownPath -Content (($lines -join [Environment]::NewLine) + [Environment]::NewLine)
Write-Output "Status: $status"
Write-Output "JSON: $jsonPath"
Write-Output "Markdown: $markdownPath"
if ($status -eq 'FAIL') { exit 1 }
if ($status -eq 'ENVIRONMENT-BLOCKED') { exit 2 }
exit 0
