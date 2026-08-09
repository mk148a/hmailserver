using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("FB8812B0-524A-4922-9E29-A7E9A9E9151D")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDiagnostics
{
    [DispId(1)]
    IInterfaceDiagnosticResults PerformTests();

    [DispId(2)]
    string LocalDomainName
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(3)]
    string TestDomainName
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }
}

[ComVisible(true)]
[Guid("27EDFA15-CD0B-40C9-86D0-1BB11B3A1310")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDiagnosticResults
{
    [DispId(1)]
    int Count { get; }

    [DispId(2)]
    IInterfaceDiagnosticResult this[int index] { get; }
}

[ComVisible(true)]
[Guid("7E97DEEC-29B3-4ADA-8524-EA8CEEE38918")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDiagnosticResult
{
    [DispId(1)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(2)]
    string Description { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(3)]
    string ExecutionDetails { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(4)]
    bool Result { [return: MarshalAs(UnmanagedType.VariantBool)] get; }
}

[ComVisible(true)]
[Guid("EB576B35-8F97-47AB-A0D1-80A3D514610B")]
[ProgId("hMailServer.Diagnostics.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDiagnostics))]
public sealed class Diagnostics : IInterfaceDiagnostics
{
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const string LegacyAccessDeniedMessage =
        "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.";

    private readonly IDiagnosticsRuntime? _runtime;
    private readonly Func<bool>? _isServerAdministrator;
    private string _localDomainName = string.Empty;
    private string _testDomainName = string.Empty;

    public Diagnostics()
    {
    }

    private Diagnostics(IDiagnosticsRuntime runtime, Func<bool>? isServerAdministrator)
    {
        _runtime = runtime;
        _isServerAdministrator = isServerAdministrator;
    }

    public string LocalDomainName
    {
        get
        {
            _ = Runtime;
            return _localDomainName;
        }
        set
        {
            _ = Runtime;
            _localDomainName = value ?? string.Empty;
        }
    }

    public string TestDomainName
    {
        get
        {
            _ = Runtime;
            return _testDomainName;
        }
        set
        {
            _ = Runtime;
            _testDomainName = value ?? string.Empty;
        }
    }

    public IInterfaceDiagnosticResults PerformTests()
    {
        var results = Runtime
            .PerformTestsAsync(_localDomainName, _testDomainName, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return DiagnosticResults.CreateAuthorized(results, _isServerAdministrator);
    }

    internal static Diagnostics CreateAuthorized(
        IDiagnosticsRuntime runtime,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return new Diagnostics(runtime, isServerAdministrator);
    }

    private void EnsureAuthorized()
    {
        if (_runtime is null || (_isServerAdministrator is not null && !_isServerAdministrator()))
        {
            throw new COMException(LegacyAccessDeniedMessage, ELegacyComError);
        }
    }

    private IDiagnosticsRuntime Runtime
    {
        get
        {
            EnsureAuthorized();
            return _runtime!;
        }
    }
}

[ComVisible(true)]
[Guid("3AC49BB3-3F3C-4D82-AC0F-28464C408EA9")]
[ProgId("hMailServer.DiagnosticResults.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDiagnosticResults))]
public sealed class DiagnosticResults : IInterfaceDiagnosticResults
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const string LegacyAccessDeniedMessage =
        "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.";

    private readonly IReadOnlyList<DiagnosticResultSnapshot>? _results;
    private readonly Func<bool>? _isServerAdministrator;

    public DiagnosticResults()
    {
    }

    private DiagnosticResults(
        IReadOnlyList<DiagnosticResultSnapshot> results,
        Func<bool>? isServerAdministrator)
    {
        _results = results.ToArray();
        _isServerAdministrator = isServerAdministrator;
    }

    public int Count => Results.Count;

    public IInterfaceDiagnosticResult this[int index]
    {
        get
        {
            var results = Results;
            if (index < 0 || index >= results.Count)
            {
                throw new COMException("Diagnostic result index was outside the collection.", DispEBadIndex);
            }

            return DiagnosticResult.CreateAuthorized(results[index], _isServerAdministrator);
        }
    }

    internal static DiagnosticResults CreateAuthorized(
        IReadOnlyList<DiagnosticResultSnapshot> results,
        Func<bool>? isServerAdministrator = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        return new DiagnosticResults(results, isServerAdministrator);
    }

    private IReadOnlyList<DiagnosticResultSnapshot> Results
    {
        get
        {
            if (_results is null || (_isServerAdministrator is not null && !_isServerAdministrator()))
            {
                throw new COMException(LegacyAccessDeniedMessage, ELegacyComError);
            }

            return _results;
        }
    }
}

[ComVisible(true)]
[Guid("430C3328-6348-4A86-8E12-74B5B5EFF48D")]
[ProgId("hMailServer.DiagnosticResult.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDiagnosticResult))]
public sealed class DiagnosticResult : IInterfaceDiagnosticResult
{
    private const int ELegacyComError = unchecked((int)0x800403E9);
    private const string LegacyAccessDeniedMessage =
        "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.";

    private readonly DiagnosticResultSnapshot? _result;
    private readonly Func<bool>? _isServerAdministrator;

    public DiagnosticResult()
    {
    }

    private DiagnosticResult(
        DiagnosticResultSnapshot result,
        Func<bool>? isServerAdministrator)
    {
        _result = result;
        _isServerAdministrator = isServerAdministrator;
    }

    public string Name => ResultSnapshot.Name;

    public string Description => ResultSnapshot.Description;

    public string ExecutionDetails => ResultSnapshot.ExecutionDetails;

    public bool Result => ResultSnapshot.Result;

    internal static DiagnosticResult CreateAuthorized(
        DiagnosticResultSnapshot result,
        Func<bool>? isServerAdministrator = null) =>
        new(result, isServerAdministrator);

    private DiagnosticResultSnapshot ResultSnapshot
    {
        get
        {
            if (_result is null || (_isServerAdministrator is not null && !_isServerAdministrator()))
            {
                throw new COMException(LegacyAccessDeniedMessage, ELegacyComError);
            }

            return _result;
        }
    }
}

[ComVisible(false)]
public static class DiagnosticsRuntimeHost
{
    private static IDiagnosticsRuntime _runtime = EmptyDiagnosticsRuntime.Instance;

    public static void Configure(IDiagnosticsRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Volatile.Write(ref _runtime, runtime);
    }

    internal static Diagnostics CreateAuthorizedAdapter(Func<bool> isServerAdministrator) =>
        Diagnostics.CreateAuthorized(Volatile.Read(ref _runtime), isServerAdministrator);

    private sealed class EmptyDiagnosticsRuntime : IDiagnosticsRuntime
    {
        internal static readonly EmptyDiagnosticsRuntime Instance = new();

        private EmptyDiagnosticsRuntime()
        {
        }

        public ValueTask<IReadOnlyList<DiagnosticResultSnapshot>> PerformTestsAsync(
            string localDomainName,
            string testDomainName,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<DiagnosticResultSnapshot>>(Array.Empty<DiagnosticResultSnapshot>());
    }
}
