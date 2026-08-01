# Current State

- UTC timestamp: 2026-08-01T11:09:36Z
- Local timestamp: 2026-08-01T14:09:36+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `1c3a67f48`
- Last successfully pushed commit: `a0daf37d1` (latest code commit is local and pending docs commit/push)
- Latest focused-test result: BackupRestoreContainmentPreflight filter `9/9` passed
- Latest full Net10 result: `1463 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Execution-time restore containment revalidation complete in `1c3a67f48`; next is an internal restore execution gate/lock contract
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, raw message staging, message-only modes, complete option-matrix acceptance coverage, bounded event callback ordering/authorization coverage, restore metadata consistency validation, domain/account graph validation, domain child-container validation, direct account child-container validation, FetchAccountUIDs graph validation, RuleCriterias/RuleActions graph validation, folder message/subfolder graph validation, restore dry-run missing-section planning, folder scalar validation, writer folder snapshot validation, read-only restore containment preflight, bounded/cancellable containment traversal, and execution-time containment revalidation
- Open production blockers: Backup script-event adapter is not production-wired; actual backup execution remains `E_NOTIMPL`; restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: internal restore execution gate/lock contract; restore semantic identity/foreign-key plan validation; disposable SQL restore transaction planning
