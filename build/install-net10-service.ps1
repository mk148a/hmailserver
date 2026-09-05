Param(
    [string]$Configuration = 'Release',
    [string]$BinDirectory,
    [string]$InitializationFile,
    [switch]$ReplaceExisting,
    [string]$BackupArchive,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'net10-rollback-archive-preflight.ps1')
. (Join-Path $PSScriptRoot 'net10-service-rollback.ps1')

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
if (-not [string]::IsNullOrWhiteSpace($InitializationFile)) {
    $InitializationFile = [IO.Path]::GetFullPath($InitializationFile)
    if (-not (Test-Path -LiteralPath $InitializationFile -PathType Leaf)) {
        throw "Initialization file was not found: $InitializationFile"
    }
}

$serviceName = 'hMailServer'
$existingService = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'" -ErrorAction Stop
$serviceExists = $null -ne $existingService
$requiresRollbackArchive = $false
$rollbackSnapshot = $null
$legacyExecutable = $null
if ($serviceExists) {
    if ($existingService.State -ne 'Stopped') {
        throw "Stop service '$serviceName' before installation or replacement."
    }
    $existingExecutable = Get-Net10ServiceExecutablePath -PathName $existingService.PathName
    if (-not $existingExecutable.Equals($executable, [StringComparison]::OrdinalIgnoreCase)) {
        if (-not $ReplaceExisting) {
            throw "Service '$serviceName' already points to '$existingExecutable'. Use -ReplaceExisting only after backing up and stopping the legacy installation."
        }
        if ($existingService.State -ne 'Stopped') {
            throw "Stop service '$serviceName' before using -ReplaceExisting."
        }
        $requiresRollbackArchive = $true
        $legacyExecutable = $existingExecutable
        if (-not (Test-Path -LiteralPath $legacyExecutable -PathType Leaf)) {
            throw "Legacy executable for rollback was not found: $legacyExecutable"
        }
    }

    $rollbackSnapshot = New-Net10ServiceRollbackSnapshot -Service $existingService
    $legacyExecutable = $existingExecutable
}

if ($requiresRollbackArchive) {
    if ([string]::IsNullOrWhiteSpace($BackupArchive)) {
        throw '-BackupArchive is required when -ReplaceExisting replaces a service that points to a different executable.'
    }

    $sevenZip = Join-Path $bin '7za.exe'
    Assert-Net10RollbackArchivePreflight -BackupArchive $BackupArchive -SevenZipPath $sevenZip
}

$comRegistrationAttempted = $false
$serviceMutationAttempted = $false
try {
    $comRegistrationAttempted = $true
    & $executable --register-com
    if ($LASTEXITCODE -ne 0) {
        throw "COM registration failed with exit code $LASTEXITCODE."
    }

    $quotedExecutable = '"{0}"' -f $executable
    $servicePath = $quotedExecutable
    if (-not [string]::IsNullOrWhiteSpace($InitializationFile)) {
        $servicePath = '{0} --InitializationFile "{1}"' -f $quotedExecutable, $InitializationFile
    }
    if ($serviceExists) {
        $serviceMutationAttempted = $true
        & sc.exe config $serviceName "binPath= $servicePath" 'start= auto' 'DisplayName= hMailServer'
    }
    else {
        $serviceMutationAttempted = $true
        & sc.exe create $serviceName "binPath= $servicePath" 'start= auto' 'error= normal' 'DisplayName= hMailServer' 'depend= RPCSS'
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
    $rollbackErrors = [System.Collections.Generic.List[string]]::new()
    if ($serviceExists -and $null -ne $rollbackSnapshot -and ($serviceMutationAttempted -or $comRegistrationAttempted)) {
        try {
            Invoke-Net10ServiceRollback -Snapshot $rollbackSnapshot -LegacyExecutable $legacyExecutable
        }
        catch {
            $rollbackErrors.Add($_.Exception.Message)
        }
    }
    elseif (-not $serviceExists) {
        if ($serviceMutationAttempted) {
            & sc.exe delete $serviceName | Out-Null
        }
        if ($comRegistrationAttempted) {
            & $executable --unregister-com | Out-Null
        }
    }

    if ($rollbackErrors.Count -gt 0) {
        throw "Installer failed: $($_.Exception.Message) Rollback failed: $($rollbackErrors -join ' | ')"
    }

    throw
}
