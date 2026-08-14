namespace HMailServer.Service;

internal sealed class ServiceReinitializationCoordinator
{
    private readonly object _gate = new();
    private readonly List<Participant> _participants = [];
    private readonly SemaphoreSlim _reinitializeGate = new(1, 1);
    private bool _started;

    internal void Register(
        string name,
        Func<CancellationToken, ValueTask> stopAsync,
        Func<CancellationToken, ValueTask> startAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(stopAsync);
        ArgumentNullException.ThrowIfNull(startAsync);

        lock (_gate)
        {
            if (_started)
            {
                throw new InvalidOperationException(
                    "Service reinitialization participants cannot be registered after the coordinator has started.");
            }

            if (_participants.Any(participant =>
                    string.Equals(participant.Name, name, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Service reinitialization participant '{name}' is already registered.");
            }

            _participants.Add(new Participant(name, stopAsync, startAsync));
        }
    }

    internal async ValueTask ReinitializeAsync(CancellationToken cancellationToken)
    {
        await _reinitializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Participant[] participants;
        try
        {
            lock (_gate)
            {
                if (_participants.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Service reinitialization is not configured with any lifecycle participants.");
                }

                _started = true;
                participants = _participants.ToArray();
            }

            var stopped = new List<Participant>(participants.Length);
            try
            {
                for (var index = participants.Length - 1; index >= 0; index--)
                {
                    await participants[index].StopAsync(CancellationToken.None).ConfigureAwait(false);
                    stopped.Add(participants[index]);
                }
            }
            catch (Exception stopException)
            {
                await CompensateStartsAsync(stopped, stopException).ConfigureAwait(false);
                throw;
            }

            var started = new List<Participant>(participants.Length);
            try
            {
                foreach (var participant in participants)
                {
                    await participant.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    started.Add(participant);
                }
            }
            catch (Exception startException)
            {
                await CompensateStopsAsync(started, startException).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _reinitializeGate.Release();
        }
    }

    private static async ValueTask CompensateStartsAsync(
        List<Participant> stopped,
        Exception originalException)
    {
        List<Exception>? compensationExceptions = null;
        for (var index = stopped.Count - 1; index >= 0; index--)
        {
            try
            {
                await stopped[index].StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception compensationException)
            {
                (compensationExceptions ??= []).Add(compensationException);
            }
        }

        ThrowWithCompensation("Service reinitialization stop failed.", originalException, compensationExceptions);
    }

    private static async ValueTask CompensateStopsAsync(
        List<Participant> started,
        Exception originalException)
    {
        List<Exception>? compensationExceptions = null;
        for (var index = started.Count - 1; index >= 0; index--)
        {
            try
            {
                await started[index].StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception compensationException)
            {
                (compensationExceptions ??= []).Add(compensationException);
            }
        }

        ThrowWithCompensation("Service reinitialization start failed.", originalException, compensationExceptions);
    }

    private static void ThrowWithCompensation(
        string message,
        Exception originalException,
        List<Exception>? compensationExceptions)
    {
        if (compensationExceptions is null)
        {
            return;
        }

        compensationExceptions.Insert(0, originalException);
        throw new AggregateException(message, compensationExceptions);
    }

    private sealed record Participant(
        string Name,
        Func<CancellationToken, ValueTask> StopAsync,
        Func<CancellationToken, ValueTask> StartAsync);
}
