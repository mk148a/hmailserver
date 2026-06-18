using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HMailServer.Protocols.Pop3;

public sealed class Pop3TcpListener
{
    private static readonly Encoding ResponseEncoding = Encoding.ASCII;

    private readonly Pop3Session _session;
    private readonly IPop3ConnectionStreamFactory _streamFactory;
    private readonly Pop3TcpListenerOptions _options;
    private readonly SemaphoreSlim _connectionSlots;
    private readonly ConcurrentDictionary<Task, byte> _sessions = new();
    private readonly TaskCompletionSource<IPEndPoint> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Pop3TcpListener(
        Pop3Session session,
        IPop3ConnectionStreamFactory streamFactory,
        Pop3TcpListenerOptions options)
    {
        _session = session;
        _streamFactory = streamFactory;
        _options = options;
        ValidateOptions(options);
        _connectionSlots = new SemaphoreSlim(options.MaxConcurrentConnections, options.MaxConcurrentConnections);
    }

    public Task<IPEndPoint> Started => _started.Task;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(_options.ListenAddress, _options.Port);
        try
        {
            listener.Start(_options.Backlog);
            _started.TrySetResult((IPEndPoint)listener.LocalEndpoint);

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
                        var server = (Pop3TcpListener)state!;
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
            {
                await using var stream = await _streamFactory.OpenStreamAsync(client, cancellationToken).ConfigureAwait(false);
                await _session.RunAsync(stream, cancellationToken).ConfigureAwait(false);
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
                var bytes = ResponseEncoding.GetBytes("-ERR Too many concurrent POP3 connections\r\n");
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

    private static void ValidateOptions(Pop3TcpListenerOptions options)
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
