param(
    [Parameter(Mandatory = $true)]
    [string]$CppReport,
    [Parameter(Mandatory = $true)]
    [string]$Net10Report,
    [ValidateRange(1, 100000)]
    [int]$ExpectedMessageCount = 500
)

$ErrorActionPreference = "Stop"

function Read-Report {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Report is missing: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-Equal {
    param([object]$Actual, [object]$Expected, [string]$Name)

    if ($Actual -ne $Expected) {
        throw "$Name mismatch. Expected '$Expected', got '$Actual'."
    }
}

function Assert-True {
    param([bool]$Value, [string]$Name)

    if (-not $Value) {
        throw "$Name must be true."
    }
}

$cpp = Read-Report $CppReport
$net10 = Read-Report $Net10Report

Assert-Equal $cpp.status "PASS" "C++ status"
Assert-Equal $net10.status "PASS" ".NET 10 status"
Assert-Equal $cpp.implementation "cpp" "C++ implementation"
Assert-Equal $net10.implementation "net10" ".NET 10 implementation"
Assert-Equal $cpp.requestedMessages $ExpectedMessageCount "C++ requested message count"
Assert-Equal $net10.requestedMessages $ExpectedMessageCount ".NET 10 requested message count"
Assert-Equal $cpp.acceptedMessages $ExpectedMessageCount "C++ accepted message count"
Assert-Equal $net10.acceptedMessages $ExpectedMessageCount ".NET 10 accepted message count"
Assert-Equal $cpp.errors 0 "C++ errors"
Assert-Equal $net10.errors 0 ".NET 10 errors"
Assert-Equal $cpp.fixtureId $net10.fixtureId "fixture identity"
Assert-Equal $cpp.manifestSha256 $net10.manifestSha256 "fixture manifest hash"
Assert-Equal $cpp.runStartAttestation.databaseVersion 5708 "C++ database version"
Assert-Equal $net10.runStartAttestation.databaseVersion 6000 ".NET 10 database version"
Assert-True ([bool]$cpp.localDeliveryReadbackEnabled) "C++ local-delivery readback"
Assert-True ([bool]$net10.localDeliveryReadbackEnabled) ".NET 10 local-delivery readback"
Assert-True ([bool]$cpp.localDeliveryReadback.after.valid) "C++ local-delivery readback validity"
Assert-True ([bool]$net10.localDeliveryReadback.after.valid) ".NET 10 local-delivery readback validity"
Assert-Equal $cpp.localDeliveryReadback.after.expectedCount $ExpectedMessageCount "C++ local-delivery count"
Assert-Equal $net10.localDeliveryReadback.after.expectedCount $ExpectedMessageCount ".NET 10 local-delivery count"
Assert-Equal $cpp.localDeliveryReadback.after.rowCount $ExpectedMessageCount "C++ local-delivery rows"
Assert-Equal $net10.localDeliveryReadback.after.rowCount $ExpectedMessageCount ".NET 10 local-delivery rows"
Assert-True ([bool]$cpp.productionSafety) "C++ production safety"
Assert-True ([bool]$net10.productionSafety) ".NET 10 production safety"

Write-Output "Paired SMTP acceptance report is valid: $ExpectedMessageCount messages per implementation."
