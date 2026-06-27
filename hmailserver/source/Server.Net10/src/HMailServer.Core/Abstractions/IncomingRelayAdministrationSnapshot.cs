namespace HMailServer.Core.Abstractions;

public sealed record IncomingRelayAdministrationSnapshot(
    int Id,
    string Name,
    string LowerIp,
    string UpperIp);
