[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MatrixReportPath,

    [Parameter(Mandatory = $true)]
    [string]$AuthorizedEvidencePath,

    [string]$AuthorizedResponsePath,

    [Parameter(Mandatory = $true)]
    [string]$WrongSidEvidencePath,

    [string]$WrongSidResponsePath,

    [Parameter(Mandatory = $true)]
    [string]$NonPoolEvidencePath,

    [Parameter(Mandatory = $true)]
    [string]$ProcessEvidencePath,

    [Parameter(Mandatory = $true)]
    [string]$CollectorPath,

    [Parameter(Mandatory = $true)]
    [string]$CleanupPath,

    [Parameter(Mandatory = $true)]
    [string]$BaselineGraphPath,

    [Parameter(Mandatory = $true)]
    [string]$PostGraphPath,

    [string]$OutputPath,

    [switch]$FailOnIncomplete
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Evidence file does not exist: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Has-Property {
    param(
        [object]$Object,
        [string]$Name
    )

    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Get-SnapshotHash {
    param([object]$Evidence)

    if (-not (Has-Property $Evidence 'Snapshots')) {
        return $null
    }

    $canonical = $Evidence.Snapshots | ConvertTo-Json -Depth 30 -Compress
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $script:checks.Add([pscustomobject]@{
            Name = $Name
            Passed = $Passed
            Detail = $Detail
        })
}

$checks = New-Object 'System.Collections.Generic.List[object]'
$matrix = Read-JsonFile $MatrixReportPath
$authorized = Read-JsonFile $AuthorizedEvidencePath
$authorizedResponse = $null
if (-not [string]::IsNullOrWhiteSpace($AuthorizedResponsePath)) {
    $authorizedResponse = Read-JsonFile $AuthorizedResponsePath
}
$wrongSid = Read-JsonFile $WrongSidEvidencePath
$wrongSidResponse = $null
if (-not [string]::IsNullOrWhiteSpace($WrongSidResponsePath)) {
    $wrongSidResponse = Read-JsonFile $WrongSidResponsePath
}
$nonPool = Read-JsonFile $NonPoolEvidencePath
$processes = Read-JsonFile $ProcessEvidencePath
$collector = Read-JsonFile $CollectorPath
$cleanup = Read-JsonFile $CleanupPath
$baselineGraph = Read-JsonFile $BaselineGraphPath
$postGraph = Read-JsonFile $PostGraphPath

$sourcePaths = @(
    $MatrixReportPath,
    $AuthorizedEvidencePath,
    $AuthorizedResponsePath,
    $WrongSidEvidencePath,
    $WrongSidResponsePath,
    $NonPoolEvidencePath,
    $ProcessEvidencePath,
    $CollectorPath,
    $CleanupPath,
    $BaselineGraphPath,
    $PostGraphPath
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique
$sourceHashes = foreach ($path in $sourcePaths) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        [pscustomobject]@{
            Path = $path
            Present = $true
            Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    else {
        [pscustomobject]@{
            Path = $path
            Present = $false
            Sha256 = $null
        }
    }
}

$matrixTests = @($matrix.tests)
$authorizedTest = $matrixTests | Where-Object { $_.name -eq 'authorized-real-php-fastcgi' } | Select-Object -First 1
$wrongSidTest = $matrixTests | Where-Object { $_.name -eq 'authorized-process-wrong-expected-sid' } | Select-Object -First 1
$nonPoolTest = $matrixTests | Where-Object { $_.name -eq 'genuine-nonpool-desktop-process' } | Select-Object -First 1

$authorizedCorrelation = Has-Property $authorized 'correlationId' -and [bool]$authorized.correlationId
$authorizedMatrixCorrelation = $authorizedCorrelation -and [string]::Equals(
    [string]$authorized.correlationId,
    [string]$authorizedTest.correlationId,
    [StringComparison]::Ordinal)
$authorizedResponseCorrelation = $null -eq $authorizedResponse -or (
    (Has-Property $authorizedResponse 'correlationId') -and
    [string]::Equals([string]$authorized.correlationId, [string]$authorizedResponse.correlationId, [StringComparison]::Ordinal))

$authorizedStageFields = @('activationHresult', 'interfaceHresult', 'methodHresult')
$authorizedStageSource = if ($null -ne $authorizedResponse) { $authorizedResponse } else { $authorizedTest.serverEvidence }
$authorizedStageFieldsPresent = @($authorizedStageFields | Where-Object {
        Has-Property $authorizedStageSource $_
    }).Count -eq $authorizedStageFields.Count

$poolSid = if (Has-Property $matrix.runtime 'poolSid') { [string]$matrix.runtime.poolSid } else { $null }
$authorizedSidBound = $null -ne $authorized -and
    [string]::Equals([string]$authorized.callerSid, [string]$authorized.expectedSid, [StringComparison]::OrdinalIgnoreCase) -and
    [string]::Equals([string]$authorized.callerSid, $poolSid, [StringComparison]::OrdinalIgnoreCase)
$authorizedTokenSteps = [int]$authorized.coImpersonateClientHresult -eq 0 -and
    [int]$authorized.openThreadTokenError -eq 0 -and
    [int]$authorized.coRevertToSelfHresult -eq 0 -and
    [int]$authorized.residualTokenError -eq 1008

Add-Check 'source-files-present' (@($sourceHashes | Where-Object { -not $_.Present }).Count -eq 0) 'Every attested source file exists.'
Add-Check 'authorized-correlation-bound' ($authorizedCorrelation -and $authorizedMatrixCorrelation -and $authorizedResponseCorrelation) 'The authorized server and response records share one non-empty correlation id.'
Add-Check 'authorized-effective-sid' $authorizedSidBound 'The authorized server caller SID matches the configured pool SID.'
Add-Check 'authorized-token-steps' $authorizedTokenSteps 'Impersonation, token read, revert, and residual-token cleanup are exact.'
Add-Check 'authorized-stage-hresults' $authorizedStageFieldsPresent 'Activation, interface, and method HRESULTs are explicitly captured in the authorized response record.'
Add-Check 'wrong-sid-method-denial' (
    $null -ne $wrongSid -and
    [bool]$wrongSid.sidMatchesExpected -eq $false -and
    [int]$wrongSid.errorHresult -eq -2147024891 -and
    [int]$wrongSid.invocationCount -gt 0 -and
    [bool]$wrongSid.correlationId) 'The wrong-expected-SID method denial is bound to one server invocation and correlation id.'
Add-Check 'nonpool-activation-denial' (
    [string]$nonPool.activationHresultHex -eq '0x80070005' -and
    [int]$nonPool.invocationCountDelta -eq 0 -and
    [bool]$nonPool.methodReached -eq $false -and
    -not [string]::IsNullOrWhiteSpace([string]$nonPool.processImage)) 'The non-pool process is denied before interface/method entry with no counter advance.'
Add-Check 'nonpool-client-correlation' (
    (Has-Property $nonPool 'clientRecordId') -and
    (Has-Property $nonPoolTest 'clientRecordId') -and
    [string]::Equals([string]$nonPool.clientRecordId, [string]$nonPoolTest.clientRecordId, [StringComparison]::Ordinal) -and
    -not [string]::IsNullOrWhiteSpace([string]$nonPool.clientRecordId)) 'The non-pool client record has an explicit evidence correlation identifier.'
Add-Check 'collector-caller-token' (
    [bool]$collector.CallerTokenEvidence.Valid -and
    [bool]$collector.Gate.CallerTokenMatchesWorkerSid -and
    [bool]$collector.Gate.DedicatedPoolCandidate) 'The elevated collector links the caller SID to the dedicated IIS pool.'
Add-Check 'cleanup-verified' (
    [bool]$cleanup.productionApplicationTouched -eq $false -and
    [bool]$cleanup.servicePresent -eq $false -and
    @($cleanup.probeProcess).Count -eq 0 -and
    @($cleanup.registry | Where-Object { $_.Present }).Count -eq 0 -and
    @($cleanup.paths | Where-Object { $_.Present }).Count -eq 0) 'Temporary service, process, registry objects, endpoints, and probe paths are absent.'
$baselineHash = Get-SnapshotHash $baselineGraph
$postHash = Get-SnapshotHash $postGraph
Add-Check 'installed-application-graph-unchanged' (
    [int]$baselineGraph.GraphPathCount -eq 22 -and
    [int]$postGraph.GraphPathCount -eq 22 -and
    [int]$baselineGraph.SnapshotCount -eq 44 -and
    [int]$postGraph.SnapshotCount -eq 44 -and
    [string]::Equals($baselineHash, $postHash, [StringComparison]::OrdinalIgnoreCase)) "Installed Application graph snapshot hash is unchanged: $baselineHash."

$ready = @($checks | Where-Object { -not $_.Passed }).Count -eq 0
$gateStatus = if ($ready) { 'EvidenceReadyForIndependentReview' } else { 'Incomplete' }
$gateReason = if ($ready) {
    'All stage, identity, counter, cleanup, and graph attestation checks passed; independent security and reality review is still required.'
}
else {
    'One or more immutable stage-bound evidence requirements failed; broker registration is blocked.'
}
$sourceHashesArray = @($sourceHashes)
$checksArray = @($checks | ForEach-Object {
        [pscustomobject]@{
            Name = [string]$_.Name
            Passed = [bool]$_.Passed
            Detail = [string]$_.Detail
        }
    })
$graphHashes = New-Object PSObject
$graphHashes | Add-Member -MemberType NoteProperty -Name Baseline -Value $baselineHash
$graphHashes | Add-Member -MemberType NoteProperty -Name Post -Value $postHash
$gate = New-Object PSObject
$gate | Add-Member -MemberType NoteProperty -Name EvidenceReadyForIndependentReview -Value $ready
$gate | Add-Member -MemberType NoteProperty -Name ReadyForBrokerRegistration -Value $false
$gate | Add-Member -MemberType NoteProperty -Name Status -Value $gateStatus
$gate | Add-Member -MemberType NoteProperty -Name Reason -Value $gateReason
$report = New-Object PSObject
$report | Add-Member -MemberType NoteProperty -Name SchemaVersion -Value 1
$report | Add-Member -MemberType NoteProperty -Name EvidenceKind -Value 'SEC18-DenialEvidenceAttestation'
$report | Add-Member -MemberType NoteProperty -Name AttestedUtc -Value ([DateTime]::UtcNow.ToString('o'))
$report | Add-Member -MemberType NoteProperty -Name MatrixReport -Value $MatrixReportPath
$report | Add-Member -MemberType NoteProperty -Name SourceHashes -Value $sourceHashesArray
$report | Add-Member -MemberType NoteProperty -Name Checks -Value $checksArray
$report | Add-Member -MemberType NoteProperty -Name GraphHashes -Value $graphHashes
$report | Add-Member -MemberType NoteProperty -Name Gate -Value $gate

$json = $report | ConvertTo-Json -Depth 16
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $json
}
else {
    $outputDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($OutputPath))
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        throw "Output directory does not exist: $outputDirectory"
    }

    [System.IO.File]::WriteAllText($OutputPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output $OutputPath
}

if ($FailOnIncomplete -and -not $ready) {
    exit 2
}
