using System.Globalization;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed class StoreBackedMessageIndexingRuntime : IMessageIndexingRuntime
{
    private const string BackendName = "SqlServerFullText";

    private readonly IMessageIndexingAdministrationStore _store;

    public StoreBackedMessageIndexingRuntime(IMessageIndexingAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public int TotalMessageCount => GetStatus().TotalMessageCount;

    public int TotalIndexedCount => GetStatus().TotalIndexedCount;

    public bool Enabled
    {
        get => GetStatus().Enabled;
        set => Wait(_store.SetEnabledAsync(value, CancellationToken.None));
    }

    public string Backend => BackendName;

    public bool IsFullTextReady => GetStatus().IsFullTextReady;

    public string BackfillStatus =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Queued={GetStatus().QueuedMessageCount}");

    public string LastError => GetStatus().LastError;

    public void Clear() => Wait(_store.ClearAsync(CancellationToken.None));

    public void Index() => Wait(_store.IndexAsync(CancellationToken.None));

    public void Rebuild() => Wait(_store.RebuildAsync(CancellationToken.None));

    private MessageIndexingAdministrationStatus GetStatus() =>
        Wait(_store.GetStatusAsync(CancellationToken.None));

    private static T Wait<T>(ValueTask<T> operation) =>
        operation.AsTask().GetAwaiter().GetResult();

    private static void Wait(ValueTask operation) =>
        operation.AsTask().GetAwaiter().GetResult();
}
