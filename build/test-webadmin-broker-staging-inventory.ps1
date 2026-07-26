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
$evidenceRoot = Get-Sec18EvidenceRoot -ScriptPath $subjectPath

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

    $allowedOutputPath = Join-Path $evidenceRoot ('collector-path-test-' + [Guid]::NewGuid().ToString('N') + '.json')
    $resolvedAllowedPath = Resolve-Sec18EvidenceOutputPath -RequestedPath $allowedOutputPath -EvidenceRoot $evidenceRoot
    Assert-True ([string]::Equals($resolvedAllowedPath, [IO.Path]::GetFullPath($allowedOutputPath), [StringComparison]::OrdinalIgnoreCase)) 'an unused output path under the evidence root must resolve.'
    $outsideOutputPath = Join-Path $tempRoot 'outside-output.json'
    $outsideRejected = $false
    try {
        Resolve-Sec18EvidenceOutputPath -RequestedPath $outsideOutputPath -EvidenceRoot $evidenceRoot | Out-Null
    }
    catch {
        $outsideRejected = $true
    }
    Assert-True $outsideRejected 'an output path outside the evidence root must be rejected.'
    New-Item -ItemType File -Path $allowedOutputPath -Force | Out-Null
    $existingRejected = $false
    try {
        Resolve-Sec18EvidenceOutputPath -RequestedPath $allowedOutputPath -EvidenceRoot $evidenceRoot | Out-Null
    }
    catch {
        $existingRejected = $true
    }
    Assert-True $existingRejected 'an existing evidence output path must be rejected.'
    Remove-Item -LiteralPath $allowedOutputPath -Force

    $invalidAppIdOutputPath = Join-Path $evidenceRoot ('collector-appid-test-' + [Guid]::NewGuid().ToString('N') + '.json')
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $subjectPath -WebAdminPath $PSScriptRoot -ApplicationAppId '{00000000-0000-0000-0000-000000000000}' -OutputPath $invalidAppIdOutputPath 2>$null | Out-Null
    $invalidAppIdExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    Assert-True ($invalidAppIdExitCode -ne 0) 'the collector must reject a non-canonical Application AppID.'
    Assert-True (-not (Test-Path -LiteralPath $invalidAppIdOutputPath)) 'a rejected Application AppID must not create evidence output.'
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
