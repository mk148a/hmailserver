[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$subjectPath = Join-Path $PSScriptRoot 'get-webadmin-broker-staging-inventory.ps1'
$null = . $subjectPath -WebAdminPath $PSScriptRoot

$baseKey = $null
$oleKey = $null

try {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Registry64)
    $oleKey = $baseKey.OpenSubKey('SOFTWARE\Microsoft\Ole', $false)

    if ($null -eq $oleKey) {
        throw 'The read-only HKLM\SOFTWARE\Microsoft\Ole test key is unavailable.'
    }

    $availableValueNames = @($oleKey.GetValueNames())
    $descriptorValueName = @(
        'DefaultLaunchPermission'
        'DefaultAccessPermission'
        'MachineLaunchRestriction'
        'MachineAccessRestriction'
    ) | Where-Object {
        $availableValueNames -contains $_ -and
        $oleKey.GetValueKind($_) -eq [Microsoft.Win32.RegistryValueKind]::Binary
    } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($descriptorValueName)) {
        throw 'No read-only binary DCOM security descriptor is available for the regression test.'
    }

    $value = Get-RegistryValueEvidence -Key $oleKey -Name $descriptorValueName
    if ($value -isnot [byte[]]) {
        throw "Expected Get-RegistryValueEvidence to return System.Byte[] but found $($value.GetType().FullName)."
    }

    $evidence = Convert-SecurityDescriptorToEvidence $value
    if ($null -ne $evidence.DecodeError) {
        throw "RawSecurityDescriptor decoding failed: $($evidence.DecodeError)"
    }

    if ([string]::IsNullOrWhiteSpace($evidence.Sddl)) {
        throw 'RawSecurityDescriptor decoding did not produce SDDL evidence.'
    }

    Write-Output "PASS: $descriptorValueName remained System.Byte[] and decoded as a security descriptor."
}
finally {
    if ($null -ne $oleKey) {
        $oleKey.Dispose()
    }

    if ($null -ne $baseKey) {
        $baseKey.Dispose()
    }
}
