using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DiagnosticsComContractTests
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ELegacyComError = unchecked((int)0x800403E9);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsMarshalingAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDiagnostics),
            "FB8812B0-524A-4922-9E29-A7E9A9E9151D",
            new[] { "PerformTests", "get_LocalDomainName", "set_LocalDomainName", "get_TestDomainName", "set_TestDomainName" });
        Assert.AreEqual(
            1,
            typeof(IInterfaceDiagnostics).GetMethod(nameof(IInterfaceDiagnostics.PerformTests))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        AssertBstrProperty(typeof(IInterfaceDiagnostics), nameof(IInterfaceDiagnostics.LocalDomainName), 2);
        AssertBstrProperty(typeof(IInterfaceDiagnostics), nameof(IInterfaceDiagnostics.TestDomainName), 3);

        AssertContract(
            typeof(IInterfaceDiagnosticResults),
            "27EDFA15-CD0B-40C9-86D0-1BB11B3A1310",
            new[] { "get_Count", "get_Item" });
        Assert.AreEqual(
            1,
            typeof(IInterfaceDiagnosticResults).GetProperty(nameof(IInterfaceDiagnosticResults.Count))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        var item = typeof(IInterfaceDiagnosticResults).GetMethod("get_Item");
        Assert.IsNotNull(item);
        Assert.AreEqual(typeof(IInterfaceDiagnosticResult), item.ReturnType);
        var itemProperty = typeof(IInterfaceDiagnosticResults)
            .GetProperties()
            .Single(static property => property.GetIndexParameters().Length == 1);
        Assert.AreEqual(2, itemProperty.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDiagnosticResult),
            "7E97DEEC-29B3-4ADA-8524-EA8CEEE38918",
            new[] { "get_Name", "get_Description", "get_ExecutionDetails", "get_Result" });
        AssertBstrProperty(typeof(IInterfaceDiagnosticResult), nameof(IInterfaceDiagnosticResult.Name), 1);
        AssertBstrProperty(typeof(IInterfaceDiagnosticResult), nameof(IInterfaceDiagnosticResult.Description), 2);
        AssertBstrProperty(typeof(IInterfaceDiagnosticResult), nameof(IInterfaceDiagnosticResult.ExecutionDetails), 3);
        Assert.AreEqual(
            4,
            typeof(IInterfaceDiagnosticResult).GetProperty(nameof(IInterfaceDiagnosticResult.Result))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            typeof(IInterfaceDiagnosticResult).GetProperty(nameof(IInterfaceDiagnosticResult.Result))
                ?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Diagnostics>(
            "EB576B35-8F97-47AB-A0D1-80A3D514610B",
            "hMailServer.Diagnostics.1",
            typeof(IInterfaceDiagnostics));
        AssertComClass<DiagnosticResults>(
            "3AC49BB3-3F3C-4D82-AC0F-28464C408EA9",
            "hMailServer.DiagnosticResults.1",
            typeof(IInterfaceDiagnosticResults));
        AssertComClass<DiagnosticResult>(
            "430C3328-6348-4A86-8E12-74B5B5EFF48D",
            "hMailServer.DiagnosticResult.1",
            typeof(IInterfaceDiagnosticResult));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var diagnostics = new Diagnostics();
        var diagnosticsGetError = Assert.ThrowsExactly<COMException>(() => _ = diagnostics.LocalDomainName);
        var diagnosticsSetError = Assert.ThrowsExactly<COMException>(() => diagnostics.LocalDomainName = "example.test");
        var performError = Assert.ThrowsExactly<COMException>(() => diagnostics.PerformTests());
        var resultsError = Assert.ThrowsExactly<COMException>(() => _ = new DiagnosticResults().Count);
        var resultError = Assert.ThrowsExactly<COMException>(() => _ = new DiagnosticResult().Name);
        var applicationError = Assert.ThrowsExactly<COMException>(
            () => _ = new Application(new RecordingAdministratorAuthenticationProvider("secret")).Diagnostics);

        Assert.AreEqual(ELegacyComError, diagnosticsGetError.ErrorCode);
        Assert.AreEqual(ELegacyComError, diagnosticsSetError.ErrorCode);
        Assert.AreEqual(ELegacyComError, performError.ErrorCode);
        Assert.AreEqual(ELegacyComError, resultsError.ErrorCode);
        Assert.AreEqual(ELegacyComError, resultError.ErrorCode);
        Assert.AreEqual(EAccessDenied, applicationError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedDiagnostics_StoresDomainNamesLocallyAndReturnsRuntimeResults()
    {
        var runtime = new RecordingDiagnosticsRuntime(
            new[]
            {
                new DiagnosticResultSnapshot(
                    "Collect server details",
                    "Gather local configuration",
                    "No filesystem access was performed.",
                    true),
                new DiagnosticResultSnapshot(
                    "Outbound test",
                    "Test configured remote domain",
                    "Skipped by deterministic test runtime.",
                    false)
            });
        IInterfaceDiagnostics diagnostics = Diagnostics.CreateAuthorized(runtime);

        Assert.AreEqual(string.Empty, diagnostics.LocalDomainName);
        Assert.AreEqual(string.Empty, diagnostics.TestDomainName);
        diagnostics.LocalDomainName = "local.example.test";
        diagnostics.TestDomainName = "remote.example.test";

        Assert.AreEqual("local.example.test", diagnostics.LocalDomainName);
        Assert.AreEqual("remote.example.test", diagnostics.TestDomainName);

        var results = diagnostics.PerformTests();

        Assert.AreEqual("local.example.test", runtime.LocalDomainName);
        Assert.AreEqual("remote.example.test", runtime.TestDomainName);
        Assert.AreEqual(1, runtime.CallCount);
        Assert.AreEqual(2, results.Count);
        AssertResult(
            results[0],
            "Collect server details",
            "Gather local configuration",
            "No filesystem access was performed.",
            true);
        AssertResult(
            results[1],
            "Outbound test",
            "Test configured remote domain",
            "Skipped by deterministic test runtime.",
            false);

        var negativeIndex = Assert.ThrowsExactly<COMException>(() => _ = results[-1]);
        var tooLargeIndex = Assert.ThrowsExactly<COMException>(() => _ = results[2]);
        Assert.AreEqual(DispEBadIndex, negativeIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, tooLargeIndex.ErrorCode);
    }

    [TestMethod]
    public void AuthenticatedApplication_ExposesDiagnosticsChildThroughRuntimeBoundary()
    {
        var runtime = new RecordingDiagnosticsRuntime(
            new[]
            {
                new DiagnosticResultSnapshot("Runtime", "Injected boundary", "Executed.", true)
            });
        DiagnosticsRuntimeHost.Configure(runtime);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Diagnostics);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.IsNull(application.Authenticate("administrator", "wrong"));
        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        var diagnostics = application.Diagnostics;
        diagnostics.LocalDomainName = "local.example.test";
        diagnostics.TestDomainName = "test.example.test";
        var results = diagnostics.PerformTests();

        Assert.AreEqual("local.example.test", runtime.LocalDomainName);
        Assert.AreEqual("test.example.test", runtime.TestDomainName);
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Result);
    }

    [TestMethod]
    public void RetainedDiagnostics_LocalDomainNameRechecksLiveAuthorization()
    {
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        var diagnostics = application.Diagnostics;
        diagnostics.LocalDomainName = "local.example.test";
        Assert.AreEqual("local.example.test", diagnostics.LocalDomainName);

        Assert.IsNull(application.Authenticate("administrator", "wrong"));

        var getterError = Assert.ThrowsExactly<COMException>(() => _ = diagnostics.LocalDomainName);
        var setterError = Assert.ThrowsExactly<COMException>(() => diagnostics.LocalDomainName = "denied.example.test");
        Assert.AreEqual(ELegacyComError, getterError.ErrorCode);
        Assert.AreEqual(ELegacyComError, setterError.ErrorCode);

        Assert.IsNotNull(application.Authenticate("administrator", "secret"));
        Assert.AreEqual("local.example.test", diagnostics.LocalDomainName);
        diagnostics.LocalDomainName = "restored.example.test";
        Assert.AreEqual("restored.example.test", diagnostics.LocalDomainName);
    }

    [TestMethod]
    public void RetainedDiagnostics_AllMembersRecheckLiveAuthorization()
    {
        var runtime = new RecordingDiagnosticsRuntime(
            new[]
            {
                new DiagnosticResultSnapshot("Runtime", "Injected boundary", "Executed.", true)
            });
        DiagnosticsRuntimeHost.Configure(runtime);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("administrator", "secret"));

        var diagnostics = application.Diagnostics;
        diagnostics.LocalDomainName = "local.example.test";
        diagnostics.TestDomainName = "test.example.test";
        var results = diagnostics.PerformTests();
        var result = results[0];

        Assert.AreEqual("test.example.test", diagnostics.TestDomainName);
        Assert.AreEqual(1, results.Count);
        AssertResult(result, "Runtime", "Injected boundary", "Executed.", true);

        Assert.IsNull(application.Authenticate("administrator", "wrong"));

        AssertLegacyDenied(() => _ = diagnostics.LocalDomainName);
        AssertLegacyDenied(() => diagnostics.LocalDomainName = "denied-local.example.test");
        AssertLegacyDenied(() => _ = diagnostics.TestDomainName);
        AssertLegacyDenied(() => diagnostics.TestDomainName = "denied-test.example.test");
        AssertLegacyDenied(() => _ = diagnostics.PerformTests());
        AssertLegacyDenied(() => _ = results.Count);
        AssertLegacyDenied(() => _ = results[0]);
        AssertLegacyDenied(() => _ = result.Name);
        AssertLegacyDenied(() => _ = result.Description);
        AssertLegacyDenied(() => _ = result.ExecutionDetails);
        AssertLegacyDenied(() => _ = result.Result);

        Assert.IsNotNull(application.Authenticate("administrator", "secret"));
        Assert.AreEqual("test.example.test", diagnostics.TestDomainName);
        diagnostics.TestDomainName = "restored-test.example.test";
        Assert.AreEqual("restored-test.example.test", diagnostics.TestDomainName);
        Assert.AreEqual(1, results.Count);
        AssertResult(results[0], "Runtime", "Injected boundary", "Executed.", true);
        AssertResult(result, "Runtime", "Injected boundary", "Executed.", true);
    }

    private static void AssertLegacyDenied(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(ELegacyComError, error.ErrorCode);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertBstrProperty(Type contract, string propertyName, int dispatchId)
    {
        var property = contract.GetProperty(propertyName);
        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);

        if (property.SetMethod is not null)
        {
            Assert.AreEqual(
                UnmanagedType.BStr,
                property.SetMethod.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private static void AssertResult(
        IInterfaceDiagnosticResult result,
        string name,
        string description,
        string details,
        bool success)
    {
        Assert.AreEqual(name, result.Name);
        Assert.AreEqual(description, result.Description);
        Assert.AreEqual(details, result.ExecutionDetails);
        Assert.AreEqual(success, result.Result);
    }

    private sealed class RecordingAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class RecordingDiagnosticsRuntime(IReadOnlyList<DiagnosticResultSnapshot> results)
        : IDiagnosticsRuntime
    {
        public string? LocalDomainName { get; private set; }

        public string? TestDomainName { get; private set; }

        public int CallCount { get; private set; }

        public ValueTask<IReadOnlyList<DiagnosticResultSnapshot>> PerformTestsAsync(
            string localDomainName,
            string testDomainName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LocalDomainName = localDomainName;
            TestDomainName = testDomainName;
            CallCount++;
            return ValueTask.FromResult(results);
        }
    }
}
