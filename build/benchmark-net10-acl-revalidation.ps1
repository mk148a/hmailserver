[CmdletBinding()]
param(
    [ValidateSet('Offline', 'Sql')]
    [string] $Backend = 'Offline',
    [string] $OutputDirectory,
    [int] $Warmup = 2,
    [int] $Iterations = 20
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$workspaceRoot = (Get-Item $repoRoot).Parent.FullName
$dotnet = Join-Path $workspaceRoot 'tools\dotnet10\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$project = Join-Path $repoRoot 'hmailserver\source\Server.Net10\benchmarks\HMailServer.Net10.Benchmarks\HMailServer.Net10.Benchmarks.csproj'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repoRoot 'artifacts\benchmarks\acl-revalidation' }

$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
& $dotnet run --project $project --configuration Release -- --mode acl-revalidation --backend $Backend.ToLowerInvariant() --warmup $Warmup --iterations $Iterations --git-commit $gitCommit --output $OutputDirectory
if ($LASTEXITCODE -ne 0 -and $Backend -eq 'Offline') { exit 0 }
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$report = Get-Content (Join-Path $OutputDirectory 'acl-revalidation.json') -Raw | ConvertFrom-Json
if ($report.Implementation -ne 'net10' -or $report.Scenario -ne 'imap-acl-command-boundary-revalidation') { throw 'Unexpected ACL benchmark identity.' }
if ($Backend -eq 'Offline' -and $report.Status -ne 'not-run') { throw 'Offline mode must not run a fabricated latency measurement.' }
if ($Backend -eq 'Sql' -and $report.Status -ne 'completed') { throw 'SQL ACL benchmark did not complete.' }
Write-Host "ACL benchmark report: $OutputDirectory"
