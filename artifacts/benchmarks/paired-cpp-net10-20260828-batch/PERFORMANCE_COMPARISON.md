# Paired C++ / .NET 10 diagnostic comparison

This is a manifest-bound diagnostic run on fixture
`hmail-perf-pair-provenance5-20260828`, using the same 1,000-message corpus,
byte-identical Data files, disposable SQL pair, loopback ports
`2525/1143/25110`, and the same protocol commands. Run ID:
`7a1d56e4-0c4c-4d9d-b4aa-8f4e0d8e6c01`.

Only three iterations were run per protocol. These numbers are not release
acceptance percentiles and do not support a speed or superiority claim.

| Scenario | C++ p50 ms | .NET 10 p50 ms | C++ p95 ms | .NET 10 p95 ms | .NET 10 / C++ p95 |
| --- | ---: | ---: | ---: | ---: | ---: |
| SMTP | 3.935 | 2.711 | 18.921 | 28.671 | 1.52x |
| IMAP SEARCH/SORT | 183.776 | 406.168 | 336.592 | 699.973 | 2.08x |
| POP3 | 5.724 | 26.895 | 6.942 | 52.513 | 7.56x |
| IMAP concurrent, 1 session | 211.400 | 429.250 | 211.400 | 429.250 | 2.03x |

Both implementations passed all three protocol iterations and the one-session
concurrent IMAP check. SEARCH and SORT returned the exact 1..1000 sequence.
The Net10 runner still emits uppercase `SEARCH completed` and `SORT completed`
where legacy emits `Search completed`; this remains a compatibility gap.

The results are evidence that the batch metadata slice is runnable on the
paired fixture, not evidence of production readiness. Required 1,000-session
acceptance, longer percentile runs, SMTP/delivery throughput, POP3 soak,
24-hour leak soak, restore/installer/lifecycle, COM, SEC-18, and other release
gates remain open. The performance gate is **RED**.

Source artifacts:

- `cpp-protocol/net10-live-protocol.{json,csv,md}`
- `net10-protocol/net10-live-protocol.{json,csv,md}`
- `cpp-concurrent/live-concurrent-imap.{json,csv,md}`
- `net10-concurrent/live-concurrent-imap.{json,csv,md}`
