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
                @"hMailServer.MessageIndexing.1",
                @"hMailServer.MessageIndexing",
                @"CLSID\{D6567EF8-0A6C-48E7-9288-A2463123C2F3}",
                @"CLSID\{FDF084A7-82DE-4EBE-8455-E506ACE01D63}",
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
