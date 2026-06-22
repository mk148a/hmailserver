Param(
    [Parameter(Mandatory = $true)]
    [string]$SourceIdl,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
    throw 'The hMailServer type library can only be generated on Windows.'
}

$source = (Get-Item -LiteralPath $SourceIdl).FullName
$output = [System.IO.Path]::GetFullPath($OutputPath)
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'vswhere.exe was not found. Install Visual Studio 2022 or Build Tools.'
}

$visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) {
    throw 'Visual Studio 2022 C++ build tools were not found.'
}

$developerCommand = Join-Path $visualStudio 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path -LiteralPath $developerCommand)) {
    throw "VsDevCmd.bat was not found under $visualStudio."
}

$temporaryRoot = [System.IO.Path]::GetTempPath()
$workingDirectory = Join-Path $temporaryRoot ("hmailserver-net10-tlb-{0}-{1}" -f $PID, [Guid]::NewGuid().ToString('N'))
$driveName = [char[]](90..84) |
    ForEach-Object { [string]$_ } |
    Where-Object { -not (Test-Path -LiteralPath ("{0}:\" -f $_)) } |
    Select-Object -First 1
if (-not $driveName) {
    throw 'No temporary drive letter is available for MIDL generation.'
}

$drive = "${driveName}:"
$mapped = $false
try {
    New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination (Join-Path $workingDirectory 'hMailServer.idl') -Force

    & subst.exe $drive $workingDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Could not map temporary MIDL drive $drive."
    }
    $mapped = $true

    $command = 'call "{0}" -arch=x64 -host_arch=x64 >nul && midl /nologo /env x64 /tlb hMailServer.tlb /h hMailServer.h /iid hMailServer_i.c /proxy hMailServer_p.c hMailServer.idl' -f $developerCommand
    Push-Location "$drive\"
    try {
        & $env:ComSpec /d /s /c $command
        if ($LASTEXITCODE -ne 0) {
            throw "MIDL failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    $outputDirectory = Split-Path -Parent $output
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $workingDirectory 'hMailServer.tlb') -Destination $output -Force
}
finally {
    if ($mapped) {
        & subst.exe $drive /d | Out-Null
    }

    if (Test-Path -LiteralPath $workingDirectory) {
        $resolvedWorkingDirectory = (Get-Item -LiteralPath $workingDirectory).FullName
        if (-not $resolvedWorkingDirectory.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected temporary directory: $resolvedWorkingDirectory"
        }

        Remove-Item -LiteralPath $resolvedWorkingDirectory -Recurse -Force
    }
}
