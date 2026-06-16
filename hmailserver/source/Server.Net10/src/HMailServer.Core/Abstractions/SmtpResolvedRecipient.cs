namespace HMailServer.Core.Abstractions;

public sealed record SmtpResolvedRecipient(
    string Address,
    string OriginalAddress,
    int LocalAccountId,
    bool IsLocal,
    SmtpRouteResolution? Route = null)
{
    public bool IsRouteRecipient => Route is not null;

    public bool RouteTreatsRecipientAsLocal => Route?.TreatRecipientAsLocal == true;
}
