using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols.Smtp;

public sealed class SmtpTcpListener
{
    private const int LegacySmtpSessionType = 1;
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;

    private readonly SmtpSession _session;
    private readonly ISmtpConnectionStreamFactory _streamFactory;
    private readonly SmtpTcpListenerOptions _options;
    private readonly ISmtpEventScriptExecutor? _eventScriptExecutor;
    private readonly ServerStatusRuntimeState? _statusRuntimeState;
    private readonly SemaphoreSlim _connectionSlots;
    private readonly ConcurrentDictionary<Task, byte> _sessions = new();
    private readonly TaskCompletionSource<IPEndPoint> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _nextSessionId;

    public SmtpTcpListener(
        SmtpSession session,
        ISmtpConnectionStreamFactory streamFactory,
        SmtpTcpListenerOptions options,
        ISmtpEventScriptExecutor? eventScriptExecutor = null,
        ServerStatusRuntimeState? statusRuntimeState = null)
    {
        _session = session;
        _streamFactory = streamFactory;
        _options = options;
        _eventScriptExecutor = eventScriptExecutor;
        _statusRuntimeState = statusRuntimeState;
        ValidateOptions(options);
        _connectionSlots = new SemaphoreSlim(options.MaxConcurrentConnections, options.MaxConcurrentConnections);
    }

    public Task<IPEndPoint> Started => _started.Task;

    public async Task RunAsync(CancellationToken cancellationToken)
        => await RunAsync(cancellationToken, startedEndpoint: null).ConfigureAwait(false);

    public async Task RunAsync(
        CancellationToken cancellationToken,
        Action<IPEndPoint>? startedEndpoint)
    {
        var listener = new TcpListener(_options.ListenAddress, _options.Port);
        try
        {
            listener.Start(_options.Backlog);
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            _started.TrySetResult(endpoint);
            startedEndpoint?.Invoke(endpoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                ConfigureClient(client);

                if (!await _connectionSlots.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
                {
                    await RejectBusyClientAsync(client, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var sessionTask = HandleClientAsync(client, cancellationToken);
                _sessions.TryAdd(sessionTask, 0);
                _ = sessionTask.ContinueWith(
                    static (completedTask, state) =>
                    {
                        var server = (SmtpTcpListener)state!;
                        server._sessions.TryRemove(completedTask, out _);
                    },
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (Exception ex)
        {
            _started.TrySetException(ex);
            throw;
        }
        finally
        {
            listener.Stop();
            await WaitForSessionsToDrainAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            using (_statusRuntimeState?.TrackSession(LegacySmtpSessionType))
            {
                var connectionContext = CreateConnectionContext(client);
                if (!ProtocolClientConnectEventRunner.Run(
                        _eventScriptExecutor,
                        connectionContext.ClientIPAddress,
                        connectionContext.ClientPort,
                        connectionContext.SessionId,
                        cancellationToken))
                {
                    return;
                }

                await using var stream = await _streamFactory.OpenStreamAsync(client, cancellationToken).ConfigureAwait(false);
                await _session.RunAsync(stream, _streamFactory, connectionContext, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _connectionSlots.Release();
        }
    }

    private async ValueTask RejectBusyClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var bytes = ResponseEncoding.GetBytes("421 Too many concurrent SMTP connections\r\n");
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }

    private async ValueTask WaitForSessionsToDrainAsync()
    {
        var sessions = _sessions.Keys.ToArray();
        if (sessions.Length == 0)
        {
            return;
        }

        var allSessions = Task.WhenAll(sessions);
        var completed = await Task.WhenAny(allSessions, Task.Delay(_options.ShutdownGracePeriod)).ConfigureAwait(false);
        if (completed == allSessions)
        {
            await allSessions.ConfigureAwait(false);
        }
    }

    private void ConfigureClient(TcpClient client)
    {
        client.NoDelay = _options.NoDelay;
        client.ReceiveBufferSize = _options.ReceiveBufferBytes;
        client.SendBufferSize = _options.SendBufferBytes;
    }

    private SmtpSessionConnectionContext CreateConnectionContext(TcpClient client)
    {
        var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        return new SmtpSessionConnectionContext(
            remoteEndPoint?.Address.ToString() ?? string.Empty,
            remoteEndPoint?.Port ?? 0,
            Interlocked.Increment(ref _nextSessionId));
    }

    private static void ValidateOptions(SmtpTcpListenerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ListenAddress);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Port, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Port, 65535);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Backlog);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConcurrentConnections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ReceiveBufferBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.SendBufferBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ShutdownGracePeriod.Ticks);
    }
}
