namespace HMailServer.Core.Abstractions;

public sealed record ServerMessageAdministrationSnapshot(
    int Id,
    string Name,
    string Text);
