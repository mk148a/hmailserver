using System.Runtime.Versioning;
using System.Text;
using HMailServer.ComInterop;
using Microsoft.Win32;

namespace HMailServer.Net10.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WebAdminSessionBrokerRegistryEvidenceTests
{
    private const string ExistingApplicationPath =
        $"Software\\Classes\\AppID\\{LegacyComRegistrationManifest.AppId}";

    private const string BrokerPath =
        $"Software\\Classes\\AppID\\{WebAdminSessionBrokerContract.AppId}";

    [TestMethod]
    public void CaptureReadsBothRegistryViewsAndPreservesRawValueBytes()
    {
        var reader = new FakeReader(
            Snapshot(RegistryView.Registry64, BrokerPath, [Value("LaunchPermission", [1, 2, 3])]),
            Snapshot(RegistryView.Registry32, BrokerPath, [Value("LaunchPermission", [4, 5, 6])]),
            Snapshot(RegistryView.Registry64, ExistingApplicationPath, [Value("LocalService", "hMailServer")]),
            Snapshot(RegistryView.Registry32, ExistingApplicationPath, [Value("LocalService", "hMailServer")]));

        var readback = new WindowsWebAdminBrokerRegistryEvidenceSource(reader).Capture();

        Assert.AreEqual(2, readback.BrokerAppIdViews.Count);
        Assert.AreEqual(2, readback.ExistingApplicationAppIdViews.Count);
        CollectionAssert.AreEqual(
            new byte[] { 1, 2, 3 },
            readback.BrokerAppIdViews[0].Values.Single().RawBytes);
        Assert.AreEqual(4, reader.ReadCount);
    }

    [TestMethod]
    public void MissingBrokerKeyIsReadBackWithoutRegistration()
    {
        var reader = new FakeReader(
            Snapshot(RegistryView.Registry64, ExistingApplicationPath, [Value("LocalService", "hMailServer")]),
            Snapshot(RegistryView.Registry32, ExistingApplicationPath, [Value("LocalService", "hMailServer")]));

        var readback = new WindowsWebAdminBrokerRegistryEvidenceSource(reader).Capture();

        Assert.IsFalse(readback.BrokerAppIdViews.Any(static view => view.Present));
        Assert.IsTrue(readback.ExistingApplicationAppIdViews.All(static view => view.Present));
        Assert.AreEqual(4, reader.ReadCount);
    }

    [TestMethod]
    public void UnchangedExistingApplicationSnapshotIsAccepted()
    {
        var baseline = CreateReadback(
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", [10, 20]));
        var current = CreateReadback(
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", [10, 20]));

        Assert.IsTrue(current.ExistingApplicationAppIdUnchanged(baseline));
    }

    [TestMethod]
    public void ExistingApplicationValueBytesMustRemainIdentical()
    {
        var baseline = CreateReadback(Value("LaunchPermission", [10, 20]));
        var changed = CreateReadback(Value("LaunchPermission", [10, 21]));

        Assert.IsFalse(changed.ExistingApplicationAppIdUnchanged(baseline));
    }

    [TestMethod]
    public void ExistingApplicationValueNameAndKindMustRemainIdentical()
    {
        var baseline = CreateReadback(Value("LaunchPermission", [10, 20]));
        var changedName = CreateReadback(Value("AccessPermission", [10, 20]));
        var changedKind = CreateReadback(Value("LaunchPermission", RegistryValueKind.String, [10, 20]));

        Assert.IsFalse(changedName.ExistingApplicationAppIdUnchanged(baseline));
        Assert.IsFalse(changedKind.ExistingApplicationAppIdUnchanged(baseline));
    }

    private static WebAdminBrokerAppIdRegistryReadback CreateReadback(
        params WebAdminBrokerRegistryValueSnapshot[] values)
    {
        var broker = new FakeReader(
            Snapshot(RegistryView.Registry64, BrokerPath, []),
            Snapshot(RegistryView.Registry32, BrokerPath, []),
            Snapshot(RegistryView.Registry64, ExistingApplicationPath, values),
            Snapshot(RegistryView.Registry32, ExistingApplicationPath, values));
        return new WindowsWebAdminBrokerRegistryEvidenceSource(broker).Capture();
    }

    private static WebAdminBrokerRegistryKeySnapshot Snapshot(
        RegistryView view,
        string path,
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> values) =>
        new(view, path, Present: true, values, ReadError: null);

    private static WebAdminBrokerRegistryValueSnapshot Value(string name, string value) =>
        new(name, RegistryValueKind.String, Encoding.Unicode.GetBytes(value + "\0"));

    private static WebAdminBrokerRegistryValueSnapshot Value(string name, byte[] bytes) =>
        new(name, RegistryValueKind.Binary, bytes);

    private static WebAdminBrokerRegistryValueSnapshot Value(
        string name,
        RegistryValueKind kind,
        byte[] bytes) =>
        new(name, kind, bytes);

    private sealed class FakeReader(params WebAdminBrokerRegistryKeySnapshot[] snapshots)
        : IWebAdminBrokerRegistryKeyReader
    {
        private readonly IReadOnlyDictionary<(RegistryView View, string Path), WebAdminBrokerRegistryKeySnapshot> _snapshots =
            snapshots.ToDictionary(snapshot => (snapshot.View, snapshot.KeyPath));

        public int ReadCount { get; private set; }

        public WebAdminBrokerRegistryKeySnapshot Read(RegistryView view, string keyPath)
        {
            ReadCount++;
            return _snapshots.GetValueOrDefault(
                (view, keyPath),
                new(view, keyPath, Present: false, [], ReadError: null));
        }
    }
}
