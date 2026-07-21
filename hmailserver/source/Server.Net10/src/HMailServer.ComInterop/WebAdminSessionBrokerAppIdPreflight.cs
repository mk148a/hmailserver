using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record WebAdminBrokerPermissionEvidence(
    bool Present,
    IReadOnlyCollection<string> AllowedSids,
    IReadOnlyCollection<string> DeniedSids,
    bool ExplicitDacl = false,
    IReadOnlyDictionary<string, int>? AllowedAccessMasks = null);

[ComVisible(false)]
public sealed record WebAdminBrokerAppIdEvidence(
    string AppId,
    bool BrokerRegistrationPresent,
    string? LocalService,
    WebAdminBrokerPermissionEvidence LaunchPermission,
    WebAdminBrokerPermissionEvidence AccessPermission,
    bool ExistingApplicationAppIdUnchanged);

[ComVisible(false)]
public sealed record WebAdminBrokerAppIdPreflightResult(bool Ready, string Reason);

[ComVisible(false)]
public static class WebAdminSessionBrokerAppIdPreflight
{
    public const int RequiredLocalBrokerAccessMask = 0x0B;

    private const int KeySetValueMask = 0x00000002;
    private const int KeyCreateSubKeyMask = 0x00000004;
    private const int KeyCreateLinkMask = 0x00000020;
    private const int DeleteMask = 0x00010000;
    private const int WriteDacMask = 0x00040000;
    private const int WriteOwnerMask = 0x00080000;
    private const int GenericWriteMask = 0x40000000;
    private const int GenericAllMask = 0x10000000;
    private const int MaximumAllowedMask = 0x02000000;
    private const string TrustedRegistryOwnerSid = "S-1-5-18";
    private static readonly string[] TrustedRegistryWriterSids = [TrustedRegistryOwnerSid];
    private const int BrokerKeyWriteTamperMask =
        KeySetValueMask
        | KeyCreateSubKeyMask
        | KeyCreateLinkMask
        | DeleteMask
        | WriteDacMask
        | WriteOwnerMask
        | GenericWriteMask
        | GenericAllMask
        | MaximumAllowedMask;

    [SupportedOSPlatform("windows")]
    public static WebAdminBrokerAppIdPreflightResult EvaluateFromRegistryReadback(
        string configuredWorkerSid,
        IReadOnlyCollection<string> requiredServiceSids,
        WebAdminBrokerAppIdRegistryReadback current,
        WebAdminBrokerAppIdRegistryReadback baseline,
        WindowsWebAdminBrokerRegistryEvidenceSource registryEvidenceSource,
        string requiredLocalService = "hMailServer")
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(registryEvidenceSource);

        if (!registryEvidenceSource.TryBuildPreflightEvidence(
                current,
                baseline,
                out var evidence,
                out var reason))
        {
            return Fail(reason);
        }

        if (reason == "broker-registration-missing")
        {
            return Fail(reason);
        }

        var result = Evaluate(configuredWorkerSid, requiredServiceSids, evidence, requiredLocalService);
        if (!result.Ready)
        {
            return result;
        }

        if (!HasValidBrokerKeyDacls(
                current.BrokerAppIdViews,
                configuredWorkerSid))
        {
            return Fail("broker-registry-key-dacl-policy-rejected");
        }

        return result;
    }

    public static WebAdminBrokerAppIdPreflightResult Evaluate(
        string configuredWorkerSid,
        IReadOnlyCollection<string> requiredServiceSids,
        WebAdminBrokerAppIdEvidence evidence,
        string requiredLocalService = "hMailServer")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredWorkerSid);
        ArgumentNullException.ThrowIfNull(requiredServiceSids);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredLocalService);

        if (!TryNormalizeSid(configuredWorkerSid, out var workerSid))
        {
            return Fail("configured-worker-sid-invalid");
        }

        var expectedSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            workerSid
        };
        foreach (var serviceSid in requiredServiceSids)
        {
            if (!TryNormalizeSid(serviceSid, out var normalizedServiceSid))
            {
                return Fail("required-service-sid-invalid");
            }

            expectedSids.Add(normalizedServiceSid);
        }

        if (!Guid.TryParse(evidence.AppId, out var appId))
        {
            return Fail("broker-appid-mismatch");
        }

        if (appId == new Guid(LegacyComRegistrationManifest.AppId))
        {
            return Fail("installed-application-appid-reused");
        }

        if (appId != new Guid(WebAdminSessionBrokerContract.AppId))
        {
            return Fail("broker-appid-mismatch");
        }

        if (!evidence.BrokerRegistrationPresent)
        {
            return Fail("broker-registration-missing");
        }

        if (!StringComparer.Ordinal.Equals(evidence.LocalService, requiredLocalService))
        {
            return Fail("broker-local-service-mismatch");
        }

        if (!evidence.ExistingApplicationAppIdUnchanged)
        {
            return Fail("installed-application-registration-changed");
        }

        if (!HasExactPermission(evidence.LaunchPermission, expectedSids)
            || !HasExactPermission(evidence.AccessPermission, expectedSids))
        {
            return Fail("broker-appid-permissions-not-explicit-and-exact");
        }

        return new WebAdminBrokerAppIdPreflightResult(true, "broker-only-appid-preflight-passed");
    }

    private static bool HasExactPermission(
        WebAdminBrokerPermissionEvidence permission,
        IReadOnlySet<string> expectedSids)
    {
        if (!permission.Present
            || !permission.ExplicitDacl
            || permission.DeniedSids.Count != 0
            || NormalizeSidSet(permission.AllowedSids) is not { } allowedSids
            || !allowedSids.SetEquals(expectedSids)
            || permission.AllowedAccessMasks is not { } accessMasks
            || NormalizeAccessMasks(accessMasks) is not { } normalizedMasks)
        {
            return false;
        }

        var maskSids = new HashSet<string>(normalizedMasks.Keys, StringComparer.OrdinalIgnoreCase);
        return maskSids.SetEquals(expectedSids)
            && maskSids.SetEquals(allowedSids)
            && normalizedMasks.Values.All(static mask =>
                mask == RequiredLocalBrokerAccessMask);
    }

    private static Dictionary<string, int>? NormalizeAccessMasks(
        IReadOnlyDictionary<string, int> accessMasks)
    {
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in accessMasks)
        {
            if (!TryNormalizeSid(pair.Key, out var normalizedSid)
                || !normalized.TryAdd(normalizedSid, pair.Value))
            {
                return null;
            }
        }

        return normalized;
    }

    private static HashSet<string>? NormalizeSidSet(IReadOnlyCollection<string> sids)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sid in sids)
        {
            if (!TryNormalizeSid(sid, out var normalizedSid))
            {
                return null;
            }

            normalized.Add(normalizedSid);
        }

        return normalized;
    }

    [SupportedOSPlatform("windows")]
    private static bool HasValidBrokerKeyDacls(
        IReadOnlyCollection<WebAdminBrokerRegistryKeySnapshot> snapshots,
        string configuredWorkerSid)
    {
        if (!TryNormalizeSid(configuredWorkerSid, out var workerSid)
            || !TryNormalizeSid(TrustedRegistryOwnerSid, out var trustedOwnerSid)
            || NormalizeSidSet(TrustedRegistryWriterSids) is not { } trustedWriterSids)
        {
            return false;
        }

        var presentSnapshots = snapshots.Where(static snapshot => snapshot.Present).ToArray();
        return presentSnapshots.Length == 2
            && presentSnapshots.All(snapshot => IsValidBrokerKeyDacl(
                snapshot,
                workerSid,
                trustedOwnerSid,
                trustedWriterSids));
    }

    [SupportedOSPlatform("windows")]
    private static bool IsValidBrokerKeyDacl(
        WebAdminBrokerRegistryKeySnapshot snapshot,
        string workerSid,
        string trustedOwnerSid,
        IReadOnlySet<string> trustedRegistryWriterSids)
    {
        if (snapshot.DaclReadError is not null
            || snapshot.OwnerReadError is not null
            || snapshot.RawDaclBytes is not { Length: > 0 })
        {
            return false;
        }

        try
        {
            var descriptor = new RawSecurityDescriptor(snapshot.RawDaclBytes, 0);
            if (!descriptor.ControlFlags.HasFlag(ControlFlags.SelfRelative)
                || !descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclPresent)
                || !descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected)
                || descriptor.DiscretionaryAcl is not { Count: > 0 } dacl)
            {
                return false;
            }

            if (!TryNormalizeSid(snapshot.OwnerSid ?? string.Empty, out var ownerSid)
                || !StringComparer.OrdinalIgnoreCase.Equals(ownerSid, trustedOwnerSid))
            {
                return false;
            }

            foreach (var ace in dacl)
            {
                if (ace is not CommonAce commonAce
                    || commonAce.IsCallback
                    || commonAce.IsInherited
                    || commonAce.AceFlags.HasFlag(AceFlags.InheritOnly)
                    || commonAce.AceFlags.HasFlag(AceFlags.ObjectInherit)
                    || commonAce.AceFlags.HasFlag(AceFlags.ContainerInherit)
                    || commonAce.AceFlags.HasFlag(AceFlags.NoPropagateInherit)
                    || commonAce.AceQualifier != AceQualifier.AccessAllowed)
                {
                    return false;
                }

                var sid = new SecurityIdentifier(commonAce.SecurityIdentifier.Value).Value;
                if ((commonAce.AccessMask & BrokerKeyWriteTamperMask) != 0
                    && (StringComparer.OrdinalIgnoreCase.Equals(sid, workerSid)
                        || !trustedRegistryWriterSids.Contains(sid)))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or IndexOutOfRangeException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryNormalizeSid(string sid, out string normalizedSid)
    {
        normalizedSid = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            normalizedSid = new SecurityIdentifier(sid).Value;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static WebAdminBrokerAppIdPreflightResult Fail(string reason) =>
        new(false, reason);
}
