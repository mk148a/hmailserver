# SEC-18 Bounded Probe Report

Date: 2026-07-15
Host: NOUTML-KANDIL
Environment: non-production isolated IIS staging
Decision: RED for permanent WebAdminSessionBroker registration

## Evidence

- `HMailWebAdminBrokerPool` worker and PHP process reported the expected virtual-account SID `S-1-5-82-2759919546-3181318411-3457700337-2112356574-3667061494`.
- PHP, `php_com_dotnet.dll`, the temporary probe, and the registered TypeLib were x64-compatible.
- Temporary AppID LaunchPermission and AccessPermission both contained only SYSTEM and the dedicated pool SID.
- Non-elevated `NOUTML-KANDIL\Kandil` activation failed before method execution with `0x80070005` / Win32 error 5. Evidence and invocation counter remained absent.
- The real PHP endpoint failed during COM activation with `2147942405` / `0x80070005`; interface acquisition and method invocation were not reached.
- A child executable launched from PHP inherited the exact pool SID but also failed at activation with `0x80070005`; this rules out a PHP-only COM extension explanation.
- A SYSTEM diagnostic reached the probe method, failed closed at impersonation level 1, and reverted successfully. It is not valid pool caller-token evidence.
- The collector rerun remained `Incomplete` with exit code 2 because no valid server-side caller-token evidence exists. The virtual-account doubled-backslash defect and focused regression test are fixed.

## Gate

Both `hmail_security_reviewer` and `hmail_reality_checker` returned RED. Do not register the permanent broker. The authorized pool path must first activate the temporary probe and produce server-side `CoQueryClientBlanket`, `CoImpersonateClient`, `OpenThreadToken`, exact SID comparison, and verified `CoRevertToSelf` evidence.

Exact JSON evidence is in `SEC18-bounded-probe-report-20260715.json`. Supporting files include `desktop-denial-20260715.json`, `php-activation-diagnostics-20260715.json`, `pool-child-activation-20260715.json`, `probe-registration-readback-20260715.json`, and `staging-inventory-20260715-bounded-tests.json`.

## Cleanup

The temporary probe service, COM registration, TypeLib, endpoints, and probe files were removed by `C:\SEC18-Staging\Probe\rollback-sec18-probe.ps1`. Its `-WhatIf` plan was validated before execution. Post-cleanup validation confirms the IIS site/pool/PHP runtime remain available, the staging health endpoint responds on loopback, and hMailServer remains stopped and disabled.
