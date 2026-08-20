namespace HMailServer.Core.Abstractions;

public sealed record ImapFetchRequest(
    int AccountId,
    int FolderId,
    IReadOnlyList<ImapIdRange> MessageSet,
    bool UseUid,
    IReadOnlyList<ImapFetchDataItem> Items)
{
    public bool RequiresRawMessage =>
        Items.Contains(ImapFetchDataItem.Body) ||
        Items.Contains(ImapFetchDataItem.BodyPeek) ||
        Items.Contains(ImapFetchDataItem.Envelope) ||
        Items.Contains(ImapFetchDataItem.BodyStructure) ||
        Items.Contains(ImapFetchDataItem.Rfc822);

    public bool MarksSeen =>
        Items.Contains(ImapFetchDataItem.Body) ||
        Items.Contains(ImapFetchDataItem.Rfc822);
}
