# Current State
- UTC/local timestamp: 2026-08-11T10:02:25Z / 2026-08-11T13:02:25+03:00
- Branch/upstream: `net10-modernization` -> `origin/net10-modernization`
- Current HEAD: `884100918` (code/test `e3434d4b1` plus documentation; push intentionally not performed)
- Last successfully pushed commit: `9d4b3791e`
- Latest focused-test result: `SqlServerSettingsAdministrationStore` filter `33 passed, 0 skipped, 0 failed`; SMTP/Settings greeting and authorization coverage `136 passed, 0 failed`
- Latest full Net10 result: default `2123 passed, 40 skipped, 0 failed`; fresh opt-in MSSQL disposable run `2161 passed, 2 skipped, 0 failed`
- Opt-in tests passed/skipped/blocked: SQL/Data restore and SQL/Admin integration passed against the configured local isolated-create path; installer artifact and native registry integration skipped by explicit opt-in gate; approved disposable SQL identity, SQL FTS, legacy C++ listeners, out-of-process COM, SEC-18 cutover, AD/DC, and 24-hour soak remain blocked or unproven
- Current bounded slice: completed legacy-first `WelcomeSMTP` SQL capacity parity; `@WelcomeSMTP` uses `nvarchar(4000)` metadata and a 300-character disposable SQL round trip is exact
- Completed milestones: raw backup staging, restore transaction foundations, FetchAccount/UID, Rules/Criteria/Actions, folder/message metadata, raw message-file acceptance, failed-commit rollback, settings restore parsing/store/execution, combined settings/domain DB-only restore, disposable SQL/Data start-state equivalence, and SQL/Admin opt-in integration; no release milestone complete
- Open production blockers: paired C++/Net10 protocol completion and all performance claims, live SMTP/POP3/IMAP policy reload beyond greeting, delivery batching/default parity, populated restore round-trip and rollback, non-DB settings reinitialize, SQL/FTS backfill, credential policy, ACL restore, SEC-18 cutover, migration/installer, service/out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, remaining COM/Admin parity, approved disposable SQL identity, and release-policy decision for the intentional legacy-vs-Net10 WelcomeSMTP CR/LF behavior
- Environment blocked work: healthy isolated C++ listener binary and stable Net10 POP3/IMAP live path, independently approved disposable SQL/LocalDB target, SQL Server with FTS and supported legacy ADO provider, isolated IIS/COM cutover, migration VM, domain-controller credentials, and long-running soak host
- Protected/do-not-touch areas: production service/SQL/Data, installed Application COM identity/registration/DCOM ACLs, production IIS, dirty `AGENTS.md` and backup WIP files, and untracked SEC18/benchmark/disposable artifacts
- Next three independent slices: (1) repair or replace the isolated C++ binary and fix/reproduce Net10 live IMAP/POP3 before rerunning the identical matrix, (2) populated disposable settings/message restore plus rollback acceptance, (3) bounded non-DB-only BODomains|BOMessages DataBackup staging and archive equivalence

## Current Audit Note (2026-08-11, WELCOMESMTP SQL CAPACITY PARITY)

Code/test commit `e3434d4b1` changes only the Net10 `WelcomeSMTP` SQL
parameter metadata from `nvarchar(255)` to `nvarchar(4000)`, matching the
legacy `hm_settings.settingstring nvarchar(4000)` schema. Legacy references
are `CreateTablesMSSQL.sql:299-303`,
`InterfaceSettings::put_WelcomeSMTP` (`source/Server/COM/InterfaceSettings.cpp:696-710`),
`Property::SetString`/`SQLStatement`/`ADOConnection` long-string handling
(`source/Server/DBOperation/Property.cpp:43-47,81-96`,
`source/Server/DBOperation/SQLStatement.cpp:40-67,222-257`,
`source/Server/DBOperation/ADOConnection.cpp:449-499`).

`SqlServerSettingsAdministrationStoreWelcomeSmtpIntegrationTests` creates a
random database on the configured local SQL endpoint, writes a 300-character
value, reads it back exactly, and drops the database. The focused store filter
is `33 passed`; full default Net10 is `2123 passed, 40 skipped, 0 failed`;
fresh isolated-create MSSQL/Data opt-in is `2161 passed, 2 skipped, 0 failed`.
The test targets no named hMailServer production database or Data directory,
but independent proof that the SQL instance itself is disposable remains
required. The paired live C++/.NET10 performance gate remains
**RED** because the identical SMTP/IMAP/POP3 workload still does not complete.
Next slice: populated disposable settings/message restore plus rollback
acceptance. Legacy C++ still accepts raw multiline `WelcomeSMTP`; the separate
.NET10 CR/LF hardening remains an intentional release-policy divergence.

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
