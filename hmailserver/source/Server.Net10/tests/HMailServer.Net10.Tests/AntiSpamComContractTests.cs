using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class AntiSpamComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndDispatchIds()
    {
        var contract = typeof(IInterfaceAntiSpam);

        Assert.AreEqual(new Guid("998A7E66-21FA-47CC-9DB4-81822F2D05C9"), contract.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            ExpectedMethodNames(),
            contract.GetMethods()
                .OrderBy(static method => method.MetadataToken)
                .Select(static method => method.Name)
                .ToArray());

        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.GreyListingEnabled), 1);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.GreyListingInitialDelay), 2, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.GreyListingInitialDelete), 3, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.GreyListingFinalDelete), 4, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.SURBLServers), 6, typeof(IInterfaceSURBLServers), canWrite: false);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.CheckHostInHelo), 7);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.AddHeaderSpam), 8);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.AddHeaderReason), 9);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.PrependSubject), 10);
        AssertBstrProperty(contract, nameof(IInterfaceAntiSpam.PrependSubjectText), 11);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.GreyListingWhiteAddresses), 12, typeof(IInterfaceGreyListingWhiteAddresses), canWrite: false);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.WhiteListAddresses), 13, typeof(IInterfaceWhiteListAddresses), canWrite: false);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.CheckHostInHeloScore), 14, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.SpamMarkThreshold), 15, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.SpamDeleteThreshold), 16, typeof(int), canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.UseSPF), 17);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.UseMXChecks), 18);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.UseSPFScore), 19, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.UseMXChecksScore), 20, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.DNSBlackLists), 21, typeof(IInterfaceDNSBlackLists), canWrite: false);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.TarpitDelay), 22, typeof(int), canWrite: true);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.TarpitCount), 23, typeof(int), canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.SpamAssassinEnabled), 24);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.SpamAssassinScore), 25, typeof(int), canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.SpamAssassinMergeScore), 26);
        AssertBstrProperty(contract, nameof(IInterfaceAntiSpam.SpamAssassinHost), 27);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.SpamAssassinPort), 28, typeof(int), canWrite: true);
        AssertMethod(contract, nameof(IInterfaceAntiSpam.ClearGreyListingTriplets), 29, typeof(void), parameterCount: 0);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.MaximumMessageSize), 30, typeof(int), canWrite: true);
        AssertMethod(contract, nameof(IInterfaceAntiSpam.DKIMVerify), 31, typeof(ComDkimResult), parameterCount: 1);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.DKIMVerificationEnabled), 32);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.DKIMVerificationFailureScore), 33, typeof(int), canWrite: true);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.BypassGreylistingOnSPFSuccess), 34);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.BypassGreylistingOnMailFromMX), 35);
        AssertSpamAssassinTestMethod(contract);
        AssertVariantBoolProperty(contract, nameof(IInterfaceAntiSpam.CheckPTR), 37);
        AssertProperty(contract, nameof(IInterfaceAntiSpam.CheckPTRScore), 38, typeof(int), canWrite: true);
    }

    [TestMethod]
    public void Enums_PreserveLegacyDkimResultGuidAndValues()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD12"), typeof(ComDkimResult).GUID);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            Enum.GetValues<ComDkimResult>().Select(static value => Convert.ToInt32(value)).ToArray());
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(AntiSpam);

        Assert.AreEqual(new Guid("A0B91A99-BCE8-4939-94EC-0881E25A1E5B"), type.GUID);
        Assert.AreEqual("hMailServer.AntiSpam.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceAntiSpam), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var antiSpam = new AntiSpam();

        var scalarError = Assert.ThrowsExactly<COMException>(() => _ = antiSpam.GreyListingEnabled);
        var collectionError = Assert.ThrowsExactly<COMException>(() => _ = antiSpam.DNSBlackLists);
        var methodError = Assert.ThrowsExactly<COMException>(() => antiSpam.ClearGreyListingTriplets());
        var settingsError = Assert.ThrowsExactly<COMException>(() => _ = new Settings().AntiSpam);

        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, collectionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, methodError.ErrorCode);
        Assert.AreEqual(EAccessDenied, settingsError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesReadOnlyAntiSpamSnapshot()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingEnabled: true,
                AntiSpamGreyListingInitialDelay: 30,
                AntiSpamGreyListingInitialDelete: 48,
                AntiSpamGreyListingFinalDelete: 864,
                AntiSpamCheckHostInHelo: true,
                AntiSpamCheckHostInHeloScore: 2,
                AntiSpamCheckPtr: true,
                AntiSpamCheckPtrScore: 4,
                AntiSpamAddHeaderSpam: true,
                AntiSpamAddHeaderReason: false,
                AntiSpamPrependSubject: true,
                AntiSpamPrependSubjectText: "[SPAM]",
                AntiSpamSpamMarkThreshold: 5,
                AntiSpamSpamDeleteThreshold: 20,
                AntiSpamUseSpf: true,
                AntiSpamUseSpfScore: 3,
                AntiSpamUseMxChecks: true,
                AntiSpamUseMxChecksScore: 6,
                AntiSpamSpamAssassinEnabled: true,
                AntiSpamSpamAssassinScore: 7,
                AntiSpamSpamAssassinMergeScore: false,
                AntiSpamSpamAssassinHost: "spamd.example.test",
                AntiSpamSpamAssassinPort: 783,
                AntiSpamMaximumMessageSize: 1024,
                AntiSpamDkimVerificationEnabled: true,
                AntiSpamDkimVerificationFailureScore: 8,
                AntiSpamBypassGreylistingOnSpfSuccess: true,
                AntiSpamBypassGreylistingOnMailFromMx: false));

        var antiSpam = settings.AntiSpam;

        Assert.IsTrue(antiSpam.GreyListingEnabled);
        Assert.AreEqual(30, antiSpam.GreyListingInitialDelay);
        Assert.AreEqual(48, antiSpam.GreyListingInitialDelete);
        Assert.AreEqual(864, antiSpam.GreyListingFinalDelete);
        Assert.IsTrue(antiSpam.CheckHostInHelo);
        Assert.AreEqual(2, antiSpam.CheckHostInHeloScore);
        Assert.IsTrue(antiSpam.CheckPTR);
        Assert.AreEqual(4, antiSpam.CheckPTRScore);
        Assert.IsTrue(antiSpam.AddHeaderSpam);
        Assert.IsFalse(antiSpam.AddHeaderReason);
        Assert.IsTrue(antiSpam.PrependSubject);
        Assert.AreEqual("[SPAM]", antiSpam.PrependSubjectText);
        Assert.AreEqual(5, antiSpam.SpamMarkThreshold);
        Assert.AreEqual(20, antiSpam.SpamDeleteThreshold);
        Assert.IsTrue(antiSpam.UseSPF);
        Assert.AreEqual(3, antiSpam.UseSPFScore);
        Assert.IsTrue(antiSpam.UseMXChecks);
        Assert.AreEqual(6, antiSpam.UseMXChecksScore);
        Assert.AreEqual(0, antiSpam.TarpitDelay);
        Assert.AreEqual(0, antiSpam.TarpitCount);
        Assert.IsTrue(antiSpam.SpamAssassinEnabled);
        Assert.AreEqual(7, antiSpam.SpamAssassinScore);
        Assert.IsFalse(antiSpam.SpamAssassinMergeScore);
        Assert.AreEqual("spamd.example.test", antiSpam.SpamAssassinHost);
        Assert.AreEqual(783, antiSpam.SpamAssassinPort);
        Assert.AreEqual(1024, antiSpam.MaximumMessageSize);
        Assert.IsTrue(antiSpam.DKIMVerificationEnabled);
        Assert.AreEqual(8, antiSpam.DKIMVerificationFailureScore);
        Assert.IsTrue(antiSpam.BypassGreylistingOnSPFSuccess);
        Assert.IsFalse(antiSpam.BypassGreylistingOnMailFromMX);

        AssertPending(() => antiSpam.GreyListingEnabled = false);
        AssertPending(() => antiSpam.GreyListingInitialDelay = 10);
        AssertPending(() => antiSpam.GreyListingInitialDelete = 24);
        AssertPending(() => antiSpam.GreyListingFinalDelete = 720);
        AssertPending(() => antiSpam.CheckHostInHelo = false);
        AssertPending(() => antiSpam.CheckHostInHeloScore = 1);
        AssertPending(() => antiSpam.CheckPTR = false);
        AssertPending(() => antiSpam.CheckPTRScore = 1);
        AssertPending(() => antiSpam.AddHeaderSpam = false);
        AssertPending(() => antiSpam.AddHeaderReason = true);
        AssertPending(() => antiSpam.PrependSubject = false);
        AssertPending(() => antiSpam.PrependSubjectText = "[JUNK]");
        AssertPending(() => antiSpam.SpamMarkThreshold = 4);
        AssertPending(() => antiSpam.SpamDeleteThreshold = 10);
        AssertPending(() => antiSpam.UseSPF = false);
        AssertPending(() => antiSpam.UseSPFScore = 1);
        AssertPending(() => antiSpam.UseMXChecks = false);
        AssertPending(() => antiSpam.UseMXChecksScore = 1);
        AssertPending(() => antiSpam.TarpitDelay = 1);
        AssertPending(() => antiSpam.TarpitCount = 1);
        AssertPending(() => antiSpam.SpamAssassinEnabled = false);
        AssertPending(() => antiSpam.SpamAssassinScore = 5);
        AssertPending(() => antiSpam.SpamAssassinMergeScore = true);
        AssertPending(() => antiSpam.SpamAssassinHost = "127.0.0.1");
        AssertPending(() => antiSpam.SpamAssassinPort = 1783);
        AssertPending(() => antiSpam.MaximumMessageSize = 2048);
        AssertPending(() => antiSpam.DKIMVerificationEnabled = false);
        AssertPending(() => antiSpam.DKIMVerificationFailureScore = 4);
        AssertPending(() => antiSpam.BypassGreylistingOnSPFSuccess = false);
        AssertPending(() => antiSpam.BypassGreylistingOnMailFromMX = true);
    }

    [TestMethod]
    public void AuthorizedAntiSpam_KeepsCollectionsAndOperationsPending()
    {
        IInterfaceAntiSpam antiSpam = AntiSpam.CreateAuthorized(new AntiSpamAdministrationSnapshot());

        AssertPending(() => _ = antiSpam.SURBLServers);
        AssertPending(() => _ = antiSpam.GreyListingWhiteAddresses);
        AssertPending(() => _ = antiSpam.WhiteListAddresses);
        AssertPending(() => _ = antiSpam.DNSBlackLists);
        AssertPending(() => antiSpam.ClearGreyListingTriplets());
        AssertPending(() => _ = antiSpam.DKIMVerify(@"C:\mail\message.eml"));

        var resultText = "not-empty";
        AssertPending(() => antiSpam.TestSpamAssassinConnection("127.0.0.1", 783, out resultText));
        Assert.AreEqual(string.Empty, resultText);
    }

    private static string[] ExpectedMethodNames()
    {
        var members = new (string Name, bool Property, bool Writable)[]
        {
            ("GreyListingEnabled", true, true),
            ("GreyListingInitialDelay", true, true),
            ("GreyListingInitialDelete", true, true),
            ("GreyListingFinalDelete", true, true),
            ("SURBLServers", true, false),
            ("CheckHostInHelo", true, true),
            ("AddHeaderSpam", true, true),
            ("AddHeaderReason", true, true),
            ("PrependSubject", true, true),
            ("PrependSubjectText", true, true),
            ("GreyListingWhiteAddresses", true, false),
            ("WhiteListAddresses", true, false),
            ("CheckHostInHeloScore", true, true),
            ("SpamMarkThreshold", true, true),
            ("SpamDeleteThreshold", true, true),
            ("UseSPF", true, true),
            ("UseMXChecks", true, true),
            ("UseSPFScore", true, true),
            ("UseMXChecksScore", true, true),
            ("DNSBlackLists", true, false),
            ("TarpitDelay", true, true),
            ("TarpitCount", true, true),
            ("SpamAssassinEnabled", true, true),
            ("SpamAssassinScore", true, true),
            ("SpamAssassinMergeScore", true, true),
            ("SpamAssassinHost", true, true),
            ("SpamAssassinPort", true, true),
            ("ClearGreyListingTriplets", false, false),
            ("MaximumMessageSize", true, true),
            ("DKIMVerify", false, false),
            ("DKIMVerificationEnabled", true, true),
            ("DKIMVerificationFailureScore", true, true),
            ("BypassGreylistingOnSPFSuccess", true, true),
            ("BypassGreylistingOnMailFromMX", true, true),
            ("TestSpamAssassinConnection", false, false),
            ("CheckPTR", true, true),
            ("CheckPTRScore", true, true)
        };

        return members
            .SelectMany(static member => member.Property
                ? member.Writable
                    ? new[] { $"get_{member.Name}", $"set_{member.Name}" }
                    : new[] { $"get_{member.Name}" }
                : new[] { member.Name })
            .ToArray();
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

    private static void AssertMethod(Type contract, string name, int dispatchId, Type returnType, int parameterCount)
    {
        var method = contract.GetMethod(name);

        Assert.IsNotNull(method);
        Assert.AreEqual(dispatchId, method.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(returnType, method.ReturnType);
        Assert.AreEqual(parameterCount, method.GetParameters().Length);
    }

    private static void AssertSpamAssassinTestMethod(Type contract)
    {
        var method = contract.GetMethod(nameof(IInterfaceAntiSpam.TestSpamAssassinConnection));

        Assert.IsNotNull(method);
        Assert.AreEqual(36, method.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(bool), method.ReturnType);
        Assert.AreEqual(UnmanagedType.VariantBool, method.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(3, method.GetParameters().Length);
        Assert.AreEqual(UnmanagedType.BStr, method.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(typeof(int), method.GetParameters()[1].ParameterType);
        Assert.AreEqual(UnmanagedType.BStr, method.GetParameters()[2].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.IsTrue(method.GetParameters()[2].IsOut);
    }

    private static void AssertPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }
}
