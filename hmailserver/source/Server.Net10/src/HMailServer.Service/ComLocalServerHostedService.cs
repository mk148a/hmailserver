using HMailServer.ComInterop;
using Microsoft.Extensions.Hosting;

namespace HMailServer.Service;

internal sealed class ComLocalServerHostedService : IHostedService, IDisposable
{
    private readonly ComLocalServerHost _host = new(
        new ComLocalServerRegistration(
            typeof(MessageIndexing).GUID,
            static () => new MessageIndexing()));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _host.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _host.Dispose();
}
