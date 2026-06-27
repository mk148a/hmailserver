using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class LegacyComRegistrationManifestTests
{
    [TestMethod]
    public void Create_PreservesLegacyRootClassAndTypeLibraryRegistration()
    {
        const string executablePath = @"C:\Program Files\hMailServer\Bin\hMailServer.exe";
        const string typeLibraryPath = @"C:\Program Files\hMailServer\Bin\hMailServer.tlb";

        var manifest = LegacyComRegistrationManifest.Create(executablePath, typeLibraryPath);

        AssertValue(manifest, @"AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}", null, "hMailServer");
        AssertValue(manifest, @"AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}", "LocalService", "hMailServer");
        AssertValue(manifest, @"AppID\hMailServer.EXE", "AppID", "{5EDEC473-39E0-43F6-A234-1947071721C8}");

        AssertClass(
            manifest,
            "Application",
            "{D6567EF8-0A6C-48E7-9288-A2463123C2F3}",
            executablePath);
        AssertClass(
            manifest,
            "Settings",
            "{FDF084A7-82DE-4EBE-8455-E506ACE01D63}",
            executablePath);
        AssertClass(
            manifest,
            "Domains",
            "{82AFD03C-58A4-4F04-8277-6B2812780E45}",
            executablePath);
        AssertClass(
            manifest,
            "Domain",
            "{C535E4AF-9DB3-41FC-B434-FFCDAE0EFBD5}",
            executablePath);
        AssertClass(
            manifest,
            "Accounts",
            "{403A75B8-499A-44C1-93D3-6A8A460AA88D}",
            executablePath);
        AssertClass(
            manifest,
            "Account",
            "{369BE902-9F27-4722-A29F-3059E4D7021D}",
            executablePath);
        AssertClass(
            manifest,
            "FetchAccount",
            "{6F5E2977-2F51-40B0-847B-DD44C9ACC5A5}",
            executablePath);
        AssertClass(
            manifest,
            "FetchAccounts",
            "{F17C3A00-A7A0-4519-AEDD-DCC3B8DE6A3D}",
            executablePath);
        AssertClass(
            manifest,
            "Rules",
            "{624F494B-347A-4285-9506-C54154D50B2A}",
            executablePath);
        AssertClass(
            manifest,
            "Rule",
            "{D5D7927A-7D05-40F3-91DD-968FC14316C7}",
            executablePath);
        AssertClass(
            manifest,
            "IMAPFolders",
            "{A0AAF31A-570A-4B78-BDAB-4C33E34BE85F}",
            executablePath);
        AssertClass(
            manifest,
            "IMAPFolder",
            "{9FCA085E-E475-4DEE-9D45-5519818DD6E0}",
            executablePath);
        AssertClass(
            manifest,
            "Routes",
            "{7D174A9D-D44C-4627-BE78-E5DDC513C31F}",
            executablePath);
        AssertClass(
            manifest,
            "Route",
            "{3FF9BB08-7924-4418-BADA-7D959467D51B}",
            executablePath);
        AssertClass(
            manifest,
            "Aliases",
            "{1FE5E5F1-870A-4139-9EC1-DFFA3A9A58C8}",
            executablePath);
        AssertClass(
            manifest,
            "Alias",
            "{335CE9E1-32C5-4CB0-8BF6-CB925196E4D6}",
            executablePath);
        AssertClass(
            manifest,
            "DistributionLists",
            "{C3DD0A4A-0551-442F-859A-76AAB92A6CF1}",
            executablePath);
        AssertClass(
            manifest,
            "DistributionList",
            "{990D27ED-86CE-4DCB-B1C1-1E130C07F918}",
            executablePath);
        AssertClass(
            manifest,
            "DistributionListRecipients",
            "{AB59F3C1-4904-4F1D-883A-4BFC699A7D0B}",
            executablePath);
        AssertClass(
            manifest,
            "DistributionListRecipient",
            "{0886D5D8-4C1C-46F1-BC7B-EEDC9FE9DFFA}",
            executablePath);
        AssertClass(
            manifest,
            "DomainAliases",
            "{DC25B3AD-0360-49CA-AD4B-06FA42B9DF04}",
            executablePath);
        AssertClass(
            manifest,
            "DomainAlias",
            "{D0061C74-5588-4796-B564-FE5DE85495DC}",
            executablePath);
        AssertClass(
            manifest,
            "MessageIndexing",
            "{5F414F73-8E29-4E51-86F2-13C12EF9227A}",
            executablePath);

        AssertValue(
            manifest,
            @"TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0",
            null,
            "hMailServer Type Library");
        AssertValue(
            manifest,
            @"TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\0\win64",
            null,
            typeLibraryPath);
        AssertValue(
            manifest,
            @"TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\FLAGS",
            null,
            "0");
        AssertValue(
            manifest,
            @"TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}\1.0\HELPDIR",
            null,
            @"C:\Program Files\hMailServer\Bin");
    }

    [TestMethod]
    public void Create_ProvidesSymmetricUninstallRootsWithoutMachineMutation()
    {
        var manifest = LegacyComRegistrationManifest.Create(@"C:\hMailServer.exe", @"C:\hMailServer.tlb");

        CollectionAssert.AreEquivalent(
            new[]
            {
                @"hMailServer.Application.1",
                @"hMailServer.Application",
                @"hMailServer.Settings.1",
                @"hMailServer.Settings",
                @"hMailServer.Domains.1",
                @"hMailServer.Domains",
                @"hMailServer.Domain.1",
                @"hMailServer.Domain",
                @"hMailServer.Accounts.1",
                @"hMailServer.Accounts",
                @"hMailServer.Account.1",
                @"hMailServer.Account",
                @"hMailServer.FetchAccount.1",
                @"hMailServer.FetchAccount",
                @"hMailServer.FetchAccounts.1",
                @"hMailServer.FetchAccounts",
                @"hMailServer.Rules.1",
                @"hMailServer.Rules",
                @"hMailServer.Rule.1",
                @"hMailServer.Rule",
                @"hMailServer.IMAPFolders.1",
                @"hMailServer.IMAPFolders",
                @"hMailServer.IMAPFolder.1",
                @"hMailServer.IMAPFolder",
                @"hMailServer.Routes.1",
                @"hMailServer.Routes",
                @"hMailServer.Route.1",
                @"hMailServer.Route",
                @"hMailServer.Aliases.1",
                @"hMailServer.Aliases",
                @"hMailServer.Alias.1",
                @"hMailServer.Alias",
                @"hMailServer.DistributionLists.1",
                @"hMailServer.DistributionLists",
                @"hMailServer.DistributionList.1",
                @"hMailServer.DistributionList",
                @"hMailServer.DistributionListRecipients.1",
                @"hMailServer.DistributionListRecipients",
                @"hMailServer.DistributionListRecipient.1",
                @"hMailServer.DistributionListRecipient",
                @"hMailServer.DomainAliases.1",
                @"hMailServer.DomainAliases",
                @"hMailServer.DomainAlias.1",
                @"hMailServer.DomainAlias",
                @"hMailServer.MessageIndexing.1",
                @"hMailServer.MessageIndexing",
                @"CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}",
                @"CLSID\{FDF084A7-82DE-4EBE-8455-E506ACE01D63}",
                @"CLSID\{82AFD03C-58A4-4F04-8277-6B2812780E45}",
                @"CLSID\{C535E4AF-9DB3-41FC-B434-FFCDAE0EFBD5}",
                @"CLSID\{403A75B8-499A-44C1-93D3-6A8A460AA88D}",
                @"CLSID\{369BE902-9F27-4722-A29F-3059E4D7021D}",
                @"CLSID\{6F5E2977-2F51-40B0-847B-DD44C9ACC5A5}",
                @"CLSID\{F17C3A00-A7A0-4519-AEDD-DCC3B8DE6A3D}",
                @"CLSID\{624F494B-347A-4285-9506-C54154D50B2A}",
                @"CLSID\{D5D7927A-7D05-40F3-91DD-968FC14316C7}",
                @"CLSID\{A0AAF31A-570A-4B78-BDAB-4C33E34BE85F}",
                @"CLSID\{9FCA085E-E475-4DEE-9D45-5519818DD6E0}",
                @"CLSID\{7D174A9D-D44C-4627-BE78-E5DDC513C31F}",
                @"CLSID\{3FF9BB08-7924-4418-BADA-7D959467D51B}",
                @"CLSID\{1FE5E5F1-870A-4139-9EC1-DFFA3A9A58C8}",
                @"CLSID\{335CE9E1-32C5-4CB0-8BF6-CB925196E4D6}",
                @"CLSID\{C3DD0A4A-0551-442F-859A-76AAB92A6CF1}",
                @"CLSID\{990D27ED-86CE-4DCB-B1C1-1E130C07F918}",
                @"CLSID\{AB59F3C1-4904-4F1D-883A-4BFC699A7D0B}",
                @"CLSID\{0886D5D8-4C1C-46F1-BC7B-EEDC9FE9DFFA}",
                @"CLSID\{DC25B3AD-0360-49CA-AD4B-06FA42B9DF04}",
                @"CLSID\{D0061C74-5588-4796-B564-FE5DE85495DC}",
                @"CLSID\{5F414F73-8E29-4E51-86F2-13C12EF9227A}",
                @"AppID\{5EDEC473-39E0-43F6-A234-1947071721C8}",
                @"AppID\hMailServer.EXE",
                @"TypeLib\{DB241B59-A1B1-4C59-98FC-8D101A2995F2}"
            },
            manifest.UninstallRoots.ToArray());
    }

    private static void AssertClass(
        LegacyComRegistrationManifest manifest,
        string className,
        string classId,
        string executablePath)
    {
        var versionedProgId = $"hMailServer.{className}.1";
        var versionIndependentProgId = $"hMailServer.{className}";
        var description = $"{className} Class";
        var classKey = $@"CLSID\{classId}";

        AssertValue(manifest, versionedProgId, null, description);
        AssertValue(manifest, $@"{versionedProgId}\CLSID", null, classId);
        AssertValue(manifest, versionIndependentProgId, null, description);
        AssertValue(manifest, $@"{versionIndependentProgId}\CLSID", null, classId);
        AssertValue(manifest, $@"{versionIndependentProgId}\CurVer", null, versionedProgId);
        AssertValue(manifest, classKey, null, description);
        AssertValue(manifest, $@"{classKey}\ProgID", null, versionedProgId);
        AssertValue(manifest, $@"{classKey}\VersionIndependentProgID", null, versionIndependentProgId);
        AssertValue(manifest, $@"{classKey}\LocalServer32", null, $"\"{executablePath}\"");
        AssertValue(manifest, classKey, "AppID", "{5EDEC473-39E0-43F6-A234-1947071721C8}");
        AssertValue(manifest, $@"{classKey}\TypeLib", null, "{DB241B59-A1B1-4C59-98FC-8D101A2995F2}");
        CollectionAssert.Contains(manifest.Keys.ToArray(), $@"{classKey}\Programmable");
    }

    private static void AssertValue(
        LegacyComRegistrationManifest manifest,
        string keyPath,
        string? valueName,
        string value)
    {
        CollectionAssert.Contains(
            manifest.Values.ToArray(),
            new ComRegistryValue(keyPath, valueName, value));
    }
}
