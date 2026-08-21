using HMailServer.Protocols.Pop3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class Pop3TcpListenerHostedService : BackgroundService
{
    private readonly Pop3TcpListener _listener;
    private readonly Pop3TcpListenerOptions _options;
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly ILogger<Pop3TcpListenerHostedService> _logger;
    private readonly RestartableListenerParticipant _participant;
    private readonly TaskCompletionSource<object?> _unexpectedStop = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _expectedStops;
    private CancellationToken _hostStoppingToken;

    public Pop3TcpListenerHostedService(
        Pop3TcpListener listener,
        Pop3TcpListenerOptions options,
        ServerReadinessSignal serverReadinessSignal,
        ILogger<Pop3TcpListenerHostedService> logger,
        ServiceReinitializationCoordinator reinitializationCoordinator)
    {
        _listener = listener;
        _options = options;
        _serverReadinessSignal = serverReadinessSignal;
        _logger = logger;
        _participant = new RestartableListenerParticipant(
            new RestartableListenerLifecycle((cancellationToken, startedEndpoint) =>
                _listener.RunAsync(cancellationToken, startedEndpoint)));
        reinitializationCoordinator.Register(
            "pop3-listener",
            StopForReinitializationAsync,
            StartForReinitializationAsync);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostStoppingToken = stoppingToken;
        try
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("POP3 TCP listener is disabled. Set Pop3:Enabled=true after mailbox storage is configured.");
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
                return;
            }

            await _serverReadinessSignal
                .WaitForBootstrapAsync(stoppingToken)
                .ConfigureAwait(false);
            await StartListenerAsync(stoppingToken).ConfigureAwait(false);
            await Task.WhenAny(
                _unexpectedStop.Task,
                Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken))
                .ConfigureAwait(false);
            if (_unexpectedStop.Task.IsCompleted)
            {
                await _unexpectedStop.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _serverReadinessSignal.SetCanceled(stoppingToken);
            throw;
        }
        catch (Exception exception)
        {
            _serverReadinessSignal.SetFailure(exception);
            throw;
        }
        finally
        {
            await StopForReinitializationAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask StartForReinitializationAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        _hostStoppingToken.ThrowIfCancellationRequested();
        if (_unexpectedStop.Task.IsFaulted)
        {
            throw new InvalidOperationException(
                "POP3 TCP listener supervision has faulted and cannot be restarted by reinitialization.",
                _unexpectedStop.Task.Exception?.GetBaseException());
        }

        await StartListenerAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask StopForReinitializationAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        Interlocked.Increment(ref _expectedStops);
        try
        {
            await _participant.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _expectedStops);
            throw;
        }
    }

    private async Task StartListenerAsync(CancellationToken cancellationToken)
    {
        await _participant.StartAsync(
            cancellationToken,
            endpoint => _logger.LogInformation(
                "POP3 TCP listener is accepting connections on {Endpoint}.", endpoint))
            .ConfigureAwait(false);
        _ = ObserveListenerStopAsync();
    }

    private async Task ObserveListenerStopAsync()
    {
        Exception? failure = null;
        try
        {
            await _participant.WaitForStopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (TryConsumeExpectedStop())
        {
            return;
        }

        _unexpectedStop.TrySetException(
            failure ?? new InvalidOperationException("POP3 TCP listener stopped unexpectedly."));
    }

    private bool TryConsumeExpectedStop()
    {
        while (true)
        {
            var expectedStops = Volatile.Read(ref _expectedStops);
            if (expectedStops == 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _expectedStops, expectedStops - 1, expectedStops)
                == expectedStops)
            {
                return true;
            }
        }
    }
}
