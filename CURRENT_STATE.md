# Current State

- UTC timestamp: 2026-08-01T10:51:13Z
- Local timestamp: 2026-08-01T13:51:13+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `6227cc267`
- Last successfully pushed commit: `3f9d501a1` (latest code commit is local and pending docs commit/push)
- Latest focused-test result: BackupRestoreContainmentPreflight/BackupRestoreDryRunPlanner/BackupRestoreIntegrityRuntime filter `82/82` passed
- Latest full Net10 result: `1461 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Read-only restore containment preflight complete in `6227cc267`; next is bounded/cancellable traversal and TOCTOU execution-lock design
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, raw message staging, message-only modes, complete option-matrix acceptance coverage, bounded event callback ordering/authorization coverage, restore metadata consistency validation, domain/account graph validation, domain child-container validation, direct account child-container validation, FetchAccountUIDs graph validation, RuleCriterias/RuleActions graph validation, folder message/subfolder graph validation, restore dry-run missing-section planning, folder scalar validation, writer folder snapshot validation, and read-only restore containment preflight
- Open production blockers: Backup script-event adapter is not production-wired; actual backup execution remains `E_NOTIMPL`; restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: bound/cancel containment traversal and specify TOCTOU execution revalidation; restore semantic identity/foreign-key plan validation; disposable SQL restore transaction planning
