# Live protocol benchmark

Implementation: net10
Status: PASS
Database: hmail_perf_pair_net10_provenance6_20260828
Data root: C:\hmail-perf-pair-provenance6-20260828\net10\Data
Bind/ports: 127.0.0.1 / SMTP 2525, IMAP 1143, POP3 25110
Corpus files: 1000
Run ID: 8b819d48-c41e-4f2c-b482-c39b8fca5336
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-provenance6-20260828
Fixture manifest SHA-256: 839A86CB0FF69324EDB8E3F183E19A774C3756BC5F2BF5FADBA4A246EFB4C61A
Executable SHA-256: D7FA61E8748FB6E1731ECA90A7F267A105FA33C9F8127F19409939014C23D0C8
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46

| Scenario | Success | Errors | p50 ms | p95 ms | p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| smtp | 1 | 0 | 31.127 | 31.127 | 31.127 |
| imap | 1 | 0 | 812.635 | 812.635 | 812.635 |
| pop3 | 1 | 0 | 72.24 | 72.24 | 72.24 |

COM local-server registration was intentionally omitted; the installed Application registration was not changed.
This single-implementation artifact does not calculate a C++/.NET 10 speed ratio.
