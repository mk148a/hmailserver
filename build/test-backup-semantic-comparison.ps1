$ErrorActionPreference = "Stop"
$root = Join-Path $env:TEMP ("hmailserver-backup-semantic-test-" + [Guid]::NewGuid().ToString('N'))
$left = Join-Path $root 'left'
$right = Join-Path $root 'right'
$output = Join-Path $root 'output'
$archiveDirectory = Join-Path $root 'archive-directory'
$archiveSource = Join-Path $root 'archive-source'
$compare = Join-Path $PSScriptRoot 'compare-backup-semantic-payloads.ps1'
$sevenZip = Join-Path (Split-Path $PSScriptRoot -Parent) 'hmailserver\installation\Extras\7za.exe'

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

function New-KnownLegacyDifferenceFixtures([string]$LeftPath, [string]$RightPath, [string]$RightDomainName = 'example.com') {
    foreach ($path in @($LeftPath, $RightPath)) {
        $data = Join-Path $path 'DataBackup\example.com\alice'
        New-Item -ItemType Directory -Force -Path $data | Out-Null
        Set-Content -LiteralPath (Join-Path $data 'message.eml') -Value 'same-body' -Encoding utf8 -NoNewline
    }

    @'
<Backup><BackupInformation Mode="15" /><Domains><Domain Name="example.com"><Accounts><Account Name="alice@example.com" Password="hash-a" PasswordEncryption="3" /></Accounts></Domain></Domains><Properties><smtprelayerpassword LongValue="0" StringValue="" /></Properties><SecurityRanges /><TCPIPPorts /><BlockedAttachments /><SURBLServers /><DNSBlackLists /></Backup>
'@ | Set-Content -LiteralPath (Join-Path $LeftPath 'hMailServerBackup.xml') -Encoding utf8 -NoNewline
    "<Backup><BackupInformation Mode=`"15`" /><Domains><Domain Name=`"$RightDomainName`"><Accounts><Account Name=`"alice@example.com`" Password=`"hash-b`" PasswordEncryption=`"3`" /></Accounts></Domain></Domains><Properties /></Backup>" |
        Set-Content -LiteralPath (Join-Path $RightPath 'hMailServerBackup.xml') -Encoding utf8 -NoNewline
}

function Invoke-ExpectedFailure(
    [string]$Target,
    [string]$LeftPath,
    [string]$RightPath,
    [string]$OutputPath,
    [switch]$AllowKnownLegacyDifferences) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $arguments = @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $Target,
            '-LeftInput', $LeftPath,
            '-RightInput', $RightPath,
            '-OutputDirectory', $OutputPath)
        if ($AllowKnownLegacyDifferences) {
            $arguments += '-AllowKnownLegacyDifferences'
        }
        & powershell.exe @arguments *> $null
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

    New-Item -ItemType Directory -Force -Path $archiveSource, $archiveDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $left 'hMailServerBackup.xml') -Destination $archiveSource
    & $sevenZip a -t7z (Join-Path $archiveDirectory 'backup.7z') (Join-Path $archiveSource '*') | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) 'Could not create directory archive fixture.'
    Copy-Item -LiteralPath (Join-Path $left 'DataBackup') -Destination $archiveDirectory -Recurse
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $compare -LeftInput $left -RightInput $archiveDirectory -OutputDirectory $output
    Assert-True ($LASTEXITCODE -eq 0) 'Archive directory comparison failed.'
    $report = Get-Content (Join-Path $output 'backup-semantic-comparison.json') -Raw | ConvertFrom-Json
    Assert-True ($report.status -eq 'PASS' -and $report.archiveInputsExtracted) 'Archive directory was not reported PASS.'

    New-Fixture $right 'different' 'same-body'
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output) -ne 0) 'XML mismatch was not rejected.'

    New-Fixture $right 'same' 'different-body'
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output) -ne 0) 'DataBackup mismatch was not rejected.'

    New-KnownLegacyDifferenceFixtures $left $right
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output) -ne 0) 'Known legacy differences were accepted without the profile.'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $compare -LeftInput $left -RightInput $right -OutputDirectory $output -AllowKnownLegacyDifferences
    Assert-True ($LASTEXITCODE -eq 0) 'Known legacy difference profile failed.'
    $report = Get-Content (Join-Path $output 'backup-semantic-comparison.json') -Raw | ConvertFrom-Json
    Assert-True ($report.status -eq 'PASS_EXPECTED_DIFFERENCES') 'Known legacy profile did not report expected differences.'
    Assert-True ($report.compatibilityProfileStatus -eq 'KNOWN_DIFFERENCES_ONLY' -and $report.normalizedXmlEqual) 'Profile did not normalize only known differences.'
    Assert-True ($report.knownLegacyDifferenceCodes.Count -eq 7) 'Known legacy difference code count was unexpected.'

    $rightXmlPath = Join-Path $right 'hMailServerBackup.xml'
    $rightXml = Get-Content $rightXmlPath -Raw
    $rightXml.Replace('<Properties />', '<Properties /><SecurityRanges><SecurityRange Name="default" /></SecurityRanges>') |
        Set-Content -LiteralPath $rightXmlPath -Encoding utf8 -NoNewline
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output -AllowKnownLegacyDifferences) -ne 0) 'Non-empty collection difference was accepted by the profile.'

    New-KnownLegacyDifferenceFixtures $left $right 'different.example.com'
    Assert-True ((Invoke-ExpectedFailure $compare $left $right $output -AllowKnownLegacyDifferences) -ne 0) 'Unexpected XML difference was accepted by the profile.'

    Write-Host 'PASS: backup semantic comparator equal and mismatch cases validated.'
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
