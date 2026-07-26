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
    $stalePath = Join-Path $tempRoot 'stale.json'
    $workerSid = 'S-1-5-82-1-2-3-4-5'
    $collectorInvocationId = 'collector-test-001'
    $nowUtc = [DateTimeOffset]::UtcNow

    [pscustomobject]@{
        probeVersion = 'sec18-test'
        observedUtc = $nowUtc.AddSeconds(-5).ToString('o')
        transport = 'local'
        impersonationSucceeded = $true
        callerSid = $workerSid
        correlationId = $collectorInvocationId
    } | ConvertTo-Json | Set-Content -LiteralPath $matchingPath -Encoding UTF8

    [pscustomobject]@{
        probeVersion = 'sec18-test'
        observedUtc = [DateTime]::UtcNow.ToString('o')
        transport = 'local'
        impersonationSucceeded = $true
        callerSid = 'S-1-5-21-unauthorized'
        correlationId = $collectorInvocationId
    } | ConvertTo-Json | Set-Content -LiteralPath $mismatchedPath -Encoding UTF8

    [pscustomobject]@{
        probeVersion = 'sec18-test'
        observedUtc = $nowUtc.AddSeconds(-301).ToString('o')
        transport = 'local'
        impersonationSucceeded = $true
        callerSid = $workerSid
        correlationId = $collectorInvocationId
    } | ConvertTo-Json | Set-Content -LiteralPath $stalePath -Encoding UTF8

    $matchingEvidence = Get-CallerTokenEvidence -Path $matchingPath -ExpectedWorkerSids @($workerSid) -ExpectedCollectorInvocationId $collectorInvocationId -NowUtc $nowUtc -MaxAgeSeconds 300
    Assert-True $matchingEvidence.Present 'matching caller evidence must be present.'
    Assert-True $matchingEvidence.Valid 'matching caller evidence must validate.'
    Assert-True $matchingEvidence.MatchesWorkerSid 'matching caller evidence must match the worker SID.'
    Assert-True $matchingEvidence.TimestampFresh 'matching caller evidence must be fresh.'
    Assert-True $matchingEvidence.CorrelationMatchesCollectorInvocation 'matching caller evidence must correlate to the collector invocation.'

    $mismatchedEvidence = Get-CallerTokenEvidence -Path $mismatchedPath -ExpectedWorkerSids @($workerSid) -ExpectedCollectorInvocationId 'collector-test-other' -NowUtc $nowUtc -MaxAgeSeconds 300
    Assert-True $mismatchedEvidence.Present 'mismatched caller evidence must be present.'
    Assert-True (-not $mismatchedEvidence.Valid) 'mismatched caller evidence must be denied.'
    Assert-True (-not $mismatchedEvidence.MatchesWorkerSid) 'mismatched caller evidence must not match the worker SID.'

    $staleEvidence = Get-CallerTokenEvidence -Path $stalePath -ExpectedWorkerSids @($workerSid) -ExpectedCollectorInvocationId $collectorInvocationId -NowUtc $nowUtc -MaxAgeSeconds 300
    Assert-True $staleEvidence.Present 'stale caller evidence must be present.'
    Assert-True (-not $staleEvidence.Valid) 'stale caller evidence must be denied.'
    Assert-True (-not $staleEvidence.TimestampFresh) 'stale caller evidence must fail the freshness gate.'

    $existingProcessFunction = Get-Item -LiteralPath 'Function:\Get-Process' -ErrorAction SilentlyContinue
    try {
        Set-Item -LiteralPath 'Function:\Get-Process' -Value { throw 'synthetic process enumeration failure' }
        $serviceEvidence = Get-HMailServerServiceEvidence
        Assert-True (-not [string]::IsNullOrWhiteSpace([string]$serviceEvidence.ProcessReadError)) 'process enumeration errors must be captured.'
        Assert-True (-not [string]::IsNullOrWhiteSpace([string]$serviceEvidence.ReadError)) 'any process enumeration error must fail the aggregate read gate.'
        Assert-True ($null -eq $serviceEvidence.ProcessPresent) 'process presence must be unknown, not false, after a read error.'
    }
    finally {
        if ($null -eq $existingProcessFunction) {
            Remove-Item -LiteralPath 'Function:\Get-Process' -Force -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -LiteralPath 'Function:\Get-Process' -Value $existingProcessFunction.ScriptBlock
        }
    }

    Write-Output 'PASS: virtual-account construction, caller freshness/correlation, SID resolution failure, and fail-closed service reads.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
