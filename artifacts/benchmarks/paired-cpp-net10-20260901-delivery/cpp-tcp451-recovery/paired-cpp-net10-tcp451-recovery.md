# Paired C++ / .NET 10 TCP 451 recovery acceptance

- Status: **PASS**
- Sink: 127.0.0.1:26045, first RCPT reply 451, recovery RCPT reply 250, DATA expected only after recovery
- C++ initial state: queued=1, retry=1, recipients=1, Data file=True
- C++ final state: queued=0, recipients=0, Data file=False
- Net10 initial state: queued=1, retry=1, recipients=1, DATA before recovery=False
- Net10 final state: queued=0, recipients=0, Data file absent=True
- Cleanup: service=True, route=True, message=True, recipient=True, Data file=True

This is bounded retry-recovery parity evidence. It is not throughput, soak, or release clearance.

JSON: E:\Yazılım\hmailserver57\net10-continue\artifacts\benchmarks\paired-cpp-net10-20260901-delivery\cpp-tcp451-recovery\paired-cpp-net10-tcp451-recovery.json
