using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed record SmtpQueueWriteRequest(
    string MailFrom,
    IReadOnlyList<SmtpResolvedRecipient> Recipients,
    byte[] MessageData,
    DateTimeOffset ReceivedUtc,
    int RuleForcedRouteId = 0,
    string? RuleBindAddress = null);
