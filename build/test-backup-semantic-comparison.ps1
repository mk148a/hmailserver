$ErrorActionPreference = "Stop"
$root = Join-Path $env:TEMP ("hmailserver-backup-semantic-test-" + [Guid]::NewGuid().ToString('N'))
$left = Join-Path $root 'left'
$right = Join-Path $root 'right'
$output = Join-Path $root 'output'
$compare = Join-Path $PSScriptRoot 'compare-backup-semantic-payloads.ps1'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function New-Fixture([string]$Path, [string]$Subject, [string]$Body) {
    $data = Join-Path $Path 'DataBackup\example.com\alice'
    New-Item -ItemType Directory -Force -Path $data | Out-Null
    @"
<Backup><BackupInformation Mode="7" /><Domains><Domain Name="example.com"><Accounts><Account Name="alice@example.com"><Folders><Folder Name="Inbox"><Messages /></Folder></Folders></Account></Accounts></Domain></Domains><Payload Subject="$Subject" /></Backup>
"@ | Set-Content -LiteralPath (Join-Path $Path 'hMailServerBackup.xml') -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $data 'message.eml') -Value $Body -Encoding utf8 -NoNewline
}

function Invoke-ExpectedFailure([string]$Target, [string]$LeftPath, [string]$RightPath, [string]$OutputPath) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Target -LeftInput $LeftPath -RightInput $RightPath -OutputDirectory $OutputPath *> $null
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
}

try {
    New-Fixture $left 'same' 'same-body'
    New-Fixture $right 'same' 'same-body'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $compare -LeftInput $left -RightInput $right -OutputDirectory $output
    Assert-True ($LASTEXITCODE -eq 0) 'Equal fixture comparison failed.'
    $report = Get-Content (Join-Path $output 'backup-semantic-comparison.json') -Raw | ConvertFrom-Json
    Assert-True ($report.status -eq 'PASS' -and $report.xmlEqual -and $report.dataBackupEqual) 'Equal fixture was not reported PASS.'

    New-Fixture $right 'different' 'same-body'
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output) -ne 0) 'XML mismatch was not rejected.'

    New-Fixture $right 'same' 'different-body'
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output) -ne 0) 'DataBackup mismatch was not rejected.'

    Write-Host 'PASS: backup semantic comparator equal and mismatch cases validated.'
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
