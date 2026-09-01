# Net10 TCP 451 recovery

- Result: `PASS`
- SQL database: `hmail_perf_pair_net10_20260831_225556`
- Data root: `C:\hmail-perf-pair-delivery-20260901\net10\Data`
- First attempt: `451`, queue=`1`, retry=`1`, recipient=`1`, DATA before recovery=`False`
- Recovery attempt: `250`, response observed=`True`, DATA observed=`True`
- Final state: queue=`0`, recipients=`0`, message file absent=`true`
- Status events: deferred=`2`, succeeded=`1`

This is isolated Net10 retry-recovery evidence. It is not paired C++ evidence and does not clear the performance gate.

JSON: `E:\Yazılım\hmailserver57\net10-continue\artifacts\benchmarks\paired-cpp-net10-20260901-delivery\net10-tcp451-recovery.json`
