namespace HMailServer.Core.Abstractions;

public interface IMessageAdministrationRestoreStore
{
    ValueTask<MessageAdministrationInsertResult> InsertMessageForRestoreAsync(
        int accountId,
        int folderId,
        MessageAdministrationSnapshot snapshot,
        CancellationToken cancellationToken);
}
