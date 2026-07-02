namespace HMailServer.Delivery;

public interface IDeliveryQueueClearObserver
{
    void Completed(int removedMessages);

    void Failed(Exception exception);
}
