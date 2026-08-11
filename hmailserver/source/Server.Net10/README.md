## Current authoritative continuation (2026-08-11, SMTP relayer password persistence)

Code/test commit `b518c8e83` implements authenticated Administrator
`Settings.SetSMTPRelayerPassword` persistence. Legacy references are
`InterfaceSettings::SetSMTPRelayerPassword`,
`SMTPConfiguration::SetSMTPRelayerPassword`, `PropertySet::SetString`, and
`Property::WriteStringSetting_`
(`source/Server/Common/InterfaceSettings.cpp:998-1012`,
`source/Server/SMTP/SMTPConfiguration.cpp:273-281`,
`source/Server/Common/PropertySet.cpp:153-159`,
`source/Server/Common/Property.cpp:81-96`). Net10 preserves the installed
COM identity, authenticates and leases the Settings mutation, encrypts before
the parameterized `nvarchar(4000)` update, and keeps the password out of
snapshots/backups. Legacy zero-row update `S_OK` behavior is preserved.

Focused coverage is `146/146`; full Net10 is `2159 passed, 54 skipped, 0
failed`. This historical continuation predates the relayer and outbound TLS
runtime slices documented above. The fixed-key compatibility cipher and
missing real SQL/COM evidence keep the release gate RED.

## Current paired performance gate (2026-08-11)

The disposable C++/.NET 10 start-state fixture is validated with equal SQL
row counts and equal 1,000-file Data SHA-256. The fresh C++ read-only preflight
still refuses launch because Registry32 resolves the installed hMailServer
path `C:\hMailServer57-Test\Bin` instead of the disposable target
`C:\hmail-perf-cpp-ascii-20260810\Bin`. No C++ process or machine state was
changed. Net10-only SMTP/IMAP/POP3, 1,000-concurrent IMAP, FTS, queue, POP3
large-mailbox, external-fetch, and bounded soak artifacts are documented in
`PERFORMANCE_COMPARISON_REPORT.md`; no ratio or winner is valid. The gate is
RED pending a registry-isolated legacy binary or separate staging VM.

## Historical continuation (superseded)

The following global SMTP relayer runtime entry is retained as history.

## Current authoritative continuation (2026-08-11, global SMTP relayer runtime)

Code/test commit `a0fc76a99` connects the configured global SMTP relayer to
ordinary Net10 delivery. Legacy `ServerTargetResolver::Resolve` and
`GetFixedSMTPHostForDomain_` (`source/Server/SMTP/ServerTargetResolver.cpp:38-116,
170-237`) select forced route, matching domain route, global relayer, then MX.
Net10 now loads the existing `hm_settings` relayer host, auth flag, username,
port, connection security, and encrypted password in
`SqlServerDeliveryTargetResolver`; it preserves route precedence, maps port
`0` to `25`, decrypts credentials only for a non-empty authenticated username,
and fails closed for invalid relayer security or credential decryption. The
installed COM identity, route behavior, and SMTP listener were not changed.

Focused coverage is `19/19`; full Net10 is `2155 passed, 54 skipped, 0 failed`.
The current endpoint contract supports one relayer host; legacy `|`-separated
relayer failover remains open and is rejected explicitly rather than treated as
a single malformed host. Real SQL/socket/TLS/authentication evidence remains
environment-blocked. Next slice: authenticated `Settings.SetSMTPRelayerPassword`
persistence parity.

## Current authoritative continuation (2026-08-11, ordinary-MX SMTPConnectionSecurity)

Code/test commit `921f31064` carries persisted global
`SmtpDeliveryConnectionSecurity` into ordinary-MX delivery targets. Legacy
`ServerTargetResolver::Resolve` supplies the global setting to
`ExternalDelivery::DeliverToSingleServer_`; Net10 now maps the same values
`0..3` through `DeliveryTarget` and `RemoteSmtpEndpointResolver`. Route and
forced-route security/authentication remain independent, and invalid global
values fail closed.

Optional STARTTLS remains plaintext only for an unauthenticated endpoint when
STARTTLS is not advertised. Authenticated endpoints, TLS handshake failures,
required STARTTLS, and implicit SSL do not downgrade. Legacy optional-handshake
plaintext retry is intentionally not reproduced until security/product review.
Focused coverage is `21 passed`; full Net10 is `2147 passed, 54 skipped, 0
failed`. Real SQL/socket acceptance is environment-blocked. Next slice: the
approved disposable SQL-to-MX/socket matrix.

## Historical parity continuation (2026-08-11, SMTPConnectionSecurity SQL evidence harness)

Code/test commit `81b77ac35` adds the opt-in
`SqlServerSettingsAdministrationStoreSmtpConnectionSecurityIntegrationTests`
fixture. It requires an explicitly approved local SQL/LocalDB connection and
`HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1`, rejects
non-local sources and attached database files, creates a random database,
mutates and reads back all four `SmtpDeliveryConnectionSecurity` values,
checks missing-row `false`, and drops the database in `finally`. The focused
run skipped safely because the approval variables are absent; full Net10 is
`2133 passed, 54 skipped, 0 failed`. No live SQL PASS is claimed.

Next slice: legacy-first audit of global `SMTPConnectionSecurity` in ordinary
MX delivery. Keep `RemoteSmtpEndpointResolver`/TLS runtime wiring separate
from this test-only evidence slice.

## Historical parity continuation (2026-08-11, SMTPConnectionSecurity persistence)

Code/test commit `7b3373deb` implements authenticated
`Settings.SMTPConnectionSecurity` setter parity (`DispId(92)`). Legacy
`InterfaceSettings::put_SMTPConnectionSecurity`
(`source/Server/COM/InterfaceSettings.cpp:1799-1813`) calls
`SMTPConfiguration::SetSMTPConnectionSecurity`
(`source/Server/SMTP/SMTPConfiguration.cpp:175-184`), which writes the
existing `SmtpDeliveryConnectionSecurity` row seeded by
`source/DBScripts/CreateTablesMSSQL.sql:934`. Net10 now performs the same
parameterized fixed-row persistence, requires one affected row, rechecks the
server-admin boundary, retains failed state, and publishes the snapshot only
after success. No enum validation, live SMTP/TLS reconfiguration, or delivery
behavior change was added.

Focused Settings/SQL coverage is `142 passed`; full Net10 is `2133 passed, 53
skipped, 0 failed`. The paired C++ performance gate, service/out-of-process
COM, restore/rollback, migration/installer, SEC-18, AD/DC, and long-soak gates
remain open or environment-blocked. Next slice: fresh legacy-first audit of
one remaining fixed-row Settings mutation.

## Historical parity continuation (2026-08-11, MaxAsynchronousThreads persistence)

Code/test commit `18c3685c8` implements authenticated
`Settings.MaxAsynchronousThreads` setter parity. Legacy
`InterfaceSettings::put_MaxAsynchronousThreads`
(`source/Server/COM/InterfaceSettings.cpp:1578-1588`) calls
`Configuration::SetAsynchronousThreads`
(`source/Server/Common/Application/Configuration.cpp:569-578`), which writes
the existing `MaxNumberOfAsynchronousTasks` row from
`source/DBScripts/CreateTablesMSSQL.sql:918`. Net10 now performs the same
parameterized fixed-row persistence through
`SqlServerSettingsAdministrationStore`, requires exactly one affected row,
rechecks the server-administrator boundary, retains failed state, and publishes
the snapshot only after success. No live asynchronous-worker reconfiguration
was added.

Focused Settings/SQL coverage is `138 passed`; full Net10 is `2129 passed, 53
skipped, 0 failed`. The C++ paired performance gate, service/out-of-process
COM, restore/rollback, migration/installer, SEC-18, AD/DC, and long soak gates
remain open or environment-blocked. Next slice: fresh legacy-first audit of one
remaining fixed-row Settings mutation.

## Historical parity continuation (2026-08-11, full settings/domain/message restore)

Commit `563cd0042` accepts legacy restore option `7`
(`BOSettings|BODomains|BOMessages`). Legacy `BackupExecuter::StartRestore`
restores domains/Data/messages before settings
(`source/Server/Common/Application/BackupExecuter.cpp:230-388`,
`source/Server/Common/Application/Configuration.cpp:716-760`). Net10 now uses
one SQL metadata transaction, stages Data, restores settings and populated
message metadata, cleans public folders, and preserves recovery evidence when
the SQL commit outcome is ambiguous.

Focused restore coverage is `19 passed`; opt-in restore integration is `17
passed`; fresh full Net10 isolated-create opt-in is `2163 passed, 2 skipped, 0
failed`. This remains **YELLOW** because the archive is hand-built and the SQL
endpoint is not independently certified disposable. Next slice: true isolated
`StartBackup -> LoadBackup` with populated existing state and message bytes.
Production backup execution, reinitialize, SEC-18, and paired performance stay
open.

## Current parity continuation (2026-08-11, WelcomeSMTP SQL capacity parity)

Commit `e3434d4b1` changes only the Net10 `WelcomeSMTP` SQL parameter metadata
from `nvarchar(255)` to `nvarchar(4000)`, matching legacy
`hm_settings.settingstring nvarchar(4000)`
(`source/DBScripts/CreateTablesMSSQL.sql:299-303`). The disposable integration
test writes and reads a 300-character value exactly in a random database on the
configured local SQL endpoint and drops it. Focused store coverage is `33
passed`; full default Net10 is `2123 passed, 40 skipped, 0 failed`; fresh
isolated-create opt-in is `2161 passed, 2 skipped, 0 failed`. The SQL
instance's independent disposability remains an environment gate.

Performance remains **RED** because the identical C++/.NET10 SMTP/IMAP/POP3
workload is incomplete. Next slice: populated disposable settings/message
restore plus rollback acceptance. Legacy C++ still accepts raw multiline
WelcomeSMTP; the .NET10 CR/LF rejection remains an intentional release-policy
divergence.

## Current parity continuation (2026-08-11, WelcomeSMTP CR/LF hardening)

Code/test commit `a414c88db` rejects CR/LF in authenticated `WelcomeSMTP`
setters with `E_INVALIDARG` before SQL mutation/publication and provides a safe
fallback for pre-existing unsafe persisted values at `SmtpSession.GetGreeting`.
Valid legacy empty/custom/`ESMTP`-suffixed formatting is unchanged. Legacy
anchors are `InterfaceSettings::put_WelcomeSMTP`
(`source/Server/COM/InterfaceSettings.cpp:696-710`),
`SMTPConfiguration::SetWelcomeMessage`
(`source/Server/SMTP/SMTPConfiguration.cpp:120-123`), and
`SMTPConnection::SendBanner_` (`source/Server/SMTP/SMTPConnection.cpp:167-185`).
The installed BSTR/DISPID 23 contract and activation boundaries are unchanged.

Focused coverage is `136 passed`; full default Net10 is `2123 passed, 39
skipped, 0 failed`; fresh disposable opt-in is `2160 passed, 2 skipped, 0
failed`. Legacy C++ still accepts raw multiline values; the .NET10 rejection
is an intentional security divergence requiring release-policy acceptance.
Performance remains **RED** until the identical C++/.NET10 live
protocol workload completes. Next slice: repair or replace the isolated C++
protocol target and rerun the loopback matrix.

## Current parity continuation (2026-08-11, bootstrap SMTP greeting)

Code/test commit `7a7e4b77b` loads the configured settings snapshot during
`SettingsAdministrationRuntimeHost.Configure` and publishes persisted
`WelcomeSmtp` before SMTP listener use. This matches legacy
`Application::InitInstance` / `Configuration::Load` ordering and
`SMTPConnection::SendBanner_` plus `SMTPConfiguration::GetWelcomeMessage`
(`source/Server/Common/Application/Application.cpp:108`,
`source/Server/Common/Application/Configuration.cpp:56`,
`source/Server/SMTP/SMTPConnection.cpp:167-205`). Focused coverage is `158
passed`; full default Net10 is `2120 passed, 39 skipped, 0 failed`.

The performance release gate remains **RED**: the isolated C++ target does not
provide the required POP3 listener and Net10 live IMAP/POP3 probes do not
complete the paired workload. No speedup claim is valid. Next slice is repair
or replacement of the isolated protocol target followed by the identical
loopback matrix.

## Current benchmark continuation (2026-08-11)

Code/test commit `2fe577f62` hardens the live protocol benchmark harness with
listener readiness, launched-PID ownership, SMTP/IMAP/POP3 banner probes,
clean-shutdown verification, and a 1,000-session start barrier. The paired
gate remains **RED**: C++ has no staging POP3 listener on `127.0.0.1:25110`;
Net10 passes SMTP `25/25` but fails IMAP and POP3 `0/25`; the 1,000-session
Net10 run completes `1000` probes with `0` successes. No speed-up ratio is
valid. The default Net10 suite is `2119 passed, 39 skipped, 0 failed`; fresh
disposable SQL opt-in is `2156 passed, 2 skipped, 0 failed`.

## Current parity continuation (2026-08-11, MaxNumberOfInvalidCommands authorization lease)

Code/test commit `0abe45705` extends the existing generation-bound
authorization lease to authenticated
`IInterfaceSettings.MaxNumberOfInvalidCommands` (`DispId(65)`). The lease is
acquired immediately before the existing parameterized
`maximumincorrectcommands` SQL update and held through result handling and
retained snapshot publication. Focused settings/store coverage is `134/134`;
full unfiltered Net10 is `2117 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/COM/InterfaceSettings.cpp:1695-1727`,
`source/Server/Common/Application/Configuration.cpp:501-509`,
`source/Server/Common/Application/Constants.h:90`,
`source/Server/SMTP/SMTPConnection.cpp:2207-2221`,
`source/Server/hMailServer/hMailServer.idl:612-613`, and
`source/DBScripts/CreateTablesMSSQL.sql:866`. Legacy counts every 5xx
response and disconnects only when the count is greater than the configured
limit; Net10’s SQL-backed Settings mutation still does not reload the live
`SmtpSessionOptions` value. That runtime gap remains a separate slice. No COM
identity, direct activation boundary, SQL shape, or SMTP counting behavior
changed here.

The paired performance fixture remains diagnostic only: Net10 completed
1,000 concurrent IMAP sessions, C++ completed `0/1000`, so no ratio or
speed-up claim is valid and performance remains **RED**. Next slice: legacy-
first audit of live SMTP greeting/settings propagation.

## Current parity continuation (2026-08-11, DisconnectInvalidClients authorization lease)

Code/test commit `bb20cb736` extends the existing generation-bound
authorization lease to authenticated
`IInterfaceSettings.DisconnectInvalidClients` (`DispId(64)`, `VARIANT_BOOL`).
The lease is acquired immediately before the existing parameterized
`disconnectinvalidclients` SQL update and held through result handling and
retained snapshot publication. Focused settings/store coverage is `131/131`;
full unfiltered Net10 is `2114 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/COM/InterfaceSettings.cpp:1661-1693`,
`source/Server/Common/Application/Configuration.cpp:488-498`,
`source/Server/Common/Application/Constants.h:89`,
`source/Server/SMTP/SMTPConnection.cpp:2210-2220`,
`source/Server/hMailServer/hMailServer.idl:610-611`, and
`source/DBScripts/CreateTablesMSSQL.sql:862-866`. Legacy SMTP reads this
setting on each invalid 5xx response; Net10 Settings persistence still does
not reconfigure the live `SmtpSessionOptions` object. That runtime gap is a
separate slice. No COM identity, direct activation boundary, SQL shape, or
SMTP behavior changed here.

The existing paired benchmark fixture was revalidated without rerunning the
known-broken legacy process: offline Net10 SEARCH/SORT passed with p50
`7.272 ms`, p95 `7.994 ms`, p99 `8.048 ms`; the isolated 1,000-concurrent
IMAP artifacts validate as Net10 `1000/1000` and C++ `0/1000` with ratio
invalid. Performance release remains **RED**; no speed-up claim is valid.
Next slice: fresh legacy-first audit and lease coverage for
`Settings.MaxNumberOfInvalidCommands`.

## Current parity continuation (2026-08-11, authenticated maximum MX host count mutation)

Code/test commit `3ca025ce1` adds only authenticated
`IInterfaceSettings.MaxNumberOfMXHosts` (`DispId(90)`) persistence. The SQL
command is parameterized and fixed to `settingname = N'MaxNumberOfMXHosts'`; the
live administrator callback is checked before store access and the retained
snapshot changes only after a one-row success. Focused settings/SQL coverage is
`56/56`; full Net10 is `2039 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:650-651`,
`source/Server/COM/InterfaceSettings.cpp:2189-2214`,
`source/Server/SMTP/SMTPConfiguration.cpp:237-245`,
`source/Server/Common/Application/Constants.h:120`, and the
`MaxNumberOfMXHosts` SQL seed. No COM identity, direct activation boundary,
`ExternalDelivery`, MX-host enforcement, or runtime reconfiguration path
changed. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation.

## Current parity continuation (2026-08-11, authenticated SMTP retry count mutation)

Code/test commit `f8010374d` adds only authenticated
`IInterfaceSettings.SMTPNoOfTries` (`DispId(19)`) persistence. The SQL command
is parameterized and fixed to `settingname = N'smtpnoofretries'`; the live
administrator callback is checked before store access and the retained snapshot
changes only after a one-row success. Focused settings/SQL coverage is `53/53`;
full Net10 is `2036 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:541-542`,
`source/Server/COM/InterfaceSettings.cpp` (`put_SMTPNoOfTries`),
`source/Server/SMTP/SMTPConfiguration.cpp` (`SetNoOfRetries`),
`source/Server/Common/Application/Constants.h` (`PROPERTY_SMTPNOOFTRIES`),
and the canonical `smtpnoofretries` SQL seed. No COM identity, direct
activation boundary, `ExternalDelivery`, retry scheduling, or runtime
reconfiguration path changed. The typo row `smtpnooftries` remains excluded.
Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation.

## Current parity continuation (2026-08-11, authenticated SMTP retry interval mutation)

Code/test commit `b970bf00c` adds only authenticated
`IInterfaceSettings.SMTPMinutesBetweenTry` (`DispId(20)`) persistence. The SQL
command is parameterized and fixed to
`settingname = N'smtpminutesbetweenretries'`; the live administrator callback
is checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `51/51`; full Net10 is
`2034 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:543-544`,
`source/Server/COM/InterfaceSettings.cpp:500-535`,
`source/Server/SMTP/SMTPConfiguration.cpp:101-109`,
`source/Server/Common/Application/Constants.h:12`, and
`source/DBScripts/CreateTablesMSSQL.sql:744`. No COM identity, direct
activation boundary, `ExternalDelivery`, retry scheduling, or runtime
reconfiguration path changed. Next slice: fresh legacy-first audit of one
remaining low-risk Settings mutation.

## Current parity continuation (2026-08-11, authenticated incorrect-line-endings mutation)

Code/test commit `9a7687365` adds only authenticated
`IInterfaceSettings.AllowIncorrectLineEndings` (`DispId(61)`, `VARIANT_BOOL`)
persistence. The SQL command is parameterized and fixed to
`settingname = N'smtpallowincorrectlineendings'`; the live administrator
callback is checked before store access and the retained snapshot changes only
after a one-row success. Focused settings/SQL coverage is `49/49`; full Net10
is `2032 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:604`,
`source/Server/COM/InterfaceSettings.cpp:326`,
`source/Server/SMTP/SMTPConfiguration.cpp:288`,
`source/Server/Common/Application/Property.cpp:36-78`, and
`source/DBScripts/CreateTablesMSSQL.sql` for the seeded row. No COM identity,
direct activation boundary, SMTP protocol behavior, or runtime reconfiguration
path changed. Next slice: fresh legacy-first audit of one remaining low-risk
Settings mutation.

## Current parity continuation (2026-08-11, authenticated Delivered-To header mutation)

Code/test commit `279b18f70` adds only authenticated
`IInterfaceSettings.AddDeliveredToHeader` (`DispId(73)`, `VARIANT_BOOL`)
persistence. The SQL command is parameterized and fixed to
`settingname = N'adddeliveredtoheader'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `47/47`; full Net10 is
`2030 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:520`,
`source/Server/COM/InterfaceSettings.cpp:1833`,
`source/Server/SMTP/SMTPConfiguration.cpp:300`,
`source/Server/Common/Application/Constants.h:94`, and
`source/DBScripts/CreateTablesMSSQL.sql:874`. No COM identity, direct
activation boundary, `LocalDelivery::AddTraceHeaders_`, or runtime
reconfiguration path changed. Next slice: fresh legacy-first audit of one
remaining low-risk Settings mutation.

## Current parity continuation (2026-08-10, authenticated maximum message size mutation)

Code/test commit `69aa0c6d5` adds only authenticated
`IInterfaceSettings.MaxMessageSize` (`DispId(44)`) persistence. The SQL command
is parameterized and fixed to `settingname = N'maxmessagesize'`; the live
administrator callback is checked before store access and the retained snapshot
changes only after a one-row success. Focused settings/SQL coverage is `45/45`;
full Net10 is `2028 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:576-577`,
`source/Server/COM/InterfaceSettings.cpp:65-105`,
`source/Server/SMTP/SMTPConfiguration.cpp:199-207`, and
`source/DBScripts/CreateTablesMSSQL.sql:804`. No COM identity, direct
activation boundary, SMTP/IMAP protocol enforcement, KB conversion, or live
reconfiguration path changed. Next slice: fresh legacy-first audit of one
remaining low-risk Settings mutation.

## Current parity continuation (2026-08-10, authenticated disconnect-invalid-clients mutation)

Code/test commit `2ee01f107` adds only authenticated
`IInterfaceSettings.DisconnectInvalidClients` (`DispId(64)`, `VARIANT_BOOL`)
persistence. The SQL command is parameterized and fixed to
`settingname = N'disconnectinvalidclients'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `43/43`; full Net10 is
`2026 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:610-613`,
`source/Server/COM/InterfaceSettings.cpp:1661-1693`,
`source/Server/Common/Application/Configuration.cpp:488-498`,
`source/Server/Common/Application/Property.cpp:36-78`, and
`source/Server/Common/Application/Constants.h:89`. No COM identity, direct
activation boundary, SMTP behavior, or runtime invalid-command reconfiguration
path changed. Next slice: fresh legacy-first audit of one remaining low-risk
Settings mutation.

## Current parity continuation (2026-08-10, authenticated invalid-command limit mutation)

Code/test commit `9a7e418eb` adds only authenticated
`IInterfaceSettings.MaxNumberOfInvalidCommands` (`DispId(65)`) persistence. The
SQL command is parameterized and fixed to
`settingname = N'maximumincorrectcommands'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `41/41`; full Net10 is
`2024 passed, 39 skipped, 0 failed`.

Legacy references are `source/Server/hMailServer/hMailServer.idl:612-613`,
`source/Server/COM/InterfaceSettings.cpp:1695-1720`,
`source/Server/Common/Application/Configuration.cpp:501-509`,
`source/Server/Common/Application/Constants.h:90`, and
`source/Server/SMTP/SMTPConnection.cpp:2210-2219`. No COM identity, direct
activation boundary, SMTP behavior, or runtime threshold reconfiguration path
changed. Next slice: fresh legacy-first audit of one remaining low-risk
Settings mutation.

## Current parity continuation (2026-08-10, authenticated MaxSMTPRecipientsInBatch mutation)

Code/test commit `b4cacd531` adds only the fixed-row
`MaxSMTPRecipientsInBatch` mutation (`DispId(62)`) to the existing authenticated
settings seam. The SQL command is parameterized and fixed to
`settingname = N'maxsmtprecipientsinbatch'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `39/39`; full Net10 is
`2022 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.MaxSMTPRecipientsInBatch`
(`source/Server/hMailServer/hMailServer.idl:606-607`),
`InterfaceSettings::put_MaxSMTPRecipientsInBatch`
(`source/Server/COM/InterfaceSettings.cpp:1627-1658`),
`SMTPConfiguration::SetMaxSMTPRecipientsInBatch`
(`source/Server/SMTP/SMTPConfiguration.cpp:211-220`), and
`PROPERTY_MAXSMTPRECIPIENTSINBATCH` (`source/Server/Common/Application/Constants.h:74`).
No COM identity, direct activation boundary, delivery batching, or live
reconfiguration path changed.

## Current parity continuation (2026-08-10, authenticated WelcomeSMTP mutation)

Code/test commit `6408eb8bd` adds only the fixed-row `WelcomeSMTP` mutation
(`DispId(23)`, BSTR) to the existing authenticated settings seam. The SQL
command is parameterized and fixed to `settingname = N'welcomesmtp'`; the live
administrator callback is checked before store access and the retained
snapshot changes only after a one-row success. Focused settings/SQL coverage is
`37/37`; full Net10 is `2020 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.WelcomeSMTP` (`DispId(23)`),
`InterfaceSettings::put_WelcomeSMTP`, `SMTPConfiguration::SetWelcomeMessage`,
and `SMTPConnection::SendBanner_` in the legacy IDL, COM, SMTP, and common
application paths. No COM identity, direct activation boundary, SMTP greeting,
or live reconfiguration path changed.

## Current parity continuation (2026-08-10, authenticated WelcomeIMAP mutation)

Code/test commit `df7f72c22` adds only the fixed-row `WelcomeIMAP` mutation
(`DispId(25)`, BSTR) to the existing authenticated settings seam. The SQL
command is parameterized and fixed to `settingname = N'welcomeimap'`; the live
administrator callback is checked before store access and the retained
snapshot changes only after a one-row success. Focused settings/SQL coverage is
`35/35`; full Net10 is `2018 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.WelcomeIMAP` (`DispId(25)`),
`InterfaceSettings::put_WelcomeIMAP`, `IMAPConfiguration::SetWelcomeMessage`,
`PROPERTY_WELCOMEIMAP`, and `IMAPConnection::SendBanner_` in the legacy
`Server/COM`, `Server/IMAP`, `Server/Common/Application`, and IDL paths. No
COM identity, direct activation boundary, IMAP greeting, or live
reconfiguration path changed.

## Current parity continuation (2026-08-10, authenticated WelcomePOP3 mutation)

Code/test commit `67d383ef1` adds only the fixed-row `WelcomePOP3` mutation
(`DispId(24)`, BSTR) to the existing authenticated settings seam. The SQL
command is parameterized and fixed to `settingname = N'welcomepop3'`; the live
administrator callback is checked before store access and the retained
snapshot changes only after a one-row success. Focused settings/SQL coverage is
`33/33`; full Net10 is `2016 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.WelcomePOP3`
(`source/Server/hMailServer/hMailServer.idl:547-550`),
`InterfaceSettings::put_WelcomePOP3`
(`source/Server/COM/InterfaceSettings.cpp:713-745`),
`POP3Configuration::SetWelcomeMessage`
(`source/Server/POP3/POP3Configuration.cpp:24-53`), and
`PROPERTY_WELCOMEPOP3` (`source/Server/Common/Application/Constants.h:14`).
No COM identity, direct activation boundary, POP3 greeting, or live
reconfiguration path changed.

## Current parity continuation (2026-08-10, authenticated MaxPOP3Connections mutation)

Code/test commit `e11234d8a` adds only the fixed-row
`MaxPOP3Connections` mutation (`DispId(6)`) to the existing authenticated
settings seam. The SQL command is parameterized and fixed to
`settingname = N'maxpop3connections'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `31/31`; full Net10 is
`2014 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.MaxPOP3Connections`
(`source/Server/hMailServer/hMailServer.idl:531-532`),
`InterfaceSettings::put_MaxPOP3Connections`
(`source/Server/COM/InterfaceSettings.cpp:172-199`), and
`POP3Configuration::SetMaxPOP3Connections`
(`source/Server/POP3/POP3Configuration.cpp:31-39`). No COM identity, direct
activation boundary, POP3 trust, or live listener reconfiguration path
changed.

## Current parity continuation (2026-08-10, authenticated MaxSMTPConnections mutation)

Code/test commit `9d2033677` adds only the fixed-row
`MaxSMTPConnections` mutation (`DispId(5)`) to the existing authenticated
settings seam. The SQL command is parameterized and fixed to
`settingname = N'maxsmtpconnections'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `29/29`; full Net10 is
`2012 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.MaxSMTPConnections`
(`source/Server/hMailServer/hMailServer.idl:529`),
`InterfaceSettings::put_MaxSMTPConnections`
(`source/Server/COM/InterfaceSettings.cpp:124`),
`SMTPConfiguration::SetMaxSMTPConnections`
(`source/Server/SMTP/SMTPConfiguration.cpp:51`), and
`Property::WriteLongSetting_` (`source/Server/Common/Application/Property.cpp:71`).
No COM identity, direct activation boundary, SMTP trust, or live listener
reconfiguration path changed.

## Current parity continuation (2026-08-10, authenticated WorkerThreadPriority mutation)

Code/test commit `2e60909b5` adds only the fixed-row
`WorkerThreadPriority` mutation (`DispId(57)`) to the existing authenticated
settings seam. The SQL command is parameterized and fixed to
`settingname = N'workerthreadpriority'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `27/27`; full Net10 is
`2010 passed, 39 skipped, 0 failed`.

Legacy references are `IInterfaceSettings.WorkerThreadPriority`
(`source/Server/hMailServer/hMailServer.idl:599`),
`InterfaceSettings::put_WorkerThreadPriority`
(`source/Server/COM/InterfaceSettings.cpp:1496`),
`Configuration::SetWorkerThreadPriority`
(`source/Server/Common/Application/Configuration.cpp:130`), and
`PROPERTY_WORKERTHREADPRIORITY` (`source/Server/Common/Application/Constants.h:70`).
No COM identity, direct activation boundary, SMTP behavior, or reinitialize
path changed.

## Current parity continuation (2026-08-10, authenticated MirrorEMailAddress mutation)

Code/test commit `3ba1d5f49` adds only the fixed-row
`MirrorEMailAddress` mutation to the existing settings administration seam.
The SQL command is parameterized and fixed to
`settingname = N'mirroremailaddress'`; the live administrator callback is
checked before store access and the retained snapshot changes only after a
one-row success. Focused settings/SQL coverage is `25/25`; full Net10 is
`2008 passed, 39 skipped, 0 failed`.

Legacy references are `InterfaceSettings::put_MirrorEMailAddress`
(`source/Server/COM/InterfaceSettings.cpp:224-241`),
`Configuration::SetMirrorAddress`
(`source/Server/Common/Application/Configuration.cpp:242-248`), and
`PROPERTY_MIRROREMAILADDRESS` (`source/Server/Common/Application/Constants.h:6`).
No COM identity, direct activation boundary, SMTP behavior, or reinitialize
path changed.

## Current parity continuation (2026-08-10, authenticated DefaultDomain mutation)

Code/test commit `41b77dba1` wires only `Settings.DefaultDomain` (`DispId(50)`)
to a dedicated `ISettingsAdministrationMutationStore` seam. The SQL Server
implementation uses a parameterized update fixed to `settingname =
N'defaultdomain'`, requires one affected row, and updates the retained COM
snapshot only after success. The setter calls both `EnsureAuthorized()` and the
live `EnsureServerAdministrator()` callback before any store access.

Legacy references are `InterfaceSettings::put_DefaultDomain`
(`source/Server/COM/InterfaceSettings.cpp:1272-1297`),
`Configuration::SetDefaultDomain`
(`source/Server/Common/Application/Configuration.cpp:415-424`), and
`Property::WriteStringSetting_` (`source/Server/Common/Application/Property.cpp:44-97`).
Focused settings/SQL coverage is `23/23`; full Net10 is `2006 passed, 39
skipped, 0 failed`. No COM identity, direct activation boundary, SMTP trust,
reinitialize, or other Settings mutation changed.

## Current parity continuation (2026-08-10, bounded metadata extraction)

Code/test commit `d77fa9426` changes `SevenZipBackupArchiveMetadataReader` to
bound metadata stdout at the existing `BackupArchiveXmlSnapshotParser` 1 MiB
document limit before accumulating the complete string. Focused coverage is
`BackupManagerComContractTests` `28/28`; full Net10 is `2004 passed, 39 skipped,
0 failed`. No COM contract or restore behavior changed. The security boundary is anchored by the parser's
`XmlReaderSettings.MaxCharactersInDocument` and does not touch production
archives or databases.

The live C++/.NET benchmark evidence remains diagnostic only: equal message
file corpus is proven, but C++ IMAP and POP3 did not complete the same matrix
and the concurrent pair is invalid. No performance winner or ratio may be
claimed. Release and performance gates remain RED.

## Current parity continuation (2026-08-10, combined settings/domain restore)

Code/test commit `a8f55de14` extends DB-only restore to
`RestoreSettings|RestoreDomains` when both sections are present. Domain
metadata is applied before ordered settings in the same transaction; the
SMTP relayer credential property is rejected and settings failure disposes
before commit. Legacy anchors are `BackupExecuter::Restore`
(`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:274-335`)
and `Configuration::XMLLoad`
(`hmailserver/source/Server/Common/Application/Configuration.cpp:716-758`).
Focused coverage is `19/19`; full default Net10 is `2002 passed, 39 skipped,
0 failed`. Reinitialize, non-DB combined restore, real SQL/Data rollback,
credential round-trip, and release gates remain open and RED.

## Current parity continuation (2026-08-10, settings-only restore execution)

Code/test commit `a389b0a95` wires parsed settings into a settings-only,
transaction-scoped DB restore. It requires the settings section and SQL
transaction, rejects `smtprelayerpassword` restore input, preserves property
order, and disposes before commit on failure. Legacy anchors are
`BackupExecuter::Restore` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:274-335`)
and `Configuration::XMLLoad` (`hmailserver/source/Server/Common/Application/Configuration.cpp:716-758`).
Focused execution coverage is `17/17`; full default Net10 is `2000 passed,
39 skipped, 0 failed`. Combined settings+domains restore, live
reconfiguration/reinitialize, credential round-trip policy, and disposable
SQL/Data evidence remain open and RED. Next slice: combined settings+domains
DB-only ordering and rollback.

## Current parity continuation (2026-08-10, transactional settings restore boundary)

Code/test commit `9dd56fa60` adds the transaction-scoped
`ISettingsRestoreAdministrationStore` and SQL Server update-only implementation.
Each property is applied with parameters to an existing `hm_settings` row; no
insert/delete/drop path is present. The restore executor does not call this
store yet, preserving restore flags, live settings, and COM behavior. Focused
coverage is `9/9`; full default Net10 is `1998 passed, 39 skipped, 0 failed`.
Actual disposable SQL/Data execution, rollback evidence, credential policy,
and executor wiring remain open and RED. Next slice: wire parsed settings into
the existing transactional DB-only restore path without live reconfiguration.

## Current parity continuation (2026-08-10, settings restore parsing)

Code/test commit `9b6544736` adds parser-only settings restore coverage. The
archive parser reads root `Properties` children into ordered
`BackupSettingsPropertySnapshot` values without SQL/runtime/COM mutation.
Legacy anchors are `PropertySet::XMLLoad`
(`hmailserver/source/Server/Common/Application/PropertySet.cpp:184-213`)
and `Configuration::XMLLoad`
(`hmailserver/source/Server/Common/Application/Configuration.cpp:716-758`).
The parser preserves child order, defaults missing or invalid `LongValue` to
zero and missing `StringValue` to empty, and leaves mutation out of scope.
Focused coverage is `15/15`; full default Net10 is `1997 passed, 39 skipped,
0 failed`. Settings SQL mutation/rollback, runtime reinitialization, and
release acceptance remain open and RED.

## Current Authoritative Audit (2026-08-10, recipient/search backlog correction)

Legacy `Message::XMLStore` writes only scalar message attributes
(`source/Server/Common/BO/Message.cpp:200-218`); recipients are runtime rows
loaded by `PersistentMessage::ReadRecipients_`
(`source/Server/Common/Persistence/PersistentMessage.cpp:231-267`), while
derived `hm_message_metadata` is selected for rebuild by
`PersistentMessageMetaData::GetMessagesToIndex`
(`source/Server/Common/Persistence/PersistentMessageMetaData.cpp:30-74`).
The .NET `MessageSearchBackfillProcessor` already owns missing-index lease,
upsert, and failure marking. Therefore recipient/search “restore” is stale as
an XML/schema slice and remains an environment-gated live SQL/backfill
acceptance item.

## Current Authoritative Continuation (2026-08-10, partial message rollback acceptance)

Test commit `02c221769` adds coverage for a successful first message followed
by a failing second message insert. The required rollback is bounded to the
restore root: prior message rows, folders, staged raw files, the journal, and
the rollback artifact must be absent after the original data directory is
restored. Full default Net10 is `1994 passed, 39 skipped, 0 failed`; SQL/Data
execution remains blocked without the approved disposable target.

## Current Authoritative Continuation (2026-08-10, message failure rollback)

Code/test commit `f144fbf86` records each restored root folder before its
messages are inserted. This makes non-DB compensating rollback cover the root
folder tree when the first `InsertMessageForRestoreAsync` fails. Legacy
references are `BackupExecuter::RestoreDataDirectory_`
(`source/Server/Common/Application/BackupExecuter.cpp:339-388`) and
`Collection::XMLLoad` (`source/Server/Common/BO/Collection.h:85-135`). The
focused writer suite passes `3/3`; default full Net10 passes `1994/38/0`.
The destructive SQL/Data executor test is still skipped until an approved
disposable target is configured; release remains RED.

## Current Authoritative Continuation (2026-08-10, raw message-file restore acceptance)

Test commit `84ca67ee4` proves the executor path with a disposable external
raw DataBackup graph under `DataBackup/<domain>/<account>/<guid-bucket>/`.
The restore stages the message file, inserts metadata, generates a new message
ID, and reads back the archived UID. Default full Net10 is `1993 passed, 37
skipped, 0 failed`. Recipients, search metadata, ACLs, crash-safe rollback,
and release gates remain open; release is RED.

## Historical Continuation (2026-08-10, folder message metadata)

Code/test commit `1b89ae4b8` implements folder-scoped delivered message
metadata restore. Legacy anchors are `Message::XMLLoad`,
`PersistentMessage::SaveObject`/`AddObject`, `Messages::PreSaveObject`, and
`IMAPFolder::XMLLoadSubItems`. The .NET path parses the nine legacy message
attributes, remaps account/folder ownership, generates `messageid`, preserves
nonzero UID, uses `1901-01-01`/zero retry and lock defaults, and does not
advance `foldercurrentuid`. SQL identity and UID readback pass in an isolated
database. Recipients, search tables, ACLs, and physical `.eml` acceptance are
out of scope. Default full Net10 is `1992 passed, 37 skipped, 0 failed`;
release remains RED.

## Historical Continuation (2026-08-10, restore commit rollback)

Code/test commit `915b78a4a` makes `SqlServerBackupRestoreMetadataTransaction.DisposeAsync`
attempt rollback whenever a SQL restore transaction is not committed, including
after `CommitAsync` has started and failed. It suppresses only a provider
rollback error that follows a failed commit so the original failure remains
observable. Focused restore/transaction coverage is `12 passed, 0 failed, 0
skipped`; default full Net10 is `1992 passed, 37 skipped, 0 failed`. An
injected provider-level commit-failure test and crash/power-loss recovery are
still open; release remains RED.

## Historical Continuation (2026-08-10, folder metadata restore)

Code/test commit `5b457d513` completes the bounded folder-metadata restore
slice. Legacy anchors are `Account::XMLStore`/`Account::XMLLoadSubItems`,
`IMAPFolder::XMLStore`/`IMAPFolder::XMLLoadSubItems`,
`PersistentIMAPFolder::SaveObject`, and `IMAPFolders::PreSaveObject` under
`hmailserver/source/Server/Common`. `BackupArchiveXmlSnapshotParser` parses
recursive folders, `BackupRestoreMetadataWriter.RestoreFoldersAsync` inserts
parents before children, and `SqlServerImapFolderAdministrationStore` keeps
the archived `CurrentUID` and creation time. Folder `Messages` and
`Permissions` payloads are rejected until their bounded restore slices exist.

Focused parser plus isolated SQL round-trip/rollback coverage is `25 passed,
0 failed, 0 skipped`; default full Net10 is `1992 passed, 37 skipped, 0
failed`. SQL opt-in full execution is `2021 passed, 2 skipped`, with six
unrelated existing message/indexing fixture failures. Release remains RED.
The next independent priorities are reproducible legacy C++ IMAP/POP3
startup, populated message/settings restore, and paired SMTP/delivery work.

## Historical Continuation (2026-08-10, FetchAccount Restore)

Code/test commit `7e8d71c15` adds restore-side FetchAccount and nested FetchAccountUID persistence. Legacy anchors are `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`), `FetchAccount::XMLStore` (`FetchAccount.cpp:55-79`), `FetchAccountUID::XMLStore` (`FetchAccountUID.cpp:42-49`), and owner-scoped refresh in `FetchAccounts.cpp:36-43`/`FetchAccountUIDs.cpp:29-50`. Current anchors are `BackupArchiveXmlSnapshotParser.ParseFetchAccount`, `BackupRestoreMetadataWriter.RestoreFetchAccountsAsync`, `MetadataBackupRestoreExecutor.RestoreMetadataAsync`, `IBackupRestoreMetadataTransaction.FetchAccountStore`, and `SqlServerFetchAccountAdministrationStore`.

Focused parser/SQL/restore coverage is `30/30`; disposable LocalDB FetchAccount readback and transaction rollback is `2/2`; default full Net10 is `1990 passed, 35 skipped, 0 failed`. SQL-enabled full Net10 is `2017 passed, 2 skipped, 6 unrelated existing message/indexing fixture failures`. The slice preserves legacy Blowfish ciphertext, transaction rollback, COM identity, authenticated boundaries, SMTP trust, and production isolation. Release remains RED for live paired performance, populated full restore/round-trip, SEC-18, migration/installer, service/out-of-process COM, AD/DC, protocol/load, crash/power-loss, and soak gates.

Test commit `17ba6e70a` extends `BackupRestoreRoundTripIntegrationTests` with populated FetchAccount/UID executor readback and invalid UID-date rollback. The focused disposable restore class passes `12/12`; default full Net10 is `1990 passed, 36 skipped, 0 failed`. This remains bounded restore evidence; full settings/folders/messages restore, crash recovery, and release gates remain open.

## Current Continuation (2026-08-10, SQL-backed Account.ValidatePassword)

The current code/test slice is `f34ee25c8`: attached COM `Account.ValidatePassword` now reaches the configured SQL administration store and preserves the legacy script-first, empty-password, AD, and local-hash verification order. Legacy anchors are `InterfaceAccount::ValidatePassword` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:350-364`) and `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:109-188`); COM identity, DISPIDs, attachment checks, and direct activation boundaries were not changed.

An explicit disposable LocalDB harness is available in `build/prepare-net10-disposable-localdb.ps1`, with per-process opt-in and guarded cleanup in `build/remove-net10-disposable-localdb.ps1`. The verifier-focused run is `70 passed, 0 failed, 0 skipped` (`artifacts/net10-disposable/SqlServerAccountPasswordVerifier.trx` contains the four SQL-backed tests). The full Net10 run is `2009 passed, 2 skipped, 9 failed`; the failures are existing isolated SQL fixture/schema mismatches in restore, folder, message-indexing, settings-cache, and message persistence tests, not this verifier slice. The durable run is recorded in `artifacts/net10-disposable/FullNet10-20260810.trx`.

The lower continuation entries in this file are historical. Release status remains RED: populated SQL/Data restore and rollback, SEC-18 cutover, migration/installer, out-of-process COM/service lifecycle, live SQL/protocol/load and 24-hour soak evidence remain open; live AD/script credential evidence is not independently captured, and SEC-12 rate-limit/auto-ban behavior remains separate from this direct COM parity slice. `MSSQLSERVER` and `HmailDb_Test5700` were not used.

## Current Audit Note (2026-08-09, STALE COM/WEBADMIN NEXT-SLICE RECORDS RECONCILED)

The parity audit confirmed that several older “next slice” paragraphs are historical and must not be restarted: `RuleCriteria.MatchValue` existing-row setter/save parity is implemented in `d95ce9c69`; `hm_status.php` control and `hm_backup.php` save/start actions already enforce POST-body reads plus `hmailRequirePostCsrfToken()`; `background_servermessage_save.php` already has the same server-admin, POST-only, and CSRF boundary; and authenticated `DistributionLists.Add()` plus new-item `DistributionList.Save()` insert parity is already implemented in the current `DistributionLists.cs`/SQL store path. The authoritative next production-gate slice remains approved disposable SQL/Data restore acceptance, not any of these historical COM/WebAdmin entries.

Legacy references checked for the distribution-list audit were `InterfaceDistributionLists::Add` (`hmailserver/source/Server/COM/InterfaceDistributionLists.cpp:55-84`), `InterfaceDistributionList::Save` (`InterfaceDistributionList.cpp:252-271`), `PersistentDistributionList::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:118-157`), the `hm_distributionlists` schema (`hmailserver/source/DBScripts/CreateTablesMSSQL.sql:309-328`), and installed IDL entries (`hmailserver/source/Server/hMailServer/hMailServer.idl:1148-1192,2993-3007`). Remaining evidence is live SQL identity/rollback, real out-of-process COM activation, and connection-loss reconciliation; no production SQL/Data or delivery behavior was touched.

## Current Completed Slice (2026-08-09, INTERNAL REINITIALIZATION ADMISSION SEAM)

Code/test commit `2925427d2` adds the internal `ReinitializationAdmission` single-flight gate. It atomically admits one synchronous attempt, drops duplicate requests while that attempt is running, and releases admission after success or exception. Focused coverage proves first admission, duplicate suppression, and failure recovery.

Legacy behavior is anchored by `Application::Reinitialize` (`hmailserver/source/Server/Common/Application/Application.cpp:437-450`), `Reinitializator::ReInitialize`/`WorkerFunc` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:35-57`), `InterfaceApplication::Reinitialize` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:91-108`), and `IInterfaceApplication::Reinitialize` (`hmailserver/source/Server/hMailServer/hMailServer.idl:1491-1497`). Legacy performs a full stop/exit/init/start cycle and asynchronous single-flight admission; this slice establishes only the reusable admission seam and intentionally does not claim lifecycle parity.

`ApplicationComClass.Reinitialize()` remains `E_NOTIMPL`; no COM call site, listener, SQL/configuration reload, readiness reset, restore orchestration, installed identity, DCOM, SMTP, or live reconfiguration changed. Focused coverage is `3 passed, 0 failed, 0 skipped`; default full Net10 is `1942 passed, 0 failed, 31 skipped`. Release remains RED for a restartable runtime coordinator, service/COM lifecycle, SQL/Data restore acceptance, SEC-18, migration/installer, live performance, and soak gates. Next slice: approved disposable SQL/Data acceptance for the existing restore path, when the isolated environment is available.

## Current Completed Slice (2026-08-09, COMMANDABLE OFFLINE BENCHMARK GATE)

Code/test commit `5d0e62192` adds `build/test-net10-benchmarks.ps1`, a bounded Release-mode gate for the existing deterministic offline IMAP SEARCH/SORT benchmark. It builds the benchmark project, runs 100,000 messages with seed `5700`, validates JSON/CSV/Markdown artifacts, verifies `GitCommit` against the pre-build HEAD, and fails closed on missing or inconsistent results.

Legacy parity is anchored by `IMAPCommandSEARCH::ExecuteCommand`/`DoesMessageMatch_` (`hmailserver/source/Server/IMAP/IMAPCommandSearch.cpp:40`), `IMAPSearchParser::ParseCommand` (`hmailserver/source/Server/IMAP/IMAPSearchParser.cpp:118`), `IMAPSortParser::Parse` (`hmailserver/source/Server/IMAP/IMAPSortParser.cpp:24`), and `IMAPSort::Sort`/`CacheHeaderFields_` (`hmailserver/source/Server/IMAP/IMAPSort.cpp:32`). The current benchmark remains an offline synthetic harness and does not claim live SQL FTS, protocol, or C++ timing equivalence.

The verified Release run produced p50 `6.695 ms`, p95 `7.261 ms`, p99 `7.322 ms`, `9091/9091` correct matches, and `ThresholdPassed=True`; artifacts were written to a unique local temporary directory and were not committed. Focused benchmark tests are `7 passed, 0 failed, 0 skipped`; default full Net10 is `1939 passed, 0 failed, 31 skipped`. Release remains RED for live SQL/IMAP and legacy baseline evidence, 1,000-connection and 24-hour soak gates, restore/migration/installer, SEC-18, and service/COM lifecycle acceptance. Next slice: run the benchmark against an approved disposable SQL/FTS and live protocol target.

## Current Completed Slice (2026-08-08, DB-ONLY DOMAIN CLEANUP WIRING)

Code/test commit `a2b030d82` wires the transaction-scoped `DeleteAllDomainsForRestoreAsync` capability into DB-only restore. After archive-internal duplicate validation, the existing authorization lease and SQL transaction are acquired; the existing domain graph is cleared exactly once; and archive domains/accounts/aliases/distribution lists/recipients are inserted through the same transaction. The non-DB path still requires an empty store and does not call this cleanup.

Legacy behavior is anchored by `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-215`), `PersistentAccount::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:55-100`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:35-57`). Focused restore execution and round-trip coverage is `15 passed, 0 failed, 11 skipped`; default full Net10 is `1936 passed, 0 failed, 29 skipped`.

This slice proves executor ordering and failure-before-insert with fakes; approved disposable SQL/Data replacement remains environment-blocked. Release remains RED for full filesystem/public-folder/settings/reinitialization ordering, handle-relative containment, process-kill/power-loss, SQL/filesystem atomicity, service/COM, SEC-18, installer, AD/DC, migration, and lifecycle gates. Next slice: disposable populated-store SQL/Data acceptance for the wired DB-only path.

## Current Completed Slice (2026-08-08, TRANSACTION-SCOPED DOMAIN RESTORE CLEANUP CAPABILITY)

Code/test commit `74ca89853` adds a transaction-scoped, set-based `DeleteAllDomainsForRestoreAsync` capability to the SQL restore transaction. It snapshots domain-owned accounts, lists, rules, messages, fetch accounts, IMAP folders, group memberships, and ACL ownership under the transaction, deletes dependent rows in legacy owner order, and leaves commit or rollback to the existing transaction owner.

Legacy behavior is anchored by `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-215`), `PersistentAccount::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:36-53`). Focused coverage is `6 passed, 0 failed, 3 skipped`; default full Net10 is `1934 passed, 0 failed, 29 skipped`. SQL commit/rollback tests are present but skipped without the approved disposable SQL environment.

This is a capability-only slice: restore orchestration still does not call it, and no production restore behavior changed. Release remains RED for full deletion/reinitialization ordering, public folders/settings, handle-relative containment, process-kill/power-loss, SQL/filesystem atomicity, service/COM, SEC-18, installer, AD/DC, migration, and lifecycle gates. Next slice: wire domain cleanup immediately before full-restore filesystem replacement with isolated populated-store rollback and ordering tests.

## Current Completed Slice (2026-08-08, FINAL RESTORE CONTAINMENT REVALIDATION)

Code/test commit `0d08e2c47` adds one final synchronous `BackupRestoreContainmentPreflight.Revalidate` after metadata parsing and the authorization lease, immediately before non-DB Data-directory staging. Focused negative tests mutate the raw source or target at the lease boundary and prove the restore fails before copy or domain mutation.

Focused coverage is `13 passed, 0 failed, 0 skipped`; default full Net10 is `1933 passed, 0 failed, 27 skipped`. This is path-based last-mile hardening, not a handle-relative guarantee: `Directory.Move`, recursive enumeration, and `File.Copy` still have a check-then-use race. Release remains RED for native TOCTOU, process-kill/power-loss, SQL/filesystem atomicity, full deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, migration, and lifecycle gates. Next slice: implement native handle-relative restore swap/copy containment.

## Current Completed Slice (2026-08-08, RECOVERY JOURNAL FINALIZATION DURABILITY)

Code/test commit `cc1d0f6a5` hardens the non-DB restore journal. `BackupRestoreRecoveryJournal.Persist` and `Remove` flush the containing directory after journal replacement/deletion; if removal finalization fails, the journal evidence is rewritten and flushed before the failure is surfaced. The runtime fences rollback after the rollback artifact has been deleted, leaving a pending journal for manual recovery rather than attempting an impossible rollback. Legacy anchors are `BackupExecuter::RestoreDataDirectory_`, `FileUtilities::CopyDirectory`, and `Reinitializator::ReInitialize`.

Focused recovery coverage is `15 passed, 0 failed, 0 skipped`; default full Net10 is `1931 passed, 0 failed, 27 skipped`. Fault-injection tests prove readable pending evidence and no rollback attempt after final journal flush failure. No actual process-kill/power-loss restart or handle-relative directory-handle mutation test exists. Release status remains RED: handle-relative containment, SQL/filesystem atomicity, full deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, migration, and 24-hour lifecycle gates remain open. Next slice: implement handle-relative restore swap/copy containment.

## Current Completed Slice (2026-08-08, ARCHIVED ACCOUNT CREDENTIAL RESTORE PARITY)

Code/test commit `d039b8ed8` preserves legacy archived account `Password` and `PasswordEncryption` values during restore. Legacy `Account::XMLLoad` (`hmailserver/source/Server/Common/BO/Account.cpp:335-346`) reads both fields unchanged, and `PersistentAccount::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:263-280`) persists them unchanged. The .NET restore writer now uses a restore-specific account-store operation; the SQL store writes the archived value and type as-is, while normal Administrator account insertion retains its existing Blowfish encryption path.

Focused coverage is `26 passed, 0 failed, 0 skipped`; default full Net10 is `1928 passed, 0 failed, 27 skipped`. Disposable SQL credential round-trip remains skipped without approved SQL environment variables. The restore-specific store contract fails closed when archived-credential insertion is unsupported. Release status remains RED: recovery-journal durability, full restore ordering, crash-safe SQL/filesystem outcome, service/COM, SEC-18, installer, AD/DC, migration, and lifecycle gates remain open. Next slice: harden recovery-journal durability and handle-relative containment.

## Current Completed Slice (2026-08-08, NON-DB RESTORE RECOVERY JOURNAL)

Code/test commit `904000f85` adds a durable, bounded recovery journal to the non-DB Data directory swap. It records phase transitions, cleans up after known success, preserves rollback evidence after rollback failure or uncertain metadata outcome, and blocks service startup and later restore attempts when a pending or malformed journal requires manual recovery. Focused coverage is `12 passed, 0 failed, 0 skipped`; default full Net10 is `1914 passed, 0 failed, 25 skipped`.

Legacy behavior is anchored by `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-220`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:36-53`). Legacy deletes domain/public-folder state before data replacement and reinitializes asynchronously. Current symbols are `BackupRestoreRecoveryJournal`, `BackupRestoreDataDirectoryRuntime.RestoreAsync`, `MetadataBackupRestoreExecutor.ExecuteNonDbDataRestoreAsync`, and service `Program.cs`.

The journal is not cross-resource atomicity or automatic crash recovery: power-loss rename durability, ACL/MAC/handle-relative safety, process-kill temp cleanup, SQL connection-loss/commit ambiguity, normal-installation deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Release status is RED. Next slice: legacy domain/public-folder deletion and reinitialization ordering on disposable targets.

## Historical Slice (2026-08-08, DB-ONLY RESTORE SQL TRANSACTION)

Code/test commit `41d81cca0` adds a production-wired SQL transaction boundary for DB-only `RestoreDomains` metadata restore. Domain, account, alias, distribution-list, and recipient inserts share one SQL connection and transaction; commit and rollback/disposal behavior are covered by disposable LocalDB tests. Focused coverage is `10 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 25 skipped`; SQL-enabled full is `1926 passed, 5 failed, 2 skipped`, with five unrelated message-indexing fixture failures.

Legacy behavior is anchored by `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`) and `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85`). Current implementation symbols are `MetadataBackupRestoreExecutor.RestoreMetadataAsync`, `IBackupRestoreMetadataTransactionFactory`, `SqlServerBackupRestoreMetadataTransactionFactory`, `BackupXmlPayloadRuntime`, `Host.cs`, and `Program.cs`.

Scope is deliberately limited to DB-only metadata atomicity. Non-DB SQL/filesystem restore still lacks durable crash recovery; transaction-scoped stores support the insert path used by this slice only. Full restore ordering, commit-failure/connection-loss/process-kill evidence, queued service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Release status is RED. Next slice: durable non-DB restore journal/recovery evidence.

## Historical Slice (2026-08-08, PARTIAL RECIPIENT ROLLBACK ACCEPTANCE)

Test-only code/test commit `ec9b71ed0` proves rollback after one real distribution-list recipient insert. The second recipient insert fails, and the test verifies cleanup of the generated recipient/list/alias/account/domain rows through the real SQL stores plus Data-directory restoration. Focused LocalDB coverage is `5 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 20 skipped`; SQL-enabled full Net10 is `1921 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

Legacy list-before-recipient behavior is anchored by `DistributionList::XMLLoad`/`XMLLoadSubItems`, `DistributionListRecipients::PreSaveObject`, and persistent list/list-recipient `SaveObject`. The next slice is shared SQL transaction or durable restore journal evidence; release remains RED.

## Historical Continuation (2026-08-08, DISPOSABLE NON-DB RESTORE ACCEPTANCE)

Test-only code/test commit `1b479dfac` adds real LocalDB acceptance for the bound raw non-DB restore executor. It creates a unique disposable database, restores the legacy domain graph through the SQL administration stores, replaces a temporary Data directory from the bound raw sibling, and deterministically drops/removes all test resources. Focused restore round-trip coverage is `2 passed, 0 failed, 0 skipped`; the default full Net10 suite remains `1908 passed, 0 failed, 16 skipped`.

With the approved LocalDB opt-in enabled, the full suite reached `1918 passed, 5 failed, 2 skipped`; the five failures are pre-existing `SqlServerMessageIndexingIntegrationTests` fixture failures, not restore failures. The next service/COM queued acceptance is environment-blocked because no approved isolated out-of-process composition is available; production COM registration, DCOM, service, SQL, and Data-directory changes remain fenced. Next code slice: shared SQL/filesystem transaction or durable restore journal.

## Current Continuation (2026-08-08, RAW DATABACKUP SIBLING BINDING)

Code/test commit `124acfc0c` binds raw `DataBackup` content with the archive snapshot. Legacy `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:339-388`) resolves `DataFiles/@FolderName` beside the original archive; `.NET 10` now copies that sibling into the private binding directory, rejects source changes during copy, records a deterministic tree hash, and validates the bound tree before restore. Focused archive/raw coverage is `5 passed, 0 failed, 0 skipped`; the preceding executor/runtime focus is `13 passed, 0 failed, 0 skipped`; full Net10 is `1908 passed, 0 failed, 16 skipped`.

This remains a bounded hardening slice, not release-green. Private binding ACLs and path-based copy TOCTOU remain security residuals; metadata writes are compensating rather than crash-safe across SQL/filesystem; normal-installation domain/public-folder deletion and reinitialization are not implemented; and isolated service/COM queued restore against disposable SQL/Data has not run. Next slice: isolated service/COM restore acceptance, preserving all installed COM identities and production boundaries.

## Current Continuation (2026-08-08, BOUNDED NON-DB RESTORE STAGING)

Code/test commit `68d447861` wires `BackupRestoreDataDirectoryRuntime` into the authenticated non-DB-only `RestoreDomains|RestoreMessages` executor for disposable targets. Legacy anchors are `BackupManager::StartRestore` and `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:74-135`, `hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`). Raw restores stage the `DataFiles/@FolderName` sibling; compressed restores extract `DataBackup`; metadata failure restores the original target and compensates inserted rows. Focused coverage is `13 passed, 0 failed, 0 skipped`; full Net10 is `1907 passed, 0 failed, 16 skipped`.

The slice is intentionally bounded and not release-green. The current `BackupArchiveBinding` snapshots only the archive file, so a raw sibling restore through a bound `LoadBackup` fails closed until the sibling is independently snapshotted and hashed. The executor requires an empty disposable domain store, uses compensating SQL deletes rather than a crash-safe shared SQL/filesystem transaction, and has no service/COM end-to-end evidence. Next slice: bind/hash raw sibling `DataBackup`, then run isolated queued service/COM restore acceptance. COM identity, direct activation, production service/SQL/Data, DCOM, IIS, SMTP trust, and live reconfiguration remain fenced.

## Current Continuation (2026-08-08, ARCHIVE BINDING HARDENING FOLLOW-UP)

Code/test commit `23d428569` hardens the prior snapshot-binding slice. Real 7z metadata readers now reject an archive that cannot be snapshotted instead of creating an unbound restore object; snapshot SHA-256 is computed during the copy; and a duplicate restore dispatch no longer disposes the binding still owned by the first queued task. Focused archive/restore/COM coverage is `30 passed, 0 failed, 0 skipped`; full Net10 is `1902 passed, 0 failed, 16 skipped`.

The new regression is `BackupManagerComContractTests.DuplicateRestoreDispatch_DoesNotReleaseTheFirstTaskArchiveBinding`; snapshot lifecycle coverage remains in `BackupArchiveIdentityTests`. No COM contract, production service/SQL/Data directory, registration, DCOM, IIS, SMTP trust, or live reconfiguration changed. Residual risk is confined to unimplemented raw sibling identity/full non-DB restore and best-effort cleanup of snapshots held by never-dispatched retained COM objects. Next slice: wire raw/compressed `DataBackup` staging into authenticated non-DB-only restore against disposable targets.

## Current Continuation (2026-08-08, QUEUED RESTORE ARCHIVE SNAPSHOT BINDING)

Code/test commit `435532ad0` closes the queued-restore archive replacement gap without changing the installed COM contract. `BackupManager.LoadBackup` now materializes an existing caller archive into a private snapshot before metadata parsing; the `Backup` object keeps that snapshot and its SHA-256 identity, queued restore reads only the snapshot under a read-sharing lock, and dispatch/worker failure paths clean it up. Legacy anchors are `BackupManager::LoadBackup` and `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:38-135`, `BackupExecuter.cpp:230-388`), which retain only the original path and reopen it later. Focused archive/restore/COM coverage is `38 passed, 0 failed, 0 skipped`; full Net10 is `1901 passed, 0 failed, 16 skipped`.

Current symbols are `BackupArchiveBinding.cs`, `BackupArchiveIdentity.cs`, `BackupManager.cs`, `Backup.cs`, and `BackupRestoreExecution.cs`; focused tests are `BackupArchiveIdentityTests`, `BackupRestoreExecutionTests`, `BackupComContractTests`, and `BackupManagerComContractTests`. The snapshot is an explicit security hardening over legacy valid-replacement behavior. COM GUIDs, ProgIDs, DISPIDs, vtable order, direct activation denial, SMTP trust, live reconfiguration, production service, SQL/Data directories, registration, DCOM ACLs, and IIS were untouched. Residual risk: raw sibling `DataBackup` identity and full non-DB restore are still fenced; snapshot cleanup depends on the queued task or finalization for an unused retained object. Next slice: wire raw/compressed `DataBackup` staging into authenticated non-DB-only restore with disposable target and rollback acceptance.

## Current Continuation (2026-08-08, BACKUP AUTHORIZATION REVALIDATION)

Code/test commit `edd01f557` adds a bounded internal authentication-generation guard to Application-created `BackupManager` and `Backup` objects. A retained manager or backup is denied after any later failed or successful `Application.Authenticate` attempt, while a newly acquired object after successful authentication works. Authentication state is cleared before each attempt and stale concurrent authentication completions cannot publish over a newer generation. Legacy references are `COMAuthentication::Authenticate` (`hmailserver/source/Server/COM/COMAuthentication.cpp:31`), `InterfaceApplication::get_BackupManager`/`Authenticate` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:136`), and `InterfaceBackupManager::LoadSettings`/`StartBackup`/`LoadBackup` plus `InterfaceBackup::StartRestore` (`hmailserver/source/Server/COM/InterfaceBackupManager.cpp`, `InterfaceBackup.cpp`).

Current symbols are `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/ApplicationComClass.cs` (`Authenticate`, `BackupManager`, `IsCurrentAdministrator`), `BackupManager.cs` (`EnsureAuthorized`, `LoadBackup`), and `Backup.cs` (`EnsureAuthorized`). Focused Backup/Application contract coverage is `30 passed, 0 failed, 0 skipped`; full Net10 is `1897 passed, 0 failed, 16 skipped`. COM GUIDs, ProgIDs, DISPIDs, vtable order, direct activation denial, SMTP trust, live reconfiguration, production service, SQL/Data directories, COM registration, DCOM ACLs, and IIS were untouched.

Residual risk: legacy retained Backup objects were also usable after authentication loss, so this is a deliberate security hardening over legacy behavior rather than a compatibility-preserving change. Authorization is checked at method entry; an already-running queued operation is not canceled by reauthentication. The next slice is archive path/content identity binding to close the remaining restore TOCTOU risk before wiring non-DB-only DataBackup restore into the authenticated executor. Full restore ordering, shared SQL transactionality, service/COM acceptance, SEC-18, native AD/DC, installer, and 24-hour lifecycle gates remain open.

## Current Continuation (2026-08-08, ISOLATED DATABACKUP STAGING)

Code/test commit `a4b9dfe9e` adds `BackupRestoreDataDirectoryRuntime`, an isolated raw/compressed `DataBackup` staging primitive. It requires valid archive/message-file evidence and a safe `BackupRestoreContainmentPlan`, rejects DB-only evidence and unsupported formats, rejects reparse points during copy, stages raw sibling content or extracts only the validated `DataBackup` subtree from 7z, swaps a disposable target behind a rollback artifact, restores the original target on staging failure, kills a canceled extractor process tree, and cleans successful rollback/extraction artifacts. The runtime is not yet wired into the production restore executor; no production Data directory or service behavior changed.

Legacy anchor: `hmailserver/source/Server/Common/Application/BackupExecuter.cpp` (`RestoreDataDirectory_`), where raw restores use the sibling `FolderName` directory and compressed restores extract `DataBackup` to a temporary directory before replacing the configured Data directory. Current anchors: `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/BackupRestoreDataDirectoryRuntime.cs`, `BackupRestoreContainmentPreflight.cs`, and `BackupRestoreIntegrityRuntime.cs`; focused filesystem coverage is `4 passed, 0 failed, 0 skipped`; full Net10 is `1893 passed, 0 failed, 16 skipped`.

Residual risk: no service/COM end-to-end invocation, real disposable Data-directory restore, reparse-race test, or legacy-vs-.NET large-tree performance evidence exists yet. The next slice is to wire this primitive behind the already-authenticated restore executor for non-DB-only `RestoreDomains|RestoreMessages`, with isolated raw/7z target acceptance and rollback. Settings/public folders/IMAP folders/messages, SQL transaction integration, application reinitialization, live SMTP behavior, installed COM identity, SEC-18, and production replacement remain fenced.

## Current Continuation (2026-08-08, QUEUED DB-ONLY METADATA RESTORE)

Code/test commit `26b660ff8` opens one authenticated, queued restore slice without settings, public folders, folders/messages, physical Data-directory replacement, reinitialization, or COM registration changes. `BackupManager.LoadBackup` retains the normalized archive path; `Backup.StartRestore` preserves the installed `IInterfaceBackup` contract and dispatches through the existing serialized maintenance operation boundary. `MetadataBackupRestoreExecutor` accepts only `RestoreDomains` for an archive containing only `BODomains`, runs archive integrity inspection, DTD-disabled bounded metadata parsing, containment planning/revalidation, and restores domains, accounts, aliases, distribution lists, and recipients. Generated IDs are captured for compensating rollback, including recipient IDs; unsupported selections and archive sections fail closed before mutation.

Legacy anchors: `hmailserver/source/Server/Common/Application/BackupManager.cpp` (`StartRestore`, `LoadBackup`), `hmailserver/source/Server/Common/Application/BackupExecuter.cpp` (`StartRestore`, `RestoreDataDirectory_`), and `hmailserver/source/Server/hMailServer/hMailServer.idl` (`IInterfaceBackup.StartRestore`, restore-selection DISPIDs). Current anchors: `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/BackupManager.cs`, `Backup.cs`, `BackupOperationRuntime.cs`, `BackupRestoreExecution.cs`, and `BackupRestoreMetadataWriter.cs`. Focused restore/COM coverage is `25 passed, 0 failed, 0 skipped`; disposable Local SQL restore round-trip plus restore/COM coverage is `24 passed, 0 failed, 0 skipped`; full Net10 is `1889 passed, 0 failed, 16 skipped`.

Residual risk: this is not the legacy full restore. It does not restore settings, public folders, IMAP folders/messages, message files, raw/7z `DataBackup`, or reinitialize the live application; the production path has not yet been exercised through an out-of-process COM/service host or a real disposable end-to-end SQL restore. The store abstraction uses compensating deletes rather than one shared SQL transaction, so rollback acceptance remains a gate. Next slice: execute the queued path through an isolated service/COM composition against a disposable SQL target, then implement and verify raw/compressed DataBackup restore staging and rollback. Real native AD/DC, SEC-18, out-of-proc COM/DCOM, installer, and 24-hour lifecycle gates remain open.

# hMailServer .NET 10 Rewrite

## Current Authoritative Continuation (2026-08-10, FetchAccount Restore)

Code/test commit `7e8d71c15` adds restore-side FetchAccount and nested FetchAccountUID persistence. Legacy anchors are `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`), `FetchAccount::XMLStore` (`FetchAccount.cpp:55-79`), `FetchAccountUID::XMLStore` (`FetchAccountUID.cpp:42-49`), and owner-scoped refresh in `FetchAccounts.cpp:36-43`/`FetchAccountUIDs.cpp:29-50`. Current anchors are `BackupArchiveXmlSnapshotParser.ParseFetchAccount`, `BackupRestoreMetadataWriter.RestoreFetchAccountsAsync`, `MetadataBackupRestoreExecutor.RestoreMetadataAsync`, `IBackupRestoreMetadataTransaction.FetchAccountStore`, and `SqlServerFetchAccountAdministrationStore`.

Focused parser/SQL/restore coverage is `30/30`; disposable LocalDB FetchAccount readback and transaction rollback is `2/2`; default full Net10 is `1990 passed, 35 skipped, 0 failed`. SQL-enabled full Net10 is `2017 passed, 2 skipped, 6 unrelated existing message/indexing fixture failures`. The slice preserves legacy Blowfish ciphertext, transaction rollback, COM identity, authenticated boundaries, SMTP trust, and production isolation. Release remains RED for live paired performance, populated full restore/round-trip, SEC-18, migration/installer, service/out-of-process COM, AD/DC, protocol/load, crash/power-loss, and soak gates.

## Current Continuation (2026-08-08, IMAP DOMAIN-ALIAS LAST-AT PARSING)

Corrective code/test commit `ea1299638` closes the quoted-local-part gap in the domain-alias authentication slice from `a5e250557`. The SQL lookup now uses the last `@`, matching legacy `StringParser::ExtractDomain`/`ExtractAddress` and C++ `ReverseFind`, while retaining deterministic `daid` ordering and culture-independent case-insensitive comparison. Focused SQL/shape coverage is `4 passed, 0 skipped`; full Net10 is `1884 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods. The disposable fixture covers case-insensitive alias input, a quoted local-part containing `@`, and ordinary non-AD password authentication through an alias. Next slice: isolated SQL/Data-directory restore execution and round-trip evidence; real AD/DC, 24-hour service/COM lifecycle, SEC-18, real COM/DCOM, and installer gates remain open.

## Current Continuation (2026-08-08, IMAP DOMAIN-ALIAS LOGIN LOOKUP)

Code/test commit `a5e250557` implements legacy normal IMAP domain-alias lookup parity. `SqlServerImapAccountAuthenticator.AccountLookupSql` joins `hm_domain_aliases`, maps an alias mailbox to its owning domain, preserves direct-address lookup, and orders alias matches by `daid`. Explicit `Latin1_General_100_CI_AS` comparisons avoid culture-sensitive `LOWER()` behavior under a Turkish SQL collation and match the legacy case-insensitive alias contract. The disposable local SQL fixture proves case-insensitive `ALIASUSER@ALIAS.TEST` authentication returns `aliasuser@example.test`; focused SQL coverage is `4 passed, 0 skipped`, and full Net10 is `1884 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods.

Legacy anchors are `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:44-51`), `DomainAliases::ApplyAliasesOnAddress` (`hmailserver/source/Server/Common/BO/DomainAliases.cpp:43-64`), and `CreateTablesMSSQL.sql:250-256`. No COM identity, SMTP trust, live reconfiguration, production service, database, or Data directory changed. Next slice: isolated SQL/Data-directory restore execution and round-trip evidence; real AD/DC, 24-hour service/COM lifecycle, SEC-18, real COM/DCOM, and installer gates remain open.

## Current Continuation (2026-08-08, DEFAULT DOMAIN LOGIN LOOKUP)

Code/test commit `c0d9294b6` applies configured `Settings.DefaultDomain` to normal IMAP username lookup when the username has no `@`, through the existing settings-store boundary. Disposable local SQL evidence proves `default` authenticates as `default@example.test`; focused `1 passed, 0 skipped`; full Net10 `1882 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods. Legacy `PasswordValidator.cpp:44-51` applies aliases then default domain; domain-alias translation remains a separate open slice. Next slice: `hm_domain_aliases` lookup parity.

## Current Continuation (2026-08-08, LOGIN SCRIPT ORDERING)

Code/test commit `d2c24d2c8` restores legacy normal `LOGIN` script ordering: the SQL account is materialized, the password-validation script runs before empty-password rejection, script `Accept` can authorize an empty password, and `Continue` still fails empty passwords. `AUTHENTICATE PLAIN` continues to reject empty passwords in the parser before the authenticator. Focused SQL/IMAP coverage is `40 passed, 0 skipped`; full Net10 is `1882 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods. Legacy anchors are `IMAPCommandLOGIN::ExecuteCommand` (`hmailserver/source/Server/IMAP/IMAPCommandLogin.cpp:52-57`), `IMAPCommandAUTHENTICATE::ExecuteCommand` (`hmailserver/source/Server/IMAP/IMAPCommandAuthenticate.cpp:77-79`), and `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:109-133`). Next slice: domain-alias/default-domain lookup; real AD/DC, SEC-18/COM/DCOM, installer, migration/restore, and soak gates remain open.

## Current Continuation (2026-08-08, AD VALIDATION CONNECTION LIFETIME)

Code/test commit `eec9752e8` releases the SQL reader and connection before the synchronous AD validator and uses a separate connection for successful last-logon updates. The local SQL fixture constrains the pool to one connection and opens a probe connection inside the validator; focused coverage is `7 passed, 0 skipped`. This closes the AD validation connection-pool retention risk without changing credentials, COM identity, SMTP trust, or production state.

Legacy anchors are `AccountLogon::Logon` in `hmailserver/source/Server/Common/Util/AccountLogon.cpp:37-75` and `PasswordValidator::ValidatePassword` in `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:109-147`. Real domain-controller/native `LogonUser` evidence, domain aliases/default-domain lookup, and LOGIN script-before-empty-password ordering remain open. AUTHENTICATE PLAIN empty-password rejection remains protocol-parser behavior. Next slice: legacy LOGIN script-before-empty-password ordering.

## Current Continuation (2026-08-08, SQL ACTIVE DIRECTORY AUTHENTICATION EVIDENCE)

Code/test commit `4072dbf50` adds an opt-in disposable SQL Server integration fixture for the AD IMAP authentication path. It uses a unique local database with the production MSSQL `hm_accounts` types, proves active account/domain filtering, exact AD validator arguments, successful and rejected credentials, last-logon updates, and no validator call for an inactive domain. Local SQL focused coverage is `7 passed, 0 skipped`; the normal full suite excluding the two AV-locked EICAR cleanup methods is `1880 passed, 0 failed, 16 skipped`. The SQL projection explicitly converts MSSQL `tinyint` and `datetime` columns before materialization.

Legacy behavior is anchored by `PasswordValidator::ValidatePassword` in `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:34-147` and `SSPIValidation::ValidateUser` in `hmailserver/source/Server/Common/Util/SSPIValidation.cpp:13-22`: activity checks precede password validation, script override precedes empty-password rejection, and AD uses `LOGON32_LOGON_NETWORK`. Real domain-controller/native credential evidence remains open. Domain aliases/default-domain lookup and script-before-empty-password parity remain separate slices. No production service, database, Data directory, COM registration, DCOM state, or SMTP behavior was changed.

Next slice: isolated 24-hour service restart/COM lifecycle soak where a disposable host is available; otherwise select the next unblocked legacy parity slice.

## Current Continuation (2026-08-08, AD AUTHENTICATION BOUNDARY)

Code/test commit `69f52b5d6` adds the bounded Active Directory password-validation boundary. The legacy anchors are `SSPIValidation::ValidateUser` in `hmailserver/source/Server/Common/Util/SSPIValidation.cpp` and the AD branch of `PasswordValidator::ValidatePassword` in `hmailserver/source/Server/Common/Util/PasswordValidator.cpp`: after script override and empty-password rejection, hMailServer calls `LogonUser` with `LOGON32_LOGON_NETWORK` and `LOGON32_PROVIDER_DEFAULT`. .NET now uses an injectable `IActiveDirectoryPasswordValidator`; the Windows implementation calls the same native contract, closes a returned token, rejects empty inputs, and converts native exceptions to failure. The SQL authenticator uses the existing `accountisad`, `accountaddomain`, and `accountadusername` fields and retains no password. Focused AD/IMAP/auth coverage is `46 passed, 0 skipped`; the full suite excluding the two AV-locked EICAR cleanup methods is `1880 passed, 0 failed, 15 skipped` (1895 total). Live disposable SQL/AD evidence still requires an approved test database/domain controller and is not claimed. Next slice: isolated disposable SQL/AD authentication evidence.

## Current Continuation (2026-08-08, IMAP MASTER USER)

Code/test commit `ef7e5ec65` implements the bounded legacy IMAP AUTHENTICATE PLAIN master-user path. The legacy anchors are `IMAPCommandAUTHENTICATE::ExecuteCommand` in `hmailserver/source/Server/IMAP/IMAPCommandAuthenticate.cpp` and `AccountLogon::Logon`/`PasswordValidator::ValidatePassword` in `hmailserver/source/Server/Common/Util/AccountLogon.cpp` and `PasswordValidator.cpp`: the authcid must be the configured master user, the authzid selects an active target mailbox, only the master credential is verified, and master-policy failures do not count as password failures. .NET now carries authzid through the existing authentication service, returns the target mailbox on success, preserves ordinary LOGIN/PLAIN behavior, and avoids credential persistence. Focused coverage is `43 passed, 0 skipped`; the full suite excluding the two AV-locked EICAR cleanup methods is `1877 passed, 0 failed, 15 skipped` (1892 total). AD/SSPI authentication, live SQL master-user evidence, 24-hour service/COM lifecycle soak, real COM/DCOM activation, SEC-18, InnoSetup build, and final release gates remain open.

## Current Continuation (2026-08-08)

Code/test commit `53680b0d2` adds a commandable short offline synthetic IMAP SEARCH/SORT soak. `ShortSoakBenchmark` repeats the deterministic 100k-message workload for bounded cycles and reports p50/p95/p99, errors, private-memory/working-set growth, handles, threads, TCP connections, GC collections, timestamps, runtime, and git commit in JSON/CSV/Markdown. Focused coverage is `4 passed, 0 skipped`; a smoke run completed 3/3 cycles with 0 errors and threshold pass. Example: `dotnet run --project hmailserver/source/Server.Net10/benchmarks/HMailServer.Net10.Benchmarks/HMailServer.Net10.Benchmarks.csproj --configuration Debug -- --mode short-soak --count 100000 --cycles 20 --max-seconds 30 --output artifacts/benchmarks/short-soak`. This is an offline synthetic acceptance signal only; it does not prove a 24-hour live service leak run, protocol equivalence, SQL behavior, COM lifecycle, or release readiness. Next slice: isolated 24-hour service restart/COM lifecycle soak where the required host is available. Real out-of-proc COM activation, SEC-18, the InnoSetup build, and final release gates remain open.

Code/test commit `8cc67112b` adds an honest InnoSetup installer source gate. `InstallerSourceGateTests` verifies the legacy `hMailServer64.iss` include graph and the C++ x64 payload references; the actual `ISCC.exe` build is explicit opt-in and reports `Inconclusive` when the toolchain or legacy release binary is unavailable. Focused coverage is `1 passed, 1 skipped`; the full suite excluding the two AV-locked EICAR cleanup methods is `1872 passed, 0 failed, 15 skipped` (1887 total). The installer build itself remains environment-blocked because this host has neither `ISCC.exe` nor `hmailserver/source/server/hMailServer/x64/Release/hMailServer.exe`; no installer or production state was changed. Next slice: commandable short soak/leak acceptance with explicit thresholds and artifact output. Real out-of-proc COM activation, SEC-18, the actual InnoSetup build, 24-hour soak, and final release gates remain open.

This folder contains the side-by-side .NET 10 implementation track. The legacy C++/ATL server remains the production implementation until this tree reaches protocol, data, and COM compatibility.

## Current Continuation (2026-08-05)

Code/test commit ``4191ac3d1`` adds the release artifact gate. ``ReleaseArtifactGateTests`` asserts the 13 required Net10 Service artifacts and the runtimeconfig framework. Focused ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1871 passed, 0 failed, 14 opt-in skips`` (1885 total). Next slice: InnoSetup installer build gate.


Code/test commit ``c09fcf435`` adds COM host activation feasibility evidence. ``ComHostActivationIntegrationTests`` loads the comhost DLL, verifies the ``DllGetClassObject`` export, and records HRESULT ``0x80008093`` (host-runtime dependency) for in-process invocation; genuine out-of-proc activation requires registration/DCOM (fenced). Focused ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1870 passed, 0 failed, 14 opt-in skips`` (1884 total). Next slice: installer/release artifact gate.


Code/test commit ``59623bb20`` adds live 1k-concurrent SMTP connection acceptance. ``SmtpTcpListenerTests.LoopbackConcurrency_AcceptsOneThousandClients`` opens 1000 concurrent loopback clients (backlog 1024) and asserts every one receives the 220 banner. Focused ``1/1`` (stable across repeated runs); the full suite excluding the two AV-locked EICAR cleanup methods is ``1869 passed, 0 failed, 14 opt-in skips`` (1883 total). Next slice: real COM activation evidence.


Code/test commit ``c965cf2b0`` adds live IMAP and POP3 accept-latency acceptance harnesses mirroring the SMTP harness (200 loopback clients, banner assert, p95 budget). Focused listener coverage ``15/15``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1868 passed, 0 failed, 14 opt-in skips`` (1882 total). Next slice: 1k-concurrent loopback connection acceptance.


Code/test commit ``21b63cd13`` adds the live SMTP accept-latency acceptance harness. ``SmtpAcceptLatencyIntegrationTests`` binds ``SmtpTcpListener`` on loopback, connects 200 clients, asserts the 220 banner, and measures p50/p95/p99 connect-to-banner latency against a 5s p95 budget. Focused ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1866 passed, 0 failed, 14 opt-in skips`` (1880 total). Next slice: IMAP/POP3 loopback accept-latency harnesses, then 1k-concurrent connection acceptance.


Code/test commit ``98433db25`` adds isolated database version gate and upgrade rollback evidence. ``SqlServerDatabaseAdministrationStoreIntegrationTests`` seeds ``hm_dbversion`` at 5000, simulates the upgrade write to 5708 (gate clears), then rolls back to 5000 (gate returns, one version row). Live LocalDB ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1865 passed, 0 failed, 14 opt-in skips`` (1879 total). Next slice: live protocol acceptance harness.


Code/test commit ``495ddb974`` adds isolated backup metadata restore round-trip evidence. ``BackupRestoreRoundTripIntegrationTests`` restores a crafted legacy archive (domain/account/alias/distribution-list/recipient) through the parser + transactional restore writer into a disposable LocalDB target and verifies every restored row. Live LocalDB ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1865 passed, 0 failed, 13 opt-in skips`` (1878 total). Next slice: upgrade rollback evidence.


Code/test commit ``19456d549`` adds backup archive distribution-list recipient XML parsing with transactional restore. ``ParseDistributionListRecipients`` reads ``<Recipient Name=...>``, and ``RestoreDistributionListRecipientsAsync`` replays them through ``InsertDistributionListRecipientAsync`` inside the transaction boundary with caller rollback. Focused ``9/9``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1865 passed, 0 failed, 12 opt-in skips`` (1877 total). Next slice: full restore round-trip into temp DB/Data.


Code/test commit ``8e5bfa01f`` adds backup archive alias and distribution-list XML parsing with transactional restore. ``ParseAliases``/``ParseDistributionLists`` reconstruct the legacy snapshots, and ``RestoreAliasesAsync``/``RestoreDistributionListsAsync`` replay them through the stores inside the transaction boundary with caller rollback. Focused ``2/2``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1864 passed, 0 failed, 12 opt-in skips`` (1876 total). Next slice: distribution-list recipient XML parsing to complete the restore payload.


Code/test commit ``fc8efb819`` adds backup archive account XML parsing with transactional restore. ``BackupArchiveXmlSnapshotParser.ParseAccounts`` reads the legacy ``<Account>`` attribute set into ``RestoreAccountEntry``, and ``RestoreAccountsAsync`` replays entries through ``IAccountAdministrationStore.InsertAccountAsync`` inside the transaction boundary with caller rollback. ``BackupArchiveXmlSnapshotParserTests`` asserts field reconstruction and the XML→writer→store round trip. Focused ``2/2``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1862 passed, 0 failed, 12 opt-in skips`` (1874 total). Next slice: alias/distribution-list XML parsing to complete the restore payload.


Code/test commit ``9e2d44daf`` adds the backup archive XML→snapshot parser with restore wiring. ``BackupArchiveXmlSnapshotParser.ParseDomains`` reads the legacy ``<Domain>`` attribute set into ``DomainAdministrationSnapshot`` (including anti-spam/DKIM and limitations bit packing), and the parsed snapshots feed ``BackupRestoreMetadataWriter.RestoreDomainsAsync`` for transactional restore. ``BackupArchiveXmlSnapshotParserTests`` asserts field reconstruction and the XML→writer→store round trip. Focused ``2/2``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1860 passed, 0 failed, 12 opt-in skips`` (1872 total). Next slice: account/alias/distribution-list XML parsing to complete the restore payload.


Code/test commit ``887521659`` adds the first restore-execution seam, ``BackupRestoreMetadataWriter.RestoreDomainsAsync``, which replays a snapshot batch through ``IDomainAdministrationStore.InsertDomainAsync`` inside ``BackupRestoreTransactionBoundary`` so a mid-batch failure invokes the caller rollback and rethrows. ``BackupRestoreMetadataWriterTests`` covers full replay and partial-failure rollback. Focused ``2/2``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1858 passed, 0 failed, 12 opt-in skips`` (1870 total). Next slice: wire archive XML→snapshot restore for domains/accounts.


Code/test commit ``f8fa925f0`` adds isolated SQL backup-projection evidence. ``SqlServerBackupProjectionIntegrationTests`` seeds a disposable LocalDB database and proves ``GetBackupAccountsAsync`` (identity + BlowFish password round-trip + ``PasswordEncryption=1``), ``GetBackupRulesAsync``, and ``GetDomainsAsync``. Live LocalDB ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1856 passed, 0 failed, 12 opt-in skips`` (1868 total). Next slice: full isolated backup/restore round-trip (restore execution into temp DB/Data).


Test-only commit ``bd168169d`` adds an offline acceptance seam for the performance benchmark pack via ``SyntheticBenchmarkArtifactWriterTests``, asserting the deterministic JSON/CSV/Markdown artifact serialization without running the heavy benchmark or a live server. Focused ``1/1``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1856 passed, 0 failed, 11 opt-in skips`` (1867 total). Next slice: isolated backup/restore round-trip + upgrade rollback evidence (temp DB/Data, disposable).


Code/test commit ``0429fa1f1`` adds the COM-path SSRF guard for scanner-test methods. ``LegacyLocalScannerTargetGuard`` requires every resolved address to be loopback or a local interface address (mirroring legacy ``IsLocalHost``), applied on ``AntiVirus.TestClamAVScanner`` and ``AntiSpam.TestSpamAssassinConnection`` before the runtime connects; non-local or unresolvable targets fail closed with ``E_FAIL``. Unit and COM contract tests cover local accept, public/remote denial-before-runtime, and local delegation. Focused coverage ``26/26``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1855 passed, 0 failed, 11 opt-in skips`` (1866 total). Next slice: real COM activation evidence for completed COM identities.


Read-only egress/SSRF posture audit (no production code changed). External fetch enforces ``ExternalFetchEndpointPolicy``; WebAdmin scanner AJAX handlers enforce ``IsLocalHost``-only targets plus POST-CSRF (``437612a13``); the COM ``TestClamAVScanner``/``TestSpamAssassinConnection`` methods still accept arbitrary host+port (residual gap), deferred as a security-policy slice needing an explicit allow-list design. Next slice: COM-path SSRF guard design decision for scanner-test methods.


Code/test commit ``437612a13`` completes SEC-14 WebAdmin AJAX scanner-test POST/CSRF hardening. ``background_ajax_virustest.php`` and ``background_ajax_spamassassintest.php`` now require ``hmailRequirePostCsrfToken()`` after the server-admin guard, keep local-scanner-target restrictions and POST-only reads. New source tests assert guard-before-CSRF-before-reads. Focused coverage ``2/2``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1851 passed, 0 failed, 11 opt-in skips`` (1862 total). Next slice: shared egress/SSRF policy review for external fetch and diagnostics/network tests.


Code/test commit ``795bd3b93`` completes authenticated ``Messages.DeleteByDBID`` plus ``Clear`` parity (DB-only), completing the message mutation trio (insert ``85e5a143a``, update ``f06a199b4``, delete). Legacy ``InterfaceMessages::DeleteByDBID``/``Clear`` delegate to ``PersistentMessage`` deletion. The .NET path preserves installed Messages/Message COM identity/direct activation denial, treats unknown IDs as no-ops, maps store failure to ``E_FAIL``, clears the folder snapshot only after successful whole-collection deletion, and removes only the selected snapshot after an owner+folder-scoped delete succeeds. Data-directory message-file deletion remains fenced. Focused coverage ``31/31``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1849 passed, 0 failed, 11 opt-in skips`` (1860 total). Next slice: SEC-14 WebAdmin remaining POST-only handlers.


Code/test commit ``f06a199b4`` completes authenticated existing-row ``Message.Save()`` UPDATE parity (DB-only) after message insert ``85e5a143a``. Legacy anchors are ``InterfaceMessage::Save`` and ``PersistentMessage::SaveObject``. The .NET path preserves installed Messages/Message COM identity/direct activation denial, rechecks live authentication, stages retained-message From/header setters, persists via a parameterized owner-scoped UPDATE, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Data-directory message-file creation and content rewrites remain fenced. Focused coverage ``26/26``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1844 passed, 0 failed, 11 opt-in skips`` (1855 total). Next slice: message delete/Clear parity (DB-only).


Code/test commit ``22c206330`` adds the opt-in isolated SQL identity/readback and rollback evidence for the message insert. The ``SqlServerMessageAdministrationStoreIntegrationTests`` fixture mirrors the legacy ``hm_messages`` schema in a disposable isolated database and proves ``OUTPUT INSERTED.messageid`` identity readback with per-insert increments, and a UNIQUE ``messagefilename`` violation leaving no partial row. Live LocalDB evidence is ``1/1``; the focused Messages suite is ``24/24``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1841 passed, 0 failed, 11 opt-in skips`` (1852 total). Next slice: existing-row ``Message.Save()`` UPDATE parity (DB-only).


Code/test commit ``85e5a143a`` completes folder-scoped ``Messages.Add()`` plus new-item ``Message.Save()`` INSERT parity (DB row only). Legacy anchors are ``InterfaceMessages::Add`` (``hmailserver/source/Server/COM/InterfaceMessages.cpp:102``) and ``PersistentMessage::SaveObject``. The .NET path preserves installed Messages/Message COM identity/direct activation denial, rejects the account message-cache Add with ``DISP_E_BADINDEX``, stages a folder-scoped draft with Subject/From/Date/header setters, persists via a parameterized insert with ``OUTPUT INSERTED.messageid`` and a generated partial ``.eml`` filename, maps store failure to ``E_FAIL``, and publishes only the saved snapshot after success. Data-directory message-file creation remains fenced. Focused coverage ``23/23``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1841 passed, 0 failed, 10 opt-in skips`` (1851 total). Next slice: message SQL identity/readback evidence for the insert.


Code/test commit ``29d90ca9d`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed TCP/IP port mutations. The ``SqlServerTcpIpPortAdministrationStoreIntegrationTests`` fixture mirrors the legacy ``hm_tcpipports`` schema in a disposable isolated database and proves ``OUTPUT INSERTED.portid`` identity readback with per-insert increments, identity-preserving UPDATE, delete-by-id, ``DeleteAllTcpIpPorts``, and a UNIQUE ``portnumber`` violation leaving no partial row. Live LocalDB evidence is ``1/1``; the focused TCP/IP-port suite is ``25/25``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1838 passed, 0 failed, 10 opt-in skips`` (1848 total). Next slice: bounded ``Messages.Add()``/``Message.Save()`` INSERT parity with data-directory file creation fenced.


Code/test commit ``e12dbe24a`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed account mutations. The ``SqlServerAccountAdministrationStoreIntegrationTests`` fixture mirrors the legacy ``hm_accounts`` schema plus dependents in a disposable isolated database and proves ``OUTPUT INSERTED.accountid`` identity readback with per-insert increments, BlowFish password round-trip, owner-scoped UPDATE/DELETE no-op against foreign domain IDs, conditional password update, transactional cascade DELETE, and NOT NULL address violations leaving no partial row and the original row intact. The fixture also surfaced a latent account-read defect under ``CommandBehavior.SequentialAccess``, now fixed by using ``CommandBehavior.Default``. Live LocalDB evidence is ``1/1``; the focused Accounts suite is ``57/57``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1838 passed, 0 failed, 9 opt-in skips`` (1847 total). Next slice: next authenticated Admin collection mutation after accounts.


Code/test commit ``2fbc3a085`` completes authenticated existing-row ``Account.Save()`` UPDATE parity, completing the account mutation trio (insert ``43ab59b74``, delete ``84fa764e3``, update). Legacy anchors are ``InterfaceAccount::Save`` and ``PersistentAccount::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:220-300``). The .NET path preserves installed Accounts/Account COM identity/direct activation denial, rechecks live domain/server authentication, stages retained-account setters through a mutable snapshot overlay, persists via a parameterized owner-scoped UPDATE with conditional password columns, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Retained account setters are now staged instead of ``E_NOTIMPL``. Focused coverage ``56/56``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1838 passed, 0 failed, 8 opt-in skips`` (1846 total). Next slice: isolated SQL identity/readback rollback evidence for account mutations.


Code/test commit ``84fa764e3`` completes authenticated domain-owned ``Accounts.DeleteByDBID``/``Delete`` plus attached ``Account.Delete()`` parity, following account insert ``43ab59b74``. Legacy anchors are ``InterfaceAccounts::Delete``/``DeleteByDBID`` and ``PersistentAccount::DeleteObject`` (``hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:30-70``). The .NET path preserves installed Accounts/Account COM identity/direct activation denial, rechecks live domain/server authentication, treats unknown/stale IDs as no-ops, maps store failure to ``E_FAIL``, and removes only the selected snapshot after a transactional cascade delete succeeds. Data-directory folder deletion remains fenced. Focused coverage ``51/51``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1833 passed, 0 failed, 8 opt-in skips`` (1841 total). Next slice: authenticated existing-row ``Account.Save()`` UPDATE parity.


Code/test commit ``43ab59b74`` completes authenticated domain-owned ``Accounts.Add()`` plus new-item ``Account.Save()`` INSERT parity. Legacy anchors are ``InterfaceAccounts::Add`` (``hmailserver/source/Server/COM/InterfaceAccounts.cpp:42``) and ``PersistentAccount::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:220-300``). The .NET path preserves installed Accounts/Account COM identity/direct activation denial, rechecks live domain/server authentication, stages the draft against the owning domain, stages all account setters, persists via a parameterized insert with ``OUTPUT INSERTED.accountid`` and BlowFish password encryption, retains the failed draft (``E_FAIL``), and publishes only the saved snapshot after success. Existing-row Save/Delete remain ``E_NOTIMPL``. Focused coverage ``46/46``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1828 passed, 0 failed, 8 opt-in skips`` (1836 total). Next slice: authenticated existing-row ``Account.Save()`` UPDATE parity.


Code/test commit ``794d93a3c`` completes authenticated ``TCPIPPorts.SetDefault()`` parity. Legacy anchors are ``InterfaceTCPIPPorts::SetDefault`` (``hmailserver/source/Server/COM/InterfaceTCPIPPorts.cpp:37``) and ``TCPIPPorts::SetDefault`` (``hmailserver/source/Server/Common/BO/TCPIPPorts.cpp:37-80``). The .NET path preserves installed TCPIPPorts COM identity/direct activation denial, rechecks live server-administrator authentication, performs the legacy no-op detection against the four default ports, deletes all then reinserts the four defaults and reloads the snapshot, and maps store failure to ``E_FAIL``. Focused coverage ``24/24``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1824 passed, 0 failed, 8 opt-in skips`` (1832 total). Next slice: authenticated ``Accounts.Add()`` plus new-item ``Account.Save()`` INSERT parity.


Code/test commit ``f85bf4681`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed domain mutations. The ``SqlServerDomainAdministrationStoreIntegrationTests`` fixture mirrors the legacy ``hm_domains`` schema plus dependents in a disposable isolated database and proves ``OUTPUT INSERTED.domainid`` identity readback with per-insert increments, anti-spam/limitations bit-packing round-trip, owner-scoped UPDATE/DELETE no-op against unknown IDs, transactional cascade DELETE removing domain dependents before the domain row, and NOT NULL name violations leaving no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused Domains suite is ``23/23``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1819 passed, 0 failed, 8 opt-in skips`` (1827 total). Next slice: next authenticated Admin collection mutation after domains.


Code/test commit ``aacbabb99`` completes authenticated owner-scoped ``Domains.DeleteByDBID`` plus attached ``Domain.Delete()`` parity, completing the domain mutation trio (insert ``444d4f777``, update ``1778f619d``, delete). Legacy anchors are ``InterfaceDomains::DeleteByDBID``, ``InterfaceDomain::Delete``, ``PersistentDomain::DeleteObject`` (``hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:46-70``), and ``Collection::DeleteItemByDBID``. The .NET path preserves installed Domains/Domain COM identity/direct activation denial, rechecks live server-admin authentication, treats unknown/stale IDs as no-ops, maps store failure to ``E_FAIL``, and removes only the selected snapshot after a transactional cascade delete succeeds. Data-directory folder deletion remains fenced. Focused coverage ``22/22``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1819 passed, 0 failed, 7 opt-in skips`` (1826 total). Next slice: isolated SQL identity/readback rollback evidence for domain mutations.


Code/test commit ``1778f619d`` completes authenticated existing-row ``Domain.Save()`` UPDATE parity after domain insert ``444d4f777``. Legacy anchors are ``InterfaceDomain::Save`` and ``PersistentDomain::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:167-230``). The .NET path preserves installed Domains/Domain COM identity/direct activation denial, rechecks live server-admin authentication, stages setters on retained items, persists via a parameterized identity-constrained UPDATE with the same anti-spam/limitations bit packing, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Focused coverage ``17/17``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1814 passed, 0 failed, 7 opt-in skips`` (1821 total). Next slice: owner-scoped ``Domains.DeleteByDBID`` plus attached ``Domain.Delete()`` parity.


Code/test commit ``444d4f777`` completes authenticated ``Domains.Add()`` plus new-item ``Domain.Save()`` INSERT parity. Legacy anchors are ``InterfaceDomains::Add`` (``hmailserver/source/Server/COM/InterfaceDomains.cpp:99``), ``InterfaceDomain::Save``, and ``PersistentDomain::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:167-230``). The .NET path preserves installed Domains/Domain COM identity/direct activation denial, rechecks live server-admin authentication, stages legacy defaults on the Add child, stages all 29 setters, persists via a parameterized insert with ``OUTPUT INSERTED.domainid`` including legacy anti-spam/limitations bit packing, retains the failed draft (``E_FAIL``), and appends only the saved snapshot after success. Existing-row Save/Delete remain ``E_NOTIMPL``. Focused coverage ``13/13``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1810 passed, 0 failed, 7 opt-in skips`` (1817 total). Next slice: authenticated existing-row ``Domain.Save()`` UPDATE parity.


Code/test commit ``dbcbc346a`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed rule mutations. The ``SqlServerRuleAdministrationStoreIntegrationTests`` fixture mirrors the legacy ``hm_rules``/``hm_rule_criterias``/``hm_rule_actions`` schema in a disposable isolated database and proves ``OUTPUT INSERTED.ruleid`` identity readback with per-insert increments, owner-scoped UPDATE/DELETE that no-op against foreign account IDs, transactional cascade DELETE removing criteria/action rows before the rule row, and NOT NULL name violations leaving no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused Rules suite is ``113/113``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1805 passed, 0 failed, 7 opt-in skips`` (1812 total). Next slice: next authenticated Admin collection mutation after rules.


Code/test commit ``d7694c227`` completes authenticated existing-row ``Rule.Save()`` UPDATE parity after rule insert ``0239f30a1``. Legacy anchors are ``InterfaceRule::Save`` and ``PersistentRule::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:73-120``). The .NET path preserves installed Rules/Rule COM identity/direct activation denial, rechecks live account/server authentication, stages setters on retained items, persists via a parameterized owner-scoped UPDATE (``WHERE ruleid AND ruleaccountid``), maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot in the generation after success. Focused coverage ``112/112``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1805 passed, 0 failed, 6 opt-in skips`` (1811 total). Next slice: isolated SQL identity/readback rollback evidence for rule mutations.


Code/test commit ``0239f30a1`` completes authenticated account-owned ``Rules.Add()`` plus new-item ``Rule.Save()`` INSERT parity. Legacy anchors are ``InterfaceRules::Add`` (``hmailserver/source/Server/COM/InterfaceRules.cpp:91``), ``InterfaceRule::Save``, and ``PersistentRule::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:73-120``). The .NET path preserves installed Rules/Rule COM identity/direct activation denial, rechecks live account/server authentication, stages legacy defaults on the Add child, stages all four setters, persists via a parameterized insert with ``OUTPUT INSERTED.ruleid``, retains the failed draft (``E_FAIL``), and publishes only the saved snapshot into the generation after success. Existing-row Save/setters remain ``E_NOTIMPL``. Focused coverage ``108/108``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1801 passed, 0 failed, 6 opt-in skips`` (1807 total). Next slice: authenticated existing-row ``Rule.Save()`` UPDATE parity.


Code/test commit ``ad97b391b`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed route mutations. The ``SqlServerRouteAdministrationStoreIntegrationTests`` fixture mirrors the legacy ``hm_routes``/``hm_routeaddresses`` schema in a disposable isolated database and proves ``OUTPUT INSERTED.routeid`` identity readback with per-insert increments, identity-preserving UPDATE with BlowFish password round-trip, cascade DELETE removing route-address rows before the route row, unknown-ID UPDATE/DELETE returning false with rows intact, and NOT NULL violations leaving no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused Routes suite is ``36/36``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1796 passed, 0 failed, 6 opt-in skips`` (1802 total). Next slice: next authenticated Admin collection mutation after routes.


Code/test commit ``24510aafa`` completes authenticated owner-scoped ``Routes.DeleteByDBID`` plus attached ``Route.Delete()`` parity, completing the route mutation trio (insert ``264995c17``, update ``84135364e``, delete). Legacy anchors are ``InterfaceRoutes::DeleteByDBID``, ``InterfaceRoute::Delete`` (``hmailserver/source/Server/COM/InterfaceRoute.cpp:582``), ``PersistentRoute::DeleteObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:31-40``), and ``Collection::DeleteItemByDBID`` (``hmailserver/source/Server/Common/BO/Collection.h:181-200``). The .NET path preserves installed Routes/Route COM identity/direct activation denial, rechecks live server-administrator authentication, treats unknown/stale IDs as no-ops, maps store failure to ``E_FAIL``, and removes only the selected snapshot after the cascaded route-address plus route delete. Focused coverage ``35/35``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1796 passed, 0 failed, 5 opt-in skips`` (1801 total). Next slice: isolated SQL identity/readback rollback evidence for route mutations.


Code/test commit ``84135364e`` completes authenticated existing-row ``Route.Save()`` UPDATE parity after route insert ``264995c17``. Legacy anchors are ``InterfaceRoute::Save`` (``hmailserver/source/Server/COM/InterfaceRoute.cpp:243``) and ``PersistentRoute::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:53-88``). The .NET path preserves installed Routes/Route COM identity/direct activation denial, rechecks live server-administrator authentication, stages setters on retained items including ``SetRelayerAuthPassword``, persists via a parameterized identity-constrained UPDATE, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Focused coverage ``30/30``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1791 passed, 0 failed, 5 opt-in skips`` (1796 total). Next slice: owner-scoped ``Routes.DeleteByDBID`` plus attached ``Route.Delete()`` parity.


Code/test commit ``264995c17`` completes authenticated ``Routes.Add()`` plus new-item ``Route.Save()`` INSERT parity. Legacy anchors are ``InterfaceRoutes::Add`` (``hmailserver/source/Server/COM/InterfaceRoutes.cpp``), ``InterfaceRoute::Save`` (``hmailserver/source/Server/COM/InterfaceRoute.cpp:243``), and ``PersistentRoute::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:53-88``). The .NET path preserves installed Routes/Route COM identity/direct activation denial, rechecks live server-administrator authentication on Add/setters/Save, stages legacy defaults on the Add child, stages all twelve setters including ``SetRelayerAuthPassword``, persists via a parameterized insert with ``OUTPUT INSERTED.routeid`` (BlowFish-encrypted relayer password), retains the failed draft on store failure (``E_FAIL``), and appends only the saved snapshot after success. Existing-row Save and Delete remain ``E_NOTIMPL``. Focused coverage ``26/26``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1787 passed, 0 failed, 5 opt-in skips`` (1792 total). Next slice: authenticated existing-row ``Route.Save()`` UPDATE parity.


Code/test commit ``36270f965`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed distribution-list recipient COM mutations (INSERT/UPDATE/DELETE). The new ``SqlServerDistributionListRecipientAdministrationStoreIntegrationTests`` fixture mirrors the legacy schema (`hmailserver/source/DBScripts/CreateTablesMSSQL.sql:309-340`) in a disposable isolated database and proves ``OUTPUT INSERTED.distributionlistrecipientid`` identity readback with per-insert increments, owner-scoped UPDATE/DELETE predicates that no-op against foreign list IDs, identity-preserving UPDATE, and statement-level rollback where NOT NULL address violations leave no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused recipient suite is ``24/24``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1782 passed, 0 failed, 5 opt-in skips`` (1787 total) with the SQL connection unset. With the SQL connection set, the previously skipped message-indexing opt-in fixtures execute for the first time in this environment and surface ``5`` pre-existing failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Next slice: next authenticated Admin collection mutation.


Code/test commit `20ec7a285` completes authenticated owner-scoped distribution-list recipient `DeleteByDBID` plus attached `DistributionListRecipient.Delete()` parity after `259cf0867`. Legacy anchors are `hmailserver/source/Server/COM/InterfaceDistributionListRecipients.cpp:37-51`, `InterfaceDistributionListRecipient.cpp:115-137`, `hmailserver/source/Server/Common/BO/Collection.h:181-200`, and `hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:25-43`; the schema is `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:331-340`. The .NET path preserves recipient COM identity/direct activation denial, rechecks live owner authentication, treats unknown/foreign/stale IDs as no-ops, constrains deletion by recipient ID and owning list ID, maps store failure to `E_FAIL`, retains the owner snapshot on failure, and removes only the selected snapshot after success. Focused coverage is `20/20`; the full suite excluding the two AV-locked EICAR cleanup methods is `1782 passed, 0 failed, 4 opt-in skips` (1786 total). Direct full execution remains environment-blocked by those unrelated scanner cleanup failures. SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Next slice: isolated SQL identity/readback and rollback evidence for completed COM mutations. No production resource was used.

Code/test commit `259cf0867` completes authenticated owner-scoped existing-row `DistributionListRecipient.Save()` UPDATE parity after `91645dc3a`. Legacy anchors are `hmailserver/source/Server/COM/InterfaceDistributionListRecipient.cpp:133-157` and `hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:103-139`; the schema is `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:331-340`. The .NET path preserves the installed recipient COM identity/direct activation denial, rechecks live owner authentication, stages `RecipientAddress`, constrains SQL UPDATE by recipient ID and owning list ID, retains staged state and the owner snapshot on failure, and replaces only the matching snapshot after success. Focused coverage is `15/15`; the full suite excluding the two AV-locked EICAR cleanup methods is `1777 passed, 0 failed, 4 opt-in skips` (1781 total). Direct full execution remains environment-blocked by those unrelated scanner cleanup failures. Recipient deletion, SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Next slice: authenticated owner-scoped recipient Delete/DeleteByDBID parity. No production resource was used.

Code/test commit `f2d33c348` completes authenticated domain-owned `DistributionLists.Add()` plus new-item `DistributionList.Save()` INSERT parity. Legacy anchors are `hmailserver/source/Server/COM/InterfaceDomain.cpp:574-603`, `InterfaceDistributionLists.cpp:55-84`, `InterfaceDistributionList.cpp:81-277`, and `hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:118-157`. The .NET path preserves the installed distribution-list COM identity and direct activation `E_ACCESSDENIED`, passes live domain authorization into the child facade, stages the five editable fields, binds the draft to its owning domain, uses parameterized `OUTPUT INSERTED.distributionlistid`, retains failed drafts, and appends only after successful persistence. Focused coverage is `14 passed`; the full suite excluding the two AV-locked EICAR cleanup methods is `1755 passed, 0 failed, 4 opt-in skips`; direct full execution remains environment-blocked by those two unrelated cleanup failures. Existing-row update/delete, recipients mutation, live SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Next slice: authenticated existing-row `DistributionList.Save()` UPDATE parity, then deletion/recipient mutation as separately verified slices. No production resource was used.

Code/test commit `852aa1586` completes the authenticated existing-row `DistributionList.Save()` UPDATE parity after `f2d33c348`. Legacy `InterfaceDistributionList::Save` (`hmailserver/source/Server/COM/InterfaceDistributionList.cpp:252-271`) delegates to `PersistentDistributionList::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:118-157`), which updates all six `hm_distributionlists` fields by `distributionlistid`. The .NET path preserves the installed COM identity and direct activation denial, stages updates on the owning facade, rechecks live authorization, uses a parameterized identity-constrained UPDATE, retains staged state and the owner snapshot on failure, and replaces only the matching owner snapshot after success. Focused coverage is `20/20`; the full suite excluding the two AV-locked EICAR cleanup methods is `1761 passed, 0 failed, 4 opt-in skips`; direct full execution remains environment-blocked by those unrelated scanner cleanup failures. Delete, recipients mutation, live SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Next slice: authenticated owner-scoped `DistributionLists.DeleteByDBID` plus attached `DistributionList.Delete()` parity. No production resource was used.

Code/test commit `fb6de84f7` completes authenticated owner-scoped `DistributionLists.DeleteByDBID` plus attached `DistributionList.Delete()` parity. Legacy `InterfaceDistributionLists::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceDistributionLists.cpp:38-53`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), while `PersistentDistributionList::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:35-54`) cleans `hm_distributionlistsrecipients` first and then deletes the parent row. The .NET path preserves installed COM identity and direct activation denial, live authorization, owner containment, unknown/stale no-op behavior, failed-delete snapshot retention, and post-success removal only. Focused coverage is `27/27`; the full suite excluding the two AV-locked EICAR cleanup methods is `1768 passed, 0 failed, 4 opt-in skips`; direct full execution remains environment-blocked by those unrelated scanner cleanup failures. Recipient COM mutation, SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Next slice: authenticated owner-scoped distribution-list recipient mutation. No production resource was used.

Code/test commit `91645dc3a` completes authenticated owner-scoped distribution-list recipient `Add()` plus new-item `DistributionListRecipient.Save()` INSERT parity. Legacy anchors are `hmailserver/source/Server/COM/InterfaceDistributionListRecipients.cpp:53-83`, `InterfaceDistributionListRecipient.cpp:93-157`, and `hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:103-139`; the schema is `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:331-340`. The .NET path preserves installed recipient COM identity/direct activation denial, passes the owning list’s live auth callback into recipient facades, stages raw `RecipientAddress`, binds the owner list ID, uses parameterized `OUTPUT INSERTED.distributionlistrecipientid`, retains failed drafts, denies retained reads/mutations after auth loss, and appends only after success. Focused coverage is `11/11`; the full suite excluding the two AV-locked EICAR cleanup methods is `1773 passed, 0 failed, 4 opt-in skips`; direct full execution remains environment-blocked by those unrelated scanner cleanup failures. Existing-row recipient update/delete, SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Next slice: authenticated existing-row `DistributionListRecipient.Save()` UPDATE parity. No production resource was used.

The following entries are historical.

Code/test commit `b8025f2fe` completes authenticated owner-scoped SURBL deletion parity. Legacy `InterfaceSURBLServers::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceSURBLServers.cpp:88-101`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), which deletes only a contained ID through `PersistentSURBLServer::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:25-33`); unknown IDs are successful COM no-ops. Legacy `InterfaceSURBLServer::Delete` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:187-208`) rechecks server-admin access and routes attached items through the parent. The .NET path preserves installed SURBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, direct activation denial, live administrator reauthentication, stale-item no-op behavior, and owner-snapshot removal only after successful parameterized deletion. Focused SURBL/SQL coverage is `21/21`; the direct full run has `1748 passed, 2 unrelated scanner-runtime cleanup failures, and 4 opt-in skips` (1754 total), while excluding those two pre-existing scanner classes passes `1743` with `4` skips. SQL identity/readback, real COM activation, live SURBL/SMTP behavior, backup/restore, migration, SEC-18, and release gates remain open. Next: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource was used.

Code/test commit `835e73804` completes authenticated existing-row `SURBLServer.Save()` UPDATE parity after `cd627826e`. Legacy `InterfaceSURBLServer::Save` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:11-36`) rechecks server-admin authentication and delegates persistence to `PersistentSURBLServer::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:55-90`), whose existing-row branch updates `surblactive`, `surblhost`, `surblrejectmessage`, and `surblscore` by `surblid`. The .NET path preserves installed SURBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, stages existing-item setters on the owning facade, uses a parameterized identity-constrained UPDATE, rechecks live authorization, retains the staged item and owner snapshot on failure, and replaces only the matching owner snapshot after success. Delete, live SURBL/SMTP behavior, SQL identity/readback, real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Focused SURBL/SQL coverage is `15/15`; full Net10 passes `1744` with `4` opt-in skips. Next: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource was used.

Code/test commit `cd627826e` completes authenticated `SURBLServers.Add()` plus new-item `SURBLServer.Save()` INSERT parity. Legacy `InterfaceSURBLServers::Add` (`hmailserver/source/Server/COM/InterfaceSURBLServers.cpp:134-163`) creates an ID-zero child; `InterfaceSURBLServer::Save` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:11-36`) rechecks server-admin authentication and publishes only after `PersistentSURBLServer::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:55-90`) assigns the generated `surblid`. The .NET path preserves installed SURBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, stages Active/DNSHost/RejectMessage/Score, uses parameterized `OUTPUT INSERTED.surblid`, retains failed drafts, and appends only the new owner snapshot after successful insert. Existing-row mutation, Delete, live SURBL/SMTP behavior, SQL identity/readback, real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Focused SURBL/SQL coverage is `12/12`; full Net10 passes `1741` with `4` opt-in skips. Next: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource was used.

Code/test commit `811ce0300` completes authenticated owner-scoped `DNSBlackLists.DeleteByDBID` plus attached `DNSBlackList.Delete()` parity after `f6033de3b` completed existing-row UPDATE. Legacy `InterfaceDNSBlackLists::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:91-106`) delegates to the owning collection and returns success for unknown IDs; `InterfaceDNSBlackList::Delete` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:221-242`) rechecks server-admin authentication and routes attached objects through the parent, while persistence is `PersistentDNSBlackList::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:25-32`). The .NET path preserves installed DNSBL COM identity and direct activation denial, treats unknown IDs as no-ops, maps failed deletion to `E_FAIL`, retains the owner snapshot on failure, and removes only the selected row after success. Focused DNSBlackLists/SQL coverage is `20/20`; full Net10 passes `1737` with `4` opt-in skips. Live DNSBL reconfiguration, SMTP trust, SQL identity/readback, real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Next: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource was used.

Code/test commit `f6033de3b` completes authenticated existing-row `DNSBlackList.Save()` UPDATE parity after `e956dcd3d` completed Add/INSERT. Legacy `InterfaceDNSBlackList::Save` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:14-37`) rechecks server-admin authentication and routes the attached object through `PersistentDNSBlackList::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:55-90`), which updates the five `hm_dnsbl` fields by `sblid`. The .NET path preserves the installed DNSBL COM identity and direct activation boundary, stages all five setters on an attached owner-scoped facade, uses parameterized identity-constrained SQL, rechecks live administrator access at mutation and Save, and replaces only the matching owner snapshot after a successful update. Focused DNSBlackLists/SQL coverage is `16/16`; full Net10 passes `1733` with `4` opt-in skips. Existing-row Delete, live DNSBL reconfiguration, SMTP trust, SQL identity/readback, real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Next: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource was used.

Code/test commit `e956dcd3d` completes authenticated `DNSBlackLists.Add()` plus new-item `DNSBlackList.Save()` INSERT parity. Legacy `InterfaceDNSBlackLists::Add` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:138-165`) creates an ID-zero child attached to the owning collection; `InterfaceDNSBlackList::Save` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:14-34`) persists first and publishes only after `PersistentDNSBlacklist::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:55-90`) assigns the generated `sblid`. The .NET path preserves installed DNSBlackLists/DNSBlackList IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, stages Active/DNSHost/RejectMessage/ExpectedResult/Score, uses parameterized `OUTPUT INSERTED.sblid`, retains failed drafts, and appends only the new owner snapshot after successful insert. The authenticated `Settings -> AntiSpam -> DNSBlackLists` boundary is preserved; existing-row mutation, Delete, live DNSBL reconfiguration, SMTP trust, SQL identity/readback, real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Focused DNSBlackLists/SQL coverage is `12/12`; full Net10 passes `1729` with `4` opt-in skips. Next: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, implement DNSBlackList existing-row Save UPDATE parity. No production resource was used.

Code/test commit `fdfdc6c42` completes authenticated owner-scoped `GreyListingWhiteAddresses.DeleteByDBID` plus attached `GreyListingWhiteAddress.Delete()` parity after `6ba86e16b` completed existing-row UPDATE. Legacy `InterfaceGreyListingWhiteAddresses::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddresses.cpp:85-102`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), and `InterfaceGreyListingWhiteAddress::Delete` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:117-137`) rechecks Administrator authentication before routing attached items through the owner; persistence is `PersistentGreyListingWhiteAddress::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:26-32`). The .NET path preserves the installed COM contract and direct activation denial (`hmailserver/source/Server/hMailServer/hMailServer.idl:2356-2387`), treats unknown IDs as no-ops, retains the owner snapshot on failed deletion, and removes only the selected snapshot after success. Focused coverage is `21/21`; full Net10 passes `1725` with `4` opt-in skips. Next: approved disposable SQL Group/member readback and rollback evidence; if unavailable, DNSBlackLists Add/Save INSERT parity. SQL readback, real COM activation, greylisting live reconfiguration, SMTP socket E2E, rollback injection, backup/restore, migration, SEC-18, and performance/release gates remain open. No production resource was used.

Code/test commit `6ba86e16b` completes authenticated existing-row `GreyListingWhiteAddress.Save()` UPDATE parity after `b31ce86c1` completed Add/INSERT. Legacy `InterfaceGreyListingWhiteAddress::Save` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:10-31`) invokes `PersistentGreyListingWhiteAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:52-84`), whose existing-row branch updates `whiteipaddress` and `whiteipdescription` by `whiteid`; the legacy setter still accepts invalid user-editable IP text through `GreyListingWhiteAddress::SetUserEditableIPAddress` (`hmailserver/source/Server/Common/BO/GreyListingWhiteAddress.cpp:64-74`). The .NET path preserves the installed COM contract and direct activation denial (`hmailserver/source/Server/hMailServer/hMailServer.idl:2356-2387`), owner-scoped staged setters, live Administrator reauthentication, and post-success replacement of only the matching parent snapshot. Focused coverage is `17/17`; full Net10 passes `1721` with `4` opt-in skips. Next: approved disposable SQL Group/member readback and rollback evidence; if unavailable, greylisting white-address DeleteByDBID/item Delete parity. SQL readback, real COM activation, greylisting live reconfiguration, SMTP socket E2E, rollback injection, backup/restore, migration, SEC-18, and performance/release gates remain open. No production resource was used.

Code/test commit `b31ce86c1` completes authenticated `GreyListingWhiteAddresses.Add()` plus new-item `GreyListingWhiteAddress.Save()` INSERT parity. Legacy `InterfaceGreyListingWhiteAddresses::Add` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddresses.cpp:162-187`) returns an ID-zero child; `InterfaceGreyListingWhiteAddress::Save` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:10-31`) rechecks server-admin authentication and publishes only after `PersistentGreyListingWhiteAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:52-84`) assigns `whiteid`. The legacy setter stores user-editable wildcard text through `GreyListingWhiteAddress::SetUserEditableIPAddress` (`hmailserver/source/Server/Common/BO/GreyListingWhiteAddress.cpp:64-74`) and `SQLStatement::ConvertWildcardToLike` (`hmailserver/source/Server/Common/SQL/SQLStatement.cpp:591-610`) without IP validation; invalid text is therefore retained for Save. The installed COM contract remains unchanged (`hmailserver/source/Server/hMailServer/hMailServer.idl:2356-2387`), including direct activation denial. Focused GreyListingWhiteAddresses/SQL coverage is `14/14`; full Net10 passes `1718` with `4` opt-in skips. Next: approved disposable SQL Group/member readback and rollback evidence; if unavailable, greylisting white-address existing-row Save parity. SQL readback, real COM activation, greylisting live reconfiguration, SMTP socket E2E, rollback injection, backup/restore, migration, SEC-18, and performance/release gates remain open. No production resource was used.

Code/test commit `ae6239f54` completes authenticated owner-scoped `BlockedAttachments.DeleteByDBID` plus attached `BlockedAttachment.Delete()` parity after `2324b0131` completed existing-row UPDATE. Legacy `InterfaceBlockedAttachments::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceBlockedAttachments.cpp:76-89`) delegates membership to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), and `InterfaceBlockedAttachment::Delete` (`hmailserver/source/Server/COM/InterfaceBlockedAttachment.cpp:122-141`) rechecks server-admin authentication and routes attached objects through the parent; direct objects use `PersistentBlockedAttachment::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentBlockedAttachment.cpp:26-32`). The installed COM contract remains unchanged (`hmailserver/source/Server/hMailServer/hMailServer.idl:2285-2312`), including direct activation denial. The .NET path treats unknown IDs as no-ops, rechecks retained-item authorization, maps store failure to `E_FAIL`, and removes only the matching owner snapshot after store success. Focused coverage is `19/19`; full Net10 passes `1714` with `4` opt-in skips. SQL readback, real COM activation, scanner/live reconfiguration, and rollback injection remain open. Next: approved disposable SQL Group/member evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource was used.

Code/test commit `298088e31` fixes the offline synthetic SEARCH/SORT benchmark to report the measured `ActualMatchCount` and validate expected-versus-actual count equality. Focused benchmark tests pass `3/3`, and full Net10 passes `1618` with `4` opt-in skips. Legacy parser/sort anchors remain documented, but this harness still does not prove SQL FTS, live IMAP concurrency, legacy C++ equivalence, or leak soak. Next: approved disposable SQL parent/UID evidence, the bounded SMTP whitelist bypass map, and performance acceptance beyond offline synthetic SEARCH/SORT. No production resource was used.

Code/test commit `008e949dd` adds focused false-result and exception-path coverage for external-fetch missing-known-UID cleanup. Legacy `FetchAccountUIDList::RemoveMissingUIDs` and `PersistentFetchAccountUID::DeleteObject` (`hmailserver/source/Server/ExternalFetcher/FetchAccountUIDList.cpp:90-123`; `hmailserver/source/Server/Common/Persistence/PersistentFetchAccountUID.cpp:74-85`) establish that a false persistence result is not counted and a store exception fails the account; the current tests lock that behavior without changing production code. Focused ExternalFetchProcessor tests pass `34/34`, and full Net10 passes `1617` with `4` opt-in skips. Next: approved disposable SQL parent/UID readback and rollback evidence, then the bounded SMTP whitelist bypass map and performance acceptance expansion. No production resource was used.

Code/test commit `a96ee1d10` completes account-owned message-writer account-size invalidation. This matches legacy `MailImporter::Import` and external-fetch `POP3ClientConnection::SaveMessage_`, which persist account-owned delivered messages through `PersistentMessage::SaveObject` and increment `AccountSizeCache` (`hmailserver/source/Server/Common/Util/MailImporter.cpp:39-205`; `hmailserver/source/Server/ExternalFetcher/POP3ClientConnection.cpp:910-917`; `hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:505-513`). Append/copy/mutation/local/script/import writers are covered; queue import remains account 0. Focused import/Host tests pass `8/8`, and full Net10 passes `1615` with `4` opt-in skips. GETQUOTAROOT is verified existing parity. Next: approved disposable SQL callback/readback and rollback evidence, then FetchAccountUID ordering/failure evidence and the bounded SMTP whitelist bypass map. No production resource was used.

## Prerequisites

- .NET 10 SDK.
- .NET 10 WindowsDesktop runtime for the COM compatibility assembly and Windows service target.
- Visual Studio 2022 or Build Tools 17.x with the C++ build tools and a Windows SDK/MIDL when building Windows service/COM artifacts.
- SQL Server with Full-Text Search installed and enabled.
- A configured hMailServer data directory path (`DataDirectory` or `HMAILSERVER_DATA_DIRECTORY`).

Run the local prerequisite check from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\check-net10-prereqs.ps1 -RequireMsBuild
```

Build and test the .NET 10 track from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build-net10.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\test-net10.ps1
```

The service build generates `hMailServer.tlb` from the authoritative legacy IDL and places it beside `hMailServer.exe`. Explicit service installation and removal are available for controlled, elevated test environments:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\install-net10-service.ps1 -Configuration Debug
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\uninstall-net10-service.ps1 -Configuration Debug
```

Installation registers the legacy AppID, CLSIDs, versioned/version-independent ProgIDs, CurVer aliases, LocalServer32 paths, and the 64-bit type library. It refuses to replace an existing `hMailServer` service unless `-ReplaceExisting` is explicitly supplied. When replacement targets a stopped service that points to a different executable, `-BackupArchive <path>` is also required and is checked with the packaged shell-free `7za.exe` archive test plus bounded metadata validation before COM registration or Service Control Manager changes. Use replacement only in a controlled, non-production compatibility environment. Build and test commands never mutate the registry or Service Control Manager.

## Current Next Slice

Authoritative update (2026-08-04, code/test commit `0ecde173c`): account-size invalidator registration is now reconciled per `Accounts` collection owner. Legacy `AccountSizeCache::Reset(accountId)` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:198-209`) is reflected by removing only IDs absent from that collection's refresh snapshot; re-added IDs receive a new generation and retained IDs do not force a readback. This preserves the stable host invalidator used by repeated Account facades and `Links.get_Account`, without changing COM identity or authenticated boundaries. Focused Accounts coverage passes `39/39`; full Net10 passes `1609` with `4` opt-in skips. Runtime owner cleanup after object lifetime, disposable SQL callback/readback and rollback, non-IMAP writers, FetchAccountUID SQL cleanup, and performance/soak evidence remain open. Next: verify the legacy GETQUOTAROOT no-quota/domain-limit/mailbox-quoting slice.

Authoritative update (2026-08-04, code/test commit `ba78cca9b`): the bounded EXPUNGE post-commit account-size invalidation seam is complete. Legacy `IMAPCommandExpunge::ExecuteCommand` (`hmailserver/source/Server/IMAP/IMAPCommandExpunge.cpp:24`), `Messages::DeleteMessages` (`hmailserver/source/Server/Common/BO/Messages.cpp:116`), and `PersistentMessage::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:49-95`) remove selected messages and adjust the lazy `AccountSizeCache` (`hmailserver/source/Server/Common/Cache/AccountSizeCache.cpp:27-72`). The .NET `SqlServerImapMessageMutationStore` accepts an optional callback and invokes it once after successful EXPUNGE SQL commit, before best-effort file cleanup; zero-row and failed/canceled paths do not invoke it. Focused EXPUNGE coverage passes `6/6`; full Net10 passes `1597` with `4` opt-in skips. The seam introduces no cache/schema change and remains unwired in the default Host composition, so live Account.Size refresh and disposable SQL callback/readback remain open. Next: bounded no-schema account-size invalidator ownership/wiring for the existing APPEND/COPY/EXPUNGE seams.

Authoritative update (2026-08-03, code/test commit `6a415afd1`): IMAP `GETQUOTA`/`GETQUOTAROOT` usage now matches the legacy all-message aggregate. Legacy `AccountSizeCache::GetSize`/`PersistentAccount::GetMessageBoxSize` (`hmailserver/source/Server/Common/Cache/AccountSizeCache.cpp:59-72`; `hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:341-355`) sums every `hm_messages.messagesize` row for the authenticated account, while the previous Net10 query incorrectly restricted usage to `messagetype = 2`. The .NET SQL now uses account-scoped `COALESCE(SUM(m.messagesize), 0)`; existing byte-to-KB truncation and account-limit conversion remain unchanged. Focused QUOTA coverage passes `5/5`; full Net10 passes `1584` with `4` opt-in skips. Stateful AccountSizeCache lifecycle, quota admission, live SQL mixed-type readback, and performance acceptance remain open. Next: isolate legacy `GETQUOTAROOT` no-quota response/domain-limit and mailbox-quoting parity; then return to the AccountSize semantic/writer matrix and post-commit invalidation seam.

Authoritative update (2026-08-03, code/test commit `1c201c3c4`): authenticated `FetchAccounts.Add()` plus new-item `FetchAccount.Save()` INSERT parity is complete. Legacy `InterfaceFetchAccounts::Add` (`hmailserver/source/Server/COM/InterfaceFetchAccounts.cpp:139-165`) creates an ID-zero child bound to the owning account; `InterfaceFetchAccount::Save` (`hmailserver/source/Server/COM/InterfaceFetchAccount.cpp:341-360`) persists first and publishes only after `PersistentFetchAccount::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccount.cpp:153-207`) assigns the generated `faid`. The .NET path preserves FetchAccounts/FetchAccount IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial; stages the new account fields, encrypts the staged password with the legacy Blowfish format, enforces the owning account at the runtime store boundary, uses parameterized `OUTPUT INSERTED.faid`, retains failed drafts, and appends only after successful insert. Existing-row mutation, FetchAccountUID mutation/cleanup, retry scheduling, live external-fetch behavior, and sibling-facade freshness remain open. Focused FetchAccounts/SQL coverage passes `27/27`; full Net10 passes `1584` with `4` opt-in skips. Next: approved disposable SQL identity/readback plus injected rollback evidence remains environment-blocked; then produce the AccountSize semantic/writer matrix and implement only a no-schema post-commit invalidation seam, keeping quota admission out of scope.

Authoritative update (2026-08-03, code/test commit `cc53c77eb`): authenticated `WhiteListAddresses.Clear()` parity is complete. Legacy `InterfaceWhiteListAddresses::Clear` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:42-61`) requires an attached authenticated collection and delegates to `Collection::DeleteAll`, while `PersistentWhiteListAddress::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:27-34`) removes each `hm_whitelist` row. The .NET path preserves the installed COM contract and direct activation denial, performs a scoped `DELETE FROM hm_whitelist`, empties the owner snapshot only after store success, and maps failure to `E_FAIL` without clearing the retained snapshot. Focused whitelist/SQL coverage passes `27/27`; full Net10 passes `1580` with `4` opt-in skips. SMTP evaluator/cache invalidation, sibling-facade freshness, and broader Admin mutation remain open. Next: approved disposable SQL identity/readback plus injected rollback evidence; then IMAP account-size/cache lifecycle parity and isolated FetchAccounts SQL/UID evidence.

Authoritative update (2026-08-03, code/test commit `cd91d276a`): authenticated whitelist deletion parity is complete. Legacy `InterfaceWhiteListAddresses::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:109-124`) delegates to owner-scoped `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), while `InterfaceWhiteListAddress::Delete` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:33-54`) rechecks server-admin authorization and uses its owning collection for attached items. `PersistentWhiteListAddress::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:27-34`) deletes by `whiteid`. The .NET path preserves whitelist IIDs/CLSIDs/ProgIDs/DISPIDs and direct activation denial, uses parameterized `DELETE FROM hm_whitelist WHERE whiteid = @id`, removes only after a successful store result, maps persistence failure to `E_FAIL`, and retains unknown/foreign/stale snapshots as no-ops. Focused whitelist/SQL coverage passes `24/24`; full Net10 passes `1577` with `4` opt-in skips. SMTP evaluator/cache invalidation, sibling-facade freshness, Clear, and broader Admin mutation remain open. Next: approved disposable SQL identity/readback plus injected rollback evidence; then IMAP account-size/cache lifecycle parity and isolated FetchAccounts SQL/UID evidence.

Authoritative update (2026-08-03, code/test commit `d79b84ae8`): authenticated existing-row `WhiteListAddress.Save()` UPDATE parity is complete. Legacy `InterfaceWhiteListAddress::Save` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:8-31`) persists the attached object through `PersistentWhiteListAddress::SaveObject`; the persistence path (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:57-93`) updates the existing `hm_whitelist` row by `whiteid`, writes the four IP columns plus email/description, and requests the legacy whitelist-cache refresh. The .NET path preserves the installed whitelist IID/CLSIDs/ProgIDs/DISPIDs and authenticated/direct-activation boundaries, stages setters on the retained item, uses parameterized `UPDATE hm_whitelist ... WHERE whiteid = @id`, and replaces only the matching owner snapshot after successful store completion. Invalid IP setters retain the prior parsed value; failed updates preserve both the staged item and owner snapshot. Focused whitelist/SQL coverage passes `22/22`; full Net10 passes `1573` with `4` opt-in skips. SMTP evaluator/cache invalidation, sibling-facade freshness, Delete/Clear, and broader Admin mutation remain open. Next: approved disposable SQL identity/readback plus injected rollback evidence; then IMAP account-size/cache lifecycle parity and isolated FetchAccounts SQL/UID evidence.

The preceding security follow-up code/test commit `3165b3cab` closed the retained-collection reauthentication gap in the whitelist slice: `WhiteListAddresses.Add()` now rechecks the live administrator predicate before creating a staged child, matching legacy `InterfaceWhiteListAddresses::Add`. Its focused/full evidence was `16/16` and `1569` passed with `4` opt-in skips. Whitelist-cache/SMTP evaluator integration and sibling-facade snapshot freshness remain explicit blockers outside this bounded persistence slice.

Authoritative update (2026-08-03, code/test commit `5ff2f4ab7`): authenticated `AntiSpam.WhiteListAddresses.Add()` and new-item `WhiteListAddress.Save()` INSERT parity is complete. Legacy `InterfaceWhiteListAddresses::Add` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:186-215`) creates an ID-zero item attached to the owning collection; `InterfaceWhiteListAddress::Save` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:8-31`) requires server-admin authentication, calls `PersistentWhiteListAddress::SaveObject`, and attaches only after the persistence call; `PersistentWhiteListAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:57-93`) writes the `hm_whitelist` IP, email, and description columns, assigns the generated ID, and requests whitelist-cache refresh. The .NET path preserves installed whitelist IID/CLSIDs/ProgIDs/DISPIDs and direct activation denial, rechecks the live administrator predicate before Save, stages invalid IP setters without replacing the legacy value, performs parameterized `OUTPUT INSERTED.whiteid` insertion, and appends only after success. Existing-row update, Delete/Clear, SMTP trust/cache/live reconfiguration, and other Admin collections remain out of scope. Focused whitelist/SQL coverage passes `16/16`; full Net10 has `1569` passes, `0` failures, and `4` opt-in skips. Next: approved disposable SQL identity/readback plus injected rollback evidence for IMAP deletion/Rules/ACL; then whitelist existing-row Save parity and IMAP account-size/cache lifecycle evidence. Protocol DELETE/RENAME, failed-file-cleanup repair/alert semantics, restore/upgrade rollback, SEC-18, and release performance gates remain open.

Authoritative update (2026-08-03, code/test commit `1a98e88a8`): legacy IMAP `SUBSCRIBE`/`UNSUBSCRIBE` protocol parity is complete. `IMAPCommandSUBSCRIBE::ExecuteCommand` (`hmailserver/source/Server/IMAP/IMAPCommandSubscribe.cpp:23-67`) requires authentication, accepts the first argument, silently accepts the public-folder root, requires lookup permission for other folders, and persists `folderissubscribed = 1`; `IMAPCommandUNSUBSCRIBE::ExecuteCommand` and `ConfirmPossibleToUnsubscribe` (`hmailserver/source/Server/IMAP/IMAPCommandUnsubscribe.cpp:23-67`) require one argument, reject missing/public folders, and persist `folderissubscribed = 0` for private folders. The .NET path adds a narrow `IImapMailboxSubscriptionStore`, reuses the existing SQL path parser and public-folder ACL lookup boundary, updates only the owner-scoped `hm_imapfolders` row, and dispatches both commands only for an authenticated IMAP session. COM identities, COM access boundaries, SMTP, IMAP DELETE/RENAME, and live reconfiguration remain unchanged. Focused subscription/session/store coverage passes `46/46`; full Net10 has `1564` passes, `0` failures, and `4` opt-in skips. Next: approved disposable SQL identity/readback plus injected rollback evidence for IMAP deletion/Rules/ACL; then IMAP account-size/cache lifecycle parity and isolated FetchAccounts SQL/UID evidence. Protocol DELETE, failed-file-cleanup repair/alert semantics, restore/upgrade rollback, SEC-18, and release performance gates remain open. The prior IMAP deletion update is historical below.

The paragraph immediately below is historical; it records the preceding RuleActions Add/Save slice.

Authoritative update (2026-08-01, `ce80fad48`): authenticated `RuleActions.Add()` and new-item `RuleAction.Save()` INSERT parity is complete. Legacy `InterfaceRuleActions::Add` (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:93-119`) returns an ID-zero child scoped to the owning rule; `InterfaceRuleAction::Save` (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:30-72`) assigns the next sort order, enforces the ScriptFunction administrator guard, persists, receives the generated ID, and attaches only after success; `PersistentRuleAction::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleAction.cpp:65-116`) inserts the complete `hm_rule_actions` row. The .NET path uses a typed `OUTPUT INSERTED.actionid` INSERT, immutable owning-rule binding, staged new-item fields, failure retention/retry, and post-success snapshot publication without changing installed COM identity/vtable/DISPID shape or direct activation denial. Focused RuleActions/store coverage passes `56/56`; full Net10 passes `1550` with `4` opt-in skips. Disposable SQL identity/readback, real COM activation, retained-facade reauthentication, concurrent sort allocation, SMTP rule execution, rollback fault injection, and end-to-end delivery evidence remain open. Next independent slices: disposable SQL insert/readback for RuleActions and existing ACL/deletion paths; hardened IMAP folder deletion COM/store wiring; isolated FetchAccounts SQL/UID cleanup evidence.

Authoritative update (2026-08-01, `808692ef5` following `d5b25e701`): implemented authenticated existing-row public-folder ACL `Save()` UPDATE parity. Legacy `InterfaceIMAPFolderPermission` setters/bit setters and `Save` (`hmailserver/source/Server/COM/InterfaceIMAPFolderPermission.cpp:75-208,227-242`) stage the complete ACL row; `PersistentACLPermission::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentACLPermission.cpp:76-164`) updates `aclsharefolderid`, type, group ID, account ID, and value by identity after holder validation. The .NET path propagates owner-captured update delegates through index, DBID, and name wrappers, enforces `aclid`, `aclsharefolderid`, and public-folder `folderaccountid = 0` predicates, maps false/exception to `E_FAIL`, and replaces snapshots only after success. Existing insert behavior, direct activation/public-folder boundaries, and installed COM identity/vtable/DISPID shape remain unchanged. Focused IMAP permission/store tests pass `36/36`; the full suite passes `1546` with `4` opt-in skips. Live SQL, COM activation, protocol ACL, duplicate-conflict, rollback fault injection, and round-trip evidence remain unproven. Next slice: approved disposable SQL insert/update/readback evidence for ACL Add/Save plus Rules/public-folder deletion, then the hardened IMAP folder and FetchAccounts SQL fixtures.

Authoritative update (2026-08-01, `8e3bf68d8`): authenticated existing-row `IMAPFolder.Subscribed` staging through the owning `Save` path is complete. Legacy `InterfaceIMAPFolder::put_Subscribed` (`hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:144-159`) changes only the attached object before the existing `PersistentIMAPFolder::SaveObject` update. The .NET path stages the boolean only for shared account-owned state, composes it into the existing owner/parent-scoped update, and clears it only after a successful store result; direct activation and snapshot-only facades remain denied/unimplemented. Focused IMAPFolders/SQL coverage passes `19/19`; the full suite passes `1506` with `3` opt-in skips. Live SQL, rollback, concurrency, protocol/cache notifications, deletion, and ACL mutation remain open. Next independent slice: authenticated parent-scoped existing-folder deletion parity.

Authoritative update (2026-08-01, `3f64cd731`): authenticated existing-row `IMAPFolder.Name` staging and owning `Save` update parity is complete. Legacy `InterfaceIMAPFolder::put_Name`/`Save` (`hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:47-67,92-124`) stages Modified UTF-7 in memory, then `PersistentIMAPFolder::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:100-151`) updates `folderaccountid`, `folderparentid`, `foldername`, and `folderissubscribed` by folder identity. The .NET path preserves snapshot-only read facades as read-only, stages names only for account-owned state, uses an owner/parent-scoped parameterized update, and replaces the shared snapshot only after a successful store result. Focused IMAPFolders/SQL coverage passes `18/18`; the full suite passes `1505` with `3` opt-in skips. Live SQL, rollback after post-update snapshot failure, concurrency, protocol/cache notifications, `Subscribed`, delete, and ACL mutation remain open or out of scope. Next independent slice: authenticated existing-row `IMAPFolder.Subscribed` setter through the owning `Save` path.

Authoritative update (2026-08-01, `e073b6ba7`): authenticated account-owned `IMAPFolders.Add` now follows legacy `InterfaceIMAPFolders::Add` (`hmailserver/source/Server/COM/InterfaceIMAPFolders.cpp:165-209`) and `PersistentIMAPFolder::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:100-151`). It encodes Modified UTF-7 names, rejects case-insensitive duplicates in the owning account/parent collection, auto-subscribes public folders, inserts `hm_imapfolders` with generated identity/current UID/creation time, validates the returned owner scope, and appends only the new snapshot to the shared account state. Installed `IInterfaceIMAPFolders`/`IInterfaceIMAPFolder` identity and DISPIDs remain unchanged; direct activation still returns `E_ACCESSDENIED`. Focused COM/SQL coverage passes `15/15`; the full suite has `1500` passes, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips. SQL integration, rollback after a malformed store response, concurrent adds, live protocol/cache notification, `IMAPFolder.Save`, setters, delete, and ACL mutation remain unproven or out of scope. Next independent slice: authenticated existing-row `IMAPFolder.Name` setter plus owning `Save` update parity.

Authoritative update (2026-08-01, `e38372a80`): the restore containment boundary now has an execution-time freshness contract. `BackupRestoreContainmentPreflight.RevalidateAsync` performs a fresh `BackupRestoreIntegrityRuntime.InspectAsync` against the planned archive path, requires valid fresh message-file evidence when `BOMessages` is present, and then runs the existing path/source/target/rollback containment revalidation. Focused restore integrity/preflight coverage passes `100/100`; the full suite has `1498` passes, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips. The tests detect a deleted raw message file and a changed compressed message graph at the same archive path without creating a rollback artifact. Legacy `BackupExecuter::StartRestore` and `RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`) have no equivalent freshness check. This remains an unwired, read-only contract: it does not provide atomicity with future extraction, post-extraction reparse validation, SQL mutation, data-directory replacement, rollback, or round-trip evidence. Next independent slice: isolated disposable SQL restore transaction harness/wiring when approved.

Authoritative update (2026-08-01, `77e6ad723`): read-only restore inspection now validates private-account `Message/@Filename` references against the legacy `PersistentMessage::GetFileName` layout (`hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:1120-1187`), where `Message::XMLStore` stores only the basename (`hmailserver/source/Server/Common/BO/Message.cpp:200-215`) and the account path is `DataBackup/<domain>/<local-part>/<Filename[1..2]>/<Filename>`. Compressed and raw non-DB-only payloads require each referenced file; DB-only metadata validates safe names but permits absent physical files; unreferenced files remain allowed. Focused restore integrity coverage passes `89/89`; the full suite has `1496` passes, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips; excluding those scanner classes, `1491/1491` ran with `3` skips. The slice does not mutate archives or data, does not implement restore, and does not perform execution-time revalidation. Next independent slice: execution-time message/file correspondence revalidation contract; SQL restore transaction wiring and round-trip acceptance remain environment-blocked.

Authoritative update (2026-08-01, `a829d308f`): the offline benchmark pack adds `hmailserver/source/Server.Net10/benchmarks/HMailServer.Net10.Benchmarks`, a deterministic 100,000-message synthetic IMAP SEARCH/SORT runner with JSON/CSV/Markdown output, percentile/throughput/allocation metrics, host/runtime/commit metadata, correctness checks, and focused tests. Legacy `IMAPSearchParser`/`IMAPSortParser`/`IMAPSort` (`hmailserver/source/Server/IMAP/IMAPSearchParser.cpp:118-195`, `IMAPSortParser.cpp:24-52`, `IMAPSort.cpp:108-232,265-326`) has no explicit UID tie-breaker; the current SQL planner (`hmailserver/source/Server.Net10/src/HMailServer.Search.SqlServer/SqlServerImapSortPlanner.cs:23-126`) emits requested criteria plus `m.messageuid ASC`, so the benchmark measures the current deterministic offline contract and records the legacy tie-order limitation. Focused benchmark coverage passes `2/2`; the 100k run produced `9,091` matches with p50 `7.125 ms`, p95 `7.720 ms`, and p99 `7.774 ms` on the local x64 .NET 10 host. The full suite built and ran `1484` passing, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips; excluding those scanner classes, `1479/1479` ran with `3` skips. This does not prove SQL FTS, live IMAP latency, C++ equivalence, or release performance gates. The next independent slice is read-only restore semantic validation for the remaining message/file graph; the approved disposable SQL transaction harness and round-trip acceptance remain environment-blocked. Restore mutation remains `E_NOTIMPL`.

Authoritative update (2026-08-01, `f2d1502ce`): legacy `BackupManager::OnBackupCompleted`/`OnBackupFailed` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:160-194`) dispatch `OnBackupCompleted()` and `OnBackupFailed(reason)` only after the backup task reports its durable outcome; `ScriptServer::FireEvent` (`hmailserver/source/Server/Common/Scripting/ScriptServer.cpp:202-236`) preserves handler names and optional-argument behavior. The .NET path adds `IBackupEventScriptExecutor`, preserves the no-argument success and reason-bearing failure calls in `WindowsScriptRuleExecutor`, registers it only when scripting is enabled, and wires `BackupEventDispatcherRuntimeHost` from `Program.cs` while keeping the existing COM identity and authorization boundaries unchanged. Focused script/backup-manager coverage passes `78/78`; full Net10 passes `1483` with `3` opt-in tests skipped. The next independent slice was the offline synthetic 100k-message IMAP SEARCH/SORT benchmark pack; the approved disposable SQL restore harness remains environment-blocked. Restore remains `E_NOTIMPL` and SQL/data-directory rollback remains unwired. The older paragraph is historical context.

Authoritative update (2026-08-01, `f7f90c84a`): compressed non-DB-only message `DataBackup` staging is complete for `BODomains|BOMessages|BOCompression`. Legacy `BackupExecuter::BackupDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:196-217`) recursively copies the configured data directory and removes only files directly under the staging root; `BackupExecuter::StartBackup` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:119-130,165-184`) archives `hMailServerBackup.xml` first, recursively adds `DataBackup`, and deletes the staging directory after compression. `FileUtilities::CopyDirectory` and `DeleteFilesInDirectory` (`hmailserver/source/Server/Common/Util/FileUtilities.cpp:370-440`) define the copy/root-file cleanup behavior, while `Compression::AddDirectory` (`hmailserver/source/Server/Common/Util/Compression.cpp:28-35`) defines recursive 7z input. The .NET path wires the configured data directory through `Program.cs`, stages into an isolated destination `DataBackup`, preserves nested message files, removes the staging directory after success/failure cleanup, and fails closed for raw mode, missing source data, pre-existing staging, and source-nested staging paths. Focused backup/archive coverage passes `49/49`; full Net10 passes `1367` with `3` opt-in tests skipped. The next bounded slice is raw non-DB-only `BODomains|BOMessages` staging, where the external `DataBackup` directory remains beside the archive; restore, destructive SQL, and data-directory replacement remain fenced. The older paragraphs below are historical context.

Authoritative update (2026-08-01, `384e67788`): backup-side private-account nested subfolder metadata is complete. Legacy `IMAPFolders::Refresh` (`hmailserver/source/Server/Common/BO/IMAPFolders.cpp:42-145`) loads all account rows in `folderid ASC` order and rebuilds the `folderparentid` hierarchy; `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) emits direct items and omits empty collections; and `IMAPFolder::GetSubFolders`/`XMLStore` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:61-68,123-145`) emits scalar attributes before recursive nested `Folders` containers. The .NET path now loads each selected account once through `GetFoldersForAccountAsync`, groups the existing snapshots by `ParentId`, and uses `WriteFolder` for depth-first root/child/grandchild XML while preserving scalar order and XMLite-compatible escaping. Messages, data files, ACLs, public folders, and restore remain unchanged. Focused backup/folder/host coverage passes `45/45`; full Net10 passes `1361` with `3` opt-in tests skipped. The next bounded slice is DB-only folder message metadata serialization; keep data-directory copying, `DataFiles`, ACLs/public folders, restore, destructive SQL, event dispatch, and SEC-18 work fenced. The older paragraphs below are historical context.

Authoritative update (2026-08-01, `db73812c7`): backup-side private-account root folder metadata/scalar serialization is complete. Legacy `Account::GetFolders`/`Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:131-140,280-331`) gates folder output on `BOMessages` and places `Folders` after `Rules`; `IMAPFolders::Refresh` (`hmailserver/source/Server/Common/BO/IMAPFolders.cpp:42-145`) preserves `folderid ASC` order and root scope; `IMAPFolder::XMLStore` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:123-145`) emits `Name`, `Subscribed`, `CreateTime`, and `CurrentUID` in that order; `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits empty collections; and `Time::GetTimeStampFromDateTime` (`hmailserver/source/Server/Common/Util/Time.cpp:62-72`) defines the legacy timestamp shape. The .NET path adds optional `BackupArchiveXmlPayload.Folders`, selected-account root loading through `IImapFolderAdministrationStore.GetRootFoldersAsync`, and `SevenZipBackupArchiveRuntime.WriteFolders` with XMLite-compatible escaping, exact attribute order, empty-container omission, and the `BackupMessagesFlag` gate. Focused backup/folder/host coverage passes `45/45`; full Net10 passes `1361` with `3` opt-in tests skipped. The actual archive still rejects message payload creation before writing, so this is metadata-only parity coverage. The next bounded slice is backup-side nested subfolder hierarchy metadata; keep messages, data-directory copying, ACLs, public folders, restore, destructive SQL, event dispatch, and SEC-18 work fenced. The older paragraphs below are historical context.

Authoritative update (2026-08-01, `bd37be125`): backup-side account `Rules` serialization is complete, including read-only `RuleCriterias`/`Criteria` and `RuleActions`/`Action` children. Legacy `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`) emits `FetchAccounts`, then `Rules`, then message folders; `Rules::Refresh` (`hmailserver/source/Server/Common/BO/Rules.cpp:26-34`) scopes by `ruleaccountid` and orders by `rulesortorder ASC`; `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits empty collections; `Rule::XMLStore` (`hmailserver/source/Server/Common/BO/Rule.cpp:62-75`) emits `Name`, `Active`, `UseAND`, and `SortOrder`, then criteria and actions. The .NET path adds `IBackupRuleAdministrationStore` with a backup-only SQL projection that preserves the legacy rule ordering without the COM store's `ruleid` tie-breaker, loads selected account/rule children through existing read-only stores, preserves child order, emits legacy attribute names/order, matches XMLite entity escaping, and omits empty `Rules`, `RuleCriterias`, and `RuleActions` containers. COM/admin mutation, SMTP rule execution, installed identities, authentication boundaries, folders, message/data-directory payloads, restore, destructive SQL, event dispatch, and SEC-18 remain unchanged. Focused backup/rule/host coverage passes `115/115`; full Net10 passes `1358` with `3` opt-in tests skipped. The next bounded slice is backup-side folder metadata/scalar serialization; keep nested messages, data-directory copying, subfolders, ACLs, restore, and the other fenced work out of scope. The older paragraphs below are historical context.

Authoritative update (2026-08-01, `afd0de0da`): backup-side encrypted fetch-account `Password` and nested `FetchAccountUIDs`/`UID` serialization are complete. Legacy `FetchAccount::XMLStore` (`hmailserver/source/Server/Common/BO/FetchAccount.cpp:55-79`) writes Blowfish ciphertext immediately after `Username`; `FetchAccountUIDs::Refresh` (`hmailserver/source/Server/Common/BO/FetchAccountUIDs.cpp:29-59`) filters by `uidfaid` without an `ORDER BY`, and `FetchAccountUID::XMLStore` (`hmailserver/source/Server/Common/BO/FetchAccountUID.cpp:41-58`) emits `UID` then `Date`. The .NET path uses `IBackupFetchAccountAdministrationStore` and a dedicated SQL projection, preserves raw ciphertext and reader order, omits empty UID containers, and leaves ordinary fetch administration, COM password behavior, external-fetch runtime, restore, and mutation unchanged. Focused backup/fetch/COM/security coverage passes `59/59`; full Net10 passes `1353` with `3` opt-in tests skipped. The next bounded slice is backup-side `Rules` child serialization; keep rule mutation/execution, Folders, message/data-directory payloads, restore, destructive SQL, event dispatch, and SEC-18 work fenced. The older paragraphs below are historical context.

Authoritative update (2026-07-30, `ae97a70eb`): backup-side non-secret `FetchAccounts` scalar child serialization is complete. Legacy `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`) invokes `FetchAccounts::XMLStore` after account attributes; `FetchAccounts::Refresh` (`hmailserver/source/Server/Common/BO/FetchAccounts.cpp:36-42`) scopes rows by account and orders by `faid ASC`; `FetchAccount::XMLStore` (`hmailserver/source/Server/Common/BO/FetchAccount.cpp:55-79`) emits `Name`, `ServerAddress`, `ServerType`, `Port`, `Username`, `Minutes`, `DaysToKeep`, `Active`, `MIMERecipientHeaders`, `ProcessMIMERecipients`, `ProcessMIMEDate`, `UseAntiSpam`, `UseAntiVirus`, `EnableRouteRecipients`, and `ConnectionSecurity` in that order. The .NET path emits those escaped attributes only when the owning fetch-account snapshot is non-empty and loads only selected account IDs through the existing secret-free `IFetchAccountAdministrationStore`; COM identities, authenticated boundaries, and external-fetch runtime behavior are unchanged. Focused backup/fetch/COM coverage passes `49/49`; full Net10 passes `1349` with `3` opt-in tests skipped. The next bounded slice is backup-side encrypted fetch `Password` plus `FetchAccountUIDs`/`UID` serialization; keep Rules, Folders, message/data-directory payloads, restore, destructive SQL, event dispatch, and SEC-18 work fenced. The older paragraph below is historical context.

Authoritative latest update (2026-07-30): authenticated `BackupManager` archive/XML creation, raw settings-property parity, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, backup-side normal domain `Aliases`, backup-side `DistributionLists` child serialization, and backup-side account `Password`/`PasswordEncryption` serialization are complete in `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, `3e7535d76`, `5d4981240`, and `fd30ceb33`. Legacy `PropertySet::Refresh`/`XMLStore` (`hmailserver/source/Server/Common/Application/PropertySet.cpp:31-181`), `Configuration::XMLStore` (`hmailserver/source/Server/Common/Application/Configuration.cpp:687-713`), `XMLite` escaping (`hmailserver/source/Server/Common/Util/XMLite.cpp:27-74`), `BackupExecuter::StartBackup` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:57-190`), `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:104-149`), `Accounts::Refresh` (`hmailserver/source/Server/Common/BO/Accounts.cpp:34-56`), `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`), `PersistentAccount::ReadObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:146-191`), `Aliases::Refresh` (`hmailserver/source/Server/Common/BO/Aliases.cpp:26-32`), `Alias::XMLStore` (`hmailserver/source/Server/Common/BO/Alias.cpp:28-37`), `DistributionLists::Refresh` (`hmailserver/source/Server/Common/BO/DistributionLists.cpp:40-47`), `DistributionList::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionList.cpp:31-45`), `DistributionListRecipients::Refresh` (`hmailserver/source/Server/Common/BO/DistributionListRecipients.cpp:27-34`), `DistributionListRecipient::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionListRecipient.cpp:30-38`), `Time::GetCurrentDateTime` (`hmailserver/source/Server/Common/Util/Time.cpp:25-34`), `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`), and `DomainAlias::XMLStore` (`hmailserver/source/Server/Common/BO/DomainAlias.cpp:26-33`) remain the references. The .NET path carries one successful plan evidence object into a shell-free 7z writer, keeps ordinary account administration projections non-secret, reads `accountpassword`/`accountpwencryption` only through `IBackupAccountAdministrationStore`, preserves `LongValue`/`StringValue` and ordinal property ordering, excludes `smtprelayerpassword`, and emits per-domain `DomainAliases`, scalar `Accounts`, normal `Aliases`, `DistributionLists`, and account credentials in legacy child/attribute order while omitting empty containers. `FetchAccounts`, `Rules`, and `Folders` remain explicitly fenced; `BOMessages` still rejects before file creation. Focused backup/account credential coverage passes `44/44`; full Net10 passes `1346` with `3` opt-in tests skipped. The next production-gate slice is backup-side `FetchAccounts` child serialization; keep its encrypted fetch password, UIDs, Rules, Folders, message/data-directory payloads, restore execution, destructive SQL, and event dispatch out of scope. Older current-next paragraphs below are historical context.

## SEC-11 Backup DomainAliases XML Serialization (2026-07-30)

- Legacy `BackupExecuter::BackupDomains_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:220-227`) refreshes domains before `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:105-148`). The legacy domain writer emits `DomainAliases` first; `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:55-78`) omits an empty collection, and `DomainAlias::XMLStore` (`hmailserver/source/Server/Common/BO/DomainAlias.cpp:26-33`) emits only `DomainAlias Name`, with the existing `DomainAliases::Refresh`/SQL order supplied by `hmailserver/source/Server/Common/BO/DomainAliases.cpp:25-43`.
- Code/test commit `f15e857a8` wires the existing `IDomainAliasAdministrationStore` into the backup payload provider, scopes one read-only alias snapshot per selected domain ID, and emits escaped `DomainAliases`/`DomainAlias Name` children immediately after each domain's scalar attributes. Existing COM identities, authenticated domain-owned read access, direct activation denial, SMTP/POP3 alias behavior, and mutation boundaries remain unchanged.
- Focused backup/account coverage passes `38/38`; full Net10 passes `1340` with `3` opt-in tests skipped. Account credentials, `FetchAccounts`, `Rules`, and `Folders` remain intentionally out of this non-secret scalar slice; distribution-list child serialization, message/data-directory payloads, restore, destructive SQL, and event dispatch remain explicit gaps. The subsequent bounded backup slices were normal `Aliases`, `DistributionLists`, and account credentials; the current next slice is `FetchAccounts` child serialization.

## SEC-11 Backup Accounts Scalar XML Serialization (2026-07-30)

- Legacy `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:104-148`) emits `DomainAliases` before `Accounts`; `Accounts::Refresh` (`hmailserver/source/Server/Common/BO/Accounts.cpp:34-56`) scopes rows by domain and orders by `accountaddress ASC`; `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`) emits the scalar account attributes before `FetchAccounts`, `Rules`, and message folders. `Time::GetCurrentDateTime` (`hmailserver/source/Server/Common/Util/Time.cpp:25-34`) supplies the `yyyy-MM-dd HH:mm:ss` timestamp shape.
- Code/test commit `ac611987c` wires the existing `IAccountAdministrationStore` into the backup payload provider, reads one account snapshot per selected domain ID, and emits escaped non-secret scalar `Account` attributes in legacy order after `DomainAliases`, omitting empty `Accounts` containers. It intentionally does not retrieve or emit `Password`/`PasswordEncryption`, `FetchAccounts`, `Rules`, or `Folders`; normal COM account snapshots, installed identities, authenticated access boundaries, SMTP/POP3 behavior, and `BOMessages` rejection remain unchanged.
- Focused backup/account coverage passes `38/38`; full Net10 passes `1340` with `3` opt-in tests skipped. The historical next bounded backup slice was domain `Aliases` child serialization; distribution lists, message/data-directory payloads, restore, destructive SQL, and event dispatch remain fenced.

## SEC-11 Backup Normal Aliases XML Serialization (2026-07-30)

- Legacy `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:104-149`) writes `DomainAliases`, `Accounts`, `Aliases`, and `DistributionLists` in that order. `Aliases::Refresh` (`hmailserver/source/Server/Common/BO/Aliases.cpp:26-32`) scopes `hm_aliases` by domain and orders by `aliasname ASC`; `Alias::XMLStore` (`hmailserver/source/Server/Common/BO/Alias.cpp:28-37`) emits `Alias` attributes `Name`, `Value`, and `Active` in that order. `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits empty containers.
- Code/test commit `3e7535d76` wires the existing `IAliasAdministrationStore` into the backup payload provider, loads one scoped alias snapshot per selected domain ID, and emits escaped normal `Aliases`/`Alias` scalar children after `Accounts`, without `ID` or `DomainID` attributes. Existing COM alias identities, authenticated read boundaries, SMTP/POP3 alias behavior, mutation status, restore behavior, and SQL schema remain unchanged.
- Focused backup/alias coverage passes `54/54`; full Net10 passes `1342` with `3` opt-in tests skipped. Account credential and nested account children, message/data-directory payloads, restore, destructive SQL, and event dispatch remain explicit gaps. The subsequent bounded backup slices were `DistributionLists` child serialization and account credentials; the current next slice is `FetchAccounts` child serialization.

## SEC-11 Backup DistributionLists XML Serialization (2026-07-30)

- Legacy `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:104-149`) writes `DistributionLists` after normal `Aliases`. `DistributionLists::Refresh` (`hmailserver/source/Server/Common/BO/DistributionLists.cpp:40-47`) orders lists by address; `DistributionList::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionList.cpp:31-45`) emits `Name`, `Active`, `RequiresAuth`, `RequiresAuthAddress`, and `ListMode` in that order. `DistributionListRecipients::Refresh` (`hmailserver/source/Server/Common/BO/DistributionListRecipients.cpp:27-34`) orders recipients by address, and `DistributionListRecipient::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionListRecipient.cpp:30-38`) emits the optional inner `DistributionList` container with `Recipient Name`. `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits empty containers.
- Code/test commit `5d4981240` wires the existing `IDistributionListAdministrationStore` and `IDistributionListRecipientAdministrationStore` into the backup payload provider, loads one scoped list snapshot per selected domain and one recipient snapshot per selected list, and emits escaped `DistributionLists` after `Aliases` with no `ID` or `DomainID` attributes. Existing COM identities, authenticated Settings/Domain read boundaries, direct activation denial, SMTP list-policy behavior, mutation status, restore behavior, and SQL schema remain unchanged.
- Focused backup/distribution-list coverage passes `34/34`; full Net10 passes `1344` with `3` opt-in tests skipped. The historical next bounded backup slice was account `Password`/`PasswordEncryption` serialization, now complete in `fd30ceb33`; `FetchAccounts`, nested account children, message/data-directory payloads, restore, destructive SQL, and event dispatch remain explicit gaps. The current next slice is `FetchAccounts` child serialization.

## SEC-11 Backup Account Password/PasswordEncryption XML Serialization (2026-07-30)

- Legacy `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`) emits `Password` and integer `PasswordEncryption` immediately after `Active`; `PersistentAccount::ReadObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:146-191`) loads `accountpassword` and `accountpwencryption`, and the schema defines both columns in `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:168-195`.
- Code/test commit `fd30ceb33` adds `AccountBackupAdministrationSnapshot` and `IBackupAccountAdministrationStore`, a dedicated SQL projection, host wiring, and backup XML emission at the legacy attribute position. Ordinary `IAccountAdministrationStore` queries remain secret-free; attached `Account.Password`, `ValidatePassword`, mutation, COM identities, direct activation denial, SMTP/POP3 behavior, restore behavior, and nested account children remain unchanged.
- Focused backup/account credential coverage passes `44/44`; full Net10 passes `1346` with `3` opt-in tests skipped. The next bounded backup slice is `FetchAccounts` child serialization; its encrypted fetch password, UID children, Rules, Folders, message/data-directory payloads, restore, destructive SQL, and event dispatch remain fenced.

Authoritative update for 2026-07-29: IncomingRelays WebAdmin hardening is complete in `fc2aa90f6`, blocked-attachment hardening is complete in `bfee58cab`, route-address WebAdmin hardening is complete in `2394e026`, distribution-list recipient hardening is complete in `9d6a8dda2`, alias hardening is complete in `1dc35f169`, route hardening is complete in `8d684e638`, SecurityRanges handler hardening is complete in `97e3096c3`, account handler hardening is complete in `95a7e4284`, rule handler hardening is complete in `6736e161e`, domain handler hardening is complete in `3d25cb0a7`, authenticated existing-row `RuleCriteria.HeaderField` setter parity is complete in `c8d69c9b8`, authenticated existing-row `RuleCriteria.MatchValue` setter parity is complete in `d95ce9c69`, authenticated existing-row `RuleCriteria.UsePredefined` setter parity is complete in `a4ff728c0`, authenticated existing-row `RuleCriteria.PredefinedField` setter parity is complete in `fabc7e03a`, authenticated existing-row `RuleCriteria.MatchType` setter parity is complete in `0d9e43b14`, the owner-scoped RuleCriteria save contract is complete in `edf97aeaa`, authenticated existing-row `RuleCriteria.RuleID` setter/save parity is complete in `66e72f39c`, authenticated existing-row `RuleAction.RuleID` ownership/save parity is complete in `9680640a5`, RuleAction parent-snapshot visibility within an owning collection is complete in `dc2fe2118`, per-Rules-generation repeated-`Rule.Actions` adapter visibility is complete in `493848279`, authenticated per-account repeated-`Account.Rules` adapter visibility is complete in `bb4142b99`, authenticated per-account repeated-`Account.Messages` adapter visibility is complete in `0c2ee1226`, and Account.Messages SQL projection parity is complete in `debc93dac`. Account Rules and Messages state now loads once per attached account identity, fresh child facades share the appropriate snapshot, Accounts.Refresh isolates old wrappers from new account state, and account message reads preserve legacy all-state UID ordering. Detached activation and read-only facades remain denied. Focused Messages/Accounts/Application/SQL coverage passes `48/48`; full Net10 passes `1308` with `3` opt-in tests skipped. PHP CLI is unavailable. The next production-gate slice is authenticated per-account `Account.IMAPFolders` cached snapshot and shared folder-adapter visibility; keep folder mutation, ACL changes, live protocol/cache synchronization, SMTP/POP3 behavior, SEC-18 broker registration, and PHP session cutover fenced.

## SEC-14 WebAdmin Blocked Attachment Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceAntiVirus::get_BlockedAttachments` (`hmailserver/source/Server/COM/InterfaceAntiVirus.cpp:375-398`), `InterfaceBlockedAttachments::Add` and lookup/delete methods (`hmailserver/source/Server/COM/InterfaceBlockedAttachments.cpp:75-151`), `InterfaceBlockedAttachment::Save` and setters (`hmailserver/source/Server/COM/InterfaceBlockedAttachment.cpp:14-103`), and `PersistentBlockedAttachment::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentBlockedAttachment.cpp:51-84`). Legacy Add returns a staged item attached to the owning collection; Save inserts `hm_blocked_attachments` for ID zero, assigns the generated ID, and adds the item to the parent after successful persistence.
- Code/test commit `bfee58cab` hardens `hmailserver/source/WebAdmin/background_blocked_attachment_save.php`: the existing server-admin guard remains first, the handler requires `hmailRequirePostCsrfToken()`, and `id`, `wildcard`, `description`, and `action` are read from POST only. Existing `Settings -> AntiVirus -> BlockedAttachments` lookups, Add/Edit/DeleteByDBID, field assignments, Save, redirects, and forms remain unchanged. Focused source/COM/store coverage passes `38/38`; full Net10 passes `1274` with `3` opt-in skips. PHP CLI is unavailable.
- The .NET BlockedAttachments adapter and SQL administration store remain intentionally read-only: collection Add, item setters/Save/Delete, and direct child activation remain unimplemented or denied outside the authenticated Settings boundary. SMTP attachment-policy behavior, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. The next live WebAdmin mutation is `hmailserver/source/WebAdmin/background_route_save.php`.

## SEC-14 WebAdmin Route Address Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceRoute::get_Addresses` (`hmailserver/source/Server/COM/InterfaceRoute.cpp:301-319`), `InterfaceRouteAddresses::Add`, `DeleteByDBID`, and `get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceRouteAddresses.cpp:33-164`), `InterfaceRouteAddress::put_Address`, `put_RouteID`, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceRouteAddress.cpp:47-148`), and `PersistentRouteAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRouteAddress.cpp:35-67`). The selected route owns the child collection; Add stages a child with that route ID; Save writes `hm_routeaddresses`, assigns the generated ID, and adds the item to its parent after successful persistence. Installed route-address IIDs, DISPIDs, CLSIDs, and coclass identities remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1551-1578,3097-3110`.
- Code/test commit `2394e026f` hardens `hmailserver/source/WebAdmin/background_route_address_save.php`: the existing domain-admin boundary remains first, the handler requires `hmailRequirePostCsrfToken()`, and `routeid`, `routeaddressid`, `action`, and `routeaddress` are read from POST only. Existing `Settings -> Routes -> Route -> RouteAddress` lookup, Add/Edit/Delete, `Address`/`RouteID` assignments, Save, redirects, and the already POST+CSRF-bearing edit/delete forms remain unchanged. Focused route-address source/COM/store coverage passes `19/19`; full Net10 passes `1276` with `3` opt-in skips. PHP CLI is unavailable.
- The .NET `RouteAddresses` adapter and SQL store remain intentionally read-only for Add, item setters, and Save; direct child activation remains `E_ACCESSDENIED`, while existing authorized read/delete ownership and SQL parameterization remain unchanged. SMTP routing, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. Next slice: `background_route_save.php` POST-only/CSRF hardening.

## SEC-14 WebAdmin Distribution-List Recipient Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceDistributionList::get_Recipients` (`hmailserver/source/Server/COM/InterfaceDistributionList.cpp:279-306`), `InterfaceDistributionListRecipients::Add`, `DeleteByDBID`, and `get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceDistributionListRecipients.cpp:37-82,154-180`), `InterfaceDistributionListRecipient::put_RecipientAddress`, `Delete`, and `Save` (`hmailserver/source/Server/COM/InterfaceDistributionListRecipient.cpp:93-159`), and `PersistentDistributionListRecipient::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:94-137`). Legacy `Add()` creates an unsaved recipient scoped to the owning distribution-list collection; `Save()` inserts or updates `hm_distributionlistsrecipients`, assigns the generated identity on insert, and adds a new item to the parent only after successful persistence. Installed recipient interfaces and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1199-1230,3010-3022`.
- Code/test commit `9d6a8dda2` hardens `hmailserver/source/WebAdmin/background_distributionlist_recipient_save.php`: the existing user-level denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and `distributionlistid`, `recipientid`, `domainid`, `action`, and `recipientaddress` are read from POST only. Existing domain-admin ownership, `Settings -> Domains -> Domain -> DistributionLists -> DistributionList -> Recipients` lookup, Add/Edit/Delete, `RecipientAddress` assignment, Save, redirects, and POST+CSRF forms remain unchanged. `WebAdminDistributionListRecipientPostOnlySourceTests` plus recipient/distribution-list/domain/store contract coverage passes `20/20`; full Net10 passes `1278` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET `DistributionListRecipients` adapter and SQL administration store remain intentionally read-only for Add, item setters, Save, and Delete; direct child activation remains `E_ACCESSDENIED`, while authenticated Settings read access and SQL parameterization remain unchanged. SMTP list-policy behavior, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. The next live WebAdmin mutation is `hmailserver/source/WebAdmin/background_route_save.php` POST-only/CSRF hardening.

## SEC-14 WebAdmin Alias Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceDomainAliases::get_ItemByDBID` and `Add` (`hmailserver/source/Server/COM/InterfaceDomainAliases.cpp:19-42,73-105`), `InterfaceDomainAlias::put_AliasName`, `Save`, `put_DomainID`, and `Delete` (`hmailserver/source/Server/COM/InterfaceDomainAlias.cpp:88-160`), and `PersistentDomainAlias::SaveObject`/`DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDomainAlias.cpp:58-117`). Legacy `Add()` creates an unsaved alias scoped to the owning domain collection; `Save()` inserts or updates `hm_domain_aliases`, assigns the generated `daid` on insert, and adds the new item to its parent after successful persistence. `DomainID` is retained as a compatibility setter and does not change the owning domain. Installed `DomainAliases`/`DomainAlias` interfaces and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1717-1750,3155-3167`.
- Code/test commit `1dc35f169` hardens `hmailserver/source/WebAdmin/background_alias_save.php`: the existing user-level denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and `domainid`, `aliasid`, `action`, `aliasname`, `aliasvalue`, and `aliasactive` are read from POST only. Existing same-domain domain-admin ownership, `Domain -> Aliases` lookup, `IsAddAllowed`, Add/Edit/Delete, `Name`/`Value`/`Active` assignments, Save, redirects, and POST+CSRF forms remain unchanged. `WebAdminAliasPostOnlySourceTests`, `DomainAliasesComContractTests`, `DomainsComContractTests`, and `SqlServerDomainAliasAdministrationStoreTests` pass `14/14`; full Net10 passes `1279` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET `DomainAliases` adapter and SQL administration store remain intentionally read-only for Add, item setters, Save, and Delete; direct child activation remains `E_ACCESSDENIED`, while the installed IID/vtable/DISPID/class identities and authenticated domain-owned read boundary remain unchanged. SMTP alias behavior, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. Route hardening is recorded in `8d684e638`; the next live WebAdmin mutation is `hmailserver/source/WebAdmin/background_securityrange_save.php` POST-only/CSRF hardening.

## SEC-14 WebAdmin Route Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceRoutes::LoadSettings`, `Add`, and `DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceRoutes.cpp:12-29,75-105`), `InterfaceRoute` setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceRoute.cpp:67-243,284-582`), and `PersistentRoute::SaveObject`/`DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:31-70`). The installed `IInterfaceRoute` and `IInterfaceRoutes` contracts, including DISPIDs and collection order, remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1378-1426,1533-1550`; route coclasses remain at `hmailserver/source/Server/hMailServer/hMailServer.idl:3052-3094`.
- Code/test commit `8d684e638` hardens `hmailserver/source/WebAdmin/background_route_save.php`: the existing server-admin denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and `action`, `routeid`, and all route fields are read from POST only. Existing `Settings -> Routes` ItemByDBID/Add/DeleteByDBID lookup, field assignments, conditional `SetRelayerAuthPassword`, Save, redirects, and `hm_route.php`/`hm_routes.php` forms remain unchanged. `WebAdminRoutePostOnlySourceTests` plus route COM/address and SQL-store coverage passes `19/19`; full Net10 passes `1280` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Routes` adapter and SQL administration store remain intentionally read-only for collection/item mutation; direct route activation remains `E_ACCESSDENIED`, while the authenticated Settings read boundary and installed COM identity/vtable/DISPID shape remain unchanged. SMTP routing, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. Next slice: `hmailserver/source/WebAdmin/background_securityrange_save.php` POST-only/CSRF hardening.

## SEC-14 WebAdmin SecurityRanges Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceSecurityRanges::LoadSettings`, `DeleteByDBID`, and `Add` (`hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:13-21,60-74,158-185`), `InterfaceSecurityRange::Save`, `put_LowerIP`, `put_UpperIP`, and the remaining field setters (`hmailserver/source/Server/COM/InterfaceSecurityRange.cpp:36-841`), and `PersistentSecurityRange::SaveObject`/`Validate` (`hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:52-117,268-289`). Legacy Save validates and persists the existing object through `hm_securityranges`; the handler's field mapping remains the reference. The installed `IInterfaceSecurityRange`/`IInterfaceSecurityRanges` IIDs, vtable order, and DISPIDs remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1232-1320`, with Settings.SecurityRanges at DISPID 18 (`:540`).
- Code/test commit `97e3096c3` hardens `hmailserver/source/WebAdmin/background_securityrange_save.php`: the existing server-admin denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and `action`, `securityrangeid`, and all 21 remaining range fields are read from POST only. Existing `Settings -> SecurityRanges` ItemByDBID/Add/DeleteByDBID lookup, field assignments, Save, redirects, delete flow, and `hm_securityrange.php`/`hm_securityranges.php` forms remain unchanged. `WebAdminSecurityRangePostOnlySourceTests` plus SecurityRanges COM and SQL-store coverage passes `30/30`; full Net10 passes `1281` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `SecurityRanges` adapter and SQL administration store retain the existing authenticated server-administrator boundary, installed COM identity/vtable/DISPID shape, and current mutation implementation; this slice changes no COM, SQL, IP-policy, auto-ban, SMTP trust, live-reconfiguration, service/database/Data-directory, or SEC-18 behavior. Next slice: `hmailserver/source/WebAdmin/background_account_save.php` POST-only/CSRF hardening.

## SEC-14 WebAdmin Account Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceAccounts::Add`/`DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceAccounts.cpp:42-74,202-231`), `InterfaceAccount::Save`, `put_Password`, and the account field setters (`hmailserver/source/Server/COM/InterfaceAccount.cpp:74-1048`), and `PersistentAccount::SaveObject`/`DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:55-329`). The installed `IInterfaceAccounts`/`IInterfaceAccount` IIDs, vtable order, and DISPIDs remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:794-895`; account coclasses remain at `hmailserver/source/Server/hMailServer/hMailServer.idl:2904-2919`.
- Code/test commit `95a7e4284` hardens `hmailserver/source/WebAdmin/background_account_save.php`: the handler requires `hmailRequirePostCsrfToken()` before reading `domainid`, `accountid`, `action`, or account fields, and all 26 inputs use `hmailGetPostVar()`. Existing user self-edit and session-password update behavior, domain-admin ownership, server-admin restrictions, `Settings -> Domains -> Domain -> Accounts` lookup, Add/Edit/Delete, account field mappings, Save, redirects, and `hm_account.php`/`hm_accounts.php` forms remain unchanged. `WebAdminAccountPostOnlySourceTests` plus account COM/store coverage passes `19/19`; full Net10 passes `1282` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Accounts` adapter and SQL administration store retain the existing authenticated domain-owned read boundary, direct activation denial, installed COM identity/vtable/DISPID shape, and current mutation status; this slice changes no password storage, SMTP delivery, account-rule runtime, service/database/Data-directory, or SEC-18 behavior. Rule handler hardening is recorded in `6736e161e`; the next COM/Admin slice is authenticated existing-row `RuleCriteria.MatchType` setter parity through the owning `RuleCriteria.Save()` path.

## SEC-14 WebAdmin Rule Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceRules::Add`, `DeleteByDBID`, and `Refresh` (`hmailserver/source/Server/COM/InterfaceRules.cpp:19-143`), `InterfaceRule` field setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceRule.cpp:66-306`), `InterfaceRuleCriterias::Add` and `InterfaceRuleCriteria::Save` (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:91-122`, `InterfaceRuleCriteria.cpp:13-258`), `InterfaceRuleActions::Add` and `InterfaceRuleAction::Save` (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:93-124`, `InterfaceRuleAction.cpp:30-587`), and `PersistentRule::SaveObject`, `PersistentRuleCriteria::SaveObject`, and `PersistentRuleAction::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:73-192`, `PersistentRuleCriteria.cpp:58-116`, `PersistentRuleAction.cpp:65-141`). Installed `IInterfaceRules`, `IInterfaceRule`, criteria/action interfaces, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1758-1900,3173-3215`.
- Code/test commit `6736e161e` hardens `hmailserver/source/WebAdmin/background_rule_save.php`: the existing `GetHasRuleAccess` ownership boundary remains in place, the handler requires `hmailRequirePostCsrfToken()`, and every scope, rule, criteria, and action input is read from POST only. Existing global/account `Rules` lookup, Add/Edit/Delete, criteria/action mutation, non-server-admin action restrictions, field mappings, Save calls, redirects, and the POST+CSRF forms in `hm_rule.php`, `hm_rules.php`, `hm_rule_criteria.php`, `hm_rule_action.php`, and `hm_account.php` remain unchanged. `WebAdminRulePostOnlySourceTests` plus rule COM/store coverage passes `70/70`; full Net10 passes `1283` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Rules`, `Rule`, `RuleCriterias`, `RuleActions`, and `RuleAction` adapters retain the authenticated Settings boundary and direct activation denial; all six selected criteria setters and `RuleAction.RuleID` stage through their authorized owning Save callbacks, and the owner-scoped save contracts separate immutable collection ownership from mutable snapshots. RuleAction wrappers now share mutations within one owning collection, while repeated `Rule.Actions` access creates fresh facades over shared per-Rules-generation state. Installed COM identity/vtable/DISPID shape, SMTP rule execution, service/database/Data-directory state, and SEC-18 staging state did not change. The next COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit.

## SEC-14 WebAdmin Domain Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceDomains::Add`, `get_ItemByDBID`, `DeleteByDBID`, and authentication (`hmailserver/source/Server/COM/InterfaceDomains.cpp:44-64,99-219,252-269`), `InterfaceDomain` setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceDomain.cpp:56-353,480-1431`), and `PersistentDomain::DeleteObject`/`SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:46-234`). Installed `IInterfaceDomain`/`IInterfaceDomains` contracts and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:707-753,1512-1525,2900-2902,3084-3086`.
- Code/test commit `3d25cb0a7` hardens `hmailserver/source/WebAdmin/background_domain_save.php`: the existing domain-admin ownership and server-admin delete checks remain, the handler requires `hmailRequirePostCsrfToken()`, and all 30 scope/domain/DKIM/signature inputs use `hmailGetPostVar()`. Existing `Domains` ItemByDBID/Add/Delete, domain field mappings, DomainAliases count gate, Save, redirects, and POST+CSRF forms in `hm_domain.php` and `hm_domains.php` remain unchanged. `WebAdminDomainPostOnlySourceTests` plus domain/domain-alias COM/store coverage passes `16/16`; full Net10 passes `1284` with `3` opt-in tests skipped. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Domains`/`Domain` adapters and SQL administration stores retain their current authenticated Application/Domain access boundaries, read-only COM mutation status, direct activation denial, installed identities, SMTP/domain behavior, service/database/Data-directory state, and SEC-18 staging state. The next COM/Admin slice is an ownership/save-containment audit for authenticated existing-row `RuleAction.RuleID`.

## SEC-11 RuleCriteria HeaderField Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_HeaderField` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,122-152`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38` and `hmailserver/source/Server/COM/COMCollection.h:11-38`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:65-100`). The legacy setter stages the raw BSTR with no validation or normalization; detached objects return `E_ACCESSDENIED`; Save persists `criteriaheadername` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` IIDs, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,2844-2849,3185-3200`. The .NET contracts retain the same identities, BSTR marshaling, and direct activation boundary.
- Code/test commit `c8d69c9b8` makes `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs` use a mutable item-local snapshot for `HeaderField`, passes the staged snapshot through the owning save delegate, and leaves other criteria setters, Add, Delete, Refresh, rule execution, and SMTP behavior unchanged. `RuleCriteriasComContractTests` and related SQL/integration coverage pass `31/31`; full Net10 passes `1284` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the same owning `RuleCriteria.Save()` path.

## SEC-11 RuleCriteria MatchValue Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_MatchValue` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,154-184`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:65-100`). The setter stages the raw BSTR without validation, trimming, or normalization; detached objects return `E_ACCESSDENIED`; Save persists `criteriamatchvalue` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,2844-2849,3185-3200`. The .NET contracts, BSTR marshaling, authenticated access boundary, and direct activation denial remain unchanged.
- Code/test commit `d95ce9c69` changes only `RuleCriteria.MatchValue` to use the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `33/33`; full Net10 passes `1286` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is authenticated existing-row `RuleCriteria.MatchType` setter parity through the same owning `RuleCriteria.Save()` path.

## SEC-11 RuleCriteria PredefinedField Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_PredefinedField` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,186-216`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38`), and `PersistentRuleCriteria::ReadObject`/`SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:46-52,65-100`). The attached setter casts and stores the raw enum value without validation or normalization and returns `S_OK`; detached objects return `E_ACCESSDENIED`; Save persists `criteriapredefinedfield` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, enum GUID/values, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:205-238,1792-1837,3185-3200`. The .NET contracts, enum mapping, authenticated access boundary, and direct activation denial remain unchanged.
- Code/test commit `fabc7e03a` changes only the authorized `RuleCriteria.PredefinedField` setter to stage `(int)value` through the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `37/37`; full Net10 passes `1290` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an ownership/save-containment audit for authenticated existing-row `RuleAction.RuleID`; keep the setter fenced until its parent ownership and affected-row behavior are proven.

## SEC-11 RuleCriteria MatchType Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::get_MatchType`, `put_MatchType`, and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-55,218-248`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38` and `hmailserver/source/Server/COM/COMCollection.h:11-38`), and `PersistentRuleCriteria::ReadObject`/`SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:46-52,64-100`). The attached setter raw-casts and stores any enum integer without validation or normalization and returns `S_OK`; existing-row Save persists `criteriamatchtype`; detached objects remain access-denied.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, enum GUID/values, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:240-272,1792-1837,3185-3200`. The .NET contracts, enum mapping, authenticated Settings boundary, owning collection, and direct activation denial remain unchanged.
- Code/test commit `0d9e43b14` changes only the authorized `RuleCriteria.MatchType` setter to stage `(int)value` through the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `39/39`; full Net10 passes `1292` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an ownership/save-containment audit for authenticated existing-row `RuleAction.RuleID`; keep broader criteria/action/rule mutation, SMTP rule behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleCriteria Owner-Scoped Save Contract (2026-07-29)

- Legacy ownership behavior was confirmed in `InterfaceRuleCriteria::put_RuleID`/`Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-87`), parent attachment (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:14-114` and `hmailserver/source/Server/COM/COMCollection.h:11-32`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:64-100`). Legacy accepts raw attached `RuleID` values and writes the mutable destination by `criteriaid`; detached access remains denied.
- The .NET save contract carries the immutable owning rule ID separately from the snapshot destination in `IRuleCriteriaAdministrationStore`, passes the captured parent ID from `RuleCriteriaAdministrationRuntimeHost`, scopes SQL with `criteriaruleid = @OwningRuleId AND criteriaid = @CriteriaId`, and retains `SET criteriaruleid = @RuleId`. Non-single-row updates fail deterministically. COM identity, authenticated Settings access, direct activation denial, and the RuleID staging boundary remain unchanged.
- Code/test commit `edf97aeaa` changes `IRuleCriteriaAdministrationStore`, the RuleCriteria runtime save closure, the SQL administration store, and focused COM/SQL contract tests. Focused criteria/SQL/integration coverage passes `40/40`; full Net10 passes `1293` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- The next bounded slice is an ownership/save-containment audit for authenticated existing-row `RuleAction.RuleID`; keep RuleAction setter mutation, Add/new-item Save, broader Rule/RuleAction mutation, SMTP behavior, WebAdmin, SEC-18, backup/XML execution, and service/database state out of scope.

## SEC-11 RuleCriteria RuleID Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::get_RuleID`, `put_RuleID`, and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-87`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:14-114` and `hmailserver/source/Server/COM/COMCollection.h:11-32`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:64-100`). The attached setter stores any raw `LONG`, including foreign, zero, negative, and nonexistent target rule IDs, and returns `S_OK`; Save writes the destination by criterion ID while the original parent collection remains the owner.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,3185-3200`. The .NET contracts, authenticated Settings boundary, direct activation denial, and owner-scoped SQL save path remain unchanged.
- Code/test commit `66e72f39c` changes only the authorized `RuleCriteria.RuleID` setter to stage raw values through the existing mutable snapshot/owner-scoped Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `43/43`; full Net10 passes `1296` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an authenticated existing-row RuleAction parent-snapshot visibility audit; keep Add/new-item Save, broader action/rule mutation, SMTP behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleAction RuleID Ownership/Save Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleAction::put_RuleID`, `Save`, and `get_RuleID` (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:30-72,106-136`), parent lookup/attachment (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:22-116` and `hmailserver/source/Server/COM/COMCollection.h:6-38`), and `PersistentRuleAction::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleAction.cpp:77-116`). Attached setters accept raw `LONG` values, including foreign, zero, negative, and nonexistent rule IDs, and return `S_OK`; detached access remains `E_ACCESSDENIED`. Save persists the mutable destination by action ID, while the original parent collection remains the owner and shared legacy items expose the mutation before refresh.
- Installed `IInterfaceRuleAction`/`IInterfaceRuleActions` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1839-1900,3201-3215`; the .NET contracts and direct activation/read-only facade boundaries remain unchanged.
- Code/test commit `9680640a5` changes only the authorized `RuleAction.RuleID` setter, the RuleAction administration store owner parameter, the SQL owner-plus-action scope, and focused contract/store tests in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleActions.cs`, `hmailserver/source/Server.Net10/src/HMailServer.Core/Abstractions/IRuleActionAdministrationStore.cs`, `hmailserver/source/Server.Net10/src/HMailServer.Storage.SqlServer/SqlServerRuleActionAdministrationStore.cs`, and the corresponding tests. Focused coverage passes `47/47`; full Net10 passes `1300` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit; keep message mutation, SMTP rule execution, backup/XML behavior, COM identity, and SEC-18 work out of scope.

## SEC-11 RuleAction Parent-Snapshot Visibility (2026-07-29)

- Legacy parity was confirmed in cached parent access (`hmailserver/source/Server/Common/BO/Rule.cpp:49`, `Rule.h:33`), child lookup/attachment (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:22-49` and `hmailserver/source/Server/COM/COMCollection.h:11-22`), mutable action setters (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:122-563`), and collection refresh/delete behavior (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:125-159`, `hmailserver/source/Server/Common/BO/RuleActions.cpp:25-67`). Existing child wrappers and the owning vector observe the same mutable object immediately, including after Save failure; Refresh replaces the collection and new Add items remain distinct until successful Save.
- The .NET adapter now uses a private shared `RuleActionAdministrationEntry` per loaded row. Index and DBID lookups share that entry, setters update it immediately, Save reads it, Refresh replaces entries, and Delete removes entries only after store success. Installed `IInterfaceRuleAction`/`IInterfaceRuleActions` identities and direct activation/read-only boundaries remain unchanged.
- Code/test commit `dc2fe2118` changes only `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleActions.cs` and focused `RuleActionsComContractTests`. Coverage passes `48/48`; full Net10 passes `1301` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit. Do not broaden to message mutation, SMTP rule execution, backup/XML behavior, COM identity, or SEC-18 work.

## SEC-11 Rule.Actions Per-Rules-Generation Adapter Visibility (2026-07-29)

- Legacy parity was confirmed in `HM::Rule::GetActions` (`hmailserver/source/Server/Common/BO/Rule.cpp:49-59`, `Rule.h:45-46`), which lazily caches one `RuleActions` collection, and `InterfaceRule::get_Actions` (`hmailserver/source/Server/COM/InterfaceRule.cpp:195-213`), which creates a fresh COM collection wrapper over that cached collection. `InterfaceRules::get_Item`/`get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceRules.cpp:19-72`) create fresh rule wrappers over shared `HM::Rule` objects; `Rules::Refresh` rebuilds the parent objects while existing wrappers retain their prior cached rule/action state.
- The .NET adapters now keep a private action-state generation per authenticated `Rules` facade. `Rules.cs` shares per-rule action state across fresh `Rule` and `RuleActions` wrappers, while `Rules.Refresh()` publishes a new generation and leaves existing wrappers on the old snapshot. `RuleActions.cs` keeps fresh collection facades over that state and preserves shared refresh/delete visibility, owner-scoped Save callbacks, authenticated `Settings` access, direct activation denial, and installed COM identity/vtable/DISPID shape.
- Code/test commit `493848279` changes only `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/Rules.cs`, `RuleActions.cs`, and focused `RuleActionsComContractTests.cs`. Focused Rules/RuleActions/SQL coverage passes `58/58`; full Net10 passes `1305` with `3` opt-in tests skipped. No live SQL integration ran and PHP CLI remains unavailable. SMTP rule execution, service/database/Data-directory state, COM registration/ACLs, and SEC-18 staging state did not change.
- The next bounded COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit; keep message mutation and broader rule/action mutation out of scope.

## SEC-11 Account.Rules Per-Account-State Visibility (2026-07-29)

- Legacy parity was confirmed in `InterfaceAccount::get_Rules` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:790-815`), `Account::GetRules` (`hmailserver/source/Server/Common/BO/Account.cpp:119-128`), and `Account::rules_` (`hmailserver/source/Server/Common/BO/Account.h:162`). Legacy lazily loads one `HM::Rules(id_)`, refreshes it once, and returns fresh COM `InterfaceRules` wrappers over that cached object. `InterfaceRules::Refresh` (`hmailserver/source/Server/COM/InterfaceRules.cpp:143-152`) refreshes the shared object; `InterfaceAccounts` lookups (`hmailserver/source/Server/COM/InterfaceAccounts.cpp:98-190`) create distinct Account wrappers over the same account object until the owning collection refreshes.
- The .NET adapters now create one lazy `RuleAdministrationState` per attached account entry, including Administrator account ID `0`; repeated `Account.Rules` calls return fresh `Rules` facades over that state, one store load is shared, and `Rules.Refresh()` is visible through all facades. `Accounts.Refresh()` publishes new account entries and leaves prior Account/Rules wrappers on their old state. Existing authenticated/direct-activation boundaries and installed `IInterfaceAccount`/`IInterfaceRules` identity, vtable, DISPID, CLSID, and ProgID contracts remain unchanged.
- Code/test commit `bb4142b99` changes `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AccountComClass.cs`, `Accounts.cs`, `Rules.cs`, and focused `AccountsComContractTests.cs`/`RulesComContractTests.cs`. Focused Rules/Accounts/SQL coverage passes `38/38`; full Net10 passes `1307` with `3` opt-in tests skipped. No live SQL integration ran, PHP CLI remains unavailable, and no SMTP, service/database/Data-directory, COM registration/ACL, or SEC-18 staging state changed.
- The next bounded COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit; keep message mutation and broader account/admin mutation out of scope.

## SEC-11 Account.Messages Per-Account-State Visibility (2026-07-29)

- Legacy parity was confirmed in `HM::Account::GetMessages` (`hmailserver/source/Server/Common/BO/Account.cpp:107-116`), cached `Account::messages_` (`hmailserver/source/Server/Common/BO/Account.h:161`), `InterfaceAccount::get_Messages` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:420-445`), `HM::Messages::Refresh` (`hmailserver/source/Server/Common/BO/Messages.cpp:144-209`), and `Accounts::Refresh`/collection lookup lifetime (`hmailserver/source/Server/Common/BO/Accounts.cpp:24-55`, `hmailserver/source/Server/Common/BO/Collection.h:138-177,232-277`). Legacy creates one cached message collection per loaded account and fresh COM wrappers over it; Accounts.Refresh publishes new account objects while existing wrappers retain their old children.
- The .NET adapter now creates one lazy `AccountMessageAdministrationState` per attached account entry. Repeated `Account.Messages` calls return fresh `Messages` facades over one cached store snapshot; Accounts.Refresh publishes new account/message state while old Account wrappers retain their prior snapshot. Direct activation still returns `E_ACCESSDENIED`, authenticated Settings access is unchanged, and installed Account/Messages IID, vtable, DISPID, CLSID, and ProgID identity remains unchanged.
- Code/test commit `0c2ee1226` changes only `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AccountComClass.cs`, `Accounts.cs`, `Messages.cs`, and focused `AccountsComContractTests.cs`/`MessagesComContractTests.cs`. Focused Messages/Accounts/Application/SQL coverage passes `48/48`; full Net10 passes `1308` with `3` opt-in tests skipped. No live COM integration ran, PHP CLI remains unavailable, and no SMTP, service/database/Data-directory, COM registration/ACL, or SEC-18 staging state changed.
## SEC-11 Account.Messages SQL Projection Parity (2026-07-29)

- Legacy parity was confirmed in `HM::Account::GetMessages` (`hmailserver/source/Server/Common/BO/Account.cpp:107-116`), `HM::Messages::Refresh` (`hmailserver/source/Server/Common/BO/Messages.cpp:143-209`), `Messages::AddToCollection` (`hmailserver/source/Server/Common/BO/Messages.cpp:211-241`), and `InterfaceAccount::get_Messages` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:420-445`). Account-level `Messages(accountId, -1)` uses `messageaccountid = @MESSAGEACCOUNTID`, no message-type or folder predicate, and `ORDER BY messageuid ASC`; queue and folder branches remain distinct.
- The .NET `SqlServerMessageAdministrationStore.GetAccountMessagesSql` query now matches that account-level predicate/order. `GetFolderMessagesSql`, the message snapshot shape, COM identities, and IMAP/POP3/SMTP stores remain unchanged.
- Code/test commit `debc93dac` changes only `hmailserver/source/Server.Net10/src/HMailServer.Storage.SqlServer/SqlServerMessageAdministrationStore.cs` and `SqlServerMessageAdministrationStoreTests.cs`. Focused SQL/Message/Account coverage passes `38/38`; full Net10 passes `1308` with `3` opt-in tests skipped. No live SQL integration ran.
- The next bounded slice is authenticated per-account `Account.IMAPFolders` cached snapshot and shared folder-adapter visibility. Keep folder Add/Delete/Save/setters, ACL mutation, live protocol/cache synchronization, `CurrentUID` protocol semantics, SQL schema changes, COM identity, and SEC-18 work out of scope.

## SEC-11 Account.IMAPFolders Cached Shared Visibility (2026-07-29)

- Legacy parity was confirmed in `hmailserver/source/Server/COM/InterfaceAccount.cpp:817-840` (`InterfaceAccount::get_IMAPFolders`), `hmailserver/source/Server/IMAP/IMAPFolderContainer.cpp:38-159` (`GetFoldersForAccount`, `UncacheAccount`, `Clear`, `UpdateCurrentUID`), `hmailserver/source/Server/Common/BO/IMAPFolders.cpp:21-147,149-214,303-366`, `hmailserver/source/Server/COM/InterfaceIMAPFolders.cpp:31-214`, `hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:47-301`, and `hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:193-246`. The process-wide folder container is keyed by account ID: first access loads all `hm_imapfolders` rows in `folderid ASC` order, and fresh Account/child COM wrappers share the cached folder graph until explicit invalidation.
- The .NET path now adds an account-ID keyed lazy `ImapFolderAdministrationState` in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/IMAPFolders.cs`, carries it through `AccountComClass.cs` and `Accounts.cs`, and filters fresh root/child facades over that snapshot. `GetFoldersForAccountAsync` and the ordered all-folder SQL projection are in `IImapFolderAdministrationStore.cs` and `SqlServerImapFolderAdministrationStore.cs`.
- Code/test commit `893a5c768` changes only the Account/Accounts/IMAPFolders path, the IMAP folder store contract/SQL query, and focused COM/SQL test doubles and regressions. `ImapFoldersComContractTests`, `AccountsComContractTests`, `SqlServerImapFolderAdministrationStoreTests`, and `SettingsComContractTests` pass `50/50`; full Net10 passes `1311` with `3` opt-in tests skipped. No live COM or SQL integration ran.
- Protocol cache invalidation, `CurrentUID` synchronization, folder mutation, ACL mutation, SMTP/POP3 behavior, installed COM identity, and SEC-18 state remain unchanged. The next bounded slice is parent-account-scoped `FetchAccount.DownloadNow()` retry-now parity: legacy `InterfaceFetchAccount::DownloadNow` (`hmailserver/source/Server/COM/InterfaceFetchAccount.cpp:500-520`) calls `PersistentFetchAccount::SetRetryNow` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccount.cpp:203-210`) and wakes the external-fetch manager; current `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/FetchAccounts.cs:353` is `E_NOTIMPL`. Keep broader FetchAccount mutation and external-fetch runtime out of scope.

## SEC-11 FetchAccount DownloadNow Retry-Now Persistence (2026-07-30)

- Legacy parity was confirmed in `InterfaceFetchAccount::DownloadNow` (`hmailserver/source/Server/COM/InterfaceFetchAccount.cpp:500-520`), `PersistentFetchAccount::SetRetryNow` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccount.cpp:203-210`), and `SQLStatement::GetCurrentTimestamp` (`hmailserver/source/Server/Common/SQL/SQLStatement.cpp:435-449`). The attached COM item updates `hm_fetchaccounts.fanexttry` and returns `S_OK`; detached activation returns `E_ACCESSDENIED`. MSSQL uses `GETDATE()`. The installed `IInterfaceFetchAccount.DownloadNow` DISPID 14 remains anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1626-1656`.
- Code/test commits `3d234ccde` and `e05379132` add parent-scoped `SetRetryNowAsync`, a dedicated coalescing `IExternalFetchWakeSignal`, signal-after-successful-persistence wiring, idle worker wake-up, and MSSQL `GETDATE()` parity for lease/defer/complete `fanexttry` scheduling. Installed COM identity and DISPID 14 remain unchanged; direct activation remains denied and store failures map to `E_FAIL`. `FetchAccountsComContractTests`, `ExternalFetchWakeSignalTests`, `ExternalFetchHostedServiceTests`, `SqlServerExternalFetchAccountStoreTests`, `ProductionHostCompositionTests`, and `SettingsComContractTests` pass `42/42`; full Net10 passes `1320` with `3` opt-in tests skipped. No live COM or SQL integration ran.
 - Legacy `ExternalFetchManager::SetCheckNow` (`hmailserver/source/Server/ExternalFetcher/ExternalFetchManager.cpp:144-147`) is represented by the dedicated external-fetch signal; its delivery signal remains a separate instance. The subsequent attached existing-row `FetchAccount.Delete()` parity slice is complete in `cccc3e64c`; the bounded `BackupManager` archive/XML, non-secret raw settings-property, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, backup-side normal domain `Aliases`, backup-side `DistributionLists`, and backup-side account credential child-serialization slices are complete in `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, `3e7535d76`, `5d4981240`, and `fd30ceb33`; the next production-gate slice is backup-side `FetchAccounts` child serialization.

## SEC-11 FetchAccount Delete Parity (2026-07-30)

- Legacy parity was confirmed in `InterfaceFetchAccount::Delete` (`hmailserver/source/Server/COM/InterfaceFetchAccount.cpp:590-611`), `InterfaceAccount::get_FetchAccounts` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:694-720`), `FetchAccounts::Refresh` (`hmailserver/source/Server/Common/BO/FetchAccounts.cpp:35-43`), `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), and `PersistentFetchAccount::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccount.cpp:117-133`). An attached item requires domain-admin authentication, normal deletion is routed through the owning collection, and persistence deletes the selected `hm_fetchaccounts` row followed by its `hm_fetchaccounts_uids` rows. The installed `IInterfaceFetchAccount.Delete` DISPID 17 and interface/class identities remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1620-1686` and existing registration resources.
- Code/test commit `cccc3e64c` adds owner-scoped SQL deletion, parent-row affected-row protection before UID cleanup, selected-child-only snapshot removal after successful persistence, and synchronous `FetchAccount.Delete()` HRESULT mapping. Direct activation remains `E_ACCESSDENIED`; collection-wide `FetchAccounts.Delete`/`DeleteByDBID` remains fenced; unrelated Add/Save/password/setter, POP3, SMTP trust, live reconfiguration, and SEC-18 behavior were not changed. Focused FetchAccounts/SQL coverage passes `19/19`; full Net10 passes `1324` with `3` opt-in tests skipped.
 - The bounded `BackupManager` archive/XML, non-secret raw settings-property, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, backup-side normal domain `Aliases`, backup-side `DistributionLists`, and backup-side account credential child-serialization slices are complete in `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, `3e7535d76`, `5d4981240`, and `fd30ceb33`. The next production-gate slice is backup-side `FetchAccounts` child serialization; restore execution, message/data-directory work, destructive SQL, event dispatch, and PHP session cutover remain out of scope.

## SEC-11 RuleCriteria UsePredefined Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_UsePredefined` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,105-120`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:65-100`). The attached setter stores exactly `newVal == VARIANT_TRUE` and returns `S_OK`; detached objects return `E_ACCESSDENIED`; Save persists `criteriausepredefined` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,2844-2849,3185-3200`. The .NET contracts, `VARIANT_BOOL` marshaling, authenticated access boundary, and direct activation denial remain unchanged.
- Code/test commit `a4ff728c0` changes only the authorized `RuleCriteria.UsePredefined` setter to use the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `35/35`; full Net10 passes `1288` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is authenticated existing-row `RuleCriteria.MatchType` setter parity through the same owning `RuleCriteria.Save()` path.

## Historical Next Slice Record (superseded)

Authoritative update for 2026-07-28: the TCP/IP handler hardening is complete in `272d56b5c`, and the SSL certificate handler hardening is complete in `4ed9d2f26`. `background_sslcertificate_save.php` now requires POST plus POST-body CSRF before reading `action`, `id`, `Name`, `CertificateFile`, and `PrivateKeyFile`; its existing server-admin guard, `Settings -> SSLCertificates` lookup, Add/Edit/DeleteByDBID, certificate field assignments, Save, and redirects are unchanged. Legacy references are `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-128`), `InterfaceSettings::get_SSLCertificates` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1212-1237`), `InterfaceSSLCertificates` (`hmailserver/source/Server/COM/InterfaceSSLCertificates.cpp:75-196`), `InterfaceSSLCertificate` (`hmailserver/source/Server/COM/InterfaceSSLCertificate.cpp:13-171`), and `PersistentSSLCertificate::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentSSLCertificate.cpp:25-86`). Installed SSL IIDs, vtable/DISPID order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:620,2514-2553,2844-2853` and the SSL registry resources. Focused SSL WebAdmin/COM/store coverage passes `18/18`; full Net10 passes `1267` with `3` opt-in tests skipped. The edit and delete forms already post CSRF-bearing fields; PHP CLI is unavailable. `background_iphome_save.php` remains an orphaned pre-5.0 handler because `hm_iphomes` was dropped by `Upgrade4402to5000{MySQL,MSSQL}.sql`, with no current C++/IDL/.NET IPHome surface or WebAdmin form. The next live WebAdmin mutation is `hmailserver/source/WebAdmin/background_surblserver_save.php`; keep SSL runtime listener reconfiguration, certificate-file policy, TCPIP COM mutation implementation, SMTP behavior, SEC-18 broker registration, and PHP session cutover fenced.

Historical SecurityRanges and prior WebAdmin completion summary: SecurityRanges Add/new-item Save insert parity is complete in `5c7c1010e`, authenticated `Settings -> SecurityRanges.DeleteByDBID` membership-scoped deletion is complete in `864d2e6d6`, owning-collection `SecurityRange.Delete()` parity is complete in `641599b5c`, collection `Settings -> SecurityRanges.Delete(index)` parity is complete in `77e16bd4d`, authenticated existing-row `SecurityRange.Save()` update parity is complete in `02e445a5c`, and authenticated `Settings -> SecurityRanges.SetDefault()` parity is complete in `56a668256`. `SetDefault()` performs the legacy refresh, deletes the refreshed owning IDs, inserts exact `My computer` and `Internet` defaults, and refreshes the final ordered snapshot; contained .NET store failures map to COM `E_FAIL` and retain the last published snapshot. Existing items from index, DBID, and name lookups stage through the owning save delegate; Save rechecks server-admin authorization, validates duplicate names and IP range shape, updates all persisted columns through a parameterized store operation, and replaces only the matching owning snapshot after success without reordering it. Legacy validation failures use `0x800403E9`. Direct activation remains `E_ACCESSDENIED`, and the installed COM contracts remain unchanged. Focused SecurityRanges/store tests pass `29/29`; full Net10 passes `1260` with `3` opt-in tests skipped, and no live SQL integration ran because the approved isolated connection was unset. SEC-14 performance, backup, auto-ban, greylisting, logging, scripting, diagnostics, status, TLS, POP3, IMAP, SMTP, SMTP AntiVirus, SMTP AntiSpam, and Mirror WebAdmin mutation hardening is complete in `7c7ca1049`, `7338030e6`, `a5384ae1b`, `28a830f5f`, `363a9cfb8`, `8894239af`, `5e49e73e1`, `ba2261292`, `1bd30eea`, `c28d23d79`, `5e694f49c`, `122847319`, `68d6f0006`, `8c751e65f`, and `9740cbc62`. The authoritative next slice is recorded above.

The message-indexing SQL/COM integration test is explicit opt-in and creates then drops a GUID-named isolated database; it never runs against the database named in the supplied connection string:

```powershell
$env:HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\test-net10.ps1 -Configuration Debug
```

## IMAP Listener

The .NET 10 IMAP TCP listener is wired into the Windows service but disabled by default while full command parity is still being rebuilt. Enable it for controlled compatibility testing:

```powershell
$env:HMAILSERVER_IMAP_ENABLED = "true"
$env:HMAILSERVER_IMAP_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_IMAP_PORT = "1143"
$env:HMAILSERVER_IMAP_HIERARCHY_DELIMITER = "."
$env:HMAILSERVER_IMAP_PUBLIC_FOLDER_NAME = "#Public"
$env:HMAILSERVER_IMAP_USE_ACL = "true"
$env:HMAILSERVER_IMAP_IDLE_POLL_MS = "5000"
$env:HMAILSERVER_IMAP_REQUIRE_TLS_FOR_AUTH = "false"
```

The listener supports `LOGIN`, `AUTHENTICATE PLAIN` with SASL-IR or continuation response, nested-folder `LIST`/`LSUB`/`SELECT`/`EXAMINE`, public-folder ACL-aware discovery and selection, `STATUS`, `SEARCH`, `UID SEARCH`, `SORT`, `UID SORT`, `FETCH`, `UID FETCH`, `STORE`, `UID STORE`, `COPY`, `UID COPY`, `MOVE`, `UID MOVE`, `APPEND`, `EXPUNGE`, `IDLE`/`DONE`, ACL commands (`GETACL`, `SETACL`, `DELETEACL`, `LISTRIGHTS`, `MYRIGHTS`), and QUOTA commands (`GETQUOTA`, `GETQUOTAROOT`, `SETQUOTA`) against SQL Server-backed accounts and folders. SEARCH and SORT use SQL predicates plus SQL Server Full-Text Search candidate sets; message sequence-set criteria, internal-date (`SINCE`/`BEFORE`/`ON`), and sent-date (`SENTSINCE`/`SENTBEFORE`/`SENTON`) filters stay in SQL, with sent dates using indexed metadata when available. SORT orders by existing message metadata without reading message files. Selected mailboxes keep an in-session `\Recent` UID snapshot: SELECT clears DB recent flags after capture, EXAMINE preserves them, and APPEND/COPY/MOVE update the active snapshot. IDLE can stream formatted `EXISTS`, `RECENT`, `EXPUNGE`, and `FETCH FLAGS` events, with the default notifier polling SQL mailbox status on the configured interval. ACL commands reuse the existing `hm_acl` public-folder model with account, group, and `Anyone` principals. QUOTA commands use live `hm_messages.messagesize` usage with `accountmaxsize` and domain per-account limits. FETCH metadata stays SQL-only unless RFC822/body literals, `ENVELOPE`, or `BODYSTRUCTURE` are requested; those MIME responses read from the existing hMailServer data directory only when needed. STORE updates message flags and the search document mirror; COPY/MOVE allocate destination UIDs, copy message files, and queue destination messages for search indexing; APPEND accepts synchronizing literals, writes new message files, allocates destination UIDs, and queues indexing; EXPUNGE deletes `\Deleted` messages plus search/metadata rows and removes message files after the DB transaction commits. Plaintext, legacy Blowfish, legacy MD5, and legacy salted SHA256 account passwords are supported. `HMAILSERVER_IMAP_REQUIRE_TLS_FOR_AUTH=true` suppresses `AUTH=PLAIN` on clear connections and rejects cleartext `LOGIN`/`AUTHENTICATE`. With scripting enabled, IMAP runs optional `OnClientLogon(HMAILSERVER_CLIENT)` after successful and failed authentication attempts with endpoint, session, TLS, username, and authenticated-state fields. Failed IMAP/SMTP/POP3 authentication attempts flow through the SQL auto-ban recorder, which mirrors legacy failed-logon settings, `hm_logon_failures`, deny `hm_securityranges` creation, and threshold-triggered disconnects. Active Directory, master user, remaining authentication script hooks, and the rest of the IMAP command set remain on the parity backlog.

The shared SQL account authenticator used by IMAP and SMTP AUTH runs optional `OnClientValidatePassword(HMAILSERVER_ACCOUNT, password)` before built-in password verification; legacy `Result.Value = 0` accepts, `1` rejects, and any other value continues normal verification. The account facade exposes common scalar legacy fields including ID, address, stored password, active/AD flags, AD domain/user, domain ID, max size, person name fields, admin level, vacation, forwarding, signature, and last-logon values. `HMAILSERVER_ACCOUNT.Password` is the stored legacy account value already loaded from SQL, while the handler's `password` argument remains the separate plaintext attempt. Both VBScript and JScript password-validation handlers receive the full scalar `Result` shape, including a writable `Parameter` initialized to the legacy numeric default of zero; VBScript seeds it explicitly instead of exposing an uninitialized `Empty` field.

With scripting enabled, the IMAP listener also runs `OnClientConnect(HMAILSERVER_CLIENT)` before its greeting and closes the connection without a greeting when legacy `Result.Value = 1`. The client facade includes the remote endpoint and allocated session ID.

For low-level search engine testing, a fixed preselected account/folder context can still be injected:

```powershell
$env:HMAILSERVER_IMAP_ACCOUNT_ID = "1"
$env:HMAILSERVER_IMAP_FOLDER_ID = "1"
```

The listener uses bounded concurrent connections, `TcpClient.NoDelay`, fixed socket buffers, and a stream factory boundary so STARTTLS/implicit TLS can be added without changing the IMAP session engine.

## SMTP Listener

The .NET 10 SMTP TCP listener is also wired into the Windows service and disabled by default while receive/delivery parity is rebuilt. Enable the current protocol skeleton for controlled testing:

```powershell
$env:HMAILSERVER_SMTP_ENABLED = "true"
$env:HMAILSERVER_SMTP_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_SMTP_PORT = "2525"
$env:HMAILSERVER_SMTP_MAX_CONNECTIONS = "1000"
$env:HMAILSERVER_SMTP_SERVER_NAME = "mx.example.test"
$env:HMAILSERVER_SMTP_MAX_MESSAGE_BYTES = "20971520"
$env:HMAILSERVER_SMTP_DISCONNECT_INVALID_CLIENTS = "false"
$env:HMAILSERVER_SMTP_MAXIMUM_INCORRECT_COMMANDS = "100"
$env:HMAILSERVER_SMTP_REQUIRE_TLS_FOR_AUTH = "false"
$env:HMAILSERVER_SMTP_TLS_CERTIFICATE_PATH = "C:\certs\mx.example.test.pfx"
$env:HMAILSERVER_SMTP_TLS_CERTIFICATE_PASSWORD = "changeit"
$env:HMAILSERVER_SCRIPTING_ENABLED = "false"
$env:HMAILSERVER_SCRIPTING_LANGUAGE = "VBScript"
$env:HMAILSERVER_SCRIPT_EVENT_DIRECTORY = "C:\Program Files (x86)\hMailServer\Events"
$env:HMAILSERVER_SCRIPT_EVENT_LOG_PATH = "C:\Program Files (x86)\hMailServer\Logs\hmailserver_events.log"
$env:HMAILSERVER_SCRIPT_TIMEOUT_MS = "5000"
$env:HMAILSERVER_CLAMAV_ENABLED = "false"
$env:HMAILSERVER_CLAMAV_HOST = "127.0.0.1"
$env:HMAILSERVER_CLAMAV_PORT = "3310"
$env:HMAILSERVER_CLAMAV_TIMEOUT_SECONDS = "30"
$env:HMAILSERVER_CLAMAV_CHUNK_SIZE_BYTES = "65536"
$env:HMAILSERVER_SPAMASSASSIN_ENABLED = "false"
$env:HMAILSERVER_SPAMASSASSIN_HOST = "127.0.0.1"
$env:HMAILSERVER_SPAMASSASSIN_PORT = "783"
$env:HMAILSERVER_SPAMASSASSIN_TIMEOUT_SECONDS = "30"
$env:HMAILSERVER_SPAMASSASSIN_MAX_RESPONSE_HEADER_BYTES = "16384"
$env:HMAILSERVER_SPAMASSASSIN_MAX_RESPONSE_BYTES = "104857600"
$env:HMAILSERVER_SPAM_POLICY_ADD_HEADER_SPAM = "false"
$env:HMAILSERVER_SPAM_POLICY_ADD_REASON_HEADERS = "false"
$env:HMAILSERVER_SPAM_POLICY_PREPEND_SUBJECT = "false"
$env:HMAILSERVER_SPAM_POLICY_MARK_THRESHOLD = "0"
$env:HMAILSERVER_SPAM_POLICY_DELETE_THRESHOLD = "0"
$env:HMAILSERVER_SPAM_POLICY_SUBJECT_PREFIX = "[SPAM]"
$env:HMAILSERVER_SPAM_POLICY_MAX_HEADER_VALUE_LENGTH = "900"
$env:HMAILSERVER_SPF_ENABLED = "false"
$env:HMAILSERVER_SPF_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_SPF_FAIL_SCORE = "3"
$env:HMAILSERVER_SPF_TIMEOUT_SECONDS = "20"
$env:HMAILSERVER_DKIM_ENABLED = "false"
$env:HMAILSERVER_DKIM_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_DKIM_FAILURE_SCORE = "5"
$env:HMAILSERVER_DMARC_ENABLED = "false"
$env:HMAILSERVER_DMARC_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_DMARC_MARK_FAILURES_AS_SPAM = "false"
$env:HMAILSERVER_DMARC_FAILURE_SCORE = "5"
$env:HMAILSERVER_DMARC_PUBLIC_SUFFIX_LIST = "C:\path\to\public_suffix_list.dat"
$env:HMAILSERVER_ATTACHMENT_BLOCKING_ENABLED = "false"
$env:HMAILSERVER_ATTACHMENT_BLOCKING_WILDCARDS = "*.exe;*.bat;*.cmd;*.scr"
$env:HMAILSERVER_ATTACHMENT_BLOCKING_REPLACEMENT_TEXT = "The attachment %MACRO_FILE% was removed because it matched an attachment blocking rule."
$env:HMAILSERVER_DNSBL_ENABLED = "false"
$env:HMAILSERVER_DNSBL_ZONES = "zen.spamhaus.org;bl.spamcop.net"
$env:HMAILSERVER_DNSBL_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_DNSBL_TIMEOUT_SECONDS = "5"
$env:HMAILSERVER_DNSBL_REJECTION_MESSAGE = "554 Rejected by DNS blocklist {ListHost}"
$env:HMAILSERVER_REVERSE_DNS_ENABLED = "false"
$env:HMAILSERVER_REVERSE_DNS_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_REVERSE_DNS_REQUIRE_FORWARD_CONFIRMED = "true"
$env:HMAILSERVER_REVERSE_DNS_TIMEOUT_SECONDS = "5"
$env:HMAILSERVER_REVERSE_DNS_REJECTION_MESSAGE = "554 Rejected by reverse DNS check {Reason}"
$env:HMAILSERVER_SENDER_DOMAIN_MX_ENABLED = "false"
$env:HMAILSERVER_SENDER_DOMAIN_MX_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_SENDER_DOMAIN_MX_TIMEOUT_SECONDS = "5"
$env:HMAILSERVER_SENDER_DOMAIN_MX_REJECTION_MESSAGE = "554 Sender domain does not have any MX records"
$env:HMAILSERVER_GREYLISTING_ENABLED = "false"
$env:HMAILSERVER_GREYLISTING_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_GREYLISTING_BYPASS_ON_SPF_PASS = "false"
$env:HMAILSERVER_GREYLISTING_INITIAL_DELAY_MINUTES = "30"
$env:HMAILSERVER_GREYLISTING_INITIAL_RECORD_LIFETIME_HOURS = "24"
$env:HMAILSERVER_GREYLISTING_PASSED_RECORD_LIFETIME_HOURS = "864"
$env:HMAILSERVER_GREYLISTING_FAILURE_RESPONSE = "451 Please try again later."
$env:HMAILSERVER_SURBL_ENABLED = "false"
$env:HMAILSERVER_SURBL_ZONES = "multi.surbl.org"
$env:HMAILSERVER_SURBL_SKIP_AUTHENTICATED = "true"
$env:HMAILSERVER_SURBL_TIMEOUT_SECONDS = "5"
$env:HMAILSERVER_SURBL_MAX_HOSTS = "50"
$env:HMAILSERVER_SURBL_MAX_CANDIDATE_DOMAINS_PER_HOST = "3"
$env:HMAILSERVER_SURBL_REJECTION_MESSAGE = "554 Rejected by URL blocklist {ListHost}"
```

The SMTP skeleton sends an ESMTP greeting, supports `EHLO`, `HELO`, `NOOP`, `RSET`, `QUIT`, server-side `STARTTLS`, `AUTH PLAIN`, `AUTH LOGIN`, and stages `MAIL`/`RCPT`/`DATA` transactions through `ISmtpMessageReceiver`. When a PFX certificate path is configured, `EHLO` advertises `STARTTLS`, the session upgrades the active stream through `SslStream`, clears pre-TLS session knowledge, and can require TLS before AUTH with `HMAILSERVER_SMTP_REQUIRE_TLS_FOR_AUTH=true`; without a certificate the listener preserves the existing plaintext-only behavior and returns `454 TLS not available` for explicit `STARTTLS`. The protocol layer handles declared and actual message-size checks, dot-terminated DATA reads, and dot-stuffing before handing the raw message bytes to storage.

Before queue persistence, the SQL Server receiver loads active global legacy rules from `hm_rules`, `hm_rule_criterias`, and `hm_rule_actions`; it evaluates basic criteria (`FROM`, `TO`, `CC`, `SUBJECT`, `BODY`, `MESSAGE SIZE`, `RECIPIENT LIST`, `DELIVERY ATTEMPTS`, or a named header) and applies `Delete`, `SetHeaderValue`, `StopRuleProcessing`, `Forward`, `CreateCopy`, `Reply`, `SendUsingRoute`, and `BindToAddress`. `ScriptFunction` actions flow through a Windows-only process-isolated `cscript.exe` executor when scripting is enabled; it loads `EventHandlers.vbs` or `EventHandlers.js`, calls the configured function, allows the script to mutate the message file, and exposes a file-backed `HMAILSERVER_MESSAGE` facade with `FileName`/`Filename`, `DropMessage`, `RejectReason`, `ID`, `UID`, `State`, `Flag(eMessageFlag)`, `Size`, `DeliveryAttempt`, `InternalDate`, `EncodeFields`, `Charset`, `HasBodyType`, `Subject`, `From`, `To`, `CC`, `Date`, `Body`, `HTMLBody`, `HeaderValue`, `SetHeaderValue`, `Headers` (`Count`, `Item`, `ItemByName`, `Name`, `Value`, `Delete`), `Load`/`RefreshContent`, `Save`, `Copy(destinationFolderId)`, `FromAddress`, `Recipients`, `AddRecipient`, `ClearRecipients`, and `Attachments` (`Count`, `Item`, attachment `FileName`/`Filename`/`Size`, `SaveAs`, `Delete`, `Clear`, `Add`). The script host exposes the global legacy `EventLog.Write(value)` facade for VBScript/JScript handlers and appends Unicode rows to `HMAILSERVER_SCRIPT_EVENT_LOG_PATH` using the legacy event-log line shape with CR/LF sanitized to `[nl]`. With scripting enabled, SMTP runs optional `OnClientConnect(HMAILSERVER_CLIENT)` before greeting, `OnHELO(HMAILSERVER_CLIENT)` before sending HELO/EHLO success, `OnClientLogon(HMAILSERVER_CLIENT)` after AUTH attempts, `OnRecipientUnknown(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` for unknown RCPT targets, `OnSMTPData(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` after DATA is read and before receiver/queue processing, `OnAcceptMessage(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` before global rule processing, and `OnTooManyInvalidCommands(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` when the configured invalid-command disconnect threshold is exceeded; the client facade exposes HELO, authentication, TLS, and endpoint fields, including legacy `Authenticated`/`EncryptedConnection` aliases, while global `Result.Value`/`Result.Message` can reject with legacy `554`/`453` responses. Deeper legacy COM/script object collections and methods remain on the parity backlog.

The `Load`/`RefreshContent` script methods reload file-backed headers and body after a handler rewrites the message file directly, matching the legacy COM refresh hook.

The message file path facade remains tied to the original script backing file: VBScript treats `Filename` as a read-only legacy property, and JScript `FileName`/`Filename` assignments do not redirect `Load`, `Save`, or `Copy` file I/O.

The `To`/`CC` message properties follow the legacy read-only COM shape for direct script assignment; supported recipient/header mutations still go through `AddRecipient`, `ClearRecipients`, `Recipients`, or `HeaderValue`.

Attachment `FileName`/`Filename` and `Size` metadata follow the legacy read-only COM shape. VBScript rejects direct assignment, while JScript assignment cannot mutate the attachment collection's backing metadata; attachment changes remain available through `Add`, `Clear`, and `Delete`.

Message queue metadata (`ID`, `UID`, `State`, `DeliveryAttempt`, and `InternalDate`) also follows the legacy read-only COM shape. VBScript rejects direct assignment, while JScript restores canonical seeded values at `Load`, `Save`, and `Copy` boundaries; 64-bit message IDs remain intact. Legacy message state and flags are distinct: delivery events expose `State = 1` (`Delivering`) while `Flag(eMessageFlag)` reads and mutates the separately seeded queue flags without changing `State`.

Message `Size` follows the legacy read-only, floor-KiB calculation (`bytes / 1024` using integer division), so messages smaller than 1024 bytes report `0`. Both script facades re-read the backing file size after `Save`; JScript direct assignment cannot replace the canonical value.

Recipient item `Address`, `OriginalAddress`, and `IsLocalUser` metadata follows the legacy read-only COM shape. VBScript rejects direct assignment and JScript returns detached item snapshots so assignment cannot mutate the recipient collection; supported envelope/header changes remain available through message-level `AddRecipient` and `ClearRecipients`.

The `Recipients` collection keeps the legacy supported surface of `Count` and `Item`; rewrite-internal seeding helpers are no longer published under the non-legacy `Add`, `Clear`, or `ToHeaderValue` names. Recipient mutation continues through message-level `AddRecipient` and `ClearRecipients` in both VBScript and JScript.

Message-level `AddRecipient` follows the legacy MIME header shape: every added recipient uses a quoted display name, including an empty name, and consecutive entries are joined with a comma without added whitespace.

Message-level `ClearRecipients` follows the legacy envelope and MIME cleanup behavior: it clears the recipient collection and removes the `To`, `Cc`, and `Bcc` headers in both script languages.

Message-level `Save` follows the legacy missing-date behavior: when the message has no `Date` value, both script facades generate a current local MIME date before writing the message file.

Non-empty `Body` and `HTMLBody` assignments follow the legacy message-data behavior by ending the persisted value with `CRLF` when the script did not supply it; empty bodies remain empty.

The `Headers` collection keeps the legacy supported surface of `Count`, `Item`, and `ItemByName`; runner-internal refresh/commit helpers are no longer published as `Refresh` or `Commit`. Header item `Name`/`Value` updates and `Delete` continue to persist through `Message.Save` in both script languages.

`Recipients.Item`, `Headers.Item`, and `Headers.ItemByName` follow the legacy bad-index contract: out-of-range indexes and missing header names raise script errors instead of returning `Nothing` or `null`.

The `Attachments` collection keeps the legacy supported surface of `Count`, `Item`, `Clear`, and `Add`; runner-internal manifest loading and index removal are no longer published as `Load` or `DeleteAt`. Attachment item `SaveAs` and `Delete` continue to work in VBScript and JScript.

`Attachments.Add` follows the legacy failure contract in both script languages: a missing source file raises `Failed to attach file.` instead of silently leaving the collection unchanged.

`Attachments.Item` also follows the legacy bad-index contract: an index outside the collection raises a script error instead of returning `Nothing` or `null`.

Attachment items preserve legacy object identity across collection mutations: deleting an earlier item does not retarget a previously captured attachment object's `Delete` call to a different MIME part.

Message `HasBodyType` now follows the legacy clean MIME content-type lookup across the root part and two nested part levels instead of matching arbitrary header/body text. Matching is case-insensitive, and quoted boundary parameters, including boundary values containing semicolons, are supported in both VBScript and JScript facades.

The scripting logger provider dispatches optional `OnError(iSeverity, iError, sSource, sDescription)` handlers for .NET `Warning`, `Error`, and `Critical` records as legacy severity values `3`, `2`, and `1`. The logging `EventId` becomes the error code, the logger category becomes the source, exception details are appended to the formatted description, execution is timeboxed/fail-open, and recursive logging from the handler is suppressed. All legacy protocol and delivery event names are now connected; backup-completion/failure events await the .NET backup engine.

When `HMAILSERVER_CLAMAV_ENABLED=true`, the service registers the async/timeboxed ClamAV `INSTREAM` scanner and runs it on SMTP messages after `OnAcceptMessage`, global rules, spam policy, and optional attachment blocking have had a chance to mutate the message but before the queue row and data-directory file are written. ClamAV protocol errors or timeouts fail closed with a transient SMTP rejection, while infected messages are rejected with a permanent virus response. External POP3 fetch also uses the same scanner for accounts with `UseAntiVirus` enabled; infected remote UIDs are retained/deleted according to the fetch account retention decision without queueing the message again.

When `HMAILSERVER_SPAMASSASSIN_ENABLED=true`, the service registers the async/timeboxed SpamAssassin `PROCESS SPAMC/1.2` client and runs it on messages before antivirus scanning and queue persistence. Valid spamd responses replace the message with SpamAssassin's processed message bytes, including `X-Spam-Status` headers; invalid headers, negative/missing `Content-length`, partial bodies, socket errors, and timeouts preserve the original message and continue delivery. External POP3 fetch passes each account's `UseAntiSpam` setting into the SMTP receiver path so fetched messages can opt in or out of the same scanner. Optional spam policy settings can add legacy `X-hMailServer-Spam`, `X-hMailServer-Reason-*`, and subject-prefix mutations after a successful spam scan and before antivirus/queue persistence. `HMAILSERVER_SPAM_POLICY_MARK_THRESHOLD` marks queue rows with the legacy spam flag (`eMFSpam = 128`) when the scan score reaches the configured threshold, even if header mutation is disabled. `HMAILSERVER_SPAM_POLICY_DELETE_THRESHOLD` rejects matching SMTP messages with `554` before antivirus scanning and queue persistence.

When `HMAILSERVER_SPF_ENABLED=true`, the SMTP receiver evaluates the envelope sender SPF identity through the bounded RFC 7208 evaluator and the OS-configured DNS servers before greylisting, scripts, rules, SpamAssassin, antivirus, and queue persistence. SPF is disabled by default, authenticated SMTP clients are skipped by default, and `EnableSpamScan=false` also skips this path for external fetch compatibility. The current policy boundary preserves the legacy safe subset: only SPF `Fail` marks the queued message with the legacy spam flag and increments the spam counter; SPF `Pass` can bypass greylisting only when `HMAILSERVER_GREYLISTING_BYPASS_ON_SPF_PASS=true`, while `None`, `Neutral`, `SoftFail`, `TempError`, and `PermError` fail open without greylisting bypass, reject, or tempfail behavior.

When `HMAILSERVER_DMARC_ENABLED=true`, the SMTP receiver evaluates the RFC5322.From domain after DKIM verification using the existing SPF/DKIM pass results. DMARC remains disabled by default and never directly rejects or quarantines SMTP messages; policy failures enter the existing spam-flag path only when `HMAILSERVER_DMARC_MARK_FAILURES_AS_SPAM=true`. Organizational-domain lookup uses an offline public suffix list from `HMAILSERVER_DMARC_PUBLIC_SUFFIX_LIST`, or the pinned `public_suffix_list.dat` packaged beside the service executable. The local list is loaded lazily through Nager.PublicSuffix; no online list download occurs in the SMTP path. A missing, invalid, or unreadable list fails open to exact-domain DMARC lookup, while a valid list enables parent-record fallback, `sp=` policy selection, and relaxed sibling-domain alignment.

The pinned Public Suffix List snapshot and deterministic metadata live under `assets/`. Service build and publish copy both files to the output directory, and the build fails if the offline metadata, upstream headers, byte length, or SHA-256 no longer match. Maintainers can verify the committed snapshot without network access:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\update-public-suffix-list.ps1 -Check
```

Refreshing is an explicit maintainer operation, never a runtime action. Verify the upstream commit and downloaded bytes independently, then supply both pins; the script refuses a mismatch and writes no retrieval timestamp so repeated refreshes of the same bytes remain deterministic:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\update-public-suffix-list.ps1 -Update `
  -ExpectedCommit <lowercase-40-character-commit> `
  -ExpectedSha256 <lowercase-64-character-sha256>
```

When `HMAILSERVER_ATTACHMENT_BLOCKING_ENABLED=true`, the SMTP receiver applies a MIME-aware attachment policy after spam processing and before antivirus/queue persistence. Matching wildcards from `HMAILSERVER_ATTACHMENT_BLOCKING_WILDCARDS` are case-insensitive; entries such as `.exe` or `exe` normalize to `*.exe`. Matching attachments are replaced in-place with a plain-text attachment named `<original>.txt`, and `%MACRO_FILE%` in `HMAILSERVER_ATTACHMENT_BLOCKING_REPLACEMENT_TEXT` expands to the original file name. Messages are preserved unchanged when MIME parsing fails or no wildcard matches.

When `HMAILSERVER_DNSBL_ENABLED=true`, the SMTP receiver checks the connecting client IP against `HMAILSERVER_DNSBL_ZONES` before scripts, rules, spam scanning, antivirus scanning, and queue persistence. IPv4 addresses use the standard reversed-octet query form, IPv6 addresses use reversed nibbles, and authenticated SMTP clients are skipped by default through `HMAILSERVER_DNSBL_SKIP_AUTHENTICATED=true`. A positive DNS response rejects the message with `HMAILSERVER_DNSBL_REJECTION_MESSAGE`; DNS lookup failures, NXDOMAIN responses, and the bounded timeout fail open so mail receiving is not made dependent on a blocklist outage.

When `HMAILSERVER_REVERSE_DNS_ENABLED=true`, the SMTP receiver performs a bounded PTR check before scripts, rules, spam scanning, antivirus scanning, and queue persistence. Authenticated SMTP clients are skipped by default. When `HMAILSERVER_REVERSE_DNS_REQUIRE_FORWARD_CONFIRMED=true`, at least one PTR hostname must resolve back to the connecting IP address; missing PTR records or forward-confirmation failures reject with `HMAILSERVER_REVERSE_DNS_REJECTION_MESSAGE`, while transient DNS errors and timeouts fail open.

When `HMAILSERVER_SENDER_DOMAIN_MX_ENABLED=true`, the SMTP receiver checks the envelope sender domain for MX records before scripts, rules, spam scanning, antivirus scanning, and queue persistence. Null reverse-path bounces, IP/domain literals, malformed sender values, and authenticated SMTP clients are skipped by default. Missing MX records reject with `HMAILSERVER_SENDER_DOMAIN_MX_REJECTION_MESSAGE`; transient DNS errors, timeouts, SERVFAIL/REFUSED responses, and missing local DNS resolver configuration fail open so mail receiving is not made dependent on DNS availability.

When `HMAILSERVER_GREYLISTING_ENABLED=true`, the SMTP receiver checks each `client IP + envelope sender + recipient` triplet against the legacy `hm_greylisting_triplets` table before scripts, rules, spam scanning, antivirus scanning, and queue persistence. New or still-delayed triplets return `HMAILSERVER_GREYLISTING_FAILURE_RESPONSE` (`451` by default); triplets whose block window has elapsed are accepted and have their passed lifetime extended. Authenticated SMTP clients are skipped by default, `hm_greylisting_whiteaddresses` wildcard entries are honored, and SQL errors fail open to avoid turning greylisting storage issues into mail loss. `HMAILSERVER_GREYLISTING_BYPASS_ON_SPF_PASS=false` by default preserves normal greylisting; when enabled, only an SPF `Pass` result bypasses the greylisting lookup.

When `HMAILSERVER_SURBL_ENABLED=true`, the SMTP receiver extracts bounded URL hosts from MIME `text/plain` and `text/html` parts after spam processing and attachment blocking, checks each host plus a small bounded set of parent domains against `HMAILSERVER_SURBL_ZONES`, and rejects positive DNS responses before antivirus/queue persistence. `EnableSpamScan=false` skips this URL blocklist path, so external fetch accounts can keep using their existing `UseAntiSpam` setting. DNS lookup failures and timeouts fail open.

Generated rule messages are written through the same atomic queue writer, increment `X-hMailServer-LoopCount`, honor `HMAILSERVER_SMTP_RULE_LOOP_LIMIT`, preserve generated-message recipients even when the source message is deleted by rule, create `Auto-Submitted: auto-replied` messages for `Reply`, skip auto-submitted reply sources, and `CreateCopy` adds `X-CopyRule`. The default SQL Server receiver writes the resulting message file under the hMailServer data directory, inserts a locked `hm_messages` delivery-queue row (`messagetype = 1`), writes `hm_messagerecipients`, persists rule-forced route/bind delivery metadata, and unlocks the queue row in one transaction. `RCPT TO` validation resolves active local domains, domain aliases, plus-addressing, active accounts, aliases, public/authorized distribution lists, postmaster catch-all, and configured routes before queueing; resolved local accounts populate `recipientlocalaccountid`, while route recipients carry route id/target metadata for the delivery worker. Delivery queue groundwork now leases queued rows, reloads the leased message and recipients, runs optional `OnDeliveryStart(HMAILSERVER_MESSAGE)` and `OnDeliverMessage(HMAILSERVER_MESSAGE)` script events before target resolution, persists script message-file mutations back to the queue file, classifies batches as local account, configured route, rule-forced route, or remote domain, and hands those batches to a dispatcher boundary. Delivery script events seed `HMAILSERVER_MESSAGE.ID`, `UID`, `State`, `DeliveryAttempt`, and `InternalDate` from the queued message metadata instead of placeholder values. The local mailbox writer copies leased queue messages into account Inbox folders, applies account-level rules against that per-account copy for `Delete`, `SetHeaderValue`, `StopRuleProcessing`, `MoveToIMAPFolder`, `Forward`, `CreateCopy`, and `Reply`, resolves rule destinations through the IMAP mailbox store, allocates UIDs for messages that remain, inserts delivered `hm_messages`, and queues search indexing. Account-rule `Message.Copy(folderId)` calls capture the message file at call time, require the destination folder to belong to the same account, allocate a distinct UID, write a delivered `hm_messages` row and content file, and queue the copy for full-text indexing even when a later rule deletes the source delivery. The remote SMTP sender handles route and remote-domain batches with EHLO/HELO, optional/required STARTTLS hooks, route AUTH LOGIN, rule-level local bind addresses, dot-stuffed DATA delivery, system-DNS MX lookup with TTL/negative cache, domain fallback, and per-domain/route concurrency limiting. Delivery failure handling classifies 4xx as transient and 5xx as permanent, runs optional `OnDeliveryFailed(HMAILSERVER_MESSAGE, recipient, error)` for final failed recipients, applies bounded retry/backoff, drops successful recipient batches from the queue to avoid duplicate delivery, and submits bounce messages for permanent failures or retry-limit exhaustion. Bounce subjects and bodies are templateable through `HMAILSERVER_DELIVERY_BOUNCE_SUBJECT_TEMPLATE`, `HMAILSERVER_DELIVERY_BOUNCE_BODY_TEMPLATE`, and `HMAILSERVER_DELIVERY_BOUNCE_MAX_FAILURE_DESCRIPTION_LENGTH`; templates support `{MessageId}`, `{MessageUid}`, `{AccountId}`, `{FolderId}`, `{Sender}`, `{Recipients}`, `{FailedRecipientCount}`, `{FailedRecipientAddresses}`, `{FirstFailedRecipient}`, `{FailureDescription}`, `{RetryCount}`, `{DeliveryAttempt}`, `{Size}`, `{MessageState}`, `{CreatedUtc}`, `{FileName}`, `{RuleForcedRouteId}`, `{RuleBindAddress}`, `{ServerName}`, and `{GeneratedUtc}`. The delivery worker emits best-effort `IDeliveryQueueStatusObserver` events for lease, load-missing, delivery-event failure, target success/defer/final failure, bounce, completion, release, and processing-failure transitions so production metrics/log sinks can be wired without changing the hot path. When SQL status persistence is enabled, retention cleanup keeps `hm_delivery_queue_status` bounded with `HMAILSERVER_DELIVERY_STATUS_RETENTION_DAYS` (default 30, 0 disables cleanup), `HMAILSERVER_DELIVERY_STATUS_CLEANUP_INTERVAL_MINUTES` (default 60), and `HMAILSERVER_DELIVERY_STATUS_CLEANUP_BATCH_SIZE` (default 5000). Non-local recipients without a configured route are accepted only after successful SMTP AUTH until full route/relay policy lands. The full script object model, remaining event hooks, and richer generated-message policy remain on the parity backlog.

## POP3 Listener

The .NET 10 POP3 TCP listener is wired into the Windows service and disabled by default while remaining command/security parity is rebuilt. Enable it for controlled compatibility testing:

```powershell
$env:HMAILSERVER_POP3_ENABLED = "true"
$env:HMAILSERVER_POP3_BIND_ADDRESS = "127.0.0.1"
$env:HMAILSERVER_POP3_PORT = "1110"
$env:HMAILSERVER_POP3_MAX_CONNECTIONS = "1000"
$env:HMAILSERVER_POP3_TLS_CERTIFICATE_PATH = "C:\certs\pop3.example.test.pfx"
$env:HMAILSERVER_POP3_TLS_CERTIFICATE_PASSWORD = "changeit"
$env:HMAILSERVER_EXTERNAL_FETCH_ENABLED = "true"
$env:HMAILSERVER_EXTERNAL_FETCH_BATCH_SIZE = "10"
$env:HMAILSERVER_EXTERNAL_FETCH_MAX_MESSAGES_PER_ACCOUNT = "100"
$env:HMAILSERVER_EXTERNAL_FETCH_POLL_INTERVAL_SECONDS = "30"
$env:HMAILSERVER_EXTERNAL_FETCH_EGRESS_ENFORCE = "false"
$env:HMAILSERVER_EXTERNAL_FETCH_ALLOWED_PRIVATE_CIDRS = "192.168.0.0/16"
```

The POP3 command engine supports `USER`/`PASS` through the shared account authenticator, `CAPA`, and then `STAT`, `LIST`, `UIDL`, `RETR`, `TOP`, `DELE`, `RSET`, `NOOP`, and `QUIT` over an `IPop3MailboxStore` boundary. Successful authentication acquires a mailbox lock so one POP3 session owns an account mailbox at a time, and releases it when the session ends. The SQL Server mailbox store opens the legacy root `Inbox` for the authenticated account, lists `hm_messages` rows in `messageuid` order, exposes `messageuid` as the POP3 UIDL value, streams message files from the hMailServer data directory, and deletes DB/search/metadata rows plus message files when authenticated `QUIT` commits pending `DELE` commands. `RETR` dot-stuffs while streaming instead of requiring the full message body as a byte array, and `TOP` streams only headers plus the requested body line count. Failed `PASS` attempts use the shared SQL auto-ban recorder and close the session when the configured threshold is reached. With scripting enabled, POP3 runs optional `OnClientLogon(HMAILSERVER_CLIENT)` after successful and failed authentication attempts and exposes endpoint/session/TLS metadata. When a POP3 TLS PFX certificate is configured, accepted sockets are upgraded immediately with `SslStream` for implicit TLS; set `HMAILSERVER_POP3_PORT=995` for the conventional TLS listener port. External POP3 fetch now has a SQL Server lease/UID store for legacy `hm_fetchaccounts` and `hm_fetchaccounts_uids`, resets stale `falocked` account rows once when the hosted worker starts, exposes a Windows script boundary for legacy `OnExternalAccountDownload(HMAILSERVER_FETCHACCOUNT, HMAILSERVER_MESSAGE/Nothing, uid)` with fetch-account fields including `NextDownloadTime` and `IsLocked`, maps `Result.Value` delete-retention decisions, and uses a hosted processor that connects to POP3 accounts, supports plain/implicit TLS/STLS modes, probes CAPA before STLS so optional STARTTLS falls back to plaintext only when STLS is not advertised and required STARTTLS fails before `USER`/`PASS` when STLS is missing, fails both STARTTLS modes before credentials when an advertised STLS command is rejected, uses UIDL/RETR/DELE/QUIT, dot-unstuffs message bytes, prepends the legacy `X-hMailServer-ExternalAccount` account-name header before script and receiver processing, preserves valid `Received`/`Date` timestamps, resolves MIME recipient headers and `Received ... for <recipient>` values through the SMTP recipient validator, applies the legacy `EnableRouteRecipients` local/route-recipient filter, runs ClamAV scanning for accounts with `UseAntiVirus`, carries `UseAntiSpam` into the SpamAssassin-enabled SMTP receiver path, treats permanent SMTP receiver rejections as non-accepted messages that still apply UID/remote-delete retention, queues accepted messages through the SMTP receiver path, tracks known UIDs, tolerates duplicate persisted known-UID rows, skips duplicate sequence numbers plus duplicate new and already-known UIDL values within the same POP3 listing, and applies remote delete decisions. Additional external-fetch edge-case parity remains on the backlog.

External POP3 control operations use a configurable 900-second default deadline, preserve caller cancellation, cap control lines at 250,000 bytes including required CRLF framing, and bound best-effort QUIT cleanup to five seconds; UIDL/RETR message data remains logically unbounded to preserve the legacy binary/chunked paths. Live DNS/TLS integration evidence and adaptive high-load timeout behavior remain pending.

A rejected external-fetch `CAPA` response is treated like an unavailable STLS capability: optional STARTTLS continues over plaintext, while required STARTTLS fails before `USER`/`PASS`.

A rejected external POP3 greeting fails the connection before any client command or credentials are sent in plain and STARTTLS modes.

A rejected external-fetch `USER` command fails authentication before `PASS` is sent in both plain and optional-STARTTLS plaintext fallback paths.

A rejected external-fetch `PASS` command fails authentication before message listing begins, with no `UIDL` or later command sent in plain and optional-STARTTLS plaintext fallback paths.

A rejected external-fetch `UIDL` command sends the legacy `QUIT` cleanup without issuing `RETR`, `DELE`, or other message-processing commands in plain and optional-STARTTLS plaintext fallback paths.

A truncated external-fetch `UIDL` listing after a `+OK` response remains fatal, completes the failed fetch lease through the existing SQL completion path (scheduling the next retry from `faminutes`), and does not issue `RETR`/`DELE`, submit message data, or mutate UID state.

An empty external-fetch `UIDL` listing completes without `RETR`/`DELE` commands and removes stale known UID rows that are no longer present on the remote server.

Malformed external-fetch `UIDL` listing rows are skipped while valid rows in the same listing are preserved for later `RETR`/retention processing.

External-fetch UIDL processing follows the legacy ordered-map behavior: remote entries are processed by ascending POP3 sequence number, and the last UID wins when a malformed listing repeats the same sequence.

For `OnExternalAccountDownload`, `Result.Value = 2` preserves the signed `Result.Parameter`; any negative retention value follows the legacy immediate remote-delete path for both newly downloaded and already-known UIDs.

Positive external-fetch retention uses the full elapsed timestamp span, matching legacy `DateTimeSpan`: a known UID is kept while elapsed fractional days are less than or equal to the configured value and deleted only after that boundary is exceeded.

An empty but successfully terminated external-fetch `RETR` body remains processable: the legacy `X-hMailServer-ExternalAccount` header becomes the message content, then normal script, receiver, UID tracking, and retention processing continues.

Configured external-fetch MIME recipient names use the first matching header field, matching legacy `GetRawFieldValue`; duplicate fields with the same configured name do not introduce extra recipients, while every `Received` field remains eligible for `for <recipient>` extraction.

External-fetch `Received ... for <recipient>` values pass the legacy 254-character email-address regex before recipient resolution. Malformed values such as multiple-`@` addresses are ignored even when route recipients are enabled, allowing the account fallback recipient to be used.

The legacy `Received` parser locates the literal lowercase `for ` token with a case-sensitive `std::rfind`; uppercase variants such as `FOR ` are ignored and leave recipient selection to the account fallback.

External-fetch envelope sender extraction also applies the legacy 254-character email-address validator after parsing the first `From` mailbox. Invalid or over-limit values are ignored and the message is submitted with an empty envelope sender.

Resolved external-fetch recipients are deduplicated case-insensitively by their final delivery address, matching legacy `RecipientParser::AddRecipient_`. When different MIME aliases resolve to the same mailbox, the first resolved recipient and its original-address metadata are retained.

If a configured MIME recipient field cannot be parsed as one complete address list, external fetch falls back to the legacy quote- and escape-aware comma compounds and parses each independently. A malformed address therefore does not discard a valid neighboring recipient, and commas inside quoted display names remain part of the same address.

A whitespace-only but non-empty configured MIME recipient-header value follows legacy `StdString::IsEmpty()` behavior: it contributes no configured field names, but `Received ... for` headers are still scanned for recipients.

A rejected external-fetch `RETR` command sends only the legacy `QUIT` cleanup, completes the failed fetch lease through the existing SQL completion path, and does not submit message data or mutate UID/remote-deletion state in plain and optional-STARTTLS plaintext fallback paths.

A truncated external-fetch `RETR` body after a `+OK` response remains fatal, completes the failed fetch lease through the existing SQL completion path, and does not submit message data, add UID state, or issue remote delete cleanup.

External-fetch `DELE` is legacy best-effort: any server response, including `-ERR`, advances UID cleanup and allows the session to `QUIT`; socket, I/O, and cancellation failures remain fatal. A `DELE` transport failure before any server response preserves known UID state and completes the failed fetch lease through the existing SQL completion path.

External-fetch `QUIT` cleanup is best-effort during session disposal: rejected `QUIT` responses and disconnects before the `QUIT` response do not leak disposal failures after the message-processing decision has already been made.

External-fetch destination resolution is performed once through an injected resolver, and the first approved numeric endpoint is used for TCP connection while the configured hostname remains the TLS SNI/certificate target. When `HMAILSERVER_EXTERNAL_FETCH_EGRESS_ENFORCE=true`, metadata/cloud-platform, loopback for arbitrary hostnames, unspecified, link-local, CGNAT, reserved, multicast, transition, and other special-use answers are denied; private, IPv6 ULA, and explicit localhost/loopback destinations require a matching `HMAILSERVER_EXTERNAL_FETCH_ALLOWED_PRIVATE_CIDRS` entry. Enforcement is audit-only by default to preserve configured local POP3 compatibility, and credential-free policy decisions are sent to the service logger. No proxy, redirect, SMTP trust, live reconfiguration, or COM fetch-account mutation is added. Registered legacy `InterfaceFetchAccount` direct activation now remains unattached and access-denied before persistence or external-fetch scheduling, while parent-owned collection attachment remains unchanged. See `EXTERNAL_FETCH_EGRESS_AUDIT.md` for the legacy references and remaining live DNS/TLS integration gaps.

With scripting enabled, the POP3 listener runs `OnClientConnect(HMAILSERVER_CLIENT)` before its greeting or implicit TLS setup and closes the connection when legacy `Result.Value = 1`; this complements the existing post-authentication `OnClientLogon` hook.

## Project Layout

- `HMailServer.Service`: Windows service host named `hMailServer`.
- `HMailServer.Core`: shared abstractions for search, delivery queue, message identity, failed-logon auto-ban recording, and external fetch account leasing.
- `HMailServer.Delivery`: delivery queue processor orchestration over lease/load/target-dispatch boundaries, the coalescing single-worker scheduling loop, remote delivery MX resolution, and optional sender-domain MX checks.
- `HMailServer.Protocols`: `System.IO.Pipelines` line protocol reader, bounded `Channel` work queue primitives, shared `OnClientConnect` listener event handling, shared IMAP sequence-set parsing, IMAP TCP/session/SEARCH/SORT/FETCH/IDLE/ACL/QUOTA parser/executor/command handler plumbing, the SMTP TCP/session skeleton, POP3 TCP/session command engine with implicit TLS stream support, and failed-logon auto-ban disconnect hooks.
- `HMailServer.Indexing`: SQL Server Full-Text Search backfill processor.
- `HMailServer.Storage.SqlServer`: SQL Server connection, Full-Text Search readiness, message search/sort indexing, IMAP sequence snapshots, IMAP message fetch storage, POP3 Inbox mailbox storage, external fetch account/UID leasing, failed-logon auto-ban recording, atomic delivery leasing, read-only domain/account/alias administration plus bounded Settings host/welcome/limit and server-message lookup, bounded Status delivery-queue snapshots, optional greylisting checks, optional delivery queue status persistence, retention cleanup, and event-kind metrics snapshots.
- `HMailServer.Search.SqlServer`: IMAP SEARCH and SORT query planners for SQL Server predicates, metadata ordering, and Full-Text Search.
- `HMailServer.Security`: modern spam/virus protocol helpers, including the async/timeboxed ClamAV INSTREAM client, message antivirus scanner adapter, async/timeboxed SpamAssassin client, message spam scanner adapter, SpamAssassin response validation, MIME-aware attachment replacement policy, optional DNS blocklist checker, optional reverse DNS/PTR checker, optional URL/SURBL checker, an RFC 7208-shaped SPF evaluator plus system-DNS TXT/A/AAAA/MX/PTR resolver and disabled-by-default SMTP anti-spam policy boundary, DKIM evaluation-only primitives for the legacy result model, `DKIM-Signature` tag parsing, `bh=`/`l=` body-hash verification, simple/relaxed body/header canonicalization, injected-public-key `rsa-sha1`/`rsa-sha256` header signature verification, selector TXT public-key lookup/key-record validation behind an injected resolver, message-level verification orchestration over the legacy first-five-signatures/pass-on-any-success flow, and a disabled-by-default SMTP DKIM policy boundary that maps legacy `PermFail` to spam scoring without direct SMTP rejection, plus DMARC evaluation-only primitives for TXT record parsing, exact/organizational-domain lookup, strict/relaxed SPF and DKIM alignment, policy/result modeling, an offline/local-PSL organizational-domain resolver, and a disabled-by-default SMTP DMARC policy boundary that consumes SPF/DKIM pass-domain results without direct SMTP rejection or quarantine. SPF/DKIM/DMARC do not introduce default reject/tempfail/quarantine behavior; the official PSL snapshot is pinned, integrity-checked, and packaged offline, while DKIM signing and DMARC enforcement/Admin wiring remain pending.
- `HMailServer.ComInterop`: the preserved legacy `IInterfaceMessageIndexing` IID/dual/DISPID contract plus additive COM compatibility contracts for new .NET-only capabilities. The COM-visible `MessageIndexing` adapter preserves the legacy CLSID and versioned `hMailServer.MessageIndexing.1` ProgID, exposes the legacy interface as its default, implements `IInterfaceMessageIndexing2`, and delegates every operation to the service-configured SQL runtime only when created through the authorized host adapter factory; direct class activation preserves the legacy access-denied boundary. The Windows service owns a process-local COM class-factory host that initializes a dedicated MTA, registers suspended local-server class objects, resumes activation after registration, and revokes them during shutdown. The legacy dual `Application`, `Database`, `Status`, `Utilities`, `Links`, `Diagnostics`, `DiagnosticResults`, `DiagnosticResult`, `Account`, `Messages`, `Message`, `Attachments`, `Attachment`, `Recipients`, `Recipient`, `MessageHeaders`, `MessageHeader`, `FetchAccounts`, `FetchAccount`, `Rules`, `Rule`, `Settings`, `AntiVirus`, `AntiSpam`, `BlockedAttachments`, `BlockedAttachment`, `Logging`, `Scripting`, `BackupSettings`, `BackupManager`, `Domains`, `Domain`, `Accounts`, `Aliases`, `Alias`, `DistributionLists`, `DistributionList`, `DistributionListRecipients`, `DistributionListRecipient`, `DomainAliases`, `DomainAlias`, `IncomingRelays`, `IncomingRelay`, `TCPIPPorts`, `TCPIPPort`, `SecurityRanges`, `SecurityRange`, `SSLCertificates`, `SSLCertificate`, `Groups`, `Group`, `GroupMembers`, `GroupMember`, `ServerMessages`, `ServerMessage`, `Directories`, `Language`, and `Languages` contracts preserve their installed-5.7 IIDs, complete vtable order, CLSIDs, versioned ProgIDs, and default interfaces. The service loads `[Security] AdministratorPassword` from the configured `InitializationFile`/`HMAILSERVER_INITIALIZATION_FILE` or the executable-directory `hMailServer.ini`, registers the real `Application`, `Database`, `Status`, `Utilities`, `Links`, `Diagnostics`, `DiagnosticResults`, `DiagnosticResult`, `Settings`, `AntiVirus`, `AntiSpam`, `BlockedAttachments`, `BlockedAttachment`, `Logging`, `Scripting`, `BackupSettings`, `BackupManager`, `Domains`, `Domain`, `Accounts`, `Account`, `Messages`, `Message`, `Attachments`, `Attachment`, `Recipients`, `Recipient`, `MessageHeaders`, `MessageHeader`, `FetchAccounts`, `FetchAccount`, `Rules`, `Rule`, `Aliases`, `Alias`, `DistributionLists`, `DistributionList`, `DistributionListRecipients`, `DistributionListRecipient`, `DomainAliases`, `DomainAlias`, `IncomingRelays`, `IncomingRelay`, `TCPIPPorts`, `TCPIPPort`, `SecurityRanges`, `SecurityRange`, `SSLCertificates`, `SSLCertificate`, `Groups`, `Group`, `GroupMembers`, `GroupMember`, `ServerMessages`, `ServerMessage`, `Directories`, `Language`, `Languages`, and `MessageIndexing` class factories, exposes the authenticated `Application -> Settings -> MessageIndexing` path, exposes `Application -> Database` read-only required/current DB version plus INI-backed database type/server/name/exists and connection state with legacy per-member authentication, exposes authenticated `Application -> Status` read-only delivery-queue text, legacy start-time formatting, processed/spam/virus counters, protocol session counts, and current managed thread ID from a runtime snapshot boundary, exposes authenticated `Application -> Diagnostics` process-local local/test domain state plus read-only runtime result collections from a deterministic diagnostics boundary, exposes core `Application` scalar values for `Version`/`VersionArchitecture` and authenticated `ServerState`/`InitializationFile` from a runtime/configuration boundary while keeping service-control operations explicit `E_NOTIMPL`, exposes authenticated `Application -> Domains` count/index/name/id lookup and selected read-only core `Domain` detail scalars plus read-only AD domain name, size/allocated-size aggregates, greylisting flag, signature configuration, and DKIM configuration from existing SQL data, exposes authenticated `Domain -> Accounts` count/index/address/id lookup plus selected read-only core and delivery/detail `Account` scalars (`Size`, `MaxSize`, `PersonFirstName`, `PersonLastName`, vacation/autoreply, forwarding, and signature fields) from existing SQL data, exposes authenticated `Account -> Messages` and `IMAPFolder -> Messages` count/index/DBID lookup plus read-only safe message metadata (`ID`, `Filename`, `FromAddress`, `State`, `Size`, `DeliveryAttempt`, `InternalDate`, `UID`, and flag getters) from scoped existing `hm_messages` rows and controlled file-backed MIME getters (`Subject`, `From`, `Date`, `To`, `CC`, `Body`, `HTMLBody`, `HeaderValue`, `HasBodyType`, and `Charset`) with read-only `Headers`, `Recipients`, and `Attachments` child collections, exposes authenticated `Account -> FetchAccounts` count/index/id lookup plus read-only non-secret fetch-account scalars from existing `hm_fetchaccounts` SQL data, exposes authenticated `Account -> Rules` count/index/id lookup plus read-only rule ID/account/name/active/use-AND scalars from existing `hm_rules` SQL data, exposes authenticated `Application -> Rules` count/index/id lookup plus the same read-only global-rule scalars from `hm_rules` rows with `ruleaccountid = 0`, exposes authenticated `Settings -> AntiVirus` read-only non-secret antivirus scalars from existing `hm_settings` data plus runtime-bound custom/ClamWin/ClamAV scanner tests and read-only `BlockedAttachments` rows from `hm_blocked_attachments`, exposes authenticated `Settings -> AntiSpam` read-only anti-spam scalar getters from existing `hm_settings` data plus operational `DKIMVerify`, `ClearGreyListingTriplets`, and `TestSpamAssassinConnection`, exposes authenticated `Settings -> IncomingRelays` count/index/name/id lookup plus read-only incoming-relay IP scalars from existing `hm_incoming_relays` SQL data, exposes authenticated `Settings -> SecurityRanges` count/index/name/id lookup plus read-only IP range, priority, expiry, and option scalars from existing `hm_securityranges` SQL data, exposes authenticated `Settings -> ServerMessages` count/index/name/id lookup plus message-template scalars and existing-row save from existing `hm_servermessages` SQL data, exposes authenticated `Settings -> Directories` read-only directory scalars from the configured/default legacy `hMailServer.ini`, exposes authenticated `Application -> GlobalObjects -> Languages` local `.ini` count/index/name lookup plus read-only `Language.Name`, `IsDownloaded`, and translated `String` with English fallback from the legacy `Languages` directory and `[GUILanguages] ValidLanguages`, exposes authenticated `Domain -> Aliases` count/index/name/id lookup from existing `hm_aliases` SQL data, exposes authenticated `Domain -> DistributionLists` count/index/address/id lookup plus read-only list scalars from existing `hm_distributionlists` SQL data, exposes authenticated `DistributionList -> Recipients` count/index/id lookup plus read-only recipient address from existing `hm_distributionlistsrecipients` SQL data, and exposes authenticated `Domain -> DomainAliases` count/index/id lookup plus read-only alias/domain-id scalars from existing `hm_domain_aliases` SQL data. Other Settings/Domain/Account/Accounts/Message/Messages/FetchAccount/FetchAccounts/Rule/Rules/Alias/Aliases/DistributionList/DistributionLists/DistributionListRecipient/DistributionListRecipients/DomainAlias/DomainAliases members remain explicit `E_NOTIMPL`, including domain/account/message/fetch-account/rule scalar mutations, message save/copy/refresh/content mutation/file-write behavior, security-sensitive, operational/computed, or not-yet-covered account fields, antivirus setters, anti-spam setters/collection mutations, blocked-attachment mutations, diagnostic DNS/SMTP/file execution, fetch-account password/download execution, database SQL execution/transactions/create/default/script/prerequisite operations, directory mutation/persistence, language download/network/file-write/live reload, rule criteria Add/setter mutations, RuleActions mutations, distribution-list, recipient, and domain-alias mutations, and direct child-class activation remains access-denied. Registry-free process tests cover root activation, authentication, child access, direct access denial, and revoke behavior. The SQL indexing runtime reads legacy delivered/indexed counts and the persisted `MessageIndexing` setting, reports FTS/queue status, and performs queue-driven clear/index/rebuild operations; backfill does not lease work while indexing is disabled. The service build generates the legacy type library, and guarded install/uninstall commands own the hosted classes' AppID/CLSID/ProgID/CurVer/LocalServer32/type-library registration without registry mutation during tests. Deeper Administrator object collections remain on the parity backlog.
  The bounded `Utilities` compatibility path exposes unauthenticated legacy Blowfish encrypt/decrypt, `IsLocalHost`, and `GetMailServer` through service-injected runtimes shared by `Application -> Utilities` and the hosted Utilities class. `IsLocalHost` preserves DISPID 12 and `VARIANT_BOOL`, accepts a literal IPv4 address or the first IPv4 returned for a hostname, compares it with current local IPv4 interface addresses, and returns `false` for unresolved, IPv6-only, or empty input. `GetMailServer` preserves DISPID 1/BSTR, extracts the suffix after the final `@`, follows bounded MX/CNAME and implicit address fallback through system DNS, preserves null-MX and first-seen IP de-duplication, and returns comma-separated IP strings or an empty string. Authenticated `RetrieveMessageID` preserves DISPID 17 and the 64-bit return contract, derives the legacy partial filename from configured data-directory paths, queries that value before the exact supplied filename through parameterized `hm_messages.messagefilename` lookups, and returns `0` when neither value exists. Authenticated `PerformMaintenance(UpdateImapFolderUid)` preserves DISPID 19 and enum value `1`, reads folder maximum message UIDs, rejects non-positive legacy rows, and advances only lower `hm_imapfolders.foldercurrentuid` values through the service-injected SQL store. Authenticated `MakeDependent` preserves DISPID 7 and replaces only the local `hMailServer` service dependency multi-string with legacy `RPCSS` plus the caller-supplied service through an injected Windows SCM runtime. Authenticated `ImportMessageFromFile` preserves DISPID 8 and `VARIANT_BOOL`, accepts only existing files under the configured data directory, resolves the legacy partial filename before the exact supplied path, returns `true` for already-partial persisted rows, normalizes exact-path rows and misplaced in-tree files to legacy GUID/bucket filenames before persistence, imports `accountId > 0` files into the account Inbox with MIME-derived sender/size/internal-date metadata and allocated UIDs, imports `accountId == 0` files into the queue with only local To/CC recipients, and wakes delivery only after the durable write succeeds. Authenticated `ImportMessageFromFileToIMAPFolder` preserves DISPID 13 and `VARIANT_BOOL`, keeps the same file validation, lookup, normalization, empty-folder/InBox fallback, and `accountId == 0` queue behavior, cleans legacy `%YEAR%`/`%MONTH%`/`%DAY%` tokens plus one leading hierarchy delimiter, resolves legacy modified UTF-7 path segments, and transactionally creates missing named private or public-folder-prefixed paths as subscribed folders. Empty-folder Inbox fallback still requires an existing Inbox. Existing public destinations require inherited `Insert` ACL permission, while newly created public paths preserve the legacy global-rule create bypass; public-folder UID allocation uses folder account `0` while the imported message retains the caller account ID. Direct activation remains access-denied for these administrative methods; no service start/stop/install/delete, unrelated service configuration, registry write, message UID rewrite, folder UID decrease, external-path import, rules/spam/virus processing, or other Utilities mutation was added.
  The bounded `Settings -> Logging` path preserves and hosts the legacy `Logging` vtable and class identity, reads persisted logging mask/device/format/AWStats rows from existing `hm_settings`, reads the configured/default legacy `hMailServer.ini` log directory through the existing directory store, and exposes those values plus the legacy current event/error/AWStats/default log path shapes through authenticated read-only getters. Date-bearing paths use the current local date on every access, preserve the configured directory string, and do not read or create files. Authenticated `EnableLiveLogging`, `LiveLoggingEnabled`, and `LiveLog` share a thread-safe process-local buffer with legacy enable/disable clearing, destructive reads, and the 1,000,000-character overflow auto-disable boundary. A service-registered `ILoggerProvider` skips formatting while disabled and feeds enabled managed records into that same buffer using deterministic CRLF-terminated level/category/message/exception text without file access or persisted configuration changes. The obsolete `MaskPasswordsInLog` property preserves legacy no-op behavior with a deterministic `false` getter and authenticated no-op setter. Direct activation remains access-denied; persisted setters, provider/filter reconfiguration through COM, protocol transcript reconstruction, cross-process sharing, and broader Settings/Admin mutation remain explicit gaps.
  The bounded `Settings -> Scripting` path preserves and hosts the complete legacy `Scripting` vtable and class identity, reads `usescriptserver` and `scriptlanguage` from existing `hm_settings` rows, and exposes authenticated read-only `Enabled`, `Language`, INI-backed event `Directory`, and deterministic `CurrentScriptFile` getters. The current file path preserves legacy case-sensitive `VBScript`/`JScript` extension mapping. Authenticated `CheckSyntax` validates only that configured file through a shell-free, time-bounded Windows Script Host runtime; missing/empty files preserve the legacy empty-string success, valid scripts return an empty string, and compilation/runtime failures return file-scoped error text. Authenticated `Reload` runs that check, logs non-empty syntax errors and contained load failures with legacy event IDs 5016/5017, and relies on the managed executor's per-invocation configured-file load so the next event observes updated content without long-lived script-engine state. Direct activation remains access-denied; setters, settings persistence, COM-thread event execution, and broader runtime reconfiguration remain explicit gaps.
  The bounded `Settings -> BackupSettings` path preserves and hosts the complete legacy `BackupSettings` vtable and class identity, reads `backupdestination` and `backupoptions` from existing `hm_settings` rows, and exposes authenticated read-only destination plus settings/domains/messages/compression option bits. `LogFile` uses the configured log directory and legacy separator/filename shape without reading or creating files. Direct activation remains access-denied; setters, backup/restore execution, filesystem writes, and runtime reconfiguration remain explicit gaps.
  The bounded `Settings -> AntiVirus` path preserves and hosts the complete legacy `AntiVirus` vtable and class identity, reads existing non-secret `hm_settings` rows for ClamWin, custom scanner, ClamAV, action, notification, max-size, and attachment-blocking scalar configuration, and exposes authenticated read-only getters plus runtime-bound `TestCustomerScanner(command, returnCode)`, `TestClamWinScanner(executable, database)`, and `TestClamAVScanner(host, port)`. The custom scanner test preserves the legacy clean/EICAR flow and `%FILE%` substitution shape while quoting/escaping the generated file path and splitting the resulting command into shell-free `ProcessStartInfo` arguments; the ClamWin test writes temporary clean/EICAR `.eml` files under the configured data directory and invokes structured `--database`, filename, and `--tempdir` arguments; the ClamAV test reuses the existing time-bounded INSTREAM client. All three delete test files where applicable and return only the legacy success flag plus result text without changing SMTP/external-fetch scanning. Invalid stored action values fall back to the legacy delete-email value. Direct activation remains access-denied; setters, persisted setting changes, broader network/process policy, live scanner reconfiguration, and mutation behavior remain explicit `E_NOTIMPL`.
  The bounded `Settings -> AntiVirus -> BlockedAttachments` path preserves and hosts the complete legacy `BlockedAttachments`/`BlockedAttachment` vtables and class identities, reads existing `hm_blocked_attachments` rows ordered by wildcard, and exposes authenticated read-only count/index/DBID lookup plus `ID`, `Wildcard`, and `Description`. Direct activation remains access-denied; add/delete/save/refresh, SMTP attachment-policy behavior changes, filesystem/process/network access, and mutation behavior remain explicit `E_NOTIMPL`.
  The bounded `Settings -> AntiSpam` path preserves and hosts the complete legacy `AntiSpam` vtable and class identity, reads existing `hm_settings` rows for greylisting, HELO/PTR/SPF/MX scores, spam header/subject/threshold policy, SpamAssassin scalar configuration, DKIM verification scalar configuration, and anti-spam maximum message size, and exposes authenticated read-only scalar getters. Authenticated `DKIMVerify(file)` delegates to the existing verifier through an injected runtime, reads at most the legacy 50 MiB message limit as Latin-1, uses the time-bounded system DNS resolver, and returns the installed four-value `Neutral`/`Pass`/`TempFail`/`PermFail` enum unchanged. Authenticated `TestSpamAssassinConnection(host, port)` uses the existing time-bounded SpamAssassin client behind a narrow runtime boundary, sends the legacy GTUBE test message, and returns only the success flag plus result text without changing SMTP spam scanning. Authenticated `ClearGreyListingTriplets()` routes through a narrow SQL store and preserves the legacy all-row `DELETE FROM hm_greylisting_triplets` without touching whitelist or settings data. The obsolete `TarpitDelay` and `TarpitCount` getters keep the legacy zero value. Direct activation remains access-denied; setters, DKIM signing/private-key access, SMTP policy behavior changes, live reconfiguration, and other mutations remain explicit gaps.
  The bounded `Settings -> AntiSpam -> DNSBlackLists` path preserves and hosts the complete legacy `DNSBlackLists`/`DNSBlackList` vtables and class identities, reads only existing `hm_dnsbl` rows in `sblid ASC` order, and exposes authenticated read-only count/index/DBID/DNS-host lookup plus item `Active`, `ID`, `DNSHost`, `RejectMessage`, `ExpectedResult`, and `Score`. Missing index/DBID keeps `DISP_E_BADINDEX`; missing DNS host keeps the legacy no-object result. Direct activation remains access-denied; add/delete/save/refresh, DNS lookup/test execution, SMTP policy behavior changes, live reconfiguration, and mutations remain explicit `E_NOTIMPL`.
  The bounded `Settings -> AntiSpam -> SURBLServers` path preserves and hosts the complete legacy `SURBLServers`/`SURBLServer` vtables and class identities, reads only existing `hm_surblservers` rows in `surblid ASC` order, and exposes authenticated read-only count/index/DBID/DNS-host lookup plus item `Active`, `ID`, `DNSHost`, `RejectMessage`, and `Score`. Missing index/DBID keeps `DISP_E_BADINDEX`; missing DNS host keeps the legacy no-object result. Direct activation remains access-denied; add/delete/save/refresh, DNS lookup/test execution, SMTP policy behavior changes, live reconfiguration, and mutations remain explicit `E_NOTIMPL`.
  The bounded `Settings -> AntiSpam -> GreyListingWhiteAddresses` path preserves and hosts the complete legacy `GreyListingWhiteAddresses`/`GreyListingWhiteAddress` vtables and class identities, reads only existing `hm_greylisting_whiteaddresses` rows in stored-IP order, and exposes authenticated read-only count/index/DBID/name lookup plus item `ID`, `IPAddress`, and `Description`. The getter preserves legacy SQL-LIKE-to-wildcard conversion and 64-bit SQL ID to 32-bit COM projection; missing lookups keep `DISP_E_BADINDEX`. Authenticated `Refresh` reloads the SQL-backed greylisting whitelist snapshot, atomically replaces only that collection's process-local view, and retains the previous snapshot on contained reload failure. Direct activation remains access-denied; add/delete/save, greylisting policy behavior changes, live reconfiguration, and mutations remain explicit `E_NOTIMPL`.
  The bounded `Settings -> AntiSpam -> WhiteListAddresses` path preserves and hosts the complete legacy `WhiteListAddresses`/`WhiteListAddress` vtables and class identities, reads only existing `hm_whitelist` rows in legacy lower-IP numeric order, converts IPv4/IPv6 two-column storage to COM strings, and exposes authenticated read-only count/index/DBID lookup plus item `ID`, `LowerIPAddress`, `UpperIPAddress`, `EmailAddress`, and `Description`. Authenticated `Refresh` reloads the SQL-backed whitelist address snapshot, atomically replaces only that collection's process-local view, and retains the previous snapshot on contained reload failure. Direct activation remains access-denied; add/delete/save/clear, SMTP whitelist policy changes, live reconfiguration, and mutations remain explicit `E_NOTIMPL`. All legacy `AntiSpam` collection getters are now available read-only; collection mutations and behavior changes remain pending.
  The bounded `Application -> BackupManager` path preserves and hosts the complete legacy `BackupManager` and `Backup` vtables/class identities and exposes the manager only after server-administrator authentication. `LoadBackup` invokes the packaged legacy `7za.exe` without a shell, streams only `hMailServerBackup.xml` to a DTD-disabled bounded XML reader without extraction, exposes Mode bits 1/2/4 through `ContainsSettings`/`ContainsDomains`/`ContainsMessages`, and keeps restore selections process-local. Replacement installation now requires an explicit rollback archive and runs a read-only full-archive integrity preflight before COM or service mutation; the preflight accepts only self-contained `Mode=15`/`DataFiles Format=7z` metadata, requires the legacy compressed `DataBackup` directory entry (including an empty directory), rejects unsafe archive paths, and fails closed on timeout, invalid XML, missing metadata, or archive/listing failure. Direct activation remains access-denied; authenticated `StartBackup` now publishes one service-owned maintenance request, carries the successful read-only plan into the bounded shell-free archive writer, reads all non-secret `hm_settings` rows through a backup-only raw store, preserves legacy property attributes/order, serializes the backup-side `DomainAliases` child collection in the existing per-domain order, and reports failures through the existing status/thread-stop boundary. The relayer credential remains intentionally excluded; Accounts/Aliases/DistributionLists children, message payloads, data-directory staging, `StartRestore`, database/data-directory writes, restore execution, and event dispatch remain explicit gaps.
  The bounded `Application -> GlobalObjects -> DeliveryQueue` path preserves and hosts the complete legacy `GlobalObjects` and `DeliveryQueue` vtables/class identities and exposes the child graph only after server-administrator authentication. `ResetDeliveryTime` updates only the selected type-1/type-3 queue row to an immediately eligible UTC next-try time while preserving retry, lease, recipient, and content state. A single service-owned hosted worker now runs the existing lease-aware `DeliveryQueueProcessor`, drains full batches without an idle wait, uses the legacy one-minute empty-queue poll interval, retries batch failures without terminating the service, and honors shutdown cancellation. Authenticated `StartDelivery` wakes that worker through a bounded coalescing in-process signal without changing queue data. Direct activation remains access-denied.
  The bounded `Application -> GlobalObjects -> Languages` path preserves and hosts the complete legacy `Languages`/`Language` vtables/class identities, reads local `.ini` translations from the executable-directory `Languages` folder using the `[GUILanguages] ValidLanguages` filter, lower-cases language names, keeps deterministic collection ordering, and exposes read-only `Name`, `IsDownloaded`, and translated `String(EnglishString)` with English fallback. `Download`, network access, file writes, live reload, and direct child activation remain out of scope.
  Production SMTP primary and rule-generated queue writes pass through the same signaling decorator, which wakes delivery only after the durable SQL/file writer completes; failed or canceled writes do not signal, and a best-effort signal failure cannot turn an already-durable message into an SMTP failure.
  Successful lease-owned delivery completion now removes the source queue file through the same path-contained content-store boundary after database completion succeeds. Normal delivery, delivery-event drops, and no-target completion share this behavior; defer, release, load failure, cancellation, lost-lease completion, and delete I/O failure preserve retry state or leave cleanup to later orphan maintenance.
  Authenticated `DeliveryQueue.Remove` atomically removes one type-1/type-3 queue message and its recipients through the administration store, skips current owner leases and delivered mailbox rows, and best-effort deletes the stored filename only within the configured data directory after the database transaction succeeds. Missing, non-queue, and actively leased IDs are silent no-ops, preserving the legacy COM method shape and 64-bit message ID.
  Authenticated `DeliveryQueue.Clear` schedules a process-local single-flight cleanup and returns without running SQL on the COM thread. The coordinator drains bounded administration-store batches, coalesces repeated calls while active, skips current owner leases and delivered mailbox rows, deletes recipients/messages atomically, performs contained best-effort file cleanup after each committed batch, and honors service shutdown cancellation.
  The bounded `Domain -> Accounts` path now also exposes read-only legacy `Account` AD scalars (`IsAD`, `ADDomain`, `ADUsername`), `QuotaUsed` from existing `hm_messages.messagesize` usage and `accountmaxsize`, `LastLogonTime` from `hm_accounts.accountlastlogontime`, and authenticated `Accounts.Refresh` for the selected domain's SQL-backed account snapshot. Refresh atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure; AD authentication, quota enforcement, mailbox scans, login-time updates, and account mutations remain out of scope.
  The bounded `Domain -> Aliases` path now also exposes authenticated `Aliases.Refresh` for the selected domain's SQL-backed alias snapshot. Refresh atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure; alias add/delete/save/setters and domain-alias refresh remain out of scope.
  The bounded `Domain -> DomainAliases` path now also exposes authenticated `DomainAliases.Refresh` for the selected domain's SQL-backed domain-alias snapshot. Refresh atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure; domain-alias add/delete/save/setters and alias refresh remain out of scope.
  The bounded `Domain -> DistributionLists` path now also exposes authenticated `DistributionLists.Refresh` for the selected domain's SQL-backed distribution-list snapshot. Refresh atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure; list add/delete/save/setters, recipient collection refresh, and broader distribution-list mutation remain out of scope.
  The bounded `Account -> FetchAccounts` path now also exposes authenticated `FetchAccounts.Refresh` for the selected account's SQL-backed non-secret fetch-account snapshot. Refresh atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure; direct `FetchAccount`/`FetchAccounts` activation denies every getter, setter, collection mutator, `Save`, `Delete`, `DownloadNow`, and `Refresh` path with `E_ACCESSDENIED` before snapshot or store access, while password access, download execution, external POP3 behavior, and fetch-account mutations remain out of scope.
  The bounded `Account -> Rules` path now also exposes authenticated account-scoped `Rules.Refresh` for the selected account's SQL-backed rule snapshot. Refresh atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure; criterias/actions refresh, rule execution behavior, and rule mutations remain out of scope.
  The bounded `Application -> Domains` path also exposes `Domains.Names` using the legacy `id\tname\tactive\r\n` collection string and authenticated `Domains.Refresh` reloads the same SQL-backed domain snapshots through the configured administration store, atomically replacing only that COM collection's process-local view. Direct activation remains access-denied, contained reload failure reports COM `E_FAIL` while retaining the prior snapshot, and collection/domain mutation remains out of scope.
  The bounded authenticated `Application -> Settings` path exposes read-only `HostName`, protocol welcome strings, connection/delivery/retry limits, SMTP message/recipient/invalid-command guardrails, selected SMTP policy flags, SMTP routing strings, non-secret SMTP relayer settings, SMTP delivery connection security plus the legacy `SMTPRelayerUseSSL` compatibility projection, selected rule/thread/asynchronous-task/MX numeric runtime settings, selected SSL verification/cipher scalars, TLS version/option bitmask flags, IPv6 preference, auto-ban scalar settings plus authenticated legacy logon-failure cleanup, protocol service flags, IMAP SORT/QUOTA/IDLE/ACL capability flags, IMAP SASL PLAIN/initial-response flags, IMAP public-folder/hierarchy naming strings, the configured IMAP master-user name from their existing `hm_settings` rows, persisted logging flags/device/format/AWStats values plus the INI-backed directory and current log paths through `Logging`, read-only script enabled/language/event-directory/current-file values through `Scripting`, read-only backup destination/options/log-file path through `BackupSettings`, read-only non-secret antivirus scalar values and blocked-attachment rows through `AntiVirus`, read-only anti-spam scalar values through `AntiSpam`, read-only cache enabled/TTL values plus authenticated process-local `Clear` and hit-rate/current-size/max-size statistics through `Cache`, the legacy constant `#Public` through `PublicFolderDiskName`, INI-backed UI-language/forwarding-envelope values, and process-local crash-simulation mode with their legacy defaults. Authenticated `BlockedAttachments.Refresh` reloads the SQL-backed blocked-attachment snapshot exposed through `AntiVirus`, while authenticated `DNSBlackLists.Refresh`, `SURBLServers.Refresh`, `GreyListingWhiteAddresses.Refresh`, and `WhiteListAddresses.Refresh` reload their SQL-backed anti-spam collection snapshots; each atomically replaces only that COM collection's process-local view and retains the previous snapshot on contained reload failure. `SMTPRelayerUseSSL` is true only for the legacy direct TLS mode, not either STARTTLS mode. `DenyMailFromNull` preserves the legacy inversion of `allowmailfromnull`; retry lookup intentionally uses `smtpnoofretries` and `smtpminutesbetweenretries`, not the obsolete `smtpnooftries` row. The installed vtable/DISPIDs, `VARIANT_BOOL`/`BSTR`/enum marshaling, and direct-activation access boundary remain unchanged; setters, antivirus scanner test actions, anti-spam collections/SpamAssassin test actions, blocked-attachment mutations, crash/fault injection, INI persistence/UI resource reload, SMTP/rule forwarding and DKIM-signing integration, filesystem/public-folder/log-file mutation, live listener/session/delivery-worker/work-queue/logger reconfiguration, retry scheduling changes, IMAP master-user authentication behavior, service state, secret settings, and broader Settings mutation remain explicit gaps. Authenticated `ClearLogonFailureList()` preserves DISPID 86 and the legacy MSSQL `failuretime < DATEADD(minute, 1, GETDATE())` cleanup threshold through a narrow store without changing security ranges, auto-ban settings, or failed-logon recording.
  A Settings-bound `Cache` adapter carries the application's revocable administrator state: after failed reauthentication, retained `Settings` cannot obtain `Cache`, while an attached `Cache.Clear()` and the four hit-rate getters deny before runtime access. Attached cache configuration, current-size, and maximum-size getters intentionally retain the legacy no-recheck behavior. Direct activation, IID/vtable/DISPID shape, cache persistence, and live cache reconfiguration remain unchanged.
  The bounded `Account -> IMAPFolders` path additionally preserves the legacy `IMAPFolders`/`IMAPFolder` vtable and class identities, reads authenticated top-level folders plus parent-scoped `SubFolders` from `hm_imapfolders` in legacy folder-ID order, converts stored modified UTF-7 names at the COM boundary, and exposes read-only ID, parent ID, name, subscribed, current UID, and creation-time values. Direct activation remains access-denied; folder add/delete/save/name/subscription mutations remain explicit `E_NOTIMPL`, while private account-folder permission access preserves the legacy public-folder-only error.
  Authenticated `Settings -> PublicFolders` reuses the same bounded adapter and store for `folderaccountid = 0` public roots and public `SubFolders`; account and public collections remain isolated without widening mutation scope.
  Authenticated public `IMAPFolder -> Permissions` preserves the legacy `IMAPFolderPermissions`/`IMAPFolderPermission` vtables/class identities and ACL enum GUIDs/values, reads only existing `hm_acl` rows scoped to the selected public folder in `aclid ASC` order, and exposes read-only count/index/DBID/legacy-name lookup plus item ID, share-folder ID, principal IDs/type, raw value, permission-flag getters, and read-only `Account`/`Group` child objects for existing user/group principals. Authenticated `Refresh` reloads the selected public folder's SQL-backed ACL snapshot, atomically replaces only that collection's process-local view, and retains the previous snapshot on contained reload failure. Missing/zero principal IDs preserve legacy `DISP_E_BADINDEX`; add/delete/save, setters, account/group mutations, ACL runtime policy changes, and SQL mutations remain explicit `E_NOTIMPL`.
  Authenticated `Application -> Rules` reuses the same bounded rule adapter and store for `ruleaccountid = 0` global rules, including authenticated global `Rules.Refresh`; account and global rule collections remain isolated without widening execution or mutation scope.
  Authenticated `Rule -> Criterias` preserves and hosts the complete legacy `RuleCriterias`/`RuleCriteria` vtables, enum GUIDs, and class identities, reads only the selected rule's existing `hm_rule_criterias` rows in legacy `criteriaid ASC` order, and exposes count/index/DBID lookup plus item `ID`, `RuleID`, match value, predefined-field selection, match type, and header field. Authenticated `RuleCriterias.Refresh` reloads the selected rule's SQL-backed criteria snapshot, atomically replaces only that COM collection's process-local view, and retains the prior snapshot on contained reload failure. Authenticated `DeleteByDBID(id)` first requires membership in that rule's snapshot, deletes one row through a parameterized rule-and-criteria scoped store operation, and removes only that item after success; foreign, unknown, and repeated IDs no-op, while contained store failure maps to `E_FAIL` and retains the snapshot. Authenticated `RuleCriteria.Delete()` reuses the owning collection's scoped delete path, removes only after success, no-ops when repeated or stale, and maps contained store failure to `E_FAIL`. Authenticated `RuleCriterias.Delete(index)` uses the zero-based owning snapshot, deletes the selected criterion through the scoped store path, removes only after success, maps contained store failure to `E_FAIL`, and silently no-ops for negative or out-of-range indices. Authenticated `RuleCriteria.Save()` persists the existing item snapshot through the owning rule-scoped store update, maps contained store failure to `E_FAIL`, and leaves the item readable for retry; setter staging and Add/new-item Save remain out of scope. Direct activation remains access-denied; rule execution behavior changes and broader mutations remain explicit `E_NOTIMPL`.
   Authenticated `Rule -> Actions` preserves and hosts the complete legacy `RuleActions`/`RuleAction` vtables, action-type enum GUID/values, and class identities, reads only the selected rule's existing `hm_rule_actions` rows in legacy `actionsortorder ASC` order, and exposes read-only count/index/DBID lookup plus all persisted action scalar fields. Authenticated `RuleActions.Refresh` reloads the selected rule's SQL-backed action snapshot, atomically replaces only that COM collection's process-local view, and retains the prior snapshot on contained reload failure. Authenticated `RuleActions.DeleteByDBID(id)` requires selected-rule snapshot membership, deletes the matching row through a parameterized rule/action-scoped store operation, removes only after success, maps contained store failure to `E_FAIL`, and no-ops for foreign, unknown, or repeated IDs. Authenticated `RuleActions.Delete(index)` uses the zero-based owning snapshot, reuses that scoped delete path, removes only after success, maps contained failure to `E_FAIL`, and silently no-ops for invalid indices. Authenticated `RuleAction.Delete()` reuses the owning collection's scoped delete path, removes only after success, no-ops when repeated or stale, and maps contained store failure to `E_FAIL`. Authenticated existing-row `RuleAction.Type` stages enum changes on the owning item facade, preserves the server-administrator guard for `RunScriptFunction`, and persists the staged value through `Save()`. Authenticated existing-row `RuleAction.ScriptFunction` stages the raw string on the owning item facade, preserves the server-administrator guard, and persists it through `Save()`. Authenticated existing-row `RuleAction.FromAddress` stages the exact raw string on the owning item facade and persists it through the existing parameterized `actionfromaddress` save path. Authenticated existing-row `RuleAction.IMAPFolder` decodes the legacy Modified UTF-7 getter boundary, encodes setter values, and persists through the existing owning Save path. Authenticated existing-row `RuleAction.HeaderName` stages the exact raw value and persists through the existing parameterized `actionheader` save path. Authenticated existing-row `RuleAction.Value` stages the exact raw value and persists through the existing parameterized `actionvalue` save path. Authenticated existing-row `RuleAction.RouteID` stages the raw integer, including zero, negative, and arbitrary values, and persists through the existing parameterized `actionrouteid` save path without route validation. Authenticated existing-row `RuleAction.AbortSpamFlagged` stages the raw `VARIANT_BOOL` value and persists through the existing parameterized `actionabortspamflagged` save path without an action-specific administrator guard. Account-rule execution now carries persisted message spam state into Forward/Reply action evaluation and skips only the matching spam-aborted action. Existing-row `RuleAction.Save()` reissues the owning snapshot through a parameterized rule/action-scoped update, maps contained store failure to `E_FAIL`, and preserves the readable item for retry. Direct activation remains access-denied; Add, `RuleID` and remaining setters, MoveUp, MoveDown, reordering, new-item persistence, rule execution behavior changes, and broader mutations remain explicit `E_NOTIMPL`.
  The bounded `Settings -> Routes` path preserves the legacy `Routes`/`Route` vtable and class identities, reads non-secret route configuration from `hm_routes` in domain-name order, and exposes read-only target, retry, authentication-username, local-domain, and connection-security values. A Settings-bound route adapter carries the application's revocable administrator state: after a failed reauthentication, retained `Settings` cannot obtain `Routes` or read the store again, while already attached `Routes` count/lookups/`Refresh` retain their legacy no-recheck behavior. Authenticated `Routes.Refresh` reloads the same SQL-backed route snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Password data is not selected; route-address refresh, live routing behavior, credentials, and all mutations remain explicit gaps.
  Authenticated `Route -> Addresses` preserves and hosts the complete legacy `RouteAddresses`/`RouteAddress` vtables and class identities, reads only the selected route's existing `hm_routeaddresses` rows without introducing a new sort order, and exposes read-only count/index/DBID lookup plus item `ID`, `Address`, and `RouteID`. Retained `Route.Addresses` and `RouteAddresses.DeleteByDBID`/`DeleteByAddress` intentionally retain their legacy no-recheck behavior after failed reauthentication, while attached `RouteAddress.Delete()` denies before persistence. Authenticated `DeleteByDBID(id)` deletes only an ID present in the owning collection snapshot and then uses the SQL store's selected-route scope; unknown/cross-route IDs and stale item facades no-op before persistence. Authenticated `RouteAddress.Delete()` reuses that parent-owned delete path for items returned by the authorized collection; authenticated `DeleteByAddress(address)` applies the legacy case-insensitive first-match collection delete. Direct activation remains access-denied; add/save, routing behavior changes, live reconfiguration, and broader mutations remain explicit `E_NOTIMPL`.
  The bounded `Settings -> IncomingRelays` path preserves the legacy `IncomingRelays`/`IncomingRelay` vtable and class identities, reads relay ranges from `hm_incoming_relays` in relay-name order, converts legacy two-column IP storage to COM strings, and exposes ID, name, lower-IP, and upper-IP values. Authenticated `IncomingRelays.Refresh` reloads the same SQL-backed relay snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Authenticated `DeleteByDBID(id)` invokes persistence only for IDs present in the owning snapshot, deletes the matching relay row, and atomically removes it from that snapshot; unknown IDs and repeated deletes through stale `IncomingRelay` facades no-op before persistence. Authenticated `Delete(index)` and `IncomingRelay.Delete()` reuse that same parent-owned path. A Settings-bound adapter carries the application's revocable administrator state: after a failed reauthentication, retained `Settings` cannot obtain `IncomingRelays`, and retained item `Save()`/`Delete()` deny before persistence, while collection delete/refresh/add and staged item setters retain the legacy attached-object behavior. Authenticated existing-row `IncomingRelay` setters stage name/lower-IP/upper-IP changes on the item facade until `Save()`, then update only the matching `hm_incoming_relays` row and owning collection snapshot. `LowerIP`/`UpperIP` setter parity normalizes malformed no-colon values to `0.0.0.0` and malformed colon-containing values to `::` before save, matching legacy setter success and save timing for existing and unsaved `Add()` items. Authenticated `IncomingRelays.Add` returns an owning-collection-scoped unsaved item facade; saving it inserts one `hm_incoming_relays` row, assigns the generated ID, and appends the item to the owning snapshot. SMTP trust behavior, live reconfiguration, and broader relay mutations remain explicit gaps.
  The bounded `Settings -> TCPIPPorts` path preserves the legacy `TCPIPPorts`/`TCPIPPort` vtable and class identities, reads listener port configuration from `hm_tcpipports` in address/port order, converts legacy two-column IP storage to COM strings, and exposes read-only ID, protocol, port, address, SSL-certificate, and connection-security values. Authenticated `TCPIPPorts.Refresh` reloads the same SQL-backed port snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Listener reconfiguration, certificate loading/validation, `SetDefault`, live binding changes, and all mutations remain explicit gaps.
  The bounded `Settings -> SecurityRanges` path preserves the legacy `SecurityRanges`/`SecurityRange` vtable and class identities, reads IP ranges from `hm_securityranges` in expiry/priority/name order, converts legacy two-column IP storage to COM strings, and exposes read-only IP range, priority, expiry, and option-bit values. Authenticated `SecurityRanges.Refresh` reloads the same SQL-backed range snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Authenticated `DeleteByDBID(id)` and zero-based `Delete(index)` invoke the configured store only for an owning snapshot member, remove only after successful persistence, map contained failure to `E_FAIL`, and no-op for unknown/repeated or invalid index requests; authenticated `SecurityRange.Delete()` reuses the owning collection path. Existing-row SecurityRange setters stage on the owning item facade, and `Save()` rechecks server-admin authorization, validates the legacy name/IP constraints, updates all persisted columns through a parameterized `hm_securityranges` update, preserves the item ID and collection index, and replaces only the matching owner snapshot after success. Authenticated `SetDefault()` now follows the legacy `Refresh -> DeleteAll -> My computer/Internet inserts -> Refresh` sequence with exact default options and final ordered snapshot publication; contained store failures map to `E_FAIL` and retain the last published snapshot. IP policy enforcement, auto-ban runtime behavior, live reconfiguration, and broader mutation remain explicit gaps.
  The bounded `Settings -> SSLCertificates` path preserves and hosts the legacy `SSLCertificates`/`SSLCertificate` vtable and class identities, reads certificate rows from `hm_sslcertificates` in certificate-name order, and exposes read-only ID, name, certificate-file, and private-key-file values. Authenticated `SSLCertificates.Refresh` reloads the same SQL-backed certificate snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Authenticated `SSLCertificates.Clear` deletes only existing `hm_sslcertificates` rows through the SQL administration store and atomically empties that authorized collection's process-local snapshot; authenticated `DeleteByDBID(id)` invokes persistence only for IDs present in the owning snapshot, deletes the matching row, and atomically removes it from that snapshot. Unknown IDs and repeated deletes through stale `SSLCertificate` facades no-op before persistence; item `Delete()` reuses the same parent-owned path. A Settings-bound adapter carries the application's revocable administrator state: after a failed reauthentication, retained `Settings` cannot obtain `SSLCertificates`, and retained item `Save()`/`Delete()` deny before persistence, including unsaved `Add()` items, while collection delete/add/refresh/clear and staged item setters retain the legacy attached-object behavior. Authenticated existing-row `SSLCertificate` setters stage name/certificate-file/private-key-file changes on the item facade until `Save()`, then update only the matching `hm_sslcertificates` row and owning collection snapshot. Authenticated `SSLCertificates.Add` returns an unsaved item facade; saving it inserts one certificate row, assigns the generated ID, and appends the item to the owning snapshot. Mutation authorization coverage proves that a configured runtime does not grant direct class activation access to collection delete/add/clear/refresh or item setter/save/delete paths; store delegates remain reachable only through the authenticated Settings-bound adapter. Certificate/private-key file reads, certificate loading/validation, TCP/IP listener reconfiguration, live TLS reload, and broader TLS runtime changes remain explicit gaps.
  The bounded `Settings -> Groups` path preserves and hosts the legacy `Groups`/`Group` vtable and class identities, reads server-wide groups from `hm_groups` in group-name order, and exposes read-only ID and name values. Authenticated `Groups.Refresh` reloads the same SQL-backed group snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Group-member refresh, ACL behavior integration, and all mutations remain explicit gaps.
  The bounded `Group -> Members` path preserves and hosts the legacy `GroupMembers`/`GroupMember` vtable and class identities, reads `hm_group_members` rows for the selected group in member-ID order, and exposes read-only ID, group-ID, account-ID, and `Account` child object values through the existing non-secret account-by-ID projection. Authenticated `GroupMembers.Refresh` reloads the selected group's SQL-backed member snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Missing accounts preserve legacy `DISP_E_BADINDEX`; ACL runtime behavior, member/account mutations, membership recalculation, and SQL writes remain explicit gaps.
  The bounded `Settings -> ServerMessages` path preserves and hosts the legacy `ServerMessages`/`ServerMessage` vtable and class identities, reads server-message templates from `hm_servermessages` in message-name order, and exposes ID, name, and text values. A Settings-bound adapter carries the application's revocable administrator state: after a failed reauthentication, retained `Settings` cannot obtain `ServerMessages` or read the store again, while already attached `ServerMessages.Refresh` and `ServerMessage` setters/`Save()` retain their legacy no-recheck behavior. Authenticated `ServerMessages.Refresh` reloads the same SQL-backed server-message snapshot, atomically replaces only that COM collection's process-local view, and retains the previous snapshot on contained reload failure. Authenticated existing-row `ServerMessage` setters stage name/text changes on the item facade until `Save()`, then update only the matching `hm_servermessages` row and owning collection snapshot. Delivery template execution, inserts/deletes, live reload, and broader Settings/Admin mutations remain explicit gaps.
  The bounded `Settings -> Directories` path preserves and hosts the legacy `Directories` vtable and class identity, reads configured/default `hMailServer.ini` directory values with legacy normalization, and exposes read-only `ProgramDirectory`, `DatabaseDirectory`, `DataDirectory`, `LogDirectory`, `TempDirectory`, `EventDirectory`, and `DBScriptDirectory` values. Direct activation remains access-denied; directory mutation and persistence remain explicit `E_NOTIMPL`.
  The bounded `Application -> Database` path preserves and hosts the legacy `Database` vtable, enum, and class identity, reads configured/default `hMailServer.ini` database type/server/name settings, reports required/current SQL database version and connection state, and preserves legacy per-member authentication for admin-only configuration fields. Authenticated `UtilGetFileNameByMessageID` performs a parameterized 64-bit lookup of only `hm_messages.messagefilename`, returns the stored string or the legacy empty string for a missing row, and performs no file/path/content access. SQL execution, transactions, database creation/default-selection, script execution, and prerequisite operations remain explicit `E_NOTIMPL`.
  The bounded `Application -> Status` path preserves and hosts the legacy `Status` vtable and class identity, requires authenticated `Application` access, reads legacy delivery-queue rows from `hm_messages`/`hm_messagerecipients`, exposes process start time and message/spam/virus/session counters from a runtime snapshot, and keeps direct activation access-denied.
  The bounded `Application` core-scalar path exposes legacy `Version` without authentication, exposes authenticated `ServerState` and `InitializationFile`, preserves legacy `VersionArchitecture` values (`x86`/`x64`), and keeps `Start`, `Stop`, `Connect`, `Reinitialize`, and `SubmitEMail` explicit `E_NOTIMPL`.
  The bounded `Application -> Utilities` path preserves and hosts the legacy `Utilities` vtable and class identity. It exposes unauthenticated pure helpers for MD5, legacy salted SHA256, GUID generation, email/domain/IP validation, strong-password checks, criteria matching, legacy Blowfish encryption/decryption through the existing static-key compatibility cipher, IPv4-only local-host checks, and legacy mail-server DNS lookup through injected service runtimes, plus authenticated read-only message-ID lookup, IMAP folder UID maintenance, Windows service dependency replacement, `ImportMessageFromFile`, `ImportMessageFromFileToIMAPFolder`, and `EmailAllAccounts`. The file-import operation preserves DISPID 8 and its `VARIANT_BOOL` result, accepts only existing files under the configured data directory, keeps legacy partial-first/exact lookup plus already-partial success, normalizes exact-path or misplaced in-tree files to GUID/bucket filenames before persistence, imports account-targeted files to Inbox with MIME-derived metadata and UID allocation, imports `accountId == 0` files into the queue with only local To/CC recipients, and wakes delivery only after the durable write succeeds. The IMAP-folder overload preserves DISPID 13 and its `VARIANT_BOOL` result, reuses that same validation and normalization path, preserves empty-folder/InBox fallback and `accountId == 0` queue behavior, cleans legacy date tokens plus one leading hierarchy delimiter, and currently routes only to existing private account folders resolved through legacy modified UTF-7 path segments. The mass-mail operation preserves DISPID 9 and its `VARIANT_BOOL` result, selects active accounts whose address-derived domain is active in account-address order, applies the legacy case-insensitive `*`/`?` wildcard, and writes one legacy-shaped plain-text MIME message with local recipient IDs and an empty envelope sender through the existing signaling queue writer. The explicit queue-write flag used by this operation preserves legacy zero-recipient message creation without weakening the normal SMTP empty-recipient guard. Blowfish preserves DISPIDs 5/6, BSTR contracts, Latin-1 conversion, lower-case hex, block padding, empty strings, and valid-ciphertext round trips; it does not add password storage, database writes, key rotation, or secret migration, so SEC-21 remains open. Automatic IMAP-folder creation, public-folder import semantics, and `RunTestSuite` remain explicit gaps; side-effecting members still enforce the legacy server-admin boundary before returning that status.
  The bounded `Application -> Links` path preserves and hosts the legacy `Links` vtable and class identity. Authenticated DBID lookup for `Domain`, `Account`, `Alias`, and `DistributionList` reuses the existing read-only administration stores/adapters without new SQL or mutations; unknown IDs return legacy `DISP_E_BADINDEX`, and direct activation remains access-denied.
  The bounded `Application -> Diagnostics` path preserves and hosts the legacy `Diagnostics`/`DiagnosticResults`/`DiagnosticResult` vtables and class identities, exposes authenticated `LocalDomainName`/`TestDomainName` process-local state, and returns read-only result collections through an injected deterministic runtime boundary. Direct activation remains access-denied; actual DNS, SMTP, network, filesystem, and broader operational health checks remain out of scope.
- `tests/HMailServer.Net10.Tests`: MSTest coverage for protocol framing, literal reads, SpamAssassin response/client behavior, ClamAV, SpamAssassin, attachment policy, DNSBL, reverse DNS/PTR, sender-domain MX, greylisting, SURBL pipeline wiring, SPF parsing/evaluation/result/limit behavior, system-DNS response parsing, disabled-by-default SMTP policy mapping, DKIM parsing/canonicalization/body-hash/header-signature/message-level verification and disabled-by-default DKIM policy mapping, DMARC record parsing/evaluation/alignment behavior, local public-suffix wildcard/exception resolution, pinned public-suffix snapshot integrity/current multi-label resolution, and disabled-by-default SMTP DMARC policy/receiver wiring, SQL search/sort planning, failed-logon auto-ban SQL shape and protocol disconnect wiring, external fetch account/UID SQL shape, bounded Settings scalar COM contract/store coverage plus authenticated SQL integration, AntiVirus/custom/ClamWin/ClamAV scanner tests, AntiSpam/SpamAssassin connection test, Cache.Clear/runtime statistics, BlockedAttachments.Refresh, DNSBlackLists.Refresh, SURBLServers.Refresh, GreyListingWhiteAddresses.Refresh, WhiteListAddresses.Refresh, Routes, Routes.Refresh, retained-Settings route reauthentication, IncomingRelays.Refresh/Delete/DeleteByDBID/Add plus IncomingRelay.Delete/Save, invalid-IP setter/save fallback, delete membership containment, configured direct activation, and reauthentication authorization coverage, SecurityRanges.Refresh, TCPIPPorts.Refresh, SSLCertificates.Refresh/Clear/DeleteByDBID/Add plus SSLCertificate.Delete/Save, direct-activation mutation authorization, and reauthentication authorization coverage, Groups.Refresh, GroupMembers.Refresh, ServerMessages.Refresh plus ServerMessage.Save and retained-Settings reauthentication coverage, RouteAddresses.DeleteByDBID/DeleteByAddress plus RouteAddress.Delete, parent-route ownership guards, and reauthentication authorization coverage, IMAPFolders/SubFolders, IMAPFolderPermissions.Refresh ACL/principal child facade, and Diagnostics COM runtime-boundary coverage, Account size, Accounts.Refresh, Aliases.Refresh, DomainAliases.Refresh, DistributionLists.Refresh, account/global Rules.Refresh, RuleCriterias.Refresh/RuleCriteria.Save, RuleActions.Refresh/RuleAction.Save, FetchAccounts.Refresh plus FetchAccounts COM contract/store coverage with account-scoped SQL integration, Message metadata/MIME COM contract and content-source coverage, Domains.Refresh plus Domain size/allocated-size/AD/greylisting/signature/DKIM COM/store/integration coverage, GlobalObjects language COM/parser/manifest coverage, SMTP session/listener skeleton flow, POP3 session command flow, IMAP LOGIN/AUTHENTICATE/LIST/STATUS/nested SELECT/SEARCH/SORT/FETCH/STORE/COPY/MOVE/APPEND/EXPUNGE/IDLE/ACL/QUOTA parsing, TCP listener flow, and SEARCH/SORT/FETCH/IDLE/ACL/QUOTA, including ENVELOPE/BODYSTRUCTURE, plus STORE/COPY/MOVE/APPEND/EXPUNGE response execution.

  Authenticated existing-row `RuleAction.Subject` stages the exact raw string on the owning item facade and persists it through the existing parameterized `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and no administrator guard, normalization, SQL schema, identity, Add/new-item, ordering, rule execution, SMTP, or delivery behavior changes were made. Focused RuleActions/Rules/store tests pass 28/28; full Net10 reached 1150/1152 with only unrelated scanner cleanup failures.

  Authenticated existing-row `RuleAction.Body` stages the exact raw string on the owning item facade and persists it through the existing parameterized `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and no administrator guard, normalization, SQL schema, identity, Add/new-item, ordering, rule execution, SMTP, or delivery behavior changes were made. Focused RuleActions/Rules/store tests pass 30/30; full Net10 reached 1152/1154 with only unrelated scanner cleanup failures.

  Authenticated existing-row `RuleAction.FromName` stages the exact raw string on the owning item facade and persists it through the existing parameterized `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and no administrator guard, normalization, SQL schema, identity, Add/new-item, ordering, rule execution, SMTP, or delivery behavior changes were made. Focused RuleActions/Rules/store tests pass 32/32; full Net10 passes 1156/1156.

  Authenticated existing-row `RuleAction.FromAddress` stages the exact raw string on the owning item facade and persists it through the existing parameterized `actionfromaddress` `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and no administrator guard, normalization, SQL schema, identity, Add/new-item, ordering, rule execution, SMTP, or delivery behavior changes were made. Focused RuleActions/Rules/store tests pass 27/27; full Net10 passes 1156/1156 with one opt-in native registry test skipped.

  Authenticated existing-row `RuleAction.IMAPFolder` preserves the legacy Modified UTF-7 encode/decode COM boundary and persists through the existing parameterized `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and folder resolution, ACL, creation, data-directory movement, SMTP, and live reconfiguration behavior remain unchanged. Focused RuleActions/Rules/store tests pass 49/49; full Net10 passes 1163/1164 with one opt-in native registry test skipped.

  Authenticated existing-row `RuleAction.HeaderName` stages the exact raw string and persists it through the existing parameterized `actionheader` `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and the existing SMTP `SetHeaderValue` runtime path remains unchanged. Focused RuleActions/Rules/store tests pass 51/51; full Net10 passes 1165/1166 with one opt-in native registry test skipped.

  Authenticated existing-row `RuleAction.Value` stages the exact raw string and persists it through the existing parameterized `actionvalue` `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and existing `SetHeaderValue`/`BindToAddress` runtime behavior remains unchanged. Focused RuleActions/Rules/store tests pass 53/53; full Net10 passes 1167/1168 with one opt-in native registry test skipped.

  Authenticated existing-row `RuleAction.RouteID` stages the raw integer and persists it through the existing parameterized `actionrouteid` `Save()` path; attached setters accept zero, negative, and arbitrary values without route validation, while detached/direct activation remains access-denied and read-only facades remain `E_NOTIMPL`. Existing route selection and delivery runtime behavior remains unchanged. Focused RuleActions/Rules/store tests pass 55/55; full Net10 passes 1169/1170 with one opt-in native registry test skipped.

  Authenticated existing-row `RuleAction.AbortSpamFlagged` stages the exact `VARIANT_BOOL` value and persists it through the existing parameterized `actionabortspamflagged` `Save()` path; detached/direct activation remains access-denied, read-only facades remain `E_NOTIMPL`, and the legacy no-field-specific-admin-guard behavior is preserved. Focused RuleActions/Rules/store tests pass 58/58; full Net10 passes 1172/1173 with one opt-in native registry test skipped.

  Account-rule runtime now carries persisted `hm_messages.messageflags` spam state through `SmtpReceiveRequest` and skips only spam-flagged Forward/Reply actions when `AbortSpamFlagged` is true, after the legacy loop/recipient prechecks. Later actions continue normally. The inbound SMTP path now runs the existing spam scan/policy before global rule processing and carries the resulting flag through `SmtpReceiveRequest`, matching the legacy pre-queue classification boundary without changing later DKIM/DMARC, antivirus, trust, or delivery stages. Focused receiver/rule tests pass 52/52; full Net10 passes 1181/1182 with one opt-in native registry test skipped.

  Generated-message flag parity now records the legacy distinction: `PersistentMessage::CopyToQueue`/`RuleApplier::ApplyAction_Forward` copies the source spam flag, while `ApplyAction_Reply` creates a fresh message and receives only the new-message `Recent` flag. The .NET `SmtpRuleGeneratedMessage.SpamFlagged` field carries Forward/CreateCopy state through the global and account generated queue writers; Reply remains clean. Focused processor/receiver tests pass 53/53; full Net10 passes 1182/1183 with one opt-in native registry test skipped. SPF/DKIM/DMARC classification ordering and coverage remain a separate gap.

  CreateCopy now preserves the original spam flag through its generated queue message, matching legacy `RuleApplier::ApplyAction_Copy` -> `PersistentMessage::CopyToQueue` -> `CreateCopy_`. Focused processor/receiver tests pass 53/53; full Net10 passes 1182/1183 with one opt-in native registry test skipped. SPF/DKIM/DMARC classification ordering and coverage remain a separate gap.
  SPF/DKIM/DMARC classification ordering now matches the legacy pre-queue boundary: SPF pre-transmission and DKIM post-transmission results, plus the existing optional DMARC policy, are applied before global rules, and their combined spam state is visible to `SmtpReceiveRequest` and the primary queue writer. Legacy `SMTPConnection::DoPreAcceptSpamProtection_`/`DoPreAcceptMessageModifications_` and `SMTPDeliverer::RunGlobalRules_` remain the reference; no default policy rejection or live reconfiguration was added. Focused receiver tests pass 54/54; full Net10 passes 1183/1184 with one opt-in native registry test skipped. Shared WebAdmin scanner-target egress remains the next security gate.
  WebAdmin ClamAV/SpamAssassin scanner-test egress input hardening now uses a shared POST-only source path, the existing once-resolved local IPv4 literal policy, strict decimal port validation for `1..65535`, and a shared HTTP 400/body `0` rejection path before COM. Legacy `Utilities::IsLocalHost`, `VirusScannerTester::TestClamAVConnect`, and `SpamAssassinTestConnect::TestConnect` remain the behavior references; valid local scanner behavior, direct COM tests, scanner settings, SMTP scanning, and external fetch remain unchanged. PHP lint/runtime checks pass; focused .NET contract tests pass 38/38 and full Net10 passes 1183/1184 with one opt-in native registry test skipped. The `BackupManager.StartBackup` preflight/plan, authenticated operation-state/dispatch, service-owned maintenance queue/status/failure callback boundary, read-only queued `BackupStartPlan` acquisition, archive/XML creation, non-secret raw settings-property coverage, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, and backup-side normal domain `Aliases` child serialization are complete in `8681b1d23`, `cf15929a0`, `832b9c933`, `d4360fd3e`, `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, and `3e7535d76`; focused backup/alias coverage passes 54/54. The next gate is `DistributionLists` child serialization.

## Security Review Status

The deduplicated 2026-06 security inventory and per-finding disposition are maintained in `REWRITE_BACKLOG.md`; the maintainer-supplied June 27 inventory maps to the same 21 unique records and adds no new finding. Current security hardening makes an unset administrator hash fail closed in both legacy and .NET 10, removes constructor-time anonymous COM administration, fixes legacy JScript literal escaping for password validation, delivery failure, and external-account UID events, restricts rule `ScriptFunction` names and COM mutation authorization, requires authentication for legacy SMTP `ETRN`, requires destination-parent Create ACL permission for legacy public-folder `RENAME`, regenerates WebAdmin sessions after login, removes the predictable CSRF-token fallback, hardens WebAdmin AV/SpamAssassin AJAX test actions, external-account mutations, dedicated settings/start/test/control/save pages, and Mirror save to POST-only handlers with CSRF tokens outside the URL, pins each WebAdmin ClamAV/SpamAssassin test host to a once-resolved local IPv4 address verified through `Utilities.IsLocalHost`, and quotes/escapes the custom antivirus `%FILE%` message-file argument. The scanner guard rejects remote, unresolved, IPv6-only, and array-valued targets before COM invocation; normal scanner behavior, direct COM tests, and external fetch remain unchanged. Legacy `background_*_save.php` add/edit handlers for domains and rules still need the same POST-body/CSRF audit; the incoming-relay, server-message, blocked-attachment, alias, route, SecurityRanges, account, and other recorded SEC-14 mutation handlers have now been hardened. The earlier ClamAV INSTREAM framing defect is already fixed in both implementations. The reported critical VBScript password payload was revalidated on June 28: quote doubling keeps the expression inside the string value, and executable Windows Script Host coverage confirms it reaches the event handler as data. The separate VBScript delivery-failure report likewise remains unconfirmed and regression-covered.

The WebAdmin external-account background save path now requires POST before scope IDs or domain/account/fetch-account objects are read, uses POST-only accessors for action/scope/field values, and requires the CSRF token in POST rather than the query string. The existing background entry point still validates CSRF through `index.php`, and the add/edit form carries the token in a hidden POST field. Existing field mapping, account/domain ownership checks, and save redirect remain unchanged. On edit, a blank or omitted password now clears the stored password when the `ServerAddress`/`Port`/`Username`/`ConnectionSecurity` authority tuple changes; unchanged tuple edits retain the existing password, explicit replacement passwords win, and new adds retain their existing behavior. The hardening is WebAdmin-only: legacy `InterfaceFetchAccount`/`PersistentFetchAccount` behavior remains the reference, while .NET 10 `FetchAccount` setters and `Save()` remain `E_NOTIMPL`.

Current WebAdmin reconciliation: AV/SpamAssassin, external-account, settings, protocol, Mirror, distribution-list, IncomingRelays, server-message, blocked-attachment, route-address, distribution-list-recipient, alias, route, SecurityRanges, account, rule, and domain handler hardening is complete in the listed commits. The account, rule, and domain candidates are superseded by `95a7e4284`, `6736e161e`, and `3d25cb0a7` above; the next COM/Admin slice is authenticated existing-row `RuleCriteria.MatchType` setter parity through the owning `RuleCriteria.Save()` path. PHP CLI lint remains unavailable.

SEC-18 remains open because legacy PHP WebAdmin still stores the authenticated mailbox/admin password in the PHP session while each request creates a fresh COM `Application`. The staged native broker design remains unregistered. Commit `16e8b431f` binds caller freshness to a post-host-read collector timestamp and adds explicit service-read-error attestation coverage; the timestamp is not yet a trusted final-state seal. The prior `staging-inventory-20260726-live-bound.json` remains intentionally rejected as stale and no fresh authorized probe has been run in this slice. Fresh reviews remain `YELLOW` for bounded evidence and `RED` for permanent registration because the bundle is not externally signed or pinned, operator-supplied correlation is not a collector-issued challenge, and broker-only authorization/method enforcement remains unimplemented. PHP session cutover was not attempted. See `artifacts/sec18-staging/staging-inventory-20260726-final-timestamp-gate.json` for the read-only stale-evidence rejection and `WEBADMIN_SESSION_REAUTH_DESIGN.md` for the invocation contract. SEC-11 `RuleAction.To`, `RuleAction.IMAPFolder`, `RuleAction.HeaderName`, `RuleAction.Value`, `RuleAction.RouteID`, and `RuleAction.AbortSpamFlagged` setter parity, account-rule Forward/Reply consumption, inbound SMTP spam-state ordering, Forward/CreateCopy generated-message flag propagation, SPF/DKIM/DMARC pre-rule classification, and WebAdmin ClamAV/SpamAssassin scanner-test target hardening are complete. WebAdmin performance, backup, auto-ban, greylisting, logging, scripting, diagnostics, status, TLS, POP3, IMAP, SMTP, SMTP AntiVirus, SMTP AntiSpam, Mirror, distribution-list, and IncomingRelays mutation hardening is complete in `7c7ca1049`, `7338030e6`, `a5384ae1b`, `28a830f5f`, `363a9cfb8`, `8894239af`, `5e49e73e1`, `ba2261292`, `1bd30eea`, `c28d23d79`, `5e694f49c`, `122847319`, `68d6f0006`, `8c751e65f`, `9740cbc62`, `c7e5bc23a`, and `fc2aa90f6`; the next candidate is the bounded `background_distributionlist_recipient_save.php` POST-only/CSRF audit. Backup message payloads, restore, and the next backup-side `DistributionLists` child-serialization slice remain separately fenced.

## Database

The SEC-18 deterministic installed-Application graph collector, key-DACL capture, and native owner/DACL readback test remain read-only and unchanged. The 2026-07-22 post-cleanup graph evidence reports 44 snapshots with zero differences from the pre-probe graph; the temporary probe ACL and registration readback are retained only as sanitized evidence. No permanent broker AppID, DCOM ACL, existing Application identity, PHP session behavior, service, database, data directory, or firewall object was changed. The `RuleAction.To`, `RuleAction.IMAPFolder`, `RuleAction.HeaderName`, `RuleAction.Value`, and `RuleAction.RouteID`/`RuleAction.AbortSpamFlagged` setter slices, account-rule Forward/Reply consumption, inbound SMTP spam-state ordering, Forward/CreateCopy generated-message flag propagation, SPF/DKIM/DMARC pre-rule classification, and WebAdmin ClamAV/SpamAssassin scanner-test target hardening are complete. The `BackupManager.StartBackup` preflight/plan, authenticated operation-state/dispatch, service-owned maintenance queue/status/failure callback boundary, read-only queued `BackupStartPlan` acquisition, archive/XML creation, non-secret raw settings-property coverage, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, and backup-side normal domain `Aliases` child serialization are complete in `8681b1d23`, `cf15929a0`, `832b9c933`, `d4360fd3e`, `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, and `3e7535d76`; the next code slice is backup-side `DistributionLists` child serialization, while destructive restore and the SEC-18 permanent registration gate remain RED.

Apply `hmailserver/source/DBScripts/Upgrade5708to6000MSSQL.sql` on a backed-up MS SQL hMailServer database. It adds delivery lease columns, rule delivery metadata columns, search queue/documents tables, `hm_delivery_queue_status`, and the SQL Server Full-Text Search catalog/index used by fast mode. Set `HMAILSERVER_DELIVERY_STATUS_SQL_ENABLED=true` after that migration to persist delivery worker transition events to SQL Server.

The migration is additive: existing `hm_messages`, `hm_message_metadata`, and the data directory remain the source of truth during the transition.

## SEC-14 WebAdmin DNS Blacklist Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-134`), `InterfaceSettings::get_AntiSpam` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1138-1159`), `InterfaceAntiSpam::get_DNSBlackLists` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:707-722`), `InterfaceDNSBlackLists` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:12-194`), `InterfaceDNSBlackList` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:14-242`), and `PersistentDNSBlackList` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:25-89`). Legacy `Add()` returns an unsaved item with `ID == 0` scoped to the owning collection; `Save()` inserts `Active`, `DNSHost`, `ExpectedResult`, `RejectMessage`, and `Score`, assigns the generated identity, and adds the item after success. The installed DNSBL IIDs, DISPIDs/vtable order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:1437-1469,2236,3057-3070` and the DNSBL registry resources.
- Code/test commit `92586a66d` hardens `hmailserver/source/WebAdmin/background_dnsblacklist_save.php`: the existing server-admin check remains first, the handler requires `hmailRequirePostCsrfToken()`, and all seven mutation fields are read from POST only. Existing `Settings -> AntiSpam -> DNSBlackLists` Add/Edit/DeleteByDBID, `Active`/`DNSHost`/`ExpectedResult`/`RejectMessage`/`Score` assignments, Save, and redirects remain unchanged. `WebAdminDnsBlackListPostOnlySourceTests`, `DnsBlackListsComContractTests`, and `SqlServerDnsBlackListAdministrationStoreTests` pass `9/9`; full Net10 passes `1269` with `3` opt-in skips. PHP is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET read-only DNSBL path remains separate in `DnsBlackLists.cs` and `SqlServerDnsBlackListAdministrationStore.cs`; COM writes remain `E_NOTIMPL`. No DNSBL/SMTP runtime, live reconfiguration, COM identity, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest live WebAdmin mutation is `hmailserver/source/WebAdmin/background_whitelistaddress_save.php`; keep broader background hardening and PHP session cutover out of scope.

## SEC-14 WebAdmin Whitelist Address Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-134`), `InterfaceSettings::get_AntiSpam` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1138-1159`), `InterfaceAntiSpam::get_WhiteListAddresses` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:546-569`), `InterfaceWhiteListAddresses` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:63-215`), `InterfaceWhiteListAddress` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:8-197`), and `PersistentWhiteListAddress` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:27-92`). Legacy `Add()` returns an unsaved item with `ID == 0` scoped to the owning collection; `Save()` inserts or updates the four persisted address fields and appends a new item after the generated `whiteid` is assigned. The installed whitelist IIDs, DISPIDs/vtable order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:2195-2220,2443-2475,3404-3420` and the whitelist registry resources.
- Code/test commit `34d8fe83e` hardens `hmailserver/source/WebAdmin/background_whitelistaddress_save.php`: the existing server-admin check remains first, the handler requires `hmailRequirePostCsrfToken()`, and all six mutation reads use POST only. Existing lower/upper IP defaults (`0.0.0.0`/`255.255.255.255`), empty email default (`*`), `Settings -> AntiSpam -> WhiteListAddresses` Add/Edit/DeleteByDBID, field assignments, Save, and redirects remain unchanged. `WebAdminWhiteListAddressPostOnlySourceTests`, `WhiteListAddressesComContractTests`, and `SqlServerWhiteListAddressAdministrationStoreTests` pass `12/12`; full Net10 passes `1270` with `3` opt-in skips. PHP is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET read-only whitelist path remains separate in `WhiteListAddresses.cs` and `SqlServerWhiteListAddressAdministrationStore.cs`; COM writes remain `E_NOTIMPL`. No whitelist/DNSBL/SMTP runtime, live reconfiguration, COM identity, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest live WebAdmin mutation is `hmailserver/source/WebAdmin/background_distributionlist_save.php`; keep broader background hardening and PHP session cutover out of scope.

## SEC-14 WebAdmin Distribution List Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceDomains::Refresh/get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceDomains.cpp:49-65`), `InterfaceDomain::get_DistributionLists` (`hmailserver/source/Server/COM/InterfaceDomain.cpp:447-468`), `InterfaceDistributionLists::Add/DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceDistributionLists.cpp:38-84`), `InterfaceDistributionList` setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceDistributionList.cpp:81-277`), `PersistentDistributionList::SaveObject/DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:36-157`), and `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`). Legacy `Add()` creates an unsaved item with `ID == 0` scoped to the owning domain collection; `Save()` inserts or updates `distributionlistdomainid`, `distributionlistenabled`, `distributionlistaddress`, `distributionlistrequireauth`, `distributionlistrequireaddress`, and `distributionlistmode`, assigns a generated identity on insert, and adds a new item after success. The installed `DistributionLists`/`DistributionList` IIDs, DISPIDs/vtable order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:1148-1192,2993-3007` and the distribution-list registry resources. Legacy SMTP list-policy consumption remains in `RecipientParser::UserCanSendToList_` (`hmailserver/source/Server/SMTP/RecipientParser.cpp:365-451`).
- Code/test commit `c7e5bc23a` hardens `hmailserver/source/WebAdmin/background_distributionlist_save.php`: the existing user-level denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and all eight mutation reads use POST only. Existing same-domain domain-admin ownership, `IsAddAllowed`, `Domain -> DistributionLists -> DistributionList`, Add/Edit/Delete, five field assignments, defaults, Save/error handling, and redirects remain unchanged. `WebAdminDistributionListPostOnlySourceTests`, `DistributionListsComContractTests`, `DomainsComContractTests`, and `SqlServerDistributionListAdministrationStoreTests` pass `14/14`; full Net10 passes `1271` with `3` opt-in skips. The edit and delete forms already submit POST with CSRF-bearing fields; PHP CLI is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET distribution-list path remains intentionally read-only in `DistributionLists.cs`, `IDistributionListAdministrationStore`, and `SqlServerDistributionListAdministrationStore.cs`; collection Add/Delete and item setters/Save/Delete remain `E_NOTIMPL`. No COM identity, direct activation boundary, domain-admin/server-admin boundary, SMTP list-policy behavior, live reconfiguration, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest live WebAdmin mutation is `hmailserver/source/WebAdmin/background_distributionlist_recipient_save.php` POST-only/CSRF handling; keep broader background hardening and PHP session cutover out of scope.

## Current Status Update (2026-07-28)

The authoritative SEC-14 state is now TCP/IP hardening in `272d56b5c`, SSL certificate hardening in `4ed9d2f26`, SURBL-server hardening in `d8e785231`, DNS-blacklist hardening in `92586a66`, whitelist-address hardening in `34d8fe83e`, distribution-list hardening in `c7e5bc23a`, IncomingRelays hardening in `fc2aa90f6`, blocked-attachment hardening in `bfee58cab`, route-address hardening in `2394e026f`, distribution-list recipient hardening in `9d6a8dda2`, and alias hardening in `1dc35f169`; the focused alias WebAdmin/COM/store set is `14/14`, and full Net10 is `1279` passed with `3` opt-in skips. The next smallest live WebAdmin mutation slice is `background_route_save.php` POST-only/CSRF handling. The historical `background_iphome_save.php` endpoint is fenced as an orphan because its `hm_iphomes` table was dropped during the 5.0 upgrade and no current COM, .NET, or form surface exists.

SEC-18 remains **RED** for permanent registration. The fresh fail-closed collector `artifacts/sec18-staging/staging-inventory-20260728-nonpool-denial-failclosed.json` exits `2` with IIS unavailable and no caller-token evidence; it reports `DedicatedPoolCandidate=false` and `ReadyForBrokerRegistration=false`. The sanitized approval-rerun report is `artifacts/sec18-staging/SEC18-nonpool-approval-rerun-20260728.md`. The temporary probe is absent, the current endpoint is `404`, hMailServer remains stopped/disabled, and both `hmail_security_reviewer` and `hmail_reality_checker` require a fresh elevated, correlation-bound matrix before any broker or PHP session cutover.
## Current Completed Slice (2026-08-08, RESTORE ROLLBACK ACCEPTANCE)

Test-only code/test commit `e93d0021e` adds disposable LocalDB acceptance for metadata failure compensation in the bound raw non-DB restore executor. An injected alias insertion failure now proves Data-directory rollback and removal of generated account/domain rows through the real SQL administration stores. The production-compatible fixture includes the delete-path tables required by those stores.

Legacy anchors are `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `BackupManager::OnBackupFailed` (`BackupManager.cpp:177`), and sequential `Collection<T,P>::XMLLoad`/`DeleteAll` (`Collection.h:85`). Focused LocalDB coverage is `3 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 18 skipped`; SQL-enabled full Net10 is `1919 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

The slice remains bounded to disposable restore acceptance. It does not claim queued COM event delivery, full child-list rollback, crash-safe SQL/filesystem atomicity, normal-installation deletion/reinitialization ordering, or release readiness. Next slice: shared SQL transaction or durable restore journal with crash/recovery tests.
## Current Completed Slice (2026-08-08, DISTRIBUTION-LIST ROLLBACK ACCEPTANCE)

Test-only code/test commit `387589ce1` adds disposable LocalDB acceptance for failure after distribution-list creation. A first recipient insertion failure proves Data-directory rollback and cleanup of generated list, alias, account, and domain rows through the real SQL administration stores. Focused coverage is `4 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 18 skipped`; SQL-enabled full Net10 is `1919 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

Legacy list/recipient save order is anchored by `DistributionList::XMLLoad`/`XMLLoadSubItems`, `DistributionListRecipients::PreSaveObject`, and persistent list/list-recipient `SaveObject` symbols. Legacy leaves partial rows after recipient failure; the bounded .NET executor compensates in reverse dependency order. The next test slice will fail the second recipient insert to prove real recipient deletion. Release remains RED.
## Current Completed Slice (2026-08-08, TRANSACTION-SCOPED RESTORE STORES FAIL CLOSED)

Code/test commit `342f95325` makes unsupported members on transaction-scoped SQL restore stores fail closed with `InvalidOperationException`. The shared transaction remains limited to the DB-only restore insert/read paths; no independent connection is opened from a transaction-scoped store. Focused SQL restore coverage is `11 passed, 0 failed, 0 skipped`; default full Net10 is `1914 passed, 0 failed, 26 skipped`.

Legacy has no transaction-scoped administration-store API: `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`) and `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-220`) use ordinary collection persistence, while `IInterfaceDatabase::BeginTransaction` only scopes `ExecuteSQL`. Current symbols are `IBackupRestoreMetadataTransaction`, `SqlServerBackupRestoreMetadataTransaction`, and the five SQL administration stores. COM identities, authentication boundaries, SMTP behavior, and production/machine state are unchanged.

Release status remains RED. Execution-time queued-restore authorization, archived credential preservation, crash-safe SQL/filesystem recovery, full deletion/reinitialization, isolated service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: revalidate authorization immediately before queued restore execution.
## Current Completed Slice (2026-08-08, QUEUED RESTORE AUTHORIZATION REVALIDATION)

Code/test commit `1e717bb1d` revalidates the existing authentication-generation guard at `BackupManager.ExecuteRestoreAsync` before a queued restore executor runs. `BackupManagerComContractTests.QueuedRestore_DoesNotInvokeExecutorAfterAuthenticationInvalidation` proves delayed execution is denied with `E_ACCESSDENIED` and no executor call. Focused coverage is `23 passed, 0 failed, 0 skipped`; default full Net10 is `1915 passed, 0 failed, 26 skipped`.

Legacy `COMAuthentication::Authenticate` clears `account_` (`hmailserver/source/Server/COM/COMAuthentication.cpp:30-68`), but legacy `InterfaceBackupManager`/`InterfaceBackup` methods and the queued `BackupTask::DoWork` path do not recheck it after acquisition (`hmailserver/source/Server/COM/InterfaceBackupManager.cpp:43-69`, `InterfaceBackup.cpp:16-33`, `hmailserver/source/Server/Common/Application/BackupTask.cpp:27-40`). The .NET guard is deliberate security hardening and does not alter COM identity, direct activation, SMTP behavior, SQL schema, or production state.

Release remains RED: preflight can still outlive an auth generation before final SQL/filesystem admission, pending queue shutdown cleanup is incomplete, and credential preservation, crash-safe restore, full deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: final authorization admission immediately before restore mutation.
## Current Completed Slice (2026-08-08, DB-ONLY RESTORE FINAL AUTHORIZATION ADMISSION)

Code/test commit `2e9728452` adds a non-COM authorization admission after DB-only restore preflight and before SQL transaction creation. The focused gated-preflight test proves auth invalidation prevents transaction creation and metadata inserts; coverage is `8 passed, 0 failed, 0 skipped`. Default full Net10 is `1916 passed, 0 failed, 26 skipped`.

Legacy `BackupExecuter::StartRestore` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-335`) and `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-135,203-215`) do not perform worker-time COM authorization checks. The new internal check is intentional security hardening and preserves installed COM identity, direct activation boundaries, SQL schema, SMTP behavior, non-DB path, and production state.

Release remains RED: plain generation checking is not an atomic admission lease, non-DB filesystem staging lacks final admission, and queue shutdown cleanup, credential preservation, crash-safe restore, deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: atomic authorization admission at the SQL mutation boundary.
## Current Completed Slice (2026-08-08, ATOMIC DB-ONLY RESTORE AUTHORIZATION LEASE)

Code/test commit `aae5137a9` adds an internal per-`Application` authorization authority and lease for DB-only restore. Authentication invalidation and SQL restore admission share a linearization point; the lease remains held through transaction begin, metadata mutation, commit, and disposal. Focused coverage is `9 passed, 0 failed, 0 skipped`; default full Net10 is `1917 passed, 0 failed, 26 skipped`.

Legacy `COMAuthentication::Authenticate` (`hmailserver/source/Server/COM/COMAuthentication.cpp:30-68`) and queued `BackupTask::DoWork` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:27-40`) have no generation/lease. The new authority is `[ComVisible(false)]` security hardening; COM identity, direct activation, SQL schema, SMTP behavior, non-DB path, and production state are unchanged.

Release remains RED: non-DB filesystem staging lacks the lease, queued shutdown cleanup is incomplete, and credential preservation, crash-safe restore, deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: apply the lease before non-DB filesystem staging.
## Current Completed Slice (2026-08-08, NON-DB RESTORE AUTHORIZATION LEASE)

Code/test commit `efd873fea` extends the per-Application restore authorization lease to the non-DB `RestoreDomains|RestoreMessages` path. `MetadataBackupRestoreExecutor.ExecuteNonDbDataRestoreAsync` admits the lease immediately before `BackupRestoreDataDirectoryRuntime.RestoreAsync`, covering extraction, Data-directory replacement, rollback, and the following metadata commit. Legacy behavior is anchored by `BackupExecuter::StartRestore` and `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`); the legacy worker does not revalidate Administrator state, so the lease is deliberate security hardening.

Focused restore execution coverage is `11 passed, 0 failed, 0 skipped`; default full Net10 is `1919 passed, 0 failed, 26 skipped`. Tests prove denial before lease admission causes no filesystem or SQL mutation and that authentication invalidation is blocked until copy and metadata commit finish. Full crash-safe SQL/filesystem atomicity, journal reconciliation, normal-installation restore ordering, service/COM acceptance, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: deterministic shutdown drain/abort and idempotent cleanup for queued restore tasks.
## Current Completed Slice (2026-08-08, QUEUED RESTORE SHUTDOWN CLEANUP)

Code/test commit `68a75427c` makes queued backup/restore shutdown deterministic. `BackupTaskQueue` completes and drains pending requests; `BackupTaskHostedService` aborts work dequeued after cancellation before execution; and restore requests clean their private archive binding through the abort callback. `ThreadStopped` notification is idempotent, so coordinator state is released once even when queue disposal follows service cancellation.

Legacy behavior is anchored by `WorkQueue::Stop` (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`), `BackupTask::DoWork` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:27-41`), and `Application::ExitInstance` (`hmailserver/source/Server/Common/Application/Application.cpp:211-234`). Focused queue/restore coverage is `28 passed, 0 failed, 0 skipped`; default full Net10 is `1922 passed, 0 failed, 26 skipped`.

The slice does not change COM identity or activation, SQL schema, SMTP trust, live reconfiguration, or production service/SQL/Data state. It does not provide crash-safe SQL/filesystem restore, full restore ordering, credential encryption parity, service/COM end-to-end evidence, SEC-18, installer, AD/DC, or lifecycle acceptance. Next slice: preserve archived account credential/encryption type during restore.
## Current Completed Slice (2026-08-08, QUEUE SHUTDOWN ADMISSION FENCE)

Code/test commit `ba8390f2c` linearizes backup/restore queue shutdown admission. `BackupTaskQueue.StopAccepting` changes queue state under the same lifecycle lock used by `TryEnqueue`, so work submitted after shutdown begins is rejected rather than accepted into a queue with no worker. Pending requests are drained after worker shutdown, and cancellation-dequeued requests are aborted before execution.

Legacy behavior is anchored by `WorkQueue::Stop` and `WorkQueueManager::RemoveQueue` (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`, `hmailserver/source/Server/Common/Threading/WorkQueueManager.cpp:68-107`). Focused queue/restore coverage is `28 passed, 0 failed, 0 skipped`; default full Net10 is `1922 passed, 0 failed, 26 skipped`. Remaining risks are non-cooperative active-task shutdown, abort callback exception isolation, duplicate/denied archive-binding ownership, and the broader restore, COM/service, SEC-18, installer, AD/DC, and lifecycle gates. Next slice: fence or explicitly retain non-cooperative active restore during shutdown.
## Current Completed Slice (2026-08-08, ACTIVE RESTORE SHUTDOWN COMPLETION FENCE)

Code/test commit `3599ce44d` makes service shutdown wait for the active backup/restore request to finish its execute or abort path. `BackupTaskHostedService` tracks the active request through `NotifyThreadStopped`; `StopAsync` closes admission, cancels the worker, then waits for completion even when the host timeout fires. Focused queue/restore coverage is `29 passed, 0 failed, 0 skipped`; default full Net10 is `1923 passed, 0 failed, 26 skipped`.

Legacy `WorkQueue::Stop` interrupts and joins maintenance workers (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`). The new non-cooperative test confirms that .NET shutdown does not return while a delegate still runs. The fence does not force-kill a stuck task, and broader restore atomicity, archive ownership, service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: isolate pending-abort callback failures while draining.
## Current Completed Slice (2026-08-08, QUEUED ABORT FAILURE ISOLATION)

Code/test commit `4864a4dba` continues draining queued restore cleanup after an abort callback fails. The queue traces the callback failure and proceeds to remaining requests; cancellation-dequeued requests are logged similarly by the hosted service. Focused queue/restore coverage is `30 passed, 0 failed, 0 skipped`; default full Net10 is `1924 passed, 0 failed, 26 skipped`.

Legacy pending `BackupTask` objects were discarded by `WorkQueue::Stop` without cleanup callbacks (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`). This is an internal cleanup hardening with no COM identity, activation, SQL, SMTP, or live-reconfiguration changes. Next slice: close non-queued archive binding ownership on duplicate/denied dispatch.
## Current Completed Slice (2026-08-08, REJECTED RESTORE ARCHIVE OWNERSHIP)

Code/test commit `d1fa4a6a5` gives queued restore archive bindings explicit ownership. Same-object duplicate dispatch leaves the first queued snapshot intact; a distinct `AlreadyRunning`, queue-unavailable, thrown-dispatch, or pre-dispatch denied restore cleans only its own rejected binding. Focused BackupManager/COM coverage is `26 passed, 0 failed, 0 skipped`; default full Net10 is `1926 passed, 0 failed, 26 skipped`.

Legacy ownership is anchored by `BackupManager::StartRestore` and `BackupTask::SetBackupToRestore` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:75-98`, `hmailserver/source/Server/Common/Application/BackupTask.cpp:44-49`). The internal claim does not change COM identity, activation, SQL, SMTP trust, live reconfiguration, or production state. Next slice: preserve archived account credential/encryption type during restore.
## Current Completed Slice (2026-08-08, PUBLIC-FOLDER RESTORE CLEANUP CAPABILITY)

Code/test commit `5d9ad666c` adds a transaction-scoped public-folder cleanup capability without wiring it into restore orchestration. The SQL path preserves legacy public-folder ownership by selecting only `folderaccountid = 0` and `messageaccountid = 0`, removes recipients for non-Delivered messages plus search/metadata/ACL rows before messages and non-Inbox folders, and uses the existing caller-owned SQL transaction for commit or rollback.

Legacy behavior is anchored by `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), the public-folder `DeleteAll()` call (`BackupExecuter.cpp:287-289`), `PersistentIMAPFolder::DeleteObject`, and `Reinitializator::ReInitialize` (`Reinitializator.cpp:35-57`). DB-only restore intentionally does not invoke this capability because legacy skips public-folder deletion for `bMessagesDBOnly`; public message-file staging and full-restore reinitialization remain unimplemented.

Focused store tests: `11 passed, 0 failed, 0 skipped`. Full Net10: `1937 passed, 0 failed, 29 skipped`. Release remains RED.

## Current Completed Slice (2026-08-08, SOURCE-HANDLE-BACKED RESTORE SWAP)

Code/test commit `3e912982a` routes the non-DB restore target swap and rollback through the internal Windows-only `WindowsBackupRestoreDataDirectoryMutation`. It opens the source directory with `CreateFileW` and applies a non-overwriting `FILE_RENAME_INFO` rename; `BackupRestoreDataDirectoryRuntime` uses the seam for both mutation directions. Focused runtime coverage is `17 passed, 0 failed, 0 skipped`; default full Net10 is `1939 passed, 0 failed, 29 skipped`.

Legacy `BackupExecuter::RestoreDataDirectory_` and `FileUtilities::CopyDirectory` are path-based (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:339-388`, `hmailserver/source/Server/Common/Util/FileUtilities.cpp:370-402`). This is bounded hardening, not full handle-relative containment: absolute destination resolution, path-based `CopyTree`, and path-based cleanup remain residual risks. No COM/IDL, SQL, protocol, service, recovery-journal, or production state changed. Next slice: full-restore public-folder deletion with staged message-file cleanup and reinitialization ordering on isolated disposable SQL/Data.
## Current Completed Slice (2026-08-08, PUBLIC RESTORE DELETION MANIFEST)

Code/test commit `4cc66396a` adds an additive transaction-scoped `DeleteAllPublicFoldersForRestoreWithManifestAsync` capability. It captures the legacy public-folder message filename, account/folder IDs, account address, and message type before deleting public-folder dependents; the existing caller-owned SQL transaction still controls commit or rollback, and the existing no-return capability remains available. Legacy behavior is anchored by `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `PersistentIMAPFolder::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:48-97`), and `PersistentMessage::DeleteObject`/`DeleteFile` (`hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:48-146`).

Focused static SQL coverage is `11 passed, 0 failed, 0 skipped`; the related disposable SQL integration set is `3 skipped` because the approved local SQL connection and database-create opt-in are unset. Default full Net10 is `1939 passed, 0 failed, 31 skipped`. The commit does not wire full `RestoreSettings|RestoreDomains|RestoreMessages` orchestration, physical file cleanup, reinitialization, COM identity, protocol behavior, or production state. Release remains RED for full restore ordering, live SQL/Data acceptance, SQL/filesystem atomicity, native containment, process-kill/power-loss, service/COM, SEC-18, installer, migration, AD/DC, and soak gates.

## Current Completed Slice (2026-08-08, DB-ONLY DOMAIN CLEANUP WIRING)
## Current Authoritative Continuation (2026-08-10, PAIRED LIVE PERFORMANCE GATE)

The current paired evidence is **RED**. `build/benchmark-net10-live-protocol.ps1`
ran the same SMTP, IMAP, and POP3 loopback scenarios against separate
disposable MSSQLSERVER databases and separate Data directories containing a
byte-identical 1,000-message corpus. `artifacts/benchmarks/live-cpp-net10-20260810_152708/paired-live-comparison.md`
contains the raw JSON/CSV and chart inputs.

| Scenario | .NET 10 | Legacy C++ | Ratio |
| --- | --- | --- | --- |
| SMTP greeting/EHLO/QUIT | `25/25`, p95 `13.616 ms` | `25/25`, p95 `10.948 ms` | invalid |
| IMAP login/select/search/sort/logout | `25/25`, p95 `3.027 ms` | `4/25`, p95 `29.929 ms` | invalid |
| POP3 login/stat/list/quit | `25/25`, p95 `5.962 ms` | `0/25`, no successful sample | invalid |

The C++ `/Debug` probe was not a normal reproducible release build and did not
open POP3. The normal .NET 10 host opens all three listeners but fails the
installed Application AppID COM identity check (`0x80004015`), so the benchmark
used a listener-only helper with COM intentionally omitted. No performance
winner or speed-up claim is valid. The later concurrent IMAP run provides
valid .NET 10-only evidence (`1000/1000`) but C++ completed `0/1000`, so it
remains non-comparable. SMTP message acceptance, delivery queue, and 24-hour
soak remain unmeasured.
## Current production-gate status (2026-08-10, 1,000-concurrent IMAP)

Code/test commit `21cc042c9` adds the isolated live concurrent IMAP runner and
validator. Both disposable SQL targets have the same 1,000-message metadata,
the same byte-identical Data corpus (`1000/1000`), the same account/root
`INBOX`, and the same loopback ports `2525`/`1143`/`25110`. .NET 10 completed
`1000/1000` authenticated `LOGIN`, `SELECT`, `SEARCH`, `SORT`, and `LOGOUT`
sessions with p50 `48.706 ms`, p95 `183.157 ms`, and p99 `558.690 ms`.

The temporary legacy C++ `/Debug` process completed `0/1000`; it aborted the
IMAP banner/read path and did not open POP3. Therefore this is valid .NET 10
acceptance evidence but not a paired comparison. No ratio or performance
winner is valid, and the release performance gate remains **RED**. SMTP
message acceptance, delivery queue, service/COM lifecycle, and 24-hour soak
remain open.

The commandable runner is `build/benchmark-net10-live-concurrent-imap.ps1`;
the focused report validator is `build/test-net10-live-concurrent-imap.ps1`.
## Current parity continuation (2026-08-10, Rules restore)

Code/test commit `4f43db7b2` implements the bounded legacy Rules/Criteria/Actions
restore slice. Legacy anchors are `Rule::XMLStore`, `Rule::XMLLoadSubItems`,
`PersistentRule::SaveObject`, `PersistentRuleCriteria::SaveObject`, and
`PersistentRuleAction::SaveObject` in `hmailserver/source/Server/Common/BO`
and `Persistence`. Current anchors are
`BackupArchiveXmlSnapshotParser.ParseRule`,
`BackupRestoreMetadataWriter.RestoreRulesAsync`,
`MetadataBackupRestoreExecutor.RestoreMetadataAsync`, and the transaction-aware
SQL rule stores.

The isolated SQL readback and injected action failure rollback tests pass
`13/13`; default full Net10 is `1991 passed, 37 skipped, 0 failed`. SQL opt-in
full execution is `2020 passed, 2 skipped` with six unrelated existing
message/indexing fixture failures. The slice does not change COM identity,
Administrator access boundaries, SMTP trust, production SQL/Data, service,
IIS, DCOM, or machine state. Release remains RED. Next: reproduce the legacy
C++ IMAP/POP3 runtime, then continue with populated folder/message/settings
restore rollback evidence.
## Latest parity slice: SMTP greeting

Commit `c26479d9b` matches the legacy `WelcomeSMTP` banner rules in the Net10
SMTP session. Empty settings use the machine name followed by `ESMTP`; custom
settings get the suffix unless it is already present. The authenticated COM
setter publishes successful changes to the running greeting provider.

Legacy references: `source/Server/SMTP/SMTPConnection.cpp:166-205`,
`source/Server/SMTP/SMTPConfiguration.cpp:113`,
`source/Server/COM/InterfaceSettings.cpp:679-696`, and
`source/Server/hMailServer/hMailServer.idl:547`. Focused coverage is `135/135`
and full Net10 coverage is `2118 passed, 39 skipped, 0 failed`.

The paired C++/.NET10 performance release gate remains **RED**. Existing
artifacts do not prove identical SQL/Data copies and message corpora, and the
C++ concurrent IMAP run is `0/1000` versus Net10 `1000/1000`; no speed-up or
performance superiority is claimed.
## Current parity continuation (2026-08-11, DISPOSABLE SQL OPT-IN GREEN)

Code/test commit `8972eb9d4` repairs the isolated SQL integration fixtures and
fixes `SqlServerWhiteListAddressAdministrationStore.GetWhiteListAddressesAsync`
to consume `SequentialAccess` columns in the legacy SELECT order. Focused
SQL/IMAP/COM integration coverage is `16/16`; whitelist store coverage is
`11/11`; the full opt-in MSSQL disposable run is `2156 passed, 2 skipped,
0 failed`. Installer artifact and native registry integration remain explicit
skips. No production SQL/Data/service/COM registration or DCOM ACL changed.

The paired C++/.NET10 performance gate remains **RED** because the shared
start state does not yet produce a completed equivalent protocol workload.
Next slice: repair or replace the isolated C++/Net10 protocol target before
claiming any speed-up.
## Current parity continuation (2026-08-11, composed mode-7 dispatch)

Code/test commit `149770381` adds a focused Administrator backup dispatch test.
It uses the real `SevenZipBackupArchiveRuntime`, `BackupManager.StartBackup`,
`LoadBackup`, and hosted task queue to verify mode `7` reaches `StartRestore`
with all settings/domain/message flags selected. The restore executor is a
recording test double, so this is not a SQL/Data restore acceptance test.

Focused coverage is `1 passed, 0 failed`; full default Net10 is `2124 passed,
42 skipped, 0 failed`. The paired protocol performance gate remains **RED**.
Next slice: raw DataBackup reparse-point rejection and failure cleanup.
## Current parity continuation (2026-08-11, raw DataBackup staging hardening)

Code/test commit `73405caa1` rejects reparse points in
`SevenZipBackupArchiveRuntime.CopyDirectory` and cleans partial raw
`DataBackup` staging after an unsuccessful archive creation. Focused coverage
is `46 passed, 1 skipped, 0 failed`; full default Net10 is `2125 passed, 43
skipped, 0 failed`. The paired protocol performance gate remains **RED**.
Next slice: harden the shared-baseline collector and make its graph use current
latency samples.
## Current performance evidence (2026-08-11)

The disposable paired baseline has `33/33` matching SQL table row counts and
`1000/1000` matching Data file hashes on loopback SMTP `2525`, IMAP `1143`, and
POP3 `25110`. Live completion is not equivalent: C++ POP3 readiness fails;
Net10 SMTP is `25/25`, IMAP and POP3 are `0/25`; concurrent IMAP is `0/1000`
successes for Net10 and did not start for C++ because readiness failed. The
performance release gate is **RED** and no ratio is valid.

The offline Net10-only 100k SEARCH/SORT gate passes at p50 `7.101 ms`, p95
`9.734 ms`, p99 `9.784 ms`; this is not a legacy comparison. Current evidence:
`artifacts/benchmarks/live-cpp-net10-20260811/`.

## Current benchmark continuation (2026-08-11, listener-only COM isolation)

Code/tool commit `f754c86c3` adds the explicit
`HMAILSERVER_COM_LOCAL_SERVER_ENABLED=false` benchmark switch. It preserves
the production default and prevents the existing installed Application AppID
from stopping a listener-only process with `0x80004015`. Disposable probes
verify SMTP `220`, IMAP `* OK`, and POP3 `+OK` on the configured loopback ports.

The live gate remains **RED**: the disposable SQL instance lacks Full-Text
Search for the `SEARCH TEXT needle` scenario, and the C++ target still lacks
POP3 readiness. No ratio or winner is valid. Default full Net10 is `2126
passed, 44 skipped, 0 failed`; focused registration coverage is `5/5`.
## Current authoritative continuation (2026-08-11, ambiguous full restore commit)

Code/test commit `55f252fb3` extends the ambiguous-commit acceptance through
the startup recovery gate. `EnsureNoPendingRecovery` rejects the preserved
manual-recovery journal before a subsequent restore can mutate SQL/Data and
the new Data target remains intact. This is not a process-kill/power-loss
drill. Default full Net10 remains `2126 passed, 46 skipped, 0 failed`.

Code/test commit `8ebace0de` adds
`RestoreExecutor_PreservesJournalWhenFullRestoreCommitOutcomeIsAmbiguous`.
The disposable SQL/Data test commits the real metadata and then returns an
error from the commit boundary. Net10 preserves the
`MetadataCommitStarted` recovery journal, new Data target, and rollback
artifact, requiring manual reconciliation. This is bounded ambiguous-commit
evidence; a process-kill/power-loss drill remains open. Focused restore
coverage is `20/20`; disposable opt-in is `55/55`; default full Net10 is
`2126 passed, 46 skipped, 0 failed`.

## Current authoritative continuation (2026-08-11, queued full restore)

Code/test commit `0d03adfac` extends the disposable acceptance boundary to
the real authenticated `BackupManager.StartBackup -> LoadBackup ->
StartRestore` chain. `BackupRestoreRoundTripIntegrationTests` creates a real
7z archive and raw `DataBackup` through `SevenZipBackupArchiveRuntime`, loads
the archive through `BackupManager`, and restores it with the real
`MetadataBackupRestoreExecutor` against an isolated SQL/Data target. It
verifies settings/domain replacement, message-file staging, and old-root
cleanup. Focused restore coverage is `19/19`; disposable SQL opt-in is
`54/54`; default full Net10 is `2126 passed, 45 skipped, 0 failed`.

This closes only the bounded queue/archive/restore execution path. Full
production payload-provider readback, crash/power-loss recovery, independent
SQL Server certification, service/COM lifecycle, SEC-18, migration/installer,
and the paired C++/.NET10 performance gate remain open and **RED**.

## Current authoritative continuation (2026-08-11, queued full restore)

Code/test commit `2564cc45b` proves the real authenticated restore composition
against a disposable LocalDB target. `BackupManager.StartRestore` dispatches
through `BackupTaskQueue` and `BackupTaskHostedService` into
`MetadataBackupRestoreExecutor`; the target is pre-populated with a stale
domain/public-folder graph and Data file, and the test verifies replacement,
public-folder cleanup, message/folder readback, and completion dispatch.

Legacy anchors are `BackupManager::StartRestore` and
`BackupExecuter::StartRestore`/`RestoreDataDirectory_` in
`source/Server/Common/Application/BackupManager.cpp` and
`BackupExecuter.cpp`. `BackupRestoreRoundTripIntegrationTests` passes `18/18`
with disposable SQL opt-in categories at `53/53`; default full Net10 is
`2125 passed, 44 skipped, 0 failed`.

The remaining restore blockers are real `StartBackup -> LoadBackup` round trip,
crash/power-loss and ambiguous-commit recovery evidence, service/COM lifecycle,
and independent SQL Server certification. Release remains **RED**.
## Current authoritative continuation (2026-08-11, global relayer host failover)

Code/test commit `50e6d843f` implements legacy global SMTP relayer host
failover for `RouteId == 0`. Legacy references are
`ServerTargetResolver::Resolve` and `GetFixedSMTPHostForDomain_`
(`source/Server/SMTP/ServerTargetResolver.cpp:38-116,170-237`) plus
`ExternalDelivery::ResolveRecipientServers_` and
`DeliverToSingleServer_` (`source/Server/SMTP/ExternalDelivery.cpp:58-107,
109-280,373-413`). Net10 splits non-empty `|`-separated global relayer hosts
in order, preserves shared port/security/authentication settings, advances on
transient early SMTP or transport failures, and stops on permanent failures.
After any RCPT recipient is accepted, same-run failover is suppressed to avoid
duplicate delivery.

Focused coverage is `34/34`; full Net10 is `2164 passed, 54 skipped, 0 failed`.
Only global relayer targets changed. Route/forced-route precedence, ordinary MX
resolution, COM identity, SQL schema, SMTP trust, and live reconfiguration are
outside this slice. DNS address ordering, legacy `MaxNumberOfMXHosts`, exact
per-recipient queue completion, and real SQL/socket/TLS/authentication evidence
remain open. Release status remains RED.
## Current authoritative continuation (2026-08-11, outbound TLS verification)

Code/test commit `a2be0c906` wires `VerifyRemoteSslCertificate` from the
existing `hm_settings` row into remote SMTP MX, route, forced-route, and
global-relayer targets. Legacy anchors are
`TCPConnection::AsyncHandshake` (`source/Server/Common/TCPIP/TCPConnection.cpp:308-350`),
`InterfaceSettings::put_VerifyRemoteSslCertificate`
(`source/Server/COM/InterfaceSettings.cpp:2244-2254`), and
`CertificateVerifier::VerifyCertificate_` / `OverrideResult_`
(`source/Server/Common/TCPIP/CertificateVerifier.cpp:18-45,125-171`). Net10
defaults missing/null SQL values to verification enabled, sets online
certificate revocation checking, preserves hostname validation, and retains
legacy certificate-error bypass for optional STARTTLS.

Focused coverage is `35/35`; full Net10 is `2165 passed, 54 skipped, 0 failed`.
The slice changes no COM identity, SQL schema, SMTP trust, or live
reconfiguration. Real invalid-certificate/revocation sockets and disposable
SQL/TLS acceptance remain environment-blocked. Release status remains RED.

## Current authoritative continuation (2026-08-11, null-MX parity)

Code/test commit `b39a17abf` preserves legacy null-MX rejection in ordinary
remote delivery. `DNSResolver::GetEmailServersRecursive_`
(`source/Server/Common/TCPIP/DNSResolver.cpp:208-260`) rejects an MX exchange
of `.` with preference `0`; Net10 now preserves that root exchange in
`SystemDnsMxResolver` and fails `RemoteSmtpEndpointResolver` with an
`IOException` instead of falling back to the domain. Focused coverage is
`40/40`; full Net10 is `2170 passed, 54 skipped, 0 failed`.

The slice leaves A/AAAA expansion/deduplication, implicit-MX fallback,
endpoint-level `MaxNumberOfMXHosts`, and fixed-relayer address expansion open.
No COM identity, SQL schema, SMTP trust, or live reconfiguration changed.
Real DNS/socket acceptance remains blocked; release remains RED.
## Current authoritative continuation (2026-08-11, normal-MX candidate ordering)

Code/test commit `d569a0780` preserves all ordered MX exchange hostnames for
ordinary remote delivery and applies the existing `MaxNumberOfMXHosts` setting
before handing candidates to the SMTP failover loop. Legacy anchors are
`ExternalDelivery::ResolveRecipientServers_`
(`source/Server/Common/../SMTP/ExternalDelivery.cpp:192-280`),
`DNSResolver::GetEmailServers` (`source/Server/Common/TCPIP/DNSResolver.cpp:170-330`),
and `SMTPConfiguration::GetMaxNumberOfMXHosts`. The setting is loaded from the
existing `hm_settings` row only for ordinary remote targets; routes and global
relayers are unchanged.

Focused coverage is `36/36`; full Net10 is `2166 passed, 54 skipped, 0 failed`.
The remaining legacy gap is A/AAAA expansion and deduplication, implicit-MX
fallback, and deterministic address-to-TLS-name separation. Real SQL/DNS/
socket acceptance remains blocked. Release status remains RED.

## Current authoritative continuation (2026-08-12, normal-MX addresses)

Code/test commit `1ffc564cb` implements address-level planning for ordinary
remote MX delivery. Legacy `DNSResolver::GetEmailServersRecursive_`
(`source/Server/Common/TCPIP/DNSResolver.cpp:170-330`) resolves every ordered
MX exchange to A/AAAA addresses, removes duplicate IPs, caps the flattened
list with `MaxNumberOfMXHosts`, and uses implicit domain A/AAAA addresses when
no MX exists. Net10 now preserves the original host for TLS/SNI and connects
through `ConnectionAddress`; null MX and failed/no-address resolution fail
closed. Focused coverage is `52/52`; full Net10 is `2184 passed, 54 skipped,
0 failed`.

CNAME target-name preservation and real DNS/socket acceptance remain open.
Global relayer, forced routes, COM identity, SQL schema, SMTP trust, and live
reconfiguration are unchanged; release remains RED.

## Current authoritative continuation (2026-08-11, global-relayer addresses)

Code/test commit `90146b45e` implements legacy address planning for global
relayers (`RouteId == 0`). `ExternalDelivery::ResolveRecipientServers_`
(`source/Server/SMTP/ExternalDelivery.cpp:192-280`) and
`DNSResolver::GetIpAddresses` (`source/Server/Common/TCPIP/DNSResolver.cpp:60-119`)
define host/address order, duplicate-IP removal, literal-IP bypass, and the
post-flattening `MaxNumberOfMXHosts` cap. Net10 retains the original hostname
as `RemoteSmtpEndpoint.Host` for TLS/SNI and uses `ConnectionAddress` for TCP.
Focused coverage is `46/46`; full Net10 is `2177 passed, 54 skipped, 0 failed`.

Normal-MX address expansion, implicit-MX fallback, real DNS/socket acceptance,
and C++/.NET paired performance remain open. Forced routes and COM identity
remain unchanged; release status remains RED.

## Current authoritative continuation (2026-08-12, normal-MX CNAME parity)

Code/test commit `bf6018662` implements the bounded legacy no-MX CNAME
behavior. The legacy reference is
`DNSResolver::GetEmailServersRecursive_`
(`hmailserver/source/Server/Common/TCPIP/DNSResolver.cpp:208-260`): when MX
is empty it queries `DNS_TYPE_CNAME`, follows one target recursively, and uses
implicit A/AAAA records for the original name when there is no single CNAME.
Net10 adds `IDnsCnameResolver`/`DnsCnameRecord`, raw CNAME parsing in
`SystemDnsMxResolver`, bounded cycle/depth handling in
`RemoteSmtpEndpointResolver`, and preserves the canonical target as the
implicit SMTP Host/TLS name while `ConnectionAddress` carries the resolved IP.

Focused resolver/parser coverage is `42/42`; full Net10 is `2193 passed, 54
skipped, 0 failed`. Tests cover one CNAME, zero/multiple CNAME fallback,
CNAME lookup failure fallback, cycles, parser TTL/target preservation, and
implicit target address resolution. Real DNS/socket/TLS/SNI evidence remains
environment-blocked. The security review also leaves the shared outbound
egress/SSRF policy, DNS response validation, and aggregate DNS deadline open.
No COM identity, SQL schema, SMTP trust, route behavior, or live
reconfiguration changed. Release remains RED.

Next independent work: approved disposable real DNS/socket/TLS acceptance;
shared outbound egress/SSRF policy hardening; registry-isolated or separate-VM
C++ listener execution; and the restore protocol drain/reinitialize contract.

## Current authoritative continuation (2026-08-12, SMTP self-connect parity)

Code/test commit `9e1bbb53b` closes the bounded legacy local-listening-port
guard for ordinary DNS-derived SMTP delivery. Legacy behavior is anchored by
`TCPConnection::StartAsyncConnect_` and `LocalIPAddresses::IsLocalPort`
(`hmailserver/source/Server/Common/TCPIP/TCPConnection.cpp:75` and
`hmailserver/source/Server/Common/LocalIPAddresses.cpp:101`): the server's own
active listening endpoint is rejected, but loopback to an unused port remains
allowed. Net10 checks active TCP listeners before creating the socket only for
normal MX/implicit address candidates; explicit routes and global relayers are
not broadened. Wildcard listeners are matched only to actual local addresses,
and IPv4-mapped IPv6 targets are normalized.

Focused coverage is `65/65`; full Net10 is `2202 passed, 54 skipped, 0 failed`.
Guard denials are represented as transient endpoint results so sequential MX
failover and queue handling remain deterministic. The shared outbound
egress/SSRF policy, DNS response validation, real DNS/socket/TLS evidence, and
paired C++/.NET performance gate remain open. Release remains **RED**.

## Current authoritative continuation (2026-08-12, explicit relay self-connect)

Code/test commit `b66f00e95` extends the local-listener guard to explicit IP
route targets and global-relayer candidates whose addresses are already known.
Legacy anchors are `TCPConnection::StartAsyncConnect_` and
`LocalIPAddresses::IsLocalPort` (`hmailserver/source/Server/Common/TCPIP/
TCPConnection.cpp:130-160`, `LocalIPAddresses.cpp:108-133`). Private and
link-local destinations remain allowed, hostname routes retain runtime DNS
semantics, and route/relayer candidate order is unchanged.

Focused coverage is `70/70`; full Net10 is `2207 passed, 54 skipped, 0 failed`.
Hostname-route resolution/failover, hMailServer-owned listener discovery,
real DNS/socket/TLS acceptance, DNS response validation, the shared SMTP
egress/SSRF decision, and paired C++/.NET performance remain open. Release is
still **RED**.

## Current authoritative continuation (2026-08-12, fixed-route host planning)

Code/test commit `622d6296c` expands configured non-global route targets using
legacy pipe-separated host order. Each hostname is resolved through the
existing address resolver, literal IPs bypass DNS, duplicate addresses are
removed, the cap is applied after flattening when present on the target, and
the original hostname remains the TLS/SNI name while `ConnectionAddress` is the
socket destination. Partial host-resolution failure does not discard later
usable candidates.

Legacy anchors are `ExternalDelivery::ResolveRecipientServers_` and
`TCPConnection::StartAsyncConnect_` (`hmailserver/source/Server/SMTP/
ExternalDelivery.cpp:195-330`, `Server/Common/TCPIP/TCPConnection.cpp:130-160`).
Focused coverage is `73/73`; full Net10 is `2210 passed, 54 skipped, 0 failed`.
Route `MaxNumberOfMXHosts` SQL propagation, global-relayer partial failure,
hMailServer listener ownership, live DNS/socket/TLS, shared SMTP SSRF policy,
and paired C++/.NET performance remain open. Release remains **RED**.

## Current authoritative continuation (2026-08-12, route MX cap propagation)

Code/test commit `c519f6e87` closes the SQL-to-target propagation gap for
`MaxNumberOfMXHosts`. `SqlServerDeliveryTargetResolver` now loads the cached
setting for matched routes and forced routes and places it on
`DeliveryTarget`; the existing `RemoteSmtpEndpointResolver` applies it after
route host/address flattening. This matches legacy
`ExternalDelivery::ResolveRecipientServers_` and preserves zero as no cap.

Focused coverage is `51/51`; full Net10 is `2212 passed, 54 skipped, 0 failed`.
Global-relayer partial DNS fallback, hMailServer listener ownership, DNS
response validation, live DNS/socket/TLS, broad SMTP egress/SSRF, and paired
C++/.NET performance remain open. Release remains **RED**.
