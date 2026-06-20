# hMailServer .NET 10 Rewrite

This folder contains the side-by-side .NET 10 implementation track. The legacy C++/ATL server remains the production implementation until this tree reaches protocol, data, and COM compatibility.

## Prerequisites

- .NET 10 SDK.
- .NET 10 WindowsDesktop runtime for the COM compatibility assembly and Windows service target.
- Visual Studio 2022 or Build Tools 17.x when building Windows service/COM artifacts from Visual Studio.
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

The shared SQL account authenticator used by IMAP and SMTP AUTH runs optional `OnClientValidatePassword(HMAILSERVER_ACCOUNT, password)` before built-in password verification; legacy `Result.Value = 0` accepts, `1` rejects, and any other value continues normal verification. The account facade exposes common scalar legacy fields including ID, address, active/AD flags, AD domain/user, domain ID, max size, person name fields, admin level, vacation, forwarding, signature, and last-logon values.

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

The scripting logger provider dispatches optional `OnError(iSeverity, iError, sSource, sDescription)` handlers for .NET `Warning`, `Error`, and `Critical` records as legacy severity values `3`, `2`, and `1`. The logging `EventId` becomes the error code, the logger category becomes the source, exception details are appended to the formatted description, execution is timeboxed/fail-open, and recursive logging from the handler is suppressed. All legacy protocol and delivery event names are now connected; backup-completion/failure events await the .NET backup engine.

When `HMAILSERVER_CLAMAV_ENABLED=true`, the service registers the async/timeboxed ClamAV `INSTREAM` scanner and runs it on SMTP messages after `OnAcceptMessage`, global rules, spam policy, and optional attachment blocking have had a chance to mutate the message but before the queue row and data-directory file are written. ClamAV protocol errors or timeouts fail closed with a transient SMTP rejection, while infected messages are rejected with a permanent virus response. External POP3 fetch also uses the same scanner for accounts with `UseAntiVirus` enabled; infected remote UIDs are retained/deleted according to the fetch account retention decision without queueing the message again.

When `HMAILSERVER_SPAMASSASSIN_ENABLED=true`, the service registers the async/timeboxed SpamAssassin `PROCESS SPAMC/1.2` client and runs it on messages before antivirus scanning and queue persistence. Valid spamd responses replace the message with SpamAssassin's processed message bytes, including `X-Spam-Status` headers; invalid headers, negative/missing `Content-length`, partial bodies, socket errors, and timeouts preserve the original message and continue delivery. External POP3 fetch passes each account's `UseAntiSpam` setting into the SMTP receiver path so fetched messages can opt in or out of the same scanner. Optional spam policy settings can add legacy `X-hMailServer-Spam`, `X-hMailServer-Reason-*`, and subject-prefix mutations after a successful spam scan and before antivirus/queue persistence. `HMAILSERVER_SPAM_POLICY_MARK_THRESHOLD` marks queue rows with the legacy spam flag (`eMFSpam = 128`) when the scan score reaches the configured threshold, even if header mutation is disabled. `HMAILSERVER_SPAM_POLICY_DELETE_THRESHOLD` rejects matching SMTP messages with `554` before antivirus scanning and queue persistence.

When `HMAILSERVER_ATTACHMENT_BLOCKING_ENABLED=true`, the SMTP receiver applies a MIME-aware attachment policy after spam processing and before antivirus/queue persistence. Matching wildcards from `HMAILSERVER_ATTACHMENT_BLOCKING_WILDCARDS` are case-insensitive; entries such as `.exe` or `exe` normalize to `*.exe`. Matching attachments are replaced in-place with a plain-text attachment named `<original>.txt`, and `%MACRO_FILE%` in `HMAILSERVER_ATTACHMENT_BLOCKING_REPLACEMENT_TEXT` expands to the original file name. Messages are preserved unchanged when MIME parsing fails or no wildcard matches.

When `HMAILSERVER_DNSBL_ENABLED=true`, the SMTP receiver checks the connecting client IP against `HMAILSERVER_DNSBL_ZONES` before scripts, rules, spam scanning, antivirus scanning, and queue persistence. IPv4 addresses use the standard reversed-octet query form, IPv6 addresses use reversed nibbles, and authenticated SMTP clients are skipped by default through `HMAILSERVER_DNSBL_SKIP_AUTHENTICATED=true`. A positive DNS response rejects the message with `HMAILSERVER_DNSBL_REJECTION_MESSAGE`; DNS lookup failures, NXDOMAIN responses, and the bounded timeout fail open so mail receiving is not made dependent on a blocklist outage.

When `HMAILSERVER_REVERSE_DNS_ENABLED=true`, the SMTP receiver performs a bounded PTR check before scripts, rules, spam scanning, antivirus scanning, and queue persistence. Authenticated SMTP clients are skipped by default. When `HMAILSERVER_REVERSE_DNS_REQUIRE_FORWARD_CONFIRMED=true`, at least one PTR hostname must resolve back to the connecting IP address; missing PTR records or forward-confirmation failures reject with `HMAILSERVER_REVERSE_DNS_REJECTION_MESSAGE`, while transient DNS errors and timeouts fail open.

When `HMAILSERVER_SENDER_DOMAIN_MX_ENABLED=true`, the SMTP receiver checks the envelope sender domain for MX records before scripts, rules, spam scanning, antivirus scanning, and queue persistence. Null reverse-path bounces, IP/domain literals, malformed sender values, and authenticated SMTP clients are skipped by default. Missing MX records reject with `HMAILSERVER_SENDER_DOMAIN_MX_REJECTION_MESSAGE`; transient DNS errors, timeouts, SERVFAIL/REFUSED responses, and missing local DNS resolver configuration fail open so mail receiving is not made dependent on DNS availability.

When `HMAILSERVER_GREYLISTING_ENABLED=true`, the SMTP receiver checks each `client IP + envelope sender + recipient` triplet against the legacy `hm_greylisting_triplets` table before scripts, rules, spam scanning, antivirus scanning, and queue persistence. New or still-delayed triplets return `HMAILSERVER_GREYLISTING_FAILURE_RESPONSE` (`451` by default); triplets whose block window has elapsed are accepted and have their passed lifetime extended. Authenticated SMTP clients are skipped by default, `hm_greylisting_whiteaddresses` wildcard entries are honored, and SQL errors fail open to avoid turning greylisting storage issues into mail loss.

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
```

The POP3 command engine supports `USER`/`PASS` through the shared account authenticator, `CAPA`, and then `STAT`, `LIST`, `UIDL`, `RETR`, `TOP`, `DELE`, `RSET`, `NOOP`, and `QUIT` over an `IPop3MailboxStore` boundary. Successful authentication acquires a mailbox lock so one POP3 session owns an account mailbox at a time, and releases it when the session ends. The SQL Server mailbox store opens the legacy root `Inbox` for the authenticated account, lists `hm_messages` rows in `messageuid` order, exposes `messageuid` as the POP3 UIDL value, streams message files from the hMailServer data directory, and deletes DB/search/metadata rows plus message files when authenticated `QUIT` commits pending `DELE` commands. `RETR` dot-stuffs while streaming instead of requiring the full message body as a byte array, and `TOP` streams only headers plus the requested body line count. Failed `PASS` attempts use the shared SQL auto-ban recorder and close the session when the configured threshold is reached. With scripting enabled, POP3 runs optional `OnClientLogon(HMAILSERVER_CLIENT)` after successful and failed authentication attempts and exposes endpoint/session/TLS metadata. When a POP3 TLS PFX certificate is configured, accepted sockets are upgraded immediately with `SslStream` for implicit TLS; set `HMAILSERVER_POP3_PORT=995` for the conventional TLS listener port. External POP3 fetch now has a SQL Server lease/UID store for legacy `hm_fetchaccounts` and `hm_fetchaccounts_uids`, resets stale `falocked` account rows once when the hosted worker starts, exposes a Windows script boundary for legacy `OnExternalAccountDownload(HMAILSERVER_FETCHACCOUNT, HMAILSERVER_MESSAGE/Nothing, uid)` with fetch-account fields including `NextDownloadTime` and `IsLocked`, maps `Result.Value` delete-retention decisions, and uses a hosted processor that connects to POP3 accounts, supports plain/implicit TLS/STLS modes, probes CAPA before STLS so optional STARTTLS falls back to plaintext only when STLS is not advertised and required STARTTLS fails before `USER`/`PASS` when STLS is missing, fails both STARTTLS modes before credentials when an advertised STLS command is rejected, uses UIDL/RETR/DELE/QUIT, dot-unstuffs message bytes, preserves valid `Received`/`Date` timestamps, resolves MIME recipient headers and `Received ... for <recipient>` values through the SMTP recipient validator, applies the legacy `EnableRouteRecipients` local/route-recipient filter, runs ClamAV scanning for accounts with `UseAntiVirus`, carries `UseAntiSpam` into the SpamAssassin-enabled SMTP receiver path, treats permanent SMTP receiver rejections as non-accepted messages that still apply UID/remote-delete retention, queues accepted messages through the SMTP receiver path, tracks known UIDs, tolerates duplicate persisted known-UID rows, skips duplicate sequence numbers plus duplicate new and already-known UIDL values within the same POP3 listing, and applies remote delete decisions. Additional external-fetch edge-case parity remains on the backlog.

A rejected external-fetch `CAPA` response is treated like an unavailable STLS capability: optional STARTTLS continues over plaintext, while required STARTTLS fails before `USER`/`PASS`.

A rejected external POP3 greeting fails the connection before any client command or credentials are sent in plain and STARTTLS modes.

A rejected external-fetch `USER` command fails authentication before `PASS` is sent in both plain and optional-STARTTLS plaintext fallback paths.

A rejected external-fetch `PASS` command fails authentication before message listing begins, with no `UIDL` or later command sent in plain and optional-STARTTLS plaintext fallback paths.

A rejected external-fetch `UIDL` command sends the legacy `QUIT` cleanup without issuing `RETR`, `DELE`, or other message-processing commands in plain and optional-STARTTLS plaintext fallback paths.

A truncated external-fetch `UIDL` listing after a `+OK` response remains fatal, releases the fetch-account lease as failed, and does not issue `RETR`/`DELE`, submit message data, or mutate UID state.

An empty external-fetch `UIDL` listing completes without `RETR`/`DELE` commands and removes stale known UID rows that are no longer present on the remote server.

Malformed external-fetch `UIDL` listing rows are skipped while valid rows in the same listing are preserved for later `RETR`/retention processing.

A rejected external-fetch `RETR` command sends only the legacy `QUIT` cleanup, releases the failed account lease, and does not submit message data or mutate UID/remote-deletion state in plain and optional-STARTTLS plaintext fallback paths.

A truncated external-fetch `RETR` body after a `+OK` response remains fatal, releases the fetch-account lease as failed, and does not submit message data, add UID state, or issue remote delete cleanup.

External-fetch `DELE` is legacy best-effort: any server response, including `-ERR`, advances UID cleanup and allows the session to `QUIT`; socket, I/O, and cancellation failures remain fatal. A `DELE` transport failure before any server response preserves known UID state and releases the fetch-account lease as failed.

External-fetch `QUIT` cleanup is best-effort during session disposal: rejected `QUIT` responses and disconnects before the `QUIT` response do not leak disposal failures after the message-processing decision has already been made.

With scripting enabled, the POP3 listener runs `OnClientConnect(HMAILSERVER_CLIENT)` before its greeting or implicit TLS setup and closes the connection when legacy `Result.Value = 1`; this complements the existing post-authentication `OnClientLogon` hook.

## Project Layout

- `HMailServer.Service`: Windows service host named `hMailServer`.
- `HMailServer.Core`: shared abstractions for search, delivery queue, message identity, failed-logon auto-ban recording, and external fetch account leasing.
- `HMailServer.Delivery`: delivery queue processor orchestration over lease/load/target-dispatch boundaries, remote delivery MX resolution, and optional sender-domain MX checks.
- `HMailServer.Protocols`: `System.IO.Pipelines` line protocol reader, bounded `Channel` work queue primitives, shared `OnClientConnect` listener event handling, shared IMAP sequence-set parsing, IMAP TCP/session/SEARCH/SORT/FETCH/IDLE/ACL/QUOTA parser/executor/command handler plumbing, the SMTP TCP/session skeleton, POP3 TCP/session command engine with implicit TLS stream support, and failed-logon auto-ban disconnect hooks.
- `HMailServer.Indexing`: SQL Server Full-Text Search backfill processor.
- `HMailServer.Storage.SqlServer`: SQL Server connection, Full-Text Search readiness, message search/sort indexing, IMAP sequence snapshots, IMAP message fetch storage, POP3 Inbox mailbox storage, external fetch account/UID leasing, failed-logon auto-ban recording, atomic delivery leasing, optional greylisting checks, optional delivery queue status persistence, retention cleanup, and event-kind metrics snapshots.
- `HMailServer.Search.SqlServer`: IMAP SEARCH and SORT query planners for SQL Server predicates, metadata ordering, and Full-Text Search.
- `HMailServer.Security`: modern spam/virus protocol helpers, including the async/timeboxed ClamAV INSTREAM client, message antivirus scanner adapter, async/timeboxed SpamAssassin client, message spam scanner adapter, SpamAssassin response validation, MIME-aware attachment replacement policy, optional DNS blocklist checker, optional reverse DNS/PTR checker, and optional URL/SURBL checker.
- `HMailServer.ComInterop`: additive COM compatibility contracts for new .NET-only capabilities.
- `tests/HMailServer.Net10.Tests`: MSTest coverage for protocol framing, literal reads, SpamAssassin response/client behavior, ClamAV, SpamAssassin, attachment policy, DNSBL, reverse DNS/PTR, sender-domain MX, greylisting, and SURBL pipeline wiring, SQL search/sort planning, failed-logon auto-ban SQL shape and protocol disconnect wiring, external fetch account/UID SQL shape, SMTP session/listener skeleton flow, POP3 session command flow, IMAP LOGIN/AUTHENTICATE/LIST/STATUS/nested SELECT/SEARCH/SORT/FETCH/STORE/COPY/MOVE/APPEND/EXPUNGE/IDLE/ACL/QUOTA parsing, TCP listener flow, and SEARCH/SORT/FETCH/IDLE/ACL/QUOTA, including ENVELOPE/BODYSTRUCTURE, plus STORE/COPY/MOVE/APPEND/EXPUNGE response execution.

## Database

Apply `hmailserver/source/DBScripts/Upgrade5708to6000MSSQL.sql` on a backed-up MS SQL hMailServer database. It adds delivery lease columns, rule delivery metadata columns, search queue/documents tables, `hm_delivery_queue_status`, and the SQL Server Full-Text Search catalog/index used by fast mode. Set `HMAILSERVER_DELIVERY_STATUS_SQL_ENABLED=true` after that migration to persist delivery worker transition events to SQL Server.

The migration is additive: existing `hm_messages`, `hm_message_metadata`, and the data directory remain the source of truth during the transition.
