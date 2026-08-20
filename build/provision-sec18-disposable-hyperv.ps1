[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $IsoPath,

    [string] $RootPath = 'C:\SEC18-Disposable',
    [string] $VmName = 'HMailServer-SEC18-Disposable',
    [string] $SwitchName = 'HMailServer-SEC18-Private',
    [UInt64] $VhdSizeBytes = 64GB,
    [UInt64] $MemoryStartupBytes = 8GB,
    [int] $ProcessorCount = 4,
    [switch] $Start
)

$ErrorActionPreference = 'Stop'
$transcriptPath = Join-Path $env:PUBLIC 'sec18-hyperv-provision.log'
Start-Transcript -Path $transcriptPath -Force | Out-Null

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must run in an elevated PowerShell process.'
    }
}

function Assert-DisposablePath {
    param([string] $Path)

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $allowedRoot = 'C:\SEC18-Disposable'
    if (-not ($fullPath.Equals($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or
            $fullPath.StartsWith($allowedRoot + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing path outside ${allowedRoot}: $fullPath"
    }
    if ($fullPath -match '(?i)hMailServer57-Test|HmailDb|Program Files|Windows') {
        throw "Refusing path that resembles a production or system path: $fullPath"
    }
    return $fullPath
}

function Get-NonCoreServerImage {
    param([string] $ImageFile)

    $getWindowsImage = Get-Command Get-WindowsImage -ErrorAction SilentlyContinue
    if (-not $getWindowsImage) {
        throw 'Get-WindowsImage is required to select a non-Core Windows Server image.'
    }

    $images = @(Get-WindowsImage -ImagePath $ImageFile)
    $images | ForEach-Object {
        Write-Output ("Image {0}: {1} | {2}" -f $_.ImageIndex, $_.ImageName, $_.ImageDescription)
    }
    $image = $images |
        Where-Object { $_.ImageName -notmatch '(?i)CORE' -and $_.ImageDescription -notmatch '(?i)CORE' } |
        Select-Object -First 1
    if (-not $image) {
        $image = $images | Select-Object -First 1
    }
    if (-not $image) {
        throw 'No non-Core Windows Server image was found in the ISO.'
    }
    return $image
}

Assert-Administrator

$safeRoot = Assert-DisposablePath -Path $RootPath
$iso = (Resolve-Path -LiteralPath $IsoPath).Path
if ([IO.Path]::GetExtension($iso) -ne '.iso') {
    throw "Expected an ISO path: $iso"
}
if (-not (Test-Path -LiteralPath $iso -PathType Leaf)) {
    throw "ISO does not exist: $iso"
}
if (Get-VM -Name $VmName -ErrorAction SilentlyContinue) {
    throw "A VM named $VmName already exists; refusing to reuse it."
}

$vmRoot = Join-Path $safeRoot $VmName
$vhdRoot = Join-Path $vmRoot 'VirtualHardDisks'
$evidenceRoot = Join-Path $vmRoot 'Evidence'
New-Item -ItemType Directory -Force -Path $vhdRoot, $evidenceRoot | Out-Null
$vhdPath = Join-Path $vhdRoot "$VmName.vhdx"
$isoCopy = Join-Path $vmRoot ([IO.Path]::GetFileName($iso))

if (-not (Test-Path -LiteralPath $isoCopy)) {
    Copy-Item -LiteralPath $iso -Destination $isoCopy -Force
}
$isoHash = (Get-FileHash -LiteralPath $isoCopy -Algorithm SHA256).Hash

$switch = Get-VMSwitch -Name $SwitchName -ErrorAction SilentlyContinue
if (-not $switch) {
    $switch = New-VMSwitch -Name $SwitchName -SwitchType Private
}
if ($switch.SwitchType -ne 'Private') {
    throw "Refusing to use a non-private switch: $SwitchName ($($switch.SwitchType))"
}

$isoMount = $null
$vhdMount = $null
try {
    $isoMount = Mount-DiskImage -ImagePath $isoCopy -PassThru
    $isoVolume = $isoMount | Get-Volume
    $isoDrive = "$($isoVolume.DriveLetter):"
    $imageFile = Join-Path $isoDrive 'sources\install.wim'
    if (-not (Test-Path -LiteralPath $imageFile)) {
        $imageFile = Join-Path $isoDrive 'sources\install.esd'
    }
    if (-not (Test-Path -LiteralPath $imageFile)) {
        throw 'The mounted ISO has no sources\install.wim or sources\install.esd.'
    }
    $image = Get-NonCoreServerImage -ImageFile $imageFile

    New-VHD -Path $vhdPath -SizeBytes $VhdSizeBytes -Dynamic | Out-Null
    $vhdMount = Mount-VHD -Path $vhdPath -Passthru
    $disk = $vhdMount | Get-Disk
    Initialize-Disk -Number $disk.Number -PartitionStyle GPT -PassThru | Out-Null

    $efi = New-Partition -DiskNumber $disk.Number -Size 260MB -GptType '{C12A7328-F81F-11D2-BA4B-00A0C93EC93B}' -AssignDriveLetter
    New-Partition -DiskNumber $disk.Number -Size 16MB -GptType '{E3C9E316-0B5C-4DB8-817D-F92DF00215AE}' | Out-Null
    $os = New-Partition -DiskNumber $disk.Number -UseMaximumSize -AssignDriveLetter
    Format-Volume -Partition $efi -FileSystem FAT32 -NewFileSystemLabel 'SYSTEM' -Confirm:$false | Out-Null
    Format-Volume -Partition $os -FileSystem NTFS -NewFileSystemLabel 'WINDOWS' -Confirm:$false | Out-Null

    $efiPath = "$($efi.DriveLetter):\"
    $osPath = "$($os.DriveLetter):\"
    & dism.exe /English /Apply-Image /ImageFile:$imageFile /Index:$($image.ImageIndex) /ApplyDir:$osPath
    if ($LASTEXITCODE -ne 0) {
        throw "DISM image application failed with exit code $LASTEXITCODE."
    }
    & bcdboot.exe "$osPath`Windows" /s $efiPath /f UEFI
    if ($LASTEXITCODE -ne 0) {
        throw "BCDBoot failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($vhdMount) {
        Dismount-VHD -Path $vhdPath -ErrorAction SilentlyContinue
    }
    if ($isoMount) {
        Dismount-DiskImage -ImagePath $isoCopy -ErrorAction SilentlyContinue
    }
}

$vm = New-VM -Name $VmName -Generation 2 -MemoryStartupBytes $MemoryStartupBytes -VHDPath $vhdPath -Path $vmRoot -SwitchName $SwitchName
Set-VMProcessor -VMName $VmName -Count $ProcessorCount
Set-VM -Name $VmName -AutomaticStopAction ShutDown -CheckpointType Disabled
Set-VMFirmware -VMName $VmName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows

$evidence = [ordered]@{
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
    VmName = $VmName
    VmPath = $vmRoot
    VhdPath = $vhdPath
    SwitchName = $SwitchName
    SwitchType = (Get-VMSwitch -Name $SwitchName).SwitchType.ToString()
    IsoPath = $isoCopy
    IsoSha256 = $isoHash
    ImageIndex = $image.ImageIndex
    ImageName = $image.ImageName
    MemoryStartupBytes = $MemoryStartupBytes
    ProcessorCount = $ProcessorCount
    Started = [bool]$Start
    ProductionPathsTouched = @()
}
$evidencePath = Join-Path $evidenceRoot 'provisioning.json'
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidencePath -Encoding UTF8

if ($Start) {
    Start-VM -Name $VmName | Out-Null
}

$evidence | ConvertTo-Json -Depth 5
