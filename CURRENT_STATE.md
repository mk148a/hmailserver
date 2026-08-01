# Current State

- UTC timestamp: 2026-08-01T12:19:26Z
- Local timestamp: 2026-08-01T15:19:26+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `f2d1502ce7001f1f46c1c1f8cc40b83511f0fa96`
- Last successfully pushed commit: `dbed77e55fe162bfd81c973582eebc9017f497f9`
- Latest focused-test result: WindowsScriptRuleExecutor and BackupManagerComContract filters `78/78` passed
- Latest full Net10 result: `1483 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Backup ScriptServer event dispatch completed in `f2d1502ce`; next is the offline synthetic 100k-message SEARCH/SORT benchmark because the approved disposable SQL restore harness is environment-blocked
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, raw message staging, message-only modes, complete option-matrix acceptance coverage, production-wired backup event callbacks, restore metadata consistency validation, domain/account graph validation, domain child-container validation, direct account child-container validation, FetchAccountUIDs graph validation, RuleCriterias/RuleActions graph validation, folder message/subfolder graph validation, restore dry-run missing-section planning, folder scalar validation, writer folder snapshot validation, read-only restore containment preflight, bounded/cancellable containment traversal, execution-time containment revalidation, restore execution gate contract, read-only Domain/Account identity uniqueness validation, one-pass identity scanning with trailing-space coverage, parent-scoped folder identity validation, explicit SQL/filesystem mutation safety requirements in the dry-run plan, and rollback-preserving transaction boundary orchestration
- Open production blockers: Restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open; the backup manager's no-runtime fallback remains `E_NOTIMPL` outside the production composition
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: offline synthetic 100k-message SEARCH/SORT benchmark pack; isolated disposable SQL restore transaction harness/wiring when approved; isolated backup/restore round-trip acceptance harness
