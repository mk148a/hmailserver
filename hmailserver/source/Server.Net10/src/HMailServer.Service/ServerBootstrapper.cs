using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class ServerBootstrapper : BackgroundService
{
    private readonly SqlServerFullTextSearchHealthCheck _fullTextSearchHealthCheck;
    private readonly IMessageSearchIndex _messageSearchIndex;
    private readonly DatabaseVersionStartupGuard _databaseVersionStartupGuard;
    private readonly ServerReadinessSignal _serverReadinessSignal;
    private readonly ServerStatusRuntimeState _serverStatusRuntimeState;
    private readonly ILogger<ServerBootstrapper> _logger;

    public ServerBootstrapper(
        SqlServerFullTextSearchHealthCheck fullTextSearchHealthCheck,
        IMessageSearchIndex messageSearchIndex,
        DatabaseVersionStartupGuard databaseVersionStartupGuard,
        ServerReadinessSignal serverReadinessSignal,
        ServerStatusRuntimeState serverStatusRuntimeState,
        ILogger<ServerBootstrapper> logger)
    {
        _fullTextSearchHealthCheck = fullTextSearchHealthCheck;
        _messageSearchIndex = messageSearchIndex;
        _databaseVersionStartupGuard = databaseVersionStartupGuard;
        _serverReadinessSignal = serverReadinessSignal;
        _serverStatusRuntimeState = serverStatusRuntimeState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _serverStatusRuntimeState.SetServerState(2);
        try
        {
            await _databaseVersionStartupGuard
                .EnsureCompatibleAsync(stoppingToken)
                .ConfigureAwait(false);

            var fullTextReady = await _fullTextSearchHealthCheck
                .IsFullTextInstalledAsync(stoppingToken)
                .ConfigureAwait(false);

            if (!fullTextReady)
            {
                _logger.LogWarning(
                    "SQL Server Full-Text Search is unavailable; IMAP SEARCH/SORT will use the legacy file-backed fallback where possible.");
            }

            var searchIndexReady = await _messageSearchIndex
                .IsReadyAsync(stoppingToken)
                .ConfigureAwait(false);

            if (!searchIndexReady)
            {
                _logger.LogWarning(
                    "The hMailServer message search Full-Text index is not ready; IMAP SEARCH/SORT will use the legacy file-backed fallback where possible.");
            }

            _serverReadinessSignal.SetBootstrapComplete();
            _logger.LogInformation("hMailServer .NET 10 bootstrap checks completed. IMAP session engine and background indexer are ready.");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _serverStatusRuntimeState.SetServerState(1);
            _serverReadinessSignal.SetCanceled(stoppingToken);
            throw;
        }
        catch (Exception exception)
        {
            _serverStatusRuntimeState.SetServerState(1);
            _serverReadinessSignal.SetFailure(exception);
            throw;
        }
    }
}
