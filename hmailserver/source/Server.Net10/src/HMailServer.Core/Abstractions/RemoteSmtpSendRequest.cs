namespace HMailServer.Core.Abstractions;

public sealed record RemoteSmtpSendRequest(
    RemoteSmtpEndpoint Endpoint,
    string HeloHost,
    string SenderAddress,
    IReadOnlyList<string> RecipientAddresses,
    byte[] MessageData);
