using HMailServer.Protocols.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class SmtpTcpListenerHostedService : BackgroundService
{
    private readonly SmtpTcpListener _listener;
    private readonly SmtpTcpListenerOptions _options;
    private readonly ILogger<SmtpTcpListenerHostedService> _logger;

    public SmtpTcpListenerHostedService(
        SmtpTcpListener listener,
        SmtpTcpListenerOptions options,
        ILogger<SmtpTcpListenerHostedService> logger)
    {
        _listener = listener;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SMTP TCP listener is disabled. Set Smtp:Enabled=true after receive-pipeline storage is configured.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
            return;
        }

        var runTask = _listener.RunAsync(stoppingToken);
        var endpoint = await _listener.Started.WaitAsync(stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("SMTP TCP listener is accepting connections on {Endpoint}.", endpoint);
        await runTask.ConfigureAwait(false);
    }
}
