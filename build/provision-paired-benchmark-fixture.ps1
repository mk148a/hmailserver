param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [Parameter(Mandatory = $true)]
    [string]$SourceDataRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [string]$LegacyBinPath = "",
    [string]$Net10BinPath = "",
    [string]$UpgradeScriptPath = "",
    [string]$Stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss')
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot 'live-benchmark-provenance.ps1')
$expectedUpgradeScriptPath = [IO.Path]::GetFullPath((Join-Path $repoRoot 'hmailserver\source\DBScripts\Upgrade5708to6000MSSQL.sql'))
$expectedUpgradeScriptSha256 = '7B0C7A56545912C8A1A85E361D52D52E5B56BDEC6B19E9BA95901CFA106E2FB2'
$expectedNet10BinPath = [IO.Path]::GetFullPath((Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Service\bin\Release\net10.0-windows'))

function Test-PathContainsReparsePoint {
    param([string]$Path)

    $current = [IO.Path]::GetFullPath($Path)
    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                return $true
            }
        }
        $parent = [IO.Directory]::GetParent($current)
        if ($null -eq $parent -or $parent.FullName -eq $current) {
            break
        }
        $current = $parent.FullName
    }
    return $false
}

function Test-TreeContainsReparsePoint {
    param([string]$Root)

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return $false
    }
    foreach ($item in Get-ChildItem -LiteralPath $Root -Force -Recurse -ErrorAction Stop) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            return $true
        }
    }
    return $false
}

function Resolve-SafeBenchmarkInput {
    param([string]$Path, [string]$Label)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if (Test-PathContainsReparsePoint $fullPath) {
        throw "$Label must not use a reparse point: $fullPath"
    }
    if ($fullPath -match '(?i)(^|\\)(hmailserver57|hmailserver|hmaildb_test5700|Program Files|ProgramData|Windows)(\\|$)') {
        throw "$Label is protected or production-like: $fullPath"
    }
    if ($fullPath -notmatch '(?i)^C:\\hmail-perf-[a-z0-9_-]+(?:\\.*)?$') {
        throw "$Label must be under an approved disposable benchmark root: $fullPath"
    }
    if (Test-TreeContainsReparsePoint $fullPath) {
        throw "$Label must not contain a reparse point: $fullPath"
    }
    return $fullPath
}

function Assert-DisposableName {
    param([string]$Value, [string]$Label)
    if ($Value -notmatch '^hmail_perf_pair_(cpp|net10)_[a-z0-9_]+$') {
        throw "$Label is not a disposable benchmark name: $Value"
    }
}

function Invoke-Sql {
    param([string]$Query)
    & sqlcmd.exe -S localhost -E -b -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed with exit code $LASTEXITCODE."
    }
}

function Invoke-SqlFile {
    param([string]$Database, [string]$Path)
    & sqlcmd.exe -S localhost -E -b -d $Database -i $Path
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed to execute $Path against $Database with exit code $LASTEXITCODE."
    }
}

function Get-DatabaseVersion {
    param([string]$Database)
    $value = (& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -Q 'SET NOCOUNT ON; SELECT value FROM hm_dbversion;').Trim()
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^\d+$') {
        throw "Could not read hm_dbversion from $Database."
    }
    return [int]$value
}

function Get-DirectoryManifest {
    param([string]$Root)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $rows = foreach ($file in Get-ChildItem -LiteralPath $fullRoot -File -Recurse | Sort-Object FullName) {
        $relative = $file.FullName.Substring($fullRoot.Length).TrimStart('\').Replace('/', '\')
        "$relative|$($file.Length)|$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
    }
    $payload = [Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $digest = ([BitConverter]::ToString($sha.ComputeHash($payload))).Replace('-', '') }
    finally { $sha.Dispose() }
    return [pscustomobject]@{
        fileCount = @($rows).Count
        bytes = (Get-ChildItem -LiteralPath $fullRoot -File -Recurse | Measure-Object Length -Sum).Sum
        sha256 = $digest
    }
}

function Get-MessageFingerprint {
    param([string]$Database)
    $query = @"
SET NOCOUNT ON;
DECLARE @payload nvarchar(max);
SELECT @payload = STRING_AGG(CAST(CONCAT(
    messageid, N'|', messageaccountid, N'|', messagefolderid, N'|',
    CASE WHEN CHARINDEX(N'\Data\', messagefilename) > 0
         THEN SUBSTRING(messagefilename, CHARINDEX(N'\Data\', messagefilename) + 6, 4000)
         ELSE messagefilename END, N'|',
    messagetype, N'|', COALESCE(messagefrom, N''), N'|', messagesize, N'|',
    messagecurnooftries, N'|', CONVERT(nvarchar(33), messagenexttrytime, 126), N'|',
    messageflags, N'|', CONVERT(nvarchar(33), messagecreatetime, 126), N'|',
    messagelocked, N'|', messageuid, N'|', COALESCE(messageruleforcedrouteid, -1), N'|',
    COALESCE(messagerulebindaddress, N'')) AS nvarchar(max)), NCHAR(10))
    WITHIN GROUP (ORDER BY messageid)
FROM hm_messages;
SELECT CONCAT(COUNT_BIG(*), N'|', CONVERT(varchar(64), HASHBYTES('SHA2_256', COALESCE(@payload, N'')), 2))
FROM hm_messages;
"@
    $value = (& sqlcmd.exe -S localhost -E -b -d $Database -h-1 -W -Q $query).Trim()
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^\d+\|[0-9A-F]{64}$') {
        throw "Could not calculate the logical message fingerprint for $Database."
    }
    $parts = $value.Split('|')
    return [pscustomobject]@{ rowCount = [long]$parts[0]; sha256 = $parts[1] }
}

function Get-BackupLogicalFiles {
    param([string]$Path)
    $rows = @(& sqlcmd.exe -S localhost -E -W -s '|' -h-1 -b -Q "RESTORE FILELISTONLY FROM DISK = N'$($Path.Replace("'", "''"))'")
    if ($LASTEXITCODE -ne 0 -or $rows.Count -lt 2) {
        throw "Could not inspect SQL backup: $Path"
    }
    $files = foreach ($row in $rows) {
        $parts = $row.Trim().Split('|')
        if ($parts.Count -ge 3 -and ($parts[2] -eq 'D' -or $parts[2] -eq 'L')) {
            [pscustomobject]@{ LogicalName = $parts[0].Trim(); Type = $parts[2].Trim() }
        }
    }
    if (@($files | Where-Object Type -eq 'D').Count -ne 1 -or @($files | Where-Object Type -eq 'L').Count -ne 1) {
        throw "Expected exactly one data and one log file in backup: $Path"
    }
    return $files
}

if (-not [string]::IsNullOrWhiteSpace($UpgradeScriptPath) -and
    -not [string]::Equals([IO.Path]::GetFullPath($UpgradeScriptPath), $expectedUpgradeScriptPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "UpgradeScriptPath is pinned to the checked-in repository script: $expectedUpgradeScriptPath"
}
$UpgradeScriptPath = $expectedUpgradeScriptPath
if (-not (Test-Path -LiteralPath $UpgradeScriptPath -PathType Leaf)) {
    throw "Net10 upgrade script is missing: $UpgradeScriptPath"
}
$upgradeScriptSha256 = (Get-FileHash -LiteralPath $UpgradeScriptPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($upgradeScriptSha256 -ne $expectedUpgradeScriptSha256) {
    throw "Checked-in Net10 upgrade script hash does not match the expected SHA-256."
}

$BackupPath = Resolve-SafeBenchmarkInput $BackupPath 'BackupPath'
$SourceDataRoot = Resolve-SafeBenchmarkInput $SourceDataRoot 'SourceDataRoot'
if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) { throw "Backup does not exist: $BackupPath" }
if (-not (Test-Path -LiteralPath $SourceDataRoot -PathType Container)) { throw "Source Data root does not exist: $SourceDataRoot" }
if (-not [string]::IsNullOrWhiteSpace($Net10BinPath) -and
    -not [string]::Equals([IO.Path]::GetFullPath($Net10BinPath), $expectedNet10BinPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Net10BinPath is pinned to the repository Release output: $expectedNet10BinPath"
}
$Net10BinPath = $expectedNet10BinPath
if ([string]::IsNullOrWhiteSpace($LegacyBinPath)) {
    $LegacyBinPath = Join-Path (Split-Path -Parent $SourceDataRoot) 'Bin'
}
$LegacyBinPath = [IO.Path]::GetFullPath($LegacyBinPath)
if ($LegacyBinPath -notmatch '(?i)^C:\\hmail-perf-cpp-build-[a-z0-9_-]+\\Bin$') {
    throw "LegacyBinPath is not an approved disposable clean C++ build root: $LegacyBinPath"
}
if (Test-PathContainsReparsePoint $LegacyBinPath) {
    throw "LegacyBinPath must not use a reparse point: $LegacyBinPath"
}
$legacyBuildRoot = Split-Path -Parent $LegacyBinPath
$legacyBuildManifestPath = Join-Path $legacyBuildRoot 'legacy-build.json'
if (-not (Test-Path -LiteralPath $legacyBuildManifestPath -PathType Leaf)) {
    throw "LegacyBinPath is not a clean C++ build root with a build manifest: $LegacyBinPath"
}
try {
    $legacyBuildManifest = Get-Content -LiteralPath $legacyBuildManifestPath -Raw | ConvertFrom-Json
}
catch {
    throw "Legacy C++ build manifest is invalid: $legacyBuildManifestPath"
}
if ($legacyBuildManifest.status -ne 'PASS' -or $legacyBuildManifest.postBuildRegistrationDisabled -ne $true -or
    -not [string]::Equals([IO.Path]::GetFullPath([string]$legacyBuildManifest.outputRoot), $legacyBuildRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals([IO.Path]::GetFullPath([string]$legacyBuildManifest.executable), (Join-Path $LegacyBinPath 'hMailServer.exe'), [StringComparison]::OrdinalIgnoreCase)) {
    throw "LegacyBinPath is not a clean, registration-disabled C++ build root: $LegacyBinPath"
}
if (-not (Test-Path -LiteralPath (Join-Path $LegacyBinPath 'hMailServer.exe') -PathType Leaf)) {
    throw "Legacy C++ executable is missing under: $LegacyBinPath"
}
$actualLegacyHash = (Get-FileHash -LiteralPath (Join-Path $LegacyBinPath 'hMailServer.exe') -Algorithm SHA256).Hash
if (-not [string]::Equals($actualLegacyHash, [string]$legacyBuildManifest.executableSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw "LegacyBinPath executable hash does not match the clean build manifest."
}
if ((Test-PathContainsReparsePoint $Net10BinPath) -or (Test-TreeContainsReparsePoint $Net10BinPath)) {
    throw "Net10BinPath must not use or contain a reparse point: $Net10BinPath"
}
$net10SourceExecutable = Join-Path $Net10BinPath 'hMailServer.exe'
if (-not (Test-Path -LiteralPath $net10SourceExecutable -PathType Leaf)) {
    throw "Net10 Release executable is missing under: $Net10BinPath"
}
$fullOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ($fullOutputRoot -notmatch '(?i)^C:\\hmail-perf-pair-') { throw "Output root is not disposable: $fullOutputRoot" }
if ($fullOutputRoot -match '(?i)hmailserver57|hmaildb_test5700') { throw "Output root resembles production: $fullOutputRoot" }

$cppDatabase = "hmail_perf_pair_cpp_$Stamp"
$net10Database = "hmail_perf_pair_net10_$Stamp"
Assert-DisposableName $cppDatabase 'C++ database'
Assert-DisposableName $net10Database '.NET database'
if (Test-Path -LiteralPath $fullOutputRoot) { throw "Refusing to overwrite fixture root: $fullOutputRoot" }

$backupFiles = @(Get-BackupLogicalFiles $BackupPath)
$sqlDataDirectory = [IO.Path]::GetFullPath((Join-Path $fullOutputRoot 'sql'))
$cppRoot = Join-Path $fullOutputRoot 'cpp'
$net10Root = Join-Path $fullOutputRoot 'net10'
New-Item -ItemType Directory -Force -Path $sqlDataDirectory, $cppRoot, $net10Root | Out-Null

try {
    foreach ($database in @($cppDatabase, $net10Database)) {
        $safeDatabase = $database.Replace("'", "''")
        $mdf = Join-Path $sqlDataDirectory "$database.mdf"
        $ldf = Join-Path $sqlDataDirectory "${database}_log.ldf"
        $dataLogical = ($backupFiles | Where-Object Type -eq 'D').LogicalName.Replace("'", "''")
        $logLogical = ($backupFiles | Where-Object Type -eq 'L').LogicalName.Replace("'", "''")
        $query = @"
RESTORE DATABASE [$safeDatabase]
FROM DISK = N'$($BackupPath.Replace("'", "''"))'
WITH MOVE N'$dataLogical' TO N'$($mdf.Replace("'", "''"))',
     MOVE N'$logLogical' TO N'$($ldf.Replace("'", "''"))',
     RECOVERY, REPLACE, STATS = 5;
"@
        Invoke-Sql $query
    }

    $cppVersionBefore = Get-DatabaseVersion $cppDatabase
    $net10VersionBefore = Get-DatabaseVersion $net10Database
    if ($cppVersionBefore -ne 5708 -or $net10VersionBefore -ne 5708) {
        throw "Paired provisioning requires a legacy 5708 backup; restored versions were C++=$cppVersionBefore and Net10=$net10VersionBefore."
    }

    Invoke-SqlFile -Database $net10Database -Path $UpgradeScriptPath
    $cppVersionAfter = Get-DatabaseVersion $cppDatabase
    $net10VersionAfter = Get-DatabaseVersion $net10Database
    if ($cppVersionAfter -ne 5708 -or $net10VersionAfter -ne 6000) {
        throw "Paired schema preparation failed; expected C++=5708 and Net10=6000, got C++=$cppVersionAfter and Net10=$net10VersionAfter."
    }

    foreach ($database in @($cppDatabase, $net10Database)) {
        $side = if ($database -eq $cppDatabase) { 'cpp' } else { 'net10' }
        $dataRoot = Join-Path $fullOutputRoot "$side\Data"
        $safeDatabase = $database.Replace("'", "''")
        $safeDataRoot = $dataRoot.Replace("'", "''")
        Invoke-Sql @"
USE [$safeDatabase];
UPDATE hm_messages
SET messagefilename = N'$safeDataRoot' + SUBSTRING(messagefilename, CHARINDEX(N'\Data\', messagefilename) + 5, 4000)
WHERE CHARINDEX(N'\Data\', messagefilename) > 0;
"@
    }

    Copy-Item -LiteralPath $SourceDataRoot -Destination (Join-Path $cppRoot 'Data') -Recurse -Force
    Copy-Item -LiteralPath $SourceDataRoot -Destination (Join-Path $net10Root 'Data') -Recurse -Force

    $cppBin = Join-Path $cppRoot 'Bin'
    $net10Bin = Join-Path $net10Root 'Bin'
    New-Item -ItemType Directory -Force -Path $cppBin | Out-Null
    New-Item -ItemType Directory -Force -Path $net10Bin | Out-Null
    Copy-Item -Path (Join-Path $LegacyBinPath '*') -Destination $cppBin -Recurse -Force
    Copy-Item -Path (Join-Path $Net10BinPath '*') -Destination $net10Bin -Recurse -Force
    $languageFile = Join-Path $cppBin 'Languages\english.ini'
    if (-not (Test-Path -LiteralPath $languageFile -PathType Leaf)) {
        $translation = Join-Path $repoRoot 'hmailserver\source\Translations\english.ini'
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $languageFile) | Out-Null
        Copy-Item -LiteralPath $translation -Destination $languageFile -Force
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $cppRoot 'Database'), (Join-Path $cppRoot 'Logs'), (Join-Path $cppRoot 'Temp'), (Join-Path $cppRoot 'Events') | Out-Null

    $cppIni = @"
[Directories]
ProgramFolder=$cppBin
DatabaseFolder=$(Join-Path $cppRoot 'Database')
DataFolder=$(Join-Path $cppRoot 'Data')
LogFolder=$(Join-Path $cppRoot 'Logs')
TempFolder=$(Join-Path $cppRoot 'Temp')
EventFolder=$(Join-Path $cppRoot 'Events')

[Database]
Type=MSSQL
Server=localhost
Database=$cppDatabase
Username=
Password=
PasswordEncryption=0
Provider=MSOLEDBSQL
Port=0
Internal=0
NumberOfConnections=5
ConnectionAttempts=3
ConnectionAttemptsDelay=1

[GUILanguages]
ValidLanguages=english

[Security]
AdministratorPassword=99a5668ead23a01d65f447e37206ac97
"@
    Set-Content -LiteralPath (Join-Path $cppBin 'hMailServer.ini') -Value $cppIni -Encoding ASCII

    $net10Ini = @"
Server=localhost
Database=$net10Database
DataFolder=$(Join-Path $net10Root 'Data')
"@
    Set-Content -LiteralPath (Join-Path $net10Root 'hmailServer.ini') -Value $net10Ini -Encoding ASCII

    $cppDataManifest = Get-LiveBenchmarkDirectoryFingerprint (Join-Path $cppRoot 'Data')
    $net10DataManifest = Get-LiveBenchmarkDirectoryFingerprint (Join-Path $net10Root 'Data')
    if ($cppDataManifest.sha256 -ne $net10DataManifest.sha256 -or $cppDataManifest.fileCount -ne $net10DataManifest.fileCount) {
        throw "Paired Data copies are not byte-for-byte equivalent."
    }
    $cppMessageFingerprint = Get-LiveBenchmarkMessageFingerprint $cppDatabase
    $net10MessageFingerprint = Get-LiveBenchmarkMessageFingerprint $net10Database
    if ($cppMessageFingerprint.sha256 -ne $net10MessageFingerprint.sha256 -or $cppMessageFingerprint.rowCount -ne $net10MessageFingerprint.rowCount) {
        throw "Paired SQL message projections differ after the Net10 schema migration."
    }

    $fixtureReport = [pscustomobject]@{
        schema = 'paired-benchmark-fixture-v2'
        status = 'PASS'
        generatedUtc = [DateTime]::UtcNow.ToString('o')
        backupPath = [IO.Path]::GetFullPath($BackupPath)
        backupSha256 = (Get-FileHash -LiteralPath $BackupPath -Algorithm SHA256).Hash
        upgradeScriptPath = $UpgradeScriptPath
        upgradeScriptSha256 = (Get-FileHash -LiteralPath $UpgradeScriptPath -Algorithm SHA256).Hash
        outputRoot = $fullOutputRoot
        cppDatabase = $cppDatabase
        net10Database = $net10Database
        cppDatabaseVersion = $cppVersionAfter
        net10DatabaseVersion = $net10VersionAfter
        cppDataRoot = [IO.Path]::GetFullPath((Join-Path $cppRoot 'Data'))
        net10DataRoot = [IO.Path]::GetFullPath((Join-Path $net10Root 'Data'))
        cppExecutable = [IO.Path]::GetFullPath((Join-Path $cppBin 'hMailServer.exe'))
        cppExecutableSha256 = (Get-FileHash -LiteralPath (Join-Path $cppBin 'hMailServer.exe') -Algorithm SHA256).Hash
        net10Executable = [IO.Path]::GetFullPath((Join-Path $net10Bin 'hMailServer.exe'))
        net10ExecutableSha256 = (Get-FileHash -LiteralPath (Join-Path $net10Bin 'hMailServer.exe') -Algorithm SHA256).Hash
        dataParity = [pscustomobject]@{
            fileCount = $cppDataManifest.fileCount
            bytes = $cppDataManifest.bytes
            sha256 = $cppDataManifest.sha256
            exact = $true
        }
        messageParity = [pscustomobject]@{
            rowCount = $cppMessageFingerprint.rowCount
            sha256 = $cppMessageFingerprint.sha256
            exact = $true
        }
    }
    $fixtureReportPath = Join-Path $fullOutputRoot 'paired-fixture.json'
    $fixtureReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $fixtureReportPath -Encoding UTF8
    $fixtureReport | ConvertTo-Json -Depth 6
}
catch {
    foreach ($database in @($cppDatabase, $net10Database)) {
        Invoke-Sql "IF DB_ID(N'$($database.Replace("'", "''"))') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;"
    }
    throw
}
