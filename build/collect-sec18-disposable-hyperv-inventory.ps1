[CmdletBinding()]
param(
    [string] $RootPath = 'C:\SEC18-Disposable',
    [string] $VmName = 'HMailServer-SEC18-Disposable',
    [string] $SwitchName = 'HMailServer-SEC18-Private'
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must run in an elevated PowerShell process.'
}

$root = [IO.Path]::GetFullPath($RootPath).TrimEnd('\')
if (-not $root.Equals('C:\SEC18-Disposable', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing an inventory root outside C:\SEC18-Disposable: $root"
}

$vm = Get-VM -Name $VmName
$switch = Get-VMSwitch -Name $SwitchName
if ($switch.SwitchType -ne 'Private') {
    throw "Refusing to certify a non-private switch: $SwitchName ($($switch.SwitchType))"
}
$adapters = @(Get-VMNetworkAdapter -VMName $VmName)
$isoPath = Join-Path (Join-Path $root $VmName) 'WindowsServer2025-Eval-x64.iso'
if (-not (Test-Path -LiteralPath $isoPath -PathType Leaf)) {
    throw "Staged ISO is missing: $isoPath"
}
$iso = Get-Item -LiteralPath $isoPath
$hardDisk = @(Get-VMHardDiskDrive -VMName $VmName)
$processor = Get-VMProcessor -VMName $VmName
$evidenceRoot = Join-Path (Join-Path $root $VmName) 'Evidence'
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null

$evidence = [ordered]@{
    CollectedUtc = [DateTime]::UtcNow.ToString('o')
    VmName = $vm.Name
    VmState = $vm.State.ToString()
    VmPath = $vm.Path
    MemoryStartupBytes = [UInt64]$vm.MemoryStartup
    MemoryAssignedBytes = [UInt64]$vm.MemoryAssigned
    ProcessorCount = [int]$processor.Count
    VhdPaths = @($hardDisk.Path)
    SwitchName = $switch.Name
    SwitchType = $switch.SwitchType.ToString()
    AdapterCount = $adapters.Count
    AdapterSwitches = @($adapters.SwitchName)
    IsoPath = $iso.FullName
    IsoLength = $iso.Length
    IsoSha256 = (Get-FileHash -LiteralPath $iso.FullName -Algorithm SHA256).Hash
    ProductionPathsTouched = @()
}
$path = Join-Path $evidenceRoot 'hyperv-inventory.json'
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
$evidence | ConvertTo-Json -Depth 5
