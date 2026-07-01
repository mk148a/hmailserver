using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("B1EA04C0-B0B7-4638-80E4-41278CEF8C19")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceScripting
{
    [DispId(1)]
    bool Enabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    string Language
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    void Reload();

    [DispId(4)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string CheckSyntax();

    [DispId(5)]
    string Directory { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(6)]
    string CurrentScriptFile { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

[ComVisible(true)]
[Guid("68A73A47-5B56-43A3-BC11-CFC436F3BA9E")]
[ProgId("hMailServer.Scripting.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceScripting))]
public sealed class Scripting : ScriptingComAdapter
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly ScriptingAdministrationSnapshot? _snapshot;

    public Scripting()
    {
    }

    private Scripting(ScriptingAdministrationSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public override bool Enabled { get => Snapshot.Enabled; set => base.Enabled = value; }

    public override string Language { get => Snapshot.Language; set => base.Language = value; }

    public override string Directory => Snapshot.Directory;

    internal static Scripting CreateAuthorized(ScriptingAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Scripting(snapshot);
    }

    private ScriptingAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Scripting access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(false)]
public abstract class ScriptingComAdapter : IInterfaceScripting
{
    public virtual bool Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string Language { get => Unavailable<string>(); set => Unavailable(); }
    public void Reload() => Unavailable();
    public string CheckSyntax() => Unavailable<string>();
    public virtual string Directory => Unavailable<string>();
    public string CurrentScriptFile => Unavailable<string>();

    private T Unavailable<T>() => ScriptingComAuthorization.Unavailable<T>(this);

    private void Unavailable() => ScriptingComAuthorization.Unavailable(this);
}

[ComVisible(false)]
internal static class ScriptingComAuthorization
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    internal static T Unavailable<T>(IInterfaceScripting scripting)
    {
        EnsureAuthorized(scripting);
        throw new COMException(
            "This Scripting member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    internal static void Unavailable(IInterfaceScripting scripting)
    {
        EnsureAuthorized(scripting);
        throw new COMException(
            "This Scripting member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private static void EnsureAuthorized(IInterfaceScripting scripting)
    {
        if (scripting is Scripting authorized)
        {
            _ = authorized.Enabled;
            return;
        }

        throw new COMException(
            "Scripting access requires an authenticated server administrator.",
            EAccessDenied);
    }
}
