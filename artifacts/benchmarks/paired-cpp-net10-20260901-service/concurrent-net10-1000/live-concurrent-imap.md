# Live concurrent IMAP benchmark

Implementation: net10
Status: PASS
Implementation: net10
Database: hmail_perf_pair_net10_20260831_head3
Data root: C:\hmail-perf-pair-service-20260901\net10\Data
Bind/port: 127.0.0.1:1143
Corpus files: 1000
Run ID: 512e2cc0-5ebd-4d17-b7cf-a18a28bccdb4
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-service-20260901
Fixture manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
Executable SHA-256: 393449EA88D8D3C63D044D759D715322FD07A6F365FB10F18C1F0F126C6B4675
SQL provider/server/database: Microsoft.Data.SqlClient / localhost / hmail_perf_pair_net10_20260831_head3
SQL pooling/max pool/timeout: True / 100 / 15 seconds
IMAP probe fan-out: one TCP client and one sequential IMAP session per sample
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46
Concurrency: 1000
Waves / requested sessions: 1 / 1000
Timeout: 5000 ms
Launch stagger: 0 ms per session index
Post-workload settle: 1 seconds

| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Workload s | Throughput/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| imap-concurrent | 1000 | 1000 | 0 | 0 | 3667.67 | 3946.014 | 4098.305 | 4.292686 | 232.954 |

No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.
COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed.
