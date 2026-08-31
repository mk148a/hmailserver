# Restore Dry-Run Audit

- Audit date: 2026-08-31
- Decision: **PARTIAL PASS / ENVIRONMENT-BLOCKED**
- Scope: read-only/unit restore planning and containment validation. No SQL
  database was created, changed, or dropped. No existing Data directory was
  opened or replaced.

## Focused Validation

The built test assembly was executed with `dotnet vstest`:

```text
BackupRestoreDryRunPlannerTests
BackupRestoreContainmentPreflightTests
BackupRestoreExecutionGateTests
BackupRestoreMetadataWriterTests
BackupRestoreIntegrityRuntimeTests

Passed: 121  Failed: 0  Skipped: 0
```

These tests validate the read-only plan, option warnings, path containment,
reparse-point protections, execution ownership gate, metadata writing, and
integrity/runtime boundaries.

## Blocked Acceptance

The SQL/Data acceptance tests require both:

- `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION`
- `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1`

Neither variable was configured in this run. The live round-trip suite was
therefore not invoked and no substitute database or Data path was guessed.
The required environment must be a disposable SQL Server database plus a new
isolated Data directory whose target is proven disposable before any destructive
restore test.

## Required Continuation

1. Provide a local disposable SQL connection and explicit isolated-create opt-in.
2. Provision a fresh isolated Data root outside every hMailServer production or
   test installation path.
3. Run the populated backup -> restore -> backup semantic round trip and
   injected-failure rollback tests.
4. Capture SQL/Data hashes, row counts, file manifests, rollback journal state,
   and cleanup evidence.

Until that evidence exists, restore MVP is not release-accepted and the release
gate remains RED.
