[CmdletBinding()]
param(
    [string]$BinDirectory,
    [string]$InitializationFile,
    [string]$ServiceName = 'hMailServer',
    [Parameter(Mandatory)][string]$BackupArchive,
    [Parameter(Mandatory)][string]$UpgradeReportPath,
    [Parameter(Mandatory)][string]$HandoffManifestPath,
    [Parameter(Mandatory)][string]$ExpectedTargetIdentity,
    [string]$OutputDirectory,
    [switch]$Execute,
    [switch]$Start
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
if (-not $BinDirectory) {
    $BinDirectory = Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Service\bin\Release\net10.0-windows'
}
$bin = [IO.Path]::GetFullPath($BinDirectory)
$executable = Join-Path $bin 'hMailServer.exe'
$typeLibrary = Join-Path $bin 'hMailServer.tlb'
$installerPath = Join-Path $PSScriptRoot 'install-net10-service.ps1'

. (Join-Path $PSScriptRoot 'net10-rollback-archive-preflight.ps1')
. (Join-Path $PSScriptRoot 'net10-service-rollback.ps1')

function Assert-RequiredFile {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Description)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
}

function Assert-Sha256 {
    param([Parameter(Mandatory)][string]$Value, [Parameter(Mandatory)][string]$Description)

    if ($Value -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "$Description is not a SHA-256 digest."
    }
}

function Assert-MatchingPath {
    param(
        [Parameter(Mandatory)][string]$Actual,
        [Parameter(Mandatory)][string]$Expected,
        [Parameter(Mandatory)][string]$Description
    )

    if (-not [string]::Equals(
            [IO.Path]::GetFullPath($Actual),
            [IO.Path]::GetFullPath($Expected),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description does not match the requested path."
    }
}

function Assert-UpgradeHandoff {
    param(
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string]$RequestedBackupArchive,
        [Parameter(Mandatory)][string]$RequestedUpgradeReport,
        [Parameter(Mandatory)][string]$TargetIdentity
    )

    Assert-RequiredFile -Path $ManifestPath -Description 'Upgrade handoff manifest'
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($manifest.status -ne 'ReadyForServiceMutation' -or $manifest.serviceMutationAllowed -ne $true) {
        throw 'Upgrade handoff does not authorize service mutation.'
    }
    if ([string]$manifest.targetIdentity -ne $TargetIdentity) {
        throw 'Upgrade handoff target identity does not match the requested target.'
    }

    Assert-MatchingPath -Actual ([string]$manifest.backupArtifactPath) -Expected $RequestedBackupArchive -Description 'Handoff backup artifact'
    Assert-Sha256 -Value ([string]$manifest.backupArtifactSha256) -Description 'Handoff backup artifact'
    Assert-RequiredFile -Path $RequestedBackupArchive -Description 'Verified backup archive'
    $backupHash = (Get-FileHash -LiteralPath $RequestedBackupArchive -Algorithm SHA256).Hash
    if (-not [string]::Equals($backupHash, [string]$manifest.backupArtifactSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Verified backup archive digest does not match the handoff manifest.'
    }

    Assert-MatchingPath -Actual ([string]$manifest.upgradeReportPath) -Expected $RequestedUpgradeReport -Description 'Handoff upgrade report'
    Assert-Sha256 -Value ([string]$manifest.upgradeReportSha256) -Description 'Handoff upgrade report'
    Assert-RequiredFile -Path $RequestedUpgradeReport -Description 'Completed upgrade report'
    $reportHash = (Get-FileHash -LiteralPath $RequestedUpgradeReport -Algorithm SHA256).Hash
    if (-not [string]::Equals($reportHash, [string]$manifest.upgradeReportSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Upgrade report digest does not match the handoff manifest.'
    }

    $report = Get-Content -LiteralPath $RequestedUpgradeReport -Raw | ConvertFrom-Json
    if ($report.status -ne 'Completed' -or $null -eq $report.migration -or $report.migration.status -ne 'Completed') {
        throw 'Upgrade report does not prove completed migration and reinitialization.'
    }

    return $manifest
}

function Get-LegacyServiceState {
    param([Parameter(Mandatory)][string]$Name)

    $escapedName = $Name.Replace("'", "''")
    return Get-CimInstance -ClassName Win32_Service -Filter "Name='$escapedName'" -ErrorAction SilentlyContinue
}

Assert-RequiredFile -Path $executable -Description 'Net10 service executable'
Assert-RequiredFile -Path $typeLibrary -Description 'Net10 type library'
Assert-RequiredFile -Path $installerPath -Description 'Net10 installer'
Assert-RequiredFile -Path $InitializationFile -Description 'Legacy initialization file'

$handoff = Assert-UpgradeHandoff `
    -ManifestPath $HandoffManifestPath `
    -RequestedBackupArchive $BackupArchive `
    -RequestedUpgradeReport $UpgradeReportPath `
    -TargetIdentity $ExpectedTargetIdentity

$sevenZip = Join-Path $bin '7za.exe'
Assert-Net10RollbackArchivePreflight -BackupArchive $BackupArchive -SevenZipPath $sevenZip

$legacyService = Get-LegacyServiceState -Name $ServiceName
if ($null -eq $legacyService) {
    throw "Legacy service '$ServiceName' was not found. This script performs an upgrade, not a first install."
}
if ($legacyService.State -ne 'Stopped') {
    throw "Legacy service '$ServiceName' must be stopped before upgrade."
}

$legacyExecutable = Get-Net10ServiceExecutablePath -PathName $legacyService.PathName
if ([string]::Equals($legacyExecutable, $executable, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Service '$ServiceName' already points to the Net10 executable."
}
Assert-RequiredFile -Path $legacyExecutable -Description 'Legacy service executable'

$plan = [ordered]@{
    status = 'ReadyForServiceMutation'
    mode = if ($Execute) { 'Execute' } else { 'PlanOnly' }
    serviceName = $ServiceName
    serviceState = $legacyService.State
    legacyExecutable = $legacyExecutable
    net10Executable = $executable
    typeLibrary = $typeLibrary
    initializationFile = [IO.Path]::GetFullPath($InitializationFile)
    backupArchive = [IO.Path]::GetFullPath($BackupArchive)
    upgradeReport = [IO.Path]::GetFullPath($UpgradeReportPath)
    handoffManifest = [IO.Path]::GetFullPath($HandoffManifestPath)
    targetIdentity = $ExpectedTargetIdentity
    serviceMutation = 'install-net10-service.ps1 -ReplaceExisting'
    startRequested = [bool]$Start
    rollback = 'net10-service-rollback.ps1 plus legacy /Register on installer failure'
}

if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $planPath = Join-Path $outputRoot 'upgrade-plan.json'
    $plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $planPath -Encoding UTF8
    $plan.planPath = $planPath
}

if (-not $Execute) {
    [pscustomobject]$plan
    return
}

if (-not [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run an executing upgrade from an elevated PowerShell session.'
}

$installerArguments = @(
    '-NoProfile'
    '-ExecutionPolicy'
    'Bypass'
    '-File'
    $installerPath
    '-BinDirectory'
    $bin
    '-InitializationFile'
    ([IO.Path]::GetFullPath($InitializationFile))
    '-ReplaceExisting'
    '-BackupArchive'
    ([IO.Path]::GetFullPath($BackupArchive))
)
if ($Start) {
    $installerArguments += '-Start'
}

& powershell.exe @installerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Net10 installer failed with exit code $LASTEXITCODE. Its compensating service/COM rollback was invoked."
}

[pscustomobject]$plan
