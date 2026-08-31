$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$runnerPath = Join-Path $repoRoot "build\benchmark-disposable-cpp-service-protocol.ps1"
$protocolPath = Join-Path $repoRoot "build\benchmark-net10-live-protocol.ps1"
$smtpPath = Join-Path $repoRoot "build\benchmark-net10-live-smtp-acceptance.ps1"

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Name)
    if ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -lt 0) {
        throw "${Name}: missing '$Needle'"
    }
}

function Assert-Parses {
    param([string]$Path)
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -ne 0) {
        throw "$Path has PowerShell parse errors: $((($errors | ForEach-Object { $_.Message }) -join '; '))"
    }
}

$runner = Get-Content -LiteralPath $runnerPath -Raw
$protocol = Get-Content -LiteralPath $protocolPath -Raw
$smtp = Get-Content -LiteralPath $smtpPath -Raw
Assert-Parses $runnerPath
Assert-Parses $protocolPath
Assert-Parses $smtpPath

Assert-Contains $runner '[string]$FixtureManifest' 'fixture input'
Assert-Contains $runner '[string]$Workload = "protocol"' 'workload selector'
Assert-Contains $runner 'benchmark-net10-live-concurrent-imap.ps1' 'concurrent IMAP child runner'
Assert-Contains $runner 'benchmark-net10-live-smtp-acceptance.ps1' 'SMTP child runner'
Assert-Contains $runner "'-Concurrency', $Concurrency" 'concurrent IMAP child arguments'
Assert-Contains $runner 'disposable-cpp-service-concurrent-imap-v1' 'concurrent IMAP report schema'
Assert-Contains $runner "'-MessageCount', $MessageCount" 'SMTP child arguments'
Assert-Contains $runner 'disposable-cpp-service-smtp-v1' 'SMTP report schema'
Assert-Contains $runner 'Read-LiveBenchmarkFixtureManifest -Path $FixtureManifest -Implementation cpp' 'fixture binding'
Assert-Contains $runner 'NT AUTHORITY\LocalService' 'dedicated service identity'
Assert-Contains $runner 'CREATE LOGIN [$principal] FROM WINDOWS' 'disposable SQL login provisioning'
Assert-Contains $runner 'DROP LOGIN [$principal]' 'disposable SQL login cleanup'
Assert-Contains $runner 'Get-ServiceRecord "hMailServer"' 'production service guard'
Assert-Contains $runner "sc.exe @createArgs" 'SCM service creation'
Assert-Contains $runner 'sc.exe start $ServiceName' 'SCM service start'
Assert-Contains $runner 'sc.exe stop $ServiceName' 'SCM service stop'
Assert-Contains $runner 'sc.exe delete $ServiceName' 'SCM service deletion'
Assert-Contains $runner '-ExternalServiceProcessId' 'delegated process identity'
Assert-Contains $runner 'sqlPrincipalCreatedAndRemoved' 'SQL cleanup evidence'
Assert-Contains $runner 'productionServiceUntouched' 'production safety evidence'
Assert-Contains $runner 'function Get-TcpIpPortPreflight' 'SQL listener preflight'
Assert-Contains $runner 'tcpipPortPreflight = $portPreflight' 'SQL listener evidence'
Assert-Contains $runner "'1|2525|2130706433|NULL'" 'SMTP loopback mapping'
Assert-Contains $runner "'3|25110|2130706433|NULL'" 'POP3 loopback mapping'
Assert-Contains $runner "'5|1143|2130706433|NULL'" 'IMAP loopback mapping'

Assert-Contains $protocol '[int]$ExternalServiceProcessId = 0' 'external worker parameter'
Assert-Contains $protocol 'ExternalServiceProcessId is supported only for the legacy C++ implementation' 'external mode implementation guard'
Assert-Contains $protocol '-DisposableRegistrationGuarded:$externalService' 'guarded C++ preflight'
Assert-Contains $protocol 'if (-not $externalService -and $null -ne $process' 'external lifecycle ownership'
Assert-Contains $protocol 'serviceBacked = $externalService' 'service-backed report field'

Assert-Contains $smtp '[int]$ExternalServiceProcessId = 0,' 'SMTP external worker parameter'
Assert-Contains $smtp 'ExternalServiceProcessId is supported only for the legacy C++ implementation' 'SMTP external mode implementation guard'
Assert-Contains $smtp 'if (-not $externalService -and $null -ne $process' 'SMTP external lifecycle ownership'
Assert-Contains $smtp 'serviceBacked = $externalService' 'SMTP service-backed report field'

Write-Output 'PASS: disposable C++ service protocol runner contract tests'
