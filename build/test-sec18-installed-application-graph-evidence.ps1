[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

$collectorPath = Join-Path $PSScriptRoot 'get-sec18-installed-application-graph-evidence.ps1'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('hmailserver-sec18-' + [Guid]::NewGuid().ToString('N'))
$outputPath = Join-Path $temporaryDirectory 'evidence.json'
$mismatchOutputPath = Join-Path $temporaryDirectory 'mismatch.json'

try {
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $collectorPath -OfflineFixture -OutputPath $outputPath -FailOnIncomplete
    Assert-True ($LASTEXITCODE -eq 0) "offline collector exited with $LASTEXITCODE"

    $evidence = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    Assert-True ($evidence.SchemaVersion -eq 1) 'schema version is not 1'
    Assert-True ($evidence.EvidenceKind -eq 'SEC18-InstalledApplicationGraph') 'evidence kind is incorrect'
    Assert-True ($evidence.CollectionSource -eq 'offline-fixture') 'offline fixture source was not recorded'
    Assert-True (-not [bool]$evidence.RegistryReadPerformed) 'offline fixture touched registry'
    Assert-True ($evidence.GraphPathCount -eq 22) 'graph path count is not 22'
    Assert-True ($evidence.SnapshotCount -eq 44) 'snapshot count is not 44'
    Assert-True ([bool]$evidence.CanonicalExpectedContentsValidated) 'canonical values were not validated'
    Assert-True ([bool]$evidence.UnknownSubkeysEnumerated) 'direct subkey enumeration was not recorded'
    Assert-True ([bool]$evidence.CollectorAttested) 'collector/source attestation is incomplete'
    Assert-True ([bool]$evidence.CompleteReadback) 'offline graph readback is incomplete'
    Assert-True ($evidence.GateDecision -eq 'EvidenceReadyForIndependentReview') 'unexpected gate decision'
    Assert-True ($evidence.CanonicalValidation.ExpectedGraphPathCount -eq 22) 'canonical graph count is incorrect'
    Assert-True ([bool]$evidence.CanonicalValidation.DirectSubkeysValidated) 'direct-subkey validation is incomplete'
    Assert-True ([bool]$evidence.CanonicalValidation.Registry32AsymmetryValidated) 'Registry32 asymmetry is incomplete'
    Assert-True ([bool]$evidence.CanonicalValidation.InstallationPathsValidated) 'installation path validation is incomplete'
    Assert-True ($evidence.Attestation.SourceFiles.Count -ge 6) 'source attestation file list is incomplete'
    Assert-True ($evidence.Attestation.CollectorSha256 -match '^[A-F0-9]{64}$') 'collector hash is not SHA-256'
    foreach ($source in $evidence.Attestation.SourceFiles) {
        Assert-True ([bool]$source.Present) "attested source is missing: $($source.Path)"
        Assert-True ($source.Sha256 -match '^[A-F0-9]{64}$') "source hash is not SHA-256: $($source.Path)"
    }

    $absentRegistry32 = @($evidence.Snapshots | Where-Object {
        $_.View -eq 'Registry32' -and -not [bool]$_.Present -and $_.KeyPath -like '*CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}*'
    })
    Assert-True ($absentRegistry32.Count -eq 6) 'Registry32 Application CLSID subtree asymmetry is incorrect'

    $applicationClass = $evidence.Snapshots | Where-Object {
        $_.View -eq 'Registry64' -and $_.KeyPath -eq 'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}'
    }
    Assert-True ($null -ne $applicationClass) 'Registry64 Application CLSID root is missing'
    Assert-True (@($applicationClass.DirectSubkeyNames) -contains 'LocalServer32') 'LocalServer32 direct subkey was not captured'

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $collectorPath -OfflineFixture -ExpectedModulePath 'C:\different\hMailServer.exe' -OutputPath $mismatchOutputPath -FailOnIncomplete
    Assert-True ($LASTEXITCODE -eq 2) "mismatched module path exited with $LASTEXITCODE instead of 2"
    $mismatch = Get-Content -LiteralPath $mismatchOutputPath -Raw | ConvertFrom-Json
    Assert-True (-not [bool]$mismatch.CanonicalExpectedContentsValidated) 'mismatched module path was accepted'
    Assert-True ($mismatch.CanonicalValidation.Errors.Count -gt 0) 'mismatched module path produced no validation error'

    Write-Output 'SEC-18 installed Application graph collector tests passed.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
