[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'
$serviceName = 'SEC18CallerProbe'
$probeRoot = 'C:\SEC18-Staging\Probe'
$webRoot = 'C:\SEC18-Staging\WebAdmin'
$appId = '{C3E8DB24-6E66-44A4-9DAF-E7961BF1F3B6}'
$clsid = '{A0D0C0DD-3B8D-4A44-BD18-40BF4E9DF8D4}'
$iid = '{7F0E3F9A-4A4C-4D44-AD09-5E13DF84B76E}'
$typeLib = '{A3D26C98-52F2-4F21-8D2F-5BC1F5D1F7D1}'
$registryViews = @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)

function Invoke-ProbeRollback {
    param([string]$Description, [scriptblock]$Action)
    if ($WhatIfPreference) { Write-Output "WHATIF: $Description" } else { & $Action }
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Invoke-ProbeRollback "stop temporary service $serviceName" { Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue }
    Invoke-ProbeRollback "delete temporary service $serviceName" { & sc.exe delete $serviceName | Out-Null }
}

foreach ($view in $registryViews) {
    $paths = @(
        "SOFTWARE\Classes\CLSID\$clsid",
        "SOFTWARE\Classes\AppID\$appId",
        'SOFTWARE\Classes\SEC18.CallerProbe',
        'SOFTWARE\Classes\SEC18.CallerProbe.1',
        "SOFTWARE\Classes\Interface\$iid",
        "SOFTWARE\Classes\TypeLib\$typeLib"
    )
    if ($WhatIfPreference) {
        foreach ($path in $paths) { Write-Output "WHATIF: remove temporary registry key $view $path" }
        continue
    }

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
    try {
        foreach ($path in $paths) {
            $parentPath = Split-Path -Path $path -Parent
            $leafName = Split-Path -Path $path -Leaf
            $parent = $base.OpenSubKey($parentPath, $true)
            try {
                if ($null -ne $parent -and $null -ne $parent.OpenSubKey($leafName, $false)) {
                    Invoke-ProbeRollback "remove temporary registry key $view $path" { $parent.DeleteSubKeyTree($leafName) }
                }
            }
            finally {
                if ($null -ne $parent) { $parent.Dispose() }
            }
        }
    }
    finally { $base.Dispose() }
}

foreach ($endpoint in @('caller-probe.php','caller-probe-wrong-sid.php','caller-probe-diagnostics.php','caller-probe-child-client.php','identity-debug.php')) {
    $path = Join-Path $webRoot $endpoint
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Invoke-ProbeRollback "remove temporary staging endpoint $path" { Remove-Item -LiteralPath $path -Force }
    }
}

if (Test-Path -LiteralPath $probeRoot -PathType Container) {
    Invoke-ProbeRollback "remove temporary probe files under $probeRoot" { Remove-Item -LiteralPath $probeRoot -Recurse -Force }
}

Write-Output 'Probe rollback does not modify IIS site/pool, PHP, WebAdmin authentication, firewall, hMailServer service, hMailServer registry, database, or data directory.'
