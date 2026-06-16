namespace HMailServer.Core.Abstractions;

public sealed record ImapAclRightsChange(
    ImapAclRightsChangeMode Mode,
    long Rights)
{
    public long Apply(long existingRights) =>
        Mode switch
        {
            ImapAclRightsChangeMode.Replace => Rights,
            ImapAclRightsChangeMode.Add => existingRights | Rights,
            ImapAclRightsChangeMode.Remove => existingRights & ~Rights,
            _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown ACL rights change mode.")
        };
}
