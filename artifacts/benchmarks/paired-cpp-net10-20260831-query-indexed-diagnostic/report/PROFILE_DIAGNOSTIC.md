# Paired IMAP profile diagnostic

Status: **RED**

This report uses one disposable fixture, 1,000 concurrent sessions, and one wave.
The profiles isolate listener admission, SQL-backed authentication/SELECT, SEARCH, SORT, and full SEARCH/SORT.

| Profile | C++ success | Net10 success | C++ p95 ms | Net10 p95 ms | Ratio valid |
| --- | ---: | ---: | ---: | ---: | :---: |
| Admission | 1000/1000 | 1000/1000 | 1104.908 | 1029.883 | yes |
| AuthSelect | 1000/1000 | 1000/1000 | 2716.66 | 1408.884 | yes |
| Search | 579/1000 | 1000/1000 | 16076.729 | 3242.418 | no |
| Sort | 1000/1000 | 1000/1000 | 16701.678 | 2879.619 | yes |
| Full | 775/1000 | 1000/1000 | 39912.976 | 4663.22 | no |

Admission passed 1,000/1,000 on both implementations, so the observed full-load failure is not explained by the listener acceptance path alone.
AuthSelect and Full are not both passing, so no cross-implementation latency ratio or performance winner is claimed for those profiles.
The full release gate remains RED pending a successful equivalent C++/Net10 workload matrix, installed-service/native evidence, larger corpus, and soak coverage.

Charts:

- `profile-success-count.png`
- `profile-p95-latency.png`
- `profile-throughput.png`
