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

Assert-DisposableTarget -Database $CppDatabase -DataRoot $CppDataRoot
Assert-DisposableTarget -Database $Net10Database -DataRoot $Net10DataRoot
if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
    throw "SQL backup evidence file does not exist: $BackupPath"
}

$cppCounts = @(Get-TableCounts -Database $CppDatabase)
$net10Counts = @(Get-TableCounts -Database $Net10Database)
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

$report = [ordered]@{
    schema = 'paired-shared-baseline-v1'
    status = if ($sqlEqual -and $dataEqual) { 'EQUIVALENT_START_STATE' } else { 'NOT_EQUIVALENT' }
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
    releaseGate = 'RED until both implementations complete identical protocol, message-acceptance, delivery, load, and soak scenarios.'
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
    '- Loopback: `127.0.0.1`; SMTP `2525`, IMAP `1143`, POP3 `25110`',
    '',
    'This report proves only the starting SQL row-count and Data SHA-256 equivalence. It does not claim protocol or performance parity.',
    '',
    'The release gate remains **RED** until C++ and .NET 10 both complete the same SMTP, IMAP, POP3, message-acceptance, delivery, concurrent-load, and soak scenarios.',
    '',
    ('JSON evidence: `' + $jsonPath + '`')
)
$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8
Write-Output "JSON: $jsonPath"
Write-Output "Markdown: $markdownPath"
