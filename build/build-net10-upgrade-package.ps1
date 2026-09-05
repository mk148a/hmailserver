#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "OutputDirectory must be fresh; already exists: $output" }
$parent = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'OutputDirectory requires an existing parent directory.' }
$localDotnet = Join-Path $workspaceRoot 'tools\dotnet10\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet -PathType Leaf) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$project = Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Service\HMailServer.Service.csproj'
$scripts = @('upgrade-net10-from-legacy.ps1', 'install-net10-service.ps1',
    'net10-service-rollback.ps1', 'net10-rollback-archive-preflight.ps1')
$revision = & git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') { throw 'Cannot determine git source revision.' }
$sourceStatus = @(& git -C $repoRoot status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine git source status.' }
$tracked = @(& git -C $repoRoot -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate checked-in licenses.' }
$licenses = @($tracked | Where-Object { $_ -match '(^|/)([^/]*license[^/]*|copying[^/]*|notice[^/]*)$' })
$sourceFiles = @{ 'Setup.ps1' = Join-Path $PSScriptRoot 'setup-net10-upgrade.ps1';
    'DBScripts/Upgrade5708to6000MSSQL.sql' = Join-Path $repoRoot 'hmailserver\source\DBScripts\Upgrade5708to6000MSSQL.sql' }
foreach ($name in $scripts) { $sourceFiles["Scripts/$name"] = Join-Path $PSScriptRoot $name }
foreach ($name in $licenses) {
    if (Test-Path -LiteralPath (Join-Path $repoRoot $name) -PathType Leaf) {
        $sourceFiles["Licenses/$name"] = Join-Path $repoRoot $name
    }
}
foreach ($path in @($project) + @($sourceFiles.Values)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required source file missing: $path" }
}

New-Item -ItemType Directory -Path $output | Out-Null
$publish = Join-Path $output 'publish'
$package = Join-Path $output 'package'
$bin = Join-Path $package 'Bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$oldDotnetRoot = $env:DOTNET_ROOT
$oldPath = $env:PATH
try {
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $env:PATH = "$env:DOTNET_ROOT;$oldPath"
    & $dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
        --output $publish --artifacts-path (Join-Path $output 'build-artifacts') `
        -p:PublishTrimmed=false -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Net10 publish failed with exit code $LASTEXITCODE. Output retained: $output" }
} finally {
    $env:DOTNET_ROOT = $oldDotnetRoot
    $env:PATH = $oldPath
}
$required = @('hMailServer.exe', 'hMailServer.dll', 'hMailServer.tlb', '7za.exe',
    'hMailServer.deps.json', 'hMailServer.runtimeconfig.json', 'public_suffix_list.dat',
    'public_suffix_list.meta.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $name) -PathType Leaf)) { throw "Publish is missing $name" }
}
# Only fresh publish runtime assets and the explicit source map enter the ZIP.
foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse) {
    $relative = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
    # Keep every file emitted by the self-contained publish. Native runtime
    # assets include DLLs and executables that vary by SDK/runtime patch level.
    $destination = Join-Path $bin $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
foreach ($relative in $sourceFiles.Keys) {
    $destination = Join-Path $package $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $sourceFiles[$relative] -Destination $destination
}
$readme = @'
# hMailServer Net10 Standalone Upgrade ZIP (win-x64)

This ZIP is the installer; Inno Setup is not required. It contains the Release,
self-contained .NET 10 service, unchanged legacy type library and guarded upgrade
scripts, the 5708-to-6000 MSSQL migration, 7-Zip, public suffix snapshot, and
available checked-in licenses. No installed .NET runtime is required.
It is an upgrade package, not a first-install or legacy binary replacement tool.

Verify the adjacent .zip.sha256 against Get-FileHash -Algorithm SHA256 before
extracting. manifest.json records source revision, dirty-worktree status, and
SHA-256/length for every package file except the manifest itself. Hashes detect
corruption, not publisher authenticity. This package is not signed. Bin contains
only fresh publish assets; no legacy INI, passwords, mail, or backups are included.

Extract ALL files to a NEW PERMANENT fixed-local-drive directory, for example
C:\hMailServer Net10. Never extract into the legacy install tree or DataFolder.
Do not move/delete that directory after cutover: the service runs from its Bin.
Do not copy Bin into the legacy installation. Keep legacy binaries for rollback.
Setup rejects reparse points, short-name aliases, and ambiguous directory paths.

Use 64-bit Windows PowerShell 5.1. Supply absolute drive-qualified paths (UNC paths
are not supported by this wrapper). The legacy INI must contain explicit absolute
[Directories] ProgramFolder and DataFolder settings. Do not put credentials on
the command line. Keep the package and report directory writable only by trusted
administrators; reports contain local paths and should not be redistributed.

Before planning/execution, arrange an offline maintenance window and stop the
legacy hMailServer service yourself. Supply a full compressed hMailServer backup
including settings, domains, and messages. The existing guarded runner validates
the archive and requires the legacy service to be stopped even for a plan.

Example (default PlanOnly, no service/COM/SQL mutation):

```powershell
& 'C:\hMailServer Net10\Setup.ps1' `
  -InitializationFile 'C:\Program Files\hMailServer\Bin\hMailServer.ini' `
  -BackupArchive 'D:\Backups\Full hMailServer backup.7z' `
  -SqlRollbackBackupPath 'D:\Backups\New Net10 rollback.bak'
```

Review the returned plan and Reports\upgrade-<UTC>-<GUID>\upgrade-plan.json.
Each invocation gets new report/handoff paths, bound to machine name + INI path.
To execute, rerun from elevated PowerShell with -Execute. Add -Start only when
ready to start the upgraded service; -Start alone still only plans. Without
-Start, cutover leaves the service stopped. Setup delegates all mutation and
rollback to the bundled existing guarded runner; it does not stop services or
change legacy files itself. Never bypass the runner to install directly.

SqlRollbackBackupPath must be a NEW file in an existing directory, different
from BackupArchive. SQL Server's service identity must have write/read access;
the path is interpreted by SQL Server. For this local-path wrapper use a local
SQL Server target. A plan does not prove SQL connectivity, backup permissions,
schema compatibility, or successful recovery. Execute creates a COPY_ONLY SQL
backup before migration. Keep both backups, reports, and the legacy tree until
the upgrade is independently verified. No live cutover is exercised by the
package tests; they use a stub runner and temporary fixtures only.

Build from source using build\build-net10-upgrade-package.ps1 -OutputDirectory
<new-directory>. The builder prefers ..\tools\dotnet10\dotnet.exe and requires
the existing Windows SDK/Visual Studio MIDL prerequisites. It never deletes an
existing output directory. Review Licenses and provide corresponding source as
required before redistribution; included notices are not a full license audit.
'@
Set-Content -LiteralPath (Join-Path $package 'README.md') -Value $readme -Encoding UTF8
$fileHashes = @(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path = $_.FullName.Substring($package.Length + 1).Replace('\', '/');
        length = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$manifest = [ordered]@{
    schemaVersion = 1; sourceRevision = [string]$revision; sourceWorkingTreeDirty = ($sourceStatus.Count -gt 0)
    createdUtc = [DateTime]::UtcNow.ToString('o'); configuration = 'Release'; runtimeIdentifier = 'win-x64'
    selfContained = $true; trimmed = $false; singleFile = $false; files = $fileHashes
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding UTF8
$zipPath = Join-Path $output ('hMailServer-net10-upgrade-win-x64-' + $revision.Substring(0, 12) + '.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($zipPath + '.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($zipPath)) -Encoding ASCII
[pscustomobject]@{ ZipPath = $zipPath; Sha256 = $hash; ManifestPath = Join-Path $package 'manifest.json' }
