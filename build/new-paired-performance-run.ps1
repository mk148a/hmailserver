param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureManifest,
    [Parameter(Mandatory = $true)]
    [string]$InputRoot,
    [string]$OutputPath = "",
    [string]$RunId = "",
    [string]$ExistingDescriptorPath = "",
    [switch]$Seal
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
. (Join-Path $PSScriptRoot "live-benchmark-provenance.ps1")

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash.ToUpperInvariant()
}

function Resolve-BenchmarkArtifactRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $benchmarkRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\benchmarks")).TrimEnd('\')
    if (-not $fullPath.StartsWith($benchmarkRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Paired run input root must be under the repository benchmark artifacts directory: $benchmarkRoot"
    }
    if ($fullPath -match '(?i)(^|\\)(hmaildb_test5700|Program Files|ProgramData|Windows)(\\|$)') {
        throw "Paired run input root is production-like: $fullPath"
    }
    return $fullPath
}

function Get-RunGuid {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return [Guid]::NewGuid().ToString("D")
    }
    try {
        $parsed = [Guid]::Parse($Value)
    }
    catch {
        throw "RunId must be a valid GUID."
    }
    if ($parsed -eq [Guid]::Empty) {
        throw "RunId must not be empty."
    }
    return $parsed.ToString("D")
}

$slotDefinitions = @(
    [pscustomobject]@{ name = "protocol-cpp"; implementation = "cpp"; relativePath = "protocol-cpp\net10-live-protocol.json" },
    [pscustomobject]@{ name = "protocol-net10"; implementation = "net10"; relativePath = "protocol-net10\net10-live-protocol.json" },
    [pscustomobject]@{ name = "concurrent-cpp-100"; implementation = "cpp"; relativePath = "concurrent-cpp-100\live-concurrent-imap.json" },
    [pscustomobject]@{ name = "concurrent-cpp-500"; implementation = "cpp"; relativePath = "concurrent-cpp-500\live-concurrent-imap.json" },
    [pscustomobject]@{ name = "concurrent-cpp-1000"; implementation = "cpp"; relativePath = "concurrent-cpp-1000\live-concurrent-imap.json" },
    [pscustomobject]@{ name = "concurrent-net10-100"; implementation = "net10"; relativePath = "concurrent-net10-100\live-concurrent-imap.json" },
    [pscustomobject]@{ name = "concurrent-net10-500"; implementation = "net10"; relativePath = "concurrent-net10-500\live-concurrent-imap.json" },
    [pscustomobject]@{ name = "concurrent-net10-1000"; implementation = "net10"; relativePath = "concurrent-net10-1000\live-concurrent-imap.json" },
    [pscustomobject]@{ name = "smtp-cpp-500"; implementation = "cpp"; relativePath = "smtp-cpp-500\cpp-smtp-message-acceptance.json" },
    [pscustomobject]@{ name = "smtp-net10-500"; implementation = "net10"; relativePath = "smtp-net10-500\net10-smtp-message-acceptance.json" },
    [pscustomobject]@{ name = "soak-net10-1000x20"; implementation = "net10"; relativePath = "soak-net10-1000x20\live-concurrent-imap.json" }
)

if ($Seal) {
    if ([string]::IsNullOrWhiteSpace($ExistingDescriptorPath)) {
        if ([string]::IsNullOrWhiteSpace($OutputPath)) {
            throw "ExistingDescriptorPath or OutputPath is required when sealing a descriptor."
        }
        $ExistingDescriptorPath = $OutputPath
    }
    $descriptorPath = [IO.Path]::GetFullPath($ExistingDescriptorPath)
    if (-not (Test-Path -LiteralPath $descriptorPath -PathType Leaf)) {
        throw "Run descriptor is missing: $descriptorPath"
    }
    $descriptor = Get-Content -LiteralPath $descriptorPath -Raw | ConvertFrom-Json
    if ($descriptor.schema -cne "paired-cpp-net10-run-v1" -or $descriptor.status -cne "OPEN") {
        throw "Only an OPEN paired-cpp-net10-run-v1 descriptor can be sealed."
    }
    $inputRoot = Resolve-BenchmarkArtifactRoot $descriptor.inputRoot
    $fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation net10 -RepositoryRoot $repoRoot
    $manifestHash = Get-Sha256 $FixtureManifest
    if ($descriptor.fixtureId -cne $fixture.fixtureId -or $descriptor.manifestSha256 -cne $manifestHash) {
        throw "Run descriptor fixture identity does not match the current fixture manifest."
    }
    if ($descriptor.artifactSlots.Count -ne $slotDefinitions.Count) {
        throw "Run descriptor does not contain the complete paired matrix."
    }
    foreach ($definition in $slotDefinitions) {
        $slot = @($descriptor.artifactSlots | Where-Object name -ceq $definition.name)
        if ($slot.Count -ne 1 -or $slot[0].implementation -cne $definition.implementation -or $slot[0].relativePath -cne $definition.relativePath) {
            throw "Run descriptor artifact slot is invalid: $($definition.name)"
        }
        $artifactPaths = [ordered]@{
            json = Join-Path $inputRoot $definition.relativePath
            csv = Join-Path $inputRoot ([IO.Path]::ChangeExtension($definition.relativePath, '.csv'))
            markdown = Join-Path $inputRoot ([IO.Path]::ChangeExtension($definition.relativePath, '.md'))
        }
        foreach ($artifactName in $artifactPaths.Keys) {
            if (-not (Test-Path -LiteralPath $artifactPaths[$artifactName] -PathType Leaf)) {
                throw "Cannot seal missing $artifactName report: $($artifactPaths[$artifactName])"
            }
        }
        $slot[0].sha256 = Get-Sha256 $artifactPaths.json
        $slot[0].artifacts.json = $slot[0].sha256
        $slot[0].artifacts.csv = Get-Sha256 $artifactPaths.csv
        $slot[0].artifacts.markdown = Get-Sha256 $artifactPaths.markdown
    }
    $descriptor.status = "SEALED"
    $descriptor.sealedUtc = [DateTimeOffset]::UtcNow.ToString("o")
    $descriptor | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $descriptorPath -Encoding UTF8
    Write-Output "Sealed paired run descriptor: $descriptorPath"
    exit 0
}

$inputRoot = Resolve-BenchmarkArtifactRoot $InputRoot
$fixture = Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation net10 -RepositoryRoot $repoRoot
$descriptorPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $inputRoot "paired-run.json"
}
else {
    [IO.Path]::GetFullPath($OutputPath)
}
if (Test-Path -LiteralPath $descriptorPath) {
    throw "Refusing to overwrite an existing run descriptor: $descriptorPath"
}
$descriptor = [ordered]@{
    schema = "paired-cpp-net10-run-v1"
    status = "OPEN"
    runId = Get-RunGuid $RunId
    createdUtc = [DateTimeOffset]::UtcNow.ToString("o")
    fixtureId = $fixture.fixtureId
    manifestSha256 = Get-Sha256 $FixtureManifest
    inputRoot = $inputRoot
    artifactSlots = @(
        foreach ($definition in $slotDefinitions) {
            [ordered]@{
                name = $definition.name
                implementation = $definition.implementation
                relativePath = $definition.relativePath
                sha256 = $null
                artifacts = [ordered]@{
                    json = $null
                    csv = $null
                    markdown = $null
                }
            }
        }
    )
}
$parent = Split-Path -Parent $descriptorPath
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$descriptor | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $descriptorPath -Encoding UTF8
Write-Output "Created OPEN paired run descriptor: $descriptorPath"
Write-Output "RunId: $($descriptor.runId)"
