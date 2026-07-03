namespace HMailServer.Core.Abstractions;

public interface IMessageAdministrationContentSource
{
    ValueTask<byte[]?> TryLoadMessageAsync(
        MessageAdministrationSnapshot message,
        CancellationToken cancellationToken);
}
