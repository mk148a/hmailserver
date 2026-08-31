# Net10 TCP 451 retry state

- Result: `PASS`
- SMTP reply: `451`
- SQL database: `hmail_perf_pair_net10_20260831_225556`
- Data root: `C:\hmail-perf-pair-delivery-20260901\net10\Data`
- Queue state: `messagetype=1`, `queued=1`, `locked=0`, `leaseOwnerIsNull=True`
- Retry state: `retryCount=1`, `recipientCount=1`, `nextTryUtc=2026-08-31T23:42:48.8230000Z`
- Protocol guard: EHLO/HELO, MAIL FROM, and RCPT observed; `451` sent; DATA observed: `False`
- Deferred status events: `2`

This is Net10 component-level disposable evidence. It is not paired C++ evidence and does not clear the production performance gate.

JSON: `E:\Yazılım\hmailserver57\net10-continue\artifacts\benchmarks\paired-cpp-net10-20260901-delivery\net10-tcp451-retry.json`
