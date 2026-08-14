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

    public SmtpTcpListenerHostedService(
        SmtpTcpListener listener,
        SmtpTcpListenerOptions options,
        ServerReadinessSignal serverReadinessSignal,
        ILogger<SmtpTcpListenerHostedService> logger)
    {
        _listener = listener;
        _options = options;
        _serverReadinessSignal = serverReadinessSignal;
        _logger = logger;
        _participant = new RestartableListenerParticipant(
            new RestartableListenerLifecycle((cancellationToken, startedEndpoint) =>
                _listener.RunAsync(cancellationToken, startedEndpoint)));
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

            await _participant.StartAsync(
                stoppingToken,
                endpoint => _logger.LogInformation(
                    "SMTP TCP listener is accepting connections on {Endpoint}.", endpoint))
                .ConfigureAwait(false);
            await _participant.WaitForStopAsync().ConfigureAwait(false);
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
            await _participant.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
