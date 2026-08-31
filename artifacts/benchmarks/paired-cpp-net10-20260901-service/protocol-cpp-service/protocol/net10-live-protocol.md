# Live protocol benchmark

Implementation: cpp
Status: PASS
Database: hmail_perf_pair_cpp_20260831_head3
Data root: C:\hmail-perf-pair-service-20260901\cpp\Data
Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110
Corpus files: 1000
Run ID: 24164b34-1fb1-4e48-b2c6-031b8770df77
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-service-20260901
Fixture manifest SHA-256: 6B6B696877239D511C71F759D1A38D8CFB5836D23C85A2CE78E07D930068D919
Executable SHA-256: B3DF9E8BBF4BEDB1102C94DF7C86356E1FEABCBC02A18E8EABEB1EA1453D09B8
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46

| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| smtp | 2 | 0 | 10.252 | 17.501 | 18.145 |
| imap | 2 | 0 | 287.433 | 419.088 | 430.79 |
| pop3 | 2 | 0 | 8.086 | 8.804 | 8.868 |

COM local-server registration was intentionally omitted; the installed Application registration was not changed.
This single-implementation artifact does not calculate a C++/.NET 10 speed ratio.
