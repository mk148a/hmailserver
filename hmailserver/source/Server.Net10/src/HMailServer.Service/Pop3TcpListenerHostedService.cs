using HMailServer.Protocols.Pop3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class Pop3TcpListenerHostedService : BackgroundService
{
    private readonly Pop3TcpListener _listener;
    private readonly Pop3TcpListenerOptions _options;
    private readonly ILogger<Pop3TcpListenerHostedService> _logger;

    public Pop3TcpListenerHostedService(
        Pop3TcpListener listener,
        Pop3TcpListenerOptions options,
        ILogger<Pop3TcpListenerHostedService> logger)
    {
        _listener = listener;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("POP3 TCP listener is disabled. Set Pop3:Enabled=true after mailbox storage is configured.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
            return;
        }

        var runTask = _listener.RunAsync(stoppingToken);
        var endpoint = await _listener.Started.WaitAsync(stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("POP3 TCP listener is accepting connections on {Endpoint}.", endpoint);
        await runTask.ConfigureAwait(false);
    }
}
