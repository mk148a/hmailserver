namespace HMailServer.Search.SqlServer;

public sealed record SqlSearchPlan(
    string CommandText,
    IReadOnlyDictionary<string, object> Parameters);
