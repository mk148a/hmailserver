using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("5F414F73-8E29-4E51-86F2-13C12EF9227A")]
[ProgId("hMailServer.MessageIndexing.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceMessageIndexing))]
public sealed class MessageIndexing : IInterfaceMessageIndexing, IInterfaceMessageIndexing2
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly IMessageIndexingRuntime? _runtime;
    private readonly Func<bool>? _isServerAdministrator;

    public MessageIndexing()
    {
    }

    internal MessageIndexing(
        IMessageIndexingRuntime runtime,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _isServerAdministrator = isServerAdministrator;
    }

    public int TotalMessageCount
    {
        get
        {
            EnsureServerAdministrator();
            return Runtime.TotalMessageCount;
        }
    }

    public int TotalIndexedCount
    {
        get
        {
            EnsureServerAdministrator();
            return Runtime.TotalIndexedCount;
        }
    }

    public bool Enabled
    {
        get => Runtime.Enabled;
        set => Runtime.Enabled = value;
    }

    public string Backend
    {
        get
        {
            EnsureServerAdministrator();
            return Runtime.Backend;
        }
    }

    public bool IsFullTextReady
    {
        get
        {
            EnsureServerAdministrator();
            return Runtime.IsFullTextReady;
        }
    }

    public string BackfillStatus
    {
        get
        {
            EnsureServerAdministrator();
            return Runtime.BackfillStatus;
        }
    }

    public string LastError
    {
        get
        {
            EnsureServerAdministrator();
            return Runtime.LastError;
        }
    }

    private IMessageIndexingRuntime Runtime =>
        _runtime ?? throw new COMException("Access denied.", EAccessDenied);

    public void Clear()
    {
        EnsureServerAdministrator();
        Runtime.Clear();
    }

    public void Index()
    {
        EnsureServerAdministrator();
        Runtime.Index();
    }

    public void Rebuild()
    {
        EnsureServerAdministrator();
        Runtime.Rebuild();
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "MessageIndexing access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }
}
