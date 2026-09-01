param(
    [string]$SqlConnection = $env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION,
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$dotnet = Join-Path $workspaceRoot "tools\dotnet10\dotnet.exe"
$testProject = Join-Path $repoRoot "hmailserver\source\Server.Net10\tests\HMailServer.Net10.Tests\HMailServer.Net10.Tests.csproj"

if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "The bundled .NET 10 SDK is missing: $dotnet"
}
if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) {
    throw "The Net10 test project is missing: $testProject"
}
if ($env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE -ne "1") {
    throw "Set HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1 for disposable SQL/Data restore tests."
}
if ([string]::IsNullOrWhiteSpace($SqlConnection) -or
    $SqlConnection -notmatch '(?i)(^|;)\s*Server\s*=\s*(localhost|127\.0\.0\.1)(,\d+)?\s*(;|$)' -or
    $SqlConnection -notmatch '(?i)Integrated Security\s*=\s*True' -or
    $SqlConnection -match '(?i)AttachDbFilename|User Id|Password') {
    throw "Refusing non-local or credential-bearing SQL connection. Use localhost with Integrated Security=True and no AttachDbFilename/User Id/Password."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\migration\net10-backup-restore-roundtrip"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$trxPath = Join-Path $OutputDirectory "backup-restore-roundtrip.trx"
$logPath = Join-Path $OutputDirectory "backup-restore-roundtrip.log"
$reportPath = Join-Path $OutputDirectory "backup-restore-roundtrip.json"
$start = [DateTimeOffset]::UtcNow

$env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION = $SqlConnection
$env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE = "1"
$output = @(& $dotnet test $testProject --configuration Release --filter "FullyQualifiedName~BackupRestoreRoundTripIntegrationTests" --logger "trx;LogFileName=$trxPath" --logger "console;verbosity=minimal" 2>&1)
$exitCode = $LASTEXITCODE
$output | Out-File -LiteralPath $logPath -Encoding utf8
$end = [DateTimeOffset]::UtcNow

$total = 0
$passed = 0
$failed = 0
$skipped = 0
if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -ne $counters) {
        $total = [int]$counters.total
        $passed = [int]$counters.passed
        $failed = [int]$counters.failed
        $skipped = [int]$counters.notExecuted
    }
}

$report = [ordered]@{
    schema = "net10-backup-restore-roundtrip-v1"
    status = if ($exitCode -eq 0 -and $failed -eq 0 -and $passed -gt 0) { "PASS" } else { "FAIL" }
    generatedUtc = $end
    startedUtc = $start
    endedUtc = $end
    durationSeconds = ($end - $start).TotalSeconds
    testClass = "BackupRestoreRoundTripIntegrationTests"
    total = $total
    passed = $passed
    failed = $failed
    skipped = $skipped
    sqlDataSource = "localhost"
    sqlAuthentication = "Integrated Security"
    disposableDatabasePattern = "hmailserver_net10_*"
    disposableDatabaseCleanup = "test finally blocks drop each unique database"
    dataRootPolicy = "test-owned temporary roots only"
    productionTargetsUsed = $false
    hmailDbTest5700Used = $false
    hmailServerServiceUsed = $false
    rollback = "test finally blocks drop SQL databases and delete temporary Data roots"
}
$json = $report | ConvertTo-Json -Depth 5
Set-Content -LiteralPath $reportPath -Value ($json + [Environment]::NewLine) -Encoding utf8 -NoNewline
Set-Content -LiteralPath ([IO.Path]::ChangeExtension($reportPath, ".csv")) -Value ("status,total,passed,failed,skipped,duration_seconds,sql_data_source,production_targets_used`n" + $report.status + ",$total,$passed,$failed,$skipped," + $report.durationSeconds + ",localhost,False`n") -Encoding utf8 -NoNewline
$markdown = @"
# Net10 backup -> restore -> backup round-trip

- Result: ``$($report.status)``
- Tests: ``$passed/$total`` passed, ``$failed`` failed, ``$skipped`` skipped
- SQL: ``localhost``, Integrated Security
- Database pattern: ``hmailserver_net10_*`` with drop in test ``finally``
- Data roots: test-owned temporary roots with delete in ``finally``
- Production service/database/Data directory used: ``$($report.productionTargetsUsed)``

This is isolated restore evidence; it does not prove production backup/restore, installer rollback, or service rollback.
"@
Set-Content -LiteralPath ([IO.Path]::ChangeExtension($reportPath, ".md")) -Value $markdown -Encoding utf8 -NoNewline

if ($exitCode -ne 0 -or $report.status -ne "PASS") {
    throw "Backup/restore round-trip failed. See $logPath and $reportPath."
}
Write-Host "Backup/restore round-trip passed: $passed/$total tests. Report: $reportPath"
