[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$scriptPath = Join-Path $PSScriptRoot 'upgrade-net10-from-legacy.ps1'
$installerPath = Join-Path $PSScriptRoot 'install-net10-service.ps1'
$hostPath = Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Service\Host.cs'
$initializationPath = Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Security\LegacyInitializationFile.cs'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

foreach ($path in @($scriptPath, $installerPath, $hostPath, $initializationPath)) {
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Required upgrade source is missing: $path"
}

$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$null, [ref]$parseErrors) | Out-Null
Assert-True ($parseErrors.Count -eq 0) "Upgrade script has PowerShell parse errors: $($parseErrors -join '; ')"

$source = Get-Content -LiteralPath $scriptPath -Raw
$installer = Get-Content -LiteralPath $installerPath -Raw
$hostSource = Get-Content -LiteralPath $hostPath -Raw
$initialization = Get-Content -LiteralPath $initializationPath -Raw

Assert-True ($source -match 'mode = if \(\$Execute\) \{ ''Execute'' \} else \{ ''PlanOnly'' \}') 'Upgrade defaults to neither explicit plan nor execute mode.'
Assert-True ($source -match 'Assert-UpgradeHandoff') 'Upgrade does not require a completed handoff manifest.'
Assert-True ($source -match 'ServiceMutationAllowed|serviceMutationAllowed') 'Upgrade does not require service mutation authorization.'
Assert-True ($source -match 'State -ne ''Stopped''') 'Upgrade does not require the legacy service to be stopped.'
Assert-True ($source -match 'install-net10-service\.ps1') 'Upgrade does not delegate service mutation to the guarded installer.'
Assert-True ($source -match "'-ReplaceExisting'") 'Upgrade does not use replacement mode.'
Assert-True ($source -match "'-InitializationFile'") 'Upgrade does not carry the legacy initialization file into the new service.'
Assert-True ($source -match 'if \(-not \$Execute\)') 'Upgrade does not stop after a non-mutating plan.'
Assert-True ($source -notmatch 'sc\.exe\s+(config|create|delete)') 'Upgrade script performs direct SCM mutation outside the guarded installer.'

Assert-True ($installer -match '\[string\]\$InitializationFile') 'Installer does not accept a legacy initialization file.'
Assert-True ($installer -match 'servicePath') 'Installer does not construct a service command line.'
Assert-True ($installer -match '--InitializationFile') 'Installer does not persist the initialization file argument.'
Assert-True ($hostSource -match 'BuildLegacySqlServerConnectionString') 'Host does not support legacy SQL configuration fallback.'
Assert-True ($hostSource -match 'LoadLegacyDataDirectory') 'Host does not support legacy DataFolder fallback.'
Assert-True ($initialization -match 'LegacyBlowfishPasswordCipher\.TryDecrypt') 'Legacy database password decryption is not wired.'

Write-Output 'hMailServer legacy upgrade guard tests passed.'
