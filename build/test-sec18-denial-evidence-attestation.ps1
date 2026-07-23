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
        throw "Assertion failed: $Message"
    }
}

function Write-JsonFixture {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

$attester = Join-Path $PSScriptRoot 'attest-sec18-denial-evidence.ps1'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('sec18-attestation-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null

try {
    $poolSid = 'S-1-5-82-1-2-3-4-5'
    $authorizedCorrelation = 'fixture-authorized-001'
    $wrongCorrelation = 'fixture-wrong-002'
    $nonPoolRecord = 'fixture-nonpool-003'
    $authorizedServer = [pscustomobject]@{
        correlationId = $authorizedCorrelation
        expectedSid = $poolSid
        callerSid = $poolSid
        coImpersonateClientHresult = 0
        openThreadTokenError = 0
        sidComparisonHresult = 0
        coRevertToSelfHresult = 0
        residualTokenError = 1008
        activationHresult = 0
        interfaceHresult = 0
        methodHresult = 0
        invocationCount = 1
    }
    $wrongServer = [pscustomobject]@{
        correlationId = $wrongCorrelation
        expectedSid = 'S-1-5-21-unauthorized'
        callerSid = $poolSid
        sidMatchesExpected = $false
        errorHresult = -2147024891
        invocationCount = 2
    }
    $matrix = [pscustomobject]@{
        runtime = [pscustomobject]@{ poolSid = $poolSid }
        tests = @(
            [pscustomobject]@{ name = 'authorized-real-php-fastcgi'; correlationId = $authorizedCorrelation; clientStage = 'complete'; serverEvidence = $authorizedServer },
            [pscustomobject]@{ name = 'authorized-process-wrong-expected-sid'; correlationId = $wrongCorrelation; clientStage = 'method'; serverEvidence = $wrongServer },
            [pscustomobject]@{ name = 'genuine-nonpool-desktop-process'; clientRecordId = $nonPoolRecord; activationHresultHex = '0x80070005'; invocationCountDelta = 0; methodReached = $false; processImage = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' }
        )
    }
    $authorizedPath = Join-Path $temporaryDirectory 'authorized.json'
    $authorizedResponsePath = Join-Path $temporaryDirectory 'authorized-response.json'
    $wrongPath = Join-Path $temporaryDirectory 'wrong.json'
    $wrongResponsePath = Join-Path $temporaryDirectory 'wrong-response.json'
    $nonPoolPath = Join-Path $temporaryDirectory 'nonpool.json'
    $processPath = Join-Path $temporaryDirectory 'process.json'
    $collectorPath = Join-Path $temporaryDirectory 'collector.json'
    $cleanupPath = Join-Path $temporaryDirectory 'cleanup.json'
    $baselinePath = Join-Path $temporaryDirectory 'baseline.json'
    $postPath = Join-Path $temporaryDirectory 'post.json'
    $matrixPath = Join-Path $temporaryDirectory 'matrix.json'
    $badMatrixPath = Join-Path $temporaryDirectory 'bad-matrix.json'
    $goodOutputPath = Join-Path $temporaryDirectory 'good-output.json'
    $badOutputPath = Join-Path $temporaryDirectory 'bad-output.json'

    Write-JsonFixture $matrixPath $matrix
    Write-JsonFixture $authorizedPath $authorizedServer
    Write-JsonFixture $authorizedResponsePath ([pscustomobject]@{
            correlationId = $authorizedCorrelation
            activationHresult = 0
            interfaceHresult = 0
            methodHresult = 0
        })
    Write-JsonFixture $wrongPath $wrongServer
    Write-JsonFixture $wrongResponsePath ([pscustomobject]@{ correlationId = $wrongCorrelation })
    Write-JsonFixture $nonPoolPath ([pscustomobject]@{
            clientRecordId = $nonPoolRecord
            activationHresultHex = '0x80070005'
            invocationCountDelta = 0
            methodReached = $false
            processImage = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
        })
    Write-JsonFixture $processPath @([pscustomobject]@{ Name = 'php-cgi.exe'; UserSid = $poolSid })
    Write-JsonFixture $collectorPath ([pscustomobject]@{
            CallerTokenEvidence = [pscustomobject]@{ Valid = $true }
            Gate = [pscustomobject]@{ CallerTokenMatchesWorkerSid = $true; DedicatedPoolCandidate = $true }
        })
    Write-JsonFixture $cleanupPath ([pscustomobject]@{
            productionApplicationTouched = $false
            servicePresent = $false
            probeProcess = @()
            registry = @([pscustomobject]@{ Present = $false })
            paths = @([pscustomobject]@{ Present = $false })
        })
    Write-JsonFixture $baselinePath ([pscustomobject]@{ GraphPathCount = 22; SnapshotCount = 44; Snapshots = @([pscustomobject]@{ Key = 'same' }) })
    Write-JsonFixture $postPath ([pscustomobject]@{ GraphPathCount = 22; SnapshotCount = 44; Snapshots = @([pscustomobject]@{ Key = 'same' }) })

    $commonArguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $attester,
        '-MatrixReportPath', $matrixPath,
        '-AuthorizedEvidencePath', $authorizedPath,
        '-AuthorizedResponsePath', $authorizedResponsePath,
        '-WrongSidEvidencePath', $wrongPath,
        '-WrongSidResponsePath', $wrongResponsePath,
        '-NonPoolEvidencePath', $nonPoolPath,
        '-ProcessEvidencePath', $processPath,
        '-CollectorPath', $collectorPath,
        '-CleanupPath', $cleanupPath,
        '-BaselineGraphPath', $baselinePath,
        '-PostGraphPath', $postPath
    )
    & powershell.exe @commonArguments '-OutputPath' $goodOutputPath '-FailOnIncomplete' | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) 'complete attestation fixture must pass.'
    $good = Get-Content -LiteralPath $goodOutputPath -Raw | ConvertFrom-Json
    Assert-True ([bool]$good.Gate.EvidenceReadyForIndependentReview) 'complete fixture must be review-ready.'
    Assert-True (@($good.Checks).Count -eq 11) 'attestation must emit all eleven checks as an array.'
    Assert-True ($good.SourceHashes.Count -eq 11) 'attestation must hash every source file.'

    $bad = $matrix | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $badNonPool = $bad.tests | Where-Object { $_.name -eq 'genuine-nonpool-desktop-process' }
    $badNonPool.PSObject.Properties.Remove('clientRecordId')
    Write-JsonFixture $badMatrixPath $bad
    $badArguments = $commonArguments.Clone()
    $badArguments[$badArguments.IndexOf('-MatrixReportPath') + 1] = $badMatrixPath
    $badArguments += @('-OutputPath', $badOutputPath, '-FailOnIncomplete')
    & powershell.exe @badArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'incomplete attestation fixture must fail closed with exit 2.'
    $badReport = Get-Content -LiteralPath $badOutputPath -Raw | ConvertFrom-Json
    Assert-True (-not [bool]$badReport.Gate.EvidenceReadyForIndependentReview) 'incomplete fixture must not be review-ready.'

    Write-Output 'SEC-18 denial evidence attestation tests passed.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
