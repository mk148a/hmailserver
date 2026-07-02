using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class AntiVirusComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndDispatchIds()
    {
        var contract = typeof(IInterfaceAntiVirus);

        Assert.AreEqual(new Guid("952EE84F-C1D4-4869-8B86-76A3BA8F39FA"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                "get_ClamWinEnabled",
                "set_ClamWinEnabled",
                "get_ClamWinExecutable",
                "set_ClamWinExecutable",
                "get_ClamWinDBFolder",
                "set_ClamWinDBFolder",
                "get_Action",
                "set_Action",
                "get_NotifyReceiver",
                "set_NotifyReceiver",
                "get_NotifySender",
                "set_NotifySender",
                "get_CustomScannerEnabled",
                "set_CustomScannerEnabled",
                "get_CustomScannerExecutable",
                "set_CustomScannerExecutable",
                "get_CustomScannerReturnValue",
                "set_CustomScannerReturnValue",
                "get_MaximumMessageSize",
                "set_MaximumMessageSize",
                "get_BlockedAttachments",
                "get_EnableAttachmentBlocking",
                "set_EnableAttachmentBlocking",
                "get_ClamAVEnabled",
                "set_ClamAVEnabled",
                "get_ClamAVHost",
                "set_ClamAVHost",
                "get_ClamAVPort",
                "set_ClamAVPort",
                "TestCustomerScanner",
                "TestClamWinScanner",
                "TestClamAVScanner"
            },
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiVirus.ClamWinEnabled), 1);
        AssertBstrProperty(contract, nameof(IInterfaceAntiVirus.ClamWinExecutable), 2);
        AssertBstrProperty(contract, nameof(IInterfaceAntiVirus.ClamWinDBFolder), 3);
        AssertProperty(contract, nameof(IInterfaceAntiVirus.Action), 4, typeof(ComAntivirusAction), canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiVirus.NotifyReceiver), 5);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiVirus.NotifySender), 6);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiVirus.CustomScannerEnabled), 7);
        AssertBstrProperty(contract, nameof(IInterfaceAntiVirus.CustomScannerExecutable), 8);
        AssertProperty(contract, nameof(IInterfaceAntiVirus.CustomScannerReturnValue), 9, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiVirus.MaximumMessageSize), 10, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiVirus.BlockedAttachments), 11, typeof(IInterfaceBlockedAttachments), canWrite: false);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiVirus.EnableAttachmentBlocking), 12);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiVirus.ClamAVEnabled), 13);
        AssertBstrProperty(contract, nameof(IInterfaceAntiVirus.ClamAVHost), 14);
        AssertProperty(contract, nameof(IInterfaceAntiVirus.ClamAVPort), 15, typeof(int), canWrite: true);
        AssertScannerMethod(contract, nameof(IInterfaceAntiVirus.TestCustomerScanner), 16, 3);
        AssertScannerMethod(contract, nameof(IInterfaceAntiVirus.TestClamWinScanner), 17, 3);
        AssertScannerMethod(contract, nameof(IInterfaceAntiVirus.TestClamAVScanner), 18, 3);
    }

    [TestMethod]
    public void Enums_PreserveLegacyGuidsAndValues()
    {
        Assert.AreEqual(new Guid("FD97B388-4C39-11D9-8361-94B829D736A2"), typeof(ComAntivirusAction).GUID);
        CollectionAssert.AreEqual(
            new[] { 0, 1 },
            Enum.GetValues<ComAntivirusAction>().Select(static value => Convert.ToInt32(value)).ToArray());
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(AntiVirus);

        Assert.AreEqual(new Guid("82D6DBF9-DDDB-4C4A-A52A-92B6ED16D8EA"), type.GUID);
        Assert.AreEqual("hMailServer.AntiVirus.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceAntiVirus), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var antivirus = new AntiVirus();

        var scalarError = Assert.ThrowsExactly<COMException>(() => _ = antivirus.ClamWinEnabled);
        var blockedAttachmentsError = Assert.ThrowsExactly<COMException>(() => _ = antivirus.BlockedAttachments);
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().AntiVirus);

        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, blockedAttachmentsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesReadOnlyAntiVirusSnapshot()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinEnabled: true,
                AntiVirusClamWinExecutable: @"C:\ClamWin\bin\clamscan.exe",
                AntiVirusClamWinDatabase: @"C:\ClamWin\db",
                AntiVirusAction: 1,
                AntiVirusNotifyReceiver: true,
                AntiVirusNotifySender: false,
                AntiVirusCustomScannerEnabled: true,
                AntiVirusCustomScannerExecutable: @"C:\Tools\virus-scan.cmd",
                AntiVirusCustomScannerReturnValue: 7,
                AntiVirusMaximumMessageSize: 4096,
                AntiVirusEnableAttachmentBlocking: true,
                AntiVirusClamAvEnabled: true,
                AntiVirusClamAvHost: "127.0.0.1",
                AntiVirusClamAvPort: 3310));

        var antivirus = settings.AntiVirus;

        Assert.IsTrue(antivirus.ClamWinEnabled);
        Assert.AreEqual(@"C:\ClamWin\bin\clamscan.exe", antivirus.ClamWinExecutable);
        Assert.AreEqual(@"C:\ClamWin\db", antivirus.ClamWinDBFolder);
        Assert.AreEqual(ComAntivirusAction.DeleteAttachments, antivirus.Action);
        Assert.IsTrue(antivirus.NotifyReceiver);
        Assert.IsFalse(antivirus.NotifySender);
        Assert.IsTrue(antivirus.CustomScannerEnabled);
        Assert.AreEqual(@"C:\Tools\virus-scan.cmd", antivirus.CustomScannerExecutable);
        Assert.AreEqual(7, antivirus.CustomScannerReturnValue);
        Assert.AreEqual(4096, antivirus.MaximumMessageSize);
        Assert.IsTrue(antivirus.EnableAttachmentBlocking);
        Assert.IsTrue(antivirus.ClamAVEnabled);
        Assert.AreEqual("127.0.0.1", antivirus.ClamAVHost);
        Assert.AreEqual(3310, antivirus.ClamAVPort);

        AssertPending(() => antivirus.ClamWinEnabled = false);
        AssertPending(() => antivirus.ClamWinExecutable = @"D:\Other\clamscan.exe");
        AssertPending(() => antivirus.ClamWinDBFolder = @"D:\Other\db");
        AssertPending(() => antivirus.Action = ComAntivirusAction.DeleteEmail);
        AssertPending(() => antivirus.NotifyReceiver = false);
        AssertPending(() => antivirus.NotifySender = true);
        AssertPending(() => antivirus.CustomScannerEnabled = false);
        AssertPending(() => antivirus.CustomScannerExecutable = @"D:\Other\scan.cmd");
        AssertPending(() => antivirus.CustomScannerReturnValue = 1);
        AssertPending(() => antivirus.MaximumMessageSize = 2048);
        AssertPending(() => _ = antivirus.BlockedAttachments);
        AssertPending(() => antivirus.EnableAttachmentBlocking = false);
        AssertPending(() => antivirus.ClamAVEnabled = false);
        AssertPending(() => antivirus.ClamAVHost = "clamav.example.test");
        AssertPending(() => antivirus.ClamAVPort = 3311);

        var resultText = "not-empty";
        AssertPending(() => antivirus.TestCustomerScanner(@"C:\scan.cmd", 7, out resultText));
        Assert.AreEqual(string.Empty, resultText);
        resultText = "not-empty";
        AssertPending(() => antivirus.TestClamWinScanner(@"C:\clamscan.exe", @"C:\db", out resultText));
        Assert.AreEqual(string.Empty, resultText);
        resultText = "not-empty";
        AssertPending(() => antivirus.TestClamAVScanner("127.0.0.1", 3310, out resultText));
        Assert.AreEqual(string.Empty, resultText);
    }

    [TestMethod]
    public void AuthorizedSettings_InvalidAntiVirusActionFallsBackToLegacyDeleteEmailDefault()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusAction: 99));

        Assert.AreEqual(ComAntivirusAction.DeleteEmail, settings.AntiVirus.Action);
    }

    private static void AssertBstrProperty(Type contract, string name, int dispatchId)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static void AssertVariantBoolProperty(Type contract, string name, int dispatchId)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    private static void AssertProperty(Type contract, string name, int dispatchId, Type propertyType, bool canWrite)
    {
        var property = contract.GetProperty(name);

        Assert.IsNotNull(property);
        Assert.AreEqual(dispatchId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(propertyType, property.PropertyType);
        Assert.AreEqual(canWrite, property.CanWrite);
    }

    private static void AssertScannerMethod(Type contract, string name, int dispatchId, int parameterCount)
    {
        var method = contract.GetMethod(name);

        Assert.IsNotNull(method);
        Assert.AreEqual(dispatchId, method.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(bool), method.ReturnType);
        Assert.AreEqual(UnmanagedType.VariantBool, method.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(parameterCount, method.GetParameters().Length);
        Assert.AreEqual(UnmanagedType.BStr, method.GetParameters().Last().GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.IsTrue(method.GetParameters().Last().IsOut);
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }
}
