# SMTP message acceptance benchmark

Implementation: net10
Status: PASS
Implementation: net10
Database: hmail_perf_pair_net10_20260831_head3
Data root: C:\hmail-perf-pair-service-20260901\net10\Data
Bind/port: 127.0.0.1:2525
Run ID: f81e7aab-0215-49ba-bea9-0f85d3ae0a71
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-service-20260901
Fixture manifest SHA-256: 06EFCBA9520D478A6DAC7ABB7F09F3B63D015A0CD5ECA1EB4D0FD2CC8C38F9FD
Executable SHA-256: 393449EA88D8D3C63D044D759D715322FD07A6F365FB10F18C1F0F126C6B4675
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46
Requested/accepted: 100 / 100
p50/p95/p99: 4.716 / 8.605 / 63.547 ms
Throughput: 19.287 messages/s
Post-workload settle: 1 seconds
Fixture identity: 66EDD47F05E5D30C23562F7FE55AC0C5328233AABA1A8DE3339C3A2A15E83E9F
Fixture valid before/after: True / True
Post-run accounting: True; message/data deltas 100 / 100

This is a loopback disposable-target measurement. A C++/.NET 10 ratio is valid only when both implementations pass the same readiness, SQL/Data, message, and cleanup gates.
