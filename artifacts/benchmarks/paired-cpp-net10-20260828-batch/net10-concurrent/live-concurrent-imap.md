# Live concurrent IMAP benchmark

Implementation: net10
Status: PASS
Implementation: net10
Database: hmail_perf_pair_net10_provenance5_20260828
Data root: C:\hmail-perf-pair-provenance5-20260828\net10\Data
Bind/port: 127.0.0.1:1143
Corpus files: 1000
Run ID: 7a1d56e4-0c4c-4d9d-b4aa-8f4e0d8e6c01
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-provenance5-20260828
Fixture manifest SHA-256: 473290E591EC64BFB01BE9361A354CF0D99FEE43B1E19201C7F891EBDD457B11
Executable SHA-256: 6215A50DBEFD1BB54327A3A98ADD5DC3B3FCDD61CDFFBF53C04EB10DE3F2E447
Concurrency: 1
Waves / requested sessions: 1 / 1
Timeout: 5000 ms
Post-workload settle: 5 seconds

| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Workload s | Throughput/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| imap-concurrent | 1 | 1 | 0 | 0 | 429.251 | 429.251 | 429.251 | 0.434642 | 2.301 |

No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.
COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed.
