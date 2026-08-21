# SEC-18 Phase 2 IIS Inventory

- Status: `HISTORICAL` pre-install snapshot; superseded by
  SEC18-phase3-iis-php-inventory-20260821.md
- Collected UTC: `2026-08-21T04:29:33.8456973Z`
- Guest: `HMailServer-SEC18-Disposable`
- Guest computer: `WIN-6TGBDE5C01K`
- OS: Windows Server 2025 Standard Evaluation, build `26100`
- JSON evidence: `staging-inventory-20260821-phase2-iis.json`
- JSON SHA-256: `80F063D90C8EEEBF97F3A5CDB955ECA57BD6902B7DDB23E95C628973DDC6143D`

## Feature result

The exact discovered feature names were enabled with
`Enable-WindowsOptionalFeature -Online -All -NoRestart`:

- `IIS-WebServerRole`: enabled
- `IIS-WebServer`: enabled
- `IIS-CGI`: enabled
- `IIS-ManagementConsole`: enabled
- `IIS-ManagementScriptingTools`: enabled

All five were previously disabled. No restart is pending.

## Isolation result

- The guest exposes no hMailServer or MSSQLSERVER service.
- No PHP executable or `C:\SEC18-Staging` child root exists yet.
- No listener was found on `8088`, `2525`, `1143`, or `25110`.
- IIS `W3SVC` is present and running after feature enablement.

## Historical gate status

`YELLOW`: the disposable VM and required IIS components are proven, but the
staging site, PHP runtime, FastCGI mapping, application pool, PHP COM probe,
worker identity evidence, and rollback validation are still incomplete.

PHP 8.4.23 NTS x64 archive URL and byte size were verified against the official
PHP Windows archive. The official archive exposes no matching SHA-256,
SPDX, or OpenVEX sidecar for this archived build; download and extraction are
therefore paused pending the required approval to proceed with the locally
computed hash plus official archive listing evidence.

The guest does not have the Microsoft Visual C++ 2015-2022 x64 runtime
registry entry (`HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64`).
`curl.exe` and `Expand-Archive` are present. PHP installation therefore also
requires approval to download/install the runtime from Microsoft if the PHP
binary reports that dependency.
