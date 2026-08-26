# .NET 10 live protocol benchmark

Status: PASS
Database: hmail_perf_pair_net10_20260825_143100
Data root: C:\hmail-perf-pair-codex-20260825_1600\net10\Data
Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110
Corpus files: 1000

| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| smtp | 25 | 0 | 0.733 | 16.928 | 50.8 |
| imap | 25 | 0 | 11.745 | 17.691 | 157.739 |
| pop3 | 25 | 0 | 13.243 | 16.67 | 25.124 |

COM local-server registration was intentionally omitted because the installed AppID rejects the rewrite caller with 0x80004015.
This is live .NET 10 listener evidence, not a C++ comparison; no speed-up ratio is valid.
