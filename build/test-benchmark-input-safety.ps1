$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$stamp = [Guid]::NewGuid().ToString('N')
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "hmail-benchmark-safety-$stamp"
$approvedInputRoot = "C:\hmail-perf-safety-$stamp"
$approvedOutput = Join-Path $repoRoot "artifacts\benchmarks\paired-cpp-net10-safety-$stamp"
$junctionTarget = Join-Path $tempRoot 'junction-target'
$junctionPath = Join-Path $tempRoot 'junction'
$artifactJunctionPath = Join-Path $repoRoot "artifacts\benchmarks\paired-cpp-net10-symlink-$stamp"

function Assert-ScriptRejects {
    param(
        [string]$ScriptPath,
        [string[]]$Arguments,
        [string]$ExpectedText
    )

    $commandArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ScriptPath) + $Arguments
    $priorErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& powershell.exe @commandArguments 2>&1)
    }
    finally {
        $ErrorActionPreference = $priorErrorActionPreference
    }
    if ($LASTEXITCODE -eq 0) {
        throw "Expected $ScriptPath to reject its input."
    }
    $text = $output -join "`n"
    if ($text -notlike "*$ExpectedText*") {
        throw "Unexpected rejection from ${ScriptPath}: $text"
    }
}

function Assert-PythonRejects {
    param(
        [string[]]$Arguments,
        [string]$ExpectedText
    )

    $priorErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& python (Join-Path $repoRoot 'build\generate-paired-performance-report.py') @Arguments 2>&1)
    }
    finally {
        $ErrorActionPreference = $priorErrorActionPreference
    }
    if ($LASTEXITCODE -eq 0) {
        throw 'Expected the paired report generator to reject its input.'
    }
    $text = $output -join "`n"
    if ($text -notlike "*$ExpectedText*") {
        throw "Unexpected rejection from the paired report generator: $text"
    }
}

function New-JunctionIfSupported {
    param([string]$Path, [string]$Target)

    try {
        New-Item -ItemType Junction -Path $Path -Target $Target -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        Write-Warning "Skipping junction rejection checks because junction creation is unavailable: $($_.Exception.Message)"
        return $false
    }
}

New-Item -ItemType Directory -Path $tempRoot, $junctionTarget, $approvedInputRoot -Force | Out-Null
$backupFile = Join-Path $approvedInputRoot 'backup.bak'
$sourceDataRoot = Join-Path $approvedInputRoot 'source-data'
Set-Content -LiteralPath $backupFile -Value 'not a SQL backup' -Encoding ASCII
New-Item -ItemType Directory -Path $sourceDataRoot -Force | Out-Null

try {
    . (Join-Path $repoRoot 'build\live-cpp-isolation-preflight.ps1')
    Assert-ApprovedBenchmarkExecutable -Path 'C:\hmail-perf-cpp-ascii-20260810\Bin\hMailServer.exe' -Implementation cpp -RepositoryRoot $repoRoot
    Assert-ApprovedBenchmarkExecutable -Path (Join-Path $repoRoot 'artifacts\benchmarks\live-cpp-net10-20260810_152708\LiveListenerHost\bin\Release\net10.0-windows\LiveListenerHost.exe') -Implementation net10 -RepositoryRoot $repoRoot

    $fixtureScript = Join-Path $repoRoot 'build\provision-paired-benchmark-fixture.ps1'
    Assert-ScriptRejects $fixtureScript @(
        '-BackupPath', (Join-Path $repoRoot 'hmailserver\source\DBScripts\Upgrade5708to6000MSSQL.sql'),
        '-SourceDataRoot', $sourceDataRoot,
        '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp"
    ) 'protected or production-like'
    Assert-ScriptRejects $fixtureScript @(
        '-BackupPath', $backupFile,
        '-SourceDataRoot', $repoRoot,
        '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp"
    ) 'protected or production-like'
    Assert-ScriptRejects $fixtureScript @(
        '-BackupPath', $backupFile,
        '-SourceDataRoot', $tempRoot,
        '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp"
    ) 'approved disposable benchmark root'
    Assert-ScriptRejects $fixtureScript @(
        '-BackupPath', $backupFile,
        '-SourceDataRoot', $sourceDataRoot,
        '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp",
        '-Net10BinPath', (Join-Path $repoRoot 'build')
    ) 'pinned to the repository Release output'
    Assert-ScriptRejects $fixtureScript @(
        '-BackupPath', $backupFile,
        '-SourceDataRoot', $sourceDataRoot,
        '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp",
        '-LegacyBinPath', (Join-Path $repoRoot 'build')
    ) 'approved disposable clean C++ build root'
    Assert-ScriptRejects $fixtureScript @(
        '-BackupPath', $backupFile,
        '-SourceDataRoot', $sourceDataRoot,
        '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp",
        '-UpgradeScriptPath', (Join-Path $repoRoot 'build\provision-paired-benchmark-fixture.ps1')
    ) 'pinned to the checked-in repository script'

    $buildScript = Join-Path $repoRoot 'build\build-disposable-legacy-server.ps1'
    Assert-ScriptRejects $buildScript @(
        '-OutputRoot', "C:\hmail-perf-cpp-build-safety-$stamp",
        '-LibrariesRoot', $repoRoot,
        '-MsBuildPath', (Join-Path $repoRoot 'build\provision-paired-benchmark-fixture.ps1')
    ) 'approved Visual Studio MSBuild path'

    $invalidExecutable = Join-Path $repoRoot 'build\provision-paired-benchmark-fixture.ps1'
    foreach ($runner in @(
        'benchmark-net10-live-protocol.ps1',
        'benchmark-net10-live-concurrent-imap.ps1',
        'benchmark-net10-live-smtp-acceptance.ps1'
    )) {
        Assert-ScriptRejects (Join-Path $repoRoot "build\$runner") @(
            '-Implementation', 'net10',
            '-BenchmarkServiceExecutable', $invalidExecutable
        ) 'approved disposable benchmark executable'
    }
    foreach ($runner in @(
        'benchmark-net10-live-imap-search.ps1',
        'benchmark-net10-live-pop3.ps1',
        'benchmark-net10-live-pop3-large-mailbox.ps1',
        'benchmark-net10-live-restart-lifecycle.ps1'
    )) {
        Assert-ScriptRejects (Join-Path $repoRoot "build\$runner") @(
            '-BenchmarkServiceExecutable', $invalidExecutable
        ) 'approved disposable benchmark executable'
    }
    $pop3LargeMailboxRunner = Get-Content -LiteralPath (Join-Path $repoRoot 'build\benchmark-net10-live-pop3-large-mailbox.ps1') -Raw
    $pop3LargeMailboxValidator = Get-Content -LiteralPath (Join-Path $repoRoot 'build\test-net10-live-pop3-large-mailbox.ps1') -Raw
    if ($pop3LargeMailboxRunner -notmatch '\[ValidateRange\(1, 100000\)\]\s*\[int\]\$ExpectedMessages' -or
        $pop3LargeMailboxValidator -notmatch '\[ValidateRange\(1, 100000\)\]\s*\[int\]\$ExpectedMessages') {
        throw 'POP3 large-mailbox runner and validator must support the required 100,000-message acceptance fixture.'
    }
    Assert-ScriptRejects (Join-Path $repoRoot 'build\new-paired-performance-run.ps1') @(
        '-FixtureManifest', (Join-Path $tempRoot 'missing-fixture.json'),
        '-InputRoot', $repoRoot
    ) 'repository benchmark artifacts directory'

    $reportArguments = @(
        '--input-root', $repoRoot,
        '--fixture-manifest', (Join-Path $tempRoot 'missing-fixture.json'),
        '--environment', (Join-Path $tempRoot 'missing-environment.json'),
        '--legacy-build-manifest', (Join-Path $tempRoot 'missing-build.json'),
        '--net10-executable', (Join-Path $tempRoot 'missing-net10.exe'),
        '--repository-root', $repoRoot,
        '--run-descriptor', (Join-Path $tempRoot 'missing-run-descriptor.json')
    )
    Assert-PythonRejects ($reportArguments + @('--output-directory', $repoRoot)) 'approved paired benchmark directory'

    New-Item -ItemType Directory -Path $approvedOutput -Force | Out-Null
    $marker = Join-Path $approvedOutput 'unrelated-marker.txt'
    Set-Content -LiteralPath $marker -Value 'must survive' -Encoding ASCII
    Assert-PythonRejects ($reportArguments + @('--output-directory', $approvedOutput)) 'Required JSON artifact'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw 'The report generator removed an unrelated pre-existing file.'
    }

    if (New-JunctionIfSupported $junctionPath $junctionTarget) {
        Assert-ScriptRejects $fixtureScript @(
            '-BackupPath', (Join-Path $junctionPath 'backup.bak'),
            '-SourceDataRoot', $sourceDataRoot,
            '-OutputRoot', "C:\hmail-perf-pair-safety-$stamp"
        ) 'must not use a reparse point'

        if (New-JunctionIfSupported $artifactJunctionPath $junctionTarget) {
            Assert-PythonRejects ($reportArguments + @('--output-directory', $artifactJunctionPath)) 'symlink or reparse point'
        }
    }

    Write-Output 'Benchmark input safety rejection tests passed.'
}
finally {
    if (Test-Path -LiteralPath $artifactJunctionPath) {
        Remove-Item -LiteralPath $artifactJunctionPath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $junctionPath) {
        Remove-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $approvedOutput) {
        Remove-Item -LiteralPath $approvedOutput -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $approvedInputRoot) {
        Remove-Item -LiteralPath $approvedInputRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
