# POP3 Large-Mailbox Comparison

Date: 2026-08-31
Decision: disposable acceptance **PASS** for both implementations; POP3
performance release evidence remains incomplete.

## Fixture and commands

Both runs used the manifest-bound disposable fixture
`hmail-perf-pair-profiles-20260831` with the same 1,000-message SQL mailbox and
1,000-file Data tree, loopback `127.0.0.1:25110`, account
`test@perf.test`, and five repetitions of:

`USER`, `PASS`, `STAT`, `LIST`, `UIDL`, `RETR 1`, `QUIT`.

No production service, database, Data directory, COM registration, or DCOM
permission was used or changed.

## Results

| Implementation | Iterations | Mailbox rows | Success | Total p50 | LIST p50 | UIDL p50 | RETR p50 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Legacy C++ `/Debug` | 5 | 1000/1000 | 5/5 | 102.670 ms | 33.987 ms | 31.786 ms | 0.895 ms |
| Net10 | 5 | 1000/1000 | 5/5 | 91.739 ms | 30.994 ms | 30.721 ms | 1.721 ms |

The focused v2 validator passed for both reports. The result is a correctness
and disposable mailbox-acceptance result, not a production performance winner:
the C++ target is a standalone `/Debug` process, the mailbox is 1,000 rather
than the required 100,000 messages, and the sample is five iterations.

## Legacy source anchors

- `hmailserver/source/Server/POP3/POP3Connection.cpp:502-504`,
  `POP3Connection::ProtocolPASS_`, loads the Inbox message list after
  authentication.
- `hmailserver/source/Server/POP3/POP3Connection.cpp:543-602`,
  `ProtocolLIST_`, enumerates the in-memory mailbox and emits message sizes.
- `hmailserver/source/Server/POP3/POP3Connection.cpp:605-659`,
  `ProtocolUIDL_`, enumerates the same mailbox and emits each message UID.
- `hmailserver/source/Server/POP3/POP3Connection.cpp:663-686`,
  `ProtocolRETR_`, resolves the one-based message index, ensures the Data file
  exists, and starts file transmission.
- `hmailserver/source/Server/POP3/POP3Connection.cpp:932-961`,
  `ProtocolSTAT_`, counts non-deleted messages and total octets.
- `hmailserver/source/Server/POP3/POP3Connection.cpp:385-395`,
  `ProtocolQUIT_`, saves mailbox changes, unlocks the mailbox, and emits the
  goodbye response.
- `hmailserver/source/Server/POP3/POP3Connection.cpp:984-992`,
  `GetMessage_`, preserves one-based POP3 message indexing.

The paired runner is `build/benchmark-net10-live-pop3-large-mailbox.ps1`; its
implementation switch selects the disposable C++ `/Debug` executable or Net10
apphost while retaining the same wire sequence. The focused validator is
`build/test-net10-live-pop3-large-mailbox.ps1`.

## Residual gate status

This slice closes the disposable POP3 large-mailbox acceptance gap for the
tested 1,000-message fixture. It does not close the required 100,000-message
POP3 soak, installed-service/native lifecycle, 24-hour leak, remote delivery,
or overall C++/.NET 10 performance gates. Those remain **RED**.
