# SEC-18 Non-Pool Identity Denial Review

Collected: 2026-07-21 UTC
Environment: non-production isolated IIS staging
Decision: RED - permanent WebAdminSessionBroker registration remains blocked.

## Authorization Boundary

The approved scope was negative testing only. The desktop COM test had to run non-elevated. No desktop user, standard user, group, pool-wide ACL, machine DCOM default, existing hMailServer Application registration, hMailServer service, database, data directory, PHP authentication flow, or firewall rule was changed.

## Exact Prior Live Results

- PHP/FastCGI failed at COM activation with `0x80070005` / Win32 error `5`. Interface acquisition and method invocation were not reached. No invocation counter or caller-evidence file appeared.
- Non-elevated `NOUTML-KANDIL\\Kandil` failed at COM activation with `0x80070005` / Win32 error `5`. Method entry, invocation counter, and caller evidence remained absent.
- The separately authorized pool path reached the temporary probe. `CoImpersonateClient=0`, `OpenThreadToken=0`, the caller SID matched `S-1-5-82-2759919546-3181318411-3457700337-2112356574-3667061494`, `CoRevertToSelf=0`, and post-return token lookup returned Win32 `1008` (`ERROR_NO_TOKEN`).
- A wrong-SID method call returned `0x80070005`.

These are temporary-probe results. They do not prove the permanent broker registration boundary.

## Current Host State

- The temporary probe service, registry roots, endpoints, binaries, and unsanitized evidence are absent after cleanup.
- The permanent broker AppID is absent in both Registry64 and Registry32 views.
- The staging PHP health endpoint remains available with PHP 8.4.23 and COM support loaded.
- hMailServer is `Stopped` and `Disabled` with no hMailServer process.

## Current Collector Run

`staging-inventory-20260721-nonpool-denial-review.json` was generated read-only. Because this shell is non-elevated, IIS configuration could not be read. The collector therefore reports no live worker SID, no caller-token evidence, `Status=Incomplete`, and `ReadyForBrokerRegistration=false`. The virtual-account construction helper remains fixed and its focused test passes.

## Validation

- Collector helper tests: pass.
- REG_BINARY security-descriptor test: pass.
- Focused broker tests: 50 passed, 1 skipped by default.
- Focused native registry/boundary tests with integration enabled: 37/37 passed.
- Full Net10 tests with native registry integration enabled: 1131/1131 passed.

## Independent Gate Reviews

- `hmail_security_reviewer`: RED. Require fresh broker-specific dual-view registration/readback, isolated activation, pool caller-token proof, and negative desktop/wrong-SID tests.
- `hmail_reality_checker`: RED. Current evidence is historical for the probe; the current collector is incomplete and no permanent registration is justified.

## Next Safe Slice

Run the staging inventory elevated and read-only. With explicit authorization only, reprovision a fresh isolated temporary probe and rerun the complete positive/negative identity matrix, then clean up and retain only sanitized evidence. Do not register the permanent broker, change DCOM ACLs, activate `hMailServer.Application`, or change PHP session behavior.

Sources: `php-activation-diagnostics-20260715.json`, `desktop-denial-20260715.json`, `SEC18-authorized-pool-evidence-20260716.json`, `SEC18-nonpool-denial-continuation-20260720.json`, `probe-cleanup-validation-20260715.json`, `staging-inventory-20260721-nonpool-denial-review.json`.
