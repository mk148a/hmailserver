# hMailServer .NET 10 Remaining Work

This backlog tracks the remaining production-parity work for the side-by-side .NET 10 rewrite. Keep it current as each slice lands.

## Current Status

- Done: .NET 10 solution skeleton, local build/test wrappers, prerequisite checks.
- Done: legacy C++ phase-0 fixes for ClamAV INSTREAM framing, synchronous timeout cancellation, SpamAssassin partial/invalid responses, and MSBuild 17 discovery.
- Done: SQL Server Full-Text Search migration, search document queue, backfill processor, MIME text extraction, IMAP SEARCH planner/parser/executor, IMAP session loop, SQL-backed LOGIN/SELECT slice, legacy password verification, and bounded TCP listener.
- Done: IMAP nested private folder path resolution, public folder root mapping, and ACL-aware public folder SELECT/EXAMINE read/write selection.
- Done: IMAP FETCH/UID FETCH metadata and RFC822/body literal slice backed by SQL Server and the existing data directory.
- Done: IMAP LIST/LSUB folder discovery and STATUS counters backed by SQL Server folders/messages and public-folder ACL lookup/read checks.
- Done: IMAP STORE/UID STORE flag mutation and EXPUNGE cleanup of deleted messages, search docs, metadata, and message files.
- Done: IMAP COPY/UID COPY and MOVE/UID MOVE with destination UID allocation, message file copy, search reindex queueing, and source cleanup for MOVE.
- Done: IMAP APPEND synchronizing literal handling, destination UID allocation, message file write, and search indexing queue.
- Done: IMAP FETCH ENVELOPE and BODYSTRUCTURE MIME response formatting with raw message reads only for MIME-dependent FETCH items.
- Done: IMAP SORT/UID SORT parser, executor, session dispatch, SQL Server metadata ordering, and FTS candidate filtering.
- Done: IMAP SEARCH/SORT sent-date criteria (`SENTSINCE`, `SENTBEFORE`, `SENTON`) stay SQL-backed through message metadata date filters.
- Done: IMAP SEARCH/SORT sequence-set criteria stay SQL-backed through mailbox `ROW_NUMBER()` predicates.
- Done: IMAP IDLE/DONE session flow, capability advertising, idle event formatting, and mailbox status polling notifier.
- Done: IMAP ACL commands (`GETACL`, `SETACL`, `DELETEACL`, `LISTRIGHTS`, `MYRIGHTS`) backed by the existing public-folder `hm_acl` model.
- Done: IMAP QUOTA commands (`GETQUOTA`, `GETQUOTAROOT`, `SETQUOTA`) backed by live mailbox usage and hMailServer account/domain limits.
- Done: Shared IMAP sequence-set parser used by SEARCH, FETCH, STORE, COPY, and MOVE.
- Done: IMAP recent flag lifecycle: SELECT captures and clears DB `\Recent`, EXAMINE preserves it, SEARCH/SORT use session recent UID snapshots, and APPEND/COPY/MOVE update selected-mailbox snapshots.
- Done: IMAP SASL PLAIN with initial response and continuation flow, plus TLS-required authentication policy hooks.
- Done: IMAP `OnClientLogon(HMAILSERVER_CLIENT)` event hook runs after successful and failed authentication attempts and exposes endpoint/session/TLS metadata.
- Done: shared IMAP/SMTP `OnClientValidatePassword(HMAILSERVER_ACCOUNT, password)` event hook runs before built-in password verification with legacy accept/reject/continue decisions and expanded scalar account facade fields.
- Done: SMTP TCP listener/session skeleton with bounded connection handling and `EHLO`/`HELO`/`NOOP`/`RSET`/`QUIT` responses.
- Done: SMTP `MAIL`/`RCPT`/`DATA` receive staging with dot-terminator handling, dot-stuffing, size limits, and `ISmtpMessageReceiver` storage boundary.
- Done: SQL-backed SMTP receive store persists inbound DATA to the hMailServer data directory, inserts locked `hm_messages` queue rows, writes `hm_messagerecipients`, and unlocks atomically.
- Done: SMTP recipient/domain validation for active local domains, domain aliases, plus-addressing, active accounts, aliases, distribution lists, postmaster catch-all, and local account id resolution before queueing.
- Done: SMTP `AUTH PLAIN`/`AUTH LOGIN` using the SQL-backed account authenticator, plus authenticated relay allowance for non-local recipients.
- Done: SMTP route-aware recipient validation for `hm_routes`/`hm_routeaddresses`, including wildcard route domains and route delivery metadata on resolved recipients.
- Done: atomic delivery queue lease store skeleton.
- Done: delivery queue message loading, local/route/remote target classification, and dispatcher-based delivery processor handoff.
- Done: SMTP local mailbox delivery writer copies leased queue messages into account Inbox folders, allocates UIDs, inserts delivered `hm_messages`, and queues search indexing.
- Done: SMTP remote delivery sender for route and remote-domain batches with EHLO/HELO, optional/required STARTTLS hooks, route AUTH LOGIN, dot-stuffed DATA streaming, and dispatcher integration.
- Done: SMTP remote DNS/MX resolution using system DNS servers, MX preference ordering, TTL/negative caching, domain fallback, and per-domain/route concurrency limiting.
- Done: SMTP delivery retry/backoff classification, recipient-level queue cleanup after successful batches, permanent-failure bounce submission, and retry-limit bounce handling.
- Done: SMTP server-side STARTTLS capability, stream upgrade, session reset after TLS negotiation, TLS-required AUTH policy, and PFX certificate loading.
- Done: SMTP global rule queue-acceptance hook backed by legacy `hm_rules` / criteria / actions tables, with criteria matching plus `Delete`, `SetHeaderValue`, and `StopRuleProcessing`.
- Done: SMTP account-level local-delivery rule hook applies `Delete`, `SetHeaderValue`, and `StopRuleProcessing` to each per-account message copy before DB insert/index queueing.
- Done: SMTP `MoveToIMAPFolder` rule action for local delivery resolves destinations through the IMAP mailbox store, moves the per-account/public-folder file when needed, and allocates UID in the rule-selected folder.
- Done: SMTP `Forward` and `CreateCopy` rule actions submit generated messages through the atomic queue writer, increment `X-hMailServer-LoopCount`, honor the rule loop limit, and preserve generated-message recipients.
- Done: SMTP `SendUsingRoute` and `BindToAddress` global rule actions persist delivery metadata, force remote recipients through the selected route, and bind remote SMTP sockets to the rule-selected local IP address.
- Done: SMTP `Reply` rule action generates auto-replied queue messages, skips auto-submitted sources, increments rule loop count, and works for global and account-level rules.
- Done: SMTP `ScriptFunction` rule action plumbing now calls a pluggable executor boundary that can mutate message bytes, drop a message, or reject processing.
- Done: Windows-only process-isolated SMTP rule script executor runs `EventHandlers.vbs`/`.js` through `cscript.exe`, timeboxes execution, round-trips message file mutations, exposes file-backed scalar, recipient, and attachment facades, and fails closed when the script runner does not return status.
- Done: SMTP/IMAP/POP3 `OnClientConnect(HMAILSERVER_CLIENT)` event hooks run before protocol greeting or implicit TLS setup and can close the connection on legacy `Result.Value = 1`.
- Done: SMTP `OnHELO(HMAILSERVER_CLIENT)` event hook runs before HELO/EHLO success responses and maps legacy `Result.Value`/`Result.Message` rejection responses.
- Done: SMTP `OnClientLogon(HMAILSERVER_CLIENT)` event hook runs after AUTH attempts and exposes attempted username plus authenticated state.
- Done: SMTP `OnRecipientUnknown(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` event hook runs for unknown RCPT targets.
- Done: SMTP `OnSMTPData(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` event hook runs after DATA is read and before receiver/queue processing, can mutate/drop/reject messages, and maps legacy rejection responses.
- Done: SMTP `OnAcceptMessage(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` event hook runs before global rule processing, can mutate/drop/reject messages, exposes basic client context, and maps legacy `Result.Value`/`Result.Message` rejection responses.
- Done: SMTP `OnTooManyInvalidCommands(HMAILSERVER_CLIENT, HMAILSERVER_MESSAGE)` event hook runs when the configured invalid-command disconnect threshold is exceeded.
- Done: SMTP delivery queue `OnDeliveryStart(HMAILSERVER_MESSAGE)`, `OnDeliverMessage(HMAILSERVER_MESSAGE)`, and `OnDeliveryFailed(HMAILSERVER_MESSAGE, recipient, error)` event hooks run with queue-file mutation persistence where applicable.
- Done: POP3 session command engine for `USER`/`PASS`, `STAT`, `LIST`, `UIDL`, `RETR`, `DELE`, `RSET`, `NOOP`, and `QUIT` over a streaming mailbox-store boundary.
- Done: SQL Server POP3 mailbox store opens the legacy account Inbox, lists by `messageuid`, streams message files from the data directory, and deletes message/search/metadata rows plus files on committed `DELE`.
- Done: POP3 disabled-by-default TCP listener and Windows service wiring with bounded concurrent connections and plain stream factory boundary.
- Done: POP3 `CAPA` and `TOP` command parity; `TOP` streams headers plus requested body lines with dot-stuffing.
- Done: POP3 process-local mailbox lock manager prevents concurrent sessions from opening the same account mailbox and releases locks on session end.
- Done: SQL auto-ban recorder mirrors legacy failed-logon settings, records `hm_logon_failures`, clears expired failures, creates deny `hm_securityranges` rows when the threshold is reached, and is wired into IMAP/SMTP/POP3 failed authentication paths.
- Done: POP3 implicit TLS listener wiring uses `SslStream` and configured PFX certificates before the session greeting.
- Done: POP3 `OnClientLogon(HMAILSERVER_CLIENT)` event hook runs after successful and failed authentication attempts and exposes endpoint/session/TLS metadata.
- Done: SQL Server external fetch account store leases due legacy `hm_fetchaccounts`, defers inactive account/domain rows, decrypts legacy Blowfish passwords, and tracks `hm_fetchaccounts_uids`.
- Done: external fetch MIME/Received recipient resolution uses the SMTP recipient validator and applies legacy local/route recipient filtering.
- Done: modern TLS option factory and spam/virus protocol helpers.
- Done: ClamAV antivirus pipeline wiring for SMTP queue acceptance and external POP3 fetch account `UseAntiVirus` scans.
- Done: async/timeboxed SpamAssassin client and SMTP receiver pipeline wiring, preserving original messages on invalid/partial spamd responses and honoring external fetch account `UseAntiSpam`.
- Done: optional spam policy pipeline adds legacy `X-hMailServer-Spam`, `X-hMailServer-Reason-*`, and subject-prefix mutations after successful spam scans.
- Done: spam policy mark threshold sets the legacy `eMFSpam` queue flag (`128`) while preserving the default `\Recent` flag.
- Done: spam policy delete threshold rejects matching SMTP messages with `554` before antivirus scanning and queue persistence.
- Done: optional MIME-aware attachment blocking replaces matching SMTP attachments with legacy-style text attachments before antivirus scanning and queue persistence.
- Done: optional DNSBL checks reject listed SMTP client IPs before scripts/rules/spam/AV/queue while failing open on DNS errors and skipping authenticated clients by default.
- Done: optional reverse DNS/PTR checks reject missing or non-forward-confirmed client hostnames before scripts/rules/spam/AV/queue while failing open on transient DNS errors.
- Done: optional sender-domain MX checks reject unauthenticated envelope sender domains without MX records before scripts/rules/spam/AV/queue while failing open on transient DNS errors.
- Done: optional SQL-backed greylisting checks legacy `hm_greylisting_triplets` before scripts/rules/spam/AV/queue, honors white-address wildcard entries, and fails open on SQL errors.
- Done: optional URL/SURBL checks extract bounded MIME text/html URL hosts, honor `EnableSpamScan`, reject listed hosts before antivirus/queue persistence, and fail open on DNS errors.
- Done: external POP3 fetch treats permanent SMTP receiver rejections as non-accepted messages with normal UID/remote-delete retention instead of failing the whole account batch.
- Done: external POP3 fetch hosted worker resets stale `falocked` fetch-account rows on startup, matching legacy `PersistentFetchAccount::UnlockAll()` recovery.
- Done: script message facade exposes legacy `Flag(eMessageFlag)` bitmask access over `State` for VBScript/JScript handlers.
- Done: JScript message facade exposes the legacy `Filename` alias alongside the existing `FileName` path.
- Done: JScript attachment facade exposes the legacy `Filename` alias alongside `FileName`.
- Done: global `EventLog.Write(value)` script facade appends legacy-shaped Unicode event-log rows for VBScript/JScript rule, error, and password-validation handlers.
- Done: scripted `HMAILSERVER_MESSAGE.RefreshContent` reloads file-backed headers and body after direct script-side message file rewrites.
- Done: script message `FileName`/`Filename` facade keeps `Load`, `Save`, and `Copy` tied to the original backing file path.
- Done: script message `To`/`CC` direct assignment no longer rewrites saved recipient headers, preserving legacy read-only property shape.
- Done: script attachment `FileName`/`Filename` and `Size` direct assignment cannot mutate attachment collection metadata, preserving the legacy read-only property shape.
- Done: script message `ID`, `UID`, `State`, `DeliveryAttempt`, and `InternalDate` preserve legacy read-only queue metadata, including message IDs above the 32-bit range; `State` and `Flag(eMessageFlag)` use distinct legacy fields.
- Done: script message `Size` preserves the legacy read-only floor-KiB calculation and refreshes from the backing file after `Save`.
- Done: script recipient item `Address`, `OriginalAddress`, and `IsLocalUser` preserve legacy read-only metadata without blocking message-level `AddRecipient`/`ClearRecipients` mutations.
- Done: external POP3 fetch tolerates duplicate persisted `hm_fetchaccounts_uids.uidvalue` rows when building the known-UID lookup for a batch.
- Done: external POP3 fetch coalesces duplicate remote UIDL sequence-number entries so a malformed listing cannot download the same remote slot twice.
- Done: external POP3 fetch probes CAPA before STLS so optional STARTTLS falls back only when STLS is not advertised, required STARTTLS fails before credentials, and an advertised-but-rejected STLS fails both modes before authentication.
- Done: external POP3 fetch treats a rejected CAPA response as unavailable STLS, continuing optional STARTTLS over plaintext while failing required STARTTLS before credentials.
- Done: external POP3 fetch rejects a failed server greeting before sending any command or credentials in plain and STARTTLS modes.
- Done: external POP3 fetch fails a rejected USER command before sending PASS in plain and optional-STARTTLS plaintext fallback paths.
- Done: external POP3 fetch fails a rejected PASS command before sending UIDL or any later command in plain and optional-STARTTLS plaintext fallback paths.
- Done: external POP3 fetch handles rejected UIDL with legacy QUIT cleanup and no RETR, DELE, or other message-processing commands in plain and optional-STARTTLS plaintext fallback paths.
- Done: external POP3 fetch treats truncated UIDL listings as fatal, failed-releases the account, and avoids receiver, UID, RETR, or DELE side effects.
- Done: external POP3 fetch handles empty UIDL listings without RETR/DELE and removes stale known UID rows missing from the remote server.
- Done: external POP3 fetch skips malformed UIDL listing rows while preserving valid rows from the same response.
- Done: external POP3 fetch handles rejected RETR with legacy QUIT cleanup, failed account-lease release, and no receiver, UID, or remote-deletion side effects.
- Done: external POP3 fetch treats truncated RETR bodies as fatal, failed-releases the account, and avoids receiver, UID, or remote-delete side effects.
- Done: external POP3 fetch treats DELE server rejection as legacy best-effort, continuing UID cleanup and QUIT while preserving fatal socket, I/O, and cancellation failures.
- Done: external POP3 fetch preserves known UID state and failed-releases the account when DELE transport fails before any server response.
- Done: external POP3 fetch treats rejected QUIT responses and disconnects before QUIT response as best-effort cleanup during session disposal.

## Production Parity Backlog

1. IMAP authentication and mailbox selection.
   - Done: SQL account lookup against `hm_accounts` + active `hm_domains`.
   - Done: Legacy password verification for plaintext, Blowfish, MD5, and salted SHA256.
   - Done: LOGIN, SELECT, EXAMINE, authenticated/selected session states.
   - Done: Nested private folder selection with hierarchy delimiter.
   - Done: Public folder root mapping through `imappublicfoldername` / `#Public`.
   - Done: Public folder ACL inheritance for read and read/write selection.
   - Done: UIDVALIDITY, UIDNEXT, RECENT/UNSEEN counters.
   - Done: SASL PLAIN authentication with SASL-IR and TLS-required auth policy.
   - Done: `OnClientLogon(HMAILSERVER_CLIENT)` script hook after successful and failed IMAP authentication attempts.
   - Done: shared `OnClientValidatePassword(HMAILSERVER_ACCOUNT, password)` script hook with expanded scalar account facade.
   - Remaining: Deeper account facade collections/methods, Active Directory auth, and master user.

2. IMAP command parity beyond SEARCH.
   - Done: FETCH/UID FETCH for `FLAGS`, `UID`, `RFC822.SIZE`, `INTERNALDATE`, `ENVELOPE`, `BODYSTRUCTURE`, `BODY[]`, `BODY.PEEK[]`, and `RFC822`.
   - Done: LIST/LSUB folder discovery and STATUS `MESSAGES`, `RECENT`, `UIDNEXT`, `UIDVALIDITY`, `UNSEEN`.
   - Done: STORE/UID STORE for `FLAGS`, `+FLAGS`, `-FLAGS`, silent variants, and EXPUNGE for `\Deleted` messages.
   - Done: COPY/UID COPY and MOVE/UID MOVE for selected source mailboxes and writable destination folders.
   - Done: APPEND for synchronizing literals with optional flags and internal date.
   - Done: SORT/UID SORT using SQL-backed metadata ordering and FTS-filtered candidate sets.
   - Done: `SENTSINCE`, `SENTBEFORE`, and `SENTON` search criteria using metadata-backed sent-date predicates.
   - Done: message sequence-set search criteria using SQL mailbox sequence predicates.
   - Done: IDLE/DONE with EXISTS/RECENT/EXPUNGE/FETCH FLAGS event formatting and a SQL status polling notifier.
   - Done: ACL command set on top of public-folder ACL rights (`lrswipkxtea`) and SQL account/group/Anyone principals.
   - Done: QUOTA command set using `accountmaxsize`, domain per-account limits, and live `hm_messages.messagesize` usage.
   - Done: Sequence set parser shared across SEARCH/FETCH/STORE/COPY/MOVE.
   - Done: Correct recent flag lifecycle and notifications based on selected-mailbox recent UID snapshots.

3. SMTP production pipeline.
   - Done: disabled-by-default TCP listener, bounded connection handling, greeting, `EHLO`/`HELO`, `NOOP`, `RSET`, and `QUIT`.
   - Done: `MAIL`/`RCPT`/`DATA` transaction staging, declared/actual message-size checks, and protocol-to-storage receive boundary.
   - Done: durable SQL/data-directory receive store for delivery queue staging.
   - Done: active local domain/account/alias/distribution-list validation, domain aliases, plus-addressing, postmaster fallback, and `recipientlocalaccountid` population.
   - Done: `AUTH PLAIN`/`AUTH LOGIN` with initial/challenge responses and authenticated relay allowance for non-local recipients.
   - Done: route-aware recipient validation against `hm_routes`/`hm_routeaddresses` with route classification metadata.
   - Done: delivery queue lease/load/target-resolution processor skeleton for local account, configured route, and remote-domain batches.
   - Done: local mailbox writer for leased local-account batches with Inbox UID allocation and search indexing queue.
   - Done: remote SMTP sender for configured route and remote-domain batches, including route authentication and protocol-level DATA delivery.
   - Done: DNS/MX resolution cache and per-domain/route concurrency gate for remote delivery.
   - Done: retry/backoff classification with 4xx transient / 5xx permanent SMTP semantics, retry-limit bounce handling, and recipient-level queue cleanup to avoid duplicate delivery.
   - Done: server-side STARTTLS with `SslStream`, capability advertising, post-upgrade session reset, TLS-required AUTH policy, and service-level PFX certificate loading.
   - Done: global SMTP rules before queue acceptance for common criteria plus `Delete`, `SetHeaderValue`, and `StopRuleProcessing`.
   - Done: account-level/local-delivery rules for per-account message copies with `Delete`, `SetHeaderValue`, and `StopRuleProcessing`.
   - Done: `MoveToIMAPFolder` for local delivery using rule-selected existing private/public IMAP folders.
   - Done: `Forward` and `CreateCopy` queue submissions with generated-message loop-count protection.
   - Done: `SendUsingRoute` and `BindToAddress` delivery metadata for global rules, including forced route target resolution and local socket bind for remote SMTP.
   - Done: `Reply` generated response action with Auto-Submitted and rule loop protection.
   - Done: `ScriptFunction` executor boundary inside the rule processor.
   - Done: Windows-only process-isolated VBScript/JScript host for SMTP rule functions with file-backed scalar message facade (`FileName`/`Filename`, `DropMessage`, `RejectReason`, IDs/state placeholders, `Flag(eMessageFlag)`, size, delivery attempt, charset, body type checks, common envelope fields, body fields, header value access, message header collection, and `Save`), envelope recipient collection facade, attachment collection facade with `FileName`/`Filename`, size, save, delete, clear, and add support, plus global `EventLog.Write(value)` event-log output.
   - Done: `HMAILSERVER_MESSAGE.RefreshContent` reloads file-backed headers and body after scripts mutate the message file directly.
   - Done: message `FileName`/`Filename` script aliases keep backing file operations on the original runner path, including read-only VBScript `Filename` parity.
   - Done: message `To`/`CC` script properties preserve legacy read-only direct-assignment behavior while leaving `AddRecipient`, `ClearRecipients`, `Recipients`, and `HeaderValue` mutation paths intact.
   - Done: attachment `FileName`/`Filename` and `Size` script properties preserve legacy read-only metadata while leaving `Add`, `Clear`, `Delete`, and `SaveAs` behavior intact.
   - Done: message `ID`, `UID`, `State`, `DeliveryAttempt`, and `InternalDate` script properties preserve canonical read-only queue metadata in VBScript/JScript while retaining 64-bit message IDs; delivery events seed `State = 1` independently from queue-backed `Flag(eMessageFlag)` values.
   - Done: message `Size` script property uses legacy integer `bytes / 1024` semantics, remains read-only, and re-measures the backing file after `Save` in VBScript/JScript.
   - Done: recipient item `Address`, `OriginalAddress`, and `IsLocalUser` script properties remain read-only in VBScript/JScript while message-level recipient mutation paths stay intact.
   - Done: account-rule `HMAILSERVER_MESSAGE.Copy(folderId)` captures call-time message content, preserves repeated copy requests, validates same-account destination folders, allocates distinct UIDs, writes delivered message files/rows, and queues each copy for search indexing.
   - Done: shared `OnClientConnect` protocol event hook before SMTP/IMAP/POP3 greeting or implicit TLS setup with connection-close handling for legacy `Result.Value = 1`.
   - Done: `OnHELO` protocol event hook before HELO/EHLO success responses with `HMAILSERVER_CLIENT` and legacy `Result.Value`/`Result.Message` rejection handling.
   - Done: `OnClientLogon` protocol event hook after successful and failed SMTP AUTH attempts with attempted username and authenticated state.
   - Done: `HMAILSERVER_CLIENT` exposes legacy `Authenticated` and `EncryptedConnection` aliases alongside the existing authenticated/TLS state fields.
   - Done: `OnRecipientUnknown` protocol event hook for unknown RCPT validation failures.
   - Done: `OnSMTPData` protocol event hook after DATA is read and before receiver/queue processing with message mutation/drop/reject handling.
   - Done: `OnAcceptMessage` protocol event hook before global rule processing with `HMAILSERVER_CLIENT`, `HMAILSERVER_MESSAGE`, and legacy `Result.Value`/`Result.Message` rejection handling.
   - Done: `OnTooManyInvalidCommands` protocol event hook plus disconnect-invalid-clients/maximum-incorrect-commands session policy.
   - Done: `OnError(iSeverity, iError, sSource, sDescription)` script hook for .NET warning/error/critical logging with legacy severity mapping, `EventId` propagation, exception details, fail-open execution, and recursive-log suppression.
   - Done: delivery queue `OnDeliveryStart` and `OnDeliverMessage` script hooks before target resolution with queue-file mutation persistence and legacy `Result.Value = 1` drop handling.
   - Done: delivery queue `OnDeliveryFailed` script hook for final failed recipients with legacy recipient/error arguments.
   - Done: delivery queue status observer boundary with best-effort events for lease, load-missing, target success/defer/final failure, bounce, completion, release, and processing-failure transitions.
   - Done: optional SQL Server delivery status sink and additive `hm_delivery_queue_status` migration for durable queue transition history.
   - Done: configurable delivery bounce subject/body templates with queue metadata tokens, failed-recipient formatting, header sanitization, and bounded failure descriptions.
   - Done: delivery status retention cleanup worker with configurable retention window, interval, and batch size.
- Done: delivery status metrics query surface for event-kind counts over a requested time window.
- Done: delivery event script message metadata seeds real queue ID, UID, state, delivery attempt, and internal date for VBScript/JScript handlers.
- Done: delivery event failures emit a distinct `DeliveryEventFailed` queue status before deferral, preserving script/error text for SQL metrics and diagnostics.
- Done: delivery bounce templates expose richer queue/recipient tokens including message UID/account/folder/state, delivery attempt, failed recipient count/address list/first recipient, and rule route/bind metadata.
- Done: external POP3 fetch `OnExternalAccountDownload(HMAILSERVER_FETCHACCOUNT, HMAILSERVER_MESSAGE/Nothing, uid)` script hook boundary with legacy fetch-account fields including `NextDownloadTime`/`IsLocked`, nullable message argument handling, and `Result.Value`/`Result.Parameter` delete-retention mapping.
- Remaining: full legacy script object model plus `OnBackupCompleted`/`OnBackupFailed` once the .NET backup engine exists; legacy protocol and delivery event hooks are connected.

4. POP3 and external fetch.
   - Done: POP3 session command engine with shared account authentication, session-held deletes committed on `QUIT`, `RSET` undo, `LIST`/`UIDL` visibility checks, and streaming `RETR` dot-stuffing.
   - Done: SQL Server/data-directory POP3 mailbox store for the authenticated account's root `Inbox`, using legacy `messageuid` as UIDL and removing search queue/document plus metadata artifacts on delete.
   - Done: POP3 TCP listener and service configuration via `HMAILSERVER_POP3_ENABLED`, bind address, port, backlog, and max connection settings.
   - Done: `CAPA` advertises current POP3 capabilities and `TOP` returns headers plus requested body lines without loading the full message.
   - Done: process-local mailbox lock parity for one POP3 session per account mailbox.
   - Done: implicit TLS listener stream factory and service certificate configuration for POP3.
   - Done: `OnClientLogon(HMAILSERVER_CLIENT)` script hook after successful and failed POP3 authentication attempts.
   - Done: SQL Server external fetch account lease/UID store for existing `hm_fetchaccounts` and `hm_fetchaccounts_uids`.
   - Done: external fetch processor core that leases accounts, uses a bounded POP3 session abstraction, removes stale UID rows, runs `OnExternalAccountDownload` for new and already-known remote UIDs, queues accepted messages through the SMTP receiver path, and applies legacy remote delete retention decisions.
   - Done: external POP3 network session factory and hosted worker scheduling with plain, implicit TLS, and STLS modes, plus UIDL/RETR/DELE/QUIT loopback protocol coverage.
   - Done: external fetch received-time parity from valid `Received` header dates, falling back to `Date` and then current UTC when MIME dates are missing or outside legacy bounds.
   - Done: external fetch MIME recipient headers and `Received ... for <recipient>` values resolve through the SQL SMTP recipient validator, then preserve legacy local-account filtering unless route recipients are enabled.
   - Done: external fetch applies UID tracking and remote-delete retention for permanent SMTP receiver rejections such as spam delete-threshold `554` responses.
   - Done: external fetch hosted worker clears stale account locks once on startup before polling due accounts.
   - Done: external fetch skips duplicate new UIDL values within the same POP3 listing so a malformed server response cannot queue the same remote message twice.
   - Done: external fetch skips duplicate already-known UIDL values within the same POP3 listing so script/retention cleanup runs once per remote UID.
   - Done: external fetch tolerates duplicate persisted known-UID rows so corrupted or legacy-created duplicates do not fail the whole account batch.
   - Done: external fetch coalesces duplicate remote sequence numbers within the same UIDL listing before download/retention processing.
   - Done: external fetch probes CAPA before STLS, preserving legacy optional STARTTLS plaintext fallback, required STARTTLS pre-auth failure when STLS is not advertised, and pre-auth failure in both modes when an advertised STLS command is rejected.
   - Done: external fetch preserves legacy CAPA-rejection behavior by continuing optional STARTTLS over plaintext and failing required STARTTLS before authentication.
   - Done: external fetch preserves legacy greeting-rejection behavior without sending any client command or credentials in plain and STARTTLS modes.
   - Done: external fetch preserves legacy USER-rejection behavior without sending PASS in plain and optional-STARTTLS plaintext fallback paths.
   - Done: external fetch preserves legacy PASS-rejection behavior without sending UIDL or later commands in plain and optional-STARTTLS plaintext fallback paths.
   - Done: external fetch preserves legacy UIDL-rejection cleanup by sending QUIT without RETR, DELE, or other message-processing commands in plain and optional-STARTTLS plaintext fallback paths.
   - Done: external fetch keeps truncated UIDL multiline listings fatal without RETR, DELE, receiver, or UID side effects.
   - Done: external fetch completes empty UIDL listings, skips message commands, and deletes stale known UID rows no longer returned by the server.
   - Done: external fetch skips malformed UIDL rows and keeps valid rows in the same listing available for processing.
   - Done: external fetch preserves rejected-RETR cleanup by sending only QUIT, releasing the failed account lease, and avoiding receiver, UID, or remote-deletion side effects.
   - Done: external fetch keeps truncated RETR multiline bodies fatal without receiver, UID, or remote-delete side effects.
   - Done: external fetch preserves legacy best-effort DELE semantics by advancing UID cleanup after any server response while keeping transport and cancellation failures fatal.
   - Done: external fetch keeps DELE transport failures fatal before any server response, preserving known UID state and failed-releasing the account.
   - Done: external fetch keeps QUIT cleanup best-effort during session disposal when the server rejects QUIT or closes before a QUIT response.
   - Remaining: additional external fetch edge-case parity.

5. Security and anti-abuse modernization.
   - Done: SQL failed-logon auto-ban recorder preserves legacy settings/table compatibility, deny-range creation semantics, and IMAP/SMTP/POP3 threshold-triggered disconnect wiring.
   - Done: async/timeboxed ClamAV INSTREAM client with raw network-order chunk framing, bounded streaming, clean/infected/error result parsing, and fake-daemon protocol tests.
   - Done: ClamAV scanner adapter wired into SMTP receive processing and external POP3 fetch, including fail-closed scan errors, infected-message rejection/skipping, and account-level antivirus enablement.
   - Done: async/timeboxed SpamAssassin `PROCESS SPAMC/1.2` client with bounded response headers/body, original-message preservation on invalid/partial responses, score parsing, SMTP receive wiring, and external fetch `UseAntiSpam` propagation.
   - Done: optional hMailServer spam header/subject policy for `X-hMailServer-Spam`, `X-hMailServer-Reason-*`, score header replacement, and subject prefixing.
   - Done: hMailServer spam mark threshold maps scan scores to the legacy spam message flag before queue persistence.
   - Done: hMailServer spam delete/reject threshold maps scan scores to SMTP `554` rejection before antivirus/queue persistence.
   - Done: optional attachment blocking policy replaces matching MIME attachments with text notice attachments before antivirus/queue persistence.
   - Done: optional DNSBL checker with bounded DNS queries, fail-open lookup errors, IPv4/IPv6 query formatting, and SMTP receiver rejection wiring.
   - Done: optional reverse DNS/PTR checker with bounded lookup, authenticated-client bypass, forward-confirmed hostname mode, SMTP receiver rejection wiring, and fail-open transient DNS handling.
   - Done: optional sender-domain MX checker with bounded lookup, authenticated-client bypass, null reverse-path/domain-literal skip, SMTP receiver rejection wiring, and fail-open transient DNS handling.
   - Done: optional SQL-backed greylisting checker with authenticated-client bypass, legacy triplet/white-address table compatibility, SMTP receiver temporary rejection wiring, and fail-open SQL error handling.
   - Done: optional SURBL checker with bounded MIME URL host extraction, parent-domain candidate limits, fail-open lookup errors, and SMTP receiver rejection wiring.
   - Remaining: SPF, DKIM, DMARC.
   - Implicit TLS stream factories and listener ports; STARTTLS uses OS default TLS policy and online certificate revocation checks.

6. COM/API compatibility.
   - Preserve existing GUID/ProgID/DISPID/type library contracts.
   - Implement legacy Administrator-visible objects and collections.
   - Keep `IInterfaceMessageIndexing` and complete `IInterfaceMessageIndexing2`.

7. Migration, operations, and observability.
   - Full in-place upgrade runner with mandatory backup checks and rollback-from-backup documentation.
   - Search backfill progress metrics, health endpoints/logging, orphan cleanup.
   - Windows Service install/uninstall scripts and production configuration.

8. Performance and soak validation.
   - SQL Server FTS integration tests.
   - 100k message mailbox SEARCH/SORT acceptance.
   - 1k concurrent IMAP connections, SMTP latency, delivery queue throughput, memory/handle leak soak tests.

## Current Next Slice

Continue the remaining legacy script object parity beyond `Message.Copy`, design backup events with the .NET backup engine instead of emitting synthetic callbacks, keep delivery status observability/bounce template parity verified as the queue worker evolves, and close remaining external fetch edge cases as they surface.
