function Get-LiveBenchmarkFullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "A live benchmark path is required."
    }
    try {
        return [IO.Path]::GetFullPath($Path)
    }
    catch {
        throw "Live benchmark path is invalid: $Path"
    }
}

function Test-LiveBenchmarkPathContainsReparsePoint {
    param([Parameter(Mandatory = $true)][string]$Path)

    $current = Get-LiveBenchmarkFullPath $Path
    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                return $true
            }
        }
        $parent = [IO.Directory]::GetParent($current)
        if ($null -eq $parent -or $parent.FullName -eq $current) {
            break
        }
        $current = $parent.FullName
    }
    return $false
}

function Assert-LiveBenchmarkDisposablePath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ValidateSet("manifest", "root", "data", "executable")][string]$Kind
    )

    $fullPath = Get-LiveBenchmarkFullPath $Path
    if (Test-LiveBenchmarkPathContainsReparsePoint $fullPath) {
        throw "Refusing live benchmark $Kind through a reparse point: $fullPath"
    }
    if ($fullPath -match '(?i)(?:^|\\)(?:hmailserver57|hmaildb_test5700|program files|windows)(?:\\|$)') {
        throw "Refusing protected live benchmark $Kind path: $fullPath"
    }

    $approved = switch ($Kind) {
        "manifest" { $fullPath -match '(?i)^C:\\hmail-perf-[a-z0-9_-]+(?:\\[^\\]+)*\.json$' }
        "root" { $fullPath -match '(?i)^C:\\hmail-perf-[a-z0-9_-]+$' }
        "data" { $fullPath -match '(?i)^C:\\hmail-perf-[a-z0-9_-]+(?:\\(?:cpp|net10))?\\Data$' }
        "executable" { $fullPath -match '(?i)^C:\\hmail-perf-[a-z0-9_-]+(?:\\[^\\]+)*\\[^\\]+\.exe$' }
    }
    if (-not $approved) {
        throw "Live benchmark $Kind is not under an approved disposable C:\\hmail-perf-* path: $fullPath"
    }
    return $fullPath
}

function Assert-LiveBenchmarkDatabase {
    param([Parameter(Mandatory = $true)][string]$Database)

    if ($Database -notmatch '^hmail_perf_[a-z0-9_]+$' -or $Database -match '(?i)hmaildb_test5700|production') {
        throw "Refusing non-disposable live benchmark database: $Database"
    }
    return $Database
}

function Assert-LiveBenchmarkExecutablePath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ValidateSet("net10", "cpp")][string]$Implementation,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $fullPath = Get-LiveBenchmarkFullPath $Path
    if (Test-LiveBenchmarkPathContainsReparsePoint $fullPath) {
        throw "Refusing live benchmark executable through a reparse point: $fullPath"
    }
    $repositoryHost = $false
    if ($Implementation -eq "net10") {
        $benchmarkRoot = (Get-LiveBenchmarkFullPath (Join-Path $RepositoryRoot "artifacts\benchmarks")).TrimEnd('\') + '\'
        if ($fullPath.StartsWith($benchmarkRoot, [StringComparison]::OrdinalIgnoreCase)) {
            $relativePath = $fullPath.Substring($benchmarkRoot.Length)
            $repositoryHost = $relativePath -match '(?i)^live-cpp-net10-[a-z0-9_-]+\\LiveListenerHost\\bin\\(?:Release|Debug)\\net10\.0-windows\\LiveListenerHost\.exe$'
        }
    }
    $cppShape = $fullPath -match '(?i)^C:\\hmail-perf-(?:cpp|pair)-[a-z0-9_-]+(?:\\cpp)?\\Bin\\hMailServer\.exe$'
    $net10Shape = $fullPath -match '(?i)^C:\\hmail-perf-(?:net10|pair)-[a-z0-9_-]+(?:\\net10)?(?:\\Bin)?\\(?:hMailServer|LiveListenerHost)\.exe$'
    if (($Implementation -eq "cpp" -and -not $cppShape) -or ($Implementation -eq "net10" -and -not ($repositoryHost -or $net10Shape))) {
        throw "Executable is not an approved live benchmark target for ${Implementation}: $fullPath"
    }
    return $fullPath
}

function Get-LiveBenchmarkManifestProperty {
    param(
        [Parameter(Mandatory = $true)][object]$Manifest,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $property = $Manifest.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Fixture manifest is missing required property '$Name'."
    }
    return $property.Value
}

function Get-LiveBenchmarkManifestHash {
    param([Parameter(Mandatory = $true)][string]$ManifestPath)

    return (Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256 -ErrorAction Stop).Hash.ToUpperInvariant()
}

function Read-LiveBenchmarkFixtureManifest {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][ValidateSet("net10", "cpp")][string]$Implementation,
        [string]$RepositoryRoot = (Get-Location).Path
    )

    $manifestPath = Assert-LiveBenchmarkDisposablePath -Path $Path -Kind manifest
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Fixture manifest is missing: $manifestPath"
    }
    $manifestRoot = [IO.Path]::GetFullPath((Split-Path -Parent $manifestPath)).TrimEnd('\')
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $manifestSha256 = ([BitConverter]::ToString($sha.ComputeHash($manifestBytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString($manifestBytes)
    if ($raw.Length -gt 0 -and $raw[0] -eq [char]0xFEFF) {
        $raw = $raw.Substring(1)
    }
    try {
        $manifest = $raw | ConvertFrom-Json
    }
    catch {
        throw "Fixture manifest is not valid JSON: $manifestPath"
    }
    if ($manifest.schema -cne "paired-benchmark-fixture-v2") {
        throw "Unexpected fixture manifest schema: $($manifest.schema)"
    }
    if ($manifest.status -cne "PASS") {
        throw "Fixture manifest is not a passing disposable fixture: $($manifest.status)"
    }
    if ([int]$manifest.cppDatabaseVersion -ne 5708 -or [int]$manifest.net10DatabaseVersion -ne 6000) {
        throw "Fixture manifest must preserve the C++ 5708 and Net10 6000 database-version boundary."
    }

    $outputRoot = Assert-LiveBenchmarkDisposablePath -Path (Get-LiveBenchmarkManifestProperty $manifest "outputRoot") -Kind root
    if (-not [string]::Equals($outputRoot, $manifestRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture manifest outputRoot does not match its disposable manifest directory."
    }
    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
        throw "Fixture manifest outputRoot is missing: $outputRoot"
    }

    $cppDatabase = Assert-LiveBenchmarkDatabase (Get-LiveBenchmarkManifestProperty $manifest "cppDatabase")
    $net10Database = Assert-LiveBenchmarkDatabase (Get-LiveBenchmarkManifestProperty $manifest "net10Database")
    $cppDataRoot = Assert-LiveBenchmarkDisposablePath -Path (Get-LiveBenchmarkManifestProperty $manifest "cppDataRoot") -Kind data
    $net10DataRoot = Assert-LiveBenchmarkDisposablePath -Path (Get-LiveBenchmarkManifestProperty $manifest "net10DataRoot") -Kind data
    if (-not [string]::Equals($cppDataRoot, (Join-Path $outputRoot "cpp\Data"), [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($net10DataRoot, (Join-Path $outputRoot "net10\Data"), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture manifest implementation Data roots are not under the approved paired output root."
    }
    foreach ($dataRoot in @($cppDataRoot, $net10DataRoot)) {
        if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
            throw "Fixture manifest Data root is missing: $dataRoot"
        }
    }

    if ($null -eq $manifest.dataParity -or [int]$manifest.dataParity.fileCount -ne 1000 -or $manifest.dataParity.exact -ne $true -or $manifest.dataParity.sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "Fixture manifest does not prove the exact 1,000-file Data corpus."
    }
    if ($null -eq $manifest.messageParity -or [int]$manifest.messageParity.rowCount -ne 1000 -or $manifest.messageParity.exact -ne $true -or $manifest.messageParity.sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "Fixture manifest does not prove the exact 1,000-message corpus."
    }

    $cppExecutable = Assert-LiveBenchmarkExecutablePath -Path (Get-LiveBenchmarkManifestProperty $manifest "cppExecutable") -Implementation cpp -RepositoryRoot $RepositoryRoot
    $cppExpectedHash = [string](Get-LiveBenchmarkManifestProperty $manifest "cppExecutableSha256")
    if ($cppExpectedHash -notmatch '^[0-9A-Fa-f]{64}$' -or -not (Test-Path -LiteralPath $cppExecutable -PathType Leaf)) {
        throw "Fixture manifest C++ executable provenance is incomplete."
    }
    if ((Get-LiveBenchmarkManifestHash $cppExecutable) -ne $cppExpectedHash.ToUpperInvariant()) {
        throw "Fixture manifest C++ executable hash does not match the executable."
    }

    $net10Executable = $null
    $net10Property = $manifest.PSObject.Properties["net10Executable"]
    if ($null -ne $net10Property -and -not [string]::IsNullOrWhiteSpace([string]$net10Property.Value)) {
        $net10Executable = Assert-LiveBenchmarkExecutablePath -Path ([string]$net10Property.Value) -Implementation net10 -RepositoryRoot $RepositoryRoot
        if (-not (Test-Path -LiteralPath $net10Executable -PathType Leaf)) {
            throw "Fixture manifest .NET 10 executable is missing: $net10Executable"
        }
    }
    $net10ExpectedHash = [string](Get-LiveBenchmarkManifestProperty $manifest "net10ExecutableSha256")
    if ($net10ExpectedHash -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "Fixture manifest .NET 10 executable provenance is incomplete."
    }
    if ($null -ne $net10Executable -and (Get-LiveBenchmarkManifestHash $net10Executable) -ne $net10ExpectedHash.ToUpperInvariant()) {
        throw "Fixture manifest .NET 10 executable hash does not match the executable."
    }

    $fixtureIdProperty = $manifest.PSObject.Properties["fixtureId"]
    $fixtureId = if ($null -ne $fixtureIdProperty -and -not [string]::IsNullOrWhiteSpace([string]$fixtureIdProperty.Value)) {
        [string]$fixtureIdProperty.Value
    }
    else {
        Split-Path -Leaf $outputRoot
    }
    if ($fixtureId -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
        throw "Fixture manifest fixtureId is invalid: $fixtureId"
    }

    [pscustomobject]@{
        path = $manifestPath
        sha256 = $manifestSha256
        fixtureId = $fixtureId
        outputRoot = $outputRoot
        database = if ($Implementation -eq "cpp") { $cppDatabase } else { $net10Database }
        dataRoot = if ($Implementation -eq "cpp") { $cppDataRoot } else { $net10DataRoot }
        executable = if ($Implementation -eq "cpp") { $cppExecutable } else { $net10Executable }
        expectedExecutableSha256 = if ($Implementation -eq "cpp") { $cppExpectedHash.ToUpperInvariant() } else { $net10ExpectedHash.ToUpperInvariant() }
        cppExecutable = $cppExecutable
        cppExecutableSha256 = $cppExpectedHash.ToUpperInvariant()
        net10Executable = $net10Executable
        net10ExecutableSha256 = $net10ExpectedHash.ToUpperInvariant()
    }
}

function Assert-LiveBenchmarkLoopbackPorts {
    param(
        [Parameter(Mandatory = $true)][string]$Bind,
        [Parameter(Mandatory = $true)][object]$Ports
    )

    if ($Bind -cne "127.0.0.1") {
        throw "Live benchmark must bind to 127.0.0.1: $Bind"
    }
    $expected = [ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 }
    foreach ($name in $expected.Keys) {
        $value = if ($Ports -is [Collections.IDictionary]) { $Ports[$name] } else { $Ports.$name }
        if ($null -eq $value -or [int]$value -ne $expected[$name]) {
            throw "Unexpected loopback benchmark port for $name."
        }
    }
    [pscustomobject]@{
        smtp = 2525
        imap = 1143
        pop3 = 25110
        text = "SMTP 2525, IMAP 1143, POP3 25110"
    }
}

function Get-LiveBenchmarkProvenance {
    param(
        [string]$FixtureManifest = "",
        [Parameter(Mandatory = $true)][ValidateSet("net10", "cpp")][string]$Implementation,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$DataRoot,
        [Parameter(Mandatory = $true)][string]$ServiceExecutable,
        [string]$RunId = "",
        [string]$Bind = "127.0.0.1",
        [Parameter(Mandatory = $true)][object]$Ports
    )

    $manifest = $null
    if (-not [string]::IsNullOrWhiteSpace($FixtureManifest)) {
        $manifest = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation $Implementation -RepositoryRoot $RepositoryRoot
        if (-not [string]::Equals($Database, $manifest.database, [StringComparison]::Ordinal)) {
            throw "Live benchmark database does not match the fixture manifest."
        }
        $fullDataRoot = Get-LiveBenchmarkFullPath $DataRoot
        if (-not [string]::Equals($fullDataRoot, $manifest.dataRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Live benchmark Data root does not match the fixture manifest."
        }
        if ($null -ne $manifest.executable -and -not [string]::Equals((Get-LiveBenchmarkFullPath $ServiceExecutable), $manifest.executable, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Live benchmark executable does not match the fixture manifest."
        }
    }

    $database = Assert-LiveBenchmarkDatabase $Database
    $dataRoot = Assert-LiveBenchmarkDisposablePath -Path $DataRoot -Kind data
    if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
        throw "Live benchmark Data root is missing: $dataRoot"
    }
    $serviceExecutable = Assert-LiveBenchmarkExecutablePath -Path $ServiceExecutable -Implementation $Implementation -RepositoryRoot $RepositoryRoot
    if (-not (Test-Path -LiteralPath $serviceExecutable -PathType Leaf)) {
        throw "Live benchmark executable is missing: $serviceExecutable"
    }
    $portBinding = Assert-LiveBenchmarkLoopbackPorts -Bind $Bind -Ports $Ports
    $observedHash = Get-LiveBenchmarkManifestHash $serviceExecutable
    if ($null -ne $manifest -and $observedHash -ne $manifest.expectedExecutableSha256) {
        throw "Live benchmark $Implementation executable hash does not match the fixture manifest."
    }
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        $RunId = [Guid]::NewGuid().ToString("D")
    }
    else {
        try {
            $RunId = ([Guid]::Parse($RunId)).ToString("D")
        }
        catch {
            throw "Live benchmark RunId must be a valid GUID."
        }
    }

    [pscustomobject]@{
        runId = $RunId
        manifestBound = $null -ne $manifest
        fixtureId = if ($null -ne $manifest) { $manifest.fixtureId } else { $null }
        manifestSha256 = if ($null -ne $manifest) { $manifest.sha256 } else { $null }
        manifestPath = if ($null -ne $manifest) { $manifest.path } else { $null }
        implementation = $Implementation
        database = $database
        dataRoot = $dataRoot
        bind = $Bind
        ports = $portBinding.text
        portMap = $portBinding
        executableProvenance = [pscustomobject]@{
            implementation = $Implementation
            path = $serviceExecutable
            sha256 = $observedHash
            expectedSha256 = if ($null -ne $manifest) { $manifest.expectedExecutableSha256 } else { $null }
            length = (Get-Item -LiteralPath $serviceExecutable -Force).Length
            lastWriteTimeUtc = (Get-Item -LiteralPath $serviceExecutable -Force).LastWriteTimeUtc.ToString("o")
        }
    }
}

function Assert-LiveBenchmarkManifestBoundArtifact {
    param(
        [Parameter(Mandatory = $true)][object]$Report,
        [Parameter(Mandatory = $true)][string]$CsvPath,
        [Parameter(Mandatory = $true)][string]$MarkdownPath
    )

    if ($Report.provenanceStatus -cne "MANIFEST_BOUND") {
        throw "Live benchmark acceptance requires MANIFEST_BOUND provenance; found '$($Report.provenanceStatus)'."
    }
    $parsedRunId = [Guid]::Empty
    if (-not [Guid]::TryParse([string]$Report.runId, [ref]$parsedRunId) -or $parsedRunId -eq [Guid]::Empty) {
        throw "Live benchmark report runId is missing or invalid."
    }
    if ([string]$Report.fixtureId -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$' -or [string]$Report.manifestSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "Live benchmark fixture provenance is missing or invalid."
    }
    if ($null -eq $Report.executableProvenance -or
        [string]$Report.executableProvenance.sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        [string]$Report.executableProvenance.expectedSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        -not [string]::Equals([string]$Report.executableProvenance.sha256, [string]$Report.executableProvenance.expectedSha256, [StringComparison]::OrdinalIgnoreCase) -or
        [int64]$Report.executableProvenance.length -le 0) {
        throw "Live benchmark executable provenance is incomplete or does not match the manifest."
    }
    if (-not (Test-Path -LiteralPath $CsvPath -PathType Leaf) -or -not (Test-Path -LiteralPath $MarkdownPath -PathType Leaf)) {
        throw "Live benchmark CSV or Markdown provenance artifact is missing."
    }

    $csvRows = @(Import-Csv -LiteralPath $CsvPath)
    $sampleCount = @($Report.samples).Count
    if ($csvRows.Count -ne $sampleCount) {
        throw "Live benchmark CSV sample count does not match JSON."
    }
    foreach ($row in $csvRows) {
        if ($row.runId -cne [string]$Report.runId -or
            $row.provenanceStatus -cne "MANIFEST_BOUND" -or
            $row.fixtureId -cne [string]$Report.fixtureId -or
            $row.manifestSha256 -cne [string]$Report.manifestSha256 -or
            $row.implementation -cne [string]$Report.implementation -or
            $row.database -cne [string]$Report.database -or
            -not [string]::Equals($row.dataRoot, [string]$Report.dataRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals($row.executableSha256, [string]$Report.executableProvenance.sha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Live benchmark CSV provenance does not match JSON."
        }
    }

    $markdown = Get-Content -LiteralPath $MarkdownPath -Raw
    foreach ($line in @(
        "Implementation: $($Report.implementation)",
        "Run ID: $($Report.runId)",
        "Provenance: MANIFEST_BOUND",
        "Fixture ID: $($Report.fixtureId)",
        "Fixture manifest SHA-256: $($Report.manifestSha256)",
        "Executable SHA-256: $($Report.executableProvenance.sha256)"
    )) {
        if ($markdown.IndexOf($line, [StringComparison]::Ordinal) -lt 0) {
            throw "Live benchmark Markdown provenance is missing: $line"
        }
    }
}
