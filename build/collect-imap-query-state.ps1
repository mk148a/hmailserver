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
    [string]$FixtureManifest,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'live-benchmark-provenance.ps1')

function Assert-DisposableStateTarget {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$DataRoot
    )

    if ($Database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $Database -match '(?i)production|hmaildb_test5700') {
        throw "Refusing non-disposable query-state database: $Database"
    }

    $fullRoot = [IO.Path]::GetFullPath($DataRoot)
    if ($fullRoot -notmatch '(?i)^C:\\hmail-perf-' -or $fullRoot -match '(?i)hmailserver57|production') {
        throw "Refusing non-disposable query-state Data root: $fullRoot"
    }
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Query-state Data root does not exist: $fullRoot"
    }
}

function Invoke-ScalarQuery {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$Query
    )

    $lines = @(sqlcmd -S localhost -E -d $Database -W -h-1 -b -Q $Query 2>&1)
    if ($LASTEXITCODE -ne 0 -or $lines.Count -eq 0) {
        throw "sqlcmd query failed for disposable database '$Database': $($lines -join ' ')"
    }
    $value = @($lines | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -ne '' }) | Select-Object -First 1
    if ($null -eq $value) {
        throw "sqlcmd returned no scalar value for disposable database '$Database'."
    }
    return [string]$value
}

function Get-QueryState {
    param(
        [Parameter(Mandatory = $true)][string]$Implementation,
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$DataRoot
    )

    $documentsTable = [int](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.hm_message_search_documents', N'U') IS NULL THEN 0 ELSE 1 END;")
    $queueTable = [int](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.hm_message_search_queue', N'U') IS NULL THEN 0 ELSE 1 END;")
    $messages = [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_messages WHERE messagetype = 2;")
    $indexingEnabled = [int](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT CASE WHEN EXISTS (SELECT 1 FROM hm_settings WHERE settingname = N'MessageIndexing' AND settinginteger <> 0) THEN 1 ELSE 0 END;")
    $documents = if ($documentsTable -eq 1) {
        [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_message_search_documents;")
    } else { $null }
    $queue = if ($queueTable -eq 1) {
        [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_message_search_queue;")
    } else { $null }
    $leased = if ($queueTable -eq 1) {
        [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_message_search_queue WHERE searchleaseowner IS NOT NULL;")
    } else { $null }
    $failed = if ($queueTable -eq 1) {
        [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM hm_message_search_queue WHERE lasterror IS NOT NULL;")
    } else { $null }
    $fullTextInstalled = [int](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'));")
    $catalogs = [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM sys.fulltext_catalogs WHERE name = N'hm_message_search_catalog';")
    $indexes = [int64](Invoke-ScalarQuery -Database $Database -Query "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.hm_message_search_documents');")

    [pscustomobject]@{
        implementation = $Implementation
        database = $Database
        dataRoot = [IO.Path]::GetFullPath($DataRoot)
        messageCount = $messages
        messageIndexingEnabled = $indexingEnabled -eq 1
        searchDocumentsTablePresent = $documentsTable -eq 1
        searchDocumentCount = $documents
        searchQueueTablePresent = $queueTable -eq 1
        searchQueueCount = $queue
        searchQueueLeasedCount = $leased
        searchQueueFailedCount = $failed
        indexedCoveragePercent = if ($null -eq $documents -or $messages -eq 0) { $null } else { [math]::Round(($documents / $messages) * 100, 3) }
        fullTextInstalled = $fullTextInstalled -eq 1
        fullTextCatalogCount = $catalogs
        fullTextIndexCount = $indexes
        fullTextReady = $fullTextInstalled -eq 1 -and $documentsTable -eq 1 -and $catalogs -ge 1 -and $indexes -ge 1
        indexedSearchAcceptanceReady = $fullTextInstalled -eq 1 -and $documentsTable -eq 1 -and $documents -eq $messages -and $queue -eq 0
    }
}

Assert-DisposableStateTarget -Database $CppDatabase -DataRoot $CppDataRoot
Assert-DisposableStateTarget -Database $Net10Database -DataRoot $Net10DataRoot
$cppManifest = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation 'cpp' -RepositoryRoot ((Get-Item $PSScriptRoot).Parent.FullName)
$net10Manifest = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation 'net10' -RepositoryRoot ((Get-Item $PSScriptRoot).Parent.FullName)
if ($cppManifest.database -cne $CppDatabase -or $net10Manifest.database -cne $Net10Database -or
    $cppManifest.dataRoot -ine [IO.Path]::GetFullPath($CppDataRoot) -or
    $net10Manifest.dataRoot -ine [IO.Path]::GetFullPath($Net10DataRoot) -or
    $cppManifest.sha256 -cne $net10Manifest.sha256) {
    throw 'Query-state inputs do not match the paired fixture manifest.'
}

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$report = [ordered]@{
    schema = 'paired-imap-query-state-v1'
    status = 'PASS'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
    fixtureManifestSha256 = $cppManifest.sha256
    readOnly = $true
    sqlServer = 'localhost'
    states = @(
        (Get-QueryState -Implementation 'cpp' -Database $CppDatabase -DataRoot $CppDataRoot),
        (Get-QueryState -Implementation 'net10' -Database $Net10Database -DataRoot $Net10DataRoot)
    )
    productionSafety = 'Only disposable hmail_perf_* databases and C:\hmail-perf-* Data roots were queried; no SQL or filesystem mutation was performed.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$jsonPath = Join-Path $OutputDirectory 'imap-query-state.json'
$markdownPath = Join-Path $OutputDirectory 'imap-query-state.md'
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$markdown = @(
    '# Paired IMAP query-state evidence',
    '',
    'Status: **PASS**',
    '',
    'This read-only evidence records SQL indexing and backfill state for a disposable paired fixture.',
    '',
    '| Implementation | Messages | Search documents | Coverage | Queue | Indexing enabled | Full-Text ready | Search-ready |',
    '| --- | ---: | ---: | ---: | ---: | :---: | :---: | :---: |'
)
foreach ($state in $report.states) {
    $documents = if ($null -eq $state.searchDocumentCount) { 'absent' } else { $state.searchDocumentCount }
    $coverage = if ($null -eq $state.indexedCoveragePercent) { 'n/a' } else { "$($state.indexedCoveragePercent)%" }
    $queue = if ($null -eq $state.searchQueueCount) { 'absent' } else { $state.searchQueueCount }
    $leased = if ($null -eq $state.searchQueueLeasedCount) { 'n/a' } else { $state.searchQueueLeasedCount }
    $markdown += "| $($state.implementation) | $($state.messageCount) | $documents | $coverage | $queue | $($state.messageIndexingEnabled) | $($state.fullTextReady) | $($state.indexedSearchAcceptanceReady) |"
}
$markdown += @(
    '',
    'This report is diagnostic evidence only. It does not establish a performance winner or authorize a production capacity change.'
)
$markdown -join [Environment]::NewLine | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "Wrote read-only query-state evidence: $OutputDirectory"
