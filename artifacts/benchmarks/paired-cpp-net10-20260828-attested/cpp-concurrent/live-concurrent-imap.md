# Live concurrent IMAP benchmark

Implementation: cpp
Status: PASS
Implementation: cpp
Database: hmail_perf_pair_cpp_provenance6_20260828
Data root: C:\hmail-perf-pair-provenance6-20260828\cpp\Data
Bind/port: 127.0.0.1:1143
Corpus files: 1000
Run ID: 8b819d48-c41e-4f2c-b482-c39b8fca5336
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-provenance6-20260828
Fixture manifest SHA-256: 839A86CB0FF69324EDB8E3F183E19A774C3756BC5F2BF5FADBA4A246EFB4C61A
Executable SHA-256: 2DE1AA51367268CECA019365B14AF7237EA00AAED1360C6793BA381879E3E9B1
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46
Concurrency: 1
Waves / requested sessions: 1 / 1
Timeout: 5000 ms
Post-workload settle: 5 seconds

| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Workload s | Throughput/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| imap-concurrent | 1 | 1 | 0 | 0 | 178.747 | 178.747 | 178.747 | 0.18416 | 5.43 |

No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.
COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed.
