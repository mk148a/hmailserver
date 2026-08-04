namespace HMailServer.Core.Abstractions;

public interface IGroupAdministrationStore
{
    ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(CancellationToken cancellationToken);

    ValueTask<int> InsertGroupAsync(
        GroupAdministrationSnapshot group,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Group insertion is not implemented by this store.");

    ValueTask<bool> UpdateGroupAsync(
        GroupAdministrationSnapshot group,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Group update is not implemented by this store.");

    ValueTask<bool> DeleteGroupByIdAsync(
        int groupId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Group deletion is not implemented by this store.");
}
