[CmdletBinding()]
param(
    [string]$OutputPath,

    [string]$RepositoryRoot,

    [string]$ExpectedModulePath,

    [switch]$OfflineFixture,

    [switch]$FailOnIncomplete
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot '..\artifacts\sec18-staging\SEC18-installed-application-graph-evidence.json'
}
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$classesPath = 'Software\Classes\'
$applicationClassId = '{D6567EF8-0A6C-48E7-9288-A2463123C2F3}'
$applicationAppId = '{5EDEC473-39E0-43F6-A234-1947071721C8}'
$typeLibraryId = '{DB241B59-A1B1-4C59-98FC-8D101A2995F2}'
$applicationInterfaceId = '{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}'
$views = @('Registry64', 'Registry32')

$graphPaths = @(
    "$classesPath`hMailServer.Application.1",
    "$classesPath`hMailServer.Application.1\CLSID",
    "$classesPath`hMailServer.Application",
    "$classesPath`hMailServer.Application\CLSID",
    "$classesPath`hMailServer.Application\CurVer",
    "$classesPath`CLSID\$applicationClassId",
    "$classesPath`CLSID\$applicationClassId\ProgID",
    "$classesPath`CLSID\$applicationClassId\VersionIndependentProgID",
    "$classesPath`CLSID\$applicationClassId\Programmable",
    "$classesPath`CLSID\$applicationClassId\LocalServer32",
    "$classesPath`CLSID\$applicationClassId\TypeLib",
    "$classesPath`AppID\$applicationAppId",
    "$classesPath`AppID\hMailServer.EXE",
    "$classesPath`TypeLib\$typeLibraryId",
    "$classesPath`TypeLib\$typeLibraryId\1.0",
    "$classesPath`TypeLib\$typeLibraryId\1.0\0",
    "$classesPath`TypeLib\$typeLibraryId\1.0\0\win64",
    "$classesPath`TypeLib\$typeLibraryId\1.0\FLAGS",
    "$classesPath`TypeLib\$typeLibraryId\1.0\HELPDIR",
    "$classesPath`Interface\$applicationInterfaceId",
    "$classesPath`Interface\$applicationInterfaceId\ProxyStubClsid32",
    "$classesPath`Interface\$applicationInterfaceId\TypeLib"
)

$directSubkeys = @{
    "$classesPath`hMailServer.Application.1" = @('CLSID')
    "$classesPath`hMailServer.Application" = @('CLSID', 'CurVer')
    "$classesPath`CLSID\$applicationClassId" = @('ProgID', 'VersionIndependentProgID', 'Programmable', 'LocalServer32', 'TypeLib')
    "$classesPath`TypeLib\$typeLibraryId" = @('1.0')
    "$classesPath`TypeLib\$typeLibraryId\1.0" = @('0', 'FLAGS', 'HELPDIR')
    "$classesPath`TypeLib\$typeLibraryId\1.0\0" = @('win64')
    "$classesPath`Interface\$applicationInterfaceId" = @('ProxyStubClsid32', 'TypeLib')
}

$registry32AbsentPaths = @(
    "$classesPath`CLSID\$applicationClassId",
    "$classesPath`CLSID\$applicationClassId\ProgID",
    "$classesPath`CLSID\$applicationClassId\VersionIndependentProgID",
    "$classesPath`CLSID\$applicationClassId\Programmable",
    "$classesPath`CLSID\$applicationClassId\LocalServer32",
    "$classesPath`CLSID\$applicationClassId\TypeLib"
)

$legacySourcePaths = @(
    'hmailserver/source/Server/hMailServer/hMailServer.cpp',
    'hmailserver/source/Server/hMailServer/hMailServer.rgs',
    'hmailserver/source/Server/COM/InterfaceApplication.h',
    'hmailserver/source/Server/COM/InterfaceApplication.rgs',
    'hmailserver/source/Server/hMailServer/hMailServer.idl',
    'hmailserver/source/Server/hMailServer/hMailServer.rc',
    'hmailserver/source/Server.Net10/src/HMailServer.ComInterop/WindowsWebAdminBrokerRegistryEvidenceSource.cs'
)

function ConvertTo-Utf16Base64 {
    param([Parameter(Mandatory = $true)][string]$Value)

    return [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Value + [char]0))
}

function New-StringValue {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    return [pscustomobject]@{
        Name = $Name
        Kind = 1
        RawBytesBase64 = ConvertTo-Utf16Base64 $Value
    }
}

function New-Snapshot {
    param(
        [Parameter(Mandatory = $true)][string]$View,
        [Parameter(Mandatory = $true)][string]$KeyPath,
        [Parameter(Mandatory = $true)][bool]$Present,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowNull()][object[]]$Values,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowNull()][string[]]$DirectSubkeyNames,
        [AllowNull()][string]$ReadError
    )

    $normalizedValues = @($Values | Where-Object { $null -ne $_ })
    $normalizedSubkeyNames = @($DirectSubkeyNames | Where-Object { $null -ne $_ } | Sort-Object)
    $normalizedReadError = if ([string]::IsNullOrWhiteSpace($ReadError)) { $null } else { $ReadError }
    return [pscustomobject]@{
        View = $View
        KeyPath = $KeyPath
        Present = $Present
        Values = $normalizedValues
        DirectSubkeyNames = $normalizedSubkeyNames
        ReadError = $normalizedReadError
    }
}

function Get-CanonicalValues {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ModulePath
    )

    switch ($Path) {
        "$classesPath`hMailServer.Application.1" { return @(New-StringValue '' 'Application Class') }
        "$classesPath`hMailServer.Application.1\CLSID" { return @(New-StringValue '' $applicationClassId) }
        "$classesPath`hMailServer.Application" { return @(New-StringValue '' 'Application Class') }
        "$classesPath`hMailServer.Application\CLSID" { return @(New-StringValue '' $applicationClassId) }
        "$classesPath`hMailServer.Application\CurVer" { return @(New-StringValue '' 'hMailServer.Application.1') }
        "$classesPath`CLSID\$applicationClassId" { return @(New-StringValue '' 'Application Class'; New-StringValue 'AppID' $applicationAppId) }
        "$classesPath`CLSID\$applicationClassId\ProgID" { return @(New-StringValue '' 'hMailServer.Application.1') }
        "$classesPath`CLSID\$applicationClassId\VersionIndependentProgID" { return @(New-StringValue '' 'hMailServer.Application') }
        "$classesPath`CLSID\$applicationClassId\Programmable" { return @() }
        "$classesPath`CLSID\$applicationClassId\LocalServer32" { return @(New-StringValue '' ('"' + $ModulePath + '"')) }
        "$classesPath`CLSID\$applicationClassId\TypeLib" { return @(New-StringValue '' $typeLibraryId) }
        "$classesPath`AppID\$applicationAppId" { return @(New-StringValue '' 'hMailServer'; New-StringValue 'LocalService' 'hMailServer') }
        "$classesPath`AppID\hMailServer.EXE" { return @(New-StringValue 'AppID' $applicationAppId) }
        "$classesPath`TypeLib\$typeLibraryId" { return @() }
        "$classesPath`TypeLib\$typeLibraryId\1.0" { return @(New-StringValue '' 'hMailServer Type Library') }
        "$classesPath`TypeLib\$typeLibraryId\1.0\0" { return @() }
        "$classesPath`TypeLib\$typeLibraryId\1.0\0\win64" { return @(New-StringValue '' $ModulePath) }
        "$classesPath`TypeLib\$typeLibraryId\1.0\FLAGS" { return @(New-StringValue '' '0') }
        "$classesPath`TypeLib\$typeLibraryId\1.0\HELPDIR" { return @(New-StringValue '' ([IO.Path]::GetDirectoryName($ModulePath))) }
        "$classesPath`Interface\$applicationInterfaceId" { return @(New-StringValue '' 'IInterfaceApplication') }
        "$classesPath`Interface\$applicationInterfaceId\ProxyStubClsid32" { return @(New-StringValue '' '{00020424-0000-0000-C000-000000000046}') }
        "$classesPath`Interface\$applicationInterfaceId\TypeLib" { return @(New-StringValue '' $typeLibraryId; New-StringValue 'Version' '1.0') }
        default { throw "Unknown registration graph path: $Path" }
    }
}

function Get-OfflineSnapshots {
    param([Parameter(Mandatory = $true)][string]$ModulePath)

    $snapshots = @()
    foreach ($view in $views) {
        foreach ($path in $graphPaths) {
            $present = $view -ne 'Registry32' -or $registry32AbsentPaths -notcontains $path
            $children = if ($present -and $directSubkeys.ContainsKey($path)) { $directSubkeys[$path] } else { @() }
            $values = if ($present) { Get-CanonicalValues $path $ModulePath } else { @() }
            $snapshots += New-Snapshot $view $path $present $values $children $null
        }
    }

    return @($snapshots)
}

function Ensure-NativeRegistryReader {
    if ($null -eq ('Sec18.NativeRegistry' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace Sec18
{
    public static class NativeRegistry
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            out uint lpType,
            byte[] lpData,
            ref uint lpcbData);
    }
}
'@
    }
}

function Read-NativeRegistryValue {
    param(
        [Parameter(Mandatory = $true)][Microsoft.Win32.RegistryKey]$Key,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Name
    )

    $type = [uint32]0
    $byteCount = [uint32]0
    $nativeName = if ([string]::IsNullOrEmpty($Name)) { $null } else { $Name }
    $handle = $Key.Handle.DangerousGetHandle()
    $result = [Sec18.NativeRegistry]::RegQueryValueEx(
        $handle,
        $nativeName,
        [IntPtr]::Zero,
        [ref]$type,
        $null,
        [ref]$byteCount)
    if ($result -ne 0) {
        throw [ComponentModel.Win32Exception]::new($result)
    }

    $rawBytes = New-Object byte[] ([int]$byteCount)
    if ($byteCount -gt 0) {
        $result = [Sec18.NativeRegistry]::RegQueryValueEx(
            $handle,
            $nativeName,
            [IntPtr]::Zero,
            [ref]$type,
            $rawBytes,
            [ref]$byteCount)
        if ($result -ne 0) {
            throw [ComponentModel.Win32Exception]::new($result)
        }
    }

    return [pscustomobject]@{
        Name = $Name
        Kind = [int]$type
        RawBytesBase64 = [Convert]::ToBase64String($rawBytes)
    }
}

function Get-LiveSnapshots {
    Ensure-NativeRegistryReader
    $snapshots = @()
    foreach ($viewName in $views) {
        $view = [Microsoft.Win32.RegistryView]::$viewName
        $baseKey = $null
        try {
            $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
                [Microsoft.Win32.RegistryHive]::LocalMachine,
                $view)
            foreach ($path in $graphPaths) {
                $key = $null
                try {
                    $key = $baseKey.OpenSubKey($path, $false)
                    if ($null -eq $key) {
                        $snapshots += New-Snapshot $viewName $path $false @() @() $null
                        continue
                    }

                    $values = @(
                        $key.GetValueNames() |
                            Sort-Object |
                            ForEach-Object { Read-NativeRegistryValue $key $_ }
                    )
                    $children = @($key.GetSubKeyNames() | Sort-Object)
                    $snapshots += New-Snapshot $viewName $path $true $values $children $null
                }
                catch {
                    $snapshots += New-Snapshot $viewName $path $false @() @() $_.Exception.GetType().Name
                }
                finally {
                    if ($null -ne $key) {
                        $key.Dispose()
                    }
                }
            }
        }
        catch {
            foreach ($path in $graphPaths) {
                $snapshots += New-Snapshot $viewName $path $false @() @() $_.Exception.GetType().Name
            }
        }
        finally {
            if ($null -ne $baseKey) {
                $baseKey.Dispose()
            }
        }
    }

    return @($snapshots)
}

function ConvertFrom-Utf16Value {
    param([Parameter(Mandatory = $true)][AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ([int]$Value.Kind -ne 1) {
        return $null
    }

    try {
        $bytes = [Convert]::FromBase64String([string]$Value.RawBytesBase64)
        if ($bytes.Length -lt 2 -or ($bytes.Length % 2) -ne 0 -or $bytes[$bytes.Length - 1] -ne 0 -or $bytes[$bytes.Length - 2] -ne 0) {
            return $null
        }

        $text = [Text.Encoding]::Unicode.GetString($bytes)
        if ($text.Length -lt 1 -or $text[$text.Length - 1] -ne [char]0) {
            return $null
        }

        $withoutTerminator = $text.Substring(0, $text.Length - 1)
        if ($withoutTerminator.IndexOf([char]0) -ge 0) {
            return $null
        }

        return $withoutTerminator
    }
    catch {
        return $null
    }
}

function Get-ValueByName {
    param(
        [Parameter(Mandatory = $true)][object]$Snapshot,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Name
    )

    @($Snapshot.Values | Where-Object { [string]$_.Name -ceq $Name }) | Select-Object -First 1
}

function Test-ValueSet {
    param(
        [Parameter(Mandatory = $true)][object]$Snapshot,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowNull()][object[]]$Expected
    )

    $actual = @($Snapshot.Values | Sort-Object Name)
    $expectedSorted = @($Expected | Sort-Object Name)
    if ($actual.Count -ne $expectedSorted.Count) {
        return $false
    }

    for ($i = 0; $i -lt $actual.Count; $i++) {
        if ([string]$actual[$i].Name -cne [string]$expectedSorted[$i].Name -or [int]$actual[$i].Kind -ne [int]$expectedSorted[$i].Kind -or [string]$actual[$i].RawBytesBase64 -cne [string]$expectedSorted[$i].RawBytesBase64) {
            return $false
        }
    }

    return $true
}

function Test-PathIsEqual {
    param(
        [Parameter(Mandatory = $true)][string]$Left,
        [Parameter(Mandatory = $true)][string]$Right
    )

    try {
        $leftFull = [IO.Path]::GetFullPath($Left).TrimEnd('\')
        $rightFull = [IO.Path]::GetFullPath($Right).TrimEnd('\')
        return [string]::Equals($leftFull, $rightFull, [StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        return $false
    }
}

function Test-FullyQualifiedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return $Path -match '^[A-Za-z]:\\' -or $Path -match '^\\\\'
}

function Test-CanonicalGraph {
    param(
        [Parameter(Mandatory = $true)][object[]]$Snapshots,
        [string]$ExpectedPath
    )

    $errors = @()
    $expectedModulePath = if ([string]::IsNullOrWhiteSpace($ExpectedPath)) { $null } else { $ExpectedPath }
    $modulePathFromRegistry = $null
    $completeShape = $Snapshots.Count -eq ($views.Count * $graphPaths.Count)
    if (-not $completeShape) {
        $errors += "Expected $($views.Count * $graphPaths.Count) snapshots, found $($Snapshots.Count)."
    }

    foreach ($view in $views) {
        foreach ($path in $graphPaths) {
            $matches = @($Snapshots | Where-Object { [string]$_.View -ceq $view -and [string]$_.KeyPath -ceq $path })
            if ($matches.Count -ne 1) {
                $errors += "Missing or duplicate snapshot: $view $path."
                continue
            }

            $snapshot = $matches[0]
            $expectedPresent = $view -ne 'Registry32' -or $registry32AbsentPaths -notcontains $path
            if ([bool]$snapshot.Present -ne $expectedPresent -or $null -ne $snapshot.ReadError) {
                $errors += "Presence/read error mismatch: $view $path."
                continue
            }

            $expectedChildren = if ($directSubkeys.ContainsKey($path)) { @($directSubkeys[$path]) } else { @() }
            $actualChildren = @($snapshot.DirectSubkeyNames | Sort-Object)
            $expectedChildren = @($expectedChildren | Sort-Object)
            if ($expectedPresent) {
                if ($actualChildren.Count -ne $expectedChildren.Count) {
                    $errors += "Direct subkey count mismatch: $view $path."
                }
                else {
                    for ($i = 0; $i -lt $actualChildren.Count; $i++) {
                        if ([string]$actualChildren[$i] -cne [string]$expectedChildren[$i]) {
                            $errors += "Direct subkey mismatch: $view $path."
                            break
                        }
                    }
                }
            }

            if (-not $expectedPresent) {
                if (@($snapshot.Values).Count -ne 0 -or $actualChildren.Count -ne 0) {
                    $errors += "Absent Registry32 key has contents: $path."
                }
                continue
            }

            if ($path -eq "$classesPath`CLSID\$applicationClassId\LocalServer32") {
                $value = Get-ValueByName $snapshot ''
                $text = if ($null -ne $value) { ConvertFrom-Utf16Value $value } else { $null }
                if ($null -eq $text -or $text.Length -lt 3 -or $text[0] -ne '"' -or $text[$text.Length - 1] -ne '"' -or $text.Substring(1, $text.Length - 2).IndexOf('"') -ge 0) {
                    $errors += "LocalServer32 is not a single quoted path: $view."
                }
                else {
                    $modulePathFromRegistry = $text.Substring(1, $text.Length - 2)
                    if (-not (Test-FullyQualifiedPath $modulePathFromRegistry) -or [IO.Path]::GetFileName($modulePathFromRegistry) -ine 'hMailServer.exe') {
                        $errors += "LocalServer32 module path is invalid: $view."
                    }
                }
            }
            elseif ($path -eq "$classesPath`TypeLib\$typeLibraryId\1.0\0\win64") {
                $value = Get-ValueByName $snapshot ''
                $text = if ($null -ne $value) { ConvertFrom-Utf16Value $value } else { $null }
                if ($null -eq $text -or $text.IndexOf('"') -ge 0 -or -not (Test-FullyQualifiedPath $text) -or [IO.Path]::GetFileName($text) -ine 'hMailServer.exe') {
                    $errors += "TypeLib win64 module path is invalid: $view."
                }
                elseif ($null -eq $expectedModulePath) {
                    $expectedModulePath = $text
                }
            }
            elseif ($path -eq "$classesPath`TypeLib\$typeLibraryId\1.0\HELPDIR") {
                $value = Get-ValueByName $snapshot ''
                $text = if ($null -ne $value) { ConvertFrom-Utf16Value $value } else { $null }
                if ($null -eq $text -or $text.IndexOf('"') -ge 0 -or -not (Test-FullyQualifiedPath $text)) {
                    $errors += "TypeLib HELPDIR is invalid: $view."
                }
                elseif ($null -ne $modulePathFromRegistry -and -not (Test-PathIsEqual ([IO.Path]::GetDirectoryName($modulePathFromRegistry)) $text)) {
                    $errors += "TypeLib HELPDIR does not match LocalServer32 directory: $view."
                }
            }
            else {
                $expectedValues = Get-CanonicalValues $path $(if ($null -ne $expectedModulePath) { $expectedModulePath } else { 'C:\hMailServer57-Test\Bin\hMailServer.exe' })
                if (-not (Test-ValueSet $snapshot $expectedValues)) {
                    if ($path -eq "$classesPath`AppID\$applicationAppId") {
                        $required = @(
                            (New-StringValue '' 'hMailServer'),
                            (New-StringValue 'LocalService' 'hMailServer')
                        )
                        $missing = $false
                        foreach ($requiredValue in $required) {
                            $actual = Get-ValueByName $snapshot $requiredValue.Name
                            if ($null -eq $actual -or [int]$actual.Kind -ne 1 -or [string]$actual.RawBytesBase64 -cne [string]$requiredValue.RawBytesBase64) {
                                $missing = $true
                            }
                        }
                        if ($missing) {
                            $errors += "Existing Application AppID required values are not canonical: $view."
                        }
                    }
                    else {
                        $errors += "Canonical value mismatch: $view $path."
                    }
                }
            }
        }
    }

    foreach ($view in $views) {
        $typeLibrary = @($Snapshots | Where-Object { [string]$_.View -ceq $view -and [string]$_.KeyPath -ceq "$classesPath`TypeLib\$typeLibraryId\1.0\0\win64" }) | Select-Object -First 1
        $helpDirectory = @($Snapshots | Where-Object { [string]$_.View -ceq $view -and [string]$_.KeyPath -ceq "$classesPath`TypeLib\$typeLibraryId\1.0\HELPDIR" }) | Select-Object -First 1
        if ($null -ne $typeLibrary -and $null -ne $helpDirectory) {
            $module = ConvertFrom-Utf16Value (Get-ValueByName $typeLibrary '')
            $help = ConvertFrom-Utf16Value (Get-ValueByName $helpDirectory '')
            if ($null -ne $module -and $null -ne $help -and -not (Test-PathIsEqual ([IO.Path]::GetDirectoryName($module)) $help)) {
                $errors += "TypeLib module and HELPDIR relationship failed: $view."
            }
            if ($null -ne $expectedModulePath -and $null -ne $module -and -not (Test-PathIsEqual $expectedModulePath $module)) {
                $errors += "TypeLib module path differs from expected module path: $view."
            }
        }
    }

    $registry32ApplicationClassPresent = @($Snapshots | Where-Object { $_.View -eq 'Registry32' -and $registry32AbsentPaths -contains $_.KeyPath -and $_.Present }).Count -gt 0
    return [pscustomobject]@{
        Complete = $errors.Count -eq 0
        Errors = @($errors)
        ExpectedGraphPathCount = $graphPaths.Count
        SnapshotCount = $Snapshots.Count
        FixedValuesValidated = $errors.Count -eq 0
        DirectSubkeysValidated = $errors.Count -eq 0
        Registry32AsymmetryValidated = -not $registry32ApplicationClassPresent
        InstallationPathsValidated = $errors.Count -eq 0 -and $null -ne $expectedModulePath
        ExpectedModulePath = $expectedModulePath
        Registry32ApplicationClassIdPresent = $registry32ApplicationClassPresent
    }
}

function Get-GitValue {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    try {
        $values = @(& git -C $Root @Arguments 2>$null)
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0 -and $values.Count -gt 0) {
            return [string]$values[0]
        }
    }
    catch {
    }

    return $null
}

function Get-SourceAttestation {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$CollectorPath
    )

    $collectorRelativePath = 'build/' + [IO.Path]::GetFileName($CollectorPath)
    $collectorHash = $null
    if (Test-Path -LiteralPath $CollectorPath -PathType Leaf) {
        $collectorHash = (Get-FileHash -LiteralPath $CollectorPath -Algorithm SHA256).Hash.ToUpperInvariant()
    }

    $sourceFiles = @()
    foreach ($relativePath in $legacySourcePaths) {
        $fullPath = Join-Path $Root ($relativePath.Replace('/', '\'))
        $hash = $null
        $exists = Test-Path -LiteralPath $fullPath -PathType Leaf
        if ($exists) {
            $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToUpperInvariant()
        }
        $sourceFiles += [pscustomobject]@{
            Path = $relativePath
            Present = $exists
            Sha256 = $hash
        }
    }

    $head = Get-GitValue $Root @('rev-parse', 'HEAD')
    $status = @(& git -C $Root status --porcelain 2>$null)
    $statusExitCode = $LASTEXITCODE
    $statusAvailable = $statusExitCode -eq 0
    $complete = $null -ne $collectorHash -and $sourceFiles.Count -eq $legacySourcePaths.Count -and @($sourceFiles | Where-Object { -not $_.Present -or [string]::IsNullOrWhiteSpace($_.Sha256) }).Count -eq 0 -and $null -ne $head -and $statusAvailable

    return [pscustomobject]@{
        CollectorPath = $collectorRelativePath
        CollectorSha256 = $collectorHash
        SourceFiles = @($sourceFiles)
        RepositoryHead = $head
        RepositoryWorktreeDirty = $status.Count -gt 0
        Complete = $complete
    }
}

$resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$collectorPath = [IO.Path]::GetFullPath($PSCommandPath)
if ($OfflineFixture) {
    $snapshots = Get-OfflineSnapshots 'C:\hMailServer57-Test\Bin\hMailServer.exe'
    $collectionSource = 'offline-fixture'
    $registryReadPerformed = $false
}
else {
    $snapshots = Get-LiveSnapshots
    $collectionSource = 'live-registry'
    $registryReadPerformed = $true
}

$canonical = Test-CanonicalGraph $snapshots $ExpectedModulePath
$attestation = Get-SourceAttestation $resolvedRepositoryRoot $collectorPath
$result = [pscustomobject]@{
    SchemaVersion = 1
    EvidenceKind = 'SEC18-InstalledApplicationGraph'
    CollectionSource = $collectionSource
    RegistryReadPerformed = $registryReadPerformed
    GraphPathCount = $graphPaths.Count
    SnapshotCount = $snapshots.Count
    Snapshots = @($snapshots)
    CanonicalValidation = $canonical
    CanonicalExpectedContentsValidated = [bool]$canonical.Complete
    UnknownSubkeysEnumerated = @($snapshots | Where-Object { $null -eq $_.DirectSubkeyNames }).Count -eq 0
    CollectorAttested = [bool]$attestation.Complete
    Attestation = $attestation
    CompleteReadback = [bool]$canonical.Complete
    GateDecision = if ($canonical.Complete -and $attestation.Complete) { 'EvidenceReadyForIndependentReview' } else { 'RED-incomplete-evidence' }
}

$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [IO.Path]::GetDirectoryName($outputFullPath)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
[IO.File]::WriteAllText(
    $outputFullPath,
    ($result | ConvertTo-Json -Depth 20),
    [Text.UTF8Encoding]::new($false))

if ($FailOnIncomplete -and (-not $canonical.Complete -or -not $attestation.Complete)) {
    exit 2
}

exit 0
