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
- Done: SMTP `OnClientConnect(HMAILSERVER_CLIENT)` event hook runs before the greeting and can close the connection on legacy `Result.Value = 1`.
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
- Done: modern TLS option factory and spam/virus protocol helpers.

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
   - Done: Windows-only process-isolated VBScript/JScript host for SMTP rule functions with file-backed scalar message facade (`FileName`, `DropMessage`, `RejectReason`, IDs/state placeholders, size, delivery attempt, charset, body type checks, common envelope fields, body fields, header value access, message header collection, and `Save`), envelope recipient collection facade, and attachment collection facade.
   - Done: `OnClientConnect` protocol event hook before SMTP greeting with connection-close handling for legacy `Result.Value = 1`.
   - Done: `OnHELO` protocol event hook before HELO/EHLO success responses with `HMAILSERVER_CLIENT` and legacy `Result.Value`/`Result.Message` rejection handling.
   - Done: `OnClientLogon` protocol event hook after successful and failed SMTP AUTH attempts with attempted username and authenticated state.
   - Done: `OnRecipientUnknown` protocol event hook for unknown RCPT validation failures.
   - Done: `OnSMTPData` protocol event hook after DATA is read and before receiver/queue processing with message mutation/drop/reject handling.
   - Done: `OnAcceptMessage` protocol event hook before global rule processing with `HMAILSERVER_CLIENT`, `HMAILSERVER_MESSAGE`, and legacy `Result.Value`/`Result.Message` rejection handling.
   - Done: `OnTooManyInvalidCommands` protocol event hook plus disconnect-invalid-clients/maximum-incorrect-commands session policy.
   - Done: delivery queue `OnDeliveryStart` and `OnDeliverMessage` script hooks before target resolution with queue-file mutation persistence and legacy `Result.Value = 1` drop handling.
   - Done: delivery queue `OnDeliveryFailed` script hook for final failed recipients with legacy recipient/error arguments.
   - Remaining: full legacy script object model and remaining protocol/delivery event scripting hooks.
   - Delivery queue worker remaining: delivery status observability and richer bounce templates.

4. POP3 and external fetch.
   - Done: POP3 session command engine with shared account authentication, session-held deletes committed on `QUIT`, `RSET` undo, `LIST`/`UIDL` visibility checks, and streaming `RETR` dot-stuffing.
   - Done: SQL Server/data-directory POP3 mailbox store for the authenticated account's root `Inbox`, using legacy `messageuid` as UIDL and removing search queue/document plus metadata artifacts on delete.
   - Done: POP3 TCP listener and service configuration via `HMAILSERVER_POP3_ENABLED`, bind address, port, backlog, and max connection settings.
   - Done: `CAPA` advertises current POP3 capabilities and `TOP` returns headers plus requested body lines without loading the full message.
   - Done: process-local mailbox lock parity for one POP3 session per account mailbox.
   - Remaining: TLS listener wiring.
   - Remaining: External POP3 fetch accounts, UID tracking, antivirus/spam pipeline integration.

5. Security and anti-abuse modernization.
   - Done: SQL failed-logon auto-ban recorder preserves legacy settings/table compatibility, deny-range creation semantics, and IMAP/SMTP/POP3 threshold-triggered disconnect wiring.
   - Async/timeboxed ClamAV and SpamAssassin clients.
   - SPF, DKIM, DMARC, DNSBL, SURBL, PTR/MX checks, greylisting, attachment blocking.
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

Continue POP3 toward TLS listener wiring, external fetch, and remaining authentication/script-object parity.
