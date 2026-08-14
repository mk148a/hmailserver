Set-StrictMode -Version Latest

function Get-Net10ServiceExecutablePath {
    param(
        [Parameter(Mandatory)]
        [string]$PathName
    )

    $text = $PathName.Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw 'Service PathName is empty.'
    }

    if ($text.StartsWith('"', [StringComparison]::Ordinal)) {
        $closingQuote = $text.IndexOf('"', 1)
        if ($closingQuote -lt 2) {
            throw "Service PathName has an unterminated quoted executable: $PathName"
        }

        return [IO.Path]::GetFullPath($text.Substring(1, $closingQuote - 1))
    }

    $executable = ($text -split '\s+', 2)[0]
    return [IO.Path]::GetFullPath($executable)
}

function New-Net10ServiceRollbackSnapshot {
    param(
        [Parameter(Mandatory)]
        [psobject]$Service
    )

    $dependencies = @()
    if ($null -ne $Service.Dependencies) {
        $dependencies = @($Service.Dependencies | ForEach-Object { [string]$_ } | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_)
        })
    }

    [pscustomobject]@{
        ServiceName = [string]$Service.Name
        PathName = [string]$Service.PathName
        StartMode = [string]$Service.StartMode
        ErrorControl = [string]$Service.ErrorControl
        DisplayName = [string]$Service.DisplayName
        Description = [string]$Service.Description
        Dependencies = [string[]]$dependencies
    }
}

function Get-Net10ScStartMode {
    param([string]$StartMode)

    switch -Regex ($StartMode) {
        '^(Auto|Automatic)$' { return 'auto' }
        '^Manual$' { return 'demand' }
        '^Disabled$' { return 'disabled' }
        default { throw "Unsupported service start mode for rollback: '$StartMode'." }
    }
}

function Get-Net10ScErrorControl {
    param([string]$ErrorControl)

    switch -Regex ($ErrorControl) {
        '^Normal$' { return 'normal' }
        '^Severe$' { return 'severe' }
        '^Critical$' { return 'critical' }
        '^Ignore$' { return 'ignore' }
        default { throw "Unsupported service error control for rollback: '$ErrorControl'." }
    }
}

function Restore-Net10ServiceRollbackSnapshot {
    param(
        [Parameter(Mandatory)]
        [psobject]$Snapshot
    )

    $arguments = @(
        'config'
        $Snapshot.ServiceName
        "binPath= $($Snapshot.PathName)"
        "start= $(Get-Net10ScStartMode $Snapshot.StartMode)"
        "error= $(Get-Net10ScErrorControl $Snapshot.ErrorControl)"
        "DisplayName= $($Snapshot.DisplayName)"
    )
    $dependencies = @($Snapshot.Dependencies | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_)
    })
    if ($dependencies.Count -gt 0) {
        $arguments += "depend= $($dependencies -join '/')"
    }
    else {
        $arguments += 'depend= '
    }

    & sc.exe @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Service rollback config failed with exit code $LASTEXITCODE."
    }

    & sc.exe description $Snapshot.ServiceName $Snapshot.Description | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Service rollback description failed with exit code $LASTEXITCODE."
    }
}

function Restore-Net10LegacyComRegistration {
    param(
        [Parameter(Mandatory)]
        [string]$LegacyExecutable
    )

    if (-not (Test-Path -LiteralPath $LegacyExecutable -PathType Leaf)) {
        throw "Legacy executable for COM rollback was not found: $LegacyExecutable"
    }

    & $LegacyExecutable /Register | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Legacy COM rollback failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Net10ServiceRollback {
    param(
        [Parameter(Mandatory)]
        [psobject]$Snapshot,
        [Parameter(Mandatory)]
        [string]$LegacyExecutable
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    try {
        Restore-Net10ServiceRollbackSnapshot -Snapshot $Snapshot
    }
    catch {
        $errors.Add($_.Exception.Message)
    }

    try {
        Restore-Net10LegacyComRegistration -LegacyExecutable $LegacyExecutable
    }
    catch {
        $errors.Add($_.Exception.Message)
    }

    if ($errors.Count -gt 0) {
        throw "Rollback failed: $($errors -join ' | ')"
    }
}
