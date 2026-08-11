namespace HMailServer.Delivery;

public sealed record DnsCnameRecord(
    string Target,
    TimeSpan TimeToLive);
