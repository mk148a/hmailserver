namespace HMailServer.Storage.SqlServer;

public sealed record SqlMessageFetchPlan(
    string CommandText,
    IReadOnlyDictionary<string, object> Parameters);
