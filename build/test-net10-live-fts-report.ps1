param(
    [Parameter(Mandatory = $true)]
    [string]$InputFile,
    [ValidateRange(1, 300)]
    [int]$ExpectedIterations = 25,
    [ValidateRange(1, 1000000)]
    [int]$ExpectedMessages = 1000
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $InputFile -PathType Leaf)) {
    throw "FTS report does not exist: $InputFile"
}

$report = Get-Content -LiteralPath $InputFile -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()

function Require-Equal {
    param([string]$Name, $Actual, $Expected)
    if ($Actual -ne $Expected) {
        $failures.Add("$Name expected '$Expected' but was '$Actual'.")
    }
}

function Require-True {
    param([string]$Name, [object]$Value)
    if (-not [bool]$Value) {
        $failures.Add("$Name must be true.")
    }
}

Require-Equal 'schema' $report.schema 'live-imap-search-acceptance-v1'
Require-Equal 'status' $report.status 'PASS'
Require-Equal 'implementation' $report.implementation 'net10'
Require-Equal 'bind' $report.bind '127.0.0.1'
Require-Equal 'port' $report.port 1143
Require-Equal 'indexedMessages' $report.indexedMessages $ExpectedMessages
Require-Equal 'iterations' $report.iterations $ExpectedIterations
Require-Equal 'successes' $report.successes $ExpectedIterations
Require-Equal 'errors' $report.errors 0
Require-True 'preparationPassed' $report.preparationPassed
Require-True 'shutdownPassed' $report.shutdownPassed

if ([string]$report.database -notmatch '^hmail_perf_[a-z0-9_]+$') {
    $failures.Add("database is not disposable: $($report.database)")
}
if ([string]$report.dataRoot -notmatch '(?i)^C:\\hmail-perf-') {
    $failures.Add("dataRoot is not disposable: $($report.dataRoot)")
}
if ([string]$report.productionSafety -notmatch 'loopback-only.*disposable SQL/Data.*cleared') {
    $failures.Add('productionSafety does not attest loopback, disposable targets, and cleanup.')
}

foreach ($prefix in @('', 'search_')) {
    $p50 = [double]$report.("${prefix}p50_ms")
    $p95 = [double]$report.("${prefix}p95_ms")
    $p99 = [double]$report.("${prefix}p99_ms")
    if ($p50 -lt 0 -or $p50 -gt $p95 -or $p95 -gt $p99) {
        $failures.Add("${prefix}percentiles are not ordered and non-negative.")
    }
}

$samples = @($report.samples)
Require-Equal 'sample count' $samples.Count $ExpectedIterations
foreach ($sample in $samples) {
    Require-True "sample $($sample.iteration) ok" $sample.ok
    Require-Equal "sample $($sample.iteration) matches" $sample.matches $ExpectedMessages
    Require-Equal "sample $($sample.iteration) result count" $sample.searchResultCount $ExpectedMessages
    Require-True "sample $($sample.iteration) exact sequence" $sample.searchExactSequence
    if ($null -ne $sample.error -and [string]$sample.error -ne '') {
        $failures.Add("sample $($sample.iteration) reported error: $($sample.error)")
    }
}

if ($failures.Count -gt 0) {
    throw ("FTS report validation failed:`n - " + ($failures -join "`n - "))
}

Write-Output "PASS: $InputFile ($($report.successes)/$($report.iterations), $($report.search_p95_ms) ms SEARCH p95)"
