namespace HMailServer.Core.Abstractions;

public sealed record DeliveryQueuedMessage(
    MessageIdentity Identity,
    string FileName,
    string FromAddress,
    long Size,
    DateTimeOffset CreatedUtc,
    byte Flags,
    int CurrentRetryCount,
    IReadOnlyList<DeliveryQueueRecipient> Recipients,
    int RuleForcedRouteId = 0,
    string? RuleBindAddress = null);
