# Paired C++ / .NET 10 TCP 451 retry acceptance

- Status: **PASS**
- Sink: 127.0.0.1:26045, RCPT reply 451, DATA expected on first attempt: false
- C++: queued=1, type=1, locked=0, retry=1, recipients=1, Data file=True
- Net10: queued=1, type=1, locked=0, retry=1, recipients=1, no DATA=True
- Cleanup: service=True, route=True, message=True, recipient=True, Data file=True

This is bounded transient-state parity evidence. It is not retry recovery, throughput, soak, or release clearance.

JSON: E:\Yazılım\hmailserver57\net10-continue\artifacts\benchmarks\paired-cpp-net10-20260901-delivery\cpp-tcp451-retry\paired-cpp-net10-tcp451-retry.json
