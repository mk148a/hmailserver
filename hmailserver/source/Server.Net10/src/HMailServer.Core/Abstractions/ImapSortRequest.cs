namespace HMailServer.Core.Abstractions;

public sealed record ImapSortRequest(
    ImapSearchRequest SearchRequest,
    IReadOnlyList<ImapSortCriterion> Criteria)
{
    public bool ReturnUid => SearchRequest.ReturnUid;
}
