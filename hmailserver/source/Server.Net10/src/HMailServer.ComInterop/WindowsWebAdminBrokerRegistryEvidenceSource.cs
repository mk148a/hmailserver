using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
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
        if (View != other.View
            || !string.Equals(KeyPath, other.KeyPath, StringComparison.Ordinal)
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
