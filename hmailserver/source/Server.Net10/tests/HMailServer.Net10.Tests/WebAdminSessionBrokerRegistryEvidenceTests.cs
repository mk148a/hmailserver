using System.ComponentModel;
using System.Runtime.InteropServices;
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
    private const string AlternateServiceSid = "S-1-5-21-1000-2000-3000-4000";

    private const string ExistingApplicationPath =
        $"Software\\Classes\\AppID\\{LegacyComRegistrationManifest.AppId}";

    private const string BrokerPath =
        $"Software\\Classes\\AppID\\{WebAdminSessionBrokerContract.AppId}";

    private const string ApplicationClassPath =
        "Software\\Classes\\CLSID\\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}";

    private const string ApplicationClassId =
        "{D6567EF8-0A6C-48E7-9288-A2463123C2F3}";

    private const string ExistingAppId = LegacyComRegistrationManifest.AppId;

    private const string TypeLibraryId = LegacyComRegistrationManifest.TypeLibraryId;

    private const string TypeLibraryRootPath =
        "Software\\Classes\\TypeLib\\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}";

    private const string TypeLibraryVersionPath =
        "Software\\Classes\\TypeLib\\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\\1.0";

    private const string ApplicationInterfacePath =
        "Software\\Classes\\Interface\\{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}";

    private const string TestModulePath =
        @"C:\hMailServer57-Test\Bin\hMailServer.exe";

    private const string TestModuleDirectory =
        @"C:\hMailServer57-Test\Bin";

    private static readonly byte[] TestDaclBytes =
        SecurityDescriptorWithSddl("D:(A;;FA;;;SY)");

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
    public void CapturePreservesRawDaclBytesAndDaclReadErrors()
    {
        var daclBytes = new byte[] { 1, 2, 3, 4 };
        var reader = new FakeReader(
            Snapshot(RegistryView.Registry64, BrokerPath, []) with
            {
                RawDaclBytes = daclBytes,
                DaclReadError = null
            },
            Snapshot(RegistryView.Registry32, BrokerPath, []) with
            {
                RawDaclBytes = null,
                DaclReadError = "UnauthorizedAccessException"
            });

        var readback = new WindowsWebAdminBrokerRegistryEvidenceSource(reader).Capture();

        CollectionAssert.AreEqual(daclBytes, readback.BrokerAppIdViews[0].RawDaclBytes);
        Assert.IsNull(readback.BrokerAppIdViews[0].DaclReadError);
        Assert.IsNull(readback.BrokerAppIdViews[1].RawDaclBytes);
        Assert.AreEqual("UnauthorizedAccessException", readback.BrokerAppIdViews[1].DaclReadError);
    }

    [TestMethod]
    public void RegistryReadbackRejectsInstalledApplicationDaclChanges()
    {
        var baseline = CreateReadyReadback();
        var original = FindGraphSnapshot(baseline, RegistryView.Registry64, ApplicationClassPath);
        var changed = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            original with { RawDaclBytes = [.. original.RawDaclBytes!, 1] });

        var result = EvaluateRegistryReadback(changed, baseline);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("installed-application-registration-changed", result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsMissingOrUnreadableInstalledApplicationDacl()
    {
        var baseline = CreateReadyReadback();
        var original = FindGraphSnapshot(baseline, RegistryView.Registry64, ApplicationClassPath);
        var missing = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            original with { RawDaclBytes = null });
        var readError = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            original with { DaclReadError = "UnauthorizedAccessException" });
        var empty = ReplaceGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath,
            original with { RawDaclBytes = [] });

        foreach (var current in new[] { missing, readError, empty })
        {
            var result = EvaluateRegistryReadback(current, baseline);

            Assert.IsFalse(result.Ready);
            Assert.AreEqual("installed-application-appid-readback-incomplete", result.Reason);
        }
    }

    [TestMethod]
    [TestCategory("WindowsRegistryIntegration")]
    public void OptInNativeRegistryIntegrationCapturesLegacyOwnerAndDaclAndMissingBroker()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("HMAILSERVER_NET10_RUN_WINDOWS_REGISTRY_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set HMAILSERVER_NET10_RUN_WINDOWS_REGISTRY_INTEGRATION=1 to run the read-only registry integration test.");
        }

        var readback = new WindowsWebAdminBrokerRegistryEvidenceSource().Capture();
        var captured = new[] { RegistryView.Registry64, RegistryView.Registry32 }
            .Select(view => readback.ExistingApplicationAppIdViews.Single(snapshot =>
                snapshot.View == view
                && string.Equals(snapshot.KeyPath, ExistingApplicationPath, StringComparison.Ordinal)))
            .ToArray();
        if (captured.Any(static snapshot => !snapshot.Present))
        {
            Assert.Inconclusive($"Known graph key is not installed: {ExistingApplicationPath}");
        }

        foreach (var snapshot in captured)
        {
            Assert.IsTrue(snapshot.Present, snapshot.ReadError ?? "Known graph key was reported absent.");
            Assert.IsNull(snapshot.ReadError);
            Assert.IsNull(snapshot.DaclReadError);
            Assert.IsNull(snapshot.OwnerReadError);
            Assert.IsNotNull(snapshot.RawDaclBytes);
            Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.OwnerSid));

            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, snapshot.View);
            using var key = baseKey.OpenSubKey(ExistingApplicationPath, writable: false);
            Assert.IsNotNull(key);

            Assert.AreEqual(ReadNativeOwnerSid(key!), snapshot.OwnerSid);
            CollectionAssert.AreEqual(ReadNativeDacl(key!), snapshot.RawDaclBytes);
        }

        Assert.AreEqual(captured[0].OwnerSid, captured[1].OwnerSid);
        CollectionAssert.AreEqual(captured[0].RawDaclBytes, captured[1].RawDaclBytes);

        Assert.AreEqual(2, readback.BrokerAppIdViews.Count);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var broker = readback.BrokerAppIdViews.Single(snapshot => snapshot.View == view);
            Assert.IsFalse(broker.Present);
            Assert.IsNull(broker.ReadError);
        }
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
    public void RegistryReadbackRejectsMissingEmptyAndUnreadableBrokerKeyDacl()
    {
        var baseline = CreateReadyReadback();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var broker = FindBrokerSnapshot(baseline, view);
            var cases = new[]
            {
                ReplaceBrokerSnapshot(baseline, broker with { RawDaclBytes = null }),
                ReplaceBrokerSnapshot(baseline, broker with { RawDaclBytes = [] }),
                ReplaceBrokerSnapshot(baseline, broker with { DaclReadError = "UnauthorizedAccessException" })
            };

            foreach (var current in cases)
            {
                var result = EvaluateRegistryReadback(current, baseline);

                Assert.IsFalse(result.Ready, result.Reason);
                Assert.AreEqual("broker-registry-key-dacl-incomplete", result.Reason);
            }
        }
    }

    [TestMethod]
    public void RegistryReadbackAcceptsExplicitReadOnlyAndTrustedSystemWriteBrokerKeyDacl()
    {
        var baseline = CreateReadyReadback();
        var validDaclBytes = SecurityDescriptorWithSddl(
            $"D:(A;;0x20019;;;BU)(A;;FA;;;{SystemSid})");
        var current = ReplaceBrokerSnapshot(
            ReplaceBrokerSnapshot(
                baseline,
                FindBrokerSnapshot(baseline, RegistryView.Registry64) with
                {
                    RawDaclBytes = [.. validDaclBytes]
                }),
            FindBrokerSnapshot(baseline, RegistryView.Registry32) with
            {
                RawDaclBytes = [.. validDaclBytes]
            });

        var result = EvaluateRegistryReadback(current, baseline);

        Assert.IsTrue(result.Ready, result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsConfiguredServiceSidWithBrokerKeyWriteAccess()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid, AlternateServiceSid);
        var brokerValues = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var baseline = CreateReadbackWithBroker(brokerValues, brokerValues);
        var daclBytes = SecurityDescriptorWithSddl(
            $"D:(A;;FA;;;{AlternateServiceSid})(A;;0x20019;;;BU)(A;;FA;;;{SystemSid})");
        var current = ReplaceBrokerSnapshot(
            ReplaceBrokerSnapshot(
                baseline,
                FindBrokerSnapshot(baseline, RegistryView.Registry64) with
                {
                    RawDaclBytes = [.. daclBytes]
                }),
            FindBrokerSnapshot(baseline, RegistryView.Registry32) with
            {
                RawDaclBytes = [.. daclBytes]
            });

        var result = WebAdminSessionBrokerAppIdPreflight.EvaluateFromRegistryReadback(
            WorkerSid,
            [SystemSid, AlternateServiceSid],
            current,
            baseline,
            new WindowsWebAdminBrokerRegistryEvidenceSource(new FakeReader()));

        Assert.IsFalse(result.Ready, result.Reason);
        Assert.AreEqual("broker-registry-key-dacl-policy-rejected", result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsMalformedNonSelfRelativeAndUnsafeBrokerKeyDacls()
    {
        var baseline = CreateReadyReadback();
        var cases = new[]
        {
            new byte[] { 0x01, 0x7F, 0xFF },
            NonSelfRelativeSecurityDescriptor(),
            SecurityDescriptorWithoutDacl(),
            SecurityDescriptorWithSddl("D:"),
            SecurityDescriptorWithSddl(
                $"D:(D;;FA;;;BU)(A;;FA;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(A;ID;0x20019;;;BU)(A;;FA;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(A;IO;0x20019;;;BU)(A;;FA;;;{SystemSid})"),
            UnprotectedSecurityDescriptor(
                $"D:(A;;0x20019;;;BU)(A;;FA;;;{SystemSid})"),
            SecurityDescriptorWithCallbackAce(WorkerSid),
            SecurityDescriptorWithObjectAce(WorkerSid),
            SecurityDescriptorWithSddl(
                $"D:(A;;FA;;;{WorkerSid})(A;;FA;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(A;;FA;;;BU)(A;;FA;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(A;;FA;;;WD)(A;;FA;;;{SystemSid})"),
            SecurityDescriptorWithSddl(
                $"D:(A;;0x20;;;BU)(A;;FA;;;{SystemSid})")
        };

        foreach (var daclBytes in cases)
        {
            var current = ReplaceBrokerSnapshot(
                ReplaceBrokerSnapshot(
                    baseline,
                    FindBrokerSnapshot(baseline, RegistryView.Registry64) with
                    {
                        RawDaclBytes = [.. daclBytes]
                    }),
                FindBrokerSnapshot(baseline, RegistryView.Registry32) with
                {
                    RawDaclBytes = [.. daclBytes]
                });

            var result = EvaluateRegistryReadback(current, baseline);

            Assert.IsFalse(result.Ready, result.Reason);
            Assert.AreEqual("broker-registry-key-dacl-policy-rejected", result.Reason);
        }
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
        Assert.AreEqual("installed-application-appid-readback-incomplete", result.Reason);
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
        Assert.AreEqual("installed-application-appid-readback-incomplete", emptiedResult.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsInstalledApplicationGraphValueNameKindAndBytesChanges()
    {
        var baseline = CreateReadyReadback();
        var original = FindGraphSnapshot(
            baseline,
            RegistryView.Registry64,
            ApplicationClassPath).Values.Single(value => string.IsNullOrEmpty(value.Name));
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
            Assert.AreEqual("installed-application-appid-readback-incomplete", result.Reason);
        }
    }

    [TestMethod]
    public void RegistryReadbackRejectsStableNonCanonicalInstalledApplicationValues()
    {
        var canonical = CreateReadyReadback();
        var applicationClass = FindGraphSnapshot(canonical, RegistryView.Registry64, ApplicationClassPath);
        var changed = ReplaceGraphSnapshot(
            canonical,
            RegistryView.Registry64,
            ApplicationClassPath,
            applicationClass with { Values = [Value(string.Empty, "Wrong Application Class")] });

        var result = EvaluateRegistryReadback(changed, changed);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("installed-application-appid-readback-incomplete", result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackPreservesAdditionalExistingApplicationPolicyValues()
    {
        var permission = SecurityDescriptor(WorkerSid, SystemSid);
        var brokerValues = new[]
        {
            Value("LocalService", "hMailServer"),
            Value("LaunchPermission", permission),
            Value("AccessPermission", permission)
        };
        var readback = CreateReadbackWithBroker(
            brokerValues,
            brokerValues,
            [
                Value(string.Empty, "hMailServer"),
                Value("LocalService", "hMailServer"),
                Value("LaunchPermission", permission)
            ]);

        var result = EvaluateRegistryReadback(readback, readback);

        Assert.IsTrue(result.Ready, result.Reason);
    }

    [TestMethod]
    public void RegistryReadbackRejectsMalformedInstalledApplicationModulePaths()
    {
        var canonical = CreateReadyReadback();
        var localServerPath =
            $"{ApplicationClassPath}\\LocalServer32";
        var typeLibraryModulePath =
            $"{TypeLibraryVersionPath}\\0\\win64";
        var helpDirectoryPath =
            $"{TypeLibraryVersionPath}\\HELPDIR";
        var cases = new[]
        {
            ReplaceGraphSnapshot(
                canonical,
                RegistryView.Registry64,
                localServerPath,
                [Value(string.Empty, TestModulePath)]),
            ReplaceGraphSnapshot(
                canonical,
                RegistryView.Registry64,
                typeLibraryModulePath,
                [Value(string.Empty, $"\"{TestModulePath}\"")]),
            ReplaceGraphSnapshot(
                canonical,
                RegistryView.Registry64,
                helpDirectoryPath,
                [Value(string.Empty, "relative-bin")]),
            ReplaceGraphSnapshot(
                canonical,
                RegistryView.Registry64,
                typeLibraryModulePath,
                [Value(string.Empty, string.Empty)])
        };

        foreach (var changed in cases)
        {
            var result = EvaluateRegistryReadback(changed, changed);

            Assert.IsFalse(result.Ready);
            Assert.AreEqual("installed-application-appid-readback-incomplete", result.Reason);
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
        existing = NormalizeExistingApplicationValues(existing);
        var snapshots = CreateInstalledApplicationGraph(existing);
        if (broker64.Count > 0 || broker32.Count > 0)
        {
            snapshots.Add(Snapshot(RegistryView.Registry64, BrokerPath, broker64) with
            {
                RawDaclBytes = [.. TestDaclBytes],
                OwnerSid = SystemSid
            });
            snapshots.Add(Snapshot(RegistryView.Registry32, BrokerPath, broker32) with
            {
                RawDaclBytes = [.. TestDaclBytes],
                OwnerSid = SystemSid
            });
        }

        var reader = new FakeReader([.. snapshots]);
        return new WindowsWebAdminBrokerRegistryEvidenceSource(reader).Capture();
    }

    private static IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> NormalizeExistingApplicationValues(
        IReadOnlyList<WebAdminBrokerRegistryValueSnapshot>? values)
    {
        var normalized = new List<WebAdminBrokerRegistryValueSnapshot>
        {
            Value(string.Empty, "hMailServer"),
            Value("LocalService", "hMailServer")
        };
        foreach (var value in values ?? [])
        {
            var index = normalized.FindIndex(existing =>
                string.Equals(existing.Name, value.Name, StringComparison.Ordinal));
            if (index >= 0)
            {
                normalized[index] = value;
            }
            else
            {
                normalized.Add(value);
            }
        }

        return normalized;
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
        existingApplicationValues ??= [
            Value(string.Empty, "hMailServer"),
            Value("LocalService", "hMailServer")
        ];
        var snapshots = new List<WebAdminBrokerRegistryKeySnapshot>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var path in InstalledApplicationGraphPaths)
            {
                var present = view != RegistryView.Registry32
                    || !Registry32AbsentApplicationClassPaths.Contains(path, StringComparer.Ordinal);
                IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> values = !present
                    ? []
                    : string.Equals(path, ExistingApplicationPath, StringComparison.Ordinal)
                        ? existingApplicationValues
                        : CanonicalValues(view, path);
                snapshots.Add(new(view, path, present, values, ReadError: null)
                {
                    DirectSubkeyNames = present ? ExpectedDirectSubkeyNames(path) : [],
                    RawDaclBytes = present ? [.. TestDaclBytes] : null,
                    DaclReadError = null
                });
            }
        }

        return snapshots;
    }

    private static IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> CanonicalValues(
        RegistryView view,
        string path) => path switch
    {
        "Software\\Classes\\hMailServer.Application.1" => [Value(string.Empty, "Application Class")],
        "Software\\Classes\\hMailServer.Application.1\\CLSID" =>
            [Value(string.Empty, ApplicationClassId)],
        "Software\\Classes\\hMailServer.Application" => [Value(string.Empty, "Application Class")],
        "Software\\Classes\\hMailServer.Application\\CLSID" =>
            [Value(string.Empty, ApplicationClassId)],
        "Software\\Classes\\hMailServer.Application\\CurVer" =>
            [Value(string.Empty, "hMailServer.Application.1")],
        ApplicationClassPath =>
            [Value(string.Empty, "Application Class"), Value("AppID", ExistingAppId)],
        $"{ApplicationClassPath}\\ProgID" =>
            [Value(string.Empty, "hMailServer.Application.1")],
        $"{ApplicationClassPath}\\VersionIndependentProgID" =>
            [Value(string.Empty, "hMailServer.Application")],
        $"{ApplicationClassPath}\\Programmable" => [],
        $"{ApplicationClassPath}\\LocalServer32" =>
            [Value(string.Empty, $"\"{TestModulePath}\"")],
        $"{ApplicationClassPath}\\TypeLib" =>
            [Value(string.Empty, TypeLibraryId)],
        $"Software\\Classes\\AppID\\{ExistingAppId}" =>
            [Value(string.Empty, "hMailServer"), Value("LocalService", "hMailServer")],
        "Software\\Classes\\AppID\\hMailServer.EXE" =>
            [Value("AppID", ExistingAppId)],
        TypeLibraryRootPath => [],
        TypeLibraryVersionPath => [Value(string.Empty, "hMailServer Type Library")],
        $"{TypeLibraryVersionPath}\\0" => [],
        $"{TypeLibraryVersionPath}\\0\\win64" => [Value(string.Empty, TestModulePath)],
        $"{TypeLibraryVersionPath}\\FLAGS" => [Value(string.Empty, "0")],
        $"{TypeLibraryVersionPath}\\HELPDIR" => [Value(string.Empty, TestModuleDirectory)],
        ApplicationInterfacePath => [Value(string.Empty, "IInterfaceApplication")],
        $"{ApplicationInterfacePath}\\ProxyStubClsid32" =>
            [Value(string.Empty, "{00020424-0000-0000-C000-000000000046}")],
        $"{ApplicationInterfacePath}\\TypeLib" =>
            [Value(string.Empty, TypeLibraryId), Value("Version", "1.0")],
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, $"Unknown graph path for {view}.")
    };

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

    private static WebAdminBrokerRegistryKeySnapshot FindBrokerSnapshot(
        WebAdminBrokerAppIdRegistryReadback readback,
        RegistryView view) =>
        readback.BrokerAppIdViews.Single(snapshot => snapshot.View == view);

    private static WebAdminBrokerAppIdRegistryReadback ReplaceBrokerSnapshot(
        WebAdminBrokerAppIdRegistryReadback readback,
        WebAdminBrokerRegistryKeySnapshot replacement) =>
        readback with
        {
            BrokerAppIdViews = readback.BrokerAppIdViews
                .Select(snapshot => snapshot.View == replacement.View
                    && string.Equals(snapshot.KeyPath, replacement.KeyPath, StringComparison.Ordinal)
                        ? replacement
                        : snapshot)
                .ToArray()
        };

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
        var parsed = new RawSecurityDescriptor(sddl);
        var descriptor = new RawSecurityDescriptor(
            parsed.ControlFlags | ControlFlags.DiscretionaryAclProtected,
            parsed.Owner,
            parsed.Group,
            parsed.SystemAcl,
            parsed.DiscretionaryAcl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static byte[] UnprotectedSecurityDescriptor(string sddl)
    {
        var parsed = new RawSecurityDescriptor(sddl);
        var descriptor = new RawSecurityDescriptor(
            parsed.ControlFlags & ~ControlFlags.DiscretionaryAclProtected,
            parsed.Owner,
            parsed.Group,
            parsed.SystemAcl,
            parsed.DiscretionaryAcl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static byte[] SecurityDescriptorWithoutDacl()
    {
        var descriptor = new RawSecurityDescriptor(
            ControlFlags.SelfRelative,
            owner: new SecurityIdentifier(SystemSid),
            group: new SecurityIdentifier(SystemSid),
            systemAcl: null,
            discretionaryAcl: null);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static byte[] NonSelfRelativeSecurityDescriptor()
    {
        var bytes = SecurityDescriptorWithSddl($"D:(A;;FA;;;{SystemSid})");
        bytes[3] = (byte)(bytes[3] & ~0x80);
        return bytes;
    }

    private static byte[] ReadNativeDacl(RegistryKey key)
    {
        return ReadNativeSecurityInformation(key, securityInformation: 0x00000004);
    }

    private static string ReadNativeOwnerSid(RegistryKey key)
    {
        var descriptor = new RawSecurityDescriptor(
            ReadNativeSecurityInformation(key, securityInformation: 0x00000001),
            0);
        return descriptor.Owner?.Value
            ?? throw new InvalidDataException("Registry key owner is missing.");
    }

    private static byte[] ReadNativeSecurityInformation(RegistryKey key, uint securityInformation)
    {
        const int errorInsufficientBuffer = 122;

        uint byteCount = 0;
        var result = RegGetKeySecurity(
            key.Handle.DangerousGetHandle(),
            securityInformation,
            null,
            ref byteCount);
        if (result != 0 && result != errorInsufficientBuffer)
        {
            throw new Win32Exception(result);
        }

        var bytes = new byte[checked((int)byteCount)];
        result = RegGetKeySecurity(
            key.Handle.DangerousGetHandle(),
            securityInformation,
            bytes,
            ref byteCount);
        if (result != 0)
        {
            throw new Win32Exception(result);
        }

        if (byteCount != bytes.Length)
        {
            Array.Resize(ref bytes, checked((int)byteCount));
        }

        return bytes;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegGetKeySecurity(
        IntPtr hKey,
        uint securityInformation,
        byte[]? pSecurityDescriptor,
        ref uint lpcbSecurityDescriptor);

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
