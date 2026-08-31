# Paired Concurrent IMAP Capacity Matrix

- Fixture: hmail-perf-pair-service-20260901
- Manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
- Corpus: 1,000 messages; matching copied Data trees; Full profile; loopback 127.0.0.1:1143

| Sessions | C++ status | C++ success/errors | C++ p95 ms | C++ throughput/s | Net10 status | Net10 success/errors | Net10 p95 ms | Net10 throughput/s |
| ---: | --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| 100 | PASS | 100/0 | 4334.2 | 22.717 | PASS | 100/0 | 629.023 | 148.932 |
| 500 | FAIL | 189/311 | 7551.733 | 24.617 | PASS | 500/0 | 2357.067 | 201.084 |
| 1000 | FAIL | 186/814 | 7512.292 | 24.196 | PASS | 1000/0 | 3946.014 | 232.954 |

C++ 500 and 1,000-session failures are retained as RED acceptance results. The C++ disposable wrapper reported zero cleanup failures, removed the temporary SQL principal, deleted the service, and preserved the production service state in both runs.

**Decision:** no general speedup, ratio, or winner is claimed. The 100-session cell is a descriptive paired observation; the 500/1,000 cells are not paired PASS cells because legacy C++ failed capacity acceptance. The performance release gate remains RED.
