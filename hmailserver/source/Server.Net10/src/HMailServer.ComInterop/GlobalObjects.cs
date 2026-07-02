using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("F0FE3307-66D9-480D-B1B5-3AF6CD3C0C8A")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceGlobalObjects
{
    [DispId(1)]
    IInterfaceDeliveryQueue DeliveryQueue { get; }

    [DispId(2)]
    IInterfaceLanguages Languages { get; }
}

[ComVisible(true)]
[Guid("B870F27A-CA77-473C-8106-A9F296F342A5")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDeliveryQueue
{
    [DispId(1)]
    void Clear();

    [DispId(2)]
    void ResetDeliveryTime(long messageId);

    [DispId(3)]
    void StartDelivery();

    [DispId(4)]
    void Remove(long messageId);
}

[ComVisible(true)]
[Guid("D8CC854E-F6F6-402C-ADF7-73F4569B9F8B")]
[ProgId("hMailServer.GlobalObjects.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceGlobalObjects))]
public sealed class GlobalObjects : IInterfaceGlobalObjects
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly bool _authorized;

    public GlobalObjects()
    {
    }

    private GlobalObjects(bool authorized)
    {
        _authorized = authorized;
    }

    public IInterfaceDeliveryQueue DeliveryQueue
    {
        get
        {
            EnsureAuthorized();
            return DeliveryQueueAdministrationRuntimeHost.CreateAuthorizedAdapter();
        }
    }

    public IInterfaceLanguages Languages
    {
        get
        {
            EnsureAuthorized();
            throw new COMException(
                "This GlobalObjects member is not implemented by the .NET 10 rewrite yet.",
                ENotImplemented);
        }
    }

    internal static GlobalObjects CreateAuthorized() => new(authorized: true);

    private void EnsureAuthorized()
    {
        if (!_authorized)
        {
            throw new COMException(
                "GlobalObjects access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }
}

[ComVisible(true)]
[Guid("27473BB7-4272-4693-ACA6-FD9D4C9C3FC5")]
[ProgId("hMailServer.DeliveryQueue.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDeliveryQueue))]
public sealed class DeliveryQueue : IInterfaceDeliveryQueue
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly bool _authorized;
    private readonly IDeliveryQueueAdministrationStore? _store;
    private readonly IDeliveryQueueWakeSignal? _wakeSignal;

    public DeliveryQueue()
    {
    }

    private DeliveryQueue(
        bool authorized,
        IDeliveryQueueAdministrationStore? store = null,
        IDeliveryQueueWakeSignal? wakeSignal = null)
    {
        _authorized = authorized;
        _store = store;
        _wakeSignal = wakeSignal;
    }

    public void Clear() => Unavailable();

    public void ResetDeliveryTime(long messageId)
    {
        EnsureAuthorized();
        if (_store is null)
        {
            throw NotImplemented();
        }

        _ = _store
            .ResetDeliveryTimeAsync(messageId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public void StartDelivery()
    {
        EnsureAuthorized();
        if (_wakeSignal is null)
        {
            throw NotImplemented();
        }

        _wakeSignal.Signal();
    }

    public void Remove(long messageId)
    {
        EnsureAuthorized();
        if (_store is null)
        {
            throw NotImplemented();
        }

        _ = _store
            .RemoveAsync(messageId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    internal static DeliveryQueue CreateAuthorized(
        IDeliveryQueueAdministrationStore? store = null,
        IDeliveryQueueWakeSignal? wakeSignal = null) =>
        new(authorized: true, store, wakeSignal);

    private void Unavailable()
    {
        EnsureAuthorized();

        throw NotImplemented();
    }

    private void EnsureAuthorized()
    {
        if (!_authorized)
        {
            throw new COMException(
                "DeliveryQueue access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private static COMException NotImplemented() => new(
        "This DeliveryQueue member is not implemented by the .NET 10 rewrite yet.",
        ENotImplemented);
}

[ComVisible(false)]
public static class DeliveryQueueAdministrationRuntimeHost
{
    private static IDeliveryQueueAdministrationStore? _store;
    private static IDeliveryQueueWakeSignal? _wakeSignal;

    public static void Configure(
        IDeliveryQueueAdministrationStore store,
        IDeliveryQueueWakeSignal? wakeSignal = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
        Volatile.Write(ref _wakeSignal, wakeSignal);
    }

    internal static DeliveryQueue CreateAuthorizedAdapter() =>
        DeliveryQueue.CreateAuthorized(
            Volatile.Read(ref _store),
            Volatile.Read(ref _wakeSignal));

    internal static void ResetForTests()
    {
        Volatile.Write(ref _store, null);
        Volatile.Write(ref _wakeSignal, null);
    }
}
