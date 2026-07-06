namespace HMailServer.Core.Abstractions;

public interface IImapFolderUidMaintenanceStore
{
    ValueTask<bool> RecalculateCurrentUidsAsync(CancellationToken cancellationToken);
}
