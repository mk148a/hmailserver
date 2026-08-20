[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string] $RootPath = 'C:\SEC18-Disposable',
    [string] $VmName = 'HMailServer-SEC18-Disposable',
    [string] $SwitchName = 'HMailServer-SEC18-Private',
    [switch] $RemovePrivateSwitch
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must run in an elevated PowerShell process.'
}

$allowedRoot = [IO.Path]::GetFullPath($RootPath).TrimEnd('\')
if (-not $allowedRoot.Equals('C:\SEC18-Disposable', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove anything outside C:\SEC18-Disposable: $allowedRoot"
}

$vm = Get-VM -Name $VmName -ErrorAction SilentlyContinue
if ($vm) {
    if ($PSCmdlet.ShouldProcess($VmName, 'Stop and remove disposable Hyper-V VM')) {
        if ($vm.State -ne 'Off') {
            Stop-VM -Name $VmName -TurnOff -Force
        }
        Remove-VM -Name $VmName -Force
    }
}

$vmRoot = Join-Path $allowedRoot $VmName
if (Test-Path -LiteralPath $vmRoot) {
    if ($PSCmdlet.ShouldProcess($vmRoot, 'Remove disposable VM files')) {
        Remove-Item -LiteralPath $vmRoot -Recurse -Force
    }
}

if ($RemovePrivateSwitch) {
    $switch = Get-VMSwitch -Name $SwitchName -ErrorAction SilentlyContinue
    if ($switch) {
        if ($switch.SwitchType -ne 'Private') {
            throw "Refusing to remove a non-private switch: $SwitchName ($($switch.SwitchType))"
        }
        $attached = @(Get-VMNetworkAdapter -All | Where-Object SwitchName -eq $SwitchName)
        if ($attached.Count -gt 0) {
            throw "Refusing to remove $SwitchName while adapters are attached."
        }
        if ($PSCmdlet.ShouldProcess($SwitchName, 'Remove disposable private Hyper-V switch')) {
            Remove-VMSwitch -Name $SwitchName -Force
        }
    }
}

Write-Output 'Disposable SEC-18 Hyper-V objects removed or already absent.'
