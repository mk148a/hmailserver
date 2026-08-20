$ErrorActionPreference = 'Stop'

$provisionPath = Join-Path $PSScriptRoot 'provision-sec18-disposable-hyperv.ps1'
$removePath = Join-Path $PSScriptRoot 'remove-sec18-disposable-hyperv.ps1'
$inventoryPath = Join-Path $PSScriptRoot 'collect-sec18-disposable-hyperv-inventory.ps1'
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($provisionPath, [ref]$null, [ref]$errors) | Out-Null
if ($errors) { throw "Provision script parse failed: $($errors | Out-String)" }
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($removePath, [ref]$null, [ref]$errors) | Out-Null
if ($errors) { throw "Removal script parse failed: $($errors | Out-String)" }
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($inventoryPath, [ref]$null, [ref]$errors) | Out-Null
if ($errors) { throw "Inventory script parse failed: $($errors | Out-String)" }

$provision = Get-Content -LiteralPath $provisionPath -Raw
$remove = Get-Content -LiteralPath $removePath -Raw
$inventory = Get-Content -LiteralPath $inventoryPath -Raw

@(
    'Assert-Administrator',
    'C:\SEC18-Disposable',
    "-SwitchType Private",
    'ProductionPathsTouched',
    'Set-VMFirmware',
    'Get-FileHash'
) | ForEach-Object {
    if ($provision.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Provision script is missing required safety marker: $_"
    }
}

@(
    'SupportsShouldProcess',
    'Get-VMNetworkAdapter -All',
    'Remove-VMSwitch',
    'C:\SEC18-Disposable'
) | ForEach-Object {
    if ($remove.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Removal script is missing required safety marker: $_"
    }
}

if ($provision -match '(?i)New-VMSwitch\s+[^\r\n]*-SwitchType\s+(?!Private)') {
    throw 'Provision script does not enforce a Private switch.'
}
if ($provision -notmatch '(?i)HmailDb|Program Files') {
    throw 'Provision script is missing protected-path rejection markers.'
}
if ($remove -match '(?i)Remove-Item\s+-LiteralPath\s+C:\\') {
    throw 'Removal script contains an unbounded root deletion.'
}
if ($inventory -notmatch '(?i)ProductionPathsTouched') {
    throw 'Inventory script is missing the production-path evidence field.'
}
if ($inventory -notmatch '(?i)SwitchType.*Private|SwitchType -ne ''Private''') {
    throw 'Inventory script is missing private-switch validation.'
}

Write-Output 'SEC18 disposable Hyper-V script tests passed.'
