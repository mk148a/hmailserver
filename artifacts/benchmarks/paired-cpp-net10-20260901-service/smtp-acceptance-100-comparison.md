# Paired Durable SMTP 100-Message Comparison

- Fixture: hmail-perf-pair-service-20260901
- Manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
- Corpus state: 1,000-message base fixture; each implementation used its own matching disposable SQL/Data copy
- Workload: 100 loopback SMTP messages to 127.0.0.1:2525; SQL/Data post-run accounting required

| Implementation | p50 ms | p95 ms | p99 ms | Throughput/s | Accepted | Status |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Legacy C++ disposable SCM service | 6.678 | 10.555 | 13.322 | 19.274 | 100/100 | PASS |
| .NET 10 disposable process | 4.716 | 8.605 | 63.547 | 19.287 | 100/100 | PASS |

C++ service evidence: wrapper status PASS; SQL principal removed True; production service untouched True.
Both child reports passed exact SQL message-row/Data-file delta accounting and observed every accepted message.

**Interpretation:** this is one 100-message descriptive acceptance cell. It is not a release-wide speedup or winner claim. Larger SMTP batches, queue/delivery throughput, retries, and long soak remain open.
