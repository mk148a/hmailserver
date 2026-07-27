using Microsoft.Extensions.Hosting;

namespace HMailServer.Service;

public sealed class ServerStartupCoordinator : IHostedService
{
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly IReadOnlyList<Task> _listenerStartupTasks;

    public ServerStartupCoordinator(
        ServerReadinessSignal serverReadinessSignal,
        IReadOnlyList<Task> listenerStartupTasks)
    {
        ArgumentNullException.ThrowIfNull(serverReadinessSignal);
        ArgumentNullException.ThrowIfNull(listenerStartupTasks);

        _serverReadinessSignal = serverReadinessSignal;
        _listenerStartupTasks = listenerStartupTasks;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _serverReadinessSignal
                .WaitForBootstrapAsync(cancellationToken)
                .ConfigureAwait(false);

            if (_listenerStartupTasks.Count > 0)
            {
                await Task.WhenAll(_listenerStartupTasks)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            _serverReadinessSignal.SetReady();
        }
        catch (OperationCanceledException exception)
        {
            var readinessCancellationToken = cancellationToken.IsCancellationRequested
                ? cancellationToken
                : exception.CancellationToken.IsCancellationRequested
                    ? exception.CancellationToken
                    : new CancellationToken(canceled: true);
            _serverReadinessSignal.SetCanceled(readinessCancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            _serverReadinessSignal.SetFailure(exception);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
