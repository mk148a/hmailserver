Param(
    [string]$Configuration = 'Release',
    [string]$BinDirectory,
    [switch]$ReplaceExisting,
    [string]$BackupArchive,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'net10-rollback-archive-preflight.ps1')

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session.'
    }
}

Assert-Administrator

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
if (-not $BinDirectory) {
    $BinDirectory = Join-Path $repoRoot "hmailserver\source\Server.Net10\src\HMailServer.Service\bin\$Configuration\net10.0-windows"
}

$bin = [System.IO.Path]::GetFullPath($BinDirectory)
$executable = Join-Path $bin 'hMailServer.exe'
$typeLibrary = Join-Path $bin 'hMailServer.tlb'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Service executable was not found: $executable"
}
if (-not (Test-Path -LiteralPath $typeLibrary)) {
    throw "Type library was not found: $typeLibrary"
}

$serviceName = 'hMailServer'
$existingService = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
$serviceExists = $null -ne $existingService
$requiresRollbackArchive = $false
if ($serviceExists) {
    $existingExecutable = $existingService.PathName.Trim().Trim('"')
    if (-not $existingExecutable.Equals($executable, [StringComparison]::OrdinalIgnoreCase)) {
        if (-not $ReplaceExisting) {
            throw "Service '$serviceName' already points to '$existingExecutable'. Use -ReplaceExisting only after backing up and stopping the legacy installation."
        }
        if ($existingService.State -ne 'Stopped') {
            throw "Stop service '$serviceName' before using -ReplaceExisting."
        }
        $requiresRollbackArchive = $true
    }
}

if ($requiresRollbackArchive) {
    if ([string]::IsNullOrWhiteSpace($BackupArchive)) {
        throw '-BackupArchive is required when -ReplaceExisting replaces a service that points to a different executable.'
    }

    $sevenZip = Join-Path $bin '7za.exe'
    Assert-Net10RollbackArchivePreflight -BackupArchive $BackupArchive -SevenZipPath $sevenZip
}

& $executable --register-com
if ($LASTEXITCODE -ne 0) {
    throw "COM registration failed with exit code $LASTEXITCODE."
}

try {
    $quotedExecutable = '"{0}"' -f $executable
    if ($serviceExists) {
        & sc.exe config $serviceName "binPath= $quotedExecutable" 'start= auto' 'DisplayName= hMailServer'
    }
    else {
        & sc.exe create $serviceName "binPath= $quotedExecutable" 'start= auto' 'DisplayName= hMailServer'
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Windows service registration failed with exit code $LASTEXITCODE."
    }

    & sc.exe description $serviceName 'hMailServer .NET 10 rewrite service'
    if ($LASTEXITCODE -ne 0) {
        throw "Windows service description update failed with exit code $LASTEXITCODE."
    }

    if ($Start) {
        Start-Service -Name $serviceName
    }
}
catch {
    if (-not $serviceExists) {
        & sc.exe delete $serviceName | Out-Null
    }
    & $executable --unregister-com
    throw
}
