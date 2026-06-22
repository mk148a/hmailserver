using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("5F414F73-8E29-4E51-86F2-13C12EF9227A")]
[ProgId("hMailServer.MessageIndexing.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceMessageIndexing))]
public sealed class MessageIndexing : IInterfaceMessageIndexing, IInterfaceMessageIndexing2
{
    private readonly IMessageIndexingRuntime? _runtime;

    public MessageIndexing()
    {
    }

    internal MessageIndexing(IMessageIndexingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public int TotalMessageCount => Runtime.TotalMessageCount;

    public int TotalIndexedCount => Runtime.TotalIndexedCount;

    public bool Enabled
    {
        get => Runtime.Enabled;
        set => Runtime.Enabled = value;
    }

    public string Backend => Runtime.Backend;

    public bool IsFullTextReady => Runtime.IsFullTextReady;

    public string BackfillStatus => Runtime.BackfillStatus;

    public string LastError => Runtime.LastError;

    private IMessageIndexingRuntime Runtime =>
        _runtime ?? MessageIndexingRuntimeHost.GetRequiredRuntime();

    public void Clear() => Runtime.Clear();

    public void Index() => Runtime.Index();

    public void Rebuild() => Runtime.Rebuild();
}
