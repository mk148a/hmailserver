$ErrorActionPreference = 'Stop'

function Assert-LiveEquivalenceEvidence {
    param([object]$Report)

    if ($Report.schema -ne 'paired-shared-baseline-v2') {
        throw "Unexpected shared-baseline schema: $($Report.schema)"
    }
    if ($Report.status -notin @('EQUIVALENT_START_STATE', 'NOT_EQUIVALENT')) {
        throw "Unexpected shared-baseline status: $($Report.status)"
    }
    $snapshot = $Report.databaseSnapshot
    if ($null -eq $snapshot -or $null -eq $snapshot.cppFixture -or $null -eq $snapshot.net10Fixture) {
        throw 'Shared-baseline report is missing SQL fixture evidence.'
    }
    foreach ($fixture in @($snapshot.cppFixture, $snapshot.net10Fixture)) {
        foreach ($property in @('available', 'fixtureValid', 'fullTextReady', 'domainMatches', 'accountMatches', 'inboxMatches', 'matchingLoopbackPorts', 'messageFilesOutsideDataRoot')) {
            if ($fixture.PSObject.Properties.Name -notcontains $property) {
                throw "SQL fixture evidence is missing '$property'."
            }
        }
    }
    if ($Report.status -eq 'EQUIVALENT_START_STATE' -and $snapshot.fixtureEvidenceEqual -ne $true) {
        throw 'Equivalent start state cannot be claimed without equal fixture evidence.'
    }
}

$good = [pscustomobject]@{
    schema = 'paired-shared-baseline-v2'
    status = 'NOT_EQUIVALENT'
    databaseSnapshot = [pscustomobject]@{
        fixtureEvidenceEqual = $false
        cppFixture = [pscustomobject]@{
            available = $true; fixtureValid = $false; fullTextReady = $false
            domainMatches = 1; accountMatches = 1; inboxMatches = 0
            matchingLoopbackPorts = 3; messageFilesOutsideDataRoot = 10
        }
        net10Fixture = [pscustomobject]@{
            available = $true; fixtureValid = $true; fullTextReady = $true
            domainMatches = 1; accountMatches = 1; inboxMatches = 1
            matchingLoopbackPorts = 3; messageFilesOutsideDataRoot = 0
        }
    }
}
Assert-LiveEquivalenceEvidence -Report $good

$bad = $good | ConvertTo-Json -Depth 8 | ConvertFrom-Json
$bad.databaseSnapshot.cppFixture.PSObject.Properties.Remove('fullTextReady')
try {
    Assert-LiveEquivalenceEvidence -Report $bad
    throw 'Expected missing Full-Text evidence to be rejected.'
}
catch {
    if ($_.Exception.Message -notmatch "missing 'fullTextReady'") {
        throw
    }
}

Write-Output 'Validated shared-baseline v2 fixture and Full-Text evidence gates, including fail-closed missing evidence.'
