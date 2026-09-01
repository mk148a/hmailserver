# Live concurrent IMAP benchmark

Implementation: net10
Status: PASS
Implementation: net10
Database: hmail_perf_pair_net10_20260831_213833
Data root: C:\hmail-perf-pair-100k-20260901\net10\Data
Bind/port: 127.0.0.1:1143
Corpus files: 100000
Run ID: ba2af146-0d84-47c8-bc8c-26f21b5c00ab
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-100k-20260901
Fixture manifest SHA-256: DE4DA2CDCDA01B1BE6D8C9BC98A377167205E940722D2BBCEE98A15A16ACB23A
Executable SHA-256: EBBF2395E8933C7BACE46AD51D451033745819B4B168934194C43B3B5121FAED
SQL provider/server/database: Microsoft.Data.SqlClient / localhost / hmail_perf_pair_net10_20260831_213833
SQL pooling/max pool/timeout: True / 100 / 15 seconds
IMAP probe fan-out: one TCP client and one sequential IMAP session per sample
Run-start attestation: PASS
Run-start Data SHA-256: E92ED927F40EA825DD551A4BA512B278123451CFAC76F908716AF84199904862
Run-start message SHA-256: 6FDC539CD1D13C7514886E7F7595F7A2A7107313E145AA44C7F0F223DA7B40B8
Concurrency: 100
Waves / requested sessions: 5 / 500
Timeout: 10000 ms
Launch stagger: 0 ms per session index
Post-workload settle: 5 seconds

| Scenario | Completed | Success | Errors | Timeouts | p50 ms | p95 ms | p99 ms | Workload s | Throughput/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| imap-concurrent | 500 | 500 | 0 | 0 | 11.265 | 20.072 | 38.713 | 0.137854 | 3627.031 |

No C++/.NET 10 ratio is calculated by this artifact. A paired performance claim requires both implementations to complete the same concurrency scenario successfully.
COM registration was not started; the installed hMailServer.Application registration and DCOM permissions were not changed.
