namespace HMailServer.Core.Abstractions;

public interface IGroupMemberAdministrationStore
{
    ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
        int groupId,
        CancellationToken cancellationToken);
}
