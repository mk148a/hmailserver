# CODEX_HANDOFF.md

## Current Authoritative Continuation (2026-08-21, IMAP ACL persistence parity)

Code/test commit `a824f4d92` implements authenticated
`Settings.IMAPACLEnabled` persistence through
`SqlServerSettingsAdministrationStore.UpdateImapAclEnabledAsync`. It updates
only the existing `enableimapacl` row, requires the existing authenticated
server-administrator boundary and authorization lease, publishes the snapshot
after a successful one-row update, and keeps direct activation fallback
behavior. Focused COM/SQL coverage is `218 passed, 0 skipped, 0 failed`;
disposable LocalDB SQL integration passed; full Net10 Debug is `2533 passed,
10 skipped, 0 failed` (`2543` total).

Legacy references are `InterfaceSettings::get/put_IMAPACLEnabled`
(`source/Server/COM/InterfaceSettings.cpp:1463-1490`) and
`IMAPConfiguration::Get/SetUseIMAPACL`
(`source/Server/IMAP/IMAPConfiguration.cpp:90-98`). No installed COM
identity, direct activation boundary, SMTP trust behavior, ACL rights
enforcement, or live IMAP capability was changed. The next slice is
IMAPSASLPlainEnabled persistence with a security review. Migration/installer,
SEC-18, paired C++ performance, and 24-hour soak remain open; release is
`RED`; no push was performed.

## Current Authoritative Continuation (2026-08-21, IMAP IDLE persistence parity)

Code/test commit `e27385413` implements authenticated
`Settings.IMAPIdleEnabled` persistence through
`SqlServerSettingsAdministrationStore.UpdateImapIdleEnabledAsync`. It updates
only the existing `enableimapidle` row, requires the existing authenticated
server-administrator boundary, publishes the snapshot after a successful
one-row update, and keeps direct activation fallback behavior. Focused COM/SQL
coverage is `216 passed, 0 skipped, 0 failed`; disposable LocalDB SQL
integration passed; full Net10 Debug is `2530 passed, 10 skipped, 0 failed`
(`2540` total).

Legacy references are `InterfaceSettings::get/put_IMAPIdleEnabled`
(`source/Server/COM/InterfaceSettings.cpp:1432-1458`) and
`IMAPConfiguration::Get/SetUseIMAPIdle`
(`source/Server/IMAP/IMAPConfiguration.cpp:78-86`). No installed COM
identity, direct activation boundary, SMTP trust behavior, or live IMAP
capability was changed. The next slice is IMAPACLEnabled persistence with a
security review. Migration/installer, SEC-18, paired C++ performance, and
24-hour soak remain open; release is `RED`; no push was performed.

## Current Authoritative Continuation (2026-08-21, IMAP QUOTA persistence parity)

Code/test commit `36c8ffa86` implements authenticated
`Settings.IMAPQuotaEnabled` persistence through
`SqlServerSettingsAdministrationStore.UpdateImapQuotaEnabledAsync`. It updates
only the existing `enableimapquota` row, requires the existing authenticated
server-administrator boundary, publishes the snapshot after a successful
one-row update, and keeps direct activation fallback behavior. Focused COM/SQL
coverage is `214 passed, 0 skipped, 0 failed`; disposable LocalDB SQL
integration passed; full Net10 Debug is `2527 passed, 10 skipped, 0 failed`
(`2537` total).

Legacy references are `InterfaceSettings::get/put_IMAPQuotaEnabled`
(`source/Server/COM/InterfaceSettings.cpp:1400-1426`) and
`IMAPConfiguration::Get/SetUseIMAPQuota`
(`source/Server/IMAP/IMAPConfiguration.cpp:75-83`). No installed COM
identity, direct activation boundary, SMTP trust behavior, or live IMAP
capability was changed. The next slice is IMAPIdleEnabled persistence parity.
Migration/installer, SEC-18, paired C++ performance, and 24-hour soak remain
open; release is `RED`; no push was performed.

## Current Authoritative Continuation (2026-08-21, IMAP SORT persistence parity)

Code/test commit `73d8e9e13` implements authenticated
`Settings.IMAPSortEnabled` persistence through
`SqlServerSettingsAdministrationStore.UpdateImapSortEnabledAsync`. It updates
only the existing `enableimapsort` row, requires the existing authenticated
server-administrator boundary, publishes the snapshot after a successful
one-row update, and keeps direct activation fallback behavior. Focused COM/SQL
coverage is `212 passed, 0 skipped, 0 failed`; disposable LocalDB SQL
integration passed; full Net10 Debug is `2524 passed, 10 skipped, 0 failed`
(`2534` total).

Legacy references are `InterfaceSettings::get/put_IMAPSortEnabled`
(`source/Server/COM/InterfaceSettings.cpp:1368-1394`) and
`IMAPConfiguration::Get/SetUseIMAPSort`
(`source/Server/IMAP/IMAPConfiguration.cpp:102-110`). No installed COM
identity, direct activation boundary, SMTP trust behavior, or live IMAP
capability was changed. The next slice is IMAPQuotaEnabled persistence parity.
Migration/installer, SEC-18, paired C++ performance, and 24-hour soak remain
open; release is `RED`; no push was performed.

## Current Authoritative Continuation (2026-08-21, SMTP delivery bind persistence parity)

Code/test commit `10339d49a` implements authenticated
`Settings.SMTPDeliveryBindToIP` persistence through
`SqlServerSettingsAdministrationStore.UpdateSmtpDeliveryBindToIpAsync`.
It updates only the existing `smtpdeliverybindtoip` row, requires the existing
authenticated server-administrator boundary, publishes the snapshot after a
successful one-row update, and keeps direct activation fallback behavior.
Focused COM/SQL coverage is `210 passed, 0 skipped, 0 failed`; disposable
LocalDB SQL integration passed; full Net10 Debug is `2521 passed, 10 skipped,
0 failed` (`2531` total).

Legacy references are `InterfaceSettings::get/put_SMTPDeliveryBindToIP`
(`source/Server/COM/InterfaceSettings.cpp:1336-1363`) and
`SMTPConfiguration::Get/SetSMTPDeliveryBindToIP`
(`source/Server/SMTP/SMTPConfiguration.cpp:126-134`). No installed COM
identity, direct activation boundary, SMTP trust behavior, or live outbound
socket was changed. The next slice is production-hosted SMTP
enable/disable/timing acceptance. Migration/installer, SEC-18, paired C++
performance, and 24-hour soak remain open; release is `RED`; no push was
performed.

## Current Authoritative Continuation (2026-08-21, ServiceIMAP persistence parity)

Code/test commit `5ba1ceb68` implements authenticated `Settings.ServiceIMAP`
persistence through `SqlServerSettingsAdministrationStore.UpdateServiceImapAsync`.
It updates only the existing `protocolimap` row, requires the existing
authenticated server-administrator boundary, publishes the snapshot after a
successful one-row update, and keeps direct activation fallback behavior.
Focused COM/SQL coverage is `208 passed, 0 skipped, 0 failed`; disposable
LocalDB SQL integration passed; full Net10 Debug is `2518 passed, 10 skipped,
0 failed` (`2528` total).

Legacy references are `InterfaceSettings::get/put_ServiceIMAP`
(`source/Server/COM/InterfaceSettings.cpp:862-890`) and
`Configuration::GetUseIMAP/SetUseIMAP`
(`source/Server/Common/Application/Configuration.cpp:200-208`). No installed
COM identity, direct activation boundary, SMTP trust behavior, or live IMAP
listener was changed. The next slice is production-hosted SMTP
enable/disable/timing acceptance. Migration/installer, SEC-18, paired C++
performance, and 24-hour soak remain open; release is `RED`; no push was
performed.

## Current Authoritative Continuation (2026-08-21, ServicePOP3 persistence parity)

Code/test commit `2698fd964` implements authenticated `Settings.ServicePOP3`
persistence through `SqlServerSettingsAdministrationStore.UpdateServicePop3Async`.
It updates only the existing `protocolpop3` row, requires the existing
authenticated server-administrator boundary, publishes the snapshot after a
successful one-row update, and keeps direct activation fallback behavior.
Focused COM/SQL coverage is `206 passed, 0 skipped, 0 failed`; disposable
LocalDB SQL integration passed; full Net10 Debug is `2515 passed, 10 skipped,
0 failed` (`2525` total).

Legacy references are `InterfaceSettings::get/put_ServicePOP3`
(`source/Server/COM/InterfaceSettings.cpp:821-860`) and
`Configuration::GetUsePOP3/SetUsePOP3`
(`source/Server/Common/Application/Configuration.cpp:187-195`). No installed
COM identity, direct activation boundary, SMTP trust behavior, or live POP3
listener was changed. The next slice is ServiceIMAP persistence parity.
Migration/installer, registered COM/DCOM, SEC-18, paired C++ performance, and
24-hour soak remain open; release is `RED`; no push was performed.

## Current Authoritative Continuation (2026-08-21, ServiceSMTP persistence parity)

Code/test commit `43d4b9abf` implements authenticated `Settings.ServiceSMTP`
persistence through `SqlServerSettingsAdministrationStore.UpdateServiceSmtpAsync`.
It updates only the existing `protocolsmtp` row, requires the existing
authenticated server-administrator boundary, publishes the snapshot after a
successful one-row update, and keeps direct activation fallback behavior.
Focused COM coverage is `150 passed, 0 skipped, 0 failed`; SQL unit coverage
is `53 passed, 0 skipped, 0 failed`; disposable LocalDB SQL integration is
`1 passed, 0 skipped, 0 failed`; full Net10 Debug is `2512 passed, 10 skipped,
0 failed` (`2522` total).

Legacy references are `InterfaceSettings::get/put_ServiceSMTP`
(`source/Server/COM/InterfaceSettings.cpp:781-819`) and
`Configuration::GetUseSMTP/SetUseSMTP`
(`source/Server/Common/Application/Configuration.cpp:175-184`). No installed
COM identity, direct activation boundary, SMTP trust behavior, or live listener
was changed. Live SMTP enable/disable/timing acceptance is the next slice.
Migration/installer, registered COM/DCOM, SEC-18, paired C++ performance, and
24-hour soak remain open; release is `RED`; no push was performed.

## Current Authoritative Continuation (2026-08-21, domain quota setter/save parity)

Code/test commit `8a5bcb5ad` adds focused COM and disposable SQL coverage for
authenticated domain `MaxSize` and `MaxAccountSize` staging and Save, while retaining the authenticated
`Add()` -> `Save()` ->
`GreyListingWhiteAddress.Delete()` path after
the SPF, MX checks, SpamAssassin, scanner endpoint, maximum-size, DKIM
verification, greylisting bypass, CheckHostInHelo, and AddHeader pairs.
Focused domain COM coverage is `17 passed, 0 skipped, 0 failed`; related
disposable domain SQL store integration is `3 passed, 0 skipped, 0 failed`; the
disposable LocalDB/Data full suite is `2509 passed, 10 skipped, 0 failed` (`2519` total).
Direct activation, failed reauthentication, missing-row failure, retained
object snapshots, and existing COM identity boundaries remain covered.

Legacy references for this slice are `InterfaceDomain::get/put_MaxSize`
(`source/Server/COM/InterfaceDomain.cpp:518-554`) and
`InterfaceDomain::get/put_MaxAccountSize`
(`source/Server/COM/InterfaceDomain.cpp:1008-1044`), plus the matching
`Domain` setters in `source/Server/Common/BO/Domain.cpp`.
`DomainsComContractTests` proves Save publication and failed-save snapshot
retention; `SqlServerDomainAdministrationStoreIntegrationTests` proves
`domainmaxsize` and `domainmaxaccountsize` update/readback in disposable SQL.
Legacy references are `InterfaceGreyListingWhiteAddresses::Add/DeleteByDBID`
(`source/Server/COM/InterfaceGreyListingWhiteAddresses.cpp:85-93,162-183`),
`InterfaceGreyListingWhiteAddress::Save/Delete`
(`source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:9-138`), and
`PersistentGreyListingWhiteAddress`
(`source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:26-104`).
Domain anchors are `InterfaceDomain::get/put_AntiSpamEnableGreylisting`
(`source/Server/COM/InterfaceDomain.cpp:634-671`) and
`Domain::Get/SetASUseGreyListing` (`source/Server/Common/BO/Domain.cpp:207-217`).
The next bounded slice is production-hosted SMTP enable/disable/timing
acceptance on an isolated production-like host; it remains environment-gated.
Triplet cleanup, production-hosted SMTP socket acceptance, migration/installer,
SEC-18, paired C++ performance, and soak remain separate. Release remains
`RED`.
the migration/installer drill remains environment-gated because `Get-VM` is
access-denied and the running MSSQLSERVER instance is not an approved
disposable target. Live anti-spam reconfiguration is still unproven. Release
remains `RED`; no push was performed.

## Historical Authoritative Continuation (2026-08-20, transaction-scoped group/member restore)

Code/test commit `b834892dd` wires parsed legacy `Groups/GroupMembers` into the
existing SQL metadata transaction. `RestoreGroupsAsync` resolves member
addresses against newly inserted accounts, persists members using the
generated group IDs, and feeds those restored IDs to public-folder ACL holder
resolution. Focused writer coverage is `10 passed, 0 skipped, 0 failed`; the
populated public-folder restore integration is `1 passed, 0 skipped, 0 failed`;
the disposable LocalDB/Data full suite is `2442 passed, 10 skipped, 0 failed`
(`2452` total).

Legacy references are `GroupMembers::PreSaveObject/PostStoreObject`
(`source/Server/Common/BO/GroupMembers.cpp:57-84`) and
`IMAPConfiguration::XMLLoad` (`source/Server/IMAP/IMAPConfiguration.cpp:225-248`).
The outer SQL transaction rolls back the inserted rows. Target group
replacement/merge and settings-only group restore without recreated account
IDs remain the next bounded slice. Release remains `RED`; no push was
performed.

## Historical Authoritative Continuation (2026-08-20, strict group/member restore parser/model)

Code/test commit `28b6d6cf4` adds `RestoreGroupEntry` and strict
`BackupArchiveXmlSnapshotParser.ParseGroupEntries` for legacy `Groups` and
`GroupMembers` metadata. It preserves XML names/order and rejects duplicate
groups, missing names, repeated containers, and unexpected children. Focused
coverage is `21 passed, 0 skipped, 0 failed`; the disposable LocalDB/Data full
suite is `2439 passed, 10 skipped, 0 failed` (`2449` total).

Legacy references are `IMAPConfiguration::XMLStore/XMLLoad`
(`source/Server/IMAP/IMAPConfiguration.cpp:225-248`), `Group::XMLStore` and
`XMLLoadSubItems` (`source/Server/Common/BO/Group.cpp:55-79`), and
`GroupMembers::PostStoreObject/PreSaveObject`
(`source/Server/Common/BO/GroupMembers.cpp:57-84`). The slice is intentionally
parser/model-only; SQL insertion, ACL holder remapping, and restore rollback
remain the next bounded slice. Release remains `RED`; no push was performed.

## Historical Authoritative Continuation (2026-08-20, group/member backup capture)

Code/test commit `7213e522d` adds legacy `Groups/GroupMembers` backup XML
capture. `BackupXmlPayloadRuntime` reads the configured group and member
stores, resolves member account addresses, and `SevenZipBackupArchiveRuntime`
writes the group collection after public folders. Focused coverage is `56
passed, 1 skipped, 0 failed`; the disposable LocalDB/Data full suite is
`2437 passed, 10 skipped, 0 failed` (`2447` total).

Legacy references are `IMAPConfiguration::XMLStore/XMLLoad`
(`source/Server/IMAP/IMAPConfiguration.cpp:225-248`), `Group::XMLStore` and
`XMLLoadSubItems` (`source/Server/Common/BO/Group.cpp:55-79`), and
`GroupMembers::PostStoreObject/PreSaveObject`
(`source/Server/Common/BO/GroupMembers.cpp:57-84`). The capture path rejects
unresolved member accounts. It intentionally does not restore groups/members
yet; the next slice is transaction-scoped group/member restore with ACL holder
resolution against restored IDs and mid-batch rollback. Release remains
`RED`; no push was performed.

## Current Authoritative Continuation (2026-08-20, legacy restore UID allocation)

Code/test commit `4843c59b8` completes the legacy restore defaults and
owner-scoped UID-zero allocation. Restore resets retry count to `0`, adds
`ImapMessageFlags.Recent` (`32`), allocates a new folder UID only when the
archived UID is zero, and returns the effective UID. Focused SQL/store coverage
is `9 unit passed, 2 integration passed, 0 failed`; the full disposable
LocalDB/Data suite is `2434 passed, 10 skipped, 0 failed` (`2444` total).

Legacy anchors are `Messages::Refresh` (`source/Server/Common/BO/Messages.cpp:165-197`),
`Message::XMLStore/XMLLoad` (`source/Server/Common/BO/Message.cpp:200-230`),
`PersistentMessage::AddObject` (`source/Server/Common/Persistence/PersistentMessage.cpp:574-646`),
and `BackupExecuter::BackupDataDirectory_/RestoreDataDirectory_`
(`source/Server/Common/Application/BackupExecuter.cpp:196-216, 372-386`).
Net10 now proves all-state nested-file round trip, matches the legacy retry/
flag defaults, and uses the SQL equivalent of
`PersistentIMAPFolder::GetUniqueMessageID` for zero-UID mailbox messages. No
COM/IDL identity, SMTP trust, shared IMAP/COM read behavior, production
service, production SQL/Data, or installed registration changed.

Next slice: implement target-preexisting group dependency parity with explicit
unresolved-group and restore rollback coverage. Restore/migration, COM/DCOM,
SEC-18, paired C++/.NET performance, and soak remain open; release remains
**RED** and no push was performed.

## Current Authoritative Continuation (2026-08-20, ACL restore storage foundation)

HEAD is `6ec5d23d7`. The bounded code/test slice adds
`IImapFolderPermissionAdministrationRestoreStore`, exposes it from the SQL
backup transaction, and implements strict public-folder ACL insertion in
`SqlServerImapFolderAdministrationStore`. Legacy references are
`IMAPFolder::XMLLoadSubItems` (`source/Server/Common/BO/IMAPFolder.cpp:161-179`),
`ACLPermission::XMLLoad` (`source/Server/Common/BO/ACLPermission.cpp:230-264`),
and `PersistentACLPermission::{Validate,SaveObject}`
(`source/Server/Common/Persistence/PersistentACLPermission.cpp:77-145`).

Focused ACL SQL coverage is `16 passed, 0 failed`; restore/parser/transaction
coverage is `44 passed, 21 skipped, 0 failed`; disposable full Net10 is
`2414 passed, 10 skipped, 0 failed`. The parser still rejects ACL-bearing
archives, so no ACL restore parity claim is made. Security review is `NO-GO`
until holder-name resolution, public-folder graph ownership, malformed input,
and rollback-after-mid-batch-failure are implemented and tested.

Next slice: strict `<Permissions>` parser/model with holder-name validation,
without wiring restore execution. No push was performed; release remains
**RED**.

## Current Authoritative Continuation (2026-08-20, restore retry metadata)

Current HEAD is `1d38c85a2`. The latest disposable LocalDB/Data full Net10
suite is `2411 passed, 10 skipped, 0 failed`; focused queued backup failure
coverage is `1 passed, 0 failed`, and backup archive coverage is `51 passed, 1
skipped, 0 failed`. The ACL revalidation benchmark completed
`80/80` with p50/p95/p99 `0.499/0.856/1.317 ms` in
`artifacts/benchmarks/acl-revalidation-localdb/`; this is Net10-only evidence,
not a C++ performance comparison.

Legacy `BackupExecuter::StartBackup` and `BackupDataDirectory_` define raw
message staging. Net10 raw `BODomains|BOMessages` staging is already complete
in `50d8cefc3`; the current test slice covers the remaining backup option
metadata combinations. `FULL` now matches the actual C++ parser: it emits
`ENVELOPE` and `BODYSTRUCTURE`, not a raw `BODY[]` response, and does not mark
messages seen.

Queued success and failure paths now prove event ordering against the real
archive runtime on disposable filesystems. Code/test commit `1d38c85a2` also
preserves legacy message retry metadata: C++ `Message::XMLLoad`
(`hmailserver/source/Server/Common/BO/Message.cpp`) reads `NoOfRetries`, and
Net10 `BackupArchiveXmlSnapshotParser.ParseFolder` binds it through
`SqlServerMessageAdministrationStore.InsertMessageForRestoreAsync` as
`@CurrentNumberOfTries`. Focused parser/store coverage is `23/23`; disposable
restore coverage is `21/21`, including SQL readback of `9` retries. Next slice:
broaden populated restore semantic-equivalence and crash/recovery evidence.
Release remains **RED**: paired C++/.NET load, out-of-process COM/DCOM,
migration/installer rollback, SEC-18, and 24-hour soak are still unproven. No
push was performed.

## Current Authoritative Continuation (2026-08-20, reversible ACL read-only state)

Code/test commit `778cadfcd` makes selected-mailbox ACL writeability reversible
after a later grant while preserving EXAMINE read-only state through a
`RequestedReadOnly` marker. Focused IMAP/SQL coverage is `52/52`; full Debug is
`2346 passed, 58 skipped, 0 failed`. The guarded benchmark tool remains in
`73af63531`; its SQL mode is still unrun because no qualifying LocalDB/Data
fixture exists.

Parity anchors: legacy `IMAPConnection::CheckPermission` and
`CheckFolderPermissions` at `hmailserver/source/Server/IMAP/IMAPConnection.cpp:875-921`,
with handler-specific `WriteSeen`, `WriteDeleted`, `Insert`, and `Expunge`
checks. The remaining ACL production gap is individual-right enforcement at
those handler boundaries. Release remains **RED**. No push was performed.

Next slice: add handler-level rights tests and enforcement for STORE/APPEND/
COPY/EXPUNGE. The disposable VM,
LocalDB/Data, COM/DCOM, SEC-18, paired C++ performance, and soak gates remain
environment-blocked or unproven.

## Current Authoritative Continuation (2026-08-20, ACL revalidation query bound)

Legacy `IMAPConnection::CheckPermission` and `CheckFolderPermissions` in
`hmailserver/source/Server/IMAP/IMAPConnection.cpp:875-921` re-resolve ACL
permission at command boundaries. Net10
`SqlServerImapMailboxStore.RevalidateSelectedMailboxAsync` now uses the
selected folder ID and `ResolveAccessAsync` directly, preserving the current
selection counters and avoiding the full mailbox-counter query on every
command. Focused coverage is `52/52`; full Debug is `2341 passed, 58 skipped,
0 failed`.

The change is a bounded SQL-cost reduction, not live SQL benchmark evidence.
Next: run this path against an approved disposable SQL/Data fixture, then
complete the disposable VM migration/rollback prerequisite. Paired C++/.NET
performance, out-of-process COM, SEC-18, and soak remain open; release is
**RED**. No push was performed.

## Current Authoritative Continuation (2026-08-20, IDLE parity verified)

Read-only legacy inspection confirms `IMAPCommandIdle::ExecuteCommand` only
starts IDLE. `IMAPConnection::AnswerCommand` consumes the next client command
via `EndIdleMode_()` before invoking a command handler; ACL checks occur in the
actual command paths through `CheckPermission`/`CheckFolderPermissions`.
Net10 `HandleIdleAsync` therefore does not need an asynchronous ACL disconnect
to preserve legacy behavior.

Next slice: measure the per-command SQL ACL revalidation cost, then provision
and verify a disposable VM before migration/rollback. `Get-VM` currently
returns no disposable VM; host SQL is not proven disposable and was not used.
Release remains **RED**.

## Current Authoritative Continuation (2026-08-20, IMAP ACL command audit)

Test-only commit `17fae65c1` covers selected-folder ACL command boundaries:
SEARCH denies after read revocation without tracker publication, COPY/MOVE
denies after source revocation, and COPY/MOVE denies a read-only destination.
Focused coverage is `49/49`; full Debug is `2341 passed, 58 skipped, 0 failed`.
No production behavior or COM identity changed.

Next slice: IDLE-time unsolicited ACL revocation and inherited group-membership
propagation, followed by measuring the per-command SQL ACL lookup. Live
SQL/Data, migration/rollback, cross-process COM, paired C++ performance, and
soak remain open; release is **RED**.

## Current Authoritative Continuation (2026-08-20, live ACL revalidation)

Code/test commit `61cb3368c` adds per-command selected-mailbox ACL
revalidation for SQL-backed IMAP sessions. It closes the specific external-ACL
revocation gap against legacy `IMAPConnection::CheckPermission`: read
revocation clears selection/recent state and write revocation changes the
selection to read-only without requiring a tracker publication. Focused
coverage is `76/76`; full Debug is `2338 passed, 58 skipped, 0 failed`.

Residual risk is explicit: the SQL lookup cost is unbenchmarked, COPY/MOVE
source/destination checks need a handler-level audit, and IDLE-time unsolicited
revocation and inherited group membership remain open. Live SQL/Data,
migration/rollback, cross-process COM, paired C++ performance, and soak remain
open; release is **RED**.

Next slice: audit the selected-folder read commands and COPY/MOVE source and
destination authorization, then establish a measurable ACL lookup threshold.

## Current Authoritative Continuation (2026-08-20, tracker concurrency boundaries)

Test-only commit `c07c386ac` records 128 concurrent ACL publications for one
folder as lossless, keeps ACL and folder-tree generations in separate
namespaces, and confirms latest-only folder snapshot retention. Focused
tracker/session coverage is `75/75`; full Debug is `2337 passed, 58 skipped, 0
failed`. No production behavior or COM identity changed.

Next production slice: live authorization for selected-folder reads and
COPY/MOVE source/destination, or an explicitly approved external-SQL sync
design. Direct SQL changes, inherited group membership, IDLE-time revocation,
live SQL/Data, migration/rollback, cross-process COM, paired C++ performance,
and soak remain open; release is **RED**.

## Historical completed slice (2026-08-20, ACL revocation signal)

Code/test commit `bce828b9f` completes the bounded public IMAP ACL revocation
signal/session invalidation slice. Legacy anchors are `ACLManager::SetACL`,
`PersistentACLPermission::{SaveObject,DeleteObject}`,
`IMAPConnection::CheckPermission`, `IMAPCommandSelect`,
`IMAPCommandStore`, `IMAPStore`, `IMAPCommandSetAcl`, and
`IMAPCommandDeleteAcl`. Net10 publishes a folder-scoped ACL generation after
successful COM and SQL-backed ACL persistence. `ImapSession` revalidates a
selected mailbox before command dispatch after a generation change; no-read
clears selection/recent state and no-write changes the selection to read-only.
Failed persistence does not publish. COM identities, vtable/DISPID/ProgID,
authenticated Settings boundary, and direct activation denial are unchanged.

Focused coverage is `108/108`; full Debug is `2335 passed, 58 skipped, 0
failed`. The signal is process-local and only covers published Net10 mutation
paths. Direct external SQL changes, inherited group membership changes, IDLE
unsolicited cancellation, cross-process COM, live SQL/Data, migration/rollback,
paired C++ performance, and soak gates remain open. Release is **RED**.

Next slice: bound tracker namespace, generation ordering/retention, and
concurrency/soak tests. Separately, the isolated VM remains at the disposable
Administrator password prompt; no guest service, SQL/Data, COM, or installer
mutation has run.

## Current Authoritative Continuation (2026-08-20, stale IMAP parent parity)

Code/test commit `db9d690e8` adds focused parity tests for the legacy stale
child collection behavior. After a retained child collection's parent is
deleted, `IMAPFolders.Add` forwards the old numeric parent ID and the orphan
row is hidden from a fresh root collection. `InsertFolderSql` remains
unvalidated for parent existence/account ownership, matching legacy
`InterfaceIMAPFolders::Add` and `PersistentIMAPFolder::SaveObject`; no
production behavior or COM identity changed. Focused coverage is `44/44`; full
Debug is `2333 passed, 58 skipped, 0 failed`.

This is a documented integrity risk, not a release gate pass. A strict SQL
guard would intentionally diverge from legacy behavior and must be a separately
approved compatibility/security decision. Next slice: public ACL revocation
and selected-session invalidation, with legacy ACL anchors and negative tests.
Migration/rollback, live SQL/Data, registered/out-of-process COM, paired C++
performance, and soak remain open; release is **RED**.

## Current Authoritative Continuation (2026-08-20, public reauthentication and rename)

Code/test commit `2c7147b6b` closes the bounded public-folder reauthentication
and session-rename gap. `Settings.PublicFolders` rechecks live server-admin
authentication before returning a fresh adapter after failed reauthentication;
retained collection/item reads remain compatible with legacy C++ behavior.
Public account-0 rename upserts refresh selected IMAP mailbox names using the
selected mailbox owner. Installed COM IID/vtable/DISPID/ProgID/class identity
and direct activation boundaries are unchanged. Focused coverage is `181/181`;
full Debug is `2331 passed, 58 skipped, 0 failed`.

Legacy anchors are `COMAuthentication::Authenticate`,
`InterfaceSettings::get_PublicFolders`, `InterfaceIMAPFolders` read methods,
`InterfaceIMAPFolder` read methods, `IMAPConnection::SetCurrentFolder`, and
`PersistentIMAPFolder::SaveObject`. Public ACL revocation, stale-parent/account
insert scope, account-wide deletion, tracker ordering/retention, live SQL/Data,
registered service/out-of-process COM, migration/rollback, paired C++/.NET
performance, and soak remain open; release is **RED**.

Next slice: enforce stale child collection and parent-account ownership at the
IMAP folder insert/SQL boundary, with negative and failure-path tests.

## Current Authoritative Continuation (2026-08-20, public IMAP folder mutation)

Code/test commit `4d6ca8b50` completes the bounded public-folder COM mutation
slice. `Settings.PublicFolders` now uses the shared account-0 state-backed
adapter; root and nested `Add`, `Save`, `DeleteByDBID`, and item `Delete` keep
legacy ownership, parent filtering, public auto-subscription, Inbox behavior,
failure snapshot retention, and live authenticated lease checks. The installed
IMAPFolders/IMAPFolder IID, vtable, DISPID, ProgID, class identity, and direct
activation boundaries are unchanged. Focused public-folder, Settings, and SQL
tests pass `151/151`; full Debug passes `2328`, skips `58`, and has `0` failures.

Legacy anchors are `InterfaceIMAPFolders::{Add,DeleteByDBID}` and
`InterfaceIMAPFolder::{Save,Delete}` in `source/Server/COM`, with persistence
in `PersistentIMAPFolder::{SaveObject,DeleteObject}`. No SMTP trust, live
reconfiguration, SQL/Data, registration, service, or protocol behavior was
changed.

Security review remains open for retained public-folder read reauthentication,
public ACL revocation, public rename session refresh, and stale-parent insert
scope. Live SQL/Data, registered service/out-of-process COM, migration/rollback,
paired C++/.NET performance, and soak gates remain unproven; release is
**RED**.

Next slice: close retained public-folder read reauthentication and public
rename invalidation with focused negative/session tests, then enforce stale
parent/account scope at the SQL insert boundary.

## Current Authoritative Continuation (2026-08-20, isolated Hyper-V guest execution)

Code/test commit `279609c07` adds guarded provisioning, inventory, rollback,
and focused static tests for a non-production Hyper-V VM. The official
Microsoft Windows Server 2025 Evaluation x64 ISO is verified at
`8,152,356,864` bytes with SHA-256
`7B052573BA7894C9924E3E87BA732CCD354D18CB75A883EFA9B900EA125BFD51`.

`HMailServer-SEC18-Disposable` is **Running** with 3 GB RAM and 4 vCPUs. It
has exactly one adapter on the **Private** switch
`HMailServer-SEC18-Private`; inventory is recorded at
`C:\SEC18-Disposable\HMailServer-SEC18-Disposable\Evidence\hyperv-inventory.json`.

Test-only commits `11129543f` and `56eadeda4` make SQL source-shape assertions
checkout-independent and retry scanner test-file cleanup across transient
antivirus locks. Code/test commit `d1547d4a4` also closes the bounded
`DeliveryQueue.Clear()` lifecycle gap: `DeliveryQueuePauseDrainGate` prevents
worker/clear overlap, the SQL clear path targets only type 1 and honors a
clear-start boundary, and each batch rechecks the live administrator guard.
Focused queue coverage is `27/27`; the full Debug suite is `2319 passed, 58
skipped, 0 failed`. Code/test commit `b278c212e` then adds an in-process,
account-scoped IMAP folder change tracker. Successful `IMAPFolders.Add`/
`DeleteByDBID` and `IMAPFolder.Save`/`Delete` mutations publish after store
success; `ImapSession` checks the selected mailbox storage-owner generation,
refreshes renames, and rejects deleted selected subtrees. Focused COM/session
coverage is `63/63`; the current full Debug suite is `2324 passed, 58 skipped,
0 failed`.

Legacy anchors are `InterfaceDeliveryQueue::Clear` and
`DeliveryQueueClearer::DoWork` (`source/Server/COM/InterfaceDeliveryQueue.cpp:15-34`;
`source/Server/SMTP/DeliveryQueue.cpp:44-78`). Installed DeliveryQueue COM
identity/direct activation and `ResetDeliveryTime`/`StartDelivery`/`Remove`
boundaries are unchanged. Live disposable SQL/Data readback, expired-lease
and in-flight delivery races, file-cleanup failure evidence, registered
service/out-of-process COM, and queue performance/soak remain open; release is
still **RED**.

The IMAP folder tracker is intentionally an in-process signal. Public-folder
ACL revocation, account-wide deletion, concurrent publication ordering, live
SQL/Data readback, registered service lifecycle, and cross-process COM/session
propagation remain unproven. Legacy anchors for the folder mutation boundary
are `InterfaceIMAPFolders::{Add,DeleteByDBID}` and
`InterfaceIMAPFolder::{Save,Delete}` in `source/Server/COM`, with persistence
in `PersistentIMAPFolder::{SaveObject,DeleteObject}`.

The guest first boot is complete and the VMConnect console previously reached
`win-6tgbde5c01k\\administrator`, but it is currently at the Administrator
password prompt. Guest Services is enabled for the private VM
channel. The host-built staging payload was extracted offline into guest
`C:\SEC18\Payload` (171 files, 43,773,872 bytes); source and transfer SHA-256
are `C16C00B65C13189130B548EFEDE587D50875CE6323B2A429ACD0BB559D6053A9`.
Guest inventory was written and copied out to
`C:\Users\Public\sec18-guest-inventory.json`, with copy evidence at
`C:\Users\Public\sec18-guest-inventory-copy.json`. Official Microsoft .NET
Runtime 10.0.10 x64 and SQL Server 2022 Express installers were downloaded
with final-domain checks, hashed, and transferred to guest
`C:\SEC18\Packages`; package inventory is at
`C:\Users\Public\sec18-package-inventory.md` and transfer evidence is at
`C:\Users\Public\sec18-package-transfer.json`. No installer was executed.

The inventory proves the guest is Windows Server 2025 Evaluation, has no
.NET runtime, no SQL Server/SQL Express service, and no hMailServer service.
The guest is currently at the Administrator password prompt; the blank
password Enter attempt did not authenticate. No password was entered or
bypassed. Therefore no server, SQL/Data, COM, DCOM, or migration workload has
run yet.
Production service, database, Data directory, COM registration, DCOM ACLs,
and firewall remain untouched. Release remains **RED**.

Next slice: manually complete disposable Administrator sign-in in VMConnect,
then install only the already-verified official .NET/SQL packages and the
isolated hMailServer test stack before provisioning disposable SQL/Data/message
state and running the guarded migration/rollback drill.

The backlog was audited after the green suite: the IMAP master-user runtime is
implemented and the disposable Net10 acceptance matrix is recorded. Remaining
gates are native AD evidence, a registry-isolated legacy C++ comparison, live
100k-mailbox/SMTP/delivery thresholds, and the 24-hour leak soak.

The latest parity audit also confirmed the legacy `BlockedAttachments` mutation
path (`Add`, `Save`, `DeleteByDBID`, item `Delete`, and setters) against
`InterfaceBlockedAttachments.cpp:75-145` and `InterfaceBlockedAttachment.cpp:14-143`;
focused coverage is `15/15`.

## Current Authoritative Continuation (2026-08-14, installer rollback compensation)

Code/test commits `3fe4cb513` and `ff100f32a` add
`build/net10-service-rollback.ps1` and wire
it into `build/install-net10-service.ps1`. Replacement of a stopped legacy
service now snapshots the original `PathName` (including `RunAsService`),
start mode, error control, display name, description, and dependencies; it
requires the legacy executable and a valid explicit rollback archive before
`--register-com` or `sc.exe` mutation. A post-mutation failure restores the
service snapshot and invokes the legacy executable's `/Register` path to
restore the previous COM registration. New service creation includes the
legacy `RPCSS` dependency. Focused rollback/preflight tests pass and full
Net10 Debug is `2313 passed, 58 skipped, 0 failed`.

The uninstaller also snapshots the owned service before deletion and restores
it when the later COM unregister fails, preserving legacy service-then-COM
ordering while closing the post-delete failure window.

Legacy anchors: `hMailServer.cpp::_tWinMain`,
`ServiceManager::RegisterService`, `ServiceManager::ReconfigureService_`,
and `ServiceManager::UnregisterService` in
`hmailserver/source/Server/Common`; installer anchors are
`hmailserver/installation/hMailServerInnoExtension.iss:536-663`.

No Windows service, registry, COM, SQL, or Data mutation was run. The real
disposable replacement/rollback drill, DB setup/upgrade parity, and forced
failure evidence remain environment-blocked. Release remains **RED**.

Next slice: run the disposable legacy-to-Net10 replacement drill on a
registry-isolated VM and verify service, COM, SQL, and Data rollback together.

## Historical Continuation (2026-08-14, isolated backup/restore semantic round trip)

Code/test commits `3288249ad` and `83c77b86d` delegate runtime-created
`Application.Reinitialize` synchronously to the existing coordinator after
server-administrator authentication. Direct/parameterless instances retain
their `E_NOTIMPL` behavior when no runtime delegate exists. COM identity,
ProgID, DISPID 13, installed registration, and direct activation boundaries
are unchanged. The authorization generation lease is held across the runtime
call. Focused COM coverage is `16/16`; full Net10 Debug is `2313
passed, 58 skipped, 0 failed`.

The production restore callback from `894affe5f` remains post-restore. The
bounded isolated restore/rollback and raw DataBackup semantic round-trip suite
now passes `21/21` with unique local SQL databases and temporary Data roots;
normalized metadata and staged Data file SHA-256 evidence match. The slice also
corrects legacy `Recipients` XML nesting and sequential fetch-row reading. No
production resource was used. Paired C++/.NET performance, SEC-18,
migration/installer, out-of-process COM, and soak remain open, so release
remains **RED**.

Next slice: isolated migration/installer rollback planning and disposable drill.

## Current paired performance evidence (2026-08-14)

Fresh disposable SQL/Data equivalence passed with 37 equal table row counts,
1,000 identical message files, Full-Text readiness, and loopback SMTP/IMAP/POP3
ports. Net10 passed the protocol, SMTP acceptance, 1,000-session IMAP, FTS,
delivery queue, and POP3-large scenarios. The C++ binary was refused by the
read-only Registry32 isolation preflight because its installed path resolves to
`C:\hMailServer57-Test\Bin`; no C++ process was launched. Performance remains
**RED** with no ratio or winner. Evidence is under
`artifacts/benchmarks/live-cpp-net10-20260814/`.

## Historical production restore callback (2026-08-14)

Code/test commit `894affe5f` carries the production restore reinitializer from
`Program.cs` through `SevenZipBackupArchiveRuntime` and
`BackupXmlPayloadRuntime` into `MetadataBackupRestoreExecutor`. The callback is
post-restore and is invoked only after supported SQL/Data restore work. Focused
restore/runtime coverage is `82 passed, 1 skipped, 0 failed`; full Net10 Debug
is `2308 passed, 57 skipped, 0 failed`.

Legacy `BackupExecuter::StartRestore` schedules `Reinitializator::ReInitialize`
on a worker after restore completion. COM `Application.Reinitialize` remains
unimplemented in this slice; no installed COM identity, authentication
boundary, or SMTP behavior changed. Isolated restore/rollback, paired
C++/.NET performance, SEC-18, migration/installer, out-of-process COM, and
soak remain open; release remains **RED**.

Next slice: bounded authenticated `ApplicationComClass.Reinitialize`
delegation, then isolated restore/rollback acceptance.

## Historical protocol participant registration (2026-08-14)

Code/test commits `0e9164404` and `63f512752` register IMAP, POP3, and SMTP restartable
listener participants in production DI. The hosted services retain ownership
of their long-running tasks across coordinator stop/start transitions;
`ServiceReinitializationCoordinator` performs reverse stop and forward start,
and `ServerReadinessSignal` publishes bootstrap/readiness for a fresh
generation only after success; faulted supervision and host shutdown are
fail-closed.
Focused coverage is `25/25`; full Net10 Debug is `2307 passed, 57 skipped,
0 failed`.

Legacy anchors are `Reinitializator::{ReInitialize,WorkerFunc}` and
`Application::{StopServers,Reinitialize,StartServers}`. The restore callback
and `ApplicationComClass.Reinitialize` are still not wired, so restore remains
fail-closed. Paired C++/.NET performance, isolated restore/rollback, SEC-18,
migration/installer, out-of-process COM, and soak remain open; release remains
**RED**.

Next slice: connect the service-owned restore callback and bounded COM
`Application.Reinitialize` path, then prove isolated restore/rollback.

## Current Authoritative Continuation (2026-08-14, service reinitialization seam)

Code/test commit `a84c1a032` adds and tests the internal
`ServiceReinitializationCoordinator`. Focused coverage is `6/6`; full Net10
Debug is `2296 passed, 57 skipped, 0 failed`. It preserves the legacy
restore/reinitialize ordering anchored by `BackupExecuter::StartRestore`,
`Reinitializator::ReInitialize`, and `Application::StopServers` /
`Reinitialize` / `StartServers`, while compensating partial lifecycle failures.

This is an architecture seam only. It is not registered in production DI,
there are no restartable hosted-service participants or readiness barrier,
`BackupArchiveRuntime` has no reinitialize callback, and
`ApplicationComClass.Reinitialize` remains `E_NOTIMPL`. Restore remains
fail-closed and release remains **RED**. Next slice: implement participant
adapters/readiness, then connect restore and COM only after those are proven.

## Current Authoritative Continuation (2026-08-14, readiness generation seam)

Code/test commit `a4323a102` adds `ServerReadinessGeneration` behind the
existing `ServerReadinessSignal`. `BeginReinitialization()` creates a fresh
bootstrap/readiness pair while preserving the completed prior generation.
Focused coverage is `5/5`; full Net10 Debug is `2297 passed, 57 skipped,
0 failed`.

The seam is not wired to listeners, hosted-service participants, restore, or
COM. Next slice: restartable participant adapters and a real readiness barrier.
Release remains **RED**.

## Current Authoritative Continuation (2026-08-14, listener start callback seam)

Code/test commit `c9937dd87` adds additive per-run endpoint callbacks to the
IMAP, POP3, and SMTP TCP listeners. Existing startup paths are unchanged; the
new tests bind each listener object twice. Focused coverage is `20/20`; full
Net10 Debug is `2300 passed, 57 skipped, 0 failed`.

No hosted-service participant, readiness generation, restore callback, or COM
integration is wired. Next slice: explicit hosted-service stop/start adapter
and drain/readiness ordering. Release remains **RED**.

## Current Authoritative Continuation (2026-08-14, restartable listener primitive)

Code/test commit `4cb46e777` adds internal `RestartableListenerLifecycle`.
Focused coverage is `3/3`; full Net10 Debug is `2303 passed, 57 skipped,
0 failed`. The primitive serializes transitions, waits for drain, and cleans
up failed starts, but is not wired to hosted services, readiness, restore, or
COM. Next slice: the three hosted-service adapters. Release remains **RED**.

## Current Authoritative Continuation (2026-08-14, SMTP hosted-service adapter)

Code/test commit `0633bd2cb` wires the SMTP hosted service through the
restartable participant facade. Focused coverage is `9/9`; full Net10 Debug is
`2305 passed, 57 skipped, 0 failed`. All three protocol services now use the
helper, but readiness/coordinator registration, restore, and COM integration
remain open. Next slice: protocol participant registration. Release remains
**RED**.

## Historical POP3 hosted-service adapter (2026-08-14)

Code/test commit `9500dbee4` wires the POP3 hosted service through the
restartable participant facade. Focused coverage is `9/9`; full Net10 Debug is
`2305 passed, 57 skipped, 0 failed`. SMTP remains one-shot and all readiness,
participant registration, restore, and COM integration remain open. Next slice:
SMTP hosted-service adapter. Release remains **RED**.

## Historical IMAP hosted-service adapter (2026-08-14)

Code/test commit `5d44dd4f0` wires the IMAP hosted service through the
restartable participant facade. Focused coverage is `8/8`; full Net10 Debug is
`2305 passed, 57 skipped, 0 failed`. POP3/SMTP remain one-shot, all participant
registration and readiness integration remain open, and restore/COM are still
fail-closed. Next slice: POP3 hosted-service adapter. Release remains **RED**.

## Historical listener participant facade (2026-08-14)

Code/test commit `2aa8d32ee` adds `RestartableListenerParticipant` over the
listener lifecycle helper. Focused coverage is `4/4`; full Net10 Debug is
`2304 passed, 57 skipped, 0 failed`. The facade is still not registered in
production hosted services. Next slice: three adapter registrations with
readiness-generation ordering. Release remains **RED**.

## Current Authoritative Continuation (2026-08-14, transactional distribution-list deletion)

Code/test commit `1e90198e4` completes direct distribution-list deletion
atomicity. `SqlServerDistributionListAdministrationStore.DeleteDistributionListAsync`
now runs the owner-scoped recipient and parent DELETEs in one `SqlTransaction`;
`SET XACT_ABORT ON` and `UPDLOCK, HOLDLOCK` protect the parent ownership check,
and a zero-row parent delete rolls back. Legacy references remain
`PersistentDistributionList::DeleteObject`, `DeleteMembers`, and
`PersistentDistributionListRecipient::DeleteByListID`; the legacy path is
numeric-ID based and non-transactional, while Net10 preserves its stricter
owner/failure contract.

Focused SQL contract tests: `8/8`. The disposable MSSQLSERVER integration
test passed `1/1` with owner delete, wrong-domain no-op, and injected parent
failure rollback; its GUID database was removed. Default full Net10 Debug:
`2290 passed, 57 skipped, 0 failed` because the destructive SQL opt-in is not
exported. Release remains **RED** for restore/reinitialization, paired C++
performance, SEC-18, migration/installer, and soak gates.

Next independent slices: service-owned restore reinitialization architecture;
isolated restore/rollback round trip; registry-isolated C++ paired benchmark.

## Current Authoritative Continuation (2026-08-14, owner-scoped distribution-list recipient deletion)

Code/test commit `143db0bb4` adds an owner-domain `EXISTS` predicate to
`SqlServerDistributionListAdministrationStore.DeleteDistributionListRecipientsSql`
and binds `@DomainID` in `DeleteDistributionListAsync`. Legacy references are
`PersistentDistributionList::DeleteObject`,
`PersistentDistributionList::DeleteMembers`, and
`PersistentDistributionListRecipient::DeleteByListID` in
`hmailserver/source/Server/Common/Persistence/`; those paths delete recipient
rows by numeric list ID and are non-transactional. Net10 now prevents a stable
wrong-domain direct deletion from removing another domain's recipients.

Focused SQL tests: `8 passed, 0 failed`. Full Net10 Debug: `2290 passed,
56 skipped, 0 failed`. Security review: **YELLOW** because parent/recipient
deletion remains two non-transactional commands; a failure or concurrent parent
change can still leave partial state. Next slice: explicit transaction,
rollback, and concurrency acceptance for direct distribution-list deletion.
No COM identity, direct activation, schema, SMTP, or unrelated Admin behavior
changed. Release remains **RED**.

## Historical Continuation (2026-08-14, SQL owner-scoped distribution-list update; superseded)

Code/test commit `3383b0847` adds `distributionlistdomainid = @DomainID` to
the `hm_distributionlists` UPDATE predicate in
`SqlServerDistributionListAdministrationStore.UpdateDistributionListSql`.
The focused SQL contract now passes `8/8`, including all legacy update fields
and the owner predicate. Legacy SQL ownership is anchored by
`PersistentDistributionList::SaveObject` and the `hm_distributionlists`
schema; no COM identity, SMTP, schema, or unrelated Admin collection changed.

Full Net10 Debug passes `2290`, skips `56`, fails `0`.

The matched disposable performance fixture remains valid and the Net10 matrix
passes, but the C++ run is still refused by read-only registry preflight because
legacy `_tWinMain` calls `RegisterAppID()` and Registry32 points to the installed
`C:\hMailServer57-Test\Bin`. No C++ ratio or winner is valid. Performance and
overall release remain **RED**. Evidence is under
`artifacts/benchmarks/live-cpp-net10-20260813/`.

Next independent work: service-owned restore reinitialization architecture;
isolated restore/rollback round trip; registry-isolated C++ paired benchmark.

## Historical Continuation (2026-08-14, Links cross-facade lifetime; superseded)

Code/test commit `88ef1006b` shares a process-local lifetime registry keyed by
`(DomainId, DistributionListId)` between `Application.Links` and
`Domain.DistributionLists`. Legacy `InterfaceLinks::get_DistributionList`
creates a detached wrapper without cross-facade invalidation; Net10 retains
the stricter fail-closed contract. Domain collection deletion/refresh now
invalidates retained Links list, recipient collection, and child facades, and
runtime store reconfiguration resets the registry. COM identities, direct
activation, SQL schema, SMTP behavior, and protocol paths are unchanged.

Focused Links/distribution-list coverage: `54 passed, 0 failed`. Full Net10
Debug: `2290 passed, 56 skipped, 0 failed`. Remaining blockers include
non-transactional SQL parent/recipient deletion, missing recipient
parent/domain predicates, restore/reinitialization and rollback, paired C++
performance, SEC-18, migration/installer, and soak. Release remains **RED**.

## Current Authoritative Continuation (2026-08-14, stale DistributionList facades)

Code/test commit `7e5afe134` closes one bounded stale-object safety gap for
`Application/Domains -> DistributionLists -> DistributionList -> Recipients`.
Legacy anchors are `InterfaceDistributionList::Delete`,
`InterfaceDistributionLists::DeleteByDBID`, `DistributionList::GetMembers`,
and `PersistentDistributionListRecipient::{DeleteObject,DeleteByListID}`;
legacy wrappers remain numeric-ID based. Net10 now shares a process-local
parent lifetime with recipient facades, invalidates it after successful delete,
invalidates retained facades removed by `Refresh`, and invalidates a displaced
token on same-ID registration. Retained list/recipient access and child
mutations fail closed with `E_ACCESSDENIED`.

Focused coverage: `53 passed, 0 failed`. Full Net10 Debug:
`2289 passed, 56 skipped, 0 failed`. Residual risks are deliberately open:
SQL parent/recipient deletion is still non-transactional, recipient SQL lacks
domain/parent-existence predicates, and the separate `Links` facade registry
is not shared with domain collections. Release remains **RED**; restore,
rollback, paired C++ performance, SEC-18, installer/migration, and soak gates
remain open.

## Current Authoritative Continuation (2026-08-14, Links recipient lease propagation)

Code/test commit `6e3bf3d5f` fixes the retained-object authorization gap in
`Application.Links -> DistributionList -> Recipients`. Legacy references are
`InterfaceLinks::get_DistributionList`,
`InterfaceDistributionList::get_Recipients`,
`InterfaceDistributionListRecipients::{Add,DeleteByDBID}`, and
`InterfaceDistributionListRecipient::{Save,Delete}`. Legacy child wrappers do
not reauthenticate; Net10 intentionally remains stricter. `Links.get_DistributionList`
now forwards the generation-bound authorization lease factory to the owning
list and recipient collection. A retained list obtained before failed/successful
reauthentication now fails closed before its recipient store is called.

Focused Links/recipient/SQL coverage: `36 passed, 0 failed`. Full Net10 Debug:
`2287 passed, 56 skipped, 0 failed`. The separate stale-parent deletion or
numeric list-ID reuse risk remains open and is not claimed solved. Release
remains **RED**; restore/reinitialization, isolated rollback, paired C++
performance, SEC-18, migration/installer, and soak gates remain open.

## Current Authoritative Continuation (2026-08-13, DistributionListRecipients lease)

Code/test commit `b8227a1b2` completes one bounded legacy-anchored mutation
slice. Legacy references are
`InterfaceDistributionListRecipients::{Add,DeleteByDBID}`,
`InterfaceDistributionListRecipient::{Save,Delete}`, and
`PersistentDistributionListRecipient::{SaveObject,DeleteObject}`. The Net10
owner path now carries a generation-bound authorization lease through
`Domain.DistributionLists` -> `DistributionLists` -> `DistributionList` ->
`DistributionListRecipients`. Child and collection mutations hold the lease
through SQL callbacks, fail closed on unavailable authorization, and publish
only successful snapshots. COM identities and direct activation boundaries
were not changed.

Focused COM/SQL coverage: `51 passed, 0 failed`. Full Net10 Debug:
`2286 passed, 56 skipped, 0 failed`. Release remains **RED**. The next
independent slices are service-owned restore reinitialization, isolated
restore/rollback acceptance, and registry-isolated C++/.NET paired
performance. No performance ratio is valid until both implementations run
the same fixture and workloads.

## Current Authoritative Continuation (2026-08-13, restore reinitialization)

Code/test commit `24405daa6` closes the execution-boundary gap identified by
legacy `BackupExecuter::StartRestore` in
`source/Server/Common/Application/BackupExecuter.cpp`: after restoring
domains/data/settings, legacy calls
`Reinitializator::Instance()->ReInitialize()` before success. Net10
`MetadataBackupRestoreExecutor.ExecuteAsync` now invokes one injected
reinitialization callback after successful restore completion for all supported
restore branches. Focused restore tests are `35 passed, 0 failed`; full Net10
Debug is `2282 passed, 56 skipped, 0 failed`.

The production archive runtime requires that callback and fails closed before
mutation when it is absent. Tests cover success-once, failure-no-invocation,
and missing-production-callback denial. The callback is not yet wired to a
real service-owned coordinator, so restore/rollback remains an open release
blocker. The paired C++/.NET performance gate remains **RED** and no ratio is
claimed. Next independent slices: wire and test isolated service-owned
reinitialization, then disposable restore round-trip/rollback acceptance, then
registry-isolated C++ paired performance.

## Current Authoritative Continuation (2026-08-13, DomainAliases mutation lease)

Code/test commit `baa50bd4a` extends the generation-bound authorization lease
to legacy `DomainAliases` mutations. Legacy anchors:
`InterfaceDomain::get_DomainAliases`,
`InterfaceDomainAliases::{Add,Delete,DeleteByDBID,get_Item,get_ItemByDBID}`,
`InterfaceDomainAlias::{AliasName,Save,Delete}`, and
`PersistentDomainAlias::{SaveObject,DeleteObject}`. Net10 propagates the
lease through `Domain.DomainAliases`, holds it across new/existing child
Save/Delete and collection deletion store calls, avoids nested child-delete
acquisition and callback authority reentrancy, and preserves owner-scoped
snapshots and `E_ACCESSDENIED`.

Focused DomainAliases plus related SQL/protocol coverage: `28 passed, 0
failed`. Full Net10 Debug: `2279 passed, 56 skipped, 0 failed`. Security review:
GREEN. Installed COM identity/direct activation, SMTP alias resolution, SQL
schema/parameterization, and broader collections were unchanged.

Paired C++/.NET performance remains **RED**: the disposable fixture and Net10
matrix are valid, but legacy startup is blocked on this host by installed
Registry32 identity. Next: run the same matrix in a registry-isolated C++ VM;
then isolated restore/rollback; then the next legacy-anchored Admin/protocol
gap. Do not claim a performance ratio.

## Current Authoritative Continuation (2026-08-13, matched performance fixture)

The disposable C++/.NET 10 start state is now equivalent: 37 SQL tables and
row counts match, 1,000 Data files have equal SHA-256 manifests, Full-Text is
ready on both sides, and SMTP/IMAP/POP3 use identical loopback ports
`127.0.0.1:2525`, `:1143`, and `:25110`. Net10 passed the current matrix:
SMTP/IMAP/POP3 protocol `25/25` each, SMTP acceptance `25/25`, IMAP-1000
`1000/1000`, FTS `25/25`, fresh delivery queue `50/50`, and POP3-large `5/5`.
The full normal Net10 suite passed `2275`, skipped `56`, failed `0`.

Evidence: `artifacts/benchmarks/live-cpp-net10-20260813/`; report:
`hmailserver/source/Server.Net10/PERFORMANCE_COMPARISON_REPORT.md`.

The C++ launch was refused before execution because Registry32 resolves the
installed path to `C:\hMailServer57-Test\Bin`, while the disposable target is
under `C:\hmail-perf-pair-run-20260813_223908\cpp\Bin`. Legacy `/Debug`
startup can register the installed Application AppID, so changing that state
on this host is prohibited. Exact same-fixture evidence:
`artifacts/benchmarks/live-cpp-net10-20260813/cpp-preflight-same-fixture-20260813_223908.json`.
Performance remains **RED**; no C++/.NET 10 ratio or winner is claimed.

Next independent slice: run the same fixture and workload matrix on a separate
registry-isolated C++ staging VM/installation, then complete paired soak and
ratio validation. Restore/rollback, SEC-18, installer/out-of-process COM, and
other release gates remain open.

## Current Authoritative Continuation (2026-08-13, RouteAddress mutation authorization lease)

Code/test commit `cd2146e45` extends the generation-bound authorization lease
to legacy `Settings.Routes[...].Addresses` mutations. The legacy anchors are
`InterfaceRouteAddresses::DeleteByDBID/Add/DeleteByAddress`,
`InterfaceRouteAddress::Save/Delete`, and
`PersistentRouteAddress::SaveObject/DeleteObject` in
`source/Server/COM/InterfaceRouteAddresses.cpp`,
`InterfaceRouteAddress.cpp`, and
`source/Server/Common/Persistence/PersistentRouteAddress.cpp`. Net10 leases
collection deletion and child Save/Delete, avoids nested child-delete leases,
preserves owner-scoped snapshots, and passes lease denial as
`E_ACCESSDENIED`. COM identity/direct activation, SMTP route behavior, and SQL
schema are unchanged.

Focused Route/RouteAddress plus SQL store coverage: `40 passed, 0 failed`.
Full Net10 Debug: `2275 passed, 56 skipped, 0 failed`. Paired C++/.NET
performance remains **RED** because equivalent registry-isolated legacy
execution is unavailable; no speedup claim is made. Next: the next
legacy-anchored Admin/protocol mutation gap; registry-isolated C++ benchmark,
restore/rollback, SEC-18, and soak gates remain open.

## Current Authoritative Continuation (2026-08-13, Route mutation authorization lease)

Code/test commits `6567adc72` and `f2c63c5fe` close the bounded
generation-bound authorization lease gap for legacy `Settings.Routes`
mutations and remove a reentrant authority check that could deadlock a real
Application-backed save. The legacy anchors are
`InterfaceRoute::Save`/`Delete` in
`source/Server/COM/InterfaceRoute.cpp` and persistence in
`source/Server/Common/Persistence/PersistentRoute.cpp`. Net10 holds the
authenticated lease across existing/new `Route.Save`, child `Route.Delete`,
and direct `Routes.DeleteByDBID`; child deletion delegates to the owning
collection without nested lease acquisition and updates only that snapshot.
Null leases fail closed with `E_ACCESSDENIED`. Installed COM identity,
direct activation boundaries, SMTP route runtime behavior, and SQL schema are
unchanged.

Focused Route coverage: `20 passed, 0 failed`; Route plus SQL store coverage:
`23 passed, 0 failed`. Full Net10 Debug: `2273 passed, 56 skipped, 0 failed`.
Paired C++/.NET performance remains
**RED** because the equivalent registry-isolated legacy run is unavailable;
no speedup claim is made. Next: the adjacent legacy-anchored `RouteAddresses`
lease slice; registry-isolated C++ paired benchmark, isolated restore/rollback,
and the remaining release gates remain open.

## Current Authoritative Continuation (2026-08-13, FetchAccount SQL UPDATE/readback acceptance)

Code/test commit `abfed117e` adds isolated SQL acceptance for legacy
`InterfaceFetchAccount::Save` existing-row behavior and
`PersistentFetchAccount::SaveObject`, anchored in
`source/Server/COM/InterfaceFetchAccount.cpp` and
`source/Server/Common/Persistence/PersistentFetchAccount.cpp`. The fixture
creates a GUID-named database on user-owned `(localdb)\MSSQLLocalDB`, uses a
new marked TEMP Data root, verifies existing-row UPDATE/readback, owner-scope
rejection, and updated legacy Blowfish ciphertext, and drops the database in
`finally`.

Focused integration: `3 passed, 0 failed`; related SQL store tests:
`10 passed, 0 failed`. Full Net10 with disposable opt-in:
`2316 passed, 10 skipped, 0 failed`. Evidence is under
`artifacts/net10-disposable/run-20260813-imap-permission/`. MSSQLSERVER,
`HmailDb_Test5700`, production Data, installed COM registration, and the
production service were not used. The paired C++/.NET performance gate remains
RED because the registry-isolated legacy run is unavailable.

Next independent slices: registry-isolated C++ paired benchmark execution;
then the next legacy-anchored Admin/protocol gap.

## Current Authoritative Continuation (2026-08-13, IMAP folder-permission authorization lease)

Legacy `InterfaceIMAPFolderPermissions` collection deletion/add paths and
`InterfaceIMAPFolderPermission::Save/Delete` are in
`source/Server/COM/InterfaceIMAPFolderPermissions.cpp` and
`InterfaceIMAPFolderPermission.cpp`. Code/test commit `23802be01` consumes
the existing generation-bound authorization lease across permission
collection Delete/DeleteByDBID and item new/existing Save/Delete callbacks.
New items retain the owning collection lease factory; by-name wrappers retain
their delete/update delegates; null leases fail closed with `E_ACCESSDENIED`;
logout checks reject retained mutation facades before store callbacks. COM
identity, direct activation denial, owner/snapshot checks, SMTP trust, live
reconfiguration, and SQL schema were unchanged.

Focused permission coverage: `30 passed, 0 failed`; combined IMAP
folder/permission/SQL-store coverage: `63 passed, 0 failed`. Full Net10:
`2269 passed, 55 skipped, 0 failed`. Retained wrappers can still read or stage
in-memory values after logout; that is outside this persistence-mutation
slice. Paired C++/.NET performance, live SQL/readback, registered
COM/service/worker, restore, SEC-18, and soak gates remain unproven; release
is **RED**.

Parity clarification commit `5cb18f4b2` proves retained permission wrappers
keep legacy read/stage behavior after logout while persistence Save remains
denied. Next independent slices: approved disposable SQL FetchAccount UPDATE/readback;
registry-isolated C++ paired performance execution; remaining
legacy-anchored Admin/protocol parity selected by the production gate.

## Current Authoritative Continuation (2026-08-13, IMAP folder mutation authorization lease)

Legacy `InterfaceIMAPFolders::Add/DeleteByDBID` and
`InterfaceIMAPFolder::Save/Delete` are in
`source/Server/COM/InterfaceIMAPFolders.cpp` and
`InterfaceIMAPFolder.cpp`; collection ownership follows the legacy lookup
and delete paths. Code/test commit `59dd1d7d1` consumes the existing
generation-bound authorization lease around IMAP folder insert, update, and
delete callbacks. Add and Save recheck the live authentication predicate
when a state-backed object has no lease factory; null lease acquisition fails
closed with `E_ACCESSDENIED`, unknown IDs remain `DISP_E_BADINDEX`, and the
owning snapshot is appended/updated only after successful store completion.
COM identity, direct activation denial, SMTP trust, live reconfiguration, and
SQL schema were unchanged.

Focused IMAP folder/permission/SQL-store coverage: `60 passed, 0 failed`.
Full Net10: `2266 passed, 55 skipped, 0 failed`. The next bounded security
slice is IMAP folder-permission mutation lease consumption. Live SQL/readback,
registered COM/service/worker, paired C++/.NET performance, restore, SEC-18,
and soak gates remain unproven; release is **RED**.

Next independent slices: consume the lease across IMAP folder permissions;
approved disposable SQL FetchAccount UPDATE/readback; registry-isolated C++
paired performance execution.

## Current Authoritative Continuation (2026-08-13, Group/GroupMember mutation authorization lease)

Legacy `InterfaceGroup::Save/Delete`, `InterfaceGroupMembers::DeleteByDBID`,
`InterfaceGroupMember::Save/Delete`, and
`InterfaceGroupMembers::DeleteByDBID` are in
`source/Server/COM/InterfaceGroup.cpp`, `InterfaceGroupMembers.cpp`, and
`InterfaceGroupMember.cpp`; collection ownership is defined by the legacy
`Collection::DeleteItemByDBID` path. Code/test commit `90b68a7fa` consumes the
existing generation-bound authorization lease immediately around Group and
GroupMember insert/update/delete store callbacks. Direct retained child
mutations and direct collection deletion both fail closed when lease
acquisition returns null; retained Group facades revalidate that their
owning group is still present before exposing or mutating GroupMembers;
unknown collection IDs remain no-ops without a store call or lease
acquisition. COM identity, direct activation denial,
existing server-administrator checks, owner scoping, SMTP trust, and live
reconfiguration were unchanged.

Focused Group/GroupMember coverage: `31 passed, 0 failed`; with the related
SQL group-store classes: `39 passed, 0 failed`. Full Net10:
`2263 passed, 55 skipped, 0 failed`. Security review remains required for the
next IMAP folder/permission mutation slice. No live SQL/readback, registered
COM/service/worker, paired C++/.NET performance, restore, SEC-18, or soak gate
is proven; release remains **RED**.

Next independent slices: consume authorization leases across IMAP folder and
IMAP folder-permission mutations; approved disposable SQL FetchAccount
UPDATE/readback; registry-isolated C++ paired performance execution.

## Current Authoritative Continuation (2026-08-13, indirect FetchAccount lease propagation)

Legacy `InterfaceFetchAccounts::Add`, `Delete`, and `DeleteByDBID`, and
`InterfaceFetchAccount::Save`, `DownloadNow`, and `Delete`, are in
`source/Server/COM/InterfaceFetchAccounts.cpp` and
`InterfaceFetchAccount.cpp`. The bounded code/test slice carries the
generation-bound authorization lease through `Application.Links`,
`GroupMember.Account`, and `IMAPFolderPermission.Account` into the existing
`Account -> FetchAccounts` adapter. Indirect `DownloadNow` paths hold and
dispose the lease; null lease acquisition returns `E_ACCESSDENIED`. Retained
`Application.Links` objects and descendants are generation-bound, and the
permission-to-Group child retains the delegate. COM identities/direct
activation boundaries and SMTP/external-fetch behavior are unchanged.

Focused coverage: `81 passed, 0 failed` (FetchAccounts 31, Links 11,
GroupMembers 13, IMAPFolderPermissions 26). Full Net10:
`2258 passed, 55 skipped, 0 failed`. Separate high-risk work remains for
lease consumption by Group, GroupMember, IMAP folder, and IMAP permission
mutations; existing-row `FetchAccount.Password` is still fenced. No live
SQL/readback, registered COM/service/worker, paired performance, restore,
SEC-18, or soak gate is proven. Release remains **RED**.

Next independent slices: consume authorization leases across Group/GroupMember
and IMAP folder/permission mutations; approved disposable SQL FetchAccount
readback; registry-isolated C++ paired performance execution.

## Current Authoritative Continuation (2026-08-13, FetchAccount mutation authorization lease)

Legacy `InterfaceFetchAccount::DownloadNow`, `Save`, and `Delete`, together
with `InterfaceFetchAccounts::Add`, `Delete`, and `DeleteByDBID`, are in
`source/Server/COM/InterfaceFetchAccount.cpp` and `InterfaceFetchAccounts.cpp`.
Code/test commit `0589d0862` carries the existing generation-bound lease from
the authenticated `AccountComClass.FetchAccounts` boundary across direct
DownloadNow, insert, update, and delete store calls. Null lease acquisition
returns `E_ACCESSDENIED`; successful mutations dispose the lease after the
store/wake operation and publish only the owning snapshot. COM identity,
direct activation denial, owner scoping, SMTP trust, and external-fetch
runtime behavior are unchanged.

Focused FetchAccounts coverage: `29 passed, 0 failed`. Full Net10:
`2255 passed, 55 skipped, 0 failed`. The next bounded security slice is to
propagate the same lease factory through `Links`, `GroupMembers`, and
`IMAPFolderPermissions` Account adapters. No live SQL/readback, registered
COM/service/worker, paired performance, restore, SEC-18, or soak gate is
proven; release remains **RED**.

## Current Authoritative Continuation (2026-08-13, FetchAccount Save UPDATE parity)

Legacy `InterfaceFetchAccount::Save` (`source/Server/COM/InterfaceFetchAccount.cpp`)
calls `PersistentFetchAccount::SaveObject`
(`source/Server/Common/Persistence/PersistentFetchAccount.cpp`), whose
existing-row branch updates `hm_fetchaccounts` by `faid`, writes `fanexttry`,
and encrypts `fapassword` through the legacy Blowfish cipher.

Code/test commit `6573fdeda` adds the bounded Net10 equivalent. Existing-row
setters are staged only through the owning authenticated `Account ->
FetchAccounts` collection; Save rejects cross-parent account changes, SQL is
parameterized and scoped by `faid` plus `faaccountid`, unchanged password
ciphertext is preserved, and explicit password changes use the existing
encryption boundary. Success updates retained child and collection snapshots;
failures preserve staged child and old collection state. COM identity/DISPIDs,
direct activation denial, DownloadNow/Delete, SMTP, and external-fetch runtime
behavior are unchanged.

Focused coverage: `36 passed, 0 failed`. Full Net10: `2252 passed, 55 skipped,
0 failed`. No live SQL update/readback, registered out-of-process COM,
Administrator, or worker-cycle evidence exists. Existing-row
`FetchAccount.Password` getter remains `E_NOTIMPL` to avoid putting plaintext
credentials in normal administration snapshots. Reality review is NEEDS WORK
for those live gates; release remains **RED**.

Next independent slices: approved disposable SQL FetchAccount UPDATE/readback
with unchanged/changed password and wrong-owner cases; registry-isolated C++
paired performance evidence; isolated service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-13, Account.DeleteMessages parity)

Legacy `InterfaceAccount::DeleteMessages`
(`source/Server/COM/InterfaceAccount.cpp`, DISPID 11) delegates to
`PersistentAccount::DeleteMessages`, which traverses
`PersistentIMAPFolder::DeleteByAccount` and `DeleteObject` to remove
account-owned IMAP content while retaining the Inbox root. Code/test commit
`0da667302` implements the bounded authenticated Net10 equivalent through the
existing `Application -> Domains -> Accounts` boundary. The store transaction
rechecks account ID/domain/address ownership, scopes message deletion to the
account's folder set, removes dependencies and ACL rows, preserves Inbox,
invalidates the account-size cache, deletes owned files, and publishes only
the owning snapshot. The existing authorization lease is held through the
store call; direct activation remains denied and no SMTP trust or live
reconfiguration behavior changed.

Focused Account/IMAP/SQL coverage: `97 passed, 0 failed`. Full Net10:
`2247 passed, 55 skipped, 0 failed`. No live disposable SQL/Data deletion,
real COM activation, or post-commit manifest-recovery drill is proven. A
manifest read failure after SQL commit can still leave committed rows without
file/snapshot cleanup; this is a documented residual requiring durable
reconciliation or an isolated live drill. Release remains **RED**.

Next independent slices: approved disposable SQL/Data Account.DeleteMessages
acceptance with rollback/recovery evidence; registry-isolated C++ execution
and the identical paired performance matrix; isolated Windows
service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-13, AntiSpam local target pinning)

Legacy `SpamAssassinTestConnect::TestConnect`
(`source/Server/Common/AntiSpam/SpamAssassin/SpamAssassinTestConnect.cpp`)
resolves once via `DNSResolver::GetIpAddressesRecursive_` and connects to the
first selected IP; `InterfaceAntiSpam::TestSpamAssassinConnection`
(`source/Server/COM/InterfaceAntiSpam.cpp`, DISPID 36) preserves the COM
boundary. Code/test commit `55c9473ac` makes Net10
`AntiSpam.TestSpamAssassinConnection` pass the validated local IP literal to
the existing runtime, catches malformed host arguments as `E_FAIL`, and uses
IPv4-first selection when a dual-stack local result is available.

Focused AntiSpam coverage: `18 passed, 0 failed`. Full Net10: `2240 passed,
55 skipped, 0 failed`. No IID/DISPID, authentication/direct-activation,
SQL/WebAdmin, SMTP trust, service, registry, or live scanner socket behavior
changed. No live COM activation was demonstrated; release remains **RED**.
Next independent slice: approved real DNS/socket/TLS acceptance or a
registry-isolated C++ paired benchmark; otherwise continue the next
legacy-anchored COM/Admin gap without claiming release readiness.

## Current Authoritative Continuation (2026-08-13, ordinary-MX partial DNS resolution)

Legacy `DNSResolver::GetEmailServersRecursive_` in
`hmailserver/source/Server/Common/TCPIP/DNSResolver.cpp` continues collecting
healthy MX host addresses after an individual address lookup fails; it reports
overall failure only when no usable address remains. Net10
`RemoteSmtpEndpointResolver.ResolveRemoteAddressCandidatesAsync` now matches
that delivery behavior while narrowing failure handling to expected DNS
exceptions. Caller-requested cancellation and unexpected exceptions propagate;
non-requested DNS cancellation is treated as a transient per-host timeout.

Code/test commit: `575734089`. Focused resolver tests: `52 passed, 0 failed`.
Full Net10: `2237 passed, 55 skipped, 0 failed`. No COM, SQL schema, service,
listener, SMTP trust, Data directory, or live reconfiguration changes were
made. Security review found no new issue in the bounded parity change, but
generic private/link-local DNS egress remains a separate policy question and
real DNS/socket/TLS/SNI/revocation acceptance is still absent.

Release remains **RED**. The paired C++/.NET performance matrix is still
blocked by safe legacy Registry32 isolation, so no ratio or winner is valid.
Next independent slice: approved disposable real DNS/MX-to-TCP SMTP and
STARTTLS/implicit-TLS acceptance, or a registry-isolated C++ benchmark VM.

## Historical Authoritative Continuation (2026-08-13, IncomingRelays mutation authorization lease)

Legacy `InterfaceIncomingRelays::Delete` and `DeleteByDBID`, and child
`InterfaceIncomingRelay::Save` and `Delete`, are anchored in
`hmailserver/source/Server/COM/InterfaceIncomingRelays.cpp` and
`InterfaceIncomingRelay.cpp`. Code/test commit `339df5867` threads the
existing generation-bound authorization lease from `Application.Settings`
through `Settings.IncomingRelays` and holds it across IncomingRelays insert,
update, collection delete, child delete, and local snapshot publication.

The installed IncomingRelays/IncomingRelay IIDs, CLSIDs, ProgIDs, DISPIDs,
vtable order, direct activation denial, SQL store interface, SMTP trust
behavior, and live reconfiguration boundary are unchanged. Null leases fail
closed with `E_ACCESSDENIED`; store failures retain the existing `E_FAIL`
mapping and dispose the lease.

Focused IncomingRelays coverage is `23/23`; full Net10 at that historical commit is `2232 passed, 55
skipped, 0 failed`. Release remains **RED**: paired C++ performance,
restore/migration, service/out-of-process COM, SEC-18, live network, and
long-soak evidence are still unavailable or incomplete. Next independent slice:
registry-isolated C++ execution and the identical paired performance matrix.

## Current Authoritative Continuation (2026-08-13, IncomingRelays retained authorization)

Legacy `InterfaceIncomingRelays::Delete` and `DeleteByDBID`
(`hmailserver/source/Server/COM/InterfaceIncomingRelays.cpp`) mutate an
already acquired incoming-relay collection. The .NET retained collection now
rechecks the current server-administrator callback immediately before each
collection delete store call, preserving index snapshot behavior and the
existing `IncomingRelays`/`IncomingRelay` COM identity, direct activation
boundary, and SMTP trust behavior.

Code/test commit `b06f01257` adds focused denial coverage for both collection
mutation paths after administrator reauthentication fails. IncomingRelays
coverage is `14/14`; full Net10 is `2223 passed, 55 skipped, 0 failed`.

Security review found one medium residual risk: the callback check and store
mutation are not atomic. A concurrent authorization revocation can race a
retained mutation. The next bounded slice is to plumb the existing
authorization lease through IncomingRelays and hold it across insert/update/
delete. Release remains **RED**; the paired C++/.NET performance gate is still
blocked by registry-isolated legacy execution.

## Current Authoritative Continuation (2026-08-13, SecurityRanges retained authorization)

Legacy `InterfaceSecurityRanges::Delete`, `DeleteByDBID`, and `SetDefault`
(`source/Server/COM/InterfaceSecurityRanges.cpp`) operate on an acquired
server-admin collection. The .NET retained collection now rechecks the current
server-admin callback immediately before each of those store mutation paths,
while `SecurityRange.Delete` and `Save` retain their existing item-level
checks. This closes the stale-authorized-collection mutation gap without
changing the installed IIDs, DISPIDs, vtable order, class identities, direct
activation boundary, or SMTP behavior.

Code/test commit `c4b417035` adds focused denial coverage for index delete,
DBID delete, and SetDefault after administrator revocation. SecurityRanges
coverage is `26/26`; full Net10 is `2223 passed, 55 skipped, 0 failed`.

The same commit hardens the opt-in SQL identity/readback fixture: only the
current-user `(localdb)\\MSSQLLocalDB` target is accepted, a GUID marker is
written before schema creation, and cleanup verifies that marker before using
`WITH ROLLBACK IMMEDIATE`. The real SQL integration remains skipped because
the explicit disposable LocalDB approval variables are unset.

Release remains **RED**. Next slice: registry-isolated C++ execution for the
paired performance gate, or a separate staging VM.

## Current Authoritative Continuation (2026-08-13, SecurityRanges SQL evidence)

Legacy `InterfaceSecurityRanges::Add`
(`source/Server/COM/InterfaceSecurityRanges.cpp`) returns an unsaved item
scoped to the owning collection. `InterfaceSecurityRange::Save`
(`source/Server/COM/InterfaceSecurityRange.cpp`) calls
`PersistentSecurityRange::SaveObject`
(`source/Server/Common/Persistence/PersistentSecurityRange.cpp`) and appends
only after the insert succeeds. The .NET implementation already matches this
authenticated COM ownership and identity behavior.

Code/test commit `b535f94b0` adds an opt-in, fail-closed SQL Server integration
test that creates a random local disposable database, creates only the legacy
`hm_securityranges` table, verifies generated identity and stored columns, and
checks store readback before cleanup. Focused SQL tests are `4 passed, 1
skipped`; the new integration test skipped because
`HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` and
`HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1` are not
configured. Full Net10 is `2222 passed, 55 skipped, 0 failed`.

The test does not prove production database compatibility until it runs against
an explicitly approved disposable SQL target. No production SQL/Data,
registration, DCOM ACL, or SMTP trust behavior was changed. Release remains
**RED**. Next slice: registry-isolated C++ execution for the paired
performance gate, or a separate staging VM.

## Current Authoritative Continuation (2026-08-13, external-fetch library default)

Code/test commit `bf2f2c2dd` makes
`ExternalFetchPop3ClientOptions.EnforceEgressPolicy` default to `true`, so
direct `TcpExternalFetchSessionFactory` construction is fail-closed as well as
the production `Host.Build` path. Existing loopback protocol fixtures now
declare the explicit `false` audit/test opt-out. Focused external-fetch
coverage is `49/49`; full Net10 is `2222 passed, 54 skipped, 0 failed`.

Legacy `ExternalFetch::Start` and `POP3ClientConnection` remain unrestricted,
and the explicit .NET `false` override remains a security risk unless limited
operationally to controlled audit/test use. Release reality remains **RED**.
Next slice: registry-isolated C++ execution for the paired performance gate,
or the approved disposable real DNS/socket/TLS matrix.

## Current Authoritative Continuation (2026-08-13, external-fetch egress default)

Code/test commit `54694e852` changes `Host.Build` so
`ExternalFetch:EgressEnforce` defaults to `true`. The explicit configuration
override remains supported. This closes the default fail-open configuration
gap in the existing `ExternalFetchEndpointPolicy` path; it does not unify
scanner production traffic, implement live Diagnostics probes, or claim legacy
network behavior parity. Focused host/external-fetch coverage passed as part of
the latest full run: `2220 passed, 54 skipped, 0 failed`.

Next slice: registry-isolated C++ execution for the paired performance gate.
If that remains unavailable, continue with the approved disposable DNS/
socket/TLS acceptance slice. Release reality remains **RED**.

## Current Authoritative Continuation (2026-08-13, clean paired performance rerun)

The fresh disposable C++/.NET 10 SQL/Data pair was verified equivalent before
testing: 37 table row counts, 1,000 equal-SHA-256 Data files, active fixture,
Full-Text readiness, and `127.0.0.1:2525/1143/25110` matched. Net10 passed
SMTP acceptance `25/25`, SMTP/IMAP/POP3 protocol `25/25` each, IMAP-1000
`1000/1000`, FTS SEARCH `25/25`, queue `50/50`, and POP3 large-mailbox `5/5`.

The C++ process was not started. The read-only preflight rejects the target
because Registry32 still points at installed `C:\hMailServer57-Test\Bin`,
and legacy `/Debug` startup could write the installed Application AppID.
Consequently the performance gate remains RED: no ratio, regression
percentage, or winner is valid. No production service, DB/Data, COM identity,
DCOM ACL, or firewall state was changed.

Next slice: registry-isolated C++ execution on the same clean fixture and
identical workload matrix, or document the separate-VM prerequisite as the
release-gate blocker.

## Current Authoritative Continuation (2026-08-13, TCP/IP DEFAULT RESET)

Code/test commit `8440f7fc9` aligns `Settings.TCPIPPorts.SetDefault()` with
legacy `TCPIPPorts::SetDefault` (`hmailserver/source/Server/Common/BO/
TCPIPPorts.cpp`). The implementation refreshes before comparison, ignores
address and certificate ID when deciding whether the four defaults already
match, and preserves the current snapshot when refresh fails. Focused
TCPIPPorts coverage is `23/23`; related SQL coverage is `5 passed, 1 explicit
opt-in skip`; full Net10 is `2219 passed, 54 skipped, 0 failed`.

The paired C++/.NET 10 performance gate remains **RED**. The disposable
SQL/Data/message start state is equivalent, but safe C++ launch remains blocked
by installed Registry32 ownership; no ratio or winner is valid. Next slices are
disposable SQL/DNS/socket/TLS delivery evidence, registry-isolated C++ paired
execution, and isolated service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-13, TCP/IP PORT CERTIFICATE SAVE)

Code/test commit `e0abbba3d` implements the smallest remaining authenticated
`TCPIPPort.Save()` parity slice. Legacy
`PersistentTCPIPPort::SaveObject` (`hmailserver/source/Server/Common/
Persistence/PersistentTCPIPPort.cpp`) rejects normal saves when connection
security is SSL, optional STARTTLS, or required STARTTLS and the certificate ID
is zero; `InterfaceTCPIPPort::Save` returns the legacy interface error. Net10
now applies that check for new and existing items before store mutation,
retaining the draft/owning snapshot on failure. Focused TCPIPPorts tests pass
`21/21`; related SQL tests pass `5` with `1` explicit opt-in skip; full Net10
passes `2217`, skips `54`, and has `0` failures.

The current docs commit records this slice separately. Do not broaden it into
runtime listener creation or live reconfiguration. The paired C++/.NET 10
performance gate remains **RED** because the disposable C++ target is still
blocked by the read-only Registry32 preflight; no ratio or winner is valid.
Next slices: disposable SQL/DNS/socket/TLS delivery evidence, registry-isolated
C++ paired execution, and isolated Windows service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-12, NORMAL-MX ADDRESSES)

Code/test commit `1ffc564cb` implements normal-MX address candidate parity.
Legacy `DNSResolver::GetEmailServersRecursive_`
(`source/Server/Common/TCPIP/DNSResolver.cpp:170-330`) expands ordered MX
exchanges to A/AAAA addresses, deduplicates IPs, applies the positive cap after
flattening, and uses implicit domain A/AAAA addresses when MX is absent. Net10
now uses the same candidate shape as global relayers: original `Host` for
TLS/SNI and `ConnectionAddress` for TCP. Literal MX IPs, null MX, failures, and
no-address outcomes are covered by focused tests.

Focused tests are `52/52`; full Net10 is `2184 passed, 54 skipped, 0 failed`.
Real DNS/CNAME/socket acceptance and paired C++ performance remain unavailable;
release is **RED**. Next slices: approved disposable SQL/DNS/socket/TLS
delivery acceptance; registry-isolated or separate-VM C++ benchmark execution;
and real CNAME/normal-MX acceptance.

## Current Authoritative Continuation (2026-08-11, GLOBAL-RELAYER ADDRESSES)

Code/test commit `90146b45e` implements global relayer (`RouteId == 0`) address
candidate parity. Legacy anchors are
`ExternalDelivery::ResolveRecipientServers_`
(`source/Server/SMTP/ExternalDelivery.cpp:192-280`),
`DNSResolver::GetIpAddresses` (`source/Server/Common/TCPIP/DNSResolver.cpp:60-119`),
and `TCPConnection` (`source/Server/Common/TCPIP/TCPConnection.cpp:123-158`).
Net10 now preserves host order, expands ordered addresses, deduplicates IPs,
caps after flattening, bypasses DNS for literals, and keeps Host separate from
ConnectionAddress for TLS/SNI. SQL global-relayer targets receive the existing
`MaxNumberOfMXHosts` setting; forced routes are unchanged.

Focused tests are `46/46`; full Net10 is `2177 passed, 54 skipped, 0 failed`.
No COM identity, SQL schema, SMTP trust, or live reconfiguration changed.
Real DNS/socket acceptance and paired C++ performance remain unavailable and
RED. Next slices: approved disposable SQL/DNS/socket/TLS delivery acceptance;
registry-isolated or separate-VM C++ benchmark execution; normal-MX
address-level expansion and implicit-MX fallback.

## Current Authoritative Continuation (2026-08-11, NULL-MX)

Code/test commit `b39a17abf` preserves legacy null-MX failure behavior. The
legacy anchor is `DNSResolver::GetEmailServersRecursive_`
(`source/Server/Common/TCPIP/DNSResolver.cpp:208-260`), which rejects MX
exchange `.` at preference `0` before implicit-MX fallback. Net10 preserves the
root DNS name in `SystemDnsMxResolver` and returns an `IOException` from
`RemoteSmtpEndpointResolver`; the dispatcher maps resolution failure to its
existing transient result. No COM identity, SQL schema, SMTP trust, or route
behavior changed.

Focused tests are `40/40`; full Net10 is `2170 passed, 54 skipped, 0 failed`.
Remaining normal-MX gaps are A/AAAA expansion and deduplication, implicit-MX
fallback, endpoint cap position, and connect-address versus TLS/SNI-name
separation. Release remains **RED**. Next slices: fixed-relayer address
planner; approved disposable SQL/DNS/socket/TLS delivery; registry-isolated or
separate-VM C++ benchmark execution.

## Current Authoritative Continuation (2026-08-11, NORMAL-MX CANDIDATES)

Code/test commit `d569a0780` preserves all ordered MX exchanges for ordinary
remote delivery and applies `MaxNumberOfMXHosts` from `hm_settings` before the
existing sequential SMTP candidate loop. Legacy references are
`ExternalDelivery::ResolveRecipientServers_`
(`source/Server/SMTP/ExternalDelivery.cpp:192-280`),
`DNSResolver::GetEmailServers` (`source/Server/Common/TCPIP/DNSResolver.cpp:170-330`),
and `SMTPConfiguration::GetMaxNumberOfMXHosts`. This slice does not alter
routes, forced routes, or global relayers.

Focused tests are `36/36`; full Net10 is `2166 passed, 54 skipped, 0 failed`.
It does not yet expand MX exchanges to legacy A/AAAA address candidates,
deduplicate addresses, preserve implicit-MX A/AAAA fallback, or separate
resolved connect addresses from TLS/SNI hostnames. Real SQL/DNS/socket
acceptance and paired C++ performance remain unavailable and RED. Next slices:
fixed-relayer address planner; approved disposable SQL/DNS/socket/TLS delivery;
and registry-isolated or separate-VM C++ benchmark execution.

## Current Authoritative Continuation (2026-08-11, OUTBOUND TLS VERIFICATION)

Code/test commit `a2be0c906` applies the existing global
`VerifyRemoteSslCertificate` setting to remote SMTP implicit SSL and STARTTLS.
Legacy anchors are `TCPConnection::AsyncHandshake`
(`source/Server/Common/TCPIP/TCPConnection.cpp:308-350`),
`InterfaceSettings::put_VerifyRemoteSslCertificate`
(`source/Server/COM/InterfaceSettings.cpp:2244-2254`), and
`CertificateVerifier::VerifyCertificate_` / `OverrideResult_`
(`source/Server/Common/TCPIP/CertificateVerifier.cpp:18-45,125-171`). Net10
propagates the setting through MX, route, forced-route, and global-relayer
targets; missing/null rows fail closed to verification enabled; enabled TLS
uses hostname validation plus online revocation checking; optional STARTTLS
retains the legacy certificate-error override. Explicit `false` remains the
only normal way to disable verification.

Focused tests are `35/35`; full Net10 is `2165 passed, 54 skipped, 0 failed`.
Real invalid-certificate/revocation socket evidence and disposable SQL/TLS
acceptance are not available. Performance remains RED because the paired C++
process is still blocked by Registry32 path isolation. Next independent
slices: disposable SQL/socket/TLS/authentication acceptance; registry-isolated
or separate-VM C++ benchmark execution; and fixed-relayer DNS/
`MaxNumberOfMXHosts` parity.

## Current Authoritative Continuation (2026-08-11, GLOBAL SMTP RELAYER HOST FAILOVER)

Code/test commit `50e6d843f` implements the bounded global SMTP relayer
`|`-host failover slice. Legacy anchors are
`ServerTargetResolver::Resolve` and `GetFixedSMTPHostForDomain_`
(`source/Server/SMTP/ServerTargetResolver.cpp:38-116,170-237`) and
`ExternalDelivery::ResolveRecipientServers_` / `DeliverToSingleServer_`
(`source/Server/SMTP/ExternalDelivery.cpp:58-107,109-280,373-413`). Net10
preserves left-to-right non-empty host candidates only for the global relayer,
shares the configured port/security/authentication, continues after transient
transport or early SMTP failures, and stops on permanent replies. Once an
RCPT recipient is accepted, it does not fail over within the same attempt,
which avoids duplicate delivery after partial acceptance.

Focused tests are `34/34`; full Net10 is `2164 passed, 54 skipped, 0 failed`.
No COM identity, SQL schema, route/forced-route behavior, SMTP trust, or live
reconfiguration changed. Remaining parity gaps are fixed-relayer DNS address
ordering, legacy `MaxNumberOfMXHosts`, exact per-recipient queue accounting,
and real disposable SQL/socket/TLS/authentication evidence. Performance gate
is RED because the paired C++ process is still blocked by Registry32 path
isolation; no ratio or winner is valid.

Next independent slices: `VerifyRemoteSslCertificate` outbound runtime parity;
approved disposable SQL/socket/TLS/authentication acceptance; and a
registry-isolated or separate-VM C++ benchmark runner.

## Current Authoritative Continuation (2026-08-11, SMTP RELAYER PASSWORD PERSISTENCE)

Code/test commit `b518c8e83` implements the authenticated Administrator
`Settings.SetSMTPRelayerPassword` persistence slice. Legacy references are
`InterfaceSettings.cpp:998-1012`, `SMTPConfiguration.cpp:273-281`,
`PropertySet.cpp:153-159`, and `Property.cpp:81-96`; installed COM identity
and DISPID 36 are unchanged. Net10 authorizes the caller and server
administrator, acquires the generation lease, encrypts before the parameterized
`nvarchar(4000)` update, and preserves legacy zero-row `S_OK`. The password is
not added to snapshots or backups.

Focused coverage is `146/146`; full Net10 is `2159 passed, 54 skipped, 0
failed`. Real SQL ciphertext round-trip and out-of-process COM evidence are
still unavailable. The fixed-key compatibility cipher remains a separate
security/migration risk. Next slices: approved disposable SQL/COM round-trip,
legacy `|`-separated relayer failover, then `VerifyRemoteSslCertificate`
runtime parity. Release status remains RED.

## Current Performance Gate Verification (2026-08-11)

The disposable paired fixture has equal SQL row counts and equal Data SHA-256
for 1,000 message files on loopback SMTP/IMAP/POP3 ports `2525/1143/25110`.
Fresh C++ read-only preflight remains RED: Registry32 resolves the installed
`C:\hMailServer57-Test\Bin`, not the disposable
`C:\hmail-perf-cpp-ascii-20260810\Bin`, so no C++ process was launched. Net10
live measurements remain Net10-only; no speed-up, regression percentage, or
winner is valid. Evidence is in
`hmailserver/source/Server.Net10/PERFORMANCE_COMPARISON_REPORT.md` and
`artifacts/benchmarks/live-cpp-net10-20260811/cpp-preflight-current/`.

## Current Authoritative Continuation (2026-08-11, GLOBAL SMTP RELAYER RUNTIME)

Code/test commit `a0fc76a99` connects the persisted global SMTP relayer to
ordinary Net10 outbound delivery. Legacy `ServerTargetResolver::Resolve` and
`GetFixedSMTPHostForDomain_` (`source/Server/SMTP/ServerTargetResolver.cpp:38-116,
170-237`) use forced route, domain route, global relayer, then MX precedence.
Net10 `SqlServerDeliveryTargetResolver` now reads the existing relayer settings,
decrypts the legacy password only for an authenticated non-empty username,
defaults port `0` to `25`, and fails closed for invalid security or credential
decryption. Installed COM identity, route precedence, and SMTP listener behavior
remain unchanged.

Focused coverage is `19/19`; full Net10 is `2155 passed, 54 skipped, 0 failed`.
The current endpoint contract cannot safely model legacy `|`-separated relayer
host failover, so those values fail closed and remain a documented gap. Real
SQL/socket/TLS/authentication evidence is unavailable because no approved
disposable SQL target and loopback relay fixture are configured. The paired
C++/.NET10 performance gate remains **RED**. Next slice: authenticated
`Settings.SetSMTPRelayerPassword` persistence parity; then multi-host failover
and the disposable SQL/socket acceptance matrix.

## Current Authoritative Continuation (2026-08-11, ORDINARY-MX SMTP SECURITY)

Code/test commit `921f31064` carries the persisted global
`SmtpDeliveryConnectionSecurity` value from
`SqlServerDeliveryTargetResolver` into ordinary-MX `DeliveryTarget` records;
`RemoteSmtpEndpointResolver` maps values `0..3`. Legacy behavior is anchored by
`ServerTargetResolver::Resolve` and `ExternalDelivery::DeliverToSingleServer_`
(`source/Server/SMTP/ServerTargetResolver.cpp:104-106`,
`source/Server/SMTP/ExternalDelivery.cpp:373-392`). Route and forced-route
security/authentication are unchanged. Unknown global values fail closed.

The SMTP client now preserves plaintext only for optional STARTTLS when the
server does not advertise STARTTLS and the endpoint has no authentication.
STARTTLS rejection may retry once without TLS only for that unauthenticated
optional case; TLS handshake/certificate failures, authenticated endpoints,
required STARTTLS, and implicit SSL do not downgrade. This is a deliberate
security divergence from legacy optional-handshake plaintext retry and requires
product/security disposition before release.

Focused delivery/resolver coverage is `21 passed, 0 failed`; full Net10 is
`2147 passed, 54 skipped, 0 failed`. Real disposable SQL-to-MX/socket evidence
is still missing. Configured SMTP relayer parity remains a separate open gap;
the paired C++ matrix, service/out-of-process COM, restore/rollback,
migration/installer, SEC-18, AD/DC, and long-soak gates remain RED or
environment-blocked. Next: approved disposable SQL/socket matrix, then the
legacy-first SMTP relayer slice.

## Current Authoritative Continuation (2026-08-11, SMTP SECURITY SQL EVIDENCE HARNESS)

Code/test commit `81b77ac35` adds
`SqlServerSettingsAdministrationStoreSmtpConnectionSecurityIntegrationTests`.
The test is destructive only against an explicitly approved local SQL/LocalDB
target: it requires
`HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` and
`HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1`, rejects
non-local data sources and `AttachDbFilename`, creates a random database,
seeds only the legacy `SmtpDeliveryConnectionSecurity` row, verifies values
`0..3`, verifies missing-row `false`, and drops the database in `finally`.

The focused integration test skipped safely because the approval variables are
unset. Full Net10 is `2133 passed, 54 skipped, 0 failed`; no real SQL mutation
PASS is claimed. The next legacy-first production gap is ordinary MX delivery:
legacy `ServerTargetResolver.cpp:104-106` passes global SMTP connection
security into `SMTPClientConnection`, while Net10
`RemoteSmtpEndpointResolver.cs:60-66` currently uses `None`. Keep that runtime
delivery/TLS change separate from this evidence harness. Performance remains
RED, and service/out-of-process COM, restore/rollback, migration/installer,
SEC-18, AD/DC, and long-soak gates remain open or environment-blocked.

## Current Authoritative Continuation (2026-08-11, SMTP CONNECTION SECURITY ADMIN MUTATION)

Code/test commit `7b3373deb` implements the authenticated
`IInterfaceSettings.SMTPConnectionSecurity` setter (`DispId(92)`). Legacy
`InterfaceSettings::put_SMTPConnectionSecurity`
(`source/Server/COM/InterfaceSettings.cpp:1799-1813`) calls
`SMTPConfiguration::SetSMTPConnectionSecurity`
(`source/Server/SMTP/SMTPConfiguration.cpp:175-184`), which writes
`PROPERTY_SMTPCONNECTIONSECURITY` (`Constants.h:121`) to the existing
`SmtpDeliveryConnectionSecurity` row seeded by `source/DBScripts/CreateTablesMSSQL.sql:934`.
There is no legacy enum-range validation and legacy returns S_OK after the
setter path. Net10 now uses a parameterized one-row update, authenticated
server-admin revalidation, failed-write retention, and post-success snapshot
publication. Installed COM identity, direct activation, SMTP trust behavior,
and runtime TLS reconfiguration are unchanged.

Focused Settings/SQL coverage is `142 passed, 0 failed, 0 skipped`; full Net10
is `2133 passed, 53 skipped, 0 failed`. Performance remains RED because the
registry-isolated C++ matrix is unavailable. Next: fresh legacy-first audit of
one remaining fixed-row Settings mutation, then the registry-isolated C++
matrix and isolated service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-11, MAX ASYNCHRONOUS THREADS ADMIN MUTATION)

Code/test commit `18c3685c8` implements the authenticated
`IInterfaceSettings.MaxAsynchronousThreads` setter (`DispId(88)`). Legacy
`InterfaceSettings::put_MaxAsynchronousThreads`
(`source/Server/COM/InterfaceSettings.cpp:1578-1588`) delegates to
`Configuration::SetAsynchronousThreads`
(`source/Server/Common/Application/Configuration.cpp:569-578`), which writes
the existing `MaxNumberOfAsynchronousTasks` setting row seeded by
`source/DBScripts/CreateTablesMSSQL.sql:918` without validation or live worker
reconfiguration. Net10 now matches that persistence boundary with a
parameterized `hm_settings` update, one-row success requirement, retained
snapshot publication after success, failed-write retention, and live
server-administrator recheck. Installed COM identity and direct activation
boundaries are unchanged.

Focused Settings/SQL coverage is `138 passed, 0 failed, 0 skipped`; full Net10
is `2129 passed, 53 skipped, 0 failed`. Next: fresh legacy-first audit of one
remaining fixed-row Settings mutation, then the registry-isolated C++ matrix
and isolated service/out-of-process COM lifecycle. Performance and release
gates remain RED where previously documented.

## Current Authoritative Continuation (2026-08-11, BOUNDED PROTOCOL SOAK)

Code/test commit `2737ff625` fixes process-resource serialization in
`build/benchmark-net10-live-protocol.ps1`. Against the disposable Net10
SQL/Data pair and the same loopback ports, the bounded run completed 300 SMTP,
300 IMAP, and 300 POP3 sessions (`900/900`, zero errors). p95 latency was
`0.889/13.369/14.791 ms`; process growth was `22,581,248` private bytes,
`144` handles, and `2` threads, with zero readiness/shutdown failures. Evidence
is under `artifacts/benchmarks/live-cpp-net10-20260811/net10-protocol-soak-300/`.

The post-soak read-only pair collector remains `EQUIVALENT_START_STATE` under
`artifacts/benchmarks/live-cpp-net10-20260811/shared-baseline-pair-20260811_1748-after-protocol-soak-300/`.

This is bounded Net10-only resource evidence. The C++ process remains blocked
by the installed Registry32/AppID-isolation preflight, so no speed-up ratio,
regression percentage, or winner is valid. Full Net10 is `2127 passed, 53
skipped, 0 failed`. Next: registry-isolated C++ matrix, isolated Windows
service/out-of-process COM lifecycle, then a dedicated 24-hour soak host.

## Current Authoritative Continuation (2026-08-11, EXTERNAL FETCH ACCEPTANCE)

Code/test commit `fe915d3fb` adds a disposable real TCP/SQL external-fetch
acceptance. Legacy anchors are `ExternalFetchManager::DoWork` /
`FetchIsAllowed_`, `ExternalFetchTask::DoWork`, and `ExternalFetch::Start`; the
Net10 paths are `ExternalFetchProcessor.RunBatchAsync`,
`SqlServerExternalFetchAccountStore`, and `TcpExternalFetchSessionFactory`.

The wrapper ran five successive loopback POP3 snapshots of ten messages using
the disposable pair. Net10 downloaded and accepted `50/50`, left the current
ten UID rows, released the lease on every cycle, and recorded five allowed
egress decisions for explicit `127.0.0.0/8`. Latest cycle p50/p95/p99 was
`23.998/24.229/24.229 ms`; JSON/CSV/Markdown evidence is under
`artifacts/benchmarks/live-cpp-net10-20260811/net10-external-fetch/`. Temporary
SQL fetch rows were cleaned to `0/0`; no message files were persisted.

Full Net10 is `2127 passed, 53 skipped, 0 failed`. The paired performance gate
remains **RED** because the C++ Registry32/AppID-isolation environment is not
available. Next: longer bounded resource-growth soak, then registry-isolated
C++ matrix and isolated Windows service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-11, RESTART LIFECYCLE)

Code/test commit `46db432c6` adds the disposable Net10 restart lifecycle
acceptance. The runner starts the isolated `LiveListenerHost.exe` twice,
proves the launched PID owns loopback SMTP `2525`, IMAP `1143`, and POP3
`25110`, validates SMTP/IMAP/POP3 banners, and verifies all three ports are no
longer owned by that PID after shutdown. The run passed `2/2` cycles with
start-ready p50 `1636.538 ms` and stop p50 `1546.317 ms`. Evidence is under
`artifacts/benchmarks/live-cpp-net10-20260811/net10-restart-lifecycle/`.

The paired read-only post-run collector and validator report
`EQUIVALENT_START_STATE` under
`artifacts/benchmarks/live-cpp-net10-20260811/shared-baseline-pair-20260811_1748-after-restart-lifecycle/`.
COM local server was disabled; this does not prove Windows service or
out-of-process COM lifecycle and changed no production state. Full Net10 is
`2127 passed, 52 skipped, 0 failed`. The paired performance gate remains
**RED** because the C++ registry/AppID-isolation environment is unavailable.
Next: external-fetch soak with bounded resource evidence, then the isolated C++
matrix and Windows service/out-of-process COM lifecycle.

## Current Authoritative Continuation (2026-08-11, POP3 LARGE MAILBOX)

Code/test commit `0ec49598b` adds a real loopback Net10 POP3 large-mailbox
runner. Legacy anchors are `POP3Connection::ProtocolSTAT_`, `ProtocolLIST_`,
`ProtocolUIDL_`, and `ProtocolRETR_`
(`source/Server/POP3/POP3Connection.cpp:606-750,933-956`); Net10 uses the
matching `Pop3Session` handlers and SQL mailbox/file stores.

Against the disposable pair’s 1,000-message `test@perf.test` mailbox, five
sessions passed `STAT`, `LIST`, `UIDL`, and `RETR 1`, with `1000/1000` SQL
mailbox rows after the run. Total p50/p95/p99 was
`54.757/290.599/333.589 ms`; LIST p50/p95 `14.963/56.093 ms`, UIDL p50
`15.060 ms`, RETR p50 `1.466 ms`. Evidence is under
`artifacts/benchmarks/live-cpp-net10-20260811/net10-pop3-large-mailbox/`.

Full Net10 remains `2127 passed, 52 skipped, 0 failed`. The paired performance
gate remains **RED** because the C++ registry/AppID-isolation environment is
not available. Next: disposable service restart/COM lifecycle, then
external-fetch soak and bounded resource-leak evidence. Do not push.

## Current Authoritative Continuation (2026-08-11, DELIVERY QUEUE ACCEPTANCE)

Code/test commit `7d2aecdc0` completed the next disposable delivery slice.
Legacy anchors are `LocalDelivery::Perform` /
`LocalDelivery::DeliverToLocalAccount_` and
`PersistentMessage::CopyFromQueueToInbox`
(`source/Server/SMTP/LocalDelivery.cpp:60-112,270-317`), plus
`ExternalDelivery::RescheduleDelivery_` / `GetRetryOptions_`
(`source/Server/SMTP/ExternalDelivery.cpp:496-688`) and
`PersistentMessage::SetNextTryTime`
(`source/Server/Common/Persistence/PersistentMessage.cpp:670-695`).

The real SQL/Data acceptance processed 50 local queue messages to Inbox
(`50/50`) at `73.308` messages/s with p50/p95/p99 batch latency
`4.376/8.405/48.484 ms`. A controlled transient target verified SQL defer:
the queue row remained type 1, unlocked, retry count 1, lease owner null,
next-try in the future, and recipient retained. The Turkish-collation Inbox
lookup gap was fixed with `UPPER(foldername) = N'INBOX'` in
`SqlServerLocalDeliveryStore`; focused and live tests pass. Evidence is under
`artifacts/benchmarks/live-cpp-net10-20260811/net10-live-delivery-queue/` and
the post-run pair remains `EQUIVALENT_START_STATE`.

Full Net10: `2127 passed, 52 skipped, 0 failed`. The paired performance gate
remains **RED**: the C++ runner is registry/AppID-isolation blocked, so no
ratio, regression, or winner is valid. Next: registry-isolated C++ matrix
when available; otherwise Net10 POP3 large-mailbox/external-fetch soak, then
service restart/COM lifecycle and leak evidence. No production state changed;
do not push.

## Current Authoritative Continuation (2026-08-11, LIVE FTS SEARCH / EQUAL LIVE FIXTURE)

Code/test commit `eb0c9a7ed` parameterizes the live Net10 runners for the
fresh disposable pair and adds opt-in SQL SMTP host/queue diagnostics. The
pair now has equal 37-table SQL schemas, equal row counts, 1,000 identical
message files/Data SHA-256, matching Inbox/domain/account/ports, and Full-Text
catalog/index readiness. Evidence is
`artifacts/benchmarks/live-cpp-net10-20260811/shared-baseline-pair-20260811_1748-post-pop3-fixed/`.

Net10 results are SMTP acceptance `25/25` PASS, SMTP/IMAP/POP3 protocol
`25/25` PASS, 1,000 concurrent IMAP `1000/1000` PASS, and live IMAP Full-Text
SEARCH `25/25` PASS with 1,000 matches per session. POP3 required a
bounded production fix: `SqlServerPop3MailboxStore.ListMessagesAsync` now
reads selected SQL columns in ordinal order under `SequentialAccess`. The
focused disposable SQL diagnostic, FTS backfill/search diagnostic, and updated
Release host all pass. The FTS benchmark reports SEARCH p50/p95/p99 of
`7.900/12.802/21.557 ms`.
The C++ process was not launched because the read-only Registry32 preflight
does not resolve the disposable Bin and legacy `/Debug` would write the
installed AppID registration. The performance gate remains **RED** and no
ratio or winner is valid.

The full Net10 suite is `2127 passed, 51 skipped, 0 failed`. The detailed
measurements/charts are in
`hmailserver/source/Server.Net10/PERFORMANCE_COMPARISON_REPORT.md`. Next
slice: reproduce the same workload in a registry-isolated C++ VM, then measure
disposable delivery/queue throughput and retry/defer behavior. No production
service, database/Data directory, COM identity, DCOM ACL, or public listener
changed.

## Current Authoritative Continuation (2026-08-11, SHARED BASELINE V2 FIXTURE/FTS GATE)

Tool commit `7e58324d7` upgrades
`build/collect-live-equivalence-evidence.ps1` to shared-baseline v2 and adds
`build/test-live-equivalence-evidence.ps1`. The read-only collector now
records exact disposable fixture shape, Data filename containment, and SQL
Full-Text service/catalog/table/index readiness for both databases. The
focused validator proves missing Full-Text evidence is rejected.

The live collector rerun reported `NOT_EQUIVALENT`: both databases have the
expected domain/account and three loopback listener rows, but Inbox matches
are `0`, message files under the selected Data roots are `0` with
`1000`/`1029` outside, and Full-Text is not ready on either side. This closes
the earlier false confidence from row-count/Data-hash equality alone. The
paired performance gate remains **RED** and no ratio is valid.

Next slice: recreate fresh equal disposable SQL/Data/message roots with
correct Inbox/file provenance, enable/verify Full-Text and index the same
corpus, then rerun acceptance/load scenarios. No production DB/Data/service,
COM registration, or C++ process was touched. Do not push.

## Current Authoritative Continuation (2026-08-11, EXACT SMTP FIXTURE/PATH GATE)

Code/test commit `35f1f87e0` tightens
`build/benchmark-net10-live-smtp-acceptance.ps1` and its validator. Reports
now require the exact active domain/account/Inbox, all three loopback
`hm_tcpipports` rows, every message filename below the selected Data root, and
bounded SQL evidence that each accepted message became queued or delivered.
Legacy anchors are `SMTPConnection::HandleSMTPFinalizationTaskCompleted_`
(`source/Server/SMTP/SMTPConnection.cpp:980`), `PersistentMessage::AddObject`,
`SaveRecipients_`, and `source/DBScripts/CreateTablesMSSQL.sql:258-353`.

The current Net10 diagnostic accepted `1/1` and observed one new queued row,
but the exact fixture gate is **FAIL**: Inbox matching is `0`, Data-root
matching is `0`, and outside-root filenames are `1028` before / `1029` after.
C++ preflight refused before process creation because Registry32 still points
at the installed test Bin directory. Full Net10 remains `2127 passed, 46
skipped, 0 failed`. The paired performance gate remains **RED**; no ratio or
winner is valid. Next: recreate fresh equal disposable SQL/Data/message roots,
verify SQL FTS, then rerun the same workload. Do not push.

## Current Authoritative Continuation (2026-08-11, PAIRED REPORT EVIDENCE GATE)

Code/test commit `fdfa2e831` hardens
`build/generate-live-comparison-report.ps1` and adds
`build/test-net10-live-comparison.ps1`. The generator now accepts explicit
Net10/C++/corpus report paths, rejects preflight-less C++ input, requires C++
executable provenance, and refuses non-identical Data evidence. It no longer
asserts SQL row-count equality without a supplied SQL evidence artifact;
`sameSqlRowCounts` is explicitly `false` and the validator preserves the RED
decision with `ratio_valid=false` for every protocol row.

The explicit safe-input report is under
`artifacts/benchmarks/live-cpp-net10-20260811/comparison-preflight-evidence-20260811/`;
the default stale C++ comparison input is rejected. Focused generator parse,
stale-input rejection, paired validator, and full Net10 tests pass
(`2127 passed, 46 skipped, 0 failed`). The performance gate remains **RED**.
Next slice: fresh disposable SMTP acceptance evidence with fixture identity,
SQL/message counts, and post-run mailbox/queue/Data accounting; do not launch
C++ on this host and do not push.

## Current Authoritative Continuation (2026-08-11, CONCURRENT IMAP ISOLATION PREFLIGHT)

Code/test commit `e2ffb0ad8` applies the shared
`Get-CppIsolationPreflight`/`Get-CppExecutableProvenance` contract to
`build/benchmark-net10-live-concurrent-imap.ps1` and extends its validator.
The C++ preflight report records Registry32 `C:\hMailServer57-Test\Bin` versus
the disposable target `C:\hmail-perf-cpp-ascii-20260810\Bin`, so the C++
process was not launched and the 1,000-session workload did not start.

Parity references are `IOService::DoWork()` and `TCPServer::Run()`/
`HandleAccept()` (`source/Server/Common/TCPIP/IOService.cpp:65-134`,
`source/Server/Common/TCPIP/TCPServer.cpp:51-226`): the SQL `hm_tcpipports`
rows define the disposable 2525/1143/25110 ports, and launched-PID ownership
plus banners are required before IMAP concurrency begins.

The explorer also confirmed that legacy `/Debug` calls
`ChMailServerModule::RegisterAppID()`, which writes AppID registration before
the debug server starts (`source/Server/hMailServer/hMailServer.cpp:136-162,
192-197`). This is why the actual C++ benchmark remains environment-blocked
on this host even though the preflight is read-only. Full Net10 remains
`2127 passed, 46 skipped, 0 failed`; paired performance remains **RED** and no
ratio is valid. Next independent work is a separate registry-isolated C++ VM,
then fresh equal SQL/Data/message fixtures. Do not push.

## Current Authoritative Continuation (2026-08-11, C++ PROTOCOL PROVENANCE PREFLIGHT)

Code/test commit `f6d06e216` applies the shared read-only C++ registry/config
and service preflight to `build/benchmark-net10-live-protocol.ps1`, while the
SMTP acceptance runner now consumes the same helper. C++ reports include the
target executable path, SHA-256, byte length, and UTC write time; the new
`build/test-net10-live-protocol.ps1` rejects reports without that isolation
evidence or with unreconciled readiness/sample accounting.

Legacy anchors are `Utilities::GetBinDirectory()`
(`source/Server/Common/Util/Utilities.cpp:101-119`) and
`IniFileSettings::GetInitializationFile()`
(`source/Server/Common/Application/IniFileSettings.cpp:245-260`). The current
Registry32 value resolves to `C:\hMailServer57-Test\Bin`, so the target
`C:\hmail-perf-cpp-ascii-20260810\Bin` was refused before `Start-Process`.
The resulting FAIL report is under
`artifacts/benchmarks/live-cpp-net10-20260811/cpp-protocol-preflight-fail-20260811/`.

Focused PowerShell parse, C++ protocol validator, and SMTP validator pass; the
full Net10 suite is `2127 passed, 46 skipped, 0 failed`. The paired performance
gate remains **RED** and no ratio or winner is claimed. Next slice: extend the
same preflight/report contract to `build/benchmark-net10-live-concurrent-imap.ps1`,
then rerun only after obtaining an independently isolated C++ installation and
recreating equal SQL/Data/message roots. Do not push.

## Current Authoritative Continuation (2026-08-11, C++ ISOLATION PREFLIGHT)

Code/tool commit `6cc893f35` adds a fail-closed preflight to the isolated C++
SMTP acceptance runner. Legacy `Utilities::GetBinDirectory()` reads
`HKLM\SOFTWARE\hMailServer\InstallLocation` before falling back to the
executable path (`source/Server/Common/Util/Utilities.cpp:101-119`), and
`IniFileSettings::GetInitializationFile()` derives the INI from that location
(`source/Server/Common/Application/IniFileSettings.cpp:245-260`).

The live host preflight found `Registry32` pointing to
`C:\hMailServer57-Test\Bin`, while the requested disposable target is
`C:\hmail-perf-cpp-ascii-20260810\Bin`. The runner refused to launch the C++
process and recorded the exact result under
`artifacts/benchmarks/live-cpp-net10-20260811/cpp-preflight-fail-20260811/`.
The service definition is present but stopped; no machine or production state
was changed. PowerShell parse and the acceptance validator pass.

The paired performance gate remains **RED**. Next prerequisite is a separate
staging VM or a separately isolated legacy installation whose registry/config
resolution, SQL/Data/message roots, SMTP/IMAP/POP3 listeners, and service
identity can be proven independently. Do not push.

## Current Authoritative Continuation (2026-08-11, FRAGMENTED SMTP DATA PARITY)

Code/test commit `8f9eb3655` fixes the network-fragmented DATA line progression
in `Server.Net10` `LineProtocolReader.ReadLineAsync` by re-examining the
consumed cursor. The legacy reference is
`SMTPConnection::ParseData(ByteBuffer)` plus
`TransparentTransmissionBuffer::Append`, `Flush`, and
`RemoveTransmissionPeriod_`, followed by
`SMTPConnection::HandleSMTPFinalizationTaskCompleted_`.

`SmtpTcpListenerTests.RunAsync_StagesFragmentedDataUntilTerminatorAndQueuesAfterReceiverRelease`
passes on the real loopback listener and verifies fragmented body staging,
dot-unstuffing, delayed receiver invocation, `250 Queued`, and `221`.
Focused listener/protocol tests are `11/11`; the full Net10 suite is
`2127 passed, 46 skipped, 0 failed`.

The isolated Net10 SMTP acceptance run is `25/25` with p50 `4.053 ms`, p95
`7.176 ms`, and p99 `219.175 ms`, diagnostic only. The isolated C++ target
still fails SMTP readiness with an empty banner and lacks the paired POP3
listener. Successful Net10 samples mutated the disposable fixture, so both
SQL/Data roots must be recreated before the next paired run. The performance
gate remains **RED**; no ratio or winner is valid.

Next slice: repair or replace the isolated C++ SMTP/IMAP/POP3 target, provision
disposable SQL Full-Text Search, recreate the equal fixture, and rerun the
identical protocol/load matrix. Do not push.

## Current Authoritative Continuation (2026-08-11, SMTP MESSAGE ACCEPTANCE HARNESS)

Code/tool commit `b34b2b415` adds the loopback disposable
`build/benchmark-net10-live-smtp-acceptance.ps1` runner and
`build/test-net10-live-smtp-acceptance.ps1` validator. It measures the full
SMTP acceptance transaction and emits JSON/CSV/Markdown. Parser and report
validation pass. Fresh smoke evidence is **FAIL** for both implementations:
Net10 `0/1` reaches `354` but never returns final `250`, and C++ `0/1` fails
SMTP readiness. This is blocker evidence, not performance data; no ratio or
winner is valid.

Next slice: provision an approved SQL Server with Full-Text Search and repair
or replace the legacy C++ listener target, then rerun the same message,
protocol, queue, and concurrency matrix. Do not push.

## Current Authoritative Continuation (2026-08-11, RESTART RECOVERY GATE)

Code/test commit `55f252fb3` extends the ambiguous full-restore test so the
startup gate calls `BackupRestoreRecoveryJournal.EnsureNoPendingRecovery`
against the preserved journal. It rejects the pending manual-recovery state
before a new mutation and leaves the new Data target intact. This is bounded
recovery-reader evidence, not process-kill/power-loss evidence.

Focused restore coverage remains `20 passed, 0 failed`; default full Net10 is
`2126 passed, 46 skipped, 0 failed`. The next gate is environment-dependent:
an approved SQL Server with Full-Text Search and a normal legacy C++ binary
with SMTP/IMAP/POP3 listeners. Until both exist, the paired performance gate
is **RED** and no ratio or winner may be claimed. Do not push.

## Current Authoritative Continuation (2026-08-11, AMBIGUOUS FULL RESTORE COMMIT)

Code/test commit `8ebace0de` adds
`RestoreExecutor_PreservesJournalWhenFullRestoreCommitOutcomeIsAmbiguous`.
On the disposable SQL/Data target, the transaction performs the real SQL
commit and then reports an error. The full restore executor fails closed and
preserves a `MetadataCommitStarted` journal, the new Data target, and the
rollback artifact for manual recovery. This proves ambiguous-commit evidence,
not an actual process-kill or power-loss drill.

Focused restore coverage is `20 passed, 0 failed`; disposable SQL/opt-in/
native-registry categories are `55 passed, 0 skipped, 0 failed`; default full
Net10 is `2126 passed, 46 skipped, 0 failed`. The performance gate remains
**RED** because C++ POP3 readiness and Net10 FTS-backed IMAP SEARCH are still
not valid for the paired workload, so no ratio or winner is claimed.

Next slice: add a bounded restart/recovery-reader acceptance around the
preserved journal, then repair the C++/Net10 live protocol pair. Do not push.

## Current Authoritative Continuation (2026-08-11, REAL BACKUP/RESTORE CHAIN)

Code/test commit `0d03adfac` adds the disposable SQL/Data acceptance test
`BackupManager_StartBackupLoadBackupAndRestoreRoundTripsRealArchive` in
`BackupRestoreRoundTripIntegrationTests`. It drives the authenticated
`BackupManager.StartBackup` queue, real `SevenZipBackupArchiveRuntime`
archive/DataBackup creation, `LoadBackup`, and the real
`MetadataBackupRestoreExecutor` through queued restore. The test verifies
settings/domain replacement, raw message-file staging, and cleanup of the
old Data root. No production code, installed COM identity, service, or
production SQL/Data state changed.

Focused restore coverage is `19 passed, 0 failed`; disposable SQL/opt-in/
native-registry categories are `54 passed, 0 skipped, 0 failed`; default full
Net10 is `2126 passed, 45 skipped, 0 failed`. The performance gate remains
**RED**: C++ lacks the required POP3 listener, Net10 FTS-backed IMAP SEARCH
is not accepted on the current disposable SQL instance, and no ratio or
winner is valid.

Next slice: add isolated crash/ambiguous-commit recovery acceptance for the
full restore journal, then repair the C++/Net10 live protocol pair before
adding SMTP message-acceptance and delivery-queue scenarios. Do not push.

## Current Authoritative Continuation (2026-08-11, PERFORMANCE EVIDENCE HARDENING)

Code/tool commit `c91323197` makes the shared baseline collector fail closed
when `sqlcmd` cannot open a requested disposable database, fixes the concurrent
IMAP probe's host-compiler compatibility, and derives the latency chart from
current JSON rather than fixed historical values. The current disposable
baseline is `33/33` SQL table row counts and `1000/1000` Data file hashes.

The live paired gate remains **RED**: C++ POP3 readiness fails, Net10 SMTP is
`25/25` but IMAP/POP3 are `0/25`, and the 1,000-session run has no successful
IMAP sessions (`0/1000` C++ started, `0/1000` Net10 successes). The offline
Net10-only 100k SEARCH/SORT gate passes at p50 `7.101 ms`, p95 `9.734 ms`, and
p99 `9.784 ms`; no C++ ratio or winner is valid. Evidence is under
`artifacts/benchmarks/live-cpp-net10-20260811/`.

Next slice: complete the real isolated backup restore executor, then repair the
C++/Net10 live protocol pair before adding message-acceptance and delivery
throughput scenarios. Do not push.

## Current Authoritative Continuation (2026-08-11, RAW DATABACKUP STAGING HARDENING)

Code/test commit `73405caa1` makes raw DataBackup staging reject reparse-point
entries before copying and removes partial raw staging after archive failure or
cancellation. Legacy behavior is anchored at
`BackupExecuter::BackupDataDirectory_` (`source/Server/Common/Application/
BackupExecuter.cpp:96-211`). Focused coverage is `46 passed, 1 skipped, 0
failed`; full default Net10 is `2125 passed, 43 skipped, 0 failed`. The skip is
the host's inability to create a symbolic link, not a production pass claim.

Next slice: fail closed in the paired benchmark collector when SQL access fails
and make the latency graph data-driven. Performance remains **RED**.

## Current Authoritative Continuation (2026-08-11, COMPOSED MODE-7 DISPATCH)

Code/test commit `149770381` adds `BackupManagerMode7DispatchTests`, which
drives the real `BackupManager.StartBackup` queue through archive creation,
`LoadBackup`, all three restore flags, and queued `StartRestore` option `7`.
It proves 7z/DataBackup creation and dispatch ordering, but its restore
executor is a recording test double; real SQL/Data mutation, reinitialize, and
rollback remain open. Focused coverage is `1 passed, 0 failed`; full default
Net10 is `2124 passed, 42 skipped, 0 failed`. Performance remains **RED**.

Next slice: reject reparse points during raw DataBackup staging and remove
failed-run staging residue. Do not push.

## Current Authoritative Continuation (2026-08-11, FULL SETTINGS/DOMAIN/MESSAGE RESTORE)

Code/test commit `563cd0042` adds the legacy `BOSettings|BODomains|BOMessages`
restore combination. Legacy `BackupExecuter::StartRestore` accepts option `7`
and restores domains, Data, messages, then settings
(`source/Server/Common/Application/BackupExecuter.cpp:230-388`,
`source/Server/Common/Application/Configuration.cpp:716-760`). Net10 now
stages Data, removes domains/public folders in one SQL transaction, restores
settings and populated message metadata, and passes
`commitOutcomeMayBeAmbiguous: true` for full restores so a lost SQL commit
response leaves the recovery journal for manual reconciliation instead of
silently restoring stale Data.

Focused restore coverage is `19 passed, 0 failed`; opt-in
`BackupRestoreRoundTripIntegrationTests` is `17 passed, 0 failed`; fresh full
Net10 isolated-create opt-in is `2163 passed, 2 skipped, 0 failed`. The test
fixture uses a hand-built archive and configured local SQL endpoint, not a
production database/Data directory; independent disposable-instance proof,
real `StartBackup`, existing-state/public-folder round trip, and crash/power-
loss evidence remain open. Slice verdict: **YELLOW**; project: **RED**. Next
slice: true isolated `StartBackup -> LoadBackup` populated round trip. Do not
push.

## Current Authoritative Continuation (2026-08-11, WELCOMESMTP SQL CAPACITY PARITY)

Code/test commit `e3434d4b1` changes the Net10 `WelcomeSMTP` SQL parameter
metadata from `nvarchar(255)` to `nvarchar(4000)`, matching the legacy
`hm_settings.settingstring nvarchar(4000)` definition
(`source/DBScripts/CreateTablesMSSQL.sql:299-303`). Legacy long-string behavior
is anchored in `Property::SetString`, `SQLStatement`, and `ADOConnection`
(`source/Server/DBOperation/Property.cpp:43-47,81-96`,
`source/Server/DBOperation/SQLStatement.cpp:40-67,222-257`,
`source/Server/DBOperation/ADOConnection.cpp:449-499`); COM remains the
installed BSTR/DISPID 23 contract at `InterfaceSettings.cpp:696-710`.

`SqlServerSettingsAdministrationStoreWelcomeSmtpIntegrationTests` creates a
random database on the configured local SQL endpoint, writes a 300-character
WelcomeSMTP, reads the exact value back, and drops the database. Focused store
coverage is `33 passed, 0 failed`; full default Net10 is `2123 passed, 40
skipped, 0 failed`; fresh isolated-create MSSQL/Data opt-in is `2161 passed, 2
skipped, 0 failed`. It targets no named hMailServer production database or
Data directory, but independent proof that the SQL instance is disposable is
still an environment gate. Performance remains **RED** because
the paired C++/.NET10 SMTP/IMAP/POP3 workload is incomplete, so no ratio or
winner is valid. Next slice: populated disposable settings/message restore and
rollback acceptance. Do not push.

## Current Authoritative Continuation (2026-08-11, WELCOMESMTP CRLF HARDENING)

Code/test commit `a414c88db` adds a bounded security hardening slice for the
legacy `WelcomeSMTP` BSTR/DISPID 23 path. Legacy
`InterfaceSettings::put_WelcomeSMTP` and `SMTPConfiguration::SetWelcomeMessage`
accept the value unchanged (`source/Server/COM/InterfaceSettings.cpp:696-710`,
`source/Server/SMTP/SMTPConfiguration.cpp:120-123`), and
`SMTPConnection::SendBanner_` frames it directly
(`source/Server/SMTP/SMTPConnection.cpp:167-185`). Net10 now rejects CR/LF
before SQL/publication with `E_INVALIDARG` while retaining valid legacy
formatting; `SmtpSession.GetGreeting` also fails safe for unsafe pre-existing
rows. Installed COM identity, direct activation, and SMTP trust boundaries are
unchanged.

Focused coverage is `136 passed, 0 failed`; full default Net10 is `2123
passed, 39 skipped, 0 failed`; fresh disposable MSSQL/Data opt-in is `2160
passed, 2 skipped, 0 failed`. The legacy C++ setter remains raw by design;
the .NET10 rejection is an intentional security divergence requiring
release-policy acceptance. Performance remains **RED** because the C++ and
Net10 live SMTP/IMAP/POP3 workload pair is incomplete. Next slice: repair or
replace the isolated protocol target, then rerun the identical SQL/Data/message
and loopback matrix. Do not push.

## Current Authoritative Continuation (2026-08-11, BOOTSTRAP SMTP GREETING)

Code/test commit `7a7e4b77b` completes one legacy-first parity slice. Legacy
`Application::InitInstance` / `Configuration::Load` load persisted properties
before protocol startup (`source/Server/Common/Application/Application.cpp:108`,
`source/Server/Common/Application/Configuration.cpp:56`), while
`SMTPConnection::SendBanner_` reads `SMTPConfiguration::GetWelcomeMessage`
(`source/Server/SMTP/SMTPConnection.cpp:167-205`). Net10 now loads the
configured settings snapshot in `SettingsAdministrationRuntimeHost.Configure`
and publishes `WelcomeSmtp` before SMTP use, including startup with no COM
Settings access.

Focused COM coverage is `158 passed, 0 failed`; full default Net10 is `2120
passed, 39 skipped, 0 failed`. The fresh disposable MSSQL/Data opt-in baseline
remains `2156 passed, 2 skipped, 0 failed`. No installed COM identity, direct
activation boundary, SMTP trust behavior, or live policy reload was changed.

Performance remains **RED**. The isolated C++ target still lacks the required
POP3 listener and Net10 live IMAP/POP3 probes do not complete; no ratio or
winner is valid. Next slice: repair or replace the isolated protocol target,
then rerun the identical C++/.NET10 SQL/Data/message and loopback matrix.
Separate security follow-up: review CR/LF sanitization for administrator-
controlled `WelcomeSMTP` before SMTP framing. It was not changed in this
legacy-parity slice. Do not push.

## Current Authoritative Continuation (2026-08-11, LIVE PROTOCOL HARNESS FAIL-CLOSED)

Code/test commit `2fe577f62` hardens the live protocol benchmark scripts with
listener readiness, launched-PID ownership, SMTP/IMAP/POP3 banner probes,
clean-shutdown waits, and a 1,000-session IMAP start barrier. Parser checks,
embedded IMAP C# compilation, and the concurrent artifact validator pass.
Fresh failure-path evidence is under
`artifacts/benchmarks/live-cpp-net10-20260811/harness-full-*`:

- C++ readiness fails because POP3 does not listen on `127.0.0.1:25110`.
- Net10 protocol readiness passes, but SMTP is `25/25` and IMAP/POP3 are
  `0/25`.
- Net10 concurrent IMAP completes `1000` probes with `0` successes.

The performance gate stays **RED**. No ratio or winner is valid. Default full
Net10 is `2119 passed, 39 skipped, 0 failed`; fresh disposable MSSQL opt-in is
`2156 passed, 2 skipped, 0 failed`. Next slice: healthy isolated C++ binary
plus reproducible Net10 live IMAP/POP3 path, then rerun the identical matrix.
Do not push.

## Current Authoritative Continuation (2026-08-11, DISPOSABLE SQL OPT-IN GREEN)

Code/test commit `8972eb9d4` repairs the isolated SQL evidence fixtures and a
real `SequentialAccess` reader-order defect in
`SqlServerWhiteListAddressAdministrationStore.GetWhiteListAddressesAsync`.
Legacy `hm_imapfolders.folderid/folderparentid` and
`hm_messages.messagefolderid` are `int` in
`source/DBScripts/CreateTablesMSSQL.sql:261,355-359`; the public import fixture
now matches that contract. The retained-folder test now counts the intentionally
undeleted `Live` folder, and the domain/account/fetch fixture supplies every
non-null message column used by the SQL path. Cache runtime state is reset at
the isolated fixture boundary.

Focused SQL/IMAP/COM integration coverage is `16/16`; whitelist store coverage
is `11/11`. The full opt-in MSSQL disposable run is **2156 passed, 2 skipped,
0 failed**. The two skips are the explicit installer artifact and native
registry integration gates. No production database, Data directory, service,
COM registration, or DCOM ACL was changed.

The performance gate remains **RED**. The paired start state is equivalent by
33/33 row counts and 1,000/1,000 Data-file hashes, but C++ protocol probes did
not complete and the collector does not prove row-content equivalence. No
speed-up ratio or performance winner is valid. Next slice: repair or replace
the isolated C++/Net10 protocol target and rerun the identical loopback matrix.
Do not push.

## Current Authoritative Continuation (2026-08-11, SHARED SQL/DATA PERFORMANCE BASELINE)

Code/tool commit `8558b7a44` adds
`build/collect-live-equivalence-evidence.ps1`. A disposable C++ SQL backup was
restored to both targets; the collector verified 33/33 matching table row
counts and two 1,000-file Data trees with zero path/SHA-256 mismatches. Both
implementations then used loopback `127.0.0.1` on SMTP `2525`, IMAP `1143`, and
POP3 `25110`.

The shared-baseline protocol result is still **RED**: C++ was `0/25` for SMTP,
IMAP, and POP3; Net10 was SMTP `25/25`, IMAP `0/25`, POP3 `0/25`; 1,000-session
IMAP was `0/1000` for both. No speed-up or performance winner is valid. JSON
and Markdown evidence are under
`artifacts/benchmarks/live-cpp-net10-20260811/`; the SQL backup is outside the
repository under the isolated C++ staging directory and was not committed.

Next slice: repair or replace the isolated C++/Net10 protocol target so both
complete the same SMTP/IMAP/POP3 acceptance matrix. Do not push.

## Current Authoritative Continuation (2026-08-11, SMTP GREETING RUNTIME PROPAGATION)

Code/test commit `c26479d9b` wires legacy `WelcomeSMTP` formatting into the
Net10 SMTP session. Legacy anchors are
`source/Server/SMTP/SMTPConnection.cpp:166-205`,
`source/Server/SMTP/SMTPConfiguration.cpp:113`,
`source/Server/COM/InterfaceSettings.cpp:679-696`, and
`source/Server/hMailServer/hMailServer.idl:547` (`DispId(23)`). Empty values
use the machine name and `ESMTP`; custom values receive `ESMTP` unless already
terminated with that suffix.

The implementation is in `SmtpSession.GetGreeting`, `Host.Build`, and
`SettingsAdministrationRuntimeHost`; successful authenticated
`Settings.WelcomeSMTP` updates publish the retained runtime value. Focused
tests are `135/135`; full unfiltered Net10 is `2118 passed, 39 skipped,
0 failed`. No installed COM identity, direct activation boundary, SMTP trust
behavior, or broader live policy reload changed.

Performance remains **RED**. The current C++/.NET10 artifacts are diagnostic
only: they do not prove identical SQL/Data state and message corpus, and C++
validated `0/1000` concurrent IMAP sessions while Net10 validated `1000/1000`.
No speed-up claim is permitted. Next slice: legacy-first audit and runtime
propagation for one remaining SMTP policy setting. Do not push.

## Current Authoritative Continuation (2026-08-11, MAXIMUM INVALID COMMANDS AUTHORIZATION LEASE)

Code/test commit `0abe45705` adds the existing generation-bound authorization
lease to authenticated `Settings.MaxNumberOfInvalidCommands` (`DispId(65)`).
The lease spans the existing `maximumincorrectcommands` SQL mutation, result
handling, and retained snapshot publication. Focused settings/store coverage
is `134/134`; full unfiltered Net10 is `2117 passed, 39 skipped, 0 failed`.

Legacy anchors: `InterfaceSettings::get/put_MaxNumberOfInvalidCommands`
(`source/Server/COM/InterfaceSettings.cpp:1695-1727`),
`Configuration::Get/SetMaximumIncorrectCommands`
(`source/Server/Common/Application/Configuration.cpp:501-509`),
`PROPERTY_SMTPMAXINCORRECTCOMMANDS`
(`source/Server/Common/Application/Constants.h:90`), installed Settings IID
and `DispId(65)` (`source/Server/hMailServer/hMailServer.idl:520-528,612-613`),
the MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:866`), and the legacy
5xx threshold path (`source/Server/SMTP/SMTPConnection.cpp:2207-2221`).

Focused tests cover lease acquire/dispose, unavailable-lease denial before
mutation, and reauthentication blocking during an in-flight mutation. Live
SMTP settings reload remains a separate parity gap. Existing paired
performance evidence is diagnostic only: Net10 `1000/1000`, C++ `0/1000` for
concurrent IMAP, so performance remains **RED** and no ratio is valid. Next
slice: legacy-first audit of live SMTP greeting/settings propagation. Do not
push.

## Current Authoritative Continuation (2026-08-11, DISCONNECT INVALID CLIENTS AUTHORIZATION LEASE)

Code/test commit `bb20cb736` adds the existing generation-bound authorization
lease to authenticated `Settings.DisconnectInvalidClients` (`DispId(64)`).
The lease spans the existing `disconnectinvalidclients` SQL mutation, result
handling, and retained snapshot publication. Focused settings/store coverage
is `131/131`; full unfiltered Net10 is `2114 passed, 39 skipped, 0 failed`.

Legacy anchors: `InterfaceSettings::get/put_DisconnectInvalidClients`
(`source/Server/COM/InterfaceSettings.cpp:1661-1693`),
`Configuration::Get/SetDisconnectInvalidClients`
(`source/Server/Common/Application/Configuration.cpp:488-498`),
`PROPERTY_SMTPDISCONNECTINVALIDCLIENTS`
(`source/Server/Common/Application/Constants.h:89`), installed Settings IID
and `DispId(64)` (`source/Server/hMailServer/hMailServer.idl:520-528,610-611`),
the MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:862-866`), and the
legacy invalid-command disconnect path
(`source/Server/SMTP/SMTPConnection.cpp:2210-2220`).

Focused tests cover lease acquire/dispose, unavailable-lease denial before
mutation, and reauthentication blocking during an in-flight mutation. Live
SMTP policy reload remains a separate parity gap. Existing paired benchmark
evidence was validated: Net10 completed 1,000 concurrent IMAP sessions, C++
completed 0/1,000, so no ratio or speed-up claim is valid and performance is
still **RED**. Next slice: fresh legacy-first audit of
`Settings.MaxNumberOfInvalidCommands`. Do not push.

## Current Authoritative Continuation (2026-08-11, MAX SMTP RECIPIENTS IN BATCH AUTHORIZATION LEASE)

Code/test commit `77ea84fb9` extends the existing generation-bound
authorization lease to authenticated
`Settings.MaxSMTPRecipientsInBatch` (`DispId(62)`). The lease spans the
existing parameterized `maxsmtprecipientsinbatch` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_MaxSMTPRecipientsInBatch`
(`source/Server/COM/InterfaceSettings.cpp:1627-1659`),
`SMTPConfiguration::Get/SetMaxSMTPRecipientsInBatch`
(`source/Server/SMTP/SMTPConfiguration.cpp:211-220`),
`PROPERTY_MAXSMTPRECIPIENTSINBATCH`
(`source/Server/Common/Application/Constants.h:74`), the installed Settings
IID and `DispId(62)`
(`source/Server/hMailServer/hMailServer.idl:520-528,606-607`), and the
`maxsmtprecipientsinbatch` MSSQL seed
(`source/DBScripts/CreateTablesMSSQL.sql:862`). Focused settings/store
coverage is `128/128`; full unfiltered Net10 is `2111 passed, 39 skipped,
0 failed`.

Focused tests cover lease acquire/dispose, unavailable-lease denial before
mutation, and reauthentication blocking during an in-flight mutation. Legacy
delivery batching and its `0`-means-unlimited behavior remain unrepresented
in Net10; absent-row default `0` versus legacy install default `100` is also
open. Installed COM identity, direct activation denial, and authenticated
Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, protocol/delivery parity, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.DisconnectInvalidClients`. Do not push.

## Current Authoritative Continuation (2026-08-11, ALLOW INCORRECT LINE ENDINGS AUTHORIZATION LEASE)

Code/test commit `b6085a478` extends the existing generation-bound
authorization lease to authenticated
`Settings.AllowIncorrectLineEndings` (`DispId(61)`). The lease spans the
existing parameterized `smtpallowincorrectlineendings` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_AllowIncorrectLineEndings`
(`source/Server/COM/InterfaceSettings.cpp:326-356`),
`SMTPConfiguration::Get/SetAllowIncorrectLineEndings`
(`source/Server/SMTP/SMTPConfiguration.cpp:288-297`),
`PROPERTY_ALLOWINCORRECTLINEENDINGS`
(`source/Server/Common/Application/Constants.h:73`), the installed Settings
IID and `DispId(61)`
(`source/Server/hMailServer/hMailServer.idl:520-528,604-605`), and the
`smtpallowincorrectlineendings` MSSQL seed
(`source/DBScripts/CreateTablesMSSQL.sql:842`). Focused settings/store
coverage is `125/125`; full unfiltered Net10 is `2108 passed, 39 skipped,
0 failed`.

Focused tests cover lease acquire/dispose, unavailable-lease denial before
mutation, and reauthentication blocking during an in-flight mutation. Legacy
SMTP bare-LF validation consumes this setting, but that runtime behavior is
unchanged. Installed COM identity, direct activation denial, and authenticated
Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, protocol greeting parity, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.MaxSMTPRecipientsInBatch`. Do not push.

## Current Authoritative Continuation (2026-08-11, TCPIP THREADS AUTHORIZATION LEASE)

Code/test commit `752d55443` extends the existing generation-bound
authorization lease to authenticated `Settings.TCPIPThreads` (`DispId(60)`).
The lease spans the existing parameterized `tcpipthreads` SQL mutation,
result handling, and retained snapshot publication; unavailable leases fail
closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_TCPIPThreads`
(`source/Server/COM/InterfaceSettings.cpp:1530-1557`),
`Configuration::Get/SetTCPIPThreads`
(`source/Server/Common/Application/Configuration.cpp:142-151`),
`PROPERTY_TCPIPTHREADS` (`source/Server/Common/Application/Constants.h:72`),
the installed Settings IID and `DispId(60)`
(`source/Server/hMailServer/hMailServer.idl:520-528,601-602`), and the
`tcpipthreads` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:840`).
Focused settings/store coverage is `122/122`; full unfiltered Net10 is
`2105 passed, 39 skipped, 0 failed`.

Focused tests cover lease acquire/dispose, unavailable-lease denial before
mutation, and reauthentication blocking during an in-flight mutation. Legacy
and Net10 both persist this setting; listener-thread runtime use remains
unproven and unchanged. Installed COM identity, direct activation denial, and
authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, protocol greeting parity, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.AllowIncorrectLineEndings`. Do not push.

## Current Authoritative Continuation (2026-08-11, WORKER THREAD PRIORITY AUTHORIZATION LEASE)

Code/test commit `3ab7c8aef` extends the existing generation-bound
authorization lease to authenticated `Settings.WorkerThreadPriority`
(`DispId(57)`). The lease spans the existing parameterized
`workerthreadpriority` SQL mutation, result handling, and retained snapshot
publication; unavailable leases fail closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_WorkerThreadPriority`
(`source/Server/COM/InterfaceSettings.cpp:1496-1528`),
`Configuration::Get/SetWorkerThreadPriority`
(`source/Server/Common/Application/Configuration.cpp:129-139`),
`PROPERTY_WORKERTHREADPRIORITY`
(`source/Server/Common/Application/Constants.h:70`), the installed Settings
IID and `DispId(57)`
(`source/Server/hMailServer/hMailServer.idl:520-528,599-600`), and the
`workerthreadpriority` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:836`).
Focused settings/store coverage is `119/119`; full unfiltered Net10 is
`2102 passed, 39 skipped, 0 failed`.

Focused tests cover lease acquire/dispose, unavailable-lease denial before
mutation, and reauthentication blocking during an in-flight mutation. Source
tracing found no actual C++ or Net10 OS thread-priority application path, so
the setting remains persistence-only in this slice. Installed COM identity,
direct activation denial, and authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, protocol greeting parity, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.TCPIPThreads`. Do not push.

## Current Authoritative Continuation (2026-08-11, WELCOME IMAP AUTHORIZATION LEASE)

Code/test commit `7645f6f70` extends the existing generation-bound
authorization lease to authenticated `Settings.WelcomeIMAP` (`DispId(25)`).
The lease spans the existing parameterized `welcomeimap` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_WelcomeIMAP`
(`source/Server/COM/InterfaceSettings.cpp:747-780`),
`IMAPConfiguration::Get/SetWelcomeMessage`
(`source/Server/IMAP/IMAPConfiguration.cpp:54-63`),
`PROPERTY_WELCOMEIMAP` (`source/Server/Common/Application/Constants.h:13`),
the installed Settings IID and `DispId(25)`
(`source/Server/hMailServer/hMailServer.idl:520-528,551-552`), and the
`welcomeimap` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:754`).
Focused settings/store coverage is `116/116`; full unfiltered Net10 is
`2099 passed, 39 skipped, 0 failed`.

The focused tests cover lease acquire/dispose, unavailable-lease denial
before mutation, and reauthentication blocking during an in-flight mutation.
Legacy `IMAPConnection::SendBanner_` reads `welcomeimap` per connection, but
Net10 still uses its session greeting options; live IMAP greeting wiring is a
separate open protocol-runtime parity blocker. Installed COM identity,
direct activation denial, and authenticated Settings access remain unchanged.

Release remains RED for the greeting blocker, disposable SQL/Data restore,
non-DB restore, SQL/FTS, paired C++/.NET performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.WorkerThreadPriority`. Do not push.

## Current Authoritative Continuation (2026-08-11, WELCOME POP3 AUTHORIZATION LEASE)

Code/test commit `52c92f050` extends the existing generation-bound
authorization lease to authenticated `Settings.WelcomePOP3` (`DispId(24)`).
The lease spans the existing parameterized `welcomepop3` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_WelcomePOP3`
(`source/Server/COM/InterfaceSettings.cpp:713-745`),
`POP3Configuration::Get/SetWelcomeMessage`
(`source/Server/POP3/POP3Configuration.cpp:43-53`),
`PROPERTY_WELCOMEPOP3` (`source/Server/Common/Application/Constants.h:14`),
the installed Settings IID and `DispId(24)`
(`source/Server/hMailServer/hMailServer.idl:520-528,549-550`), and the
`welcomepop3` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:756`).
Focused settings/store coverage is `113/113`; full unfiltered Net10 is
`2096 passed, 39 skipped, 0 failed`.

The focused tests cover lease acquire/dispose, unavailable-lease denial
before mutation, and reauthentication blocking during an in-flight mutation.
Legacy `POP3Connection::SendBanner_` reads `welcomepop3` per connection, but
Net10 `Pop3Session` still uses `Pop3SessionOptions.Greeting`; live POP3
greeting wiring is a separate open protocol-runtime parity blocker. Installed
COM identity, direct activation denial, and authenticated Settings access
remain unchanged.

Release remains RED for the greeting blocker, disposable SQL/Data restore,
non-DB restore, SQL/FTS, paired C++/.NET performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.WelcomeIMAP`. Do not push.

## Current Authoritative Continuation (2026-08-11, WELCOME SMTP AUTHORIZATION LEASE)

Code/test commit `6f5a12cc6` extends the existing generation-bound
authorization lease to authenticated `Settings.WelcomeSMTP` (`DispId(23)`).
The lease spans the existing parameterized `welcomesmtp` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_WelcomeSMTP`
(`source/Server/COM/InterfaceSettings.cpp:679-711`),
`SMTPConfiguration::Get/SetWelcomeMessage`
(`source/Server/SMTP/SMTPConfiguration.cpp:113-123`),
`PROPERTY_WELCOMESMTP` (`source/Server/Common/Application/Constants.h:15`),
the installed Settings IID and `DispId(23)`
(`source/Server/hMailServer/hMailServer.idl:520-528,547-548`), and the
`welcomesmtp` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:758`).
Focused settings/store coverage is `110/110`; full unfiltered Net10 is
`2093 passed, 39 skipped, 0 failed`.

The focused tests cover lease acquire/dispose, unavailable-lease denial
before mutation, and reauthentication blocking during an in-flight mutation.
Legacy `SMTPConnection::SendBanner_` reads `welcomesmtp` per connection, but
Net10 `SmtpSession` still uses `SmtpSessionOptions.Greeting`; live greeting
wiring is a separate open protocol-runtime parity blocker. Installed COM
identity, direct activation denial, and authenticated Settings access remain
unchanged.

Release remains RED for the greeting blocker, disposable SQL/Data restore,
non-DB restore, SQL/FTS, paired C++/.NET performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: fresh
legacy-first audit of `Settings.WelcomePOP3`. Do not push.

## Current Authoritative Continuation (2026-08-11, SMTP RELAYER PORT AUTHORIZATION LEASE)

Code/test commit `f8875b316` extends the existing generation-bound
authorization lease to authenticated `Settings.SMTPRelayerPort`
(`DispId(37)`). The lease spans the existing parameterized
`smtprelayerport` SQL mutation, result handling, and retained snapshot
publication; unavailable leases fail closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_SMTPRelayerPort`
(`source/Server/COM/InterfaceSettings.cpp:609-642`),
`SMTPConfiguration::Get/SetSMTPRelayerPort`
(`source/Server/SMTP/SMTPConfiguration.cpp:151-160`), the installed Settings
IID and `DispId(37)` (`source/Server/hMailServer/hMailServer.idl:520-528,570-571`),
and the `smtprelayerport` MSSQL seed
(`source/DBScripts/CreateTablesMSSQL.sql:788`). Focused settings/store
coverage is `107/107`; full unfiltered Net10 is `2090 passed, 39 skipped,
0 failed`.

The focused tests cover lease acquire/dispose, unavailable-lease denial
before mutation, and reauthentication blocking during an in-flight mutation.
SMTP relay resolution, live reconfiguration, installed COM identity, direct
activation denial, and authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`Settings.WelcomeSMTP`. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER USERNAME AUTHORIZATION LEASE)

Code/test commit `33f48accd` extends the existing generation-bound
authorization lease to authenticated `Settings.SMTPRelayerUsername`
(`DispId(35)`). The lease spans the existing parameterized
`smtprelayerusername` SQL mutation, result handling, and retained snapshot
publication; unavailable leases fail closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_SMTPRelayerUsername`
(`source/Server/COM/InterfaceSettings.cpp:930-958`),
`SMTPConfiguration::Get/SetSMTPRelayerUsername`
(`source/Server/SMTP/SMTPConfiguration.cpp:261-270`), IDL `DispId(35)`
(`source/Server/hMailServer/hMailServer.idl:567-568`), and the
`smtprelayerusername` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:782`).
Focused settings/store coverage is `104/104`. Full Net10 excluding the two
known host/AV-locked scanner cleanup classes is `2080 passed, 39 skipped,
0 failed`; the unfiltered run has 2 unrelated temporary-`.eml`
`UnauthorizedAccessException` cleanup failures. SMTP credential handling,
live reconfiguration, installed COM identity, direct activation denial, and
authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`SMTPRelayerPort`. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER AUTHENTICATION AUTHORIZATION LEASE)

Code/test commit `29be1faa0` extends the existing generation-bound
authorization lease to authenticated
`Settings.SMTPRelayerRequiresAuthentication` (`DispId(34)`). The lease spans
the existing parameterized `usesmtprelayerauthentication` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_SMTPRelayerRequiresAuthentication`
(`source/Server/COM/InterfaceSettings.cpp:896-923`),
`SMTPConfiguration::Get/SetSMTPRelayerRequiresAuthentication`
(`source/Server/SMTP/SMTPConfiguration.cpp:249-258`), IDL `DispId(34)`
(`source/Server/hMailServer/hMailServer.idl:565-566`), and the
`usesmtprelayerauthentication` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:786`).
Focused settings/store coverage is `102/102`. Full Net10 excluding the two
known host/AV-locked scanner cleanup classes is `2078 passed, 39 skipped,
0 failed`; the unfiltered run has 2 unrelated temporary-`.eml`
`UnauthorizedAccessException` cleanup failures. SMTP delivery authentication,
live reconfiguration, installed COM identity, direct activation denial, and
authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`SMTPRelayerUsername`. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER AUTHORIZATION LEASE)

Code/test commit `c83791c3b` extends the existing generation-bound
authorization lease to authenticated `Settings.SMTPRelayer` (`DispId(22)`).
The lease spans the existing parameterized `smtprelayer` SQL mutation, result
handling, and retained snapshot publication; unavailable leases fail closed
with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_SMTPRelayer`
(`source/Server/COM/InterfaceSettings.cpp:574-605`),
`SMTPConfiguration::Get/SetSMTPRelayer`
(`source/Server/SMTP/SMTPConfiguration.cpp:139-148`), IDL `DispId(22)`
(`source/Server/hMailServer/hMailServer.idl:545-546`), and the
`smtprelayer` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:760`).
Focused settings/store coverage is `100/100`. Full Net10 excluding the two
known host/AV-locked scanner cleanup classes is `2076 passed, 39 skipped,
0 failed`; the unfiltered run had 2 unrelated temporary-`.eml`
`UnauthorizedAccessException` cleanup failures. SMTP delivery, live
reconfiguration, installed COM identity, direct activation denial, and
authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`SMTPRelayerRequiresAuthentication`. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP MINUTES BETWEEN TRY AUTHORIZATION LEASE)

Code/test commit `06af4facd` extends the existing generation-bound
authorization lease to authenticated `Settings.SMTPMinutesBetweenTry`
(`DispId(20)`). The lease spans the existing parameterized
`smtpminutesbetweenretries` SQL mutation, result handling, and retained
snapshot publication; unavailable leases fail closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_SMTPMinutesBetweenTry`
(`source/Server/COM/InterfaceSettings.cpp:500-533`),
`SMTPConfiguration::Set/GetMinutesBetweenTry`
(`source/Server/SMTP/SMTPConfiguration.cpp:101-110`), IDL `DispId(20)`
(`source/Server/hMailServer/hMailServer.idl:543-544`), and the
`smtpminutesbetweenretries` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:744`).
Focused settings/store coverage is `98/98`; full Net10 is `2081 passed, 39
skipped, 0 failed`. Retry scheduling, delivery runtime, live reconfiguration,
installed COM identity, direct activation denial, and authenticated Settings
access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`SMTPRelayer`. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP NO OF TRIES AUTHORIZATION LEASE)

Code/test commit `0bf71cd8f` extends the existing generation-bound
authorization lease to authenticated `Settings.SMTPNoOfTries` (`DispId(19)`).
The lease spans the existing parameterized `smtpnoofretries` SQL mutation,
result handling, and retained snapshot publication; unavailable leases fail
closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_SMTPNoOfTries`
(`source/Server/COM/InterfaceSettings.cpp:465-496`),
`SMTPConfiguration::Set/GetNoOfRetries`
(`source/Server/SMTP/SMTPConfiguration.cpp:88-97`), IDL `DispId(19)`
(`source/Server/hMailServer/hMailServer.idl:541-542`), and the
`smtpnoofretries` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:742`).
Focused settings/store coverage is `96/96`; full Net10 is `2079 passed, 39
skipped, 0 failed`. Retry policy, delivery runtime, live reconfiguration,
installed COM identity, direct activation denial, and authenticated Settings
access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`SMTPMinutesBetweenTry`. Do not push.

# Current Authoritative Continuation (2026-08-11, DENY MAIL FROM NULL AUTHORIZATION LEASE)

Code/test commit `a146723f4` extends the existing generation-bound
authorization lease to authenticated `Settings.DenyMailFromNull`
(`DispId(11)`). The lease spans the existing parameterized
`allowmailfromnull` SQL mutation, result handling, and retained snapshot
publication; unavailable leases fail closed with `E_ACCESSDENIED`. The legacy
inversion remains exact: setting the COM property to TRUE writes the stored
allow-mail-from-null value as FALSE.

Legacy anchors: `InterfaceSettings::get/put_DenyMailFromNull`
(`source/Server/COM/InterfaceSettings.cpp:284-321`),
`SMTPConfiguration::Set/GetAllowMailFromNull`
(`source/Server/SMTP/SMTPConfiguration.cpp:75-85`), the SMTP empty-sender
check (`source/Server/SMTP/SMTPConnection.cpp:601-614`), IDL `DispId(11)`
(`source/Server/hMailServer/hMailServer.idl:537-538`), and the
`allowmailfromnull` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:736`).
Focused settings/store coverage is `94/94`; full Net10 is `2077 passed, 39
skipped, 0 failed`. SMTP trust, live reconfiguration, installed COM identity,
direct activation denial, and authenticated Settings access remain unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`SMTPNoOfTries`. Do not push.

# Current Authoritative Continuation (2026-08-11, ALLOW SMTP AUTH PLAIN AUTHORIZATION LEASE)

Code/test commit `2d42c0006` extends the existing generation-bound
authorization lease to authenticated `Settings.AllowSMTPAuthPlain`
(`DispId(8)`). The lease spans the existing parameterized
`authallowplaintext` SQL mutation, result handling, and retained snapshot
publication; unavailable leases fail closed with `E_ACCESSDENIED`.

Legacy anchors: `InterfaceSettings::get/put_AllowSMTPAuthPlain`
(`source/Server/COM/InterfaceSettings.cpp:242-280`),
`SMTPConfiguration::Set/GetAuthAllowPlainText`
(`source/Server/SMTP/SMTPConfiguration.cpp:63-72`), IDL `DispId(8)`
(`source/Server/hMailServer/hMailServer.idl:535-536`), and the
`authallowplaintext` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:734`).
Focused settings/store coverage is `92/92`; full Net10 is `2075 passed, 39
skipped, 0 failed`. Tests cover lease acquire/dispose and unavailable-lease
denial before SQL mutation. SMTP trust, live reconfiguration, installed COM
identity, direct activation denial, and authenticated Settings access remain
unchanged.

Release remains RED for disposable SQL/Data restore, non-DB restore,
SQL/FTS, paired C++/.NET performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, 24-hour soak, and remaining
unleased COM/Admin mutations. Next slice: fresh legacy-first audit of
`DenyMailFromNull`. Do not push.

# Current Authoritative Continuation (2026-08-11, MIRROR EMAIL AUTHORIZATION LEASE)

Code/test commit `59e433449` extends the existing authorization generation
lease to authenticated `Settings.MirrorEMailAddress` (`DispId(7)`). The lease
is acquired immediately before the existing parameterized
`mirroremailaddress` SQL update and held through mutation result handling and
retained snapshot publication. No email mirroring runtime, validation, SQL
shape, BSTR contract, or installed COM identity changed.

Legacy anchors: `InterfaceSettings::get/put_MirrorEMailAddress`
(`source/Server/COM/InterfaceSettings.cpp:207-239`),
`Configuration::SetMirrorAddress`
(`source/Server/Common/Application/Configuration.cpp:240-248`), IDL
`DispId(7)` (`source/Server/hMailServer/hMailServer.idl:533-534`), and the
`mirroremailaddress` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:730`).
Focused settings/store coverage is `90/90`; full Net10 is `2073 passed, 39
skipped, 0 failed`. Tests cover direct activation, failed-write retention,
lease acquire/dispose, and unavailable-lease denial.

Remaining unleased Settings mutations and the SQL/Data restore, non-DB
restore, SQL/FTS, matched C++/.NET performance, SEC-18, installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak gates remain
RED. Next slice: fresh legacy-first audit of `AllowSMTPAuthPlain`. Do not push.

# Current Authoritative Continuation (2026-08-11, MAX POP3 CONNECTIONS AUTHORIZATION LEASE)

Code/test commit `0e4a70129` extends the existing authorization generation
lease to authenticated `Settings.MaxPOP3Connections` (`DispId(6)`). The lease
is acquired immediately before the existing parameterized
`maxpop3connections` SQL update and held through mutation result handling and
retained snapshot publication. No validation, SQL shape, POP3 listener
behavior, or installed COM identity changed.

Legacy anchors: `InterfaceSettings::get/put_MaxPOP3Connections`
(`source/Server/COM/InterfaceSettings.cpp:172-202`),
`POP3Configuration::Set/GetMaxPOP3Connections`
(`source/Server/POP3/POP3Configuration.cpp:31-40`), IDL `DispId(6)`
(`source/Server/hMailServer/hMailServer.idl:531-532`), and the
`maxpop3connections` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:726`).
Focused settings/store coverage is `88/88`; full Net10 is `2071 passed, 39
skipped, 0 failed`. Tests cover direct activation, failed-write retention,
lease acquire/dispose, and unavailable-lease denial.

Remaining unleased Settings mutations and the SQL/Data restore, non-DB
restore, SQL/FTS, matched C++/.NET performance, SEC-18, installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak gates remain
RED. Next slice: fresh legacy-first audit of `MirrorEMailAddress`. Do not push.

# Current Authoritative Continuation (2026-08-11, MAX SMTP CONNECTIONS AUTHORIZATION LEASE)

Code/test commit `9178d1b1b` extends the existing authorization generation
lease to authenticated `Settings.MaxSMTPConnections` (`DispId(5)`). The lease
is acquired immediately before the existing parameterized
`maxsmtpconnections` SQL update and held through mutation result handling and
retained snapshot publication. No validation, SQL shape, runtime listener
behavior, or installed COM identity changed.

Legacy anchors: `InterfaceSettings::get/put_MaxSMTPConnections`
(`source/Server/COM/InterfaceSettings.cpp:108-134`),
`SMTPConfiguration::Set/GetMaxSMTPConnections`
(`source/Server/SMTP/SMTPConfiguration.cpp:51-58`), IDL `DispId(5)`
(`source/Server/hMailServer/hMailServer.idl:529-530`), and the
`maxsmtpconnections` MSSQL seed (`source/DBScripts/CreateTablesMSSQL.sql:728`).
Focused settings/store coverage is `86/86`; full Net10 is `2069 passed, 39
skipped, 0 failed`. Tests cover direct activation, failed-write retention,
lease acquire/dispose, and unavailable-lease denial.

Remaining unleased Settings mutations and the SQL/Data restore, non-DB
restore, SQL/FTS, matched C++/.NET performance, SEC-18, installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak gates remain
RED. Next slice: fresh legacy-first audit of `MaxPOP3Connections`. Do not push.

# Current Authoritative Continuation (2026-08-11, SETTINGS MUTATION AUTHORIZATION LEASE)

Code/test commit `62f5ef553` closes the retained-COM authorization race for the
bounded `Settings.SMTPRelayerUseSSL`/`SMTPRelayerConnectionSecurity` mutation
path. `Application.Settings` captures the current authorization generation and
passes the existing `ApplicationAuthorizationAuthority.AcquireLeaseAsync`
through `SettingsAdministrationRuntimeHost` into `Settings`. The lease is held
from immediately before the parameterized SQL update through retained snapshot
publication; an unavailable lease fails with `E_ACCESSDENIED`.

Legacy anchors inspected: `InterfaceApplication::get_Settings`,
`InterfaceSettings::LoadSettings`, and
`InterfaceSettings::put_SMTPRelayerUseSSL` in
`source/Server/COM/InterfaceApplication.cpp` and
`source/Server/COM/InterfaceSettings.cpp`. Legacy retained scalar Settings
objects use acquisition-time `config_` authorization. The .NET rewrite retains
its stricter live mutation check, now serialized with the authorization
generation. Installed COM identity, `DispId(71)`/`DispId(91)`, VARIANT_BOOL
mapping, direct activation denial, and SMTP runtime behavior remain unchanged.

Focused settings/store coverage is `84/84`; full Net10 is `2067 passed, 39
skipped, 0 failed`. The race test proves reauthentication waits for the write
lease and the old retained proxy cannot mutate after generation invalidation.
Other Settings mutation paths still need the same lease treatment.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, 24-hour
soak, and remaining unleased COM/Admin mutations. Next slice: extend the lease
to the next smallest existing Settings mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER USE SSL MUTATION)

Code/test commit `90ecdaa5a` implements only authenticated
`Settings.SMTPRelayerUseSSL` (`DispId(71)`) persistence. It preserves the
installed COM identity, VARIANT_BOOL shape, direct activation denial, and the
existing `SMTPRelayerConnectionSecurity` projection. `true` maps to legacy
`CSSSL`/`1`; `false` maps to `CSNone`/`0` through the existing parameterized
`hm_settings.smtprelayerconnectionsecurity` store path. The retained snapshot
changes only after one-row success.

Legacy anchors: `IInterfaceSettings.SMTPRelayerUseSSL` in
`source/Server/hMailServer/hMailServer.idl`,
`InterfaceSettings::get_SMTPRelayerUseSSL` and
`put_SMTPRelayerUseSSL` (`source/Server/COM/InterfaceSettings.cpp:1729-1760`),
`SMTPConfiguration::Set/GetSMTPRelayerConnectionSecurity`
(`source/Server/SMTP/SMTPConfiguration.cpp:163-174`), and the
`smtprelayerconnectionsecurity` MSSQL seed. Focused settings/store coverage is
`81/81`; full Net10 is `2064 passed, 39 skipped, 0 failed`.

Direct activation denial, true/false mapping, failed-write retention, and
administrator revocation are covered. Outbound relayer TLS/STARTTLS,
notifications, and live reconfiguration were deliberately not changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Security review also found a medium retained-COM-proxy authorization
TOCTOU blocker: revocation can race between `Settings`' live administrator
check and the SQL mutation because no authorization lease spans both. Next
slice: legacy-first audit of the smallest safe authorization-lease fix or
remaining Settings mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER CONNECTION SECURITY MUTATION)

Code/test commit `0f7b50282` implements only authenticated
`Settings.SMTPRelayerConnectionSecurity` (`DispId(91)`) persistence. It
preserves the installed COM identity, enum values `None=0`, `Tls=1`,
`StartTlsOptional=2`, `StartTlsRequired=3`, and direct activation denial. The
setter casts the enum directly, updates only the existing
`hm_settings.smtprelayerconnectionsecurity` row with a parameterized
`SqlDbType.Int` command, and changes the retained snapshot only after one-row
success. No enum-range validation was added, matching legacy behavior.

Legacy anchors: `eConnectionSecurity` and
`IInterfaceSettings.SMTPRelayerConnectionSecurity`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_SMTPRelayerConnectionSecurity`,
`SMTPConfiguration::SetSMTPRelayerConnectionSecurity`, generic
`PropertySet::SetLong`/`Property::WriteLongSetting_`,
`ServerTargetResolver::GetFixedSMTPHostForDomain_`, and the
`smtprelayerconnectionsecurity` SQL seed
(`source/DBScripts/CreateTablesMSSQL.sql`). Focused settings/store coverage is
`80/80`; full Net10 is `2063 passed, 39 skipped, 0 failed`. The existing
`SMTPRelayerUseSSL` projection and outbound TLS/STARTTLS runtime were not
changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER MUTATION)

Code/test commit `4a5a6cf5f` implements only authenticated
`Settings.SMTPRelayer` (`DispId(22)`, `BSTR`) persistence. It preserves the
installed COM identity and direct activation denial, rechecks the existing
server-administrator boundary, updates only the existing
`hm_settings.smtprelayer` row with a parameterized `nvarchar(4000)` command,
and changes the retained snapshot only after one-row success. The legacy relay
value is written unchanged; no validation or encryption was added.

Legacy anchors: `IInterfaceSettings.SMTPRelayer`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_SMTPRelayer`,
`SMTPConfiguration::SetSMTPRelayer`, generic
`PropertySet::SetString`/`Property::WriteStringSetting_`,
`ServerTargetResolver::GetFixedSMTPHostForDomain_`, and the `smtprelayer` SQL
seed (`source/DBScripts/CreateTablesMSSQL.sql`). Focused settings/store
coverage is `78/78`; full Net10 is `2061 passed, 39 skipped, 0 failed`.
Fixed-relay routing, notifications, relayer credentials, and live
reconfiguration were deliberately not changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER USERNAME MUTATION)

Code/test commit `8e3e5cf16` implements only authenticated
`Settings.SMTPRelayerUsername` (`DispId(35)`, `BSTR`) persistence. It preserves
the installed COM identity and direct activation denial, rechecks the existing
server-administrator boundary, updates only the existing
`hm_settings.smtprelayerusername` row with a parameterized `nvarchar(4000)`
command, and changes the retained snapshot only after one-row success. The
legacy username value is written unchanged; no validation or encryption was
added, and only the legacy relayer password path remains encrypted.

Legacy anchors: `IInterfaceSettings.SMTPRelayerUsername`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_SMTPRelayerUsername`,
`SMTPConfiguration::SetSMTPRelayerUsername`, generic
`PropertySet::SetString`/`Property::WriteStringSetting_`,
`ServerTargetResolver::GetFixedSMTPHostForDomain_`, and the
`smtprelayerusername` SQL seed
(`source/DBScripts/CreateTablesMSSQL.sql`). Focused settings/store coverage is
`76/76`; full Net10 is `2059 passed, 39 skipped, 0 failed`. Relayer password
storage, fixed-relay routing, notifications, and live reconfiguration were
deliberately not changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER PORT MUTATION)

Code/test commit `0707fda27` implements only authenticated
`Settings.SMTPRelayerPort` (`DispId(37)`, `int`) persistence. It preserves the
installed COM identity and direct activation denial, rechecks the live
administrator callback, updates only the existing
`hm_settings.smtprelayerport` row with a parameterized `SqlDbType.Int`
command, and changes the retained snapshot only after one-row success. The
legacy value is written unchanged and the seeded default remains `25`.

Legacy anchors: `IInterfaceSettings.SMTPRelayerPort`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_SMTPRelayerPort`,
`SMTPConfiguration::SetSMTPRelayerPort`, generic
`PropertySet::SetLong`/`Property::WriteLongSetting_`,
`ServerTargetResolver::GetFixedSMTPHostForDomain_`, and the
`smtprelayerport` SQL seed (`source/DBScripts/CreateTablesMSSQL.sql`).
Focused settings/store coverage is `74/74`; full Net10 is `2057 passed, 39
skipped, 0 failed`. Fixed-relayer routing, configuration notifications, and
live reconfiguration were deliberately not changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RELAYER AUTHENTICATION MUTATION)

Code/test commit `429b20687` implements only authenticated
`Settings.SMTPRelayerRequiresAuthentication` (`DispId(34)`, `VARIANT_BOOL`)
persistence. It preserves the installed COM identity and direct activation
denial, rechecks the live administrator callback, updates only the existing
`hm_settings.usesmtprelayerauthentication` row with a parameterized integer
command, and changes the retained snapshot only after one-row success. The
legacy public value maps directly to storage: `true` writes `1`, and `false`
writes `0`.

Legacy anchors: `IInterfaceSettings.SMTPRelayerRequiresAuthentication`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_SMTPRelayerRequiresAuthentication`,
`SMTPConfiguration::SetSMTPRelayerRequiresAuthentication`, generic
`Property::SetBoolValue`/`Property::WriteLongSetting_`, and the
`usesmtprelayerauthentication` SQL seed
(`source/DBScripts/CreateTablesMSSQL.sql`). Focused settings/store coverage is
`72/72`; full Net10 is `2055 passed, 39 skipped, 0 failed`. Fixed-relayer
credential selection, SMTP routing, change notifications, and live
reconfiguration were deliberately not changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, DENY MAIL FROM NULL MUTATION)

Code/test commit `5d67f7eee` implements only authenticated
`Settings.DenyMailFromNull` (`DispId(11)`, `VARIANT_BOOL`) persistence. It
preserves the installed COM identity and direct activation denial, rechecks
the live administrator callback, updates only the existing
`hm_settings.allowmailfromnull` row with a parameterized integer command, and
changes the retained snapshot only after one-row success. The legacy public
value is inverted for storage: `true` writes `AllowMailFromNull = 0`, and
`false` writes `1`.

Legacy anchors: `IInterfaceSettings.DenyMailFromNull`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_DenyMailFromNull`,
`SMTPConfiguration::SetAllowMailFromNull`, generic
`PropertySet::SetBoolValue`/`Property::WriteLongSetting_`, and the
`allowmailfromnull` SQL seed (`source/DBScripts/CreateTablesMSSQL.sql`).
Focused settings/store coverage is `70/70`; full Net10 is `2053 passed, 39
skipped, 0 failed`. SMTP `MAIL FROM:<>` runtime behavior and live
reconfiguration were deliberately not changed.

Release remains RED for disposable SQL/Data rollback, non-DB restore/
reinitialization, SQL/FTS, matched C++/.NET protocol performance, SEC-18,
migration/installer, out-of-process COM, AD/DC, crash/power-loss, and 24-hour
soak. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Do not push.

# Current Authoritative Continuation (2026-08-11, ALLOW SMTP AUTH PLAIN MUTATION)

Code/test commit `5ff8ef8ee` implements only authenticated
`Settings.AllowSMTPAuthPlain` (`DispId(8)`, `VARIANT_BOOL`) persistence. It
preserves the installed COM identity and direct activation denial, rechecks
the live administrator callback, updates only the existing
`hm_settings.authallowplaintext` row with a parameterized integer command,
and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.AllowSMTPAuthPlain`
(`source/Server/hMailServer/hMailServer.idl`),
`InterfaceSettings::put_AllowSMTPAuthPlain`,
`SMTPConfiguration::SetAuthAllowPlainText`, generic `PropertySet::SetBool`,
and the `authallowplaintext` SQL seed
(`source/DBScripts/CreateTablesMSSQL.sql:734`). Focused settings/store
coverage is `68/68`; full Net10 is `2051 passed, 39 skipped, 0 failed`. SMTP
advertisement/AUTH runtime behavior was deliberately not added.

Release remains RED for real SQL/Data rollback, non-DB restore/reinitialization,
SQL/FTS, matched C++/.NET protocol performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak. Next slice:
fresh legacy-first audit of one remaining low-risk Settings mutation. Do not
push.

# Current Authoritative Continuation (2026-08-11, TCPIP THREADS MUTATION)

Code/test commit `2752b90ad` implements only authenticated
`Settings.TCPIPThreads` (`DispId(60)`) persistence. It preserves the installed
COM identity and direct activation denial, rechecks the live administrator
callback, updates only the existing `hm_settings.tcpipthreads` row with a
parameterized integer command, and changes the retained snapshot only after
one-row success.

Legacy anchors: `IInterfaceSettings.TCPIPThreads`
(`source/Server/hMailServer/hMailServer.idl:522`),
`InterfaceSettings::put_TCPIPThreads`
(`source/Server/COM/InterfaceSettings.cpp:1530`),
`Configuration::SetTCPIPThreads`
(`source/Server/Common/Application/Configuration.cpp:142`),
`IOService::DoWork` (`source/Server/Common/TCPIP/IOService.cpp:66`), and the
`tcpipthreads` SQL seed (`source/DBScripts/CreateTablesMSSQL.sql:840`).
Focused settings/store coverage is `66/66`; full Net10 is `2049 passed, 39
skipped, 0 failed`. IOService worker creation/runtime reconfiguration was
deliberately not added.

Release remains RED for real SQL/Data rollback, non-DB restore/reinitialization,
SQL/FTS, matched C++/.NET protocol performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak. Next slice:
fresh legacy-first audit of one remaining low-risk Settings mutation. Do not
push.

# Current Authoritative Continuation (2026-08-11, MAX IMAP CONNECTIONS MUTATION)

Code/test commit `ab1c7c721` implements only authenticated
`Settings.MaxIMAPConnections` (`DispId(53)`) persistence. It preserves the
installed COM identity and direct activation denial, rechecks the live
administrator callback, updates only the existing
`hm_settings.maximapconnections` row with a parameterized integer command,
and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxIMAPConnections`
(`source/Server/hMailServer/hMailServer.idl:589-590`),
`InterfaceSettings::put_MaxIMAPConnections`
(`source/Server/COM/InterfaceSettings.cpp:140`),
`IMAPConfiguration::SetMaxIMAPConnections`
(`source/Server/IMAP/IMAPConfiguration.cpp:113`),
`SessionManager::CreateSession(STIMAP)`
(`source/Server/Common/Application/SessionManager.cpp:44`), and the
`maximapconnections` SQL seed (`source/DBScripts/CreateTablesMSSQL.sql:832`).
Focused settings/store coverage is `64/64`; full Net10 is `2047 passed, 39
skipped, 0 failed`. IMAP listener/runtime connection-limit wiring was
deliberately not added.

Release remains RED for real SQL/Data rollback, non-DB restore/reinitialization,
SQL/FTS, matched C++/.NET protocol performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak. Next slice:
fresh legacy-first audit of one remaining low-risk Settings mutation. Do not
push.

# Current Authoritative Continuation (2026-08-11, MAX DELIVERY THREADS MUTATION)

Code/test commit `88aa5466c` implements only authenticated
`Settings.MaxDeliveryThreads` (`DispId(29)`) persistence. It preserves the
installed COM identity and direct activation denial, rechecks the live
administrator callback, updates only the existing
`hm_settings.maxdelivertythreads` row with a parameterized integer command,
and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxDeliveryThreads`
(`source/Server/hMailServer/hMailServer.idl:520-560`),
`InterfaceSettings::put_MaxDeliveryThreads`
(`source/Server/COM/InterfaceSettings.cpp:556-572`),
`SMTPConfiguration::SetMaxNoOfDeliveryThreads`
(`source/Server/SMTP/SMTPConfiguration.cpp:187-195`),
`SMTPDeliveryManager::OnPropertyChanged`
(`source/Server/SMTP/SMTPDeliveryManager.cpp:184-197`), and the
`maxdelivertythreads` SQL seed (`source/DBScripts/CreateTablesMSSQL.sql:762`).
Focused settings/store coverage is `62/62`; full Net10 is `2045 passed, 39
skipped, 0 failed`. Live queue resizing/runtime reconfiguration was
deliberately not added.

Release remains RED for real SQL/Data rollback, non-DB restore/reinitialization,
SQL/FTS, matched C++/.NET protocol performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak. Next slice:
fresh legacy-first audit of one remaining low-risk Settings mutation. Do not
push.

# Current Authoritative Continuation (2026-08-11, RULE LOOP LIMIT MUTATION)

Code/test commit `4d554f1b5` implements only authenticated
`Settings.RuleLoopLimit` (`DispId(48)`) persistence. It preserves the installed
COM identity and direct activation denial, rechecks the live administrator
callback, updates only the existing `hm_settings.rulelooplimit` row with a
parameterized integer command, and changes the retained snapshot only after
one-row success.

Legacy anchors: `IInterfaceSettings.RuleLoopLimit`
(`source/Server/hMailServer/hMailServer.idl:580-581`),
`InterfaceSettings::put_RuleLoopLimit`
(`source/Server/COM/InterfaceSettings.cpp:1239-1270`),
`SMTPConfiguration::SetRuleLoopLimit`
(`source/Server/SMTP/SMTPConfiguration.cpp:223-233`), generic
`PropertySet::SetLong`/`Property::WriteLongSetting_`, and the
`rulelooplimit` SQL seed (`source/DBScripts/CreateTablesMSSQL.sql:814`).
Focused settings/SQL coverage is `60/60`; full Net10 is `2043 passed, 39
skipped, 0 failed`. RuleApplier/SmtpRuleProcessor runtime wiring was
deliberately not added.

Release remains RED for real SQL/Data rollback, non-DB restore/reinitialization,
SQL/FTS, matched C++/.NET protocol performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak. Next slice:
fresh legacy-first audit of one remaining low-risk Settings mutation. Do not
push.

# Current Authoritative Continuation (2026-08-11, VERIFY REMOTE SSL CERTIFICATE MUTATION)

Code/test commit `f882ff44f` implements only authenticated
`Settings.VerifyRemoteSslCertificate` (`DispId(93)`) persistence. It preserves
the installed COM identity and direct activation denial, rechecks the live
administrator callback, updates only the existing
`hm_settings.VerifyRemoteSslCertificate` row with a parameterized integer
command, and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.VerifyRemoteSslCertificate`
(`source/Server/hMailServer/hMailServer.idl:656-657`),
`InterfaceSettings::put_VerifyRemoteSslCertificate`
(`source/Server/COM/InterfaceSettings.cpp:2244-2254`),
`Configuration::SetVerifyRemoteSslCertificate`
(`source/Server/Common/Application/Configuration.cpp:604-607`),
`PROPERTY_VERIFYREMOTESSLCERTIFICATE`
(`source/Server/Common/Application/Constants.h:122`), and the SQL seed
(`source/DBScripts/CreateTablesMSSQL.sql:936`). Focused settings/SQL coverage
is `58/58`; full Net10 is `2041 passed, 39 skipped, 0 failed`. TLS handshake
runtime reconfiguration was deliberately not added.

Release remains RED for real SQL/Data rollback, non-DB restore/reinitialization,
SQL/FTS, matched C++/.NET protocol performance, SEC-18, migration/installer,
out-of-process COM, AD/DC, crash/power-loss, and 24-hour soak. Next slice:
fresh legacy-first audit of one remaining low-risk Settings mutation. Do not
push.

# Current Authoritative Continuation (2026-08-11, MAXIMUM MX HOST COUNT MUTATION)

Code/test commit `3ca025ce1` implements only authenticated
`Settings.MaxNumberOfMXHosts` (`DispId(90)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.MaxNumberOfMXHosts` row with a parameterized integer command, and
changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxNumberOfMXHosts`
(`source/Server/hMailServer/hMailServer.idl:650-651`),
`InterfaceSettings::put_MaxNumberOfMXHosts`
(`source/Server/COM/InterfaceSettings.cpp:2189-2214`),
`SMTPConfiguration::SetMaxNumberOfMXHosts`
(`source/Server/SMTP/SMTPConfiguration.cpp:237-245`),
`PROPERTY_MAX_NUMBER_OF_MXHOSTS`
(`source/Server/Common/Application/Constants.h:120`), and the SQL seed.
Focused settings/SQL coverage is `56/56`; full Net10 is `2039 passed, 39
skipped, 0 failed`. ExternalDelivery MX-host enforcement and runtime
reconfiguration were deliberately not added. Next slice: fresh legacy-first
audit of one remaining low-risk Settings mutation. Real SQL/Data rollback,
SEC-18, installer/out-of-process COM, matched protocol performance, and
24-hour soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RETRY COUNT MUTATION)

Code/test commit `f8010374d` implements only authenticated
`Settings.SMTPNoOfTries` (`DispId(19)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the
canonical `hm_settings.smtpnoofretries` row with a parameterized integer
command, and changes the retained snapshot only after one-row success. The
unrelated typo row `smtpnooftries` remains excluded.

Legacy anchors: `IInterfaceSettings.SMTPNoOfTries`
(`source/Server/hMailServer/hMailServer.idl:541-542`),
`InterfaceSettings::put_SMTPNoOfTries`, `SMTPConfiguration::SetNoOfRetries`,
`PROPERTY_SMTPNOOFTRIES`, and the canonical SQL seed. Focused settings/SQL
coverage is `53/53`; full Net10 is `2036 passed, 39 skipped, 0 failed`.
External retry scheduling and runtime reconfiguration were deliberately not
added. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Real SQL/Data rollback, SEC-18, installer/out-of-process COM, matched
protocol performance, and 24-hour soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-11, SMTP RETRY INTERVAL MUTATION)

Code/test commit `b970bf00c` implements only authenticated
`Settings.SMTPMinutesBetweenTry` (`DispId(20)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.smtpminutesbetweenretries` row with a parameterized integer
command, and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.SMTPMinutesBetweenTry`
(`source/Server/hMailServer/hMailServer.idl:543-544`),
`InterfaceSettings::put_SMTPMinutesBetweenTry`
(`source/Server/COM/InterfaceSettings.cpp:500-535`),
`SMTPConfiguration::SetMinutesBetweenTry`
(`source/Server/SMTP/SMTPConfiguration.cpp:101-109`),
`PROPERTY_SMTPMINUTESBETWEEN`
(`source/Server/Common/Application/Constants.h:12`), and the
`smtpminutesbetweenretries` schema seed. Focused settings/SQL coverage is
`51/51`; full Net10 is `2034 passed, 39 skipped, 0 failed`. External retry
scheduling and runtime reconfiguration were deliberately not added. Next
slice: fresh legacy-first audit of one remaining low-risk Settings mutation.
Real SQL/Data rollback, SEC-18, installer/out-of-process COM, matched protocol
performance, and 24-hour soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-11, INCORRECT LINE ENDINGS MUTATION)

Code/test commit `9a7687365` implements only authenticated
`Settings.AllowIncorrectLineEndings` (`DispId(61)`, `VARIANT_BOOL`) persistence.
It preserves direct activation denial, rechecks the live administrator
callback, updates the fixed `hm_settings.smtpallowincorrectlineendings` row
with a parameterized integer command, and changes the retained snapshot only
after one-row success.

Legacy anchors: `IInterfaceSettings.AllowIncorrectLineEndings`
(`source/Server/hMailServer/hMailServer.idl:604`),
`InterfaceSettings::put_AllowIncorrectLineEndings`
(`source/Server/COM/InterfaceSettings.cpp:326`),
`SMTPConfiguration::SetAllowIncorrectLineEndings`
(`source/Server/SMTP/SMTPConfiguration.cpp:288`),
`Property::SetBoolValue` / `WriteLongSetting_`
(`source/Server/Common/Application/Property.cpp:36-78`), and the
`smtpallowincorrectlineendings` schema seed. Focused settings/SQL coverage is
`49/49`; full Net10 is `2032 passed, 39 skipped, 0 failed`. SMTP behavior and
runtime reconfiguration were deliberately not added. Next slice: fresh
legacy-first audit of one remaining low-risk Settings mutation. Real SQL/Data
rollback, SEC-18, installer/out-of-process COM, matched protocol performance,
and 24-hour soak remain open. Do not deploy to production.

# Current Authoritative Continuation (2026-08-11, DELIVERED-TO HEADER MUTATION)

Code/test commit `279b18f70` implements only authenticated
`Settings.AddDeliveredToHeader` (`DispId(73)`, `VARIANT_BOOL`) persistence. It
preserves direct activation denial, rechecks the live administrator callback,
updates the fixed `hm_settings.adddeliveredtoheader` row with a parameterized
integer command, and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.AddDeliveredToHeader`
(`source/Server/hMailServer/hMailServer.idl:520`),
`InterfaceSettings::put_AddDeliveredToHeader`
(`source/Server/COM/InterfaceSettings.cpp:1833`),
`SMTPConfiguration::SetAddDeliveredToHeader`
(`source/Server/SMTP/SMTPConfiguration.cpp:300`),
`PROPERTY_ADDDELIVEREDTOHEADER`
(`source/Server/Common/Application/Constants.h:94`), and
`source/DBScripts/CreateTablesMSSQL.sql:874`. Focused settings/SQL coverage is
`47/47`; full Net10 is `2030 passed, 39 skipped, 0 failed`. `LocalDelivery` and
runtime reconfiguration were deliberately not changed. Next slice: fresh
legacy-first audit of one remaining low-risk Settings mutation. Real SQL/Data
rollback, SEC-18, installer/out-of-process COM, matched protocol performance,
and 24-hour soak remain open. Do not deploy to production.

# Current Authoritative Continuation (2026-08-10, MAXIMUM MESSAGE SIZE MUTATION)

Code/test commit `69aa0c6d5` implements only authenticated
`Settings.MaxMessageSize` (`DispId(44)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.maxmessagesize` row with a parameterized integer command, and
changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxMessageSize`
(`source/Server/hMailServer/hMailServer.idl:576-577`),
`InterfaceSettings::put_MaxMessageSize`
(`source/Server/COM/InterfaceSettings.cpp:65-105`),
`SMTPConfiguration::SetMaxMessageSize`
(`source/Server/SMTP/SMTPConfiguration.cpp:199-207`), and
`source/DBScripts/CreateTablesMSSQL.sql:804`. Focused settings/SQL coverage is
`45/45`; full Net10 is `2028 passed, 39 skipped, 0 failed`. SMTP/IMAP runtime
enforcement, KB-to-byte conversion, and live reconfiguration were deliberately
not added. Next slice: fresh legacy-first audit of one remaining low-risk
Settings mutation. Real SQL/Data rollback, SEC-18, installer/out-of-process
COM, matched protocol performance, and 24-hour soak remain open. Repository
push verified at `32cd0c5bc`; do not deploy to production.

# Current Authoritative Continuation (2026-08-10, DISCONNECT INVALID CLIENTS MUTATION)

Code/test commit `2ee01f107` implements only authenticated
`Settings.DisconnectInvalidClients` (`DispId(64)`, `VARIANT_BOOL`) persistence.
It preserves direct activation denial, rechecks the live administrator
callback, updates the fixed `hm_settings.disconnectinvalidclients` row with a
parameterized integer command, and changes the retained snapshot only after
one-row success.

Legacy anchors: `IInterfaceSettings.DisconnectInvalidClients`
(`source/Server/hMailServer/hMailServer.idl:610-613`),
`InterfaceSettings::put_DisconnectInvalidClients`
(`source/Server/COM/InterfaceSettings.cpp:1661-1693`),
`Configuration::SetDisconnectInvalidClients`
(`source/Server/Common/Application/Configuration.cpp:488-498`),
`Property::SetBoolValue` / `WriteLongSetting_`
(`source/Server/Common/Application/Property.cpp:36-78`), and
`PROPERTY_SMTPDISCONNECTINVALIDCLIENTS`
(`source/Server/Common/Application/Constants.h:89`). Focused settings/SQL
coverage is `43/43`; full Net10 is `2026 passed, 39 skipped, 0 failed`. SMTP
invalid-command runtime reconfiguration was deliberately not added. Next
slice: fresh legacy-first audit of one remaining low-risk Settings mutation.
Real SQL/Data rollback, SEC-18, installer/out-of-process COM, matched protocol
performance, and 24-hour soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, INVALID COMMAND LIMIT MUTATION)

Code/test commit `9a7e418eb` implements only authenticated
`Settings.MaxNumberOfInvalidCommands` (`DispId(65)`) persistence. It preserves
direct activation denial, rechecks the live administrator callback, updates the
fixed `hm_settings.maximumincorrectcommands` row with a parameterized integer
command, and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxNumberOfInvalidCommands`
(`source/Server/hMailServer/hMailServer.idl:612-613`),
`InterfaceSettings::put_MaxNumberOfInvalidCommands`
(`source/Server/COM/InterfaceSettings.cpp:1695-1720`),
`Configuration::SetMaxNumberOfInvalidCommands`
(`source/Server/Common/Application/Configuration.cpp:501-509`),
`PROPERTY_MAXIMUMINCORRECTCOMMANDS`
(`source/Server/Common/Application/Constants.h:90`), and the SMTP threshold
consumer in `source/Server/SMTP/SMTPConnection.cpp:2210-2219`. Focused
settings/SQL coverage is `41/41`; full Net10 is `2024 passed, 39 skipped, 0
failed`. SMTP disconnect-threshold runtime reconfiguration was deliberately
not added. Next slice: fresh legacy-first audit of one remaining low-risk
Settings mutation. Real SQL/Data rollback, SEC-18, installer/out-of-process
COM, matched protocol performance, and 24-hour soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, MAX SMTP RECIPIENT BATCH MUTATION)

Code/test commit `b4cacd531` implements only authenticated
`Settings.MaxSMTPRecipientsInBatch` (`DispId(62)`) persistence. It preserves
direct activation denial, rechecks the live administrator callback, updates the
fixed `hm_settings.maxsmtprecipientsinbatch` row with a parameterized integer
command, and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxSMTPRecipientsInBatch`
(`source/Server/hMailServer/hMailServer.idl:606-607`),
`InterfaceSettings::put_MaxSMTPRecipientsInBatch`
(`source/Server/COM/InterfaceSettings.cpp:1627-1658`),
`SMTPConfiguration::SetMaxSMTPRecipientsInBatch`
(`source/Server/SMTP/SMTPConfiguration.cpp:211-220`), and
`PROPERTY_MAXSMTPRECIPIENTSINBATCH` (`source/Server/Common/Application/Constants.h:74`).
Focused settings/SQL coverage is `39/39`; full Net10 is `2022 passed, 39
skipped, 0 failed`. Delivery batching runtime reconfiguration was deliberately
not added. Next slice: fresh legacy-first audit of one remaining low-risk
Settings mutation. Real SQL/Data rollback, reinitialize, SEC-18, installer,
paired live performance, and soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, WELCOME SMTP MUTATION)

Code/test commit `6408eb8bd` implements only authenticated
`Settings.WelcomeSMTP` (`DispId(23)`, BSTR) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.welcomesmtp` row with a parameterized string command, and changes
the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.WelcomeSMTP` (`DispId(23)`),
`InterfaceSettings::put_WelcomeSMTP`, `SMTPConfiguration::SetWelcomeMessage`,
and `SMTPConnection::SendBanner_` in the legacy IDL, COM, SMTP, and common
application paths. Focused settings/SQL coverage is `37/37`; full Net10 is
`2020 passed, 39 skipped, 0 failed`. SMTP greeting runtime reconfiguration was
deliberately not added. Next slice: fresh legacy-first audit of one remaining
low-risk Settings mutation. Real SQL/Data rollback, reinitialize, SEC-18,
installer, paired live performance, and soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, WELCOME IMAP MUTATION)

Code/test commit `df7f72c22` implements only authenticated
`Settings.WelcomeIMAP` (`DispId(25)`, BSTR) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.welcomeimap` row with a parameterized string command, and changes
the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.WelcomeIMAP` (`DispId(25)`),
`InterfaceSettings::put_WelcomeIMAP`, `IMAPConfiguration::SetWelcomeMessage`,
`PROPERTY_WELCOMEIMAP`, and `IMAPConnection::SendBanner_` in the legacy IDL,
COM, IMAP, and common application paths. Focused settings/SQL coverage is
`35/35`; full Net10 is `2018 passed, 39 skipped, 0 failed`. IMAP greeting
runtime reconfiguration was deliberately not added. Next slice: fresh
legacy-first audit of one remaining low-risk Settings mutation. Real SQL/Data
rollback, reinitialize, SEC-18, installer, paired live performance, and soak
remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, WELCOME POP3 MUTATION)

Code/test commit `67d383ef1` implements only authenticated
`Settings.WelcomePOP3` (`DispId(24)`, BSTR) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.welcomepop3` row with a parameterized string command, and changes
the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.WelcomePOP3`
(`source/Server/hMailServer/hMailServer.idl:547-550`),
`InterfaceSettings::put_WelcomePOP3`
(`source/Server/COM/InterfaceSettings.cpp:713-745`),
`POP3Configuration::SetWelcomeMessage`
(`source/Server/POP3/POP3Configuration.cpp:24-53`), and
`PROPERTY_WELCOMEPOP3` (`source/Server/Common/Application/Constants.h:14`).
Focused settings/SQL coverage is `33/33`; full Net10 is `2016 passed, 39
skipped, 0 failed`. POP3 greeting runtime reconfiguration was deliberately not
added. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Real SQL/Data rollback, reinitialize, SEC-18, installer, paired live
performance, and soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, MAX POP3 CONNECTIONS MUTATION)

Code/test commit `e11234d8a` implements only authenticated
`Settings.MaxPOP3Connections` (`DispId(6)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.maxpop3connections` row with a parameterized integer command,
and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxPOP3Connections`
(`source/Server/hMailServer/hMailServer.idl:531-532`),
`InterfaceSettings::put_MaxPOP3Connections`
(`source/Server/COM/InterfaceSettings.cpp:172-199`), and
`POP3Configuration::SetMaxPOP3Connections`
(`source/Server/POP3/POP3Configuration.cpp:31-39`). Focused settings/SQL
coverage is `31/31`; full Net10 is `2014 passed, 39 skipped, 0 failed`. POP3
listener live reconfiguration was deliberately not added. Next slice: fresh
legacy-first audit of one remaining low-risk Settings mutation. Real SQL/Data
rollback, reinitialize, SEC-18, installer, paired live performance, and soak
remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, MAX SMTP CONNECTIONS MUTATION)

Code/test commit `9d2033677` implements only authenticated
`Settings.MaxSMTPConnections` (`DispId(5)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.maxsmtpconnections` row with a parameterized integer command,
and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.MaxSMTPConnections`
(`source/Server/hMailServer/hMailServer.idl:529`),
`InterfaceSettings::put_MaxSMTPConnections`
(`source/Server/COM/InterfaceSettings.cpp:124`),
`SMTPConfiguration::SetMaxSMTPConnections`
(`source/Server/SMTP/SMTPConfiguration.cpp:51`), and
`Property::WriteLongSetting_` (`source/Server/Common/Application/Property.cpp:71`).
Focused settings/SQL coverage is `29/29`; full Net10 is `2012 passed, 39
skipped, 0 failed`. SMTP listener live reconfiguration was deliberately not
added. Next slice: fresh legacy-first audit of one remaining low-risk Settings
mutation. Real SQL/Data rollback, reinitialize, SEC-18, installer, paired live
performance, and soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, WORKER THREAD PRIORITY MUTATION)

Code/test commit `2e60909b5` implements only authenticated
`Settings.WorkerThreadPriority` (`DispId(57)`) persistence. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.workerthreadpriority` row with a parameterized integer command,
and changes the retained snapshot only after one-row success.

Legacy anchors: `IInterfaceSettings.WorkerThreadPriority`
(`source/Server/hMailServer/hMailServer.idl:599`),
`InterfaceSettings::put_WorkerThreadPriority`
(`source/Server/COM/InterfaceSettings.cpp:1496`),
`Configuration::SetWorkerThreadPriority`
(`source/Server/Common/Application/Configuration.cpp:130`), and
`PROPERTY_WORKERTHREADPRIORITY` (`source/Server/Common/Application/Constants.h:70`).
Focused settings/SQL coverage is `27/27`; full Net10 is `2010 passed, 39
skipped, 0 failed`. Next slice: fresh legacy-first audit of one remaining
low-risk Settings mutation. Real SQL/Data rollback, reinitialize, SEC-18,
installer, paired live performance, and soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, MIRROR EMAIL MUTATION)

Code/test commit `3ba1d5f49` implements only authenticated
`Settings.MirrorEMailAddress` (`DispId(7)`) mutation. It preserves direct
activation denial, rechecks the live administrator callback, updates the fixed
`hm_settings.mirroremailaddress` row with a parameterized command, and changes
the retained snapshot only after one-row success.

Legacy anchors: `InterfaceSettings::put_MirrorEMailAddress`
(`source/Server/COM/InterfaceSettings.cpp:224-241`),
`Configuration::SetMirrorAddress`
(`source/Server/Common/Application/Configuration.cpp:242-248`), and
`PROPERTY_MIRROREMAILADDRESS` (`source/Server/Common/Application/Constants.h:6`).
Focused settings/SQL coverage is `25/25`; full Net10 is `2008 passed, 39
skipped, 0 failed`. Next slice: fresh legacy-first audit of one remaining
Settings setter. Real SQL/Data rollback, reinitialize, SEC-18, installer,
paired live performance, and soak remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, DEFAULTDOMAIN MUTATION)

Code/test commit `41b77dba1` implements the single authenticated
`Settings.DefaultDomain` mutation. It preserves legacy `DispId(50)`/BSTR and
direct activation boundaries, rechecks the live administrator callback, updates
only the existing `hm_settings.defaultdomain` row through a parameterized SQL
command, and changes a retained snapshot only after one-row success.

Legacy anchors: `InterfaceSettings::put_DefaultDomain`
(`source/Server/COM/InterfaceSettings.cpp:1272-1297`),
`Configuration::SetDefaultDomain`
(`source/Server/Common/Application/Configuration.cpp:415-424`), and
`Property::WriteStringSetting_` (`source/Server/Common/Application/Property.cpp:44-97`).
Focused settings/SQL coverage is `23/23`; full Net10 is `2006 passed, 39 skipped,
0 failed`. Next slice: legacy-first `Settings.MirrorEMailAddress` mutation
parity. Real SQL/Data rollback, reinitialize, SEC-18, installer, paired live
performance, and soak gates remain open. Do not push.

# Current Authoritative Continuation (2026-08-10, BOUNDED BACKUP METADATA)

Code/test commit `d77fa9426` bounds 7-Zip metadata extraction to the existing
1 MiB parser limit and adds boundary/overflow tests. Focused coverage is
`BackupManagerComContractTests` `28/28`; full Net10 is `2004 passed, 39 skipped,
0 failed`. No COM identity, restore ordering, production SQL, or Data directory
was changed.

The C++/.NET performance gate is still **RED**. The live evidence has equal
1,000-file message corpora, but the C++ side did not complete the same IMAP,
POP3, or 1,000-concurrent-IMAP matrix, so no speed-up ratio is valid. The next
independent code slice is route-identity validation for restored
`SendUsingRoute` actions. Environment-gated work remains the healthy isolated
C++ binary, SQL Server Full-Text Search, real SQL/Data rollback, SEC-18,
migration/installer, and 24-hour soak. Do not push.

# Current Authoritative Continuation (2026-08-10, COMBINED SETTINGS/DOMAIN RESTORE)

Code/test commit `a8f55de14` extends DB-only restore to the exact
`RestoreSettings|RestoreDomains` selection. Domain metadata is applied before
ordered settings in the same transaction; settings failure disposes before
commit and `smtprelayerpassword` is rejected. Focused coverage is `19/19`;
full default Net10 is `2002 passed, 39 skipped, 0 failed`. Reinitialize,
non-DB combined restore, real SQL/Data rollback, credential round-trip, and
release gates remain open. Next action: run settings/message rollback on the
approved disposable target when available. Release remains RED. Do not push.

# Current Authoritative Continuation (2026-08-10, SETTINGS-ONLY RESTORE EXECUTION)

Code/test commit `a389b0a95` wires settings-only restore into the existing SQL
transaction boundary. The path requires the settings section and transaction,
rejects `smtprelayerpassword` input, preserves order, and disposes the
transaction on failure before commit. Legacy restore order is settings after
domains and before reinitialization (`source/Server/Common/Application/BackupExecuter.cpp:274-335`).
Focused coverage is `17/17`; full default Net10 is `2000 passed, 39 skipped,
0 failed`. Combined settings+domains restore, live reconfiguration, credential
round-trip policy, and disposable SQL/Data acceptance remain open. Next slice:
combined settings+domains DB-only ordering and rollback. Release remains RED.
Do not push.

# Current Authoritative Continuation (2026-08-10, TRANSACTIONAL SETTINGS RESTORE BOUNDARY)

Code/test commit `9dd56fa60` adds the transaction-scoped
`ISettingsRestoreAdministrationStore` and SQL Server update-only path. It
updates existing `hm_settings` rows with parameters and is exposed from the
existing backup-restore transaction; the executor does not call it yet. Focused
coverage is `9/9`; full default Net10 is `1998 passed, 39 skipped, 0 failed`.
Disposable SQL/Data execution, settings rollback, credential handling, and
executor wiring remain unproven. Next slice: wire parsed settings into the
transactional DB-only restore path without live reconfiguration. Release and
performance gates remain RED. Do not push.

# Current Authoritative Continuation (2026-08-10, SETTINGS RESTORE PARSING)

Code/test commit `9b6544736` adds parser-only settings restore coverage.
`BackupArchiveXmlSnapshotParser.ParseSettingsProperties` preserves root
`Properties` child order and legacy missing/invalid attribute defaults without
mutating SQL, runtime settings, or COM state. Legacy anchors are
`PropertySet::XMLLoad`
(`source/Server/Common/Application/PropertySet.cpp:184-213`) and
`Configuration::XMLLoad`
(`source/Server/Common/Application/Configuration.cpp:716-758`). Focused
coverage is `15/15`; full default Net10 is `1997 passed, 39 skipped, 0
failed`. The next slice is an isolated settings restore store boundary with
failure-safe SQL behavior. Settings mutation/rollback, reinitialization,
SEC-18, migration, paired C++/.NET performance, and soak gates remain open;
release remains RED. Do not push.

## Current Authoritative Audit (2026-08-10, RECIPIENT/SEARCH BACKLOG CORRECTION)

Do not restart the old message recipient/search restore item. Legacy
`Message::XMLStore` (`source/Server/Common/BO/Message.cpp:200-218`) does not
serialize recipient children; `PersistentMessage::ReadRecipients_`
(`source/Server/Common/Persistence/PersistentMessage.cpp:231-267`) reads the
runtime SQL table, and `PersistentMessageMetaData::GetMessagesToIndex`
(`source/Server/Common/Persistence/PersistentMessageMetaData.cpp:30-74`)
rebuilds derived search metadata. The .NET
`MessageSearchBackfillProcessor.RunBatchAsync` already provides the missing
index lease/upsert/failure flow. Remaining work is live SQL/FTS/backfill
acceptance, not an XML parser or archive insert slice. Next repository slice:
settings-only restore parsing/validation. Release remains RED.

## Current Authoritative Continuation (2026-08-10, PARTIAL MESSAGE ROLLBACK)

Test commit `02c221769` adds a non-DB restore failure case where message one
inserts and message two fails. The test requires removal of the first message
row, root folder, staged raw files, recovery journal, and rollback artifact,
while restoring the original data directory. Full default Net10 is `1994
passed, 39 skipped, 0 failed`.

The destructive SQL/Data test is present but skipped without the approved
disposable connection and isolated-create opt-in. Next action is to execute
both rollback tests on that isolated target; release remains RED. Do not push.

## Current Authoritative Continuation (2026-08-10, MESSAGE FAILURE ROLLBACK)

Code/test commit `f144fbf86` records the restored root folder before message
insertion. If the first message insert fails, the existing non-DB rollback can
now delete the incomplete folder tree as well as restore the original data
directory. Legacy anchors are
`source/Server/Common/Application/BackupExecuter.cpp:339-388`
(`BackupExecuter::RestoreDataDirectory_`) and
`source/Server/Common/BO/Collection.h:85-135` (`Collection::XMLLoad`).

Focused writer tests pass `3/3`; full default Net10 passes `1994 passed, 38
skipped, 0 failed`. The actual disposable SQL/Data executor test is present but
skipped because the approved SQL connection and isolated-create opt-in are
absent. It must not be reported as PASS; release remains RED. Next slice is
message recipients/search metadata restore or the same test with a real
approved disposable target.

## Current Authoritative Continuation (2026-08-10, RAW MESSAGE-FILE RESTORE)

Test commit `84ca67ee4` proves the isolated executor path against a valid raw
DataBackup graph at `DataBackup/<domain>/<account>/<guid-bucket>/<filename>`.
The raw file is staged before metadata restore; the generated message ID and
archived UID are read back from disposable SQL. Default full Net10 is `1993
passed, 37 skipped, 0 failed`. SQL opt-in remains `2021 passed, 2 skipped`
with six unrelated existing message/indexing fixture failures.

Residual risk: no injected message-insert failure after file staging, no
multi-message rollback, no recipient/search/ACL restore, and no crash-safe
SQL/filesystem atomicity. Release remains RED. Next slice is failure cleanup
acceptance for the raw message graph. Do not push.

## Current Authoritative Continuation (2026-08-10, FOLDER MESSAGE METADATA)

Code/test commit `1b89ae4b8` adds legacy folder-scoped delivered message
metadata restore. Parity anchors inspected were `Message::XMLLoad`,
`PersistentMessage::SaveObject`/`AddObject`, `Messages::PreSaveObject`,
`IMAPFolder::XMLLoadSubItems`, and `hm_messages`. Current anchors are
`BackupArchiveXmlSnapshotParser.ParseFolder`,
`BackupRestoreMetadataWriter.RestoreFoldersAsync`,
`IMessageAdministrationRestoreStore`,
`SqlServerMessageAdministrationStore.InsertMessageForRestoreAsync`, and
`MetadataBackupRestoreExecutor.RestoreMetadataAsync`.

The implementation generates message IDs, preserves nonzero UIDs, remaps
account/folder IDs, uses legacy retry/lock defaults, and leaves
`foldercurrentuid` unchanged. Focused parser + isolated SQL round-trip and
default full Net10 pass (`1992 passed, 37 skipped, 0 failed`). SQL opt-in is
`2021 passed, 2 skipped`, with six unrelated existing message/indexing fixture
failures. Executor-level valid raw-file graph, message failure rollback,
recipient/search/ACL restore, and release gates remain open. Do not push.

Next slice: disposable DataBackup message-file graph plus executor restore and
rollback acceptance.

## Current Authoritative Continuation (2026-08-10, RESTORE COMMIT ROLLBACK)

Code/test commit `915b78a4a` updates
`SqlServerBackupRestoreMetadataTransaction.DisposeAsync` to attempt rollback
after any incomplete commit, including the commit-started failure path. A
provider rollback error after a failed commit is suppressed to retain the
original commit exception. Focused restore/transaction coverage is `12 passed,
0 failed, 0 skipped`; default full Net10 is `1992 passed, 37 skipped, 0
failed`. An injected provider commit-failure test plus crash/power-loss and
SQL/filesystem atomicity evidence remain open. Release is RED.

Next slice: legacy folder message metadata parse/restore with generated IDs and
preserved UIDs; leave message-file staging and ACL restore out of scope.

## Current Authoritative Continuation (2026-08-10, FOLDER METADATA RESTORE)

Code/test commit `5b457d513` completes the bounded folder metadata restore
slice. Legacy anchors inspected were `Account::XMLStore`/
`Account::XMLLoadSubItems`, `IMAPFolder::XMLStore`/
`IMAPFolder::XMLLoadSubItems`, `PersistentIMAPFolder::SaveObject`, and
`IMAPFolders::PreSaveObject` in `hmailserver/source/Server/Common`.

Current anchors are `BackupArchiveXmlSnapshotParser.ParseFolder`,
`BackupRestoreMetadataWriter.RestoreFoldersAsync`,
`MetadataBackupRestoreExecutor.RestoreMetadataAsync`,
`IImapFolderAdministrationRestoreStore`,
`IImapFolderAdministrationRestoreDeletionStore`, and
`SqlServerImapFolderAdministrationStore.InsertFolderForRestoreAsync`.
The slice preserves recursive parent IDs and archived `CurrentUID`/creation
time, and rejects folder message/permission payloads until those slices are
implemented.

Focused parser plus isolated SQL round-trip/rollback coverage is `25 passed,
0 failed, 0 skipped`; default full Net10 is `1992 passed, 37 skipped, 0
failed`. SQL opt-in full execution is `2021 passed, 2 skipped`, with six
unrelated existing message/indexing fixture failures. Release remains RED.
No production service, SQL/Data directory, COM identity, DCOM ACL, IIS, or
machine state changed. Do not push in this run.

Next independent slices: reproducible legacy C++ IMAP/POP3 startup; populated
message/settings restore and rollback; paired SMTP acceptance and delivery
queue after both protocol baselines run.

## Current Authoritative Continuation (2026-08-10, DISPOSABLE LOCALDB AND SQL ACCOUNT VERIFIER)

Code/test commit `f34ee25c8` implements the bounded SQL-backed `Account.ValidatePassword` slice. Legacy behavior is anchored by `InterfaceAccount::ValidatePassword` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:350-364`), `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:109-188`), `Crypt::Validate` (`hmailserver/source/Server/Common/Util/Crypt.cpp:63-84`), and `hm_accounts` (`hmailserver/source/DBScripts/CreateTablesMSSQL.sql:168-194`). Current symbols are `SqlServerAccountPasswordVerifier`, `AccountAdministrationRuntimeHost.Configure`, and `AccountComClass.ValidatePassword`. The query is parameterized and read-only, keyed by immutable account ID, and preserves the legacy script-first, empty-password, AD, and local hash order; it does not add active-account, username, last-logon, or auto-ban policy to the direct COM method. The installed Account COM contract, DISPID 22, direct activation denial, authenticated boundary, SMTP trust, and live reconfiguration are unchanged.

The disposable test environment is reproducible through `build/prepare-net10-disposable-localdb.ps1` and `build/remove-net10-disposable-localdb.ps1`. It uses only the current user's `MSSQLLocalDB`, a marker-protected TEMP Data root, and the explicit isolated-create opt-in. `MSSQLSERVER` and `HmailDb_Test5700` were not used. Focused verifier/COM/password tests passed `70/70`; the SQL verifier integration test passed `4/4` and emitted `artifacts/net10-disposable/SqlServerAccountPasswordVerifier.trx`. Full Net10 passed `2009`, skipped `2`, and failed `9` existing restore/message-index SQL fixture/schema tests. The machine-specific LocalDB JSON/TRX and untracked SEC-18/benchmark artifacts remain uncommitted.

Security review: CONDITIONAL. Reality review: RED for release. Focused SQL evidence covers local rows; live script/AD/COM evidence and SEC-12 rate-limit/auto-ban semantics remain open, along with SEC-18, restore/rollback, migration/installer, out-of-process COM, live load, and 24-hour soak. Do not call this production-ready. Next slice: repair the isolated SQL restore fixture/schema and rerun the restore/rollback matrix, then capture live script/AD/COM verifier evidence.

## Current Authoritative Continuation

2026-08-10 offline benchmark acceptance on current HEAD `7dde90db9` passed the existing synthetic 100k-message IMAP SEARCH/SORT pack: seed `5700`, expected matches `9,091`, `DATE DESC, UID ASC`, correctness true, p50 `6.888 ms`, p95 `7.276 ms`, p99 `7.324 ms`, p95 threshold `<=2500 ms`; JSON/CSV/Markdown artifacts were emitted under a unique `%TEMP%` path. Focused benchmark tests: `4 passed, 0 skipped, 0 failed`. This is offline synthetic evidence only, not live SQL/FTS, 1k IMAP, SMTP/delivery, C++ equivalence, or soak evidence. Preserve the older untracked benchmark artifacts and do not claim the performance release gate green.

Next action: approved disposable SQL/Data restore acceptance when the isolated target exists; keep live performance/load and long-duration soak gates blocked until their required infrastructure is available.

2026-08-10 code/test commit `edacbde75` adds a test-injected account-ID-scoped verifier seam for the legacy `Account.ValidatePassword` gap. Legacy `InterfaceAccount::ValidatePassword` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:350-364`) calls `PasswordValidator::ValidatePassword` without protocol last-logon or auto-ban side effects. The .NET seam performs attached/live-auth checks and forwards only `(accountId, password)`; absent a configured callback, SQL-backed accounts remain `E_NOTIMPL`, direct activation remains `E_ACCESSDENIED`, and Account COM identity/DISPID 22 is unchanged. Focused Accounts: `60 passed, 0 skipped, 0 failed`; full Net10: `1984 passed, 32 skipped, 0 failed`. Security PASS for the preparatory seam; reality YELLOW for the slice and RED for release. No production verifier is wired in `Program.cs`; SQL, hash, AD, script, auto-ban, last-logon, SQL integration, and out-of-process COM evidence remain open.

Next action: approved disposable SQL/Data restore acceptance when its isolated target exists; then independently design/review the authoritative domain-scoped verifier before production wiring. Preserve dirty `AGENTS.md`/backup files and untracked SEC-18/benchmark artifacts.

2026-08-10 code/test commit `f89890421` completes the bounded `Account.UnlockMailbox()` POP3 lock parity slice. Legacy `InterfaceAccount::UnlockMailbox` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:332`) calls process-local `POP3Sessions::Unlock(accountId)` and returns `S_OK`; acquisition/release are anchored by `POP3Connection.cpp:496,831-838`. The .NET path wires the account-ID callback through the service host, Accounts, synthetic Administrator ID 0, and the `Links` fallback account, while owner-matched lease disposal protects a replacement lock. Focused Account/Application/Links/POP3: `87 passed, 0 skipped, 0 failed`; full Net10: `1972 passed, 32 skipped, 2 failed`; AV-excluded full: `1967 passed, 32 skipped, 0 failed`. Security approves the slice; reality remains RED for release. No production SQL/Data, service, COM registration, DCOM, IIS, or firewall state changed.

Next action: approved disposable SQL/Data restore and Message.Save integration when the isolated target exists; otherwise a fresh legacy-first audit of the smallest remaining COM/Admin gap. Preserve dirty `AGENTS.md`/backup files and untracked SEC-18/benchmark artifacts.

2026-08-10 code/test commit `d87b77a15` completes the bounded saved `Rule.MoveUp()`/`MoveDown()` parity slice. Legacy `InterfaceRule::MoveUp/MoveDown`, `Rules::MoveUp/MoveDown`, and `Rules::UpdateSortOrder_()` (`hmailserver/source/Server/COM/InterfaceRule.cpp`; `hmailserver/source/Server/Common/BO/Rules.cpp`) swap adjacent account-owned rules and renumber `hm_rules.rulesortorder` (`hmailserver/source/DBScripts/CreateTablesMSSQL.sql:471-478`). The .NET path uses an owner-scoped transactional `MoveRuleAsync` with `UPDLOCK,HOLDLOCK`, updates the shared generation, and preserves that canonical sort order when a retained Rule is saved after moving. Focused Rule/SQL-contract tests: `30 passed, 0 skipped, 0 failed`; full Net10: `1977 passed, 32 skipped, 2 failed`, with host-AV `.eml` cleanup locks. Security is conditional PASS; reality is YELLOW for the slice and RED for release. Live SQL, out-of-process COM, restore/rollback, SEC-18, migration/installer, load, AD/DC, and soak gates remain open. No production resources or COM identity changed.

Next action: approved disposable SQL Rule-move readback/rollback/ownership/concurrency acceptance when available; otherwise a fresh legacy-first audit of the smallest remaining COM/Admin gap. Preserve dirty `AGENTS.md`/backup files and untracked SEC-18/benchmark artifacts.

2026-08-10 code/test commit `f86733cd8` completes the bounded Diagnostics retained reauthentication slice. Legacy `InterfaceDiagnostics::{PerformTests,get/put_LocalDomainName,get/put_TestDomainName}`, `InterfaceDiagnosticResults::{get_Count,get_Item}`, and `InterfaceDiagnosticResult::{get_Name,get_Description,get_ExecutionDetails,get_Result}` (`hmailserver/source/Server/COM/InterfaceDiagnostics.cpp:12-112`; `InterfaceDiagnosticResults.cpp:11-45`; `InterfaceDiagnosticResult.cpp:8-66`) recheck server-admin authorization on every call and return `0x800403E9` after revocation. The .NET path carries one live callback through `Diagnostics`, `DiagnosticResults`, and `DiagnosticResult`; direct member access remains denied for parameterless objects, and installed COM identity/DISPID/vtable contracts are unchanged.

Focused result: `7 passed, 0 failed, 0 skipped`. Full Net10: `1967 passed, 32 skipped, 2 failed`; both failures are host-AV locks on generated scanner `.eml` cleanup. Security disposition PASS for the slice; reality RED for release. The SQL/Data restore target and opt-in remain unavailable, and SEC-18, migration/installer, service/COM, live performance/load, AD/DC, crash/power-loss, and soak gates remain unproven. Diagnostics runtime execution remains test-configured only. No production resources or registration were touched.

Next action: approved disposable SQL/Data restore plus Message.Save integration when available; otherwise fresh legacy-first audit of the smallest remaining COM/Admin gap. Preserve dirty `AGENTS.md`/backup files and untracked SEC-18/benchmark artifacts.

2026-08-10 code/test commit `cdfc000ad` completes the parity-confirmed unsaved Rule movement error slice. Legacy `InterfaceRules::Add` and `InterfaceRule::MoveUp/MoveDown` (`hmailserver/source/Server/COM/InterfaceRules.cpp`; `InterfaceRule.cpp:221`) return HRESULT `0x800403E9` with `Object not yet saved.` for ID-zero drafts before movement or SQL. The .NET `Rule.MoveUp/MoveDown` branch now matches; saved-rule movement remains deliberately `E_NOTIMPL` and out of scope. Focused Rules: `19 passed, 0 failed, 0 skipped`; full Net10: `1968 passed, 32 skipped, 2 failed`, with known host-AV scanner cleanup locks. Security PASS for slice, reality RED for release. No production resources or COM identity changed.

Next action remains approved disposable SQL/Data restore and Message.Save integration when available, otherwise another legacy-first narrow COM/Admin audit. Preserve unrelated dirty files and untracked SEC-18/benchmark artifacts.

2026-08-10 code/test commit `c1b1734c0` completes the parity-confirmed IMAP `Message.Save()` state/UID and multi-draft publication slice. Legacy `InterfaceMessages::Add` and `InterfaceMessage::Save` (`hmailserver/source/Server/COM/InterfaceMessages.cpp:102-138`; `InterfaceMessage.cpp:390-516`) keep ID-zero Created drafts out of the parent collection, then each Save performs one Delivered insert. `PersistentMessage::AddObject` and `PersistentIMAPFolder::GetUniqueMessageID` (`PersistentMessage.cpp:542-666`; `PersistentIMAPFolder.cpp:236-247`) assign one generated ID and folder UID per save. The .NET store now allocates `foldercurrentuid` and inserts `hm_messages` transactionally, returns ID/state/UID, owner-scopes folder reads, and publishes against the live collection once, preserving both drafts when multiple unsaved items are saved in either order.

Focused result: `39 passed, 1 skipped, 0 failed`; the SQL integration test is skipped because `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` and `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1` are unset. Full Net10: `1965 passed, 32 skipped, 2 failed`; the failures are host-AV locks on generated scanner `.eml` cleanup. Security disposition is YELLOW for the bounded slice; MIME file persistence for COM-created drafts and cross-writer UID coordination remain residual risks. Reality is RED for release. No production resources or installed COM identity were changed.

Next action is approved disposable SQL/Data restore and Message.Save integration acceptance when the isolated target exists; absent that environment, perform a fresh legacy-anchored audit of the smallest remaining COM/Admin gap. Do not restart completed backup/raw staging, DNSBL, IMAP ownership, or this Message.Save slice. Preserve dirty `AGENTS.md`/backup files and untracked SEC-18/benchmark artifacts.

2026-08-10 code/test commit `e311058e8` closes the bounded IMAP empty-folder owner-ID and stale-folder insertion gap. Legacy `InterfaceIMAPFolder::get_Messages` and `InterfaceMessages::Add` (`hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:161-178`; `InterfaceMessages.cpp:102-130`) use the owning account/folder IDs even for empty folders. Legacy retained non-INBOX folder save fails before `hm_messages` insert because `PersistentMessage::AddObject` calls `PersistentIMAPFolder::GetCurrentUID_` and the deleted folder row is absent (`PersistentMessage.cpp:587-618`; `PersistentIMAPFolder.cpp:193-223`). The .NET path now passes `ImapFolderAdministrationSnapshot.AccountId` from `IMAPFolder.Messages` into `CreateAuthorizedFolderAdapter`, and `InsertMessageSql` requires matching folder/account rows atomically with `UPDLOCK,HOLDLOCK`.

Focused message/store/IMAP coverage: `36 passed, 5 skipped, 0 failed`. Full Net10: `1962 passed, 32 skipped, 2 failed`; both failures are host-AV locks on generated scanner `.eml` cleanup. The approved disposable SQL retained-folder test is skipped because its connection and isolated-create opt-in are unset. Security disposition is YELLOW for the bounded slice due the separate legacy Message.Save delivered-state/folder-UID gap; reality is RED for release. No production SQL/Data, service, COM registration, DCOM, IIS, or firewall state changed.

Next code slice: parity-confirm IMAP `Message.Save()` delivered state and folder UID publication without broadening into protocol APPEND or live reconfiguration. Next gates after that remain approved populated SQL/Data restore acceptance and AV-compatible scanner cleanup. Preserve unrelated dirty AGENTS/backup changes and untracked SEC-18/benchmark artifacts.

## Current Authoritative Continuation

2026-08-09 code/test commit `e279ac725` closes the bounded DNSBL missing-host COM status gap. Legacy `InterfaceDNSBlackLists::get_ItemByDNSHost` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:168-184`) performs a case-insensitive lookup and explicitly returns `S_FALSE` (`0x00000001`) when no DNS host matches. The .NET `DNSBlackLists.get_ItemByDNSHost` (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/DnsBlackLists.cs:208-222`) now preserves that HRESULT; hit lookup, owner snapshot, authenticated Settings access, direct activation denial, and SMTP DNSBL paths are unchanged. IDL `IInterfaceDNSBlackLists` DISPID 7 and installed COM identity remain unchanged.

Focused `DnsBlackListsComContractTests`: `15 passed, 0 failed, 0 skipped`. DNSBL plus the related SQL integration class: `27 passed, 0 failed, 0 skipped`. Full Net10: `1961 passed, 31 skipped, 2 failed`; both failures are host-AV locks deleting generated scanner `.eml` files. Security review is GREEN for the bounded slice; reality review is RED for release. No production SQL/Data, service, COM registration, DCOM, IIS, or firewall state was touched.

The next independent gates are approved populated SQL/Data restore acceptance, live SQL/FTS or protocol performance acceptance, and AV-compatible scanner cleanup. The required SQL/Data connection and isolated-create opt-in remain unset. Do not restart the stale IMAP alias/default-domain or `FetchAccount.DownloadNow` entries; both are already implemented.

## Current Authoritative Continuation

2026-08-09 code/test commit `23fd5ef74` closes a small COM error-contract parity gap. Legacy `InterfaceLanguage::Download` (`hmailserver/source/Server/COM/InterfaceLanguage.cpp:67`) calls `COMError::GenerateError("Not implemented.")` (`COMError.cpp:24`), returning HRESULT `0x800403E9`; the .NET `Language.Download` (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/Languages.cs:141`) previously returned `E_NOTIMPL` and now matches the legacy HRESULT/message. `GlobalObjectsComContractTests` passes `8/8`; full Net10 is `1961 passed, 31 skipped, 2 failed`, with the two known host-AV `.eml` cleanup failures. No COM identity, direct activation, SQL/Data, IIS, service, or production state changed.

The earlier IMAP domain-alias/default-domain item and `FetchAccount.DownloadNow` item were rechecked and are already implemented; do not restart those stale entries. The next independent slices are approved disposable SQL/Data restore acceptance, live SQL/FTS or protocol performance acceptance, and AV-compatible scanner cleanup. Release remains RED.

## Current Authoritative Continuation

2026-08-09 code/test commit `508d35d17` implements the parity-confirmed obsolete AntiSpam setter slice. Legacy `InterfaceAntiSpam::get_TarpitDelay`, `put_TarpitDelay`, `get_TarpitCount`, and `put_TarpitCount` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:729-792`) return `0` for getters and authenticated `S_OK` no-op for setters. The .NET `AntiSpam` setters (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AntiSpam.cs:292-294`) now check the authorized facade and ignore the obsolete values. `AntiSpamComContractTests` covers authorized no-op behavior and direct-activation `E_ACCESSDENIED`; no SQL, protocol, COM identity, or production state changed.

Focused result: `15 passed, 0 failed, 0 skipped`. Full Net10: `1961 passed, 31 skipped, 2 failed`; both failures are host-AV locks on generated scanner `.eml` cleanup. Security review found no COM identity or direct-activation regression. The review’s retained AntiSpam concern was compared with legacy `InterfaceAntiSpam::LoadSettings` (`InterfaceAntiSpam.cpp:28-35`): the cached `config_` lifetime and retained-operation test intentionally match legacy behavior, so it is not part of this slice. Release reality remains RED.

The attempted IMAP domain-alias/default-domain slice was not implemented because parity inspection found it already present in `SqlServerImapAccountAuthenticator.AccountLookupSql` and `AuthenticateNormalAsync`; the backlog entry is stale. Next independent slices: approved disposable SQL/Data restore acceptance; live SQL/FTS or protocol performance acceptance; AV-compatible scanner cleanup and clean default-suite evidence. Do not use production SQL/Data or stage protected SEC-18/benchmark artifacts.

## Current Authoritative Continuation

2026-08-09 release-gate revalidation: the retained Domain child-collection parity audit found no new code gap. Legacy `InterfaceDomain::get_Accounts`, `get_Aliases`, `get_DomainAliases`, and `get_DistributionLists` (`hmailserver/source/Server/COM/InterfaceDomain.cpp:308-478`) attach the shared authentication state. The .NET `Domain` adapter (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/Domains.cs:811-821,882-889`) evaluates its guarded snapshot before child adapter creation and passes the live callback into the existing owner-scoped adapters. `DomainsComContractTests`, `LinksComContractTests`, and `WebAdminRoutePostOnlySourceTests` pass `27/27`; no production code changed. The historical route handler hardening is complete in `8d684e638`; do not restart it.

Reality review is RED for release. The required disposable SQL/Data connection and isolated-create opt-in are unset, so populated restore commit/rollback, live SQL/FTS, protocol/load, service/COM, SEC-18, installer, AD/DC, migration/rollback, and 24-hour soak gates cannot be accepted. The default suite remains blocked by two host-AV `.eml` cleanup failures. The untracked `artifacts/benchmarks/` directory contains an older `d7d5cb6c4` run and must remain unstaged; the newer `565175aff` run was temporary. Next action is an approved disposable SQL/Data target; until then, no independently executable release-gate slice remains on this host.

2026-08-09 backup creation revalidation: the stale raw non-DB-only `BODomains|BOMessages` `DataBackup` item is already complete in `50d8cefc3`, and the complete option matrix is covered by `d210c5611`. Legacy anchors are `BackupExecuter::StartBackup`/`BackupDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:57-147,172-217`), `FileUtilities::CopyDirectory`/`DeleteFilesInDirectory`, and `Compression::AddDirectory`; the .NET implementation is `SevenZipBackupArchiveRuntime.CreateAsync`. Revalidation passed `150 passed, 0 failed, 0 skipped`, and `check-net10-prereqs.ps1 -RequireMsBuild` passed. Do not restart raw staging. The next gate remains disposable SQL/Data restore acceptance with an approved isolated connection and opt-in.

2026-08-09 code/test commit `414b1e9e0` closes the bounded ClamAV local-target hostname re-resolution window. Legacy `InterfaceAntiVirus::TestClamAVScanner` (`hmailserver/source/Server/COM/InterfaceAntiVirus.cpp:577-596`) delegates the supplied hostname through `VirusScannerTester::TestClamAVConnect` (`hmailserver/source/Server/Common/AntiVirus/VirusScannerTester.cpp:22-45`) to `ClamAVVirusScanner::Scan` and `SynchronousConnection::Connect` (`hmailserver/source/Server/Common/AntiVirus/ClamAVVirusScanner.cpp:48-64`). The .NET path resolves and validates a local hostname once in `LegacyLocalScannerTargetGuard.TryGetValidatedLocalAddress`, then passes the resulting IP literal to `IClamAvScannerTestRuntime`, preventing a second DNS lookup in `ClamAvInstreamClient` for this COM test flow.

Focused coverage is `20 passed, 0 failed, 0 skipped`; filtered full Net10 is `1954 passed, 0 failed, 31 skipped`; default full is `1959 passed, 2 failed, 31 skipped`, with the two known host-AV scanner cleanup failures. The general configured-message ClamAV path was deliberately not changed. Security disposition is GREEN for this bounded target handoff; release reality remains RED. Next slice: approved disposable SQL/Data restore acceptance when its isolated integration connection and opt-in exist. Do not use production SQL/Data or stage protected SEC-18/benchmark artifacts.

2026-08-09 code/test commit `3c8b58981` implements retained AntiVirus authorization parity. Legacy anchors are `InterfaceSettings::get_AntiVirus` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:387-405`) and all `InterfaceAntiVirus` public members (`hmailserver/source/Server/COM/InterfaceAntiVirus.cpp:20-581`), which require server-admin authentication at method entry. The .NET `AntiVirus.Snapshot` now rechecks the live administrator callback, and `BlockedAttachments.GetBlockedAttachments` fail-closes retained collection access and `DeleteByDBID` after revocation. The latter is intentional security hardening because the legacy collection method checked only the attached parent pointer; child Save/Delete already had live callback checks.

Focused coverage is `27 passed, 0 failed, 0 skipped`; filtered full Net10 is `1951 passed, 0 failed, 31 skipped`; default full is `1956 passed, 2 failed, 31 skipped`. The two default failures are host AV cleanup locks on generated `.eml` files in the ClamWin and custom scanner runtime tests. Security disposition is GREEN for this bounded fail-closed slice; release reality remains RED. Installed COM identity, direct activation boundaries, SMTP trust, live reconfiguration, SQL/Data, service, IIS, registry, DCOM, and production state are unchanged.

Next code/test slice: parity-confirm ClamAV local-target DNS-rebind hardening. `AntiVirus.TestClamAVScanner` validates a local hostname before invoking `ClamAvInstreamClient`, but the client can resolve the hostname again at connection time. Do not broaden into scanner redesign. The approved disposable SQL/Data restore gate remains blocked by missing approved integration connection and isolated-create opt-in. Do not stage protected SEC-18 or benchmark artifacts.

## Current Authoritative Continuation

2026-08-09 code/test commit `e2109f422` completes retained MessageIndexing authorization parity. Legacy anchors are `InterfaceSettings::get_MessageIndexing` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1974-1990`) and `InterfaceMessageIndexing::{get_TotalMessageCount,get_TotalIndexedCount,Clear,Index}` (`hmailserver/source/Server/COM/InterfaceMessageIndexing.cpp:64-137`), which recheck the attached server-admin authentication; legacy `get_Enabled/put_Enabled` (`InterfaceMessageIndexing.cpp:30-62`) intentionally do not. The .NET `Settings.MessageIndexing` now passes the live admin callback into retained facades. Legacy methods plus the extended `MessageIndexing2` status properties and destructive `Rebuild` recheck authorization; direct activation remains `E_ACCESSDENIED`.

Focused MessageIndexing/Settings coverage is `25 passed, 0 failed, 0 skipped`; filtered full Net10 is `1949 passed, 0 failed, 31 skipped`; default full is `1954 passed, 2 failed, 31 skipped`. The two failures are host-AV cleanup locks on generated scanner `.eml` files. Security review’s retained extended-operation finding is addressed; reality disposition is RED for release. No COM identity, IDL, SQL/Data, SMTP trust, live reconfiguration, service, registry, DCOM, IIS, or production state changed.

Next slice: fresh parity audit of remaining COM/Admin child-facade paths, beginning with exact legacy behavior and current .NET coverage. The approved disposable SQL/Data restore gate remains environment-blocked by its missing approved integration connection and isolated-create opt-in. Do not access production SQL/Data or stage unrelated SEC-18/benchmark artifacts.

2026-08-09 parity audit result: no additional safe COM/Admin code slice was found. `InterfaceServerMessages::{LoadSettings,Refresh,get_Count,get_Item,get_ItemByDBID,get_ItemByName}` (`hmailserver/source/Server/COM/InterfaceServerMessages.cpp:13-146`) authorizes at collection acquisition and attaches authentication to returned child objects, matching current `ServerMessages.cs:62-304` acquisition-scoped behavior. `InterfaceGlobalObjects::get_Languages` (`InterfaceGlobalObjects.cpp:58-76`), `InterfaceLanguages::{get_Count,get_Item,get_ItemByName}` (`InterfaceLanguages.cpp:12-73`), and `InterfaceLanguage` (`InterfaceLanguage.cpp:13-80`) do not carry live authentication; the proposed .NET callback propagation was rejected as a legacy retained-read regression. No production code changed.

The performance audit confirmed the existing offline 100k SEARCH/SORT gate is already implemented and reran it at HEAD `565175aff`: Release build 0 warnings/0 errors, p50/p95/p99 `6.839/13.904/16.184 ms`, correctness and threshold passed, JSON/CSV/Markdown artifacts validated in a temporary directory. Short soak remains diagnostic because it repeats synthetic in-process LINQ work and samples host-wide TCP state. Next execution remains approved disposable SQL/Data restore acceptance; required connection and isolated-create opt-in are unset.

2026-08-09 code/test commit `44e41839f` completes retained child-facade authorization propagation for `GroupMember.Account` and `IMAPFolderPermission.Account/Group`, including `Settings.PublicFolders` snapshot-only folders. Legacy anchors are `InterfaceGroupMember::get_Account` (`hmailserver/source/Server/COM/InterfaceGroupMember.cpp:125-145`), `InterfaceIMAPFolderPermission::get_Account/get_Group` (`InterfaceIMAPFolderPermission.cpp:265-315`), `InterfaceSettings::get_PublicFolders` (`InterfaceSettings.cpp:1865-1884`), `InterfaceIMAPFolders` item factories (`InterfaceIMAPFolders.cpp:55-133`), and `InterfaceIMAPFolder::get_Permissions` (`InterfaceIMAPFolder.cpp:218-244`).

Focused GroupMember/IMAP coverage is `32 passed, 0 failed, 0 skipped`; filtered full Net10 is `1948 passed, 0 failed, 31 skipped`; default full is `1953 passed, 2 failed, 31 skipped`, with both failures caused by host AV locking generated scanner `.eml` files during cleanup. Security review found no actionable issue after the PublicFolders route was covered; reality disposition is RED for release. COM IID/CLSID/ProgID/DISPID/vtable/type-library identity, direct activation denial, SQL ownership, SMTP trust, live reconfiguration, service, registry, DCOM, IIS, and production state are unchanged.

Next slice: fresh parity audit of remaining COM/Admin child-facade paths, starting only after exact legacy behavior and current .NET coverage are mapped. The approved disposable SQL/Data restore gate remains environment-blocked by missing approved integration connection and isolated-create opt-in. Do not access production SQL/Data or stage unrelated SEC-18/benchmark artifacts.

2026-08-09 code/test commit `5542ced99` completes retained Message `Attachments`/`Recipients` and Rule `Criterias` authorization propagation. Legacy anchors are `InterfaceMessage::get_Attachments` (`hmailserver/source/Server/COM/InterfaceMessage.cpp:336-357`), `get_Recipients` (`:736-755`), `InterfaceRule::get_Criterias` (`hmailserver/source/Server/COM/InterfaceRule.cpp:168-189`), and `InterfaceRuleCriterias::{get_Item,get_ItemByDBID,Add}` (`InterfaceRuleCriterias.cpp:15-72,90-118`). Focused coverage is `60 passed, 0 failed, 0 skipped`; filtered full is `1945 passed, 0 failed, 31 skipped`; the default full remains `1947 passed, 2 failed, 31 skipped` due host AV locks on scanner `.eml` cleanup.

Security review is GREEN for the bounded slice; release reality remains RED. MessageHeaders were intentionally not changed because legacy `InterfaceMessage::get_Headers` (`InterfaceMessage.cpp:363-388`) does not attach `COMAuthentication`. No COM identity, IDL, direct activation, SQL ownership, SMTP trust, live reconfiguration, service, registry, DCOM, IIS, or production state changed. Next slice: parity-confirm nested Domain collection authorization and add owner/stale-child denial tests; SQL/Data restore and all other release gates remain open.

## Current Authoritative Continuation

2026-08-09 code/test commit `1bbffd74d` closes the bounded retained draft and Account identity authorization gaps. Legacy `InterfaceAccount::SetAuthentication` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:53-56`) is attached through `get_Messages` (`:420-445`), `get_FetchAccounts` (`:694-721`), `get_Rules` (`:790-815`), and `get_IMAPFolders` (`:817-845`); scalar `get_AdminLevel` (`:723-738`) rechecks it as well. The .NET implementation now rejects direct parameterless `Account.ID` reads and rechecks authentication before retained `FetchAccount` and `Message` draft setters stage changes. Focused coverage is `81 passed, 0 failed, 0 skipped`; the filtered full assembly is `1944 passed, 0 failed, 31 skipped`. The default full suite remains `1947 passed, 2 failed, 31 skipped` because host AV locks generated scanner `.eml` files during cleanup.

Security review is clean for this bounded slice; reality disposition is YELLOW for the slice and RED for release. No installed COM identity, IDL, SQL schema, SMTP trust, live reconfiguration, service, registry, DCOM, IIS, or production state changed. Next code/test slice: carry reauthentication through Message `Attachments`/`Recipients`/`Headers` and Rule `Criterias`, then cover retained-child denial; nested Domain collections and all release gates remain open.

## Current Authoritative Continuation

2026-08-09 code/test commit `9b93e9c34` completes retained Account child authorization propagation after the earlier direct Links child slice. Legacy `InterfaceAccount::SetAuthentication` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:53-56`) is now represented through Account `AdminLevel`, `Messages`/`Message`, `FetchAccounts`/`FetchAccount`, `Rules`, and folder `Messages`. Focused Account/Links coverage is `123 passed, 0 failed, 0 skipped`; default full is `1947 passed, 2 failed, 31 skipped` because host AV locked generated scanner `.eml` files during cleanup; excluding `ClamWinScannerTestRuntimeTests` and `CustomScannerTestRuntimeTests` gives `1942 passed, 0 failed, 31 skipped`.

Only the seven bounded code/test files were changed; no COM identity, IDL, direct activation, SQL schema/owner scope, SMTP trust, live reconfiguration, service, registry, DCOM, or production state changed. Next code/test slice: add retained-child negative coverage for Rules and IMAPFolders plus post-logout draft setter denial, then rerun scanner tests on an AV-compatible isolated test path. Nested Domain collections and all release gates remain open; release remains RED.

2026-08-09 code/test commit `52d000029` completes the bounded direct-child authorization propagation slice for retained `Application.Links`. Legacy anchors are `InterfaceLinks::{get_Domain,get_Account,get_Alias,get_DistributionList}` (`hmailserver/source/Server/COM/InterfaceLinks.cpp:22-138`), which attach shared `COMAuthentication` to every returned child. The .NET path passes the live administrator guard into Domain, Alias, DistributionList, and Account facades; retained direct children deny reads and mutations after failed reauthentication and restore access after successful reauthentication. DistributionList has a separate read callback so standalone collection scalar-read compatibility remains unchanged.

Focused Links/related tests are `78 passed, 0 failed, 0 skipped`; full Net10 is `1945 passed, 0 failed, 31 skipped`; `git diff --check` passed. No installed COM identity, direct activation boundary, SQL schema/query scope, SMTP trust, live reconfiguration, service, registry, DCOM, or production state changed. Residual gap: nested `Account.Messages`, `FetchAccounts`, `Rules`, `IMAPFolders`, `AdminLevel`, and nested Domain collections still require their own callback propagation and negative retained-object tests. The approved disposable SQL/Data restore acceptance remains environment-gated; release remains RED.

Next code slice: propagate retained authorization through the Links-reachable nested Account and Domain collection graph, starting with `Account.AdminLevel` and `Account.Messages`/`FetchAccounts` callback boundaries. Do not broaden to protocol, SMTP trust, COM registration, DCOM, or production resources.

2026-08-09 code/test commit `e5b441b36` completes root retained `Links` authorization parity. Legacy anchors are `InterfaceApplication::get_Links` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:457-479`) and `InterfaceLinks` getters (`InterfaceLinks.cpp:22-138`), which recheck shared authentication before each store lookup. The .NET path passes a live admin guard into `Links`, preserves legacy `0x800403E9` plus the standard access-denied message, performs no store reads after failed reauthentication, and restores access after successful reauthentication. Focused `LinksComContractTests` is `8 passed, 0 failed, 0 skipped`; full Net10 is `1944 passed, 0 failed, 31 skipped`.

The slice preserves direct activation denial and installed Links COM identity. It deliberately stops at root `Links` getters; child facades returned by Links still need a separate authorization-propagation slice, especially `Account` nested access. No SQL, SMTP, service, registry, DCOM, or production state changed. Next code candidate: child-facade reauthentication parity; highest-priority environment-gated work remains disposable SQL/Data restore acceptance. Release remains RED.

## Current Authoritative Continuation

2026-08-09 code/test commit `89cc2c860` implements retained `GlobalObjects`/`DeliveryQueue` authorization parity. Legacy anchors are `InterfaceApplication::get_GlobalObjects` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:163`), `InterfaceGlobalObjects::get_DeliveryQueue` (`InterfaceGlobalObjects.cpp:34-54`), and `InterfaceDeliveryQueue::{Clear,ResetDeliveryTime,Remove,StartDelivery}` (`InterfaceDeliveryQueue.cpp:14-91`). The .NET path passes a live `ApplicationAuthorizationAuthority.IsServerAdministrator` guard into retained objects. Focused coverage is `8 passed, 0 failed, 0 skipped`; full Net10 is `1943 passed, 0 failed, 31 skipped`.

The slice preserves COM identity, direct activation denial, legacy `Clear` HRESULT/message, and `S_FALSE` for unauthorized `ResetDeliveryTime`, `Remove`, and `StartDelivery`. It is method-entry authorization, so check-then-use invalidation races and asynchronous `Clear` scheduling remain residual risks. No SQL, SMTP, service, registry, DCOM, or production state changed. Next code candidate: parity-confirm retained `Links` live authorization; highest-priority environment-gated work remains disposable SQL/Data restore acceptance. Release remains RED.

## Current Audit Continuation

2026-08-09 release-gate audit: no independently executable production-parity slice remains unblocked on this host. Legacy `InterfaceAccount::ValidatePassword` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:350-364`) uses the attached account and legacy `PasswordValidator` branches; the .NET `Account.ValidatePassword` (`hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AccountComClass.cs:417-426`) must remain `E_NOTIMPL` for SQL-backed snapshots until a fresh credential verifier, retained-object reauthentication, and reviewed COM/AD/script boundary exist. The protocol authenticator must not be reused as a shortcut.

The offline short-soak mode is diagnostic only and was not promoted to the release gate. The commandable 100,000-message SEARCH/SORT gate passes but does not prove live SQL FTS, protocol, C++ equivalence, concurrency, delivery, or 24-hour soak behavior. Latest full Net10 is `1942 passed, 0 failed, 31 skipped`; release status is RED.

Next execution remains approved disposable SQL/Data restore acceptance. Required SQL/Data opt-in is unset, so no production code change was made in this audit. Protected dirty source and untracked SEC18/benchmark artifacts remain untouched.

## Current Audit Continuation

2026-08-09 parity audit: do not restart the stale backlog entries for `RuleCriteria.MatchValue`, `hm_status.php`, `hm_backup.php`, `background_servermessage_save.php`, or DistributionLists Add/Save. Current source/tests already cover those boundaries. The distribution-list legacy anchors are `InterfaceDistributionLists::Add`, `InterfaceDistributionList::Save`, `PersistentDistributionList::SaveObject`, `hm_distributionlists`, and the installed IDL; the .NET path preserves owner-scoped defaults, six-field identity insert, failed-draft retention, authenticated access, and direct activation denial. Remaining evidence is isolated live SQL/COM acceptance only.

The next authoritative slice remains approved disposable SQL/Data restore acceptance. Current evidence shows the SQL service running but all required integration environment variables unset. Do not access production SQL/Data or stage unrelated SEC-18/benchmark artifacts. The project remains release RED.

## Current Authoritative Continuation

Authoritative 2026-08-09 continuation: code/test commit `2925427d2` adds internal `ReinitializationAdmission` in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/ReinitializationAdmission.cs` with three focused tests. The gate uses atomic single-flight admission, drops duplicate requests while an attempt is running, and releases the gate in `finally` after success or exception.

Parity anchors are legacy `Application::Reinitialize` (`hmailserver/source/Server/Common/Application/Application.cpp:437-450`), `Reinitializator::ReInitialize`/`WorkerFunc` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:35-57`), `InterfaceApplication::Reinitialize` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:91-108`), and IDL `IInterfaceApplication::Reinitialize` (`hmailserver/source/Server/hMailServer/hMailServer.idl:1491-1497`). The legacy operation is a full stop/exit/init/start lifecycle and its asynchronous wrapper drops requests while running. The .NET helper is intentionally unused: `ApplicationComClass.Reinitialize()` remains `E_NOTIMPL`, and no listener, SQL/configuration, readiness, restore, COM, DCOM, SMTP, or live-reconfiguration behavior changed.

Verified coverage: focused `3 passed, 0 failed, 0 skipped`; default full Net10 `1942 passed, 0 failed, 31 skipped`; `git diff --check` passed. Security review is GREEN for this bounded non-live seam; reality review is GREEN only for the bounded slice and RED for release. The next independent gate is approved disposable SQL/Data restore acceptance; the target must be explicitly disposable and must not use production SQL/Data. Native restore containment remains blocked by this host’s `ERROR_INVALID_PARAMETER` on the safe RootDirectory-relative rename path.

## Current Authoritative Continuation

Authoritative 2026-08-09 continuation: code/test commit `5d0e62192` adds `build/test-net10-benchmarks.ps1`. It builds the existing benchmark project in Release mode, invokes the fixed 100,000-message offline SEARCH/SORT scenario with seed `5700`, and validates the JSON/CSV/Markdown artifacts, correctness, threshold, timestamps, runtime description, and exact pre-build HEAD metadata. Parity anchors are `IMAPCommandSEARCH::ExecuteCommand`/`DoesMessageMatch_` (`hmailserver/source/Server/IMAP/IMAPCommandSearch.cpp:40`), `IMAPSearchParser::ParseCommand` (`hmailserver/source/Server/IMAP/IMAPSearchParser.cpp:118`), `IMAPSortParser::Parse` (`hmailserver/source/Server/IMAP/IMAPSortParser.cpp:24`), and `IMAPSort::Sort`/`CacheHeaderFields_` (`hmailserver/source/Server/IMAP/IMAPSort.cpp:32`).

Verified results: focused benchmark tests `7 passed, 0 failed, 0 skipped`; full Net10 `1939 passed, 0 failed, 31 skipped`; Release gate p50 `6.695 ms`, p95 `7.261 ms`, p99 `7.322 ms`, `9091/9091` correct matches, threshold passed. The temporary artifacts were not staged. This is an offline synthetic gate only and does not establish live SQL FTS, protocol, C++ baseline, 1,000-connection, delivery, restore, or 24-hour soak acceptance.

Security/reality disposition remains RED for release. The benchmark script is bounded and fails closed on artifact/metadata mismatch, but the highest-value follow-up is an approved disposable SQL/FTS and live protocol benchmark with explicit thresholds and machine/runtime/configuration metadata. The attempted native restore containment slice remains uncommitted: this Windows host returns `ERROR_INVALID_PARAMETER` for the safe RootDirectory-relative rename path, and the absolute fallback was rejected because it reintroduces a destination TOCTOU risk. No production service, SQL/Data directory, COM identity, DCOM ACL, IIS, registry, or firewall state changed.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `3e912982a` adds the internal `WindowsBackupRestoreDataDirectoryMutation` and injects it into `BackupRestoreDataDirectoryRuntime` for target-to-rollback and rollback-to-target swaps. The helper opens the source directory with `CreateFileW`, uses a correctly packed non-overwriting `FILE_RENAME_INFO` request with an absolute destination, and is covered by `17 passed, 0 failed, 0 skipped` focused runtime tests. Default full Net10 is `1939 passed, 0 failed, 29 skipped`.

Legacy anchors are `BackupExecuter::RestoreDataDirectory_` and `FileUtilities::CopyDirectory` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:339-388`, `hmailserver/source/Server/Common/Util/FileUtilities.cpp:370-402`). The slice is deliberately bounded: destination-parent resolution, recursive `CopyTree`, and cleanup deletion remain path-based and are not claimed as full handle-relative containment. No COM identity/IDL, SQL, protocol, service, recovery-journal, or production state changed. Release remains RED. Next slice: full-restore public-folder deletion with staged message-file cleanup and reinitialization ordering on isolated disposable SQL/Data; native destination/copy containment remains open.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `5d9ad666c` adds `IBackupRestoreMetadataTransaction.DeleteAllPublicFoldersForRestoreAsync` and the SQL implementation `SqlServerImapFolderAdministrationStore.DeleteAllPublicFoldersForRestoreSql`. It selects only `folderaccountid = 0` public folders and `messageaccountid = 0` public messages, removes recipients for non-Delivered messages plus search queue/documents and metadata, removes ACLs and messages, preserves the legacy root Inbox row, and does not begin, commit, or roll back the caller-owned transaction. Focused store coverage is `11 passed, 0 failed, 0 skipped`; default full Net10 is `1937 passed, 0 failed, 29 skipped`.

Legacy anchors: `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), public-folder `DeleteAll()` (`BackupExecuter.cpp:287-289`), `PersistentIMAPFolder::DeleteObject`, and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:35-57`). The capability is not called by DB-only restore: legacy skips public-folder deletion for `bMessagesDBOnly`, and current DB-only restore does not restore public-folder metadata/files. Do not wire it until full-restore file staging, rollback, and reinitialization ordering are implemented on isolated disposable SQL/Data. No COM identity, direct activation boundary, SMTP trust, production service/SQL/Data, DCOM, IIS, or registry state changed. Release remains RED. Next slice: full-restore public-folder deletion with staged message-file cleanup and reinitialization ordering.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `a2b030d82` wires `IBackupRestoreMetadataTransaction.DeleteAllDomainsForRestoreAsync` into the DB-only restore executor. The executor now validates duplicate names within the archive, acquires the existing authorization lease, opens the SQL transaction, clears the existing domain graph exactly once, then inserts archive metadata through the same transaction-scoped stores. The non-DB path still requires an empty store and does not invoke the destructive SQL cleanup.

Legacy anchors are `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-215`), `PersistentAccount::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:55-100`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:35-57`). Focused restore execution and round-trip coverage is `15 passed, 0 failed, 11 skipped`; default full Net10 is `1936 passed, 0 failed, 29 skipped`. Tests prove deletion-before-insert, existing-name replacement, injected deletion failure before metadata writes, and transaction wrapper compatibility. The disposable SQL commit/rollback and populated-store executor acceptance remain skipped without the approved SQL/Data environment.

No COM identity, IDL, direct activation boundary, SMTP trust, production service/SQL/Data, DCOM, IIS, or registry state changed. Release remains RED: full legacy filesystem/public-folder/settings/reinitialization ordering, native containment, process-kill/power-loss, SQL/filesystem atomicity, service/COM, SEC-18, installer, AD/DC, migration, and soak evidence remain open. Next slice: run the wired DB-only replacement through a disposable populated SQL/Data acceptance target.

Authoritative 2026-08-08 continuation: code/test commit `74ca89853` adds a transaction-scoped, set-based `DeleteAllDomainsForRestoreAsync` capability to the SQL restore transaction. It snapshots domain-owned accounts, lists, rules, messages, fetch accounts, IMAP folders, group memberships, and ACL ownership, then deletes dependents in legacy owner order while leaving commit or rollback to the existing transaction owner. Legacy anchors are `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-215`), `PersistentAccount::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:36-53`).

Focused domain-store coverage is `6 passed, 0 failed, 3 skipped`; default full Net10 is `1934 passed, 0 failed, 29 skipped`. SQL commit/rollback integration is present but skipped because the approved disposable SQL variables are unset. The capability is intentionally not wired into restore orchestration yet, so no production restore behavior changed. No COM identity, direct activation, authentication boundary, SMTP trust, production service/SQL/Data, DCOM, IIS, or registry state changed. Release remains RED. Next slice: wire domain cleanup immediately before full-restore filesystem replacement and prove populated-store ordering and rollback on an isolated disposable target.

Authoritative 2026-08-08 continuation: code/test commit `0d08e2c47` adds a final synchronous `BackupRestoreContainmentPreflight.Revalidate` after metadata parsing and the authorization lease, immediately before non-DB `BackupRestoreDataDirectoryRuntime.RestoreAsync`. Source and target replacement negative tests prove that a lease-time mutation is rejected with zero filesystem copy and zero domain mutation.

This is path-based last-mile hardening, not handle-relative containment: the remaining check-then-use race in `Directory.Move`, recursive enumeration, and `File.Copy` requires a separate Windows-native implementation. Focused `BackupRestoreExecutionTests` coverage is `13 passed, 0 failed, 0 skipped`; default full Net10 is `1933 passed, 0 failed, 27 skipped`. No COM identity, direct activation, SMTP trust, production service/SQL/Data, DCOM, IIS, or registry state changed. Release gate remains RED for native TOCTOU, process-kill/power-loss, SQL/filesystem atomicity, full reinitialization, service/COM, SEC-18, installer, AD/DC, migration, and soak evidence. Next slice: implement native handle-relative restore swap/copy containment.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `cc1d0f6a5` hardens non-DB restore recovery-journal finalization. `BackupRestoreRecoveryJournal.Persist` flushes the containing directory after atomic replacement, `Remove` flushes after deletion and restores flushed evidence if finalization fails, and the Windows path uses a private `CreateFileW(FILE_FLAG_BACKUP_SEMANTICS)`/`FlushFileBuffers` handle. `BackupRestoreDataDirectoryRuntime` does not attempt rollback after the rollback artifact has already been deleted; it leaves a pending journal for manual recovery instead of risking data loss.

Legacy anchors are `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `FileUtilities::CopyDirectory` (`hmailserver/source/Server/Common/Util/FileUtilities.cpp:369-430`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:36-53`). Focused recovery coverage is `15 passed, 0 failed, 0 skipped`; default full Net10 is `1931 passed, 0 failed, 27 skipped`. Tests cover simulated directory-flush failure and pending evidence, not actual process-kill/power-loss restart. Handle-relative mutation, cross-resource SQL/filesystem atomicity, normal-installation reinitialization, service/COM, SEC-18, installer, AD/DC, and soak gates remain open. Next slice: implement handle-relative restore swap/copy containment.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `d039b8ed8` preserves legacy archived account credentials during restore. `Account::XMLLoad` (`hmailserver/source/Server/Common/BO/Account.cpp:335-346`) reads `Password` and `PasswordEncryption` verbatim, and `PersistentAccount::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:263-280`) writes both values unchanged. `BackupRestoreMetadataWriter.RestoreAccountsAsync` now calls the restore-specific account-store operation; `SqlServerAccountAdministrationStore` writes the archived password as-is with its archived encryption type, while normal Administrator insert/update still encrypts new plaintext passwords as before.

Focused account/parser/store coverage is `26 passed, 0 failed, 0 skipped`; default full Net10 is `1928 passed, 0 failed, 27 skipped`. The SQL credential round-trip is present but skipped because approved SQL environment variables are unset. The restore-specific abstraction fails closed for stores that do not implement archived-credential insertion. No COM identity, direct activation boundary, authentication gate, SMTP trust, production service/SQL/Data, DCOM, IIS, or registry state changed. Security review found no new authorization issue; reality review remains RED for release because live SQL/service/COM and broader restore gates are incomplete. Next slice: harden recovery-journal durability and handle-relative containment.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `d1fa4a6a5` makes restore archive-binding ownership explicit for terminal dispatch outcomes. A claimed `Backup` binding remains owned by its first queued request; a distinct `AlreadyRunning`, queue-unavailable, thrown-dispatch, or pre-dispatch authorization failure releases only that rejected backup’s binding. Same-object duplicate dispatch remains safe, while an unclaimed denied object cleans its private snapshot. Focused `BackupManagerComContractTests` coverage is `26 passed, 0 failed, 0 skipped`; default full Net10 is `1926 passed, 0 failed, 26 skipped`.

Legacy references are `BackupManager::StartRestore` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:75-98`), `BackupTask::SetBackupToRestore` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:44-49`), and `BackupTask::DoWork` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:27-41`). The internal claim is a lifecycle hardening around the legacy shared-pointer ownership and does not alter installed COM identities, direct activation, authentication boundaries, SMTP trust, or live reconfiguration. Next slice: preserve archived account credential/encryption type during restore.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `4864a4dba` isolates queued-abort cleanup failures. `BackupTaskQueue.CompleteAndAbortPending` logs a failing request callback and continues draining later requests; `BackupTaskHostedService` applies the same isolation when cancellation dequeues a request. `BackupTaskRequest.AbortPending` still guarantees idempotent `ThreadStopped` notification.

Legacy `WorkQueue::Stop` stops pending work without a callback contract (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`); the .NET cleanup extension therefore continues after one cleanup failure and preserves evidence through tracing/logging. Focused queue/restore coverage is `30 passed, 0 failed, 0 skipped`; default full Net10 is `1924 passed, 0 failed, 26 skipped`. Next slice: close non-queued archive binding ownership on duplicate/denied dispatch.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `3599ce44d` adds an active-task completion barrier to `BackupTaskHostedService`. Each dequeued request owns a completion source until its execute/abort path and idempotent `ThreadStopped` notification finish; `StopAsync` closes admission, cancels the worker, and waits for active completion before returning, even if its host cancellation timeout fires. This prevents a non-cooperative backup/restore delegate from continuing after service shutdown returns.

Legacy `WorkQueue::Stop` interrupts and joins workers with a bounded wait (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`); the .NET test explicitly proves the stronger completion-fence behavior with a cancellation-ignoring delegate. Focused queue/restore coverage is `29 passed, 0 failed, 0 skipped`; default full Net10 is `1923 passed, 0 failed, 26 skipped`. COM identity, direct activation, authentication, SMTP trust, live reconfiguration, SQL schema, production service, and Data directory remain untouched.

Remaining queue residuals: per-request abort callback failures can still interrupt a drain, and restore archive binding ownership on duplicate/denied dispatch needs explicit cleanup. Next slice: isolate pending-abort callback failures while draining.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `ba8390f2c` closes the shutdown admission race left by the queued-cleanup slice. `BackupTaskQueue.StopAccepting` transitions the queue to stopping under the same lifecycle lock used by `TryEnqueue`, then completes the channel; `BackupTaskHostedService` performs this synchronously at `StopAsync` entry and drains after worker shutdown. The transition is linearized, so a dispatch either wins before shutdown or is rejected, and pending work is aborted without starting.

Legacy references are `WorkQueue::Stop` (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`), `WorkQueueManager::RemoveQueue` (`hmailserver/source/Server/Common/Threading/WorkQueueManager.cpp:68-107`), and `Application::ExitInstance` (`hmailserver/source/Server/Common/Application/Application.cpp:222-244`). Focused queue/restore coverage is `28 passed, 0 failed, 0 skipped`; default full Net10 is `1922 passed, 0 failed, 26 skipped`.

Remaining queue risks are explicitly open: a running task that ignores cancellation may outlive the host stop timeout, abort callback exceptions can stop a multi-item drain, and duplicate/denied restore dispatch still needs explicit archive-binding ownership. Push is currently `PUSH-BLOCKED` because the configured GitHub remote was unreachable; last verified upstream remains `70dcb9621`. Next slice: fence or explicitly retain non-cooperative active restore during shutdown.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `68a75427c` adds deterministic shutdown cleanup for queued backup/restore work. `BackupTaskQueue.CompleteAndAbortPending` completes the channel and aborts pending requests once; `BackupTaskHostedService.StopAsync` drains after worker shutdown, and a request dequeued after cancellation is aborted before it can start. `BackupManager.StartRestore` supplies `Backup.CleanupArchiveBinding` as the pending-abort callback. `BackupTaskRequest.NotifyThreadStopped` is idempotent, preserving coordinator state under abort/dispose races.

Legacy references are `WorkQueue::Stop` (`hmailserver/source/Server/Common/Threading/WorkQueue.cpp:128-181`), `BackupTask::DoWork` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:27-41`), and `Application::ExitInstance` maintenance queue removal (`hmailserver/source/Server/Common/Application/Application.cpp:211-234`). The .NET behavior is intentionally fail-closed at service cancellation and preserves installed COM identities, direct activation denial, authenticated boundaries, SMTP trust, and live reconfiguration. Focused queue/restore coverage is `28 passed, 0 failed, 0 skipped`; default full Net10 is `1922 passed, 0 failed, 26 skipped`.

Residual risks: a running task must honor cancellation; this slice does not make SQL/filesystem restore crash-atomic, does not prove out-of-process service/COM restore, and does not address account credential encryption semantics. Next slice: inspect legacy account password encryption during archive restore and preserve the archived credential type without double encryption.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `aae5137a9` adds `ApplicationAuthorizationAuthority`, which serializes authentication generation/state publication and DB-only restore admission. `Backup.AcquireAuthorizationLeaseAsync` holds the internal lease through `BeginAsync`, all metadata writes, commit, and disposal. `BackupRestoreExecutionTests` covers invalidation-before-lease and lease-before-invalidation interleavings; focused coverage is `9 passed, 0 failed, 0 skipped`; default full Net10 is `1917 passed, 0 failed, 26 skipped`.

Legacy references: `COMAuthentication::Authenticate` (`hmailserver/source/Server/COM/COMAuthentication.cpp:30-68`), `InterfaceBackup::StartRestore` (`InterfaceBackup.cpp:16-33`), and `BackupTask::DoWork` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:27-40`). Legacy has no equivalent lease; the new authority is deliberate internal security hardening and `[ComVisible(false)]`. Installed COM contracts, direct activation, SQL schema, SMTP behavior, non-DB path, and production state are unchanged.

Remaining RED blockers: non-DB filesystem restore has no final lease, queue cancellation does not deterministically drain pending tasks, credential preservation, crash-safe SQL/NTFS recovery, full deletion/reinitialization, isolated service/COM, SEC-18, installer, AD/DC, and lifecycle evidence remain incomplete. Next slice: apply the authority lease before non-DB filesystem staging.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `2e9728452` adds an internal non-COM authorization admission to DB-only restore after read-only preflight and immediately before `IBackupRestoreMetadataTransactionFactory.BeginAsync`. `BackupRestoreExecutionTests.ExecuteAsync_RejectsDbOnlyRestoreAfterReadOnlyPreflightWhenAuthorizationIsInvalidated` gates the domain read, invalidates the generation, and proves zero transaction begins and zero inserts. Focused coverage is `8 passed, 0 failed, 0 skipped`; default full Net10 is `1916 passed, 0 failed, 26 skipped`.

Legacy references: `BackupExecuter::StartRestore` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-335`) and `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-135,203-215`) have no worker-time COM authorization check. Current symbols: `Backup.EnsureAuthorizedForRestoreCommit`, `MetadataBackupRestoreExecutor.ExecuteDbOnlyMetadataRestoreAsync`, and `RestoreMetadataAsync`. This is deliberate security hardening; COM identity, direct activation, SQL schema, SMTP behavior, non-DB path, and production state remain unchanged.

Residual RED risk: a plain generation check can still race authentication invalidation before `BeginAsync`; non-DB restore lacks final admission; pending queue shutdown cleanup, credential preservation, crash-safe SQL/NTFS recovery, deletion/reinitialization, service/COM, SEC-18, installer, AD/DC, and lifecycle gates remain open. Next slice: replace the plain check with an atomic authorization admission lease at the SQL mutation boundary.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `1e717bb1d` adds `EnsureAuthorized()` at the queued restore worker boundary in `BackupManager.ExecuteRestoreAsync`, before the restore executor can inspect or mutate SQL/filesystem state. `BackupManagerComContractTests.QueuedRestore_DoesNotInvokeExecutorAfterAuthenticationInvalidation` proves enqueue → failed reauthentication → delayed task denial (`E_ACCESSDENIED`) with zero executor invocation. Focused coverage is `23 passed, 0 failed, 0 skipped`; default full Net10 is `1915 passed, 0 failed, 26 skipped`.

Legacy anchors: `COMAuthentication::Authenticate` (`hmailserver/source/Server/COM/COMAuthentication.cpp:30-68`), `InterfaceBackupManager::LoadBackup`/`StartBackup` (`InterfaceBackupManager.cpp:43-69`), `InterfaceBackup::StartRestore` (`InterfaceBackup.cpp:16-33`), `BackupManager::StartRestore` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:74-98`), and `BackupTask::DoWork` (`BackupTask.cpp:27-40`). Legacy has no worker-time auth recheck; this is an intentional security hardening. Current symbols: `BackupManager.ExecuteRestoreAsync`, `BackupTaskQueue`, `BackupTaskHostedService`, and `MetadataBackupRestoreExecutor.ExecuteAsync`. No COM identity, direct activation, SMTP trust, production service/SQL/Data, DCOM, IIS, or registry state changed.

Remaining RED risks: auth generation can change during async preflight before final SQL/filesystem admission; pending queue items lack deterministic shutdown cleanup; archived account credential/encryption preservation, crash-safe SQL/NTFS recovery, full deletion/reinitialization, isolated service/COM, SEC-18, installer, AD/DC, and 24-hour lifecycle gates remain incomplete. Next slice: add final authorization admission immediately before restore mutation and test in-flight invalidation.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `342f95325` makes all unsupported members on the five transaction-scoped SQL restore stores fail closed with `InvalidOperationException` instead of `NullReferenceException`. The shared transaction still exposes only the insert/read operations required by DB-only restore; no COM identity or direct activation boundary changed. Focused SQL restore coverage is `11 passed, 0 failed, 0 skipped`; default full Net10 is `1914 passed, 0 failed, 26 skipped`.

Legacy references: `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-220`), and `IInterfaceDatabase::BeginTransaction`/`ExecuteSQL`, which has no legacy transaction-scoped administration-store equivalent. Current symbols: `IBackupRestoreMetadataTransaction`, `SqlServerBackupRestoreMetadataTransaction`, and the five `SqlServer*AdministrationStore` classes. The implementation intentionally does not open an independent connection from a transaction-scoped store.

Open blockers remain: queued restore execution must revalidate authorization after queueing; restore must preserve archived account credential/encryption type instead of blindly re-encrypting; journal power-loss/ACL/handle-relative durability, full deletion/reinitialization, crash-safe SQL/NTFS outcome, isolated service/COM, SEC-18, installer, AD/DC, and 24-hour lifecycle evidence are incomplete. Security/reality gate remains RED. Next slice: execution-time authorization revalidation for queued restore with invalidation and cancellation tests.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `904000f85` adds a bounded non-DB restore recovery journal. The journal records target/rollback/archive identity and phase transitions, cleans on known success, preserves evidence on rollback failure or ambiguous metadata outcome, and `Program.cs` fails closed before service startup when a pending or malformed journal exists. Focused recovery coverage is `12 passed, 0 failed, 0 skipped`; default full Net10 is `1914 passed, 0 failed, 25 skipped`.

Legacy references: `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85-220`), and `Reinitializator::ReInitialize` (`hmailserver/source/Server/Common/Application/Reinitializator.cpp:36-53`). Legacy deletes domains/public folders before data replacement and reinitializes asynchronously. Current symbols: `BackupRestoreRecoveryJournal`, `BackupRestoreDataDirectoryRuntime.RestoreAsync`, `MetadataBackupRestoreExecutor.ExecuteNonDbDataRestoreAsync`, and service `Program.cs`.

This slice deliberately stops at durable evidence and fail-closed detection; it does not reconcile SQL and NTFS atomically or auto-rollback an uncertain commit. Journal ACL/power-loss durability, handle-relative containment, process-kill cleanup, full restore deletion/reinitialization, isolated service/COM, SEC-18, installer, AD/DC, and 24-hour lifecycle evidence remain open. Security/reality gate is RED. Next slice: implement legacy domain/public-folder deletion and asynchronous reinitialization ordering against disposable targets.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `41d81cca0` adds a production-wired SQL transaction boundary for DB-only `RestoreDomains` metadata restore. One `SqlConnection`/`SqlTransaction` is shared by domain, account, alias, distribution-list, and recipient inserts; successful restore commits once, failed/disposed restore rolls back, and the production service fails closed when the factory is missing. Focused LocalDB coverage is `10 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 25 skipped`; SQL-enabled full is `1926 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

Legacy references: `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`) and `Collection<T,P>::XMLLoad`/`DeleteAll` (`hmailserver/source/Server/Common/BO/Collection.h:85`). Current symbols: `MetadataBackupRestoreExecutor.RestoreMetadataAsync`, `IBackupRestoreMetadataTransactionFactory`, `SqlServerBackupRestoreMetadataTransactionFactory`, `BackupXmlPayloadRuntime`, `Host.cs`, and `Program.cs`. This is a bounded SQL-only hardening slice, not legacy full restore parity: non-DB SQL/filesystem recovery remains compensation-based, and commit-failure/connection-loss/crash evidence is still missing. Queued service/COM, SEC-18, installer, AD/DC, normal-installation deletion/reinitialization, and 24-hour lifecycle gates remain open; production status is RED. Next slice: durable non-DB restore journal/recovery evidence.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: test-only code/test commit `ec9b71ed0` adds disposable LocalDB acceptance for a failure after one real distribution-list recipient insert. The second recipient insert is injected to fail; the test verifies filesystem rollback and removal of the inserted recipient, list, alias, account, and domain through the real SQL stores. Focused restore coverage is `5 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 20 skipped`; SQL-enabled full Net10 is `1921 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

Legacy anchors: `DistributionList::XMLLoad`/`XMLLoadSubItems`, `DistributionListRecipients::PreSaveObject`, and persistent list/list-recipient `SaveObject`; legacy leaves partial rows after recipient failure. Current compensation is `BackupRestoreMetadataWriter.RestoreDistributionListRecipientsAsync` plus `MetadataBackupRestoreExecutor.RollbackAsync`. Next slice: shared SQL transaction or durable restore journal with crash/recovery evidence. Production release remains RED and queued service/COM acceptance remains environment-blocked.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: test-only code/test commit `387589ce1` adds disposable LocalDB acceptance for a recipient-stage restore failure. The test injects failure on the first distribution-list recipient insert and verifies filesystem rollback plus compensation of the created distribution list, alias, account, and domain through the real SQL stores. Focused restore coverage is `4 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 18 skipped`; SQL-enabled full Net10 is `1919 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

Legacy parity anchors: `BackupExecuter::StartRestore`, `Collection<T,P>::XMLLoad`/`DeleteAll`, `DistributionList::XMLLoad`/`XMLLoadSubItems`, `DistributionListRecipients::PreSaveObject`/`GetCollectionName`, and persistent list/list-recipient `SaveObject` implementations. Legacy writes the list before recipients and does not compensate a later recipient failure. Current .NET anchors are `BackupRestoreMetadataWriter.RestoreDistributionListsAsync`/`RestoreDistributionListRecipientsAsync` and `MetadataBackupRestoreExecutor.RollbackAsync`, which compensate in reverse dependency order.

Residual gap: this test fails before any recipient ID is generated, so real recipient-delete rollback remains unproven. Next slice: pass the first recipient insert through, fail the second, and assert all restored rows are removed. No production or machine state changed.


## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `23d428569` hardens `435532ad0`. Real-reader `BackupManager.LoadBackup` now fails closed when no private snapshot can be created; `BackupArchiveBinding` hashes while copying; and duplicate restore dispatch retains the snapshot for the first queued task. Focused archive/restore/COM coverage is `30 passed, 0 failed, 0 skipped`; full Net10 is `1902 passed, 0 failed, 16 skipped`. The new regression is `BackupManagerComContractTests.DuplicateRestoreDispatch_DoesNotReleaseTheFirstTaskArchiveBinding`.

No COM identity, direct activation/authentication boundary, production service/SQL/Data directory, registration, DCOM, IIS, SMTP trust, or live reconfiguration changed. Residual risk: raw sibling `DataBackup` identity and full non-DB restore remain open, and a snapshot retained by an unused COM object relies on finalization for cleanup. Next slice: wire raw/7z `DataBackup` staging into bound non-DB-only restore on disposable targets with rollback/cancellation evidence.

Authoritative 2026-08-08 continuation: code/test commit `435532ad0` adds private archive snapshot binding before `BackupManager.LoadBackup` metadata parsing. Legacy `BackupManager::LoadBackup` stores only the caller path and `BackupExecuter::StartRestore` reopens it; .NET now copies an existing source while holding a read lock, parses metadata from the snapshot, carries SHA-256 identity, holds the snapshot read-locked through queued DB-only restore execution, and cleans it after dispatch/worker completion. Focused archive/restore/COM coverage is `38 passed, 0 failed, 0 skipped`; full Net10 is `1901 passed, 0 failed, 16 skipped`.

Exact current symbols: `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/BackupArchiveBinding.cs`, `BackupArchiveIdentity.cs`, `BackupManager.cs`, `Backup.cs`, and `BackupRestoreExecution.cs`; tests are `BackupArchiveIdentityTests.cs`, `BackupRestoreExecutionTests.cs`, `BackupComContractTests.cs`, and `BackupManagerComContractTests.cs`. This is deliberate security hardening over legacy valid-replacement behavior; COM identity/direct activation/authentication boundaries remain unchanged. Residual risk is raw sibling `DataBackup` identity and full non-DB restore. Next slice: connect the existing raw/7z staging runtime to bound non-DB-only restore on disposable targets and prove rollback/cancellation/containment. Release remains RED pending full restore, isolated service/COM/DCOM, SEC-18, installer, AD/DC, and 24-hour lifecycle evidence.

Authoritative 2026-08-08 continuation: code/test commit `edd01f557` adds live authentication-generation revalidation for Application-created BackupManager/Backup children. Legacy `COMAuthentication::Authenticate` clears the root account, but `InterfaceBackupManager` and `InterfaceBackup` only check child state after acquisition; retained legacy children therefore remain usable. .NET now captures the Application generation in `BackupManager`, propagates it to loaded `Backup`, invalidates it on every authentication attempt, and rejects stale concurrent authentication completions. Focused Backup/Application contract coverage is `30 passed, 0 failed, 0 skipped`; full Net10 is `1897 passed, 0 failed, 16 skipped`.

Exact current symbols: `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/ApplicationComClass.cs` (`Authenticate`, `BackupManager`, `IsCurrentAdministrator`), `BackupManager.cs` (`LoadBackup`, `EnsureAuthorized`), and `Backup.cs` (`EnsureAuthorized`). Tests are `BackupComContractTests.cs` and `BackupManagerComContractTests.cs`. No COM interface identity, direct activation boundary, SMTP trust, live reconfiguration, production service/SQL/Data directory, registration, DCOM ACL, or IIS state changed. Residual risk: method-entry authorization does not cancel already queued work, and archive content/path replacement remains a TOCTOU risk. Next slice: bind loaded archive content to service-owned identity or hash with focused replacement/deletion tests; then connect raw/7z DataBackup staging to non-DB-only restore on disposable targets. Release remains RED pending full restore, isolated service/COM/DCOM, SEC-18, installer, AD/DC, and 24-hour lifecycle evidence.

Authoritative 2026-08-08 continuation: code/test commit `a4b9dfe9e` adds the isolated `BackupRestoreDataDirectoryRuntime` for the next restore gate. Legacy `BackupExecuter::RestoreDataDirectory_` resolves raw `DataBackup` beside the archive, extracts compressed `DataBackup` to a temporary directory, replaces the configured Data directory, and cleans temporary state. The new runtime keeps that behavior bounded to an explicit containment plan and integrity evidence, rejects DB-only/unsupported/reparse inputs, uses a rollback directory for disposable-target replacement, restores the original target on staging failure, and terminates canceled 7z extraction. It is not wired into production restore execution yet.

Focused filesystem coverage is `4 passed, 0 failed, 0 skipped`; full Net10 is `1893 passed, 0 failed, 16 skipped`. Current symbols: `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/BackupRestoreDataDirectoryRuntime.cs`, `BackupRestoreContainmentPreflight.cs`, `BackupRestoreIntegrityRuntime.cs`, and `BackupRestoreDataDirectoryRuntimeTests.cs`. Residual risk: no service/COM end-to-end restore, disposable live Data-directory acceptance, reparse race evidence, or large-tree performance baseline. Next slice: connect raw/7z staging to the authenticated restore executor for non-DB-only `RestoreDomains|RestoreMessages` and test it only against disposable targets. No production service, SQL/Data directory, COM identity/registration, DCOM ACL, or IIS state changed.

Authoritative 2026-08-08 continuation: code/test commit `26b660ff8` opens one authenticated, queued DB-only metadata restore slice. Legacy `BackupManager::StartRestore`/`LoadBackup`, `BackupExecuter::StartRestore`, and `InterfaceBackup::StartRestore` preserve the asynchronous serialized operation boundary and the installed `IInterfaceBackup` restore DISPIDs; `BackupExecuter::RestoreDataDirectory_` remains intentionally unopened. `.NET 10` retains the loaded archive path, dispatches `Backup.StartRestore` through `TryStartRestore`, validates the archive with integrity/DTD/path-containment preflight and revalidation, and restores only `BODomains` metadata (domains, accounts, aliases, distribution lists, recipients) with generated-ID compensating rollback. `RestoreSettings`, `RestoreMessages`, public folders, IMAP folders/messages, message files, raw/7z `DataBackup`, application reinitialization, live SMTP behavior, COM registration, and DCOM permissions remain fenced and fail closed.

Focused restore/COM coverage is `25 passed, 0 failed, 0 skipped`; disposable Local SQL round-trip plus restore/COM coverage is `24 passed, 0 failed, 0 skipped`; full Net10 is `1889 passed, 0 failed, 16 skipped`. Exact current symbols: `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/BackupManager.cs`, `Backup.cs`, `BackupOperationRuntime.cs`, `BackupRestoreExecution.cs`, `BackupRestoreMetadataWriter.cs`, `BackupArchiveXmlSnapshotParser.cs`, and tests `BackupManagerComContractTests.cs`/`BackupRestoreExecutionTests.cs`. Residual risk is significant: the real service/COM composition has not yet executed this path against a disposable SQL target, the SQL stores use compensating deletes rather than one shared transaction, and full settings/filesystem restore is still incomplete. Next slice: isolated service/COM queued DB-only restore acceptance; then raw/compressed DataBackup restore staging and rollback. Real native AD/DC, SEC-18, out-of-proc COM/DCOM, installer, migration, and 24-hour lifecycle gates remain open.

Authoritative 2026-08-08 continuation: corrective code/test commit `ea1299638` closes the quoted-local-part gap in the domain-alias authentication slice `a5e250557`. `SqlServerImapAccountAuthenticator.AccountLookupSql` now splits usernames at the last `@` using the SQL equivalent of legacy `StringParser::ReverseFind`, preserving alias order by `daid` and the explicit case-insensitive comparison. The disposable local SQL fixture proves case-insensitive alias input, a quoted local-part containing `@`, and ordinary non-AD password authentication through an alias; focused coverage is `4 passed, 0 skipped`, and full Net10 is `1884 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods. Legacy references are `hmailserver/source/Server/Common/Util/StringParser.cpp` (`ExtractDomain`/`ExtractAddress`), `hmailserver/source/Server/Common/BO/DomainAliases.cpp:43-64`, and `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:44-51`. Next action: isolated SQL/Data-directory restore execution and round-trip evidence. Real native AD/DC, 24-hour service/COM lifecycle, SEC-18, real COM/DCOM, installer build, migration, and final release gates remain open; no production state or installed COM identity changed.

Authoritative 2026-08-08 continuation: code/test commit `a5e250557` implements legacy normal IMAP domain-alias lookup parity. `SqlServerImapAccountAuthenticator.AccountLookupSql` joins `hm_domain_aliases`, maps alias mailbox local parts to the owning domain, retains direct-address lookup, and orders alias candidates by `daid`. Explicit `Latin1_General_100_CI_AS` comparisons preserve case-insensitive legacy behavior under a Turkish database collation. The disposable local SQL fixture proves `ALIASUSER@ALIAS.TEST` authenticates as `aliasuser@example.test` through the owning AD validator; focused coverage is `4 passed, 0 skipped`, and full Net10 is `1884 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods. Legacy references are `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:44-51`), `DomainAliases::ApplyAliasesOnAddress` (`hmailserver/source/Server/Common/BO/DomainAliases.cpp:43-64`), and `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:250-256`. Next action: isolated SQL/Data-directory restore execution and round-trip evidence. Real native AD/DC, 24-hour service/COM lifecycle, SEC-18, real COM/DCOM, installer build, migration, and final release gates remain open; no production state or installed COM identity changed.

Authoritative 2026-08-08 continuation: code/test commit `c0d9294b6` applies the configured `Settings.DefaultDomain` to normal IMAP usernames without `@` through the existing `ISettingsAdministrationStore` boundary. The disposable local SQL fixture proves `default` authenticates as `default@example.test`; focused coverage is `1 passed, 0 skipped`, and full Net10 is `1882 passed, 0 failed, 16 skipped` excluding the two AV-locked EICAR cleanup methods. Legacy `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:44-51`) applies aliases and then default domain. Domain-alias translation through `hm_domain_aliases` remains open. Next action: normal IMAP domain-alias lookup parity. No production state, COM identity, SMTP behavior, or SQL schema changed.

Authoritative 2026-08-08 continuation: code/test commit `d2c24d2c8` restores legacy normal `LOGIN` script ordering in `SqlServerImapAccountAuthenticator`. The SQL account is materialized before `IClientPasswordValidationScriptExecutor` runs; script `Accept` may authorize an empty password, `Reject` fails, and only `Continue` reaches empty-password rejection. `AUTHENTICATE PLAIN` remains parser-rejected for an empty password before the authenticator. Focused SQL/IMAP coverage is `40 passed, 0 skipped`; full Net10 excluding the two AV-locked EICAR cleanup methods is `1882 passed, 0 failed, 16 skipped`. Legacy references are `hmailserver/source/Server/IMAP/IMAPCommandLogin.cpp:52-57`, `hmailserver/source/Server/IMAP/IMAPCommandAuthenticate.cpp:77-79`, and `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:109-133`. Next action: domain-alias/default-domain lookup. Real AD/DC, SEC-18/COM/DCOM, installer, migration/restore, and 24-hour lifecycle gates remain open; no production state or COM identity changed.

Authoritative 2026-08-08 continuation: code/test commit `eec9752e8` closes the SQL reader and connection before the synchronous AD validator and performs successful last-logon updates through a separate short-lived connection. The opt-in local SQL fixture sets `Max Pool Size=1` and opens a second probe connection inside the validator; focused local SQL coverage is `7 passed, 0 skipped`, proving the old connection-pool retention path is gone. Master-user target lookup also disposes before its update. Legacy anchors are `hmailserver/source/Server/Common/Util/AccountLogon.cpp:37-75` and `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:109-147`. Real domain-controller/native `LogonUser` evidence, aliases/default-domain lookup, and LOGIN script-before-empty-password ordering remain open; AUTHENTICATE PLAIN parser rejection remains preserved. Next action: legacy LOGIN script-before-empty-password ordering. No production service, database, Data directory, COM registration, DCOM ACL, or SMTP state was touched.

Authoritative 2026-08-08 continuation: code/test commit `4072dbf50` completes the isolated SQL-backed AD authentication evidence. `SqlServerImapActiveDirectoryIntegrationTests` creates and drops a unique local SQL Server database, uses production-compatible MSSQL `hm_accounts` types, and proves active account/domain filtering, exact AD validator arguments, success/rejection last-logon behavior, and inactive-domain non-invocation. Local SQL focused coverage is `7 passed, 0 skipped`; the normal full Net10 run excluding the two AV-locked EICAR cleanup methods is `1880 passed, 0 failed, 16 skipped`. The authenticator SQL projection now converts MSSQL `tinyint` flags and `datetime` values before `ScriptAccount` materialization. Legacy references are `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:34-147` and `hmailserver/source/Server/Common/Util/SSPIValidation.cpp:13-22`. Real domain-controller/native `LogonUser` acceptance remains environment-gated. Legacy domain alias/default-domain lookup and script-before-empty-password ordering remain open behavioral gaps. Next action: isolated 24-hour service restart/COM lifecycle soak if a disposable host is available; otherwise take the next unblocked parity slice. No production service, database, Data directory, COM registration, DCOM ACL, or SMTP state was touched.

Authoritative 2026-08-08 continuation: code/test commit `69f52b5d6` adds the bounded Active Directory password-validation boundary. Legacy anchors are `SSPIValidation::ValidateUser` (`hmailserver/source/Server/Common/Util/SSPIValidation.cpp:13-22`) and the AD branch of `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/PasswordValidator.cpp:78-164`), where script override and empty-password rejection precede `LogonUser` network validation. .NET uses injectable `IActiveDirectoryPasswordValidator`; the Windows implementation calls the native contract, closes any token, rejects empty inputs, and fails closed on exceptions. `SqlServerImapAccountAuthenticator` now uses existing AD account fields, while no password is retained. Focused coverage is `46 passed, 0 skipped`; full Net10 excluding the two AV-locked EICAR cleanup methods is `1880 passed, 0 failed, 15 skipped` (1895 total). Live disposable SQL/AD evidence is not claimed because the approved domain-controller/database prerequisite is absent. Next action: isolated SQL/AD evidence, then 24-hour service/COM lifecycle soak; SEC-18, real COM/DCOM, InnoSetup, and final release gates remain open.

Authoritative 2026-08-08 continuation: code/test commit `ef7e5ec65` implements the bounded legacy IMAP AUTHENTICATE PLAIN master-user path. Legacy anchors are `IMAPCommandAUTHENTICATE::ExecuteCommand` (`hmailserver/source/Server/IMAP/IMAPCommandAuthenticate.cpp:27-122`) plus `AccountLogon::Logon` and `PasswordValidator::ValidatePassword` (`hmailserver/source/Server/Common/Util/AccountLogon.cpp:37-75`, `hmailserver/source/Server/Common/Util/PasswordValidator.cpp:78-164`). .NET carries authzid through the existing authentication service, checks the configured master authcid, returns the active target mailbox, preserves ordinary auth, emits protocol `BAD` for master-policy errors, and excludes those errors from auto-ban accounting. Focused coverage is `43 passed, 0 skipped`; full Net10 excluding the two AV-locked EICAR cleanup methods is `1877 passed, 0 failed, 15 skipped` (1892 total). No COM identity, SMTP trust, settings setter, live reconfiguration, database schema, or production state changed. Residual risk: AD/SSPI is still fail-closed/unimplemented in the .NET authenticator, and live SQL master-user evidence is not run. Next action: isolated SQL/AD authentication evidence and boundary review; live 24-hour service/COM lifecycle, SEC-18, real COM/DCOM, InnoSetup, and final release gates remain open.

Authoritative 2026-08-08 continuation: code/test commit `53680b0d2` adds the commandable short offline synthetic IMAP SEARCH/SORT soak. `ShortSoakBenchmark` repeats the deterministic workload for bounded cycles and writes JSON/CSV/Markdown containing p50/p95/p99, errors, memory, handles, threads, TCP connections, GC deltas, timestamps, runtime, and git commit. Focused coverage is `4 passed, 0 skipped`; the full Net10 suite excluding the two AV-locked EICAR cleanup methods is `1875 passed, 0 failed, 15 skipped` (1890 total). A smoke run completed 3/3 cycles with 0 errors and threshold pass. Run with `dotnet run --project hmailserver/source/Server.Net10/benchmarks/HMailServer.Net10.Benchmarks/HMailServer.Net10.Benchmarks.csproj --configuration Debug -- --mode short-soak --count 100000 --cycles 20 --max-seconds 30 --output artifacts/benchmarks/short-soak`. This is not evidence of a 24-hour live service leak-free run, SQL/protocol equivalence, COM lifecycle, or release readiness. Next action: isolated live 24-hour service restart/COM lifecycle soak when the required host is available; real out-of-proc COM/DCOM activation, SEC-18, and InnoSetup build remain environment-gated.

Authoritative 2026-08-08 continuation: code/test commit `8cc67112b` adds an honest InnoSetup installer source gate. `InstallerSourceGateTests` validates the legacy `hMailServer64.iss` include graph and C++ x64 payload references; the actual `ISCC.exe` build is opt-in and returns `Inconclusive` when its toolchain or legacy release binary is absent. Focused coverage is `1 passed, 1 skipped`; the full suite excluding the two AV-locked EICAR cleanup methods is `1872 passed, 0 failed, 15 skipped` (1887 total). The actual installer build is environment-blocked on this host because `ISCC.exe` and `hmailserver/source/server/hMailServer/x64/Release/hMailServer.exe` are absent. No installer, service, registry, DCOM, database, or production state changed. Next action: commandable short soak/leak acceptance with explicit thresholds and JSON/CSV/Markdown artifacts; 24-hour soak, COM/DCOM activation, SEC-18, and installer build remain open.

## Current Authoritative Continuation

Authoritative 2026-08-05 continuation: code/test commit ``4191ac3d1`` adds the release artifact gate. ``ReleaseArtifactGateTests`` asserts the 13 required Net10 Service artifacts and the runtimeconfig framework. Focused ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1871 passed, 0 failed, 14 opt-in skips`` (1885 total). Next action: InnoSetup installer build gate.

Authoritative 2026-08-05 continuation: code/test commit ``c09fcf435`` adds COM host activation feasibility evidence. ``ComHostActivationIntegrationTests`` loads the comhost DLL, verifies the ``DllGetClassObject`` export, and records HRESULT ``0x80008093`` (host-runtime dependency) for in-process invocation; genuine out-of-proc activation requires registration/DCOM (fenced). Focused ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1870 passed, 0 failed, 14 opt-in skips`` (1884 total). Next action: installer/release artifact gate.

Authoritative 2026-08-05 continuation: code/test commit ``59623bb20`` adds live 1k-concurrent SMTP connection acceptance. ``SmtpTcpListenerTests.LoopbackConcurrency_AcceptsOneThousandClients`` opens 1000 concurrent loopback clients (backlog 1024) and asserts every one receives the 220 banner. Focused ``1/1`` (stable across repeated runs); full suite excluding the two AV-locked EICAR cleanup methods ``1869 passed, 0 failed, 14 opt-in skips`` (1883 total). Next action: real COM activation evidence.

Authoritative 2026-08-05 continuation: code/test commit ``c965cf2b0`` adds live IMAP and POP3 accept-latency acceptance harnesses mirroring the SMTP harness (200 loopback clients, banner assert, p95 budget). Focused listener coverage ``15/15``; full suite excluding the two AV-locked EICAR cleanup methods ``1868 passed, 0 failed, 14 opt-in skips`` (1882 total). Next action: 1k-concurrent loopback connection acceptance.

Authoritative 2026-08-05 continuation: code/test commit ``21b63cd13`` adds the live SMTP accept-latency acceptance harness. ``SmtpAcceptLatencyIntegrationTests`` binds ``SmtpTcpListener`` on loopback, connects 200 clients, asserts the 220 banner, and measures p50/p95/p99 connect-to-banner latency against a 5s p95 budget. Focused ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1866 passed, 0 failed, 14 opt-in skips`` (1880 total). Next action: IMAP/POP3 loopback accept-latency harnesses, then 1k-concurrent connection acceptance.

Authoritative 2026-08-05 continuation: code/test commit ``98433db25`` adds isolated database version gate and upgrade rollback evidence. ``SqlServerDatabaseAdministrationStoreIntegrationTests`` seeds ``hm_dbversion`` at 5000, simulates the upgrade write to 5708 (gate clears), then rolls back to 5000 (gate returns, one version row). Live LocalDB ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1865 passed, 0 failed, 14 opt-in skips`` (1879 total). Data-directory message-file restore, protocol acceptance, real COM activation, SEC-18, and release gates remain open. Next action: live protocol acceptance harness.

Authoritative 2026-08-05 continuation: code/test commit ``495ddb974`` adds isolated backup metadata restore round-trip evidence. ``BackupRestoreRoundTripIntegrationTests`` restores a crafted legacy archive (domain/account/alias/distribution-list/recipient) through the parser + transactional restore writer into a disposable LocalDB target and verifies every restored row. Live LocalDB ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1865 passed, 0 failed, 13 opt-in skips`` (1878 total). Data-directory message-file restore, upgrade rollback, protocol acceptance, and release gates remain open. Next action: upgrade rollback evidence.

Authoritative 2026-08-05 continuation: code/test commit ``19456d549`` adds backup archive distribution-list recipient XML parsing with transactional restore. ``ParseDistributionListRecipients`` reads ``<Recipient Name=...>``, and ``RestoreDistributionListRecipientsAsync`` replays them through ``InsertDistributionListRecipientAsync`` inside the transaction boundary with caller rollback. Focused ``9/9`` (full restore payload XML surface parsed); full suite excluding the two AV-locked EICAR cleanup methods ``1865 passed, 0 failed, 12 opt-in skips`` (1877 total). The full restore round-trip into temp DB/Data and upgrade rollback remain open. Next action: full restore round-trip into temp DB/Data.

Authoritative 2026-08-05 continuation: code/test commit ``8e5bfa01f`` adds backup archive alias and distribution-list XML parsing with transactional restore. ``ParseAliases``/``ParseDistributionLists`` reconstruct the legacy snapshots, and ``RestoreAliasesAsync``/``RestoreDistributionListsAsync`` replay them through the stores inside the transaction boundary with caller rollback. Focused ``2/2``; full suite excluding the two AV-locked EICAR cleanup methods ``1864 passed, 0 failed, 12 opt-in skips`` (1876 total). Distribution-list recipient XML parsing and the full restore round-trip into temp DB/Data remain open. Next action: distribution-list recipient XML parsing to complete the restore payload.

Authoritative 2026-08-05 continuation: code/test commit ``fc8efb819`` adds backup archive account XML parsing with transactional restore. ``BackupArchiveXmlSnapshotParser.ParseAccounts`` reads the legacy ``<Account>`` attribute set into ``RestoreAccountEntry``, and ``RestoreAccountsAsync`` replays entries through ``IAccountAdministrationStore.InsertAccountAsync`` inside the transaction boundary with caller rollback. ``BackupArchiveXmlSnapshotParserTests`` asserts field reconstruction and the XML→writer→store round trip. Focused ``2/2``; full suite excluding the two AV-locked EICAR cleanup methods ``1862 passed, 0 failed, 12 opt-in skips`` (1874 total). Alias/distribution-list XML parsing and the full restore round-trip into a temp DB/Data remain open. Next action: alias/distribution-list XML parsing to complete the restore payload.

Authoritative 2026-08-05 continuation: code/test commit ``9e2d44daf`` adds the backup archive XML→snapshot parser with restore wiring. ``BackupArchiveXmlSnapshotParser.ParseDomains`` reads the legacy ``<Domain>`` attribute set into ``DomainAdministrationSnapshot`` (including anti-spam/DKIM and limitations bit packing), and the parsed snapshots feed ``BackupRestoreMetadataWriter.RestoreDomainsAsync`` for transactional restore. ``BackupArchiveXmlSnapshotParserTests`` asserts field reconstruction and the XML→writer→store round trip. Focused ``2/2``; full suite excluding the two AV-locked EICAR cleanup methods ``1860 passed, 0 failed, 12 opt-in skips`` (1872 total). Account/alias/distribution-list XML parsing and the full restore round-trip into temp DB/Data remain open. Next action: account/alias/distribution-list XML parsing to complete the restore payload.

Authoritative 2026-08-05 continuation: code/test commit ``887521659`` adds the first restore-execution seam, ``BackupRestoreMetadataWriter.RestoreDomainsAsync``, which replays a domain snapshot batch through ``IDomainAdministrationStore.InsertDomainAsync`` inside ``BackupRestoreTransactionBoundary`` so a mid-batch failure invokes the caller rollback and rethrows. ``BackupRestoreMetadataWriterTests`` covers full replay and partial-failure rollback. Focused ``2/2``; full suite excluding the two AV-locked EICAR cleanup methods ``1858 passed, 0 failed, 12 opt-in skips`` (1870 total). Archive XML→snapshot parsing and the full restore round-trip remain open. Next action: wire archive XML→snapshot restore for domains/accounts.

Authoritative 2026-08-05 continuation: code/test commit ``f8fa925f0`` adds isolated SQL backup-projection evidence. ``SqlServerBackupProjectionIntegrationTests`` seeds a disposable LocalDB database and proves ``GetBackupAccountsAsync`` (identity + BlowFish password round-trip + ``PasswordEncryption=1``), ``GetBackupRulesAsync``, and ``GetDomainsAsync``. Live LocalDB ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1856 passed, 0 failed, 12 opt-in skips`` (1868 total). Restore execution, upgrade rollback, and the full backup/restore round-trip queue remain open and environment-gated (writes into a temp DB cannot run against a production Data directory). Real COM activation, SEC-18, protocol acceptance, and release gates remain open. Next action: full isolated backup/restore round-trip (restore execution into temp DB/Data).

Authoritative 2026-08-05 continuation: test-only commit ``bd168169d`` adds an offline acceptance seam for the performance benchmark pack via ``SyntheticBenchmarkArtifactWriterTests``, asserting the deterministic JSON/CSV/Markdown artifact serialization without running the heavy benchmark or a live server. Focused ``1/1``; full suite excluding the two AV-locked EICAR cleanup methods ``1856 passed, 0 failed, 11 opt-in skips`` (1867 total). Offline-verifiable C++-to-.NET COM parity, SQL evidence, WebAdmin/SSRF hardening are complete; live COM activation (registry), SEC-18, protocol acceptance, live benchmark evals, and backup/restore round trip remain environment-gated. Next action: isolated backup/restore round-trip + upgrade rollback evidence (temp DB/Data, disposable).

Authoritative 2026-08-05 continuation: code/test commit ``0429fa1f1`` adds the COM-path SSRF guard for scanner-test methods. ``LegacyLocalScannerTargetGuard`` requires every resolved address to be loopback or a local interface address (mirroring legacy ``IsLocalHost``), applied on ``AntiVirus.TestClamAVScanner`` and ``AntiSpam.TestSpamAssassinConnection`` before the runtime connects; non-local or unresolvable targets fail closed with ``E_FAIL``. Unit and COM contract tests cover local accept, public/remote denial-before-runtime, and local delegation. Focused coverage ``26/26``; full suite excluding the two AV-locked EICAR cleanup methods ``1855 passed, 0 failed, 11 opt-in skips`` (1866 total). Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. Next action: real COM activation evidence for completed COM identities.

Authoritative 2026-08-05 continuation: read-only egress/SSRF posture audit, no production code changed. External fetch enforces ``ExternalFetchEndpointPolicy``; WebAdmin scanner AJAX handlers enforce ``IsLocalHost``-only targets plus POST-CSRF (``437612a13``); the COM ``TestClamAVScanner``/``TestSpamAssassinConnection`` methods still accept arbitrary host+port (residual gap). A COM-level SSRF guard would change behavior exercised by existing committed tests that drive non-local hostnames through an online runtime and require DNS, so it is deferred as a security-policy slice needing an explicit allow-list design. Full baseline ``1851 passed, 0 failed, 11 opt-in skips`` (1862 total). Next action: COM-path SSRF guard design decision for scanner-test methods, then real COM activation evidence.

Authoritative 2026-08-05 continuation: code/test commit ``437612a13`` completes SEC-14 WebAdmin AJAX scanner-test POST/CSRF hardening. ``hmailserver/source/WebAdmin/background_ajax_virustest.php`` and ``background_ajax_spamassassintest.php`` now require ``hmailRequirePostCsrfToken()`` after the server-admin guard, keep local-scanner-target restrictions and POST-only reads; the calling forms already submit ``csrftoken``. New source tests assert guard-before-CSRF-before-reads. Focused coverage ``2/2``; full suite excluding the two AV-locked EICAR cleanup methods ``1851 passed, 0 failed, 11 opt-in skips`` (1862 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. Next action: shared egress/SSRF policy review for external fetch and diagnostics/network tests.

Authoritative 2026-08-05 continuation: code/test commit ``795bd3b93`` completes authenticated ``Messages.DeleteByDBID`` plus ``Clear`` parity (DB-only), completing the message mutation trio (insert ``85e5a143a``, update ``f06a199b4``, delete). Legacy ``InterfaceMessages::DeleteByDBID``/``Clear`` delegate to ``PersistentMessage`` deletion. .NET path preserves installed Messages/Message COM identity/direct activation denial, treats unknown IDs as no-ops, maps store failure to ``E_FAIL``, clears the folder snapshot only after successful whole-collection deletion, and removes only the selected snapshot after an owner+folder-scoped delete succeeds. Data-directory message-file deletion remains fenced. Focused coverage ``31/31``; full suite excluding the two AV-locked EICAR cleanup methods ``1849 passed, 0 failed, 11 opt-in skips`` (1860 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. Next action: SEC-14 WebAdmin remaining POST-only handlers.

Authoritative 2026-08-05 continuation: code/test commit ``f06a199b4`` completes authenticated existing-row ``Message.Save()`` UPDATE parity (DB-only) after message insert ``85e5a143a``. Legacy anchors are ``InterfaceMessage::Save`` and ``PersistentMessage::SaveObject``. .NET path preserves installed Messages/Message IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live authentication, stages retained-message From/header setters, persists via a parameterized owner-scoped UPDATE, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Data-directory message-file creation and content rewrites remain fenced. Focused coverage ``26/26``; full suite excluding the two AV-locked EICAR cleanup methods ``1844 passed, 0 failed, 11 opt-in skips`` (1855 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: message delete/Clear parity (DB-only).

Authoritative 2026-08-05 continuation: code/test commit ``22c206330`` adds the opt-in isolated SQL identity/readback and rollback evidence for the message insert. Legacy schema anchor is ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql`` (``hm_messages``). The fixture proves ``OUTPUT INSERTED.messageid`` identity readback with per-insert increments through ``GetFolderMessagesAsync``, and a UNIQUE ``messagefilename`` violation on INSERT leaving no partial row. Live LocalDB evidence is ``1/1``; the focused Messages suite is ``24/24``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1841 passed, 0 failed, 11 opt-in skips`` (1852 total). Data-directory message-file creation remains fenced. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: existing-row ``Message.Save()`` UPDATE parity (DB-only).

Authoritative 2026-08-05 continuation: code/test commit ``85e5a143a`` completes folder-scoped ``Messages.Add()`` plus new-item ``Message.Save()`` INSERT parity (DB row only). Legacy anchors are ``InterfaceMessages::Add`` (``hmailserver/source/Server/COM/InterfaceMessages.cpp:102``) and ``PersistentMessage::SaveObject``; data-directory message-file creation remains fenced. .NET path preserves installed Messages/Message IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rejects the account message-cache Add with ``DISP_E_BADINDEX``, stages a folder-scoped draft with Subject/From/Date/header setters, persists via a parameterized insert with ``OUTPUT INSERTED.messageid`` and a generated partial ``.eml`` filename, maps store failure to ``E_FAIL`` retaining the draft, and publishes only the saved snapshot after success. Focused coverage ``23/23``; full suite excluding the two AV-locked EICAR cleanup methods ``1841 passed, 0 failed, 10 opt-in skips`` (1851 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: message SQL identity/readback evidence for the insert.

Authoritative 2026-08-05 continuation: code/test commit ``29d90ca9d`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed TCP/IP port mutations. Legacy schema anchor is ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql`` (``hm_tcpipports``). The fixture proves ``OUTPUT INSERTED.portid`` identity readback with per-insert increments, identity-preserving UPDATE, delete-by-id, ``DeleteAllTcpIpPorts``, and a UNIQUE ``portnumber`` violation on INSERT leaving no partial row. Live LocalDB evidence is ``1/1``; the focused TCP/IP-port suite is ``25/25``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1838 passed, 0 failed, 10 opt-in skips`` (1848 total). Running the full suite with the SQL connection set still surfaces the ``5`` pre-existing message-indexing opt-in failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open; ``Messages`` COM mutation remains the only large Admin collection not yet opened (data-directory/file coupled, fenced). No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: bounded ``Messages.Add()``/``Message.Save()`` INSERT parity with data-directory file creation fenced.

Authoritative 2026-08-05 continuation: code/test commit ``e12dbe24a`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed account mutations. Legacy schema anchors are ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql`` (``hm_accounts`` and dependents). The fixture proves ``OUTPUT INSERTED.accountid`` identity readback with per-insert increments, BlowFish password round-trip, owner-scoped UPDATE/DELETE no-op against foreign domain IDs, conditional password update, transactional cascade DELETE removing account dependents, and NOT NULL address violations leaving no partial row and the original row intact. It also surfaced and fixed a latent defect where the account admin read was invalid under ``CommandBehavior.SequentialAccess`` (out-of-order ordinal reads); the store read now uses ``CommandBehavior.Default``. Live LocalDB evidence is ``1/1``; the focused Accounts suite is ``57/57``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1838 passed, 0 failed, 9 opt-in skips`` (1847 total). Running the full suite with the SQL connection set still surfaces the ``5`` pre-existing message-indexing opt-in failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: next authenticated Admin collection mutation after accounts.

Authoritative 2026-08-05 continuation: code/test commit ``2fbc3a085`` completes authenticated existing-row ``Account.Save()`` UPDATE parity, completing the account mutation trio (insert ``43ab59b74``, delete ``84fa764e3``, update). Legacy anchors are ``InterfaceAccount::Save`` and ``PersistentAccount::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:220-300``). .NET path preserves installed Accounts/Account IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live domain/server authentication, stages retained-account setters through a mutable ``_currentSaveSnapshot`` overlay, persists via a parameterized owner-scoped UPDATE with conditional password columns, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Retained account setters are now staged instead of ``E_NOTIMPL``. Focused coverage ``56/56``; full suite excluding the two AV-locked EICAR cleanup methods ``1838 passed, 0 failed, 8 opt-in skips`` (1846 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: isolated SQL identity/readback rollback evidence for account mutations.

Authoritative 2026-08-05 continuation: code/test commit ``84fa764e3`` completes authenticated domain-owned ``Accounts.DeleteByDBID``/``Delete`` plus attached ``Account.Delete()`` parity, following account insert ``43ab59b74``. Legacy anchors are ``InterfaceAccounts::Delete``/``DeleteByDBID`` and ``PersistentAccount::DeleteObject`` (``hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:30-70``). .NET path preserves installed Accounts/Account IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live domain/server authentication, treats unknown/stale IDs as successful no-ops, maps store failure to ``E_FAIL`` retaining the owner snapshot, and removes only the selected snapshot after a transactional cascade delete succeeds. Data-directory folder deletion remains fenced. Focused coverage ``51/51``; full suite excluding the two AV-locked EICAR cleanup methods ``1833 passed, 0 failed, 8 opt-in skips`` (1841 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: authenticated existing-row ``Account.Save()`` UPDATE parity.

Authoritative 2026-08-05 continuation: code/test commit ``43ab59b74`` completes authenticated domain-owned ``Accounts.Add()`` plus new-item ``Account.Save()`` INSERT parity. Legacy anchors are ``InterfaceAccounts::Add`` (``hmailserver/source/Server/COM/InterfaceAccounts.cpp:42``) and ``PersistentAccount::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:220-300``). .NET path preserves installed Accounts/Account IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live domain/server authentication on Add/Save, stages the draft against the owning domain, stages all account setters, persists via a parameterized insert with ``OUTPUT INSERTED.accountid`` and BlowFish password encryption (``accountpwencryption=1``), retains the failed draft on store failure mapping to ``E_FAIL``, and publishes only the saved snapshot with child-state adapters after success. Existing-row Save/Delete remain ``E_NOTIMPL``. Focused coverage ``46/46``; full suite excluding the two AV-locked EICAR cleanup methods ``1828 passed, 0 failed, 8 opt-in skips`` (1836 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: authenticated existing-row ``Account.Save()`` UPDATE parity.

Authoritative 2026-08-05 continuation: code/test commit ``794d93a3c`` completes authenticated ``TCPIPPorts.SetDefault()`` parity. Legacy anchors are ``InterfaceTCPIPPorts::SetDefault`` (``hmailserver/source/Server/COM/InterfaceTCPIPPorts.cpp:37``) and ``TCPIPPorts::SetDefault`` (``hmailserver/source/Server/Common/BO/TCPIPPorts.cpp:37-80``). .NET path preserves installed TCPIPPorts COM identity/direct activation denial, rechecks live server-administrator authentication, performs the legacy no-op detection against the four default ports (25/SMTP, 110/POP3, 143/IMAP, 587/SMTP on 0.0.0.0 with security None), deletes all then reinserts the four defaults and reloads the snapshot, and maps store failure to ``E_FAIL`` retaining the prior snapshot. Focused coverage ``24/24``; full suite excluding the two AV-locked EICAR cleanup methods ``1824 passed, 0 failed, 8 opt-in skips`` (1832 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: authenticated ``Accounts.Add()`` plus new-item ``Account.Save()`` INSERT parity.

Authoritative 2026-08-05 continuation: code/test commit ``f85bf4681`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed domain mutations. Legacy schema anchors are ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql`` (``hm_domains`` and dependent tables). The fixture proves ``OUTPUT INSERTED.domainid`` identity readback with per-insert increments, anti-spam/limitations bit-packing round-trip, owner-scoped UPDATE/DELETE no-op against unknown IDs, transactional cascade DELETE removing domain aliases, distribution lists + recipients, aliases, account rules and message chains, and accounts before the domain row, and NOT NULL name violations on INSERT/UPDATE leaving no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused Domains suite is ``23/23``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1819 passed, 0 failed, 8 opt-in skips`` (1827 total). Running the full suite with the SQL connection set still surfaces the ``5`` pre-existing message-indexing opt-in failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: next authenticated Admin collection mutation after domains.

Authoritative 2026-08-05 continuation: code/test commit ``aacbabb99`` completes authenticated owner-scoped ``Domains.DeleteByDBID`` plus attached ``Domain.Delete()`` parity, completing the domain mutation trio (insert ``444d4f777``, update ``1778f619d``, delete). Legacy anchors are ``InterfaceDomains::DeleteByDBID``, ``InterfaceDomain::Delete``, ``PersistentDomain::DeleteObject`` (``hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:46-70``), and ``Collection::DeleteItemByDBID`` (``hmailserver/source/Server/Common/BO/Collection.h:181-200``). .NET path preserves installed Domains/Domain IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live server-admin authentication, treats unknown/stale IDs as successful no-ops, maps store failure to ``E_FAIL`` retaining the owner snapshot, and removes only the selected snapshot after a transactional cascade delete succeeds. Data-directory folder deletion remains fenced. Focused coverage ``22/22``; full suite excluding the two AV-locked EICAR cleanup methods ``1819 passed, 0 failed, 7 opt-in skips`` (1826 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: isolated SQL identity/readback rollback evidence for domain mutations.

Authoritative 2026-08-05 continuation: code/test commit ``1778f619d`` completes authenticated existing-row ``Domain.Save()`` UPDATE parity after domain insert ``444d4f777``. Legacy anchors are ``InterfaceDomain::Save`` and ``PersistentDomain::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:167-230``). .NET path preserves installed Domains/Domain IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live server-admin authentication, stages setters on retained items, persists via a parameterized identity-constrained UPDATE with the same anti-spam/limitations bit packing, maps failed or no-row updates to ``E_FAIL``, and replaces only the matching snapshot after success. Focused coverage ``17/17``; full suite excluding the two AV-locked EICAR cleanup methods ``1814 passed, 0 failed, 7 opt-in skips`` (1821 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: owner-scoped ``Domains.DeleteByDBID`` plus attached ``Domain.Delete()`` parity.

Authoritative 2026-08-05 continuation: code/test commit ``444d4f777`` completes authenticated ``Domains.Add()`` plus new-item ``Domain.Save()`` INSERT parity. Legacy anchors are ``InterfaceDomains::Add`` (``hmailserver/source/Server/COM/InterfaceDomains.cpp:99``), ``InterfaceDomain::Save``, and ``PersistentDomain::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:167-230``). .NET path preserves installed Domains/Domain IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live server-admin authentication on Add/setters/Save, stages legacy defaults on the Add child, stages all 29 setters, persists via a parameterized insert with ``OUTPUT INSERTED.domainid`` including legacy anti-spam/limitations bit packing, retains the failed draft on store failure mapping to ``E_FAIL``, and appends only the saved snapshot after success. Existing-row Save/Delete remain ``E_NOTIMPL``. Focused coverage ``13/13``; full suite excluding the two AV-locked EICAR cleanup methods ``1810 passed, 0 failed, 7 opt-in skips`` (1817 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: authenticated existing-row ``Domain.Save()`` UPDATE parity.

Authoritative 2026-08-05 continuation: code/test commit ``dbcbc346a`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed rule mutations. Legacy schema anchors are ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql`` (``hm_rules``, ``hm_rule_criterias``, ``hm_rule_actions``). The fixture proves ``OUTPUT INSERTED.ruleid`` identity readback with per-insert increments, owner-scoped UPDATE/DELETE that no-op against foreign account IDs, transactional cascade DELETE removing criteria/action rows before the rule row, and NOT NULL name violations on INSERT/UPDATE leaving no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused Rules suite is ``113/113``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1805 passed, 0 failed, 7 opt-in skips`` (1812 total). Running the full suite with the SQL connection set still surfaces the ``5`` pre-existing message-indexing opt-in failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: next authenticated Admin collection mutation after rules.

Authoritative 2026-08-05 continuation: code/test commit ``d7694c227`` completes authenticated existing-row ``Rule.Save()`` UPDATE parity after rule insert ``0239f30a1``. Legacy anchors are ``InterfaceRule::Save`` and ``PersistentRule::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:73-120``), whose existing-row branch updates ``hm_rules`` columns by ``ruleid``. .NET path preserves installed Rules/Rule IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live account/server authentication on setters/Save, stages setters on retained items, persists via a parameterized owner-scoped UPDATE (``WHERE ruleid AND ruleaccountid``), maps failed or no-row updates to ``E_FAIL`` retaining staged snapshot and generation, and replaces only the matching snapshot after success. Focused coverage ``112/112``; full suite excluding the two AV-locked EICAR cleanup methods ``1805 passed, 0 failed, 6 opt-in skips`` (1811 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: isolated SQL identity/readback rollback evidence for rule mutations.

Authoritative 2026-08-05 continuation: code/test commit ``0239f30a1`` completes authenticated account-owned ``Rules.Add()`` plus new-item ``Rule.Save()`` INSERT parity. Legacy anchors are ``InterfaceRules::Add`` (``hmailserver/source/Server/COM/InterfaceRules.cpp:91``), ``InterfaceRule::Save``, and ``PersistentRule::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:73-120``). .NET path preserves installed Rules/Rule IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live account/server authentication on Add/setters/Save, stages legacy defaults (``Active=true``, ``UseAnd=true``, empty name, sort order 0) on the Add child, stages all four setters, uses a parameterized insert with ``OUTPUT INSERTED.ruleid``, retains the failed draft on store failure mapping to ``E_FAIL``, and publishes only the saved snapshot into the generation after success. Existing-row Save/setters remain ``E_NOTIMPL``. Focused coverage ``108/108``; full suite excluding the two AV-locked EICAR cleanup methods ``1801 passed, 0 failed, 6 opt-in skips`` (1807 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: authenticated existing-row ``Rule.Save()`` UPDATE parity.

Authoritative 2026-08-05 continuation: code/test commit ``ad97b391b`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed route mutations. Legacy schema anchors are ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql`` (``hm_routes``, ``hm_routeaddresses``). The fixture proves ``OUTPUT INSERTED.routeid`` identity readback with per-insert increments, identity-preserving UPDATE with BlowFish password round-trip, cascade DELETE removing ``hm_routeaddresses`` rows before the ``hm_routes`` row, unknown-ID UPDATE/DELETE returning false with rows intact, and NOT NULL violations on INSERT/UPDATE leaving no partial row and the original row intact. Live LocalDB evidence is ``1/1``; the focused Routes suite is ``36/36``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1796 passed, 0 failed, 6 opt-in skips`` (1802 total). Running the full suite with the SQL connection set still surfaces the ``5`` pre-existing message-indexing opt-in failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: next authenticated Admin collection mutation after routes.

Authoritative 2026-08-05 continuation: code/test commit ``24510aafa`` completes authenticated owner-scoped ``Routes.DeleteByDBID`` plus attached ``Route.Delete()`` parity, completing the route mutation trio (insert ``264995c17``, update ``84135364e``, delete). Legacy anchors are ``InterfaceRoutes::DeleteByDBID``, ``InterfaceRoute::Delete`` (``hmailserver/source/Server/COM/InterfaceRoute.cpp:582``), ``PersistentRoute::DeleteObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:31-40``) deleting route addresses then the ``hm_routes`` row, and ``Collection::DeleteItemByDBID`` (``hmailserver/source/Server/Common/BO/Collection.h:181-200``). .NET path preserves installed Routes/Route IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live server-administrator authentication, treats unknown/stale IDs as successful no-ops, maps store failure to ``E_FAIL`` retaining the owner snapshot, and removes only the selected snapshot after the cascaded delete succeeds. Focused coverage ``35/35``; full suite excluding the two AV-locked EICAR cleanup methods ``1796 passed, 0 failed, 5 opt-in skips`` (1801 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: isolated SQL identity/readback rollback evidence for route mutations.

Authoritative 2026-08-05 continuation: code/test commit ``84135364e`` completes authenticated existing-row ``Route.Save()`` UPDATE parity after route insert ``264995c17``. Legacy anchors are ``InterfaceRoute::Save`` (``hmailserver/source/Server/COM/InterfaceRoute.cpp:243``) and ``PersistentRoute::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:53-88``), whose existing-row branch updates all legacy ``hm_routes`` columns by ``routeid``. .NET path preserves installed Routes/Route IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live server-administrator authentication on setters/Save, stages setters on retained items including ``SetRelayerAuthPassword``, persists via a parameterized identity-constrained UPDATE, maps failed or no-row updates to ``E_FAIL`` retaining staged snapshot and collection state, and replaces only the matching snapshot after success. Focused coverage ``30/30``; full suite excluding the two AV-locked EICAR cleanup methods ``1791 passed, 0 failed, 5 opt-in skips`` (1796 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: owner-scoped ``Routes.DeleteByDBID`` plus attached ``Route.Delete()`` parity.

Authoritative 2026-08-05 continuation: code/test commit ``264995c17`` completes authenticated ``Routes.Add()`` plus new-item ``Route.Save()`` INSERT parity. Legacy anchors are ``InterfaceRoutes::Add`` (``hmailserver/source/Server/COM/InterfaceRoutes.cpp``), ``InterfaceRoute::Save`` (``hmailserver/source/Server/COM/InterfaceRoute.cpp:243``), and ``PersistentRoute::SaveObject`` (``hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:53-88``), which inserts all legacy ``hm_routes`` columns with ``routeid`` identity. .NET path preserves installed Routes/Route IID/CLSID/ProgID/DISPID/vtable/type-library shape plus direct activation denial, rechecks live server-administrator authentication on Add/setters/Save, stages legacy defaults (``AllAddresses=true``, zeros, empty strings) on the Add child, stages all twelve setters including ``SetRelayerAuthPassword``, uses a parameterized insert with ``OUTPUT INSERTED.routeid`` persisting the BlowFish-encrypted relayer password, retains the failed draft on store failure mapping to ``E_FAIL``, and appends only the saved snapshot after success. Existing-row Save and Delete remain ``E_NOTIMPL``. Focused coverage ``26/26``; full suite excluding the two AV-locked EICAR cleanup methods ``1787 passed, 0 failed, 5 opt-in skips`` (1792 total). Real COM activation, backup/restore, migration, SEC-18, release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state used. Next action: authenticated existing-row ``Route.Save()`` UPDATE parity.

Authoritative 2026-08-05 continuation: code/test commit ``36270f965`` adds the opt-in isolated SQL identity/readback and rollback evidence for the completed distribution-list recipient COM mutations. Legacy schema anchors are ``hmailserver/source/DBScripts/CreateTablesMSSQL.sql:309-340``. The fixture proves ``OUTPUT INSERTED.distributionlistrecipientid`` identity readback with per-insert increments, owner-scoped UPDATE/DELETE predicates that no-op against foreign list IDs, identity-preserving UPDATE, and statement-level rollback where NOT NULL address violations leave no partial row and the original row intact. Live LocalDB evidence is ``1/1``; focused recipient coverage is ``24/24``; the full suite excluding the two AV-locked EICAR cleanup methods is ``1782 passed, 0 failed, 5 opt-in skips`` (1787 total). Running the full suite with the SQL connection set executes the previously skipped message-indexing opt-in fixtures for the first time in this environment and surfaces ``5`` pre-existing failures unrelated to this test-only slice. Real COM activation, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: next authenticated Admin collection mutation.

Authoritative 2026-08-05 continuation: code/test commit `20ec7a285` completes authenticated owner-scoped distribution-list recipient `DeleteByDBID` plus attached `DistributionListRecipient.Delete()` parity after `259cf0867`. Legacy references are `hmailserver/source/Server/COM/InterfaceDistributionListRecipients.cpp:37-51`, `InterfaceDistributionListRecipient.cpp:115-137`, `hmailserver/source/Server/Common/BO/Collection.h:181-200`, `hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:25-43`, and `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:331-340`. The .NET path preserves the installed recipient IID/CLSID/ProgID/DISPID/vtable/type-library shape and direct activation `E_ACCESSDENIED`, carries live owner authentication into retained items, treats unknown/foreign/stale IDs as no-ops, constrains deletion by recipient ID and owner list ID, maps store failure to `E_FAIL`, retains the owner snapshot on failure, and removes only the selected snapshot after success. Focused coverage is `20/20`; the full suite excluding the two AV-locked EICAR cleanup methods is `1782 passed, 0 failed, 4 opt-in skips` (1786 total). Direct full execution remains environment-blocked by the two unrelated scanner-runtime cleanup failures. SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: isolated SQL identity/readback and rollback evidence for completed COM mutations.

Authoritative 2026-08-05 continuation: code/test commit `259cf0867` completes authenticated owner-scoped existing-row `DistributionListRecipient.Save()` UPDATE parity after `91645dc3a`. Legacy references are `hmailserver/source/Server/COM/InterfaceDistributionListRecipient.cpp:133-157`, `hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:103-139`, and `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:331-340`. The .NET path preserves the installed recipient IID/CLSID/ProgID/DISPID/vtable/type-library shape and direct activation `E_ACCESSDENIED`, carries live owner authentication into retained items, stages `RecipientAddress`, constrains the update by recipient ID and owner list ID, retains staged state and the owner snapshot on failure, and replaces only the matching snapshot after successful persistence. Focused coverage is `15/15`; the full suite excluding the two AV-locked EICAR cleanup methods is `1777 passed, 0 failed, 4 opt-in skips` (1781 total). Direct full execution remains environment-blocked by the two unrelated scanner-runtime cleanup failures. Recipient Delete/DeleteByDBID, SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: authenticated owner-scoped recipient `DeleteByDBID` plus attached `DistributionListRecipient.Delete()` parity.

Authoritative 2026-08-05 continuation: code/test commit `91645dc3a` completes authenticated owner-scoped distribution-list recipient `Add()` plus new-item `DistributionListRecipient.Save()` INSERT parity. Legacy references are `hmailserver/source/Server/COM/InterfaceDistributionListRecipients.cpp:53-83`, `InterfaceDistributionListRecipient.cpp:93-157`, `hmailserver/source/Server/Common/Persistence/PersistentDistributionListRecipient.cpp:103-139`, and `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:331-340`. The .NET path preserves the installed `IInterfaceDistributionListRecipients`/`IInterfaceDistributionListRecipient` IID, CLSID, ProgID, DISPID, vtable, and type-library shape and direct activation `E_ACCESSDENIED`, passes the owning list’s live authentication callback into recipient facades, binds staged recipients to the owning list ID, uses parameterized `OUTPUT INSERTED.distributionlistrecipientid`, retains failed drafts, denies retained reads/mutations after auth loss, and publishes only after successful insert. Focused coverage is `11/11`; the full suite excluding the two AV-locked EICAR cleanup methods is `1773 passed, 0 failed, 4 opt-in skips` (1777 total). Direct full execution remains environment-blocked by the two unrelated scanner-runtime cleanup failures. Existing-row recipient update/delete, SMTP list policy, live SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: authenticated existing-row `DistributionListRecipient.Save()` UPDATE parity.

Authoritative 2026-08-05 continuation: code/test commit `fb6de84f7` completes authenticated owner-scoped `DistributionLists.DeleteByDBID` plus attached `DistributionList.Delete()` parity. Legacy `InterfaceDistributionLists::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceDistributionLists.cpp:38-53`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), and `PersistentDistributionList::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:35-54`) deletes `hm_distributionlistsrecipients` by list ID before the owner-scoped `hm_distributionlists` row. The .NET path preserves installed `IInterfaceDistributionLists`/`IInterfaceDistributionList` IID, CLSID, ProgID, DISPID, vtable, and type-library shape plus direct activation `E_ACCESSDENIED`, rechecks live authorization, scopes deletion to the owning domain snapshot, treats unknown/stale IDs as successful no-ops, maps failed main deletion to `E_FAIL`, retains the snapshot on failure, and removes it only after success. Focused coverage is `27/27`; the full suite excluding the two AV-locked EICAR cleanup methods is `1768 passed, 0 failed, 4 opt-in skips` (1772 total). Direct full execution remains environment-blocked by the two unrelated scanner-runtime cleanup failures. Recipient COM mutation, SMTP list policy, live SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: authenticated owner-scoped distribution-list recipient mutation.

Authoritative 2026-08-05 continuation: code/test commit `f2d33c348` completes the bounded authenticated domain-owned `DistributionLists.Add()` plus new-item `DistributionList.Save()` INSERT slice. Legacy references are `hmailserver/source/Server/COM/InterfaceDomain.cpp:574-603`, `InterfaceDistributionLists.cpp:55-84`, `InterfaceDistributionList.cpp:81-277`, and `hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:118-157`. The .NET path preserves the installed `IInterfaceDistributionLists`/`IInterfaceDistributionList` IID, CLSID, ProgID, DISPID, vtable, and type-library shape and direct activation `E_ACCESSDENIED`, passes live authorization through `Domain.DistributionLists`, stages all five editable fields, binds drafts to the owning domain, uses parameterized `OUTPUT INSERTED.distributionlistid`, retains failed drafts, and publishes the generated-ID owner snapshot only after success. Focused coverage is `14/14`; the full suite excluding the two AV-locked EICAR cleanup methods is `1755 passed, 0 failed, 4 opt-in skips` (1759 total). Direct full execution remains environment-blocked by the two unrelated scanner-runtime cleanup failures. Existing-row update/delete, recipients mutation, SMTP list policy, live SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: authenticated existing-row `DistributionList.Save()` UPDATE parity, then deletion/recipient mutation as separate slices.
Authoritative 2026-08-05 continuation: code/test commit `852aa1586` completes authenticated existing-row `DistributionList.Save()` UPDATE parity after `f2d33c348`. Legacy `InterfaceDistributionList::Save` (`hmailserver/source/Server/COM/InterfaceDistributionList.cpp:252-271`) delegates to `PersistentDistributionList::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:118-157`), which updates all six `hm_distributionlists` fields by `distributionlistid`. The .NET path preserves the installed `IInterfaceDistributionLists`/`IInterfaceDistributionList` IID, CLSID, ProgID, DISPID, vtable, and type-library shape and direct activation `E_ACCESSDENIED`, passes live authorization through `Domain.DistributionLists`, stages updates on the owning facade, uses parameterized `WHERE distributionlistid = @ID`, retains staged state and the owner snapshot on failure, and replaces only the matching owner snapshot after success. Focused coverage is `20/20`; the full suite excluding the two AV-locked EICAR cleanup methods is `1761 passed, 0 failed, 4 opt-in skips` (1765 total). Direct full execution remains environment-blocked by the two unrelated scanner-runtime cleanup failures. Delete, recipients mutation, SMTP list policy, live SQL identity/readback, real COM activation, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. No production service, database, Data directory, IIS, COM registration, or DCOM state was used. Next action: authenticated owner-scoped `DistributionLists.DeleteByDBID` plus attached `DistributionList.Delete()` parity.

Authoritative 2026-08-05 continuation: code/test commit `b8025f2fe` completes authenticated owner-scoped SURBL deletion parity. Legacy `InterfaceSURBLServers::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceSURBLServers.cpp:88-101`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), which calls `PersistentSURBLServer::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:25-33`) only for contained IDs; unknown and stale IDs are successful no-ops. Legacy `InterfaceSURBLServer::Delete` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:187-208`) rechecks server-admin authorization and routes attached objects through the parent. The .NET path preserves the installed SURBL IID/CLSID/ProgID/DISPID/vtable shape and direct activation denial, rechecks live authorization, retains the owner snapshot on store failure, and publishes removal only after successful parameterized deletion. Focused coverage is `21/21`. Direct full Net10 is `1748/1754` with `2` unrelated scanner-runtime cleanup failures and `4` opt-in skips; the suite excluding those two scanner classes is `1743/1747` with `4` skips. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `835e73804` completes authenticated existing-row `SURBLServer.Save()` UPDATE parity after `cd627826e`. Legacy `InterfaceSURBLServer::Save` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:11-36`) rechecks server-admin authentication and delegates to `PersistentSURBLServer::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:55-90`) for identity-constrained updates to `surblactive`, `surblhost`, `surblrejectmessage`, and `surblscore`. The .NET path preserves installed SURBL IID/CLSID/ProgID/DISPID/vtable identity and direct activation denial, scopes staged setters and Save to the owning collection, retains staged state and the owner snapshot on failure, and publishes only the matching replacement snapshot after success. Delete, SQL identity/readback, real COM activation, live SURBL behavior, rollback injection, backup/restore, migration, SEC-18, and release gates remain open. Focused coverage is `15/15`; full Net10 is `1744/1748` with `4` opt-in skips. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `cd627826e` completes authenticated `SURBLServers.Add()` plus new-item `SURBLServer.Save()` INSERT parity. Legacy `InterfaceSURBLServers::Add` (`hmailserver/source/Server/COM/InterfaceSURBLServers.cpp:134-163`) creates an ID-zero child; `InterfaceSURBLServer::Save` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:11-36`) rechecks server-admin authentication and publishes only after `PersistentSURBLServer::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:55-90`) assigns `surblid`. The .NET path preserves installed SURBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, stages all four new-item fields, uses parameterized `OUTPUT INSERTED.surblid`, retains failed drafts, and appends only the saved snapshot. Existing-row mutation, Delete, live SURBL behavior, SQL identity/readback, real COM activation, rollback injection, and release gates remain open. Focused coverage is `12/12`; full Net10 is `1741/1745` with `4` opt-in skips. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `811ce0300` completes authenticated owner-scoped `DNSBlackLists.DeleteByDBID` plus attached `DNSBlackList.Delete()` parity after `f6033de3b` completed existing-row UPDATE. Legacy `InterfaceDNSBlackLists::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:91-106`) delegates to the owning collection and treats unknown IDs as successful no-ops; `InterfaceDNSBlackList::Delete` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:221-242`) rechecks server-admin authentication and routes attached objects through the parent, while persistence is `PersistentDNSBlackList::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:25-32`). The .NET path preserves installed DNSBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, maps store failure to `E_FAIL`, retains the owner snapshot on failure, and removes only the selected snapshot after success. Focused coverage is `20/20`; full Net10 is `1737/1741` with `4` opt-in skips. Live DNSBL behavior, SQL identity/readback, real COM activation, rollback injection, and release gates remain open. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `f6033de3b` completes authenticated existing-row `DNSBlackList.Save()` UPDATE parity after `e956dcd3d` completed Add/INSERT. Legacy `InterfaceDNSBlackList::Save` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:14-37`) rechecks server-admin authentication and calls `PersistentDNSBlackList::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:55-90`), which updates all five `hm_dnsbl` fields by `sblid`. The .NET path preserves installed DNSBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, scopes setters and Save to the owning collection, rechecks retained-object administrator access, maps failed updates to `E_FAIL`, and replaces only the matching owner snapshot after successful SQL. Focused coverage is `16/16`; full Net10 is `1733/1737` with `4` opt-in skips. Existing-row Delete, SQL identity/readback, live DNSBL behavior, real COM activation, rollback injection, and release gates remain open. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `e956dcd3d` completes authenticated `DNSBlackLists.Add()` plus new-item `DNSBlackList.Save()` INSERT parity. Legacy `InterfaceDNSBlackLists::Add` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:138-165`) creates an ID-zero child scoped to the owning collection; `InterfaceDNSBlackList::Save` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:14-34`) persists first and publishes only after `PersistentDNSBlacklist::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:55-90`) assigns `sblid`. The .NET path preserves installed DNSBL IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, and direct activation denial, stages all five new-item fields, uses parameterized `OUTPUT INSERTED.sblid`, retains failed drafts, and appends only the saved relay to the owning snapshot. Settings authentication is rechecked for Add, setters, and Save; existing-row mutation, Delete, live DNSBL reconfiguration, SMTP trust, SQL identity/readback, real COM activation, and release gates remain open. Focused coverage is `12/12`; full Net10 is `1729/1733` with `4` opt-in skips. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, implement DNSBlackList existing-row Save UPDATE parity. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `fdfdc6c42` completes authenticated owner-scoped `GreyListingWhiteAddresses.DeleteByDBID` plus attached `GreyListingWhiteAddress.Delete()` parity after `6ba86e16b` completed existing-row UPDATE. Legacy `InterfaceGreyListingWhiteAddresses::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddresses.cpp:85-102`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`); `InterfaceGreyListingWhiteAddress::Delete` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:117-137`) rechecks Administrator authentication and routes attached items through the owner, while direct persistence is `PersistentGreyListingWhiteAddress::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:26-32`). The installed COM identity/vtable/DISPID shape and direct activation denial remain unchanged (`hmailserver/source/Server/hMailServer/hMailServer.idl:2356-2387`). Focused GreyListingWhiteAddresses/SQL coverage is `21/21`; full Net10 is `1725/1729` with `4` opt-in skips. The .NET path treats unknown IDs as no-ops, retains the owner snapshot on failed deletion, and removes only the selected snapshot after success. SQL identity/readback, real COM activation, greylisting live reconfiguration, and rollback injection remain open. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, implement DNSBlackLists Add/Save INSERT parity. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `6ba86e16b` completes authenticated existing-row `GreyListingWhiteAddress.Save()` UPDATE parity after `b31ce86c1` completed Add/INSERT. Legacy `InterfaceGreyListingWhiteAddress::Save` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:10-31`) invokes `PersistentGreyListingWhiteAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:52-84`), whose existing-row path updates `whiteipaddress` and `whiteipdescription` by `whiteid`; the setter continues to accept invalid user-editable IP text through `GreyListingWhiteAddress::SetUserEditableIPAddress` (`hmailserver/source/Server/Common/BO/GreyListingWhiteAddress.cpp:64-74`). The installed COM identity/vtable/DISPID shape and direct activation denial remain unchanged (`hmailserver/source/Server/hMailServer/hMailServer.idl:2356-2387`). Focused GreyListingWhiteAddresses/SQL coverage is `17/17`; full Net10 is `1721/1725` with `4` opt-in skips. The .NET path retains failed staged values and replaces only the matching owner snapshot after successful update. SQL identity/readback, real COM activation, greylisting Delete/live reconfiguration, and rollback injection remain open. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, implement greylisting white-address DeleteByDBID/item Delete parity. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `b31ce86c1` completes authenticated `GreyListingWhiteAddresses.Add()` plus new-item `GreyListingWhiteAddress.Save()` INSERT parity. Legacy `InterfaceGreyListingWhiteAddresses::Add` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddresses.cpp:162-187`) creates an ID-zero child; `InterfaceGreyListingWhiteAddress::Save` (`hmailserver/source/Server/COM/InterfaceGreyListingWhiteAddress.cpp:10-31`) rechecks server-admin authentication and publishes only after `PersistentGreyListingWhiteAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentGreyListingWhiteAddress.cpp:52-84`) assigns `whiteid`. `GreyListingWhiteAddress::SetUserEditableIPAddress` (`hmailserver/source/Server/Common/BO/GreyListingWhiteAddress.cpp:64-74`) and `SQLStatement::ConvertWildcardToLike` (`hmailserver/source/Server/Common/SQL/SQLStatement.cpp:591-610`) preserve invalid user-editable IP text rather than rejecting it. The installed `IInterfaceGreyListingWhiteAddresses`/`IInterfaceGreyListingWhiteAddress` IIDs, vtable, DISPIDs, ProgIDs, and direct activation denial remain unchanged (`hmailserver/source/Server/hMailServer/hMailServer.idl:2356-2387`). Focused GreyListingWhiteAddresses/SQL coverage is `14/14`; full Net10 is `1718/1722` with `4` opt-in skips. SQL identity/readback, real COM activation, existing-row mutation, greylisting live reconfiguration, and rollback injection remain open. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, implement greylisting white-address existing-row Save parity. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `ae6239f54` completes authenticated owner-scoped `BlockedAttachments.DeleteByDBID` plus attached `BlockedAttachment.Delete()` parity after `2324b0131` completed existing-row UPDATE. Legacy `InterfaceBlockedAttachments::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceBlockedAttachments.cpp:76-89`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`); `InterfaceBlockedAttachment::Delete` (`hmailserver/source/Server/COM/InterfaceBlockedAttachment.cpp:122-141`) rechecks server-admin authentication and routes attached items through the parent, while direct objects call `PersistentBlockedAttachment::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentBlockedAttachment.cpp:26-32`). The installed `IInterfaceBlockedAttachments`/`IInterfaceBlockedAttachment` IIDs, vtable, DISPIDs, ProgIDs, and direct activation denial remain unchanged (`hmailserver/source/Server/hMailServer/hMailServer.idl:2285-2312`). The .NET path treats unknown IDs as no-ops, rechecks retained-item administrator authentication, maps store failure to `E_FAIL`, and publishes removal only after the delete call succeeds. Focused BlockedAttachments/SQL coverage is `19/19`; full Net10 is `1714/1718` with `4` opt-in skips. SQL readback, real COM activation, scanner/live reconfiguration, and rollback injection remain open. Next actions: obtain approved disposable SQL Group/member insert/update/delete/readback and rollback evidence; if unavailable, map the next independent authenticated Admin mutation. No production resource or service state was used.

Authoritative 2026-08-04 continuation: code/test commit `843360528` wires `ISmtpGlobalWhitelistEvaluator` into `SqlServerSmtpMessageReceiver` and registers it from `Host`. `SqlServerSmtpGlobalWhitelistEvaluator` loads `hm_whitelist` per request, uses `WhiteListMatcher`, and fails closed on non-cancellation store faults. Legacy anchors are `WhiteListCache::IsWhitelisted` (`hmailserver/source/Server/Common/AntiSpam/WhiteListCache.cpp:52-88`), `SMTPConnection::InitializeSpamProtectionType_` (`hmailserver/source/Server/SMTP/SMTPConnection.cpp:419-424`), and `SMTPConnection::GetDoSpamProtection_` (`hmailserver/source/Server/SMTP/SMTPConnection.cpp:2330-2358`). Listener-created SMTP requests retain socket peer identity through `SmtpTcpListener.CreateConnectionContext` and `SmtpSession.HandleDataAsync`; ExternalFetch requests are marked `IsExternalFetch` and do not use the SMTP evaluator. A match skips the selected anti-spam checks while preserving URL, scripts, rules, attachment, antivirus, queue, and upstream recipient/auth/relay controls. Focused coverage is `71/71`; full Net10 is `1632/1632` with `4` opt-in skips. No live SQL readback, SMTP socket E2E, mutation visibility, or rollback evidence exists; bare sender default-domain canonicalization remains separate. Next actions: approved disposable SQL parent/UID evidence; performance acceptance beyond offline synthetic SEARCH/SORT; then disposable SQL whitelist mutation/readback and SMTP acceptance. No production resource or installed registration was used.

Authoritative 2026-08-04 continuation: code/test commit `298088e31` corrects the offline synthetic SEARCH/SORT benchmark so `ActualMatchCount` reports the measured result and `Correct` validates expected-versus-actual count equality (`hmailserver/source/Server.Net10/benchmarks/HMailServer.Net10.Benchmarks/SyntheticImapSearchSortBenchmark.cs:121-174`). Focused benchmark tests pass `3/3`; full Net10 passes `1618/1618` with `4` opt-in skips. Legacy anchors are `IMAPSearchParser::ParseCommand`, `IMAPSortParser::Parse`, and `IMAPSort::Sort`; this remains offline synthetic evidence and does not prove SQL FTS, live IMAP concurrency, C++ equivalence, or leak soak. Next actions: obtain approved disposable SQL parent/UID evidence; map and implement the bounded SMTP whitelist bypass; then extend performance acceptance beyond offline synthetic SEARCH/SORT. No production resource or installed registration was used.

Authoritative 2026-08-04 continuation: code/test commit `008e949dd` adds false-result and exception-path coverage for missing-known-UID cleanup in `ExternalFetchProcessor.DeleteMissingKnownUidsAsync` (`hmailserver/source/Server.Net10/src/HMailServer.Protocols/Pop3/ExternalFetchProcessor.cs:270-290`). Legacy `FetchAccountUIDList` (`hmailserver/source/Server/ExternalFetcher/FetchAccountUIDList.cpp:90-123`) similarly ignores a false persistence result while `PersistentFetchAccountUID::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccountUID.cpp:74-85`) returns the database result. The current path counts only successful `DeleteKnownUidAsync` results and fails the account on an exception; the tests preserve remote-success-before-local-UID deletion and do not change production behavior. Focused ExternalFetchProcessor coverage is `34/34`; full Net10 passes `1617/1617` with `4` opt-in skips. Remaining risks are no disposable SQL parent/UID readback or rollback evidence, quota admission, SMTP whitelist evaluator/cache parity, and performance/release gates. Next actions: obtain approved disposable SQL parent/UID evidence; map and implement the bounded SMTP whitelist bypass; then extend performance acceptance beyond offline synthetic SEARCH/SORT. No production resource or installed registration was used.

Authoritative 2026-08-04 continuation: code/test commit `a96ee1d10` completes the account-owned message-writer invalidation sub-matrix. Legacy `MailImporter::Import` (`hmailserver/source/Server/Common/Util/MailImporter.cpp:39-205`) and external-fetch `POP3ClientConnection::SaveMessage_` (`hmailserver/source/Server/ExternalFetcher/POP3ClientConnection.cpp:910-917`) persist account-owned delivered messages through `PersistentMessage::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:505-513`), which increments `AccountSizeCache`; queue import uses account 0 and remains unchanged. Append/copy/mutation/local/script/import writers now invalidate only after commit. Focused import/Host coverage passes `8/8`; full Net10 passes `1615/1615` with `4` opt-in skips. Residual risks are no disposable SQL callback/readback or rollback evidence, FetchAccountUID SQL evidence, quota admission, SMTP whitelist bypass parity, and performance/release gates. Next actions: obtain approved disposable SQL callback/readback and rollback evidence; prove FetchAccountUID ordering/failure in isolated SQL; then map and implement the bounded SMTP whitelist bypass. No production resource or installed registration was used.

Authoritative 2026-08-03 continuation: code/test commit `6a415afd1` aligns IMAP `GETQUOTA`/`GETQUOTAROOT` usage with legacy all-message account size. Legacy `AccountSizeCache::GetSize`/`PersistentAccount::GetMessageBoxSize` (`hmailserver/source/Server/Common/Cache/AccountSizeCache.cpp:59-72`; `hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:341-355`) sum every account-owned `hm_messages.messagesize` row; the prior Net10 `CASE WHEN m.messagetype = 2` expression did not. The current query uses account-scoped `COALESCE(SUM(m.messagesize), 0)` and keeps the established `/1024` truncation and account-limit conversion. Focused QUOTA tests pass `5/5`; full Net10 passes `1584/1584` with `4` opt-in skips. This does not close stateful AccountSizeCache lifecycle, quota admission, live SQL mixed-type readback, or performance acceptance. Next three actions: `GETQUOTAROOT` no-quota/domain-limit/mailbox-quoting parity; AccountSize semantic/writer matrix plus no-schema post-commit invalidation seam; approved disposable SQL identity/readback and rollback evidence when the isolated gate is available.

Authoritative 2026-08-03 continuation: code/test commit `1c201c3c4` completes authenticated `FetchAccounts.Add()` plus new-item `FetchAccount.Save()` INSERT parity. Legacy `InterfaceFetchAccounts::Add` (`hmailserver/source/Server/COM/InterfaceFetchAccounts.cpp:139-165`) creates an ID-zero child bound to the owning account; `InterfaceFetchAccount::Save` (`hmailserver/source/Server/COM/InterfaceFetchAccount.cpp:341-360`) persists first through `PersistentFetchAccount::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccount.cpp:153-207`) and assigns the generated `faid` before publication. The .NET path preserves FetchAccounts/FetchAccount IIDs, CLSIDs, ProgIDs, DISPIDs, vtable order, direct activation denial, owner-account scope, legacy Blowfish password storage, parameterized insert SQL, failed-draft retention, and post-success snapshot publication. Existing-row mutation, FetchAccountUID cleanup, retry/live-fetch behavior, and sibling-facade freshness remain open. Focused coverage is `27/27`; full Net10 is `1584/1584` with `4` opt-in skips. Next three actions: approved disposable SQL identity/readback plus rollback evidence when the isolated gate is available; AccountSize semantic/writer matrix and no-schema post-commit invalidation seam; then FetchAccountUID cleanup and existing-row mutation. No production resource or installed registration was used.

Authoritative 2026-08-03 continuation: code/test commit `cc53c77eb` completes authenticated whitelist `Clear()` parity. Legacy `InterfaceWhiteListAddresses::Clear` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:42-61`) delegates to `Collection::DeleteAll`, which calls `PersistentWhiteListAddress::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:27-34`) for each `hm_whitelist` row. The .NET path preserves installed COM identity/direct activation denial, uses a whole-collection parameterized delete, clears the owner snapshot only after store success, and maps failure to `E_FAIL` while retaining the previous snapshot. Focused whitelist/SQL coverage is `27/27`; full Net10 is `1580/1580` with `4` opt-in skips. SMTP evaluator/cache invalidation, sibling-facade freshness, and broader Admin mutation remain explicit blockers. Next three independent actions: approved disposable SQL identity/readback plus injected rollback evidence; IMAP account-size/cache lifecycle parity; isolated FetchAccounts SQL/UID cleanup evidence. No production resource or installed registration was used.

Authoritative 2026-08-03 continuation: code/test commit `cd91d276a` completes authenticated whitelist deletion parity. Legacy `InterfaceWhiteListAddresses::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:109-124`) delegates to `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), and `InterfaceWhiteListAddress::Delete` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:33-54`) rechecks server-admin access before routing attached items through the owner; persistence is `PersistentWhiteListAddress::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:27-34`). The .NET path retains installed COM identity and direct activation denial, parameterizes the `hm_whitelist` identity delete, publishes snapshot removal only after store success, maps failure to `E_FAIL`, and preserves owner-scoped unknown/foreign/stale no-op behavior. Focused whitelist/SQL coverage is `24/24`; full Net10 is `1577/1577` with `4` opt-in skips. SMTP evaluator/cache invalidation, sibling-facade freshness, and Clear remain blockers. Next three independent actions: approved disposable SQL identity/readback plus injected rollback evidence; IMAP account-size/cache lifecycle parity; isolated FetchAccounts SQL/UID cleanup evidence. No production resource or installed registration was used.

Authoritative 2026-08-03 continuation: code/test commit `d79b84ae8` completes authenticated existing-row `WhiteListAddress.Save()` UPDATE parity. Legacy `InterfaceWhiteListAddress::Save` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:8-31`) calls `PersistentWhiteListAddress::SaveObject`, whose existing-row path (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:57-93`) updates `hm_whitelist` by `whiteid`, stores the four IP columns/email/description, and requests the legacy cache refresh. The .NET path keeps the installed whitelist COM contract, authenticated `AntiSpam.WhiteListAddresses` access, direct activation denial, and owner-scoped staged facades; it publishes the matching parent snapshot only after parameterized SQL update success. Invalid parsed-IP retention and failed-update snapshot preservation are covered. Focused whitelist/SQL tests pass `22/22`; full Net10 passes `1573/1573` with `4` opt-in skips. SMTP evaluator/cache invalidation and sibling-facade freshness remain explicit blockers, with Delete/Clear and broader Admin mutation still fenced. Next three independent actions: approved disposable SQL identity/readback plus injected rollback evidence; IMAP account-size/cache lifecycle parity; isolated FetchAccounts SQL/UID cleanup evidence. No production resource or installed COM registration was used.

The preceding security follow-up `3165b3cab` hardened retained `WhiteListAddresses.Add()` against live administrator loss before creating a child; its focused/full results were `16/16` and `1569/0/4`. Do not claim SMTP whitelist cache/evaluator parity or cross-facade snapshot freshness; both require separate work.

Authoritative 2026-08-03 continuation: code/test commit `5ff2f4ab7` completes authenticated `AntiSpam.WhiteListAddresses.Add()` and new-item `WhiteListAddress.Save()` INSERT parity. Legacy `InterfaceWhiteListAddresses::Add` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:186-215`) creates an ID-zero item bound to the owner; `InterfaceWhiteListAddress::Save` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:8-31`) rechecks server-admin authorization, calls `PersistentWhiteListAddress::SaveObject`, and publishes through `AddToParentCollection`; `PersistentWhiteListAddress::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:57-93`) writes the six legacy `hm_whitelist` fields, assigns the generated ID, and signals the legacy whitelist cache. Current code keeps the installed COM contract, authenticated AntiSpam boundary, direct activation denial, owner-scoped staged facade, and failure-retaining snapshot behavior. Focused whitelist/SQL tests pass `16/16`; full Net10 passes `1569/1569` with `4` opt-in skips. No live SQL, native COM activation, SMTP cache refresh, or production resource was used. Next three independent actions: approved disposable SQL identity/readback plus injected rollback evidence; whitelist existing-row Save parity; IMAP account-size/cache lifecycle evidence. Protocol DELETE/RENAME, failed-file-cleanup repair/alert semantics, restore/upgrade rollback, SEC-18, and performance/release gates remain open. The previous subscription continuation below is historical.

Authoritative 2026-08-03 continuation: code/test commit `1a98e88a8` completes legacy IMAP `SUBSCRIBE`/`UNSUBSCRIBE` protocol parity. `IMAPCommandSUBSCRIBE::ExecuteCommand` (`hmailserver/source/Server/IMAP/IMAPCommandSubscribe.cpp:23-67`) authenticates, accepts the first argument, silently accepts the public-folder root, requires lookup permission for other folders, and persists `folderissubscribed = 1`; `IMAPCommandUNSUBSCRIBE::ExecuteCommand`/`ConfirmPossibleToUnsubscribe` (`hmailserver/source/Server/IMAP/IMAPCommandUnsubscribe.cpp:23-67`) require exactly one argument, reject missing/public folders, and persist private-folder unsubscription. The .NET path adds a narrow protocol subscription store, reuses SQL mailbox-path and public ACL resolution, updates only the owner-scoped `hm_imapfolders.folderissubscribed` row, and dispatches only from an authenticated session. COM identity/direct activation, SMTP, IMAP DELETE/RENAME, and live reconfiguration remain unchanged. Focused subscription/session/store coverage is `46/46`; full Net10 is `1564/1564` with `4` opt-in skips. Next three independent actions: approved disposable SQL identity/readback plus injected rollback evidence for IMAP deletion/Rules/ACL; IMAP account-size/cache lifecycle parity; isolated FetchAccounts SQL/UID cleanup evidence. Protocol DELETE, failed-file-cleanup repair/alert semantics, restore/upgrade rollback, SEC-18, and performance/release gates remain open. The older IMAP deletion paragraph immediately below is historical.

Authoritative 2026-08-03 continuation: code/test commit `0677320ad` completes authenticated parent-scoped IMAP folder deletion COM/store wiring. Legacy `InterfaceIMAPFolder::Delete` (`hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:249-262`) delegates attached items to their owning collection; `InterfaceIMAPFolders::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceIMAPFolders.cpp:145-161`) returns `DISP_E_BADINDEX` for absent IDs; `Collection<T,P>::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`) controls membership; and `PersistentIMAPFolder::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:48-97`) recursively deletes children/messages/ACLs while preserving root Inbox. The .NET path wires both COM deletion methods to the existing owner/account/parent-scoped transactional store, removes only the selected subtree from shared state after success, invokes configured file cleanup, and carries live Application authentication through Domains -> Accounts -> IMAPFolders so retained deletion fails closed after reauthentication failure. Installed COM identity/vtable/DISPID shape and direct activation denial remain unchanged. Focused IMAPFolders/Application coverage is `27/27`; deletion/store/helper coverage is `33/33`; full Net10 is `1556 passed, 0 failed, 4 opt-in skips`. Next three independent actions: approved disposable SQL identity/readback plus injected rollback evidence; IMAP account-size/cache/protocol lifecycle parity; isolated FetchAccounts SQL/UID cleanup evidence. Protocol `DELETE`, failed-file-cleanup repair/alert semantics, real COM activation, restore/upgrade rollback, SEC-18, and performance/release gates remain open. Older continuation paragraphs below are historical.

Authoritative 2026-08-01 continuation: code/test commit `808692ef5` follows `d5b25e701` and completes authenticated existing-row public-folder ACL `Save()` UPDATE parity. Legacy `InterfaceIMAPFolderPermission` setters/bit setters and `Save` (`hmailserver/source/Server/COM/InterfaceIMAPFolderPermission.cpp:75-208,227-242`) stage the complete ACL row; `PersistentACLPermission::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentACLPermission.cpp:76-164`) updates all `hm_acl` fields by identity after holder validation. The .NET path propagates owner-captured update delegates through index, DBID, and name wrappers, enforces owner/public-folder SQL predicates, maps false/exception to `E_FAIL`, and replaces snapshots only after success. Existing insert behavior, direct activation/public-folder scope, and installed COM identity/DISPIDs remain unchanged. Focused IMAP permission/store coverage is `36/36`; full Net10 is `1546` with `4` opt-in skips. No live SQL, COM activation, protocol ACL, duplicate-conflict, rollback fault injection, or round-trip evidence ran. Next slice: approved disposable SQL insert/update/readback evidence for ACL Add/Save plus Rules/public-folder deletion, then the hardened IMAP folder and FetchAccounts SQL fixtures. Older current-slice paragraphs below are historical.

Authoritative 2026-08-01 continuation: code/test commit `158774b65` follows `28bdc36d4` and completes authenticated public-folder `IMAPFolderPermissions.DeleteByDBID` parity. Legacy `InterfaceIMAPFolderPermissions::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceIMAPFolderPermissions.cpp:166-182`) calls the attached `ACLPermissions::DeleteItemByDBID` through `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`) and returns `S_OK` for unknown IDs; `PersistentACLPermission::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentACLPermission.cpp:28-36`) deletes the selected `hm_acl` row. The .NET path preserves public-folder-only `IMAPFolder.Permissions`, direct activation denial, and installed ACL COM identity/DISPIDs; SQL requires `aclid`, `aclsharefolderid`, and `folderaccountid = 0`, while snapshot removal occurs only after one affected row and store failures map to `E_FAIL`. Focused IMAP permission/store coverage is `17/17`; full Net10 is `1527` with `4` opt-in skips. No live SQL, rollback fault injection, or protocol ACL acceptance ran. Next slice: approved disposable SQL evidence for Rules and public-folder permission deletion, then the hardened IMAP folder and FetchAccounts SQL fixtures. Older current-slice paragraphs below are historical.

Authoritative 2026-08-01 continuation: code/test commit `28bdc36d4` follows `1a501b0cd` and completes the bounded authenticated Rules parent-delete slice. Legacy `InterfaceRules::DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceRules.cpp:126-135`) and `InterfaceRule::Delete` (`hmailserver/source/Server/COM/InterfaceRule.cpp:298-310`) route through `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`), so only a member of the owning collection is eligible and the COM wrapper returns `S_OK` for unknown IDs. `PersistentRule::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:175-188`) deletes the parent and `hm_rule_actions`/`hm_rule_criterias` children; those tables have no cascade in `hmailserver/source/Server/DBScripts/CreateTablesMSSQL.sql:471-523`. The .NET implementation adds one owner-scoped transactional store operation, preserves Rules/Rule COM identity and authenticated/direct activation boundaries, removes the shared generation only after success, maps persistence failure to `E_FAIL`, and keeps foreign, unknown, repeated, and stale objects as no-ops. Focused Rules COM/store coverage is `15/15`; full Net10 is `1523` with `4` opt-in skips. No live SQL or rollback fault-injection evidence ran. Next slice: approved disposable Rules SQL deletion/rollback evidence; then the hardened IMAP and FetchAccounts SQL fixtures and the next authenticated COM/Admin mutation. Older current-slice paragraphs below are historical.

The current bounded slice is code/test commit `310086e66` following `8167856b4`: authenticated parent-account-scoped `FetchAccounts.Delete(index)` and `DeleteByDBID` now reuse the existing `DeleteSelectedAsync` owner-scoped callback. Legacy `InterfaceFetchAccounts::Delete`/`DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceFetchAccounts.cpp:88-116`) invoke `Collection::DeleteItem`/`DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-231`) and return `S_OK` even for unknown IDs/indexes; `PersistentFetchAccount::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentFetchAccount.cpp:116-140`) removes the row and UID children. The .NET path keeps direct activation denied, removes the shared snapshot only after store success, maps failures to `E_FAIL`, and preserves the existing item-level Delete behavior. Focused FetchAccounts COM tests pass `16/16`; full Net10 passes `1517` with `4` opt-in skips. Live SQL/UID cleanup, rollback, and production COM activation remain unproven. Next slices: run IMAP and FetchAccounts SQL deletion evidence only after the approved disposable connection/isolated-create gate is supplied; see `CURRENT_STATE.md` for concise state.

The current bounded slice is complete in code/test commit `8e3bf68d8`: authenticated existing-row `IMAPFolder.Subscribed` staging through the owning `Save` path. Legacy reference is `InterfaceIMAPFolder::put_Subscribed` (`hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:144-159`), composed with `PersistentIMAPFolder::SaveObject`; current code is `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/IMAPFolders.cs`. Focused IMAPFolders/SQL tests pass `19/19`; full Net10 passes `1506` with `3` opt-in skips. Snapshot-only/direct activation denial and failed-update snapshot preservation remain covered. Live SQL, rollback, concurrency, protocol/cache notification, deletion, and ACL mutation remain open. Next independent slice: authenticated parent-scoped existing-folder deletion parity; see `CURRENT_STATE.md` for the concise state.

The current bounded slice is complete in code/test commit `3f64cd731`: authenticated existing-row `IMAPFolder.Name` staging and owning `Save` update parity. Legacy references are `InterfaceIMAPFolder::put_Name`/`Save` (`hmailserver/source/Server/COM/InterfaceIMAPFolder.cpp:47-67,92-124`) and `PersistentIMAPFolder::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:100-151`); current code is `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/IMAPFolders.cs` plus `SqlServerImapFolderAdministrationStore`. Focused IMAPFolders/SQL tests pass `18/18`; full Net10 passes `1505` with `3` opt-in skips. Snapshot-only facades and direct activation remain denied for mutation, and failed store updates preserve the shared snapshot. Live SQL, post-update rollback, concurrency, protocol/cache notification, `Subscribed`, delete, and ACL mutation remain open. Next independent slice: authenticated existing-row `IMAPFolder.Subscribed` setter through the owning `Save` path; see `CURRENT_STATE.md` for the concise state.

The current bounded slice is complete in code/test commit `e073b6ba7`: authenticated account-owned `IMAPFolders.Add` insert parity. Legacy references are `InterfaceIMAPFolders::Add` (`hmailserver/source/Server/COM/InterfaceIMAPFolders.cpp:165-209`) and `PersistentIMAPFolder::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentIMAPFolder.cpp:100-151`); current code is `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/IMAPFolders.cs` plus `SqlServerImapFolderAdministrationStore`. Focused COM/SQL tests pass `15/15`; full Net10 had `1500` passes, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips. Direct activation denial and owner-scope failure paths are covered. SQL integration, rollback, concurrency, protocol/cache notification, `IMAPFolder.Save`, setters, delete, and ACL mutation remain open. Next independent slice: authenticated existing-row `IMAPFolder.Name` setter plus owning `Save` update parity; see `CURRENT_STATE.md` for the concise state.

The current bounded slice is complete in code/test commit `e38372a80`: `BackupRestoreContainmentPreflight.RevalidateAsync` performs fresh archive/raw integrity inspection before rerunning containment checks and fails closed when message-file evidence changes. Legacy `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`) has no equivalent check. Focused restore integrity/preflight tests pass `100/100`; the full suite had `1498` passes, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips. The two new tests cover deleted raw content and a changed compressed message graph at the same archive path. This remains an unwired read-only contract: no atomicity with extraction, post-extraction reparse scan, SQL restore, target replacement, rollback, round-trip, or production release claim. Next independent slice: isolated disposable SQL restore transaction harness/wiring when the required integration connection and isolated-create opt-in are approved; see `CURRENT_STATE.md` for the concise state.

The current bounded slice is complete in code/test commit `77e6ad723`: read-only restore message/file correspondence validation for private-account messages. Legacy references are `hmailserver/source/Server/Common/BO/Message.cpp:200-232` and `hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:1120-1187`; current code is `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/BackupRestoreIntegrityRuntime.cs`. Focused restore integrity tests pass `89/89`; the full suite had `1496` passes, `2` unrelated scanner temporary-file cleanup failures, and `3` opt-in skips; excluding those scanner classes, `1491` tests passed with `3` skips. The check is read-only and does not provide execution-time revalidation, restore mutation, SQL, replacement, rollback, or round-trip evidence. Next independent slice: execution-time message/file correspondence revalidation contract. The SQL transaction harness and backup/restore round-trip remain blocked; see `CURRENT_STATE.md` for the concise state.
- Authoritative 2026-08-01 continuation: code/test commit `73d3435d6` follows `4f0787515` and adds an unreferenced internal restore execution gate. Legacy `BackupManager`/`BackupExecuter::StartRestore` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:75-98`, `BackupExecuter.cpp:230-388`) has no process-wide restore serialization; the .NET `BackupTaskQueue` only serializes backup tasks. `BackupRestoreExecutionGate` provides a single-owner async lease with timeout, cancellation, and idempotent release, without changing COM or service wiring. Focused gate coverage is `3/3`; full Net10 is `1466` with `3` opt-in skips. `StartRestore` remains `E_NOTIMPL`; gate wiring, SQL, extraction, data replacement, rollback, and round-trip equivalence remain blocked. Next slice: read-only restore semantic identity/foreign-key plan validation; keep COM activation and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `1c3a67f48` follows `a0daf37d1` and adds execution-time revalidation to the internal restore containment planner. Legacy `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`) has no revalidation or restore lock. `BackupRestoreContainmentPreflight.Revalidate` reruns the bounded containment checks against the original target and rollback paths and fails closed when the archive or raw source changes after the initial plan. Focused preflight coverage is `9/9`; full Net10 is `1463` with `3` opt-in skips. This remains unwired revalidation, not a concurrency guarantee: `StartRestore` remains `E_NOTIMPL`; SQL, extraction, data replacement, rollback, lock acquisition, and round-trip equivalence remain blocked. Next slice: internal restore execution gate/lock contract; keep COM activation and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `156baff66` follows `efa08aff4` and bounds the internal read-only restore containment scan. Legacy `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:338-388`) and `FileUtilities::CopyDirectory` (`hmailserver/source/Server/Common/Util/FileUtilities.cpp:369-402`) recurse synchronously without cancellation or an entry limit. `BackupRestoreContainmentPreflight` now stops at 100,000 scanned entries or a canceled token and returns a failed plan without mutation, retaining the previous archive/source/target/rollback and reparse checks. Focused restore/preflight coverage is `83/83`; full Net10 is `1462` with `3` opt-in skips. `StartRestore` remains `E_NOTIMPL`; TOCTOU revalidation/locking, SQL, extraction, data replacement, rollback, and round-trip equivalence remain blocked. Next slice: specify execution-time TOCTOU revalidation and locking; keep COM activation and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `6227cc267` follows `3f9d501a` and completes an internal read-only restore containment preflight. Legacy `BackupExecuter::StartRestore` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-335`) deletes domains before data restore, loads settings last, and reinitializes asynchronously; `RestoreDataDirectory_` (`BackupExecuter.cpp:338-388`) uses extracted `DataBackup` or a raw sibling without rollback/containment checks, and `FileUtilities::CopyDirectory`/cleanup (`hmailserver/source/Server/Common/Util/FileUtilities.cpp:369-519`) lacks reparse protection. `BackupRestoreContainmentPreflight` now requires real archive/target/rollback-parent paths, supports DB-only raw evidence without a physical source, rejects archive/source/target/rollback overlap, scans existing source/target trees for reparse points with fail-closed attribute handling, and marks compressed restores as requiring isolated extraction. Focused restore/preflight coverage is `82/82`; full Net10 is `1461` with `3` opt-in skips. The planner remains unwired, `StartRestore` remains `E_NOTIMPL`, and restore mutation, SQL, extraction, data replacement, rollback, and round-trip equivalence remain blocked. Next slice: bound/cancel containment traversal and specify execution-time TOCTOU revalidation/locking; keep COM activation and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `9f194323c` follows `882fbab65` and completes writer-side folder snapshot validation. Legacy `IMAPFolder::XMLStore` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:123-145`) serializes an existing in-memory hierarchy and `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits only empty collections; `XMLLoad`/`XMLLoadSubItems` (`IMAPFolder.cpp:150-179`) reconstructs it through persistence. `BackupArchiveRuntime.WriteFolders` now rejects duplicate IDs, orphaned parent IDs, and parent cycles before emitting XML, preserving valid ordering and empty-container omission. Focused archive coverage is `45/45`; full Net10 is `1454` with `3` opt-in skips. Next slice: isolated restore plan containment and rollback preflight; keep restore mutation, SQL writes, data-directory replacement, round-trip equivalence, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `4bc9bdc89` follows `1c2bf4a6b` and completes read-only folder scalar presence validation. Legacy `IMAPFolder::XMLStore`/`XMLLoad` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:123-179`) emits and loads `Name`, `Subscribed`, `CreateTime`, and `CurrentUID`; value/date parsing remains permissive. The validator now requires those four attributes for root and nested folders while preserving empty containers, order-independent loading, and non-mutating planning. Focused restore integrity/planner coverage is `75/75`; full Net10 is `1451` with `3` opt-in skips. Next slice: writer-side orphan/cycle/duplicate snapshot validation; keep containment/rollback preflight, restore mutation, SQL writes, data-directory replacement, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `149dcf027` follows `a512891ba` and completes read-only restore structural/dry-run missing-section planning. Legacy `BackupExecuter::StartRestore` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-333`) orders restore as domain deletion, public-folder deletion, optional data-directory restore, domain load, settings load, and asynchronous reinitialization. `BackupRestoreDryRunPlanner` preserves that order and now reports `MissingRestoreOptions` plus warnings when requested settings/domains/messages sections are absent from valid metadata; `WouldMutate` remains false. Focused restore integrity/planner coverage is `71/71`; full Net10 is `1447` with `3` opt-in skips. Next slice: folder scalar presence validation; keep writer orphan/cycle detection, containment/rollback preflight, restore mutation, SQL writes, data-directory replacement, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `5be27ae08` follows `87d7e6529` and completes read-only folder message/subfolder graph validation. Legacy `IMAPFolder::XMLStore`/`XMLLoadSubItems`, `Messages::GetCollectionName`, `Message::XMLStore`/`XMLLoad`, and `Collection<T,P>` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:124-179`, `Messages.h:45`, `Message.cpp:200-234`, `Collection.h:61-125`) define optional `Messages` and recursive `Folders` under each `Folder`; `Message` children carry nine serialized attributes and absent/empty collections remain valid. The validator now checks root/nested folder placement, duplicate containers, child names, and message attribute presence. Focused restore integrity/planner coverage is `70/70`; full Net10 is `1446` with `3` opt-in skips. Next slice: read-only restore structural/dry-run equivalence planning; keep folder scalar validation, restore mutation, SQL writes, data-directory replacement, rollback, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `fc4c5590d` follows `f32727669` and completes read-only nested rule child validation. Legacy `Rule::XMLStore`/`XMLLoadSubItems`, `RuleCriteria::XMLStore`/`XMLLoad`, `RuleAction::XMLStore`/`XMLLoad`, and collection names (`RuleCriterias.h:24`, `RuleActions.h:28`) (`hmailserver/source/Server/Common/BO/Rule.cpp:62-99`, `RuleCriteria.cpp:27-49`, `RuleAction.cpp:28-67`) define optional `RuleCriterias` and `RuleActions` under `Rule`, with `Criteria` and `Action` children; absent/empty collections remain valid. The validator now checks placement, duplicates, child names, and serialized attributes. Focused restore integrity/planner coverage is `62/62`; full Net10 is `1438` with `3` opt-in skips. Next slice: folder message/subfolder graph validation; keep restore mutation, SQL writes, data-directory replacement, rollback, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `40df08f32` follows `a031e777f` and completes read-only nested `FetchAccountUIDs` validation. Legacy `FetchAccount::XMLStore`/`XMLLoadSubItems`, `FetchAccountUID::XMLStore`/`XMLLoad`, `FetchAccountUIDs::GetCollectionName`, and `Collection<T,P>` (`hmailserver/source/Server/Common/BO/FetchAccount.cpp:55-121`, `FetchAccountUID.cpp:42-61`, `FetchAccountUIDs.h:25`, `Collection.h:61-125`) define optional `FetchAccountUIDs` under `FetchAccount`, with `UID` children carrying `UID` and `Date`; absent/empty collections remain valid. The validator now checks placement, duplicates, child names, and serialized attributes. Focused restore integrity/planner coverage is `54/54`; full Net10 is `1430` with `3` opt-in skips. Next slice: nested `RuleCriterias`/`RuleActions` graph validation; keep folder message metadata, restore mutation, SQL writes, data-directory replacement, rollback, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `0e6df4f65` follows `86a866380` and completes read-only direct Account child-container validation. Legacy `Account::XMLStore`/`XMLLoadSubItems` (`hmailserver/source/Server/Common/BO/Account.cpp:280-327,380-398`) and collection names (`FetchAccounts.h:26`, `Rules.h:27`, `IMAPFolders.cpp:351`) define optional `FetchAccounts`, `Rules`, and `Folders` directly under `Account`; nested folder containers remain recursive. `BackupRestoreIntegrityRuntime.ValidateDomainAccountGraph` now rejects misplaced/duplicate direct containers and wrong child names while preserving explicit empties and nested folders. Focused restore integrity/planner coverage is `49/49`; full Net10 is `1425` with `3` opt-in skips. Next slice: nested `FetchAccountUIDs` graph validation; keep rule criteria/actions, folder message metadata, restore mutation, SQL writes, data-directory replacement, rollback, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `8849a498d` follows `2f4448c36` and completes read-only Domain child-container validation. Legacy `Domain::XMLStore`/`XMLLoadSubItems`, `DomainAlias`, `Alias`, `DistributionList`, and `Collection<T,P>` behavior was confirmed in `hmailserver/source/Server/Common/BO/Domain.cpp:103-149`, `DomainAlias.cpp:26-33`, `Alias.cpp:28-37`, `DistributionList.cpp:32-45`, and `Collection.h:61-82`. `BackupRestoreIntegrityRuntime.ValidateDomainAccountGraph` now checks `DomainAliases`, `Aliases`, and `DistributionLists` placement, duplicate containers, expected child names, and writer-emitted scalar attributes; explicit empty containers remain valid and unknown non-target Domain children remain tolerated. Focused restore integrity/planner coverage is `40/40`; full Net10 is `1416` with `3` opt-in skips. Next slice: account child-container graph validation through legacy `Account::XMLLoadSubItems`; keep restore mutation, SQL writes, data-directory replacement, rollback, COM activation, and the backup ScriptServer adapter fenced.
- Authoritative 2026-08-01 continuation: code/test commit `dad4bf79a` follows `63b555fe8` and adds read-only `Backup/Domains/Domain/Accounts/Account` graph validation, grounded in legacy `Domain::XMLLoad`, `Account::XMLLoadSubItems`, and `Collection<T,P>::XMLLoad`. Focused coverage is `22/22`; full Net10 is `1398` with `3` opt-in skips. Named domains/accounts and expected placement are enforced; invalid evidence produces no dry-run restore steps. The next slice is domain child-container placement/scalar validation for `DomainAliases`, `Aliases`, and `DistributionLists`. The backup ScriptServer adapter remains open; keep SQL writes, data-directory replacement, restore execution, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `f7f90c84a` completes compressed non-DB-only message `DataBackup` staging for `BODomains|BOMessages|BOCompression`. Legacy `BackupExecuter::BackupDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:196-217`) recursively copies the configured data directory and removes only files directly under the staging root; `BackupExecuter::StartBackup` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:119-130,165-184`) archives metadata first, recursively adds `DataBackup`, and deletes the staging directory after compression. `FileUtilities::CopyDirectory`/`DeleteFilesInDirectory` and `Compression::AddDirectory` define the copied tree, root-file omission, and recursive 7z behavior. `SevenZipBackupArchiveRuntime` now receives the configured data directory, stages and cleans `DataBackup`, preserves nested message files, and rejects raw mode, missing source data, pre-existing staging, and source-nested staging. Focused backup/archive coverage is `49/49`; full Net10 is `1367` with `3` opt-in skips. The next slice is raw non-DB-only `BODomains|BOMessages` staging, leaving external `DataBackup` beside the archive; keep restore, destructive SQL, data-directory replacement, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `1e5e4a75b` completes DB-only message archive execution and legacy `DataFiles` metadata. Legacy `BackupExecuter::StartBackup` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:57-147,172-184`) reads `BackupMessagesDBOnly`, skips `DataBackup` staging and message-file compression in DB-only mode, but still appends `BackupInformation/DataFiles` for `BOMessages`; `BOCompression` selects `Format="7z"` plus `Size`, while the pre-archive `FileUtilities::FileSize` (`hmailserver/source/Server/Common/Util/FileUtilities.cpp:318-327`) returns `0` for the not-yet-created archive. Without compression the legacy attributes are `Format="Raw"` and `FolderName="DataBackup"`. `BackupStartPlan.BackupCompressionFlag` and `SevenZipBackupArchiveRuntime.WriteDataFiles` now preserve those attributes; `CreateAsync` allows only DB-only message archive execution and still rejects physical message staging before writing files. Focused backup/archive/start-plan coverage is `40/40`; full Net10 is `1365` with `3` opt-in skips. The next slice is compressed `DataBackup` staging for non-DB-only `BODomains|BOMessages|BOCompression`; keep raw staging, restore, destructive SQL, data-directory replacement, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `b7dbd7c3b` completes DB-only private-folder message metadata serialization. Legacy `Messages::Refresh` (`hmailserver/source/Server/Common/BO/Messages.cpp:143-209`) scopes folder reads by account and folder and orders by `messageuid ASC`; `Message::XMLStore` (`hmailserver/source/Server/Common/BO/Message.cpp:200-217`) emits `CreateTime`, `Filename`, `FromAddress`, `State`, `Size`, `NoOfRetries`, `Flags`, `ID`, and `UID` in that order; `PersistentMessage::ReadObject` (`hmailserver/source/Server/Common/Persistence/PersistentMessage.cpp:194-208`) supplies those database fields; and `IMAPFolder::XMLStore` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:124-145`) places `Messages` before recursive `SubFolders`. `BackupArchiveXmlPayload.FolderMessages`, `BackupXmlPayloadRuntime`, and `WriteMessages` now load selected-folder rows through `IMessageAdministrationStore`, emit escaped DB-only metadata, and omit empty `Messages` containers. Message content, `DataFiles`, data-directory copying, restore, ACLs, public folders, and the existing archive message-mode rejection remain fenced. Focused backup/message/folder/host coverage is `49/49`; full Net10 is `1362` with `3` opt-in skips. The next slice is DB-only message archive execution plus `DataFiles` metadata; keep physical data-directory staging, restore, destructive SQL, event dispatch, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `384e67788` completes backup-side private-account nested subfolder metadata. Legacy `IMAPFolders::Refresh` (`hmailserver/source/Server/Common/BO/IMAPFolders.cpp:42-145`) loads all account rows in `folderid ASC` order and rebuilds the `folderparentid` hierarchy; `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) emits direct items and omits empty collections; and `IMAPFolder::GetSubFolders`/`XMLStore` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:61-68,123-145`) emits scalar attributes before recursive nested `Folders` containers. The .NET path loads each selected account once through `GetFoldersForAccountAsync`, groups snapshots by `ParentId`, and uses `WriteFolder` for depth-first root/child/grandchild XML while preserving scalar order and XMLite-compatible escaping. Messages, data files, ACLs, public folders, and restore remain unchanged. Focused backup/folder/host coverage is `45/45`; full Net10 is `1361` with `3` opt-in skips. The next slice is DB-only folder message metadata serialization; keep data-directory copying, `DataFiles`, ACLs/public folders, restore, destructive SQL, event dispatch, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `db73812c7` completes backup-side private-account root folder metadata/scalar serialization. Legacy `Account::GetFolders`/`Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:131-140,280-331`) gates folder output on `BOMessages` and places `Folders` after `Rules`; `IMAPFolders::Refresh` (`hmailserver/source/Server/Common/BO/IMAPFolders.cpp:42-145`) preserves `folderid ASC` order and root scope; `IMAPFolder::XMLStore` (`hmailserver/source/Server/Common/BO/IMAPFolder.cpp:123-145`) emits `Name`, `Subscribed`, `CreateTime`, and `CurrentUID` in that order; `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits empty collections; and `Time::GetTimeStampFromDateTime` (`hmailserver/source/Server/Common/Util/Time.cpp:62-72`) defines the legacy timestamp shape. `BackupArchiveXmlPayload.Folders`, `BackupXmlPayloadRuntime`, and `SevenZipBackupArchiveRuntime.WriteFolders` now load selected account root folders through `IImapFolderAdministrationStore.GetRootFoldersAsync`, preserve store order, escape values compatibly, omit empty `Folders`, and gate emission on `BackupMessagesFlag`. Focused backup/folder/host coverage is `45/45`; full Net10 is `1361` with `3` opt-in skips. The archive still rejects message payload creation before writing, so this is metadata-only parity. The next slice is backup-side nested subfolder hierarchy metadata; keep messages/data-directory copying, ACLs, public folders, restore, destructive SQL, event dispatch, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `bd37be125` completes backup-side account `Rules` serialization, including read-only `RuleCriterias`/`Criteria` and `RuleActions`/`Action` children. Legacy `Account::XMLStore`, `Rules::Refresh`, `Collection<T,P>::XMLStore`, `Rule::XMLStore`, `RuleCriteria::XMLStore`, and `RuleAction::XMLStore` were confirmed first; the .NET path preserves `FetchAccounts -> Rules` placement, legacy attribute/container order, XMLite entity escaping, empty-container omission, selected account/rule scoping, and the legacy backup rule ordering without changing the COM/admin query or behavior. Focused backup/rule/host coverage is `115/115`; full Net10 is `1358` with `3` opt-in skips. The next slice is backup-side folder metadata/scalar serialization. Keep nested messages, data-directory copying, subfolders, ACLs, restore, destructive SQL, event dispatch, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-08-01 continuation: code/test commit `afd0de0da` completes backup-side encrypted fetch `Password` plus `FetchAccountUIDs`/`UID` serialization. Focused backup/fetch/COM/security coverage is `59/59`; full Net10 is `1353` with `3` opt-in skips. The next slice is backup-side `Rules` child serialization. Keep rule mutation/execution, Folders, message/data-directory payloads, restore, destructive SQL, event dispatch, COM registration, and SEC-18/PHP session work fenced.
- Authoritative 2026-07-30 continuation: code/test commit `ae97a70eb` completes backup-side non-secret `FetchAccounts` scalar child serialization. Legacy references are `Account::XMLStore`, `FetchAccounts::Refresh`, `FetchAccount::XMLStore`, and `Collection<T,P>::XMLStore`; focused backup/fetch/COM coverage passes `49/49`, and full Net10 passes `1349` with `3` opt-in skips. The next slice is encrypted fetch `Password` plus `FetchAccountUIDs`/`UID` serialization. Keep Rules, Folders, message/data-directory payloads, restore, destructive SQL, event dispatch, COM registration, and SEC-18/PHP session work fenced.
- Validation: `test-sec18-denial-evidence-attestation.ps1`, `test-sec18-installed-application-graph-evidence.ps1`, and `test-webadmin-broker-staging-inventory.ps1` remain the SEC-18 evidence commands; full `build/test-net10.ps1 -Configuration Debug` passes `1346` with `3` opt-in tests skipped. The completed bounded `BackupManager` archive/XML, non-secret raw settings-property, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, backup-side normal domain `Aliases`, backup-side `DistributionLists` child serialization, and backup-side account `Password`/`PasswordEncryption` slices pass focused backup/account credential coverage `44/44`; ordinary account administration projections remain secret-free and no PHP CLI is installed. Rollback exit code is `0`; temporary registry keys, service/process, endpoints, client helper, and probe paths are absent; hMailServer remains stopped/disabled.
Current authoritative continuation (2026-08-01): code/test commit `c8b1dc9e1` follows `b91552323` and adds the unreferenced `BackupRestoreTransactionBoundary`. It preserves mutation failures, performs rollback on failed mutation/commit/cancellation, and reports rollback failure without enabling SQL or restore mutation. Focused transaction-boundary tests pass `5/5`; full Net10 passes `1481` with `3` opt-in tests skipped. Next slice: isolated disposable SQL transaction harness/wiring. No COM identity, service, SQL, data-directory, IIS, DCOM, registry, firewall, or production state changed. Existing SEC-18 artifacts and dirty AGENTS.md remain protected and unstaged.

Bu dosya yeni bir Codex thread'inin hMailServer .NET 10 rewrite calismasina kaldigi yerden devam edebilmesi icin hazirlandi.

## Projenin Amaci

hMailServer icin Windows uyumlu, side-by-side .NET 10 tabanli yeni server cekirdegi gelistiriliyor. Hedef; legacy C++/ATL hMailServer davranisi, mevcut SQL Server verisi, data directory duzeni, COM/API sozlesmeleri, Administrator uyumlulugu ve VBScript/JScript event davranislari korunarak modern protokol, arama, teslimat, spam/virus ve operasyon altyapisina gecmek.

Legacy C++ server production referansi olmaya devam ediyor. .NET 10 agaci production parity saglanana kadar kontrollu test/uyumluluk hattidir.

## Mevcut Tamamlanan Buyuk Isler

- .NET 10 solution skeleton, servis hostu, local build/test wrapper'lari ve on kosul kontrol scriptleri eklendi.
- Phase 0 legacy C++ stabilizasyonlari tamamlandi: ClamAV INSTREAM raw network-order chunk framing, synchronous timeout cancellation, SpamAssassin partial/invalid response korunumu, MSBuild 17 discovery.
- SQL Server Full-Text Search icin additive migration, search document/queue tablolari, backfill processor, IMAP SEARCH/SORT planner ve SQL-backed metadata arama katmani eklendi.
- IMAP tarafinda LOGIN/AUTHENTICATE PLAIN, SELECT/EXAMINE, nested/public folder, ACL, QUOTA, SEARCH/SORT, FETCH, STORE, COPY/MOVE, APPEND, EXPUNGE, IDLE ve recent flag lifecycle icin buyuk parity dilimleri tamamlandi.
- SMTP tarafinda listener/session skeleton, STARTTLS, AUTH PLAIN/LOGIN, MAIL/RCPT/DATA staging, local/route recipient validation, durable queue persistence, global/account rule islemleri, delivery queue lease/load/dispatch, local delivery, remote SMTP sender, retry/backoff, bounce ve delivery status gozlemlenebilirligi eklendi.
- POP3 tarafinda USER/PASS, CAPA, STAT/LIST/UIDL/RETR/TOP/DELE/RSET/NOOP/QUIT, mailbox lock, implicit TLS ve SQL/data-directory mailbox store eklendi.
- External POP3 fetch icin SQL lease/UID store, POP3 network session, CAPA/STLS probing, UIDL/RETR/DELE/QUIT akis, recipient resolution, yeni/bilinen UIDL ve duplicate sequence baskilama, persisted known-UID duplicate toleransi, legacy `X-hMailServer-ExternalAccount` basligi, spam/AV entegrasyonu ve `OnExternalAccountDownload` script hook'u eklendi; fetch-account script facade'i `NextDownloadTime`/`IsLocked` alanlarini da tasiyor.
- Modern security slice'lari eklendi: ClamAV, SpamAssassin, spam policy, attachment blocking, DNSBL, reverse DNS/PTR, sender-domain MX, greylisting, SURBL, failed-logon auto-ban ve davranis degistirmeyen/disabled-by-default SPF evaluator + SMTP policy temeli.
- Legacy script/event parity buyuk olcude ilerledi: `OnClientConnect`, `OnClientValidatePassword`, `OnClientLogon`, `OnHELO`, `OnRecipientUnknown`, `OnSMTPData`, `OnAcceptMessage`, `OnTooManyInvalidCommands`, delivery eventleri, `OnDeliveryFailed`, `OnError`, rule `ScriptFunction`, mesaj/recipient/attachment facade'leri, client `Authenticated`/`EncryptedConnection` alias'lari ve account-rule `Message.Copy(folderId)`.
- Iki guvenlik raporu 21 benzersiz kayitta birlestirildi. `4dd984156` ile bos administrator hash'i fail-closed yapildi; legacy JScript event literal'lari, rule `ScriptFunction` runtime/COM yetki siniri, SMTP `ETRN`, WebAdmin session fixation ve CSRF rastgeleligi sertlestirildi. ClamAV framing duzeltmesinin daha once `d8942bc12` ile kapandigi dogrulandi; yeniden uretilemeyen iki VBScript iddiasi regresyon testleriyle izleniyor.

## Production-Ready Seviyesi

Durum: production-ready degil. Proje ciddi bir parity seviyesine geldi, fakat halen side-by-side rewrite/test hatti olarak ele alinmali.

Ana nedenler:

- COM/Admin yuzeyi ve legacy object model henuz tam degil.
- SPF evaluator, disabled-by-default SMTP policy boundary, explicit SPF-pass greylisting bypass boundary, DKIM parser/canonicalization/body-hash/header-crypto/DNS lookup/message-level verification + disabled-by-default policy boundary, DKIM pass-domain result surface, disabled-by-default DMARC evaluation/SMTP policy boundary, offline local-PSL organizational-domain resolver ve pinned/paketlenmis PSL lifecycle var; DKIM signing/setter/Admin mutation wiring, DMARC enforcement/Admin policy wiring ve daha sonra SPF/greylisting Administrator/COM setting parity eksik.
- Backup engine ve `OnBackupCompleted` / `OnBackupFailed` eventleri beklemede.
- Full in-place upgrade runner, backup creation/restore, and mandatory backup/rollback akis dokumani tamamlanmadi; replacement install now has a read-only rollback archive integrity and legacy `DataBackup` payload preflight before COM or service mutation.
- Buyuk olcekli performance/soak kabul testleri henuz production gate olarak tamamlanmadi.

## Kalan Kritik Backlog

- Full legacy script object model parity.
- Backup engine tasarimi ve backup completed/failed eventlerinin gercek engine uzerinden baglanmasi.
- Active Directory auth, master user ve daha derin account facade collections/methods.
- DKIM signing/setter/Admin mutation wiring ve DMARC enforcement/Admin policy wiring; daha sonra SPF/greylisting Administrator/COM setting parity.
- COM/API compatibility: mevcut GUID/ProgID/DISPID/type library sozlesmelerinin tam korunmasi ve Administrator-visible nesnelerin tamamlanmasi.
- Migration/operations: full in-place upgrade runner, backup creation/restore, semantic restore validation, rollback-from-backup dokumani, orphan cleanup, health/metrics/logging, and broader Windows Service install/uninstall integration; replacement install's read-only rollback archive and `DataBackup` payload preflight is landed.
- SQL Server FTS integration testleri ve production acceptance: 100k mailbox SEARCH/SORT, 1k IMAP connection, SMTP queue latency, memory/handle leak soak.
- External fetch edge-case parity.
- Acik P1 guvenlik maddeleri: WebAdmin'in kalan mutation/delete POST/token gecisi, plaintext PHP session parolasi, legacy COM mutation ownership denetimi, external-fetch egress/SSRF rollout'u ve custom antivirus komutunun structured executable/arguments modeline gecisi. AV/SpamAssassin AJAX test action'lari, external-account add/edit, ve onceki deletion/download-now action'lari POST-only/CSRF-korumali hale getirildi; .NET `FetchAccount`/`FetchAccounts` direct activation boundary'si tum access path'lerde `E_ACCESSDENIED` ile testle kapatildi ve legacy C++ registered `FetchAccount` constructor ownership riski `62f40dc77` ile kapatildi. External-fetch failure scheduling parity, WebAdmin credential-authority retargeting, .NET resolve-once/audit-only egress boundary ve POP3 operation timeout/line budget kapandi; enforcement rollout'u, adaptive high-load timeout behavior ve DNS/TLS live integration evidence acik kaldi.

- `RuleAction.Subject` existing-row setter parity dilimi (`fd6d58f3a`), legacy `InterfaceRuleAction::put_Subject` detached-object `E_ACCESSDENIED` boundary'sini current authorized RuleActions facade'na tasiyor. Raw BSTR validation/normalization ve admin guard olmadan owning item snapshot'ini stage ediyor ve existing-row Save ile mevcut parameterized `actionsubject` update yoluna yaziyor; direct activation, read-only/test-only `E_NOTIMPL`, Add/new-item Save, RuleID/remaining setters, ordering, rule execution, SMTP/delivery behavior ve broader mutation degismedi. Dar RuleActions/Rules/store filtresi 28/28; full Net10 1150/1152, iki alakasiz ClamWin/CustomScanner generated `.eml` cleanup `UnauthorizedAccessException` ile kaldi; live SQL integration bu turda kurulmadi.

- `RuleAction.Body` existing-row setter parity dilimi (`fee2fec0a`), legacy `InterfaceRuleAction::put_Body` detached-object `E_ACCESSDENIED` boundary'sini current authorized RuleActions facade'na tasiyor. Raw BSTR validation/normalization ve admin guard olmadan owning item snapshot'ini stage ediyor ve existing-row Save ile mevcut parameterized `actionbody` update yoluna yaziyor; direct activation, read-only/test-only `E_NOTIMPL`, Add/new-item Save, RuleID/remaining setters, ordering, rule execution, SMTP/delivery behavior ve broader mutation degismedi. Dar RuleActions/Rules/store filtresi 30/30; full Net10 1152/1154, iki alakasiz ClamWin/CustomScanner generated `.eml` cleanup `UnauthorizedAccessException` ile kaldi; live SQL integration bu turda kurulmadi.

- `RuleAction.FromName` existing-row setter parity dilimi (`e7ee6b669`), legacy `InterfaceRuleAction::put_FromName` detached-object `E_ACCESSDENIED` boundary'sini current authorized RuleActions facade'na tasiyor. Raw BSTR validation/normalization ve admin guard olmadan owning item snapshot'ini stage ediyor ve existing-row Save ile mevcut parameterized `actionfromname` update yoluna yaziyor; direct activation, read-only/test-only `E_NOTIMPL`, Add/new-item Save, RuleID/remaining setters, ordering, rule execution, SMTP/delivery behavior ve broader mutation degismedi. Dar RuleActions/Rules/store filtresi 32/32; full Net10 1156/1156; live SQL integration bu turda kurulmadi.
- SEC-11 `RuleAction.FromAddress` existing-row setter parity (`4ed42c2cf`): legacy `InterfaceRuleAction::put_FromAddress` only denies detached objects, otherwise stages the exact raw BSTR without an administrator guard or normalization; the authorized .NET facade now uses the owning `Mutate` path and existing parameterized `actionfromaddress` save. Detached activation remains `E_ACCESSDENIED`, direct COM identity and DISPID 7 remain unchanged, and Add/new-item Save, other setters, SMTP behavior, and broader mutation remain out of scope. Focused RuleActions/Rules/store tests pass 27/27; full Net10 passes 1156/1156 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes.

- SEC-11 `RuleAction.Filename` existing-row setter parity (`fe9763b6f`): legacy `InterfaceRuleAction::put_Filename` (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:351-365`) stores the raw BSTR on an attached object and returns `S_OK`; `PersistentRuleAction` reads/writes `actionfilename` (`hmailserver/source/Server/Common/Persistence/PersistentRuleAction.cpp:31-109`). The legacy `RuleApplier::ApplyAction_Reply` path (`hmailserver/source/Server/SMTP/RuleApplier.cpp:429-482`) does not consume the field for file I/O, path resolution, or network access. The authorized .NET facade now stages the exact value through the owning `Mutate`/Save path; detached activation remains `E_ACCESSDENIED`, no-save-delegate/test-only mutation remains `E_NOTIMPL`, and installed IID/vtable/DISPID 8 are unchanged. Focused RuleActions/Rules/store tests pass 29/29; full Net10 passes 1158/1159 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes.

- SEC-11 `RuleAction.To` existing-row setter parity (`e628dc487`): legacy `InterfaceRuleAction::put_To` (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:367-397`) stores the raw BSTR on an attached object and returns `S_OK`; `RuleAction::GetTo/SetTo` are raw accessors and `PersistentRuleAction` reads/writes `actionto` (`hmailserver/source/Server/Common/Persistence/PersistentRuleAction.cpp:53,102`). The legacy `RuleApplier::ApplyAction_Forward` path (`hmailserver/source/Server/SMTP/RuleApplier.cpp:243-307`) passes the field to `RecipientParser::CreateMessageRecipientList`; the .NET runtime already has the equivalent `action.To` forwarding path, so this slice changes no delivery or egress behavior. The authorized .NET facade now stages the exact value through the owning `Mutate`/Save path; detached activation remains `E_ACCESSDENIED`, no-save-delegate/test-only mutation remains `E_NOTIMPL`, and installed DISPID 9 is unchanged. Focused RuleActions/Rules/store tests pass 31/31; full Net10 passes 1160/1161 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes.

- SEC-11 `RuleAction.IMAPFolder` existing-row setter parity (`0ec2ff11e`): legacy `InterfaceRuleAction::put_IMAPFolder` encodes the incoming Unicode BSTR to Modified UTF-7 and returns `S_OK` without lookup, ACL, creation, or validation; the getter decodes the stored representation. The authorized .NET facade preserves the encode/decode boundary and existing owning Save path; detached activation remains `E_ACCESSDENIED`, read-only/no-save facades remain `E_NOTIMPL`, and folder runtime resolution/movement, SMTP trust, and live reconfiguration remain unchanged. Focused RuleActions/Rules/store tests pass 49/49; full Net10 passes 1163/1164 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes.

- SEC-11 `RuleAction.HeaderName` existing-row setter parity (`0f0f576b6`): legacy `InterfaceRuleAction::put_HeaderName` directly stages the raw BSTR and returns `S_OK` without administrator or validation checks; `PersistentRuleAction` persists `actionheader`. The authorized .NET facade stages the exact raw value through the existing owning Save path; detached activation remains `E_ACCESSDENIED`, read-only/no-save facades remain `E_NOTIMPL`, installed DISPID 15 is unchanged, and the existing SMTP `SetHeaderValue` runtime path is untouched. Focused RuleActions/Rules/store tests pass 51/51; full Net10 passes 1165/1166 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes.

- SEC-11 `RuleAction.Value` existing-row setter parity (`2ce09ccdb`): legacy `InterfaceRuleAction::put_Value` directly stages the raw BSTR and returns `S_OK` without administrator or validation checks; `PersistentRuleAction` persists `actionvalue`. The authorized .NET facade stages the exact raw value through the existing owning Save path; detached activation remains `E_ACCESSDENIED`, read-only/no-save facades remain `E_NOTIMPL`, installed DISPID 16 is unchanged, and the existing SMTP `SetHeaderValue`/`BindToAddress` runtime paths are untouched. Focused RuleActions/Rules/store tests pass 53/53; full Net10 passes 1167/1168 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes.

- SEC-11 `RuleAction.RouteID` existing-row setter parity (`c7e2c4080`): legacy `InterfaceRuleAction::put_RouteID` directly stages the raw LONG and returns `S_OK` without route-existence, range, ownership, normalization, or field-specific administrator checks; `PersistentRuleAction` persists `actionrouteid` unchanged. The authorized .NET facade stages the raw integer through the existing owning Save path; detached activation remains `E_ACCESSDENIED`, read-only/no-save facades remain `E_NOTIMPL`, installed DISPID 18 is unchanged, and existing route selection/delivery runtime paths are untouched. Focused COM/store/SMTP tests pass 55/55; full Net10 passes 1169/1170 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes. Legacy route-resolution and delivery differences remain a separate runtime-risk gap.

- SEC-11 `RuleAction.AbortSpamFlagged` existing-row setter parity (`e14040d63`): legacy `InterfaceRuleAction::put_AbortSpamFlagged` stores `newVal == VARIANT_TRUE` and returns `S_OK` for attached objects without an action-specific administrator or validation guard; `PersistentRuleAction` persists `actionabortspamflagged` on insert/update. The authorized .NET facade stages the exact bool through the existing owning Save path; detached activation remains `E_ACCESSDENIED`, read-only/no-save facades remain `E_NOTIMPL`, installed DISPID 19 is unchanged, and the existing SMTP rule model/load path is untouched. Focused COM/store/SMTP tests pass 58/58; full Net10 passes 1172/1173 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes. Legacy Forward/Reply spam-abort consumption remains a separate runtime parity gap.

- SEC-11 account-rule `AbortSpamFlagged` Forward/Reply runtime parity (`3909d2441`): legacy `RuleApplier::ApplyAction_Forward`/`ApplyAction_Reply` reads the original message spam flag after the existing loop/recipient prechecks and silently skips only the flagged action. The .NET account-rule path now carries persisted `hm_messages.messageflags` state through `SmtpReceiveRequest`, applies the same action-local checks, and leaves later actions running. Focused processor/local-delivery/receiver tests pass 51/51; full Net10 passes 1180/1181 with one opt-in native registry test skipped. The docs remain unstaged because the protected worktree contains prior dirty documentation changes. SPF/DKIM/DMARC classification coverage remained a separate gap.

- SEC-11 inbound SMTP spam-state ordering parity: legacy `SMTPConnection::DoPreAcceptSpamProtection_`/`DoPreAcceptMessageModifications_` establish the message spam flag before `SMTPDeliverer::RunGlobalRules_`; the .NET receiver now runs its existing spam scan/policy before global rule processing and carries the resulting flag and message bytes through `SmtpReceiveRequest`. Later DKIM/DMARC, antivirus, trust, and delivery stages remain unchanged. Focused receiver/rule tests pass 52/52; full Net10 passes 1181/1182 with one opt-in native registry test skipped. SPF/DKIM/DMARC classification coverage remained a separate gap.

- SEC-11 generated-message flag parity for global `Forward`/`Reply`: legacy `PersistentMessage::CopyToQueue`/`RuleApplier::ApplyAction_Forward` copies source flags, while `RuleApplier::ApplyAction_Reply` constructs a fresh message and receives only the new-message `Recent` flag. The .NET `SmtpRuleGeneratedMessage.SpamFlagged` field carries Forward state through both generated queue writers; Reply remains clean. Focused processor/receiver tests pass 53/53; full Net10 passes 1182/1183 with one opt-in native registry test skipped. SPF/DKIM/DMARC classification ordering remains a separate gap.

- SEC-11 `CreateCopy` generated-message spam-flag parity: legacy `RuleApplier::ApplyAction_Copy` calls `PersistentMessage::CopyToQueue`, whose `CreateCopy_` copies source flags before the queued message is saved. The .NET CreateCopy path now carries `SmtpReceiveRequest.OriginalMessageSpamFlagged` into `SmtpRuleGeneratedMessage.SpamFlagged`. Focused processor/receiver tests pass 53/53; full Net10 passes 1182/1183 with one opt-in native registry test skipped. SPF/DKIM/DMARC classification ordering and coverage remain separate gaps.
- SEC-11 SPF/DKIM/DMARC pre-rule classification ordering: legacy SPF runs in `SMTPConnection::DoSpamProtection_(SPPreTransmission)`, DKIM runs through the post-transmission `SpamTestRunner`, and `DoPreAcceptMessageModifications_` establishes the spam flag before `SMTPDeliverer::RunGlobalRules_`. The .NET receiver now evaluates the existing SPF/DKIM/DMARC policies before global rules and carries the combined spam state through `SmtpReceiveRequest` and the primary queue writer without adding default rejection. Focused receiver tests pass 54/54; full Net10 passes 1183/1184 with one opt-in native registry test skipped. The next security gate is a shared WebAdmin scanner-target egress policy.
- SEC-15/SEC-16 WebAdmin scanner-test target hardening (`f586332cf`, following `7e98a4178`): legacy `Utilities::IsLocalHost` and the first-address `VirusScannerTester::TestClamAVConnect`/`SpamAssassinTestConnect::TestConnect` behavior remain the reference. Both AJAX paths now read target inputs only from POST, pass the once-resolved local IPv4 literal to COM, validate a strict decimal `1..65535` port, and reject invalid input through one HTTP 400/body `0` path before COM. Valid local scanner deployments, direct COM identity, scanner settings, SMTP scanning, and external fetch remain unchanged. PHP lint/runtime port checks pass; focused .NET contract tests pass 38/38 and full Net10 passes 1183/1184 with one opt-in native registry test skipped.
- Backup migration gate preflight (`8681b1d23`): legacy `InterfaceBackupManager::StartBackup`, `BackupManager::StartBackup`, `BackupTask::DoWork`, `BackupExecuter::LoadSettings_`, and `BackupExecuter::StartBackup` were traced before editing. The internal `BackupStartPlan` now preserves one-trailing-backslash normalization, message-placement-before-destination failure order, the `BackupMessagesDBOnly` message precondition, and the domain/message data-copy decision without touching filesystem, SQL, data directory, archive, queue, or event state. `IInterfaceBackupManager` identity and COM `StartBackup` remain unchanged/pending. Focused backup/settings/store tests pass 24/24; full Net10 passes 1187/1188 with one opt-in native registry test skipped.
- Backup operation-state and dispatch boundary (`cf15929a0`): `BackupOperationCoordinator` mirrors legacy `is_running_`, unavailable-maintenance-queue reset, and `OnThreadStopped` release while `BackupManager.StartBackup` invokes it only after the existing authenticated boundary. The runtime host is internal and opt-in; no archive, SQL, data-directory, status, or event execution was added, and `IInterfaceBackupManager` identity remains unchanged. Focused backup/settings/store tests pass 36/36; full Net10 passes 1190/1191 with one opt-in native registry test skipped.
- Service-owned backup task queue and callbacks (`832b9c933`): legacy `BackupManager::StartBackup`/`BackupTask::DoWork` remain the references for asynchronous `BackupTask(true)` dispatch and thread-stop release. `HMailServer.Service` now registers a single-reader `BackupTaskQueue` and `BackupTaskHostedService`; the authenticated manager publishes one request carrying start/loading/failure/completion/thread-stop callbacks. The queued execution delegate remains an explicit `E_NOTIMPL` boundary with no archive, SQL, data-directory, restore, or event work, and `IInterfaceBackupManager` identity/direct activation boundaries remain unchanged. Focused backup manager/task-queue tests pass 15/15; the Windows service build passes with 0 warnings/0 errors; full Net10 passes 1193/1194 with one opt-in native registry test skipped.
- Backup queued read-only start preflight (`d4360fd3e`): legacy `BackupExecuter::LoadSettings_`, `BackupExecuter::StartBackup`, `PersistentMessage::GetAllMessageFilesAreInDataFolder`, and `BackupManager::OnBackupFailed` were traced. The queued task now loads the current settings snapshot, honors `Settings:BackupMessagesDBOnly`, checks the parameterized read-only `hm_messages` filename predicate before normalized destination existence, and routes the first plan failure through the existing failure/thread-stop callbacks. `IInterfaceBackupManager` identity and direct activation remain unchanged; archive/XML creation, SQL/data-directory writes, restore, and event execution remain out of scope. Focused backup/preflight/store tests pass 24/24; full Net10 passes 1198/1199 with one opt-in native registry test skipped. Archive/XML creation remains deferred behind the current WebAdmin security gate.

## Current Next Slice

Authoritative continuation (2026-08-01, code/test commit `ce80fad48`): authenticated `RuleActions.Add()` plus new-item `RuleAction.Save()` INSERT parity is complete. Legacy references are `InterfaceRuleActions::Add` (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:93-119`), `InterfaceRuleAction::Save` (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:30-72`), `PersistentRuleAction::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleAction.cpp:65-116`), and `hm_rule_actions` schema (`hmailserver/source/DBScripts/CreateTablesMSSQL.sql:501-523`). The implementation uses owner-scoped typed INSERT with `OUTPUT INSERTED.actionid`, stages ID-zero children, assigns legacy max-plus-one sort order, rechecks ScriptFunction administrator authorization at Save, retains failed items for retry, and appends to the owner snapshot only after identity success. Installed COM identity/vtable/DISPID shape and direct activation denial are unchanged. Focused RuleActions/store tests pass `56/56`; full Net10 passes `1550` with `4` opt-in skips. Release evidence remains RED pending disposable SQL identity/readback, real COM activation, retained-facade reauthentication, concurrent sort allocation, SMTP execution, rollback, and delivery evidence. Next three independent slices: disposable SQL RuleActions/ACL/deletion readback; authenticated IMAP folder deletion COM/store wiring; isolated FetchAccounts SQL/UID cleanup evidence. Older Current Next Slice paragraphs below are historical.

Authoritative continuation (2026-08-01, `afd0de0da`): backup-side encrypted fetch `Password` plus `FetchAccountUIDs`/`UID` serialization is complete; focused coverage is `59/59` and full Net10 is `1353` with `3` opt-in skips. The next bounded slice is backup-side `Rules` child serialization. Keep rule mutation/execution, Folders, message/data-directory payloads, restore, destructive SQL, event dispatch, and SEC-18 work fenced. The older paragraphs below are superseded context.

Authoritative continuation (2026-07-30, `ae97a70eb`): backup-side non-secret `FetchAccounts` scalar child serialization is complete; focused backup/fetch/COM coverage is `49/49` and full Net10 is `1349` with `3` opt-in skips. The next bounded slice is encrypted fetch `Password` plus `FetchAccountUIDs`/`UID` serialization. Keep Rules, Folders, message/data-directory payloads, restore, destructive SQL, event dispatch, and SEC-18 work fenced. The older paragraphs below are superseded context.

Authoritative latest update (2026-07-30): authenticated `BackupManager` archive/XML, non-secret raw settings-property parity, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, backup-side normal domain `Aliases`, backup-side `DistributionLists` child serialization, and backup-side account `Password`/`PasswordEncryption` serialization are complete in `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, `3e7535d76`, `5d4981240`, and `fd30ceb33`; focused backup/account credential coverage passes `44/44` and full Net10 passes `1346` with `3` opt-in skips. Ordinary account administration projections remain secret-free. The next production-gate slice is backup-side `FetchAccounts` child serialization; nested account Rules/Folders, message/data-directory payloads, restore execution, destructive SQL, event dispatch, SEC-18 broker registration, and PHP session cutover remain fenced. Older current-next paragraphs below are historical context.

Authoritative update (2026-07-29): `RuleCriteria.HeaderField`, `RuleCriteria.MatchValue`, `RuleCriteria.UsePredefined`, `RuleCriteria.PredefinedField`, `RuleCriteria.MatchType`, and `RuleCriteria.RuleID` setter/save parity are complete in `c8d69c9b8`, `d95ce9c69`, `a4ff728c0`, `fabc7e03a`, `0d9e43b14`, and `66e72f39c`; the owner-scoped RuleCriteria save contract is complete in `edf97aeaa`; authenticated existing-row `RuleAction.RuleID` ownership/save parity is complete in `9680640a5`; RuleAction parent-snapshot visibility within an owning collection is complete in `dc2fe2118`; per-Rules-generation repeated `Rule.Actions` adapter visibility is complete in `493848279`; per-account repeated `Account.Rules` adapter visibility is complete in `bb4142b99`; authenticated per-account repeated-`Account.Messages` adapter visibility and SQL projection parity are complete in `0c2ee1226` and `debc93dac`. Focused Messages/Accounts/Application/SQL coverage passes `48/48`; full Net10 passes `1308` with `3` opt-in skips; PHP CLI remains unavailable. The next production-gate slice is authenticated per-account `Account.IMAPFolders` cached snapshot and shared folder-adapter visibility; keep folder mutation, ACL changes, live protocol/cache synchronization, SMTP/POP3 behavior, SEC-18 broker registration, and PHP session cutover fenced. The older paragraphs below are superseded context.

IncomingRelays WebAdmin hardening is complete in code/test commit `fc2aa90f6`, blocked-attachment WebAdmin hardening is complete in `bfee58cab`, route-address hardening is complete in `2394e026`, distribution-list recipient hardening is complete in `9d6a8dda2`, alias hardening is complete in `1dc35f169`, rule hardening is complete in `6736e161e`, domain hardening is complete in `3d25cb0a7`, and authenticated existing-row `RuleCriteria.HeaderField` setter parity is complete in `c8d69c9b8`. Legacy IncomingRelays, recipient, alias, rule, domain, and criteria access, staged Add/Save behavior, installed COM identities, and their persistence boundaries remain documented above. `RuleCriteria.HeaderField` stages raw input through the owning Save delegate while detached activation remains denied; focused criteria/SQL/integration coverage passes `31/31`; full Net10 passes `1284` with `3` opt-in skips; PHP CLI remains unavailable. The next COM/Admin mutation slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the owning `RuleCriteria.Save()` path. Keep broader criteria mutation, background hardening, backup archive/XML execution, SEC-18 broker registration, SMTP rule behavior, and PHP session cutover out of scope.

## Historical Next Slice Record (superseded)

The current smallest safe production-gate slice is a bounded SEC-14 audit of `hmailserver/source/WebAdmin/background_dnsblacklist_save.php` for POST-only handling, POST-body field reads, and preservation of the authenticated `Settings -> AntiSpam -> DNSBlackLists -> DNSBlackList` field mapping. SURBL-server mutation hardening is complete in `d8e785231`: `background_surblserver_save.php` now requires POST plus POST-body CSRF before reading `action`, `id`, `Active`, `DNSHost`, `RejectMessage`, and `Score`, while preserving the server-admin guard, `Settings -> AntiSpam -> SURBLServers` Add/Edit/DeleteByDBID, property assignments, Save, redirects, and installed COM identity. Focused SURBL WebAdmin/COM/store coverage passes `9/9`; full Net10 passes `1268` with `3` opt-in skips. PHP CLI is unavailable. Keep DNSBL mutation, SMTP/SURBL runtime behavior, live reconfiguration, COM mutation implementation, broader background handler hardening, backup archive/XML execution, and SEC-18 broker registration out of scope.

### Historical Prior Slice Records

The current smallest safe production-gate slice is a bounded SEC-14 audit of `hmailserver/source/WebAdmin/background_surblserver_save.php` for POST-only handling, POST-body field reads, and preservation of the authenticated `Settings -> AntiSpam -> SURBLServers -> SURBLServer` field mapping. SSL certificate mutation hardening is complete in `4ed9d2f26`: `background_sslcertificate_save.php` now requires POST plus POST-body CSRF before reading `action`, `id`, `Name`, `CertificateFile`, and `PrivateKeyFile`, while preserving the server-admin guard, `Settings -> SSLCertificates` Add/Edit/DeleteByDBID, field assignments, Save, redirects, and installed COM identity. Focused SSL WebAdmin/COM/store coverage passes `18/18`; full Net10 passes `1267` with `3` opt-in skips. PHP CLI is unavailable. Keep TLS runtime listener reconfiguration, certificate-file policy, broader background handler hardening, SMTP trust, backup archive/XML execution, and SEC-18 broker registration out of scope.

The current smallest safe production-gate slice is a bounded SEC-14 audit of `hmailserver/source/WebAdmin/background_sslcertificate_save.php` for POST-only handling, POST-body field reads, and preservation of the authenticated `Settings -> SSLCertificates -> SSLCertificate` field mapping. TCPIP port mutation hardening is complete in `272d56b5c`; full Net10 is `1266` passed with `3` opt-in skips. `background_iphome_save.php` is fenced as an orphaned pre-5.0 handler: `hm_iphomes` was dropped during the 5.0 upgrade and no current C++/IDL/.NET/form surface exists. SEC-18 remains RED at the freshness gate: the permanent broker, DCOM ACL writes, `hMailServer.Application` activation, PHP session cutover, and existing Application identity remain blocked. Backup archive/XML creation, service mutation execution, SMTP runtime mutation, and TLS runtime reconfiguration remain deferred until the higher-priority WebAdmin security gate clears or is explicitly re-prioritized.

## SEC-14 WebAdmin SSL Certificate Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-128`), `InterfaceSettings::get_SSLCertificates` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1212-1237`), `InterfaceSSLCertificates` (`hmailserver/source/Server/COM/InterfaceSSLCertificates.cpp:75-196`), `InterfaceSSLCertificate` (`hmailserver/source/Server/COM/InterfaceSSLCertificate.cpp:13-171`), and `PersistentSSLCertificate::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentSSLCertificate.cpp:25-86`). `Add()` returns an unsaved certificate scoped to `SSLCertificates`; `ItemByDBID` resolves existing rows; `Save()` persists `Name`, `CertificateFile`, and `PrivateKeyFile`; and `DeleteByDBID` preserves the existing collection mutation path. Installed SSL IIDs, vtable/DISPID order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:620,2514-2553,2844-2853` and `InterfaceSSLCertificate(s).rgs`.
- Code/test commit `4ed9d2f26` hardens `hmailserver/source/WebAdmin/background_sslcertificate_save.php`: the existing server-admin authorization check remains first, the handler requires `hmailRequirePostCsrfToken()`, and all five mutation fields are read from POST only. Existing `Settings -> SSLCertificates` Add/Edit/DeleteByDBID, certificate field assignments, Save, and redirects remain unchanged. `hm_sslcertificate.php` and `hm_sslcertificates.php` already carry POST, CSRF, page, action, ID, and certificate fields. `WebAdminSslCertificatePostOnlySourceTests`, `SslCertificatesComContractTests`, and `SqlServerSslCertificateAdministrationStoreTests` pass `18/18`; full Net10 passes `1267` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- TLS listener certificate loading remains the legacy startup path in `IOService::DoWork` (`hmailserver/source/Server/Common/TCPIP/IOService.cpp:82-131`) and `SslContextInitializer::InitServer` (`hmailserver/source/Server/Common/TCPIP/SslContextInitializer.cpp:63-99`). No live reconfiguration, certificate path validation, referenced-port deletion policy, COM implementation, service/database/Data-directory state, SMTP behavior, or SEC-18 staging state changed. Next slice: `hmailserver/source/WebAdmin/background_surblserver_save.php`; keep broader background hardening and PHP session cutover out of scope.

## SEC-14 WebAdmin SURBL Server Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-134`), `InterfaceSettings::get_AntiSpam` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1138-1159`), `InterfaceAntiSpam::get_SURBLServers` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:39-59`), `InterfaceSURBLServers` (`hmailserver/source/Server/COM/InterfaceSURBLServers.cpp:12-192`), `InterfaceSURBLServer` (`hmailserver/source/Server/COM/InterfaceSURBLServer.cpp:11-208`), and `PersistentSURBLServer` (`hmailserver/source/Server/Common/Persistence/PersistentSURBLServer.cpp:25-90`). `Add()` returns an unsaved item scoped to the owning collection; `Save()` persists `Active`, `DNSHost`, `RejectMessage`, and `Score` and adds the item after success. Installed SURBL IIDs, vtable/DISPID order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:2144-2220,3314-3337` and the SURBL registry resources.
- Code/test commit `d8e785231` hardens `hmailserver/source/WebAdmin/background_surblserver_save.php`: the existing server-admin check remains first, the handler requires `hmailRequirePostCsrfToken()`, and all six mutation fields are read from POST only. Existing `Settings -> AntiSpam -> SURBLServers` Add/Edit/DeleteByDBID, property assignments, Save, and redirects remain unchanged. `hm_surblserver.php` and `hm_surblservers.php` already carry POST, CSRF, page, action, ID, and SURBL fields. `WebAdminSurblServerPostOnlySourceTests`, `SurblServersComContractTests`, and `SqlServerSurblServerAdministrationStoreTests` pass `9/9`; full Net10 passes `1268` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- Legacy SMTP/SURBL runtime remains separate in `SpamTestSURBL::RunTest` (`hmailserver/source/Server/Common/AntiSpam/SpamTestSURBL.cpp:28-79`) and `SMTPConnection` (`hmailserver/source/Server/SMTP/SMTPConnection.cpp:802-869`). No DNSBL behavior, SMTP/SURBL runtime, live reconfiguration, COM/store mutation implementation, service/database/Data-directory state, or SEC-18 staging state changed. Next slice: `hmailserver/source/WebAdmin/background_dnsblacklist_save.php`; keep broader background hardening and PHP session cutover out of scope.

## SEC-14 WebAdmin Whitelist Address Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-134`), `InterfaceSettings::get_AntiSpam` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1138-1159`), `InterfaceAntiSpam::get_WhiteListAddresses` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:546-569`), `InterfaceWhiteListAddresses` (`hmailserver/source/Server/COM/InterfaceWhiteListAddresses.cpp:63-215`), `InterfaceWhiteListAddress` (`hmailserver/source/Server/COM/InterfaceWhiteListAddress.cpp:8-197`), and `PersistentWhiteListAddress` (`hmailserver/source/Server/Common/Persistence/PersistentWhiteListAddress.cpp:27-92`). Legacy `Add()` returns an unsaved item with `ID == 0` scoped to the owning collection; `Save()` inserts or updates the four persisted address fields and appends a new item after the generated `whiteid` is assigned. The installed whitelist IIDs, DISPIDs/vtable order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:2195-2220,2443-2475,3404-3420` and the whitelist registry resources.
- Code/test commit `34d8fe83e` hardens `hmailserver/source/WebAdmin/background_whitelistaddress_save.php`: the existing server-admin check remains first, the handler requires `hmailRequirePostCsrfToken()`, and all six mutation reads use POST only. Existing lower/upper IP defaults (`0.0.0.0`/`255.255.255.255`), empty email default (`*`), `Settings -> AntiSpam -> WhiteListAddresses` Add/Edit/DeleteByDBID, field assignments, Save, and redirects remain unchanged. `WebAdminWhiteListAddressPostOnlySourceTests`, `WhiteListAddressesComContractTests`, and `SqlServerWhiteListAddressAdministrationStoreTests` pass `12/12`; full Net10 passes `1270` with `3` opt-in skips. PHP is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET read-only whitelist path remains separate in `WhiteListAddresses.cs` and `SqlServerWhiteListAddressAdministrationStore.cs`; COM writes remain `E_NOTIMPL`. No whitelist/DNSBL/SMTP runtime, live reconfiguration, COM identity, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest live WebAdmin mutation is `hmailserver/source/WebAdmin/background_distributionlist_save.php`; keep broader background hardening and PHP session cutover out of scope.

## SEC-14 WebAdmin Distribution List Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceDomains::Refresh/get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceDomains.cpp:49-65`), `InterfaceDomain::get_DistributionLists` (`hmailserver/source/Server/COM/InterfaceDomain.cpp:447-468`), `InterfaceDistributionLists::Add/DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceDistributionLists.cpp:38-84`), `InterfaceDistributionList` setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceDistributionList.cpp:81-277`), `PersistentDistributionList::SaveObject/DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentDistributionList.cpp:36-157`), and `Collection::DeleteItemByDBID` (`hmailserver/source/Server/Common/BO/Collection.h:181-200`). Legacy `Add()` creates an unsaved item with `ID == 0` scoped to the owning domain collection; `Save()` inserts or updates all six persisted distribution-list columns, assigns a generated identity on insert, and adds a new item after success. The installed `DistributionLists`/`DistributionList` IIDs, DISPIDs/vtable order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:1148-1192,2993-3007`. Legacy SMTP list-policy consumption remains in `RecipientParser::UserCanSendToList_` (`hmailserver/source/Server/SMTP/RecipientParser.cpp:365-451`).
- Code/test commit `c7e5bc23a` hardens `hmailserver/source/WebAdmin/background_distributionlist_save.php`: the existing user-level denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and all eight mutation reads use POST only. Existing same-domain domain-admin ownership, `IsAddAllowed`, `Domain -> DistributionLists -> DistributionList`, Add/Edit/Delete, five field assignments, defaults, Save/error handling, and redirects remain unchanged. `WebAdminDistributionListPostOnlySourceTests`, `DistributionListsComContractTests`, `DomainsComContractTests`, and `SqlServerDistributionListAdministrationStoreTests` pass `14/14`; full Net10 passes `1271` with `3` opt-in skips. The edit and delete forms already submit POST with CSRF-bearing fields; PHP CLI is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET distribution-list path remains intentionally read-only in `DistributionLists.cs`, `IDistributionListAdministrationStore`, and `SqlServerDistributionListAdministrationStore.cs`; collection Add/Delete and item setters/Save/Delete remain `E_NOTIMPL`. No COM identity, direct activation boundary, domain-admin/server-admin boundary, SMTP list-policy behavior, live reconfiguration, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest live WebAdmin mutation is `hmailserver/source/WebAdmin/background_servermessage_save.php`; keep broader background hardening and PHP session cutover out of scope.

## SEC-14 WebAdmin DNS Blacklist Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-134`), `InterfaceSettings::get_AntiSpam` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1138-1159`), `InterfaceAntiSpam::get_DNSBlackLists` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:707-722`), `InterfaceDNSBlackLists` (`hmailserver/source/Server/COM/InterfaceDNSBlackLists.cpp:12-194`), `InterfaceDNSBlackList` (`hmailserver/source/Server/COM/InterfaceDNSBlackList.cpp:14-242`), and `PersistentDNSBlackList` (`hmailserver/source/Server/Common/Persistence/PersistentDNSBlacklist.cpp:25-89`). Legacy `Add()` returns an unsaved item with `ID == 0` scoped to the owning collection; `Save()` inserts `Active`, `DNSHost`, `ExpectedResult`, `RejectMessage`, and `Score`, assigns the generated identity, and adds the item after success. The installed DNSBL IIDs, DISPIDs/vtable order, CLSIDs, and ProgIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:1437-1469,2236,3057-3070` and the DNSBL registry resources.
- Code/test commit `92586a66d` hardens `hmailserver/source/WebAdmin/background_dnsblacklist_save.php`: the existing server-admin check remains first, the handler requires `hmailRequirePostCsrfToken()`, and all seven mutation fields are read from POST only. Existing `Settings -> AntiSpam -> DNSBlackLists` Add/Edit/DeleteByDBID, `Active`/`DNSHost`/`ExpectedResult`/`RejectMessage`/`Score` assignments, Save, and redirects remain unchanged. `WebAdminDnsBlackListPostOnlySourceTests`, `DnsBlackListsComContractTests`, and `SqlServerDnsBlackListAdministrationStoreTests` pass `9/9`; full Net10 passes `1269` with `3` opt-in skips. PHP is unavailable, so runtime PHP lint and WebAdmin request execution were not run.
- The .NET read-only DNSBL path remains separate in `DnsBlackLists.cs` and `SqlServerDnsBlackListAdministrationStore.cs`; COM writes remain `E_NOTIMPL`. No DNSBL/SMTP runtime, live reconfiguration, COM identity, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest live WebAdmin mutation is `hmailserver/source/WebAdmin/background_whitelistaddress_save.php`; keep broader background hardening and PHP session cutover out of scope.

### Historical SEC-18 context

The 2026-07-22 non-production SEC-18 rerun supersedes the earlier incomplete staging note. The rerun and bound invocation artifacts prove the authorized pool-token path, captured desktop activation denial, both-view temporary ACL scope, cleanup, and unchanged installed Application graph; the reviewers additionally require a true unauthorized-process test and stronger evidence binding before registration. The temporary probe and its registry keys are absent after cleanup; no permanent broker or existing Application identity was registered or changed. The bounded `RuleAction.To`, `RuleAction.IMAPFolder`, `RuleAction.HeaderName`, and `RuleAction.Value` setter slices are complete and validated; the next candidate is `RuleAction.RouteID`, which requires an explicit legacy behavior review before mutation is opened. SEC-18 permanent registration remains RED and must not be opened by that slice. SEC-11 `RuleCriterias.DeleteByDBID` and SEC-20 external-fetch/POP3 bounded slices remain landed as recorded below.

Son tamamlanan kucuk dilimler:

- SEC-11 `RuleCriterias.DeleteByDBID` ownership/mutation parity (`2da6a368c`): legacy `InterfaceRule::get_Criterias`, `InterfaceRuleCriterias::DeleteByDBID`, `Collection::DeleteItemByDBID`, and `PersistentRuleCriteria::DeleteObject` behavior is now represented by a rule-scoped parameterized SQL delete. Authorized collections require current snapshot membership, remove only after store success, no-op for foreign/unknown/repeated IDs, and map contained failure to `E_FAIL` while retaining the snapshot. Direct activation remains `E_ACCESSDENIED` with zero store work; retained attached collections keep legacy no-recheck behavior after failed reauthentication; IDL/RGS/COM identity, RuleActions, item Delete/Save/setters, rule execution, SMTP/delivery, and broader mutations are unchanged. `RuleCriteriasComContractTests` plus SQL store tests 12/12, full Net10 1080/1080; no live SQL Server deletion integration was run.
- SEC-11 `RuleCriteria.Delete()` ownership parity (`f537bd8aa`): legacy `InterfaceRuleCriteria::Delete` delegates to its attached parent `RuleCriterias` collection, which owns rule-scoped persistence and snapshot removal. Authorized .NET item facades now retain the owning collection delete path; successful deletion removes only that parent snapshot item, repeated/stale item deletion no-ops, contained store failure maps to `E_FAIL` with the item retained, and direct activation remains `E_ACCESSDENIED`. Add, Save, setters, rule execution, SMTP/delivery behavior, and broader mutation remain out of scope. Focused RuleCriterias/Rules/store tests pass 20/20; no live SQL Server deletion integration was run.
- SEC-11 `RuleCriterias.Delete(index)` ownership/mutation parity (`a209e2cc1`): legacy `InterfaceRuleCriterias::Delete` (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp`) delegates to `Collection::DeleteItem` (`hmailserver/source/Server/Common/BO/Collection.h`), which invokes `PersistentRuleCriteria::DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp`); negative and out-of-range indices silently no-op while valid indices return `S_OK`. The authorized .NET collection uses the selected rule snapshot and scoped store path, removes only after store success, maps contained store failure to `E_FAIL` while retaining the snapshot, and preserves direct activation denial and the installed COM contract. Add, Save/setters, RuleCriteria item behavior, rule execution, SMTP/delivery behavior, and broader mutation remain out of scope. Focused RuleCriterias/Rules/store tests pass 22/22; full Net10 with native integration passes 1141/1141; no live SQL Server deletion integration was run.
- SEC-11 `RuleCriteria.Save()` existing-row parity (`e1c74c9a4`): legacy `InterfaceRuleCriteria::Save` (`Server/COM/InterfaceRuleCriteria.cpp:30`) calls `PersistentRuleCriteria::SaveObject` (`Server/Common/Persistence/PersistentRuleCriteria.cpp:65`) for the attached rule and updates all six persisted criterion fields for the existing `criteriaid`. The authorized .NET item retains the installed COM identity and direct-activation denial, persists through an owning rule-plus-criteria scoped SQL update, maps contained store failure to `E_FAIL`, and remains readable for retry. Setter staging, Add/new-item Save, and broader rule mutation remain out of scope. Focused COM/store tests pass 25/25; full Net10 with native integration passes 1144/1144; no live SQL Server integration was run.
- SEC-11 `RuleActions.DeleteByDBID` ownership/mutation parity (`449d0a692`): legacy `InterfaceRuleActions::DeleteByDBID` (`Server/COM/InterfaceRuleActions.cpp:125`) delegates to `Collection::DeleteItemByDBID` (`Server/Common/BO/Collection.h:181`) and `PersistentRuleAction::DeleteObject` (`Server/Common/Persistence/PersistentRuleAction.cpp:121`), with unknown IDs as silent no-ops. The authorized .NET collection now requires selected-rule snapshot membership, executes a parameterized rule/action-scoped delete, removes only after store success, maps contained failure to `E_FAIL`, and preserves direct activation denial and installed COM identity. Add, index-based Delete, item Delete/Save/setters, reordering, rule execution, SMTP/delivery behavior, and broader mutation remain out of scope. Focused COM/store tests pass 16/16; no live SQL Server deletion integration was run.
- SEC-11 `RuleActions.Delete(index)` ownership/mutation parity (`181ffa958`): legacy `InterfaceRuleActions::Delete` (`Server/COM/InterfaceRuleActions.cpp:142`) delegates to the owning `Collection::DeleteItem` (`Server/Common/BO/Collection.h:218`) and `PersistentRuleAction::DeleteObject` (`Server/Common/Persistence/PersistentRuleAction.cpp:121`); valid zero-based indices return `S_OK`, and negative/out-of-range indices silently no-op. The authorized .NET collection reuses the existing rule/action-scoped delete delegate, removes only the selected snapshot item after store success, maps contained failure to `E_FAIL`, and preserves direct activation denial. RuleAction item Delete, Add, Save, setters, reordering, rule execution, SMTP/delivery behavior, and broader mutation remain out of scope. Focused RuleActions/Rules/store tests pass 18/18; no live SQL Server deletion integration was run.
- SEC-11 `RuleAction.Delete()` ownership parity (`18267d2f3`): legacy `InterfaceRuleAction::Delete` (`Server/COM/InterfaceRuleAction.cpp`) delegates to its attached parent `RuleActions` collection, which owns rule-scoped persistence and snapshot removal. Authorized .NET item facades now retain the owning collection delete path; successful deletion removes only that parent snapshot item, repeated/stale item deletion no-ops, contained store failure maps to `E_FAIL` with the item retained, and direct activation remains `E_ACCESSDENIED`. Add, Save, setters, MoveUp, MoveDown, rule execution, SMTP/delivery behavior, and broader mutation remain out of scope. Focused RuleActions/Rules/store tests pass 20/20; no live SQL Server deletion integration was run.
- SEC-20 legacy registered `InterfaceFetchAccount` direct-activation containment (`62f40dc77`): `InterfaceFetchAccount::InterfaceFetchAccount` artik detached `HM::FetchAccount` olusturmuyor, bu nedenle direct `hMailServer.FetchAccount` getter/setter, `Save`, `DownloadNow` ve `Delete` yollarinda existing null-object guards access-denied donuyor. Parent-owned `InterfaceFetchAccounts::Add/get_Item/get_ItemByDBID` attachment, IID/CLSID/ProgID/DISPID/vtable/type library/RGS, SQL schema, external-fetch scheduling/protocol ve .NET production code degismedi. `TestDirectlyConstructedFetchAccountIsUnattached` eklendi; RegressionTests build 0/0, changed C++ translation unit `/utf-8` compile pass, .NET FetchAccounts/manifest filter 12/12, full Net10 1076/1076. Installed legacy service regression cannot run on this host because `hMailServer` is stopped/disabled and the expected NUnit console package is absent.
- SEC-18 bounded identity-denial/staging slice (2026-07-15): dedicated IIS site/pool/PHP health and x64 compatibility were verified; the virtual-account collector defect was fixed with `build/test-webadmin-broker-staging-inventory.ps1`, and the REG_BINARY test also passes. A temporary fresh-GUID LocalSystem probe read back exact SYSTEM+pool AppID ACLs. Non-elevated desktop activation, PHP activation, and a PHP-inherited child all fail at activation with `0x80070005`; no method counter/evidence advances on denial. SYSTEM-only diagnostic reached the method but observed impersonation level 1 and is invalid for pool evidence. The collector remains `Incomplete`/exit `2`; both independent reviewers returned RED. The temporary service/registration/endpoints were removed, while IIS site/pool/PHP and sanitized reports under `artifacts/sec18-staging/` remain. Next slice is diagnosis of the authorized pool activation path, not permanent broker registration.
- SEC-18 authorized caller-token evidence and direct-PHP path (2026-07-16): the fresh-GUID LocalSystem probe used AppID LaunchPermission/AccessPermission containing only SYSTEM plus `IIS AppPool\HMailWebAdminBrokerPool`; direct PHP FastCGI activation reached the temporary out-of-process service, `CoImpersonateClient` plus `OpenThreadToken` captured the exact worker SID at `SecurityIdentification`, and `CoRevertToSelf` left no residual token. Wrong-SID method invocation returned `0x80070005`; non-elevated desktop activation was denied before method entry with unchanged counter/evidence. The corrected elevated collector returned `EvidenceCollectedSecurityReviewRequired` with one dedicated pool mapping and matching SID; both reviewers returned GREEN for bounded staging implementation. Focused collector tests 2/2 and full Net10 1080/1080 passed. Cleanup validation confirmed no temporary service/registry/endpoint/probe residual and preserved IIS/PHP plus stopped/disabled hMailServer. Evidence: `artifacts/sec18-staging/SEC18-authorized-pool-evidence-20260716.json`, `SEC18-dacl-cleanup-validation-20260716.json`, and `staging-inventory-20260716-sec18.json`. Next slice is permanent broker contract/preflight/strict-denial implementation on isolated staging only.
- SEC-18 additive broker contract freeze (2026-07-16, `06820aa7d`): the .NET 10 rewrite now declares a fresh type-library/interface/class/AppID identifier set and the dual, nonextensible `IInterfaceWebAdminSessionBroker` four-member DISPID 1-4 contract from `WEBADMIN_SESSION_REAUTH_DESIGN.md`. Contract tests prove the new identifiers are distinct from the installed Application/type-library identities and that `LegacyComRegistrationManifest` does not publish broker roots. No broker class factory, installer, registry/AppID ACL, PHP, or existing COM identity changed. Focused contract plus legacy-manifest tests pass 5/5; full Net10 is 1081/1083 because the existing ClamWin/custom-scanner cleanup tests fail deleting temporary `.eml` files. Next slice is the actual impersonated caller-token guard and strict denial/revert coverage on isolated staging.
- SEC-18 .NET caller-guard policy slice (2026-07-16, `24df969ac`): `WebAdminSessionBrokerCallerGuard` accepts only a captured non-remote impersonation identity at identification-or-higher level whose normalized SID matches the configured worker SID, invokes the broker operation without a caller-supplied expected SID, and always attempts revert. Missing, anonymous, primary, remote, mismatched, and failed-revert paths return detail-free `E_ACCESSDENIED`; operation failures are preserved after successful revert. `WebAdminSessionBrokerCallerGuardTests` pass 11/11. This is an injectable policy boundary only: native `CoImpersonateClient`/`OpenThreadToken` capture, broker-only AppID preflight, registration, PHP cutover, and existing COM identity remain out of scope. Full Net10 was 1090/1092 at this gate.
- SEC-18 Windows caller-source and broker-only AppID preflight slice (2026-07-16, `4b109682d`): `WindowsWebAdminBrokerCallerIdentitySource` now captures the effective COM token through `CoImpersonateClient` and `OpenThreadToken` with `OpenAsSelf`, extracts token SID/type/impersonation level, closes the handle, and delegates revert to `CoRevertToSelf`. `WebAdminSessionBrokerCallerGuard` sanitizes capture errors and reverts before invoking any broker operation. `WebAdminSessionBrokerAppIdPreflight` accepts only the fresh broker AppID, unchanged installed Application evidence, explicit exact worker/service SID allow-lists, and no deny entries. Focused boundary tests pass 17/17 and full Net10 passes 1100/1100. No broker registration, registry write, DCOM ACL mutation, PHP cutover, or existing COM identity change occurred. Next slice is isolated COM integration plus actual broker-only registry evidence/readback.
- SEC-18 read-only broker/legacy AppID registry readback slice (2026-07-16, `dfef2a136`): `WindowsWebAdminBrokerRegistryEvidenceSource` reads both `Registry64` and `Registry32` `HKLM\Software\Classes\AppID` views through non-writable handles, records missing/read-error state, preserves registry value kinds and exact raw bytes, and compares the existing legacy Application AppID snapshot before/after. Focused tests cover missing broker registration, dual-view capture, unchanged snapshots, and changed value names/kinds/bytes; the readback is not yet wired into preflight, COM class-factory registration, PHP, DCOM ACL mutation, or existing Application identity. Focused boundary/readback tests pass 22/22 and full Net10 passes 1105/1105. Next slice is isolated COM integration plus feeding readback into the preflight gate.
- SEC-18 readback-to-preflight fail-closed slice (2026-07-16): `EvaluateFromRegistryReadback` now requires readable matching legacy Application AppID snapshots at the expected path, exactly one readable Registry64 and Registry32 broker snapshot with byte-equivalent content, fresh broker AppID/LocalService evidence, and ordinary explicit LaunchPermission/AccessPermission ACEs for only the worker/service SID set with the required local DCOM access mask. Registry parsing rejects malformed/non-binary values, missing DACLs, deny/inherited/callback/inherit-only/object/duplicate/wrong-mask ACEs, wrong registry views, wrong legacy paths, read errors, and duplicate normalized mask SIDs. Focused registry/boundary tests pass 20/20 and full Net10 passes 1114/1114. No broker registration, COM class factory, DCOM ACL mutation, PHP cutover, installed Application identity, production service/database/data, or SMTP/IMAP behavior changed. Independent reality review remains RED for broker registration because the full installed Application CLSID/ProgID/CurVer/LocalServer32/TypeLib graph and trusted production preflight caller are not yet captured. Next slice is complete installed Application registration-graph readback and byte/key preservation evidence.
- SEC-18 full installed Application registration-graph readback slice (2026-07-16): `WindowsWebAdminBrokerRegistryEvidenceSource` now snapshots the legacy `hMailServer.Application` ProgID/CLSID/AppID/TypeLib/Interface graph in both Registry64 and Registry32 views through non-writable handles. It records key presence/read errors, value names, `RegistryValueKind`, and exact native `RegQueryValueEx` bytes; before/after comparison is keyed by `(RegistryView, KeyPath)` and preserves the observed Registry32 absence of the installed Application CLSID root. Legacy behavior is anchored to `InterfaceApplication.rgs`, `hMailServer.rgs`, `hMailServer.idl`, `hMailServer.cpp`, and `hMailServer.rc`. Focused graph/preflight tests pass 17/17 and full Net10 passes 1118/1118. No broker registration, existing Application registration mutation, DCOM ACL change, PHP cutover, service/database/data access, or protocol behavior changed. Next slice is independent review plus live graph evidence capture before isolated COM integration.
- SEC-18 live installed Application graph evidence (2026-07-18): a read-only native `RegQueryValueEx` collector captured 44 before/after snapshots for the 22 tracked legacy graph paths across Registry64 and Registry32. Read errors were zero, the snapshots were byte-identical, the hMailServer service was stopped/disabled, and no COM activation, registration, database, or data-directory access occurred. Live parity confirmed all 22 Registry64 paths and six absent Registry32 CLSID-subtree paths. Both hmail_security_reviewer and hmail_reality_checker returned RED: canonical expected contents, recursive unknown-subkey/key-ACL detection, checked-in collector attestation, and native-reader integration tests remain required. Next slice is the deterministic checked-in canonical graph verifier and collector.
- External-fetch failure scheduling parity dilimi (`dada12fea`), legacy `ExternalFetchTask::DoWork` retry timing'ini .NET `ExternalFetchProcessor` failure cleanup'ina tasiyor: connection/POP3/receiver/retention failure durumunda `CompleteAsync` ile `falocked` temizleniyor ve `fanexttry` `faminutes` ile ileri aliniyor. Session-factory connection failure dahil odakli testler 31/31, external-fetch filtresi 70/70, full Net10 suite 1050/1050 gecti. COM/IDL, schema, POP3 commands, TLS, destination policy ve `ReleaseAsync` lock-only semantics degismedi.
- .NET `FetchAccount`/`FetchAccounts` direct-activation denial evidence dilimi (`baf9d9f33`), installed CLSID/IID/ProgID/DISPID/vtable shape'i ve authenticated `Account -> FetchAccounts` boundary'sini degistirmeden, directly constructed item getter/setter, `Save`, `Delete`, `DownloadNow` ve collection lookup/`Refresh`/`Delete`/`DeleteByDBID`/`Add` yollarinin tamamini `E_ACCESSDENIED` ile snapshot/store access oncesinde kilitledi. `FetchAccountsComContractTests` 10/10, ilgili COM/manifest/SQL filter 14/14, full Net10 suite 1051/1051 gecti. Uretim kodu, legacy C++ registration, password/download execution ve fetch-account mutation degismedi.
- WebAdmin external-account add/edit POST boundary dilimi (`ba115c720`), `background_account_externalaccount_save.php` girisinde POST/POST-only CSRF guard'ini scope/domain/account/fetch-account lookup oncesine aldi ve action/scope/field okumalarini POST-only helper'a tasidi; mevcut add/edit formu hidden CSRF ve field mapping ile ayni kaldi. Manual Web form test sozlesmesine add/edit GET 405, query-only POST rejection, authorized POST ve cross-scope denial senaryolari eklendi; deterministic source-surface check gecti, PHP runtime lint bu hostta kullanilabilir degildi, full Net10 suite 1051/1051 gecti. Credential-retargeting, egress policy, POP3 ve live behavior degismedi.
- WebAdmin external-account credential-authority retargeting dilimi (`0a667b9fc`), edit oncesi old `ServerAddress`/`Port`/`Username`/`ConnectionSecurity` tuple capture ile blank/omitted-password changed-authority case'inde stored password clear ediyor; string comparisons type-safe, numeric fields strict, explicit password replacement precedence korunuyor, unchanged edit ve new add legacy behavior'i koruyor. Legacy `InterfaceFetchAccount`/`PersistentFetchAccount` references and .NET `FetchAccount` E_NOTIMPL boundary documented; manual Web form scenarios plus deterministic source-surface check 12/12 gecti, PHP runtime lint bu hostta kullanilabilir degildi, full Net10 suite 1051/1051 gecti. Egress policy, POP3, TLS, registry, service config ve live behavior degismedi.
- External-fetch egress/SSRF resolve-once slice (`442b94c40`), legacy `ExternalFetch::Start` A/AAAA/CNAME sonucu ilk numeric endpoint'e baglanma ve TLS hostname davranisini .NET `IExternalFetchAddressResolver`/`ExternalFetchEndpointPolicy` sinirina tasidi. Mapped IPv6, private/CGNAT/link-local/ULA/reserved/multicast/transition/metadata classification, arbitrary-host loopback denial, explicit localhost/loopback CIDR opt-in, credential-free service logging, audit-only default ve enforcement-before-connect coverage eklendi; no-proxy/no-redirect, SMTP trust, POP3 commands, live reconfiguration, COM identity ve production data degismedi. Focused external-fetch filter 82/82, full Net10 1063/1063 gecti.
- SEC-20 external-fetch POP3 timeout/line-budget parity (`a5c761702`), legacy `POP3ClientConnection`/`TCPConnection` 900-second low-load timeout, five-second QUIT grace, and 250000-byte CRLF control buffer behavior are now represented in `TcpExternalFetchSessionFactory`. DNS, TCP connect, implicit/STARTTLS, commands, and line reads share the configurable operation deadline; internal expiry maps to `TimeoutException`, caller cancellation remains `OperationCanceledException`, UIDL/RETR data stays logically unbounded, and failed leases complete through the existing processor path. `TcpExternalFetchSessionFactoryTests` + `ExternalFetchProcessorTests` filter 79/79; full Net10 1076/1076. Adaptive 14000-session timeout and live DNS/TLS evidence remain open.
- Legacy registered `InterfaceFetchAccount` ownership-boundary evidence slice (`285146c4d`), exact C++ constructor/collection attach behavior ile .NET direct-activation policy'sini karsilastirdi. `Activator.CreateInstance` ile item'in tum getter/setter, `Save`, `Delete`, `DownloadNow` yollarinin; standalone collection'in lookup/`Refresh`/`Delete`/`DeleteByDBID`/`Add` yollarinin snapshot/store access oncesi `E_ACCESSDENIED` dondugu test edildi. Authenticated `Account -> FetchAccounts` DBID lookup'u refresh oncesi ve sonrasi dogrulandi; installed CLSID/IID/ProgID/DISPID/vtable, password/download behavior, legacy C++ registration ve fetch mutation degismedi. Focused COM/manifest/store filtre 14/14, full Net10 1063/1063.
- `Settings -> AntiSpam` reauthentication ownership slice (`116b316ea`), legacy `InterfaceSettings::get_AntiSpam` plus `InterfaceAntiSpam::LoadSettings`, `ClearGreyListingTriplets`, and `TestSpamAssassinConnection` call orderunu .NET boundary'sine tasidi. Direct `AntiSpam` activation'da iki operational method `E_ACCESSDENIED`; failed reauthentication sonrasi new child facade method-level deny ve zero extra settings read, retained child ise one store/one runtime call ile usable. AntiSpam COM identity, setters, SMTP policy, scanner egress ve live reconfiguration degismedi. Dar AntiSpam/Settings/greylisting/SpamAssassin filtresi 41/41, full Net10 1064/1064.
- `Settings -> AntiSpam.DKIMVerify` reauthentication parity test slice (`46663f7be`), legacy `InterfaceSettings::get_AntiSpam`, `InterfaceAntiSpam::LoadSettings`, `InterfaceAntiSpam::DKIMVerify` ve `DKIM::Verify` boundary'sini testle kapatti. Failed reauthentication sonrasi newly obtained child `E_ACCESSDENIED` ile file/runtime access oncesi duruyor; retained child caller-supplied file ile `Pass` donuyor ve verifier runtime'i tam bir kez cagiriyor. Direct activation, installed COM identity, signing/setter, SMTP policy ve network behavior degismedi. Dar AntiSpam filter 12/12, full Net10 suite 1064/1064.
- `Settings -> AntiSpam -> SURBLServers` reauthentication parity slice (`c05cc0d38`), legacy `InterfaceAntiSpam::get_SURBLServers`, `InterfaceSURBLServers::LoadSettings` ve collection lifetime boundary'sini .NET callback ile esledi. Failed reauthentication sonrasi retained `AntiSpam` yeni SURBL facade'i denied donuyor ve store read yapmiyor; once alinmis collection facade'i count/lookup ile usable kaliyor. Installed COM identity, direct activation, SURBL mutation, DNS lookup, SMTP policy ve live reconfiguration degismedi. Dar AntiSpam/SURBL/SQL filter 20/20, full Net10 suite 1064/1064.
- `Settings -> AntiSpam -> GreyListingWhiteAddresses` reauthentication parity test slice (`01efc555b`), legacy `InterfaceAntiSpam::get_GreyListingWhiteAddresses`, collection/item lifetime ve direct store access order'unu testle kapatti. Failed reauthentication sonrasi new AntiSpam child greylist access oncesi `E_ACCESSDENIED` donuyor; retained AntiSpam yeni collection icin one store read yapiyor; once alinmis collection/item getter'lari ek read olmadan usable kaliyor. Item Save/Delete, collection mutation, Refresh failure, greylisting policy, live reconfiguration ve COM identity degismedi. Dar GreyListingWhiteAddresses/AntiSpam/SQL filter 22/22, full Net10 suite 1065/1065.
- Security review follow-up: .NET `FetchAccount`/`FetchAccounts` direct activation boundary'si tum access path'lerde `E_ACCESSDENIED` ile testle kapatildi; external-account add/edit de POST-only/CSRF-korumali hale getirildi ve WebAdmin credential-authority retargeting clear-on-change hardening'i tamamlandi. Legacy registered `FetchAccount` constructor ownership riski `62f40dc77` ile kapatildi; .NET COM mutation `E_NOTIMPL`, adaptive high-load timeout behavior ve DNS/TLS live integration evidence acik; sonraki production-gate slice SEC-18 dedicated-IIS-staging evidence run'i.
- SEC-15/SEC-16 WebAdmin scanner-test target pinning dilimi (`7e98a4178`), `hmailResolveLocalScannerTarget` ile ClamAV/SpamAssassin test hostname'ini bir kez IPv4'e cozer, legacy `Utilities::IsLocalHost` ile local interface oldugunu dogrular ve COM test methoduna yalniz bu literal'i verir. Remote, unresolved, IPv6-only ve array-valued input HTTP 400/body `0` ile COM cagrisindan once reddedilir; bu legacy ClamAV'in tekrar hostname resolve etmesini ve SpamAssassin'in ayri ilk-DNS sonucu secmesini engeller. Local scanner tests, POST/server-admin/CSRF sinirlari, saved settings, normal SMTP scanner behavior, direct COM tests ve external fetch degismedi. PHP runtime/harness olmadigi icin manual Web form regression senaryosu ve focused source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-18 repeatable dedicated-IIS staging-inventory collector dilimi, `build/get-webadmin-broker-staging-inventory.ps1` ile WebAdmin path, IIS site/application-pool mappings/worker SID, existing Application AppID'nin iki registry view'i/machine default DCOM descriptor'leri ve externally produced caller-token evidence'ini JSON'a topluyor. Script COM baslatmaz, probe execute etmez, registry/IIS/service degistirmez; `-FailOnIncomplete` exit `2` ve local focused check no-IIS hostun `Incomplete` sonucunu dogruluyor. Broker registration her durumda security review gerektirir.
- SEC-18 collector REG_BINARY preservation slice (`f2043db01`), `Get-RegistryValueEvidence` binary return'unu pipeline enumeration'dan koruyarak `DefaultLaunchPermission` gibi machine DCOM descriptor'lerinin SDDL decode edilmesini sagladi. Read-only focused test ve parser validation gecti; full Net10 suite 1066/1066. 2026-07-14 local collector rerun descriptor decode'u basariyla yapiyor ama IIS module, dedicated worker SID ve independent caller-token evidence yoklugunda `Incomplete` kaliyor; broker registration ve production WebAdmin/COM behavior degismedi.
- SEC-18 non-production deployment inventory dilimi, mevcut Windows 11 test hostunda `C:\hMailServer57-Test\PHPWebAdmin` dosyalarini ve disabled/stopped `hMailServer` LocalSystem service'ini buldu; IIS application pool, PHP/IIS worker ve authorized caller-token probe bulunmadi. Mevcut Application AppID `{5EDEC473-39E0-43F6-A234-1947071721C8}` icin per-AppID LaunchPermission/AccessPermission yok; machine DefaultLaunchPermission `BA`, `IU`, `SY` iceriyor. COM baslatilmadi, registry/IIS/service degistirilmedi. Sonuc broker registration icin negative evidence: dedicated IIS staging hostunda worker SID/pool isolation/caller-token kaniti gerekir.
- SEC-18 broker-bridge identity/caller-access audit dilimi, PHP'nin her istekte `hMailServer.Application` olusturdugunu; installer'in yalniz PHPWebAdmin dosyalarini kopyaladigini, IIS worker SID veya DCOM ACL tanimlamadigini; mevcut Application'in `{5EDEC473-39E0-43F6-A234-1947071721C8}` AppID'sini paylastigini ve server tarafinda caller-token/SID kontrolu olmadigini kaydetti. Bu nedenle broker register/expose edilmedi: future bridge ayri type library, IID/CLSID/ProgID/AppID, broker-only LaunchPermission/AccessPermission ve per-method impersonated caller-SID verification gerektirir. Ilk sonraki is non-production deployment evidence capture; PHP/WebAdmin, public COM, password/token persistence, protocol ve live behavior degismedi.
- SEC-18 legacy broker-lifetime-owner dilimi (`7974cbd98`), `LegacyWebAdminSessionService` ile service sahibi instance basina `LegacyWebAdminSessionBrokerFactory` uzerinden tek broker tutuyor ve brokeri disari vermeden yalniz mevcut credential-admission/session-request helper'larini sariyor. Ayni owner ile token admission/request success, ayri owner ile restart-model denial ve null broker fail-closed `ClassTester` kapsaminda. Service startup, broker registration, PHP/WebAdmin, public COM, password/token persistence, protocol ve live behavior degismedi. Broker ve `ClassTester` translation unit `/utf-8 /wd4566 /WX` ile derlendi; full Net10 1049/1049 gecti.
- SEC-18 legacy session-request-composition dilimi (`9c8cf9ef6`), `LegacyWebAdminSessionRequest::CreateApplication` ile mevcut broker tokenini ve PHP-session binding'ini `OpenSession` uzerinden current authenticated `COMAuthentication` degerine cozup yalniz basarili sonucu `LegacyWebAdminApplicationFactory` ile yayinliyor. Null output `E_POINTER`; missing/unknown token, wrong session, expired ve revoked token `E_ACCESSDENIED` donuyor ve caller output'u sifirlaniyor. Valid token fresh authenticated Application uretiyor; direct activation, installed Application IID/CLSID/Authenticate signature/DISPID 17, broker registration, PHP/WebAdmin, public COM, password/token persistence, protocol ve live behavior degismedi. Broker ve `ClassTester` translation unit `/utf-8 /wd4566 /WX` ile derlendi; full Net10 1049/1049 gecti.
- SEC-18 legacy credential-admission dilimi (`3b6109728`), `LegacyWebAdminCredentialAdmission` ile production username/password dogrulamasini fresh local `COMAuthentication::Authenticate` uzerinden yapip yalniz basarili principal'i mevcut broker token-create path'ine aktariyor. Native test seam accept/null/throw/empty PHP session/credential-version denial akisini token clearing ve hook call count ile kapsiyor; helper ve `SessionRecord` raw password tutmuyor. Admission legacy account/domain cache yolunu korurken existing token verifier fresh persistence/configuration kontrolu yapiyor. Broker register/expose edilmedi; PHP/WebAdmin, public COM, password/token persistence, protocol ve live behavior degismedi. Broker ve `ClassTester` translation unit `/utf-8 /wd4566 /WX` ile derlendi; full Net10 1049/1049 gecti.
- SEC-18 legacy internal-application factory dilimi (`7241f2e13`), `LegacyWebAdminApplicationFactory` ile authenticated broker sonucundan fresh existing `InterfaceApplication` uretiyor ve private shared `COMAuthentication` degerini `IInterfaceApplication` yayinlanmadan once native friend seam ile bagliyor. Null/anonymous auth deny, broker-authenticated admin success, direct application activation'in unauthenticated kalmasi ve installed Application IID/CLSID/Authenticate signature/DISPID 17 `ClassTester` kapsaminda. Broker register/expose edilmedi; PHP/WebAdmin, public COM, password/token persistence, protocol ve live behavior degismedi. Uc ilgili C++ translation unit `/utf-8 /wd4566 /WX` ile derlendi; full Net10 1049/1049 gecti. Legacy full build bu makinede `MIDL2020 SaveAllChanges` ile mevcut `hMailServer.tlb` dosyasinda duruyor.
- SEC-18 legacy authoritative-source hook dilimi (`7de1d80d0`), `LegacyWebAdminSessionBrokerFactory` ile native broker'i fresh `PersistentAccount::ReadObject`, active `PersistentDomain::ReadObject` ve `IniFileSettings::GetAdministratorPassword` kaynaklarina bagladi. `ClassTester` disabled/deleted account, domain/admin-level mismatch ve administrator/account verifier degisikliginde token deny ediyor. Current persisted verifier bulunmadigi icin external AD accounts fail-closed kalir. Broker register/expose edilmedi, `InterfaceApplication` olusturulmadi; PHP/WebAdmin, public COM, password/token persistence, protocol ve live behavior degismedi. Degisen broker ve `ClassTester` translation unit `/utf-8` ile derlendi; full Net10 1049/1049 gecti.
- SEC-18 legacy broker-foundation dilimi (`b82272b41`), `Server/COM/WebAdminSessionBroker` ile unregistered native process-local 32-byte random token store ekledi. Sadece process-key HMAC token/session/credential kayitlari tutuluyor; 20 dakika idle/8 saat absolute expiry, binding-checked revoke, process restart invalidation, injected principal/credential hooks ve internal `COMAuthentication::AttachAuthenticatedPrincipal` var. `ClassTester` lifecycle, binding mismatch, expiry, revocation, principal/credential denial, restart invalidation ve installed Application IID/CLSID/Authenticate signature/DISPID 17 kontrol ediyor. PHP/WebAdmin, public COM kaydi, password/token persistence, protocol ve live behavior degismedi. Legacy full project build'i bu makinede MIDL TLB write (`MIDL2020`) ve Turkish-path CP1251 `/WX` hatalariyla engelli; degisen uc C++ translation unit `/utf-8` ile derlendi, full Net10 1049/1049 gecti.
- SEC-18 read-only WebAdmin session-reauthentication design audit dilimi, `background_login.php::Login`, `background_account_save.php` current-user password update, `initialize.php` per-request COM authentication ve legacy `InterfaceApplication::Authenticate`/`COMAuthentication` object lifetime'ini dogruladi. `WEBADMIN_SESSION_REAUTH_DESIGN.md`, PHP'nin `session_password` degerini basitce kaldiramayacagini; additive service-local opaque-token broker, token/session binding, current-principal refresh, credential-version invalidation, current-user password rotation, direct-activation restriction, focused test ve rollout/rollback tasarimini kaydediyor. Session, COM, password-persistence veya protocol behavior degismedi.
- SEC-14 WebAdmin blocked-attachment-delete hardening dilimi, `hm_smtp_antivirus.php` GET linki yerine CSRF token ve delete parametrelerini detached per-attachment POST formlariyla `background_blocked_attachment_save.php` yoluna tasidi; visible delete controls antivirus settings formunu nested etmeden bu formlara bagli. Handler mevcut server-administrator boundary sonrasinda ve settings/collection lookup/mutation oncesinde `action=delete` non-POST istegini `hmailRequirePost()` ile reddediyor. Selected blocked-attachment scope, delete redirect, antivirus settings save, scanner tests, blocked-attachment add/edit, non-delete navigation ve diger mutation path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin rule-action-delete hardening dilimi, `hm_rule.php` GET linki yerine CSRF token ve delete parametrelerini detached per-action POST formlariyla `background_rule_save.php` yoluna tasidi; visible action controls rule-edit formunu nested etmeden bu formlara bagli. Shared rule handler `GetHasRuleAccess` sonrasinda ve action lookup/mutation oncesinde `savetype=action` non-POST istegini `hmailRequirePost()` ile reddediyor. Selected domain/account/rule/action scope, rule-action delete redirect, global/account-rule delete, rule-edit submit behavior, rule execution, criteria delete, non-delete navigation ve diger mutation path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin rule-criteria-delete hardening dilimi, `hm_rule.php` GET linki yerine CSRF token ve delete parametrelerini detached per-criteria POST formlariyla `background_rule_save.php` yoluna tasidi; visible criteria controls rule-edit formunu nested etmeden bu formlara bagli. Shared rule handler `GetHasRuleAccess` sonrasinda ve criteria lookup/mutation oncesinde `savetype=criteria` non-POST istegini `hmailRequirePost()` ile reddediyor. Selected domain/account/rule/criteria scope, rule-criteria delete redirect, global/account-rule delete, rule-edit submit behavior, rule execution, action delete, non-delete navigation ve diger mutation path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin account-rule-delete hardening dilimi, `hm_account.php` GET linki yerine CSRF token ve delete parametrelerini detached per-rule POST formlariyla `background_rule_save.php` yoluna tasidi; visible rule controls account-edit formunu nested etmeden bu formlara bagli. Shared rule handler `GetHasRuleAccess` sonrasinda ve rule lookup/mutation oncesinde `savetype=rule` non-POST istegini `hmailRequirePost()` ile reddediyor. Selected domain/account/rule scope, account-rule delete redirect, global-rule delete, account-edit submit behavior, rule execution, criteria/action delete, non-delete navigation ve diger mutation path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin global-rule-delete hardening dilimi, `hm_rules.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_rule_save.php` yoluna tasidi. Shared rule handler `GetHasRuleAccess` sonrasinda ve global-rule lookup/mutation oncesinde `savetype=rule`/`domainid=0` non-POST istegini `hmailRequirePost()` ile reddediyor. `domainid=0`/`accountid=0` global scope, selected-rule delete/redirect behavior, rule execution, account-rule delete, criteria/action delete, non-delete navigation ve diger mutation path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin external-account-download-now hardening dilimi, `hm_account_externalaccounts.php` GET linki yerine CSRF token ve download-now parametrelerini POST body ile `background_account_externalaccount_save.php` yoluna tasidi. Server-side download-now dali `hmailRequirePost()` ile non-POST istegini selected-item lookup ve `DownloadNow()` oncesi reddediyor; mevcut account/domain administrator ownership kontrolleri, selected account scope ve terminal redirect korundu. External-fetch configuration, delete behavior, non-delete navigation ve diger mutation path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin external-account-delete hardening dilimi, `hm_account_externalaccounts.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_account_externalaccount_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut account/domain administrator ownership kontrolleri, selected account scope ve terminal redirect korundu. External-fetch configuration, mevcut download-now action, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin whitelist-address-delete hardening dilimi, `hm_whitelistaddresses.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_whitelistaddress_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve terminal redirect korundu. Whitelist-address selection, anti-spam whitelist behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin SURBL-server-delete hardening dilimi, `hm_surblservers.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_surblserver_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve legacy delete/save control flow korundu. SURBL-server selection, anti-spam SURBL behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin DNS-blacklist-delete hardening dilimi, `hm_dnsblacklists.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_dnsblacklist_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve legacy delete/save control flow korundu. Blacklist selection, anti-spam DNS-blacklist behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin TCP/IP-port-delete hardening dilimi, `hm_tcpipports.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_tcpipport_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve terminal redirect korundu. Port selection, protocol/listener configuration, certificate association, live binding/restart behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin SSL-certificate-delete hardening dilimi, `hm_sslcertificates.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_sslcertificate_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve legacy delete/save control flow korundu. Certificate selection, certificate/private-key file behavior, TCP/IP port configuration, TLS policy, live reconfiguration, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin incoming-relay-delete hardening dilimi, `hm_incomingrelays.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_incomingrelay_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve legacy delete/save control flow korundu. Incoming-relay selection, SMTP trust behavior, live reconfiguration, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin security-range-delete hardening dilimi, `hm_securityranges.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_securityrange_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve legacy delete/save control flow korundu. Security-range selection, IP policy, auto-ban behavior, live reconfiguration, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin route-address-delete hardening dilimi, `hm_route_addresses.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_route_address_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu, parent-route selection ve terminal redirect korundu. Route-address selection, edit/save behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin route-delete hardening dilimi, `hm_routes.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_route_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu ve terminal redirect korundu. Route selection, edit/save behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin domain-alias-delete hardening dilimi, `hm_domain.php` Names-tab GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_domain_name_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut server-administrator authorization kontrolu korundu. Domain/alias selection, unrelated domain edit/save behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin distribution-list-recipient-delete hardening dilimi, `hm_distributionlist_recipients.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_distributionlist_recipient_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini recipient lookup ve COM mutation oncesi reddediyor; mevcut user ve domain-scope authorization kontrolleri korundu. Distribution-list/recipient selection, edit behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin distribution-list-delete hardening dilimi, `hm_distributionlists.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_distributionlist_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut user ve domain-scope authorization kontrolleri korundu. Distribution-list selection, edit behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin alias-delete hardening dilimi, `hm_aliases.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_alias_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor; mevcut user ve domain-scope authorization kontrolleri korundu. Alias selection, edit behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin domain-delete hardening dilimi, `hm_domains.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_domain_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini ve server-admin olmayan istegi COM mutation oncesi reddediyor. Domain selection, edit behavior, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-14 WebAdmin account-delete hardening dilimi, `hm_accounts.php` GET linki yerine CSRF token ve delete parametrelerini POST body ile `background_account_save.php` yoluna tasidi. Server-side delete dali `hmailRequirePost()` ile non-POST istegini COM mutation oncesi reddediyor. Account selection, authorization, non-delete navigation ve diger mutation/delete path'leri degismedi. PHP runtime/harness olmadigi icin yeni manual Web form regression senaryosu ve odakli source-surface check calistirildi; full Net10 suite 1049/1049 gecti.
- SEC-11 `Settings -> Cache` reauthentication parity dilimi, basarili administrator authentication sonrasi basarisiz `Authenticate` ile legacy member-level kontrol noktalarini esledi. Retained `Settings.Cache` yeni facade veremiyor; attached `Cache.Clear` ve dort hit-rate getter'i runtime access oncesi `E_ACCESSDENIED` oluyor. Legacy'nin recheck etmedigi cache configuration, current-size ve maximum-size getter'lari aynen calisiyor. Direct activation, IID/vtable/DISPID shape, persistence ve live cache reconfiguration degismedi. Dar Cache/Settings-store filtresi 9/9, full Net10 suite 1049/1049 gecti.
- SEC-11 `Settings -> Routes -> Addresses` reauthentication parity dilimi, basarili administrator authentication sonrasi basarisiz `Authenticate` ile legacy C++ kontrol noktalarini esledi. Retained `Settings.Routes` yeni adapter veya route-store read uretemiyor; attached `RouteAddress.Delete` persistence oncesi `E_ACCESSDENIED` oluyor. Legacy'nin recheck etmedigi attached `Routes` count/lookup/`Refresh`, `Route.Addresses` ve route-address collection `DeleteByDBID`/`DeleteByAddress` aynen calisiyor. Direct activation, IID/vtable/DISPID shape, route matching, credentials ve live routing degismedi. Dar Routes/RouteAddresses/store filtresi 18/18, full Net10 suite 1048/1048 gecti.
- SEC-11 `ServerMessages` retained-Settings parity dilimi, basarili administrator authentication sonrasi basarisiz `Authenticate` ile legacy getter kontrolunu esledi. Retained `Settings.ServerMessages` yeni adapter veya store read uretemiyor ve `E_ACCESSDENIED` oluyor; legacy'nin recheck etmedigi attached `ServerMessages.Refresh`, `ServerMessage` setter'lari ve `Save` aynen calisiyor. Direct activation, IID/vtable/DISPID shape, delivery-template execution, insert/delete ve live reload degismedi. Dar ServerMessages/store filtresi 10/10, full Net10 suite 1047/1047 gecti.
- SEC-11 `SSLCertificates` reauthentication parity dilimi, basarili administrator authentication sonrasi basarisiz `Authenticate` ile retained facade'larda legacy C++ kontrol noktalarini esledi. Configured runtime ile direct `SSLCertificates`/`SSLCertificate` activation tum collection/item mutation yollarinda `E_ACCESSDENIED` ve sifir store isini koruyor. Retained `Settings.SSLCertificates` yeniden adapter veremiyor; attached item `Save`/`Delete` (unsaved `Add()` item'i dahil) persistence oncesi `E_ACCESSDENIED` oluyor. Legacy'nin recheck etmedigi attached collection `DeleteByDBID`/`Add`/`Refresh`/`Clear` ve staged setter davranisi degismedi. Dar SSL/store filtresi 17/17, full Net10 suite 1046/1046 gecti.
- SEC-11 `IncomingRelays` reauthentication parity dilimi, basarili administrator authentication sonrasi basarisiz `Authenticate` ile retained facade'larda legacy C++ kontrol noktalarini esledi. Configured runtime ile direct `IncomingRelays`/`IncomingRelay` activation tum collection/item mutation yollarinda `E_ACCESSDENIED` ve sifir store isini koruyor. Retained `Settings.IncomingRelays` yeniden adapter veremiyor; attached item `Save`/`Delete` (unsaved `Add()` item'i dahil) persistence oncesi `E_ACCESSDENIED` oluyor. Legacy'nin recheck etmedigi attached collection `Delete`/`DeleteByDBID`/`Refresh`/`Add` ve staged setter davranisi degismedi. Dar IncomingRelays/store filtresi 18/18, full Net10 suite 1045/1045 gecti.
- `IncomingRelays` delete membership containment dilimi, legacy `Collection::DeleteItemByDBID` davranisini esleyerek configured delete store cagrisi oncesinde target ID'nin owning snapshot'ta bulunmasini zorunlu kildi. Unknown DBID ve collection'dan cikarilmis stale `IncomingRelay` facade uzerinden tekrar `Delete`, persistence'a ulasmadan no-op kaliyor. Authenticated `Settings.IncomingRelays`, direct activation `E_ACCESSDENIED`, SQL shape, collection-index `Delete`, `Add`/`Save`, SMTP trust ve live reconfiguration degismedi. Legacy-focused test once 2/13 fail ile gap'i kanitladi; duzeltme sonrasi dar IncomingRelays/store filtresi 17/17 ve full Net10 suite 1044/1044 gecti.
- `SSLCertificates` delete membership containment dilimi, legacy `Collection::DeleteItemByDBID` davranisini esleyerek configured delete store cagrisi oncesinde target ID'nin owning snapshot'ta bulunmasini zorunlu kildi. Unknown DBID ve collection'dan cikarilmis stale `SSLCertificate` facade uzerinden tekrar `Delete`, persistence'a ulasmadan no-op kaliyor. Authenticated Settings boundary, direct activation `E_ACCESSDENIED`, SQL shape, `Clear`/`Add`/`Save`, certificate/private-key file scope, TCP/IP listener ve live TLS davranisi degismedi. Legacy-focused test once 2/11 fail ile gap'i kanitladi; duzeltme sonrasi dar SSL COM/store filtresi 16/16 ve full Net10 suite 1044/1044 gecti.
- SEC-11 `Settings -> SSLCertificates` mutation authorization audit'i, runtime store configured olsa bile direct `SSLCertificates`/`SSLCertificate` activation'inin `DeleteByDBID`, `Add`, `Refresh`, `Clear`, tum item setter'lari, `Save` ve `Delete` icin `E_ACCESSDENIED` kaldigini ve store read/mutation sayaçlarinin sifir oldugunu kanitladi. Mutation delegate'leri yalniz authenticated Settings-bound adapter uzerinden ulasilabilir; production-code gap veya yeni mutator yok. Hedefli attack-path testi 1/1, dar SSL COM/store filtresi 16/16 ve full Net10 suite 1044/1044 gecti.
- SEC-11 `Route -> Addresses` ownership boundary dilimi, legacy `Collection::DeleteItemByDBID` davranisini esleyerek `DeleteByDBID` store cagrisi oncesinde owning collection membership'i zorunlu kildi. Cross-route/unknown DBID ve stale item facade uzerinden tekrarlanan `Delete` store'a ulasmadan no-op kaliyor; `DeleteByAddress` ve item `Delete` ayni parent-owned yolu kullaniyor, SQL store'un `routeaddressrouteid = @RouteId AND routeaddressid = @Id` defense-in-depth scope'u korundu. Direct activation `E_ACCESSDENIED`, yeni mutator acilmadi, routing/live reconfiguration degismedi. Legacy-focused test once 2/8 fail ile gap'i kanitladi; duzeltme sonrasi dar RouteAddresses/store filtresi 10/10 ve full Net10 suite 1044/1044 gecti.
- `IncomingRelay` invalid-IP setter/save parity dilimi, authenticated `Settings -> IncomingRelays` yolunda existing item ve unsaved `Add()` item icin legacy setter success/save timing davranisini esledi. `LowerIP`/`UpperIP` malformed no-colon degerleri `0.0.0.0`, malformed colon-containing degerleri `::` fallback'ine setter aninda normalize ediyor; `Save` configured update/insert delegate uzerinden basarili olunca owning snapshot guncelleniyor veya append ediliyor. Installed IID/vtable/DISPID shape, direct activation `E_ACCESSDENIED`, valid Add/new-item insert behavior ve contained failure snapshot semantics korunuyor. SMTP trust behavior, live reconfiguration ve genis relay mutation degismedi. Dar IncomingRelays/store filtresi 17/17 ve full Net10 suite 1044/1044 gecti.
- `IncomingRelays.Add` plus new-item `IncomingRelay.Save` insert parity dilimi, authenticated `Settings -> IncomingRelays` yolunda installed collection DISPID 5'i acti. `Add` owning collection'a scoped unsaved item facade donduruyor; setter'lar name/lower-IP/upper-IP degerlerini stage ediyor; `Save` tek `hm_incoming_relays` row'unu parametreli `INSERT ... OUTPUT INSERTED.relayid` ile yazip generated ID'yi item'a atiyor ve authorized collection snapshot'ine yalniz yeni relay'i ekliyor. Direct activation `E_ACCESSDENIED`, insert delegate olmayan authorized collection `Add` `E_NOTIMPL`; contained insert failure `E_FAIL` ile item'i ID 0 birakip collection snapshot'ini koruyor. Invalid-IP setter/save parity, SMTP trust behavior, live reconfiguration ve genis relay mutation degismedi. Prereq kontrolu temizdi; dar IncomingRelays/store filtresi 15/15 ve full Net10 suite 1042/1042 gecti.
- `IncomingRelay.Save` existing-row mutation parity dilimi, authenticated `Settings -> IncomingRelays` yolunda installed setter DISPIDs 1/2/3 ve item `Save` DISPID 5'i existing-row update icin acti. Authorized item setter'lari name/lower-IP/upper-IP degerlerini item facade uzerinde stage ediyor; `Save` matching `hm_incoming_relays` row'unu parametreli `UPDATE` ile yaziyor, IPv4/IPv6 stringlerini legacy iki kolonlu SQL storage'a ceviriyor ve owning collection snapshot'inde yalniz o row'u guncelliyor. Direct activation `E_ACCESSDENIED`, save delegate olmayan test-only authorized item setter/Save `E_NOTIMPL`; contained save failure `E_FAIL` ile onceki collection snapshot'ini koruyor. `Add`, SMTP trust behavior, live reconfiguration ve genis relay mutation degismedi. Dar IncomingRelays/store/integration filtresi 14/14, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1040/1040 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `IncomingRelays.Delete(index)` authenticated collection mutation parity dilimi, authenticated `Settings -> IncomingRelays` yolunda installed DISPID 2'yi mevcut delete-by-ID operasyonuna bagladi. Collection index invalid ise `DISP_E_BADINDEX`; valid indexte delete delegate yoksa `E_NOTIMPL`; basarida yalniz indexed relay row'u snapshot'tan kalkiyor, contained failure `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`; `Add`, `Save`/setter'lar, SMTP trust behavior, live reconfiguration ve genis relay mutation degismedi. Dar IncomingRelays/store/integration filtresi 12/12, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1038/1038 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `IncomingRelay.Delete` authenticated item mutation parity dilimi, authenticated `Settings -> IncomingRelays` yolunda installed item DISPID 4'u mevcut delete-by-ID operasyonuna bagladi. Authorized collection'dan donen item facade'i owning collection `DeleteByDBID` yolunu kullaniyor; basarida yalniz matching row snapshot'tan kalkiyor, contained failure `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, delete delegate olmayan test-only authorized item `E_NOTIMPL`; `Add`, collection index `Delete`, `Save`/setter'lar, SMTP trust behavior, live reconfiguration ve genis relay mutation degismedi. Dar IncomingRelays/store/integration filtresi 11/11, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1037/1037 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `IncomingRelays.DeleteByDBID` authenticated collection mutation parity dilimi, authenticated `Settings -> IncomingRelays` yolunda installed DISPID 3'u narrow SQL store operasyonuna bagladi. Store parametreli `DELETE FROM hm_incoming_relays WHERE relayid = @id` calistiriyor; COM basarida authorized collection snapshot'inden yalniz matching row'u atomik kaldiriyor, missing ID no-op kaliyor, contained failure'da `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, delete delegate olmayan test-only authorized snapshot `E_NOTIMPL`; `Add`, collection index `Delete`, item `Delete`, `Save`/setter'lar, SMTP trust behavior, live reconfiguration ve genis relay mutation degismedi. Dar IncomingRelays/store/integration filtresi 10/10, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1036/1036 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `RouteAddresses.DeleteByAddress` authenticated collection mutation parity dilimi, authenticated `Route -> Addresses` yolunda installed DISPID 4'u legacy case-insensitive first-match collection lookup olarak acti ve mevcut secili-route-scoped delete-by-ID operasyonunu kullandi. Missing address no-op kaliyor; basarida yalniz ilk matching row snapshot'tan kalkiyor, contained failure `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`; `Add`, `Save`/setter'lar, route matching behavior, live reconfiguration ve genis route mutation degismedi. Dar RouteAddresses/store/integration filtresi 11/11, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1034/1034 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `RouteAddress.Delete` authenticated item mutation parity dilimi, authenticated `Route -> Addresses` yolunda installed item DISPID 5'i mevcut secili-route-scoped delete-by-ID operasyonuna bagladi. Authorized collection'dan donen item facade'i owning collection `DeleteByDBID` yolunu kullaniyor; basarida yalniz matching row snapshot'tan kalkiyor, contained failure `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, delete delegate olmayan test-only authorized item `E_NOTIMPL`; `Add`, `DeleteByAddress`, `Save`/setter'lar, route matching behavior, live reconfiguration ve genis route mutation degismedi. Dar RouteAddresses/store/integration filtresi 10/10, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1033/1033 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `RouteAddresses.DeleteByDBID` authenticated collection mutation parity dilimi, authenticated `Route -> Addresses` yolunda installed DISPID 2'yi route-scoped SQL store operasyonuna bagladi. Store parametreli `DELETE FROM hm_routeaddresses WHERE routeaddressrouteid = @RouteId AND routeaddressid = @Id` calistiriyor; COM basarida authorized collection snapshot'inden yalniz matching row'u atomik kaldiriyor, secili route disi DBID store seviyesinde no-op kaliyor, contained failure'da `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, delete delegate olmayan test-only authorized snapshot `E_NOTIMPL`; `Add`, `DeleteByAddress`, item `Delete`, `Save`/setter'lar, route matching behavior, live reconfiguration ve genis route mutation degismedi. Dar RouteAddresses/store/integration filtresi 9/9, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1032/1032 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `ServerMessage.Save` existing-row mutation parity dilimi, authenticated `Settings -> ServerMessages` yolunda installed setter DISPIDs 2/4 ve `Save` DISPID 3'u existing-row update icin acti. Authorized item setter'lari name/text degerlerini item facade uzerinde stage ediyor; `Save` matching `hm_servermessages` row'unu parametreli `UPDATE` ile yaziyor ve owning collection snapshot'inde yalniz o row'u guncelliyor. Direct activation `E_ACCESSDENIED`, save delegate olmayan test-only authorized item setter/Save `E_NOTIMPL`; contained save failure `E_FAIL` ile onceki collection snapshot'ini koruyor. Delivery template execution, insert/delete, live reload ve genis Settings/Admin mutation degismedi. Dar ServerMessages/store/integration filtresi 10/10, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1030/1030 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SSLCertificates.Add` + new-item `SSLCertificate.Save` insert parity dilimi, authenticated `Settings -> SSLCertificates` yolunda installed collection DISPID 3'u acti. `Add` owning collection'a scoped unsaved item facade donduruyor; setter'lar scalar degerleri stage ediyor; `Save` tek `hm_sslcertificates` row'unu parametreli `INSERT ... OUTPUT INSERTED.sslcertificateid` ile yazip generated ID'yi item'a atiyor ve authorized collection snapshot'ine ekliyor. Direct activation `E_ACCESSDENIED`, insert delegate olmayan authorized collection `Add` `E_NOTIMPL`; contained insert failure `E_FAIL` ile item'i ID 0 birakip collection snapshot'ini koruyor. Existing-row update/delete davranisi, certificate/private-key file read, certificate validation/loading, TCP/IP listener reconfiguration, live TLS reload ve genis TLS runtime degismedi. Dar SSLCertificates/store/integration filtresi 17/17, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1028/1028 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SSLCertificate.Save` existing-row mutation parity dilimi, authenticated `Settings -> SSLCertificates` yolunda installed setter DISPIDs 2/4/5 ve `Save` DISPID 3'u existing-row update icin acti. Authorized item setter'lari name/certificate-file/private-key-file degerlerini item facade uzerinde stage ediyor; `Save` matching `hm_sslcertificates` row'unu parametreli `UPDATE` ile yaziyor ve owning collection snapshot'inde yalniz o row'u guncelliyor. Direct activation `E_ACCESSDENIED`, save delegate olmayan test-only authorized item setter/Save `E_NOTIMPL`; contained save failure `E_FAIL` ile onceki collection snapshot'ini koruyor. `SSLCertificates.Add`, insert, certificate/private-key file read, certificate validation/loading, TCP/IP listener reconfiguration, live TLS reload ve genis TLS runtime degismedi. Dar SSLCertificates/store/integration filtresi 15/15, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1026/1026 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SSLCertificate.Delete` authenticated item mutation parity dilimi, authenticated `Settings -> SSLCertificates` yolunda installed item DISPID 6'yi existing delete-by-ID store operasyonu uzerinden acti. Authorized collection'dan donen item facade'i owning collection `DeleteByDBID` yolunu kullaniyor; basarida yalniz matching row snapshot'tan kalkiyor, contained failure `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, delete delegate olmayan test-only authorized item `E_NOTIMPL`; add/save/setters, certificate/private-key file read, certificate validation/loading, TCP/IP listener reconfiguration ve genis TLS runtime degismedi. Dar SSLCertificates/store/integration filtresi 13/13, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1024/1024 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SSLCertificates.DeleteByDBID` authenticated collection mutation parity dilimi, authenticated `Settings -> SSLCertificates` yolunda installed DISPID 2'yi narrow SQL store operasyonuna bagladi. Store parametreli `DELETE FROM hm_sslcertificates WHERE sslcertificateid = @id` calistiriyor; COM basarida authorized collection snapshot'inden yalniz matching row'u atomik kaldiriyor, missing ID no-op kaliyor, contained failure'da `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, delete delegate olmayan test-only authorized snapshot `E_NOTIMPL`; add/save/setters, item `Delete`, certificate/private-key file read, certificate validation/loading, TCP/IP listener reconfiguration ve genis TLS runtime degismedi. Dar SSLCertificates/store/integration filtresi 12/12, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1023/1023 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SSLCertificates.Clear` authenticated collection mutation parity dilimi, authenticated `Settings -> SSLCertificates` yolunda installed DISPID 7'yi narrow SQL store operasyonuna bagladi. Store yalniz `DELETE FROM hm_sslcertificates` calistiriyor; COM clear basarisinda authorized collection snapshot'ini atomik bosaltiyor, contained failure'da `E_FAIL` ile onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, clear delegate olmayan test-only authorized snapshot `E_NOTIMPL`; add/delete-by-DBID/save/setters, certificate/private-key file read, certificate validation/loading, TCP/IP listener reconfiguration ve genis TLS runtime degismedi. Dar SSLCertificates/store/integration filtresi 10/10, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1021/1021 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `AntiVirus.TestCustomerScanner` runtime-boundary parity dilimi, authenticated `Settings -> AntiVirus` yolunda installed DISPID 16'yi dar process/file runtime uzerinden acti. Runtime clean/EICAR `.eml` test dosyalari uretiyor, legacy `%FILE%` substitution shape'ini ve SEC-04 quote/escaping hardening'ini koruyup command'i shell-free structured `ProcessStartInfo` argument'larina ayiriyor, test dosyalarini siliyor ve COM yalniz success flag + result text donduruyor; direct activation `E_ACCESSDENIED`, runtime olmayan test-only authorized snapshot `E_NOTIMPL`, contained runtime failure `E_FAIL`. Antivirus setter, persisted setting, SMTP/external-fetch virus scanning behavior, live scanner reconfiguration ve genis custom-scanner policy degismedi. Dar AntiVirus/custom-scanner/settings integration filtresi 23/23, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1019/1019 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `AntiVirus.TestClamWinScanner` runtime-boundary parity dilimi, authenticated `Settings -> AntiVirus` yolunda installed DISPID 17'yi dar process/file runtime uzerinden acti. Runtime configured data/temp directory altinda clean/EICAR `.eml` test dosyalari uretiyor, caller executable'ini structured ClamWin `--database`, filename ve `--tempdir` argument'lariyla calistiriyor, test dosyalarini siliyor ve COM yalniz success flag + result text donduruyor; direct activation `E_ACCESSDENIED`, runtime olmayan test-only authorized snapshot `E_NOTIMPL`, contained runtime failure `E_FAIL`. Antivirus setter, persisted setting, SMTP/external-fetch virus scanning behavior, custom scanner command-template execution, live scanner reconfiguration ve genis process policy degismedi. Dar AntiVirus/ClamWin/settings integration filtresi 18/18, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1014/1014 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `AntiVirus.TestClamAVScanner` runtime-boundary parity dilimi, authenticated `Settings -> AntiVirus` yolunda installed DISPID 18'i existing time-bounded ClamAV INSTREAM client uzerinden acti. Runtime clean payload ve EICAR payload check'lerini yapiyor; COM yalniz success flag + result text donduruyor; direct activation `E_ACCESSDENIED`, runtime olmayan test-only authorized snapshot `E_NOTIMPL`, contained runtime failure `E_FAIL`. Antivirus setter, persisted setting, SMTP/external-fetch virus scanning behavior, file/process scanner tests ve genis network/egress policy degismedi. Dar AntiVirus/ClamAV/settings integration filtresi 16/16, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1010/1010 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `AntiSpam.TestSpamAssassinConnection` runtime-boundary parity dilimi, authenticated `Settings -> AntiSpam` yolunda installed DISPID 36'yi existing time-bounded SpamAssassin client uzerinden acti. Runtime legacy GTUBE test payload'ini gonderiyor ve COM yalniz success flag + result text donduruyor; direct activation `E_ACCESSDENIED`, runtime olmayan test-only authorized snapshot `E_NOTIMPL`, contained runtime failure `E_FAIL`. Anti-spam setter, persisted setting, SMTP spam policy wiring, SpamAssassin message scanning behavior ve genis network/egress policy degismedi. Dar AntiSpam/SpamAssassin/settings integration filtresi 19/19, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1007/1007 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Cache` runtime statistics parity dilimi, authenticated `Settings -> Cache` yolunda installed hit-rate/current-size/max-size getter'larini ayni process-local cache runtime boundary uzerinden acti. Host runtime saglarsa statistics geliyor; runtime yoksa rewrite shared admin cache doldurmadigi icin deterministic zero degerler donuyor. Direct activation `E_ACCESSDENIED`; contained runtime failure `E_FAIL`; `Cache.Clear`, cache setter, persistence, SQL write ve live cache reconfiguration degismedi. Dar Cache/Settings/store/integration filtresi 27/27, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1005/1005 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Cache.Clear` authenticated operation parity dilimi, authenticated `Settings -> Cache` yolunda installed DISPID 8'i process-local runtime boundary uzerinden acti. Host runtime saglarsa `.NET 10` cache state temizleniyor; runtime yoksa mevcut rewrite shared admin cache doldurmadigi icin no-op basarili donuyor. Direct activation `E_ACCESSDENIED`; contained runtime failure `E_FAIL`; hit-rate/current-size/max-size stats, cache setter, persistence, SQL write ve live cache reconfiguration kapsam disi. Dar Cache/Settings/store/integration filtresi 27/27, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1005/1005 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `IMAPFolderPermissions.Refresh` read-only collection parity dilimi, authenticated public `IMAPFolder -> Permissions` yolundaki authorized collection'i selected public folder icin existing IMAP folder administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID/name lookup, item getter'lari ve read-only `Account`/`Group` child access refreshed ACL row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, private account-folder permissions mevcut public-folder-only COM error, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/setter, ACL policy/runtime change ve broader mutation kapsam disi. Dar IMAPFolderPermissions/IMAPFolders/store/integration filtresi 18/18, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1004/1004 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `WhiteListAddresses.Refresh` read-only collection parity dilimi, authenticated `Settings -> AntiSpam -> WhiteListAddresses` yolundaki authorized collection'i existing whitelist address administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed whitelist row'lari goruyor; IPv4/IPv6 two-column conversion, lower-IP numeric ordering ve bigint-to-COM-ID projection korunuyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/Clear/setter, SMTP whitelist policy/runtime change ve broader mutation kapsam disi. Dar WhiteListAddresses/store/integration filtresi 11/11, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1003/1003 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `GreyListingWhiteAddresses.Refresh` read-only collection parity dilimi, authenticated `Settings -> AntiSpam -> GreyListingWhiteAddresses` yolundaki authorized collection'i existing greylisting white-address administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID/name lookup ve item getter'lari refreshed greylisting whitelist row'lari goruyor; stored SQL-LIKE lookup ve wildcard conversion korunuyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/setter, greylisting policy/runtime change ve broader mutation kapsam disi. Dar GreyListingWhiteAddresses/store/integration filtresi 10/10, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1002/1002 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Settings -> AntiSpam -> WhiteListAddresses` reauthentication ownership slice (`996025527`), legacy `InterfaceAntiSpam::get_WhiteListAddresses`, `InterfaceWhiteListAddresses` collection access ve `InterfaceWhiteListAddress` item lifetime/access ordering'ini testle kapatti. Failed reauthentication sonrasi new AntiSpam child whitelist store access oncesi `E_ACCESSDENIED` donuyor; retained AntiSpam yeni collection icin one store read yapiyor; once alinmis collection/item getter'lari ek read olmadan usable kaliyor. Direct activation, installed COM identity, item Save/Delete, collection mutation, Refresh, SMTP whitelist policy ve live reconfiguration degismedi. Dar WhiteList/AntiSpam/SQL filter 23/23, full Net10 suite 1066/1066 gecti. Sonraki production-gate slice SEC-18 dedicated-IIS-staging evidence run'i.
- `SURBLServers.Refresh` read-only collection parity dilimi, authenticated `Settings -> AntiSpam -> SURBLServers` yolundaki authorized collection'i existing SURBL server administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID/DNS-host lookup ve item getter'lari refreshed SURBL server row'lari goruyor; missing DNS-host lookup legacy null sonucunu koruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/setter, DNS lookup execution, SMTP policy/runtime change ve broader mutation kapsam disi. Dar SURBLServers/store/integration filtresi 9/9, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1001/1001 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `DNSBlackLists.Refresh` read-only collection parity dilimi, authenticated `Settings -> AntiSpam -> DNSBlackLists` yolundaki authorized collection'i existing DNS blacklist administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID/DNS-host lookup ve item getter'lari refreshed DNS blacklist row'lari goruyor; missing DNS-host lookup legacy null sonucunu koruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/setter, DNS lookup execution, SMTP policy/runtime change ve broader mutation kapsam disi. Dar DNSBlackLists/store/integration filtresi 9/9, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 1000/1000 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `BlockedAttachments.Refresh` read-only collection parity dilimi, authenticated `Settings -> AntiVirus -> BlockedAttachments` yolundaki authorized collection'i existing blocked-attachment administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed blocked-attachment row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/setter, scanner test/runtime execution, attachment-policy runtime change ve broader mutation kapsam disi. Dar BlockedAttachments/store/integration filtresi 9/9, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 999/999 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `ServerMessages.Refresh` read-only collection parity dilimi, authenticated `Settings -> ServerMessages` yolundaki authorized collection'i existing server-message administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/name/DBID lookup ve item getter'lari refreshed server-message row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, delivery-template execution, save/setter, SQL write ve broader mutation kapsam disi. Dar ServerMessages/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 998/998 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `GroupMembers.Refresh` read-only collection parity dilimi, authenticated `Group -> Members` yolundaki authorized collection'i selected parent group ID icin existing group-member administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup, item getter'lari ve read-only `Account` child access refreshed group-member row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete-by-DBID/save/delete/setter, account child mutation, ACL behavior integration, membership recalculation ve broader mutation kapsam disi. Dar GroupMembers/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 997/997 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Groups.Refresh` read-only collection parity dilimi, authenticated `Settings -> Groups` yolundaki authorized collection'i existing group administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/name/DBID lookup ve item getter'lari refreshed group row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, group-member refresh, ACL behavior integration, membership recalculation ve broader mutation kapsam disi. Dar Groups/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 996/996 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SSLCertificates.Refresh` read-only collection parity dilimi, authenticated `Settings -> SSLCertificates` yolundaki authorized collection'i existing SSL certificate administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed SSL certificate row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/Clear/save/setter, certificate/private-key file read, certificate validation/loading, TCP/IP port reconfiguration ve broader mutation kapsam disi. Dar SSLCertificates/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 995/995 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `TCPIPPorts.Refresh` read-only collection parity dilimi, authenticated `Settings -> TCPIPPorts` yolundaki authorized collection'i existing TCP/IP port administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed TCP/IP port row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/SetDefault/save/setter, listener reconfiguration, certificate loading/validation, live binding change ve broader mutation kapsam disi. Dar TCPIPPorts/store/integration filtresi 9/9, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 994/994 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `SecurityRanges.Refresh` read-only collection parity dilimi, authenticated `Settings -> SecurityRanges` yolundaki authorized collection'i existing security-range administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/name/DBID lookup ve item getter'lari refreshed security-range row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/SetDefault/save/setter, IP policy/auto-ban behavior, live reconfiguration ve broader mutation kapsam disi. Dar SecurityRanges/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 993/993 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `IncomingRelays.Refresh` read-only collection parity dilimi, authenticated `Settings -> IncomingRelays` yolundaki authorized collection'i existing incoming-relay administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/name/DBID lookup ve item getter'lari refreshed incoming-relay row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, SMTP trust/policy behavior, live reconfiguration ve broader mutation kapsam disi. Dar IncomingRelays/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 992/992 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Routes.Refresh` read-only collection parity dilimi, authenticated `Settings -> Routes` yolundaki authorized collection'i existing route administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/name/DBID lookup ve item getter'lari refreshed route row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, route-address refresh, live routing behavior, credential handling ve broader mutation kapsam disi. Dar Routes/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 991/991 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `DistributionLists.Refresh` read-only collection parity dilimi, authenticated `Domain -> DistributionLists` yolundaki authorized collection'i selected parent domain ID icin existing distribution-list administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/address/DBID lookup ve item getter'lari refreshed distribution-list row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, recipient refresh ve broader mutation kapsam disi. Dar DistributionLists/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 990/990 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `DomainAliases.Refresh` read-only collection parity dilimi, authenticated `Domain -> DomainAliases` yolundaki authorized collection'i selected parent domain ID icin existing domain-alias administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed domain-alias row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, alias refresh ve broader mutation kapsam disi. Dar DomainAliases/store/integration filtresi 8/8, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 989/989 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Aliases.Refresh` read-only collection parity dilimi, authenticated `Domain -> Aliases` yolundaki authorized collection'i selected parent domain ID icin existing alias administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/name/DBID lookup ve item getter'lari refreshed alias row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, domain-alias refresh ve broader mutation kapsam disi. Dar Aliases/store/integration filtresi 13/13, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 988/988 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
 - `RuleActions.Refresh` read-only collection parity dilimi, authenticated `Rule -> Actions` yolundaki authorized collection'i selected parent rule ID icin existing rule action administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed action row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, reordering, rule execution behavior ve broader mutation kapsam disi. Dar RuleActions/store/integration filtresi 15/15, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 987/987 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `RuleAction.Save` existing-row parity dilimi (`5e24a9bda`), legacy `InterfaceRuleAction::Save` ve `PersistentRuleAction::SaveObject` davranisini current authenticated administrator-owned `Rule -> Actions` boundary'ne bagliyor. Index/DBID facade Save'i owning rule snapshot'indaki tum persisted action alanlarini parameterized `actionruleid` + `actionid` update ile yaziyor; contained store failure `E_FAIL`, readable item retention ve retry korunuyor. Direct activation `E_ACCESSDENIED`; legacy script-action administrator guard mevcut root boundary ile korunuyor. Add/new-item Save, setters, MoveUp/MoveDown, reordering, rule execution, SMTP/delivery behavior ve broader mutation degismedi. Dar COM/store filtresi 23/23, full Net10 native integration 1147/1147; live SQL integration bu turda kurulmadi.
- `RuleAction.Type` existing-row setter parity dilimi (`8b608f396`), legacy `InterfaceRuleAction::put_Type` detached-object denial'i ve `eRARunScriptFunction` icin `GetIsServerAdmin()` guard'ini current authorized RuleActions facade'na tasiyor. Store-backed owning item Type enum'ini stage ediyor, `RunScriptFunction` non-admin predicate ile `E_ACCESSDENIED` donuyor ve existing-row Save staged snapshot'i yaziyor; read-only/test-only facade'lar eski `E_NOTIMPL` boundary'sini koruyor. Add/new-item Save, RuleID/remaining setters, MoveUp/MoveDown, reordering, rule execution, SMTP/delivery behavior ve broader mutation degismedi. Dar RuleActions/Rules/store filtresi 25/25, full Net10 native integration 1149/1149; live SQL integration bu turda kurulmadi.
- `RuleAction.ScriptFunction` existing-row setter parity dilimi (`47f33df20`), legacy `InterfaceRuleAction::put_ScriptFunction` detached-object ve non-admin `E_ACCESSDENIED` guard'ini current authorized RuleActions facade'na tasiyor. Raw string validation/normalization yapmadan owning item snapshot'ini stage ediyor ve existing-row Save ile mevcut parameterized `actionscriptfunction` update yoluna yaziyor; direct activation, read-only/test-only `E_NOTIMPL`, Add/new-item Save, RuleID/remaining setters, ordering, rule execution, SMTP/delivery behavior ve broader mutation degismedi. Dar RuleActions/Rules/store filtresi 27/27; full Net10 1149/1151, iki alakasiz ClamWin/CustomScanner generated `.eml` cleanup `UnauthorizedAccessException` ile kaldi; live SQL integration bu turda kurulmadi.
- `RuleCriterias.Refresh` read-only collection parity dilimi, authenticated `Rule -> Criterias` yolundaki authorized collection'i selected parent rule ID icin existing rule criteria administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed criterion row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, Add, Save/setter, action refresh, rule execution behavior ve broader mutation kapsam disi. Dar RuleCriterias/store/integration filtresi 15/15, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 986/986 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi. Index and item deletion are recorded in the SEC-11 entries above.
- `Application -> Rules.Refresh` read-only collection parity dilimi, authenticated server-admin global `Application -> Rules` yolundaki authorized collection'i `ruleaccountid = 0` icin existing rule administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed global row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, add/delete/save/setter, account-rule behavior change, criteria/actions refresh ve rule execution behavior kapsam disi. Dar Rules/Application/store/integration filtresi 18/18, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 985/985 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Account -> Rules.Refresh` read-only collection parity dilimi, authenticated account-scoped `Account -> Rules` yolundaki authorized collection'i selected parent account ID icin existing rule administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, criteria/actions refresh, rule execution behavior ve broader mutation kapsam disi. Dar Rules/Application/store/integration filtresi 18/18, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 985/985 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `FetchAccounts.Refresh` read-only collection parity dilimi, authenticated `Account -> FetchAccounts` yolundaki authorized collection'i selected parent account ID icin existing fetch-account administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/DBID lookup ve item getter'lari refreshed non-secret row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, delete/download/password/save/setter, external POP3 execution ve broader mutation kapsam disi. Dar FetchAccounts/store/integration filtresi 12/12, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 984/984 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Accounts.Refresh` read-only collection parity dilimi, authenticated `Domain -> Accounts` yolundaki authorized collection'i selected parent domain ID icin existing account administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, index/address/DBID lookup ve item getter'lari refreshed row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter, password/AD/auth behavior ve broader mutation kapsam disi. Dar Accounts/store/integration filtresi 17/17, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 983/983 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Domains.Refresh` read-only collection parity dilimi, authenticated `Application -> Domains` yolundaki authorized collection'i existing domain administration store'dan yeniden yukleyip process-local snapshot'i atomik degistiriyor. `Count`, `Names`, index/name/DBID lookup ve item getter'lari refreshed row'lari goruyor; contained store failure `E_FAIL` ile donup onceki snapshot'i koruyor. Direct activation `E_ACCESSDENIED`, reload delegate olmayan test-only authorized snapshot `E_NOTIMPL`, add/delete/save/setter ve broader mutation kapsam disi. Dar domain/Application/store/integration filtresi 19/19, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 982/982 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Utilities.ImportMessageFromFileToIMAPFolder` folder-resolution parity follow-up'i missing named private/public folder path'lerini serializable SQL transaction icinde legacy `folderissubscribed=1`, `foldercurrentuid=0` ve current creation time ile olusturuyor; bos folder/InBox fallback ise legacy gibi existing-only kaliyor. Existing public destination inherited `Insert` ACL isterken missing public path global-rule create bypass'ini koruyor. Public folder UID'si folder account `0` uzerinden allocate ediliyor, imported message kaynak account ID'sini koruyor. Installed DISPID 13/`VARIANT_BOOL`, server-admin check, data-directory/lookup/GUID-bucket/token/queue davranislari, contained `false`, direct activation `E_ACCESSDENIED` ve unrelated scope sinirlari degismedi. Dar runtime/store/Utilities/integration filtresi 25/25, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 981/981 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Utilities.ImportMessageFromFileToIMAPFolder` compatibility dilimi, authenticated `Application -> Utilities` yolunu mevcut service-injected import runtime/store'una bagladi. Installed DISPID 13/`VARIANT_BOOL` ve server-admin check korundu; mevcut `ImportMessageFromFile` validation/lookup/GUID-bucket normalization davranisi yeniden kullaniliyor, bos folder string legacy gibi Inbox'e dusuyor ve `accountId == 0` hala queue yolunu kullaniyor. `accountId > 0` icin legacy `%YEAR%`/`%MONTH%`/`%DAY%` token cleanup'i ve bir leading hierarchy delimiter trim'i uygulanip existing private IMAP folder path'i modified UTF-7 segmentleriyle cozuluyor, destination folder UID allocate edilip delivered persistence oraya yaziliyor. Contained failure `false`, runtime yoklugu `E_NOTIMPL`, direct activation `E_ACCESSDENIED`; automatic folder creation, public-folder semantics, external path ve rules/spam/virus eklenmedi. Dar folder-import/runtime-store-contract/integration filtresi 32/32, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 978/978 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi.
- `Utilities.ImportMessageFromFile` compatibility dilimi, authenticated `Application -> Utilities` yolunu service-injected runtime/store'a bagladi. Installed DISPID 8/`VARIANT_BOOL` ve server-admin check korundu; yalniz configured data directory altindaki existing source file kabul ediliyor, legacy partial-first sonra exact persisted-message lookup ve already-partial `true` davranisi korunuyor. Existing full-path row ve misplaced in-tree file legacy GUID/bucket filename'e normalize edilip persistence update ediliyor; `accountId > 0` dosyalari MIME sender/size/internal-date parse edilerek Inbox/UID yoluna delivered olarak yaziliyor, `accountId == 0` dosyalari ise yalniz local To/CC recipient'lariyla queue'ya yazilip durable commit sonrasinda wake ediliyor. Store/runtime icindeki contained failure `false`, runtime yoklugu `E_NOTIMPL`, direct activation `E_ACCESSDENIED`; IMAP-folder overload, external path ve rules/spam/virus eklenmedi. Dar import/runtime-store-contract/integration filtresi 34/34, prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 975/975 gecti. Opt-in SQL connection unset oldugu icin gercek SQL integration kurulumu bu turda calismadi. Ayni landing, WSH `CheckSyntax` yolunu temp-copy uzerinden calistirip source-script file lock race'ini kapatti; full suite artik cleanup kilidine takilmiyor.
- `Utilities.EmailAllAccounts` compatibility dilimi, authenticated `Application -> Utilities` yolunu service-injected recipient store ve existing signaling queue writer'a bagladi. Installed DISPID 9/`VARIANT_BOOL`, account-address order, active account + active address-derived domain filtresi ve legacy case-insensitive `*`/`?` wildcard korundu; local recipient ID'leriyle tek legacy-shaped plain-text MIME mesaj empty envelope sender kullanarak persist ediliyor ve delivery durable write sonrasinda wake ediliyor. Explicit request flag legacy zero-recipient queue creation davranisini korurken normal SMTP empty-recipient guard'ini gevsetmiyor. Store/write failure `false`, runtime yoklugu `E_NOTIMPL`, direct activation `E_ACCESSDENIED`; SMTP validation/rules/spam/virus, remote expansion, per-recipient message ve import eklenmedi. Dar runtime/store/Utilities/integration/signaling filtresi 29/29, prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 965/965 gecti. Opt-in SQL connection unset oldugu icin izole gercek DB kurulumu bu turda calismadi.
- `Utilities.MakeDependent` compatibility dilimi, authenticated `Application -> Utilities` yolunu service-injected Windows SCM runtime'a bagladi. Installed DISPID 7/vtable ve server-admin check korundu; yalniz local `hMailServer` servisi hedeflenip dependency multi-string legacy sirayla `RPCSS` + caller-supplied service ve double-NUL terminator olarak degistiriliyor. Native return caller-visible void success olarak kaliyor; runtime yoklugu `E_NOTIMPL`, runtime exception `E_FAIL`, direct activation `E_ACCESSDENIED`. SCM/service/lock handle'lari kapatiliyor; start/stop/install/delete, binary path/start type/account/display name, registry/DB/file write ve existing dependency append eklenmedi. Dar runtime/Utilities/COM-host filtresi 17/17, prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 958/958 gecti.
- `Utilities.PerformMaintenance(UpdateImapFolderUid)` compatibility dilimi, authenticated `Application -> Utilities` yolunu service-injected SQL store'a bagladi. Installed DISPID 19/vtable ve maintenance enum GUID/value `1` korundu; `hm_messages` folder gruplarinin `MAX(messageuid)` degerleri okunup non-positive legacy row operation failure oluyor ve yalniz daha dusuk matching `hm_imapfolders.foldercurrentuid` degerleri parameterized update ile ileri aliniyor. Unknown operation/store false/exception `E_FAIL`, runtime yoklugu `E_NOTIMPL`, direct activation `E_ACCESSDENIED`; message UID/file, yuksek folder UID, scheduling, import ve unrelated table mutation eklenmedi. Dar Utilities/store/integration filtresi 19/19, prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 954/954 gecti. Opt-in SQL connection unset oldugu icin izole gercek DB kurulumu bu turda calismadi.
- `Utilities.RetrieveMessageID` compatibility dilimi, authenticated `Application -> Utilities` yolunu service-injected read-only resolver'a bagladi. Installed DISPID 17/vtable ve 64-bit `hyper` return korundu; server-admin check sonrasinda configured data-directory full path'i legacy kurallarla partial filename'e cevrilip once partial, sonra exact supplied filename parameterized `hm_messages.messagefilename` lookup'unda araniyor ve missing row `0` donuyor. Direct activation `E_ACCESSDENIED`; message file read/existence check, arbitrary canonicalization, join/content access ve SQL/file mutation eklenmedi. Dar contract/resolver/store/integration filtresi 28/28, prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 950/950 gecti. Opt-in SQL connection unset oldugu icin izole gercek DB kurulumu bu turda calismadi.
- `Utilities.GetMailServer` compatibility dilimi, unauthenticated `Application -> Utilities` ve service-hosted direct Utilities yollarini ortak injected resolver'a bagladi. Installed DISPID 1/vtable ve BSTR metadata korundu; son `@` sonrasi domain icin system DNS uzerinden MX, null-MX, bounded CNAME ve no-MX implicit A/AAAA fallback, IPv4-first/IPv6-available sirasi, partial target success ve first-seen IP de-dup uygulanip bosluksuz virgullu IP listesi veya bos string donuyor. SMTP delivery resolver/cache, DB/config, persistent DNS cache, reverse DNS, policy ve mutations degismedi. Dar resolver/raw-DNS/Utilities filtresi 19/19, prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata ve full Net10 suite 943/943 gecti.
- `Utilities.IsLocalHost` compatibility dilimi, unauthenticated `Application -> Utilities` ve service-hosted direct Utilities yollarini ortak injected runtime'a bagladi. Installed DISPID 12/vtable ve `VARIANT_BOOL` metadata korundu; literal IPv4 veya hostname'in yalniz ilk resolved IPv4 adresi current local IPv4 interface listesiyle karsilastiriliyor, unresolved/IPv6-only/empty input `false`, runtime'siz public class construction `E_NOTIMPL` donuyor. DB/config write, security-range/firewall policy, listener degisikligi, persistent cache, reverse DNS ve IPv6 parity eklenmedi. Dar runtime 4/4 ve Utilities contract 6/6 gecti; prereq temiz, Net10 Debug build typelib dahil 0 uyari/0 hata. Prescribed full script 932/935 kaldi ve isolated WSH sinifi 1/4 oldu cunku ayni uc syntax-check testinin cleanup'i `EventHandlers.vbs/js` dosya kilidine denk geldi; WSH sinifi disindaki tum 931 test gecti ve kosu sonunda `cscript`/`wscript` sureci kalmadi.
- `Utilities.BlowfishEncrypt`/`BlowfishDecrypt` compatibility dilimi, unauthenticated `Application -> Utilities` ve service-hosted direct Utilities yollarini existing tested `LegacyBlowfishPasswordCipher` icin injected runtime'a bagladi. Installed DISPIDs 5/6, BSTR metadata, static key/table, Latin-1, lower-case hex, block padding, empty string ve valid ciphertext round-trip korundu; public runtime'siz class construction `E_NOTIMPL`, storage/DB write/key rotation/migration ve SEC-21 durumu degismedi. Dar Utilities/cipher filtresi 12/12, prereq temiz ve Net10 Debug build 0 uyari/0 hata gecti. Full davranis seti syntax-checker haric 927/927 ve syntax-checker 4/4 olarak gecti; prescribed full script iki kez 928/931 kaldi cunku ayni uc WSH syntax testinin cleanup'i `EventHandlers.vbs/js` dosya kilidine denk geldi, testler tek basina geciyor ve kosu sonunda `cscript`/`wscript` sureci kalmiyor.
- `Settings.ClearLogonFailureList` operational dilimi, authenticated `Application -> Settings` yolunda parameterless DISPID 86 call'i dar SQL administration store'a bagladi. Legacy MSSQL `ClearOldFailures(-1)` esigi `failuretime < DATEADD(minute, 1, GETDATE())` olarak korundu; direct activation `E_ACCESSDENIED`, missing runtime `E_NOTIMPL`, security ranges, auto-ban settings/runtime policy ve failed-logon recording degismedi. Dar Settings/store/integration filtresi 28/28, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 930/930 gecti. Opt-in SQL schema/seed/assert genisletildi; `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `AntiSpam.ClearGreyListingTriplets` operational dilimi, authenticated `Application -> Settings -> AntiSpam` yolunda parameterless DISPID 29 call'i dar SQL administration store'a bagladi ve legacy all-row `DELETE FROM hm_greylisting_triplets` davranisini korudu. Direct activation `E_ACCESSDENIED`; whitelist/settings data, greylisting policy, SMTP behavior, cleanup scheduling ve diger AntiSpam mutations degismedi. Dar AntiSpam/store/integration filtresi 20/20, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 929/929 gecti. Opt-in SQL schema/seed/assert genisletildi; `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `AntiSpam.DKIMVerify` operational dilimi, authenticated `Application -> Settings -> AntiSpam` yolunda caller-supplied message file'i injected runtime ile legacy 50 MiB sinirinda Latin-1 okuyup existing message verifier ve time-bounded system DNS resolver'a bagladi. Installed vtable/DISPID, direct activation `E_ACCESSDENIED` ve dort degerli `Neutral`/`Pass`/`TempFail`/`PermFail` enum mapping'i korundu; signing, private-key access, SMTP policy/settings mutation ve arbitrary command execution eklenmedi. Dar AntiSpam/file-runtime/DKIM filtresi 51/51, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 928/928 gecti.
- Service-side live-log provider dilimi, managed `ILogger` kayitlarini existing process-local `Logging` buffer'a yalniz enabled iken level/category/message/exception iceren deterministic CRLF metniyle besliyor; disabled durumda formatter dahi calismiyor. COM destructive read ve 1.000.000 karakter overflow auto-disable aynen kullaniliyor. File logging, persistence, COM provider/filter reconfiguration, protocol transcript reconstruction ve cross-process state eklenmedi. Dar provider/Logging/runtime filtresi 16/16, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 920/920 gecti.
- `Database.UtilGetFileNameByMessageID` read-only dilimi, authenticated `Application -> Database` yolunda parameterized `bigint` ile yalniz `hm_messages.messagefilename` kolonunu okuyor; stored string aynen, missing row legacy bos string donuyor. Per-call server-admin ve direct activation `E_ACCESSDENIED` sinirlari korundu; join, file/path/content access, arbitrary SQL ve mutation eklenmedi. Dar Database/store/integration filtresi 15/15, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 916/916 gecti.
- `Scripting.Reload` compatibility dilimi, authenticated `Settings -> Scripting` yolunda yalniz configured current file'i syntax-check ediyor; non-empty syntax sonucu legacy 5016, contained load exception 5017 event ID ile managed logger'a gidiyor. Managed executor script dosyasini her invocation'da okudugu icin reload sonrasi event guncel icerigi goruyor ve long-lived engine/cache eklenmiyor. Direct activation `E_ACCESSDENIED`; settings persistence, arbitrary path ve COM-thread event execution kapsam disi. Dar Scripting/reloader/executor/Application filtresi 20/20, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 915/915 gecti.
- `Scripting.CheckSyntax` runtime dilimi, authenticated `Settings -> Scripting` yolunda yalniz configured `CurrentScriptFile` icin shell-free ve timeout-bounded Windows Script Host kontrolu aciyor. Missing/empty/valid dosya legacy gibi bos string, compilation/runtime hatasi file-scoped error text donduruyor; VBScript/JScript engine secimi stored language ile case-sensitive korunuyor. Direct activation `E_ACCESSDENIED`; `Reload`, settings persistence, arbitrary paths ve event-executor reconfiguration kapsam disi. Dar Scripting/WSH/Application filtresi 21/21, prereq temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 912/912 gecti.
- `Logging` live-log runtime dilimi, authenticated `Settings -> Logging` yolunda `EnableLiveLogging`, `LiveLoggingEnabled` ve destructive-read `LiveLog` uyelerini ortak thread-safe process-local tampon uzerinden acti. Enable/disable tamponu temizliyor; 1.000.000 karakter siniri asildiginda legacy gibi tampon temizlenip live logging kapanıyor. Installed vtable/DISPIDs ve direct activation `E_ACCESSDENIED` korundu; file read, persisted setting degisikligi, managed logger-provider reconfiguration, cross-process sharing ve genis mutation eklenmedi. Dar contract/runtime/integration filtresi 21/21, prereq kontrolu temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 907/907 gecti.
- `Logging.MaskPasswordsInLog` obsolete parity dilimi, authenticated `Settings -> Logging` yolunda legacy obsolete no-op davranisini deterministic `false` getter ve no-op setter olarak aciyor. Direct activation `E_ACCESSDENIED`; live logging buffer, logger reconfiguration, file read ve persistence degisikligi eklenmedi. Dar `LoggingComContractTests|SettingsComContractTests|SqlServerSettingsAdministrationStoreTests|SqlServerMessageIndexingIntegrationTests` filtresi 38/38, prereq kontrolu temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 904/904 gecti.
- `GroupMember.Account` child facade dilimi, existing `hm_group_members.memberaccountid` degerleri icin authenticated `Group -> Members` yolundan read-only `Account` child objesi aciyor. Lookup onceki non-secret account-by-ID projection'ini kullaniyor; missing account legacy `DISP_E_BADINDEX`, direct activation `E_ACCESSDENIED`, member/account mutations ve membership/ACL policy davranisi kapsam disi kaliyor. Dar `GroupMembersComContractTests|SqlServerGroupMemberAdministrationStoreTests|SqlServerAccountAdministrationStoreTests|AccountsComContractTests|SqlServerMessageIndexingIntegrationTests` filtresi 30/30, prereq kontrolu temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 904/904 gecti.
- `IMAPFolderPermission.Account/Group` principal child facade dilimi, existing `hm_acl` user/group principal ID'leri icin authenticated public-folder permissions yolundan read-only `Account` ve `Group` child objelerini aciyor. Account lookup non-secret by-ID projection kullaniyor; missing/zero principal ID legacy `DISP_E_BADINDEX` donuyor. Account/group mutations, ACL mutation/recalculation/policy davranisi ve SQL write'lari kapsam disi; child object mutations `E_NOTIMPL` kaldi. Dar `IMAPFolderPermissionsComContractTests|SqlServerAccountAdministrationStoreTests|AccountsComContractTests|GroupsComContractTests|SqlServerMessageIndexingIntegrationTests` filtresi 34/34, prereq kontrolu temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 904/904 gecti.
- `IMAPFolder -> SubFolders` read-only dilimi, existing `hm_imapfolders` satirlarini selected parent/account veya public-folder scope'unda `folderid ASC` sirasi ile child `IMAPFolders` collection olarak aciyor. Root collection nested folder'lari disarida tutmaya devam ediyor; `SubFolders` yalniz secili parent ve ayni account/public scope icindeki child folder'lari donduruyor. Folder scalar'lari read-only, legacy modified UTF-7 decode siniri korunuyor, direct activation `E_ACCESSDENIED`, add/delete/save/name/subscription mutation `E_NOTIMPL`, private folder permissions legacy public-folder-only error olarak kaldi. Dar `ImapFoldersComContractTests|SqlServerImapFolderAdministrationStoreTests|SettingsComContractTests|SqlServerMessageIndexingIntegrationTests` filtresi 38/38, prereq kontrolu temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 903/903 gecti.
- `Message` read-only MIME surface dilimi, mevcut authenticated `Account.Messages` / `IMAPFolder.Messages` yolundan controlled file-backed getter'lari acti: `Subject`, `From`, `Date`, `To`, `CC`, `Body`, `HTMLBody`, `HeaderValue`, `HasBodyType`, `Charset`, read-only `Headers`, `Recipients` ve `Attachments`. `Attachments`/`Attachment`, `Recipients`/`Recipient`, `MessageHeaders`/`MessageHeader` COM contract/class identity'leri ve manifest/service registration eklendi. Message file path mevcut data-directory resolver boundary'sinden okunuyor; file yoksa COM `E_FAIL`, content source yoksa eski `E_NOTIMPL` siniri korunuyor. Setter'lar, `Save`, `Copy`, `RefreshContent`, header/recipient/attachment mutations, file writes, delivery/rescan side effect ve genis message mutation `E_NOTIMPL`; direct activation `E_ACCESSDENIED`. Dar `MessagesComContractTests|SqlServerMessageAdministrationStoreTests|LegacyComRegistrationManifestTests` filtresi 19/19, prereq kontrolu temiz, Net10 Debug build 0 uyari/0 hata, full Net10 testleri 901/901 gecti.
- `Account/IMAPFolder -> Messages` placeholder'i complete legacy `Messages`/`Message` contract/class identity, `eMessageFlag` enum GUID/value, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated `Account.Messages` ve `IMAPFolder.Messages` existing delivered `hm_messages` satirlarini selected account/folder scope'unda count/index/DBID lookup olarak aciyor; `Message` safe metadata getter'lari (`ID`, `Filename`, `FromAddress`, `State`, floor-KiB `Size`, `DeliveryAttempt`, `InternalDate`, `UID`, flag reads) read-only ve file okumadan geliyor. Body/header/attachment/file-content access, `Save`, `Delete`, `Clear`, `Refresh`, `Add`, flag mutation, delivery/rescan side effect ve genis message mutation `E_NOTIMPL`; direct activation `E_ACCESSDENIED`. Dar Messages/IMAPFolders/manifest/store/integration filtresi 33/33, full Net10 testleri 899/899 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `Application -> Diagnostics` placeholder'i complete legacy `Diagnostics`/`DiagnosticResults`/`DiagnosticResult` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated `Application.Diagnostics` `LocalDomainName`/`TestDomainName` getter/setter'larini process-local COM state olarak aciyor ve `PerformTests` deterministic runtime boundary uzerinden read-only result collection donduruyor. Direct activation `E_ACCESSDENIED`; actual DNS/SMTP/network/filesystem diagnostics ve genis operational health checks kapsam disi kaldi. Dar Diagnostics/Application/manifest filtresi 17/17, full Net10 testleri 889/889 gecti; Windows service/COM build 0 uyari/0 hata verdi. `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin opt-in gercek SQL integration kurulumu calismadi.
- `IMAPFolder -> Permissions` placeholder'i complete legacy `IMAPFolderPermissions`/`IMAPFolderPermission` contract/class identity, ACL enum GUID/value, manifest ve service class-factory registration ile degistirildi. Authenticated public folder `Permissions` existing `hm_acl` satirlarini selected folder scope'unda `aclid ASC` sirasiyla count/index/DBID/legacy-name lookup olarak aciyor; item ID/share-folder/principal/type/value ve permission-flag getter'lari read-only, son principal facade dilimiyle user/group principal row'lari icin `Account`/`Group` child objeleri de read-only acildi. Account/private folder `Permissions` legacy `0x800403E9` public-folder-only error'ini koruyor. Direct activation `E_ACCESSDENIED`; add/delete/refresh/save, setter'lar, ACL runtime policy degisikligi ve SQL mutations `E_NOTIMPL` kaldi. Dar IMAPFolderPermissions/IMAPFolders/Settings/store/manifest/integration filtresi 35/35, full Net10 testleri 884/884 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `Settings -> Cache` placeholder'i complete legacy `Cache` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated `Settings.Cache` existing `hm_settings` satirlarindan `Enabled`, `DomainCacheTTL`, `AccountCacheTTL`, `AliasCacheTTL` ve `DistributionListCacheTTL` getter'larini read-only aciyor. Direct activation `E_ACCESSDENIED`; hit-rate/current-size/max-size runtime istatistikleri, `Clear`, setter/persistence ve live cache reconfiguration `E_NOTIMPL` kaldi. Dar Cache/Settings/store/manifest/integration filtresi 28/28, full Net10 testleri 877/877 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `Rules -> Actions` placeholder'i complete legacy `RuleActions`/`RuleAction` contract/class identity, versioned ProgID, action-type enum GUID/value, manifest ve service class-factory registration ile degistirildi. Authenticated `Rule.Actions` yalniz selected rule'a ait existing `hm_rule_actions` satirlarini legacy `actionsortorder ASC` sirasiyla count/index/DBID lookup olarak aciyor; persisted action scalar getter'larinin tamami read-only. Rule disi DBID `DISP_E_BADINDEX`, direct activation `E_ACCESSDENIED`; add/delete/save/reordering, rule execution davranis degisikligi ve mutations `E_NOTIMPL` kaldi. Dar RuleActions/Rules/store/manifest/integration filtresi 16/16, full Net10 testleri 872/872 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `Rules -> Criterias` placeholder'i complete legacy `RuleCriterias`/`RuleCriteria` contract/class identity, versioned ProgID, rule-field/match-type enum GUID/value, manifest ve service class-factory registration ile degistirildi. Authenticated `Rule.Criterias` yalniz selected rule'a ait existing `hm_rule_criterias` satirlarini legacy `criteriaid ASC` sirasiyla count/index/DBID lookup olarak aciyor; item scalar getter'lari read-only. Rule disi DBID `DISP_E_BADINDEX`, direct activation `E_ACCESSDENIED`; Add, Save/setter, rule execution davranis degisikligi ve mutations `E_NOTIMPL` kaldi. Dar RuleCriterias/Rules/store/manifest/integration filtresi 16/16, full Net10 testleri 865/865 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi. Authenticated collection index deletion is now recorded in `a209e2cc1` above.
- `Settings -> Routes -> RouteAddresses` placeholder'i complete legacy `RouteAddresses`/`RouteAddress` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated `Route.Addresses` yalniz selected route'a ait existing `hm_routeaddresses` satirlarini yeni sort eklemeden count/index/DBID lookup olarak aciyor; item `ID`, `Address` ve `RouteID` getter'lari read-only. Route disi DBID `DISP_E_BADINDEX`, direct activation `E_ACCESSDENIED`; add/delete/delete-by-address/save, routing davranis degisikligi, live reconfiguration ve mutations `E_NOTIMPL` kaldi. Dar RouteAddresses/Routes/store/manifest/integration filtresi 15/15, full Net10 testleri 858/858 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_CONNECTION` unset oldugu icin gercek DB kurulumu calismadi.
- `Settings -> AntiSpam -> WhiteListAddresses` placeholder'lari complete legacy `WhiteListAddresses`/`WhiteListAddress` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated collection existing `hm_whitelist` satirlarini lower-IP numeric sirasi ile count/index/DBID lookup olarak aciyor; item `ID`, `LowerIPAddress`, `UpperIPAddress`, `EmailAddress` ve `Description` getter'lari read-only. IPv4/IPv6 two-column storage COM string'lerine cevriliyor, SQL `bigint` ID legacy 32-bit COM projection'ini koruyor ve missing lookup `DISP_E_BADINDEX`. Direct activation `E_ACCESSDENIED`; add/delete/save/refresh/clear, SMTP whitelist policy davranisi, live reconfiguration ve mutations `E_NOTIMPL` kaldi. Dar WhiteListAddresses/AntiSpam/store/manifest/integration filtresi 28/28, full Net10 testleri 852/852 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `Settings -> AntiSpam -> GreyListingWhiteAddresses` placeholder'lari complete legacy `GreyListingWhiteAddresses`/`GreyListingWhiteAddress` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated collection existing `hm_greylisting_whiteaddresses` satirlarini `whiteipaddress asc` sirasi ile count/index/DBID/name lookup olarak aciyor; item `ID`, `IPAddress` ve `Description` getter'lari read-only. SQL LIKE pattern COM wildcard string'ine legacy sirayla cevriliyor, SQL `bigint` ID legacy 32-bit COM `long` projection'ini koruyor ve missing lookup `DISP_E_BADINDEX`. Direct activation `E_ACCESSDENIED`; add/delete/save/refresh, greylisting policy davranisi, live reconfiguration ve mutations `E_NOTIMPL` kaldi. Dar GreyListingWhiteAddresses/AntiSpam/store/manifest/integration filtresi 27/27, full Net10 testleri 843/843 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `Settings -> AntiSpam -> SURBLServers` placeholder'lari complete legacy `SURBLServers`/`SURBLServer` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated collection existing `hm_surblservers` satirlarini `surblid asc` sirasi ile count/index/DBID/DNS-host lookup olarak aciyor; item `Active`, `ID`, `DNSHost`, `RejectMessage` ve `Score` getter'lari read-only. Missing index/DBID `DISP_E_BADINDEX`, missing DNS host legacy no-object sonucunu koruyor. Direct activation `E_ACCESSDENIED`; add/delete/save/refresh, DNS lookup/test execution, SMTP policy davranisi, live reconfiguration ve mutations `E_NOTIMPL` kaldi. Dar SURBLServers/AntiSpam/store/manifest/integration filtresi 26/26, full Net10 testleri 835/835 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `Settings -> AntiSpam -> DNSBlackLists` placeholder'lari complete legacy `DNSBlackLists`/`DNSBlackList` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated collection existing `hm_dnsbl` satirlarini `sblid asc` sirasi ile count/index/DBID/DNS-host lookup olarak aciyor; item `Active`, `ID`, `DNSHost`, `RejectMessage`, `ExpectedResult` ve `Score` getter'lari read-only. Missing index/DBID `DISP_E_BADINDEX`, missing DNS host legacy no-object sonucunu koruyor. Direct activation `E_ACCESSDENIED`; add/delete/save/refresh, DNS lookup/test execution, SMTP policy davranisi, live reconfiguration ve mutations `E_NOTIMPL` kaldi. Dar DNSBlackLists/AntiSpam/store/manifest/integration filtresi 26/26, full Net10 testleri 828/828 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `Settings -> AntiSpam` placeholder'i complete legacy `AntiSpam` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated getter'lar existing `hm_settings` scalar satirlarindan geliyor: greylisting enabled/timing/bypass, HELO/PTR/SPF/MX score flags, spam header/subject/threshold policy, SpamAssassin enabled/score/merge/host/port, DKIM verification enabled/failure score ve anti-spam max message size. Obsolete `TarpitDelay`/`TarpitCount` legacy gibi `0` donuyor. Direct activation `E_ACCESSDENIED`; tum setter'lar, DNS/network/file access beyond the bounded operational probes, SMTP policy davranis degisikligi ve live reconfiguration `E_NOTIMPL` kaldi. Dar AntiSpam/Settings/store/manifest filtresi 37/37, full Net10 testleri 821/821 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `Settings -> AntiVirus -> BlockedAttachments` placeholder'lari complete legacy `BlockedAttachments`/`BlockedAttachment` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated collection existing `hm_blocked_attachments` satirlarini `bawildcard asc` sirasi ile count/index/DBID lookup olarak aciyor; item `ID`, `Wildcard` ve `Description` getter'lari read-only. Direct activation `E_ACCESSDENIED`; add/delete/save/refresh, SMTP attachment-policy davranisi, process/network/file access ve mutations `E_NOTIMPL` kaldi. Dar BlockedAttachments/AntiVirus/store/manifest filtresi 26/26, full Net10 testleri 815/815 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL integration seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `Settings -> AntiVirus` placeholder'i complete legacy `AntiVirus` contract/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated getter'lar existing `hm_settings` non-secret AV scalar satirlarindan geliyor: ClamWin enabled/exe/db, action, notify sender/receiver, custom scanner enabled/exe/return value, max message size, attachment-blocking flag ve ClamAV enabled/host/port. Invalid `avaction` legacy gibi delete-email'e dusuyor. Direct activation `E_ACCESSDENIED`; tum setter'lar, scanner test methodlari, process/network/file access ve live reconfiguration `E_NOTIMPL` kaldi; blocked-attachment collection sonraki dilimde acildi. Dar AntiVirus/Settings/store/manifest filtresi 28/28, full Net10 testleri 808/808 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- `GlobalObjects.Languages` placeholder'i complete legacy `Languages`/`Language` contract/class identity ve hosted registration ile degistirildi. Runtime store executable-directory `Languages` klasorundeki `.ini` dosyalarini `[GUILanguages] ValidLanguages` filtresiyle okuyor; language name'leri lower-case ve deterministic order, `Item`/`ItemByName`, `DISP_E_BADINDEX`, `Name`, `IsDownloaded`, `String(EnglishString)` English fallback, direct activation `E_ACCESSDENIED` ve `Download` `E_NOTIMPL` sinirlari testlerde kilitlendi. Network/file write/live reload eklenmedi. Dar contract/parser/manifest/INI filtresi 18/18, full Net10 testleri 802/802 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `DeliveryQueue.Clear` process-local single-flight coordinator ile acildi. COM call SQL calistirmadan schedule edip hizli donuyor; tekrar cagrilar coalesce, coordinator 500-row bounded batch'leri drain ediyor. Store current owner lease ve delivered type-2 row'lari atliyor, recipient + queue message rows atomik siliyor, commit sonrasi contained best-effort file cleanup yapiyor; completion/failure log observer ve shutdown cancellation baglandi. Dar coordinator/COM/store/integration filtresi 14/14, full Net10 testleri 797/797 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- Authenticated `DeliveryQueue.Remove` dar SQL Server administration store uzerinden acildi. Tek type-1/type-3 queue row `UPDLOCK/READPAST/ROWLOCK` transaction ile seciliyor; current owner lease ve delivered type-2 row atlanirken recipient + message rows atomik siliniyor, commit sonrasi stored filename configured data directory icinde best-effort kaldiriliyor. Missing/non-queue/active-lease ID silent no-op, 64-bit ID ve direct activation denial korundu. Dar COM/store/integration filtresi 9/9, full Net10 testleri 792/792 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- Successful lease-owned delivery completion sonrasi source queue file lifecycle'i kapatildi. Normal delivery, `OnDeliveryStart`/`OnDeliverMessage` drop ve no-target completion DB `CompleteAsync` basarili olduktan sonra existing path-contained content store uzerinden best-effort `.eml` delete yapiyor. Defer, release, load failure, cancellation, lost-lease completion dosya silmiyor; delete I/O exception'i tamamlanmis mesaji tekrar defer etmiyor. Dar processor/content-store filtresi 17/17, full Net10 testleri 791/791 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Production SMTP primary ve rule-generated queue write'lari `ISmtpQueueWriter` uzerindeki signaling decorator'dan gecirildi. Decorator mevcut SQL/file writer tamamlandiktan sonra hosted delivery worker'in coalescing signal'ini tetikliyor; durable writer failure/cancellation durumunda signal yok, post-commit signal exception'i already-durable mesaji SMTP failure'a cevirmiyor. SQL, file cleanup, SMTP acceptance ve lease/retry davranisi degismedi. Dar SMTP writer/receiver/worker filtresi 41/41, full Net10 testleri 784/784 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Mevcut lease-aware `DeliveryQueueProcessor`, service-owned tek hosted worker altinda operational hale getirildi. Worker full batch'leri idle wait olmadan drain ediyor, bos/kismi batch'te legacy bir dakikalik poll fallback'i olan bounded coalescing signal bekliyor, batch exception sonrasinda service'i dusurmeden retry ediyor ve shutdown cancellation'i hata olarak raporlamadan koruyor. Authenticated `DeliveryQueue.StartDelivery` ayni signal'i non-mutating olarak tetikliyor; direct activation `E_ACCESSDENIED`, `Clear`/`Remove` `E_NOTIMPL` kaliyor. Dar worker/signal/GlobalObjects/Application filtresi 26/26, full Net10 testleri 778/778 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `DeliveryQueue.ResetDeliveryTime` dar SQL Server administration store/runtime boundary uzerinden acildi. Yalniz selected type-1/type-3 `hm_messages` row `messagetype = 1` ve immediately eligible UTC next-try timestamp ile guncelleniyor; 64-bit ID, retry count, lease, recipient ve content state degismiyor, missing ID silent no-op. Dar contract/store/integration filtresi 18/18, full Net10 testleri 768/768 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL seed/assert eklendi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- `GlobalObjects` placeholder complete legacy IID/vtable/class identity ile degistirildi; complete `DeliveryQueue` contract/class identity ve iki hosted registration eklenip authenticated `Application -> GlobalObjects -> DeliveryQueue` graph'i acildi. Direct activation `E_ACCESSDENIED`; `Clear`, `ResetDeliveryTime`, `StartDelivery`, `Remove` ve `Languages` `E_NOTIMPL` kaliyor, SQL mutation/worker wake-up eklenmedi. Legacy `hyper` message ID'leri `Int64` olarak ve IDL coclass CLSID'si stale `.rgs` degerine karsi testte kilitlendi. Dar GlobalObjects/Application/manifest filtresi 17/17, full Net10 testleri 765/765 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Complete legacy `Backup` IID/vtable/class identity ve hosted registration eklendi; authenticated `BackupManager.LoadBackup` packaged legacy `7za.exe`yi shell olmadan cagirip archive icindeki yalniz `hMailServerBackup.xml` entry'sini disk'e extract etmeden streaming okuyor. DTD-disabled/bounded XML parser Mode bit 1/2/4'u `ContainsSettings`/`ContainsDomains`/`ContainsMessages` olarak aciyor; restore secimleri process-local, `StartBackup`/`StartRestore`, DB/data writes, restore execution ve event dispatch kapsam disi. Gercek tracked 7z archive testi dahil dar filtre 24/24, full Net10 testleri 760/760 gecti; Windows service/COM build 0 uyari/0 hata verdi ve `7za.exe` output'ta dogrulandi.
- Replacement install rollback preflight (`7ab5d4c02`) eklendi. `-ReplaceExisting` also requires `-BackupArchive <path>` when a stopped existing service points to a different executable; before `--register-com` or any `sc.exe` mutation, the packaged shell-free `7za.exe` runs a full archive test and streams bounded DTD-disabled metadata. Only self-contained `Mode=15` and `DataFiles Format=7z` archives pass; timeout, nonzero archive test, missing metadata, malformed/DTD XML, and incomplete/raw metadata fail closed. Focused PowerShell 7 and Windows PowerShell 5.1 tests pass; full Net10 suite is 1066/1066. This is an operational rollback guard only: backup creation, restore, SQL/data writes, and backup events remain unimplemented.
- Rollback preflight payload hardening (`ed7376063`) eklendi. Shell-free, timeout/output-bounded `7za l -slt` listing now requires the compressed legacy `DataBackup` directory entry while accepting an empty data directory, and rejects missing/file-shaped payloads plus absolute or parent-traversal entry paths. Focused PowerShell 7 and Windows PowerShell 5.1 tests pass; full Net10 suite is 1066/1066. Archive listing/structure is validated read-only; semantic XML/SQL restorability, backup creation, destructive restore, and backup events remain unimplemented.
- `Application -> BackupManager` placeholder'i complete legacy IID/vtable/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Child object yalniz server-administrator authentication sonrasinda donuyor; direct activation `E_ACCESSDENIED`, `StartBackup` ve `LoadBackup` `E_NOTIMPL` kaliyor, dolayisiyla archive parsing, filesystem access, backup/restore execution veya event dispatch eklenmedi. Dar BackupManager/Application/manifest filtresi 17/17, full Net10 testleri 753/753 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- `Settings -> BackupSettings` placeholder'i complete legacy IID/vtable/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated `Destination` ve settings/domains/messages/compression getter'lari existing `backupdestination`/`backupoptions` `hm_settings` satirlarindan, deterministic `LogFile` configured log directory'den read-only geliyor; legacy bit 1/2/4/8 ve separator davranisi korunuyor, dosya okunmuyor/olusturulmuyor. Direct activation `E_ACCESSDENIED`; tum setter'lar, backup/restore execution, filesystem writes ve runtime reconfiguration `E_NOTIMPL` kaldi. Genisletilmis dar filtre 33/33, full Net10 testleri 748/748 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- Authenticated `Scripting.CurrentScriptFile` getter'i configured event directory ve stored language'dan legacy case-sensitive `EventHandlers.vbs`/`.js`, bilinmeyen dilde bos extension sekliyle acildi. INI directory string'i legacy gibi normalize edilmeden korunuyor; getter eksik klasorde dahi dosya okumuyor/olusturmuyor. DISPID 6/`BSTR`, direct activation `E_ACCESSDENIED` ve kalan reload/syntax/mutation `E_NOTIMPL` sinirlari korundu. Dar Scripting/Application/integration filtresi 24/24, full Net10 testleri 742/742 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- `Settings -> Scripting` placeholder'i complete legacy IID/vtable/class identity, versioned ProgID, manifest ve service class-factory registration ile degistirildi. Authenticated `Enabled`/`Language` existing `usescriptserver`/`scriptlanguage` `hm_settings` satirlarindan, `Directory` configured/default legacy INI `EventFolder` degerinden read-only geliyor. Direct activation `E_ACCESSDENIED`; setter, `Reload`, `CheckSyntax`, `CurrentScriptFile`, script execution ve runtime reconfiguration `E_NOTIMPL` kaldi. Dar contract/store/manifest/integration filtresi 39/39, full Net10 testleri 741/741 gecti; Windows service/COM build 0 uyari/0 hata verdi. Opt-in SQL seed/assert genisletildi; bu turda dis SQL baglanti degiskeni olmadigi icin gercek DB kurulumu calismadi.
- Authenticated `Settings.SMTPRelayerUseSSL` getter'i mevcut `SmtpRelayerConnectionSecurity` snapshot'indan legacy uyumluluk projection'i olarak acildi: yalniz `ComConnectionSecurity.Tls` true, `None` ve iki STARTTLS modu false donuyor. DISPID 71/`VARIANT_BOOL` metadata'si, direct activation `E_ACCESSDENIED` ve setter `E_NOTIMPL` sinirlari korundu; yeni SQL, credential handling veya live relay routing eklenmedi. Dar Settings/Application filtresi 22/22, full Net10 testleri 736/736 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings -> Logging` yolu `CurrentEventLog`, `CurrentErrorLog`, `CurrentAwstatsLog` ve `CurrentDefaultLog` getter'larini configured log directory ile legacy filename/local-date sekillerinde read-only acacak sekilde genisletildi. Tarihli path'ler her erisimde current local date'i yeniden aliyor, INI directory string'ini normalize etmeden koruyor ve eksik klasorde dahi dosya okumuyor/olusturmuyor. DISPIDs 18-21/`BSTR` metadata'si ve direct activation `E_ACCESSDENIED` siniri korundu; live logging/logger reconfiguration kapsam disi kaldi. Dar Logging/Application filtresi 19/19, full Net10 testleri 735/735 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings -> Logging.Directory` getter'i configured/default legacy `hMailServer.ini` `[Directories] LogFolder` degerini mevcut directory store uzerinden read-only dondurecek sekilde acildi. DISPID 14/`BSTR` metadata'si ve complete `Logging` identity/vtable'i degismedi; direct activation `E_ACCESSDENIED`, live/current-log getter'lari ve mutation yuzeyi `E_NOTIMPL` kaldi. Yeni SQL, dosya icerigi okuma veya logger reconfiguration eklenmedi. Dar Logging/Application/INI filtresi 20/20, full Net10 testleri 734/734 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings -> Logging` yolu persisted logging flag/device/format getter'larini acacak sekilde eklendi. `Logging` legacy IID/vtable/class identity'si korunup hosted class manifest ve service local-server registration kapsamina alindi; `Enabled`, `LogSMTP`, `LogPOP3`, `LogTCPIP`, `LogApplication`, `LogDebug`, `LogIMAP`, `KeepFilesOpen`, `Device`, `LogFormat` ve `AWStatsEnabled` mevcut `hm_settings` `logging`/`logdevice`/`logformat`/`awstatsenabled` satirlarindan read-only okunuyor. `ComLogDevice` ve `ComLogOutputFormat` enum GUID/degerleri testlerde kilitlendi. Direct activation `E_ACCESSDENIED`; setter'lar, live logging, log directory/current-log file access, logger reconfiguration ve genis Settings/Admin mutation `E_NOTIMPL` kaldi. Dar filtre 23/23, full Net10 testleri 734/734 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` yolu process-local `CrashSimulationMode` getter'ini acacak sekilde genisletildi. Runtime snapshot legacy default `0` ile basliyor ve enjekte edilen runtime degeri de ayni getter'dan okunuyor. DISPID 99/integer metadata'si, direct-activation `E_ACCESSDENIED` ve setter `E_NOTIMPL` sinirlari testlerde kilitlendi. SMTP crash/fault injection ve exception-handler test behavior kapsam disi kaldi. Dar filtre 21/21, full Net10 testleri 726/726 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` yolu INI-backed `RewriteEnvelopeFromWhenForwarding` getter'ini acacak sekilde genisletildi. Service configured/default legacy `hMailServer.ini` `[Settings] RewriteEnvelopeFromWhenForwarding` degerini runtime configuration snapshot'ina yukluyor; yalniz integer `1` true, diger degerler ve eksik dosya/anahtar false oluyor. DISPID 107 ve `VARIANT_BOOL` metadata'si, direct-activation `E_ACCESSDENIED` ve setter `E_NOTIMPL` sinirlari testlerde kilitlendi. INI persistence, SMTP/rule forwarding behavior ve DKIM signing interaction kapsam disi kaldi. Dar filtre 26/26, full Net10 testleri 726/726 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` yolu INI-backed `UserInterfaceLanguage` getter'ini acacak sekilde genisletildi. Service configured/default legacy `hMailServer.ini` `[Settings] UseLanguage` degerini runtime configuration snapshot'ina yukluyor; eksik dosya veya anahtar legacy gibi `English` donduruyor. DISPID 42 ve `BSTR` metadata'si, direct-activation `E_ACCESSDENIED` ve setter `E_NOTIMPL` sinirlari testlerde kilitlendi. INI persistence ve Administrator UI resource reload kapsam disi kaldi. Dar filtre 25/25, full Net10 testleri 725/725 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` yolu getter-only `PublicFolderDiskName` degerini legacy gibi sabit `#Public` olarak dondurecek sekilde genisletildi. DISPID 79, getter-only yuzey ve `BSTR` metadata'si contract testinde kilitlendi; direct activation `E_ACCESSDENIED` kaldi. Filesystem access, public-folder creation/rename behavior, data-directory reconfiguration ve daha genis Settings/Admin mutation kapsam disi kaldi. Dar filtre 21/21, full Net10 testleri 724/724 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u asynchronous-thread getter'ini read-only acacak sekilde genisletildi: `MaxAsynchronousThreads` mevcut legacy `MaxNumberOfAsynchronousTasks` `hm_settings.settinginteger` satirindan geliyor; COM property adinin SQL storage adi olarak kullanilmadigi store testinde kilitlendi. DISPID 88 ve integer metadata'si korundu. Setter, live work-queue resizing, service restart/reconfiguration ve daha genis Settings/Admin mutation kapsam disi kaldi. Dar filtre 23/23, full Net10 testleri 723/723 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u IMAP master-user getter'ini read-only acacak sekilde genisletildi: `IMAPMasterUser` mevcut legacy `ImapMasterUser` `hm_settings.settingstring` satirindan geliyor. DISPID 100 ve `BSTR` metadata'si contract testinde kilitlendi. Setter, IMAP authentication/master-user behavior, live IMAP configuration reload, secret handling ve daha genis Settings/Admin mutation kapsam disi kaldi. Dar filtre 23/23, full Net10 testleri 723/723 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u TLS bitmask getter'larini read-only acacak sekilde genisletildi: `TlsVersion10Enabled`, `TlsVersion11Enabled`, `TlsVersion12Enabled`, `TlsVersion13Enabled`, `TlsOptionPreferServerCiphersEnabled` ve `TlsOptionPrioritizeChaChaEnabled` mevcut legacy `SslVersions`/`TlsOptions` `hm_settings.settinginteger` bitmask satirlarindan geliyor. Legacy bitler TLS version icin 2/4/8/16 ve TLS option icin 2/4 olarak map ediliyor. DISPIDs 96/97/98/103/105/106 ve `VARIANT_BOOL` metadata'si contract testinde kilitlendi. Setter'lar, live TLS reload/listener context rebuild, cipher validation, OS TLS policy degisikligi ve daha genis Settings/Admin mutation kapsam disi kaldi. Dar filtre 23/23, full Net10 testleri 723/723 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u SMTP delivery connection-security getter'ini read-only acacak sekilde genisletildi: `SMTPConnectionSecurity` mevcut legacy `SmtpDeliveryConnectionSecurity` `hm_settings.settinginteger` satirindan geliyor. DISPID 92 ve `ComConnectionSecurity` enum tipi contract testinde kilitlendi. Setter, WebAdmin/Admin mutation, live SMTP delivery routing, delivery-worker reconfiguration ve TLS policy degisikligi kapsam disi kaldi. Dar filtre 23/23, full Net10 testleri 723/723 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u non-secret SMTP relayer getter'larini read-only acacak sekilde genisletildi: `SMTPRelayer`, `SMTPRelayerRequiresAuthentication`, `SMTPRelayerUsername`, `SMTPRelayerPort` ve `SMTPRelayerConnectionSecurity` mevcut legacy `smtprelayer`/`usesmtprelayerauthentication`/`smtprelayerusername`/`smtprelayerport`/`smtprelayerconnectionsecurity` `hm_settings` satirlarindan geliyor. DISPIDs 22/34/35/37/91, `BSTR`/`VARIANT_BOOL`/integer/enum metadata'si ve `ComConnectionSecurity` enum tipi contract testinde kilitlendi. `smtprelayerpassword` store projection disinda kaldi; `SetSMTPRelayerPassword`, legacy `SMTPRelayerUseSSL`, setter'lar, credential handling ve live SMTP delivery routing kapsam disi kaldi. Dar filtre 23/23, full Net10 testleri 723/723 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u auto-ban scalar getter'larini read-only acacak sekilde genisletildi: `AutoBanOnLogonFailure`, `MaxInvalidLogonAttempts`, `MaxInvalidLogonAttemptsWithin` ve `AutoBanMinutes` mevcut legacy `AutoBanOnLogonFailureEnabled`/`MaxInvalidLogonAttempts`/`LogonAttemptsWithinMinutes`/`AutoBanMinutes` `hm_settings.settinginteger` satirlarindan geliyor. DISPIDs 82/83/84/85 ve `VARIANT_BOOL`/integer metadata'si contract testinde kilitlendi. Setter'lar, `ClearLogonFailureList`, live auto-ban policy/security-range mutation ve daha genis Settings/Admin mutation kapsam disi kaldi. Dar filtre 22/22, full Net10 testleri 722/722 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u network preference getter'ini read-only acacak sekilde genisletildi: `IPv6PreferredEnabled` mevcut legacy `IPv6Preferred` `hm_settings.settinginteger` satirindan geliyor. DISPID 104 ve `VARIANT_BOOL` metadata'si contract testinde kilitlendi. Setter ve live network preference/reconfiguration davranisi kapsam disi kaldi. Dar filtre 22/22, full Net10 testleri 722/722 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u SSL/TLS scalar getter'larini read-only acacak sekilde genisletildi: `VerifyRemoteSslCertificate` ve `SslCipherList` mevcut legacy `VerifyRemoteSslCertificate` `hm_settings.settinginteger` ve `SslCipherList` `hm_settings.settingstring` satirlarindan geliyor. DISPIDs 93/94 ve `VARIANT_BOOL`/`BSTR` metadata'si contract testinde kilitlendi. Setter'lar `E_NOTIMPL`, live TLS reload, cipher validation, certificate policy ve TLS version flag davranisi kapsam disi kaldi. Dar filtre 22/22, full Net10 testleri 722/722 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u Settings numeric runtime getter'larini read-only acacak sekilde genisletildi: `RuleLoopLimit`, `WorkerThreadPriority`, `TCPIPThreads` ve `MaxNumberOfMXHosts` mevcut legacy `rulelooplimit`/`workerthreadpriority`/`tcpipthreads`/`MaxNumberOfMXHosts` `hm_settings.settinginteger` satirlarindan geliyor. DISPIDs 48/57/60/90 ve integer metadata'si contract testinde kilitlendi. Setter'lar `E_NOTIMPL`, live rule-loop/thread tuning, SMTP MX scheduling ve `MaxAsynchronousThreads` storage dogrulanmadan kapsam disi kaldi. Dar filtre 22/22, full Net10 testleri 722/722 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u SMTP routing string getter'larini read-only acacak sekilde genisletildi: `MirrorEMailAddress`, `DefaultDomain` ve `SMTPDeliveryBindToIP` mevcut legacy `mirroremailaddress`/`defaultdomain`/`smtpdeliverybindtoip` `hm_settings.settingstring` satirlarindan geliyor. DISPIDs 7/50/51 ve `BSTR` metadata'si contract testinde kilitlendi. Setter'lar `E_NOTIMPL`, relayer credential ve live SMTP routing/bind degisikligi kapsam disi kaldi. Dar filtre 21/21, full Net10 testleri 721/721 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u SMTP policy getter'larini read-only acacak sekilde genisletildi: `AllowSMTPAuthPlain`, `DenyMailFromNull`, `AllowIncorrectLineEndings` ve `AddDeliveredToHeader` mevcut legacy `authallowplaintext`/`allowmailfromnull`/`smtpallowincorrectlineendings`/`adddeliveredtoheader` `hm_settings.settinginteger` satirlarindan geliyor. `DenyMailFromNull`, raw `AllowMailFromNull` degerini legacy gibi ters ceviriyor; DISPIDs 8/11/61/73 ve `VARIANT_BOOL` metadata'si contract testinde kilitlendi. Setter'lar `E_NOTIMPL`, live SMTP authentication/session/delivery degisikligi kapsam disi kaldi. Dar filtre 26/26, full Net10 testleri 721/721 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u IMAP naming getter'larini read-only acacak sekilde genisletildi: `IMAPPublicFolderName` ve `IMAPHierarchyDelimiter` mevcut legacy `imappublicfoldername`/`IMAPHierarchyDelimiter` `hm_settings.settingstring` satirlarindan geliyor. DISPIDs 74/87 ve `BSTR` metadata'si yeni contract testinde kilitlendi; setter'lar `E_NOTIMPL`, folder/rule rename-rewrite ve live IMAP namespace degisikligi kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 26/26, full Net10 testleri 721/721 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u IMAP SASL getter'larini read-only acacak sekilde genisletildi: `IMAPSASLPlainEnabled` ve `IMAPSASLInitialResponseEnabled` mevcut legacy `EnableImapSASLPlain`/`EnableImapSASLInitialResponse` `hm_settings.settinginteger` satirlarindan geliyor. DISPIDs 101/102 ve `VARIANT_BOOL` metadata'si contract testinde kilitlendi; setter'lar `E_NOTIMPL`, live IMAP authentication/capability degisikligi kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u IMAP capability getter'larini read-only acacak sekilde genisletildi: `IMAPSortEnabled`, `IMAPQuotaEnabled`, `IMAPIdleEnabled` ve `IMAPACLEnabled` mevcut legacy `enableimapsort`/`enableimapquota`/`enableimapidle`/`enableimapacl` `hm_settings.settinginteger` satirlarindan geliyor. DISPIDs ve `VARIANT_BOOL` metadata'si contract testinde kilitlendi; setter'lar `E_NOTIMPL`, live IMAP capability/session degisikligi kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u SMTP guardrail getter'larini read-only acacak sekilde genisletildi: `MaxMessageSize`, `MaxSMTPRecipientsInBatch`, `DisconnectInvalidClients` ve `MaxNumberOfInvalidCommands` mevcut legacy `hm_settings.settinginteger` satirlarindan geliyor. `DisconnectInvalidClients` `VARIANT_BOOL` metadata'si contract testinde kilitlendi; setter'lar `E_NOTIMPL`, live SMTP session/listener policy degisikligi kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u legacy retry getter'larini read-only acacak sekilde genisletildi: `SMTPNoOfTries` `smtpnoofretries`, `SMTPMinutesBetweenTry` ise `smtpminutesbetweenretries` satirindan geliyor; obsolete `smtpnooftries` decoy satiri SQL store'dan acikca dislandi. Setter'lar `E_NOTIMPL`, delivery retry scheduling degisikligi kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u legacy protocol-enabled getter'larini read-only acacak sekilde genisletildi: `ServiceSMTP`, `ServicePOP3` ve `ServiceIMAP` mevcut `protocolsmtp`/`protocolpop3`/`protocolimap` `hm_settings.settinginteger` satirlarindan geliyor. `VARIANT_BOOL` getter/setter metadata'si contract testinde kilitlendi; setter'lar `E_NOTIMPL`, live listener enable/disable ve service state kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 25/25, full Net10 testleri 720/720 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` snapshot/store'u legacy integer limit getter'larini read-only acacak sekilde genisletildi: `MaxSMTPConnections`, `MaxPOP3Connections`, `MaxIMAPConnections` ve `MaxDeliveryThreads` mevcut legacy `hm_settings.settinginteger` satirlarindan geliyor. Setter'lar `E_NOTIMPL`; live listener/delivery-worker reconfiguration ve service state kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 24/24, full Net10 testleri 719/719 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Settings` yolu legacy `HostName`, `WelcomeSMTP`, `WelcomePOP3` ve `WelcomeIMAP` getter'larini existing `hm_settings.settingstring` satirlarindan read-only acacak sekilde genisletildi. Installed vtable/DISPID ve direct-activation `E_ACCESSDENIED` siniri korundu; setter'lar `E_NOTIMPL`, listener reconfiguration, service state, secret settings ve genis Settings mutation kapsam disi kaldi. Dar Settings/Application/COM-host/store/integration filtresi 24/24, full Net10 testleri 719/719 gecti; Windows service/COM build 0 uyari/0 hata verdi.
- Authenticated `Application -> Domains` koleksiyonu legacy `Domains.Names` getter'ini read-only acacak sekilde genisletildi; loaded domain snapshot'larindan `id\tname\tactive\r\n` formatini uretiyor. `Refresh`, collection mutation ve database reload kapsam disi kaldi. Dar domain contract/integration filtresi 6/6, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i non-secret Active Directory scalar getter'larini read-only acacak sekilde genisletildi: `IsAD`, `ADDomain`, ve `ADUsername` mevcut `hm_accounts.accountisad`/`accountaddomain`/`accountadusername` kolonlarindan geliyor. Setter, AD auth, password/security-sensitive alanlar ve account mutation kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i legacy `Account.LastLogonTime` getter'ini mevcut `hm_accounts.accountlastlogontime` degerinden read-only acacak sekilde genisletildi. Login-time update, authentication davranisi ve account mutation kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i legacy `Account.QuotaUsed` getter'ini secili account'un `hm_messages.messagesize` byte toplami ve `accountmaxsize` MB limitinden legacy integer yuzde/truncation davranisiyla read-only acacak sekilde genisletildi. Quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Account` adapter'i legacy `Account.Size` getter'ini secili account'un `hm_messages.messagesize` byte toplamindan 3 basamakli MB float degeri olarak read-only acacak sekilde genisletildi. Quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar account contract/store/integration filtresi 15/15, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i legacy MSSQL `Domain.Size` getter'ini read-only aggregate olarak acacak sekilde genisletildi; SQL shape `hm_messages.messagesize` toplamindan MB'a truncate ediyor ve legacy `messageaccountid IN (SELECT accountdomainid ...)` davranisi dar store/integration testleriyle sabitlendi. Quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i legacy `Domain.AllocatedSize` getter'ini secili domain'in `hm_accounts.accountmaxsize` toplamindan gelen read-only aggregate olarak acacak sekilde genisletildi. `Domain.Size`, quota enforcement, account mutation ve filesystem/mailbox scan davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains.domainaddomain` degerini read-only `Domain.ADDomainName` getter'i olarak acacak sekilde genisletildi. Setter, AD synchronization ve authentication davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains.domainantispamoptions` greylisting flag'ini read-only `Domain.AntiSpamEnableGreylisting` getter'i olarak acacak sekilde genisletildi. Setter, SMTP policy davranisi ve runtime greylisting degisikligi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains` signature ayarlarini read-only COM getter'lariyla acacak sekilde genisletildi: `SignatureEnabled`, `SignatureMethod`, `SignaturePlainText`, `SignatureHTML`, `AddSignaturesToReplies` ve `AddSignaturesToLocalMail` mevcut legacy kolonlardan geliyor. Setter'lar, message mutation, SMTP signature uygulama davranisi ve migration kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Authenticated SQL-backed `Domain` adapter'i mevcut `hm_domains` DKIM ayarlarini read-only COM getter'lariyla acacak sekilde genisletildi: `DKIMSignEnabled`, selector, private-key file path, header/body canonicalization, signing algorithm ve alias-signing flag'i legacy `domainantispamoptions` bitleri ile `domaindkimselector`/`domaindkimprivatekeyfile` kolonlarindan geliyor. Setter'lar, signing, private-key file icerigi okuma ve SMTP policy davranisi kapsam disi kaldi. Dar domain contract/store/integration filtresi 8/8, full Net10 testleri 715/715 gecti.
- Resmi Public Suffix List snapshot'i `2026-06-24_06-18-09_UTC` / `18ecca5d54471f21918798da451dd8d03a18f3c7` commit'ine ve `8208f0c918c6cb3ab77b484635fc8683c94cbfff818be81950908e881a5f8be2` SHA-256 degerine pinlendi. Snapshot + deterministic metadata Service build/publish ciktilarina kopyalaniyor; offline build gate header/hash/byte length'i dogruluyor ve maintainer-only refresh komutu expected commit + hash olmadan calismiyor. Runtime/SMTP download eklenmedi. Dar DMARC/PSL filtresi 32/32, prereq temiz, Net10 build 0 uyari/0 hata, publish hash smoke testi basarili ve full Net10 testleri 708/708 gecti.
- DMARC organizational-domain/public-suffix boundary eklendi: `IDmarcOrganizationalDomainResolver` arkasinda Nager.PublicSuffix 3.8.0 ile local PSL dosyasi lazy/thread-safe tek sefer yukleniyor; `HMAILSERVER_DMARC_PUBLIC_SUFFIX_LIST`/`AntiSpam:Dmarc:PublicSuffixListPath` veya executable yanindaki `public_suffix_list.dat` kullaniliyor. Valid liste parent-record fallback, `sp=` secimi ve relaxed sibling alignment'i aciyor; wildcard/exception kurallari testli, missing/invalid/unreadable liste exact-domain DMARC'a fail-open kaliyor ve SMTP path'inde online download yok. Dar DMARC filtresi 30/30, prereq temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 706/706 gecti.
- Disabled-by-default SMTP DMARC policy/input boundary eklendi: `HMAILSERVER_DMARC_ENABLED=false` varsayilani SMTP davranisini degistirmiyor; explicit acikken RFC5322.From domain'i cikariliyor, SPF sonucu ve DKIM pass signing-domain listesi DMARC evaluator'a veri olarak tasiniyor, malformed input/DNS/runtime hatalari fail-open kaliyor ve yalniz `HMAILSERVER_DMARC_MARK_FAILURES_AS_SPAM=true` ile policy failure mevcut spam-flag yoluna map edilebiliyor. Direct SMTP reject/quarantine, organizational-domain/public-suffix discovery, signing ve Administrator/COM setting plumbing baglanmadi. Dar `SmtpDmarcPolicyTests` + `SmtpDkimPolicyTests` + receiver filtresi 38/38, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 698/698 gecti.
- DMARC evaluation-only foundation eklendi: injected TXT resolver arkasinda DMARC record/result modeli, deterministic `p`/`sp`/`aspf`/`adkim`/`pct` parser'i, exact-domain + optional organizational-domain fallback lookup'u, strict/relaxed SPF ve DKIM alignment kontrolleri, subdomain policy secimi, temp DNS failure ile malformed/duplicate record sonuc map'leri kapatildi. SPF/DKIM sonuclari yalniz veri olarak tuketiliyor; SMTP reject/quarantine, spam scoring, signing ve Administrator/COM setting plumbing baglanmadi. Dar `DmarcEvaluationTests` 12/12, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 688/688 gecti.
- Disabled-by-default DKIM SMTP policy boundary eklendi: `HMAILSERVER_DKIM_ENABLED=false` varsayilani SMTP davranisini degistirmiyor; explicit acikken message-level verifier sonucunu tuketiyor, legacy spam-test subset'ine uygun sekilde yalniz `PermFail` icin configured failure score ile spam flag/status uretip `Pass` sonucunu pass sinyali olarak tasiyor, `Neutral`/`TempFail` fail-open kaliyor ve dogrudan SMTP reject eklenmiyor. Signing, DMARC ve Administrator/COM setting plumbing baglanmadi. Dar `SmtpDkimPolicyTests` 5/5 ve receiver DKIM filtresi 2/2 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 676/676 gecti.
- WebAdmin AV/SpamAssassin AJAX test action'lari GET + URL token yerine `application/x-www-form-urlencoded` POST body kullanacak sekilde daraltildi; `background_ajax_virustest.php` ve `background_ajax_spamassassintest.php` POST-only oldu ve mevcut server-admin/CSRF kontrolleri korunuyor. Bu SEC-14/15/16 icin kismi hardening; egress/private-network allowlist politikasi ve diger legacy mutation linkleri P1 olarak kaliyor.
- DKIM message-level verifier cekirdegi eklendi: raw/header-body mesaj girdilerinden `DKIM-Signature` field'lari cikariliyor, legacy gibi ilk 5 imza degerlendiriliyor, parse edilemeyen imzalar `Neutral` olarak atlanip devam ediliyor, herhangi bir imza body-hash + header-signature + DNS key zincirinden `Pass` alirsa hemen `Pass` donuluyor, aksi halde legacy dongudeki son non-pass sonuc korunuyor. SMTP reject, policy score, signing ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 37/37, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 669/669 gecti.
- DKIM DNS/public-key lookup cekirdegi eklendi: `{selector}._domainkey.{domain}` TXT lookup'u `IDkimTxtResolver` boundary arkasindan yapiliyor; key record `v=DKIM1`, non-empty/revoked `p=`, optional `h=`, `g=`, ve `t=s` sinirlariyla legacy result modeline map ediliyor. `SystemDkimTxtResolver` mevcut system DNS TXT resolver'ini yeniden kullaniyor ve async `DkimSignatureVerifier.VerifyAsync` DNS'ten gelen key'i body-hash + header-signature verifier'a besliyor. SMTP reject, policy score, signing ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 31/31, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 663/663 gecti.
- DKIM header crypto verifier eklendi: signed header'lar ve `b=` blanked `DKIM-Signature` canonicalize edilip injected SubjectPublicKeyInfo public key ile `rsa-sha1`/`rsa-sha256` RSA/PKCS#1 imzasi dogrulaniyor. Full evaluation yalniz body hash ve header signature birlikte basarili olursa `Pass` modeli donuyor; signed-header/body/public-key/signature hatalari `PermFail`. Live DNS selector lookup, SMTP reject, policy score, signing ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 23/23, prereq kontrolu temiz, Net10 build 0 uyari/0 hata ve full Net10 testleri 655/655 gecti.
- DKIM body-hash verifier eklendi: parsed signature uzerinden canonicalized body icin `bh=` karsilastirmasi, opsiyonel `l=` body length siniri, SHA1/SHA256 secimi, body-hash match icin `Neutral` ve mismatch/uzunluk asimi icin `PermFail` sonuc modeli test edildi. SMTP reject, policy score, signing, DNS selector lookup, public-key/header crypto ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 18/18, full Net10 testleri 650/650 ve Net10 build 0 uyari/0 hata ile gecti.
- DKIM evaluation-only temeli eklendi: legacy `Neutral`/`Pass`/`TempFail`/`PermFail` sonuc modeli, deterministic `DKIM-Signature` tag parser'i, required-field validation, default/simple/relaxed canonicalization secimi, signed-header list parsing, `b=` signature-value blanking ve simple/relaxed body/header canonicalization testleri eklendi. SMTP reject, policy score, signing, DNS selector lookup, public-key/header crypto ve Administrator ayarlari baglanmadi. Dar DKIM filtresi 13/13, full Net10 testleri 645/645 ve Net10 build 0 uyari/0 hata ile gecti.
- SPF pass -> greylisting bypass parity eklendi: `HMAILSERVER_GREYLISTING_BYPASS_ON_SPF_PASS=false` varsayilani normal greylisting davranisini koruyor; yalniz explicit acikken SPF `Pass` greylisting lookup'unu bypass ediyor. `Fail`/`None`/`Neutral`/`SoftFail`/`TempError`/`PermError` bypass veya reject/tempfail uretmiyor. Dar greylisting/SPF/receiver filtresi 34/34, full Net10 testleri 632/632 ve Net10 build 0 uyari/0 hata ile gecti.
- SPF system-DNS + disabled SMTP policy boundary eklendi: OS DNS server'lari uzerinden TXT/A/AAAA/MX/PTR cozen `SystemSpfDnsResolver`, `ISmtpSpfPolicy` boundary'si, `HMAILSERVER_SPF_ENABLED=false` varsayilani, authenticated ve `EnableSpamScan=false` skip yollari, `Fail` -> legacy spam flag/status mapping'i ve `Pass` result preservation tamamlandi. Reject/tempfail davranisi eklenmedi. Dar SPF/receiver filtresi 57/57, full Net10 testleri 629/629 ve Net10 build 0 uyari/0 hata ile gecti.
- SPF evaluation-only temeli eklendi: bounded resolver abstraction, deterministik `v=spf1` parser'i, RFC 7208 sonuc modeli, macro expansion, `include`/`redirect` ve `all`/`a`/`mx`/`ptr`/`ip4`/`ip6`/`exists` mekanizmalari, global DNS-term/void-lookup/recursion/MX/PTR limitleri ve timeout/temporary-error yollari dar testlerle kapatildi. SMTP policy/reject/tempfail davranisi bilincli olarak baglanmadi. Dar SPF filtresi 25/25, full Net10 testleri 614/614 ve Net10 build 0 uyari/0 hata ile gecti.
- Legacy `Links` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Application -> Links`, mevcut read-only SQL administration store/adapter hatlarini yeniden kullanarak `Domain`, `Account`, `Alias` ve `DistributionList` DBID lookup'u aciyor; bilinmeyen ID `DISP_E_BADINDEX`, direct activation `E_ACCESSDENIED` kaliyor ve yeni SQL/mutation eklenmiyor.
- Legacy `Utilities` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. `Application -> Utilities` ile hosted direct class auth istemeden `MD5`, salted `SHA256`, `GenerateGUID`, email/domain/IP validator, `IsStrongPassword`, `CriteriaMatch`, legacy Blowfish, IPv4-only `IsLocalHost` ve `GetMailServer` davranisini aciyor; authenticated Application yolu ayrica read-only `RetrieveMessageID` lookup'ini, IMAP folder UID maintenance operasyonunu, Windows service dependency replacement'ini, `ImportMessageFromFile`, private/public folder creation ve ACL-aware `ImportMessageFromFileToIMAPFolder` davranislarini ve `EmailAllAccounts` mass-mail queue davranisini sagliyor. Yalniz embedded/internal `RunTestSuite` acik; yan etkili uyeler once legacy server-admin sinirini uyguluyor ve runtime'siz public construction runtime-backed uyelerde `E_NOTIMPL` kaliyor.
- 27 Haziran derlenmis guvenlik envanteri mevcut SEC-01..SEC-21 tablosuyla birlestirildi; yeni benzersiz kayit cikmadi. Tek kritik SEC-01, 28 Haziran'da rapordaki `x" & ... & "` payload sekliyle yeniden dogrulandi: VBScript quote doubling payload'i ifade olarak calistirmiyor, handler'a veri olarak iletiyor. WSH tabanli .NET security/ClamAV/admin-auth dar filtresi 10/10 gecti; production kodunda legacy davranisi bozacak gereksiz bir degisiklik yapilmadi.
- Legacy `Application` core scalar davranisi runtime/configuration boundary arkasindan eklendi: `Version` legacy gibi auth istemeden donuyor, `ServerState` ve `InitializationFile` server-admin auth istiyor, `VersionArchitecture` legacy `x86`/`x64` formatina cekildi. `Start`, `Stop`, `Connect`, `Reinitialize` ve `SubmitEMail` yan etkili operasyonlari bilincli olarak `E_NOTIMPL` kaliyor.
- Legacy `Status` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Application -> Status` delivery queue metnini `hm_messages`/`hm_messagerecipients` uzerinden, `StartTime`, processed/spam/virus sayaçlari, `SessionCount` ve `ThreadID` degerlerini runtime snapshot boundary uzerinden read-only aciyor. SMTP/POP3/IMAP session count lease'leri ile delivery completed, spam-detected ve virus-detected counter hook'lari baglandi; direct activation `E_ACCESSDENIED` kaliyor.
- Legacy `ServerMessages` ve `ServerMessage` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> ServerMessages` count/index/name/id lookup'u mevcut `hm_servermessages` SQL verisinden `smname` sirasiyla geliyor ve `ID`, `Name`, `Text` scalar'larini read-only aciyor. Delivery template execution, `Refresh`, `Save`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `Directories` COM kontrati tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> Directories` configured/default legacy `hMailServer.ini` degerlerinden `ProgramDirectory`, `DatabaseDirectory`, `DataDirectory`, `LogDirectory`, `TempDirectory`, `EventDirectory` ve `DBScriptDirectory` scalar'larini legacy normalization ile read-only aciyor. Directory mutation/persistence ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `Database` COM kontrati ve `eDBtype` enum degerleri tam vtable/identity sirasiyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. `Application -> Database` required/current DB version, requires-upgrade, database-exists, is-connected ve INI-backed type/server/name scalar'larini read-only aciyor; legacy per-member auth korunuyor. SQL execution, transaction, database create/default-selection, script execution, message filename utility, prerequisite ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- SEC-19 legacy IMAP `RENAME` ACL siniri kapatildi: public-folder hiyerarsi degisikligi kaynakta Delete iznine ek olarak hedefteki en ust mevcut parent uzerinde Create izni istiyor. Mevcut regresyon senaryosu Create olmadan red, izin verildikten sonra basariyi kanitlayacak sekilde daraltildi; RegressionTests assembly ve degisen C++ translation unit derlendi.
- Security hardening dilimi: bos administrator hash'i legacy ve .NET 10'da fail-closed; constructor-time anonymous COM auth kaldirildi; legacy JScript password/delivery/UID literal escaping'i duzeltildi; `ScriptFunction` isim/yetki siniri kapatildi; SMTP `ETRN` auth zorunlu oldu; custom antivirus `%FILE%` message-file argumani quote/escape edildi; WebAdmin login session ID/CSRF token rotation ve cryptographic CSRF token generation eklendi. Dar .NET security testleri 15/15, full Net10 testleri 549/549, opt-in LocalDB 6/6 gecti; legacy RegressionTests assembly build'i ve degisen C++ dosyalarinin selected-file compile'i basarili oldu.
- Legacy `GroupMembers` ve `GroupMember` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Group -> Members` count/index/id lookup'u mevcut `hm_group_members` SQL verisinden `memberid` sirasiyla ve group-ID filtresiyle geliyor; `ID`, `GroupID`, `AccountID` scalar'larini ve son child facade dilimiyle `Account` objesini read-only aciyor. ACL runtime davranisi, mutation'lar ve direct activation sinirlari kapsam disi/`E_ACCESSDENIED` kaliyor.
- Legacy `Groups` ve `Group` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> Groups` count/index/name/id lookup'u mevcut `hm_groups` SQL verisinden `groupname` sirasiyla geliyor ve `ID`/`Name` scalar'larini read-only aciyor. Members alt koleksiyonu, ACL davranis entegrasyonu, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `SSLCertificates` ve `SSLCertificate` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> SSLCertificates` count/index/id lookup'u mevcut `hm_sslcertificates` SQL verisinden `sslcertificatename` sirasiyla geliyor ve `ID`, `Name`, `CertificateFile`, `PrivateKeyFile` scalar'larini read-only aciyor. Sertifika yukleme/dogrulama, TCP/IP port reconfiguration, `Clear`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `SecurityRanges` ve `SecurityRange` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> SecurityRanges` count/index/name/id lookup'u mevcut `hm_securityranges` SQL verisinden `rangeexpires`, `rangepriorityid desc`, `rangename` sirasiyla geliyor. DB-backed adapter legacy iki kolonlu IP depolamasini COM'da `LowerIP`/`UpperIP` string'lerine ceviriyor ve read-only IP range, priority, expiry ve option bit scalar'larini aciyor. IP policy enforcement, auto-ban runtime davranisi, `SetDefault`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `TCPIPPorts` ve `TCPIPPort` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> TCPIPPorts` count/index/id lookup'u mevcut `hm_tcpipports` SQL verisinden `portaddress1`, `portaddress2`, `portnumber` sirasiyla geliyor. DB-backed adapter legacy iki kolonlu IP depolamasini COM'da `Address` string'ine ceviriyor ve `ID`, `Protocol`, `PortNumber`, `Address`, `UseSSL`, `SSLCertificateID`, `ConnectionSecurity` scalar'larini read-only aciyor. Listener yeniden konfigurasyonu, `SetDefault`, mutation'lar ve direct activation sinirlari `E_NOTIMPL`/`E_ACCESSDENIED` kaliyor.
- Legacy `IncomingRelays` ve `IncomingRelay` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> IncomingRelays` count/index/name/id lookup'u mevcut `hm_incoming_relays` SQL verisinden `relayname` sirasiyla geliyor. DB-backed adapter legacy iki kolonlu IP depolamasini COM'da `LowerIP`/`UpperIP` string'lerine ceviriyor; SMTP trust davranisi ve mutation'lar `E_NOTIMPL`, direct activation `E_ACCESSDENIED` kaliyor.
- Authenticated `Application -> Rules`, mevcut testli `Rules`/`Rule` adapter ve SQL store hattini `ruleaccountid = 0` global-rule verisi icin yeniden kullaniyor. Yalniz server-admin Application yolu koleksiyona erisebiliyor; global/account rule ayrimi ve gercek SQL yolu test edildi. Criteria/actions, execution ve mutation sinirlari degismeden `E_NOTIMPL` kaliyor.
- Legacy `Routes` ve `Route` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Settings -> Routes` count/index/domain-name/id lookup'u mevcut `hm_routes` SQL verisinden legacy domain-name sirasiyla geliyor. DB-backed route adapter parola kolonunu okumadan ID, domain/description/target, retry, all-addresses, relayer auth kullanimi ve username'i, sender/recipient-local bayraklari ile connection-security/UseSSL scalar'larini read-only aciyor; obsolete `TreatSecurityAsLocalDomain` recipient-local alias'ini koruyor. Direct activation `E_ACCESSDENIED`; route-address alt koleksiyonu, parola setter'i ve mutation'lar `E_NOTIMPL` kaliyor.
- Authenticated `Settings -> PublicFolders`, mevcut testli `IMAPFolders`/`IMAPFolder` adapter ve SQL store hattini `folderaccountid = 0` public-root verisi icin yeniden kullaniyor. Yalniz server-admin Settings yolu koleksiyona erisebiliyor; account/public kok ayrimi ve gercek SQL yolu test edildi. Public folder permissions bounded ACL slice ile read-only acildi; `SubFolders` artik selected public parent scope'unda read-only child folder collection donduruyor; mutation sinirlari `E_NOTIMPL` kaliyor.
- Legacy `IMAPFolders` ve `IMAPFolder` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Account -> IMAPFolders` top-level (`folderparentid = -1`) count/index/name/id lookup'u ve `IMAPFolder -> SubFolders` parent-scoped child lookup'u mevcut `hm_imapfolders` SQL verisinden `folderid` sirasiyla getiriyor. DB-backed folder adapter `ID`, `ParentID`, legacy modified UTF-7 decode edilmis `Name`, `Subscribed`, `CurrentUID`, ve `CreationTime` scalar'larini read-only aciyor; direct `IMAPFolders`/`IMAPFolder` aktivasyonu `E_ACCESSDENIED`, private account-folder permissions legacy public-folder-only error donuyor, folder mutation'lar `E_NOTIMPL` kaliyor. Dar contract/store/manifest testleri ve opt-in izole SQL integration kapsami guncellendi.
- Legacy `Rules` ve `Rule` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Account -> Rules` count/index/id lookup mevcut `hm_rules` SQL verisinden geliyor. DB-backed rule adapter `ID`, `AccountID`, `Name`, `Active`, ve `UseAND` scalar'larini read-only aciyor; direct `Rules`/`Rule` aktivasyonu `E_ACCESSDENIED`, criteria/actions, execution ve mutation'lar `E_NOTIMPL` kaliyor. Dar contract/store/manifest testleri ve opt-in izole SQL integration kapsami guncellendi.
- Legacy `FetchAccounts` ve `FetchAccount` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration kapsamina alindi. Authenticated `Account -> FetchAccounts` count/index/id lookup mevcut `hm_fetchaccounts` SQL verisinden geliyor. DB-backed fetch-account adapter non-secret scalar'lari read-only aciyor (`ID`, `AccountID`, `Name`, server/port/type/user, minutes/days, enabled, MIME processing flags/headers, connection security/UseSSL, antispam/antivirus/route flags, next download time, lock state); password access, `DownloadNow`, `Save`, `Delete`, setters ve direct activation `E_NOTIMPL`/`E_ACCESSDENIED` sinirlarini koruyor. Dar contract/store/manifest ve opt-in izole SQL integration kapsami legacy enum degerleri, direct-TLS-only `UseSSL` alias'i, read-only mutation sinirlari, non-secret SQL projection sirasi, account-scoped DBID izolasyonu ve password omission icin sikilastirildi; focused filtre 11/11 gecti.
- Authenticated SQL-backed `Account` adapter'i secili delivery/detail scalar'lari read-only acacak sekilde genisletildi: vacation/autoreply (`VacationMessageIsOn`, `VacationMessage`, `VacationSubject`, expiry/spam-abort), forwarding (`ForwardEnabled`, `ForwardAddress`, `ForwardKeepOriginal`, spam-abort) ve signature (`SignatureEnabled`, `SignaturePlainText`, `SignatureHTML`) alanlari mevcut `hm_accounts` SQL verisinden geliyor. Behavior execution, password/security-sensitive alanlar, child collection'lar ve scalar mutation'lari `E_NOTIMPL` kaliyor. Dar contract/store testleri ve opt-in izole SQL integration kapsami guncellendi.
- Authenticated SQL-backed `Account` adapter'i secili core detail scalar'lari read-only acacak sekilde genisletildi: `MaxSize`, `PersonFirstName`, ve `PersonLastName` mevcut `hm_accounts` SQL verisinden geliyor. Password/security-sensitive alanlar, behavior-heavy alanlar, child collection'lar ve scalar mutation'lari `E_NOTIMPL` kaliyor. Dar contract/store testleri ve opt-in izole SQL integration kapsami guncellendi.
- Authenticated SQL-backed `Domain` adapter'i secili core detail scalar'lari read-only acacak sekilde genisletildi: `Postmaster`, `MaxMessageSize`, `PlusAddressingEnabled`, `PlusAddressingCharacter`, `MaxSize`, `MaxNumberOfAccounts`, `MaxNumberOfAliases`, `MaxNumberOfDistributionLists`, bunlarin enabled bitleri ve `MaxAccountSize` mevcut `hm_domains` SQL verisinden geliyor. Scalar mutation'lari ve computed/behavior-heavy alanlar (`Size` vb.) `E_NOTIMPL` kaliyor. Dar contract/store testleri ve opt-in izole SQL integration kapsami guncellendi.
- Legacy `DomainAliases` ve `DomainAlias` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `Domain -> DomainAliases` count/index/id lookup mevcut `hm_domain_aliases` SQL verisinden geliyor. DB-backed domain-alias adapter `ID`, `DomainID`, ve `AliasName` scalar'larini read-only aciyor; direct `DomainAliases`/`DomainAlias` aktivasyonu `E_ACCESSDENIED`, domain-alias mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `DistributionListRecipients` ve `DistributionListRecipient` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `DistributionList -> Recipients` count/index/id lookup mevcut `hm_distributionlistsrecipients` SQL verisinden geliyor. DB-backed recipient adapter `ID` ve `RecipientAddress` scalar'larini read-only aciyor; direct `DistributionListRecipients`/`DistributionListRecipient` aktivasyonu `E_ACCESSDENIED`, recipient mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `DistributionLists` ve `DistributionList` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `Domain -> DistributionLists` count/index/address/id lookup mevcut `hm_distributionlists` SQL verisinden geliyor. DB-backed distribution-list adapter `ID`, `Address`, `Active`, `RequireSMTPAuth`, `RequireSenderAddress`, ve `Mode` scalar'larini read-only aciyor; direct `DistributionLists`/`DistributionList` aktivasyonu `E_ACCESSDENIED`, list mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `Aliases` ve `Alias` COM kontratlari tam vtable/identity siralariyla eklendi; authenticated `Domain -> Aliases` count/index/name/id lookup mevcut `hm_aliases` SQL verisinden geliyor. DB-backed alias adapter yalniz `ID`, `DomainID`, `Name`, `Value`, `Active` scalar'larini read-only aciyor; direct `Aliases`/`Alias` aktivasyonu `E_ACCESSDENIED`, alias mutation'lari `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `Accounts` collection COM kontrati ve hosted `Accounts`/`Account` class identity'leri eklendi; authenticated `Domain -> Accounts` count/index/address/id lookup mevcut `hm_accounts` SQL verisinden geliyor. DB-backed account adapter yalniz `ID`, `DomainID`, `Address`, `Active`, `AdminLevel` scalar'larini read-only aciyor; direct `Accounts`/`Account` aktivasyonu `E_ACCESSDENIED`, account mutation'lari ve derin child collection'lar `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Legacy `Domains` ve `Domain` COM kontratlari tam vtable/identity siralariyla eklendi; hosted class manifest ve process-local service registration `Application`, `Settings`, `Domains`, `Domain`, `MessageIndexing` siniflarini kapsiyor. Authenticated `Application -> Domains` count/index/name/id lookup mevcut `hm_domains` SQL verisinden geliyor; direct `Domains`/`Domain` aktivasyonu `E_ACCESSDENIED`, mutations ve nested collections `E_NOTIMPL` kaliyor. Dar contract/COM host/store testleri ve opt-in izole SQL integration kapsami eklendi.
- Explicit opt-in SQL Server integration testi GUID isimli izole database olusturup siliyor ve real store uzerinden authenticated `Application -> Settings -> MessageIndexing` status, Enabled, queue, Clear ve Index akislarini dogruluyor; verilen connection string'deki database'e dokunmuyor. LocalDB canli kosusu basarili.
- Service build/publish authoritative legacy IDL'den `hMailServer.tlb` uretiyor. Saf manifest ve guarded install/uninstall wiring'i hosted COM siniflari icin AppID, CLSID, LocalServer32, versioned/version-independent ProgID, CurVer ve 64-bit TypeLib kayitlarini tamamliyor; normal build/test registry veya SCM'e dokunmuyor ve mevcut legacy service replacement acik opt-in gerektiriyor.
- Legacy installed 5.7 type library ile karsilastirilarak dual `Settings` IID'si, tam 142-accessor/method vtable sirasi, CLSID/versioned ProgID/default-interface metadata'si ve `MessageIndexing` DISPID 89 korundu. Authenticated yol `Application -> Settings -> MessageIndexing` olarak gercek runtime'a ulasiyor; diger Settings uyeleri yetki kontrolunden sonra acik `E_NOTIMPL`, direct Settings aktivasyonu `E_ACCESSDENIED` kaliyor.
- Service configured `InitializationFile`/`HMAILSERVER_INITIALIZATION_FILE` yolundan veya executable-directory `hMailServer.ini` varsayilanindan `[Security] AdministratorPassword` hash'ini yukluyor. Case-insensitive `Administrator` ve legacy MD5/salted-SHA256 dogrulamasi korunuyor; guvensiz empty-hash/empty-password anonymous administration davranisi legacy ve .NET 10'da fail-closed olarak sertlestirildi.
- Windows service dedicated MTA uzerindeki process-local host'a gercek `Application`, `Settings` ve `MessageIndexing` CLSID factory'lerini kaydediyor. Registry'siz process testleri authentication, authenticated child adapter, direct child access denial ve revoke davranislarini dogruluyor; guarded registry/type-library/service-install wiring'i de tamamlandi.
- Legacy installed 5.7 type library ile karsilastirilarak dual `Application` ve `Account` IID'leri, tam 20/61-member vtable sirasi, CLSID/versioned ProgID/default-interface metadata'si, server-admin account sonucu ve detached-account access-denied siniri korundu.
- SQL Server message-indexing administration store'u legacy delivered/indexed count'lari, persisted `MessageIndexing` ayarini, FTS/queue status'unu ve queue-driven `Clear`/`Index`/`Rebuild` islemlerini sagliyor. Service bu store-backed runtime'i process host'a configure ediyor; direct COM activation legacy gibi `E_ACCESSDENIED` kaliyor ve backfill processor `Enabled=false` iken lease almiyor.
- COM-visible `MessageIndexing` class'i legacy CLSID ve versioned `hMailServer.MessageIndexing.1` ProgID'sini koruyor, legacy interface'i default tutuyor, additive `IInterfaceMessageIndexing2` yuzeyini uyguluyor ve butun v1/v2 cagrilarini process host'un sagladigi zorunlu runtime'a delege ediyor. Version-independent ProgID/CurVer alias'i ve authenticated `Application -> Settings` factory yolu da tamamlandi.
- .NET COM assembly'si legacy dual `IInterfaceMessageIndexing` IID'sini, DISPID 1-5 uye seklini ve `Enabled` icin `VARIANT_BOOL` marshaling'ini koruyor; portable kontrat ve Windows COM-host hedefleri birlikte build ediliyor, legacy ve additive `IInterfaceMessageIndexing2` yuzeyleri reflection testleriyle kilitlendi.
- VBScript/JScript `OnClientValidatePassword` account facade'i legacy `Password` scalar'ini SQL'den okunan stored degerle tasiyor; attempted plaintext ayri `password` argumani olarak kaliyor.
- VBScript `OnClientValidatePassword` runner'i `Result.Parameter` alanini uninitialized `Empty` birakmak yerine legacy COM scalar parity'siyle acik numeric `0` olarak seed ediyor.
- JScript `OnClientValidatePassword` runner'i legacy `Result` constructor parity'siyle yazilabilir `Parameter = 0` alani tasiyor; VBScript ve genel event runner'lariyla scalar facade sekli esitlendi.
- Global VBScript/JScript `EventLog.Write(value)` facade'i rule script, `OnError`, ve password-validation script yollarinda legacy event log bicimiyle tamamlandi.
- External POP3 fetch hosted worker startup'ta stale `hm_fetchaccounts.falocked` satirlarini resetleyecek sekilde legacy `PersistentFetchAccount::UnlockAll()` davranisina yaklastirildi.
- External POP3 fetch ayni POP3 listing icindeki duplicate yeni UIDL degerlerini tek indirme/kuyruklama ile sinirlayacak sekilde kapatildi.
- External POP3 fetch ayni POP3 listing icindeki duplicate bilinen UIDL degerlerini tek script/retention cleanup ile sinirlayacak sekilde kapatildi.
- `OnExternalAccountDownload` fetch-account facade'i SQL lease'ten gelen `fanexttry`/`falocked` degerlerini legacy `NextDownloadTime` ve `IsLocked` script alanlari olarak yayacak sekilde genisletildi.
- `HMAILSERVER_CLIENT` facade'i legacy COM isimleri olan `Authenticated` ve `EncryptedConnection` alias'larini VBScript/JScript event handler'larinda destekleyecek sekilde genisletildi.
- External POP3 fetch duplicate persisted `hm_fetchaccounts_uids.uidvalue` satirlarini batch lookup olustururken tolere edecek sekilde kapatildi.
- External POP3 fetch UIDL satirlarini legacy `std::map` gibi artan sequence sirasinda isliyor; duplicate sequence icin son UID'yi tutup ayni remote slotu tek indirme/kuyruklama ile sinirliyor.
- External POP3 fetch yeni indirilen mesajlara script ve receiver islemlerinden once legacy `X-hMailServer-ExternalAccount: <account name>` basligini ekliyor.
- External POP3 fetch `OnExternalAccountDownload` icin negatif `Result.Parameter` degerlerini koruyor ve yeni/bilinen UID yollarinda legacy gibi immediate remote-delete uyguluyor.
- External POP3 fetch pozitif known-UID retention yasini takvim gunune kirmadan tam timestamp farkiyla hesapliyor; esit sinir tutuluyor, asildiginda remote mesaj siliniyor.
- External POP3 fetch normal sonlanan bos `RETR` payload'ini legacy account header eklendikten sonra script/receiver ve UID retention akisinda islemeye devam ediyor.
- External POP3 fetch configured MIME recipient header adlarinda legacy gibi yalniz ilk eslesen alani kullaniyor; duplicate alanlar ek recipient uretmiyor, tum `Received` alanlari ayrica taranmaya devam ediyor.
- External POP3 fetch `Received ... for` recipient degerlerini legacy 254 karakter/email regex'iyle dogruluyor; malformed adresler route acik olsa da account fallback'i asamiyor.
- External POP3 fetch `Received` recipient token'ini legacy `std::rfind` gibi case-sensitive ariyor; uppercase `FOR ` eslesmeyip account fallback recipient'ine donuyor.
- External POP3 fetch ilk parse edilen `From` mailbox'ini legacy 254 karakter/email validator'undan geciriyor; gecersiz veya limit ustu degerleri bos envelope sender'a dusuruyor.
- External POP3 fetch cozulmus recipient'lari legacy `RecipientParser::AddRecipient_` gibi yalniz case-insensitive final adrese gore tekillestiriyor; ayni mailbox'a giden farkli alias'larda ilkini tutuyor.
- External POP3 fetch configured MIME recipient listesi topluca parse edilemezse legacy quote/escape-aware comma compound'larina dusuyor; bozuk adres yanindaki gecerli adresi koruyor.
- External POP3 fetch whitespace-only fakat non-empty MIME recipient-header ayarinda legacy `StdString::IsEmpty()` gibi recipient islemine girip `Received ... for` taramasini koruyor.
- External POP3 fetch STARTTLS akisi legacy CAPA/STLS davranisina yaklastirildi: optional STARTTLS sadece STLS advertise edilmezse plaintext'e duser, required STARTTLS credentials gondermeden fail eder ve advertise edilip reddedilen STLS iki modda da credentials oncesi fail eder.
- External POP3 fetch CAPA reddi davranisi legacy ile sabitlendi: optional STARTTLS plaintext'e devam ederken required STARTTLS `USER`/`PASS` oncesi fail eder.
- External POP3 fetch reddedilen server greeting'inde plain ve STARTTLS modlarinda hicbir istemci komutu veya credential gondermeden fail edecek sekilde legacy ile sabitlendi.
- External POP3 fetch reddedilen `USER` komutunda plain ve optional-STARTTLS plaintext fallback yollarinda `PASS` gondermeden fail edecek sekilde legacy ile sabitlendi.
- External POP3 fetch reddedilen `PASS` komutunda plain ve optional-STARTTLS plaintext fallback yollarinda `UIDL` veya sonraki bir komut gondermeden fail edecek sekilde legacy ile sabitlendi.
- External POP3 fetch reddedilen `UIDL` komutunda plain ve optional-STARTTLS plaintext fallback yollarinda `RETR`/`DELE` gondermeden legacy `QUIT` cleanup yapacak sekilde sabitlendi.
- External POP3 fetch `UIDL +OK` sonrasi terminator gelmeden socket kapanirsa fatal kalacak, account failed-release edilecek ve receiver/UID/`RETR`/`DELE` yan etkisi uretilmeyecek sekilde testle sabitlendi.
- External POP3 fetch bos `UIDL` listing'de `RETR`/`DELE` gondermeden hesabi tamamlayacak ve remote server'da artik gorunmeyen known UID satirlarini silecek sekilde testle sabitlendi.
- External POP3 fetch malformed `UIDL` listing satirlarini atlayip ayni response icindeki gecerli satirlari islemeye devam edecek sekilde TCP parser testiyle sabitlendi.
- External POP3 fetch reddedilen `RETR` komutunda yalniz legacy `QUIT` cleanup yapacak, failed account lease'i release edecek ve receiver/UID/remote-delete yan etkisi uretmeyecek sekilde sabitlendi.
- External POP3 fetch `RETR +OK` sonrasi message body terminator gelmeden socket kapanirsa fatal kalacak, account failed-release edilecek ve receiver/UID/remote-delete yan etkisi uretilmeyecek sekilde testle sabitlendi.
- External POP3 fetch `DELE -ERR` yanitini legacy best-effort cleanup olarak kabul edip UID cleanup ve `QUIT` akisina devam edecek; socket/I/O/cancellation hatalarini fatal tutacak sekilde duzeltildi.
- External POP3 fetch `DELE` response gelmeden socket/I/O koparsa fatal kalacak, known UID korunacak ve account lease failed-release edilecek sekilde testle sabitlendi.
- External POP3 fetch session disposal sirasinda `QUIT -ERR` veya QUIT response oncesi disconnect exception sizdirmeyecek sekilde legacy best-effort cleanup testiyle sabitlendi.
- External POP3 fetch yeni mesaj byte'larinin onune hesap adini tasiyan legacy `X-hMailServer-ExternalAccount` basligini ekliyor; script girdisi ve receiver'a giden sonuc testle sabitlendi.
- External POP3 fetch negatif script retention parametrelerini sifira sikistirmiyor; hem yeni mesaj hem bilinen UID cleanup akisinda tum negatif degerler remote silme karari veriyor.
- External POP3 fetch known-UID yas hesabinda legacy `DateTimeSpan.GetNumberOfDays()` gibi kesirli elapsed gun kullaniyor; 47 saatlik UID 1 gun politikasinda silinirken tam 24 saatlik UID tutuluyor.
- External POP3 fetch sifir-byte `RETR` sonucunu erken hata yapmiyor; `X-hMailServer-ExternalAccount` basligi header-only mesaj olusturuyor ve normal queue/UID akisi testle sabitlendi.
- External POP3 fetch duplicate configured recipient header'larinda `MimeHeader::GetRawFieldValue` parity'siyle ilk degeri kullaniyor; validator ve receiver'a ikinci duplicate adres sizmiyor.
- External POP3 fetch `bad@@example.test` gibi bozuk `Received for` adreslerini legacy `StringParser::IsValidEmailAddress` sozlesmesiyle reddedip account recipient fallback'ine donuyor.
- External POP3 fetch uppercase `FOR ` belirtecini recipient token'i saymiyor; lowercase `for ` davranisi korunurken route recipient yerine account fallback secimi testle sabitlendi.
- External POP3 fetch 255 karakterlik `From` mailbox'ini envelope sender olarak sizdirmiyor; legacy 254 karakter siniri sonrasi bos sender ile receiver akisina devam ettigi testle sabitlendi.
- External POP3 fetch iki farkli MIME alias'i ayni `user@example.test` hesabina cozuldugunde receiver'a tek recipient veriyor ve ilk alias'in `OriginalAddress` degerini koruyor.
- External POP3 fetch `bad@@example.test` yanindaki `"Valid, Recipient" <valid@example.test>` adresini kaybetmiyor; quoted display-name virgulu compound'u bolmeden validator ve receiver'a gecerli adresi tasiyor.
- External POP3 fetch MIME recipient-header ayari `" "` oldugunda configured token uretmese de lowercase `Received for <alias@example.test>` recipient'ini validator ve receiver'a tasiyor.
- JScript password-validation handler'i `Result.Parameter` alanini ilk okumada numeric `0` goruyor ve alani yazip geri okuyabiliyor; eksik-property nedeniyle `undefined` donusu testle kapatildi.
- VBScript password-validation handler'i `Result.Parameter` alaninda `IsEmpty = False`, deger `0` ve yazilabilirlik sozlesmesini goruyor; class-field `Empty` farki testle kapatildi.
- Password-validation handler'lari `oAccount.Password = "legacy-password-hash"` ile `password = "attempted-secret"` degerlerini iki dilde ayri goruyor; stored/attempted ayrimi testle sabitlendi.
- `HMAILSERVER_MESSAGE.RefreshContent`, script tarafindan message file dogrudan degistirildikten sonra header/body alanlarini yeniden yukleyecek sekilde VBScript/JScript testleriyle sabitlendi.
- `HMAILSERVER_MESSAGE.FileName`/`Filename` facade'i script assignment sonrasi `Load`/`Save`/`Copy` file I/O'sunu orijinal runner backing path'inde tutacak sekilde legacy `Filename` read-only davranisina yaklastirildi.
- `HMAILSERVER_MESSAGE.To`/`CC` direct assignment, legacy COM read-only property sekline yaklastirildi; recipient/header mutasyonlari `AddRecipient`, `ClearRecipients`, `Recipients`, ve `HeaderValue` yollarinda kalacak sekilde testlendi.
- Attachment `FileName`/`Filename` ve `Size` metadata'si legacy COM read-only property sekline yaklastirildi; VBScript direct assignment'i reddederken JScript assignment'inin collection backing metadata'sini degistirmedigi testlendi.
- `HMAILSERVER_MESSAGE.ID`, `UID`, `State`, `DeliveryAttempt` ve `InternalDate` queue metadata'si legacy COM read-only property sekline yaklastirildi; VBScript assignment'i reddediyor, JScript canonical seed'leri `Load`/`Save`/`Copy` sinirlarinda geri yukluyor ve 64-bit message ID korunuyor. Legacy C++'taki gibi `State` ile message flags ayrildi; delivery eventleri `State = 1` ve queue `messageflags` degerini `Flag(eMessageFlag)` icin ayri seed ediyor.
- `HMAILSERVER_MESSAGE.Size`, legacy integer `bytes / 1024` floor-KiB hesabina cekildi; 1024 byte altindaki mesajlar `0` donuyor, property read-only kaliyor ve VBScript/JScript `Save` sonrasi backing file boyutunu yeniden okuyor.
- Recipient item `Address`, `OriginalAddress` ve `IsLocalUser` metadata'si legacy COM read-only property sekline yaklastirildi; VBScript assignment'i reddediyor, JScript detached snapshot donduruyor ve `AddRecipient`/`ClearRecipients` message-level mutasyonlari korunuyor.
- `Recipients` collection facade'inda legacy disi `Add`, `Clear` ve `ToHeaderValue` isimleri kaldirildi; `Count`/`Item` okumalari ile message-level `AddRecipient`/`ClearRecipients` mutasyonlari VBScript/JScript'te korunuyor.
- `HMAILSERVER_MESSAGE.AddRecipient`, bos display-name dahil recipient'lari legacy C++ bicimindeki quoted MIME adresiyle ve bosluksuz virgul birlestirmesiyle VBScript/JScript'te yaziyor.
- `HMAILSERVER_MESSAGE.ClearRecipients`, envelope recipient collection ile birlikte legacy C++ davranisindaki gibi `To`, `Cc` ve `Bcc` MIME header'larini VBScript/JScript'te temizliyor.
- `HMAILSERVER_MESSAGE.Save`, legacy C++ davranisindaki gibi bos `Date` degerine current local MIME date ekleyerek mesaji VBScript/JScript'te kaydediyor.
- `HMAILSERVER_MESSAGE.Body` ve `HTMLBody`, bos olmayan script atamalarini legacy `MessageData` davranisindaki gibi trailing `CRLF` ile kaydediyor; bos degerler bos kaliyor.
- `Headers` collection facade'inda runner-only `Refresh` ve `Commit` isimleri kaldirildi; legacy `Count`/`Item`/`ItemByName` okumalari ile header `Name`/`Value`/`Delete` mutasyonlari `Save` uzerinden korunuyor.
- `Recipients.Item`, `Headers.Item` ve `Headers.ItemByName`, gecersiz indeks veya eksik isimde `Nothing`/`null` yerine legacy `DISP_E_BADINDEX` sozlesmesine uygun script hatasi yukseltiyor.
- `Attachments` collection facade'inda runner-only `Load` ve `DeleteAt` isimleri kaldirildi; legacy `Count`/`Item`/`Clear`/`Add` ile attachment item `SaveAs`/`Delete` davranislari korunuyor.
- `Attachments.Add`, kaynak dosya yoksa sessizce donmek yerine legacy `Failed to attach file.` hatasini VBScript/JScript'te yukseltiyor.
- `Attachments.Item`, collection disindaki indekste `Nothing`/`null` dondurmek yerine legacy `DISP_E_BADINDEX` sozlesmesine uygun script hatasi yukseltiyor.
- Yakalanmis attachment item nesneleri collection'da daha onceki bir oge silinse de sabit kimligini koruyor; `Delete` VBScript/JScript'te legacy gibi ilk secilen MIME parcasini kaldiriyor.
- `HMAILSERVER_MESSAGE.HasBodyType`, ham header/body substring aramasi yerine legacy temiz MIME content-type davranisina cekildi; root ve iki nested part seviyesi, case-insensitive eslesme ve noktalivirgul iceren quoted boundary degerleri VBScript/JScript testleriyle sabitlendi.

Yeni thread baslamadan once yine `git status --short --branch` ve `git diff` okunmali. Calisma agaci temiz degilse once mevcut WIP'in kime ait oldugu ve hangi slice'a hizmet ettigi anlasilmali.

## Son Git Durumu

Branch:

```text
net10-modernization...origin/net10-modernization
```

Bu dokuman guncellemesinden once tamamlanan kod commit'i:

```text
74434165b feat(net10): expose crash simulation mode getter
```

Son 30 commit icinde one cikan son dilimler:

- `2087a4e1e feat(net10): expose script account password`
- `1e36992fb fix(net10): seed vb password result parameter`
- `d39e90ca4 fix(net10): seed password result parameter`
- `2db4de6cc fix(net10): scan received with blank fetch headers`
- `eb5a497ef fix(net10): recover valid fetched recipients`
- `cf8e965f9 fix(net10): deduplicate fetched alias recipients`
- `12542dfb5 fix(net10): validate external fetch senders`
- `eb239fe5b fix(net10): match received for token casing`
- `8d94cf12a fix(net10): validate fetched received recipients`
- `67471a5a0 fix(net10): use first fetch recipient header`
- `208705faf fix(net10): accept empty external fetch messages`
- `6b481c125 fix(net10): use elapsed fetch retention days`
- `fbb46edbc fix(net10): honor negative fetch retention`
- `4faa60ea9 fix(net10): tag external fetch messages`
- `49ef83587 fix(net10): order external fetch uidl sequences`
- `a1541a1a1 fix(net10): preserve script attachment identity`
- `78a4bfd5e fix(net10): terminate script message bodies`
- `24e703780 fix(net10): reject invalid script collection lookups`
- `3da58a9c6 fix(net10): reject invalid script attachment indexes`
- `25029bb0a fix(net10): fail missing script attachments`
- `49430b3b1 fix(net10): match script recipient header format`
- `697943ed0 fix(net10): add missing message date on save`
- `c8c92d92f fix(net10): clear script message blind recipients`
- `b5db584df fix(net10): match legacy script body type checks`
- `47df94c53 fix(net10): hide attachment collection helpers`
- `3869e31bf fix(net10): hide message header helpers`
- `aeed04e3b fix(net10): hide recipient collection mutators`
- `5a57de685 fix(net10): keep script recipient metadata readonly`
- `7bcb50f9d fix(net10): match legacy script message size`
- `ae404dcf0 fix(net10): separate script message state and flags`
- `59650f826 fix(net10): keep message queue metadata readonly`
- `cd22514b4 fix(net10): keep attachment metadata readonly`
- `9d899c20d fix(net10): keep script message recipient headers readonly`
- `0e93f5606 fix(net10): keep script message filename backing path stable`
- `49691e554 test(net10): cover message RefreshContent script facade`
- `dbc462807 test(net10): cover empty external fetch UIDL listings`
- `0b9c2d914 test(net10): cover malformed external fetch UIDL rows`
- `68cf89432 test(net10): cover truncated external fetch RETR bodies`
- `2c0ee55db test(net10): cover truncated external fetch UIDL listings`
- `c663cefe0 test(net10): cover external fetch QUIT cleanup failures`
- `7e40efe1a test(net10): cover external fetch DELE transport failures`
- `87d50855d fix(net10): tolerate rejected external fetch DELE`
- `c85ce6aa0 test(net10): cover rejected external fetch RETR`
- `d2e89fe68 test(net10): cover rejected external fetch UIDL`
- `691fe2532 test(net10): cover rejected external fetch PASS`
- `3c485df40 test(net10): cover rejected external fetch USER`
- `ab2710f72 test(net10): cover rejected external fetch greeting`
- `9e187a7fb test(net10): cover rejected external fetch CAPA`
- `79e02e4fe test(net10): cover rejected external fetch STLS`
- `0cb9152bb fix(net10): probe external fetch STLS capability`
- `f2048517a fix(net10): skip duplicate external fetch sequence entries`
- `bfd9916ac fix(net10): tolerate duplicate external fetch UID rows`
- `9470c2e53 feat(net10): add legacy client auth script aliases`
- `bb5e8c0df feat(net10): expose fetch account lock script fields`
- `79327bc45 fix(net10): skip duplicate known external fetch UIDs`
- `27df051c2 fix(net10): skip duplicate external fetch UIDs`
- `718108bf6 fix(net10): reset external fetch locks on startup`
- `f65bb2a05 feat(net10): expose script event log facade`
- `254e118da feat(net10): dispatch legacy OnError scripts`
- `03df16257 feat(net10): support scripted message folder copies`
- `9a0fc5f41 feat(net10): run client connect events for IMAP and POP3`
- `c703f48de feat(net10): add SQL greylisting checks`
- `ce5693bc1 feat(net10): add sender domain MX checks`
- `b7462af49 feat(net10): add optional reverse DNS checks`
- `76f6b0d2a feat(net10): support IMAP search sequence sets`
- `4603eb773 perf(net10): stream SQL search result readers`
- `8cb42b48d perf(net10): reduce IMAP search result allocations`

Bu dokumanin onceki surumundeki EventLog dirty-WIP notu artik gecerli degil; ilgili slice testlenip commit/push edildi.

## Build/Test Komutlari

.NET 10 on kosul kontrolu:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\check-net10-prereqs.ps1 -RequireMsBuild
```

.NET 10 build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build-net10.ps1 -Configuration Debug
```

.NET 10 test:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\test-net10.ps1 -Configuration Debug
```

Legacy C++ build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build.ps1 -Configuration Debug
```

Legacy regression test build/run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\build-tests.ps1 -Configuration Debug
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\run-tests.ps1
```

`build/build-net10.ps1` su projeleri tek tek build eder:

- `HMailServer.Service`
- `HMailServer.Indexing`
- `HMailServer.Delivery`
- `HMailServer.ComInterop`

`build/test-net10.ps1`, `hmailserver/source/Server.Net10/tests/HMailServer.Net10.Tests/HMailServer.Net10.Tests.csproj` uzerinden MSTest calistirir.

`tools/dotnet10/dotnet.exe` varsa scriptler onu kullanir; yoksa PATH'teki `dotnet` kullanilir.

## Son Build/Test Ciktisi

Son temiz dogrulama notlari:

- Account/IMAPFolder Messages COM metadata dilimi icin dar `MessagesComContractTests|SqlServerMessageAdministrationStoreTests|ImapFoldersComContractTests|LegacyComRegistrationManifestTests|SqlServerMessageIndexingIntegrationTests` filtresi 33/33 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 899/899 gecti.
- Message MIME COM read-only surface dilimi icin dar `MessagesComContractTests|SqlServerMessageAdministrationStoreTests|LegacyComRegistrationManifestTests` filtresi 19/19 gecti; prereq kontrolu temizdi, Net10 Debug build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 901/901 gecti.
- IMAPFolder SubFolders COM read-only dilimi icin dar `ImapFoldersComContractTests|SqlServerImapFolderAdministrationStoreTests|SettingsComContractTests|SqlServerMessageIndexingIntegrationTests` filtresi 38/38 gecti; prereq kontrolu temizdi, Net10 Debug build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 903/903 gecti.
- EventLog facade dilimi icin Net10 build basariliydi ve full Net10 testler 327/327 gecmisti.
- External fetch stale-lock startup reset dilimi icin dar `ExternalFetchProcessorTests` filtresi 9/9 gecti; ardindan Net10 build basarili oldu ve full Net10 testler 328/328 gecti.
- External fetch duplicate UIDL dilimi icin dar `ExternalFetchProcessorTests` filtresi 10/10 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 329/329 gecti.
- External fetch duplicate known UIDL dilimi icin dar `ExternalFetchProcessorTests` filtresi 11/11 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 330/330 gecti.
- Fetch-account script facade `NextDownloadTime`/`IsLocked` dilimi icin dar `WindowsScriptRuleExecutorTests|SqlServerExternalFetchAccountStoreTests` filtresi 34/34 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 330/330 gecti.
- Client script facade alias dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 30/30 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 330/330 gecti.
- External fetch duplicate persisted known-UID row dilimi icin dar `ExternalFetchProcessorTests` filtresi 12/12 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 331/331 gecti.
- External fetch duplicate remote sequence dilimi icin dar `ExternalFetchProcessorTests` filtresi 13/13 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 332/332 gecti.
- External fetch STLS CAPA probing dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 3/3 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 334/334 gecti.
- External fetch rejected-STLS parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 5/5 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 336/336 gecti.
- External fetch rejected-CAPA parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 7/7 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 338/338 gecti.
- External fetch rejected-greeting parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 10/10 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 341/341 gecti.
- External fetch rejected-USER parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 12/12 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 343/343 gecti.
- External fetch rejected-PASS parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 14/14 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 345/345 gecti.
- External fetch rejected-UIDL parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 16/16 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 347/347 gecti.
- External fetch rejected-RETR parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 32/32 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 350/350 gecti.
- External fetch best-effort DELE parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 34/34 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 352/352 gecti.
- External fetch DELE transport-failure parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 37/37 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 355/355 gecti.
- External fetch QUIT cleanup parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 26/26 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 359/359 gecti.
- External fetch truncated-UIDL-listing parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 362/362 gecti.
- External fetch truncated-RETR-body parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 47/47 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 365/365 gecti.
- External fetch malformed-UIDL-row parity dilimi icin dar `TcpExternalFetchSessionFactoryTests` filtresi 32/32 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 367/367 gecti.
- External fetch empty-UIDL-listing parity dilimi icin dar `TcpExternalFetchSessionFactoryTests|ExternalFetchProcessorTests` filtresi 52/52 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 370/370 gecti.
- `HMAILSERVER_MESSAGE.RefreshContent` script facade dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 32/32 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 372/372 gecti.
- `HMAILSERVER_MESSAGE.FileName`/`Filename` backing-path parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 34/34 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 374/374 gecti.
- `HMAILSERVER_MESSAGE.To`/`CC` read-only direct-assignment parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 36/36 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 376/376 gecti.
- Attachment `FileName`/`Filename`/`Size` read-only metadata parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 38/38 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 378/378 gecti.
- Message `ID`/`UID`/`DeliveryAttempt`/`InternalDate` read-only queue metadata parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 40/40 gecti; 64-bit message ID seed'i dogrulandi, prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 380/380 gecti.
- Message `State`/`Flag(eMessageFlag)` ayrimi dilimi icin dar `WindowsScriptRuleExecutorTests|DeliveryQueueProcessorTests` filtresi 50/50 gecti; delivery event `State = 1` ve queue flag seed'leri ayri dogrulandi, prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 380/380 gecti.
- Message `Size` read-only floor-KiB ve `Save` sonrasi yeniden olcum parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 42/42 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 382/382 gecti.
- Recipient item `Address`/`OriginalAddress`/`IsLocalUser` read-only metadata parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Recipient collection `Count`/`Item` legacy surface parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Message header collection `Count`/`Item`/`ItemByName` legacy surface parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Attachment collection `Count`/`Item`/`Clear`/`Add` legacy surface parity dilimi icin dar `WindowsScriptRuleExecutorTests` filtresi 44/44 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 384/384 gecti.
- Message `HasBodyType` MIME part parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 46/46 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 386/386 gecti.
- Message `ClearRecipients` Bcc cleanup parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 46/46 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 386/386 gecti.
- Message `Save` missing-date parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- Message `AddRecipient` legacy MIME header format parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- `Attachments.Add` missing-file error parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- `Attachments.Item` bad-index parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- Recipient/header collection bad-index parity dilimi icin dort hedefli VBScript/JScript testi 4/4 ve dar `WindowsScriptRuleExecutorTests` filtresi 48/48 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 388/388 gecti.
- Message `Body`/`HTMLBody` trailing-CRLF parity dilimi icin dort hedefli VBScript/JScript testi 4/4 ve dar `WindowsScriptRuleExecutorTests` filtresi 50/50 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- Attachment item stable-identity parity dilimi icin iki hedefli VBScript/JScript testi 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 50/50 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- External fetch UIDL ordered-map parity dilimi icin hedefli duplicate/out-of-order testi 1/1 ve dar `ExternalFetchProcessorTests` filtresi 18/18 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- External fetch legacy account-header parity dilimi icin hedefli script/receiver testi 1/1 ve dar `ExternalFetchProcessorTests` filtresi 18/18 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 390/390 gecti.
- External fetch negatif retention parity dilimi icin iki processor ve bir gercek VBScript hedefli testi 3/3, birlesik `ExternalFetchProcessorTests|WindowsScriptRuleExecutorTests` filtresi 70/70 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 392/392 gecti.
- External fetch elapsed-retention parity dilimi icin 47 saat/sil ve tam 24 saat/tut hedefli testleri 2/2, dar `ExternalFetchProcessorTests` filtresi 22/22 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 394/394 gecti.
- External fetch bos-RETR parity dilimi icin processor ve loopback TCP hedefli testleri 2/2, birlesik `ExternalFetchProcessorTests|TcpExternalFetchSessionFactoryTests` filtresi 58/58 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 396/396 gecti.
- External fetch duplicate configured-recipient-header parity dilimi icin hedefli test 1/1 ve dar `ExternalFetchProcessorTests` filtresi 24/24 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 397/397 gecti.
- External fetch malformed `Received for` recipient parity dilimi icin gecerli/bozuk hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 25/25 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 398/398 gecti.
- External fetch `Received for` token-casing parity dilimi icin lowercase/uppercase hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 26/26 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 399/399 gecti.
- External fetch sender-validation parity dilimi icin gecerli/255-karakter hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 27/27 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 400/400 gecti.
- External fetch alias-recipient dedup parity dilimi icin alias/duplicate-header hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 28/28 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 401/401 gecti.
- External fetch malformed-neighbor recipient parity dilimi icin quoted-comma hedefli test 1/1 ve dar `ExternalFetchProcessorTests` filtresi 29/29 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 402/402 gecti.
- External fetch whitespace-header gate parity dilimi icin whitespace/normal hedefli testler 2/2 ve dar `ExternalFetchProcessorTests` filtresi 30/30 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 403/403 gecti.
- JScript password-validation `Result.Parameter` parity dilimi icin default/reject hedefli testler 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 51/51 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 404/404 gecti.
- VBScript password-validation `Result.Parameter` parity dilimi icin VB/JScript default hedefli testler 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 52/52 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 405/405 gecti.
- Password-validation stored-account-password parity dilimi icin VB/JScript stored/attempted hedefleri 2/2 ve dar `WindowsScriptRuleExecutorTests` filtresi 54/54 gecti; prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 407/407 gecti.
- Legacy message-indexing COM kontrat dilimi icin once eksik `IInterfaceMessageIndexing` derleme hatasiyla kanitlandi; interface eklendikten sonra dar `MessageIndexingComContractTests` filtresi 2/2 gecti. Prereq kontrolu temizdi, Net10 build portable ve Windows COM-host assembly'lerini 0 uyari/0 hata ile uretti ve full Net10 testler 409/409 gecti.
- Message-indexing COM class/runtime-adapter dilimi icin eksik class/runtime once derleme hatasiyla kanitlandi; legacy CLSID/versioned ProgID/default-interface metadata'si ve v1/v2 delegasyonu icin dar `MessageIndexingComContractTests` filtresi 5/5 gecti. Sonraki authorization duzeltmesi direct parameterless activation'i legacy `E_ACCESSDENIED` davranisina cekti.
- SQL/service message-indexing runtime dilimi icin store-backed adapter, SQL command sekilleri, authorized host factory ve disabled backfill gate testleriyle birlesik dar filtre 14/14 gecti. Prereq kontrolu temizdi, Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu ve full Net10 testler 418/418 gecti.
- Service-process COM local-server host dilimi once eksik host API derleme hatasiyla kanitlandi; registry'siz process activation/revoke testi ve mevcut COM contract testleri 7/7 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 419/419 gecti.
- COM Application/auth-root dilimi once eksik contract derleme hatasiyla kanitlandi; legacy metadata/vtable, credential ve process activation testleri 11/11 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 429/429 gecti.
- Account Rules COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 493/493 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build 0 uyari/0 hata ile basarili oldu.
- Account IMAPFolders COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path kapsami legacy modified UTF-7 ornekleri ve root/account filtreleriyle 10/10, full Net10 testler 500/500, opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings PublicFolders COM dilimi once authorized Settings yolunda `E_NOTIMPL` ile kanitlandi; dar Settings/folder/SQL-path filtresi 13/13, full Net10 testler 501/501 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings Routes COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 507/507 ve opt-in izole LocalDB integration testleri duzeltilen sequential-reader ordinal sirasi sonrasinda 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Application global Rules COM dilimi once `Application.Rules` yolunda `E_NOTIMPL` ile kanitlandi; dar Application/SQL-path filtresi 9/9, full Net10 testler 508/508 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings IncomingRelays COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 514/514 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings TCPIPPorts COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 10/10, full Net10 testler 521/521 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings SecurityRanges COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 527/527 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings SSLCertificates COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 533/533 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Settings Groups COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/manifest/SQL-path filtresi 9/9, full Net10 testler 539/539 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Group Members COM dilimi once eksik contract/store derleme hatasiyla kanitlandi; dar contract/store/Groups/manifest/SQL-path filtresi 14/14, full Net10 testler 545/545 ve opt-in izole LocalDB integration testleri 6/6 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Application Utilities COM dilimi once eksik contract/enum/class derleme hatasiyla kanitlandi; dar Utilities/Application/manifest/process-host filtresi 21/21, full Net10 testler 582/582 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- Application Links COM dilimi once eksik contract/class/runtime derleme hatasiyla kanitlandi; dar Links/Application/manifest/process-host filtresi 23/23, full Net10 testler 589/589 gecti. Net10 build portable/Windows COM hedeflerinde 0 uyari/0 hata ile basarili oldu.
- 28 Haziran security revalidation filtresi VBScript/JScript password, delivery/external-UID escaping, administrator authentication ve ClamAV kapsamini birlikte 10/10 gecti.
- DKIM header crypto dilimi icin dar `DkimEvaluationTests` filtresi 23/23 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 655/655 gecti.
- DKIM DNS/public-key lookup dilimi icin dar `DkimEvaluationTests` filtresi 31/31 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 663/663 gecti.
- DKIM message-level verifier dilimi icin dar `DkimEvaluationTests` filtresi 37/37 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 669/669 gecti.
- DKIM disabled SMTP policy boundary dilimi icin dar `SmtpDkimPolicyTests` filtresi 5/5 ve receiver DKIM filtresi 2/2 gecti. Prereq kontrolu temizdi, Net10 build 0 uyari/0 hata ile basarili oldu ve full Net10 testler 676/676 gecti.

Terminal/log incelemesi:

- Aktif terminalde eski basarisiz build/test ciktisi yoktu.
- `hmailserver/source/Server.Net10/tests/HMailServer.Net10.Tests/TestResults` altinda MSTest deploy klasorleri var, fakat `.trx` sonuc dosyasi bulunmadi.
- `<workspace-root>\build-logs` altinda son gorunen loglar OpenSSL/PostgreSQL dependency build loglari; Net10 test/build failure logu degil.

## Bilinen Riskler

- Dirty WIP kod dosyalari tamamlanmadan commit/push edilmemeli.
- Script host parity hassas: VBScript/JScript quoting, CR/LF sanitization, temp dosya lifecycle, fail-open/fail-closed semantikleri ve legacy `Result.Value` anlamlari kolay bozulabilir.
- Delivery queue degisiklikleri duplicate delivery, mail kaybi veya yanlis bounce uretebilir.
- SQL FTS/search degisiklikleri hot path performansini ve data-directory fallback davranisini etkileyebilir.
- Anti-abuse kontrollerinde DNS/SQL/socket timeout kararlari mail kabulunu durdurabilir; dokumante edilen fail-open/fail-closed politikaya bagli kal.
- COM/GUID/ProgID/DISPID degisiklikleri Administrator ve dis otomasyonlari kirar.
- Migration DDL'i additive kalmali; eski hMailServer DB'lerinde destructive veya implicit data conversion riski alinmamali.

## Dokunulmamasi Gereken Hassas Alanlar

- Legacy C++ davranisi referans olarak okunmali; uyumluluk amacli degilse gereksiz degistirilmemeli.
- `hmailserver/source/DBScripts/Upgrade5708to6000MSSQL.sql`
- `hmailserver/source/Server.Net10/src/HMailServer.ComInterop`
- Script executor ve facade dosyalari, ozellikle legacy event/object sozlesmeleri.
- IMAP/SMTP/POP3 parser/session hot path'leri.
- Delivery lease/retry/bounce/status persistence kodlari.
- SQL-backed mailbox/search/indexing store'lari.

## Siradaki Onerilen 3 Milestone

1. Legacy script object model parity tamamlama.
   - EventLog.Write tamamlandi; bundan sonra eksik global objeler, account/domain/application facade metodlari ve script collection davranislari legacy testlerle kapatilmali.

2. COM/Admin ve migration operasyonlarini production gate'e tasima.
   - GUID/ProgID/DISPID sozlesmeleri icin compatibility testleri.
   - In-place upgrade runner, backup zorunlulugu, rollback-from-backup akisi, service install/uninstall ve operator dokumani.

3. Security + performance acceptance.
   - DKIM signing/setter/Admin mutation wiring ve DMARC enforcement/Admin policy wiring.
   - SQL Server FTS integration ve 100k mailbox SEARCH/SORT p95 hedefi.
   - 1k concurrent IMAP, SMTP queue latency, delivery throughput ve uzun soak memory/handle testleri.

## SEC-18 Fresh Non-Pool Denial Matrix (2026-07-23)

- The isolated matrix completed without touching the installed `hMailServer.Application` registration or starting the hMailServer service. The real PHP/FastCGI request reached the temporary COM method under `IIS APPPOOL\HMailWebAdminBrokerPool`, and the server captured `CoImpersonateClient=0`, `OpenThreadToken=0`, exact caller SID equality, `CoRevertToSelf=0`, and residual `ERROR_NO_TOKEN=1008`. A wrong-expected-SID control reached the method and rejected with server `0x80070005` and PHP `0x80020009`. A genuinely non-pool medium-integrity desktop PowerShell process was denied at activation with `0x80070005`; no interface/method entry occurred and invocation-counter delta was zero.
- Evidence: `artifacts/sec18-staging/SEC18-nonpool-denial-exact-20260723-report.json`/`.md`, `SEC18-authorized-pool-evidence-exact-20260723.json`, `SEC18-method-wrong-sid-exact-20260723.json`, `SEC18-nonpool-denial-exact-20260723.json`, `SEC18-process-evidence-exact-20260723.json`, `staging-inventory-20260723-sec18-exact.json`, `SEC18-installed-application-graph-evidence-20260723-post-probe.json`, and `SEC18-temporary-probe-cleanup-20260723.json`.
- Rollback passed after adding the disposable `sec18-identity.php` diagnostic to `rollback-sec18-nonpool-probe-20260722.ps1`: both Registry64/Registry32 temporary AppID/CLSID/ProgID keys are absent, the temporary service/process/endpoints/probe directories are absent, the IIS site/pool/PHP staging remains, and the hMailServer service remains stopped/disabled. Installed Application graph snapshots remain 22 paths/44 snapshots with identical snapshot hash `EB0D90539F2F0DD8E9F3A92755F127D30D571DED43B0A2492A416F05D05BF6A0` before/after.
- Independent `hmail_security_reviewer` and `hmail_reality_checker` decisions are RED for permanent broker registration. Remaining gate work is immutable, stage-bound counter/response attestation with a separately launched non-pool client, explicit activation/interface HRESULT capture, and fresh review. Permanent broker registration, DCOM ACL writes, PHP cutover, installed Application activation, production service/database/data/firewall changes, and SMTP/IMAP behavior remain out of scope.
- Validation: `test-webadmin-broker-staging-inventory.ps1` passed; `test-sec18-installed-application-graph-evidence.ps1` passed; full `build/test-net10.ps1 -Configuration Debug` passed `1198/1198` with one opt-in native-registry test skipped. No production code changed in this staging slice.

## SEC-18 Live-Bound Evidence Attestation (2026-07-26, superseded)

- Added live hMailServer service identity/state binding in `build/get-webadmin-broker-staging-inventory.ps1`, `build/attest-sec18-denial-evidence.ps1`, and the focused attestation test in code/test commit `c3444829b`, after the cleanup/provenance hardening commit `686ad8179`. The fresh collector records exact service name `hMailServer`, `Stopped`/`Disabled` state, no matching process, and binds those values to verified cleanup evidence; the attester now hashes fourteen sources and passes 16/16 checks.
- `artifacts/sec18-staging/SEC18-denial-evidence-attestation-live-bound-20260726.json` is `EvidenceReadyForIndependentReview` with 16/16 checks passed and `ReadyForBrokerRegistration=false`; the companion Markdown report records the same result. Fresh independent reviewers rate bounded evidence integrity YELLOW because the bundle remains self-attested without a detached/external signature, cleanup evidence is not time-correlated to the fresh collector, caller-token freshness is not enforced, and process-read errors are not fail-closed. Permanent broker registration/implementation remains RED because broker-only AppID authorization and per-method caller enforcement are not implemented or approved.
- Validation: `test-sec18-denial-evidence-attestation.ps1`, `test-sec18-installed-application-graph-evidence.ps1`, and `test-webadmin-broker-staging-inventory.ps1` pass; full `build/test-net10.ps1 -Configuration Debug` passes `1198` with one opt-in native-registry test skipped. Rollback exit code is `0`; temporary registry keys, service/process, endpoints, client helper, and probe paths are absent; hMailServer remains stopped/disabled.

## SEC-18 Freshness and Fail-Closed Collector (2026-07-26)

- Code/test commit `d7048d467` closes the next two bounded collector gaps. `build/get-webadmin-broker-staging-inventory.ps1` now requires a parseable `observedUtc` no more than 300 seconds old and no more than 30 seconds in the future, requires the caller probe `correlationId` to equal an explicit `CollectorInvocationId`, and records service/process read errors. `Get-HMailServerServiceEvidence` treats only an expected not-found result as no process; query failures leave `ProcessPresent` unknown and make the gate incomplete.
- `build/attest-sec18-denial-evidence.ps1` independently parses `CollectedUtc`/`ObservedUtc`, verifies the shared invocation ID and bounded age, and adds separate freshness/correlation and service-read fail-closed checks. Focused collector, attester, registry-binary, and graph tests pass; the full Net10 suite passes `1198` with one opt-in native-registry test skipped.
- The prior `artifacts/sec18-staging/staging-inventory-20260726-live-bound.json` was collected before this contract. A read-only rerun using its caller evidence produces `artifacts/sec18-staging/staging-inventory-20260726-freshness-gate.json` with `exit 2`, `Status=Incomplete`, `TimestampFresh=false`, and matching correlation, proving stale evidence is rejected. No production installation, service, database, data directory, COM registration, DCOM ACL, firewall, or PHP behavior changed.
- Fresh independent reviewers rate this bounded slice YELLOW and permanent broker approval RED. They additionally require explicit attester rejection of `ServiceReadError`, freshness binding to the final collector timestamp, a collector-issued challenge or external seal instead of operator-controlled equality, and canonical AppID/output path restrictions. The superseded 16-check artifact is historical evidence only and must not be used as approval evidence.

## SEC-18 Final Validation and Service-Read Attestation (2026-07-26)

- Code/test commit `16e8b431f` binds caller freshness to `CollectedUtc` captured after registry, IIS, service, and path reads, while preserving `CollectionStartedUtc` for duration evidence. The attester now explicitly requires empty `ServiceReadError`, `ProcessReadError`, and aggregate `ReadError`; the focused fixture includes process-only and service-only failure cases.
- Focused collector, attester, registry-binary, and installed-Application graph tests pass. Full `build/test-net10.ps1 -Configuration Debug` passes `1198`, with one opt-in native-registry test skipped. The read-only `staging-inventory-20260726-final-timestamp-gate.json` rerun remains `Incomplete`/exit `2` because the old caller evidence is stale; no fresh authorized probe or production state change occurred.
- Reviewers confirm the explicit `ServiceReadError` control is green. The remaining timestamp is only post-host-read and is captured before caller validation/final gate calculation, so it is not yet a trusted final-state seal; the collector still accepts operator-supplied correlation equality and unrestricted AppID/output paths. Bounded evidence remains YELLOW and permanent broker approval RED.
- The remaining production-gate work is a fresh isolated authorized/non-pool matrix with one shared invocation ID, collector-issued challenge or external evidence seal, and canonical AppID/output path restrictions. Permanent broker registration, DCOM ACL writes, PHP session cutover, and existing Application activation remain blocked.

## SEC-18 Canonical AppID and Bounded Evidence Output (2026-07-26)

- Code/test commit `6d40f1b6d` hardens `build/get-webadmin-broker-staging-inventory.ps1` and `build/attest-sec18-denial-evidence.ps1` without changing production hMailServer registration, DCOM permissions, service state, database, data directory, IIS configuration, PHP behavior, or protocol behavior. Both scripts require the installed Application AppID `{5EDEC473-39E0-43F6-A234-1947071721C8}`; output paths must resolve under `artifacts/sec18-staging`, every existing directory in the path ancestry is checked for reparse points, existing files/directories are rejected, and evidence uses exclusive `CreateNew` writes. The attester independently adds `collector-application-appid`, producing 19 checks across 14 source hashes.
- Focused `test-webadmin-broker-staging-inventory.ps1` covers non-canonical AppID, outside-root, existing-output, caller freshness/correlation, and service-process read failures. `test-sec18-denial-evidence-attestation.ps1` covers non-canonical collector evidence, outside-root/no-clobber outputs, stale caller evidence, process-only and service-only read errors, cleanup, correlation, and duplicate JSON. Registry-binary and installed-Application graph tests pass. Full `build/test-net10.ps1 -Configuration Debug` passes `1198`, with one opt-in native-registry test skipped.
- The read-only `staging-inventory-20260726-canonical-output-gate.json` rerun uses the canonical AppID and clean `hMailServer` Stopped/Disabled/no-process read state but exits `2` because the old caller evidence is stale (`TimestampFresh=false`). It is not approval evidence. Bounded evidence remains YELLOW; permanent broker registration remains RED. The post-host-read timestamp is not a trusted final-state seal, and `CollectorInvocationId` equality is still operator-supplied rather than a collector-issued challenge.
- Independent reviewers keep the gate RED: evidence inputs are not externally authenticated, AppID enforcement is syntactic rather than semantic graph validation, authorized HRESULTs are only presence-checked, `ProcessEvidencePath` is not consumed, endpoint cleanup is not attested, and the output directory check has a TOCTOU race. These are residual review blockers; this slice did not broaden into their implementation.
- Current next slice: rerun the isolated authorized PHP/FastCGI and genuine non-pool denial matrix with one shared invocation ID and fresh evidence. Then add collector-issued challenge/final-state sealing and externally pin or sign the complete manifest before fresh independent review. Do not register the broker, change DCOM ACLs, activate the installed Application, cut over PHP sessions, or alter production service/database/data/firewall/SMTP/IMAP state.

## SEC-18 Non-Pool Denial Recheck (2026-07-26)

- The approved negative-test continuation ran under the current desktop identity `NOUTML-KANDIL\Kandil` at medium integrity without elevation. No temporary standard user was created because the existing desktop denial was sufficient for the bounded negative test. The historical matrix records COM activation `0x80070005` (`E_ACCESSDENIED`) before interface/method entry and an invocation-counter delta of zero; interface, method, impersonation, token, and SID stages therefore have no result for that denied request. Historical authorized evidence separately records `CoImpersonateClient=0`, `OpenThreadToken=0`, exact pool-SID equality, `CoRevertToSelf=0`, and residual `ERROR_NO_TOKEN=1008`.
- Current readback confirms the temporary probe service/process, both Registry32/Registry64 probe AppID/CLSID/ProgID keys, and probe paths are absent. `hMailServer` is `Stopped`/`Disabled` with no process. The installed Application graph pre/post evidence remains equal and no production database, data directory, service start, installed Application activation, DCOM ACL, firewall, or PHP authentication change occurred.
- The fresh non-elevated collector output `artifacts/sec18-staging/staging-inventory-20260726-nonpool-current.json` is intentionally `Incomplete` because the desktop cannot read IIS mappings and no fresh server-generated caller-token evidence was supplied. The current attestation `SEC18-denial-evidence-attestation-20260726-nonpool-current.json` exits `2` with `14/19` checks passed, proving the gate remains fail-closed rather than silently accepting stale evidence. `test-sec18-denial-evidence-attestation.ps1`, `test-webadmin-broker-staging-inventory.ps1`, and `test-webadmin-broker-staging-inventory-regbinary.ps1` pass; full Net10 passes `1198` with one opt-in native-registry test skipped.
- Independent `hmail_security_reviewer` and `hmail_reality_checker` both returned RED. Permanent broker registration remains blocked: the next SEC-18 slice is an elevated isolated authorized PHP/FastCGI plus genuine non-pool matrix with one collector-issued challenge/correlation, immutable counter/final-state binding, current script hashes, and fresh independent review. Do not add non-pool principals, broaden the probe ACL, register the broker, change DCOM defaults, activate `hMailServer.Application`, or cut over PHP sessions.

## SEC-12 Account ValidatePassword Authorization Fence (2026-07-26)

- Parity explorer confirmed legacy `InterfaceAccount::ValidatePassword` (DISPID 22) calls `PasswordValidator::ValidatePassword` directly for attached objects, while detached objects return `E_ACCESSDENIED`; protocol `AccountLogon::Logon` carries client-aware `hm_logon_failures`/`hm_securityranges` auto-ban behavior that the COM call does not have.
- Test commit `aab2e307e` adds focused `AccountsComContractTests` coverage proving direct `Account.ValidatePassword` remains `E_ACCESSDENIED` and authenticated SQL-backed account snapshots remain explicit `E_NOTIMPL`. This intentionally does not add password lookup, SQL access, auto-ban writes, or protocol behavior, and preserves Account IID `{E5EDC050-0899-4A3B-BF4C-420212FC3895}`, CLSID `{369BE902-9F27-4722-A29F-3059E4D7021D}`, ProgID `hMailServer.Account.1`, and installed dispatch shape.
- Focused Accounts tests pass `16/16`; full Net10 passed `1198` at the fence commit, with one opt-in native-registry test skipped. The caller-aware boundary is now recorded in the following section; the COM method remains unavailable until a trusted caller-aware COM boundary exists. SEC-18 remains RED for bridge registration and fresh evidence/challenge sealing.
- Independent review is GREEN for this fence and RED for enabling the method. The coverage is process-local/in-memory; an installed out-of-process `CoCreateInstance`/DISPID 22 check remains a later opt-in validation item and is not required to justify keeping the method disabled.

## SEC-12 Caller-Aware Authentication Boundary (2026-07-26)

- The parity explorer confirmed the legacy split: `AccountLogon::Logon` receives the remote client IP, calls `PasswordValidator::ValidatePassword`, and records threshold failures in `hm_logon_failures`/`hm_securityranges`; COM `InterfaceAccount::ValidatePassword` carries only the password and bypasses that client-aware path. The .NET implementation remains fenced at `AccountComClass.ValidatePassword`/`Account.cs` and preserves the installed Account IID/CLSID/ProgID/DISPID shape.
- Code/test commit `acd52d43a` adds `ClientAuthenticationCaller`, `ClientAuthenticationRequest`, `ClientAuthenticationResult`, `IClientAwareAuthenticationService`, and `ClientAwareAuthenticationService`. The service composes `IImapAccountAuthenticator` with `IAutoBanLogonFailureRecorder`, carries parsed client IP plus `Imap`/`Pop3`/`Smtp` caller kind, records only failed or account-less authentication, preserves invalid-IP skip behavior, propagates cancellation, and contains recorder failures as the prior protocol helper did. `ImapSession`, `Pop3Session`, and `SmtpSession` use the boundary while retaining their existing constructor dependencies and protocol responses; `HMailServer.Service.Program` registers the shared service.
- Focused `ClientAwareAuthenticationServiceTests` pass `5/5`; the existing IMAP/POP3/SMTP session filters pass `65/65`; full Net10 passes `1203` with one opt-in native-registry test skipped. No SQL/live database integration, COM registration, installed/out-of-process COM invocation, or production service/data change occurred.
- Independent security review is GREEN for this bounded slice. Reality review is YELLOW: the fake-service tests and existing protocol tests do not yet assert injected caller metadata end to end, no host DI smoke test proves the runtime constructor graph, no isolated SQL threshold integration test was added, and `ClientAuthenticationRequest.Caller` is carried as metadata rather than selecting caller-specific policy. Keep `Account.ValidatePassword` at `E_NOTIMPL` for authorized snapshots and `E_ACCESSDENIED` for detached objects.
- Current next slice: add injected-session tests for all three caller kinds and client IP, a no-database DI smoke test, and isolated SQL recorder threshold/disconnect coverage. Do not enable COM password validation or alter COM identity until a trusted caller-aware COM boundary is separately designed and reviewed.

## SEC-12 Injected Session Caller Propagation (2026-07-26)

- Legacy references remain `AccountLogon::Logon` (`hmailserver/source/Server/Common/Util/AccountLogon.cpp:44-95`), the IMAP login/auth commands (`hmailserver/source/Server/IMAP/IMAPCommandLogin.cpp:55-65`, `IMAPCommandAuthenticate.cpp:117-127`), `POP3Connection` (`hmailserver/source/Server/POP3/POP3Connection.cpp:447-455`), and `SMTPConnection` (`hmailserver/source/Server/SMTP/SMTPConnection.cpp:2116-2127`). They pass the socket remote endpoint into password validation, record failed IP-aware logons, and honor the disconnect result. The COM `InterfaceAccount::ValidatePassword` path remains separate and lacks that client-aware boundary.
- Code/test commit `10111a50f` adds `CapturingClientAwareAuthenticationService` and focused IMAP/POP3/SMTP session tests. The success cases assert username, password, parsed remote IP, and `ClientAuthenticationCaller`; the failure cases inject `ImapAuthenticationResult.Failure(...)` with `Disconnect=true`, assert the protocol failure response, and assert no later authenticated/session operation. The implementation and production registration are unchanged in this test-only follow-up: `ClientAwareAuthenticationService`, `ImapSession`, `Pop3Session`, `SmtpSession`, and `HMailServer.Service.Program` remain the inspected runtime symbols.
- Focused IMAP/POP3/SMTP tests pass `71/71`; full Net10 passes `1209`, with one opt-in native-registry test skipped. No SQL/live database integration, host construction, real TCP listener, COM registration, installed/out-of-process COM invocation, or production service/data change occurred.
- Security review is GREEN with no findings. Reality review is YELLOW for this bounded slice and RED for production release: the tests directly construct sessions and inject the boundary; they do not resolve the service DI graph or prove `ImapTcpListener.HandleClientAsync`, `Pop3TcpListener`, or `SmtpTcpListener` propagation. A no-database host DI smoke test, real loopback listener tests, and isolated SQL threshold/disconnect integration remain open. `ClientAuthenticationRequest.Caller` and `ClientAddress` remain metadata, not independently authenticated trust claims.
- Current next slice: add the no-database service-host DI construction smoke test for the shared authentication service, sessions, listeners, and hosted services. Keep `Account.ValidatePassword` fenced at `E_NOTIMPL` for authorized snapshots and `E_ACCESSDENIED` for detached objects; do not alter COM identity or enable password lookup.

## SEC-12 Shared Protocol DI Registration Smoke (2026-07-26)

- The parity explorer confirmed the legacy ordering in `hmailserver/source/Server/hMailServer/hMailServer.cpp:338-371`: `ServiceMain` registers service state, calls `InitializeApplication()`, and only then resumes COM objects. `AccountLogon::Logon` in `hmailserver/source/Server/Common/Util/AccountLogon.cpp:38-98` remains the client-aware authentication reference. The .NET top-level `HMailServer.Service.Program` currently requires SQL/data-directory configuration before `Host.Build`, then configures runtime hosts before `RunAsync`.
- Code/test commit `0da18ad53` adds `ProtocolServiceCollectionExtensions.AddCallerAwareProtocolServices`. `HMailServer.Service.Program` uses it for the existing caller-aware service, IMAP search registrations, all three protocol sessions, and all three TCP listeners; no COM identity, listener option, authentication, or hosted-service behavior was changed. `ServiceProtocolDependencyInjectionTests` builds a validated provider with test-local auth, recorder, search, mailbox, context, stream, and loopback option doubles, then resolves `ClientAwareAuthenticationService`, `ImapSession`, `Pop3Session`, `SmtpSession`, `ImapTcpListener`, `Pop3TcpListener`, and `SmtpTcpListener` without SQL or data-directory setup.
- Focused caller-aware/protocol tests pass `77/77`; the Windows service build passes with 0 warnings and 0 errors; full Net10 passes `1210` with one opt-in native-registry test skipped. No SQL/live database, data-directory, COM registration, service start, listener socket, or production state was touched.
- Security review is GREEN. Reality review is YELLOW for this bounded seam and RED for production release: the test invokes the shared protocol extension directly, not the real `HMailServer.Service` host; it does not resolve hosted-service descriptors, start listeners, or prove endpoint/caller propagation through sockets. SQL threshold/disconnect integration and the trusted caller-aware COM boundary remain open.
- Current next slice: add actual no-database `HMailServer.Service` host/hosted-service registration coverage while keeping hosted services stopped and `Account.ValidatePassword` fenced at `E_NOTIMPL` for authorized snapshots and `E_ACCESSDENIED` for detached objects.

## SEC-12 Hosted-Service Descriptor Registration (2026-07-26)

- The parity explorer confirmed the legacy ordering in `hmailserver/source/Server/hMailServer/hMailServer.cpp:338-371` and `hmailserver/source/Server/Common/Application/Application.cpp:94-368`: database/configuration initialization and `StartServers()` complete before `_AtlModule.ResumeObjects()`. Current .NET `HMailServer.Service.Program` registers `ComLocalServerHostedService` before `ServerBootstrapper`, delivery/indexing services, optional external fetch, and the IMAP/POP3/SMTP listener hosted services; `Host.Build()` and runtime configuration precede `RunAsync`, but hosted-service startup ordering remains a separate readiness risk.
- Code/test commit `ccaa3f7ed` extracts the unchanged hosted-service registration block into `HMailServerServiceCollectionExtensions.AddProductionHostedServices(IServiceCollection, bool)`, and `HMailServer.Service.Program` calls it at the same location and order. `ProductionHostedServiceRegistrationTests` calls that production method with external fetch enabled and disabled, then inspects ordered `IHostedService` descriptors and implementation types. It does not build a provider, resolve service instances, start services, access SQL/data directories, activate COM, or open sockets.
- Focused registration/caller-aware/protocol tests pass `78/78`; the Windows service build passes with 0 warnings and 0 errors; full Net10 passes `1211`, with one opt-in native-registry test skipped. No production service, database, data directory, COM registration, or listener socket was touched.
- Security review is YELLOW because no new auth or COM identity regression was found, but the unchanged COM/listener readiness ordering remains a production risk relative to legacy. Reality review is GREEN for this descriptor slice and RED for production: actual `Host.Build()`, hosted-service instance resolution, SQL/data-directory isolation, startup readiness, COM readiness, and production-hosted listener I/O remain unproven.
- Current next slice: add real loopback listener caller propagation through IMAP/POP3/SMTP while keeping hosted services stopped and preserving COM identity, startup ordering, service state, SQL, data-directory, and listener behavior outside the test boundary. Keep `Account.ValidatePassword` fenced.

## SEC-12 Host Composition Seam Blocker (2026-07-26, superseded)

- The parity explorer re-confirmed legacy startup in `hmailserver/source/Server/hMailServer/hMailServer.cpp:338-371` and `hmailserver/source/Server/Common/Application/Application.cpp:94-368`: database/configuration initialization and `StartServers()` complete before `_AtlModule.ResumeObjects()`. Current `HMailServer.Service.Program` is a top-level composition at `hmailserver/source/Server.Net10/src/HMailServer.Service/Program.cs:25-952`; it requires SQL/data-directory configuration before `Host.Build()`, owns the complete registration graph, configures runtime globals after the build, and proceeds to `RunAsync()`.
- The hmail_minimal_implementer inspected `HMailServerServiceCollectionExtensions.AddProductionHostedServices` and `ProductionHostedServiceRegistrationTests`. It stopped without edits because a smaller `Host.Build()` test would either duplicate the production graph or create a test-only graph, which would not prove production composition. No code/test commit was made and no tests were run for this attempted slice.
- Independent reality review is RED for production release and confirms a missing production-owned composition seam. Security review finds no new COM identity or authorization change but keeps a medium startup risk: `ComLocalServerHostedService.StartAsync` can resume COM before `ServerBootstrapper.ExecuteAsync` proves SQL Full-Text/index readiness, unlike legacy `ResumeObjects()`. Directly invoking `Program` is also unsafe for tests because registration arguments can mutate COM state and post-build code reads directory/SQL-backed services; TLS certificate loading can persist machine key material when configured.
- Superseded by code/test commit `f1718658a`, which extracted the production-owned seam and added the no-database resolution test. The remaining risks identified here, including COM readiness ordering and absent real listener/SQL evidence, remain open.

## SEC-12 Production Host Composition Resolution (2026-07-26)

- Legacy references remain `hmailserver/source/Server/hMailServer/hMailServer.cpp:338-371` and `hmailserver/source/Server/Common/Application/Application.cpp:94-368`: initialization, database/configuration loading, and `StartServers()` precede `_AtlModule.ResumeObjects()`. The .NET seam intentionally changes no hosted-service order or startup behavior.
- Code/test commit `f1718658a` moves the existing pre-build registration graph into `HMailServer.Service.Host.Build(string[])`, returns the built `IHost` plus the values needed by the unchanged post-build runtime configuration, and makes `Program.cs` call that production method. The existing `AddProductionHostedServices` order and COM class registration list are unchanged.
- `ProductionHostCompositionTests.HostBuild_ResolvesEveryRegisteredHostedServiceWithoutStartingOrUsingDatabase` builds the production graph twice with external fetch enabled/disabled, resolves all `IHostedService` instances, verifies order, asserts `ApplicationStarted` is not cancelled, and asserts the inert temporary data directory was not created. It never calls `StartAsync`/`RunAsync`, opens listener sockets, activates COM, or uses a real database.
- Focused composition coverage passes `1/1`; the Windows service/full test build passes with 0 warnings and 0 errors; full Net10 passes `1212` with one opt-in native-registry test skipped. Security review found no new COM identity or authorization issue; reality review confirms this evidence closes the host-build/instance-resolution gap but keeps production RED because readiness, sockets, SQL threshold/disconnect, and broader parity gates remain open.
- Superseded by code/test commit `ac3798455`, which adds real loopback listener caller propagation coverage. The remaining risks identified here, including COM readiness ordering and absent isolated SQL threshold/disconnect evidence, remain open.

## SEC-12 Real Loopback Caller Propagation (2026-07-26)

- The parity explorer confirmed the legacy path: `AccountLogon::Logon` in `hmailserver/source/Server/Common/Util/AccountLogon.cpp:44-95` receives the TCP remote endpoint from `TCPConnection::GetRemoteEndpointAddress`/`GetIPAddressString`, and IMAP `IMAPCommandLOGIN::ExecuteCommand`/`IMAPCommandAUTHENTICATE::ExecuteCommand`, `POP3Connection::ProtocolPASS_`, and SMTP `SMTPConnection::AuthenticateUsingPLAIN_`/`Authenticate_` pass that client address into the password validation and failed-logon path. Legacy behavior has no caller-kind metadata; the .NET caller kind is new metadata around the same client-aware boundary.
- Code/test commit `ac3798455` adds `ImapTcpListenerTests.RunAsync_PropagatesLoopbackAddressToImapAuthenticationBoundary`, `Pop3TcpListenerTests.RunAsync_PropagatesLoopbackAddressToPop3AuthenticationBoundary`, and `SmtpTcpListenerTests.RunAsync_PropagatesLoopbackAddressToSmtpAuthenticationBoundary`. Each starts the bounded listener, connects a real `TcpClient` over loopback, performs the protocol's authentication exchange, and asserts `IPAddress.Loopback`, the `Imap`/`Pop3`/`Smtp` caller kind, and credentials at `CapturingClientAwareAuthenticationService`.
- Focused listener tests pass `13/13`; full Net10 passes `1215` with one opt-in native-registry test skipped. The prior no-database production host composition proof remains in `f1718658a` and `ProductionHostCompositionTests.HostBuild_ResolvesEveryRegisteredHostedServiceWithoutStartingOrUsingDatabase`. No production code, COM identity/registration, service, SQL/data directory, SMTP trust behavior, live reconfiguration, or production host start changed.
- Security review found no actionable authorization, identity, or test-isolation regression in this bounded test-only slice. Reality review accepts the real socket and endpoint/caller evidence but keeps production RED: injected failure/disconnect coverage at the listener boundary, COM readiness ordering, isolated SQL threshold/disconnect integration, trusted caller-aware COM mutation, broader COM/Admin parity, migration/backup, and performance/soak gates remain open.
- Current next slice: review and, if required, add the smallest test-only COM readiness barrier against legacy `ResumeObjects` ordering. Keep `Account.ValidatePassword` fenced at `E_NOTIMPL` for authorized snapshots and `E_ACCESSDENIED` for detached objects; do not change COM identity or enable password lookup.

## SEC-12 COM Readiness Barrier (2026-07-27)

- The parity explorer confirmed the exact legacy order: `hmailserver/source/Server/hMailServer/hMailServer.cpp:353-371` registers suspended COM objects where required, `InitializeApplication()` at `:433-449` validates configuration/database state and calls `Application::StartServers()`, `Application::StartServers()` at `hmailserver/source/Server/Common/Application/Application.cpp:289-344` waits for scheduler, SMTP delivery, external fetch, and IO startup before `StateRunning`, and `_AtlModule.ResumeObjects()` calls `CoResumeClassObjects` at `hMailServer.cpp:106-121` only afterward. The stable COM contract remains `IInterfaceApplication` and `Application` in `hmailServer.idl:1470-1502` and `:3072-3079`.
- Code/test commit `a6e150134` adds `ServerReadinessSignal`, makes `ServerBootstrapper.ExecuteAsync` publish readiness success/failure/cancellation, registers `ServerBootstrapper` before `ComLocalServerHostedService`, and makes `ComLocalServerHostedService.StartAsync` await readiness before its existing `ComLocalServerHost.Start()`/class resumption path. `ServerReadinessSignalTests` cover success, failure, and caller cancellation; `ProductionHostCompositionTests.ComLocalServerHostedService_WaitsForReadinessBeforeStartingCom` proves the COM service remains pending and does not start when readiness is unavailable; registration/composition tests preserve the production graph.
- Focused readiness/listener coverage passes `19/19` (`6/6` readiness/composition plus `13/13` listener); full Net10 passes `1219` with one opt-in native-registry test skipped. No production service start, real SQL, listener socket, data-directory, registry, COM identity, direct activation boundary, SMTP trust, or SEC-18 state was touched by the tests. No IID/CLSID/ProgID/DISPID/vtable/type-library change was made.
- Reality review is RED for production release: the barrier covers Full-Text and search-index readiness but does not yet prove that all required listeners/workers have reached the legacy `StartServers()` equivalent, and no real successful host-start/COM lifecycle evidence was added. The COM password-validation method remains fenced; broader COM/Admin parity, isolated SQL threshold/disconnect, migration/backup, performance/soak, and SEC-18 gates remain open.
- Current next slice: add the smallest production startup-coordinator evidence that completes the required server-service readiness boundary before COM resumption. Preserve the existing COM identity and direct activation denial boundaries; do not enable `Account.ValidatePassword`.

## SEC-12 Listener Startup Coordinator (2026-07-27)

- The parity explorer confirmed the legacy completion boundary in `hmailserver/source/Server/hMailServer/hMailServer.cpp:353-371` and `:106-121`, plus `hmailserver/source/Server/Common/Application/Application.cpp:289-344`: `InitializeApplication()` calls `StartServers()`, waits for required service startup events, then `ResumeObjects()` calls `CoResumeClassObjects`. The installed `IInterfaceApplication` contract in `hmailserver/source/Server/hMailServer/hMailServer.idl:1470-1502` and `:3072-3079` was not changed.
- Code/test commit `64424782b` adds `ServerStartupCoordinator` and splits `ServerReadinessSignal` into bootstrap and final readiness. `HMailServerServiceCollectionExtensions.AddProductionHostedServices` registers the coordinator after the IMAP/POP3/SMTP hosted services and before `ComLocalServerHostedService`; it waits for each enabled listener's `Started` task. Listener startup failures and cancellation fail the shared readiness signal closed, while disabled listeners are omitted from the wait set.
- `ServerStartupCoordinatorTests` cover bootstrap gating, disabled-listener readiness, listener bind failure, and cancellation. `ProductionHostCompositionTests` and `ProductionHostedServiceRegistrationTests` preserve the production registration order. Focused readiness/listener/composition coverage passes `24/24`; full Net10 passes `1224` with one opt-in native-registry test skipped. No SQL/data directory, service, COM registration, registry, DCOM, SMTP trust, protocol command behavior, or SEC-18 state was changed.
- Security review is GREEN for the bounded startup-failure propagation and identity-preservation scope. Reality review remains RED for production release: delivery/external-fetch worker readiness is not represented, no successful real host/COM lifecycle was run, and isolated SQL threshold/disconnect, trusted caller-aware COM mutation, broader COM/Admin parity, migration/backup, performance/soak, and SEC-18 gates remain open.
- Current next slice: add isolated SQL threshold/disconnect integration for the existing client-aware authentication recorder while keeping `Account.ValidatePassword` fenced and preserving COM identity/direct-activation boundaries.

## SEC-12 SQL Threshold/Disconnect Integration (2026-07-27)

- The parity explorer/local legacy audit confirmed `AccountLogon::Logon` in `hmailserver/source/Server/Common/Util/AccountLogon.cpp:42-92`: failed attempts increment the per-IP failure count, the `>= MaxInvalidLogonAttempts` threshold clears failures, creates an `Auto-ban: username` security range when `AutoBanMinutes` is nonzero, and returns disconnect when the duration is zero. `PersistentLogonFailure.cpp:27-91` is the persistence reference for count, insert, and per-IP clear; `RemoveExpiredRecords.cpp:33-40` separately performs expiry cleanup.
- Code/test commit `dba32694a` adds cancellation propagation coverage and `SqlServerAutoBanLogonFailureRecorderIntegrationTests`. The fixture is local-data-source-only, requires `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1`, creates a GUID-named disposable database with only the needed tables/settings, exercises threshold counts, failure clearing, priority-100 expiring range creation, the no-range disconnect branch, and the real `ClientAwareAuthenticationService` result, then drops the database. This host ran no SQL integration because the approved connection variable was unset.
- Focused auth/SQL coverage passes `9` with one explicit inconclusive; full Net10 passes `1225` with two opt-in tests skipped. No production code, COM identity, direct activation boundary, SMTP trust behavior, service state, production database, or data directory was changed.
- Security review found no SQL injection, COM identity, authorization, or test-isolation regression in this test-only slice. Reality review remains RED for production because the opt-in SQL path lacks captured disposable-instance execution evidence, the production recorder has a concurrent per-IP threshold race, and non-cancellation recorder failures are swallowed by the client-aware service. Broader COM/Admin parity, migration/backup, performance/soak, worker readiness, real host/COM lifecycle, and SEC-18 evidence gates remain open.
- Current next slice: make failed-logon threshold enforcement atomic per IP without enabling `Account.ValidatePassword` or changing COM registration.

## SEC-12 Atomic SQL Threshold Enforcement (2026-07-27)

- The legacy audit remains anchored to `AccountLogon::Logon` in `hmailserver/source/Server/Common/Util/AccountLogon.cpp:42-92`, `PersistentLogonFailure.cpp:27-91`, and `CreateTablesMSSQL.sql:662-671`. The legacy flow counts the current IP, clears that IP at `>= MaxInvalidLogonAttempts`, creates the priority-100 expiring deny range when `AutoBanMinutes` is nonzero, or disconnects without a range when it is zero. The installed SQL schema has `idx_hm_logon_failures_ipaddress` on `(ipaddress1, ipaddress2)`.
- Production/test commit `d2fe76e14` adds SQL Server `UPDLOCK, HOLDLOCK` to `SqlServerAutoBanLogonFailureRecorder.CountFailuresSql` inside the existing transaction. The opt-in `RecordFailureAsync_SerializesConcurrentFailuresPerIp` fixture launches three same-IP failures and asserts counts `1`, `2`, `3`, exactly one disconnect/range transition, cleared failure rows, and one priority-100 expiring range. No COM identity, direct activation boundary, SMTP trust behavior, production database, or data directory changed.
- The fixture is guarded by a local SQL/LocalDB data source and `HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE=1`; this host ran no SQL integration because the approved connection variable was unset. Focused auth/SQL coverage passes `9` with two explicit inconclusives; full Net10 passes `1225` with three opt-in tests skipped.
- Security review of the bounded change found no SQL injection, COM identity, authorization, or test-isolation regression. The concurrent SQL result still needs execution evidence on an approved disposable instance; non-cancellation recorder failures remain swallowed by the client-aware service. Worker readiness, real host/COM lifecycle, broader COM/Admin parity, migration/backup, performance/soak, and SEC-18 gates remain open.
- Current next slice: audit authenticated `Settings -> SecurityRanges` ownership and legacy `InterfaceSecurityRanges::Add`/`InterfaceSecurityRange::Save` insert parity. Keep `SetDefault`, deletion, live policy reconfiguration, SMTP trust, and `Account.ValidatePassword` out of scope.

## SEC-11 SecurityRanges Add/Save Insert Parity (2026-07-27)

- Code/test commit `5c7c1010e` implements authenticated `Settings -> SecurityRanges.Add()` and new-item `SecurityRange.Save()` insert parity. `Settings.SecurityRanges` preserves the authenticated server-administrator boundary and passes the authorization callback into the owning adapter; direct child activation remains access-denied and installed SecurityRanges/SecurityRange identity/vtable/DISPID contracts remain unchanged.
- Legacy references: `hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:12-21` (`LoadSettings`), `:158-185` (`Add`); `hmailserver/source/Server/COM/InterfaceSecurityRange.cpp:36-61` (`Save`), `:97-146` (IP setters); `hmailserver/source/Server/Common/BO/SecurityRange.cpp:17-53` (constructor and IP parsing); and `hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:60-117,268-289` (insert persistence, validation, truncation, and expiry fallback). Invalid IP setter input retains the previous parsed address while returning through the COM setter path; Save inserts one parameterized `hm_securityranges` row, assigns `OUTPUT INSERTED.rangeid`, and appends only that new snapshot to the owning collection.
- Focused SecurityRanges/store coverage passes `10/10`; full Net10 passes `1228` with `3` opt-in tests skipped. No live SQL integration ran because the approved isolated connection was unset. No production service, database, data directory, COM registration/ACL, SMTP trust behavior, or SEC-18 staging state changed.
- Residual risks remain: existing-row SecurityRange setters/Save, collection/item deletion, `SetDefault`, live IP policy/auto-ban reconfiguration, and SQL integration evidence are still open or intentionally fenced. The next smallest safe slice is authenticated `SecurityRanges.DeleteByDBID` membership containment; keep item/index deletion, `SetDefault`, live reconfiguration, SMTP trust, and `Account.ValidatePassword` out of scope.

## SEC-11 SecurityRanges DeleteByDBID Parity (2026-07-27)

- Code/test commit `864d2e6d6` implements authenticated `Settings -> SecurityRanges.DeleteByDBID` membership-scoped deletion through the configured administration store. The authorized collection calls the delete delegate only when the target ID is present in its owning snapshot, removes only that item after successful store completion, silently no-ops for unknown/foreign/repeated IDs, and maps contained store exceptions to COM `E_FAIL` while retaining the prior snapshot. Direct activation remains `E_ACCESSDENIED`, and the installed SecurityRanges/SecurityRange IID, vtable, DISPID, CLSID, ProgID, and default-interface boundaries are unchanged.
- Legacy references: `hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:59-74` (`DeleteByDBID`), `hmailserver/source/Server/Common/BO/Collection.h:181-200` (`DeleteItemByDBID`), and `hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:33-49` (`DeleteObject`). The legacy SQL shape is `delete from hm_securityranges where rangeid = @RANGEID`; the collection ignores the persistence boolean after a matching member is found, while the COM wrapper returns success for unknown IDs and converts thrown exceptions to its generic COM error.
- Focused SecurityRanges/store coverage passes `13/13`; full Net10 passes `1231` with `3` opt-in tests skipped. No live SQL integration ran because the approved isolated connection was unset. No production service, database, data directory, COM registration/ACL, SMTP trust behavior, or SEC-18 staging state changed.
- Residual risks remain: owning-collection `SecurityRange.Delete()`, collection index `Delete`, existing-row setters/Save, `SetDefault`, live IP policy/auto-ban reconfiguration, and SQL integration evidence. The next smallest safe slice is `SecurityRange.Delete()` ownership parity; keep index deletion, `SetDefault`, live reconfiguration, SMTP trust, and `Account.ValidatePassword` out of scope.

## SEC-11 SecurityRange Item Delete Parity (2026-07-27)

- Code/test commit `641599b5c` implements authenticated owning-collection `SecurityRange.Delete()` parity. Existing and unsaved item facades receive the parent collection delete delegate from index, DBID, name, and Add paths; item Delete rechecks server-admin authorization and reuses `SecurityRanges.DeleteByDBID`, so membership containment, stale/repeated/ID-zero no-op behavior, snapshot removal after success, and `E_FAIL` failure retention remain centralized.
- Legacy references: `hmailserver/source/Server/COM/InterfaceSecurityRange.cpp:759-780` (`Delete`), `hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:92-185` (existing item lookup and `Add` parent attachment), `hmailserver/source/Server/COM/COMCollection.h:11-38` (`AttachParent`), `hmailserver/source/Server/Common/BO/Collection.h:181-200` (`DeleteItemByDBID`), and `hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:33-49` (`DeleteObject`). Direct activation remains `E_ACCESSDENIED`; installed SecurityRanges/SecurityRange identities and COM metadata remain unchanged.
- Focused SecurityRanges/store coverage passes `18/18`; full Net10 passes `1236` with `3` opt-in tests skipped. No live SQL integration ran because the approved isolated connection was unset. No production service, database, data directory, COM registration/ACL, SMTP trust behavior, or SEC-18 staging state changed.
- Residual risks remain: authenticated collection index `Delete`, existing-row SecurityRange setters/Save, `SetDefault`, live IP policy/auto-ban reconfiguration, and SQL integration evidence. The next smallest safe slice is `Settings -> SecurityRanges.Delete(index)` parity; keep `SetDefault`, live reconfiguration, SMTP trust, and `Account.ValidatePassword` out of scope.

## SEC-11 SecurityRange Index Delete Parity (2026-07-27)

- Code/test commit `77e16bd4d` implements authenticated `Settings -> SecurityRanges.Delete(index)` parity without changing installed `IInterfaceSecurityRanges`/`IInterfaceSecurityRange` identity, vtable, DISPID, CLSID, ProgID, type-library, or direct-activation boundaries. The collection selects one zero-based owning snapshot entry, invokes the configured administration store, removes exactly that entry only after success, maps contained store exceptions to COM `E_FAIL`, and silently no-ops for negative, out-of-range, and empty snapshots.
- Legacy references: `hmailserver/source/Server/COM/InterfaceSettings.cpp:440-463` (`get_SecurityRanges` authentication), `hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:42-57` (`Delete`), `hmailserver/source/Server/Common/BO/Collection.h:217-230` (`DeleteItem`), `hmailserver/source/Server/Common/BO/SecurityRanges.cpp:25-37` (expiry/priority/name ordering), `hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:33-49` (`DeleteObject`), `hmailserver/source/Server/hMailServer/hMailServer.idl:528-540` (`Settings.SecurityRanges`), and `:1309-1320` (`IInterfaceSecurityRanges.Delete`/`DeleteByDBID`). Legacy invalid indexes no-op after `LONG` to unsigned conversion; valid deletion ignores the persistence Boolean result and returns `S_OK`.
- Focused SecurityRanges/store coverage passes `20/20`; full Net10 passes `1238` with `3` opt-in tests skipped. No live SQL integration ran because the approved isolated connection was unset. No production service, database, data directory, COM registration/ACL, SMTP trust behavior, or SEC-18 staging state changed.
- Residual risks remain: existing-row SecurityRange setters/Save, `SetDefault`, live IP policy/auto-ban reconfiguration, and SQL integration evidence. Next slice: audit and implement authenticated existing-row `SecurityRange.Save()` update parity; keep `SetDefault`, live reconfiguration, SMTP trust, and `Account.ValidatePassword` out of scope.

## SEC-11 SecurityRange Existing Save Parity (2026-07-27)

- Code/test commit `02e445a5c` implements authenticated existing-row `SecurityRange.Save()` update parity. Items returned by authorized index, DBID, and name lookups receive the owning save delegate; setters stage the full persisted snapshot, Save rechecks server-admin authorization, validates empty/duplicate names and mixed or reversed IP ranges, updates all mutable `hm_securityranges` columns through a parameterized update, preserves the ID and current collection index, and replaces only that owner snapshot after store success. Store exceptions map to COM `E_FAIL` and retain the previous owner snapshot; legacy validation failures use `0x800403E9`.
- Legacy references: `hmailserver/source/Server/COM/InterfaceSettings.cpp:440-463`, `InterfaceSecurityRanges.cpp:12-21,92-185`, `InterfaceSecurityRange.cpp:36-61`, `hmailserver/source/Server/COM/COMCollection.h:6-38`, `hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:60-117,268-289`, `PreSaveLimitationsCheck.cpp:407-423`, and `SecurityRanges.cpp:25-38`. Installed SecurityRanges/SecurityRange IID, vtable, DISPID, CLSID, ProgID, type-library, direct activation, SMTP trust, and live policy boundaries remain unchanged.
- Focused SecurityRanges/store coverage passes `27/27`; full Net10 passes `1245` with `3` opt-in tests skipped. No live SQL integration ran because the approved isolated connection was unset. No production service, database, data directory, COM registration/ACL, SMTP trust behavior, or SEC-18 staging state changed.
- Residual risks remain: exact live SQL row-count/error-info evidence, `SetDefault`, live IP policy/auto-ban reconfiguration, and the documented snapshot-model difference where staged changes on one retained item facade are not visible through a second lookup until Save succeeds. Next slice: audit and implement authenticated `Settings -> SecurityRanges.SetDefault()` parity; keep live reconfiguration, SMTP trust, and `Account.ValidatePassword` out of scope.

## SEC-11 SecurityRange SetDefault Parity (2026-07-27)

- Code/test commit `56a668256` authenticated `Settings -> SecurityRanges.SetDefault()` parity'sini ekledi. Legacy `InterfaceSecurityRanges::SetDefault` (`hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:219-237`) authenticated collection elde edildikten sonra ikinci admin recheck yapmiyor; `HM::SecurityRanges::SetDefault` (`hmailserver/source/Server/Common/BO/SecurityRanges.cpp:25-70`) `Refresh -> DeleteAll -> My computer/Internet insert -> Refresh` siralamasini kullaniyor. .NET path exact default rows'i, mevcut parameterized delete/insert delegates'i, final ordered snapshot refresh'ini ve direct activation `E_ACCESSDENIED` sinirini koruyor; contained store failure `E_FAIL` ile donuyor ve son published snapshot okunabilir kaliyor.
- Legacy refs: `Collection.h:203-215` (`DeleteAll`), `PersistentSecurityRange.cpp:33-49,60-117` (delete/insert persistence), `IPAddressSQLHelper.cpp:74-84` (IPv4 split columns), `hMailServer.idl:528-540,1309-1320` (Settings/SetDefault contract). Exact defaults: `My computer` loopback, priority `30`, options `71627`; `Internet` full IPv4, priority `10`, options `96203`.
- Focused SecurityRanges/store `29/29`, full Net10 `1247` pass, `3` opt-in skip. No live SQL integration; production service/database/data directory, COM registration/ACL, SMTP/live policy, and SEC-18 staging state untouched. Residual: legacy boolean-failure continuation/generic `0x800403E9` error behavior is not reproduced for contained .NET store exceptions; IP policy/auto-ban reconfiguration remains fenced.

## SEC-14 WebAdmin Performance Mutation Hardening (2026-07-27)

- Code/test commit `7c7ca1049` hardens `hmailserver/source/WebAdmin/hm_performance.php`: `action` and all performance save fields now come from the POST body, and both `save` and `ClearMessageIndexingCache` require `hmailRequirePostCsrfToken()` before mutation. Normal `GET page=performance` rendering remains unchanged; the existing server-admin gate and COM `MessageIndexing->Clear()` behavior remain unchanged.
- Legacy references: `hmailserver/source/Server/COM/InterfaceMessageIndexing.cpp:102-114` (`InterfaceMessageIndexing::Clear`) and `hmailserver/source/Server/hMailServer/hMailServer.h:25684-25813` (`IInterfaceMessageIndexing`). WebAdmin references: `hmailserver/source/WebAdmin/index.php:47-50`, `hmailserver/source/WebAdmin/include/functions.php:411-439`, and `hmailserver/source/WebAdmin/hm_performance.php:11-34`.
- Focused `WebAdminPerformancePostOnlySourceTests` passes `1/1`; full Net10 passes `1248` with `3` opt-in skips. The WebAdmin manual checklist covers GET clear, query-only action/field/token, valid POST, and server-admin behavior. PHP CLI lint was unavailable because PHP is not installed on this host.
- No COM identity, direct-activation boundary, authenticated Settings boundary, SMTP/indexing runtime, production service, database, data directory, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations.

## SEC-14 WebAdmin Backup Mutation Hardening (2026-07-27)

- Code/test commit `7338030e6` hardens `hmailserver/source/WebAdmin/hm_backup.php`: `action` and all backup settings fields now come from the POST body, and both `save` and `startbackup` require `hmailRequirePostCsrfToken()` before mutation. Normal `GET page=backup` rendering remains unchanged; the existing server-admin gate and asynchronous `BackupManager->StartBackup()` behavior remain unchanged.
- Legacy references: `hmailserver/source/Server/COM/InterfaceBackupManager.cpp:27-40`, `hmailserver/source/Server/Common/Application/BackupManager.cpp:38-71`, `hmailserver/source/Server/Common/Application/BackupTask.cpp:27-40`, and `hmailserver/source/Server/Common/Application/BackupExecuter.cpp:47-78`. WebAdmin references: `hmailserver/source/WebAdmin/index.php:47-50`, `hmailserver/source/WebAdmin/include/functions.php:411-439`, and `hmailserver/source/WebAdmin/hm_backup.php:11-25`.
- Focused `WebAdminBackupPostOnlySourceTests` passes `1/1`; combined focused WebAdmin source coverage is `2/2`; full Net10 passes `1249` with `3` opt-in skips. The manual checklist covers GET start, query-only action/field/token, valid POST, and server-admin/asynchronous behavior. PHP CLI lint was unavailable because PHP is not installed on this host.
- No COM identity, direct-activation boundary, authenticated Settings boundary, asynchronous backup runtime, archive/XML path, production service, database, data directory, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations.

## SEC-14 WebAdmin Greylisting Mutation Hardening (2026-07-27)

- Code/test commit `28a830f5f` hardens `hmailserver/source/WebAdmin/hm_greylisting.php`: `action` and all greylisting fields now come from the POST body, and `save` requires `hmailRequirePostCsrfToken()` before mutation. Normal `GET page=greylisting` rendering remains unchanged; the existing server-admin gate, anti-spam settings, and legacy day-to-hour conversion remain unchanged.
- Legacy references: `hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:265-385,1121-1184` and `hmailserver/source/Server/hMailServer/hMailServer.h:19556-19744`. WebAdmin references: `hmailserver/source/WebAdmin/index.php:47-50`, `hmailserver/source/WebAdmin/include/functions.php:411-439`, and `hmailserver/source/WebAdmin/hm_greylisting.php:8-24`.
- Focused `WebAdminGreyListingPostOnlySourceTests` passes `1/1`; combined focused WebAdmin source coverage is `3/3`; full Net10 passes `1251` with `3` opt-in skips. The manual checklist covers GET save, query-only action/field/token, valid POST, and server-admin/anti-spam behavior. PHP CLI lint was unavailable because PHP is not installed on this host.
- No COM identity, direct-activation boundary, authenticated Settings boundary, anti-spam/SMTP runtime, production service, database, data directory, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations.

## SEC-14 WebAdmin Logging Mutation Hardening (2026-07-27)

- The parity audit confirmed the legacy authenticated path in `hmailserver/source/Server/COM/InterfaceSettings.cpp:360-383`: `InterfaceSettings::get_Logging` denies non-server-admin callers, creates `InterfaceLogging`, attaches the current authentication object, and loads settings. `InterfaceLogging::put_Enabled`, `put_LogSMTP`, `put_LogPOP3`, `put_LogIMAP`, `put_AWStatsEnabled`, `put_LogTCPIP`, `put_LogApplication`, `put_LogDebug`, and `put_KeepFilesOpen` at `hmailserver/source/Server/COM/InterfaceLogging.cpp:29-47,64-85,103-124,142-165,183-201,219-240,258-281,459-486,578-601` persist the corresponding configuration values. The installed Settings/IInterfaceLogging contract is `hmailserver/source/Server/hMailServer/hMailServer.idl:539,1104-1148`.
- Code/test commit `363a9cfb8` hardens `hmailserver/source/WebAdmin/hm_logging.php`: `action` and all nine logging fields use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM property writes. The authenticated Settings boundary, direct-activation denial, COM identity, and logging behavior remain unchanged. Focused `WebAdminLoggingPostOnlySourceTests.LoggingSettingsSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `4/4`; full Net10 passes `1252` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases were added for GET save, query-only action/field/token, and valid POST behavior.
- Next bounded slice: audit `hmailserver/source/WebAdmin/hm_scripts.php` `save`, `checksyntax`, and `reloadscripts` actions for POST-only handling and CSRF-token placement. Keep script execution semantics, COM identity, direct activation boundaries, authenticated Settings access, SMTP/anti-spam behavior, diagnostics/network egress, backup archive/XML execution, and SEC-18 broker registration out of scope until that slice is selected.

## SEC-14 WebAdmin Scripts Mutation Hardening (2026-07-27)

- The parity audit confirmed the legacy authenticated path in `hmailserver/source/Server/COM/InterfaceSettings.cpp:1060-1081`: `InterfaceSettings::get_Scripting` denies non-server-admin callers, creates `InterfaceScripting`, attaches authentication, and loads settings. `InterfaceScripting::put_Enabled`/ `put_Language` persist configuration through `Configuration`, while `Reload` calls `ScriptServer::LoadScripts` and `CheckSyntax` returns `ScriptServer::CheckSyntax`; these methods retain their existing authenticated COM boundary. The installed contract is `hmailserver/source/Server/hMailServer/hMailServer.idl:575,1697-1710`.
- Code/test commit `8894239af` hardens `hmailserver/source/WebAdmin/hm_scripts.php`: `action`, `scriptingenabled`, and `scriptinglanguage` use `hmailGetPostVar()`, and `save`, `checksyntax`, and `reloadscripts` call `hmailRequirePostCsrfToken()` before COM-backed work. Existing `VBScript`/`JScript` validation, script execution behavior, authenticated Settings access, direct activation denial, and COM identity remain unchanged. Focused `WebAdminScriptsPostOnlySourceTests.ScriptActionsUsePostBodyAndRequirePostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `5/5`; full Net10 passes `1253` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases were added for GET actions, query-only action/field/token, and valid POST behavior.
- Next bounded slice: audit `hmailserver/source/WebAdmin/hm_diagnostics.php` `performTests` for POST-only handling, CSRF-token placement, and diagnostics/network-egress safety. Keep the existing diagnostic test set, COM identity, direct activation boundaries, authenticated Settings access, SMTP behavior, backup archive/XML execution, and SEC-18 broker registration out of scope until that slice is selected.

## SEC-14 WebAdmin Diagnostics Mutation Hardening (2026-07-27)

- The parity audit confirmed the legacy authenticated path in `hmailserver/source/Server/COM/InterfaceApplication.cpp:481-501`: `InterfaceApplication::get_Diagnostics` denies non-server-admin callers, creates `InterfaceDiagnostics`, and attaches the current authentication object. `InterfaceDiagnostics::PerformTests` and `put_LocalDomainName` retain the server-admin check in `hmailserver/source/Server/COM/InterfaceDiagnostics.cpp:10-35,43-72`; `HM::Diagnostic::PerformTests` preserves the existing diagnostic test composition in `hmailserver/source/Server/Common/Diagnostics/Diagnostic.cpp:59-98`. The installed `IInterfaceApplication`/`IInterfaceDiagnostics` contract remains `hmailserver/source/Server/hMailServer/hMailServer.idl:1478-1490,2804-2812`.
- Code/test commit `5e49e73e1` hardens `hmailserver/source/WebAdmin/hm_diagnostics.php`: `action` and `LocalDomainName` use `hmailGetPostVar()`, and the `performTests` branch calls `hmailRequirePostCsrfToken()` before COM-backed diagnostics. Normal GET rendering, the existing diagnostic test set, diagnostics/network behavior, authenticated Settings access, direct activation denial, and COM identity remain unchanged. Focused `WebAdminDiagnosticsPostOnlySourceTests.DiagnosticsPerformTestsUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `6/6`; full Net10 passes `1254` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases cover GET action, query-only action/input/token, and valid POST behavior.
- No service Start/Stop, live IP policy reconfiguration, SMTP trust, production service, database, data directory, COM registration/ACL, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations. Next bounded slice: audit `hmailserver/source/WebAdmin/hm_status.php` `control` for POST-only handling while preserving the legacy Start/Stop boundary; keep service mutation execution out of source-only tests.

## SEC-14 WebAdmin Status Control Mutation Hardening (2026-07-27)

- The parity audit confirmed the legacy Application control path in `hmailserver/source/Server/COM/InterfaceApplication.cpp:54-89`: `InterfaceApplication::Start` and `Stop` require server-admin authentication and call the existing `HM::Application::Instance()->StartServers()`/`StopServers()` boundary. The installed `IInterfaceApplication` contract keeps `Start` DISPID 1, `Stop` DISPID 2, and `ServerState` DISPID 5 at `hmailserver/source/Server/hMailServer/hMailServer.idl:1478-1485`.
- Code/test commit `ba2261292` hardens `hmailserver/source/WebAdmin/hm_status.php`: `action` and `controlaction` use `hmailGetPostVar()`, and the `control` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed Start/Stop calls. Normal GET rendering, status reads, control-button mapping, service-state behavior, authenticated Settings access, direct activation denial, and COM identity remain unchanged. Focused `WebAdminStatusPostOnlySourceTests.StatusControlUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `7/7`; full Net10 passes `1255` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases cover GET control, query-only action/input/token, and valid POST behavior.
- No service Start/Stop execution, live TLS/IP policy reconfiguration, SMTP trust, production service, database, data directory, COM registration/ACL, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations. Next bounded slice: audit `hmailserver/source/WebAdmin/hm_ssltls.php` `save` for POST-only handling while preserving the legacy Settings TLS field mapping; keep TLS runtime mutation out of source-only tests.

## SEC-14 WebAdmin SSL/TLS Settings Mutation Hardening (2026-07-27)

- The parity audit confirmed the authenticated Settings path in `hmailserver/source/Server/COM/InterfaceApplication.cpp:110-120`: `InterfaceApplication::get_Settings` denies non-server-admin callers before returning the Settings facade. The existing `InterfaceSettings` setters preserve the TLS field mapping and configuration semantics in `hmailserver/source/Server/COM/InterfaceSettings.cpp:2244-2289,2292-2424,2426-2488`; the installed Settings DISPIDs are 93, 94, 96-98, 103, and 105-106 in `hmailserver/source/Server/hMailServer/hMailServer.idl:656-667,681-691`.
- Code/test commit `1bd30eea` hardens `hmailserver/source/WebAdmin/hm_ssltls.php`: `action` and all TLS save fields use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed Settings property writes. Normal GET rendering, TLS field mapping, conditional ChaCha behavior, authenticated Settings access, direct activation denial, and COM identity remain unchanged. Focused `WebAdminSslTlsPostOnlySourceTests.SslTlsSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `8/8`; full Net10 passes `1256` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases cover GET save, query-only action/field/token, and valid POST behavior.
- No TLS runtime reconfiguration, service Start/Stop, live IP policy reconfiguration, SMTP trust, production service, database, data directory, COM registration/ACL, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations. SMTP `save` is complete in `122847319`; next bounded slice: audit `hmailserver/source/WebAdmin/hm_smtp_antispam.php` `save` for POST-only handling while preserving the legacy AntiSpam field mapping; keep SpamAssassin test behavior and broader AntiSpam mutations out of scope.

## SEC-14 WebAdmin POP3 Settings Mutation Hardening (2026-07-27)

- The parity audit confirmed `InterfaceApplication::get_Settings` in `hmailserver/source/Server/COM/InterfaceApplication.cpp:110-120` denies non-server-admin callers before returning Settings. `InterfaceSettings::put_MaxPOP3Connections` and `put_WelcomePOP3` preserve the two field writes in `hmailserver/source/Server/COM/InterfaceSettings.cpp:190-220,730-760`; installed DISPIDs are 6 and 24 at `hmailserver/source/Server/hMailServer/hMailServer.idl:528-532,549-550`.
- Code/test commit `c28d23d79` hardens `hmailserver/source/WebAdmin/hm_pop3.php`: `action`, `maxpop3connections`, and `welcomepop3` use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed Settings writes. Normal GET rendering, POP3 field mapping, authenticated Settings access, direct activation denial, and COM identity remain unchanged. Focused `WebAdminPop3PostOnlySourceTests.Pop3SaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `9/9`; full Net10 passes `1257` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases cover GET save, query-only action/field/token, and valid POST behavior.
- No POP3 runtime reconfiguration, service Start/Stop, live TLS/IP policy reconfiguration, SMTP trust, production service, database, data directory, COM registration/ACL, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations. SMTP `save` is complete in `122847319`; next bounded slice: audit `hmailserver/source/WebAdmin/hm_smtp_antispam.php` `save` for POST-only handling while preserving the legacy AntiSpam field mapping; keep SpamAssassin test behavior and broader AntiSpam mutations out of scope.

## SEC-14 WebAdmin IMAP Settings Mutation Hardening (2026-07-27)

- The parity audit confirmed `InterfaceApplication::get_Settings` in `hmailserver/source/Server/COM/InterfaceApplication.cpp:110-120` denies non-server-admin callers before returning Settings. `InterfaceSettings::put_MaxIMAPConnections` and `put_WelcomeIMAP` preserve their configuration writes at `hmailserver/source/Server/COM/InterfaceSettings.cpp:156-164,764-779`; the IMAP extension setters are at `:1384-1494`, the delimiter setter and its legacy folder-conflict error are at `:2171-2187`, and the master-user/SASL setters are at `:2508-2570`. Installed Settings DISPIDs remain 25, 53-56, 75, 87, and 100-102 at `hmailserver/source/Server/hMailServer/hMailServer.idl:551-552,589-597,625-626,645-646,672-679`.
- Code/test commit `5e694f49c` hardens `hmailserver/source/WebAdmin/hm_imap.php`: `action` and all ten existing IMAP fields use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed Settings writes. Normal GET rendering, IMAP field mapping, hierarchy-delimiter behavior, authenticated Settings access, direct activation denial, and COM identity remain unchanged. Focused `WebAdminImapPostOnlySourceTests.ImapSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `10/10`; full Net10 passes `1258` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases cover GET save, query-only action/field/token, valid POST behavior, and hierarchy-delimiter preservation.
- No IMAP runtime reconfiguration, service Start/Stop, live TLS/IP policy reconfiguration, SMTP trust, production service, database, data directory, COM registration/ACL, or SEC-18 staging state changed. Residual SEC-14 risk remains in other less-visible WebAdmin mutations. SMTP `save` is complete in `122847319`; next bounded slice: audit `hmailserver/source/WebAdmin/hm_smtp_antispam.php` `save` for POST-only handling while preserving the legacy AntiSpam field mapping; keep SpamAssassin test behavior and broader AntiSpam mutations out of scope.

## SEC-14 WebAdmin SMTP Settings Mutation Hardening (2026-07-28)

- The parity audit confirmed `InterfaceApplication::get_Settings` in `hmailserver/source/Server/COM/InterfaceApplication.cpp:110-123` denies non-server-admin callers before returning Settings. Existing POST/CSRF enforcement remains `hmailRequirePost()` and `hmailRequirePostCsrfToken()` in `hmailserver/source/WebAdmin/include/functions.php:411-439`.
- Legacy setter references are `hmailserver/source/Server/COM/InterfaceSettings.cpp:92-105,124-137,263-281,304-319,343-358,482-498,519-535,592-607,627-642,662-677,696-710,913-928,948-962,998-1008,1256-1268,1352-1364,1644-1659,1678-1693,1712-1724+,1766-1780,1799-1813,1849-1862,2206-2218`; installed Settings DISPIDs remain `hmailserver/source/Server/hMailServer/hMailServer.idl:528-571,576-587,604-622,650-655`.
- Code/test commit `122847319` hardens `hmailserver/source/WebAdmin/hm_smtp.php`: `action` and all existing SMTP save fields use `hmailGetPostVar()`, and the `save` branch requires `hmailRequirePostCsrfToken()` before COM-backed Settings mutation. `WebAdminSmtpPostOnlySourceTests.SmtpSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `11/11`; full Net10 passes `1259` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. The manual checklist covers GET save, query-only action/field/token, valid POST, `AllowMailFromNull` inversion, `SMTPConnectionSecurity` mapping, and non-empty relayer-password updates.
- No SMTP runtime behavior, live reconfiguration, SMTP trust, COM identity/direct-activation boundary, authenticated Settings boundary, service/database/data-directory state, or SEC-18 staging state changed. SMTP AntiVirus `save` is complete in `68d6f0006`; next bounded slice: audit `hmailserver/source/WebAdmin/hm_smtp_antispam.php` `save` for POST-only handling while preserving the legacy AntiSpam field mapping; keep SpamAssassin test behavior and broader AntiSpam mutations out of scope.

## SEC-14 WebAdmin SMTP AntiVirus Settings Mutation Hardening (2026-07-28)

- The parity audit confirmed the authenticated path: `InterfaceApplication::get_Settings` in `hmailserver/source/Server/COM/InterfaceApplication.cpp:110-123` denies non-server-admin callers before returning Settings, and `InterfaceSettings::get_AntiVirus` in `hmailserver/source/Server/COM/InterfaceSettings.cpp:387-410` repeats the server-administrator boundary before creating the child facade. Existing POST/CSRF enforcement remains `hmailRequirePost()` and `hmailRequirePostCsrfToken()` in `hmailserver/source/WebAdmin/include/functions.php:411-439`.
- Legacy AntiVirus setter behavior is preserved by `hmailserver/source/Server/COM/InterfaceAntiVirus.cpp`: ClamWin setters are `put_ClamWinEnabled`, `put_ClamWinExecutable`, and `put_ClamWinDBFolder` at `:37-111`; custom-scanner setters are `put_CustomScannerEnabled`, `put_CustomScannerExecutable`, and `put_CustomScannerReturnValue` at `:138-213`; action/notification/max-size setters are `put_Action`, `put_NotifySender`, `put_NotifyReceiver`, and `put_MaximumMessageSize` at `:248-369`; attachment blocking is `put_EnableAttachmentBlocking` at `:400-407`; and ClamAV setters are `put_ClamAVEnabled`, `put_ClamAVHost`, and `put_ClamAVPort` at `:451-526`. The installed contract keeps `Settings.AntiVirus` at DISPID 30 and `IInterfaceAntiVirus` property DISPIDs 1-15 in `hmailserver/source/Server/hMailServer/hMailServer.idl:561,1329-1364`.
- Code/test commit `68d6f0006` hardens `hmailserver/source/WebAdmin/hm_smtp_antivirus.php`: `action` and all 14 existing AntiVirus save fields use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed AntiVirus writes. `WebAdminSmtpAntivirusPostOnlySourceTests.SmtpAntivirusSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `12/12`; full Net10 passes `1260` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. Manual checklist cases cover GET save, query-only action/field/token, valid POST behavior, scanner-test preservation, and blocked-attachment deletion preservation.
- Normal GET rendering, AntiVirus field defaults, scanner AJAX behavior, blocked-attachment delete handling, the server-administrator Settings boundary, direct activation denial, installed COM identity/vtable/DISPID shape, SMTP/anti-spam/auto-ban behavior, live reconfiguration, service/database/data-directory state, and SEC-18 staging state remain unchanged. Next bounded slice: audit `hmailserver/source/WebAdmin/hm_smtp_antispam.php` `save` for POST-only handling while preserving the legacy AntiSpam field mapping; keep SpamAssassin test behavior and broader AntiSpam mutations out of scope.

## SEC-14 WebAdmin SMTP AntiSpam Settings Mutation Hardening (2026-07-28)

- The parity audit confirmed the authenticated path: legacy `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-123`) denies non-server-admin callers before returning Settings; `InterfaceSettings::get_AntiSpam` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1138-1159`) creates the child; and `InterfaceAntiSpam::LoadSettings` (`hmailserver/source/Server/COM/InterfaceAntiSpam.cpp:28-35`) requires server-admin state before loading the legacy configuration. The installed contract keeps `Settings.AntiSpam` at DISPID 63 and the `IInterfaceAntiSpam` property DISPIDs 1-38 (`hmailserver/source/Server/hMailServer/hMailServer.idl:608,2195-2273`).
- Code/test commit `8c751e65f` hardens `hmailserver/source/WebAdmin/hm_smtp_antispam.php`: `action` and all 22 existing AntiSpam save fields use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed AntiSpam writes. `WebAdminSmtpAntispamPostOnlySourceTests.SmtpAntispamSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `13/13`; full Net10 passes `1261` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. The manual checklist covers GET save, query-only action/field/token, valid POST behavior, AntiSpam field mapping, SpamAssassin test behavior, and SMTP spam-policy preservation.
- Normal GET rendering, AntiSpam field defaults, SpamAssassin AJAX behavior, the server-administrator Settings boundary, direct activation denial, installed COM identity/vtable/DISPID shape, SMTP/anti-spam/auto-ban behavior, live reconfiguration, service/database/data-directory state, and SEC-18 staging state remain unchanged. Next bounded slice: audit `hmailserver/source/WebAdmin/hm_mirror.php` `save` for POST-only handling while preserving the `MirrorEMailAddress` field mapping; keep SMTP delivery mirroring, live reconfiguration, SMTP trust, and broader Settings mutation out of scope.

## SEC-14 WebAdmin Mirror Settings Mutation Hardening (2026-07-28)

- The parity audit confirmed the authenticated path: legacy `InterfaceApplication::get_Settings` (`hmailserver/source/Server/COM/InterfaceApplication.cpp:110-123`) denies non-server-admin callers before returning Settings. `InterfaceSettings::get_MirrorEMailAddress` and `put_MirrorEMailAddress` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:207-240`) preserve `GetMirrorAddress`/`SetMirrorAddress`; the installed Settings contract retains `MirrorEMailAddress` at DISPID 7 (`hmailserver/source/Server/hMailServer/hMailServer.idl:528-534`).
- Code/test commit `9740cbc62` hardens `hmailserver/source/WebAdmin/hm_mirror.php`: `action` and `mirroremailaddress` use `hmailGetPostVar()`, and the `save` branch calls `hmailRequirePostCsrfToken()` before the existing COM-backed Settings write. `WebAdminMirrorPostOnlySourceTests.MirrorSaveUsesPostBodyAndRequiresPostCsrf` passes `1/1`; combined focused WebAdmin source coverage is `14/14`; full Net10 passes `1262` with `3` opt-in skips. PHP CLI is unavailable, so PHP lint was not run. The manual checklist covers GET save, query-only action/field/token, valid POST behavior, `MirrorEMailAddress` mapping, and SMTP delivery-mirroring preservation.
- Normal GET rendering, the Mirror field mapping, the server-administrator Settings boundary, direct activation denial, installed COM identity/vtable/DISPID shape, SMTP delivery behavior, live reconfiguration, service/database/data-directory state, and SEC-18 staging state remain unchanged. Next bounded slice: audit `hmailserver/source/WebAdmin/background_servermessage_save.php` for POST-only handling while preserving the existing server-message text Save behavior; keep broader background handler hardening and unrelated Settings mutation out of scope.

## SEC-14 WebAdmin ServerMessage Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceSettings::get_ServerMessages` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1161-1185`), `InterfaceServerMessages::LoadSettings` and `get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceServerMessages.cpp:13-21,91-117`), and `InterfaceServerMessage::put_Text`/`Save` (`hmailserver/source/Server/COM/InterfaceServerMessage.cpp:13-35,87-101`). The installed collection/item IIDs, DISPIDs, and vtable order remain those in `hmailserver/source/Server/hMailServer/hMailServer.idl:2324-2348`; server-admin Settings access remains enforced by the legacy `get_ServerMessages` boundary.
- Code/test commit `7d8321bc8` hardens `background_servermessage_save.php`: the handler requires POST plus POST-body CSRF, reads `messageid`, `messagename`, and `messagetext` through `hmailGetPostVar()`, and preserves the legacy name-integrity check, `ServerMessage.Text` assignment, `Save()`, and redirect. `hm_servermessage.php` posts the existing message name as a hidden field so the existing guard remains effective. Focused `WebAdminServerMessagePostOnlySourceTests`, `ServerMessagesComContractTests`, and `SqlServerServerMessageAdministrationStoreTests` pass `11/11`; full Net10 passes `1263` with `3` opt-in skips. PHP CLI is unavailable.
- No COM identity, direct activation boundary, authenticated Settings boundary, delivery-template execution, live reconfiguration, SMTP behavior, service, database, Data directory, or SEC-18 staging state changed. The next smallest remaining WebAdmin mutation is `hmailserver/source/WebAdmin/background_domain_name_save.php`; broader background handlers remain open.

## SEC-14 WebAdmin DomainAlias Mutation Hardening (2026-07-28)

- The parity audit confirmed the legacy C++ behavior in `hmailserver/source/Server/COM/InterfaceDomain.cpp:413-438` (`InterfaceDomain::get_DomainAliases`), `hmailserver/source/Server/COM/InterfaceDomainAliases.cpp:73-103,139-154` (`Add`, `DeleteByDBID`), and `hmailserver/source/Server/COM/InterfaceDomainAlias.cpp:71-128` (`put_AliasName`, `Save`). The authenticated child collection requires an attached domain and domain-admin state; `Add()` creates an unsaved item scoped to the owning collection, `Save()` persists it and attaches it after success, and `DeleteByDBID()` delegates to the parent collection. Installed IIDs, vtable/DISPID order, and CLSIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:1717-1752,3156-3168`.
- Code/test commit `6c4d879ee` hardens `hmailserver/source/WebAdmin/background_domain_name_save.php` with `hmailRequirePostCsrfToken()` before POST-only reads of `domainid`, `aliasid`, `action`, and `aliasname`. It preserves the existing server-admin guard, DomainAliases `Add`/`AliasName`/`Save`, `DeleteByDBID`, and redirect behavior. `WebAdminDomainAliasPostOnlySourceTests` plus DomainAliases COM/store coverage pass `8/8`; full Net10 passes `1264` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run. The manual WebAdmin checklist now covers both DomainAlias add and delete GET rejection, query-only inputs, and valid server-admin POST behavior.
- No production COM identity, direct activation boundary, authenticated domain/server-admin boundary, SMTP behavior, live reconfiguration, service/database/Data-directory state, or SEC-18 staging state changed. The next smallest safe WebAdmin mutation is `hmailserver/source/WebAdmin/background_tcpipport_save.php`.

## SEC-14 WebAdmin TCPIP Port Mutation Hardening (2026-07-28)

- Legacy behavior was confirmed in `InterfaceSettings::get_TCPIPPorts` (`hmailserver/source/Server/COM/InterfaceSettings.cpp:1187-1203`), `InterfaceTCPIPPorts::Add`/`DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceTCPIPPorts.cpp:101-168`), `InterfaceTCPIPPort::Save` and setters (`hmailserver/source/Server/COM/InterfaceTCPIPPort.cpp:33-293`), `PersistentTCPIPPort::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentTCPIPPort.cpp:53-101`), and `IOService::DoWork` (`hmailserver/source/Server/Common/TCPIP/IOService.cpp:66-145`). Installed `IInterfaceTCPIPPorts`/`IInterfaceTCPIPPort` IIDs, vtable/DISPID order, and CLSIDs remain in `hmailserver/source/Server/hMailServer/hMailServer.idl:616,2391-2432`.
- Code/test commit `272d56b5c` hardens `hmailserver/source/WebAdmin/background_tcpipport_save.php` with handler-wide `hmailRequirePostCsrfToken()` and POST-only reads for all seven mutation fields. Existing server-admin authorization, `Settings -> TCPIPPorts`, Add/Edit/DeleteByDBID, field assignments, Save, Stop/Start restart, ID assignment, redirects, and COM identity/direct activation boundaries are unchanged. Existing edit/delete forms already post the CSRF-bearing fields. Focused TCPIP/WebAdmin/store/COM tests pass `10/10`; full Net10 passes `1266` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The historical `background_iphome_save.php` path is not a parity implementation target: `hm_iphomes` was created by the 4.1 migration and dropped by `Upgrade4402to5000{MySQL,MSSQL}.sql`; no current legacy C++ class, IDL contract, .NET surface, or WebAdmin form exists. Next slice: `hmailserver/source/WebAdmin/background_sslcertificate_save.php`. SEC-18 remains RED and no production service, database, Data directory, DCOM, firewall, or staging broker state changed.

## SEC-14 WebAdmin Blocked Attachment Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceAntiVirus::get_BlockedAttachments` (`hmailserver/source/Server/COM/InterfaceAntiVirus.cpp:375-398`), `InterfaceBlockedAttachments::Add` and lookup/delete methods (`hmailserver/source/Server/COM/InterfaceBlockedAttachments.cpp:75-151`), `InterfaceBlockedAttachment::Save` and setters (`hmailserver/source/Server/COM/InterfaceBlockedAttachment.cpp:14-103`), and `PersistentBlockedAttachment::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentBlockedAttachment.cpp:51-84`). Legacy Add returns a staged item attached to the owning collection; Save inserts `hm_blocked_attachments` for ID zero, assigns the generated ID, and adds the item to the parent after successful persistence.
- Code/test commit `bfee58cab` hardens `hmailserver/source/WebAdmin/background_blocked_attachment_save.php`: the existing server-admin guard remains first, the handler requires `hmailRequirePostCsrfToken()`, and `id`, `wildcard`, `description`, and `action` are read from POST only. Existing `Settings -> AntiVirus -> BlockedAttachments` mappings, Add/Edit/DeleteByDBID, field assignments, Save, redirects, and forms remain unchanged. Focused source/COM/store coverage passes `38/38`; full Net10 passes `1274` with `3` opt-in skips. PHP CLI is unavailable.
 - The .NET BlockedAttachments adapter and SQL administration store remain intentionally read-only; direct child activation remains denied outside the authenticated Settings boundary. SMTP attachment-policy behavior, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. Next slice: `background_route_save.php` POST-only/CSRF hardening.

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
- The .NET `DomainAliases` adapter and SQL administration store remain intentionally read-only for Add, item setters, Save, and Delete; direct child activation remains `E_ACCESSDENIED`, while the installed IID/vtable/DISPID/class identities and authenticated domain-owned read boundary remain unchanged. SMTP alias behavior, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. Route hardening is recorded in `8d684e638`, SecurityRanges handler hardening in `97e3096c3`, account handler hardening in `95a7e4284`, rule handler hardening in `6736e161e`, and domain handler hardening in `3d25cb0a7`; the next COM/Admin slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the owning `RuleCriteria.Save()` path.

## SEC-14 WebAdmin Route Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceRoutes::LoadSettings`, `Add`, and `DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceRoutes.cpp:12-29,75-105`), `InterfaceRoute` setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceRoute.cpp:67-243,284-582`), and `PersistentRoute::SaveObject`/`DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentRoute.cpp:31-70`). The installed `IInterfaceRoute` and `IInterfaceRoutes` contracts, DISPIDs, and collection order remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1378-1426,1533-1550`; route coclasses remain at `hmailserver/source/Server/hMailServer/hMailServer.idl:3052-3094`.
- Code/test commit `8d684e638` hardens `hmailserver/source/WebAdmin/background_route_save.php`: the existing server-admin denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and `action`, `routeid`, and all route fields are read from POST only. Existing `Settings -> Routes` ItemByDBID/Add/DeleteByDBID lookup, field assignments, conditional `SetRelayerAuthPassword`, Save, redirects, and `hm_route.php`/`hm_routes.php` forms remain unchanged. `WebAdminRoutePostOnlySourceTests` plus route COM/address and SQL-store coverage passes `19/19`; full Net10 passes `1280` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Routes` adapter and SQL administration store remain intentionally read-only for collection/item mutation; direct route activation remains `E_ACCESSDENIED`, while the authenticated Settings read boundary and installed COM identity/vtable/DISPID shape remain unchanged. SMTP routing, live reconfiguration, service/database/Data-directory state, and SEC-18 staging state did not change. SecurityRanges handler hardening is recorded in `97e3096c3`; next slice: `hmailserver/source/WebAdmin/background_account_save.php` POST-only/CSRF hardening.

## SEC-14 WebAdmin SecurityRanges Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceSecurityRanges::LoadSettings`, `DeleteByDBID`, and `Add` (`hmailserver/source/Server/COM/InterfaceSecurityRanges.cpp:13-21,60-74,158-185`), `InterfaceSecurityRange::Save`, `put_LowerIP`, `put_UpperIP`, and the remaining field setters (`hmailserver/source/Server/COM/InterfaceSecurityRange.cpp:36-841`), and `PersistentSecurityRange::SaveObject`/`Validate` (`hmailserver/source/Server/Common/Persistence/PersistentSecurityRange.cpp:52-117,268-289`). Legacy Save validates and persists through `hm_securityranges`; the handler field mapping remains the reference. Installed `IInterfaceSecurityRange`/`IInterfaceSecurityRanges` IIDs, vtable order, and DISPIDs remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1232-1320`, with `Settings.SecurityRanges` at DISPID 18 (`:540`).
- Code/test commit `97e3096c3` hardens `hmailserver/source/WebAdmin/background_securityrange_save.php`: the existing server-admin denial remains first, the handler requires `hmailRequirePostCsrfToken()`, and `action`, `securityrangeid`, and all remaining range fields are read from POST only. Existing `Settings -> SecurityRanges` ItemByDBID/Add/DeleteByDBID lookup, field assignments, Save, redirects, delete flow, and both forms remain unchanged. `WebAdminSecurityRangePostOnlySourceTests` plus SecurityRanges COM and SQL-store coverage passes `30/30`; full Net10 passes `1281` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `SecurityRanges` adapter and SQL administration store retain the existing authenticated server-administrator boundary, installed COM identity/vtable/DISPID shape, and current mutation implementation; this slice changes no COM, SQL, IP-policy, auto-ban, SMTP trust, live-reconfiguration, service/database/Data-directory, or SEC-18 behavior. Account handler hardening is recorded in `95a7e4284`, rule handler hardening in `6736e161e`, domain handler hardening in `3d25cb0a7`; the next COM/Admin slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the owning `RuleCriteria.Save()` path.

## SEC-14 WebAdmin Account Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceAccounts::Add`/`DeleteByDBID` (`hmailserver/source/Server/COM/InterfaceAccounts.cpp:42-74,202-231`), `InterfaceAccount::Save`, `put_Password`, and the account field setters (`hmailserver/source/Server/COM/InterfaceAccount.cpp:74-1048`), and `PersistentAccount::SaveObject`/`DeleteObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:55-329`). Installed `IInterfaceAccounts`/`IInterfaceAccount` IIDs, vtable order, and DISPIDs remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:794-895`; account coclasses remain at `hmailserver/source/Server/hMailServer/hMailServer.idl:2904-2919`.
- Code/test commit `95a7e4284` hardens `hmailserver/source/WebAdmin/background_account_save.php`: the handler requires `hmailRequirePostCsrfToken()` before reading `domainid`, `accountid`, `action`, or account fields, and all 26 inputs use `hmailGetPostVar()`. Existing user self-edit and session-password update behavior, domain-admin ownership, server-admin restrictions, `Settings -> Domains -> Domain -> Accounts` lookup, Add/Edit/Delete, account field mappings, Save, redirects, and both forms remain unchanged. `WebAdminAccountPostOnlySourceTests` plus account COM/store coverage passes `19/19`; full Net10 passes `1282` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Accounts` adapter and SQL administration store retain the existing authenticated domain-owned read boundary, direct activation denial, installed COM identity/vtable/DISPID shape, and current mutation status; this slice changes no password storage, SMTP delivery, account-rule runtime, service/database/Data-directory, or SEC-18 behavior. Rule handler hardening is recorded in `6736e161e`, domain handler hardening in `3d25cb0a7`; the next COM/Admin slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the owning `RuleCriteria.Save()` path.

## SEC-14 WebAdmin Rule Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceRules::Add`, `DeleteByDBID`, and `Refresh` (`hmailserver/source/Server/COM/InterfaceRules.cpp:19-143`), `InterfaceRule` field setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceRule.cpp:66-306`), `InterfaceRuleCriterias::Add`/`InterfaceRuleCriteria::Save` (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:91-122`, `hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:13-258`), `InterfaceRuleActions::Add`/`InterfaceRuleAction::Save` (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:93-124`, `hmailserver/source/Server/COM/InterfaceRuleAction.cpp:30-587`), and `PersistentRule*::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRule.cpp:73-192`, `PersistentRuleCriteria.cpp:58-116`, `PersistentRuleAction.cpp:65-141`). Installed rule interfaces and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1758-1900,3173-3215`.
- Code/test commit `6736e161e` hardens `hmailserver/source/WebAdmin/background_rule_save.php`: `GetHasRuleAccess` remains the ownership boundary, the handler requires `hmailRequirePostCsrfToken()`, and all scope, rule, criteria, and action inputs use `hmailGetPostVar()`. Existing global/account lookup, Add/Edit/Delete, criteria/action branches, non-server-admin action restrictions, field mapping, Save, redirects, and POST+CSRF forms remain unchanged. `WebAdminRulePostOnlySourceTests` plus rule COM/store coverage passes `70/70`; full Net10 passes `1283` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET rule adapters retain their current read-only/E_NOTIMPL mutation boundaries and direct activation denial; `RuleCriteria.HeaderField` now stages through its authorized owning Save callback, while the other criteria setters remain E_NOTIMPL. No COM identity, authenticated Settings boundary, SMTP rule behavior, service/database/Data-directory state, or SEC-18 staging state changed. Next slice: authenticated existing-row `RuleCriteria.MatchValue` setter parity through the owning `RuleCriteria.Save()` path.

## SEC-14 WebAdmin Domain Mutation Hardening (2026-07-28)

- Legacy parity was confirmed in `InterfaceDomains::Add`, `get_ItemByDBID`, `DeleteByDBID`, and authentication (`hmailserver/source/Server/COM/InterfaceDomains.cpp:44-64,99-219,252-269`), `InterfaceDomain` setters, `Save`, and `Delete` (`hmailserver/source/Server/COM/InterfaceDomain.cpp:56-353,480-1431`), and `PersistentDomain::DeleteObject`/`SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentDomain.cpp:46-234`). Installed `IInterfaceDomain`/`IInterfaceDomains` contracts and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:707-753,1512-1525,2900-2902,3084-3086`.
- Code/test commit `3d25cb0a7` hardens `hmailserver/source/WebAdmin/background_domain_save.php`: the existing domain-admin ownership and server-admin delete checks remain, the handler requires `hmailRequirePostCsrfToken()`, and all 30 scope/domain/DKIM/signature inputs use `hmailGetPostVar()`. Existing domain lookup/mutation/field mappings, Save, redirects, and POST+CSRF forms remain unchanged. `WebAdminDomainPostOnlySourceTests` plus domain/domain-alias COM/store coverage passes `16/16`; full Net10 passes `1284` with `3` opt-in skips. PHP CLI is unavailable, so runtime PHP validation was not run.
- The .NET `Domains`/`Domain` adapters and SQL administration stores retain their current authenticated access boundaries, read-only COM mutation status, direct activation denial, installed identities, SMTP/domain behavior, service/database/Data-directory state, and SEC-18 staging state. The next COM/Admin slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the existing owning `RuleCriteria.Save()` path.

## SEC-11 RuleCriteria HeaderField Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_HeaderField` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,122-152`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38` and `hmailserver/source/Server/COM/COMCollection.h:11-38`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:65-100`). The legacy setter stages the raw BSTR with no validation or normalization; detached objects return `E_ACCESSDENIED`; Save persists `criteriaheadername` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` IIDs, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,2844-2849,3185-3200`. The .NET contracts retain the same identities, BSTR marshaling, and direct activation boundary.
- Code/test commit `c8d69c9b8` makes `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs` use a mutable item-local snapshot for `HeaderField`, passes the staged snapshot through the owning save delegate, and leaves other criteria setters, Add, Delete, Refresh, rule execution, and SMTP behavior unchanged. `RuleCriteriasComContractTests` and related SQL/integration coverage pass `31/31`; full Net10 passes `1284` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is authenticated existing-row `RuleCriteria.MatchValue` setter parity through the same owning `RuleCriteria.Save()` path.

## SEC-11 RuleCriteria MatchValue Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_MatchValue` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,154-184`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:65-100`). The setter stages the raw BSTR without validation, trimming, or normalization; detached objects return `E_ACCESSDENIED`; Save persists `criteriamatchvalue` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,2844-2849,3185-3200`. The .NET contracts, BSTR marshaling, authenticated access boundary, and direct activation denial remain unchanged.
- Code/test commit `d95ce9c69` changes only `RuleCriteria.MatchValue` to use the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `33/33`; full Net10 passes `1286` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- Next slice: authenticated existing-row `RuleCriteria.MatchType` setter parity through the same owning `RuleCriteria.Save()` path. Keep broader criteria mutation, SMTP rule behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleCriteria UsePredefined Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_UsePredefined` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,105-120`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:65-100`). The attached setter stores exactly `newVal == VARIANT_TRUE` and returns `S_OK`; detached objects return `E_ACCESSDENIED`; Save persists `criteriausepredefined` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,2844-2849,3185-3200`. The .NET contracts, `VARIANT_BOOL` marshaling, authenticated access boundary, and direct activation denial remain unchanged.
- Code/test commit `a4ff728c0` changes only the authorized `RuleCriteria.UsePredefined` setter to use the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `35/35`; full Net10 passes `1288` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- Next slice: authenticated existing-row `RuleCriteria.PredefinedField` setter parity through the same owning `RuleCriteria.Save()` path. Keep broader criteria mutation, SMTP rule behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleCriteria PredefinedField Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::put_PredefinedField` and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-48,186-216`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38`), and `PersistentRuleCriteria::ReadObject`/`SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:46-52,65-100`). The attached setter casts and stores the raw enum value without validation or normalization and returns `S_OK`; detached objects return `E_ACCESSDENIED`; Save persists `criteriapredefinedfield` for the attached criterion.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, enum GUID/values, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:205-238,1792-1837,3185-3200`. The .NET contracts, enum mapping, authenticated access boundary, and direct activation denial remain unchanged.
- Code/test commit `fabc7e03a` changes only the authorized `RuleCriteria.PredefinedField` setter to stage `(int)value` through the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `37/37`; full Net10 passes `1290` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- Next slice: authenticated existing-row `RuleCriteria.RuleID` setter/save parity through the owner-scoped path; keep broader criteria mutation, SMTP rule behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleCriteria MatchType Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::get_MatchType`, `put_MatchType`, and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-55,218-248`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:19-38` and `hmailserver/source/Server/COM/COMCollection.h:11-38`), and `PersistentRuleCriteria::ReadObject`/`SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:46-52,64-100`). The attached setter raw-casts and stores any enum integer without validation or normalization and returns `S_OK`; existing-row Save persists `criteriamatchtype`; detached objects remain access-denied.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, enum GUID/values, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:240-272,1792-1837,3185-3200`. The .NET contracts, enum mapping, authenticated Settings boundary, owning collection, and direct activation denial remain unchanged.
- Code/test commit `0d9e43b14` changes only the authorized `RuleCriteria.MatchType` setter to stage `(int)value` through the existing mutable snapshot/owning Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `39/39`; full Net10 passes `1292` with `3` opt-in tests skipped. PHP CLI is unavailable and no live SQL integration ran because the approved connection variable was unset.
- Next slice: authenticated existing-row `RuleCriteria.RuleID` setter/save parity through the owner-scoped path; keep broader criteria/action/rule mutation, SMTP rule behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleCriteria Owner-Scoped Save Contract (2026-07-29)

- Legacy ownership behavior was confirmed in `InterfaceRuleCriteria::put_RuleID`/`Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-87`), parent attachment (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:14-114` and `hmailserver/source/Server/COM/COMCollection.h:11-32`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:64-100`). Legacy accepts raw attached `RuleID` values and writes the mutable destination by `criteriaid`; detached access remains denied.
- The .NET save contract now carries the immutable owning rule ID separately from the snapshot destination in `IRuleCriteriaAdministrationStore`, passes the captured parent ID from `RuleCriteriaAdministrationRuntimeHost`, scopes SQL with `criteriaruleid = @OwningRuleId AND criteriaid = @CriteriaId`, and retains `SET criteriaruleid = @RuleId`. Non-single-row updates now fail deterministically. COM identity, authenticated Settings access, direct activation denial, and the RuleID staging boundary remain unchanged.
- Code/test commit `edf97aeaa` changes `IRuleCriteriaAdministrationStore`, the RuleCriteria runtime save closure, the SQL administration store, and focused COM/SQL contract tests. Focused criteria/SQL/integration coverage passes `40/40`; full Net10 passes `1293` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- Next slice: an authenticated repeated-`Rule.Actions` adapter visibility audit; keep Add/new-item Save, broader action mutation, SMTP rule behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleCriteria RuleID Setter Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleCriteria::get_RuleID`, `put_RuleID`, and `Save` (`hmailserver/source/Server/COM/InterfaceRuleCriteria.cpp:30-87`), the owning lookup/parent attachment path (`hmailserver/source/Server/COM/InterfaceRuleCriterias.cpp:14-114` and `hmailserver/source/Server/COM/COMCollection.h:11-32`), and `PersistentRuleCriteria::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleCriteria.cpp:64-100`). The attached setter stores any raw `LONG`, including foreign, zero, negative, and nonexistent target rule IDs, and returns `S_OK`; Save writes the destination by criterion ID while the original parent collection remains the owner.
- Installed `IInterfaceRuleCriteria`/`IInterfaceRuleCriterias` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1792-1837,3185-3200`. The .NET contracts, authenticated Settings boundary, direct activation denial, and owner-scoped SQL save path remain unchanged.
- Code/test commit `66e72f39c` changes only the authorized `RuleCriteria.RuleID` setter to stage raw values through the existing mutable snapshot/owner-scoped Save delegate pattern in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleCriterias.cs`. Focused criteria/SQL/integration coverage passes `43/43`; full Net10 passes `1296` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- Next slice: an ownership/save-containment audit for authenticated existing-row `RuleAction.RuleID`; keep RuleAction setter mutation, parent snapshot propagation, broader action/rule mutation, SMTP behavior, backup archive/XML execution, SEC-18 broker registration, DCOM ACL changes, and PHP session cutover out of scope.

## SEC-11 RuleAction RuleID Ownership/Save Parity (2026-07-29)

- Legacy parity was confirmed in `InterfaceRuleAction::put_RuleID`, `Save`, and `get_RuleID` (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:30-72,106-136`), parent lookup/attachment (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:22-116` and `hmailserver/source/Server/COM/COMCollection.h:6-38`), and `PersistentRuleAction::SaveObject` (`hmailserver/source/Server/Common/Persistence/PersistentRuleAction.cpp:77-116`). Attached setters accept raw `LONG` values, including foreign, zero, negative, and nonexistent rule IDs, and return `S_OK`; detached access remains `E_ACCESSDENIED`. Save persists the mutable destination by action ID, while the original parent collection remains the owner and shared legacy items expose the mutation before refresh.
- Installed `IInterfaceRuleAction`/`IInterfaceRuleActions` identities, vtable order, DISPIDs, and coclasses remain anchored by `hmailserver/source/Server/hMailServer/hMailServer.idl:1839-1900,3201-3215`; the .NET contracts and direct activation/read-only facade boundaries remain unchanged.
- Code/test commit `9680640a5` changes only the authorized `RuleAction.RuleID` setter, the RuleAction administration store owner parameter, the SQL owner-plus-action scope, and focused contract/store tests in `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleActions.cs`, `hmailserver/source/Server.Net10/src/HMailServer.Core/Abstractions/IRuleActionAdministrationStore.cs`, `hmailserver/source/Server.Net10/src/HMailServer.Storage.SqlServer/SqlServerRuleActionAdministrationStore.cs`, and the corresponding tests. Focused coverage passes `47/47`; full Net10 passes `1300` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit. Do not broaden to message mutation, SMTP execution, backup/XML behavior, COM identity, or SEC-18 work.

## SEC-11 RuleAction Parent-Snapshot Visibility (2026-07-29)

- Legacy parity was confirmed in cached parent access (`hmailserver/source/Server/Common/BO/Rule.cpp:49`, `Rule.h:33`), child lookup/attachment (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:22-49` and `hmailserver/source/Server/COM/COMCollection.h:11-22`), mutable action setters (`hmailserver/source/Server/COM/InterfaceRuleAction.cpp:122-563`), and collection refresh/delete behavior (`hmailserver/source/Server/COM/InterfaceRuleActions.cpp:125-159`, `hmailserver/source/Server/Common/BO/RuleActions.cpp:25-67`). Existing child wrappers and the owning vector observe the same mutable object immediately, including after Save failure; Refresh replaces the collection and new Add items remain distinct until successful Save.
- The .NET adapter now uses a private shared `RuleActionAdministrationEntry` per loaded row. Index and DBID lookups share that entry, setters update it immediately, Save reads it, Refresh replaces entries, and Delete removes entries only after store success. Installed `IInterfaceRuleAction`/`IInterfaceRuleActions` identities and direct activation/read-only boundaries remain unchanged.
- Code/test commit `dc2fe2118` changes only `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/RuleActions.cs` and focused `RuleActionsComContractTests`. Coverage passes `48/48`; full Net10 passes `1301` with `3` opt-in tests skipped. No live SQL integration ran because the approved connection variable was unset.
- The next bounded COM/Admin slice is an authenticated repeated-`Account.Messages` adapter visibility audit. Do not broaden to message mutation, SMTP execution, backup/XML behavior, COM identity, or SEC-18 work.

## SEC-11 Rule.Actions Per-Rules-Generation Adapter Visibility (2026-07-29)

- Legacy parity was confirmed in `HM::Rule::GetActions` (`hmailserver/source/Server/Common/BO/Rule.cpp:49-59`, `Rule.h:45-46`), `InterfaceRule::get_Actions` (`hmailserver/source/Server/COM/InterfaceRule.cpp:195-213`), and `InterfaceRules::get_Item`/`get_ItemByDBID` (`hmailserver/source/Server/COM/InterfaceRules.cpp:19-72`). Legacy returns fresh COM collection/rule wrappers over cached per-rule BO objects; `Rules::Refresh` rebuilds the parent objects, while existing wrappers retain the old rule/action state.
- The .NET `Rules` adapter now maintains private per-generation, per-rule action state. Repeated `Rule.Actions` access and distinct rule wrappers share the same state within one authenticated `Rules` generation; `Rules.Refresh()` publishes a new generation without rebinding existing wrappers. `RuleActions` retains fresh facades over shared state, owner-scoped mutation callbacks, authenticated `Settings` access, direct activation denial, and installed COM identity/vtable/DISPID shape.
- Code/test commit `493848279` changes `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/Rules.cs`, `RuleActions.cs`, and focused `RuleActionsComContractTests.cs`. Focused Rules/RuleActions/SQL coverage passes `58/58`; full Net10 passes `1305` with `3` opt-in tests skipped. No live SQL integration ran and PHP CLI remains unavailable. No SMTP, service/database/Data-directory, COM registration/ACL, or SEC-18 staging state changed.
- Next slice: authenticated repeated-`Account.Messages` adapter visibility audit. Keep message mutation and broader rule/action mutation out of scope.

## SEC-11 Account.Rules Per-Account-State Visibility (2026-07-29)

- Legacy parity was confirmed in `InterfaceAccount::get_Rules` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:790-815`), `Account::GetRules` (`hmailserver/source/Server/Common/BO/Account.cpp:119-128`), and cached `Account::rules_` (`hmailserver/source/Server/Common/BO/Account.h:162`). Legacy lazily loads one `HM::Rules(id_)`, refreshes it once, and returns fresh COM `InterfaceRules` wrappers over that cached object. `InterfaceRules::Refresh` (`hmailserver/source/Server/COM/InterfaceRules.cpp:143-152`) refreshes shared state; `InterfaceAccounts` lookups (`hmailserver/source/Server/COM/InterfaceAccounts.cpp:98-190`) share the account object until collection refresh.
- The .NET adapters now create one lazy `RuleAdministrationState` per attached account entry, including Administrator account ID `0`; repeated `Account.Rules` calls return fresh `Rules` facades over shared state, one store load is shared, and `Rules.Refresh()` is visible through all facades. `Accounts.Refresh()` isolates new account entries while old Account/Rules wrappers retain their previous state. Existing authenticated/direct-activation boundaries and installed Account/Rules COM identities remain unchanged.
- Code/test commit `bb4142b99` changes `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AccountComClass.cs`, `Accounts.cs`, `Rules.cs`, and focused `AccountsComContractTests.cs`/`RulesComContractTests.cs`. Focused Rules/Accounts/SQL coverage passes `38/38`; full Net10 passes `1307` with `3` opt-in tests skipped. No live SQL integration ran and PHP CLI remains unavailable; SMTP, service/database/Data-directory, COM registration/ACL, and SEC-18 staging state did not change.
- Next slice: authenticated repeated-`Account.Messages` adapter visibility audit; keep message mutation and broader account/admin mutation out of scope.

## SEC-11 Account.Messages Per-Account-State Visibility (2026-07-29)

- Legacy parity was confirmed in `HM::Account::GetMessages` (`hmailserver/source/Server/Common/BO/Account.cpp:107-116`), cached `Account::messages_` (`hmailserver/source/Server/Common/BO/Account.h:161`), `InterfaceAccount::get_Messages` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:420-445`), `HM::Messages::Refresh` (`hmailserver/source/Server/Common/BO/Messages.cpp:144-209`), and `Accounts::Refresh`/collection lookup lifetime (`hmailserver/source/Server/Common/BO/Accounts.cpp:24-55`, `hmailserver/source/Server/Common/BO/Collection.h:138-177,232-277`). Legacy creates one cached message collection per loaded account and fresh COM wrappers over it; Accounts.Refresh publishes new account objects while existing wrappers retain their old children.
- The .NET adapter now creates one lazy `AccountMessageAdministrationState` per attached account entry. Repeated `Account.Messages` calls return fresh `Messages` facades over one cached store snapshot; Accounts.Refresh publishes new account/message state while old Account wrappers retain their prior snapshot. Direct activation remains `E_ACCESSDENIED`, authenticated Settings access is unchanged, and installed Account/Messages IID, vtable, DISPID, CLSID, and ProgID identity remains unchanged.
- Code/test commit `0c2ee1226` changes only `hmailserver/source/Server.Net10/src/HMailServer.ComInterop/AccountComClass.cs`, `Accounts.cs`, `Messages.cs`, and focused `AccountsComContractTests.cs`/`MessagesComContractTests.cs`. Focused Messages/Accounts/Application/SQL coverage passes `48/48`; full Net10 passes `1308` with `3` opt-in tests skipped. No live COM integration ran, PHP CLI remains unavailable, and no SMTP, service/database/Data-directory, COM registration/ACL, or SEC-18 staging state changed.
## SEC-11 Account.Messages SQL Projection Parity (2026-07-29)

- Legacy parity was confirmed in `HM::Account::GetMessages` (`hmailserver/source/Server/Common/BO/Account.cpp:107-116`), `HM::Messages::Refresh` (`hmailserver/source/Server/Common/BO/Messages.cpp:143-209`), `Messages::AddToCollection` (`hmailserver/source/Server/Common/BO/Messages.cpp:211-241`), and `InterfaceAccount::get_Messages` (`hmailserver/source/Server/COM/InterfaceAccount.cpp:420-445`). Account-level `Messages(accountId, -1)` uses `messageaccountid = @MESSAGEACCOUNTID`, no message-type or folder predicate, and `ORDER BY messageuid ASC`; queue and folder branches remain distinct.
- The .NET `SqlServerMessageAdministrationStore.GetAccountMessagesSql` query now matches that account-level predicate/order. `GetFolderMessagesSql`, the message snapshot shape, COM identities, and IMAP/POP3/SMTP stores remain unchanged.
- Code/test commit `debc93dac` changes only `hmailserver/source/Server.Net10/src/HMailServer.Storage.SqlServer/SqlServerMessageAdministrationStore.cs` and `SqlServerMessageAdministrationStoreTests.cs`. Focused SQL/Message/Account coverage passes `38/38`; full Net10 passes `1308` with `3` opt-in tests skipped. No live SQL integration ran.
- Next slice: authenticated per-account `Account.IMAPFolders` cached snapshot and shared folder-adapter visibility. Keep folder Add/Delete/Save/setters, ACL mutation, live protocol/cache synchronization, `CurrentUID` protocol semantics, SQL schema changes, COM identity, and SEC-18 work out of scope.

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
- The bounded `BackupManager` archive/XML, non-secret raw settings-property, backup-side `DomainAliases`, backup-side non-secret scalar `Accounts`, backup-side normal domain `Aliases`, backup-side `DistributionLists`, and backup-side account credential child-serialization slices are complete in code/test commits `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, `3e7535d76`, `5d4981240`, and `fd30ceb33`. Legacy `PropertySet::Refresh`/`XMLStore` (`hmailserver/source/Server/Common/Application/PropertySet.cpp:31-181`), `Configuration::XMLStore` (`hmailserver/source/Server/Common/Application/Configuration.cpp:687-713`), `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:104-149`), `Accounts::Refresh` (`hmailserver/source/Server/Common/BO/Accounts.cpp:34-56`), `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`), `PersistentAccount::ReadObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:146-191`), `Aliases::Refresh` (`hmailserver/source/Server/Common/BO/Aliases.cpp:26-32`), `Alias::XMLStore` (`hmailserver/source/Server/Common/BO/Alias.cpp:28-37`), `DistributionLists::Refresh` (`hmailserver/source/Server/Common/BO/DistributionLists.cpp:40-47`), `DistributionList::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionList.cpp:31-45`), `DistributionListRecipients::Refresh` (`hmailserver/source/Server/Common/BO/DistributionListRecipients.cpp:27-34`), `DistributionListRecipient::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionListRecipient.cpp:30-38`), `Time::GetCurrentDateTime` (`hmailserver/source/Server/Common/Util/Time.cpp:25-34`), `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`), and `DomainAlias::XMLStore` (`hmailserver/source/Server/Common/BO/DomainAlias.cpp:26-33`) are documented; the raw read preserves row names, integer/string values, ordinal ordering, seeded/unknown rows, and excludes `smtprelayerpassword`. Focused backup/account credential coverage passes `44/44`; the next production-gate slice is backup-side `FetchAccounts` child serialization. Nested account children, `Rules`, and `Folders`, plus message/data-directory payloads, restore execution, destructive SQL, event dispatch, and PHP session cutover remain out of scope.

## SEC-11 Backup Archive/XML Creation (2026-07-30)

- Legacy parity was confirmed in `InterfaceBackupManager::StartBackup` (`hmailserver/source/Server/COM/InterfaceBackupManager.cpp:26-40`), `BackupManager::StartBackup` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:37-71`), `BackupTask::DoWork` (`hmailserver/source/Server/Common/Application/BackupTask.cpp:26-40`), `BackupExecuter::StartBackup` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:57-190`), `Configuration::XMLStore` (`hmailserver/source/Server/Common/Configuration/Configuration.cpp:687-713`), and `Compression::AddFile` (`hmailserver/source/Server/Common/Util/Compression.cpp:27-45`).
- Code/test commit `a1f1d92f4` carries one successful read-only `BackupStartPlanEvidence` object into a shell-free 7z writer with legacy filename, mode, metadata XML, and loose-XML cleanup behavior. Scalar settings/domain snapshots are emitted; `BOMessages` rejects before file creation and no `DataBackup` directory is staged. Installed `IInterfaceBackupManager` identity and direct activation boundaries remain unchanged.
- Code/test commit `59ac1b7c6` adds the backup-only raw `hm_settings` snapshot, commit `f15e857a8` adds the existing `IDomainAliasAdministrationStore` plumbing plus `DomainAliases`/`DomainAlias Name` serialization in legacy order, commit `ac611987c` adds existing `IAccountAdministrationStore` plumbing plus non-secret scalar `Accounts`/`Account` serialization after `DomainAliases`, and commit `3e7535d76` adds existing `IAliasAdministrationStore` plumbing plus normal `Aliases`/`Alias` serialization after `Accounts`. Empty child containers remain omitted. Account credentials, `FetchAccounts`, `Rules`, and `Folders` remain fenced. Focused backup/alias coverage passes `54/54`; full Net10 passes `1342` with `3` opt-in tests skipped. Remaining backup risks are `DistributionLists` children, message/data-directory payloads, restore, destructive SQL, and event dispatch. The next slice is backup-side `DistributionLists` child serialization.

## SEC-11 Backup DistributionLists XML Serialization (2026-07-30)

- Legacy `Domain::XMLStore` (`hmailserver/source/Server/Common/BO/Domain.cpp:104-149`) writes `DistributionLists` after normal `Aliases`. `DistributionLists::Refresh` (`hmailserver/source/Server/Common/BO/DistributionLists.cpp:40-47`) orders lists by address; `DistributionList::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionList.cpp:31-45`) emits `Name`, `Active`, `RequiresAuth`, `RequiresAuthAddress`, and `ListMode` in that order. `DistributionListRecipients::Refresh` (`hmailserver/source/Server/Common/BO/DistributionListRecipients.cpp:27-34`) orders recipients by address, and `DistributionListRecipient::XMLStore` (`hmailserver/source/Server/Common/BO/DistributionListRecipient.cpp:30-38`) emits the optional inner `DistributionList` container with `Recipient Name`. `Collection<T,P>::XMLStore` (`hmailserver/source/Server/Common/BO/Collection.h:61-82`) omits empty containers.
- Code/test commit `5d4981240` wires the existing `IDistributionListAdministrationStore` and `IDistributionListRecipientAdministrationStore` into the backup payload provider, loads one scoped list snapshot per selected domain and one recipient snapshot per selected list, and emits escaped `DistributionLists` after `Aliases` with no `ID` or `DomainID` attributes. Existing COM identities, authenticated Settings/Domain read boundaries, direct activation denial, SMTP list-policy behavior, mutation status, restore behavior, and SQL schema remain unchanged.
- Focused backup/distribution-list coverage passes `34/34`; full Net10 passes `1344` with `3` opt-in tests skipped. The historical next bounded backup slice was account `Password`/`PasswordEncryption` serialization, now complete in `fd30ceb33`; `FetchAccounts`, nested account children, message/data-directory payloads, restore, destructive SQL, and event dispatch remain explicit gaps. The current next slice is `FetchAccounts` child serialization.

## SEC-11 Backup Account Password/PasswordEncryption XML Serialization (2026-07-30)

- Legacy `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`) emits `Password` and integer `PasswordEncryption` immediately after `Active`; `PersistentAccount::ReadObject` (`hmailserver/source/Server/Common/Persistence/PersistentAccount.cpp:146-191`) loads `accountpassword` and `accountpwencryption`, and the schema defines both columns in `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:168-195`.
- Code/test commit `fd30ceb33` adds `AccountBackupAdministrationSnapshot` and `IBackupAccountAdministrationStore`, a dedicated SQL projection, host wiring, and backup XML emission at the legacy attribute position. Ordinary `IAccountAdministrationStore` queries remain secret-free; attached `Account.Password`, `ValidatePassword`, mutation, COM identities, direct activation denial, SMTP/POP3 behavior, restore behavior, and nested account children remain unchanged.
- Focused backup/account credential coverage passes `44/44`; full Net10 passes `1346` with `3` opt-in tests skipped. The next bounded backup slice is `FetchAccounts` child serialization; its encrypted fetch password, UID children, Rules, Folders, message/data-directory payloads, restore, destructive SQL, and event dispatch remain fenced.

## SEC-18 Current Evidence (2026-07-28)

- The fresh fail-closed collector `artifacts/sec18-staging/staging-inventory-20260728-nonpool-denial-failclosed.json` exits `2` because the current medium-integrity shell cannot read IIS mappings and no fresh caller-token evidence exists. The temporary probe is absent, the current endpoint is `404`, hMailServer is `Stopped`/`Disabled`, and the sanitized approval-rerun report is recorded in `artifacts/sec18-staging/SEC18-nonpool-approval-rerun-20260728.md`.
- `hmail_security_reviewer` and `hmail_reality_checker` both returned **RED**. Permanent broker registration, DCOM ACL changes, `hMailServer.Application` activation, and PHP session cutover remain blocked until an elevated isolated authorized/non-pool matrix provides fresh exact stage/HRESULT, immutable counter/correlation, cleanup, and independent-review evidence.

The current sections supersede the older next-slice and SEC-18 status notes below.


## Yeni Thread Icin Baslangic Talimati

1. Repo kokune gec: `<repo-root>`.
2. `README.md`, `hmailserver/source/Server.Net10/README.md`, `hmailserver/source/Server.Net10/REWRITE_BACKLOG.md`, `AGENTS.md` ve bu dosyayi oku.
3. `git status --short --branch` ve `git diff` calistir; mevcut WIP kod degisikliklerini sahiplenmeden once anla.
4. Net10 on kosullari dogrula:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build\check-net10-prereqs.ps1 -RequireMsBuild
```

5. Current Next Slice olarak backup-side `DistributionLists` child serialization'i tamamlandi. Authenticated `BackupManager` archive/XML, non-secret raw settings-property, `DomainAliases`, non-secret scalar `Accounts`, normal `Aliases`, `DistributionLists`, and account `Password`/`PasswordEncryption` child parity `a1f1d92f4`, `59ac1b7c6`, `f15e857a8`, `ac611987c`, `3e7535d76`, `5d4981240`, and `fd30ceb33` ile bounded seviyede tamamlandi. FetchAccount.Delete parity `cccc3e64c` ile tamamlandi. `FetchAccounts`, Rules, Folders, nested account children, message/data-directory payloads, restore execution, destructive SQL, event dispatch, service/DB/data-directory state, SEC-18 permanent broker registration, DCOM ACL yazimi, `hMailServer.Application` activation ve PHP session cutover halen fenced/RED/block ediliyor. Next slice `FetchAccounts` child serialization.
6. Kucuk kod/test commit'i yap, sonra README/backlog/handoff dokumanlarini ayri committe guncelle; kullanici ozellikle istemedikce push yapma.
## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `68d447861` wires bounded raw/compressed non-DB `RestoreDomains|RestoreMessages` staging into the authenticated restore executor. Legacy `BackupManager::StartRestore` and `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:74-135`, `BackupExecuter.cpp:230-388`) are the behavior references. The focused executor/runtime suite is `13 passed, 0 failed, 0 skipped`; full Net10 is `1907 passed, 0 failed, 16 skipped`.

The implementation is fenced to disposable empty domain stores. It stages raw sibling or compressed `DataBackup`, validates containment/revalidation, compensates metadata inserts, rolls back the old data directory on metadata failure, and preserves a rollback artifact if recovery fails. Raw restores invoked through a bound `LoadBackup` fail closed because `BackupArchiveBinding` currently snapshots only the 7z file; the next slice must snapshot/hash the external sibling. Crash-safe shared SQL/filesystem transactionality, normal-installation domain/public-folder deletion ordering, isolated service/COM queued restore, real COM/DCOM, SEC-18, installer, AD/DC, migration, and 24-hour lifecycle gates remain open. No COM identity, production service/SQL/Data directory, registration, DCOM ACL, IIS, SMTP trust, or live reconfiguration changed.

Do not treat older `Current Next Slice` paragraphs below this entry as current; they are historical records.
## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: code/test commit `124acfc0c` binds raw external `DataBackup` content with the loaded archive snapshot. Legacy anchors are `BackupManager::LoadBackup` (`hmailserver/source/Server/Common/Application/BackupManager.cpp:101-135`) and `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:339-388`), which resolve `DataFiles/@FolderName` beside the original archive. The .NET binding copies the sibling into the private snapshot directory, verifies source stability and a deterministic tree hash, and revalidates the bound tree before restore. Focused archive/raw coverage is `5 passed, 0 failed, 0 skipped`; full Net10 is `1908 passed, 0 failed, 16 skipped`.

The next gate is isolated service/COM queued restore against disposable SQL/Data. Do not claim production parity: private temp ACL and path-based TOCTOU remain, SQL/filesystem restore is not crash-safe, normal-installation domain/public-folder deletion ordering and application reinitialization remain open, and native COM/DCOM, SEC-18, installer, AD/DC, migration, and 24-hour lifecycle gates are still RED. No production service, SQL/Data directory, COM registration, DCOM ACL, IIS, SMTP trust, or live reconfiguration changed.

Older `Current Next Slice` paragraphs below this entry are historical.
## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: test-only code/test commit `1b479dfac` adds real disposable LocalDB + filesystem acceptance for the bound raw non-DB restore executor. The test creates a unique database, restores domains/accounts/aliases/distribution lists/recipients, verifies the raw DataBackup replacement, and drops both SQL/temp resources in `finally`. Focused coverage is `2 passed, 0 failed, 0 skipped`; default full Net10 remains `1908 passed, 0 failed, 16 skipped`.

The SQL-enabled full run was `1918 passed, 5 failed, 2 skipped`; the five failures are existing `SqlServerMessageIndexingIntegrationTests` fixture/schema/ACL/cache failures and are unrelated to restore. The queued out-of-process service/COM acceptance is environment-blocked because no approved isolated composition exists; do not change installed COM registration, DCOM ACLs, production service, SQL, Data directory, or IIS. Next independent slice: shared SQL/filesystem transaction or durable restore journal, then legacy delete/reinitialize ordering.

Older continuation paragraphs below this entry are historical.

## Current Authoritative Continuation

Authoritative 2026-08-08 continuation: test-only code/test commit `e93d0021e` adds disposable LocalDB rollback acceptance for the bound raw non-DB restore executor. `RestoreExecutor_RollsBackSqlAndDataOnMetadataFailure` injects an alias-store insert failure, verifies the original Data directory is restored, verifies the staged file is absent, and verifies account/domain rows are compensated. The LocalDB fixture now contains the SQL tables referenced by the real account/domain delete stores. Focused restore coverage is `3 passed, 0 failed, 0 skipped`; default full Net10 is `1908 passed, 0 failed, 18 skipped`; SQL-enabled full Net10 is `1919 passed, 5 failed, 2 skipped` with five unrelated message-indexing fixture failures.

Parity anchors inspected: legacy `BackupExecuter::StartRestore`/`RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`), `BackupManager::OnBackupFailed` (`BackupManager.cpp:177`), `Collection<T,P>::XMLLoad`/`DeleteAll` (`Collection.h:85`), and persistent domain/account deletion. Current anchors are `MetadataBackupRestoreExecutor.RestoreMetadataAsync`/`RollbackAsync`, `BackupRestoreTransactionBoundary.ExecuteAsync`, `BackupRestoreDataDirectoryRuntime.RestoreAsync`, and the SQL domain/account delete stores. Legacy restore is non-compensating; .NET compensation is bounded to the disposable executor path.

No production behavior or machine state changed. Remaining risks are incomplete list/recipient rollback coverage, non-atomic SQL/filesystem recovery, normal-installation restore ordering/reinitialization, and the environment-blocked service/COM queued acceptance. Next slice: shared SQL transaction or durable restore journal with recovery evidence.
## Current Continuation (2026-08-08, NON-DB RESTORE AUTHORIZATION LEASE)

Code/test commit `efd873fea` closes the remaining authorization admission gap for non-DB restore. `MetadataBackupRestoreExecutor.ExecuteNonDbDataRestoreAsync` acquires the existing per-Application lease immediately before `BackupRestoreDataDirectoryRuntime.RestoreAsync`; it therefore covers filesystem staging, directory swap, rollback, and the metadata commit that follows. The legacy anchors are `BackupExecuter::StartRestore` and `BackupExecuter::RestoreDataDirectory_` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`). Legacy has no worker-time reauthentication check; this lease is an explicit fail-closed hardening and does not change installed COM identity or access boundaries.

Focused `BackupRestoreExecutionTests` pass `11/11`; default full Net10 passes `1919` with `26` skipped. The code commit is ready for its separate documentation commit and normal push. Do not stage dirty `AGENTS.md`, `artifacts/sec18-staging/`, or `artifacts/benchmarks/`. Next independent slice: inspect legacy `BackupTask`/`WorkQueue` shutdown semantics and implement deterministic cancellation/drain with idempotent cleanup for queued restores.
## Authoritative Continuation (2026-08-08, PUBLIC RESTORE DELETION MANIFEST)

Code/test commit `4cc66396a` is the current bounded slice. First, `hmail_parity_explorer` confirmed the legacy full restore sequence in `BackupExecuter::StartRestore`: delete domains, delete public folders, replace the Data directory, load domain XML and children, load settings/public-folder XML, then asynchronously reinitialize. `PersistentIMAPFolder::DeleteObject` and `PersistentMessage::DeleteObject` define recursive public-folder cleanup and the Delivered-recipient exception. The current .NET executor still rejects settings/full restore and does not invoke the public cleanup capability.

The slice adds `IBackupRestoreMetadataTransaction.DeleteAllPublicFoldersForRestoreWithManifestAsync` and the SQL Server implementation. It captures public deleted-message file metadata before the existing dependent-row deletion, while preserving caller-owned transaction boundaries and the old no-return method. Focused static coverage is `11 passed`; the three related SQL integration tests are skipped because no approved disposable SQL connection/database-create opt-in is configured. Full Net10 is `1939 passed, 0 failed, 31 skipped`. The commit does not change COM identity, authenticated boundaries, SMTP trust, service, filesystem mutation, production SQL/Data, or reinitialization.

Review gate: bounded capability accepted as YELLOW, production release remains RED. Next independent action is approved populated-store SQL/Data acceptance covering both manifest and no-return cleanup, commit and rollback, public/private scope, dependent rows, and Inbox preservation. Do not wire full settings/public-folder restore until archive metadata/message/ACL representation and staged file cleanup are complete.

## Authoritative Continuation (2026-08-10, LIVE C++/.NET 10 PERFORMANCE GATE)

The isolated paired-run attempt created two new SQL Server databases, two separate Data directories, and identical 1,000-message corpora on loopback ports SMTP `25250`, POP3 `25110`, and IMAP `25143`. The exact evidence is `artifacts/benchmarks/live-cpp-net10-20260810_152708/live-comparison-attempt-20260810.json` and its Markdown companion. Existing hMailServer service state was `Stopped`/`Disabled`; `HmailDb_Test5700`, the existing Data directory, installed COM registration, and production ports were not used.

No paired protocol measurement is valid. The copied legacy C++ process crashed with `0xC0000409` in `ucrtbase.dll` for LocalDB/default provider, LocalDB named-pipe/`MSOLEDBSQL`, MSSQLSERVER/`sqloledb`, and MSSQLSERVER/`MSOLEDBSQL` configurations. The .NET 10 process was blocked before listener creation because LocalDB reports `IsFullTextInstalled = 0`. No speed-up or regression ratio may be claimed. Full Net10 is `1987 passed, 33 skipped, 0 failed`; the benchmark-focused suite passes and current offline SEARCH/SORT remains diagnostic only.

The performance release gate remains **RED**. Next independent environment action: use a separate disposable SQL Server instance with Full-Text Search and a legacy-supported ADO provider, or a dedicated staging VM, then rerun the paired SMTP/IMAP/POP3/concurrency harness. Do not use `HmailDb_Test5700`, the existing Data directory, production service, installed COM registration, DCOM ACLs, or production ports.

## Current Authoritative Continuation

Authoritative 2026-08-10 continuation: test-only code/test commit `877f72160` repairs the isolated restore schema fixture in `BackupRestoreRoundTripIntegrationTests.CreateTargetSchemaAsync`. The observed `Invalid column name 'faid'` came from `SqlServerDomainAdministrationStore.DeleteAllDomainsForRestoreSql` selecting the legacy `hm_fetchaccounts.faid` identity; the fixture also now provides the empty `hm_imapfolders`, `hm_acl`, `hm_group_members`, and `hm_fetchaccounts_uids` tables/columns used by the same cleanup batch. Legacy anchors are `BackupExecuter::StartRestore`/`RestoreDataDirectory_`, `Collection<T,P>::XMLLoad/DeleteAll`, and `CreateTablesMSSQL.sql` (`hmailserver/source/Server/Common/Application/BackupExecuter.cpp:230-388`, `hmailserver/source/Server/Common/BO/Collection.h:85-135,202-215`, `hmailserver/source/DBScripts/CreateTablesMSSQL.sql:355-370,433-469,627-648`).

The isolated LocalDB restore class passes `11/11`; default full Net10 passes `1987`, skips `33`, and fails `0`. No production code, COM identity, SQL/Data directory, service, IIS, DCOM, or machine state changed. The fixture repair does not close populated restore/rollback, crash/power-loss, service/COM, SEC-18, migration/installer, live C++/.NET 10 performance, or 24-hour soak gates. Release remains RED. Next bounded action: use the repaired fixture for the smallest additional populated restore graph readback/rollback test that remains disposable and isolated; keep performance pairing environment-blocked until SQL Full-Text Search and legacy ADO startup are available.

## Current Authoritative Continuation (2026-08-10, FETCHACCOUNT RESTORE)

Code/test commit `7e8d71c15` implements the smallest restore-side FetchAccount slice after parity review. Legacy references: `Account::XMLStore` (`hmailserver/source/Server/Common/BO/Account.cpp:280-331`), `FetchAccount::XMLStore` (`FetchAccount.cpp:55-79`), `FetchAccountUID::XMLStore` (`FetchAccountUID.cpp:42-49`), and owner-scoped refresh (`FetchAccounts.cpp:36-43`, `FetchAccountUIDs.cpp:29-50`). Current references: `BackupArchiveXmlSnapshotParser.ParseFetchAccount`, `BackupRestoreMetadataWriter.RestoreFetchAccountsAsync`, `MetadataBackupRestoreExecutor.RestoreMetadataAsync`, `IBackupRestoreMetadataTransaction.FetchAccountStore`, and `SqlServerFetchAccountAdministrationStore`.

The parser preserves encrypted legacy Blowfish ciphertext and nested UID values. SQL restore now provides a transaction-scoped FetchAccount store, so generated FetchAccount and UID rows commit or roll back with the DB-only metadata transaction. Focused parser/SQL/restore tests pass `30/30`; isolated LocalDB FetchAccount readback and transaction rollback pass `2/2`; default full Net10 passes `1990/35/0`. SQL-enabled full Net10 passes `2017/2` with `6` unrelated existing message/indexing fixture failures. `git diff --check` passes.

No installed COM IID/CLSID/ProgID/DISPID/vtable/type-library identity, authenticated Administrator boundary, SMTP trust, production SQL/Data, service, IIS, DCOM, or machine state changed. Do not stage dirty `AGENTS.md`, `WindowsBackupRestoreDataDirectoryMutation.cs`, `BackupRestoreDataDirectoryRuntimeTests.cs`, or untracked SEC-18/benchmark/disposable artifacts. Release remains RED. Next slice: executor-level populated FetchAccount restore/readback plus injected UID/date rollback on disposable SQL, then reassess broader restore graph; performance pairing remains blocked by FTS/legacy ADO staging.

## Current Authoritative Continuation (2026-08-10, FETCHACCOUNT EXECUTOR ACCEPTANCE)

Test commit `17ba6e70a` closes the executor-test gap for the FetchAccount restore slice. `BackupRestoreRoundTripIntegrationTests` now uses the populated FetchAccount/UID archive through `MetadataBackupRestoreExecutor`, asserts generated-ID/readback, and injects an invalid UID date to prove transaction rollback of both parent and child rows. Focused LocalDB restore coverage is `12/12`; default full Net10 is `1990 passed, 36 skipped, 0 failed`; `git diff --check` passes.

No production code, installed COM identity, authenticated access boundary, SMTP trust, production SQL/Data, service, IIS, DCOM, or machine state changed. Keep dirty `AGENTS.md`, DataDirectory test files, and all untracked staging/benchmark/disposable artifacts out of commits. Release remains RED. Next slice: parity-review and implement the smallest Rules child restore/readback failure-rollback slice, while paired protocol performance remains blocked on disposable FTS and legacy ADO staging.

## Current Authoritative Continuation (2026-08-10, PAIRED LIVE PERFORMANCE GATE)

Code/test commit `29beaf8c8` adds the isolated live protocol runner and paired
report generator. Separate MSSQLSERVER databases and Data roots contain the
same 1,000-message corpus (`1000/1000` Data SHA-256 equality; `1000` messages,
metadata rows, and recipients per database). The same loopback ports were used:
SMTP `2525`, IMAP `1143`, and POP3 `25110`.

The .NET 10 listener-only run passed `25/25` SMTP, `25/25` IMAP, and `25/25`
POP3. The copied legacy C++ `/Debug` probe passed SMTP `25/25`, IMAP `4/25`,
and POP3 `0/25`; therefore the paired matrix is incomplete and all ratios are
invalid. The normal .NET host opens all three listeners but fails the installed
Application AppID COM identity check (`0x80004015`), so the helper intentionally
omitted COM registration. No installed COM identity, DCOM ACL, production
service, SQL/Data directory, or production port was changed.

Artifacts: `artifacts/benchmarks/live-cpp-net10-20260810_152708/`.
Focused live smoke passed for .NET 10; full Net10 is `1990 passed, 36 skipped,
0 failed`. The performance gate remains **RED**. Open evidence includes a
reproducible C++ binary with POP3/IMAP parity, SMTP message acceptance,
delivery-queue throughput, 1,000-concurrent IMAP, and 24-hour leak soak.
Older performance continuation entries below this one are historical.

## Current Authoritative Continuation (2026-08-10, 1,000-CONCURRENT IMAP ACCEPTANCE)

Code/test commit `21cc042c9` adds the bounded live concurrent IMAP benchmark
and report validator. The isolated C++ and .NET 10 targets use the same SQL
fixture shape, byte-identical 1,000-message Data corpus, account, root INBOX,
and loopback ports SMTP `2525`, IMAP `1143`, and POP3 `25110`. SQL readback
confirmed `1000` messages, metadata rows, and message-recipient rows in each
database and `folderparentid = -1` for the root INBOX.

.NET 10 passed `1000/1000` authenticated
`LOGIN/SELECT/SEARCH/SORT/LOGOUT` sessions with p50 `48.706 ms`, p95
`183.157 ms`, and p99 `558.690 ms`. The copied legacy `/Debug` process passed
`0/1000`; IMAP banner/read aborted and POP3 did not open. The validator
correctly retains C++ `FAIL` and forbids a ratio. Performance release status
remains **RED**.

Focused results: .NET and C++ concurrent reports validated; PowerShell parse
passed; full Net10 is `1990 passed, 36 skipped, 0 failed`; `git diff --check`
passes. Raw reports are under
`artifacts/benchmarks/live-cpp-net10-20260810_152708/{net10,cpp}-concurrent-imap/`.

Next independent slices, in priority order: (1) obtain a reproducible legacy
C++ runtime exposing all three listeners, (2) execute disposable populated
restore graph readback/rollback, and (3) once both baselines run, add paired
SMTP message-acceptance and delivery-queue workloads. Do not stage dirty
`AGENTS.md`, backup test WIP files, or untracked SEC-18/benchmark/disposable
artifacts. Do not push in this run.

## Current Authoritative Continuation (2026-08-10, RULES RESTORE)

Code/test commit `4f43db7b2` completes one bounded restore slice for legacy
Rules, RuleCriterias, and RuleActions. The parity anchors are
`Rule::XMLStore/XMLLoadSubItems`, `Account::XMLStore/XMLLoadSubItems`,
`PersistentRule::SaveObject`, `PersistentRuleCriteria::SaveObject`, and
`PersistentRuleAction::SaveObject` in `hmailserver/source/Server/Common`.
The parent is inserted first, generated IDs are propagated to children, and
SQL transaction disposal rolls back the whole graph. Non-transaction rollback
removes the owner-scoped rule and its dependent children.

Focused isolated SQL coverage is `13 passed, 0 failed, 0 skipped`, including
rule/criterion/action readback and injected action failure rollback. Default
full Net10 is `1991 passed, 37 skipped, 0 failed`. SQL opt-in full execution is
`2020 passed, 2 skipped`, with six unrelated existing message/indexing fixture
failures. `git diff --check` passes. No production service, SQL/Data,
installed COM identity, DCOM, IIS, SMTP, or machine state changed.

Residual release risk remains full settings/folders/messages restore,
crash/power-loss recovery, reproducible legacy C++ IMAP/POP3 startup, paired
SMTP/delivery performance, SEC-18, migration/installer, service/out-of-process
COM, AD/DC, and 24-hour soak. Release remains RED. Next three independent
slices: reproducible legacy C++ listener runtime; populated
folder/message/settings restore readback and rollback; paired SMTP acceptance
and delivery queue after both baselines are runnable. Do not push.
## Current Authoritative Continuation (2026-08-11, QUEUED FULL RESTORE)

Code/test commit `2564cc45b` adds the missing composed acceptance path for a
full restore. The test
`BackupRestoreRoundTripIntegrationTests.BackupManager_StartRestoreDispatchesRealFullRestoreIntoPopulatedTarget`
uses a disposable LocalDB database and isolated Data root, seeds an existing
domain/public-folder graph, invokes `BackupManager.StartRestore`, and waits for
the real `BackupTaskQueue`/`BackupTaskHostedService` completion before checking
settings, domain, folder, message, and Data-file replacement. The legacy
references are `BackupManager::StartRestore` and
`BackupExecuter::StartRestore`/`RestoreDataDirectory_` in
`source/Server/Common/Application`.

Focused restore integration is `18/18`; disposable SQL opt-in categories are
`53/53`; default full Net10 is `2125 passed, 44 skipped, 0 failed`.
No production SQL/Data, service, COM registration, DCOM ACL, IIS, or installed
Application identity changed. This closes queued full-restore execution
coverage only. Real `StartBackup -> LoadBackup`, crash/power-loss recovery,
service/COM lifecycle, independent SQL Server certification, SEC-18, and the
paired C++/.NET10 performance gate remain RED.

Next three independent slices: (1) full-restore crash/ambiguous-commit journal
evidence, (2) repair the isolated C++ protocol target and reproduce Net10
IMAP/POP3, and (3) add paired SMTP acceptance and delivery-queue workloads
after both protocol baselines are runnable. Older continuation entries below
this one are historical.
## Current Authoritative Continuation (2026-08-11, LISTENER-ONLY BENCHMARK COM ISOLATION)

Code/tool commit `f754c86c3` adds an explicit
`HMAILSERVER_COM_LOCAL_SERVER_ENABLED=false` setting to `Host.Build()` and the
listener benchmark scripts. `AddProductionHostedServices` keeps the COM local
server enabled by default and omits only that hosted service when the setting
is explicitly false. This preserves the installed Application IID/CLSID/ProgID,
AppID, type library, registration, and DCOM permissions.

The disposable listener probe now opens SMTP `220`, IMAP `* OK`, and POP3
`+OK` on loopback. Focused registration/composition coverage is `5/5`; default
full Net10 is `2126 passed, 44 skipped, 0 failed`. The live gate remains RED:
the available SQL instance has no Full-Text Search for `SEARCH TEXT needle`,
and the copied C++ target still does not provide POP3 readiness. Therefore no
speed-up ratio or winner is valid.

Next independent work is isolated full-restore crash/ambiguous-commit evidence,
then approved SQL Server FTS provisioning and a normal legacy C++ listener
target, followed by paired SMTP acceptance/delivery/load scenarios. No
production service, database, Data directory, COM registration, DCOM ACL, IIS,
or firewall state changed.

## Current Authoritative Continuation (2026-08-12, NORMAL-MX CNAME)

Code/test commit `bf6018662` completes one bounded normal-domain no-MX CNAME
parity slice. `hmail_parity_explorer` inspected legacy
`DNSResolver::GetEmailServersRecursive_` in
`hmailserver/source/Server/Common/TCPIP/DNSResolver.cpp:208-260`, plus
`BackupExecuter::StartRestore`, `RestoreDataDirectory_`,
`BackupManager::StartRestore`, `BackupTask::DoWork`, and
`Reinitializator::ReInitialize`; the restore journal/crash-evidence slice is
already implemented and was not restarted. Legacy DNS behavior is MX first,
single-CNAME recursion only when MX is empty, then implicit A/AAAA for the
original name.

The code adds `IDnsCnameResolver.cs`, `DnsCnameRecord.cs`, raw CNAME query and
parser support in `SystemDnsMxResolver.cs`, and bounded CNAME target selection
in `RemoteSmtpEndpointResolver.cs`. Focused tests in
`RemoteSmtpEndpointResolverTests.cs` and `SystemDnsMxResolverTests.cs` pass
`42/42`; full Net10 passes `2193/54/0` (passed/skipped/failed), and
`git diff --check` passes. Coverage includes one target, zero/multiple target
fallback, lookup failure fallback, implicit target address/SNI preservation,
parser TTL/target readback, and cycle fail-closed behavior.

The security/reality review is YELLOW for this bounded code slice and RED for
release. The shared outbound egress/SSRF policy, DNS response source/question
validation, and aggregate DNS deadline remain open findings. Live CNAME to
MX/A/AAAA/socket/TLS acceptance, reproducible C++ listeners, paired
performance, restore lifecycle reinitialization, migration/installer,
SEC-18, AD/DC, and 24-hour soak also remain open. Do not claim a C++/.NET
speed ratio or production readiness.

Next independent slices: (1) approved disposable real DNS/socket/TLS CNAME
acceptance, (2) shared outbound egress/SSRF hardening, (3) registry-isolated
or separate-VM C++ listener/benchmark execution, and (4) a separately
designed restore protocol drain/reinitialize lifecycle contract. Preserve
dirty `AGENTS.md`, existing backup/Smtp WIP, and all untracked
SEC-18/benchmark/disposable artifacts.

## Current Authoritative Continuation (2026-08-12, SMTP SELF-CONNECT GUARD)

Code/test commit `9e1bbb53b` implements one bounded legacy SMTP self-connect
slice. `hmail_parity_explorer` verified
`TCPConnection::StartAsyncConnect_` and `LocalIPAddresses::IsLocalPort` in
`hmailserver/source/Server/Common/TCPIP/TCPConnection.cpp:75` and
`hmailserver/source/Server/Common/LocalIPAddresses.cpp:101`: an active local
listening address/port is rejected, but an unused loopback port is allowed.
The implementation is in
`hmailserver/source/Server.Net10/src/HMailServer.Delivery/RemoteSmtpLocalEndpointPolicy.cs`,
with candidate marking in `RemoteSmtpEndpointResolver.cs` and the pre-connect
check in `SmtpRemoteDeliveryClient.cs`; `RemoteSmtpEndpoint.cs` carries the
optional guard flag. Focused tests are in
`RemoteSmtpLocalEndpointPolicyTests.cs` and
`SmtpRemoteDeliveryClientTests.cs`.

Focused coverage is `65/65`; full Net10 is `2202 passed, 54 skipped, 0 failed`.
The slice preserves installed COM identity, authenticated Admin boundaries,
SMTP trust behavior, explicit routes, and global relayers. Wildcard listeners
match only actual local addresses, mapped IPv6 is normalized, and guard denial
is converted into a transient endpoint result so MX failover can continue.
This is not the broader SMTP SSRF policy: private/link-local/mixed-answer
restrictions, DNS source/question validation, aggregate DNS deadlines, live
DNS/socket/TLS acceptance, and paired C++/.NET performance remain open.
Release remains RED.

Next three independent slices: (1) approved disposable real DNS/socket/TLS
acceptance, (2) separately reviewed shared SMTP egress/SSRF policy, and (3)
registry-isolated or separate-VM C++ listener/benchmark execution. Preserve
dirty `AGENTS.md`, backup/Smtp WIP, and untracked SEC-18/benchmark/disposable
artifacts.

## Current Authoritative Continuation (2026-08-12, FIXED-ROUTE HOST PLANNING)

Code/test commit `622d6296c` implements the resolver half of fixed-route SMTP
parity. `hmail_parity_explorer` verified
`ExternalDelivery::ResolveRecipientServers_` in
`hmailserver/source/Server/SMTP/ExternalDelivery.cpp:195-330` and
`TCPConnection::StartAsyncConnect_` in
`hmailserver/source/Server/Common/TCPIP/TCPConnection.cpp:130-160`.
`RemoteSmtpEndpointResolver.ResolveConfiguredRouteAsync` now splits `|` host
syntax, resolves addresses in order, deduplicates, caps after flattening when
the target carries a cap, preserves hostname TLS/SNI identity, and marks
resolved addresses for the existing local guard. Tests are in
`RemoteSmtpEndpointResolverTests.cs`.

Focused coverage is `73/73`; full Net10 is `2210 passed, 54 skipped, 0 failed`.
No COM/IDL/SQL schema/SMTP trust/live reconfiguration changed. SQL target
construction still does not propagate `MaxNumberOfMXHosts` for matched/forced
routes; global-relayer partial DNS fallback, hMailServer listener ownership,
DNS source/question validation, live DNS/socket/TLS, broad SMTP SSRF, and
paired C++/.NET performance remain open. Release is RED.

Next three slices: (1) SQL route MX-cap propagation, (2) global-relayer
partial-DNS fallback, and (3) approved disposable DNS/socket/TLS acceptance or
registry-isolated C++ listener/benchmark execution. Preserve dirty `AGENTS.md`,
backup/Smtp WIP, and untracked SEC-18/benchmark/disposable artifacts.

## Current Authoritative Continuation (2026-08-13, LISTENER OWNERSHIP)

Code/test commit `fb09dba17` implements the bounded listener-ownership slice.
Legacy `LocalIPAddresses::LoadIPAddresses` / `IsLocalPort`
(`hmailserver/source/Server/Common/TCPIP/LocalIPAddresses.cpp:28-133`)
builds self-connect checks from configured hMailServer `TCPIPPorts`, while the
old Net10 production DI path used every active machine TCP listener. `Host`
now gives the production `RemoteSmtpLocalEndpointPolicy` the enabled Net10
IMAP, SMTP, and POP3 listener endpoints only.

Focused results: Host composition `4/4`, listener policy `8/8`, remote
transport `20/20`, and new Host listener test `1/1`. The post-commit full suite
was `2213 passed, 54 skipped, 2 failed`; both failures were existing
ClamWin/CustomScanner cleanup failures caused by antivirus-held `.eml` files.

Residual scope: one endpoint per protocol in Host, no multiple
`hm_tcpipports` rows, no live refresh, no runtime bind-ownership evidence,
and no paired C++/.NET performance evidence. Security/reality status remains
RED. Preserve dirty `AGENTS.md`, backup/Smtp WIP, and untracked SEC-18,
benchmark, and disposable artifacts.

Next three slices: (1) multiple configured TCPIPPorts endpoint planning
without live reconfiguration, (2) approved disposable DNS/socket/TLS
acceptance, and (3) registry-isolated C++ execution with paired benchmark
evidence.

## Historical continuation (2026-08-12, GLOBAL-RELAYER PARTIAL DNS)

Code/test commit `85ab61f04` implements one bounded legacy parity slice.
`hmail_parity_explorer` verified
`ExternalDelivery::ResolveRecipientServers_`
(`hmailserver/source/Server/SMTP/ExternalDelivery.cpp:204-280`): each
pipe-separated global-relayer member is resolved independently, successful
addresses are retained, and failure occurs only when the result is empty.
`RemoteSmtpEndpointResolver.ResolveGlobalRelayerAsync` now follows that rule.
Focused resolver tests are `47 passed, 0 failed, 0 skipped`; full Net10 is
`2214 passed, 54 skipped, 0 failed`.

Security review found no new authorization, SSRF-scope, local-endpoint-guard,
TLS/SNI, ordering, COM, or SQL regression in this bounded change. Reality
review remains RED because real DNS/socket/TLS evidence, DNS validation,
hMailServer-owned listener discovery, broad SMTP egress/SSRF policy, and paired
C++/.NET performance evidence are still absent.

Next three slices: (1) exact listener ownership for self-connect parity, (2)
approved disposable DNS/socket/TLS acceptance, and (3) registry-isolated C++
execution with paired benchmark evidence. Preserve dirty `AGENTS.md`,
backup/Smtp WIP, and untracked SEC-18/benchmark/disposable artifacts.

## Historical continuation (2026-08-12, ROUTE MX CAP)

Code/test commit `c519f6e87` completes one bounded SQL-to-target parity gap.
`hmail_parity_explorer` verified legacy
`ExternalDelivery::ResolveRecipientServers_` (`hmailserver/source/Server/SMTP/
ExternalDelivery.cpp:195-280`) applies `MaxNumberOfMXHosts` after fixed-route
address expansion, including forced routes. Net10 now loads the existing
`hm_settings` value for matched and forced route targets in
`SqlServerDeliveryTargetResolver.cs`; `RemoteSmtpEndpointResolver` consumes
the value after host/address flattening. Tests are in
`SqlServerDeliveryTargetResolverTests.cs`.

Focused coverage is `51/51`; full Net10 is `2212 passed, 54 skipped, 0 failed`.
No schema, COM identity, route fields, SMTP trust, or live reconfiguration
changed. Global-relayer partial DNS fallback, hMailServer-owned listener
discovery, DNS validation, live DNS/socket/TLS, broad SMTP SSRF, and paired
C++/.NET performance remain open. Release is RED.

Next three slices: (1) global-relayer partial DNS fallback, (2) exact listener
ownership for self-connect parity, and (3) approved disposable DNS/socket/TLS
acceptance or registry-isolated C++ execution. Preserve dirty `AGENTS.md`,
backup/Smtp WIP, and untracked SEC-18/benchmark/disposable artifacts.

## Current Authoritative Continuation (2026-08-12, EXPLICIT RELAY SELF-CONNECT)

Code/test commit `b66f00e95` implements the next bounded parity slice after
the ordinary-MX guard. `hmail_parity_explorer` inspected
`ExternalDelivery::ResolveRecipientServers_` and
`TCPConnection::StartAsyncConnect_` -> `LocalIPAddresses::IsLocalPort` in
`hmailserver/source/Server/SMTP/ExternalDelivery.cpp:195-330` and
`hmailserver/source/Server/Common/TCPIP/TCPConnection.cpp:130-160`.
Net10 changes `RemoteSmtpEndpointResolver.cs` so literal route targets and
already-resolved global-relayer candidates carry `ConnectionAddress` plus the
existing local guard. Tests are in `RemoteSmtpEndpointResolverTests.cs`,
`RemoteSmtpLocalEndpointPolicyTests.cs`, and
`SmtpRemoteDeliveryClientTests.cs`.

Focused coverage is `70/70`; full Net10 is `2207 passed, 54 skipped, 0 failed`.
Private/link-local targets remain compatible, hostname routes are not silently
re-resolved in this slice, and no COM/IDL/SQL/trust/reconfiguration behavior
changed. Security/reality review remains RED for production acceptance:
hostname-route self-connect, hMailServer-owned listener discovery, partial
relayer DNS semantics, DNS source/question validation, broad SMTP SSRF policy,
live DNS/socket/TLS, and paired C++/.NET performance are open.

Next three slices: (1) hostname-route resolution/failover with exact listener
ownership, (2) approved disposable DNS/socket/TLS acceptance, and (3)
registry-isolated or separate-VM C++ listener/benchmark execution. Preserve
dirty `AGENTS.md`, backup/Smtp WIP, and untracked SEC-18/benchmark/disposable
artifacts.
