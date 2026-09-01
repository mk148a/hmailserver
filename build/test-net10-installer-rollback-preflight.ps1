param(
    [string]$OutputDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$preflightScript = Join-Path $PSScriptRoot "test-net10-rollback-archive-preflight.ps1"
if (-not (Test-Path -LiteralPath $preflightScript -PathType Leaf)) {
    throw "Rollback preflight test is missing: $preflightScript"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\migration\installer-rollback-preflight-current"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$jsonPath = Join-Path $OutputDirectory "installer-rollback-preflight.json"
$csvPath = Join-Path $OutputDirectory "installer-rollback-preflight.csv"
$markdownPath = Join-Path $OutputDirectory "installer-rollback-preflight.md"
$logPath = Join-Path $OutputDirectory "installer-rollback-preflight.log"
$startedUtc = [DateTimeOffset]::UtcNow

$service = Get-Service -Name "hMailServer" -ErrorAction SilentlyContinue
$servicePresentBefore = $null -ne $service
$preflightOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $preflightScript 2>&1)
$preflightExitCode = $LASTEXITCODE
$preflightOutput | Out-File -LiteralPath $logPath -Encoding utf8
$serviceAfter = Get-Service -Name "hMailServer" -ErrorAction SilentlyContinue
$servicePresentAfter = $null -ne $serviceAfter
$endedUtc = [DateTimeOffset]::UtcNow

$report = [ordered]@{
    schema = "net10-installer-rollback-preflight-v1"
    status = if ($preflightExitCode -eq 0) { "ENVIRONMENT-BLOCKED" } else { "FAIL" }
    preflightStatus = if ($preflightExitCode -eq 0) { "PASS" } else { "FAIL" }
    actualInstallerDrillStatus = "ENVIRONMENT-BLOCKED"
    startedUtc = $startedUtc.ToString("o")
    endedUtc = $endedUtc.ToString("o")
    durationSeconds = ($endedUtc - $startedUtc).TotalSeconds
    preflightScript = "build/test-net10-rollback-archive-preflight.ps1"
    preflightExitCode = $preflightExitCode
    servicePresentBefore = $servicePresentBefore
    servicePresentAfter = $servicePresentAfter
    machineMutationPerformed = $false
    productionTargetsUsed = $false
    legacyRegisteredServiceComBaselineAvailable = $false
    blockedReason = "A disposable registered legacy hMailServer service/COM baseline and isolated SQL/Data clone are required before invoking installer or uninstaller mutation."
    protectedOperations = "No sc.exe create/config/delete, COM registration/unregistration, registry, DCOM, service start/stop, SQL, or Data-directory mutation was performed."
}

$json = $report | ConvertTo-Json -Depth 5
Set-Content -LiteralPath $jsonPath -Value $json -Encoding utf8 -NoNewline
Set-Content -LiteralPath $csvPath -Value ("status,preflight_status,actual_installer_drill_status,preflight_exit_code,service_present_before,service_present_after,machine_mutation_performed,production_targets_used`n" + "$($report.status),$($report.preflightStatus),$($report.actualInstallerDrillStatus),$preflightExitCode,$servicePresentBefore,$servicePresentAfter,False,False") -Encoding utf8 -NoNewline
$markdown = @"
# Installer/service/Data rollback preflight

- Overall result: ``$($report.status)``
- Archive/source preflight: ``$($report.preflightStatus)``
- Actual installer drill: ``$($report.actualInstallerDrillStatus)``
- hMailServer service present before/after: ``$servicePresentBefore`` / ``$servicePresentAfter``
- Disposable registered legacy service/COM baseline: ``$($report.legacyRegisteredServiceComBaselineAvailable)``
- Machine mutation performed: ``$($report.machineMutationPerformed)``
- Production targets used: ``$($report.productionTargetsUsed)``

The safe preflight validates archive structure, bounded reads, process limits,
service snapshot parsing, installer ordering, uninstaller compensation, and
PowerShell syntax. The actual installer drill remains blocked until a
disposable registered legacy service/COM baseline and isolated SQL/Data clone
are available. No service, registry, COM, DCOM, SQL, or Data-directory
mutation was performed.
"@
Set-Content -LiteralPath $markdownPath -Value $markdown -Encoding utf8 -NoNewline

if ($preflightExitCode -ne 0) {
    throw "Installer rollback preflight failed. See $logPath."
}

Write-Host "Installer rollback preflight passed; machine drill remains ENVIRONMENT-BLOCKED. Report: $jsonPath"
