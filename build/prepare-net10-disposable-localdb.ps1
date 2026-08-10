[CmdletBinding()]
param(
    [string] $InstanceName = 'MSSQLLocalDB',
    [string] $DataRoot,
    [string] $EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $PSScriptRoot '..\artifacts\net10-disposable'
}

function Get-ToolPath([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Required tool was not found: $Name"
    }

    return $command.Source
}

function Invoke-SqlReadOnly([string] $SqlCmdPath, [string] $Server, [string] $Query) {
    $output = & $SqlCmdPath -S $Server -d master -E -l 10 -W -s '|' -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Read-only LocalDB catalog query failed: $($output -join [Environment]::NewLine)"
    }

    return @($output | Where-Object { $_ -and $_ -notmatch '^[- ]+$' -and $_ -notmatch '^\(' })
}

$sqlLocalDbPath = Get-ToolPath 'SqlLocalDB.exe'
$sqlCmdPath = Get-ToolPath 'sqlcmd.exe'
$instanceInfo = @(& $sqlLocalDbPath info $InstanceName 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "LocalDB instance '$InstanceName' is not available: $($instanceInfo -join [Environment]::NewLine)"
}

$ownerLine = $instanceInfo | Where-Object { $_ -match '^Owner:\s+' } | Select-Object -First 1
$owner = if ($ownerLine) { ($ownerLine -replace '^Owner:\s+', '').Trim() } else { '' }
$expectedOwner = "$env:USERDOMAIN\$env:USERNAME"
if (-not $owner.Equals($expectedOwner, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing LocalDB instance not owned by the current user. Owner='$owner'; expected='$expectedOwner'."
}

$server = "(localdb)\$InstanceName"
$null = & $sqlLocalDbPath start $InstanceName 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Could not start the user-owned LocalDB instance '$InstanceName'."
}

$catalogLines = Invoke-SqlReadOnly $sqlCmdPath $server 'SELECT name, state_desc, user_access_desc, is_read_only FROM sys.databases ORDER BY name;'
$databaseNames = @(
    $catalogLines |
        Select-Object -Skip 1 |
        Where-Object { $_ -match '\|' } |
        ForEach-Object { ($_ -split '\|', 2)[0].Trim() } |
        Where-Object { $_ -and $_ -notmatch '^name$' -and $_ -notmatch '^-+$' }
)

if ([string]::IsNullOrWhiteSpace($DataRoot)) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ', [Globalization.CultureInfo]::InvariantCulture)
    $DataRoot = Join-Path $env:TEMP "hmailserver-net10-disposable-$stamp-$([Guid]::NewGuid().ToString('N'))"
}

$fullDataRoot = [IO.Path]::GetFullPath($DataRoot)
$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
if (-not $fullDataRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($fullDataRoot) -notmatch '^hmailserver-net10-disposable-') {
    throw "DataRoot must be a new hmailserver-net10-disposable-* directory under the current user's TEMP path."
}

if (Test-Path -LiteralPath $fullDataRoot) {
    throw "Refusing to reuse an existing disposable DataRoot: $fullDataRoot"
}

$null = New-Item -ItemType Directory -Path $fullDataRoot
$null = New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$markerPath = Join-Path $fullDataRoot '.net10-disposable-data-root'
Set-Content -LiteralPath $markerPath -Value "hMailServer .NET 10 disposable Data root`nCreatedUtc=$([DateTime]::UtcNow.ToString('o'))`nLocalDbInstance=$InstanceName`n" -Encoding UTF8 -NoNewline

$connectionString = "Server=$server;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=10"
$environmentScript = Join-Path $EvidenceDirectory 'Use-Net10DisposableSql.ps1'
@"
`$env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION = '$connectionString'
`$env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE = '1'
`$env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_DATA_DIRECTORY = '$fullDataRoot'
Write-Host 'Net10 disposable SQL/Data environment enabled for this PowerShell process.'
Write-Host 'Connection: $connectionString'
Write-Host 'Data directory: $fullDataRoot'
"@ | Set-Content -LiteralPath $environmentScript -Encoding UTF8

$report = [ordered]@{
    status = 'READY'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    machine = $env:COMPUTERNAME
    user = $expectedOwner
    localDbInstance = $InstanceName
    localDbOwner = $owner
    localDbServer = $server
    connectionString = $connectionString
    isolatedCreateOptIn = '1'
    dataDirectory = $fullDataRoot
    markerFile = $markerPath
    databaseNamesBeforeTests = $databaseNames
    existingMssqlServerDatabasesNotUsed = @('HmailDb_Test5700')
    environmentScript = $environmentScript
    rollbackScript = (Join-Path $PSScriptRoot 'remove-net10-disposable-localdb.ps1')
    safety = @(
        'Uses only the current users LocalDB instance.',
        'Does not connect to MSSQLSERVER.',
        'Does not use an existing hMailServer database or Data directory.',
        'Test fixtures must create GUID-named databases and drop them in finally blocks.'
    )
}
$reportPath = Join-Path $EvidenceDirectory 'net10-disposable-localdb.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Output "READY: $reportPath"
Write-Output "Use: . '$environmentScript'"
Write-Output "DataRoot: $fullDataRoot"
