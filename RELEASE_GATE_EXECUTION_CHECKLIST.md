# .NET 10 Release-Gate Execution Checklist

This is the controlled procedure for the remaining environment-gated work.
The current host installation, production SQL/Data paths, installed COM
registration, DCOM ACLs, and production ports are out of scope.

## Disposable paired runner

Run on `HMailServer-SEC18-Disposable` or another explicitly approved isolated
runner from an elevated PowerShell session:

```powershell
Get-VM
Get-Service hMailServer,MSSQLSERVER | Select-Object Name,Status,StartType
sqlcmd.exe -S localhost -E -Q "SELECT @@SERVERNAME"
```

Stop if VM identity, SQL disposability, backup hash, or Data-root ownership is
not proven. Create only uniquely named targets using the existing fixture
guard:

```powershell
& .\build\provision-paired-benchmark-fixture.ps1 `
  -BackupPath <DISPOSABLE_SQL_BACKUP> `
  -SourceDataRoot <CLONED_DATA_ROOT> `
  -OutputRoot C:\hmail-perf-pair-<STAMP>
```

Then run `build/collect-live-equivalence-evidence.ps1` and
`build/test-live-equivalence-evidence.ps1`. Require
`EQUIVALENT_START_STATE`, equal SQL/Data manifests, valid domain/account/Inbox,
three matching loopback listener rows, and Full-Text readiness on both sides.

## Paired performance

Run C++ and Net10 sequentially on the same loopback ports
`127.0.0.1:2525`, `127.0.0.1:1143`, and `127.0.0.1:25110`. Use fresh equal
fixtures, explicit executable paths, PID ownership checks, readiness checks,
port-release checks, and separate JSON/CSV/Markdown output directories.

Required scenarios are:

- `benchmark-net10-live-smtp-acceptance.ps1`
- `benchmark-net10-live-protocol.ps1`
- `benchmark-net10-live-concurrent-imap.ps1` at 1,000 sessions
- SQL Full-Text IMAP SEARCH/SORT
- POP3 large mailbox
- local/remote delivery queue and retry/defer
- external fetch
- restart lifecycle
- 24-hour resource soak

The C++ registry/configuration must resolve only the copied disposable binary.
Never start the installed `/Debug` binary: its startup can write the installed
Application registration. A speed-up or winner is valid only when both sides
pass the same fixture, workload, cleanup, and resource gates. Missing C++ data
is `BLOCKED`, never zero.

## Migration and rollback

Run `build/test-net10-rollback-archive-preflight.ps1` first. Only on the
disposable VM, with a verified archive and stopped target, exercise
`build/install-net10-service.ps1 -Configuration Release -ReplaceExisting
-BackupArchive <DISPOSABLE_ARCHIVE>`. Capture service, COM registration, SQL,
Data, and listener hashes; force registration/service/start/readiness failures;
run compensating rollback; and verify the legacy state returns. Do not touch
the installed production Application AppID.

## SEC-18 staging

Keep IIS loopback-only. Complete PHP/FastCGI, the dedicated
`HMailWebAdminBrokerPool`, site `127.0.0.1:8088`, health request, worker PID /
token SID, COM caller SID, and rollback evidence. Run
`build/get-webadmin-broker-staging-inventory.ps1 -FailOnIncomplete` and the
matching inventory/denial validators. Do not register the broker or change
the existing Application DCOM ACL until independent security and reality
reviews are `GREEN`.

## Cleanup

Stop only launched processes, verify all loopback ports are released, drop only
databases matching `hmail_perf_pair_*`, and remove only roots matching
`C:\hmail-perf-pair-*`. Preserve failed evidence. If target identity cannot be
proven, leave the gate `RED`.
