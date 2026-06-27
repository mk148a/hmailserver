namespace HMailServer.Core.Abstractions;

public interface IGroupAdministrationStore
{
    ValueTask<IReadOnlyList<GroupAdministrationSnapshot>> GetGroupsAsync(CancellationToken cancellationToken);
}
