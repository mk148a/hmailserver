# Current State

- UTC timestamp: 2026-08-01T11:05:00Z
- Local timestamp: 2026-08-01T14:05:00+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `0e6df4f65`
- Last successfully pushed commit: `86a866380` (latest code commit is local and pending push)
- Latest focused-test result: BackupRestoreIntegrityRuntime/DryRunPlanner filter `49/49` passed
- Latest full Net10 result: `1425 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Read-only direct Account child-container graph validation complete in `0e6df4f65`; next is nested FetchAccountUIDs graph validation
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, raw message staging, message-only modes, complete option-matrix acceptance coverage, bounded event callback ordering/authorization coverage, restore metadata consistency validation, domain/account graph validation, domain child-container validation, and direct account child-container validation
- Open production blockers: Backup script-event adapter is not production-wired; actual backup execution remains `E_NOTIMPL`; restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: nested FetchAccountUIDs graph validation; nested RuleCriterias/RuleActions graph validation; folder message/subfolder graph validation
