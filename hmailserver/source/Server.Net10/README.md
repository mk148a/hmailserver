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

The listener supports `LOGIN`, `AUTHENTICATE PLAIN` with SASL-IR or continuation response, nested-folder `LIST`/`LSUB`/`SELECT`/`EXAMINE`, public-folder ACL-aware discovery and selection, `STATUS`, `SEARCH`, `UID SEARCH`, `SORT`, `UID SORT`, `FETCH`, `UID FETCH`, `STORE`, `UID STORE`, `COPY`, `UID COPY`, `MOVE`, `UID MOVE`, `APPEND`, `EXPUNGE`, `IDLE`/`DONE`, ACL commands (`GETACL`, `SETACL`, `DELETEACL`, `LISTRIGHTS`, `MYRIGHTS`), and QUOTA commands (`GETQUOTA`, `GETQUOTAROOT`, `SETQUOTA`) against SQL Server-backed accounts and folders. SEARCH and SORT use SQL predicates plus SQL Server Full-Text Search candidate sets; SORT orders by existing message metadata without reading message files. Selected mailboxes keep an in-session `\Recent` UID snapshot: SELECT clears DB recent flags after capture, EXAMINE preserves them, and APPEND/COPY/MOVE update the active snapshot. IDLE can stream formatted `EXISTS`, `RECENT`, `EXPUNGE`, and `FETCH FLAGS` events, with the default notifier polling SQL mailbox status on the configured interval. ACL commands reuse the existing `hm_acl` public-folder model with account, group, and `Anyone` principals. QUOTA commands use live `hm_messages.messagesize` usage with `accountmaxsize` and domain per-account limits. FETCH metadata stays SQL-only unless RFC822/body literals, `ENVELOPE`, or `BODYSTRUCTURE` are requested; those MIME responses read from the existing hMailServer data directory only when needed. STORE updates message flags and the search document mirror; COPY/MOVE allocate destination UIDs, copy message files, and queue destination messages for search indexing; APPEND accepts synchronizing literals, writes new message files, allocates destination UIDs, and queues indexing; EXPUNGE deletes `\Deleted` messages plus search/metadata rows and removes message files after the DB transaction commits. Plaintext, legacy Blowfish, legacy MD5, and legacy salted SHA256 account passwords are supported. `HMAILSERVER_IMAP_REQUIRE_TLS_FOR_AUTH=true` suppresses `AUTH=PLAIN` on clear connections and rejects cleartext `LOGIN`/`AUTHENTICATE`; Active Directory, script hooks, auto-ban, master user, and the rest of the IMAP command set remain on the parity backlog.

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
$env:HMAILSERVER_SMTP_REQUIRE_TLS_FOR_AUTH = "false"
$env:HMAILSERVER_SMTP_TLS_CERTIFICATE_PATH = "C:\certs\mx.example.test.pfx"
$env:HMAILSERVER_SMTP_TLS_CERTIFICATE_PASSWORD = "changeit"
$env:HMAILSERVER_SCRIPTING_ENABLED = "false"
$env:HMAILSERVER_SCRIPTING_LANGUAGE = "VBScript"
$env:HMAILSERVER_SCRIPT_EVENT_DIRECTORY = "C:\Program Files (x86)\hMailServer\Events"
$env:HMAILSERVER_SCRIPT_TIMEOUT_MS = "5000"
```

The SMTP skeleton sends an ESMTP greeting, supports `EHLO`, `HELO`, `NOOP`, `RSET`, `QUIT`, server-side `STARTTLS`, `AUTH PLAIN`, `AUTH LOGIN`, and stages `MAIL`/`RCPT`/`DATA` transactions through `ISmtpMessageReceiver`. When a PFX certificate path is configured, `EHLO` advertises `STARTTLS`, the session upgrades the active stream through `SslStream`, clears pre-TLS session knowledge, and can require TLS before AUTH with `HMAILSERVER_SMTP_REQUIRE_TLS_FOR_AUTH=true`; without a certificate the listener preserves the existing plaintext-only behavior and returns `454 TLS not available` for explicit `STARTTLS`. The protocol layer handles declared and actual message-size checks, dot-terminated DATA reads, and dot-stuffing before handing the raw message bytes to storage.

Before queue persistence, the SQL Server receiver loads active global legacy rules from `hm_rules`, `hm_rule_criterias`, and `hm_rule_actions`; it evaluates basic criteria (`FROM`, `TO`, `CC`, `SUBJECT`, `BODY`, `MESSAGE SIZE`, `RECIPIENT LIST`, `DELIVERY ATTEMPTS`, or a named header) and applies `Delete`, `SetHeaderValue`, `StopRuleProcessing`, `Forward`, `CreateCopy`, `Reply`, `SendUsingRoute`, and `BindToAddress`. `ScriptFunction` actions flow through a Windows-only process-isolated `cscript.exe` executor when scripting is enabled; it loads `EventHandlers.vbs` or `EventHandlers.js`, calls the configured function, allows the script to mutate the message file, and exposes a file-backed `HMAILSERVER_MESSAGE` facade with `FileName`, `DropMessage`, `RejectReason`, `ID`, `UID`, `State`, `Size`, `DeliveryAttempt`, `InternalDate`, `EncodeFields`, `Charset`, `HasBodyType`, `Subject`, `From`, `To`, `CC`, `Date`, `Body`, `HTMLBody`, `HeaderValue`, `SetHeaderValue`, `Headers` (`Count`, `Item`, `ItemByName`, `Name`, `Value`, `Delete`), `Load`/`RefreshContent`, `Save`, `FromAddress`, `Recipients`, `AddRecipient`, `ClearRecipients`, and `Attachments` (`Count`, `Item`, `SaveAs`, `Delete`, `Clear`, `Add`). The full legacy COM message object model and protocol event hooks still remain on the parity backlog.

Generated rule messages are written through the same atomic queue writer, increment `X-hMailServer-LoopCount`, honor `HMAILSERVER_SMTP_RULE_LOOP_LIMIT`, preserve generated-message recipients even when the source message is deleted by rule, create `Auto-Submitted: auto-replied` messages for `Reply`, skip auto-submitted reply sources, and `CreateCopy` adds `X-CopyRule`. The default SQL Server receiver writes the resulting message file under the hMailServer data directory, inserts a locked `hm_messages` delivery-queue row (`messagetype = 1`), writes `hm_messagerecipients`, persists rule-forced route/bind delivery metadata, and unlocks the queue row in one transaction. `RCPT TO` validation resolves active local domains, domain aliases, plus-addressing, active accounts, aliases, public/authorized distribution lists, postmaster catch-all, and configured routes before queueing; resolved local accounts populate `recipientlocalaccountid`, while route recipients carry route id/target metadata for the delivery worker. Delivery queue groundwork now leases queued rows, reloads the leased message and recipients, classifies batches as local account, configured route, rule-forced route, or remote domain, and hands those batches to a dispatcher boundary. The local mailbox writer copies leased queue messages into account Inbox folders, applies account-level rules against that per-account copy for `Delete`, `SetHeaderValue`, `StopRuleProcessing`, `MoveToIMAPFolder`, `Forward`, `CreateCopy`, and `Reply`, resolves rule destinations through the IMAP mailbox store, allocates UIDs for messages that remain, inserts delivered `hm_messages`, and queues search indexing. The remote SMTP sender handles route and remote-domain batches with EHLO/HELO, optional/required STARTTLS hooks, route AUTH LOGIN, rule-level local bind addresses, dot-stuffed DATA delivery, system-DNS MX lookup with TTL/negative cache, domain fallback, and per-domain/route concurrency limiting. Delivery failure handling classifies 4xx as transient and 5xx as permanent, applies bounded retry/backoff, drops successful recipient batches from the queue to avoid duplicate delivery, and submits bounce messages for permanent failures or retry-limit exhaustion. Non-local recipients without a configured route are accepted only after successful SMTP AUTH until full route/relay policy lands. The full script object model, event hooks, and richer generated-message policy remain on the parity backlog.

## Project Layout

- `HMailServer.Service`: Windows service host named `hMailServer`.
- `HMailServer.Core`: shared abstractions for search, delivery queue, and message identity.
- `HMailServer.Delivery`: delivery queue processor orchestration over lease/load/target-dispatch boundaries.
- `HMailServer.Protocols`: `System.IO.Pipelines` line protocol reader, bounded `Channel` work queue primitives, shared IMAP sequence-set parsing, IMAP TCP/session/SEARCH/SORT/FETCH/IDLE/ACL/QUOTA parser/executor/command handler plumbing, and the SMTP TCP/session skeleton.
- `HMailServer.Indexing`: SQL Server Full-Text Search backfill processor.
- `HMailServer.Storage.SqlServer`: SQL Server connection, Full-Text Search readiness, message search/sort indexing, IMAP sequence snapshots, IMAP message fetch storage, and atomic delivery leasing.
- `HMailServer.Search.SqlServer`: IMAP SEARCH and SORT query planners for SQL Server predicates, metadata ordering, and Full-Text Search.
- `HMailServer.Security`: modern spam/virus protocol helpers.
- `HMailServer.ComInterop`: additive COM compatibility contracts for new .NET-only capabilities.
- `tests/HMailServer.Net10.Tests`: MSTest coverage for protocol framing, literal reads, SpamAssassin response validation, SQL search/sort planning, SMTP session/listener skeleton flow, IMAP LOGIN/AUTHENTICATE/LIST/STATUS/nested SELECT/SEARCH/SORT/FETCH/STORE/COPY/MOVE/APPEND/EXPUNGE/IDLE/ACL/QUOTA parsing, TCP listener flow, and SEARCH/SORT/FETCH/IDLE/ACL/QUOTA, including ENVELOPE/BODYSTRUCTURE, plus STORE/COPY/MOVE/APPEND/EXPUNGE response execution.

## Database

Apply `hmailserver/source/DBScripts/Upgrade5708to6000MSSQL.sql` on a backed-up MS SQL hMailServer database. It adds delivery lease columns, rule delivery metadata columns, search queue/documents tables, and the SQL Server Full-Text Search catalog/index used by fast mode.

The migration is additive: existing `hm_messages`, `hm_message_metadata`, and the data directory remain the source of truth during the transition.
