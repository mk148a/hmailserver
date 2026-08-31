# SEC-18 Staging Evidence Audit

- Audit date: 2026-08-31
- Decision: **RED / INCOMPLETE**
- Scope: read-only review of the existing disposable staging artifacts and
  repository self-tests. No IIS, registry, COM, DCOM, service, database, or
  Data-directory mutation was performed.

## Evidence Reviewed

- `artifacts/sec18-staging/sec18-staging-inventory-20260826.md`
- `artifacts/sec18-staging/staging-inventory-20260826-vm.json`
- `artifacts/sec18-staging/staging-inventory-20260826-registered.json`
- `artifacts/sec18-staging/sec18-worker-health-20260826.json`
- `build/get-webadmin-broker-staging-inventory.ps1`
- `build/get-sec18-worker-token-evidence.ps1`
- `build/attest-sec18-denial-evidence.ps1`
- `build/test-sec18-denial-evidence-attestation.ps1`
- `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/WebAdminSessionBrokerCallerGuard.cs`
- `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/WebAdminSessionBrokerAppIdPreflight.cs`

## Results

The repository-side safety tests all passed:

```text
test-webadmin-broker-staging-inventory.ps1       PASS
test-webadmin-broker-staging-inventory-regbinary.ps1 PASS
test-sec18-worker-token-evidence.ps1             PASS
test-sec18-denial-evidence-attestation.ps1       PASS
```

The VM inventory proves an IIS staging mapping, a dedicated
`ApplicationPoolIdentity`, and a real worker-token capture. It does not prove
that a COM broker received a call from that worker. Its caller evidence is
absent, invalid, and non-matching; the collector gate is `Incomplete`.

The two JSON inventories are not interchangeable evidence. The `-vm` record
reports no existing hMailServer Application AppID and no hMailServer service,
while the `-registered` record reports both as present. Both records lack
caller-token evidence and both leave the gate `Incomplete`. This conflict makes
the pair unsuitable for a registration or cutover decision until a fresh,
single-invocation inventory is captured and independently correlated.

The current host may still contain residual AppID registry values without a
complete legacy service/CLSID/COM graph. Such remnants are not a usable rollback
baseline and must not be described as a successful legacy registration.

## Why Self-Tests Are Not Live Proof

- `get-sec18-worker-token-evidence.ps1:62-63,117-118` reads the primary token of
  the live IIS worker, but does not make a COM call.
- `get-webadmin-broker-staging-inventory.ps1:515-563` validates the canonical
  installed Application AppID and fail-closed caller/service evidence, but
  never approves broker registration.
- `attest-sec18-denial-evidence.ps1:440-454,481-504` validates the shape,
  freshness, correlation, and failure semantics of supplied evidence fixtures;
  it cannot create independent caller-token evidence.
- The attestation fixtures do not independently prove live caller identity:
  authorized response fields are optional, and wrong-SID/non-pool checks rely
  on supplied records rather than a separately measured caller token. The
  attester is therefore a fixture validator, not live COM proof.
- `WebAdminSessionBrokerCallerGuard.cs:72-110` provides the guarded
  impersonation/revert operation, but no registered out-of-process caller has
  exercised it in this evidence set.
- `WebAdminSessionBrokerAppIdPreflight.cs:98-186` requires an exact broker-only
  ACL and unchanged installed Application identity; those live prerequisites
  are not proven here.

## Required Next Evidence

1. Capture one fresh inventory and worker-token record from the same isolated
   staging instance and invocation.
2. Use a separately registered disposable broker/caller probe to produce
   authorized worker, wrong-SID, and genuine non-pool denial records.
3. Correlate the server-observed caller SID with the actual worker token and
   prove guaranteed revert and method-level denial.
4. Preserve and byte-compare the installed Application registration graph.
5. Obtain independent security and reality GREEN reviews before any bridge
   registration or broker-only DCOM ACL change.

Until all five exist, SEC-18 remains environment-blocked and the release gate
remains RED.
