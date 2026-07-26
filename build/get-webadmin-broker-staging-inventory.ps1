[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$WebAdminPath,

    [string]$ApplicationAppId = '{5EDEC473-39E0-43F6-A234-1947071721C8}',

    [string]$CallerTokenEvidencePath,

    [string]$CollectorInvocationId,

    [ValidateRange(1, 3600)]
    [int]$CallerEvidenceMaxAgeSeconds = 300,

    [string]$OutputPath,

    [switch]$FailOnIncomplete
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-SecurityDescriptorToEvidence {
    param(
        [object]$Value
    )

    if ($null -eq $Value) {
        return [pscustomobject]@{
            Present = $false
            Length = 0
            Sddl = $null
            DecodeError = $null
        }
    }

    if ($Value -isnot [byte[]]) {
        return [pscustomobject]@{
            Present = $true
            Length = 0
            Sddl = $null
            DecodeError = "Expected a binary security descriptor but found $($Value.GetType().FullName)."
        }
    }

    try {
        $descriptor = [System.Security.AccessControl.RawSecurityDescriptor]::new([byte[]]$Value, 0)
        return [pscustomobject]@{
            Present = $true
            Length = $Value.Length
            Sddl = $descriptor.GetSddlForm([System.Security.AccessControl.AccessControlSections]::All)
            DecodeError = $null
        }
    }
    catch {
        return [pscustomobject]@{
            Present = $true
            Length = $Value.Length
            Sddl = $null
            DecodeError = $_.Exception.Message
        }
    }
}

function Get-RegistryValueEvidence {
    param(
        [Microsoft.Win32.RegistryKey]$Key,
        [string]$Name
    )

    if ($null -eq $Key) {
        return $null
    }

    return ,($Key.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames))
}

function Get-RegistryViewEvidence {
    param(
        [Microsoft.Win32.RegistryView]$View,
        [string]$AppId
    )

    $baseKey = $null
    $appIdKey = $null
    $oleKey = $null

    try {
        $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $View)
        $appIdKey = $baseKey.OpenSubKey("SOFTWARE\\Classes\\AppID\\$AppId", $false)
        $oleKey = $baseKey.OpenSubKey('SOFTWARE\\Microsoft\\Ole', $false)

        return [pscustomobject]@{
            View = $View.ToString()
            ApplicationAppId = [pscustomobject]@{
                Present = $null -ne $appIdKey
                LocalService = Get-RegistryValueEvidence -Key $appIdKey -Name 'LocalService'
                LaunchPermission = Convert-SecurityDescriptorToEvidence (Get-RegistryValueEvidence -Key $appIdKey -Name 'LaunchPermission')
                AccessPermission = Convert-SecurityDescriptorToEvidence (Get-RegistryValueEvidence -Key $appIdKey -Name 'AccessPermission')
            }
            MachineDefaults = [pscustomobject]@{
                Present = $null -ne $oleKey
                EnableDcom = Get-RegistryValueEvidence -Key $oleKey -Name 'EnableDCOM'
                DefaultLaunchPermission = Convert-SecurityDescriptorToEvidence (Get-RegistryValueEvidence -Key $oleKey -Name 'DefaultLaunchPermission')
                DefaultAccessPermission = Convert-SecurityDescriptorToEvidence (Get-RegistryValueEvidence -Key $oleKey -Name 'DefaultAccessPermission')
            }
        }
    }
    finally {
        if ($null -ne $appIdKey) {
            $appIdKey.Dispose()
        }

        if ($null -ne $oleKey) {
            $oleKey.Dispose()
        }

        if ($null -ne $baseKey) {
            $baseKey.Dispose()
        }
    }
}

function ConvertTo-NormalizedPath {
    param(
        [AllowNull()]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    return [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($Path)).TrimEnd('\\').ToUpperInvariant()
}

function Get-Sec18EvidenceRoot {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ScriptPath
    )

    $buildDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($ScriptPath))
    $repositoryRoot = Split-Path -Parent $buildDirectory
    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\\sec18-staging'))
}

function Resolve-Sec18EvidenceOutputPath {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$RequestedPath,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$EvidenceRoot
    )

    $fullRoot = [System.IO.Path]::GetFullPath($EvidenceRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not [System.IO.Directory]::Exists($fullRoot)) {
        throw "SEC-18 evidence root does not exist: $fullRoot"
    }

    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $fullRoot)
    $fullPath = if ([System.IO.Path]::IsPathRooted($RequestedPath)) {
        [System.IO.Path]::GetFullPath($RequestedPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $RequestedPath))
    }

    $rootPrefix = $fullRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "SEC-18 output path must remain under ${fullRoot}: $fullPath"
    }

    $outputDirectory = Split-Path -Parent $fullPath
    if (-not [System.IO.Directory]::Exists($outputDirectory)) {
        throw "SEC-18 output directory does not exist: $outputDirectory"
    }

    $currentDirectory = [System.IO.Path]::GetFullPath($outputDirectory)
    while ($true) {
        $item = Get-Item -LiteralPath $currentDirectory -Force -ErrorAction Stop
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "SEC-18 output path cannot use a reparse-point directory: $currentDirectory"
        }
        if ([string]::Equals($currentDirectory, $fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $currentDirectory = [System.IO.Path]::GetFullPath((Split-Path -Parent $currentDirectory))
        if (-not [string]::Equals($currentDirectory, $fullRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $currentDirectory.StartsWith($fullRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "SEC-18 output path ancestry escaped the evidence root: $currentDirectory"
        }
    }

    if ([System.IO.File]::Exists($fullPath) -or [System.IO.Directory]::Exists($fullPath)) {
        throw "SEC-18 output path already exists and will not be overwritten: $fullPath"
    }

    return $fullPath
}

function Resolve-AccountSid {
    param(
        [AllowNull()]
        [string]$AccountName
    )

    if ([string]::IsNullOrWhiteSpace($AccountName)) {
        return $null
    }

    try {
        return ([System.Security.Principal.NTAccount]::new($AccountName)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    }
    catch {
        return $null
    }
}

function Get-ApplicationPoolIdentityName {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$PoolName
    )

    return "IIS AppPool\$PoolName"
}

function Get-IisInventory {
    param(
        [string]$RequestedWebAdminPath
    )

    $targetPath = ConvertTo-NormalizedPath $RequestedWebAdminPath

    try {
        Import-Module WebAdministration -ErrorAction Stop
    }
    catch {
        return [pscustomobject]@{
            Available = $false
            Reason = $_.Exception.Message
            TargetPath = $targetPath
            Mappings = @()
            Pools = @()
        }
    }

    try {
        $mappings = @()
        foreach ($site in Get-Website) {
            $mappings += [pscustomobject]@{
                Site = $site.Name
                Path = '/'
                PhysicalPath = $site.PhysicalPath
                NormalizedPhysicalPath = ConvertTo-NormalizedPath $site.PhysicalPath
                ApplicationPool = $site.ApplicationPool
                Source = 'WebsiteRoot'
            }
        }

        foreach ($application in Get-WebApplication) {
            $siteName = $application.PSParentPath -replace '^MACHINE/WEBROOT/APPHOST/', ''
            $mappings += [pscustomobject]@{
                Site = $siteName
                Path = $application.Path
                PhysicalPath = $application.PhysicalPath
                NormalizedPhysicalPath = ConvertTo-NormalizedPath $application.PhysicalPath
                ApplicationPool = $application.ApplicationPool
                Source = 'Application'
            }
        }

        $targetMappings = @($mappings | Where-Object { $_.NormalizedPhysicalPath -eq $targetPath })
        $pools = @()
        foreach ($poolName in @($targetMappings.ApplicationPool | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)) {
            $pool = Get-Item -LiteralPath "IIS:\\AppPools\\$poolName"
            $identityType = [string]$pool.processModel.identityType
            $identityName = [string]$pool.processModel.userName

            if ($identityType -eq 'ApplicationPoolIdentity') {
                $identityName = Get-ApplicationPoolIdentityName -PoolName $poolName
            }

            $poolMappings = @($mappings | Where-Object { $_.ApplicationPool -eq $poolName })
            $pools += [pscustomobject]@{
                Name = $poolName
                IdentityType = $identityType
                IdentityName = $identityName
                WorkerSid = Resolve-AccountSid $identityName
                MappingCount = $poolMappings.Count
                Mappings = $poolMappings
                DedicatedPoolCandidate = ($poolMappings.Count -eq 1 -and $targetMappings.Count -eq 1)
                TrustReviewRequired = $true
            }
        }

        return [pscustomobject]@{
            Available = $true
            Reason = $null
            TargetPath = $targetPath
            Mappings = $targetMappings
            Pools = $pools
        }
    }
    catch {
        return [pscustomobject]@{
            Available = $false
            Reason = $_.Exception.Message
            TargetPath = $targetPath
            Mappings = @()
            Pools = @()
        }
    }
}

function Get-CallerTokenEvidence {
    param(
        [string]$Path,
        [string[]]$ExpectedWorkerSids,
        [string]$ExpectedCollectorInvocationId,
        [DateTimeOffset]$NowUtc,
        [int]$MaxAgeSeconds
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [pscustomobject]@{
            Present = $false
            Valid = $false
            Reason = 'No caller-token evidence file was supplied.'
            ProbeVersion = $null
            ObservedUtc = $null
            ObservedAgeSeconds = $null
            TimestampParseable = $false
            TimestampFresh = $false
            Transport = $null
            CallerSid = $null
            CorrelationId = $null
            CollectorInvocationId = $ExpectedCollectorInvocationId
            CorrelationMatchesCollectorInvocation = $false
            ImpersonationSucceeded = $false
            MatchesWorkerSid = $false
        }
    }

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{
            Present = $false
            Valid = $false
            Reason = "Caller-token evidence file was not found: $Path"
            ProbeVersion = $null
            ObservedUtc = $null
            ObservedAgeSeconds = $null
            TimestampParseable = $false
            TimestampFresh = $false
            Transport = $null
            CallerSid = $null
            CorrelationId = $null
            CollectorInvocationId = $ExpectedCollectorInvocationId
            CorrelationMatchesCollectorInvocation = $false
            ImpersonationSucceeded = $false
            MatchesWorkerSid = $false
        }
    }

    try {
        $evidence = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        $probeVersion = [string]$evidence.probeVersion
        $observedUtc = [string]$evidence.observedUtc
        $transport = [string]$evidence.transport
        $callerSid = [string]$evidence.callerSid
        $correlationId = [string]$evidence.correlationId
        $impersonationProperty = $evidence.PSObject.Properties['impersonationSucceeded']
        $impersonationSucceeded = $null -ne $impersonationProperty -and $impersonationProperty.Value -is [bool] -and $impersonationProperty.Value
        $observedTimestamp = $null
        $observedAgeSeconds = $null
        $timestampParseable = $false
        $timestampFresh = $false
        try {
            $observedTimestamp = [DateTimeOffset]::Parse(
                $observedUtc,
                [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal)
            $observedTimestamp = $observedTimestamp.ToUniversalTime()
            $observedAgeSeconds = ($NowUtc.ToUniversalTime() - $observedTimestamp).TotalSeconds
            $timestampParseable = $true
            $timestampFresh = $observedAgeSeconds -ge -30 -and $observedAgeSeconds -le $MaxAgeSeconds
        }
        catch {
            $timestampParseable = $false
        }

        $hasRequiredMetadata = -not [string]::IsNullOrWhiteSpace($probeVersion) -and
            -not [string]::IsNullOrWhiteSpace($observedUtc) -and
            -not [string]::IsNullOrWhiteSpace($correlationId)
        $transportIsLocal = [string]::Equals($transport, 'local', [System.StringComparison]::OrdinalIgnoreCase)
        $matchesWorkerSid = $ExpectedWorkerSids -contains $callerSid
        $correlationMatchesCollectorInvocation = -not [string]::IsNullOrWhiteSpace($ExpectedCollectorInvocationId) -and
            [string]::Equals($correlationId, $ExpectedCollectorInvocationId, [System.StringComparison]::Ordinal)
        $valid = $hasRequiredMetadata -and $timestampParseable -and $timestampFresh -and
            $transportIsLocal -and $impersonationSucceeded -and $matchesWorkerSid -and
            $correlationMatchesCollectorInvocation

        return [pscustomobject]@{
            Present = $true
            Valid = $valid
            Reason = if ($valid) { $null } else { 'Evidence must include probe metadata, a parseable fresh timestamp, the collector invocation correlation, local transport, a Boolean successful impersonation result, and the selected worker SID.' }
            ProbeVersion = $probeVersion
            ObservedUtc = $observedUtc
            ObservedAgeSeconds = if ($null -eq $observedAgeSeconds) { $null } else { [Math]::Round($observedAgeSeconds, 3) }
            TimestampParseable = $timestampParseable
            TimestampFresh = $timestampFresh
            Transport = $transport
            CallerSid = $callerSid
            CorrelationId = $correlationId
            CollectorInvocationId = $ExpectedCollectorInvocationId
            CorrelationMatchesCollectorInvocation = $correlationMatchesCollectorInvocation
            ImpersonationSucceeded = $impersonationSucceeded
            MatchesWorkerSid = $matchesWorkerSid
        }
    }
    catch {
        return [pscustomobject]@{
            Present = $true
            Valid = $false
            Reason = "Caller-token evidence could not be read: $($_.Exception.Message)"
            ProbeVersion = $null
            ObservedUtc = $null
            ObservedAgeSeconds = $null
            TimestampParseable = $false
            TimestampFresh = $false
            Transport = $null
            CallerSid = $null
            CorrelationId = $null
            CollectorInvocationId = $ExpectedCollectorInvocationId
            CorrelationMatchesCollectorInvocation = $false
            ImpersonationSucceeded = $false
            MatchesWorkerSid = $false
        }
    }
}

function Get-HMailServerServiceEvidence {
    $service = $null
    $serviceReadError = $null
    $processes = @()
    $processReadError = $null

    try {
        $service = Get-Service -Name 'hMailServer' -ErrorAction Stop
    }
    catch {
        if ([string]$_.CategoryInfo.Category -ne 'ObjectNotFound') {
            $serviceReadError = $_.Exception.Message
        }
    }

    try {
        $processes = @(Get-Process -Name 'hMailServer' -ErrorAction Stop)
    }
    catch {
        if ([string]$_.CategoryInfo.Category -ne 'ObjectNotFound') {
            $processReadError = $_.Exception.Message
        }
    }

    $readErrors = @(@($serviceReadError, $processReadError) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    return [pscustomobject]@{
        Name = 'hMailServer'
        Present = $null -ne $service
        Status = if ($null -ne $service) { [int]$service.Status } else { $null }
        StatusName = if ($null -ne $service) { [string]$service.Status } else { $null }
        StartType = if ($null -ne $service) { [int]$service.StartType } else { $null }
        StartTypeName = if ($null -ne $service) { [string]$service.StartType } else { $null }
        ProcessPresent = if ($null -eq $processReadError) { $processes.Count -gt 0 } else { $null }
        ProcessIds = if ($null -eq $processReadError) { @($processes | ForEach-Object { [int]$_.Id }) } else { @() }
        ServiceReadError = $serviceReadError
        ProcessReadError = $processReadError
        ReadError = if ($readErrors.Count -eq 0) { $null } else { $readErrors -join '; ' }
    }
}

$collectorStartedUtc = [DateTimeOffset]::UtcNow
if ([string]::IsNullOrWhiteSpace($CollectorInvocationId)) {
    $CollectorInvocationId = [Guid]::NewGuid().ToString('N')
}
$canonicalApplicationAppId = '{5EDEC473-39E0-43F6-A234-1947071721C8}'
if (-not [string]::Equals($ApplicationAppId, $canonicalApplicationAppId, [StringComparison]::OrdinalIgnoreCase)) {
    throw "SEC-18 collector only permits the canonical hMailServer Application AppID: $canonicalApplicationAppId"
}

$registryEvidence = @(
    Get-RegistryViewEvidence -View ([Microsoft.Win32.RegistryView]::Registry64) -AppId $ApplicationAppId
    Get-RegistryViewEvidence -View ([Microsoft.Win32.RegistryView]::Registry32) -AppId $ApplicationAppId
)
$iisEvidence = Get-IisInventory -RequestedWebAdminPath $WebAdminPath
$hMailServerService = Get-HMailServerServiceEvidence
$workerSids = @($iisEvidence.Pools | ForEach-Object { $_.WorkerSid } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
$webAdminPathExists = Test-Path -LiteralPath $WebAdminPath -PathType Container
$hasExistingApplicationAppId = @($registryEvidence | Where-Object { $_.ApplicationAppId.Present }).Count -gt 0
$hasDedicatedPoolCandidate = @($iisEvidence.Pools | Where-Object { $_.DedicatedPoolCandidate -and $_.WorkerSid }).Count -eq 1
$hasExplicitApplicationAcl = @($registryEvidence | Where-Object {
        $_.ApplicationAppId.LaunchPermission.Present -and $_.ApplicationAppId.AccessPermission.Present
    }).Count -gt 0
$collectorValidatedUtc = [DateTimeOffset]::UtcNow
$callerEvidence = Get-CallerTokenEvidence -Path $CallerTokenEvidencePath -ExpectedWorkerSids $workerSids -ExpectedCollectorInvocationId $CollectorInvocationId -NowUtc $collectorValidatedUtc -MaxAgeSeconds $CallerEvidenceMaxAgeSeconds
$hMailServerServiceSafe = $hMailServerService.Present -and
    [string]::Equals([string]$hMailServerService.Name, 'hMailServer', [StringComparison]::OrdinalIgnoreCase) -and
    [string]::Equals([string]$hMailServerService.StatusName, 'Stopped', [StringComparison]::OrdinalIgnoreCase) -and
    [string]::Equals([string]$hMailServerService.StartTypeName, 'Disabled', [StringComparison]::OrdinalIgnoreCase) -and
    $hMailServerService.ProcessPresent -eq $false -and
    [string]::IsNullOrWhiteSpace([string]$hMailServerService.ReadError)
$stagingEvidenceComplete = $webAdminPathExists -and $hasExistingApplicationAppId -and $hasDedicatedPoolCandidate -and $callerEvidence.Valid -and $hMailServerServiceSafe

$report = [pscustomobject]@{
    SchemaVersion = 1
    CollectionStartedUtc = $collectorStartedUtc.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    CollectedUtc = $collectorValidatedUtc.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    CollectorInvocationId = $CollectorInvocationId
    CallerEvidenceMaxAgeSeconds = $CallerEvidenceMaxAgeSeconds
    ComputerName = $env:COMPUTERNAME
    RequestedWebAdminPath = ConvertTo-NormalizedPath $WebAdminPath
    WebAdminPathExists = $webAdminPathExists
    ApplicationAppId = $ApplicationAppId
    Registry = $registryEvidence
    Iis = $iisEvidence
    HMailServerService = $hMailServerService
    CallerTokenEvidence = $callerEvidence
    Gate = [pscustomobject]@{
        DedicatedPoolCandidate = $hasDedicatedPoolCandidate
        ExistingApplicationAppIdPresent = $hasExistingApplicationAppId
        ExistingApplicationHasExplicitAcl = $hasExplicitApplicationAcl
        CallerTokenMatchesWorkerSid = $callerEvidence.Valid
        HMailServerServiceSafe = $hMailServerServiceSafe
        ReadyForBrokerRegistration = $false
        Status = if ($stagingEvidenceComplete) { 'EvidenceCollectedSecurityReviewRequired' } else { 'Incomplete' }
        Reason = 'This collector never approves broker registration. A reviewed broker-only AppID ACL and method-level caller-SID checks remain required.'
    }
}

$json = $report | ConvertTo-Json -Depth 12
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $json
}
else {
    $evidenceRoot = Get-Sec18EvidenceRoot -ScriptPath $MyInvocation.MyCommand.Path
    $fullOutputPath = Resolve-Sec18EvidenceOutputPath -RequestedPath $OutputPath -EvidenceRoot $evidenceRoot
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($fullOutputPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
    Write-Output $fullOutputPath
}

if ($FailOnIncomplete -and $report.Gate.Status -eq 'Incomplete') {
    exit 2
}
