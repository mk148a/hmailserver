# SEC-18 Live Worker Identity Capture

Observed: 2026-07-21 UTC
Environment: non-production isolated IIS staging

## Captured

- `http://127.0.0.1:8088/health.php` returned HTTP `200`.
- PHP `8.4.23`, `cgi-fcgi`, `comDotNetLoaded=true`, and `hMailServerComActivated=false` were reported.
- The dedicated pool SID resolved to `S-1-5-82-2759919546-3181318411-3457700337-2112356574-3667061494`.
- Current `w3wp.exe` PID `36384` and child `php-cgi.exe` PID `8428` were observed.
- hMailServer was `Stopped` and `Disabled` with zero hMailServer processes.

## Not Captured

The non-elevated caller could not open either worker process token. Both `OpenProcessToken` attempts returned `Access denied`, so no effective worker-token SID is asserted by this report. This is a current host limitation, not evidence that the process SID differs from the expected pool SID.

## Gate

`DedicatedPoolCandidate` and caller-token matching remain unproven in the current run. The next required operation is an elevated read-only staging inventory and token capture. No registry, DCOM, service, database, data-directory, PHP-authentication, or firewall change was made.

Source: `SEC18-worker-identity-live-20260721.json`.
