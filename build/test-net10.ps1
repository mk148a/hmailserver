Param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$localDotnet = Join-Path $workspaceRoot 'tools\dotnet10\dotnet.exe'
$testProject = Join-Path $repoRoot 'hmailserver\source\Server.Net10\tests\HMailServer.Net10.Tests\HMailServer.Net10.Tests.csproj'

if (Test-Path $localDotnet) {
    $dotnet = (Get-Item $localDotnet).FullName
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

& $dotnet test $testProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
