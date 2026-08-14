param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [Parameter(Mandatory = $true)]
    [string]$SourceDataRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [string]$Stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss')
)

$ErrorActionPreference = 'Stop'

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

if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) { throw "Backup does not exist: $BackupPath" }
if (-not (Test-Path -LiteralPath $SourceDataRoot -PathType Container)) { throw "Source Data root does not exist: $SourceDataRoot" }
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
    New-Item -ItemType Directory -Force -Path $cppBin | Out-Null
    $sourceBin = Join-Path (Split-Path -Parent $SourceDataRoot) 'Bin'
    if (-not (Test-Path -LiteralPath $sourceBin -PathType Container)) {
        throw "C++ Bin source is missing beside Data root: $sourceBin"
    }
    Copy-Item -Path (Join-Path $sourceBin '*') -Destination $cppBin -Recurse -Force

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

    [pscustomobject]@{
        generatedUtc = [DateTime]::UtcNow.ToString('o')
        backupPath = [IO.Path]::GetFullPath($BackupPath)
        outputRoot = $fullOutputRoot
        cppDatabase = $cppDatabase
        net10Database = $net10Database
        cppDataRoot = [IO.Path]::GetFullPath((Join-Path $cppRoot 'Data'))
        net10DataRoot = [IO.Path]::GetFullPath((Join-Path $net10Root 'Data'))
        cppExecutable = [IO.Path]::GetFullPath((Join-Path $cppBin 'hMailServer.exe'))
        sourceDataSha256 = (Get-FileHash -LiteralPath $BackupPath -Algorithm SHA256).Hash
    } | ConvertTo-Json -Depth 4
}
catch {
    foreach ($database in @($cppDatabase, $net10Database)) {
        Invoke-Sql "IF DB_ID(N'$($database.Replace("'", "''"))') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;"
    }
    throw
}
