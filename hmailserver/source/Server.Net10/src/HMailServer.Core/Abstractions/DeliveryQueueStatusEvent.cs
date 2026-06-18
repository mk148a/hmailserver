namespace HMailServer.Core.Abstractions;

public sealed record DeliveryQueueStatusEvent(
    DeliveryQueueStatusEventKind Kind,
    long MessageId,
    string LeaseOwner,
    string? TargetKey = null,
    string? TargetDomainName = null,
    DeliveryTargetKind? TargetKind = null,
    int RecipientCount = 0,
    int RetryCount = 0,
    TimeSpan? RetryDelay = null,
    DeliveryFailureKind? FailureKind = null,
    string? Description = null);
