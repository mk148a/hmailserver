# Current State

- UTC timestamp: 2026-08-01T08:14:17Z
- Local timestamp: 2026-08-01T11:14:17+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `a33a8032e`
- Last successfully pushed commit: `79f749abc` (latest code commit is local and pending push)
- Latest focused-test result: Restore integrity/planner filter `16/16` passed
- Latest full Net10 result: `1384 passed, 3 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: `0/3/0` in the full run; SQL failure-path tests and native registry integration are skipped by opt-in gates
- Current bounded slice: Zero-mutation restore dry-run planner complete; next is semantic restore XML/graph validation
- Completed milestones: Backup metadata parity through accounts, fetch accounts, rules, folders, DB-only messages, compressed message staging, and raw message staging
- Open production blockers: Restore/round-trip/rollback are incomplete; SEC-18 cutover is not independently GREEN; COM/Admin parity matrix, upgrade rollback, protocol acceptance, performance/soak, and release-artifact gates remain open
- Environment-blocked work: Disposable SQL/native registry integration and PHP runtime-dependent checks are opt-in/skipped in the current environment; no production resource may be used
- Protected/do-not-touch areas: `AGENTS.md` dirty changes; untracked `artifacts/sec18-staging/`; untracked generated `hmailserver/source/Server.Net10.zip`; production service, database, Data directory, WebAdmin, installed Application COM identity, DCOM ACLs, registry, firewall, and public ports
- Next three independent slices: Semantic restore XML/graph validation; isolated backup option-matrix and cleanup acceptance coverage; isolated restore round-trip planning with rollback checkpoints
