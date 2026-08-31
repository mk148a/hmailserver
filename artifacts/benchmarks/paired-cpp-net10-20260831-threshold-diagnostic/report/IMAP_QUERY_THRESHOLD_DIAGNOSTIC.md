# Paired IMAP query threshold diagnostic

Status: **RED**

This report uses one disposable indexed fixture and one wave at each concurrency level.
Ratios are valid only when both implementations pass the same profile and level.

| Profile | Sessions | C++ success | Net10 success | C++ p95 ms | Net10 p95 ms | Ratio valid |
| --- | ---: | ---: | ---: | ---: | ---: | :---: |
| Search | 100 | 100/100 | 100/100 | 3850.617 | 464.821 | yes |
| Search | 500 | 500/500 | 500/500 | 16866.959 | 1930.477 | yes |
| Search | 1000 | 890/1000 | 1000/1000 | 30353.639 | 3203.741 | no |
| Full | 100 | 100/100 | 100/100 | 4050.096 | 748.431 | yes |
| Full | 500 | 500/500 | 500/500 | 23351.756 | 3064.019 | yes |
| Full | 1000 | 951/1000 | 1000/1000 | 46856.266 | 4869.245 | no |

Net10 passed all indexed Search and Full levels. C++ passed 100 and 500 sessions but failed the 1,000-session Search and Full acceptance levels.
The performance release gate remains RED; no overall speed winner is claimed.

Charts:

- `threshold-success-count.png`
- `threshold-p95-latency.png`
- `threshold-throughput.png`
