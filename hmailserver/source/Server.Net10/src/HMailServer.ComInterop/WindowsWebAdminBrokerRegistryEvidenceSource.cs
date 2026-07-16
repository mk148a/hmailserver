using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record WebAdminBrokerRegistryValueSnapshot(
    string Name,
    RegistryValueKind Kind,
    byte[] RawBytes)
{
    public bool BytewiseEquals(WebAdminBrokerRegistryValueSnapshot other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal)
        && Kind == other.Kind
        && RawBytes.AsSpan().SequenceEqual(other.RawBytes);
}

[ComVisible(false)]
public sealed record WebAdminBrokerRegistryKeySnapshot(
    RegistryView View,
    string KeyPath,
    bool Present,
    IReadOnlyList<WebAdminBrokerRegistryValueSnapshot> Values,
    string? ReadError)
{
    public bool BytewiseEquals(WebAdminBrokerRegistryKeySnapshot other)
    {
        return View == other.View && ContentEquals(other);
    }

    public bool ContentEquals(WebAdminBrokerRegistryKeySnapshot other)
    {
        if (!string.Equals(KeyPath, other.KeyPath, StringComparison.Ordinal)
            || Present != other.Present
            || !string.Equals(ReadError, other.ReadError, StringComparison.Ordinal)
            || Values.Count != other.Values.Count)
        {
            return false;
        }

        var left = Values.OrderBy(static value => value.Name, StringComparer.Ordinal).ToArray();
        var right = other.Values.OrderBy(static value => value.Name, StringComparer.Ordinal).ToArray();
        return left.Zip(right).All(static pair => pair.First.BytewiseEquals(pair.Second));
    }
}

[ComVisible(false)]
public sealed record WebAdminBrokerAppIdRegistryReadback(
    IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> BrokerAppIdViews,
    IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> ExistingApplicationAppIdViews)
{
    public bool ExistingApplicationAppIdUnchanged(WebAdminBrokerAppIdRegistryReadback baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return HaveSameSnapshots(ExistingApplicationAppIdViews, baseline.ExistingApplicationAppIdViews);
    }

    private static bool HaveSameSnapshots(
        IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> left,
        IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return left.Zip(right).All(static pair => pair.First.BytewiseEquals(pair.Second));
    }
}

[ComVisible(false)]
public interface IWebAdminBrokerRegistryKeyReader
{
    WebAdminBrokerRegistryKeySnapshot Read(RegistryView view, string keyPath);
}

[SupportedOSPlatform("windows")]
[ComVisible(false)]
public sealed class WindowsWebAdminBrokerRegistryEvidenceSource
{
    private const string ClassesPath = @"Software\Classes\AppID\";

    private static readonly RegistryView[] Views =
    [
        RegistryView.Registry64,
        RegistryView.Registry32
    ];

    private readonly IWebAdminBrokerRegistryKeyReader _reader;

    public WindowsWebAdminBrokerRegistryEvidenceSource()
        : this(new WindowsWebAdminBrokerRegistryKeyReader())
    {
    }

    public WindowsWebAdminBrokerRegistryEvidenceSource(IWebAdminBrokerRegistryKeyReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public WebAdminBrokerAppIdRegistryReadback Capture()
    {
        var brokerPath = $"{ClassesPath}{WebAdminSessionBrokerContract.AppId}";
        var applicationPath = $"{ClassesPath}{LegacyComRegistrationManifest.AppId}";
        return new(
            Views.Select(view => _reader.Read(view, brokerPath)).ToArray(),
            Views.Select(view => _reader.Read(view, applicationPath)).ToArray());
    }

    public bool TryBuildPreflightEvidence(
        WebAdminBrokerAppIdRegistryReadback current,
        WebAdminBrokerAppIdRegistryReadback baseline,
        out WebAdminBrokerAppIdEvidence evidence,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!HasReadableExistingApplicationSnapshots(current)
            || !HasReadableExistingApplicationSnapshots(baseline))
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: false);
            reason = "installed-application-appid-readback-incomplete";
            return false;
        }

        if (!current.ExistingApplicationAppIdUnchanged(baseline))
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: false);
            reason = "installed-application-registration-changed";
            return false;
        }

        var broker64Views = current.BrokerAppIdViews
            .Where(static view => view.View == RegistryView.Registry64)
            .ToArray();
        if (broker64Views.Length != 1)
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-64-view-missing";
            return false;
        }

        var broker64 = broker64Views[0];
        if (broker64.ReadError is not null)
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-readback-error";
            return false;
        }

        if (!broker64.Present)
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registration-missing";
            return true;
        }

        var broker32Views = current.BrokerAppIdViews
            .Where(static view => view.View == RegistryView.Registry32)
            .ToArray();
        if (broker32Views.Length != 1)
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-32-view-missing";
            return false;
        }

        var broker32 = broker32Views[0];
        if (broker32.ReadError is not null)
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-readback-error";
            return false;
        }

        if (!broker32.Present)
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-32-view-missing";
            return false;
        }

        if (!broker32.ContentEquals(broker64))
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-views-mismatch";
            return false;
        }

        var expectedBrokerPath = $"{ClassesPath}{WebAdminSessionBrokerContract.AppId}";
        if (!string.Equals(broker64.KeyPath, expectedBrokerPath, StringComparison.Ordinal))
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: true);
            reason = "broker-registry-path-mismatch";
            return false;
        }

        evidence = new(
            WebAdminSessionBrokerContract.AppId,
            BrokerRegistrationPresent: true,
            ReadStringValue(broker64, "LocalService"),
            ReadPermissionValue(broker64, "LaunchPermission"),
            ReadPermissionValue(broker64, "AccessPermission"),
            ExistingApplicationAppIdUnchanged: true);
        reason = "broker-registry-readback-captured";
        return true;
    }

    private static bool HasReadableExistingApplicationSnapshots(
        WebAdminBrokerAppIdRegistryReadback readback)
    {
        var views = readback.ExistingApplicationAppIdViews;
        return views.Count == 2
            && views.All(static view => view.Present && view.ReadError is null)
            && views.Any(static view => view.View == RegistryView.Registry64)
            && views.Any(static view => view.View == RegistryView.Registry32)
            && views.All(view =>
                string.Equals(
                    view.KeyPath,
                    $"{ClassesPath}{LegacyComRegistrationManifest.AppId}",
                    StringComparison.Ordinal));
    }

    private static WebAdminBrokerAppIdEvidence MissingBrokerEvidence(
        bool existingApplicationAppIdUnchanged) =>
        new(
            WebAdminSessionBrokerContract.AppId,
            BrokerRegistrationPresent: false,
            LocalService: null,
            new(false, [], []),
            new(false, [], []),
            existingApplicationAppIdUnchanged);

    private static string? ReadStringValue(
        WebAdminBrokerRegistryKeySnapshot snapshot,
        string name)
    {
        var value = FindValue(snapshot, name);
        if (value is null
            || (value.Kind is not RegistryValueKind.String and not RegistryValueKind.ExpandString))
        {
            return null;
        }

        return Encoding.Unicode.GetString(value.RawBytes).TrimEnd('\0');
    }

    private static WebAdminBrokerPermissionEvidence ReadPermissionValue(
        WebAdminBrokerRegistryKeySnapshot snapshot,
        string name)
    {
        var value = FindValue(snapshot, name);
        if (value is null || value.Kind != RegistryValueKind.Binary)
        {
            return new(false, [], []);
        }

        try
        {
            var descriptor = new RawSecurityDescriptor(value.RawBytes, 0);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var accessMasks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var explicitDacl = descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclPresent)
                && descriptor.DiscretionaryAcl is not null;
            if (!explicitDacl)
            {
                return new(
                    true,
                    [],
                    [],
                    ExplicitDacl: false,
                    AllowedAccessMasks: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            }

            foreach (var ace in descriptor.DiscretionaryAcl!)
            {
                if (ace is not CommonAce commonAce
                    || commonAce.IsInherited
                    || commonAce.IsCallback
                    || commonAce.AceFlags != AceFlags.None)
                {
                    return new(
                        true,
                        [],
                        [],
                        ExplicitDacl: false,
                        AllowedAccessMasks: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
                }

                var sid = new SecurityIdentifier(commonAce.SecurityIdentifier.Value).Value;
                switch (commonAce.AceQualifier)
                {
                    case AceQualifier.AccessDenied:
                        denied.Add(sid);
                        break;
                    case AceQualifier.AccessAllowed:
                        if (commonAce.AccessMask != WebAdminSessionBrokerAppIdPreflight.RequiredLocalBrokerAccessMask
                            || !allowed.Add(sid)
                            || !accessMasks.TryAdd(sid, commonAce.AccessMask))
                        {
                            return new(
                                true,
                                [],
                                [],
                                ExplicitDacl: false,
                                AllowedAccessMasks: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
                        }

                        break;
                    default:
                        return new(
                            true,
                            [],
                            [],
                            ExplicitDacl: false,
                            AllowedAccessMasks: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
                }
            }

            return new(true, allowed.ToArray(), denied.ToArray(), ExplicitDacl: true, accessMasks);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or IndexOutOfRangeException
            or NotSupportedException)
        {
            return new(
                true,
                [],
                [],
                ExplicitDacl: false,
                AllowedAccessMasks: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static WebAdminBrokerRegistryValueSnapshot? FindValue(
        WebAdminBrokerRegistryKeySnapshot snapshot,
        string name) =>
        snapshot.Values.FirstOrDefault(value =>
            string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase));

    private sealed class WindowsWebAdminBrokerRegistryKeyReader : IWebAdminBrokerRegistryKeyReader
    {
        [SupportedOSPlatform("windows")]
        public WebAdminBrokerRegistryKeySnapshot Read(RegistryView view, string keyPath)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(keyPath, writable: false);
                if (key is null)
                {
                    return new(view, keyPath, Present: false, [], ReadError: null);
                }

                var values = key.GetValueNames()
                    .Select(name => CaptureValue(key, name))
                    .OrderBy(static value => value.Name, StringComparer.Ordinal)
                    .ToArray();
                return new(view, keyPath, Present: true, values, ReadError: null);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or IOException
                or SecurityException)
            {
                return new(view, keyPath, Present: false, [], exception.GetType().Name);
            }
        }

        private static WebAdminBrokerRegistryValueSnapshot CaptureValue(RegistryKey key, string name)
        {
            var kind = key.GetValueKind(name);
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return new(name, kind, EncodeRawValue(kind, value));
        }

        private static byte[] EncodeRawValue(RegistryValueKind kind, object? value) => kind switch
        {
            RegistryValueKind.Binary => value is byte[] bytes ? bytes.ToArray() : [],
            RegistryValueKind.String or RegistryValueKind.ExpandString =>
                EncodeUnicodeString(value as string ?? string.Empty),
            RegistryValueKind.DWord => BitConverter.GetBytes(Convert.ToInt32(value, CultureInfo.InvariantCulture)),
            RegistryValueKind.QWord => BitConverter.GetBytes(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            RegistryValueKind.MultiString => EncodeMultiString(value as string[] ?? []),
            RegistryValueKind.None => value is byte[] noneBytes ? noneBytes.ToArray() : [],
            _ => Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
        };

        private static byte[] EncodeUnicodeString(string value) =>
            Encoding.Unicode.GetBytes(value + "\0");

        private static byte[] EncodeMultiString(IReadOnlyList<string> values) =>
            EncodeUnicodeString(string.Join("\0", values) + "\0");
    }
}
