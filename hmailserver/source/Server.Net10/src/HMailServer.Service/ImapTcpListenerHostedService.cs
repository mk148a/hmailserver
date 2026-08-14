using HMailServer.Protocols.Imap;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class ImapTcpListenerHostedService : BackgroundService
{
    private readonly ImapTcpListener _listener;
    private readonly ImapTcpListenerOptions _options;
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly ILogger<ImapTcpListenerHostedService> _logger;
    private readonly RestartableListenerParticipant _participant;

    public ImapTcpListenerHostedService(
        ImapTcpListener listener,
        ImapTcpListenerOptions options,
        ServerReadinessSignal serverReadinessSignal,
        ILogger<ImapTcpListenerHostedService> logger)
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
                _logger.LogInformation("IMAP TCP listener is disabled. Set Imap:Enabled=true after authentication/session mapping is configured.");
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
                return;
            }

            await _participant.StartAsync(
                stoppingToken,
                endpoint => _logger.LogInformation(
                    "IMAP TCP listener is accepting connections on {Endpoint}.", endpoint))
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
