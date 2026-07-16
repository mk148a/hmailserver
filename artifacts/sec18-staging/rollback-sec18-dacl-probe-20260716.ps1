[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'
$serviceName = 'SEC18ProbeSvcDacl'
$probeRoot = 'C:\SEC18-Staging\Probe'
$evidenceRoot = 'C:\SEC18-Staging\EvidenceDacl'
$webRoot = 'C:\SEC18-Staging\WebAdmin'
$registryViews = @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)
$registryPaths = @(
    'SOFTWARE\Classes\CLSID\{8e294295-4b96-4079-8821-e19f3bacab54}',
    'SOFTWARE\Classes\AppID\{b5fa1c3f-b6c4-4eeb-9658-2ae538ae287e}',
    'SOFTWARE\Classes\Interface\{30ae98ce-92aa-46f0-ba30-bfbac842d71f}',
    'SOFTWARE\Classes\TypeLib\{f1779f3e-dcb4-4af0-9312-140b7b588a28}',
    'SOFTWARE\Classes\SEC18.CallerProbe.ServiceDacl',
    'SOFTWARE\Classes\SEC18.CallerProbe.ServiceDacl.1'
)
$endpoints = @(
    'sec18-pool-client-dacl.php',
    'sec18-pool-native-client.php',
    'sec18-pool-direct-com.php',
    'sec18-pool-direct-com-wrong-sid.php'
)

function Invoke-Rollback([string]$Description, [scriptblock]$Action) {
    if ($WhatIfPreference) { Write-Output "WHATIF: $Description" } else { & $Action }
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Invoke-Rollback "stop temporary service $serviceName" { Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue }
    Invoke-Rollback "delete temporary service $serviceName" { & sc.exe delete $serviceName | Out-Null }
}

foreach ($view in $registryViews) {
    if ($WhatIfPreference) {
        foreach ($path in $registryPaths) { Write-Output "WHATIF: inspect/remove temporary registry key $view $path" }
        continue
    }
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
    try {
        foreach ($path in $registryPaths) {
            $parentPath = Split-Path -Path $path -Parent
            $leafName = Split-Path -Path $path -Leaf
            $parent = $base.OpenSubKey($parentPath, $true)
            try {
                if ($null -ne $parent -and $null -ne $parent.OpenSubKey($leafName, $false)) {
                    Invoke-Rollback "remove temporary registry key $view $path" { $parent.DeleteSubKeyTree($leafName) }
                }
            }
            finally { if ($null -ne $parent) { $parent.Dispose() } }
        }
    }
    finally { $base.Dispose() }
}

foreach ($endpoint in $endpoints) {
    $path = Join-Path $webRoot $endpoint
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Invoke-Rollback "remove temporary endpoint $path" { Remove-Item -LiteralPath $path -Force }
    }
}

if (Test-Path -LiteralPath $probeRoot -PathType Container) {
    $resolvedProbe = (Resolve-Path -LiteralPath $probeRoot).Path.TrimEnd('\')
    if ($resolvedProbe.ToUpperInvariant() -ne 'C:\SEC18-STAGING\PROBE') { throw "Unexpected probe root: $resolvedProbe" }
    Invoke-Rollback "remove temporary probe root $probeRoot" { Remove-Item -LiteralPath $probeRoot -Recurse -Force }
}

if (Test-Path -LiteralPath $evidenceRoot -PathType Container) {
    $resolvedEvidence = (Resolve-Path -LiteralPath $evidenceRoot).Path.TrimEnd('\')
    if ($resolvedEvidence.ToUpperInvariant() -ne 'C:\SEC18-STAGING\EVIDENCEDACL') { throw "Unexpected evidence root: $resolvedEvidence" }
    Invoke-Rollback "remove unsanitized temporary evidence root $evidenceRoot" { Remove-Item -LiteralPath $evidenceRoot -Recurse -Force }
}

Write-Output 'DACL probe rollback does not modify the IIS site/pool, PHP runtime, WebAdmin authentication, firewall, hMailServer service, hMailServer registry, database, or data directory.'
