# Paired IMAP query-state evidence

Status: **PASS**

This read-only evidence records SQL indexing and backfill state for a disposable paired fixture.

| Implementation | Messages | Search documents | Coverage | Queue | Indexing enabled | Full-Text ready | Search-ready |
| --- | ---: | ---: | ---: | ---: | :---: | :---: | :---: |
| cpp | 1000 | absent | n/a | absent | False | False | False |
| net10 | 1000 | 1000 | 100% | 0 | True | True | True |

This report is diagnostic evidence only. It does not establish a performance winner or authorize a production capacity change.
