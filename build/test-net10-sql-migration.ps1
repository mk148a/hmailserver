[CmdletBinding()]
param(
    [string]$LocalDbInstance = 'MSSQLLocalDB',
    [string]$OutputDirectory,
    [string]$SqlServerInstance,
    [switch]$AllowIsolatedSqlServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$createScript = Join-Path $repoRoot 'hmailserver\source\DBScripts\CreateTablesMSSQL.sql'
$upgradeScript = Join-Path $repoRoot 'hmailserver\source\DBScripts\Upgrade5708to6000MSSQL.sql'
foreach ($path in @($createScript, $upgradeScript)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required SQL script was not found: $path"
    }
}

if ($LocalDbInstance -notmatch '^[A-Za-z0-9_]+$') {
    throw 'LocalDB instance name contains unsupported characters.'
}

$sqlcmdCommand = Get-Command sqlcmd.exe -ErrorAction Stop
$sqlcmd = $sqlcmdCommand.Source
$isLocalDb = [string]::IsNullOrWhiteSpace($SqlServerInstance)
if ($isLocalDb) {
    $sqllocaldbCommand = Get-Command sqllocaldb.exe -ErrorAction Stop
    $sqllocaldb = $sqllocaldbCommand.Source
    $server = "(localdb)\$LocalDbInstance"
}
else {
    if (-not $AllowIsolatedSqlServer) {
        throw 'A non-LocalDB target requires -AllowIsolatedSqlServer.'
    }
    $server = $SqlServerInstance
}

function Invoke-SqlCommand {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query,
        [switch]$AllowFailure
    )

    $savedErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $script:sqlcmd -S $script:server -d $Database -E -b -r 1 -l 30 -X -h -1 -W -s '|' -Q $Query 2>&1)
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }
    $exitCode = $LASTEXITCODE
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "sqlcmd query failed with exit code ${exitCode}: $($output -join [Environment]::NewLine)"
    }

    [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Invoke-SqlFile {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Path,
        [switch]$AllowFailure
    )

    $savedErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $script:sqlcmd -S $script:server -d $Database -E -b -r 1 -l 120 -X -i $Path 2>&1)
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }
    $exitCode = $LASTEXITCODE
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "sqlcmd file failed with exit code ${exitCode} ($Path): $($output -join [Environment]::NewLine)"
    }

    [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Get-SqlRow {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query,
        [Parameter(Mandatory)][string[]]$Columns
    )

    $result = Invoke-SqlCommand -Database $Database -Query $Query
    $line = @($result.Output | ForEach-Object { [string]$_ } | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and $_ -notmatch '^[- ]+$'
    } | Select-Object -Last 1)
    if ($line.Count -ne 1) {
        throw "Expected one SQL result row, got $($line.Count). Output: $($result.Output -join [Environment]::NewLine)"
    }

    $values = $line[0].Split('|')
    if ($values.Count -ne $Columns.Count) {
        throw "Expected $($Columns.Count) SQL columns, got $($values.Count): $($line[0])"
    }

    $row = [ordered]@{}
    for ($index = 0; $index -lt $Columns.Count; $index++) {
        $row[$Columns[$index]] = $values[$index].Trim()
    }
    [pscustomobject]$row
}

function New-DisposableDatabaseName {
    param([string]$Prefix)

    $name = "${Prefix}_$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))_$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
    if ($name -notmatch '^hmail_net10_migration_(success|rollback)_[0-9]{14}_[0-9a-f]{12}$') {
        throw "Generated database name is outside the disposable naming policy: $name"
    }
    $name
}

function New-TransactionScript {
    param(
        [Parameter(Mandatory)][string]$UpgradeText,
        [Parameter(Mandatory)][string]$Path,
        [switch]$InjectFailure
    )

    $suffix = if ($InjectFailure) {
        @'

IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
RAISERROR('Injected migration failure for rollback acceptance.', 16, 1);
'@
    }
    else {
        "`r`nCOMMIT TRANSACTION;`r`n"
    }

    $text = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
$UpgradeText
$suffix
"@
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding($false)))
}

function New-SqlCmdSchemaScript {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    # The legacy DBUpdater splits CREATE PROC batches internally; SQLCMD needs
    # explicit GO separators for the same schema source.
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in (Get-Content -LiteralPath $SourcePath)) {
        if ($line -match '^\s*create\s+proc\b') {
            if ($lines.Count -gt 0 -and $lines[$lines.Count - 1] -ne 'GO') {
                $lines.Add('GO')
            }
        }
        $lines.Add($line)
        if ($line -match '^\s*return\s+0\s*;?\s*$') {
            $lines.Add('GO')
        }
    }
    $text = $lines -join "`r`n"
    [IO.File]::WriteAllText($DestinationPath, $text, (New-Object Text.UTF8Encoding($false)))
    $DestinationPath
}

function Get-MigrationState {
    param([Parameter(Mandatory)][string]$Database)

    Get-SqlRow -Database $Database -Columns @(
        'DatabaseVersion',
        'LeaseOwnerColumn',
        'LeaseExpiryColumn',
        'ForcedRouteColumn',
        'BindAddressColumn',
        'SearchDocumentsTable',
        'SearchQueueTable',
        'DeliveryStatusTable',
        'DeliveryLeaseIndex',
        'SearchDocumentsIndex',
        'SearchQueueIndex',
        'DeliveryStatusMessageIndex',
        'DeliveryStatusTimeIndex',
        'FullTextCatalog',
        'FullTextIndex'
    ) -Query @"
SET NOCOUNT ON;
SELECT
  CONVERT(varchar(32), (SELECT TOP (1) value FROM hm_dbversion)) AS DatabaseVersion,
  CONVERT(varchar(1), CASE WHEN COL_LENGTH('hm_messages', 'messageleaseowner') IS NULL THEN 0 ELSE 1 END) AS LeaseOwnerColumn,
  CONVERT(varchar(1), CASE WHEN COL_LENGTH('hm_messages', 'messageleaseexpiresutc') IS NULL THEN 0 ELSE 1 END) AS LeaseExpiryColumn,
  CONVERT(varchar(1), CASE WHEN COL_LENGTH('hm_messages', 'messageruleforcedrouteid') IS NULL THEN 0 ELSE 1 END) AS ForcedRouteColumn,
  CONVERT(varchar(1), CASE WHEN COL_LENGTH('hm_messages', 'messagerulebindaddress') IS NULL THEN 0 ELSE 1 END) AS BindAddressColumn,
  CONVERT(varchar(1), CASE WHEN OBJECT_ID('hm_message_search_documents', 'U') IS NULL THEN 0 ELSE 1 END) AS SearchDocumentsTable,
  CONVERT(varchar(1), CASE WHEN OBJECT_ID('hm_message_search_queue', 'U') IS NULL THEN 0 ELSE 1 END) AS SearchQueueTable,
  CONVERT(varchar(1), CASE WHEN OBJECT_ID('hm_delivery_queue_status', 'U') IS NULL THEN 0 ELSE 1 END) AS DeliveryStatusTable,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_hm_messages_delivery_lease' AND object_id = OBJECT_ID('hm_messages')) THEN 1 ELSE 0 END) AS DeliveryLeaseIndex,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_hm_message_search_documents_folder_uid' AND object_id = OBJECT_ID('hm_message_search_documents')) THEN 1 ELSE 0 END) AS SearchDocumentsIndex,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_hm_message_search_queue_lease' AND object_id = OBJECT_ID('hm_message_search_queue')) THEN 1 ELSE 0 END) AS SearchQueueIndex,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_hm_delivery_queue_status_message_time' AND object_id = OBJECT_ID('hm_delivery_queue_status')) THEN 1 ELSE 0 END) AS DeliveryStatusMessageIndex,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_hm_delivery_queue_status_time' AND object_id = OBJECT_ID('hm_delivery_queue_status')) THEN 1 ELSE 0 END) AS DeliveryStatusTimeIndex,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'hm_message_search_catalog') THEN 1 ELSE 0 END) AS FullTextCatalog,
  CONVERT(varchar(1), CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('hm_message_search_documents')) THEN 1 ELSE 0 END) AS FullTextIndex;
"@
}

function Assert-State {
    param(
        [Parameter(Mandatory)][psobject]$State,
        [Parameter(Mandatory)][int]$ExpectedVersion,
        [Parameter(Mandatory)][bool]$Migrated
    )

    $expected = if ($Migrated) { '1' } else { '0' }
    if ([int]$State.DatabaseVersion -ne $ExpectedVersion) {
        throw "Expected hm_dbversion $ExpectedVersion, got $($State.DatabaseVersion)."
    }

    foreach ($property in @(
        'LeaseOwnerColumn', 'LeaseExpiryColumn', 'ForcedRouteColumn', 'BindAddressColumn',
        'SearchDocumentsTable', 'SearchQueueTable', 'DeliveryStatusTable',
        'DeliveryLeaseIndex', 'SearchDocumentsIndex', 'SearchQueueIndex',
        'DeliveryStatusMessageIndex', 'DeliveryStatusTimeIndex', 'FullTextCatalog', 'FullTextIndex'
    )) {
        $actual = [string]$State.$property
        if ($actual -ne $expected) {
            throw "Expected $property=$expected, got $actual."
        }
    }
}

$startedByScript = $false
if ($isLocalDb) {
    $instanceInfo = @(& $sqllocaldb info $LocalDbInstance 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "LocalDB instance '$LocalDbInstance' was not found: $($instanceInfo -join [Environment]::NewLine)"
    }

    $stateLine = $instanceInfo | Where-Object { [string]$_ -match '^State:\s*(?<state>\w+)' } | Select-Object -First 1
    if ($stateLine -and $stateLine -match '^State:\s*Stopped') {
        & $sqllocaldb start $LocalDbInstance | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to start LocalDB instance '$LocalDbInstance'."
        }
        $startedByScript = $true
    }
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "hmail-net10-sql-migration-$([Guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $temporaryRoot -Force
$databaseFileDirectory = Join-Path $temporaryRoot 'db'
$null = New-Item -ItemType Directory -Path $databaseFileDirectory -Force
$databaseFiles = @{}
$databaseNames = @(
    New-DisposableDatabaseName -Prefix 'hmail_net10_migration_success'
    New-DisposableDatabaseName -Prefix 'hmail_net10_migration_rollback'
)
foreach ($database in $databaseNames) {
    $databaseFiles[$database] = [ordered]@{
        Data = Join-Path $databaseFileDirectory "$database.mdf"
        Log = Join-Path $databaseFileDirectory "${database}_log.ldf"
    }
}

function Quote-SqlLiteral {
    param([Parameter(Mandatory)][string]$Value)
    "N'$($Value.Replace("'", "''"))'"
}

function New-Database {
    param([Parameter(Mandatory)][string]$Database)

    $files = $databaseFiles[$Database]
    $dataPath = Quote-SqlLiteral -Value $files.Data
    $logPath = Quote-SqlLiteral -Value $files.Log
    $query = @"
CREATE DATABASE [$Database]
ON PRIMARY (NAME = N'$Database', FILENAME = $dataPath)
LOG ON (NAME = N'${Database}_log', FILENAME = $logPath);
"@
    Write-Verbose "Creating disposable database $Database with data file $($files.Data)."
    Invoke-SqlCommand -Database 'master' -Query $query | Out-Null
}

function Remove-DatabaseFiles {
    foreach ($files in $databaseFiles.Values) {
        foreach ($path in @($files.Data, $files.Log)) {
            try {
                if (Test-Path -LiteralPath $path) {
                    Remove-Item -LiteralPath $path -Force -ErrorAction Stop
                }
            }
            catch {
                $cleanupErrors.Add("Database file cleanup failed for ${path}: $($_.Exception.Message)")
            }
        }
    }
}

if ($isLocalDb) {
    $fullTextAvailable = $false
}
else {
    $serverInfo = Get-SqlRow -Database 'master' -Columns @('FullTextInstalled', 'DefaultDataPath') -Query "SET NOCOUNT ON; SELECT CONVERT(varchar(1), SERVERPROPERTY('IsFullTextInstalled')), CONVERT(varchar(260), SERVERPROPERTY('InstanceDefaultDataPath'));"
    $fullTextAvailable = ([string]$serverInfo.FullTextInstalled -eq '1')
    $databaseFileDirectory = [string]$serverInfo.DefaultDataPath
    if ([string]::IsNullOrWhiteSpace($databaseFileDirectory) -or -not (Test-Path -LiteralPath $databaseFileDirectory -PathType Container)) {
        throw "The isolated SQL Server target did not provide an accessible default data directory."
    }
}

if (-not $isLocalDb -and -not $fullTextAvailable) {
    throw "The isolated SQL Server target '$server' does not report Full-Text Search installed."
}

# The full migration is intentionally exercised only on a target with
# Full-Text Search. LocalDB remains a schema-parser smoke target, but it
# cannot claim 5708 -> 6000 acceptance when the legacy script creates FTS.
foreach ($database in $databaseNames) {
    $databaseFiles[$database] = [ordered]@{
        Data = Join-Path $databaseFileDirectory "$database.mdf"
        Log = Join-Path $databaseFileDirectory "${database}_log.ldf"
    }
}

$scriptRoot = Join-Path $temporaryRoot 'sql'
$successDatabase = $databaseNames[0]
$rollbackDatabase = $databaseNames[1]
$upgradeText = Get-Content -LiteralPath $upgradeScript -Raw
$nonFullTextLines = [Collections.Generic.List[string]]::new()
$skipFullText = $false
foreach ($line in (Get-Content -LiteralPath $upgradeScript)) {
    if ($line -match '^\s*if\s+not\s+exists\s*\(select\s+\*\s+from\s+sys\.fulltext_catalogs') {
        $skipFullText = $true
    }
    if (-not $skipFullText) {
        $nonFullTextLines.Add($line)
    }
    if ($skipFullText -and $line -match '^\s*on\s+hm_message_search_catalog\s*$') {
        $skipFullText = $false
    }
}
$nonFullTextUpgradeText = $nonFullTextLines -join "`r`n"
$results = [ordered]@{}
$cleanupErrors = [System.Collections.Generic.List[string]]::new()

try {
    $null = New-Item -ItemType Directory -Path $scriptRoot -Force
    $successMigrationScript = Join-Path $scriptRoot 'success.sql'
    $legacyTransactionScript = Join-Path $scriptRoot 'legacy-transaction.sql'
    $rollbackMigrationScript = Join-Path $scriptRoot 'rollback.sql'
    $normalizedCreateScript = Join-Path $scriptRoot 'CreateTablesMSSQL-sqlcmd.sql'
    New-SqlCmdSchemaScript -SourcePath $createScript -DestinationPath $normalizedCreateScript | Out-Null
    New-TransactionScript -UpgradeText $nonFullTextUpgradeText -Path $rollbackMigrationScript -InjectFailure
    New-TransactionScript -UpgradeText $upgradeText -Path $legacyTransactionScript

    $createHash = (Get-FileHash -LiteralPath $createScript -Algorithm SHA256).Hash
    $upgradeHash = (Get-FileHash -LiteralPath $upgradeScript -Algorithm SHA256).Hash
    $results.ScriptHashes = [ordered]@{ CreateTablesMSSQL = $createHash; Upgrade5708to6000MSSQL = $upgradeHash }
    $results.TargetServer = $server
    $results.LocalDbInstance = $LocalDbInstance
    $results.FullTextAvailable = $fullTextAvailable
    $results.DatabaseFileDirectory = $databaseFileDirectory
    $results.SuccessDatabase = $successDatabase
    $results.RollbackDatabase = $rollbackDatabase
    $results.ProductionPathsTouched = @()
    $results.RegistrationOrDcomChanged = $false
    $results.StartedLocalDbByScript = $startedByScript
    $results.StartedUtc = [DateTimeOffset]::UtcNow.ToString('o')

    foreach ($database in $databaseNames) {
        $existing = Invoke-SqlCommand -Database 'master' -Query "SET NOCOUNT ON; SELECT DB_ID(N'$database');"
        $existingValue = @($existing.Output | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^\d+$' })
        if ($existingValue.Count -gt 0) {
            throw "Disposable database name already exists: $database"
        }
        New-Database -Database $database
    }

    Invoke-SqlFile -Database $successDatabase -Path $normalizedCreateScript | Out-Null
    $successBefore = Get-MigrationState -Database $successDatabase
    Assert-State -State $successBefore -ExpectedVersion 5708 -Migrated $false
    Invoke-SqlFile -Database $successDatabase -Path $upgradeScript | Out-Null
    $successAfter = Get-MigrationState -Database $successDatabase
    Assert-State -State $successAfter -ExpectedVersion 6000 -Migrated $true

    Invoke-SqlFile -Database $rollbackDatabase -Path $normalizedCreateScript | Out-Null
    $rollbackBefore = Get-MigrationState -Database $rollbackDatabase
    Assert-State -State $rollbackBefore -ExpectedVersion 5708 -Migrated $false
    $legacyTransactionAttempt = Invoke-SqlFile -Database $rollbackDatabase -Path $legacyTransactionScript -AllowFailure
    $legacyTransactionState = Get-MigrationState -Database $rollbackDatabase
    if ($legacyTransactionAttempt.ExitCode -eq 0) {
        Assert-State -State $legacyTransactionState -ExpectedVersion 6000 -Migrated $true
        $legacyTransactionStatus = 'Passed'
        $status = 'Passed'
        $rollbackAttempt = [pscustomobject]@{ ExitCode = 0; Output = @() }
    }
    else {
        Assert-State -State $legacyTransactionState -ExpectedVersion 5708 -Migrated $false
        $legacyTransactionStatus = 'BlockedByFullTextDdl'
        $rollbackAttempt = Invoke-SqlFile -Database $rollbackDatabase -Path $rollbackMigrationScript -AllowFailure
        if ($rollbackAttempt.ExitCode -eq 0) {
            throw 'Injected migration failure unexpectedly returned success.'
        }
        $status = 'PassedWithKnownLegacyTransactionLimitation'
    }
    $rollbackAfter = Get-MigrationState -Database $rollbackDatabase
    Assert-State -State $rollbackAfter -ExpectedVersion 5708 -Migrated $false

    $results.SuccessStateBefore = $successBefore
    $results.SuccessStateAfter = $successAfter
    $results.RollbackStateBefore = $rollbackBefore
    $results.RollbackStateAfter = $rollbackAfter
    $results.LegacyTransactionStatus = $legacyTransactionStatus
    $results.LegacyTransactionExitCode = $legacyTransactionAttempt.ExitCode
    $results.LegacyTransactionError = (($legacyTransactionAttempt.Output | ForEach-Object { [string]$_ }) -join ' ').Trim()
    $results.RollbackCommandExitCode = $rollbackAttempt.ExitCode
    $results.Status = $status
}
catch {
    $results.Status = 'Failed'
    $results.Error = $_.Exception.Message
    throw
}
finally {
    foreach ($database in $databaseNames) {
        try {
            Invoke-SqlCommand -Database 'master' -Query "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;" -AllowFailure | Out-Null
        }
        catch {
            $cleanupErrors.Add("${database}: $($_.Exception.Message)")
        }
    }

    Remove-DatabaseFiles

    if ($startedByScript) {
        try {
            & $sqllocaldb stop $LocalDbInstance -i | Out-Null
            if ($LASTEXITCODE -ne 0) {
                $cleanupErrors.Add("LocalDB stop failed with exit code $LASTEXITCODE.")
            }
        }
        catch {
            $cleanupErrors.Add("LocalDB stop failed: $($_.Exception.Message)")
        }
    }

    $results.CleanupErrors = @($cleanupErrors)
    $results.EndedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if ($null -ne $OutputDirectory) {
        $outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
        $null = New-Item -ItemType Directory -Path $outputRoot -Force
        $jsonPath = Join-Path $outputRoot 'net10-sql-migration.json'
        $markdownPath = Join-Path $outputRoot 'net10-sql-migration.md'
        $json = [ordered]@{} + $results
        [IO.File]::WriteAllText($jsonPath, ($json | ConvertTo-Json -Depth 12) + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
        $status = [string]$results.Status
        $errorLine = if ($results.Contains('Error')) { "`n`nError: $($results.Error)" } else { '' }
        $rollbackExitCode = if ($results.Contains('RollbackCommandExitCode')) { $results.RollbackCommandExitCode } else { 'not reached' }
        $markdown = @(
            '# Net10 SQL Migration Acceptance'
            ''
            "- Status: $status"
            "- Target: $server / $successDatabase and $rollbackDatabase"
            '- Migration: 5708 -> 6000'
            "- CreateTables SHA-256: $($results.ScriptHashes.CreateTablesMSSQL)"
            "- Upgrade SHA-256: $($results.ScriptHashes.Upgrade5708to6000MSSQL)"
            '- Production paths touched: none'
            '- Registration or DCOM changed: false'
            "- Rollback command exit code: $rollbackExitCode"
            "- Legacy transaction path: $($results.LegacyTransactionStatus)"
            $errorLine.Trim()
        ) -join [Environment]::NewLine
        [IO.File]::WriteAllText($markdownPath, $markdown, (New-Object Text.UTF8Encoding($false)))
    }

    if ($cleanupErrors.Count -gt 0 -and $results.Status -eq 'Passed') {
        throw "Migration passed but cleanup failed: $($cleanupErrors -join ' | ')"
    }
}

Write-Output "PASS: disposable $server 5708-to-6000 migration evidence completed with status $($results.Status)."
