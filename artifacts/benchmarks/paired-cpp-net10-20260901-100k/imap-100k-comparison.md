# Paired 100k IMAP SEARCH/SORT acceptance

Status: PASS for both implementations (one session, Full profile)
Fixture: hmail-perf-pair-100k-20260901
Manifest SHA-256: DE4DA2CDCDA01B1BE6D8C9BC98A377167205E940722D2BBCEE98A15A16ACB23A
Corpus: 100000 SQL messages and 100000 byte-matched Data files per side
Database versions: C++ 5708 / Net10 6000
Listener: 127.0.0.1:1143

| Implementation | Acceptance | p50 ms | p95 ms | p99 ms | Search | Sort |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| cpp | PASS | 15849.605 | 15849.605 | 15849.605 | 100000/100000 | 100000/100000 |
| net10 | PASS | 846.875 | 846.875 | 846.875 | 100000/100000 | 100000/100000 |

## Interpretation

The bounded single-session p50 ratio is 18.715 (C++ divided by Net10). This is a mailbox acceptance measurement, not a general performance winner claim.
The release gate remains RED because 500/1000-session C++ capacity, SMTP/delivery/queue, restore/installer, COM lifecycle, and 24-hour soak remain open.

Raw run reports remain outside the repository under C:\\hmail-perf-pair-100k-20260901; this committed evidence contains compact summaries only.
