# Live protocol benchmark

Implementation: net10
Status: FAIL
Database: hmail_perf_pair_net10_20260831_head3
Data root: C:\hmail-perf-pair-service-20260901\net10\Data
Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110
Corpus files: 1000
Run ID: 22476d94-a918-4a78-bf31-d11d287926f9
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-service-20260901
Fixture manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
Executable SHA-256: 393449EA88D8D3C63D044D759D715322FD07A6F365FB10F18C1F0F126C6B4675
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46

| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| smtp | 2 | 0 | 20.524 | 37.584 | 39.101 |
| imap | 0 | 2 |  |  |  |
| pop3 | 2 | 0 | 24.699 | 30.886 | 31.436 |

COM local-server registration was intentionally omitted; the installed Application registration was not changed.
This single-implementation artifact does not calculate a C++/.NET 10 speed ratio.
