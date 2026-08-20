namespace HMailServer.Core.Abstractions;

public interface IDeliveryQueueClearCoordinator
{
    void Schedule(Func<bool>? authorizationGuard = null);
}
