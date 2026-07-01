using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using HMailServer.ComInterop;
using BackupComClass = HMailServer.ComInterop.Backup;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class BackupComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndMarshaling()
    {
        var contract = typeof(IInterfaceBackup);

        Assert.AreEqual(new Guid("BC84454B-FCE1-41FA-A3DD-2C57F61D4310"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                "StartRestore",
                "get_ContainsSettings",
                "get_ContainsDomains",
                "get_ContainsMessages",
                "get_RestoreSettings",
                "set_RestoreSettings",
                "get_RestoreDomains",
                "set_RestoreDomains",
                "get_RestoreMessages",
                "set_RestoreMessages"
            },
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        Assert.AreEqual(1, contract.GetMethod(nameof(IInterfaceBackup.StartRestore))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackup.ContainsSettings), 2, canWrite: false);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackup.ContainsDomains), 3, canWrite: false);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackup.ContainsMessages), 4, canWrite: false);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackup.RestoreSettings), 5, canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackup.RestoreDomains), 6, canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceBackup.RestoreMessages), 7, canWrite: true);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(BackupComClass);

        Assert.AreEqual(new Guid("B088FED1-A784-4CDB-ADDF-E7332CB7F72F"), type.GUID);
        Assert.AreEqual("hMailServer.Backup.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceBackup), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var backup = new BackupComClass();

        var containsError = Assert.ThrowsExactly<COMException>(() => _ = backup.ContainsSettings);
        var setterError = Assert.ThrowsExactly<COMException>(() => backup.RestoreDomains = true);
        var restoreError = Assert.ThrowsExactly<COMException>(backup.StartRestore);

        Assert.AreEqual(EAccessDenied, containsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, setterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, restoreError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedBackup_ExposesContainsBitsAndKeepsRestoreSelectionsProcessLocal()
    {
        IInterfaceBackup backup = BackupComClass.CreateAuthorized(13);

        Assert.IsTrue(backup.ContainsSettings);
        Assert.IsFalse(backup.ContainsDomains);
        Assert.IsTrue(backup.ContainsMessages);
        Assert.IsFalse(backup.RestoreSettings);
        Assert.IsFalse(backup.RestoreDomains);
        Assert.IsFalse(backup.RestoreMessages);

        backup.RestoreSettings = true;
        backup.RestoreDomains = true;
        backup.RestoreMessages = true;
        backup.RestoreDomains = false;

        Assert.IsTrue(backup.RestoreSettings);
        Assert.IsFalse(backup.RestoreDomains);
        Assert.IsTrue(backup.RestoreMessages);
        AssertPending(backup.StartRestore);
    }

    [TestMethod]
    public void SevenZipReader_UsesShellFreeNamedEntryStdoutBoundary()
    {
        var startInfo = SevenZipBackupArchiveMetadataReader.CreateStartInfo(
            @"C:\hMailServer\Bin\7za.exe",
            @"D:\Backups\HMBackup sample.7z");

        Assert.AreEqual(@"C:\hMailServer\Bin\7za.exe", startInfo.FileName);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.CreateNoWindow);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsTrue(startInfo.RedirectStandardError);
        CollectionAssert.AreEqual(
            new[]
            {
                "x",
                @"D:\Backups\HMBackup sample.7z",
                "hMailServerBackup.xml",
                "-so",
                "-y"
            },
            startInfo.ArgumentList.ToArray());
    }

    [TestMethod]
    public void SevenZipReader_ParsesLegacyModeAndRejectsUnsafeOrMalformedXml()
    {
        Assert.AreEqual(13, Parse("<Backup><BackupInformation Mode=\"13\" /></Backup>"));
        Assert.AreEqual(0, Parse("<Backup><BackupInformation Mode=\"invalid\" /></Backup>"));
        Assert.AreEqual(0, Parse("<NotBackup><BackupInformation Mode=\"7\" /></NotBackup>"));

        Assert.ThrowsExactly<XmlException>(() => Parse("<!DOCTYPE Backup [<!ENTITY xxe SYSTEM \"file:///secret\">]><Backup><BackupInformation Mode=\"1\" />&xxe;</Backup>"));
        Assert.ThrowsExactly<XmlException>(() => Parse("<Backup><BackupInformation Mode=\"7\"></Backup>"));
    }

    [TestMethod]
    public void SevenZipReader_ReadsLegacyArchiveWithoutExtractingMetadata()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        Assert.IsTrue(File.Exists(sevenZipPath), sevenZipPath);
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"hmailserver-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var metadataPath = Path.Combine(tempDirectory, SevenZipBackupArchiveMetadataReader.MetadataEntryName);
            var archivePath = Path.Combine(tempDirectory, "sample.7z");
            File.WriteAllText(metadataPath, "<Backup><BackupInformation Mode=\"13\" /></Backup>");

            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                WorkingDirectory = tempDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("a");
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add(SevenZipBackupArchiveMetadataReader.MetadataEntryName);
            startInfo.ArgumentList.Add("-t7z");
            startInfo.ArgumentList.Add("-mmt");
            startInfo.ArgumentList.Add("-mx1");
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process);
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode);

            File.Delete(metadataPath);
            var reader = new SevenZipBackupArchiveMetadataReader(sevenZipPath);

            Assert.AreEqual(13, reader.ReadContainsOptions(archivePath));
            Assert.IsFalse(File.Exists(metadataPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static int Parse(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return SevenZipBackupArchiveMetadataReader.ParseContainsOptions(stream);
    }

    private static void AssertVariantBoolProperty(Type contract, string name, int dispatchId, bool canWrite)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(canWrite, property.CanWrite);
        if (canWrite)
        {
            Assert.AreEqual(UnmanagedType.VariantBool, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }
}
