# Paired IMAP profile diagnostic

Status: **RED**

This report uses one disposable fixture, 1,000 concurrent sessions, and one wave.
The profiles isolate listener admission, SQL-backed authentication/SELECT, and full SEARCH/SORT.

| Profile | C++ success | Net10 success | C++ p95 ms | Net10 p95 ms | Ratio valid |
| --- | ---: | ---: | ---: | ---: | :---: |
| Admission | 1000/1000 | 1000/1000 | 785.501 | 1050.048 | yes |
| AuthSelect | 979/1000 | 1000/1000 | 2636.296 | 1595.547 | no |
| Full | 223/1000 | 0/1000 | 9675.315 | n/a | no |

Admission passed 1,000/1,000 on both implementations, so the observed full-load failure is not explained by the listener acceptance path alone.
AuthSelect and Full are not both passing, so no cross-implementation latency ratio or performance winner is claimed for those profiles.
The full release gate remains RED pending a successful equivalent C++/Net10 workload matrix, installed-service/native evidence, larger corpus, and soak coverage.

Charts:

- `profile-success-count.png`
- `profile-p95-latency.png`
- `profile-throughput.png`
