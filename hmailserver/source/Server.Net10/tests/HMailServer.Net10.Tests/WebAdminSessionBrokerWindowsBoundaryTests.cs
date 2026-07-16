using System.Runtime.Versioning;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WebAdminSessionBrokerWindowsBoundaryTests
{
    private const string WorkerSid = "S-1-5-82-2759919546-3181318411-3457700337-2112356574-3667061494";
    private const string SystemSid = "S-1-5-18";

    [TestMethod]
    public void WindowsCallerSourceDelegatesCapturedIdentityAndRevert()
    {
        var native = new FakeCallerNative(new(
            WorkerSid,
            WebAdminBrokerTokenType.Impersonation,
            WebAdminBrokerImpersonationLevel.Identification,
            IsRemote: false));
        var source = new WindowsWebAdminBrokerCallerIdentitySource(native);

        var identity = source.CaptureImpersonatedCaller();

        Assert.IsNotNull(identity);
        Assert.AreEqual(WorkerSid, identity!.Sid);
        Assert.AreEqual(1, native.CaptureCalls);
        Assert.IsTrue(source.RevertToSelf());
        Assert.AreEqual(1, native.RevertCalls);
    }

    [TestMethod]
    public void WindowsCallerSourceReturnsNoIdentityWhenNativeCaptureFails()
    {
        var native = new FakeCallerNative(null)
        {
            CaptureResult = false
        };
        var source = new WindowsWebAdminBrokerCallerIdentitySource(native);

        Assert.IsNull(source.CaptureImpersonatedCaller());
        Assert.AreEqual(1, native.CaptureCalls);
    }

    [TestMethod]
    public void AppIdPreflightRejectsMissingRegistrationAndExplicitPermissions()
    {
        var evidence = Evidence(
            brokerRegistrationPresent: false,
            launchPermission: new(false, [], []),
            accessPermission: new(false, [], []));

        var result = WebAdminSessionBrokerAppIdPreflight.Evaluate(
            WorkerSid,
            [SystemSid],
            evidence);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("broker-registration-missing", result.Reason);
    }

    [TestMethod]
    public void AppIdPreflightRejectsExtraOrDeniedPermissionSids()
    {
        var evidence = Evidence(
            brokerRegistrationPresent: true,
            launchPermission: Permission(WorkerSid, SystemSid, "S-1-5-32-544"),
            accessPermission: new(
                true,
                [WorkerSid, SystemSid],
                ["S-1-1-0"],
                ExplicitDacl: true,
                AllowedAccessMasks: new Dictionary<string, int>
                {
                    [WorkerSid] = WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask,
                    [SystemSid] = WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask
                }));

        var result = WebAdminSessionBrokerAppIdPreflight.Evaluate(
            WorkerSid,
            [SystemSid],
            evidence);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("broker-appid-permissions-not-explicit-and-exact", result.Reason);
    }

    [TestMethod]
    public void AppIdPreflightAcceptsOnlyFreshAppIdExactAclAndUnchangedApplication()
    {
        var evidence = Evidence(
            brokerRegistrationPresent: true,
            launchPermission: Permission(WorkerSid, SystemSid),
            accessPermission: Permission(SystemSid, WorkerSid));

        var result = WebAdminSessionBrokerAppIdPreflight.Evaluate(
            WorkerSid,
            [SystemSid],
            evidence);

        Assert.IsTrue(result.Ready);
        Assert.AreEqual("broker-only-appid-preflight-passed", result.Reason);
    }

    [TestMethod]
    public void AppIdPreflightRejectsInstalledApplicationAppIdReuse()
    {
        var evidence = Evidence(
            brokerRegistrationPresent: true,
            launchPermission: new(true, [WorkerSid, SystemSid], []),
            accessPermission: new(true, [WorkerSid, SystemSid], [])) with
        {
            AppId = LegacyComRegistrationManifest.AppId
        };

        var result = WebAdminSessionBrokerAppIdPreflight.Evaluate(
            WorkerSid,
            [SystemSid],
            evidence);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("installed-application-appid-reused", result.Reason);
    }

    [TestMethod]
    public void AppIdPreflightRejectsDuplicateNormalizedAccessMaskSids()
    {
        var duplicateMasks = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [SystemSid] = WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask,
            [SystemSid.ToLowerInvariant()] = WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask
        };
        var permission = new WebAdminBrokerPermissionEvidence(
            true,
            [WorkerSid, SystemSid],
            [],
            ExplicitDacl: true,
            AllowedAccessMasks: duplicateMasks);
        var evidence = Evidence(true, permission, permission);

        var result = WebAdminSessionBrokerAppIdPreflight.Evaluate(
            WorkerSid,
            [SystemSid],
            evidence);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("broker-appid-permissions-not-explicit-and-exact", result.Reason);
    }

    private static WebAdminBrokerAppIdEvidence Evidence(
        bool brokerRegistrationPresent,
        WebAdminBrokerPermissionEvidence launchPermission,
        WebAdminBrokerPermissionEvidence accessPermission) =>
        new(
            WebAdminSessionBrokerContract.AppId,
            brokerRegistrationPresent,
            "hMailServer",
            launchPermission,
            accessPermission,
            ExistingApplicationAppIdUnchanged: true);

    private static WebAdminBrokerPermissionEvidence Permission(params string[] sids) =>
        new(
            true,
            sids,
            [],
            ExplicitDacl: true,
            AllowedAccessMasks: sids.ToDictionary(
                static sid => sid,
                static _ => WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask,
                StringComparer.OrdinalIgnoreCase));

    private sealed class FakeCallerNative(WebAdminBrokerCallerIdentity? identity)
        : IWebAdminBrokerCallerIdentityNative
    {
        public bool CaptureResult { get; init; } = true;

        public int CaptureCalls { get; private set; }

        public int RevertCalls { get; private set; }

        public bool TryCaptureCaller(out WebAdminBrokerCallerIdentity captured)
        {
            CaptureCalls++;
            captured = identity!;
            return CaptureResult && identity is not null;
        }

        public bool RevertToSelf()
        {
            RevertCalls++;
            return true;
        }
    }
}
