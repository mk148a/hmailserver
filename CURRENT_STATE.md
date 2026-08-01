# Current State

- UTC timestamp: 2026-08-01T07:37:15Z
- Local timestamp: 2026-08-01T10:37:15+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `6e30c5149`
- Last successfully pushed commit: `6e30c5149` (verified equal to upstream)
- Latest focused-test result: Backup filter `78/78` passed
- Latest full Net10 result: `1368 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Read-only restore dry-run and archive/DataFiles integrity validation for supported 7z and Raw `DataFiles` payloads; mutation remains fenced
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, and raw message staging
- Open production blockers: Restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: Read-only restore archive/DataFiles structural validation; isolated backup matrix/cleanup acceptance coverage; restore semantic dry-run validation with bounded XML and path-containment checks
