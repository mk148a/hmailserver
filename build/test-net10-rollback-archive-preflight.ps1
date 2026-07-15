[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$helperPath = Join-Path $PSScriptRoot 'net10-rollback-archive-preflight.ps1'
$installerPath = Join-Path $PSScriptRoot 'install-net10-service.ps1'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$trackedSevenZip = Join-Path $repoRoot 'hmailserver\installation\Extras\7za.exe'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ThrowsLike {
    param(
        [scriptblock]$Action,
        [string]$Pattern
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike $Pattern) {
            throw "Expected error like '$Pattern' but found '$($_.Exception.Message)'."
        }

        return
    }

    throw "Expected error like '$Pattern' but no error was thrown."
}

function New-TestArchive {
    param(
        [string]$SevenZipPath,
        [string]$Directory,
        [string]$Name,
        [AllowNull()]
        [string]$Metadata,
        [ValidateSet('Directory', 'EmptyDirectory', 'File', 'Missing')]
        [string]$DataBackupShape = 'Directory'
    )

    $sourceDirectory = Join-Path $Directory "$Name-source"
    $archivePath = Join-Path $Directory "$Name.7z"
    $null = New-Item -ItemType Directory -Path $sourceDirectory
    $dataBackupPath = Join-Path $sourceDirectory 'DataBackup'
    switch ($DataBackupShape) {
        'Directory' {
            $null = New-Item -ItemType Directory -Path $dataBackupPath
            Set-Content -LiteralPath (Join-Path $dataBackupPath 'message.eml') -Value 'rollback payload' -Encoding UTF8
        }
        'EmptyDirectory' {
            $null = New-Item -ItemType Directory -Path $dataBackupPath
        }
        'File' {
            Set-Content -LiteralPath $dataBackupPath -Value 'not a directory' -Encoding UTF8
        }
    }
    if ($null -ne $Metadata) {
        Set-Content -LiteralPath (Join-Path $sourceDirectory 'hMailServerBackup.xml') -Value $Metadata -Encoding UTF8
    }

    $startInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $SevenZipPath -ArgumentList @(
        'a'
        $archivePath
        (Join-Path $sourceDirectory '*')
        '-t7z'
        '-mmt'
        '-mx1'
        '-y'
    )
    $result = Invoke-Net10RollbackArchiveProcess -StartInfo $startInfo -Timeout ([TimeSpan]::FromSeconds(15))
    Assert-True ($result.ExitCode -eq 0) "Unable to create test archive '$archivePath'."
    return $archivePath
}

if (-not (Test-Path -LiteralPath $trackedSevenZip -PathType Leaf)) {
    throw "Tracked 7za.exe was not found: $trackedSevenZip"
}

$null = . $helperPath
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "hmailserver-rollback-preflight-$([Guid]::NewGuid().ToString('N'))"
$binDirectory = Join-Path $tempRoot 'bin'
$sevenZipPath = Join-Path $binDirectory '7za.exe'
$null = New-Item -ItemType Directory -Path $binDirectory
Copy-Item -LiteralPath $trackedSevenZip -Destination $sevenZipPath

try {
    $validMetadata = '<Backup><BackupInformation Mode="15"><DataFiles Format="7z" Size="not-final" /></BackupInformation></Backup>'
    $validArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'valid archive with spaces' -Metadata $validMetadata
    Assert-Net10RollbackArchivePreflight -BackupArchive $validArchive -SevenZipPath $sevenZipPath
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $tempRoot 'hMailServerBackup.xml'))) 'Preflight extracted metadata to disk.'

    $emptyDataBackupArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'empty-data-backup' -Metadata $validMetadata -DataBackupShape EmptyDirectory
    Assert-Net10RollbackArchivePreflight -BackupArchive $emptyDataBackupArchive -SevenZipPath $sevenZipPath

    $missingDataBackupArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'missing-data-backup' -Metadata $validMetadata -DataBackupShape Missing
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $missingDataBackupArchive -SevenZipPath $sevenZipPath } '*DataBackup*directory*'

    $fileDataBackupArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'file-data-backup' -Metadata $validMetadata -DataBackupShape File
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $fileDataBackupArchive -SevenZipPath $sevenZipPath } '*DataBackup*directory*'

    $parentTraversalArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'parent-traversal' -Metadata $validMetadata
    $renameStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $sevenZipPath -ArgumentList @(
        'rn'
        $parentTraversalArchive
        'DataBackup\message.eml'
        '..\message.eml'
        '-y'
    )
    $null = Invoke-Net10RollbackArchiveProcess -StartInfo $renameStartInfo -Timeout ([TimeSpan]::FromSeconds(15))
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $parentTraversalArchive -SevenZipPath $sevenZipPath } '*unsafe*parent traversal*'

    $absolutePathArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'absolute-path' -Metadata $validMetadata
    $absolutePayload = Join-Path $tempRoot 'absolute-message.eml'
    Set-Content -LiteralPath $absolutePayload -Value 'absolute payload' -Encoding UTF8
    $absoluteAddStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $sevenZipPath -ArgumentList @(
        'a'
        $absolutePathArchive
        $absolutePayload
        '-spf'
        '-t7z'
        '-y'
    )
    $null = Invoke-Net10RollbackArchiveProcess -StartInfo $absoluteAddStartInfo -Timeout ([TimeSpan]::FromSeconds(15))
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $absolutePathArchive -SevenZipPath $sevenZipPath } '*unsafe*absolute*'

    $missingMetadataArchive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name 'missing-metadata' -Metadata $null
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $missingMetadataArchive -SevenZipPath $sevenZipPath } '*metadata*'

    $corruptArchive = Join-Path $tempRoot 'corrupt.7z'
    [System.IO.File]::WriteAllBytes($corruptArchive, [byte[]](0x01, 0x02, 0x03, 0x04))
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $corruptArchive -SevenZipPath $sevenZipPath } '*archive test*'

    $truncatedArchive = Join-Path $tempRoot 'truncated.7z'
    $validBytes = [System.IO.File]::ReadAllBytes($validArchive)
    [System.IO.File]::WriteAllBytes($truncatedArchive, $validBytes[0..([Math]::Max(31, [int]($validBytes.Length / 2)))])
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $truncatedArchive -SevenZipPath $sevenZipPath } '*archive test*'

    $invalidCases = @(
        @{ Name = 'invalid-mode'; Metadata = '<Backup><BackupInformation Mode="13"><DataFiles Format="7z" /></BackupInformation></Backup>'; Pattern = '*Mode*' }
        @{ Name = 'raw-format'; Metadata = '<Backup><BackupInformation Mode="15"><DataFiles Format="Raw" /></BackupInformation></Backup>'; Pattern = '*Format*' }
        @{ Name = 'dtd'; Metadata = '<!DOCTYPE Backup [<!ENTITY xxe SYSTEM "file:///windows/win.ini">]><Backup><BackupInformation Mode="15"><DataFiles Format="7z" /></BackupInformation></Backup>'; Pattern = '*XML*' }
    )
    foreach ($case in $invalidCases) {
        $archive = New-TestArchive -SevenZipPath $sevenZipPath -Directory $tempRoot -Name $case.Name -Metadata $case.Metadata
        Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $archive -SevenZipPath $sevenZipPath } $case.Pattern
    }

    $startInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $sevenZipPath -ArgumentList @(
        'x'
        'C:\backup path\archive & literal.7z'
        'hMailServerBackup.xml'
        '-so'
        '-y'
    )
    Assert-True (-not $startInfo.UseShellExecute) 'Process execution unexpectedly uses a shell.'
    Assert-True ($startInfo.FileName -ceq $sevenZipPath) 'Process executable was not preserved as a literal path.'
    $expectedArguments = @(
        'x'
        'C:\backup path\archive & literal.7z'
        'hMailServerBackup.xml'
        '-so'
        '-y'
    )
    if ($null -ne $startInfo.PSObject.Properties['ArgumentList']) {
        Assert-True (($startInfo.ArgumentList -join "`n") -ceq ($expectedArguments -join "`n")) 'Process arguments were not preserved as an argument list.'
    }
    else {
        Assert-True ($startInfo.Arguments -ceq '"x" "C:\backup path\archive & literal.7z" "hMailServerBackup.xml" "-so" "-y"') 'Process arguments were not preserved for Windows PowerShell.'
    }

    $failureExecutable = Join-Path $tempRoot 'failure-fake.exe'
    Copy-Item -LiteralPath (Join-Path $env:WINDIR 'System32\where.exe') -Destination $failureExecutable
    $failureStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $failureExecutable -ArgumentList @('__hmailserver_missing_test_command__')
    Assert-ThrowsLike { Invoke-Net10RollbackArchiveProcess -StartInfo $failureStartInfo -Timeout ([TimeSpan]::FromSeconds(5)) } '*exit code*'

    $timeoutExecutable = Join-Path $tempRoot 'timeout-fake.exe'
    Copy-Item -LiteralPath (Join-Path $env:WINDIR 'System32\ping.exe') -Destination $timeoutExecutable
    $timeoutStartInfo = New-Net10RollbackArchiveProcessStartInfo -FilePath $timeoutExecutable -ArgumentList @('127.0.0.1', '-n', '30')
    Assert-ThrowsLike { Invoke-Net10RollbackArchiveProcess -StartInfo $timeoutStartInfo -Timeout ([TimeSpan]::FromMilliseconds(150)) } '*timed out*'

    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive (Join-Path $tempRoot 'missing.7z') -SevenZipPath $sevenZipPath } '*not found*'
    Assert-ThrowsLike { Assert-Net10RollbackArchivePreflight -BackupArchive $validArchive -SevenZipPath (Join-Path $binDirectory 'missing-7za.exe') } '*7za.exe*not found*'

    $installerSource = Get-Content -LiteralPath $installerPath -Raw
    Assert-True ($installerSource -match '\[string\]\$BackupArchive') 'Installer does not declare an explicit -BackupArchive parameter.'
    $stateCheckIndex = $installerSource.IndexOf("`$existingService.State -ne 'Stopped'", [StringComparison]::Ordinal)
    $preflightIndex = $installerSource.IndexOf('Assert-Net10RollbackArchivePreflight', [StringComparison]::Ordinal)
    $registerComIndex = $installerSource.IndexOf('& $executable --register-com', [StringComparison]::Ordinal)
    $serviceMutationIndex = $installerSource.IndexOf('& sc.exe', [StringComparison]::Ordinal)
    Assert-True ($stateCheckIndex -ge 0 -and $preflightIndex -gt $stateCheckIndex) 'Rollback preflight is not after service state checks.'
    Assert-True ($registerComIndex -gt $preflightIndex) 'Rollback preflight does not precede --register-com.'
    Assert-True ($serviceMutationIndex -gt $preflightIndex) 'Rollback preflight does not precede sc.exe mutations.'
    Assert-True ($installerSource -match '(?s)if \(\$requiresRollbackArchive\).*?Assert-Net10RollbackArchivePreflight') 'Rollback preflight is not guarded by the replacement requirement.'

    foreach ($path in @($helperPath, $installerPath, $PSCommandPath)) {
        $tokens = $null
        $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
        $parserMessages = @($errors | ForEach-Object { $_.Message }) -join '; '
        Assert-True ($errors.Count -eq 0) "PowerShell parser errors in '$path': $parserMessages"
    }

    Write-Output 'PASS: rollback archive preflight validates archives without installer side effects.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
