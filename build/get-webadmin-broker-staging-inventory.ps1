[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$WebAdminPath,

    [string]$ApplicationAppId = '{5EDEC473-39E0-43F6-A234-1947071721C8}',

    [string]$CallerTokenEvidencePath,

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

    return $Key.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
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
                $identityName = "IIS AppPool\\$poolName"
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
        [string[]]$ExpectedWorkerSids
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [pscustomobject]@{
            Present = $false
            Valid = $false
            Reason = 'No caller-token evidence file was supplied.'
            ProbeVersion = $null
            ObservedUtc = $null
            Transport = $null
            CallerSid = $null
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
            Transport = $null
            CallerSid = $null
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
        $impersonationProperty = $evidence.PSObject.Properties['impersonationSucceeded']
        $impersonationSucceeded = $null -ne $impersonationProperty -and $impersonationProperty.Value -is [bool] -and $impersonationProperty.Value
        $hasRequiredMetadata = -not [string]::IsNullOrWhiteSpace($probeVersion) -and -not [string]::IsNullOrWhiteSpace($observedUtc)
        $transportIsLocal = [string]::Equals($transport, 'local', [System.StringComparison]::OrdinalIgnoreCase)
        $matchesWorkerSid = $ExpectedWorkerSids -contains $callerSid
        $valid = $hasRequiredMetadata -and $transportIsLocal -and $impersonationSucceeded -and $matchesWorkerSid

        return [pscustomobject]@{
            Present = $true
            Valid = $valid
            Reason = if ($valid) { $null } else { 'Evidence must include probe metadata, local transport, a Boolean successful impersonation result, and the selected worker SID.' }
            ProbeVersion = $probeVersion
            ObservedUtc = $observedUtc
            Transport = $transport
            CallerSid = $callerSid
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
            Transport = $null
            CallerSid = $null
            ImpersonationSucceeded = $false
            MatchesWorkerSid = $false
        }
    }
}

$registryEvidence = @(
    Get-RegistryViewEvidence -View ([Microsoft.Win32.RegistryView]::Registry64) -AppId $ApplicationAppId
    Get-RegistryViewEvidence -View ([Microsoft.Win32.RegistryView]::Registry32) -AppId $ApplicationAppId
)
$iisEvidence = Get-IisInventory -RequestedWebAdminPath $WebAdminPath
$workerSids = @($iisEvidence.Pools | ForEach-Object { $_.WorkerSid } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
$callerEvidence = Get-CallerTokenEvidence -Path $CallerTokenEvidencePath -ExpectedWorkerSids $workerSids
$webAdminPathExists = Test-Path -LiteralPath $WebAdminPath -PathType Container
$hasExistingApplicationAppId = @($registryEvidence | Where-Object { $_.ApplicationAppId.Present }).Count -gt 0
$hasDedicatedPoolCandidate = @($iisEvidence.Pools | Where-Object { $_.DedicatedPoolCandidate -and $_.WorkerSid }).Count -eq 1
$hasExplicitApplicationAcl = @($registryEvidence | Where-Object {
        $_.ApplicationAppId.LaunchPermission.Present -and $_.ApplicationAppId.AccessPermission.Present
    }).Count -gt 0
$stagingEvidenceComplete = $webAdminPathExists -and $hasExistingApplicationAppId -and $hasDedicatedPoolCandidate -and $callerEvidence.Valid

$report = [pscustomobject]@{
    SchemaVersion = 1
    CollectedUtc = [DateTime]::UtcNow.ToString('o')
    ComputerName = $env:COMPUTERNAME
    RequestedWebAdminPath = ConvertTo-NormalizedPath $WebAdminPath
    WebAdminPathExists = $webAdminPathExists
    ApplicationAppId = $ApplicationAppId
    Registry = $registryEvidence
    Iis = $iisEvidence
    CallerTokenEvidence = $callerEvidence
    Gate = [pscustomobject]@{
        DedicatedPoolCandidate = $hasDedicatedPoolCandidate
        ExistingApplicationAppIdPresent = $hasExistingApplicationAppId
        ExistingApplicationHasExplicitAcl = $hasExplicitApplicationAcl
        CallerTokenMatchesWorkerSid = $callerEvidence.Valid
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
    $fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = Split-Path -Parent $fullOutputPath
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        throw "Output directory does not exist: $outputDirectory"
    }

    [System.IO.File]::WriteAllText($fullOutputPath, $json, [System.Text.UTF8Encoding]::new($false))
    Write-Output $fullOutputPath
}

if ($FailOnIncomplete -and $report.Gate.Status -eq 'Incomplete') {
    exit 2
}
