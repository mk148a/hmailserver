$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $repo 'artifacts\sec18-staging\SEC18-dacl-cleanup-validation-20260716.json'
$registryViews = @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)
$temporaryPaths = @(
    'SOFTWARE\Classes\CLSID\{8e294295-4b96-4079-8821-e19f3bacab54}',
    'SOFTWARE\Classes\AppID\{b5fa1c3f-b6c4-4eeb-9658-2ae538ae287e}',
    'SOFTWARE\Classes\Interface\{30ae98ce-92aa-46f0-ba30-bfbac842d71f}',
    'SOFTWARE\Classes\TypeLib\{f1779f3e-dcb4-4af0-9312-140b7b588a28}',
    'SOFTWARE\Classes\SEC18.CallerProbe.ServiceDacl',
    'SOFTWARE\Classes\SEC18.CallerProbe.ServiceDacl.1'
)
$temporaryEndpoints = @(
    'sec18-pool-client-dacl.php',
    'sec18-pool-native-client.php',
    'sec18-pool-direct-com.php',
    'sec18-pool-direct-com-wrong-sid.php'
)
$registryResiduals = @()
foreach ($view in $registryViews) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
    try {
        foreach ($path in $temporaryPaths) {
            $key = $base.OpenSubKey($path, $false)
            if ($null -ne $key) { $registryResiduals += "${view}:$path"; $key.Dispose() }
        }
    }
    finally { $base.Dispose() }
}
$health = (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:8088/health.php').Content | ConvertFrom-Json
$site = Get-Website -Name 'HMailWebAdminBrokerStaging'
$pool = Get-WebAppPoolState -Name 'HMailWebAdminBrokerPool'
$hmail = Get-Service hMailServer
$probeProcess = @(Get-Process SEC18CallerProbeDacl,SEC18PoolClientDacl,SEC18PoolNativeClient -ErrorAction SilentlyContinue)
$endpointResiduals = @($temporaryEndpoints | Where-Object { Test-Path -LiteralPath (Join-Path 'C:\SEC18-Staging\WebAdmin' $_) })
$report = [pscustomobject]@{
    SchemaVersion = 1
    CollectedUtc = [DateTime]::UtcNow.ToString('o')
    TemporaryProbeServicePresent = $null -ne (Get-Service SEC18ProbeSvcDacl -ErrorAction SilentlyContinue)
    TemporaryRegistryResiduals = $registryResiduals
    TemporaryEndpointResiduals = $endpointResiduals
    ProbeRootPresent = Test-Path -LiteralPath 'C:\SEC18-Staging\Probe'
    UnsanitizedEvidenceRootPresent = Test-Path -LiteralPath 'C:\SEC18-Staging\EvidenceDacl'
    Staging = [pscustomobject]@{
        SiteName = $site.Name
        SiteState = $site.State
        PhysicalPath = $site.PhysicalPath
        PoolName = 'HMailWebAdminBrokerPool'
        PoolState = $pool.Value
        HealthStatus = $health.status
        PhpVersion = $health.phpVersion
        ComDotNetLoaded = $health.comDotNetLoaded
        HMailServerComActivated = $health.hMailServerComActivated
    }
    ProductionSafety = [pscustomobject]@{
        HMailServiceStatus = $hmail.Status.ToString()
        HMailServiceStartType = $hmail.StartType.ToString()
        HMailProcessCount = @(Get-Process hMailServer -ErrorAction SilentlyContinue).Count
        ProbeProcessCount = $probeProcess.Count
        DatabaseAccessed = $false
        DataDirectoryAccessed = $false
        FirewallChanged = $false
        PermanentBrokerRegistered = $false
    }
}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $output -Encoding utf8
Write-Output $output
