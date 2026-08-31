# Installer/Rollback Acceptance Status

Date: 2026-08-31
Decision: **ENVIRONMENT-BLOCKED** for the machine-level drill.

## Completed safely

`build/test-net10-rollback-archive-preflight.ps1` passed. The preflight validates
rollback archive structure, bounded metadata/listing reads, process timeout and
output limits, service snapshot parsing, installer ordering, uninstaller
compensation references, and PowerShell syntax without installing a service,
registering COM, or changing the registry.

## Blocked operation

The actual installer drill was not run. `build/install-net10-service.ps1` calls
the target executable with `--register-com` and then mutates the `hMailServer`
service through `sc.exe create/config`; `build/uninstall-net10-service.ps1`
deletes the service and unregisters COM. These are machine-wide service,
registry, and COM changes and require a dedicated disposable installed-service
environment with an independently captured legacy rollback state.

The current host has no disposable registered `hMailServer` service/COM graph
that can be used as the rollback baseline. No production service, registry,
COM registration, DCOM ACL, database, or Data directory was touched.

## Required continuation

Run the installer and failure-injection rollback drill only on a disposable VM
or isolated test installation containing a verified legacy service snapshot and
an isolated Data/SQL clone. Capture pre/post service configuration, COM graph,
executable hashes, database/Data identity, listener readiness, and rollback
state. Until those artifacts exist, the installer/rollback release gate stays
**RED**.
