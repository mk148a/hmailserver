# SMTP message acceptance benchmark

Implementation: net10
Status: PASS
Implementation: net10
Database: hmail_perf_pair_net10_20260831_225556
Data root: C:\hmail-perf-pair-delivery-20260901\net10\Data
Bind/port: 127.0.0.1:2525
Run ID: 134e0ab3-c4ca-48af-ae1c-f2c091bf9001
Provenance: MANIFEST_BOUND
Fixture ID: hmail-perf-pair-delivery-20260901
Fixture manifest SHA-256: A83052CA61D7F3853E97522D3F72DDA595DB61811511435D3030E4E230E8B07E
Executable SHA-256: F6B2319F94B2989ACFC29A1A124895A151484996D94A2CC5266C1DDEAA7897AE
Run-start attestation: PASS
Run-start Data SHA-256: 45FA907CE0D3797C1E13EBB2F03F076A25656CCE27E801C18F745ACD3944FBBD
Run-start message SHA-256: 5136EEC556F9790732AE759EF52F77FE6172329EA55787A80300C4A824107E46
Requested/accepted: 500 / 500
p50/p95/p99: 3.976 / 5.875 / 10.052 ms
Throughput: 18.934 messages/s
Post-workload settle: 5 seconds
Fixture identity: 22827E2633C45C29CA7A0D2CBD5F09A0B3116982650A6FCED36BE0FA8145CED9
Fixture valid before/after: True / True
Post-run accounting: True; message/data deltas 500 / 500

This is a loopback disposable-target measurement. A C++/.NET 10 ratio is valid only when both implementations pass the same readiness, SQL/Data, message, and cleanup gates.
