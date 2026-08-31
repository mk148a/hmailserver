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
$testOutputDirectory = $null
New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null

try {
    $poolSid = 'S-1-5-82-1-2-3-4-5'
    $authorizedCorrelation = 'fixture-authorized-001'
    $wrongCorrelation = 'fixture-wrong-002'
    $nonPoolRecord = 'fixture-nonpool-003'
    $collectorInvocationId = $authorizedCorrelation
    $collectorCollectedUtc = [DateTimeOffset]::UtcNow
    $callerObservedUtc = $collectorCollectedUtc.AddSeconds(-5)
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
    $collectorScriptPath = Join-Path $temporaryDirectory 'get-webadmin-broker-staging-inventory.ps1'
    $cleanupPath = Join-Path $temporaryDirectory 'cleanup.json'
    $badCleanupPath = Join-Path $temporaryDirectory 'bad-cleanup.json'
    $duplicateCleanupPath = Join-Path $temporaryDirectory 'duplicate-cleanup.json'
    $badCollectorPath = Join-Path $temporaryDirectory 'bad-collector.json'
    $badApplicationIdCollectorPath = Join-Path $temporaryDirectory 'bad-application-id-collector.json'
    $badServiceCollectorPath = Join-Path $temporaryDirectory 'bad-service-collector.json'
    $badServiceOnlyCollectorPath = Join-Path $temporaryDirectory 'bad-service-only-collector.json'
    $rollbackPath = Join-Path $temporaryDirectory 'rollback-sec18-nonpool-probe-20260722.ps1'
    $baselinePath = Join-Path $temporaryDirectory 'baseline.json'
    $postPath = Join-Path $temporaryDirectory 'post.json'
    $matrixPath = Join-Path $temporaryDirectory 'matrix.json'
    $badMatrixPath = Join-Path $temporaryDirectory 'bad-matrix.json'
    $evidenceRoot = Join-Path $PSScriptRoot '..\artifacts\sec18-staging'
    $testOutputDirectory = Join-Path $evidenceRoot ('test-attestation-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $testOutputDirectory -Force | Out-Null
    $goodOutputPath = Join-Path $testOutputDirectory 'good-output.json'
    $badOutputPath = Join-Path $testOutputDirectory 'bad-output.json'
    $badCleanupOutputPath = Join-Path $testOutputDirectory 'bad-cleanup-output.json'
    $badCollectorOutputPath = Join-Path $testOutputDirectory 'bad-collector-output.json'
    $badApplicationIdCollectorOutputPath = Join-Path $testOutputDirectory 'bad-application-id-collector-output.json'
    $badServiceCollectorOutputPath = Join-Path $testOutputDirectory 'bad-service-collector-output.json'
    $badServiceOnlyCollectorOutputPath = Join-Path $testOutputDirectory 'bad-service-only-collector-output.json'
    $badWrongResponsePath = Join-Path $temporaryDirectory 'bad-wrong-response.json'
    $badWrongOutputPath = Join-Path $testOutputDirectory 'bad-wrong-output.json'
    $badWrongSidPath = Join-Path $temporaryDirectory 'bad-wrong-sid.json'
    $badWrongSidOutputPath = Join-Path $testOutputDirectory 'bad-wrong-sid-output.json'
    $badProcessPath = Join-Path $temporaryDirectory 'bad-process.json'
    $badProcessOutputPath = Join-Path $testOutputDirectory 'bad-process-output.json'
    $badGraphPath = Join-Path $temporaryDirectory 'bad-graph.json'
    $badGraphOutputPath = Join-Path $testOutputDirectory 'bad-graph-output.json'
    $badAuthorizedResponsePath = Join-Path $temporaryDirectory 'bad-authorized-response.json'
    $badAuthorizedOutputPath = Join-Path $testOutputDirectory 'bad-authorized-output.json'

    'fixture rollback script' | Set-Content -LiteralPath $rollbackPath -Encoding UTF8
    'fixture collector script' | Set-Content -LiteralPath $collectorScriptPath -Encoding UTF8
    $rollbackHash = (Get-FileHash -LiteralPath $rollbackPath -Algorithm SHA256).Hash

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
    Write-JsonFixture $processPath @(
        [pscustomobject]@{ Name = 'php-cgi.exe'; UserSid = $poolSid }
        [pscustomobject]@{ Name = 'powershell.exe'; Path = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'; UserSid = 'S-1-5-21-unauthorized' }
    )
    Write-JsonFixture $collectorPath ([pscustomobject]@{
            CollectedUtc = $collectorCollectedUtc.ToString('o')
            CollectorInvocationId = $collectorInvocationId
            ApplicationAppId = '{5EDEC473-39E0-43F6-A234-1947071721C8}'
            CallerEvidenceMaxAgeSeconds = 300
            CallerTokenEvidence = [pscustomobject]@{
                Valid = $true
                ObservedUtc = $callerObservedUtc.ToString('o')
                TimestampParseable = $true
                TimestampFresh = $true
                CorrelationId = $authorizedCorrelation
                CorrelationMatchesCollectorInvocation = $true
            }
            HMailServerService = [pscustomobject]@{
                Name = 'hMailServer'
                Present = $true
                Status = 1
                StatusName = 'Stopped'
                StartType = 4
                StartTypeName = 'Disabled'
                ProcessPresent = $false
                ProcessIds = @()
                ServiceReadError = $null
                ProcessReadError = $null
                ReadError = $null
            }
            Gate = [pscustomobject]@{ CallerTokenMatchesWorkerSid = $true; DedicatedPoolCandidate = $true; HMailServerServiceSafe = $true }
        })
    Write-JsonFixture $cleanupPath ([pscustomobject]@{
            productionApplicationTouched = $false
            servicePresent = $false
            probeProcess = @()
            rollbackExitCode = 0
            rollbackScript = 'artifacts/sec18-staging/rollback-sec18-nonpool-probe-20260722.ps1'
            rollbackScriptSha256 = $rollbackHash
            registry = @(
                [pscustomobject]@{ View = 'Registry64'; Key = 'SOFTWARE\Classes\AppID\{A5F0D0A4-1B58-4D84-9E0D-9D5A7C8C8A53}'; Present = $false }
                [pscustomobject]@{ View = 'Registry64'; Key = 'SOFTWARE\Classes\CLSID\{D1E02B68-7A62-4C4B-B5D4-7DA8C26C0B48}'; Present = $false }
                [pscustomobject]@{ View = 'Registry64'; Key = 'SOFTWARE\Classes\SEC18.CallerProbe'; Present = $false }
                [pscustomobject]@{ View = 'Registry64'; Key = 'SOFTWARE\Classes\SEC18.CallerProbe.1'; Present = $false }
                [pscustomobject]@{ View = 'Registry32'; Key = 'SOFTWARE\Classes\AppID\{A5F0D0A4-1B58-4D84-9E0D-9D5A7C8C8A53}'; Present = $false }
                [pscustomobject]@{ View = 'Registry32'; Key = 'SOFTWARE\Classes\CLSID\{D1E02B68-7A62-4C4B-B5D4-7DA8C26C0B48}'; Present = $false }
                [pscustomobject]@{ View = 'Registry32'; Key = 'SOFTWARE\Classes\SEC18.CallerProbe'; Present = $false }
                [pscustomobject]@{ View = 'Registry32'; Key = 'SOFTWARE\Classes\SEC18.CallerProbe.1'; Present = $false }
            )
            paths = @(
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\Probe'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\ProbeSource'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\ProbeBuild'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\ProbeSourceFx'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\WebAdmin\sec18-pool-direct-com.php'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\WebAdmin\sec18-pool-direct-com-wrong-sid.php'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\WebAdmin\caller-probe-diagnostics.php'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\WebAdmin\sec18-worker-identity.php'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\WebAdmin\sec18-identity.php'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\nonpool-client-attested.ps1'; Present = $false }
                [pscustomobject]@{ Path = 'C:\SEC18-Staging\nonpool-client-attested.json'; Present = $false }
            )
            hMailService = [pscustomobject]@{ Name = 'hMailServer'; Status = 1; StartType = 4 }
        })
    $badCleanup = Get-Content -LiteralPath $cleanupPath -Raw | ConvertFrom-Json
    $badCleanup.rollbackExitCode = 1
    $badCleanup.paths[0].Present = $true
    Write-JsonFixture $badCleanupPath $badCleanup
    $badCollector = Get-Content -LiteralPath $collectorPath -Raw | ConvertFrom-Json
    $badCollector.CallerTokenEvidence.ObservedUtc = $collectorCollectedUtc.AddSeconds(-301).ToString('o')
    $badCollector.CallerTokenEvidence.TimestampFresh = $false
    Write-JsonFixture $badCollectorPath $badCollector
    $badApplicationIdCollector = Get-Content -LiteralPath $collectorPath -Raw | ConvertFrom-Json
    $badApplicationIdCollector.ApplicationAppId = '{00000000-0000-0000-0000-000000000000}'
    Write-JsonFixture $badApplicationIdCollectorPath $badApplicationIdCollector
    '{"schemaVersion":1,"schemaVersion":2}' | Set-Content -LiteralPath $duplicateCleanupPath -Encoding UTF8
    $graphKeyPaths = @(
        'Software\Classes\hMailServer.Application.1',
        'Software\Classes\hMailServer.Application.1\CLSID',
        'Software\Classes\hMailServer.Application',
        'Software\Classes\hMailServer.Application\CLSID',
        'Software\Classes\hMailServer.Application\CurVer',
        'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}',
        'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\ProgID',
        'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\VersionIndependentProgID',
        'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\Programmable',
        'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\LocalServer32',
        'Software\Classes\CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}\TypeLib',
        'Software\Classes\AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}',
        'Software\Classes\AppID\hMailServer.EXE',
        'Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}',
        'Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0',
        'Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0',
        'Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0\win64',
        'Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\FLAGS',
        'Software\Classes\TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\HELPDIR',
        'Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}',
        'Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}\ProxyStubClsid32',
        'Software\Classes\Interface\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}\TypeLib'
    )
    $collectorScript = Join-Path $PSScriptRoot 'get-sec18-installed-application-graph-evidence.ps1'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $collectorScript -OutputPath $baselinePath -ExpectedModulePath 'C:\hMailServer57-Test\Bin\hMailServer.exe' -OfflineFixture | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) 'the canonical graph collector fixture must be generated successfully.'
    $graphEvidence = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    Write-JsonFixture $postPath $graphEvidence
    $badGraph = $graphEvidence | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $badGraph.Snapshots[0].Values[0].RawBytesBase64 = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes('Tampered' + [char]0))
    Write-JsonFixture $badGraphPath $badGraph

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
        '-CollectorScriptPath', $collectorScriptPath,
        '-CleanupPath', $cleanupPath,
        '-RollbackScriptPath', $rollbackPath,
        '-BaselineGraphPath', $baselinePath,
        '-PostGraphPath', $postPath
    )
    & powershell.exe @commonArguments '-OutputPath' $goodOutputPath '-FailOnIncomplete' | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) 'complete attestation fixture must pass.'
    $good = Get-Content -LiteralPath $goodOutputPath -Raw | ConvertFrom-Json
    Assert-True ([bool]$good.Gate.EvidenceReadyForIndependentReview) 'complete fixture must be review-ready.'
    Assert-True (@($good.Checks).Count -eq 21) 'attestation must emit all twenty-one checks as an array.'
    Assert-True ($good.SourceHashes.Count -eq 14) 'attestation must hash every source file and verifier script.'

    Write-JsonFixture $badAuthorizedResponsePath ([pscustomobject]@{
            correlationId = $authorizedCorrelation
            activationHresult = -2147024891
            interfaceHresult = 0
            methodHresult = 0
        })
    $badAuthorizedArguments = $commonArguments.Clone()
    $badAuthorizedArguments[$badAuthorizedArguments.IndexOf('-AuthorizedResponsePath') + 1] = $badAuthorizedResponsePath
    $badAuthorizedArguments += @('-OutputPath', $badAuthorizedOutputPath, '-FailOnIncomplete')
    & powershell.exe @badAuthorizedArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'non-S_OK authorized HRESULTs must fail closed with exit 2.'
    $badAuthorizedReport = Get-Content -LiteralPath $badAuthorizedOutputPath -Raw | ConvertFrom-Json
    $authorizedHresultCheck = $badAuthorizedReport.Checks | Where-Object { $_.Name -eq 'authorized-stage-hresults' }
    Assert-True (-not [bool]$authorizedHresultCheck.Passed) 'non-S_OK authorized HRESULTs must fail their check.'

    $missingAuthorizedArguments = @()
    for ($argumentIndex = 0; $argumentIndex -lt $commonArguments.Count; $argumentIndex++) {
        if ($commonArguments[$argumentIndex] -eq '-AuthorizedResponsePath') {
            $argumentIndex++
            continue
        }
        $missingAuthorizedArguments += $commonArguments[$argumentIndex]
    }
    $missingAuthorizedArguments += @('-OutputPath', (Join-Path $testOutputDirectory 'missing-authorized-output.json'), '-FailOnIncomplete')
    & powershell.exe @missingAuthorizedArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'a missing authorized response must fail closed with exit 2.'

    $badWrongSid = Get-Content -LiteralPath $wrongPath -Raw | ConvertFrom-Json
    $badWrongSid.expectedSid = $badWrongSid.callerSid
    Write-JsonFixture $badWrongSidPath $badWrongSid
    $badWrongSidArguments = $commonArguments.Clone()
    $badWrongSidArguments[$badWrongSidArguments.IndexOf('-WrongSidEvidencePath') + 1] = $badWrongSidPath
    $badWrongSidArguments += @('-OutputPath', $badWrongSidOutputPath, '-FailOnIncomplete')
    & powershell.exe @badWrongSidArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'a wrong-SID fixture with equal caller and expected SID must fail closed with exit 2.'
    $badWrongSidReport = Get-Content -LiteralPath $badWrongSidOutputPath -Raw | ConvertFrom-Json
    $wrongSidIdentityCheck = $badWrongSidReport.Checks | Where-Object { $_.Name -eq 'wrong-sid-method-denial' }
    Assert-True (-not [bool]$wrongSidIdentityCheck.Passed) 'equal caller and expected SIDs must fail the wrong-SID method check.'

    Write-JsonFixture $badProcessPath @([pscustomobject]@{ Name = 'php-cgi.exe'; UserSid = $poolSid })
    $badProcessArguments = $commonArguments.Clone()
    $badProcessArguments[$badProcessArguments.IndexOf('-ProcessEvidencePath') + 1] = $badProcessPath
    $badProcessArguments += @('-OutputPath', $badProcessOutputPath, '-FailOnIncomplete')
    & powershell.exe @badProcessArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'a non-pool denial without matching non-pool token evidence must fail closed with exit 2.'
    $badProcessReport = Get-Content -LiteralPath $badProcessOutputPath -Raw | ConvertFrom-Json
    $nonPoolTokenCheck = $badProcessReport.Checks | Where-Object { $_.Name -eq 'nonpool-process-token' }
    Assert-True (-not [bool]$nonPoolTokenCheck.Passed) 'missing non-pool process token evidence must fail its check.'

    $badGraphArguments = $commonArguments.Clone()
    $badGraphArguments[$badGraphArguments.IndexOf('-BaselineGraphPath') + 1] = $badGraphPath
    $badGraphArguments[$badGraphArguments.IndexOf('-PostGraphPath') + 1] = $badGraphPath
    $badGraphArguments += @('-OutputPath', $badGraphOutputPath, '-FailOnIncomplete')
    & powershell.exe @badGraphArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'a graph with matching counts, hash, and collector flags but a tampered raw value must fail closed with exit 2.'
    $badGraphReport = Get-Content -LiteralPath $badGraphOutputPath -Raw | ConvertFrom-Json
    $graphCheck = $badGraphReport.Checks | Where-Object { $_.Name -eq 'installed-application-graph-unchanged' }
    Assert-True (-not [bool]$graphCheck.Passed) 'a tampered raw registry value must fail the installed graph check.'

    $duplicateOutputArguments = $commonArguments.Clone()
    $duplicateOutputArguments += @('-OutputPath', $goodOutputPath)
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & powershell.exe @duplicateOutputArguments 2>$null | Out-Null
    $duplicateOutputExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    Assert-True ($duplicateOutputExitCode -ne 0) 'attestation must not overwrite an existing evidence report.'

    $outsideOutputPath = Join-Path $temporaryDirectory 'outside-output.json'
    $outsideOutputArguments = $commonArguments.Clone()
    $outsideOutputArguments += @('-OutputPath', $outsideOutputPath)
    $ErrorActionPreference = 'Continue'
    & powershell.exe @outsideOutputArguments 2>$null | Out-Null
    $outsideOutputExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    Assert-True ($outsideOutputExitCode -ne 0) 'attestation must reject output outside the SEC-18 evidence root.'
    Assert-True (-not (Test-Path -LiteralPath $outsideOutputPath)) 'rejected output outside the evidence root must not be created.'

    $badCollectorArguments = $commonArguments.Clone()
    $badCollectorArguments[$badCollectorArguments.IndexOf('-CollectorPath') + 1] = $badCollectorPath
    $badCollectorArguments += @('-OutputPath', $badCollectorOutputPath, '-FailOnIncomplete')
    & powershell.exe @badCollectorArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'stale caller-token evidence must fail closed with exit 2.'
    $badCollectorReport = Get-Content -LiteralPath $badCollectorOutputPath -Raw | ConvertFrom-Json
    $callerFreshnessCheck = $badCollectorReport.Checks | Where-Object { $_.Name -eq 'collector-caller-freshness-correlation' }
    Assert-True (-not [bool]$callerFreshnessCheck.Passed) 'stale caller-token evidence must fail its freshness/correlation check.'

    $badApplicationIdArguments = $commonArguments.Clone()
    $badApplicationIdArguments[$badApplicationIdArguments.IndexOf('-CollectorPath') + 1] = $badApplicationIdCollectorPath
    $badApplicationIdArguments += @('-OutputPath', $badApplicationIdCollectorOutputPath, '-FailOnIncomplete')
    & powershell.exe @badApplicationIdArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'a non-canonical collector Application AppID must fail closed with exit 2.'
    $badApplicationIdReport = Get-Content -LiteralPath $badApplicationIdCollectorOutputPath -Raw | ConvertFrom-Json
    $applicationAppIdCheck = $badApplicationIdReport.Checks | Where-Object { $_.Name -eq 'collector-application-appid' }
    Assert-True (-not [bool]$applicationAppIdCheck.Passed) 'a non-canonical collector Application AppID must fail its binding check.'

    $badServiceCollector = Get-Content -LiteralPath $collectorPath -Raw | ConvertFrom-Json
    $badServiceCollector.HMailServerService.ProcessReadError = 'synthetic process read failure'
    $badServiceCollector.HMailServerService.ReadError = 'synthetic process read failure'
    Write-JsonFixture $badServiceCollectorPath $badServiceCollector
    $badServiceArguments = $commonArguments.Clone()
    $badServiceArguments[$badServiceArguments.IndexOf('-CollectorPath') + 1] = $badServiceCollectorPath
    $badServiceArguments += @('-OutputPath', $badServiceCollectorOutputPath, '-FailOnIncomplete')
    & powershell.exe @badServiceArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'process read errors must fail closed with exit 2.'
    $badServiceReport = Get-Content -LiteralPath $badServiceCollectorOutputPath -Raw | ConvertFrom-Json
    $serviceReadCheck = $badServiceReport.Checks | Where-Object { $_.Name -eq 'collector-service-read-fail-closed' }
    Assert-True (-not [bool]$serviceReadCheck.Passed) 'process read errors must fail their fail-closed check.'

    $badServiceOnlyCollector = Get-Content -LiteralPath $collectorPath -Raw | ConvertFrom-Json
    $badServiceOnlyCollector.HMailServerService.ServiceReadError = 'synthetic service read failure'
    Write-JsonFixture $badServiceOnlyCollectorPath $badServiceOnlyCollector
    $badServiceOnlyArguments = $commonArguments.Clone()
    $badServiceOnlyArguments[$badServiceOnlyArguments.IndexOf('-CollectorPath') + 1] = $badServiceOnlyCollectorPath
    $badServiceOnlyArguments += @('-OutputPath', $badServiceOnlyCollectorOutputPath, '-FailOnIncomplete')
    & powershell.exe @badServiceOnlyArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'service-only read errors must fail closed with exit 2.'
    $badServiceOnlyReport = Get-Content -LiteralPath $badServiceOnlyCollectorOutputPath -Raw | ConvertFrom-Json
    $serviceOnlyReadCheck = $badServiceOnlyReport.Checks | Where-Object { $_.Name -eq 'collector-service-read-fail-closed' }
    Assert-True (-not [bool]$serviceOnlyReadCheck.Passed) 'service-only read errors must fail their fail-closed check.'

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

    $badWrongResponse = [pscustomobject]@{ correlationId = 'fixture-wrong-other' }
    Write-JsonFixture $badWrongResponsePath $badWrongResponse
    $badWrongArguments = $commonArguments.Clone()
    $badWrongArguments[$badWrongArguments.IndexOf('-WrongSidResponsePath') + 1] = $badWrongResponsePath
    $badWrongArguments += @('-OutputPath', $badWrongOutputPath, '-FailOnIncomplete')
    & powershell.exe @badWrongArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'wrong-SID correlation mismatch must fail closed with exit 2.'
    $badWrongReport = Get-Content -LiteralPath $badWrongOutputPath -Raw | ConvertFrom-Json
    $wrongCorrelationCheck = $badWrongReport.Checks | Where-Object { $_.Name -eq 'wrong-sid-correlation-bound' }
    Assert-True (-not [bool]$wrongCorrelationCheck.Passed) 'wrong-SID correlation mismatch must fail its check.'

    $badCleanupArguments = $commonArguments.Clone()
    $badCleanupArguments[$badCleanupArguments.IndexOf('-CleanupPath') + 1] = $badCleanupPath
    $badCleanupArguments += @('-OutputPath', $badCleanupOutputPath, '-FailOnIncomplete')
    & powershell.exe @badCleanupArguments | Out-Null
    Assert-True ($LASTEXITCODE -eq 2) 'incomplete cleanup fixture must fail closed with exit 2.'
    $badCleanupReport = Get-Content -LiteralPath $badCleanupOutputPath -Raw | ConvertFrom-Json
    $cleanupCheck = $badCleanupReport.Checks | Where-Object { $_.Name -eq 'cleanup-verified' }
    $provenanceCheck = $badCleanupReport.Checks | Where-Object { $_.Name -eq 'cleanup-provenance' }
    Assert-True (-not [bool]$cleanupCheck.Passed) 'incomplete cleanup coverage must fail its check.'
    Assert-True (-not [bool]$provenanceCheck.Passed) 'non-zero rollback must fail provenance check.'

    $duplicateArguments = $commonArguments.Clone()
    $duplicateArguments[$duplicateArguments.IndexOf('-CleanupPath') + 1] = $duplicateCleanupPath
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & powershell.exe @duplicateArguments 2>$null | Out-Null
    $duplicateExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    Assert-True ($duplicateExitCode -ne 0) 'duplicate JSON properties must be rejected.'

    Write-Output 'SEC-18 denial evidence attestation tests passed.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
    if ($null -ne $testOutputDirectory -and (Test-Path -LiteralPath $testOutputDirectory)) {
        Remove-Item -LiteralPath $testOutputDirectory -Recurse -Force
    }
}
