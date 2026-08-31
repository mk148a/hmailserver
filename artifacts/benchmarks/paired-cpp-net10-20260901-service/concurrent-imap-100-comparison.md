# Paired Concurrent IMAP 100-Session Comparison

- Fixture: hmail-perf-pair-service-20260901
- Manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
- Corpus: 1,000 messages; matching copied Data trees; exact message/Data attestation recorded in each report
- Profile: Full; loopback: 127.0.0.1:1143; waves: 1; sessions: 100

| Implementation | p50 ms | p95 ms | p99 ms | Throughput/s | Success | Status |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Legacy C++ disposable SCM service | 2696.204 | 4334.2 | 4377.055 | 22.717 | 100/100 | PASS |
| .NET 10 disposable process | 528.348 | 629.023 | 641.604 | 148.932 | 100/100 | PASS |

Both implementations validated SEARCH and SORT against the exact 1..1000 result sequence for every successful session. The C++ service wrapper also passed exact hm_tcpipports preflight, worker ownership, SQL principal cleanup, service deletion, and production-service preservation. The Net10 Full-Text backfill passed 1000/1000 before this run.

**Interpretation:** this is one descriptive acceptance cell. It is not a release-wide speedup or winner claim. The performance gate remains RED pending 500/1000-session capacity, 100,000-message SEARCH/SORT, durable SMTP/delivery, backup/restore timing, and soak acceptance.
