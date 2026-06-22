using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public static class WindowsComRegistration
{
    private const string MachineClassesPath = @"Software\Classes";

    [SupportedOSPlatform("windows")]
    public static void Register(string serviceExecutablePath, string typeLibraryPath)
    {
        var manifest = LegacyComRegistrationManifest.Create(serviceExecutablePath, typeLibraryPath);
        using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var classes = localMachine.CreateSubKey(MachineClassesPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the 64-bit machine COM registry.");

        foreach (var keyPath in manifest.Keys)
        {
            using var key = classes.CreateSubKey(keyPath, writable: true)
                ?? throw new InvalidOperationException($"Could not create COM registry key '{keyPath}'.");
        }

        foreach (var entry in manifest.Values)
        {
            using var key = classes.CreateSubKey(entry.KeyPath, writable: true)
                ?? throw new InvalidOperationException($"Could not open COM registry key '{entry.KeyPath}'.");
            key.SetValue(entry.ValueName ?? string.Empty, entry.Value, RegistryValueKind.String);
        }
    }

    [SupportedOSPlatform("windows")]
    public static void Unregister(string serviceExecutablePath, string typeLibraryPath)
    {
        var manifest = LegacyComRegistrationManifest.Create(serviceExecutablePath, typeLibraryPath);
        using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var classes = localMachine.OpenSubKey(MachineClassesPath, writable: true);
        if (classes is null)
        {
            return;
        }

        var ownsClassRegistration = manifest.UninstallRoots
            .Where(root => root.StartsWith(@"CLSID\", StringComparison.OrdinalIgnoreCase))
            .Any(root => IsOwnedRegistration(classes, root, manifest));
        foreach (var root in manifest.UninstallRoots)
        {
            if (root.StartsWith(@"AppID\", StringComparison.OrdinalIgnoreCase) && !ownsClassRegistration)
            {
                continue;
            }

            if (IsOwnedRegistration(classes, root, manifest))
            {
                classes.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsOwnedRegistration(
        RegistryKey classes,
        string root,
        LegacyComRegistrationManifest manifest)
    {
        var sentinel = root switch
        {
            var value when value.StartsWith(@"CLSID\", StringComparison.OrdinalIgnoreCase) =>
                manifest.Values.Single(entry =>
                    entry.KeyPath.Equals($@"{root}\LocalServer32", StringComparison.OrdinalIgnoreCase)
                    && entry.ValueName is null),
            var value when value.StartsWith("hMailServer.", StringComparison.OrdinalIgnoreCase) =>
                GetProgIdOwnershipSentinel(root, manifest),
            var value when value.StartsWith(@"TypeLib\", StringComparison.OrdinalIgnoreCase) =>
                manifest.Values.Single(entry =>
                    entry.KeyPath.StartsWith($@"{root}\", StringComparison.OrdinalIgnoreCase)
                    && entry.KeyPath.EndsWith(@"\0\win64", StringComparison.OrdinalIgnoreCase)
                    && entry.ValueName is null),
            @"AppID\hMailServer.EXE" =>
                manifest.Values.Single(entry =>
                    entry.KeyPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                    && entry.ValueName == "AppID"),
            _ => manifest.Values.Single(entry =>
                entry.KeyPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                && entry.ValueName == "LocalService")
        };

        using var key = classes.OpenSubKey(sentinel.KeyPath);
        return key?.GetValue(sentinel.ValueName ?? string.Empty) is string actual
            && actual.Equals(sentinel.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static ComRegistryValue GetProgIdOwnershipSentinel(
        string progIdRoot,
        LegacyComRegistrationManifest manifest)
    {
        var classId = manifest.Values.Single(entry =>
            entry.KeyPath.Equals($@"{progIdRoot}\CLSID", StringComparison.OrdinalIgnoreCase)
            && entry.ValueName is null).Value;
        return manifest.Values.Single(entry =>
            entry.KeyPath.Equals($@"CLSID\{classId}\LocalServer32", StringComparison.OrdinalIgnoreCase)
            && entry.ValueName is null);
    }
}
