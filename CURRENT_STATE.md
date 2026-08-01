# Current State

- UTC timestamp: 2026-08-01T13:05:00Z
- Local timestamp: 2026-08-01T16:05:00+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `149dcf027`
- Last successfully pushed commit: `a512891ba` (latest code commit is local and pending push)
- Latest focused-test result: BackupRestoreIntegrityRuntime/DryRunPlanner filter `71/71` passed
- Latest full Net10 result: `1447 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Read-only restore structural/dry-run planning complete in `149dcf027`; next is folder scalar presence validation
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, raw message staging, message-only modes, complete option-matrix acceptance coverage, bounded event callback ordering/authorization coverage, restore metadata consistency validation, domain/account graph validation, domain child-container validation, direct account child-container validation, FetchAccountUIDs graph validation, RuleCriterias/RuleActions graph validation, folder message/subfolder graph validation, and restore dry-run missing-section planning
- Open production blockers: Backup script-event adapter is not production-wired; actual backup execution remains `E_NOTIMPL`; restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: folder scalar presence validation; writer orphan/cycle/duplicate snapshot validation; isolated restore plan containment/rollback preflight
