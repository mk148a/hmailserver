using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record ComRegistryValue(string KeyPath, string? ValueName, string Value);

[ComVisible(false)]
public sealed class LegacyComRegistrationManifest
{
    public const string AppId = "{5EDEC473-39E0-43F6-A234-1947071721C8}";
    public const string TypeLibraryId = "{DB241B59-A1B1-4C59-98FC-8D101A2995F2}";

    private LegacyComRegistrationManifest(
        IReadOnlyList<string> keys,
        IReadOnlyList<ComRegistryValue> values,
        IReadOnlyList<string> uninstallRoots)
    {
        Keys = keys;
        Values = values;
        UninstallRoots = uninstallRoots;
    }

    public IReadOnlyList<string> Keys { get; }

    public IReadOnlyList<ComRegistryValue> Values { get; }

    public IReadOnlyList<string> UninstallRoots { get; }

    public static LegacyComRegistrationManifest Create(string serviceExecutablePath, string typeLibraryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeLibraryPath);

        var executablePath = Path.GetFullPath(serviceExecutablePath);
        var tlbPath = Path.GetFullPath(typeLibraryPath);
        var typeLibraryDirectory = Path.GetDirectoryName(tlbPath)
            ?? throw new ArgumentException("The type-library path must have a directory.", nameof(typeLibraryPath));
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new List<ComRegistryValue>();
        var uninstallRoots = new List<string>();

        void AddValue(string keyPath, string? valueName, string value)
        {
            keys.Add(keyPath);
            values.Add(new ComRegistryValue(keyPath, valueName, value));
        }

        AddValue($@"AppID\{AppId}", null, "hMailServer");
        AddValue($@"AppID\{AppId}", "LocalService", "hMailServer");
        AddValue(@"AppID\hMailServer.EXE", "AppID", AppId);
        uninstallRoots.Add($@"AppID\{AppId}");
        uninstallRoots.Add(@"AppID\hMailServer.EXE");

        AddClass(
            "Application",
            "{D6567EF8-0A6C-48E7-9288-A2463123C2F3}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Settings",
            "{FDF084A7-82DE-4EBE-8455-E506ACE01D63}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Directories",
            "{1969A4DF-B1B0-4A71-8196-5FD392CA3D8A}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Domains",
            "{82AFD03C-58A4-4F04-8277-6B2812780E45}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Domain",
            "{C535E4AF-9DB3-41FC-B434-FFCDAE0EFBD5}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Accounts",
            "{403A75B8-499A-44C1-93D3-6A8A460AA88D}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Account",
            "{369BE902-9F27-4722-A29F-3059E4D7021D}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "FetchAccount",
            "{6F5E2977-2F51-40B0-847B-DD44C9ACC5A5}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "FetchAccounts",
            "{F17C3A00-A7A0-4519-AEDD-DCC3B8DE6A3D}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Rules",
            "{624F494B-347A-4285-9506-C54154D50B2A}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Rule",
            "{D5D7927A-7D05-40F3-91DD-968FC14316C7}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "IMAPFolders",
            "{A0AAF31A-570A-4B78-BDAB-4C33E34BE85F}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "IMAPFolder",
            "{9FCA085E-E475-4DEE-9D45-5519818DD6E0}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Routes",
            "{7D174A9D-D44C-4627-BE78-E5DDC513C31F}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Route",
            "{3FF9BB08-7924-4418-BADA-7D959467D51B}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "IncomingRelays",
            "{3E75EE53-EAA6-40A5-B2CE-9CB8D7EE9278}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "IncomingRelay",
            "{CB3F5F58-436C-4358-8E1C-1BE1F6D822BC}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "SecurityRanges",
            "{60A752A2-1197-4841-ADD4-CE922873E794}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "SecurityRange",
            "{B149383D-151C-4585-99F8-71876D0F14C4}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "ServerMessages",
            "{379F1428-A4C9-4D43-9745-AEABF8950755}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "ServerMessage",
            "{561076C6-9174-43D3-B889-CFCC42E3AE5E}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "TCPIPPorts",
            "{225808B4-6F03-4750-843F-3150EB1C357F}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "TCPIPPort",
            "{556DF811-3E02-4106-BCA6-C75996825E9A}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "SSLCertificates",
            "{BE7AF6BB-2ECA-4313-BE00-16A72D82AE49}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "SSLCertificate",
            "{11A68C45-EC73-496A-A300-2EB8820824EF}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Groups",
            "{7573CF89-DF41-4079-91B1-894A0DF3E783}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Group",
            "{8F91E8CB-7DE5-494F-92BD-A245D8CC7E15}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "GroupMembers",
            "{19BD0117-D6EF-49B3-AAC9-9CE70266AEFF}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "GroupMember",
            "{2AF5F36A-6475-43D3-A037-D31C1FEA7BA8}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Aliases",
            "{1FE5E5F1-870A-4139-9EC1-DFFA3A9A58C8}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "Alias",
            "{335CE9E1-32C5-4CB0-8BF6-CB925196E4D6}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "DistributionLists",
            "{C3DD0A4A-0551-442F-859A-76AAB92A6CF1}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "DistributionList",
            "{990D27ED-86CE-4DCB-B1C1-1E130C07F918}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "DistributionListRecipients",
            "{AB59F3C1-4904-4F1D-883A-4BFC699A7D0B}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "DistributionListRecipient",
            "{0886D5D8-4C1C-46F1-BC7B-EEDC9FE9DFFA}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "DomainAliases",
            "{DC25B3AD-0360-49CA-AD4B-06FA42B9DF04}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "DomainAlias",
            "{D0061C74-5588-4796-B564-FE5DE85495DC}",
            executablePath,
            keys,
            values,
            uninstallRoots);
        AddClass(
            "MessageIndexing",
            "{5F414F73-8E29-4E51-86F2-13C12EF9227A}",
            executablePath,
            keys,
            values,
            uninstallRoots);

        // The legacy MessageIndexing.rgs contains an orphan TypeLib GUID. The authoritative
        // installed type library and IDL use TypeLibraryId and include the MessageIndexing coclass.
        var typeLibraryVersionKey = $@"TypeLib\{TypeLibraryId}\1.0";
        AddValue(typeLibraryVersionKey, null, "hMailServer Type Library");
        AddValue($@"{typeLibraryVersionKey}\0\win64", null, tlbPath);
        AddValue($@"{typeLibraryVersionKey}\FLAGS", null, "0");
        AddValue($@"{typeLibraryVersionKey}\HELPDIR", null, typeLibraryDirectory);
        uninstallRoots.Add($@"TypeLib\{TypeLibraryId}");

        return new LegacyComRegistrationManifest(
            keys.ToArray(),
            values,
            uninstallRoots);
    }

    private static void AddClass(
        string className,
        string classId,
        string executablePath,
        ISet<string> keys,
        ICollection<ComRegistryValue> values,
        ICollection<string> uninstallRoots)
    {
        var versionedProgId = $"hMailServer.{className}.1";
        var versionIndependentProgId = $"hMailServer.{className}";
        var description = $"{className} Class";
        var classKey = $@"CLSID\{classId}";

        AddValue(versionedProgId, null, description);
        AddValue($@"{versionedProgId}\CLSID", null, classId);
        AddValue(versionIndependentProgId, null, description);
        AddValue($@"{versionIndependentProgId}\CLSID", null, classId);
        AddValue($@"{versionIndependentProgId}\CurVer", null, versionedProgId);
        AddValue(classKey, null, description);
        AddValue($@"{classKey}\ProgID", null, versionedProgId);
        AddValue($@"{classKey}\VersionIndependentProgID", null, versionIndependentProgId);
        AddValue($@"{classKey}\LocalServer32", null, $"\"{executablePath}\"");
        AddValue(classKey, "AppID", AppId);
        AddValue($@"{classKey}\TypeLib", null, TypeLibraryId);
        keys.Add($@"{classKey}\Programmable");

        uninstallRoots.Add(versionedProgId);
        uninstallRoots.Add(versionIndependentProgId);
        uninstallRoots.Add(classKey);

        void AddValue(string keyPath, string? valueName, string value)
        {
            keys.Add(keyPath);
            values.Add(new ComRegistryValue(keyPath, valueName, value));
        }
    }
}
