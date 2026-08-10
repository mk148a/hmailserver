[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string] $DataRoot,
    [string] $InstanceName = 'MSSQLLocalDB',
    [string[]] $DatabaseName,
    [switch] $StopInstance
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$fullDataRoot = [IO.Path]::GetFullPath($DataRoot)
$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
if (-not $fullDataRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($fullDataRoot) -notmatch '^hmailserver-net10-disposable-') {
    throw "Refusing cleanup outside a hmailserver-net10-disposable-* directory under TEMP."
}

$markerPath = Join-Path $fullDataRoot '.net10-disposable-data-root'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    throw "Refusing cleanup without the disposable DataRoot marker: $markerPath"
}

$sqlLocalDbPath = (Get-Command SqlLocalDB.exe -ErrorAction Stop).Source
$sqlCmdPath = (Get-Command sqlcmd.exe -ErrorAction Stop).Source
$server = "(localdb)\$InstanceName"

foreach ($name in @($DatabaseName | Where-Object { $_ })) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name -notmatch '^hmailserver_net10_[A-Za-z0-9_]+$') {
        throw "DatabaseName must be an explicit GUID-scoped hmailserver_net10_* database name."
    }

    $escaped = $name.Replace(']', ']]')
    $sql = "IF DB_ID(N'$($name.Replace("'", "''"))') IS NOT NULL BEGIN ALTER DATABASE [$escaped] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escaped]; END"
    if ($PSCmdlet.ShouldProcess($name, 'Drop disposable LocalDB database')) {
        & $sqlCmdPath -S $server -d master -E -l 10 -b -Q $sql
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to drop disposable database '$name'."
        }
    }
}

if ($PSCmdlet.ShouldProcess($fullDataRoot, 'Remove disposable Data directory')) {
    Remove-Item -LiteralPath $fullDataRoot -Recurse -Force
}

if ($StopInstance -and $PSCmdlet.ShouldProcess($InstanceName, 'Stop user-owned LocalDB instance')) {
    & $sqlLocalDbPath stop $InstanceName -i
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to stop LocalDB instance '$InstanceName'."
    }
}

Write-Output "REMOVED: $fullDataRoot"
