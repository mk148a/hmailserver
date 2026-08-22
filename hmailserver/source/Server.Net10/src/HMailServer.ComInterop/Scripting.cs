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
    private const int EFail = unchecked((int)0x80004005);

    private readonly ScriptingAdministrationSnapshot? _snapshot;
    private readonly IScriptSyntaxChecker? _syntaxChecker;
    private readonly IScriptRuntimeReloader? _runtimeReloader;
    private readonly Func<bool, bool>? _updateEnabled;
    private readonly Func<string, bool>? _updateLanguage;
    private bool _enabled;
    private string _language = string.Empty;

    public Scripting()
    {
    }

    private Scripting(
        ScriptingAdministrationSnapshot snapshot,
        IScriptSyntaxChecker? syntaxChecker,
        IScriptRuntimeReloader? runtimeReloader,
        Func<bool, bool>? updateEnabled,
        Func<string, bool>? updateLanguage)
    {
        _snapshot = snapshot;
        _syntaxChecker = syntaxChecker;
        _runtimeReloader = runtimeReloader;
        _updateEnabled = updateEnabled;
        _updateLanguage = updateLanguage;
        _enabled = snapshot.Enabled;
        _language = snapshot.Language;
    }

    public override bool Enabled
    {
        get => _snapshot is null ? throw AccessDenied() : _enabled;
        set
        {
            if (_updateEnabled is null)
            {
                ScriptingComAuthorization.Unavailable(this);
                return;
            }

            if (!_updateEnabled(value))
                throw new COMException("The scripting enabled update did not affect the existing settings row.", EFail);

            _enabled = value;
        }
    }

    public override string Language
    {
        get => _snapshot is null ? throw AccessDenied() : _language;
        set
        {
            if (_updateLanguage is null)
            {
                ScriptingComAuthorization.Unavailable(this);
                return;
            }

            if (!_updateLanguage(value))
                throw new COMException("The scripting language update did not affect the existing settings row.", EFail);

            _language = value;
        }
    }

    public override void Reload()
    {
        if (_runtimeReloader is null)
        {
            base.Reload();
            return;
        }

        _runtimeReloader.Reload(Language, CurrentScriptFile);
    }

    public override string CheckSyntax()
    {
        return _syntaxChecker is null
            ? base.CheckSyntax()
            : _syntaxChecker.CheckSyntax(Language, CurrentScriptFile);
    }

    public override string Directory => Snapshot.Directory;

    public override string CurrentScriptFile
    {
        get
        {
            var extension = Language switch
            {
                "VBScript" => "vbs",
                "JScript" => "js",
                _ => string.Empty
            };

            return $"{Snapshot.Directory}\\EventHandlers.{extension}";
        }
    }

    internal static Scripting CreateAuthorized(
        ScriptingAdministrationSnapshot snapshot,
        IScriptSyntaxChecker? syntaxChecker = null,
        IScriptRuntimeReloader? runtimeReloader = null,
        Func<bool, bool>? updateEnabled = null,
        Func<string, bool>? updateLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Scripting(snapshot, syntaxChecker, runtimeReloader, updateEnabled, updateLanguage);
    }

    private ScriptingAdministrationSnapshot Snapshot =>
        _snapshot ?? throw new COMException(
            "Scripting access requires an authenticated server administrator.",
            EAccessDenied);

    private static COMException AccessDenied() => new(
        "Scripting access requires an authenticated server administrator.",
        EAccessDenied);
}

[ComVisible(false)]
public abstract class ScriptingComAdapter : IInterfaceScripting
{
    public virtual bool Enabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string Language { get => Unavailable<string>(); set => Unavailable(); }
    public virtual void Reload() => Unavailable();
    public virtual string CheckSyntax() => Unavailable<string>();
    public virtual string Directory => Unavailable<string>();
    public virtual string CurrentScriptFile => Unavailable<string>();

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
