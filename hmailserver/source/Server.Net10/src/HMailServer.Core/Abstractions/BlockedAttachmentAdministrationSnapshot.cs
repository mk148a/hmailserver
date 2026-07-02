namespace HMailServer.Core.Abstractions;

public sealed record BlockedAttachmentAdministrationSnapshot(
    int Id,
    string Wildcard,
    string Description);
