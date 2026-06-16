Param(
    [switch]$RequireMsBuild
)

$ErrorActionPreference = 'Stop'
$failed = $false

function Write-Check {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Detail
    )

    if ($Ok) {
        Write-Host "[OK]   $Name - $Detail" -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] $Name - $Detail" -ForegroundColor Red
        $script:failed = $true
    }
}

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$localDotnet = Join-Path $workspaceRoot 'tools\dotnet10\dotnet.exe'

$dotnet = $null
if (Test-Path $localDotnet) {
    $dotnet = Get-Item $localDotnet
}
else {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
}
$dotnetPath = if ($dotnet -and $dotnet.PSObject.Properties.Name -contains 'Source') { $dotnet.Source } elseif ($dotnet) { $dotnet.FullName } else { $null }
Write-Check "dotnet" ($null -ne $dotnetPath) ($(if ($dotnetPath) { $dotnetPath } else { "not found on PATH" }))

if ($dotnetPath) {
    $sdks = & $dotnetPath --list-sdks
    $hasNet10Sdk = $sdks | Where-Object { $_ -match '^10\.' }
    Write-Check ".NET 10 SDK" ($null -ne $hasNet10Sdk) ($(if ($hasNet10Sdk) { ($hasNet10Sdk -join ', ') } else { "install the .NET 10 SDK, not only the runtime" }))

    $runtimes = & $dotnetPath --list-runtimes
    $hasNet10Runtime = $runtimes | Where-Object { $_ -match '^Microsoft\.NETCore\.App 10\.' }
    $hasNet10Desktop = $runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 10\.' }
    Write-Check ".NET 10 runtime" ($null -ne $hasNet10Runtime) ($(if ($hasNet10Runtime) { ($hasNet10Runtime -join ', ') } else { "missing Microsoft.NETCore.App 10.x" }))
    Write-Check ".NET 10 WindowsDesktop runtime" ($null -ne $hasNet10Desktop) ($(if ($hasNet10Desktop) { ($hasNet10Desktop -join ', ') } else { "missing Microsoft.WindowsDesktop.App 10.x" }))
}

if ($RequireMsBuild) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $msbuild = $null
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -version '[17.0,18.0)' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }

    if (-not $msbuild) {
        $msbuildCmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
        if ($msbuildCmd) {
            $msbuild = $msbuildCmd.Source
        }
    }

    Write-Check "MSBuild 17.x" ($null -ne $msbuild) ($(if ($msbuild) { $msbuild } else { "install Visual Studio 2022/Build Tools with MSBuild" }))
}

if ($failed) {
    exit 1
}
