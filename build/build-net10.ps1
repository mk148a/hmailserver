Param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$localDotnet = Join-Path $workspaceRoot 'tools\dotnet10\dotnet.exe'
$projects = @(
    (Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Service\HMailServer.Service.csproj'),
    (Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Indexing\HMailServer.Indexing.csproj'),
    (Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.Delivery\HMailServer.Delivery.csproj'),
    (Join-Path $repoRoot 'hmailserver\source\Server.Net10\src\HMailServer.ComInterop\HMailServer.ComInterop.csproj')
)

if (Test-Path $localDotnet) {
    $dotnet = (Get-Item $localDotnet).FullName
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

foreach ($project in $projects) {
    & $dotnet build $project --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
