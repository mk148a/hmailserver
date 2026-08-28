# Run-start-attested paired smoke

Run ID `8b819d48-c41e-4f2c-b482-c39b8fca5336` used fixture
`hmail-perf-pair-provenance6-20260828`. Immediately before each process launch,
the harness re-read the exact manifest, rejected descendant Data reparse
points, recomputed the 1,000-file Data fingerprint, queried the disposable SQL
database version and 1,000-row message fingerprint, and re-hashed the selected
executable. JSON, CSV, and Markdown validators require agreement with that
run-start attestation.

| Scenario | Legacy C++ | .NET 10 | Correctness |
| --- | ---: | ---: | --- |
| SMTP command, one sample | 15.89 ms | 31.13 ms | PASS / PASS |
| IMAP SEARCH/SORT, one sample | 367.34 ms | 812.63 ms | exact 1..1000 / exact 1..1000 |
| POP3, one sample | 12.06 ms | 72.24 ms | PASS / PASS |
| Concurrent IMAP, 1 session | 178.75 ms | 782.70 ms | 1/1 / 1/1 |

These are smoke timings, not acceptance percentiles. No speedup or superiority
claim is valid from one sample. The run proves that both implementations can
execute against a freshly re-attested paired fixture. It does not lock every
runtime DLL/config file, fingerprint all behavior-affecting SQL settings, hold
SQL immutable through the workload, validate aggregate report provenance, or
close the 1,000-session and long-soak gates. Performance remains **RED**.

Source artifacts are under `cpp-protocol`, `net10-protocol`,
`cpp-concurrent`, and `net10-concurrent` in this directory.
