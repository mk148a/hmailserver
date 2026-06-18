namespace HMailServer.Core.Abstractions;

public enum DeliveryQueueStatusEventKind
{
    MessageLeased,
    MessageLoadMissing,
    MessageDroppedByEvent,
    MessageCompleted,
    MessageDeferred,
    MessageReleased,
    NoDeliveryTargets,
    TargetDeliverySucceeded,
    TargetDeliveryDeferred,
    TargetDeliveryFailedPermanently,
    BounceSubmitted,
    BounceSkipped,
    ProcessingFailed
}
