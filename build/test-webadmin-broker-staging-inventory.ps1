[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "FAIL: $Message"
    }
}

$subjectPath = Join-Path $PSScriptRoot 'get-webadmin-broker-staging-inventory.ps1'
$null = . $subjectPath -WebAdminPath $PSScriptRoot

$expectedPoolName = 'HMailWebAdminBrokerPool'
$expectedIdentityName = 'IIS AppPool\HMailWebAdminBrokerPool'
Assert-True ((Get-ApplicationPoolIdentityName -PoolName $expectedPoolName) -ceq $expectedIdentityName) 'application-pool virtual-account construction must use one separator.'

$resolvedSystemSid = Resolve-AccountSid -AccountName 'NT AUTHORITY\SYSTEM'
Assert-True (-not [string]::IsNullOrWhiteSpace($resolvedSystemSid)) 'a valid well-known account must resolve to a SID.'

$unresolvedPoolSid = Resolve-AccountSid -AccountName 'IIS AppPool\SEC18-Definitely-Missing-Pool'
Assert-True ([string]::IsNullOrWhiteSpace($unresolvedPoolSid)) 'an unresolved application-pool account must fail closed.'

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('sec18-collector-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    $matchingPath = Join-Path $tempRoot 'matching.json'
    $mismatchedPath = Join-Path $tempRoot 'mismatched.json'
    $workerSid = 'S-1-5-82-1-2-3-4-5'

    [pscustomobject]@{
        probeVersion = 'sec18-test'
        observedUtc = [DateTime]::UtcNow.ToString('o')
        transport = 'local'
        impersonationSucceeded = $true
        callerSid = $workerSid
    } | ConvertTo-Json | Set-Content -LiteralPath $matchingPath -Encoding UTF8

    [pscustomobject]@{
        probeVersion = 'sec18-test'
        observedUtc = [DateTime]::UtcNow.ToString('o')
        transport = 'local'
        impersonationSucceeded = $true
        callerSid = 'S-1-5-21-unauthorized'
    } | ConvertTo-Json | Set-Content -LiteralPath $mismatchedPath -Encoding UTF8

    $matchingEvidence = Get-CallerTokenEvidence -Path $matchingPath -ExpectedWorkerSids @($workerSid)
    Assert-True $matchingEvidence.Present 'matching caller evidence must be present.'
    Assert-True $matchingEvidence.Valid 'matching caller evidence must validate.'
    Assert-True $matchingEvidence.MatchesWorkerSid 'matching caller evidence must match the worker SID.'

    $mismatchedEvidence = Get-CallerTokenEvidence -Path $mismatchedPath -ExpectedWorkerSids @($workerSid)
    Assert-True $mismatchedEvidence.Present 'mismatched caller evidence must be present.'
    Assert-True (-not $mismatchedEvidence.Valid) 'mismatched caller evidence must be denied.'
    Assert-True (-not $mismatchedEvidence.MatchesWorkerSid) 'mismatched caller evidence must not match the worker SID.'

    Write-Output 'PASS: virtual-account construction, SID resolution failure, and caller evidence matching gates.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
