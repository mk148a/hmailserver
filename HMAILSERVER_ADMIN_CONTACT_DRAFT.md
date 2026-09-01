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
  C++ and .NET 10, plus two additional 1,000-session repetitions passing on
  both implementations;
- documented fixture hashes, latency percentiles, throughput, worker/TCP/SQL
  diagnostics, legacy source references, and Mermaid charts.

The current evidence establishes reproducible protocol correctness and near-
equal throughput under the controlled workload. It does not claim that the
.NET 10 rewrite is production-ready: queue/remote-delivery coverage, restore
and installer rollback, registered COM/Admin acceptance, SEC-18, AD/SSPI,
DKIM/DMARC/SPF, and long-duration soak gates remain open. The full Net10 suite
currently reports 2,788 passed, 94 skipped, and 5 known registered-COM
E_NOINTERFACE failures.

The implementation and report are currently on a private, unpushed working
branch. I would appreciate guidance on whether this parity and benchmark
approach would be useful for upstream review, and which compatibility area
you would prefer to see reviewed next.

Regards,

[Your name]
