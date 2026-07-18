using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

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
    public IReadOnlyList<string> DirectSubkeyNames { get; init; } = [];

    public bool BytewiseEquals(WebAdminBrokerRegistryKeySnapshot other)
    {
        return View == other.View && ContentEquals(other);
    }

    public bool ContentEquals(WebAdminBrokerRegistryKeySnapshot other)
    {
        if (!string.Equals(KeyPath, other.KeyPath, StringComparison.Ordinal)
            || Present != other.Present
            || !string.Equals(ReadError, other.ReadError, StringComparison.Ordinal)
            || Values.Count != other.Values.Count
            || DirectSubkeyNames.Count != other.DirectSubkeyNames.Count)
        {
            return false;
        }

        var left = Values.OrderBy(static value => value.Name, StringComparer.Ordinal).ToArray();
        var right = other.Values.OrderBy(static value => value.Name, StringComparer.Ordinal).ToArray();
        return left.Zip(right).All(static pair => pair.First.BytewiseEquals(pair.Second))
            && DirectSubkeyNames.Order(StringComparer.Ordinal).SequenceEqual(
                other.DirectSubkeyNames.Order(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }
}

[ComVisible(false)]
public sealed record WebAdminBrokerAppIdRegistryReadback(
    IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> BrokerAppIdViews,
    IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> InstalledApplicationGraphViews)
{
    private const string ExistingApplicationAppIdPath =
        $"Software\\Classes\\AppID\\{LegacyComRegistrationManifest.AppId}";

    public IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> ExistingApplicationAppIdViews =>
        InstalledApplicationGraphViews
            .Where(static snapshot => string.Equals(
                snapshot.KeyPath,
                ExistingApplicationAppIdPath,
                StringComparison.Ordinal))
            .ToArray();

    public bool ExistingApplicationAppIdUnchanged(WebAdminBrokerAppIdRegistryReadback baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return HaveSameSnapshots(ExistingApplicationAppIdViews, baseline.ExistingApplicationAppIdViews);
    }

    public bool InstalledApplicationGraphUnchanged(WebAdminBrokerAppIdRegistryReadback baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return HaveSameSnapshots(InstalledApplicationGraphViews, baseline.InstalledApplicationGraphViews);
    }

    private static bool HaveSameSnapshots(
        IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> left,
        IReadOnlyList<WebAdminBrokerRegistryKeySnapshot> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return left.All(snapshot =>
        {
            var matches = right.Where(candidate =>
                candidate.View == snapshot.View
                && string.Equals(candidate.KeyPath, snapshot.KeyPath, StringComparison.Ordinal));
            return matches.Count() == 1 && snapshot.BytewiseEquals(matches.Single());
        });
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
    private const string ClassesPath = @"Software\Classes\";
    private const string ApplicationClassId = "{D6567EF8-0A6C-48E7-9288-A2463123C2F3}";
    private const string ApplicationInterfaceId = "{2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8}";

    private static readonly string[] InstalledApplicationGraphPaths =
    [
        $"{ClassesPath}hMailServer.Application.1",
        $"{ClassesPath}hMailServer.Application.1\\CLSID",
        $"{ClassesPath}hMailServer.Application",
        $"{ClassesPath}hMailServer.Application\\CLSID",
        $"{ClassesPath}hMailServer.Application\\CurVer",
        $"{ClassesPath}CLSID\\{ApplicationClassId}",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\ProgID",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\VersionIndependentProgID",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\Programmable",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\LocalServer32",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\TypeLib",
        $"{ClassesPath}AppID\\{LegacyComRegistrationManifest.AppId}",
        $"{ClassesPath}AppID\\hMailServer.EXE",
        $"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}",
        $"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0",
        $"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0\\0",
        $"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0\\0\\win64",
        $"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0\\FLAGS",
        $"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0\\HELPDIR",
        $"{ClassesPath}Interface\\{ApplicationInterfaceId}",
        $"{ClassesPath}Interface\\{ApplicationInterfaceId}\\ProxyStubClsid32",
        $"{ClassesPath}Interface\\{ApplicationInterfaceId}\\TypeLib"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> InstalledApplicationGraphDirectSubkeyNames =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [$"{ClassesPath}hMailServer.Application.1"] = ["CLSID"],
            [$"{ClassesPath}hMailServer.Application"] = ["CLSID", "CurVer"],
            [$"{ClassesPath}CLSID\\{ApplicationClassId}"] =
                ["ProgID", "VersionIndependentProgID", "Programmable", "LocalServer32", "TypeLib"],
            [$"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}"] = ["1.0"],
            [$"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0"] =
                ["0", "FLAGS", "HELPDIR"],
            [$"{ClassesPath}TypeLib\\{LegacyComRegistrationManifest.TypeLibraryId}\\1.0\\0"] = ["win64"],
            [$"{ClassesPath}Interface\\{ApplicationInterfaceId}"] = ["ProxyStubClsid32", "TypeLib"]
        };

    private static readonly HashSet<string> Registry32AbsentApplicationGraphPaths =
    [
        $"{ClassesPath}CLSID\\{ApplicationClassId}",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\ProgID",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\VersionIndependentProgID",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\Programmable",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\LocalServer32",
        $"{ClassesPath}CLSID\\{ApplicationClassId}\\TypeLib"
    ];

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
        var brokerPath = $"{ClassesPath}AppID\\{WebAdminSessionBrokerContract.AppId}";
        return new(
            Views.Select(view => _reader.Read(view, brokerPath)).ToArray(),
            Views.SelectMany(view => InstalledApplicationGraphPaths.Select(
                path => _reader.Read(view, path))).ToArray());
    }

    public bool TryBuildPreflightEvidence(
        WebAdminBrokerAppIdRegistryReadback current,
        WebAdminBrokerAppIdRegistryReadback baseline,
        out WebAdminBrokerAppIdEvidence evidence,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!HasCompleteInstalledApplicationGraphSnapshots(current)
            || !HasCompleteInstalledApplicationGraphSnapshots(baseline))
        {
            evidence = MissingBrokerEvidence(existingApplicationAppIdUnchanged: false);
            reason = "installed-application-appid-readback-incomplete";
            return false;
        }

        if (!current.InstalledApplicationGraphUnchanged(baseline))
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

        var expectedBrokerPath = $"{ClassesPath}AppID\\{WebAdminSessionBrokerContract.AppId}";
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

    private static bool HasCompleteInstalledApplicationGraphSnapshots(
        WebAdminBrokerAppIdRegistryReadback readback)
    {
        var snapshots = readback.InstalledApplicationGraphViews;
        if (snapshots.Count != Views.Length * InstalledApplicationGraphPaths.Length)
        {
            return false;
        }

        return Views.All(view => InstalledApplicationGraphPaths.All(path =>
        {
            var matches = snapshots.Where(snapshot =>
                snapshot.View == view
                && string.Equals(snapshot.KeyPath, path, StringComparison.Ordinal));
            if (matches.Count() != 1)
            {
                return false;
            }

            var snapshot = matches.Single();
            var expectedPresent = view != RegistryView.Registry32
                || !Registry32AbsentApplicationGraphPaths.Contains(path);
            if (snapshot.ReadError is not null || snapshot.Present != expectedPresent)
            {
                return false;
            }

            if (!expectedPresent)
            {
                return snapshot.Values.Count == 0 && snapshot.DirectSubkeyNames.Count == 0;
            }

            var expectedSubkeyNames = InstalledApplicationGraphDirectSubkeyNames.GetValueOrDefault(path, []);
            return snapshot.DirectSubkeyNames.Count == expectedSubkeyNames.Length
                && snapshot.DirectSubkeyNames.Order(StringComparer.Ordinal).SequenceEqual(
                    expectedSubkeyNames.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal);
        }))
        && readback.ExistingApplicationAppIdViews.Count == Views.Length
        && readback.ExistingApplicationAppIdViews.All(static snapshot =>
            snapshot.Present && snapshot.ReadError is null);
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
                return new(view, keyPath, Present: true, values, ReadError: null)
                {
                    DirectSubkeyNames = key.GetSubKeyNames()
                        .Order(StringComparer.Ordinal)
                        .ToArray()
                };
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or IOException
                or SecurityException
                or Win32Exception)
            {
                return new(view, keyPath, Present: false, [], exception.GetType().Name);
            }
        }

        private static WebAdminBrokerRegistryValueSnapshot CaptureValue(RegistryKey key, string name)
        {
            var (kind, rawBytes) = ReadNativeValue(key, name);
            return new(name, kind, rawBytes);
        }

        private static (RegistryValueKind Kind, byte[] RawBytes) ReadNativeValue(
            RegistryKey key,
            string name)
        {
            var valueName = string.IsNullOrEmpty(name) ? null : name;
            uint type = 0;
            uint byteCount = 0;
            var result = RegQueryValueEx(
                key.Handle,
                valueName,
                IntPtr.Zero,
                out type,
                null,
                ref byteCount);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }

            var rawBytes = new byte[checked((int)byteCount)];
            if (byteCount != 0)
            {
                result = RegQueryValueEx(
                    key.Handle,
                    valueName,
                    IntPtr.Zero,
                    out type,
                    rawBytes,
                    ref byteCount);
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                if (byteCount != rawBytes.Length)
                {
                    Array.Resize(ref rawBytes, checked((int)byteCount));
                }
            }

            return ((RegistryValueKind)type, rawBytes);
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(
            SafeRegistryHandle hKey,
            string? lpValueName,
            IntPtr lpReserved,
            out uint lpType,
            byte[]? lpData,
            ref uint lpcbData);
    }
}
