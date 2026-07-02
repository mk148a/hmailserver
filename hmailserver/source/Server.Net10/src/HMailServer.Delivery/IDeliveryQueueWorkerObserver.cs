namespace HMailServer.Delivery;

public interface IDeliveryQueueWorkerObserver
{
    void ProcessingFailed(Exception exception);
}
