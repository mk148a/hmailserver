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
                @"hMailServer.Aliases.1",
                @"hMailServer.Aliases",
                @"hMailServer.Alias.1",
                @"hMailServer.Alias",
                @"hMailServer.MessageIndexing.1",
                @"hMailServer.MessageIndexing",
                @"CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}",
                @"CLSID\{FDF084A7-82DE-4EBE-8455-E506ACE01D63}",
                @"CLSID\{82AFD03C-58A4-4F04-8277-6B2812780E45}",
                @"CLSID\{C535E4AF-9DB3-41FC-B434-FFCDAE0EFBD5}",
                @"CLSID\{403A75B8-499A-44C1-93D3-6A8A460AA88D}",
                @"CLSID\{369BE902-9F27-4722-A29F-3059E4D7021D}",
                @"CLSID\{1FE5E5F1-870A-4139-9EC1-DFFA3A9A58C8}",
                @"CLSID\{335CE9E1-32C5-4CB0-8BF6-CB925196E4D6}",
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
