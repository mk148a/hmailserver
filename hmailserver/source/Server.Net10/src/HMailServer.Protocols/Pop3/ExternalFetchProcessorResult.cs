namespace HMailServer.Protocols.Pop3;

public sealed record ExternalFetchProcessorResult(
    int DeferredInactiveAccounts,
    int AccountsLeased,
    int AccountsCompleted,
    int AccountsFailed,
    int MessagesDownloaded,
    int MessagesAccepted,
    int RemoteMessagesDeleted,
    int KnownUidsAdded,
    int KnownUidsDeleted)
{
    public static ExternalFetchProcessorResult Empty { get; } =
        new(
            DeferredInactiveAccounts: 0,
            AccountsLeased: 0,
            AccountsCompleted: 0,
            AccountsFailed: 0,
            MessagesDownloaded: 0,
            MessagesAccepted: 0,
            RemoteMessagesDeleted: 0,
            KnownUidsAdded: 0,
            KnownUidsDeleted: 0);
}
