using HMailServer.Core.Abstractions;

namespace HMailServer.Delivery;

public sealed class RemoteDeliveryTargetDispatcher : IDeliveryTargetDispatcher
{
    private readonly IRemoteSmtpEndpointResolver _endpointResolver;
    private readonly IDeliveryMessageContentSource _contentSource;
    private readonly IRemoteSmtpClient _smtpClient;
    private readonly RemoteDeliveryOptions _options;

    public RemoteDeliveryTargetDispatcher(
        IRemoteSmtpEndpointResolver endpointResolver,
        IDeliveryMessageContentSource contentSource,
        IRemoteSmtpClient smtpClient,
        RemoteDeliveryOptions options)
    {
        _endpointResolver = endpointResolver;
        _contentSource = contentSource;
        _smtpClient = smtpClient;
        _options = options;
    }

    public async ValueTask<DeliveryTargetDispatchResult> DispatchAsync(
        DeliveryQueuedMessage message,
        DeliveryTargetBatch targetBatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(targetBatch);

        if (targetBatch.Target.Kind == DeliveryTargetKind.LocalAccount)
        {
            return DeliveryTargetDispatchResult.TransientFailure(
                "Delivery target is not handled by the remote delivery dispatcher.",
                _options.RetryDelay);
        }

        var messageData = await _contentSource.TryLoadAsync(message, cancellationToken).ConfigureAwait(false);
        if (messageData is null)
        {
            return DeliveryTargetDispatchResult.TransientFailure(
                "Queued message content could not be loaded.",
                _options.RetryDelay);
        }

        var endpoint = await _endpointResolver.ResolveAsync(targetBatch.Target, cancellationToken).ConfigureAwait(false);
        var result = await _smtpClient.SendAsync(
            new RemoteSmtpSendRequest(
                endpoint,
                _options.HeloHost,
                message.FromAddress,
                targetBatch.Recipients.Select(static recipient => recipient.Address).ToArray(),
                messageData),
            cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? DeliveryTargetDispatchResult.Success()
            : result.FailureKind == DeliveryFailureKind.Permanent
                ? DeliveryTargetDispatchResult.PermanentFailure(result.Error ?? "Remote SMTP delivery failed.")
                : DeliveryTargetDispatchResult.TransientFailure(
                    result.Error ?? "Remote SMTP delivery failed.",
                    result.RetryDelay ?? _options.RetryDelay);
    }
}
