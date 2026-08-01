# Current State

- UTC timestamp: 2026-08-01T11:38:33Z
- Local timestamp: 2026-08-01T14:38:33+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `22555c2e1`
- Last successfully pushed commit: `d704937f9` (latest code/test commit is local and pending docs commit/push)
- Latest focused-test result: BackupRestoreIntegrityRuntime filter `74/74` passed
- Latest full Net10 result: `1473 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: One-pass SQL-compatible Domain/Account identity uniqueness validation completed in `22555c2e1`; next is foreign-key/parent-reference plan validation
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, raw message staging, message-only modes, complete option-matrix acceptance coverage, bounded event callback ordering/authorization coverage, restore metadata consistency validation, domain/account graph validation, domain child-container validation, direct account child-container validation, FetchAccountUIDs graph validation, RuleCriterias/RuleActions graph validation, folder message/subfolder graph validation, restore dry-run missing-section planning, folder scalar validation, writer folder snapshot validation, read-only restore containment preflight, bounded/cancellable containment traversal, execution-time containment revalidation, restore execution gate contract, read-only Domain/Account identity uniqueness validation, and one-pass identity scanning with trailing-space coverage
- Open production blockers: Backup script-event adapter is not production-wired; actual backup execution remains `E_NOTIMPL`; restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: read-only restore foreign-key/parent-reference plan validation; disposable SQL restore transaction planning; isolated backup/restore round-trip acceptance harness
