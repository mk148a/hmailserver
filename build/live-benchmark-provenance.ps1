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

function Test-LiveBenchmarkTreeContainsReparsePoint {
    param([Parameter(Mandatory = $true)][string]$Root)

    foreach ($item in Get-ChildItem -LiteralPath $Root -Force -Recurse -ErrorAction Stop) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            return $true
        }
    }
    return $false
}

function Get-LiveBenchmarkDirectoryFingerprint {
    param([Parameter(Mandatory = $true)][string]$Root)

    $fullRoot = Assert-LiveBenchmarkDisposablePath -Path $Root -Kind data
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Live benchmark Data root is missing: $fullRoot"
    }
    if (Test-LiveBenchmarkTreeContainsReparsePoint $fullRoot) {
        throw "Live benchmark Data root contains a reparse point: $fullRoot"
    }

    $trimmedRoot = $fullRoot.TrimEnd('\')
    $files = @(Get-ChildItem -LiteralPath $trimmedRoot -File -Recurse -Force -ErrorAction Stop |
        Sort-Object FullName)
    $rows = foreach ($file in $files) {
        $relative = $file.FullName.Substring($trimmedRoot.Length).TrimStart('\').Replace('/', '\')
        "$relative|$($file.Length)|$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction Stop).Hash)"
    }
    $payload = [Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = ([BitConverter]::ToString($sha.ComputeHash($payload))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }

    [pscustomobject]@{
        fileCount = $files.Count
        bytes = [long](($files | Measure-Object Length -Sum).Sum)
        sha256 = $digest
    }
}

function Get-LiveBenchmarkDatabaseVersion {
    param([Parameter(Mandatory = $true)][string]$Database)

    $database = Assert-LiveBenchmarkDatabase $Database
    $value = (& sqlcmd.exe -S localhost -E -b -d $database -h-1 -W -Q 'SET NOCOUNT ON; SELECT value FROM hm_dbversion;').Trim()
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^\d+$') {
        throw "Could not read hm_dbversion from $database."
    }
    return [int]$value
}

function Get-LiveBenchmarkMessageFingerprint {
    param([Parameter(Mandatory = $true)][string]$Database)

    $database = Assert-LiveBenchmarkDatabase $Database
    $query = @"
SET NOCOUNT ON;
DECLARE @payload nvarchar(max);
SELECT @payload = STRING_AGG(CAST(CONCAT(
    messageid, N'|', messageaccountid, N'|', messagefolderid, N'|',
    CASE WHEN CHARINDEX(N'\Data\', messagefilename) > 0
         THEN SUBSTRING(messagefilename, CHARINDEX(N'\Data\', messagefilename) + 6, 4000)
         ELSE messagefilename END, N'|',
    messagetype, N'|', COALESCE(messagefrom, N''), N'|', messagesize, N'|',
    messagecurnooftries, N'|', CONVERT(nvarchar(33), messagenexttrytime, 126), N'|',
    messageflags, N'|', CONVERT(nvarchar(33), messagecreatetime, 126), N'|',
    messagelocked, N'|', messageuid, N'|', COALESCE(messageruleforcedrouteid, -1), N'|',
    COALESCE(messagerulebindaddress, N'')) AS nvarchar(max)), NCHAR(10))
    WITHIN GROUP (ORDER BY messageid)
FROM hm_messages;
SELECT CONCAT(COUNT_BIG(*), N'|', CONVERT(varchar(64), HASHBYTES('SHA2_256', COALESCE(@payload, N'')), 2))
FROM hm_messages;
"@
    $value = (& sqlcmd.exe -S localhost -E -b -d $database -h-1 -W -Q $query).Trim()
    if ($LASTEXITCODE -ne 0 -or $value -notmatch '^\d+\|[0-9A-F]{64}$') {
        throw "Could not calculate the logical message fingerprint for $database."
    }
    $parts = $value.Split('|')
    [pscustomobject]@{ rowCount = [long]$parts[0]; sha256 = $parts[1] }
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
    if ($cppDatabase -notmatch '^hmail_perf_pair_cpp_[a-z0-9_]+$' -or
        $net10Database -notmatch '^hmail_perf_pair_net10_[a-z0-9_]+$') {
        throw "Fixture manifest databases must use implementation-specific paired disposable names."
    }
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

    if ($null -eq $manifest.dataParity -or [int]$manifest.dataParity.fileCount -ne 1000 -or [long]$manifest.dataParity.bytes -le 0 -or $manifest.dataParity.exact -ne $true -or $manifest.dataParity.sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
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
        expectedDatabaseVersion = if ($Implementation -eq "cpp") { [int]$manifest.cppDatabaseVersion } else { [int]$manifest.net10DatabaseVersion }
        expectedDataFingerprint = [pscustomobject]@{
            fileCount = [int]$manifest.dataParity.fileCount
            bytes = [long]$manifest.dataParity.bytes
            sha256 = ([string]$manifest.dataParity.sha256).ToUpperInvariant()
        }
        expectedMessageFingerprint = [pscustomobject]@{
            rowCount = [long]$manifest.messageParity.rowCount
            sha256 = ([string]$manifest.messageParity.sha256).ToUpperInvariant()
        }
        cppExecutable = $cppExecutable
        cppExecutableSha256 = $cppExpectedHash.ToUpperInvariant()
        net10Executable = $net10Executable
        net10ExecutableSha256 = $net10ExpectedHash.ToUpperInvariant()
    }
}

function Assert-LiveBenchmarkRunStartAttestation {
    param(
        [Parameter(Mandatory = $true)][string]$FixtureManifest,
        [Parameter(Mandatory = $true)][ValidateSet("net10", "cpp")][string]$Implementation,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$DataRoot,
        [Parameter(Mandatory = $true)][string]$ServiceExecutable,
        [scriptblock]$DatabaseVersionReader,
        [scriptblock]$MessageFingerprintReader
    )

    $manifest = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation $Implementation -RepositoryRoot $RepositoryRoot
    if (-not [string]::Equals($manifest.database, $Database, [StringComparison]::Ordinal) -or
        -not [string]::Equals($manifest.dataRoot, (Get-LiveBenchmarkFullPath $DataRoot), [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($manifest.executable, (Get-LiveBenchmarkFullPath $ServiceExecutable), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Run-start attestation inputs do not match the fixture manifest."
    }

    $dataFingerprint = Get-LiveBenchmarkDirectoryFingerprint $manifest.dataRoot
    if ($dataFingerprint.fileCount -ne $manifest.expectedDataFingerprint.fileCount -or
        $dataFingerprint.bytes -ne $manifest.expectedDataFingerprint.bytes -or
        $dataFingerprint.sha256 -ne $manifest.expectedDataFingerprint.sha256) {
        throw "Run-start Data fingerprint does not match the fixture manifest."
    }

    $databaseVersion = if ($null -ne $DatabaseVersionReader) {
        & $DatabaseVersionReader $manifest.database
    }
    else {
        Get-LiveBenchmarkDatabaseVersion $manifest.database
    }
    if ([int]$databaseVersion -ne $manifest.expectedDatabaseVersion) {
        throw "Run-start database version does not match the fixture manifest."
    }

    $messageFingerprint = if ($null -ne $MessageFingerprintReader) {
        & $MessageFingerprintReader $manifest.database
    }
    else {
        Get-LiveBenchmarkMessageFingerprint $manifest.database
    }
    if ([long]$messageFingerprint.rowCount -ne $manifest.expectedMessageFingerprint.rowCount -or
        ([string]$messageFingerprint.sha256).ToUpperInvariant() -ne $manifest.expectedMessageFingerprint.sha256) {
        throw "Run-start message fingerprint does not match the fixture manifest."
    }

    $executableSha256 = Get-LiveBenchmarkManifestHash $manifest.executable
    if ($executableSha256 -ne $manifest.expectedExecutableSha256) {
        throw "Run-start executable hash does not match the fixture manifest."
    }

    [pscustomobject]@{
        status = "PASS"
        observedUtc = [DateTimeOffset]::UtcNow.ToString("o")
        manifestSha256 = $manifest.sha256
        database = $manifest.database
        databaseVersion = [int]$databaseVersion
        messageRowCount = [long]$messageFingerprint.rowCount
        messageSha256 = ([string]$messageFingerprint.sha256).ToUpperInvariant()
        dataFileCount = [int]$dataFingerprint.fileCount
        dataBytes = [long]$dataFingerprint.bytes
        dataSha256 = $dataFingerprint.sha256
        executableSha256 = $executableSha256
        descendantReparsePoints = $false
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

function Assert-LiveBenchmarkRunStartArtifact {
    param(
        [Parameter(Mandatory = $true)][object]$Report,
        [Parameter(Mandatory = $true)][string]$CsvPath,
        [Parameter(Mandatory = $true)][string]$MarkdownPath
    )

    $attestation = $Report.runStartAttestation
    if ($null -eq $attestation -or $attestation.status -cne "PASS" -or
        $attestation.manifestSha256 -cne $Report.manifestSha256 -or
        $attestation.database -cne $Report.database -or
        [int]$attestation.dataFileCount -ne 1000 -or
        [long]$attestation.messageRowCount -ne 1000 -or
        $attestation.dataSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        $attestation.messageSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        $attestation.executableSha256 -cne $Report.executableProvenance.sha256 -or
        $attestation.descendantReparsePoints -ne $false) {
        throw "Live benchmark artifact does not contain a passing run-start attestation."
    }

    $csvRows = @(Import-Csv -LiteralPath $CsvPath)
    if ($csvRows.Count -eq 0 -or @($csvRows | Where-Object {
            $_.runStartAttestationStatus -cne "PASS" -or
            $_.runStartDataSha256 -cne $attestation.dataSha256 -or
            $_.runStartMessageSha256 -cne $attestation.messageSha256
        }).Count -ne 0) {
        throw "Live benchmark CSV does not agree with the run-start attestation."
    }

    $markdown = Get-Content -LiteralPath $MarkdownPath -Raw
    foreach ($line in @(
            "Run-start attestation: PASS",
            "Run-start Data SHA-256: $($attestation.dataSha256)",
            "Run-start message SHA-256: $($attestation.messageSha256)")) {
        if ($markdown.IndexOf($line, [StringComparison]::Ordinal) -lt 0) {
            throw "Live benchmark Markdown does not agree with the run-start attestation."
        }
    }
}
