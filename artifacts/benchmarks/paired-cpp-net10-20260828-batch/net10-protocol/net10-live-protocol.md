# Live protocol benchmark

Implementation: net10
Status: PASS
Database: hmail_perf_pair_net10_provenance5_20260828
Data root: C:\hmail-perf-pair-provenance5-20260828\net10\Data
Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110
Corpus files: 1000
Run ID: 7a1d56e4-0c4c-4d9d-b4aa-8f4e0d8e6c01
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-provenance5-20260828
Fixture manifest SHA-256: 473290E591EC64BFB01BE9361A354CF0D99FEE43B1E19201C7F891EBDD457B11
Executable SHA-256: 6215A50DBEFD1BB54327A3A98ADD5DC3B3FCDD61CDFFBF53C04EB10DE3F2E447

| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| smtp | 3 | 0 | 2.711 | 28.671 | 30.978 |
| imap | 3 | 0 | 406.168 | 699.973 | 726.089 |
| pop3 | 3 | 0 | 26.895 | 52.513 | 54.791 |

COM local-server registration was intentionally omitted; the installed Application registration was not changed.
This single-implementation artifact does not calculate a C++/.NET 10 speed ratio.
