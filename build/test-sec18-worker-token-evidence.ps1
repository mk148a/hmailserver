[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$collector = Join-Path $PSScriptRoot 'get-sec18-worker-token-evidence.ps1'
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($collector, [ref]$null, [ref]$errors) | Out-Null
if ($errors) {
    throw "Worker token collector parse failed: $($errors | Out-String)"
}

$source = Get-Content -LiteralPath $collector -Raw
foreach ($marker in @(
    'OpenProcessToken',
    'WindowsIdentity(token)',
    'WorkerTokenSid',
    'WorkerTokenSource',
    'TokenSidMatchesPoolSid',
    'OutputPath already exists',
    'C:\SEC18-Staging'
)) {
    if ($source.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Worker token collector is missing safety marker: $marker"
    }
}

if ($source -match '(?i)New-ItemProperty|Set-ItemProperty|reg\.exe\s+(add|delete)|Register-') {
    throw 'Worker token collector contains a registry or COM registration mutation.'
}

foreach ($forbidden in @('WorkerTokenType', 'WorkerTokenImpersonationLevel', 'ProductionPathsTouched', 'RegistrationOrDcomChanged')) {
    if ($source.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Worker token collector contains an unmeasured or out-of-scope claim: $forbidden"
    }
}

Write-Output 'SEC-18 worker token collector tests passed.'
