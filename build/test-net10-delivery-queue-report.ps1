param(
    [Parameter(Mandatory = $true)]
    [string]$ReportPath,
    [ValidateRange(1, 500)]
    [int]$ExpectedMessageCount = 100
)

$ErrorActionPreference = "Stop"

function Require-Equal([object]$Actual, [object]$Expected, [string]$Name) {
    if ($Actual -ne $Expected) {
        throw "$Name expected '$Expected' but found '$Actual'."
    }
}

function Require-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

$fullPath = [IO.Path]::GetFullPath($ReportPath)
Require-True (Test-Path -LiteralPath $fullPath -PathType Leaf) "Delivery queue report is missing: $fullPath"

$report = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
Require-Equal $report.Schema "net10-live-delivery-queue-v1" "Schema"
Require-True ($report.Database -match '(?i)^Database=hmail_perf_[a-z0-9_]+$') "Report database is not disposable: $($report.Database)"
Require-True ([IO.Path]::GetFullPath($report.DataRoot) -match '(?i)^C:\\hmail-perf-') "Report DataRoot is not disposable: $($report.DataRoot)"
Require-Equal $report.LocalMessageCount $ExpectedMessageCount "LocalMessageCount"
Require-Equal $report.SampleCount $ExpectedMessageCount "SampleCount"
Require-True ([double]$report.TotalMilliseconds -gt 0) "TotalMilliseconds must be positive."
Require-True ([double]$report.ThroughputMessagesPerSecond -gt 0) "ThroughputMessagesPerSecond must be positive."
Require-True ([double]$report.P50Milliseconds -gt 0) "P50Milliseconds must be positive."
Require-True ([double]$report.P95Milliseconds -ge [double]$report.P50Milliseconds) "P95Milliseconds must be at least P50Milliseconds."
Require-True ([double]$report.P99Milliseconds -ge [double]$report.P95Milliseconds) "P99Milliseconds must be at least P95Milliseconds."

$retry = $report.RetryEvidence
Require-Equal $retry.QueuedCount 1 "RetryEvidence.QueuedCount"
Require-Equal $retry.MessageType 1 "RetryEvidence.MessageType"
Require-Equal $retry.Locked 0 "RetryEvidence.Locked"
Require-Equal $retry.RetryCount 1 "RetryEvidence.RetryCount"
Require-Equal $retry.LeaseOwnerIsNull $true "RetryEvidence.LeaseOwnerIsNull"
Require-Equal $retry.RecipientCount 1 "RetryEvidence.RecipientCount"
Require-True ([datetime]$retry.NextTryUtc -gt (Get-Date).ToUniversalTime()) "RetryEvidence.NextTryUtc must be in the future."

$csvPath = [IO.Path]::ChangeExtension($fullPath, ".csv")
$mdPath = [IO.Path]::ChangeExtension($fullPath, ".md")
Require-True (Test-Path -LiteralPath $csvPath -PathType Leaf) "CSV sidecar is missing: $csvPath"
Require-True (Test-Path -LiteralPath $mdPath -PathType Leaf) "Markdown sidecar is missing: $mdPath"

$csv = Import-Csv -LiteralPath $csvPath
Require-Equal @($csv).Count 1 "CSV row count"
Require-Equal $csv.samples $ExpectedMessageCount "CSV samples"
Require-Equal $csv.scenario "local-delivery" "CSV scenario"

$markdown = Get-Content -LiteralPath $mdPath -Raw
Require-True ($markdown -match '(?m)^# Net10 live delivery queue') "Markdown heading is missing."
Require-True ($markdown -match 'Retry evidence') "Markdown retry evidence is missing."

Write-Host "Net10 delivery queue report is valid: $ExpectedMessageCount local messages, retry evidence present, disposable SQL/Data targets."
