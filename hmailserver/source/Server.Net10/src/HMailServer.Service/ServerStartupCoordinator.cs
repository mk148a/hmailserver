using HMailServer.Core.Abstractions;
using Microsoft.Extensions.Hosting;

namespace HMailServer.Service;

public sealed class ServerStartupCoordinator : IHostedService
{
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly IReadOnlyList<Task> _listenerStartupTasks;
    private readonly ServerStatusRuntimeState? _serverStatusRuntimeState;

    public ServerStartupCoordinator(
        ServerReadinessSignal serverReadinessSignal,
        IReadOnlyList<Task> listenerStartupTasks,
        ServerStatusRuntimeState? serverStatusRuntimeState = null,
        IHostApplicationLifetime? applicationLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(serverReadinessSignal);
        ArgumentNullException.ThrowIfNull(listenerStartupTasks);

        _serverReadinessSignal = serverReadinessSignal;
        _listenerStartupTasks = listenerStartupTasks;
        _serverStatusRuntimeState = serverStatusRuntimeState;
        if (applicationLifetime is not null && serverStatusRuntimeState is not null)
        {
            applicationLifetime.ApplicationStopped.Register(
                static state => ((ServerStatusRuntimeState)state!).SetServerState(1),
                serverStatusRuntimeState);
        }
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
            _serverStatusRuntimeState?.SetServerState(3);
        }
        catch (OperationCanceledException exception)
        {
            _serverStatusRuntimeState?.SetServerState(1);
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
            _serverStatusRuntimeState?.SetServerState(1);
            _serverReadinessSignal.SetFailure(exception);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _serverStatusRuntimeState?.SetServerState(4);
        return Task.CompletedTask;
    }
}
