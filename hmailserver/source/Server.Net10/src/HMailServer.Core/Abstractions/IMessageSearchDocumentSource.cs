namespace HMailServer.Core.Abstractions;

public interface IMessageSearchDocumentSource
{
    ValueTask<MessageSearchDocument?> TryLoadAsync(
        MessageIdentity identity,
        CancellationToken cancellationToken);
}
