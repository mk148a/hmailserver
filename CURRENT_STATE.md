# Current State
- UTC/local timestamp: 2026-08-11T09:38:03Z / 2026-08-11T12:38:03+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `b56bfb541` (code/test `a414c88db` plus documentation; push intentionally not performed)
- Last successfully pushed commit: `9d4b3791e`
- Latest focused-test result: SMTP/Settings greeting and authorization coverage `136 passed, 0 failed`; benchmark PowerShell parsers and embedded IMAP C# compile passed; live artifacts validated as explicit FAIL paths
- Latest full Net10 result: default `2123 passed, 39 skipped, 0 failed`; fresh opt-in MSSQL disposable run `2160 passed, 2 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: SQL/Data restore and SQL/Admin integration passed; installer artifact and native registry integration skipped by explicit opt-in gate; SQL FTS, legacy C++ listeners, out-of-process COM, SEC-18 cutover, AD/DC, and 24-hour soak remain blocked or unproven
- Current bounded slice: completed legacy-first `WelcomeSMTP` framing hardening; authenticated setters reject CR/LF with `E_INVALIDARG`, and runtime greeting formatting fails safe for pre-existing unsafe rows
- Completed milestones: raw backup staging, restore transaction foundations, FetchAccount/UID, Rules/Criteria/Actions, folder/message metadata, raw message-file acceptance, failed-commit rollback, settings restore parsing/store/execution, combined settings/domain DB-only restore, disposable SQL/Data start-state equivalence, and SQL/Admin opt-in integration; no release milestone complete
- Open production blockers: paired C++/Net10 protocol completion and all performance claims, live SMTP/POP3/IMAP policy reload beyond greeting, WelcomeSMTP SQL length parity (`nvarchar(255)` vs legacy `nvarchar(4000)`), delivery batching/default parity, populated restore round-trip and rollback, non-DB settings reinitialize, SQL/FTS backfill, credential policy, ACL restore, SEC-18 cutover, migration/installer, service/out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining COM/Admin parity
- Environment blocked work: healthy isolated C++ listener binary and stable Net10 POP3/IMAP live path, SQL Server with FTS and supported legacy ADO provider, isolated IIS/COM cutover, migration VM, domain-controller credentials, and long-running soak host
- Protected/do-not-touch areas: production service/SQL/Data, installed Application COM identity/registration/DCOM ACLs, production IIS, dirty `AGENTS.md` and backup WIP files, and untracked SEC18/benchmark/disposable artifacts
- Next three independent slices: (1) repair or replace the isolated C++ binary and fix/reproduce Net10 live IMAP/POP3 before rerunning the identical matrix, (2) legacy-first WelcomeSMTP SQL length and long-value parity, (3) populated disposable settings/message restore plus rollback acceptance

## Current Audit Note (2026-08-11, WELCOMESMTP CRLF HARDENING)

Code/test commit `a414c88db` rejects CR/LF in authenticated `WelcomeSMTP`
setters with `E_INVALIDARG` before SQL mutation or runtime publication, and
falls back to a single safe server-name greeting if a pre-existing unsafe
database value reaches `SmtpSession.GetGreeting`.

Legacy references are `InterfaceSettings::put_WelcomeSMTP`
(`source/Server/COM/InterfaceSettings.cpp:696-710`),
`SMTPConfiguration::SetWelcomeMessage`
(`source/Server/SMTP/SMTPConfiguration.cpp:120-123`),
`SMTPConnection::SendBanner_` (`source/Server/SMTP/SMTPConnection.cpp:167-185`),
and `EnqueueWrite_` (`source/Server/SMTP/SMTPConnection.cpp:1548-1561`).
The installed BSTR/DISPID 23 contract is unchanged. Valid empty, custom, and
`ESMTP`-suffixed greetings retain legacy formatting.

Focused SMTP/Settings coverage is `136 passed`; full default Net10 is
`2123 passed, 39 skipped, 0 failed`; fresh disposable MSSQL/Data opt-in is
`2160 passed, 2 skipped, 0 failed`. The paired live performance gate remains
**RED** because the isolated C++ listener and Net10 live IMAP/POP3 paths do
not complete the identical workload matrix. Legacy C++ still accepts raw
multiline values; this intentional cross-version security difference remains
a release review item.

## Current Audit Note (2026-08-11, BOOTSTRAP SMTP GREETING PUBLICATION)

Code/test commit `7a7e4b77b` closes the startup gap where Net10 could expose an
empty SMTP greeting until COM `Application.Settings` was first accessed.
Legacy `Application::InitInstance` and `Configuration::Load` load persisted
properties before `StartServers` (`source/Server/Common/Application/Application.cpp:108`,
`source/Server/Common/Application/Configuration.cpp:56`), and the live banner
reads `SMTPConfiguration::GetWelcomeMessage` in
`source/Server/SMTP/SMTPConnection.cpp:167`.

Net10 now loads the configured settings snapshot in
`SettingsAdministrationRuntimeHost.Configure` and publishes `WelcomeSmtp`
before the host is used. `SettingsComContractTests` covers startup without COM
Settings access; affected retained-object read-count assertions were updated
to account for the additional bootstrap snapshot read. Focused coverage is
`158 passed`; full default Net10 is `2120 passed, 39 skipped, 0 failed`.

This slice does not change installed COM identity, direct activation boundaries,
SMTP trust, live policy reload, or delivery behavior. The paired C++/.NET10
performance gate remains **RED** because the isolated C++ listener target and
Net10 live IMAP/POP3 path do not yet complete the identical workload matrix.
Separate security follow-up: administrator-controlled `WelcomeSMTP` is not yet
CR/LF-sanitized before SMTP framing; this was intentionally left out of the
legacy-parity slice and requires its own legacy-anchored review.
