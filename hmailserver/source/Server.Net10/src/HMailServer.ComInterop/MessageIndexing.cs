using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("3C58B6AD-48CC-4775-B283-53C77F717EB9")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceMessageIndexing
{
    [DispId(1)]
    int TotalMessageCount { get; }

    [DispId(2)]
    int TotalIndexedCount { get; }

    [DispId(3)]
    bool Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(4)]
    void Clear();

    [DispId(5)]
    void Index();
}
