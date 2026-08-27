param(
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [Parameter(Mandatory = $true)]
    [string]$LibrariesRoot,
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$fullOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$fullLibrariesRoot = [IO.Path]::GetFullPath($LibrariesRoot)

if ($fullOutputRoot -notmatch '(?i)^C:\\hmail-perf-cpp-build-') {
    throw "Output root is not an approved disposable C++ build root: $fullOutputRoot"
}
if ($fullOutputRoot -match '(?i)hmailserver57|hmaildb_test5700') {
    throw "Output root resembles a protected installation: $fullOutputRoot"
}
if (Test-Path -LiteralPath $fullOutputRoot) {
    throw "Refusing to overwrite existing disposable build root: $fullOutputRoot"
}
if (-not (Test-Path -LiteralPath $fullLibrariesRoot -PathType Container)) {
    throw "Legacy dependency root is missing: $fullLibrariesRoot"
}

if ([string]::IsNullOrWhiteSpace($MsBuildPath)) {
    $vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vsWhere -PathType Leaf) {
        $installationPath = (& $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath | Select-Object -First 1).Trim()
        if (-not [string]::IsNullOrWhiteSpace($installationPath)) {
            $MsBuildPath = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
        }
    }
}
if ([string]::IsNullOrWhiteSpace($MsBuildPath)) {
    $MsBuildPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
}
$MsBuildPath = [IO.Path]::GetFullPath($MsBuildPath)
if (-not (Test-Path -LiteralPath $MsBuildPath -PathType Leaf)) {
    throw "MSBuild is missing: $MsBuildPath"
}

$project = Join-Path $repoRoot 'hmailserver\source\Server\hMailServer\hMailServer.vcxproj'
$translation = Join-Path $repoRoot 'hmailserver\source\Translations\english.ini'
$extras = Join-Path $repoRoot 'hmailserver\installation\Extras'
$runtimeFiles = @(
    (Join-Path $fullLibrariesRoot 'openssl-3.5.5\out64\bin\libcrypto-3-x64.dll'),
    (Join-Path $fullLibrariesRoot 'openssl-3.5.5\out64\bin\libssl-3-x64.dll'),
    (Join-Path $fullLibrariesRoot 'postgresql-18.3\builddir\src\interfaces\libpq\libpq.dll')
)
foreach ($required in @($project, $translation) + $runtimeFiles) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required legacy build input is missing: $required"
    }
}

$bin = Join-Path $fullOutputRoot 'Bin'
$obj = Join-Path $fullOutputRoot 'obj'
$languages = Join-Path $bin 'Languages'
New-Item -ItemType Directory -Force -Path $bin, $obj, $languages | Out-Null

$priorLibraries = $env:hMailServerLibs
$priorCl = $env:CL
try {
    $env:hMailServerLibs = $fullLibrariesRoot
    $env:CL = '/utf-8 /wd4566 /wd4996 /FS'
    & $MsBuildPath $project /m /v:minimal "/p:Configuration=$Configuration" /p:Platform=x64 "/p:OutDir=$bin\" "/p:IntDir=$obj\" /p:PreBuildEventUseInBuild=false /p:PostBuildEventUseInBuild=false "/p:hMailServerLibs=$fullLibrariesRoot"
    if ($LASTEXITCODE -ne 0) {
        throw "Legacy C++ build failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:hMailServerLibs = $priorLibraries
    $env:CL = $priorCl
}

$executable = Join-Path $bin 'hMailServer.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Legacy C++ build did not produce hMailServer.exe."
}

Copy-Item -LiteralPath $runtimeFiles -Destination $bin -Force
Copy-Item -LiteralPath $translation -Destination (Join-Path $languages 'english.ini') -Force
foreach ($extraName in @('7za.exe', 'dh2048.pem', 'tlds.txt')) {
    $extraPath = Join-Path $extras $extraName
    if (Test-Path -LiteralPath $extraPath -PathType Leaf) {
        Copy-Item -LiteralPath $extraPath -Destination $bin -Force
    }
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$report = [ordered]@{
    schema = 'legacy-disposable-build-v1'
    status = 'PASS'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    sourceCommit = $commit
    configuration = $Configuration
    platform = 'x64'
    msbuildPath = $MsBuildPath
    librariesRoot = $fullLibrariesRoot
    outputRoot = $fullOutputRoot
    executable = $executable
    executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    executableBytes = (Get-Item -LiteralPath $executable).Length
    languageFile = (Join-Path $languages 'english.ini')
    postBuildRegistrationDisabled = $true
}
$reportPath = Join-Path $fullOutputRoot 'legacy-build.json'
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | ConvertTo-Json -Depth 4
