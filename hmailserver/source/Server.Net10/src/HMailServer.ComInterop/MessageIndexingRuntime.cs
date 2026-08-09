using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public interface IMessageIndexingRuntime
{
    int TotalMessageCount { get; }

    int TotalIndexedCount { get; }

    bool Enabled { get; set; }

    void Clear();

    void Index();

    string Backend { get; }

    bool IsFullTextReady { get; }

    string BackfillStatus { get; }

    string LastError { get; }

    void Rebuild();
}

[ComVisible(false)]
public static class MessageIndexingRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IMessageIndexingRuntime? _runtime;

    public static void Configure(IMessageIndexingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Volatile.Write(ref _runtime, runtime);
    }

    internal static IMessageIndexingRuntime GetRequiredRuntime()
    {
        return Volatile.Read(ref _runtime)
            ?? throw new COMException(
                "The hMailServer message-indexing runtime has not been initialized.",
                CoENotInitialized);
    }

    internal static MessageIndexing CreateAuthorizedAdapter(Func<bool>? isServerAdministrator = null) =>
        new(GetRequiredRuntime(), isServerAdministrator);
}
