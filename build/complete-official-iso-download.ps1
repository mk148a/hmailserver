[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [Parameter(Mandatory = $true)]
    [string] $Url,

    [Parameter(Mandatory = $true)]
    [Int64] $TotalBytes,

    [int] $ParallelParts = 3
)

$ErrorActionPreference = 'Stop'
if ($ParallelParts -lt 2 -or $ParallelParts -gt 8) {
    throw 'ParallelParts must be between 2 and 8.'
}
if ($Url -notmatch '^https://software-static\.download\.prss\.microsoft\.com/') {
    throw 'Refusing a non-Microsoft ISO URL.'
}
if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    throw "Expected an existing sequential partial download: $OutputPath"
}

$partialLength = (Get-Item -LiteralPath $OutputPath).Length
if ($partialLength -le 0 -or $partialLength -ge $TotalBytes) {
    throw "Partial download length is not between zero and TotalBytes: $partialLength"
}

$partPaths = @()
$partialPath = "$OutputPath.part0"
if (Test-Path -LiteralPath $partialPath) {
    Remove-Item -LiteralPath $partialPath -Force
}
Move-Item -LiteralPath $OutputPath -Destination $partialPath
$partPaths += $partialPath

$span = [math]::Ceiling(($TotalBytes - $partialLength) / $ParallelParts)
$jobs = @()
for ($index = 1; $index -le $ParallelParts; $index++) {
    $start = $partialLength + (($index - 1) * $span)
    $end = [math]::Min($TotalBytes - 1, $start + $span - 1)
    $partPath = "$OutputPath.part$index"
    $partPaths += $partPath
    $jobs += Start-Job -ScriptBlock {
        param($DownloadUrl, $RangeStart, $RangeEnd, $Destination)
        & curl.exe -L --fail --retry 3 --range "$RangeStart-$RangeEnd" --output $Destination $DownloadUrl
        if ($LASTEXITCODE -ne 0) {
            throw "curl failed with exit code $LASTEXITCODE for $RangeStart-$RangeEnd"
        }
    } -ArgumentList $Url, $start, $end, $partPath
}

try {
    $jobs | Wait-Job | Out-Null
    $failed = @($jobs | Where-Object State -ne 'Completed')
    $jobs | Receive-Job
    if ($failed.Count -gt 0) {
        throw 'One or more ISO range downloads failed.'
    }
    foreach ($partPath in $partPaths) {
        if (-not (Test-Path -LiteralPath $partPath -PathType Leaf)) {
            throw "Missing ISO part: $partPath"
        }
    }

    $output = [IO.File]::Open($OutputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        foreach ($partPath in $partPaths) {
            $input = [IO.File]::OpenRead($partPath)
            try { $input.CopyTo($output) } finally { $input.Dispose() }
        }
    } finally {
        $output.Dispose()
    }

    $actualLength = (Get-Item -LiteralPath $OutputPath).Length
    if ($actualLength -ne $TotalBytes) {
        throw "Merged ISO length $actualLength does not equal expected $TotalBytes."
    }
    Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
    Get-Item -LiteralPath $OutputPath | Select-Object FullName,Length,LastWriteTime
}
finally {
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
    foreach ($partPath in $partPaths) {
        if (Test-Path -LiteralPath $partPath) {
            Remove-Item -LiteralPath $partPath -Force -ErrorAction SilentlyContinue
        }
    }
}
