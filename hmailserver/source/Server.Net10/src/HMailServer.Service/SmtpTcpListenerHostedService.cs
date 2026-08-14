using HMailServer.Protocols.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class SmtpTcpListenerHostedService : BackgroundService
{
    private readonly SmtpTcpListener _listener;
    private readonly SmtpTcpListenerOptions _options;
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly ILogger<SmtpTcpListenerHostedService> _logger;
    private readonly RestartableListenerParticipant _participant;
    private readonly TaskCompletionSource<object?> _unexpectedStop = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _expectedStops;

    public SmtpTcpListenerHostedService(
        SmtpTcpListener listener,
        SmtpTcpListenerOptions options,
        ServerReadinessSignal serverReadinessSignal,
        ILogger<SmtpTcpListenerHostedService> logger,
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
            "smtp-listener",
            StopForReinitializationAsync,
            StartForReinitializationAsync);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("SMTP TCP listener is disabled. Set Smtp:Enabled=true after receive-pipeline storage is configured.");
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
                return;
            }

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
                "SMTP TCP listener is accepting connections on {Endpoint}.", endpoint))
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
            failure ?? new InvalidOperationException("SMTP TCP listener stopped unexpectedly."));
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
