# Paired SMTP local-delivery readback

Date: 2026-09-01. This is a bounded 25-message correctness/timing cell,
not a general performance claim.

Both implementations used the same manifest-bound disposable SQL/Data fixture
(`A83052CA61D7F3853E97522D3F72DDA595DB61811511435D3030E4E230E8B07E`). The
real legacy C++ SCM service and the Net10 service each accepted 25/25 messages.
Readback proved one `hm_messages` row and one Data file per unique marker,
`messagetype=2`, Inbox placement, account `test@perf.test`, and zero recipient
rows. The marker rows/files and disposable service/SQL principal were cleaned
after each run.

| Implementation | p50 ms | p95 ms | p99 ms | Throughput/s | Acceptance | Readback |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| Legacy C++ service | 6.845 | 10.835 | 46.054 | 18.706 | 25/25 PASS | 25 rows / 25 files PASS |
| .NET 10 | 5.336 | 29.166 | 67.014 | 18.099 | 25/25 PASS | 25 rows / 25 files PASS |

This report does not establish a winner. Transient retry/defer, larger
SMTP/delivery/queue waves, C++ 500/1000-session capacity, backup/restore,
installer/COM lifecycle, and long soak remain release blockers.

Legacy anchors: `Server/SMTP/SMTPConnection.cpp::HandleSMTPFinalizationTaskCompleted_`,
`Server/SMTP/SMTPDeliveryManager.cpp::GetNextMessage_`,
`Server/SMTP/SMTPDeliverer.cpp::DeliverMessage`, and
`Server/SMTP/ExternalDelivery.cpp::RescheduleDelivery_`.

Net10 anchors: `SmtpSession.HandleDataAsync`,
`SqlServerSmtpQueueWriter.EnqueueAsync`,
`DeliveryQueueProcessor.ProcessOneAsync`, and
`SqlServerDeliveryQueueLeaseStore.CompleteAsync`.
