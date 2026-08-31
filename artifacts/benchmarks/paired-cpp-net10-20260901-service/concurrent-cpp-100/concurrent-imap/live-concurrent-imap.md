# Live concurrent IMAP benchmark

Implementation: cpp
Status: PASS
Implementation: cpp
Database: hmail_perf_pair_cpp_20260831_head3
Data root: C:\hmail-perf-pair-service-20260901\cpp\Data
Bind/port: 127.0.0.1:1143
Corpus files: 1000
Run ID: bdb0685f-dddb-4bfd-8d79-18b45c236313
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-service-20260901
Fixture manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
Executable SHA-256: B3DF9E8BBF4BEDB1102C94DF7C86356E1FEABCBC02A18E8EABEB1EA1453D09B8
SQL provider/server/database: legacy native hMailServer SQL layer / localhost / hmail_perf_pair_cpp_20260831_head3
SQL pooling/max pool/timeout:  /  /  seconds
IMAP probe fan-out: one TCP client and one sequential IMAP session per sample
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46
Concurrency: 100
Waves / requested sessions: 1 / 100
Timeout: 5000 ms
Launch stagger: 0 ms per session index
Post-workload settle: 1 seconds

| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Workload s | Throughput/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| imap-concurrent | 100 | 100 | 0 | 0 | 2696.204 | 4334.2 | 4377.055 | 4.40198 | 22.717 |

No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.
COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed.
