param(
    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$path = [IO.Path]::GetFullPath($ReportPath)
Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Backup/restore report is missing: $path"
$report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
Assert-True ($report.schema -eq "net10-backup-restore-roundtrip-v1") "Unexpected backup/restore report schema."
Assert-True ($report.status -eq "PASS") "Backup/restore report status is not PASS."
Assert-True ($report.total -eq 25 -and $report.passed -eq 25 -and $report.failed -eq 0 -and $report.skipped -eq 0) "Backup/restore report does not prove 25/25 clean tests."
Assert-True ($report.durationSeconds -gt 0) "Backup/restore report duration is invalid."
Assert-True ($report.sqlDataSource -eq "localhost" -and $report.sqlAuthentication -eq "Integrated Security") "Backup/restore SQL source is not local integrated security."
Assert-True ($report.disposableDatabasePattern -eq "hmailserver_net10_*") "Backup/restore database pattern is not disposable."
Assert-True ($report.productionTargetsUsed -eq $false -and $report.hmailDbTest5700Used -eq $false -and $report.hmailServerServiceUsed -eq $false) "Backup/restore report claims production target use."
Assert-True ([string]$report.dataRootPolicy -match "test-owned") "Backup/restore Data root policy is not test-owned."
Assert-True ([string]$report.rollback -match "drop SQL databases" -and [string]$report.rollback -match "delete temporary Data") "Backup/restore cleanup/rollback evidence is incomplete."
foreach ($sidecar in @(([IO.Path]::ChangeExtension($path, ".csv")), ([IO.Path]::ChangeExtension($path, ".md")))) {
    Assert-True (Test-Path -LiteralPath $sidecar -PathType Leaf) "Backup/restore sidecar is missing: $sidecar"
}
Write-Output "PASS: isolated Net10 backup/restore round-trip report validated: $path"
