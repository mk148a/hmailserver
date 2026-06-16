using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("0B2CB0F7-897F-43FA-9D4A-56A62F7E8D72")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IInterfaceMessageIndexing2
{
    [DispId(1)]
    string Backend { get; }

    [DispId(2)]
    bool IsFullTextReady { get; }

    [DispId(3)]
    string BackfillStatus { get; }

    [DispId(4)]
    string LastError { get; }

    [DispId(5)]
    void Rebuild();
}
