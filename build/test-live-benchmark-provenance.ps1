$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

$passed = 0
$root = Join-Path "C:\" ("hmail-perf-pair-provenance-test-{0}" -f ([Guid]::NewGuid().ToString("D")))
$rootCreated = $false
$cppRoot = Join-Path $root "cpp"
$net10Root = Join-Path $root "net10"
$cppData = Join-Path $cppRoot "Data"
$net10Data = Join-Path $net10Root "Data"
$cppExe = Join-Path $cppRoot "Bin\hMailServer.exe"
$net10Exe = Join-Path $net10Root "Bin\LiveListenerHost.exe"
$manifestPath = Join-Path $root "paired-fixture.json"

function Assert-Throws {
    param([string]$Name, [scriptblock]$Action)

    try {
        & $Action
    }
    catch {
        $script:passed++
        return
    }
    throw "${Name}: expected a fail-closed exception."
}

try {
    if (Test-Path -LiteralPath $root) {
        throw "Refusing pre-existing provenance test root: $root"
    }
    $rootItem = New-Item -ItemType Directory -Path $root -ErrorAction Stop
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing provenance test root through a reparse point: $root"
    }
    $rootCreated = $true
    New-Item -ItemType Directory -Force -Path $cppData, $net10Data, (Split-Path -Parent $cppExe), (Split-Path -Parent $net10Exe) | Out-Null
    Set-Content -LiteralPath $cppExe -Value "cpp benchmark executable" -Encoding ASCII
    Set-Content -LiteralPath $net10Exe -Value "net10 benchmark executable" -Encoding ASCII
    $cppHash = (Get-FileHash -LiteralPath $cppExe -Algorithm SHA256).Hash
    $net10Hash = (Get-FileHash -LiteralPath $net10Exe -Algorithm SHA256).Hash
    $manifest = [pscustomobject]@{
        schema = "paired-benchmark-fixture-v2"
        status = "PASS"
        fixtureId = "provenance-test"
        outputRoot = $root
        cppDatabase = "hmail_perf_pair_cpp_provenance"
        net10Database = "hmail_perf_pair_net10_provenance"
        cppDatabaseVersion = 5708
        net10DatabaseVersion = 6000
        cppDataRoot = $cppData
        net10DataRoot = $net10Data
        cppExecutable = $cppExe
        cppExecutableSha256 = $cppHash
        net10Executable = $net10Exe
        net10ExecutableSha256 = $net10Hash
        dataParity = [pscustomobject]@{ fileCount = 1000; exact = $true; sha256 = ("a" * 64) }
        messageParity = [pscustomobject]@{ rowCount = 1000; exact = $true; sha256 = ("b" * 64) }
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $ports = [ordered]@{ smtp = 2525; imap = 1143; pop3 = 25110 }
    $runId = [Guid]::NewGuid().ToString("D")
    foreach ($implementation in @("cpp", "net10")) {
        $database = if ($implementation -eq "cpp") { $manifest.cppDatabase } else { $manifest.net10Database }
        $dataRoot = if ($implementation -eq "cpp") { $cppData } else { $net10Data }
        $executable = if ($implementation -eq "cpp") { $cppExe } else { $net10Exe }
        $expectedHash = if ($implementation -eq "cpp") { $cppHash } else { $net10Hash }
        $result = Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -RunId $runId -Implementation $implementation -RepositoryRoot $PSScriptRoot -Database $database -DataRoot $dataRoot -ServiceExecutable $executable -Ports $ports
        if (-not $result.manifestBound -or $result.implementation -ne $implementation -or $result.runId -ne $runId -or $result.fixtureId -ne "provenance-test" -or $result.manifestSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or $result.database -ne $database -or $result.dataRoot -ne [IO.Path]::GetFullPath($dataRoot) -or $result.bind -ne "127.0.0.1" -or $result.ports -ne "SMTP 2525, IMAP 1143, POP3 25110" -or $result.executableProvenance.sha256 -ne $expectedHash) {
            throw "valid $implementation provenance did not bind all required fields."
        }
        $script:passed++
    }

    $unbound = Get-LiveBenchmarkProvenance -Implementation net10 -RepositoryRoot $PSScriptRoot -Database $manifest.net10Database -DataRoot $net10Data -ServiceExecutable $net10Exe -Ports $ports
    if ($unbound.manifestBound -or $null -ne $unbound.fixtureId -or $null -ne $unbound.manifestSha256) {
        throw "manifest-free provenance was not classified as unbound."
    }
    $script:passed++

    $artifactReport = [pscustomobject]@{
        provenanceStatus = "MANIFEST_BOUND"
        runId = $runId
        fixtureId = "provenance-test"
        manifestSha256 = $result.manifestSha256
        implementation = "net10"
        database = $manifest.net10Database
        dataRoot = [IO.Path]::GetFullPath($net10Data)
        executableProvenance = $result.executableProvenance
        samples = @([pscustomobject]@{ sequence = 1 })
    }
    $artifactCsv = Join-Path $root "artifact.csv"
    $artifactMarkdown = Join-Path $root "artifact.md"
    [pscustomobject]@{
        runId = $artifactReport.runId
        provenanceStatus = $artifactReport.provenanceStatus
        fixtureId = $artifactReport.fixtureId
        manifestSha256 = $artifactReport.manifestSha256
        implementation = $artifactReport.implementation
        database = $artifactReport.database
        dataRoot = $artifactReport.dataRoot
        executableSha256 = $artifactReport.executableProvenance.sha256
    } | Export-Csv -LiteralPath $artifactCsv -NoTypeInformation
    @(
        "Implementation: $($artifactReport.implementation)",
        "Run ID: $($artifactReport.runId)",
        "Provenance: MANIFEST_BOUND",
        "Fixture ID: $($artifactReport.fixtureId)",
        "Fixture manifest SHA-256: $($artifactReport.manifestSha256)",
        "Executable SHA-256: $($artifactReport.executableProvenance.sha256)"
    ) | Set-Content -LiteralPath $artifactMarkdown -Encoding UTF8
    Assert-LiveBenchmarkManifestBoundArtifact -Report $artifactReport -CsvPath $artifactCsv -MarkdownPath $artifactMarkdown
    $script:passed++
    $artifactReport.provenanceStatus = "UNBOUND"
    Assert-Throws "unbound acceptance artifact" {
        Assert-LiveBenchmarkManifestBoundArtifact -Report $artifactReport -CsvPath $artifactCsv -MarkdownPath $artifactMarkdown
    }

    Assert-Throws "database mismatch" {
        Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -Implementation net10 -RepositoryRoot $PSScriptRoot -Database $manifest.cppDatabase -DataRoot $net10Data -ServiceExecutable $net10Exe -Ports $ports
    }
    Assert-Throws "Data root mismatch" {
        Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -Implementation cpp -RepositoryRoot $PSScriptRoot -Database $manifest.cppDatabase -DataRoot $net10Data -ServiceExecutable $cppExe -Ports $ports
    }
    Assert-Throws "executable mismatch" {
        Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -Implementation cpp -RepositoryRoot $PSScriptRoot -Database $manifest.cppDatabase -DataRoot $cppData -ServiceExecutable $net10Exe -Ports $ports
    }
    Assert-Throws "invalid run id" {
        Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -RunId "not-a-guid" -Implementation net10 -RepositoryRoot $PSScriptRoot -Database $manifest.net10Database -DataRoot $net10Data -ServiceExecutable $net10Exe -Ports $ports
    }
    Assert-Throws "unexpected loopback port" {
        Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -Implementation net10 -RepositoryRoot $PSScriptRoot -Database $manifest.net10Database -DataRoot $net10Data -ServiceExecutable $net10Exe -Ports ([ordered]@{ smtp = 2525; imap = 1144; pop3 = 25110 })
    }
    $manifest.net10ExecutableSha256 = ("c" * 64)
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    Assert-Throws "executable hash mismatch" {
        Get-LiveBenchmarkProvenance -FixtureManifest $manifestPath -Implementation net10 -RepositoryRoot $PSScriptRoot -Database $manifest.net10Database -DataRoot $net10Data -ServiceExecutable $net10Exe -Ports $ports
    }
    Assert-Throws "unsafe manifest path" {
        Read-LiveBenchmarkFixtureManifest -Path (Join-Path (Get-Location).Path "unsafe-fixture.json") -Implementation net10
    }

    Write-Output "PASS: live benchmark provenance tests ($passed assertions)"
}
finally {
    $fullRoot = [IO.Path]::GetFullPath($root)
    if ($rootCreated -and $fullRoot -match '(?i)^C:\\hmail-perf-pair-provenance-test-[0-9a-f-]{36}$' -and (Test-Path -LiteralPath $fullRoot)) {
        if (Test-LiveBenchmarkPathContainsReparsePoint $fullRoot) {
            throw "Refusing to clean a provenance test root that became a reparse point: $fullRoot"
        }
        Remove-Item -LiteralPath $fullRoot -Recurse -Force -ErrorAction Stop
    }
}
