param(
    [Parameter(Mandatory = $true)]
    [string]$LeftInput,

    [Parameter(Mandatory = $true)]
    [string]$RightInput,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$SevenZipPath = "",

    [switch]$AllowKnownLegacyDifferences
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path)
}

function Get-ArchiveEntries([string]$ArchivePath, [string]$SevenZip) {
    $listing = @(& $SevenZip l -slt $ArchivePath 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to list backup archive: $ArchivePath"
    }

    $entries = [Collections.Generic.List[string]]::new()
    $archiveHeaderSkipped = $false
    foreach ($line in $listing) {
        if ($line -match '^Path = (.+)$') {
            $entry = $Matches[1].Trim()
            if (-not $archiveHeaderSkipped) {
                $archiveHeaderSkipped = $true
                continue
            }
            $normalized = $entry.Replace('\', '/')
            Assert-True (-not [IO.Path]::IsPathRooted($normalized)) "Archive contains an absolute path: $entry"
            Assert-True ($normalized -notmatch '(^|/)\.\.?(/|$)') "Archive contains a traversal path: $entry"
            $entries.Add($normalized)
        }
    }

    return $entries.ToArray()
}

function Resolve-InputRoot([string]$InputPath, [string]$Label, [string]$WorkRoot, [string]$SevenZip) {
    $fullPath = Get-FullPath $InputPath
    Assert-True (Test-Path -LiteralPath $fullPath) "$Label input does not exist: $fullPath"

    if (Test-Path -LiteralPath $fullPath -PathType Container) {
        $xmlFiles = @(Get-ChildItem -LiteralPath $fullPath -Filter '*.xml' -File -Recurse)
        if ($xmlFiles.Count -eq 0) {
            $archives = @(Get-ChildItem -LiteralPath $fullPath -Filter '*.7z' -File)
            Assert-True ($archives.Count -eq 1) "$Label directory must contain backup XML or exactly one .7z archive: $fullPath"
            $archive = $archives[0].FullName
            Get-ArchiveEntries -ArchivePath $archive -SevenZip $SevenZip | Out-Null
            $destination = Join-Path $WorkRoot $Label
            New-Item -ItemType Directory -Force -Path $destination | Out-Null
            & $SevenZip x -y "-o$destination" $archive | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Unable to extract backup archive: $archive"
            }

            $externalDataBackup = Join-Path $fullPath 'DataBackup'
            if (Test-Path -LiteralPath $externalDataBackup -PathType Container) {
                $destinationDataBackup = Join-Path $destination 'DataBackup'
                New-Item -ItemType Directory -Force -Path $destinationDataBackup | Out-Null
                Copy-Item -Path (Join-Path $externalDataBackup '*') -Destination $destinationDataBackup -Recurse -Force
            }

            return [pscustomobject]@{
                Root = $destination
                Extracted = $true
                Source = $fullPath
            }
        }

        return [pscustomobject]@{
            Root = $fullPath
            Extracted = $false
            Source = $fullPath
        }
    }

    Assert-True ([IO.Path]::GetExtension($fullPath).Equals('.7z', [StringComparison]::OrdinalIgnoreCase)) "$Label input must be a directory or .7z archive: $fullPath"
    Get-ArchiveEntries -ArchivePath $fullPath -SevenZip $SevenZip | Out-Null
    $destination = Join-Path $WorkRoot $Label
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    & $SevenZip x -y "-o$destination" $fullPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to extract backup archive: $fullPath"
    }

    return [pscustomobject]@{
        Root = $destination
        Extracted = $true
        Source = $fullPath
    }
}

function Get-BackupXml([string]$Root, [string]$Label) {
    $xmlFiles = @(Get-ChildItem -LiteralPath $Root -Filter '*.xml' -File -Recurse | Sort-Object FullName)
    Assert-True ($xmlFiles.Count -gt 0) "$Label backup payload does not contain an XML file."
    $preferred = $xmlFiles | Where-Object Name -eq 'hMailServerBackup.xml' | Select-Object -First 1
    if ($null -ne $preferred) {
        return $preferred
    }
    return $xmlFiles[0]
}

function Escape-XmlValue([string]$Value) {
    return [Security.SecurityElement]::Escape($Value)
}

function Convert-XmlElementToCanonical([Xml.XmlElement]$Element) {
    $attributes = @($Element.Attributes |
        Where-Object Name -notin @('Version', 'ID', 'CreateTime', 'LastLogonTime', 'Date') |
        Sort-Object Name | ForEach-Object {
            ' ' + $_.Name + '="' + (Escape-XmlValue $_.Value) + '"'
        }) -join ''
    $children = @($Element.ChildNodes | ForEach-Object {
            if ($_ -is [Xml.XmlElement]) {
                Convert-XmlElementToCanonical $_
            } elseif ($_ -is [Xml.XmlText] -or $_ -is [Xml.XmlCDataSection]) {
                $text = $_.Value -replace '\s+', ' '
                if (-not [string]::IsNullOrWhiteSpace($text)) { Escape-XmlValue $text.Trim() }
            }
        }) -join ''
    return "<$($Element.Name)$attributes>$children</$($Element.Name)>"
}

function Get-CanonicalXml([string]$Path) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.Load($reader)
    } finally {
        $reader.Dispose()
    }
    Assert-True ($null -ne $document.DocumentElement) "Backup XML has no document element: $Path"
    return Convert-XmlElementToCanonical $document.DocumentElement
}

function Read-SafeXmlDocument([string]$Path) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.Load($reader)
    } finally {
        $reader.Dispose()
    }
    Assert-True ($null -ne $document.DocumentElement) "Backup XML has no document element: $Path"
    return $document
}

function Get-FirstMatchingAccount([Xml.XmlNodeList]$Accounts, [string]$Name) {
    foreach ($account in $Accounts) {
        if ($account.GetAttribute('Name') -eq $Name) {
            return $account
        }
    }
    return $null
}

function Get-KnownLegacyDifferenceCodes(
    [Xml.XmlDocument]$LeftDocument,
    [Xml.XmlDocument]$RightDocument) {
    $codes = [Collections.Generic.List[string]]::new()
    foreach ($name in @('SecurityRanges', 'TCPIPPorts', 'BlockedAttachments', 'SURBLServers', 'DNSBlackLists')) {
        $left = $LeftDocument.DocumentElement.SelectSingleNode("*[local-name()='$name']")
        $right = $RightDocument.DocumentElement.SelectSingleNode("*[local-name()='$name']")
        $leftEmpty = $null -ne $left -and @($left.ChildNodes | Where-Object { $_ -is [Xml.XmlElement] }).Count -eq 0
        $rightEmpty = $null -ne $right -and @($right.ChildNodes | Where-Object { $_ -is [Xml.XmlElement] }).Count -eq 0
        if ($leftEmpty -and $null -eq $right) {
            $codes.Add("legacy-empty-top-level:$name")
        } elseif ($rightEmpty -and $null -eq $left) {
            $codes.Add("net10-empty-top-level:$name")
        }
    }

    $leftProperty = $LeftDocument.DocumentElement.SelectSingleNode("*[local-name()='Properties']/*[local-name()='smtprelayerpassword']")
    $rightProperty = $RightDocument.DocumentElement.SelectSingleNode("*[local-name()='Properties']/*[local-name()='smtprelayerpassword']")
    if (($null -ne $leftProperty) -xor ($null -ne $rightProperty)) {
        $codes.Add('legacy-sensitive-property-omitted:smtprelayerpassword')
    }

    $leftAccounts = $LeftDocument.SelectNodes("//*[local-name()='Account']")
    $rightAccounts = $RightDocument.SelectNodes("//*[local-name()='Account']")
    foreach ($leftAccount in $leftAccounts) {
        $rightAccount = Get-FirstMatchingAccount $rightAccounts $leftAccount.GetAttribute('Name')
        if ($null -ne $rightAccount -and
            $leftAccount.GetAttribute('PasswordEncryption') -eq '3' -and
            $rightAccount.GetAttribute('PasswordEncryption') -eq '3' -and
            $leftAccount.GetAttribute('Password') -ne $rightAccount.GetAttribute('Password')) {
            if (-not $codes.Contains('legacy-salted-password-value')) {
                $codes.Add('legacy-salted-password-value')
            }
        }
    }
    return $codes.ToArray()
}

function Remove-TopLevelElement([Xml.XmlDocument]$Document, [string]$Name) {
    $element = $Document.DocumentElement.SelectSingleNode("*[local-name()='$Name']")
    if ($null -ne $element) {
        [void]$Document.DocumentElement.RemoveChild($element)
    }
}

function Normalize-KnownLegacyDifferences(
    [Xml.XmlDocument]$LeftDocument,
    [Xml.XmlDocument]$RightDocument) {
    $left = [Xml.XmlDocument]$LeftDocument.Clone()
    $right = [Xml.XmlDocument]$RightDocument.Clone()
    foreach ($name in @('SecurityRanges', 'TCPIPPorts', 'BlockedAttachments', 'SURBLServers', 'DNSBlackLists')) {
        $leftElement = $left.DocumentElement.SelectSingleNode("*[local-name()='$name']")
        $rightElement = $right.DocumentElement.SelectSingleNode("*[local-name()='$name']")
        $leftEmpty = $null -ne $leftElement -and @($leftElement.ChildNodes | Where-Object { $_ -is [Xml.XmlElement] }).Count -eq 0
        $rightEmpty = $null -ne $rightElement -and @($rightElement.ChildNodes | Where-Object { $_ -is [Xml.XmlElement] }).Count -eq 0
        if ($leftEmpty -and $null -eq $rightElement) {
            Remove-TopLevelElement $left $name
        } elseif ($rightEmpty -and $null -eq $leftElement) {
            Remove-TopLevelElement $right $name
        }
    }

    $leftProperty = $left.DocumentElement.SelectSingleNode("*[local-name()='Properties']/*[local-name()='smtprelayerpassword']")
    $rightProperty = $right.DocumentElement.SelectSingleNode("*[local-name()='Properties']/*[local-name()='smtprelayerpassword']")
    if ($null -ne $leftProperty -and $null -eq $rightProperty) {
        [void]$leftProperty.ParentNode.RemoveChild($leftProperty)
    } elseif ($null -eq $leftProperty -and $null -ne $rightProperty) {
        [void]$rightProperty.ParentNode.RemoveChild($rightProperty)
    }

    $leftAccounts = $left.SelectNodes("//*[local-name()='Account']")
    $rightAccounts = $right.SelectNodes("//*[local-name()='Account']")
    foreach ($leftAccount in $leftAccounts) {
        $rightAccount = Get-FirstMatchingAccount $rightAccounts $leftAccount.GetAttribute('Name')
        if ($null -ne $rightAccount -and
            $leftAccount.GetAttribute('PasswordEncryption') -eq '3' -and
            $rightAccount.GetAttribute('PasswordEncryption') -eq '3' -and
            $leftAccount.GetAttribute('Password') -ne $rightAccount.GetAttribute('Password')) {
            $leftAccount.SetAttribute('Password', '__legacy-salted-sha256__')
            $rightAccount.SetAttribute('Password', '__legacy-salted-sha256__')
        }
    }
    return [pscustomobject]@{ Left = $left; Right = $right }
}

function Get-Sha256Text([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    return ([Security.Cryptography.SHA256]::Create().ComputeHash($bytes) | ForEach-Object ToString x2) -join ''
}

function Get-DataBackupFiles([string]$Root) {
    $result = @{}
    $dataDirectories = @(Get-ChildItem -LiteralPath $Root -Directory -Recurse | Where-Object Name -eq 'DataBackup')
    foreach ($directory in $dataDirectories) {
        foreach ($file in Get-ChildItem -LiteralPath $directory.FullName -File -Recurse) {
            $relative = $file.FullName.Substring($directory.FullName.Length).TrimStart([char]92, [char]47).Replace('\', '/')
            $key = "DataBackup/$relative"
            $result[$key] = [pscustomobject]@{
                Length = $file.Length
                Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
    }
    return $result
}

function Compare-DataFiles($LeftFiles, $RightFiles) {
    $keys = @($LeftFiles.Keys + $RightFiles.Keys | Sort-Object -Unique)
    $differences = [Collections.Generic.List[string]]::new()
    foreach ($key in $keys) {
        if (-not $LeftFiles.ContainsKey($key)) {
            $differences.Add("missing-left:$key")
            continue
        }
        if (-not $RightFiles.ContainsKey($key)) {
            $differences.Add("missing-right:$key")
            continue
        }
        if ($LeftFiles[$key].Length -ne $RightFiles[$key].Length -or $LeftFiles[$key].Sha256 -ne $RightFiles[$key].Sha256) {
            $differences.Add("content:$key")
        }
    }
    return $differences.ToArray()
}

$leftPath = Get-FullPath $LeftInput
$rightPath = Get-FullPath $RightInput
$output = Get-FullPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
$work = Join-Path $output 'compare-work'
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    if ([string]::IsNullOrWhiteSpace($SevenZipPath)) {
        $repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
        $SevenZipPath = Join-Path $repoRoot 'hmailserver\installation\Extras\7za.exe'
    }
    $sevenZip = Get-FullPath $SevenZipPath
    Assert-True (Test-Path -LiteralPath $sevenZip -PathType Leaf) "7-Zip executable is missing: $sevenZip"

    $left = Resolve-InputRoot -InputPath $leftPath -Label 'left' -WorkRoot $work -SevenZip $sevenZip
    $right = Resolve-InputRoot -InputPath $rightPath -Label 'right' -WorkRoot $work -SevenZip $sevenZip
    $leftXml = Get-BackupXml -Root $left.Root -Label 'Left'
    $rightXml = Get-BackupXml -Root $right.Root -Label 'Right'
    $leftDocument = Read-SafeXmlDocument $leftXml.FullName
    $rightDocument = Read-SafeXmlDocument $rightXml.FullName
    $leftCanonical = Convert-XmlElementToCanonical $leftDocument.DocumentElement
    $rightCanonical = Convert-XmlElementToCanonical $rightDocument.DocumentElement
    $leftData = Get-DataBackupFiles $left.Root
    $rightData = Get-DataBackupFiles $right.Root
    $dataDifferences = @(Compare-DataFiles $leftData $rightData)
    $xmlEqual = [StringComparer]::Ordinal.Equals($leftCanonical, $rightCanonical)
    $knownDifferenceCodes = @(Get-KnownLegacyDifferenceCodes $leftDocument $rightDocument)
    $normalized = Normalize-KnownLegacyDifferences $leftDocument $rightDocument
    $normalizedLeftCanonical = Convert-XmlElementToCanonical $normalized.Left.DocumentElement
    $normalizedRightCanonical = Convert-XmlElementToCanonical $normalized.Right.DocumentElement
    $normalizedXmlEqual = [StringComparer]::Ordinal.Equals($normalizedLeftCanonical, $normalizedRightCanonical)
    $dataEqual = $dataDifferences.Count -eq 0
    $profileAcceptsXml = $xmlEqual -or (
        $AllowKnownLegacyDifferences -and
        $knownDifferenceCodes.Count -gt 0 -and
        $normalizedXmlEqual)
    $status = if ($profileAcceptsXml -and $dataEqual) {
        if ($xmlEqual) { 'PASS' } else { 'PASS_EXPECTED_DIFFERENCES' }
    } else { 'FAIL' }
    $report = [ordered]@{
        schema = 'backup-semantic-comparison-v2'
        status = $status
        generatedUtc = [DateTimeOffset]::UtcNow
        leftInput = $leftPath
        rightInput = $rightPath
        leftXml = $leftXml.Name
        rightXml = $rightXml.Name
        leftXmlSha256 = Get-Sha256Text $leftCanonical
        rightXmlSha256 = Get-Sha256Text $rightCanonical
        xmlEqual = $xmlEqual
        compatibilityProfileEnabled = [bool]$AllowKnownLegacyDifferences
        compatibilityProfileStatus = if ($normalizedXmlEqual) {
            if ($knownDifferenceCodes.Count -eq 0) { 'EXACT' } else { 'KNOWN_DIFFERENCES_ONLY' }
        } else { 'UNEXPECTED_DIFFERENCES' }
        knownLegacyDifferenceCodes = $knownDifferenceCodes
        normalizedXmlEqual = $normalizedXmlEqual
        leftDataBackupFileCount = $leftData.Count
        rightDataBackupFileCount = $rightData.Count
        dataBackupEqual = $dataEqual
        dataBackupDifferences = $dataDifferences
        archiveInputsExtracted = ($left.Extracted -or $right.Extracted)
        productionTargetsUsed = $false
        sqlAccessed = $false
        serviceAccessed = $false
        note = 'Compares normalized backup XML and DataBackup file hashes only; it does not access SQL, services, or live Data directories.'
    }
    $jsonPath = Join-Path $output 'backup-semantic-comparison.json'
    $csvPath = Join-Path $output 'backup-semantic-comparison.csv'
    $mdPath = Join-Path $output 'backup-semantic-comparison.md'
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8 -NoNewline
    "status,xml_equal,compatibility_profile_status,normalized_xml_equal,data_backup_equal,left_xml_sha256,right_xml_sha256,left_data_files,right_data_files`n$($report.status),$xmlEqual,$($report.compatibilityProfileStatus),$normalizedXmlEqual,$dataEqual,$($report.leftXmlSha256),$($report.rightXmlSha256),$($report.leftDataBackupFileCount),$($report.rightDataBackupFileCount)" | Set-Content -LiteralPath $csvPath -Encoding utf8 -NoNewline
    @"
# Backup semantic comparison

- Result: ``$($report.status)``
- XML equal: ``$xmlEqual``
- Compatibility profile: ``$($report.compatibilityProfileStatus)``
- Known legacy differences: ``$($knownDifferenceCodes -join ', ')``
- DataBackup equal: ``$dataEqual``
- Left XML SHA-256: ``$($report.leftXmlSha256)``
- Right XML SHA-256: ``$($report.rightXmlSha256)``
- DataBackup files: ``$($report.leftDataBackupFileCount)`` left / ``$($report.rightDataBackupFileCount)`` right

This comparator only reads the two supplied payloads. It does not access SQL,
services, or live Data directories. The default mode requires exact normalized
XML and DataBackup equality. ``-AllowKnownLegacyDifferences`` accepts only the
explicit legacy empty-container, salted-password, and sensitive-property
differences listed in the JSON report; any residual XML difference remains a
failure. A PASS proves payload equality for the supplied inputs; it does not
prove that the inputs came from the same fixture.
"@ | Set-Content -LiteralPath $mdPath -Encoding utf8 -NoNewline

    if ($report.status -eq 'FAIL') {
        throw "Backup semantic comparison failed. See $jsonPath"
    }
    Write-Host "Backup semantic comparison passed. Report: $jsonPath"
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
