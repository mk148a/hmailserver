using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using HMailServer.ComInterop;
using Microsoft.Win32;

namespace HMailServer.Net10.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WebAdminSessionBrokerRegistryEvidenceTests
{
    private const string WorkerSid = "S-1-5-82-2759919546-3181318411-3457700337-2112356574-3667061494";
    private const string SystemSid = "S-1-5-18";

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
    public void RegistryReadbackBuildsExactPermissionEvidenceForPreflight()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var values = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var baseline = CreateReadbackWithBroker(values, values);
        var current = CreateReadbackWithBroker(values, values);
        var source = new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader());

        var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            current,
            baseline,
            source);

        Assert.IsTrue(result.Ready, result.Reason);
        Assert.AreEqual("broker-only-appid-preflight-passed", result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsMissingBrokerAndMismatchedViews()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var values = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var baseline = CreateReadbackWithBroker([], []);
        var missing = CreateReadbackWithBroker([], []);
        var missingResult = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            missing,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var mismatched = CreateReadbackWithBroker(
            values,
            [.. values, Value("Extra", [1])]);
        var mismatchResult = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            mismatched,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        Assert.IsFalse(missingResult.Ready);
        Assert.AreEqual("broker-registration-missing", missingResult.Reason);
        Assert.IsFalse(mismatchResult.Ready);
        Assert.AreEqual("broker-registry-views-mismatch", mismatchResult.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRequiresAuthoritative64BitBrokerViewAndReadableViews()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var values = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var legacy64 = Snapshot(RegistryView.Registry64, ExistingApplicationPath, [Value("LocalService", "hMailServer")]);
        var legacy32 = Snapshot(RegistryView.Registry32, ExistingApplicationPath, [Value("LocalService", "hMailServer")]);
        var baseline = new WindowsWebAdminBrokerRegistryEvidenceSource(
            new FakeReader(legacy64, legacy32)).Capture();

        var missing64 = new WebAdminBrokerAppIdRegistryReadback(
            [Snapshot(RegistryView.Registry32, BrokerPath, values)],
            [legacy64, legacy32]);
        var missing64Result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            missing64,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var readError = new WindowsWebAdminBrokerRegistryEvidenceSource(
            new FakeReader(
                legacy64,
                legacy32,
                new(RegistryView.Registry64, BrokerPath, false, [], "UnauthorizedAccessException"))).Capture();
        var readErrorResult = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            readError,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var incompleteLegacy = new WindowsWebAdminBrokerRegistryEvidenceSource(
            new FakeReader(
                new(RegistryView.Registry64, ExistingApplicationPath, false, [], "IOException"),
                legacy32)).Capture();
        var incompleteLegacyResult = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            incompleteLegacy,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var readError32 = new WindowsWebAdminBrokerRegistryEvidenceSource(
            new FakeReader(
                legacy64,
                legacy32,
                Snapshot(RegistryView.Registry64, BrokerPath, values),
                new(RegistryView.Registry32, BrokerPath, false, [], "IOException"))).Capture();
        var readError32Result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            readError32,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        Assert.IsFalse(missing64Result.Ready);
        Assert.AreEqual("broker-registry-64-view-missing", missing64Result.Reason);
        Assert.IsFalse(readErrorResult.Ready);
        Assert.AreEqual("broker-registry-readback-error", readErrorResult.Reason);
        Assert.IsFalse(incompleteLegacyResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", incompleteLegacyResult.Reason);
        Assert.IsFalse(readError32Result.Ready);
        Assert.AreEqual("broker-registry-readback-error", readError32Result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsAbsent32BitBrokerView()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var brokerValues = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var legacyValues = new[] { Value("LocalService", "hMailServer") };
        var current = new WebAdminBrokerAppIdRegistryReadback(
            [Snapshot(RegistryView.Registry64, BrokerPath, brokerValues)],
            [
                Snapshot(RegistryView.Registry64, ExistingApplicationPath, legacyValues),
                Snapshot(RegistryView.Registry32, ExistingApplicationPath, legacyValues)
            ]);
        var baseline = new WebAdminBrokerAppIdRegistryReadback(
            [],
            [
                Snapshot(RegistryView.Registry64, ExistingApplicationPath, legacyValues),
                Snapshot(RegistryView.Registry32, ExistingApplicationPath, legacyValues)
            ]);

        var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            current,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("broker-registry-32-view-missing", result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsMalformedNonBinaryAndWrongMaskPermissions()
    {
        var valid = SecurityDescriptor(WorkerSid, SystemSid);
        var cases = new[]
        {
            new[]
            {
                Value("LocalService", "hMailServer"),
                Value("LaunchPermission", [1, 2, 3]),
                Value("AccessPermission", valid)
            },
            new[]
            {
                Value("LocalService", "hMailServer"),
                Value("LaunchPermission", "not-binary"),
                Value("AccessPermission", valid)
            },
            new[]
            {
                Value("LocalService", "hMailServer"),
                Value("LaunchPermission", SecurityDescriptorWithAccess("GA", WorkerSid, SystemSid)),
                Value("AccessPermission", valid)
            },
            new[]
            {
                Value("LocalService", "hMailServer"),
                Value("LaunchPermission", valid),
                Value("AccessPermission", [1, 2, 3])
            }
        };

        foreach (var values in cases)
        {
            var baseline = CreateReadbackWithBroker(values, values);
            var current = CreateReadbackWithBroker(values, values);
            var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
                WorkerSid,
                [SystemSid],
                current,
                baseline,
                new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

            Assert.IsFalse(result.Ready, result.Reason);
            Assert.AreEqual("broker-appid-permissions-not-explicit-and-exact", result.Reason);
        }
    }

    [TestMethod]
    public void RegistryReadbackRejectsUnsupportedInheritedAndDeniedAces()
    {
        var cases = new[]
        {
            SecurityDescriptorWithObjectAce(WorkerSid),
            SecurityDescriptorWithCallbackAce(WorkerSid),
            SecurityDescriptorWithSddl(
                $"D:(A;IO;CCDCSW;;;{WorkerSid})(A;;CCDCSW;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(A;ID;CCDCSW;;;{WorkerSid})(A;;CCDCSW;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(D;;CCDCSW;;;S-1-1-0)(A;;CCDCSW;;;{WorkerSid})(A;;CCDCSW;;;{SystemSid})")
        };

        foreach (var launchPermission in cases)
        {
            var validAccessPermission = SecurityDescriptor(WorkerSid, SystemSid);
            var values = new[]
            {
                Value("LocalService", "hMailServer"),
                Value("LaunchPermission", launchPermission),
                Value("AccessPermission", validAccessPermission)
            };
            var baseline = CreateReadbackWithBroker(values, values);
            var current = CreateReadbackWithBroker(values, values);
            var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
                WorkerSid,
                [SystemSid],
                current,
                baseline,
                new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

            Assert.IsFalse(result.Ready, result.Reason);
            Assert.AreEqual("broker-appid-permissions-not-explicit-and-exact", result.Reason);
        }
    }

    [TestMethod]
    public void RegistryReadbackRejectsChangedExistingApplicationSnapshot()
    {
        var baseline = CreateReadbackWithBroker(
            [],
            [],
            [Value("LocalService", "hMailServer")]);
        var changed = CreateReadbackWithBroker(
            [],
            [],
            [Value("LocalService", "other-service")]);

        var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            changed,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("installed-application-registration-changed", result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsWrongExistingApplicationPath()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var brokerValues = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var wrongPath = "Software\\Classes\\AppID\\{00000000-0000-0000-0000-000000000001}";
        var current = new WebAdminBrokerAppIdRegistryReadback(
            [
                Snapshot(RegistryView.Registry64, BrokerPath, brokerValues),
                Snapshot(RegistryView.Registry32, BrokerPath, brokerValues)
            ],
            [
                Snapshot(RegistryView.Registry64, wrongPath, [Value("LocalService", "hMailServer")]),
                Snapshot(RegistryView.Registry32, wrongPath, [Value("LocalService", "hMailServer")])
            ]);

        var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            current,
            current,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", result.Reason);
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

    private static WebAdminBrokerAppIdRegistryReadback CreateReadbackWithBroker(
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> broker64,
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> broker32,
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot>? existing = null)
    {
        existing ??= [Value("LocalService", "hMailServer")];
        var snapshots = new List<WebAdminBrokerRegistryKeySnapshot>
        {
            Snapshot(RegistryView.Registry64, ExistingApplicationPath, existing),
            Snapshot(RegistryView.Registry32, ExistingApplicationPath, existing)
        };
        if (broker64.Count > 0 || broker32.Count > 0)
        {
            snapshots.Add(Snapshot(RegistryView.Registry64, BrokerPath, broker64));
            snapshots.Add(Snapshot(RegistryView.Registry32, BrokerPath, broker32));
        }

        var reader = new FakeReader([.. snapshots]);
        return new WindowsWebAdminBrokerRegistryEvidenceSource(reader).Capture();
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

    private static byte[] SecurityDescriptor(params string[] allowedSids)
        => SecurityDescriptorWithAccess("CCDCSW", allowedSids);

    private static byte[] SecurityDescriptorWithAccess(string access, params string[] allowedSids)
    {
        return SecurityDescriptorWithSddl(
            "D:" + string.Concat(allowedSids.Select(sid => $"(A;;{access};;;{sid})")));
    }

    private static byte[] SecurityDescriptorWithSddl(string sddl)
    {
        var descriptor = new RawSecurityDescriptor(sddl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static byte[] SecurityDescriptorWithObjectAce(string sid)
    {
        var acl = new RawAcl(2, 1);
        acl.InsertAce(
            0,
            new ObjectAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask,
                new SecurityIdentifier(sid),
                ObjectAceFlags.ObjectAceTypePresent,
                Guid.NewGuid(),
                Guid.Empty,
                false,
                []));
        var descriptor = new RawSecurityDescriptor(
            ControlFlags.DiscretionaryAclPresent,
            owner: null,
            group: null,
            systemAcl: null,
            discretionaryAcl: acl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static byte[] SecurityDescriptorWithCallbackAce(string sid)
    {
        var acl = new RawAcl(2, 1);
        acl.InsertAce(
            0,
            new CommonAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask,
                new SecurityIdentifier(sid),
                isCallback: true,
                opaque: []));
        var descriptor = new RawSecurityDescriptor(
            ControlFlags.DiscretionaryAclPresent,
            owner: null,
            group: null,
            systemAcl: null,
            discretionaryAcl: acl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

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
