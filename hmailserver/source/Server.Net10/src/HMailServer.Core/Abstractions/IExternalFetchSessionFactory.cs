namespace HMailServer.Core.Abstractions;

public interface IExternalFetchSessionFactory
{
    ValueTask<IExternalFetchSession> ConnectAsync(
        ExternalFetchAccountLease account,
        CancellationToken cancellationToken);
}
