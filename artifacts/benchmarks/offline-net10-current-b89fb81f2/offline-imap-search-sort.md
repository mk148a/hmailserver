# Offline IMAP SEARCH/SORT

| Field | Value |
| --- | --- |
| Scenario | `offline-imap-search-sort-100k` |
| Git commit | `b89fb81f24a3fc343b7fbe6885e21c2e4976ed2d` |
| Dataset | 100,000 messages, seed `5700` |
| Search | `needle` |
| Sort | `DATE DESC, UID ASC` |
| Correctness | `True` (9091 matches) |
| p50 | 8.725 ms |
| p95 | 9.426 ms |
| p99 | 9.614 ms |
| Throughput | 1036283.014 messages/s |
| Mean allocation | 459,024 bytes |
| Peak working set | 62,017,536 bytes |
| Mean Gen 0 collections | 0 |
| Mean Gen 1 collections | 0 |
| Mean Gen 2 collections | 0 |
| p95 threshold | 2500 ms (`True`) |
| Host | Microsoft Windows 10.0.26200; .NET 10.0.9; X64; 16 logical processors |
| Window | 2026-08-21T07:53:56.6790481+00:00 to 2026-08-21T07:53:56.8862086+00:00 |

First result UIDs: `19834, 39667, 59500, 79333, 99166, 9912, 29745, 49578, 69411, 89244`

This is an offline synthetic acceptance harness. It does not prove SQL Server FTS, live IMAP protocol latency, or legacy C++ performance equivalence.