using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WindowsServiceDependencyRuntimeTests
{
    [TestMethod]
    public void MakeDependent_ReplacesOnlyServerDependenciesInLegacyOrder()
    {
        var api = new RecordingWindowsServiceDependencyApi();
        var runtime = new WindowsServiceDependencyRuntime(api);

        runtime.MakeDependent("MSSQLSERVER");

        Assert.AreEqual("hMailServer", api.ServiceName);
        CollectionAssert.AreEqual(
            new[] { "RPCSS", "MSSQLSERVER" },
            api.Dependencies.ToArray());
    }

    [TestMethod]
    public void MakeDependent_PreservesEmptyCallerDependency()
    {
        var api = new RecordingWindowsServiceDependencyApi();
        var runtime = new WindowsServiceDependencyRuntime(api);

        runtime.MakeDependent(string.Empty);

        CollectionAssert.AreEqual(
            new[] { "RPCSS", string.Empty },
            api.Dependencies.ToArray());
    }

    [TestMethod]
    public void DependencyMultiString_PreservesLegacyEntriesAndDoubleTerminator()
    {
        var value = WindowsServiceDependencyApi.BuildDependencyMultiString(
            new[] { "RPCSS", "MSSQLSERVER" });

        Assert.AreEqual("RPCSS\0MSSQLSERVER\0\0", value);
    }

    private sealed class RecordingWindowsServiceDependencyApi
        : IWindowsServiceDependencyApi
    {
        public string ServiceName { get; private set; } = string.Empty;
        public IReadOnlyList<string> Dependencies { get; private set; } = [];

        public void ReplaceDependencies(
            string serviceName,
            IReadOnlyList<string> dependencies)
        {
            ServiceName = serviceName;
            Dependencies = dependencies.ToArray();
        }
    }
}
