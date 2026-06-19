namespace HMailServer.Core.Abstractions;

public interface IScriptMessageCopyStore
{
    ValueTask<MessageIdentity> CopyAsync(
        ScriptMessageCopyRequest request,
        CancellationToken cancellationToken);
}
