using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class SignalingSmtpQueueWriter : ISmtpQueueWriter
{
    private readonly ISmtpQueueWriter _inner;
    private readonly IDeliveryQueueWakeSignal _wakeSignal;

    public SignalingSmtpQueueWriter(
        ISmtpQueueWriter inner,
        IDeliveryQueueWakeSignal wakeSignal)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(wakeSignal);

        _inner = inner;
        _wakeSignal = wakeSignal;
    }

    public async ValueTask EnqueueAsync(
        SmtpQueueWriteRequest request,
        CancellationToken cancellationToken)
    {
        await _inner.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);

        try
        {
            _wakeSignal.Signal();
        }
        catch (Exception)
        {
            // The message is already durable; wake delivery again on the idle poll.
        }
    }
}
