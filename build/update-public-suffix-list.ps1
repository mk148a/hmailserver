Param(
    [switch]$Check,
    [switch]$Update,
    [string]$ExpectedCommit = "",
    [string]$ExpectedSha256 = ""
)

$ErrorActionPreference = 'Stop'

if ($Check -and $Update) {
    throw 'Specify only one of -Check or -Update.'
}

$mode = if ($Update) { 'Update' } else { 'Check' }
$sourceUrl = 'https://publicsuffix.org/list/public_suffix_list.dat'
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$assetDirectory = Join-Path $repoRoot 'hmailserver\source\Server.Net10\assets'
$snapshotPath = Join-Path $assetDirectory 'public_suffix_list.dat'
$metadataPath = Join-Path $assetDirectory 'public_suffix_list.meta.json'
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)

function Get-Sha256Hex([byte[]]$Bytes) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-SnapshotHeaders([byte[]]$Bytes) {
    $content = [Text.Encoding]::UTF8.GetString($Bytes)
    $versionMatch = [regex]::Match($content, '(?m)^// VERSION: ([^\r\n]+)\r?$')
    $commitMatch = [regex]::Match($content, '(?m)^// COMMIT: ([0-9a-f]{40})\r?$')
    if (-not $versionMatch.Success -or -not $commitMatch.Success) {
        throw 'The public suffix snapshot is missing its VERSION or COMMIT header.'
    }

    if (-not $content.Contains('https://mozilla.org/MPL/2.0/')) {
        throw 'The public suffix snapshot is missing its MPL-2.0 notice.'
    }

    return [PSCustomObject]@{
        Version = $versionMatch.Groups[1].Value
        Commit = $commitMatch.Groups[1].Value
    }
}

function Assert-Equal($Actual, $Expected, [string]$Description) {
    if ($Actual -ne $Expected) {
        throw "$Description mismatch. Expected '$Expected', actual '$Actual'."
    }
}

if ($mode -eq 'Update') {
    if ($ExpectedCommit -notmatch '^[0-9a-f]{40}$') {
        throw '-ExpectedCommit must be a lowercase 40-character Git commit.'
    }

    if ($ExpectedSha256 -notmatch '^[0-9a-f]{64}$') {
        throw '-ExpectedSha256 must be a lowercase 64-character SHA-256 value.'
    }

    $webClient = New-Object System.Net.WebClient
    $webClient.Headers['User-Agent'] = 'hMailServer-Net10-PSL-Refresh/1.0'
    try {
        $snapshotBytes = $webClient.DownloadData($sourceUrl)
    }
    finally {
        $webClient.Dispose()
    }

    $headers = Get-SnapshotHeaders $snapshotBytes
    $actualSha256 = Get-Sha256Hex $snapshotBytes
    Assert-Equal $headers.Commit $ExpectedCommit 'Upstream commit'
    Assert-Equal $actualSha256 $ExpectedSha256 'Snapshot SHA-256'

    [IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
    $metadata = @"
{
  "sourceUrl": "$sourceUrl",
  "upstreamVersion": "$($headers.Version)",
  "upstreamCommit": "$($headers.Commit)",
  "sha256": "$actualSha256",
  "byteLength": $($snapshotBytes.Length)
}
"@.Replace("`r`n", "`n")

    $snapshotTempPath = "$snapshotPath.$([Guid]::NewGuid().ToString('N')).tmp"
    $metadataTempPath = "$metadataPath.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        [IO.File]::WriteAllBytes($snapshotTempPath, $snapshotBytes)
        [IO.File]::WriteAllText($metadataTempPath, $metadata + "`n", $utf8WithoutBom)
        Move-Item -LiteralPath $snapshotTempPath -Destination $snapshotPath -Force
        Move-Item -LiteralPath $metadataTempPath -Destination $metadataPath -Force
    }
    finally {
        Remove-Item -LiteralPath $snapshotTempPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $metadataTempPath -Force -ErrorAction SilentlyContinue
    }

    Write-Output "[OK] Updated public suffix snapshot $($headers.Version) ($($headers.Commit))."
    Write-Output "[OK] SHA-256 $actualSha256; $($snapshotBytes.Length) bytes."
    exit 0
}

if (-not (Test-Path -LiteralPath $snapshotPath -PathType Leaf)) {
    throw "Missing public suffix snapshot: $snapshotPath"
}

if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Missing public suffix metadata: $metadataPath"
}

$snapshotBytes = [IO.File]::ReadAllBytes($snapshotPath)
$headers = Get-SnapshotHeaders $snapshotBytes
$actualSha256 = Get-Sha256Hex $snapshotBytes
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json

Assert-Equal $metadata.sourceUrl $sourceUrl 'Source URL'
Assert-Equal $metadata.upstreamVersion $headers.Version 'Upstream version'
Assert-Equal $metadata.upstreamCommit $headers.Commit 'Upstream commit'
Assert-Equal $metadata.sha256 $actualSha256 'Snapshot SHA-256'
Assert-Equal ([long]$metadata.byteLength) ([long]$snapshotBytes.Length) 'Snapshot byte length'

Write-Output "[OK] Public suffix snapshot $($headers.Version) ($($headers.Commit))."
Write-Output "[OK] SHA-256 $actualSha256; $($snapshotBytes.Length) bytes."
