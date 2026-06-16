namespace HMailServer.Delivery;

public sealed record DnsMxRecord(
    string Exchange,
    ushort Preference,
    TimeSpan TimeToLive);
