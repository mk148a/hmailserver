[CmdletBinding()]
param(
    [string]$PoolName = 'HMailWebAdminBrokerPool',
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This collector must run elevated.'
}

Import-Module WebAdministration -ErrorAction Stop
$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
$workerLine = @(& $appcmd list wp) |
    Where-Object { $_ -match [regex]::Escape("applicationPool:$PoolName") } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($workerLine) -or $workerLine -notmatch 'WP\s+"(?<pid>\d+)"') {
    throw "No live IIS worker was found for application pool: $PoolName"
}

$workerPid = [int]$Matches.pid
$worker = Get-Process -Id $workerPid -IncludeUserName -ErrorAction Stop
$poolAccount = "IIS APPPOOL\$PoolName"
$poolSid = ([Security.Principal.NTAccount]::new($poolAccount)).Translate(
    [Security.Principal.SecurityIdentifier]).Value

if (-not ('Sec18NativeTokenReader' -as [type])) {
    $nativeSource = @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

public sealed class Sec18TokenSnapshot
{
    public Sec18TokenSnapshot(string sid)
    {
        Sid = sid;
        TokenType = 1;
        ImpersonationLevel = 0;
    }

    public string Sid;
    public int TokenType;
    public int ImpersonationLevel;
}

public static class Sec18NativeTokenReader
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    public static Sec18TokenSnapshot Read(int processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed.");

        try
        {
            IntPtr token;
            if (!OpenProcessToken(process, TokenQuery, out token))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken failed.");

            try
            {
                using (var tokenIdentity = new WindowsIdentity(token))
                {
                    return new Sec18TokenSnapshot(tokenIdentity.User.Value);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
'@
    Add-Type -TypeDefinition $nativeSource
}

$token = [Sec18NativeTokenReader]::Read($workerPid)
$hash = if ($worker.Path -and (Test-Path -LiteralPath $worker.Path)) {
    (Get-FileHash -LiteralPath $worker.Path -Algorithm SHA256).Hash
}
else {
    $null
}

$evidence = [ordered]@{
    SchemaVersion = 1
    EvidenceKind = 'SEC18-LiveWorkerPrimaryToken'
    PoolName = $PoolName
    PoolAccount = $poolAccount
    PoolSid = $poolSid
    WorkerPid = $workerPid
    WorkerProcessStartUtc = $worker.StartTime.ToUniversalTime().ToString('o')
    WorkerExecutable = $worker.Path
    WorkerExecutableSha256 = $hash
    WorkerReportedUserName = $worker.UserName
    WorkerTokenSid = $token.Sid
    WorkerTokenType = $token.TokenType
    WorkerTokenImpersonationLevel = $token.ImpersonationLevel
    TokenSidMatchesPoolSid = [string]::Equals($token.Sid, $poolSid, [StringComparison]::OrdinalIgnoreCase)
    CapturedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    ProductionPathsTouched = @()
    RegistrationOrDcomChanged = $false
}

if (-not $evidence.TokenSidMatchesPoolSid) {
    throw "Live worker token SID does not match the dedicated pool SID."
}

$json = $evidence | ConvertTo-Json -Depth 5
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $parent = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw "Output directory does not exist: $parent"
    }
    [IO.File]::WriteAllText(
        [IO.Path]::GetFullPath($OutputPath),
        $json + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
}

$json
