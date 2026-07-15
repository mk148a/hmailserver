function New-Net10RollbackArchiveProcessStartInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    if ($null -ne $startInfo.PSObject.Properties['ArgumentList']) {
        foreach ($argument in $ArgumentList) {
            $startInfo.ArgumentList.Add($argument)
        }
    }
    else {
        $quotedArguments = foreach ($argument in $ArgumentList) {
            if ($null -eq $argument) {
                '""'
                continue
            }

            '"' + $argument.Replace('"', '\"') + '"'
        }
        $startInfo.Arguments = [string]::Join(' ', $quotedArguments)
    }

    return $startInfo
}

function Stop-Net10RollbackArchiveProcessTree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    if (-not $Process.HasExited) {
        $killTreeMethod = $Process.GetType().GetMethod('Kill', [Type[]]@([bool]))
        if ($null -ne $killTreeMethod) {
            $null = $killTreeMethod.Invoke($Process, @($true))
        }
        else {
            $taskKill = Join-Path $env:SystemRoot 'System32\taskkill.exe'
            $killer = Start-Process -FilePath $taskKill -ArgumentList @('/PID', [string]$Process.Id, '/T', '/F') -PassThru -Wait -WindowStyle Hidden
            if ($killer.ExitCode -ne 0) {
                throw "taskkill.exe failed with exit code $($killer.ExitCode)."
            }
        }

        if (-not $Process.WaitForExit(5000)) {
            throw "Rollback archive process tree $($Process.Id) did not terminate."
        }
    }
}

function Invoke-Net10RollbackArchiveProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.ProcessStartInfo]$StartInfo,

        [TimeSpan]$Timeout = ([TimeSpan]::FromSeconds(30)),

        [ValidateRange(1, 16777216)]
        [int]$MaxStandardOutputBytes = 1048576,

        [ValidateRange(1, 1048576)]
        [int]$MaxStandardErrorBytes = 65536
    )

    if ($Timeout -le [TimeSpan]::Zero) {
        throw 'Rollback archive process timeout must be greater than zero.'
    }
    if ($StartInfo.UseShellExecute -or -not $StartInfo.RedirectStandardOutput -or -not $StartInfo.RedirectStandardError) {
        throw 'Rollback archive processes must be shell-free with redirected output.'
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $StartInfo
    $standardOutput = [System.IO.MemoryStream]::new()
    $standardError = [System.IO.MemoryStream]::new()
    $started = $false

    try {
        try {
            $started = $process.Start()
        }
        catch {
            throw "Rollback archive process could not be started: $($_.Exception.Message)"
        }

        if (-not $started) {
            throw 'Rollback archive process could not be started.'
        }

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $outputBuffer = [byte[]]::new(8192)
        $errorBuffer = [byte[]]::new(4096)
        $outputRead = $process.StandardOutput.BaseStream.ReadAsync($outputBuffer, 0, $outputBuffer.Length)
        $errorRead = $process.StandardError.BaseStream.ReadAsync($errorBuffer, 0, $errorBuffer.Length)
        $outputComplete = $false
        $errorComplete = $false

        while (-not ($outputComplete -and $errorComplete -and $process.HasExited)) {
            if ($stopwatch.Elapsed -ge $Timeout) {
                throw "Rollback archive process timed out after $([Math]::Ceiling($Timeout.TotalSeconds)) second(s)."
            }

            if (-not $outputComplete -and $outputRead.IsCompleted) {
                $count = $outputRead.GetAwaiter().GetResult()
                if ($count -eq 0) {
                    $outputComplete = $true
                }
                else {
                    if ($standardOutput.Length + $count -gt $MaxStandardOutputBytes) {
                        throw "Rollback archive process output exceeded the $MaxStandardOutputBytes-byte limit."
                    }

                    $standardOutput.Write($outputBuffer, 0, $count)
                    $outputRead = $process.StandardOutput.BaseStream.ReadAsync($outputBuffer, 0, $outputBuffer.Length)
                }
            }

            if (-not $errorComplete -and $errorRead.IsCompleted) {
                $count = $errorRead.GetAwaiter().GetResult()
                if ($count -eq 0) {
                    $errorComplete = $true
                }
                else {
                    if ($standardError.Length + $count -gt $MaxStandardErrorBytes) {
                        throw "Rollback archive process error output exceeded the $MaxStandardErrorBytes-byte limit."
                    }

                    $standardError.Write($errorBuffer, 0, $count)
                    $errorRead = $process.StandardError.BaseStream.ReadAsync($errorBuffer, 0, $errorBuffer.Length)
                }
            }

            if (-not ($outputComplete -and $errorComplete -and $process.HasExited)) {
                [System.Threading.Thread]::Sleep(5)
            }
        }

        if ($process.ExitCode -ne 0) {
            $errorText = [System.Text.Encoding]::UTF8.GetString($standardError.ToArray()).Trim()
            if ([string]::IsNullOrWhiteSpace($errorText)) {
                throw "Rollback archive process failed with exit code $($process.ExitCode)."
            }

            throw "Rollback archive process failed with exit code $($process.ExitCode): $errorText"
        }

        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StandardOutput = $standardOutput.ToArray()
            StandardError = $standardError.ToArray()
        }
    }
    catch {
        $processError = $_
        if ($started -and -not $process.HasExited) {
            try {
                Stop-Net10RollbackArchiveProcessTree -Process $process
            }
            catch {
                throw "Rollback archive process failed and its process tree could not be terminated: $($processError.Exception.Message) Termination error: $($_.Exception.Message)"
            }
        }

        throw $processError
    }
    finally {
        $standardOutput.Dispose()
        $standardError.Dispose()
        $process.Dispose()
    }
}

function Assert-Net10RollbackArchiveMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [byte[]]$Metadata,

        [ValidateRange(1, 16777216)]
        [int]$MaxMetadataBytes = 1048576
    )

    if ($Metadata.Length -eq 0) {
        throw 'Rollback archive metadata is empty.'
    }
    if ($Metadata.Length -gt $MaxMetadataBytes) {
        throw "Rollback archive metadata exceeded the $MaxMetadataBytes-byte limit."
    }

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = $MaxMetadataBytes
    $stream = [System.IO.MemoryStream]::new($Metadata, $false)
    $reader = $null

    try {
        $reader = [System.Xml.XmlReader]::Create($stream, $settings)
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    catch {
        throw "Rollback archive metadata XML is invalid or unsafe: $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
        $stream.Dispose()
    }

    $root = $document.DocumentElement
    if ($null -eq $root -or $root.Name -cne 'Backup') {
        throw 'Rollback archive metadata root must be Backup.'
    }

    $backupInformationNodes = @($root.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -ceq 'BackupInformation'
    })
    if ($backupInformationNodes.Count -ne 1) {
        throw 'Rollback archive metadata must contain one BackupInformation element.'
    }

    $backupInformation = $backupInformationNodes[0]
    if (-not $backupInformation.HasAttribute('Mode') -or $backupInformation.GetAttribute('Mode') -cne '15') {
        throw 'Rollback archive BackupInformation Mode must be exactly 15.'
    }

    $dataFilesNodes = @($backupInformation.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -ceq 'DataFiles'
    })
    if ($dataFilesNodes.Count -ne 1) {
        throw 'Rollback archive metadata must contain one DataFiles element.'
    }

    $dataFiles = $dataFilesNodes[0]
    if (-not $dataFiles.HasAttribute('Format') -or $dataFiles.GetAttribute('Format') -cne '7z') {
        throw 'Rollback archive DataFiles Format must be exactly 7z.'
    }
}

function Assert-Net10RollbackArchiveListing {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [byte[]]$Listing,

        [ValidateRange(1, 16777216)]
        [int]$MaxListingBytes = 16777216
    )

    if ($Listing.Length -eq 0) {
        throw 'Rollback backup archive listing is empty.'
    }
    if ($Listing.Length -gt $MaxListingBytes) {
        throw "Rollback backup archive listing exceeded the $MaxListingBytes-byte limit."
    }

    try {
        $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
        $listingText = $utf8.GetString($Listing)
    }
    catch {
        throw "Rollback backup archive listing is not valid UTF-8: $($_.Exception.Message)"
    }

    $entries = @()
    $currentEntry = $null
    foreach ($line in ($listingText -split "`r?`n")) {
        if ($line.StartsWith('Path = ', [StringComparison]::Ordinal)) {
            if ($null -ne $currentEntry) {
                $entries += $currentEntry
            }

            $currentEntry = [pscustomobject]@{
                Path = $line.Substring(7)
                Attributes = $null
            }
        }
        elseif ($null -ne $currentEntry -and $line.StartsWith('Attributes = ', [StringComparison]::Ordinal)) {
            $currentEntry.Attributes = $line.Substring(13)
        }
        elseif ([string]::IsNullOrWhiteSpace($line) -and $null -ne $currentEntry) {
            $entries += $currentEntry
            $currentEntry = $null
        }
    }
    if ($null -ne $currentEntry) {
        $entries += $currentEntry
    }
    if ($entries.Count -eq 0) {
        throw 'Rollback backup archive listing contains no entries.'
    }

    $hasDataBackupDirectory = $false
    foreach ($entry in $entries) {
        $entryPath = [string]$entry.Path
        if ([string]::IsNullOrWhiteSpace($entryPath)) {
            throw 'Rollback backup archive contains an empty entry path.'
        }

        if ([System.IO.Path]::IsPathRooted($entryPath) -or
            $entryPath.StartsWith('/', [StringComparison]::Ordinal) -or
            $entryPath.StartsWith('\', [StringComparison]::Ordinal) -or
            $entryPath -match '^[A-Za-z]:') {
            throw "Rollback backup archive contains an unsafe absolute entry path: $entryPath"
        }

        $pathSegments = @($entryPath -split '[\\/]')
        if ($pathSegments -contains '..') {
            throw "Rollback backup archive contains an unsafe parent traversal entry path: $entryPath"
        }

        $normalizedEntryPath = $entryPath.TrimEnd([char[]]@('\', '/'))
        if ($normalizedEntryPath -ceq 'DataBackup' -and
            $null -ne $entry.Attributes -and
            $entry.Attributes.StartsWith('D', [StringComparison]::Ordinal)) {
            $hasDataBackupDirectory = $true
        }
    }

    if (-not $hasDataBackupDirectory) {
        throw 'Rollback backup archive must contain a DataBackup directory entry.'
    }
}

function Assert-Net10RollbackArchivePreflight {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$BackupArchive,

        [Parameter(Mandatory)]
        [string]$SevenZipPath,

        [TimeSpan]$ProcessTimeout = ([TimeSpan]::FromSeconds(30)),

        [ValidateRange(1, 16777216)]
        [int]$MaxArchiveListingBytes = 16777216,

        [ValidateRange(1, 16777216)]
        [int]$MaxMetadataBytes = 1048576
    )

    if (-not (Test-Path -LiteralPath $BackupArchive -PathType Leaf)) {
        throw "Rollback backup archive was not found: $BackupArchive"
    }
    if (-not (Test-Path -LiteralPath $SevenZipPath -PathType Leaf)) {
        throw "Packaged 7za.exe was not found: $SevenZipPath"
    }

    $archivePath = (Get-Item -LiteralPath $BackupArchive).FullName
    $toolPath = (Get-Item -LiteralPath $SevenZipPath).FullName
    $testStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $toolPath -ArgumentList @(
        't'
        $archivePath
        '-y'
        '-bso0'
        '-bsp0'
    )

    try {
        $null = Invoke-Net10RollbackArchiveProcess -StartInfo $testStartInfo -Timeout $ProcessTimeout
    }
    catch {
        throw "Rollback backup archive test failed: $($_.Exception.Message)"
    }

    $listingStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $toolPath -ArgumentList @(
        'l'
        $archivePath
        '-slt'
        '-ba'
        '-sccUTF-8'
    )

    try {
        $listingResult = Invoke-Net10RollbackArchiveProcess -StartInfo $listingStartInfo -Timeout $ProcessTimeout -MaxStandardOutputBytes $MaxArchiveListingBytes
    }
    catch {
        throw "Rollback backup archive listing could not be read: $($_.Exception.Message)"
    }

    Assert-Net10RollbackArchiveListing -Listing $listingResult.StandardOutput -MaxListingBytes $MaxArchiveListingBytes

    $metadataStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $toolPath -ArgumentList @(
        'x'
        $archivePath
        'hMailServerBackup.xml'
        '-so'
        '-y'
    )

    try {
        $result = Invoke-Net10RollbackArchiveProcess -StartInfo $metadataStartInfo -Timeout $ProcessTimeout -MaxStandardOutputBytes $MaxMetadataBytes
    }
    catch {
        throw "Rollback backup archive metadata could not be streamed: $($_.Exception.Message)"
    }

    Assert-Net10RollbackArchiveMetadata -Metadata $result.StandardOutput -MaxMetadataBytes $MaxMetadataBytes
}
