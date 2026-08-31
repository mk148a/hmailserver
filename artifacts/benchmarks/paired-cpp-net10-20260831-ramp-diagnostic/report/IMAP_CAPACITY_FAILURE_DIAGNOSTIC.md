# IMAP Capacity Failure Diagnostic

Date: 2026-08-31
Decision: **RED**; no production listener or query change is justified by this evidence.

## Fixture

The runs use the disposable paired fixture `hmail-perf-pair-profiles-20260831`
from `C:\hmail-perf-pair-profiles-20260831\paired-fixture.json`. It contains
the same 1,000-message SQL corpus and 1,000-file Data corpus for legacy schema
5708 and Net10 schema 6000. Both listeners are loopback-only on IMAP
`127.0.0.1:1143`; no service, production database, or production Data directory
was used.

## Evidence

| Profile | Launch mode | Implementation | Success | Errors | Timeouts | Error boundary |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Search | simultaneous | C++ | 890/1000 | 110 | 0 | 25 connection refused, 85 transport-read failures |
| Full | simultaneous | C++ | 951/1000 | 49 | 0 | 49 transport-read failures |
| Search | 5 ms/session-index ramp | C++ | 402/1000 | 598 | 0 | 598 connection refused |
| Search | 5 ms/session-index ramp | Net10 | 1000/1000 | 0 | 0 | PASS |

The simultaneous rows are from the indexed threshold matrix:
`paired-cpp-net10-20260831-threshold-diagnostic`. The ramp rows are from
`paired-cpp-net10-20260831-ramp-diagnostic`; the runner records the ramp in
`probeConfiguration.launchStaggerMilliseconds` and the validator checks it.

The C++ failures occur before a valid IMAP `SEARCH` response is available.
The observed error is either an active connection refusal or a transport read
failure, not a SQL error, invalid search result, or IMAP completion response.
The C++ process remained alive after the workload and reported no runtime or
shutdown failure.

## Legacy source mapping

- `hmailserver/source/Server/Common/TCPIP/TCPServer.cpp:52-109`,
  `TCPServer::InitAcceptor`, opens, binds, and calls `acceptor_.listen()` without
  an explicit backlog.
- `hmailserver/source/Server/Common/TCPIP/TCPServer.cpp:132-144`,
  `TCPServer::StartAccept`, posts one asynchronous accept for a new connection.
- `hmailserver/source/Server/Common/TCPIP/TCPServer.cpp:156-177`,
  `TCPServer::HandleAccept`, reposts the accept before session admission and
  connection handling.
- `hmailserver/source/Server/Common/TCPIP/IOService.cpp:137-150`,
  `IOService::DoWork`, creates the native IOCP queue using the configured TCP/IP
  thread count. The disposable C++ database has `tcpipthreads=15`.
- `hmailserver/source/Server/Common/Application/SessionManager.cpp:43-105`,
  `SessionManager::CreateSession`, enforces security range and protocol limits;
  the disposable fixture has `maximapconnections=0`, so no configured IMAP cap
  explains the refusals.
- `hmailserver/source/Server/IMAP/IMAPCommandSearch.cpp:40-50`,
  `IMAPCommandSEARCH::ExecuteCommand`, requires authentication and a selected
  folder before query execution.
- `hmailserver/source/Server/IMAP/IMAPCommandSearch.cpp:80-105`, the legacy
  query copies and scans the selected folder messages; this path is only reached
  after a connection survives the accept/auth/select boundary.

## Harness mapping

- `build/benchmark-net10-live-concurrent-imap.ps1:256-269` starts the requested
  samples behind a common barrier; the optional launch stagger is applied only
  after that barrier.
- `build/benchmark-net10-live-concurrent-imap.ps1:301-315` opens each loopback
  TCP client and reads the greeting before sending the IMAP command sequence.
- `build/benchmark-net10-live-concurrent-imap.ps1:332-350` sends `LOGIN`,
  `SELECT`, and profile-specific `SEARCH`/`SORT` commands only after the greeting.
- `build/test-net10-live-concurrent-imap.ps1` validates the isolated manifest,
  exact session accounting, and the recorded launch-stagger value.

## Classification and action

The evidence classifies the current C++ 1,000-session failure as a native
transport/accept-processing capacity failure under this burst and ramp
workload, not as a proven Net10 SQL or search correctness defect. The ramp did
not recover C++; it produced more refusals while Net10 remained green. This is
consistent with native accept/IOCP processing saturation, but a definitive root
cause would require an installed legacy service with independent socket/IOCP
telemetry and a production-equivalent disposable host.

No listener backlog, `tcpipthreads`, session-limit, SQL, ACL, or IMAP query
behavior was changed. No C++/.NET 10 speedup or overall performance winner is
claimed. The release gate remains **RED** until the required installed-service,
100,000-message, 1,000-session, SMTP/delivery, POP3, and soak acceptance
evidence exists.
