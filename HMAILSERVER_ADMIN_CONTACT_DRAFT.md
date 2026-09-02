# hMailServer Administrator Contact Draft

Subject: .NET 10 rewrite parity work and reproducible C++ comparison

Hello,

I have been working on a side-by-side .NET 10 rewrite of hMailServer, using
the legacy C++ tree as the compatibility reference. The work is being kept
isolated from production registration, services, databases, and Data
directories.

Recent verified work includes:

- bounded COM/Admin parity slices with legacy IID, vtable, DISPID, ownership,
  authentication, and direct-activation boundaries preserved;
- disposable service-backed legacy C++ execution with a paired 5708/6000 SQL
  fixture and byte-matched 1,000-message Data corpus;
- a corrected concurrent IMAP benchmark whose batch deadline includes the
  deliberate launch ramp;
- controlled Full-profile 100/500/1,000-session acceptance passing on both
  C++ and .NET 10, plus fresh current-HEAD 25-iteration SMTP/IMAP/POP3
  protocol runs and 1,000-session IMAP runs passing on both implementations;
- documented fixture hashes, latency percentiles, throughput, worker/TCP/SQL
  diagnostics, legacy source references, and Mermaid charts.

The current evidence establishes reproducible bounded protocol correctness and
near-equal throughput under the controlled workload. It does not claim that
the .NET 10 rewrite is production-ready: queue/remote-delivery coverage,
restore and installer rollback, registered COM/Admin acceptance, SEC-18,
AD/SSPI, DKIM/DMARC/SPF, and long-duration soak gates remain open. The fresh
1,000-session comparison uses a disposable C++ service and a .NET 10 process
runner, so it is not a service-lifecycle comparison. The full Net10 suite
currently reports 2,793 passed, 95 skipped, and 7 failures out of 2,895 tests:
five known registered-COM `E_NOINTERFACE` failures and two endpoint-protection
access-denied scanner cleanup failures.

The implementation and report are currently on a private, unpushed working
branch. I would appreciate guidance on whether this parity and benchmark
approach would be useful for upstream review, and which compatibility area
you would prefer to see reviewed next.

Regards,

[Your name]
