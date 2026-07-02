using HMailServer.Delivery;
using Microsoft.Extensions.Logging;

namespace HMailServer.Service;

public sealed class DeliveryQueueClearLogObserver : IDeliveryQueueClearObserver
{
    private readonly ILogger<DeliveryQueueClearLogObserver> _logger;

    public DeliveryQueueClearLogObserver(ILogger<DeliveryQueueClearLogObserver> logger)
    {
        _logger = logger;
    }

    public void Completed(int removedMessages)
    {
        if (removedMessages > 0)
        {
            _logger.LogInformation(
                "Cleared {MessageCount} messages from the delivery queue.",
                removedMessages);
        }
    }

    public void Failed(Exception exception) =>
        _logger.LogWarning(exception, "Delivery queue clear operation failed.");
}
