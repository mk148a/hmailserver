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

    private const string ApplicationClassPath =
        "Software\\Classes\\CLSID\\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}";

    private const string TypeLibraryRootPath =
        "Software\\Classes\\TypeLib\\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}";

    private const string TypeLibraryVersionPath =
        "Software\\Classes\\TypeLib\\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\\1.0";

    private const string ApplicationInterfacePath =
        "Software\\Classes\\Interface\\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}";

    private static readonly string[] InstalledApplicationGraphPaths =
    [
        "Software\\Classes\\hMailServer.Application.1",
        "Software\\Classes\\hMailServer.Application.1\\CLSID",
        "Software\\Classes\\hMailServer.Application",
        "Software\\Classes\\hMailServer.Application\\CLSID",
        "Software\\Classes\\hMailServer.Application\\CurVer",
        ApplicationClassPath,
        $"{ApplicationClassPath}\\ProgID",
        $"{ApplicationClassPath}\\VersionIndependentProgID",
        $"{ApplicationClassPath}\\Programmable",
        $"{ApplicationClassPath}\\LocalServer32",
        $"{ApplicationClassPath}\\TypeLib",
        ExistingApplicationPath,
        "Software\\Classes\\AppID\\hMailServer.EXE",
        TypeLibraryRootPath,
        TypeLibraryVersionPath,
        $"{TypeLibraryVersionPath}\\0",
        $"{TypeLibraryVersionPath}\\0\\win64",
        $"{TypeLibraryVersionPath}\\FLAGS",
        $"{TypeLibraryVersionPath}\\HELPDIR",
        ApplicationInterfacePath,
        $"{ApplicationInterfacePath}\\ProxyStubClsid32",
        $"{ApplicationInterfacePath}\\TypeLib"
    ];

    private static readonly string[] Registry32AbsentApplicationClassPaths =
    [
        ApplicationClassPath,
        $"{ApplicationClassPath}\\ProgID",
        $"{ApplicationClassPath}\\VersionIndependentProgID",
        $"{ApplicationClassPath}\\Programmable",
        $"{ApplicationClassPath}\\LocalServer32",
        $"{ApplicationClassPath}\\TypeLib"
    ];

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
        Assert.AreEqual(InstalledApplicationGraphPaths.Length * 2, readback.InstalledApplicationGraphViews.Count);
        CollectionAssert.AreEquivalent(
            InstalledApplicationGraphPaths,
            readback.InstalledApplicationGraphViews
                .Where(static snapshot => snapshot.View == RegistryView.Registry64)
                .Select(static snapshot => snapshot.KeyPath)
                .ToArray());
        CollectionAssert.AreEqual(
            new byte[] { 1, 2, 3 },
            readback.BrokerAppIdViews[0].Values.Single().RawBytes);
        Assert.AreEqual(InstalledApplicationGraphPaths.Length * 2 + 2, reader.ReadCount);
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
        Assert.AreEqual(InstalledApplicationGraphPaths.Length * 2 + 2, reader.ReadCount);
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
        var baseline = CreateReadbackWithBroker([], []);

        var missing64 = baseline with
        {
            BrokerAppIdViews = [Snapshot(RegistryView.Registry32, BrokerPath, values)]
        };
        var missing64Result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            missing64,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var readError = baseline with
        {
            BrokerAppIdViews =
            [
                new(RegistryView.Registry64, BrokerPath, false, [], "UnauthorizedAccessException"),
                new(RegistryView.Registry32, BrokerPath, false, [], ReadError: null)
            ]
        };
        var readErrorResult = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            readError,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var incompleteLegacy = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ExistingApplicationPath,
            new WebAdminBrokerRegistryKeySnapshot(
                RegistryView.Registry64,
                ExistingApplicationPath,
                false,
                [],
                "IOException"));
        var incompleteLegacyResult = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            incompleteLegacy,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        var readError32 = baseline with
        {
            BrokerAppIdViews =
            [
                Snapshot(RegistryView.Registry64, BrokerPath, values),
                new(RegistryView.Registry32, BrokerPath, false, [], "IOException")
            ]
        };
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
        var baseline = CreateReadbackWithBroker([], []);
        var current = baseline with
        {
            BrokerAppIdViews = [Snapshot(RegistryView.Registry64, BrokerPath, brokerValues)]
        };

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
        var wrongPath = "Software\\Classes\\AppID\\{00000000-0000-0000-0000-000000000001}";
        var valid = CreateReadyReadback();
        var existing = FindGraphSnapshot(valid, RegistryView.Registry64, ExistingApplicationPath);
        var current = ReplaceGraphSnapshot(
            valid,
            RegistryView.Registry64,
            ExistingApplicationPath,
            existing with { KeyPath = wrongPath });

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
    public void RegistryReadbackAcceptsCanonicalInstalledApplicationGraphShape()
    {
        var readback = CreateReadyReadback();
        var class64 = FindGraphSnapshot(readback, RegistryView.Registry64, ApplicationClassPath);
        var class32 = FindGraphSnapshot(readback, RegistryView.Registry32, ApplicationClassPath);

        var result = EvaluateRegistryReadback(readback, readback);

        Assert.IsFalse(class64.ContentEquals(class32));
        Assert.IsTrue(class64.Present);
        CollectionAssert.AreEquivalent(
            new[] { "ProgID", "VersionIndependentProgID", "Programmable", "LocalServer32", "TypeLib" },
            class64.DirectSubkeyNames.ToArray());
        Assert.IsTrue(Registry32AbsentApplicationClassPaths.All(path =>
            !FindGraphSnapshot(readback, RegistryView.Registry32, path).Present));
        Assert.IsTrue(result.Ready, result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsUnexpectedSubkeyAndCorruptedBaselineShape()
    {
        var canonical = CreateReadyReadback();
        var class64 = FindGraphSnapshot(canonical, RegistryView.Registry64, ApplicationClassPath);
        var unexpectedSubkey = ReplaceGraphSnapshot(
            canonical,
            RegistryView.Registry64,
            ApplicationClassPath,
            class64 with { DirectSubkeyNames = [.. class64.DirectSubkeyNames, "Unexpected"] });
        var corruptedBaseline = ReplaceGraphSnapshot(
            canonical,
            RegistryView.Registry64,
            ApplicationClassPath,
            class64 with { Present = false, Values = [], DirectSubkeyNames = [] });

        var unexpectedSubkeyResult = EvaluateRegistryReadback(unexpectedSubkey, unexpectedSubkey);
        var corruptedBaselineResult = EvaluateRegistryReadback(canonical, corruptedBaseline);

        Assert.IsFalse(unexpectedSubkeyResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", unexpectedSubkeyResult.Reason);
        Assert.IsFalse(corruptedBaselineResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", corruptedBaselineResult.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsMissingExtraAndEmptiedInstalledApplicationGraphKeys()
    {
        var baseline = CreateReadyReadback();
        var missing = baseline with
        {
            InstalledApplicationGraphViews = baseline.InstalledApplicationGraphViews
                .Where(snapshot => snapshot != FindGraphSnapshot(
                    baseline,
                    RegistryView.Registry32,
                    ApplicationClassPath))
                .ToArray()
        };
        var extra = baseline with
        {
            InstalledApplicationGraphViews =
            [
                .. baseline.InstalledApplicationGraphViews,
                Snapshot(
                    RegistryView.Registry64,
                    "Software\\Classes\\Unexpected",
                    [Value(string.Empty, "unexpected")])
            ]
        };
        var emptied = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            values: []);

        var missingResult = EvaluateRegistryReadback(missing, baseline);
        var extraResult = EvaluateRegistryReadback(extra, baseline);
        var emptiedResult = EvaluateRegistryReadback(emptied, baseline);

        Assert.IsFalse(missingResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", missingResult.Reason);
        Assert.IsFalse(extraResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", extraResult.Reason);
        Assert.IsFalse(emptiedResult.Ready);
        Assert.AreEqual("installed-application-registration-changed", emptiedResult.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsInstalledApplicationGraphValueNameKindAndBytesChanges()
    {
        var baseline = CreateReadyReadback();
        var original = FindGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath).Values.Single();
        var changedName = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            [new("ChangedName", original.Kind, original.RawBytes)]);
        var changedKind = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            [new(original.Name, RegistryValueKind.DWord, original.RawBytes)]);
        var changedBytes = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            [new(original.Name, original.Kind, [.. original.RawBytes, 1])]);

        foreach (var changed in new[] { changedName, changedKind, changedBytes })
        {
            var result = EvaluateRegistryReadback(changed, baseline);

            Assert.IsFalse(result.Ready);
            Assert.AreEqual("installed-application-registration-changed", result.Reason);
        }
    }

    [TestMethod]
    public void RegistryReadbackRejectsInstalledApplicationGraphPathAndReadErrorChanges()
    {
        var baseline = CreateReadyReadback();
        var original = FindGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath);
        var changedPath = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            original with { KeyPath = $"{ApplicationClassPath}\\Changed" });
        var readError = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            original with { Present = false, Values = [], ReadError = "IOException" });

        var changedPathResult = EvaluateRegistryReadback(changedPath, baseline);
        var readErrorResult = EvaluateRegistryReadback(readError, baseline);

        Assert.IsFalse(changedPathResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", changedPathResult.Reason);
        Assert.IsFalse(readErrorResult.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", readErrorResult.Reason);
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
        return CreateReadbackWithBroker([], [], values);
    }

    private static WebAdminBrokerAppIdRegistryReadback CreateReadbackWithBroker(
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> broker64,
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> broker32,
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot>? existing = null)
    {
        existing ??= [Value("LocalService", "hMailServer")];
        var snapshots = CreateInstalledApplicationGraph(existing);
        if (broker64.Count > 0 || broker32.Count > 0)
        {
            snapshots.Add(Snapshot(RegistryView.Registry64, BrokerPath, broker64));
            snapshots.Add(Snapshot(RegistryView.Registry32, BrokerPath, broker32));
        }

        var reader = new FakeReader([.. snapshots]);
        return new WindowsWebAdminBrokerRegistryEvidenceSource(reader).Capture();
    }

    private static WebAdminBrokerAppIdRegistryReadback CreateReadyReadback()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var brokerValues = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        return CreateReadbackWithBroker(brokerValues, brokerValues);
    }

    private static List<WebAdminBrokerRegistryKeySnapshot> CreateInstalledApplicationGraph(
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot>? existingApplicationValues = null)
    {
        existingApplicationValues ??= [Value("LocalService", "hMailServer")];
        var snapshots = new List<WebAdminBrokerRegistryKeySnapshot>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var path in InstalledApplicationGraphPaths)
            {
                var present = view != RegistryView.Registry32
                    || !Registry32AbsentApplicationClassPaths.Contains(path, StringComparer.Ordinal);
                IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> values = !present
                    || string.Equals(path, TypeLibraryRootPath, StringComparison.Ordinal)
                    || string.Equals(path, $"{TypeLibraryVersionPath}\\0", StringComparison.Ordinal)
                    || string.Equals(path, $"{ApplicationClassPath}\\Programmable", StringComparison.Ordinal)
                    ? []
                    : string.Equals(path, ExistingApplicationPath, StringComparison.Ordinal)
                        ? existingApplicationValues
                        : [Value(string.Empty, $"{view}:{path}")];
                snapshots.Add(new(view, path, present, values, ReadError: null)
                {
                    DirectSubkeyNames = present ? ExpectedDirectSubkeyNames(path) : []
                });
            }
        }

        return snapshots;
    }

    private static IReadOnlyList<string> ExpectedDirectSubkeyNames(string path) => path switch
    {
        "Software\\Classes\\hMailServer.Application.1" => ["CLSID"],
        "Software\\Classes\\hMailServer.Application" => ["CLSID", "CurVer"],
        ApplicationClassPath =>
            ["ProgID", "VersionIndependentProgID", "Programmable", "LocalServer32", "TypeLib"],
        TypeLibraryRootPath => ["1.0"],
        TypeLibraryVersionPath => ["0", "FLAGS", "HELPDIR"],
        $"{TypeLibraryVersionPath}\\0" => ["win64"],
        ApplicationInterfacePath => ["ProxyStubClsid32", "TypeLib"],
        _ => []
    };

    private static WebAdminBrokerAppIdPreflightResult EvaluateRegistryReadback(
        WebAdminBrokerAppIdRegistryReadback current,
        WebAdminBrokerAppIdRegistryReadback baseline) =>
        WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid],
            current,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

    private static WebAdminBrokerRegistryKeySnapshot FindGraphSnapshot(
        WebAdminBrokerAppIdRegistryReadback readback,
        RegistryView view,
        string path) =>
        readback.InstalledApplicationGraphViews.Single(snapshot =>
            snapshot.View == view
            && string.Equals(snapshot.KeyPath, path, StringComparison.Ordinal));

    private static WebAdminBrokerAppIdRegistryReadback ReplaceGraphSnapshot(
        WebAdminBrokerAppIdRegistryReadback readback,
        RegistryView view,
        string path,
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> values) =>
        ReplaceGraphSnapshot(
            readback,
            view,
            path,
            FindGraphSnapshot(readback, view, path) with { Values = values });

    private static WebAdminBrokerAppIdRegistryReadback ReplaceGraphSnapshot(
        WebAdminBrokerAppIdRegistryReadback readback,
        RegistryView view,
        string path,
        WebAdminBrokerRegistryKeySnapshot replacement) =>
        readback with
        {
            InstalledApplicationGraphViews = readback.InstalledApplicationGraphViews
                .Select(snapshot => snapshot.View == view
                    && string.Equals(snapshot.KeyPath, path, StringComparison.Ordinal)
                        ? replacement
                        : snapshot)
                .ToArray()
        };

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
