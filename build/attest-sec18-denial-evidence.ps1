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
    [string]$RollbackScriptPath,

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

    $json = Get-Content -LiteralPath $Path -Raw
    Assert-NoDuplicateJsonProperties $json $Path
    return $json | ConvertFrom-Json
}

function Assert-NoDuplicateJsonProperties {
    param(
        [string]$Json,
        [string]$Path
    )

    $objectStack = New-Object 'System.Collections.Generic.List[hashtable]'
    $length = $Json.Length
    for ($i = 0; $i -lt $length; $i++) {
        $character = $Json[$i]
        if ($character -eq '"') {
            $start = $i
            $i++
            $escaped = $false
            for (; $i -lt $length; $i++) {
                if ($escaped) {
                    $escaped = $false
                    continue
                }
                if ($Json[$i] -eq '\\') {
                    $escaped = $true
                    continue
                }
                if ($Json[$i] -eq '"') {
                    break
                }
            }
            if ($i -ge $length) {
                throw "Invalid JSON string in evidence file: $Path"
            }

            $end = $i
            $lookahead = $i + 1
            while ($lookahead -lt $length -and [char]::IsWhiteSpace($Json[$lookahead])) {
                $lookahead++
            }
            if ($objectStack.Count -gt 0 -and $lookahead -lt $length -and $Json[$lookahead] -eq ':') {
                $rawName = $Json.Substring($start, $end - $start + 1)
                try {
                    $name = @($rawName | ConvertFrom-Json)[0]
                }
                catch {
                    throw "Invalid JSON property name in evidence file: $Path"
                }
                $currentObject = $objectStack[$objectStack.Count - 1]
                if ($currentObject.ContainsKey([string]$name)) {
                    throw "Duplicate JSON property '$name' in evidence file: $Path"
                }
                $currentObject[[string]$name] = $true
            }
            continue
        }

        if ($character -eq '{') {
            $objectStack.Add(@{})
        }
        elseif ($character -eq '}' -and $objectStack.Count -gt 0) {
            $objectStack.RemoveAt($objectStack.Count - 1)
        }
    }
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

function Test-ExactStringSet {
    param(
        [string[]]$Expected,
        [string[]]$Actual
    )

    $expectedSet = @($Expected | Sort-Object -Unique)
    $actualSet = @($Actual | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    if ($Expected.Count -ne $Actual.Count -or $expectedSet.Count -ne $actualSet.Count) {
        return $false
    }

    return @($expectedSet | Where-Object { $_ -notin $actualSet }).Count -eq 0
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

$attesterScriptPath = [IO.Path]::GetFullPath($MyInvocation.MyCommand.Path)
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
    $RollbackScriptPath,
    $BaselineGraphPath,
    $PostGraphPath,
    $attesterScriptPath
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
$wrongSidCorrelation = $null -ne $wrongSidTest -and
    (Has-Property $wrongSid 'correlationId') -and
    (Has-Property $wrongSidTest 'correlationId') -and
    [string]::Equals([string]$wrongSid.correlationId, [string]$wrongSidTest.correlationId, [StringComparison]::Ordinal) -and
    ($null -eq $wrongSidResponse -or (
        (Has-Property $wrongSidResponse 'correlationId') -and
        [string]::Equals([string]$wrongSid.correlationId, [string]$wrongSidResponse.correlationId, [StringComparison]::Ordinal)))

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

$expectedRegistry = @(
    'Registry64|SOFTWARE\Classes\AppID\{A5F0D0A4-1B58-4D84-9E0D-9D5A7C8C8A53}',
    'Registry64|SOFTWARE\Classes\CLSID\{D1E02B68-7A62-4C4B-B5D4-7DA8C26C0B48}',
    'Registry64|SOFTWARE\Classes\SEC18.CallerProbe',
    'Registry64|SOFTWARE\Classes\SEC18.CallerProbe.1',
    'Registry32|SOFTWARE\Classes\AppID\{A5F0D0A4-1B58-4D84-9E0D-9D5A7C8C8A53}',
    'Registry32|SOFTWARE\Classes\CLSID\{D1E02B68-7A62-4C4B-B5D4-7DA8C26C0B48}',
    'Registry32|SOFTWARE\Classes\SEC18.CallerProbe',
    'Registry32|SOFTWARE\Classes\SEC18.CallerProbe.1'
)
$expectedCleanupPaths = @(
    'C:\SEC18-Staging\Probe',
    'C:\SEC18-Staging\ProbeSource',
    'C:\SEC18-Staging\ProbeBuild',
    'C:\SEC18-Staging\ProbeSourceFx',
    'C:\SEC18-Staging\WebAdmin\sec18-pool-direct-com.php',
    'C:\SEC18-Staging\WebAdmin\sec18-pool-direct-com-wrong-sid.php',
    'C:\SEC18-Staging\WebAdmin\caller-probe-diagnostics.php',
    'C:\SEC18-Staging\WebAdmin\sec18-worker-identity.php',
    'C:\SEC18-Staging\WebAdmin\sec18-identity.php',
    'C:\SEC18-Staging\nonpool-client-attested.ps1',
    'C:\SEC18-Staging\nonpool-client-attested.json'
)
$actualRegistry = @($cleanup.registry | ForEach-Object {
        if ((Has-Property $_ 'View') -and (Has-Property $_ 'Key')) {
            '{0}|{1}' -f ([string]$_.View), ([string]$_.Key)
        }
    })
$actualCleanupPaths = @($cleanup.paths | ForEach-Object {
        if (Has-Property $_ 'Path') { [string]$_.Path }
    })
$cleanupCoverageExact = (Test-ExactStringSet $expectedRegistry $actualRegistry) -and
    (Test-ExactStringSet $expectedCleanupPaths $actualCleanupPaths) -and
    @($cleanup.registry | Where-Object { (Has-Property $_ 'Present') -and [bool]$_.Present }).Count -eq 0 -and
    @($cleanup.paths | Where-Object { (Has-Property $_ 'Present') -and [bool]$_.Present }).Count -eq 0
$rollbackHash = (Get-FileHash -LiteralPath $RollbackScriptPath -Algorithm SHA256).Hash
$cleanupRollbackName = if (Has-Property $cleanup 'rollbackScript') { Split-Path -Leaf ([string]$cleanup.rollbackScript) } else { $null }
$cleanupProvenance = (Has-Property $cleanup 'rollbackExitCode') -and
    [int]$cleanup.rollbackExitCode -eq 0 -and
    (Has-Property $cleanup 'rollbackScriptSha256') -and
    [string]::Equals([string]$cleanup.rollbackScriptSha256, $rollbackHash, [StringComparison]::OrdinalIgnoreCase) -and
    [string]::Equals($cleanupRollbackName, (Split-Path -Leaf $RollbackScriptPath), [StringComparison]::OrdinalIgnoreCase)
$hMailServiceSafe = (Has-Property $cleanup 'hMailService') -and
    (Has-Property $cleanup.hMailService 'Name') -and
    [string]::Equals([string]$cleanup.hMailService.Name, 'hMailServer', [StringComparison]::OrdinalIgnoreCase) -and
    [int]$cleanup.hMailService.Status -eq 1 -and
    [int]$cleanup.hMailService.StartType -eq 4
$rollbackSourcePresent = @($sourceHashes | Where-Object {
        [string]::Equals([IO.Path]::GetFullPath($_.Path), [IO.Path]::GetFullPath($RollbackScriptPath), [StringComparison]::OrdinalIgnoreCase) -and $_.Present
    }).Count -eq 1
$attesterSourcePresent = @($sourceHashes | Where-Object {
        [string]::Equals([IO.Path]::GetFullPath($_.Path), $attesterScriptPath, [StringComparison]::OrdinalIgnoreCase) -and $_.Present
    }).Count -eq 1

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
Add-Check 'wrong-sid-correlation-bound' $wrongSidCorrelation 'The wrong-SID server, matrix, and optional response records share one correlation id.'
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
    $cleanupCoverageExact -and
    $hMailServiceSafe) 'Temporary service, process, registry objects, endpoints, probe paths, and hMailServer state are exactly accounted for.'
Add-Check 'cleanup-provenance' $cleanupProvenance 'Rollback completed successfully and its exact script hash/name are bound to the cleanup evidence.'
Add-Check 'attester-provenance' ($rollbackSourcePresent -and $attesterSourcePresent) 'The rollback script and attester script are present in the hashed source set.'
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
