param(
    [Parameter(Mandatory = $true)]
    [string]$CppDatabase,
    [Parameter(Mandatory = $true)]
    [string]$Net10Database,
    [Parameter(Mandatory = $true)]
    [string]$CppDataRoot,
    [Parameter(Mandatory = $true)]
    [string]$Net10DataRoot,
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

function Assert-DisposableTarget {
    param(
        [string]$Database,
        [string]$DataRoot
    )

    if ($Database -notmatch '^hmail_perf_[a-z0-9_]+$') {
        throw "Refusing non-disposable database name: $Database"
    }

    $fullRoot = [IO.Path]::GetFullPath($DataRoot)
    if ($fullRoot -match '(?i)hmailserver57|HmailDb_Test5700') {
        throw "Refusing possible production Data root: $fullRoot"
    }
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Data root does not exist: $fullRoot"
    }
}

function Get-TableCounts {
    param([string]$Database)

    $query = @'
SET NOCOUNT ON;
SELECT s.name + '.' + t.name, SUM(p.rows)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY s.name, t.name
ORDER BY 1;
'@
    $lines = @(sqlcmd -S localhost -E -d $Database -W -s '|' -h-1 -Q $query)
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd could not read disposable database '$Database' (exit code $LASTEXITCODE)."
    }

    foreach ($line in $lines) {
        if ($line -notmatch '\|') {
            continue
        }

        $parts = $line.Trim().Split('|')
        [pscustomobject]@{
            table = $parts[0].Trim()
            rows = [int64]$parts[1].Trim()
        }
    }
}

function Get-DataManifest {
    param([string]$Root)

    $fullRoot = [IO.Path]::GetFullPath($Root)
    $manifest = @{}
    Get-ChildItem -LiteralPath $fullRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($fullRoot.Length).TrimStart('\')
        $manifest[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    return $manifest
}

function Get-SqlFixtureSnapshot {
    param(
        [string]$Database,
        [string]$DataRoot
    )

    try {
        $dataRootSql = ([IO.Path]::GetFullPath($DataRoot)).Replace("'", "''")
        $query = @"
SET NOCOUNT ON;
SELECT
    (SELECT COUNT_BIG(*) FROM hm_domains WHERE domainname = N'perf.test' AND domainactive <> 0),
    (SELECT COUNT_BIG(*) FROM hm_accounts WHERE accountaddress = N'test@perf.test' AND accountactive <> 0),
    (SELECT COUNT_BIG(*) FROM hm_imapfolders WHERE folderaccountid = 1 AND folderparentid = -1 AND LOWER(foldername) = N'inbox'),
    (SELECT COUNT_BIG(*) FROM hm_tcpipports WHERE
        (portprotocol = 1 AND portnumber = 2525 AND portaddress1 = 2130706433) OR
        (portprotocol = 3 AND portnumber = 25110 AND portaddress1 = 2130706433) OR
        (portprotocol = 5 AND portnumber = 1143 AND portaddress1 = 2130706433)),
    (SELECT COUNT_BIG(*) FROM hm_messages WHERE messagefilename IS NOT NULL AND LEFT(messagefilename, LEN(N'$dataRootSql')) = N'$dataRootSql'),
    (SELECT COUNT_BIG(*) FROM hm_messages WHERE messagefilename IS NULL OR LEFT(messagefilename, LEN(N'$dataRootSql')) <> N'$dataRootSql'),
    CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')),
    (SELECT COUNT_BIG(*) FROM sys.fulltext_catalogs WHERE name = N'hm_message_search_catalog'),
    CASE WHEN OBJECT_ID(N'dbo.hm_message_search_documents', N'U') IS NOT NULL THEN 1 ELSE 0 END,
    (SELECT COUNT_BIG(*) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.hm_message_search_documents'));
"@
        $lines = @(sqlcmd -S localhost -E -d $Database -W -s '|' -h-1 -b -Q $query)
        if ($LASTEXITCODE -ne 0 -or $lines.Count -ne 1) {
            throw "sqlcmd returned no single fixture snapshot row (exit code $LASTEXITCODE)."
        }

        $parts = $lines[0].Trim().Split('|')
        if ($parts.Count -ne 10) {
            throw "sqlcmd fixture snapshot returned $($parts.Count) fields instead of 10."
        }

        $domainMatches = [int64]$parts[0].Trim()
        $accountMatches = [int64]$parts[1].Trim()
        $inboxMatches = [int64]$parts[2].Trim()
        $matchingLoopbackPorts = [int64]$parts[3].Trim()
        $messageFilesWithinDataRoot = [int64]$parts[4].Trim()
        $messageFilesOutsideDataRoot = [int64]$parts[5].Trim()
        $fullTextInstalled = [int64]$parts[6].Trim()
        $fullTextCatalogs = [int64]$parts[7].Trim()
        $searchDocumentTable = [int64]$parts[8].Trim()
        $fullTextIndexes = [int64]$parts[9].Trim()
        $fullTextReady = $fullTextInstalled -eq 1 -and $fullTextCatalogs -ge 1 -and $searchDocumentTable -eq 1 -and $fullTextIndexes -ge 1

        [pscustomobject]@{
            available = $true
            dataRoot = [IO.Path]::GetFullPath($DataRoot)
            domainMatches = $domainMatches
            accountMatches = $accountMatches
            inboxMatches = $inboxMatches
            matchingLoopbackPorts = $matchingLoopbackPorts
            messageFilesWithinDataRoot = $messageFilesWithinDataRoot
            messageFilesOutsideDataRoot = $messageFilesOutsideDataRoot
            fullTextInstalled = $fullTextInstalled
            fullTextCatalogs = $fullTextCatalogs
            searchDocumentTable = $searchDocumentTable
            fullTextIndexes = $fullTextIndexes
            fixtureValid = $domainMatches -eq 1 -and $accountMatches -eq 1 -and $inboxMatches -eq 1 -and $matchingLoopbackPorts -eq 3 -and $messageFilesOutsideDataRoot -eq 0
            fullTextReady = $fullTextReady
            error = $null
        }
    }
    catch {
        [pscustomobject]@{
            available = $false
            dataRoot = [IO.Path]::GetFullPath($DataRoot)
            domainMatches = $null
            accountMatches = $null
            inboxMatches = $null
            matchingLoopbackPorts = $null
            messageFilesWithinDataRoot = $null
            messageFilesOutsideDataRoot = $null
            fullTextInstalled = $null
            fullTextCatalogs = $null
            searchDocumentTable = $null
            fullTextIndexes = $null
            fixtureValid = $false
            fullTextReady = $false
            error = $_.Exception.Message
        }
    }
}

Assert-DisposableTarget -Database $CppDatabase -DataRoot $CppDataRoot
Assert-DisposableTarget -Database $Net10Database -DataRoot $Net10DataRoot
if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
    throw "SQL backup evidence file does not exist: $BackupPath"
}

$cppCounts = @(Get-TableCounts -Database $CppDatabase)
$net10Counts = @(Get-TableCounts -Database $Net10Database)
$cppFixture = Get-SqlFixtureSnapshot -Database $CppDatabase -DataRoot $CppDataRoot
$net10Fixture = Get-SqlFixtureSnapshot -Database $Net10Database -DataRoot $Net10DataRoot
$net10ByTable = @{}
$net10Counts | ForEach-Object { $net10ByTable[$_.table] = $_.rows }
$rowMismatches = @(
    $cppCounts | Where-Object {
        -not $net10ByTable.ContainsKey($_.table) -or $net10ByTable[$_.table] -ne $_.rows
    }
)
$rowMismatches += @(
    $net10Counts | Where-Object {
        -not ($cppCounts.table -contains $_.table)
    }
)

$cppFiles = Get-DataManifest -Root $CppDataRoot
$net10Files = Get-DataManifest -Root $Net10DataRoot
$allPaths = @($cppFiles.Keys + $net10Files.Keys | Sort-Object -Unique)
$pathMismatches = @(
    $allPaths | Where-Object {
        -not $cppFiles.ContainsKey($_) -or
        -not $net10Files.ContainsKey($_) -or
        $cppFiles[$_] -ne $net10Files[$_]
    }
)
$backupHash = (Get-FileHash -LiteralPath $BackupPath -Algorithm SHA256).Hash
$sqlEqual = $rowMismatches.Count -eq 0 -and $cppCounts.Count -eq $net10Counts.Count
$dataEqual = $pathMismatches.Count -eq 0 -and $cppFiles.Count -eq $net10Files.Count
$fixtureEvidenceEqual = $cppFixture.available -and $net10Fixture.available -and
    $cppFixture.fixtureValid -and $net10Fixture.fixtureValid -and
    $cppFixture.fullTextReady -and $net10Fixture.fullTextReady -and
    $cppFixture.domainMatches -eq $net10Fixture.domainMatches -and
    $cppFixture.accountMatches -eq $net10Fixture.accountMatches -and
    $cppFixture.inboxMatches -eq $net10Fixture.inboxMatches -and
    $cppFixture.matchingLoopbackPorts -eq $net10Fixture.matchingLoopbackPorts

$report = [ordered]@{
    schema = 'paired-shared-baseline-v2'
    status = if ($sqlEqual -and $dataEqual -and $fixtureEvidenceEqual) { 'EQUIVALENT_START_STATE' } else { 'NOT_EQUIVALENT' }
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    commit = (git rev-parse HEAD)
    databaseSnapshot = [ordered]@{
        cppDatabase = $CppDatabase
        net10Database = $Net10Database
        backupPath = [IO.Path]::GetFullPath($BackupPath)
        backupSha256 = $backupHash
        cppTableCount = $cppCounts.Count
        net10TableCount = $net10Counts.Count
        rowCountMismatches = $rowMismatches
        identicalRowCounts = $sqlEqual
        fixtureEvidenceEqual = $fixtureEvidenceEqual
        cppFixture = $cppFixture
        net10Fixture = $net10Fixture
    }
    dataSnapshot = [ordered]@{
        cppRoot = [IO.Path]::GetFullPath($CppDataRoot)
        net10Root = [IO.Path]::GetFullPath($Net10DataRoot)
        cppFiles = $cppFiles.Count
        net10Files = $net10Files.Count
        commonFiles = @($allPaths | Where-Object { $cppFiles.ContainsKey($_) -and $net10Files.ContainsKey($_) }).Count
        mismatchedPaths = $pathMismatches
        identicalSha256 = $dataEqual
    }
    loopback = '127.0.0.1'
    ports = [ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 }
    productionSafety = 'Only disposable hmail_perf_* databases and isolated Data roots were inspected.'
    releaseGate = 'RED until fixture identity, SQL Full-Text readiness, and identical protocol, message-acceptance, delivery, load, and soak scenarios are proven.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory 'paired-shared-baseline.json'
$markdownPath = Join-Path $OutputDirectory 'paired-shared-baseline.md'
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$markdown = @(
    '# Paired C++ vs .NET 10 shared-baseline evidence',
    '',
    "Start-state result: **$($report.status)**",
    '',
    ('- SQL backup SHA-256: `' + $backupHash + '`'),
    "- SQL row-count mismatches: $($rowMismatches.Count)",
    "- Data files: C++ $($cppFiles.Count), .NET 10 $($net10Files.Count)",
    "- Data path/hash mismatches: $($pathMismatches.Count)",
    "- Fixture evidence equal: $fixtureEvidenceEqual",
    "- Full-Text ready: C++ $($cppFixture.fullTextReady), .NET 10 $($net10Fixture.fullTextReady)",
    "- Fixture valid: C++ $($cppFixture.fixtureValid), .NET 10 $($net10Fixture.fixtureValid)",
    '- Loopback: `127.0.0.1`; SMTP `2525`, IMAP `1143`, POP3 `25110`',
    '',
    'This report proves only the starting SQL row-count, Data SHA-256, fixture-shape, and Full-Text readiness evidence. It does not claim protocol or performance parity.',
    '',
    'The release gate remains **RED** until C++ and .NET 10 both complete the same SMTP, IMAP, POP3, message-acceptance, delivery, concurrent-load, and soak scenarios.',
    '',
    ('JSON evidence: `' + $jsonPath + '`')
)
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "JSON: $jsonPath"
Write-Output "Markdown: $markdownPath"
