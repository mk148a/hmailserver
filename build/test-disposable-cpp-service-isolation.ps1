$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$sourcePath = Join-Path $repoRoot 'hmailserver\source\Server\hMailServer\hMailServer.cpp'
$preflightPath = Join-Path $repoRoot 'build\live-cpp-isolation-preflight.ps1'
$source = Get-Content -LiteralPath $sourcePath -Raw

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Description)
    if ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing ${Description}: $Needle"
    }
}

Assert-Contains $source 'DISPOSABLE_BENCHMARK_MODE = HasCommandParameter(vecParams, _T("/DisposableBenchmark"));' 'explicit disposable opt-in'
Assert-Contains $source 'if (!DISPOSABLE_BENCHMARK_MODE)' 'default AppID registration guard'
Assert-Contains $source 'const String prefix = _T("/ServiceName=");' 'service-name option'
Assert-Contains $source '{ const_cast<LPTSTR>(SERVICE_NAME.c_str()), ServiceMain },' 'disposable service dispatch binding'
Assert-Contains $source 'RegisterServiceCtrlHandler(SERVICE_NAME.c_str(), ServiceController)' 'disposable service control binding'
Assert-Contains $source 'return String(_T("hMailServer"));' 'legacy service-name fallback'

$guardIndex = $source.IndexOf('if (!DISPOSABLE_BENCHMARK_MODE)', [StringComparison]::Ordinal)
$registerIndex = $source.IndexOf('_AtlModule.RegisterAppID();', $guardIndex, [StringComparison]::Ordinal)
if ($guardIndex -lt 0 -or $registerIndex -lt $guardIndex) {
    throw 'AppID registration is not guarded by the explicit disposable mode.'
}

. $preflightPath
$parameters = (Get-Command Get-CppIsolationPreflight).Parameters
if (-not $parameters.ContainsKey('DisposableRegistrationGuarded')) {
    throw 'C++ isolation preflight is missing the disposable registration guard.'
}

$default = Get-CppIsolationPreflight -TargetExecutable 'C:\hmail-perf-cpp-test\Bin\hMailServer.exe' -ExpectedStagingRoot 'C:\hmail-perf-cpp-test' -ExpectedDatabase 'hmail_perf_cpp_test'
if ($default.registryInstallLocationMismatchAccepted) {
    throw 'Default C++ preflight must not accept an installed registry mismatch.'
}

$guarded = Get-CppIsolationPreflight -TargetExecutable 'C:\hmail-perf-cpp-test\Bin\hMailServer.exe' -ExpectedStagingRoot 'C:\hmail-perf-cpp-test' -ExpectedDatabase 'hmail_perf_cpp_test' -DisposableRegistrationGuarded
if (-not $guarded.registryInstallLocationMismatchAccepted) {
    throw 'Guarded C++ preflight did not record the accepted disposable registration isolation.'
}

Write-Output 'PASS: disposable C++ service isolation contract checks.'
