# Short Soak Acceptance

| Field | Value |
| --- | --- |
| Scenario | `short-offline-imap-search-sort-soak` |
| Git commit | `7ff53732eb6c4c57e6a2b02c0cb76d276e1726e2` |
| Cycles | 20/20 completed of 20 requested |
| Errors | 0 |
| p50 / p95 / p99 | 4.367 / 9.843 / 10.936 ms |
| Private memory growth | -4,616,192 bytes (limit 67,108,864) |
| Handle growth | 20 (limit 100) |
| Thread growth | 0 (limit 20) |
| TCP connection growth | 0 (limit 50) |
| GC growth | Gen0 40, Gen1 40, Gen2 40 |
| Threshold | `True` |
| Window | 2026-08-21T13:37:33.5919100+00:00 to 2026-08-21T13:37:34.2822429+00:00 |

This is a short offline synthetic soak. It does not prove a 24-hour service leak-free run, live protocol equivalence, SQL behavior, COM lifecycle, or production readiness.