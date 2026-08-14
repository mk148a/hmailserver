Param(
    [string]$Configuration = 'Release',
    [string]$BinDirectory
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'net10-service-rollback.ps1')

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session.'
    }
}

Assert-Administrator

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
if (-not $BinDirectory) {
    $BinDirectory = Join-Path $repoRoot "hmailserver\source\Server.Net10\src\HMailServer.Service\bin\$Configuration\net10.0-windows"
}

$executable = Join-Path ([System.IO.Path]::GetFullPath($BinDirectory)) 'hMailServer.exe'
$serviceName = 'hMailServer'
$serviceDetails = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
$serviceSnapshot = if ($null -ne $serviceDetails) {
    New-Net10ServiceRollbackSnapshot -Service $serviceDetails
}
else {
    $null
}
$serviceDeleted = $false
if ($serviceDetails) {
    $registeredExecutable = Get-Net10ServiceExecutablePath -PathName $serviceDetails.PathName
    if (-not $registeredExecutable.Equals($executable, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove service '$serviceName' because it points to '$registeredExecutable', not '$executable'."
    }

    $service = Get-Service -Name $serviceName
    if ($service.Status -ne [ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $serviceName
        $service.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
    }

    try {
        & sc.exe delete $serviceName
        if ($LASTEXITCODE -ne 0) {
            throw "Windows service removal failed with exit code $LASTEXITCODE."
        }

        $serviceDeleted = $true
        if (Test-Path -LiteralPath $executable) {
            & $executable --unregister-com
            if ($LASTEXITCODE -ne 0) {
                throw "COM unregistration failed with exit code $LASTEXITCODE."
            }
        }
        else {
            throw "Service executable was not found, so owned COM registrations could not be verified and removed: $executable"
        }
    }
    catch {
        $failureMessage = $_.Exception.Message
        if ($serviceDeleted -and $null -ne $serviceSnapshot) {
            try {
                Restore-Net10ServiceRollbackSnapshot -Snapshot $serviceSnapshot
            }
            catch {
                throw "Uninstall failed: $failureMessage Service rollback failed: $($_.Exception.Message)"
            }
        }

        throw
    }
}
elseif (Test-Path -LiteralPath $executable) {
    & $executable --unregister-com
    if ($LASTEXITCODE -ne 0) {
        throw "COM unregistration failed with exit code $LASTEXITCODE."
    }
}
else {
    throw "Service executable was not found, so owned COM registrations could not be verified and removed: $executable"
}
