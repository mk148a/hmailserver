namespace HMailServer.Core.Abstractions;

public interface IGroupMemberAdministrationStore
{
    ValueTask<IReadOnlyList<GroupMemberAdministrationSnapshot>> GetGroupMembersAsync(
        int groupId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertGroupMemberAsync(
        GroupMemberAdministrationSnapshot member,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Group member insertion is not implemented by this store.");
}
