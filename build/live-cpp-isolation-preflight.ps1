function Get-InstalledHmailServerLocations {
    $locations = [System.Collections.Generic.List[object]]::new()
    foreach ($view in [Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32) {
        $baseKey = $null
        $key = $null
        try {
            $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
                [Microsoft.Win32.RegistryHive]::LocalMachine,
                $view)
            $key = $baseKey.OpenSubKey("SOFTWARE\hMailServer")
            if ($null -ne $key) {
                $installLocation = [string]$key.GetValue("InstallLocation", "")
                if (-not [string]::IsNullOrWhiteSpace($installLocation)) {
                    $locations.Add([pscustomobject]@{
                        view = $view.ToString()
                        installLocation = $installLocation
                    })
                }
            }
        }
        finally {
            if ($null -ne $key) { $key.Dispose() }
            if ($null -ne $baseKey) { $baseKey.Dispose() }
        }
    }
    return $locations.ToArray()
}

function Get-CppIsolationPreflight {
    param(
        [string]$TargetExecutable,
        [string]$ExpectedStagingRoot,
        [string]$ExpectedDatabase
    )

    $failures = [System.Collections.Generic.List[string]]::new()
    $targetBin = [IO.Path]::GetFullPath((Split-Path -Parent $TargetExecutable))
    $registryLocations = @(Get-InstalledHmailServerLocations)
    $normalizedTargetBin = $targetBin.TrimEnd('\')
    foreach ($location in $registryLocations) {
        $configuredBin = [IO.Path]::GetFullPath((Join-Path $location.installLocation "Bin")).TrimEnd('\')
        if (-not [string]::Equals($configuredBin, $normalizedTargetBin, [StringComparison]::OrdinalIgnoreCase)) {
            $failures.Add("Legacy C++ launch refused: $($location.view) HKLM hMailServer InstallLocation resolves to '$configuredBin', not the disposable target '$normalizedTargetBin'.")
        }
    }

    $service = Get-CimInstance Win32_Service -Filter "Name='hMailServer'" -ErrorAction SilentlyContinue
    $serviceState = if ($null -eq $service) { "missing" } else { [string]$service.State }
    if ($null -ne $service -and $service.State -ne "Stopped") {
        $failures.Add("Legacy C++ launch refused: hMailServer service state is '$($service.State)'; a separate staging VM is required.")
    }

    $iniPath = Join-Path $ExpectedStagingRoot "Bin\hMailServer.ini"
    $iniText = if (Test-Path -LiteralPath $iniPath -PathType Leaf) { Get-Content -LiteralPath $iniPath -Raw } else { "" }
    if ([string]::IsNullOrWhiteSpace($iniText)) {
        $failures.Add("Legacy C++ launch refused: disposable hMailServer.ini is missing at '$iniPath'.")
    }
    else {
        if ($iniText -notmatch "(?m)^Database=$([regex]::Escape($ExpectedDatabase))\s*$") {
            $failures.Add("Legacy C++ launch refused: disposable INI does not name expected database '$ExpectedDatabase'.")
        }
        $expectedData = [regex]::Escape((Join-Path $ExpectedStagingRoot "Data"))
        if ($iniText -notmatch "(?mi)^DataFolder=$expectedData\\?\s*$") {
            $failures.Add("Legacy C++ launch refused: disposable INI does not point DataFolder at '$ExpectedStagingRoot\Data'.")
        }
    }

    [pscustomobject]@{
        targetExecutable = [IO.Path]::GetFullPath($TargetExecutable)
        targetBin = $targetBin
        registryInstallLocations = $registryLocations
        serviceState = $serviceState
        iniPath = $iniPath
        failures = $failures.ToArray()
        passed = $failures.Count -eq 0
    }
}

function Get-CppExecutableProvenance {
    param([string]$TargetExecutable)

    $file = Get-Item -LiteralPath $TargetExecutable -ErrorAction Stop
    [pscustomobject]@{
        path = $file.FullName
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        length = $file.Length
        lastWriteTimeUtc = $file.LastWriteTimeUtc.ToString("o")
    }
}
