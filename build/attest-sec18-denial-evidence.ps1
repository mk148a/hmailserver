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
    [string]$CollectorScriptPath,

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

function ConvertTo-UtcTimestampString {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [DateTimeOffset]) {
        return $Value.ToUniversalTime().ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    }

    if ($Value -is [DateTime]) {
        $utcValue = $Value.ToUniversalTime()
        return ([DateTimeOffset]$utcValue).ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    }

    return [string]$Value
}

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

function Get-Sec18EvidenceRoot {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$ScriptPath
    )

    $buildDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($ScriptPath))
    $repositoryRoot = Split-Path -Parent $buildDirectory
    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\sec18-staging'))
}

function Resolve-Sec18EvidenceOutputPath {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$RequestedPath,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$EvidenceRoot
    )

    $fullRoot = [System.IO.Path]::GetFullPath($EvidenceRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not [System.IO.Directory]::Exists($fullRoot)) {
        throw "SEC-18 evidence root does not exist: $fullRoot"
    }

    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $fullRoot)
    $fullPath = if ([System.IO.Path]::IsPathRooted($RequestedPath)) {
        [System.IO.Path]::GetFullPath($RequestedPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $RequestedPath))
    }

    $rootPrefix = $fullRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "SEC-18 output path must remain under ${fullRoot}: $fullPath"
    }

    $outputDirectory = Split-Path -Parent $fullPath
    if (-not [System.IO.Directory]::Exists($outputDirectory)) {
        throw "SEC-18 output directory does not exist: $outputDirectory"
    }

    $currentDirectory = [System.IO.Path]::GetFullPath($outputDirectory)
    while ($true) {
        $item = Get-Item -LiteralPath $currentDirectory -Force -ErrorAction Stop
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "SEC-18 output path cannot use a reparse-point directory: $currentDirectory"
        }
        if ([string]::Equals($currentDirectory, $fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $currentDirectory = [System.IO.Path]::GetFullPath((Split-Path -Parent $currentDirectory))
        if (-not [string]::Equals($currentDirectory, $fullRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $currentDirectory.StartsWith($fullRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "SEC-18 output path ancestry escaped the evidence root: $currentDirectory"
        }
    }

    if ([System.IO.File]::Exists($fullPath) -or [System.IO.Directory]::Exists($fullPath)) {
        throw "SEC-18 output path already exists and will not be overwritten: $fullPath"
    }

    return $fullPath
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
$canonicalApplicationAppId = '{5EDEC473-39E0-43F6-A234-1947071721C8}'

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
    $CollectorScriptPath,
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
$authorizedResponseCorrelation = $null -ne $authorizedResponse -and (
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
$authorizedStageSource = $authorizedResponse
$authorizedStageFieldsPresent = @($authorizedStageFields | Where-Object {
        Has-Property $authorizedStageSource $_
    }).Count -eq $authorizedStageFields.Count
$authorizedStageHresultsSuccessful = $authorizedStageFieldsPresent -and
    [int]$authorizedStageSource.activationHresult -eq 0 -and
    [int]$authorizedStageSource.interfaceHresult -eq 0 -and
    [int]$authorizedStageSource.methodHresult -eq 0

$poolSid = if (Has-Property $matrix.runtime 'poolSid') { [string]$matrix.runtime.poolSid } else { $null }
$wrongSidCaseBound = $null -ne $wrongSidTest -and
    [string]::Equals([string]$wrongSidTest.name, 'authorized-process-wrong-expected-sid', [StringComparison]::Ordinal) -and
    (Has-Property $wrongSid 'callerSid') -and
    (Has-Property $wrongSid 'expectedSid') -and
    -not [string]::IsNullOrWhiteSpace([string]$wrongSid.callerSid) -and
    -not [string]::IsNullOrWhiteSpace([string]$wrongSid.expectedSid) -and
    -not [string]::Equals([string]$wrongSid.callerSid, [string]$wrongSid.expectedSid, [StringComparison]::OrdinalIgnoreCase)

$nonPoolProcessName = [IO.Path]::GetFileNameWithoutExtension([string]$nonPool.processImage)
$nonPoolProcessTokenBound = @($processes | Where-Object {
        (Has-Property $_ 'UserSid') -and
        -not [string]::IsNullOrWhiteSpace([string]$_.UserSid) -and
        -not [string]::Equals([string]$_.UserSid, $poolSid, [StringComparison]::OrdinalIgnoreCase) -and
        ((Has-Property $_ 'Path') -and [string]::Equals([string]$_.Path, [string]$nonPool.processImage, [StringComparison]::OrdinalIgnoreCase) -or
            (Has-Property $_ 'Name') -and [string]::Equals(([string]$_.Name).Replace('.exe', ''), $nonPoolProcessName, [StringComparison]::OrdinalIgnoreCase))
    }).Count -gt 0

$expectedInstalledGraphKeyPaths = @(
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
$graphEvidenceCandidates = @($baselineGraph, $postGraph)
$graphEvidenceComplete = @($graphEvidenceCandidates | Where-Object {
        (Has-Property $_ 'SchemaVersion') -and [int]$_.SchemaVersion -eq 1 -and
        (Has-Property $_ 'EvidenceKind') -and [string]::Equals([string]$_.EvidenceKind, 'SEC18-InstalledApplicationGraph', [StringComparison]::Ordinal) -and
        (Has-Property $_ 'GraphPathCount') -and [int]$_.GraphPathCount -eq $expectedInstalledGraphKeyPaths.Count -and
        (Has-Property $_ 'SnapshotCount') -and [int]$_.SnapshotCount -eq ($expectedInstalledGraphKeyPaths.Count * 2) -and
        (Has-Property $_ 'CanonicalExpectedContentsValidated') -and [bool]$_.CanonicalExpectedContentsValidated -and
        (Has-Property $_ 'CompleteReadback') -and [bool]$_.CompleteReadback -and
        (Has-Property $_ 'CanonicalValidation') -and (Has-Property $_.CanonicalValidation 'Complete') -and [bool]$_.CanonicalValidation.Complete -and
        (Has-Property $_.CanonicalValidation 'FixedValuesValidated') -and [bool]$_.CanonicalValidation.FixedValuesValidated -and
        (Has-Property $_.CanonicalValidation 'DirectSubkeysValidated') -and [bool]$_.CanonicalValidation.DirectSubkeysValidated -and
        (Has-Property $_.CanonicalValidation 'Registry32AsymmetryValidated') -and [bool]$_.CanonicalValidation.Registry32AsymmetryValidated -and
        (Has-Property $_.CanonicalValidation 'InstallationPathsValidated') -and [bool]$_.CanonicalValidation.InstallationPathsValidated -and
        @($_.Snapshots).Count -eq ($expectedInstalledGraphKeyPaths.Count * 2) -and
        (@($_.Snapshots | ForEach-Object { "$($_.View)|$($_.KeyPath)" } | Sort-Object) -join "`n") -ceq
        (@('Registry64','Registry32' | ForEach-Object { $view = $_; $expectedInstalledGraphKeyPaths | ForEach-Object { "$view|$_" } } | Sort-Object) -join "`n")
    }).Count -eq 2

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
$collectorScriptSourcePresent = @($sourceHashes | Where-Object {
        [string]::Equals([IO.Path]::GetFullPath($_.Path), [IO.Path]::GetFullPath($CollectorScriptPath), [StringComparison]::OrdinalIgnoreCase) -and $_.Present
    }).Count -eq 1
$collectorService = if (Has-Property $collector 'HMailServerService') { $collector.HMailServerService } else { $null }
$cleanupService = if (Has-Property $cleanup 'hMailService') { $cleanup.hMailService } else { $null }
$attestationMaxCallerAgeSeconds = 300
$collectorCollectedUtc = $null
$callerObservedUtc = $null
$collectorTimestampsParseable = $false
$collectorCallerAgeSeconds = $null
try {
    if ((Has-Property $collector 'CollectedUtc') -and (Has-Property $collector.CallerTokenEvidence 'ObservedUtc')) {
        $collectorCollectedUtc = [DateTimeOffset]::Parse(
            (ConvertTo-UtcTimestampString -Value $collector.CollectedUtc),
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal).ToUniversalTime()
        $callerObservedUtc = [DateTimeOffset]::Parse(
            (ConvertTo-UtcTimestampString -Value $collector.CallerTokenEvidence.ObservedUtc),
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal).ToUniversalTime()
        $collectorCallerAgeSeconds = ($collectorCollectedUtc - $callerObservedUtc).TotalSeconds
        $collectorTimestampsParseable = $true
    }
}
catch {
    $collectorTimestampsParseable = $false
}
$collectorCallerCorrelationBound = $null -ne $collector -and
    (Has-Property $collector 'CollectorInvocationId') -and
    (Has-Property $collector.CallerTokenEvidence 'CorrelationId') -and
    -not [string]::IsNullOrWhiteSpace([string]$collector.CollectorInvocationId) -and
    [string]::Equals([string]$collector.CollectorInvocationId, [string]$collector.CallerTokenEvidence.CorrelationId, [StringComparison]::Ordinal)
$collectorApplicationAppIdBound = $null -ne $collector -and
    (Has-Property $collector 'ApplicationAppId') -and
    [string]::Equals([string]$collector.ApplicationAppId, $canonicalApplicationAppId, [StringComparison]::OrdinalIgnoreCase)
$collectorCallerFreshAndCorrelated = $collectorTimestampsParseable -and
    (Has-Property $collector 'CallerEvidenceMaxAgeSeconds') -and
    [int]$collector.CallerEvidenceMaxAgeSeconds -eq $attestationMaxCallerAgeSeconds -and
    (Has-Property $collector.CallerTokenEvidence 'TimestampParseable') -and
    [bool]$collector.CallerTokenEvidence.TimestampParseable -and
    (Has-Property $collector.CallerTokenEvidence 'TimestampFresh') -and
    [bool]$collector.CallerTokenEvidence.TimestampFresh -and
    (Has-Property $collector.CallerTokenEvidence 'CorrelationMatchesCollectorInvocation') -and
    [bool]$collector.CallerTokenEvidence.CorrelationMatchesCollectorInvocation -and
    $collectorCallerCorrelationBound -and
    $collectorCallerAgeSeconds -ge -30 -and
    $collectorCallerAgeSeconds -le $attestationMaxCallerAgeSeconds
$collectorProcessReadFailClosed = $null -ne $collectorService -and
    (Has-Property $collectorService 'ServiceReadError') -and
    [string]::IsNullOrWhiteSpace([string]$collectorService.ServiceReadError) -and
    (Has-Property $collectorService 'ProcessReadError') -and
    [string]::IsNullOrWhiteSpace([string]$collectorService.ProcessReadError) -and
    (Has-Property $collectorService 'ReadError') -and
    [string]::IsNullOrWhiteSpace([string]$collectorService.ReadError)
$collectorServiceStateBound = $null -ne $collectorService -and
    [string]::Equals([string]$collectorService.Name, 'hMailServer', [StringComparison]::OrdinalIgnoreCase) -and
    [bool]$collectorService.Present -and
    [string]::Equals([string]$collectorService.StatusName, 'Stopped', [StringComparison]::OrdinalIgnoreCase) -and
    [string]::Equals([string]$collectorService.StartTypeName, 'Disabled', [StringComparison]::OrdinalIgnoreCase) -and
    [bool]$collectorService.ProcessPresent -eq $false -and
    [bool]$collector.Gate.HMailServerServiceSafe -and
    $collectorProcessReadFailClosed -and
    $null -ne $cleanupService -and
    [string]::Equals([string]$cleanupService.Name, [string]$collectorService.Name, [StringComparison]::OrdinalIgnoreCase) -and
    [int]$cleanupService.Status -eq [int]$collectorService.Status -and
    [int]$cleanupService.StartType -eq [int]$collectorService.StartType

Add-Check 'source-files-present' (@($sourceHashes | Where-Object { -not $_.Present }).Count -eq 0) 'Every attested source file exists.'
Add-Check 'authorized-correlation-bound' ($authorizedCorrelation -and $authorizedMatrixCorrelation -and $authorizedResponseCorrelation) 'The authorized server and response records share one non-empty correlation id.'
Add-Check 'authorized-effective-sid' $authorizedSidBound 'The authorized server caller SID matches the configured pool SID.'
Add-Check 'authorized-token-steps' $authorizedTokenSteps 'Impersonation, token read, revert, and residual-token cleanup are exact.'
Add-Check 'authorized-stage-hresults' $authorizedStageHresultsSuccessful 'Authorized activation, interface, and method HRESULTs are explicitly captured in the response record and are all S_OK.'
Add-Check 'wrong-sid-method-denial' (
    $null -ne $wrongSid -and
    $wrongSidCaseBound -and
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
Add-Check 'nonpool-process-token' $nonPoolProcessTokenBound 'The independently measured non-pool process evidence has a matching process identity and a SID different from the dedicated worker pool.'
Add-Check 'nonpool-client-correlation' (
    (Has-Property $nonPool 'clientRecordId') -and
    (Has-Property $nonPoolTest 'clientRecordId') -and
    [string]::Equals([string]$nonPool.clientRecordId, [string]$nonPoolTest.clientRecordId, [StringComparison]::Ordinal) -and
    -not [string]::IsNullOrWhiteSpace([string]$nonPool.clientRecordId)) 'The non-pool client record has an explicit evidence correlation identifier.'
Add-Check 'collector-caller-token' (
    [bool]$collector.CallerTokenEvidence.Valid -and
    [bool]$collector.Gate.CallerTokenMatchesWorkerSid -and
    [bool]$collector.Gate.DedicatedPoolCandidate) 'The elevated collector links the caller SID to the dedicated IIS pool.'
Add-Check 'collector-application-appid' $collectorApplicationAppIdBound 'The collector evidence is bound to the installed hMailServer Application AppID.'
Add-Check 'collector-caller-freshness-correlation' $collectorCallerFreshAndCorrelated 'Caller-token evidence is fresh, parseable, and correlated to this collector invocation.'
Add-Check 'collector-service-state' $collectorServiceStateBound 'The collector and cleanup evidence bind the exact hMailServer service to Stopped/Disabled state with no process.'
Add-Check 'collector-service-read-fail-closed' $collectorProcessReadFailClosed 'Service and process enumeration errors cannot be represented as a false absent-process result.'
Add-Check 'cleanup-verified' (
    [bool]$cleanup.productionApplicationTouched -eq $false -and
    [bool]$cleanup.servicePresent -eq $false -and
    @($cleanup.probeProcess).Count -eq 0 -and
    $cleanupCoverageExact -and
    $hMailServiceSafe) 'Temporary service, process, registry objects, endpoints, probe paths, and hMailServer state are exactly accounted for.'
Add-Check 'cleanup-provenance' $cleanupProvenance 'Rollback completed successfully and its exact script hash/name are bound to the cleanup evidence.'
Add-Check 'attester-provenance' ($rollbackSourcePresent -and $attesterSourcePresent) 'The rollback script and attester script are present in the hashed source set.'
Add-Check 'collector-provenance' $collectorScriptSourcePresent 'The collector implementation is present in the hashed source set.'
$baselineHash = Get-SnapshotHash $baselineGraph
$postHash = Get-SnapshotHash $postGraph
Add-Check 'installed-application-graph-unchanged' (
    $graphEvidenceComplete -and
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
    $evidenceRoot = Get-Sec18EvidenceRoot -ScriptPath $MyInvocation.MyCommand.Path
    $fullOutputPath = Resolve-Sec18EvidenceOutputPath -RequestedPath $OutputPath -EvidenceRoot $evidenceRoot
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($fullOutputPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
    Write-Output $fullOutputPath
}

if ($FailOnIncomplete -and -not $ready) {
    exit 2
}
