namespace HMailServer.Core.Abstractions;

public sealed record ExternalAccountDownloadScriptExecutionRequest(
    ExternalFetchAccountLease Account,
    string RemoteUid,
    byte[]? MessageData,
    long MessageId = 0,
    long MessageUid = 0,
    int MessageState = 0,
    int DeliveryAttempt = 1,
    DateTimeOffset? InternalDateUtc = null,
    int MessageFlags = 0);
