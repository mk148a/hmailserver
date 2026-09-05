[CmdletBinding()]
param([string]$PackageZip)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$setupSource = Join-Path $PSScriptRoot 'setup-net10-upgrade.ps1'
$scripts = @('upgrade-net10-from-legacy.ps1', 'install-net10-service.ps1',
    'net10-service-rollback.ps1', 'net10-rollback-archive-preflight.ps1')
$requiredBin = @('hMailServer.exe', 'hMailServer.dll', 'hMailServer.tlb', '7za.exe',
    'public_suffix_list.dat', 'public_suffix_list.meta.json', 'hMailServer.deps.json',
    'hMailServer.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')
$checks = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-Rejected([scriptblock]$Action, [string]$Pattern) {
    $message = $null
    try { & $Action | Out-Null } catch { $message = $_.Exception.Message }
    Assert-True ($null -ne $message -and $message -match $Pattern) "Expected rejection '$Pattern'; got '$message'."
}

foreach ($name in @('build-net10-upgrade-package.ps1', 'setup-net10-upgrade.ps1', 'test-net10-upgrade-package.ps1')) {
    $path = Join-Path $PSScriptRoot $name
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Missing script: $name"
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$errors) | Out-Null
    Assert-True ($errors.Count -eq 0) "Parse errors in ${name}: $errors"
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$fixture = Join-Path $tempRoot ('hmail package test ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    $package = Join-Path $fixture 'Permanent Net10'
    $legacy = Join-Path $fixture 'Legacy Install'
    $data = Join-Path $fixture 'Mail Data'
    foreach ($directory in @("$package\Bin", "$package\Scripts", "$package\DBScripts", "$legacy\Bin", $data)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $setup = Join-Path $package 'Setup.ps1'
    Copy-Item -LiteralPath $setupSource -Destination $setup
    foreach ($name in $requiredBin) { Set-Content -LiteralPath (Join-Path "$package\Bin" $name) -Value 'fixture only' }
    foreach ($name in $scripts) { Set-Content -LiteralPath (Join-Path "$package\Scripts" $name) -Value "throw 'Real installer must never run in this fixture.'" }
    Set-Content -LiteralPath "$package\DBScripts\Upgrade5708to6000MSSQL.sql" -Value '-- fixture only'
    $stub = @'
[CmdletBinding()]
param($BinDirectory, $InitializationFile, $BackupArchive, $SqlRollbackBackupPath,
    $UpgradeReportPath, $HandoffManifestPath, $ExpectedTargetIdentity, $UpgradeScriptPath,
    $OutputDirectory, [switch]$Execute, [switch]$Start)
[pscustomobject]@{
    mode = if ($Execute) { 'Execute' } else { 'PlanOnly' }
    parameters = $PSBoundParameters
}
'@
    Set-Content -LiteralPath "$package\Scripts\upgrade-net10-from-legacy.ps1" -Value $stub
    $ini = Join-Path $legacy 'Bin\hMailServer.ini'
    $iniText = "[Directories]`r`nProgramFolder=`"$legacy`"`r`nDataFolder=$data`r`n[Database]`r`nPassword=fixture-secret-not-for-package"
    Set-Content -LiteralPath $ini -Value $iniText
    $backup = Join-Path $fixture 'Full compressed backup.7z'
    Set-Content -LiteralPath $backup -Value 'Archive validation belongs to the guarded runner.'
    $rollback = Join-Path $fixture 'New SQL rollback.bak'
    $arguments = @{ InitializationFile = $ini; BackupArchive = $backup; SqlRollbackBackupPath = $rollback }
    $before = (Get-FileHash -LiteralPath $ini).Hash
    $plan = & $setup @arguments
    Assert-True ($plan.mode -eq 'PlanOnly') 'Setup must default to PlanOnly.'
    $p = $plan.parameters
    Assert-True (-not $p.Execute -and -not $p.Start) 'Setup enabled execution/start by default.'
    Assert-True ($p.BinDirectory -eq "$package\Bin") 'Setup did not select its own Bin.'
    Assert-True ($p.InitializationFile -eq $ini -and $p.BackupArchive -eq $backup -and $p.SqlRollbackBackupPath -eq $rollback) 'Explicit paths were not preserved.'
    Assert-True ($p.UpgradeScriptPath -eq "$package\DBScripts\Upgrade5708to6000MSSQL.sql") 'SQL script path was not explicit.'
    Assert-True ($p.ExpectedTargetIdentity -eq ([Environment]::MachineName + '|' + $ini)) 'Target identity does not bind machine and INI.'
    Assert-True ($p.OutputDirectory.StartsWith("$package\Reports\", [StringComparison]::OrdinalIgnoreCase)) 'Reports are not package-local and dedicated.'
    Assert-True (Test-Path -LiteralPath $p.OutputDirectory -PathType Container) 'Report directory was not created.'
    Assert-True ($p.UpgradeReportPath -eq (Join-Path $p.OutputDirectory 'upgrade-report.json')) 'Unexpected report path.'
    Assert-True ($p.HandoffManifestPath -eq (Join-Path $p.OutputDirectory 'handoff-manifest.json')) 'Unexpected handoff path.'
    $second = & $setup @arguments -Start
    Assert-True ($second.mode -eq 'PlanOnly' -and $second.parameters.Start) 'Start must not imply Execute.'
    Assert-True ($second.parameters.OutputDirectory -ne $p.OutputDirectory) 'Report paths were reused.'
    $executed = & $setup @arguments -Execute -Start
    Assert-True ($executed.mode -eq 'Execute' -and $executed.parameters.Start) 'Explicit execution/start was not forwarded.'
    Assert-True ((Get-FileHash -LiteralPath $ini).Hash -eq $before) 'Setup modified the legacy INI.'
    Assert-True (-not (Test-Path -LiteralPath $rollback)) 'Setup created the SQL rollback file itself.'
    Set-Content -LiteralPath $rollback -Value 'do not overwrite'
    Assert-Rejected { & $setup @arguments } 'SqlRollbackBackupPath.*(new|exist)'
    Remove-Item -LiteralPath $rollback
    Assert-Rejected { & $setup -InitializationFile $ini -BackupArchive $backup -SqlRollbackBackupPath $backup } 'SqlRollbackBackupPath'
    Assert-Rejected { & $setup -InitializationFile 'relative.ini' -BackupArchive $backup -SqlRollbackBackupPath $rollback } 'absolute'
    Assert-Rejected { & $setup -InitializationFile $ini -BackupArchive "$fixture\missing.7z" -SqlRollbackBackupPath $rollback } 'BackupArchive'
    foreach ($forbidden in @($legacy, $data)) {
        $nested = Join-Path $forbidden 'Net10 Package'
        New-Item -ItemType Directory -Path $nested | Out-Null
        Copy-Item -LiteralPath $setupSource -Destination "$nested\Setup.ps1"
        Assert-Rejected { & "$nested\Setup.ps1" @arguments } 'legacy|DataFolder'
        Assert-True (-not (Test-Path -LiteralPath "$nested\Reports")) 'Rejected package wrote reports.'
    }
    Set-Content -LiteralPath $ini -Value "[Directories]`nProgramFolder=$legacy"
    Assert-Rejected { & $setup @arguments } 'DataFolder'
    Set-Content -LiteralPath $ini -Value ($iniText + "`n[Directories]`nDataFolder=$package")
    Assert-Rejected { & $setup @arguments } 'Duplicate'
    Set-Content -LiteralPath $ini -Value $iniText
    Set-Content -LiteralPath "$package\Scripts\upgrade-net10-from-legacy.ps1" -Value "throw 'stub runner failed'"
    Assert-Rejected { & $setup @arguments -Execute } 'stub runner failed'

    if ($PackageZip) {
        $zipPath = (Get-Item -LiteralPath $PackageZip).FullName
        $checksum = (Get-Content -LiteralPath ($zipPath + '.sha256') -Raw).Trim().Split(' ')[0]
        Assert-True ($checksum -eq (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash) 'ZIP checksum mismatch.'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
            Assert-True (($names | Select-Object -Unique).Count -eq $names.Count) 'Duplicate ZIP entries.'
            foreach ($name in $names) {
                Assert-True ($name -notmatch '(^/|:|(^|/)\.\.(/|$))') "Unsafe ZIP entry: $name"
            }
        } finally { $zip.Dispose() }
        $extracted = Join-Path $fixture 'Verified ZIP'
        [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extracted)
        $manifest = Get-Content -LiteralPath "$extracted\manifest.json" -Raw | ConvertFrom-Json
        Assert-True ($manifest.sourceRevision -match '^[0-9a-f]{40}$') 'Missing source revision.'
        Assert-True ($manifest.runtimeIdentifier -eq 'win-x64' -and $manifest.selfContained -eq $true) 'Wrong runtime metadata.'
        Assert-True ($manifest.configuration -eq 'Release' -and -not $manifest.trimmed -and -not $manifest.singleFile) 'Wrong publish metadata.'
        $files = @(Get-ChildItem -LiteralPath $extracted -File -Recurse)
        Assert-True ($files.Count -eq @($manifest.files).Count + 1) 'Manifest must cover every file except itself.'
        Assert-True (@($manifest.files.path | Select-Object -Unique).Count -eq @($manifest.files).Count) 'Duplicate manifest entries.'
        foreach ($file in $manifest.files) {
            Assert-True ($names -contains $file.path) "Manifest entry not in ZIP: $($file.path)"
            $actual = Get-Item -LiteralPath (Join-Path $extracted $file.path)
            Assert-True ($actual.Length -eq $file.length -and (Get-FileHash -LiteralPath $actual.FullName).Hash -eq $file.sha256) "File hash/length mismatch: $($file.path)"
        }
        foreach ($name in $requiredBin) { Assert-True ($names -contains "Bin/$name") "Missing runtime file: $name" }
        $runtime = Get-Content -LiteralPath "$extracted\Bin\hMailServer.runtimeconfig.json" -Raw | ConvertFrom-Json
        Assert-True (@($runtime.runtimeOptions.includedFrameworks).Count -gt 0) 'Runtime is not self-contained.'
        $sourceFiles = @{ 'Setup.ps1' = $setupSource; 'DBScripts/Upgrade5708to6000MSSQL.sql' = "$repoRoot\hmailserver\source\DBScripts\Upgrade5708to6000MSSQL.sql" }
        foreach ($name in $scripts) { $sourceFiles["Scripts/$name"] = Join-Path $PSScriptRoot $name }
        foreach ($name in $sourceFiles.Keys) {
            Assert-True ((Get-FileHash -LiteralPath (Join-Path $extracted $name)).Hash -eq (Get-FileHash -LiteralPath $sourceFiles[$name]).Hash) "Packaged source differs: $name"
        }
        foreach ($name in $names) {
            $allowed = $sourceFiles.ContainsKey($name) -or $name -in @('README.md', 'manifest.json') -or
                $name -match '^Licenses/' -or $name -match '^Bin/'
            Assert-True $allowed "Unexpected package file: $name"
        }
        Assert-True ($names -contains 'Licenses/hmailserver/installation/License.rtf') 'Missing hMailServer license.'
        Assert-True ($names -contains 'Licenses/hmailserver/installation/Extras/7za.exe.license.txt') 'Missing 7-Zip license.'
        Write-Output "Verified package: $zipPath"
    }
    Write-Output "Net10 upgrade packaging tests passed ($checks checks; runner stub only)."
} finally {
    $resolved = (Get-Item -LiteralPath $fixture).FullName
    if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
