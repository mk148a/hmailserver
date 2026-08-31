param(
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory
)

$ErrorActionPreference = 'Stop'
$jsonPath = Join-Path $InputDirectory 'imap-query-state.json'
$markdownPath = Join-Path $InputDirectory 'imap-query-state.md'
foreach ($path in @($jsonPath, $markdownPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Query-state artifact is missing: $path" }
}
$report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
if ($report.schema -ne 'paired-imap-query-state-v1' -or $report.status -ne 'PASS') { throw 'Unexpected query-state schema or status.' }
if ($report.readOnly -ne $true -or $report.sqlServer -ne 'localhost') { throw 'Query-state evidence is not marked read-only loopback SQL.' }
if ([string]::IsNullOrWhiteSpace([string]$report.fixtureManifestSha256) -or [string]$report.fixtureManifestSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Fixture manifest binding is missing.' }
if (@($report.states).Count -ne 2) { throw 'Expected C++ and Net10 query-state rows.' }
foreach ($state in @($report.states)) {
    if ($state.implementation -notin @('cpp', 'net10')) { throw 'Unexpected implementation in query-state evidence.' }
    if ($state.database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $state.database -match '(?i)production|hmaildb_test5700') { throw 'Non-disposable database in query-state evidence.' }
    if ($state.dataRoot -notmatch '(?i)^C:\\hmail-perf-' -or $state.dataRoot -match '(?i)hmailserver57|production') { throw 'Non-disposable Data root in query-state evidence.' }
    if ([int64]$state.messageCount -lt 0 -or [int64]$state.fullTextCatalogCount -lt 0 -or [int64]$state.fullTextIndexCount -lt 0) { throw 'Negative SQL state count.' }
    if ($state.indexedSearchAcceptanceReady -eq $true -and (-not $state.fullTextReady -or $null -eq $state.searchDocumentCount -or $state.searchDocumentCount -ne $state.messageCount -or $state.searchQueueCount -ne 0)) { throw 'Search-ready state is inconsistent.' }
    if ($state.searchDocumentsTablePresent -and $null -eq $state.searchDocumentCount) { throw 'Present search document table has no count.' }
    if ($state.searchQueueTablePresent -and $null -eq $state.searchQueueCount) { throw 'Present search queue table has no count.' }
}
$text = Get-Content -LiteralPath $markdownPath -Raw
if ($text -match '(?i)C:\\|hmail_perf_|hmail-perf-|password|secret|token') { throw 'Query-state Markdown contains local paths or sensitive identifiers.' }
Write-Output 'Validated read-only paired IMAP query-state evidence.'
