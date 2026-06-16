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
   - Remaining: Auto-ban, script events, Active Directory auth, and master user.

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
   - Remaining: remaining rule actions (`Reply`, `SendUsingRoute`, `BindToAddress`) and scripting.
   - Delivery queue worker remaining: delivery status observability and richer bounce templates.

4. POP3 and external fetch.
   - POP3 listener/session, UIDL/LIST/RETR/DELE parity.
   - External POP3 fetch accounts, UID tracking, antivirus/spam pipeline integration.

5. Security and anti-abuse modernization.
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

Implement remaining non-script SMTP rule actions, starting with `SendUsingRoute` and `BindToAddress` delivery metadata.
