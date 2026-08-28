# Live protocol benchmark

Implementation: cpp
Status: PASS
Database: hmail_perf_pair_cpp_provenance5_20260828
Data root: C:\hmail-perf-pair-provenance5-20260828\cpp\Data
Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110
Corpus files: 1000
Run ID: 7a1d56e4-0c4c-4d9d-b4aa-8f4e0d8e6c01
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-provenance5-20260828
Fixture manifest SHA-256: 473290E591EC64BFB01BE9361A354CF0D99FEE43B1E19201C7F891EBDD457B11
Executable SHA-256: 2DE1AA51367268CECA019365B14AF7237EA00AAED1360C6793BA381879E3E9B1

| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| smtp | 3 | 0 | 3.935 | 18.921 | 20.253 |
| imap | 3 | 0 | 183.776 | 336.592 | 350.175 |
| pop3 | 3 | 0 | 5.724 | 6.942 | 7.05 |

COM local-server registration was intentionally omitted; the installed Application registration was not changed.
This single-implementation artifact does not calculate a C++/.NET 10 speed ratio.
