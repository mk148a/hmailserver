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
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly IDiagnosticsRuntime? _runtime;
    private string _localDomainName = string.Empty;
    private string _testDomainName = string.Empty;

    public Diagnostics()
    {
    }

    private Diagnostics(IDiagnosticsRuntime runtime)
    {
        _runtime = runtime;
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

        return DiagnosticResults.CreateAuthorized(results);
    }

    internal static Diagnostics CreateAuthorized(IDiagnosticsRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return new Diagnostics(runtime);
    }

    private IDiagnosticsRuntime Runtime =>
        _runtime ?? throw new COMException(
            "Diagnostics access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(true)]
[Guid("3AC49BB3-3F3C-4D82-AC0F-28464C408EA9")]
[ProgId("hMailServer.DiagnosticResults.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDiagnosticResults))]
public sealed class DiagnosticResults : IInterfaceDiagnosticResults
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly IReadOnlyList<DiagnosticResultSnapshot>? _results;

    public DiagnosticResults()
    {
    }

    private DiagnosticResults(IReadOnlyList<DiagnosticResultSnapshot> results)
    {
        _results = results.ToArray();
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

            return DiagnosticResult.CreateAuthorized(results[index]);
        }
    }

    internal static DiagnosticResults CreateAuthorized(IReadOnlyList<DiagnosticResultSnapshot> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return new DiagnosticResults(results);
    }

    private IReadOnlyList<DiagnosticResultSnapshot> Results =>
        _results ?? throw new COMException(
            "DiagnosticResults access requires an authenticated server administrator.",
            EAccessDenied);
}

[ComVisible(true)]
[Guid("430C3328-6348-4A86-8E12-74B5B5EFF48D")]
[ProgId("hMailServer.DiagnosticResult.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDiagnosticResult))]
public sealed class DiagnosticResult : IInterfaceDiagnosticResult
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly DiagnosticResultSnapshot? _result;

    public DiagnosticResult()
    {
    }

    private DiagnosticResult(DiagnosticResultSnapshot result)
    {
        _result = result;
    }

    public string Name => ResultSnapshot.Name;

    public string Description => ResultSnapshot.Description;

    public string ExecutionDetails => ResultSnapshot.ExecutionDetails;

    public bool Result => ResultSnapshot.Result;

    internal static DiagnosticResult CreateAuthorized(DiagnosticResultSnapshot result) => new(result);

    private DiagnosticResultSnapshot ResultSnapshot =>
        _result ?? throw new COMException(
            "DiagnosticResult access requires an authenticated server administrator.",
            EAccessDenied);
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

    internal static Diagnostics CreateAuthorizedAdapter() =>
        Diagnostics.CreateAuthorized(Volatile.Read(ref _runtime));

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
