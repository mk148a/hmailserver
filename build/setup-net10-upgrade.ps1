#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$InitializationFile,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$BackupArchive,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SqlRollbackBackupPath,
    [switch]$Execute,
    [switch]$Start
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AbsoluteLocalPath([string]$Path, [string]$Description) {
    if ($Path -notmatch '^[A-Za-z]:[\\/]' -or $Path.Substring(2).Contains(':')) {
        throw "$Description must be an absolute drive-qualified path (not a UNC or device path)."
    }
    # Refuse ambiguous Win32 aliases instead of allowing them to bypass containment checks.
    if ($Path -match '~[0-9]' -or @($Path -split '[\\/]' | Where-Object { $_ -notin @('.', '..') -and $_ -match '[. ]$' }).Count) {
        throw "$Description must not contain short-name aliases or trailing dots/spaces."
    }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $ancestor = $full
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "$Description must not traverse a reparse point: $ancestor"
            }
        }
        $ancestor = Split-Path -Parent $ancestor
    }
    return $full
}

function Test-Within([string]$Path, [string]$Root) {
    return $Path.Equals($Root, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($Root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)
}

$packageRoot = Get-AbsoluteLocalPath $PSScriptRoot 'Package root'
if ([IO.DriveInfo]::new([IO.Path]::GetPathRoot($packageRoot)).DriveType -ne [IO.DriveType]::Fixed) {
    throw 'Extract the package to a permanent directory on a fixed local drive.'
}
$ini = Get-AbsoluteLocalPath $InitializationFile 'InitializationFile'
if (-not (Test-Path -LiteralPath $ini -PathType Leaf)) { throw "InitializationFile was not found: $ini" }

# Read only the two directory settings; do not load, log, or copy database credentials.
$directories = @{}
$section = ''
foreach ($line in [IO.File]::ReadAllLines($ini)) {
    $text = $line.Trim()
    if (-not $text -or $text.StartsWith(';') -or $text.StartsWith('#')) { continue }
    if ($text.StartsWith('[') -and $text.EndsWith(']')) {
        $section = $text.Substring(1, $text.Length - 2).Trim()
        continue
    }
    if ($section -ne 'Directories') { continue }
    $equals = $text.IndexOf('=')
    if ($equals -lt 0) { throw 'Invalid [Directories] entry in InitializationFile.' }
    $key = $text.Substring(0, $equals).Trim()
    if ($key -notin @('ProgramFolder', 'DataFolder')) { continue }
    if ($directories.ContainsKey($key)) { throw "Duplicate [Directories] $key in InitializationFile." }
    $value = $text.Substring($equals + 1).Trim()
    if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    $directories[$key] = Get-AbsoluteLocalPath $value "[Directories] $key"
}
foreach ($key in @('ProgramFolder', 'DataFolder')) {
    if (-not $directories.ContainsKey($key)) { throw "InitializationFile requires an explicit [Directories] $key." }
}
$iniRoot = Split-Path -Parent $ini
if ((Split-Path -Leaf $iniRoot) -eq 'Bin') { $iniRoot = Split-Path -Parent $iniRoot }
foreach ($protectedRoot in @($directories.ProgramFolder, $iniRoot, $directories.DataFolder)) {
    if (Test-Within $packageRoot $protectedRoot) {
        throw 'Package root must be outside the legacy install tree and DataFolder. Extract to a separate permanent directory.'
    }
}

$backup = Get-AbsoluteLocalPath $BackupArchive 'BackupArchive'
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw "BackupArchive was not found: $backup" }
$rollback = Get-AbsoluteLocalPath $SqlRollbackBackupPath 'SqlRollbackBackupPath'
if ((Test-Path -LiteralPath $rollback) -or $rollback.Equals($backup, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'SqlRollbackBackupPath must name a new file, not an existing backup or directory.'
}
if (-not (Test-Path -LiteralPath (Split-Path -Parent $rollback) -PathType Container)) {
    throw 'SqlRollbackBackupPath requires an existing parent directory accessible to SQL Server.'
}
$bin = Join-Path $packageRoot 'Bin'
$runner = Join-Path $packageRoot 'Scripts\upgrade-net10-from-legacy.ps1'
$upgradeScript = Join-Path $packageRoot 'DBScripts\Upgrade5708to6000MSSQL.sql'
foreach ($relative in @('Bin\hMailServer.exe', 'Bin\hMailServer.tlb', 'Bin\7za.exe',
        'Bin\public_suffix_list.dat', 'Bin\public_suffix_list.meta.json',
        'Scripts\upgrade-net10-from-legacy.ps1', 'Scripts\install-net10-service.ps1',
        'Scripts\net10-service-rollback.ps1', 'Scripts\net10-rollback-archive-preflight.ps1',
        'DBScripts\Upgrade5708to6000MSSQL.sql')) {
    $path = Get-AbsoluteLocalPath (Join-Path $packageRoot $relative) 'Package file'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required package file was not found: $relative" }
}
$reportRoot = Get-AbsoluteLocalPath (Join-Path $packageRoot 'Reports') 'Report directory'
foreach ($protectedRoot in @($directories.ProgramFolder, $iniRoot, $directories.DataFolder)) {
    if (Test-Within $reportRoot $protectedRoot) { throw 'Report directory must be outside the legacy install tree and DataFolder.' }
}
$reportDirectory = Join-Path $reportRoot ('upgrade-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reportDirectory | Out-Null
$arguments = @{
    BinDirectory = $bin
    InitializationFile = $ini
    BackupArchive = $backup
    SqlRollbackBackupPath = $rollback
    UpgradeScriptPath = $upgradeScript
    UpgradeReportPath = Join-Path $reportDirectory 'upgrade-report.json'
    HandoffManifestPath = Join-Path $reportDirectory 'handoff-manifest.json'
    ExpectedTargetIdentity = [Environment]::MachineName + '|' + $ini
    OutputDirectory = $reportDirectory
    Execute = [bool]$Execute
    Start = [bool]$Start
}
& $runner @arguments
