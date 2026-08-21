# SEC-18 Phase 3 IIS/PHP Staging Inventory

- Status: YELLOW for isolated staging infrastructure; RED for broker
  registration and release cutover
- Collected UTC: 2026-08-21T05:45:39Z
- Guest: HMailServer-SEC18-Disposable / WIN-6TGBDE5C01K
- Combined evidence: sec18-phase3-evidence-20260821.json
- Combined evidence SHA-256:
  909EC58458D4B16F2BFE969704A2AD7A3A729D6B0EA780B831CF591CB60648C
- Commit-safe redacted evidence:
  sec18-phase3-evidence-public-20260821.json
- Redacted evidence SHA-256:
  A820DC6975C3A3BB52BC212367199236F7DE78926A7B198E86CD056C007E2C4A

## Disposable boundary

- Hyper-V VM state: Running
- Switch: HMailServer-SEC18-Private (Private)
- VHD and Windows Server ISO hashes are recorded in
  hyperv-inventory-20260821.json.
- ProductionPathsTouched is empty.
- The guest has no hMailServer or MSSQLSERVER service and no hMailServer
  process. No hMailServer database or Data directory was accessed.

## PHP and WebAdmin

- PHP archive: https://downloads.php.net/~windows/releases/archives/php-8.4.23-nts-Win32-vs17-x64.zip
- Final host: downloads.php.net
- Size: 34,680,647 bytes
- Local SHA-256:
  826EFA189B21F46314AD497FF31467DE9F0953292F42B235542BE4FEEA182B48
- Official checksum sidecar: unavailable for this archived build; the hash is
  local acquisition/integrity evidence only.
- PHP 8.4.23, php-cgi.exe exit 0, openssl=true, com_dotnet=true.
- Microsoft VC++ x64 package was downloaded through
  https://aka.ms/vs/17/release/vc_redist.x64.exe, redirected to the official
  download.visualstudio.microsoft.com host, Authenticode Valid, signer
  Microsoft Corporation, exit code 0, no restart requested.
- WebAdmin was copied to C:\SEC18-Staging\WebAdmin from the test
  PHPWebAdmin tree; config.php was absent and the source tree was not
  modified.

## IIS worker

- Site: HMailWebAdminBrokerStaging
- Application pool: HMailWebAdminBrokerPool
- Identity: ApplicationPoolIdentity
- 32-bit mode: false (x64 PHP)
- Physical path: C:\SEC18-Staging\WebAdmin
- Binding: http/127.0.0.1:8088:
- Health request: HTTP 200,
  {"status":"ok","php":"8.4.23","com_dotnet":true,"openssl":true}
- Worker: PID 3684, C:\Windows\System32\inetsrv\w3wp.exe
- The OS-reported worker account and the live primary-token SID read through
  `OpenProcessToken` plus `WindowsIdentity(token)` matched the dedicated pool
  account/SID. The redacted report is
  `worker-token-evidence-public-20260821.json`; the raw SID is retained only
  in the local `worker-token-evidence-20260821.json` evidence.
- Worker token collector code/test commit: `e2c9387ff`.
- The collector found exactly one site mapped to the dedicated pool.

The guest did not expose a non-loopback IPv4 address during the bounded
denial check. Get-WebBinding proves the IIS site binding is loopback-only;
HTTP.sys reports its normal kernel listener as 0.0.0.0:8088, so a
network-level external reachability proof is still required before a security
GREEN decision.

## Gate decision

RED for broker implementation. The live collector report is Incomplete: the
existing hMailServer Application AppID is not registered in the disposable
guest, no effective COM caller-token evidence was captured, and the legacy
hMailServer service is intentionally absent. The worker primary-token proof
only establishes the IIS process identity; it does not establish the caller
identity seen through COM. This is expected for the pre-registration gate and
proves that no existing Application registration or DCOM ACL was changed.

Do not register WebAdminSessionBroker, modify any DCOM ACL, alter the existing
Application identity, or change PHP authentication/session behavior. Remaining
mandatory work is an independently trusted caller-token/native-reader probe,
authorized and non-pool denial evidence, rollback validation, and independent
security/reality GREEN reviews.

## Verification

- Hyper-V disposable script tests: passed.
- Full Net10 Debug suite: 2482 passed, 84 skipped, 0 failed; TRX:
  artifacts/net10-disposable/test-results/full-net10-sec18-utc-parser-20260821.trx
- Collector inventory, registry-binary evidence, denial attestation, Hyper-V
  disposable script, and worker-token collector tests all passed. Full Net10
  Debug passed `2482`, skipped `84`, failed `0` (`2566` total); TRX:
  `artifacts/net10-disposable/test-results/full-net10-sec18-utc-parser-20260821.trx`.
