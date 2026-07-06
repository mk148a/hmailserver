using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class UtilitiesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EInvalidArgument = unchecked((int)0x80070057);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidEnumsDispatchIdsAndCompleteVtableOrder()
    {
        Assert.AreEqual(
            new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD03"),
            typeof(ComRuleMatchType).GUID);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 9).ToArray(),
            Enum.GetValues<ComRuleMatchType>().Select(static value => (int)value).ToArray());
        Assert.AreEqual(
            new Guid("87FDF5A8-567E-4BDD-B5E0-4742D4801A92"),
            typeof(ComMaintenanceOperation).GUID);
        CollectionAssert.AreEqual(
            new[] { 1 },
            Enum.GetValues<ComMaintenanceOperation>().Select(static value => (int)value).ToArray());

        var type = typeof(IInterfaceUtilities);
        Assert.AreEqual(new Guid("F6BB0F43-EDEE-49A8-8166-672F3017426F"), type.GUID);
        Assert.AreEqual(
            ComInterfaceType.InterfaceIsDual,
            type.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            type.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        var methods = type
            .GetMethods()
            .OrderBy(static method => method.MetadataToken)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "GetMailServer",
                "IsValidEmailAddress",
                "IsValidDomainName",
                "MD5",
                "BlowfishEncrypt",
                "BlowfishDecrypt",
                "MakeDependent",
                "ImportMessageFromFile",
                "EmailAllAccounts",
                "GenerateGUID",
                "RunTestSuite",
                "IsLocalHost",
                "ImportMessageFromFileToIMAPFolder",
                "IsStrongPassword",
                "SHA256",
                "CriteriaMatch",
                "RetrieveMessageID",
                "IsValidIPAddress",
                "PerformMaintenance"
            },
            methods.Select(static method => method.Name).ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 19).ToArray(),
            methods
                .Select(static method => method.GetCustomAttribute<DispIdAttribute>()?.Value ?? -1)
                .ToArray());
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            methods
                .Single(static method => method.Name == nameof(IInterfaceUtilities.IsLocalHost))
                .ReturnParameter
                .GetCustomAttribute<MarshalAsAttribute>()
                ?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Utilities);

        Assert.AreEqual(new Guid("E116DCB7-7FEC-4540-BEA1-FA1B19D05B5F"), type.GUID);
        Assert.AreEqual("hMailServer.Utilities.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceUtilities), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_ExposesPureHelpersAndPreservesAdministrativeBoundary()
    {
        IInterfaceUtilities utilities = new Utilities();

        Assert.AreEqual("dc647eb65e6711e155375218212b3964", utilities.MD5("Password"));
        Assert.IsTrue(utilities.IsValidEmailAddress("user@example.test"));
        Assert.IsTrue(utilities.IsValidDomainName("example.test"));
        Assert.IsTrue(utilities.IsValidIPAddress("127.0.0.1"));

        AssertOperationPending(() => _ = utilities.GetMailServer("user@example.test"));
        AssertOperationPending(() => _ = utilities.BlowfishEncrypt("secret"));
        AssertOperationPending(() => _ = utilities.BlowfishDecrypt("secret"));
        AssertOperationPending(() => _ = utilities.IsLocalHost("localhost"));

        var denied = Assert.ThrowsExactly<COMException>(() => utilities.MakeDependent("MSSQLSERVER"));
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
    }

    [TestMethod]
    public void ApplicationUtilities_SharesAuthenticationAndKeepsSideEffectsPending()
    {
        var cipher = new RecordingLegacyBlowfishCipher();
        var localHostRuntime = new RecordingLocalHostRuntime("local.example.test");
        var application = new Application(
            new RecordingAdministratorAuthenticationProvider("secret"),
            legacyBlowfishCipher: cipher,
            localHostRuntime: localHostRuntime);
        var utilities = application.Utilities;

        Assert.AreEqual("dc647eb65e6711e155375218212b3964", utilities.MD5("Password"));
        Assert.AreEqual("encrypted:secret", utilities.BlowfishEncrypt("secret"));
        Assert.AreEqual("plain:ciphertext", utilities.BlowfishDecrypt("ciphertext"));
        CollectionAssert.AreEqual(
            new[] { "encrypt:secret", "decrypt:ciphertext" },
            cipher.Calls);
        Assert.IsTrue(utilities.IsLocalHost("local.example.test"));
        Assert.IsFalse(utilities.IsLocalHost("remote.example.test"));
        CollectionAssert.AreEqual(
            new[] { "local.example.test", "remote.example.test" },
            localHostRuntime.Calls);
        var denied = Assert.ThrowsExactly<COMException>(() => utilities.MakeDependent("MSSQLSERVER"));
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        AssertOperationPending(() => utilities.MakeDependent("MSSQLSERVER"));
        AssertOperationPending(() => _ = utilities.ImportMessageFromFile("message.eml", 1));
        AssertOperationPending(
            () => _ = utilities.EmailAllAccounts("*@example.test", "admin@example.test", "Admin", "Subject", "Body"));
        AssertOperationPending(() => utilities.RunTestSuite("I know what I am doing."));
        AssertOperationPending(
            () => _ = utilities.ImportMessageFromFileToIMAPFolder("message.eml", 1, "Inbox"));
        AssertOperationPending(() => _ = utilities.RetrieveMessageID("message.eml"));
        AssertOperationPending(
            () => utilities.PerformMaintenance(ComMaintenanceOperation.UpdateImapFolderUid));
    }

    [TestMethod]
    public void RuntimeUtilities_ExposeLegacyBlowfishAndLocalHostWithoutAuthentication()
    {
        var localHostRuntime = new RecordingLocalHostRuntime("127.0.0.1");
        IInterfaceUtilities utilities =
            Utilities.CreateForRuntime(new LegacyBlowfishCipherRuntime(), localHostRuntime);

        Assert.AreEqual("a62b3c438efae3db", utilities.BlowfishEncrypt("secret"));
        Assert.AreEqual("e79ca726380cc3b1", utilities.BlowfishEncrypt("Hejsan"));
        Assert.AreEqual("53017df649201454294938b861b56ab2", utilities.BlowfishEncrypt("Secret123"));
        Assert.AreEqual(string.Empty, utilities.BlowfishEncrypt(string.Empty));
        Assert.AreEqual(string.Empty, utilities.BlowfishDecrypt(string.Empty));
        Assert.AreEqual("secret", utilities.BlowfishDecrypt("a62b3c438efae3db"));
        Assert.IsTrue(utilities.IsLocalHost("127.0.0.1"));
        Assert.IsFalse(utilities.IsLocalHost("192.0.2.1"));

        var latin1 = "p\u00E4ssw\u00F6rd";
        Assert.AreEqual(latin1, utilities.BlowfishDecrypt(utilities.BlowfishEncrypt(latin1)));

        var invalid = Assert.ThrowsExactly<COMException>(() => utilities.BlowfishDecrypt("not-hex"));
        Assert.AreEqual(EInvalidArgument, invalid.ErrorCode);
    }

    [TestMethod]
    public void PureHelpers_PreserveLegacyHashValidationAndParsingBehavior()
    {
        IInterfaceUtilities utilities = new Utilities();

        var firstHash = utilities.SHA256("Password");
        var secondHash = utilities.SHA256("Password");
        Assert.AreEqual(70, firstHash.Length);
        Assert.AreEqual(70, secondHash.Length);
        Assert.AreNotEqual(firstHash, secondHash);
        Assert.IsTrue(
            LegacyPasswordVerifier.Verify(
                "Password",
                firstHash,
                LegacyPasswordEncryptionType.SHA256));

        var guid = utilities.GenerateGUID();
        Assert.IsTrue(guid.StartsWith('{'));
        Assert.IsTrue(guid.EndsWith('}'));
        Assert.IsTrue(Guid.TryParse(guid, out _));

        Assert.IsTrue(utilities.IsValidEmailAddress("\"va ff\"@example.test"));
        Assert.IsFalse(utilities.IsValidEmailAddress("us..er@example.test"));
        Assert.IsTrue(
            utilities.IsValidEmailAddress(
                "user@[IPv6:2001:0db8:85a3:0000:0000:8a2e:0370:7334]"));
        Assert.IsFalse(utilities.IsValidDomainName("-example.test"));
        Assert.IsTrue(utilities.IsValidDomainName("sub.example.test"));
        Assert.IsTrue(utilities.IsValidDomainName("[192.168.1.1]"));
        Assert.IsTrue(utilities.IsValidIPAddress("2001:db8::1428:7ab"));
        Assert.IsFalse(utilities.IsValidIPAddress("127.0.0"));
        Assert.IsFalse(utilities.IsValidIPAddress("127.0.0.A"));
        Assert.IsFalse(utilities.IsValidIPAddress("999.999.999.999"));

        Assert.IsFalse(utilities.IsStrongPassword("testar@example.test", "testar"));
        Assert.IsFalse(utilities.IsStrongPassword("vaffe@example.test", "testar"));
        Assert.IsFalse(utilities.IsStrongPassword("vaffe@example.test", "secret"));
        Assert.IsTrue(utilities.IsStrongPassword("vaffe@example.test", "testarp"));
        Assert.IsTrue(utilities.IsStrongPassword("vaffe@example.test", "test_"));

        Assert.IsTrue(utilities.CriteriaMatch("Test", ComRuleMatchType.Equals, "test"));
        Assert.IsTrue(utilities.CriteriaMatch("two", ComRuleMatchType.Contains, "one-TWO-three"));
        Assert.IsTrue(utilities.CriteriaMatch("5", ComRuleMatchType.LessThan, "4"));
        Assert.IsTrue(utilities.CriteriaMatch("5", ComRuleMatchType.GreaterThan, "6"));
        Assert.IsTrue(utilities.CriteriaMatch("Test*", ComRuleMatchType.Wildcard, "testar!"));
        Assert.IsFalse(utilities.CriteriaMatch("Test*", ComRuleMatchType.Wildcard, "tesb"));
    }

    private static void AssertOperationPending(Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    private sealed class RecordingAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class RecordingLegacyBlowfishCipher : ILegacyBlowfishCipher
    {
        public List<string> Calls { get; } = [];

        public string Encrypt(string input)
        {
            Calls.Add($"encrypt:{input}");
            return $"encrypted:{input}";
        }

        public bool TryDecrypt(string input, out string output)
        {
            Calls.Add($"decrypt:{input}");
            output = $"plain:{input}";
            return true;
        }
    }

    private sealed class RecordingLocalHostRuntime(string localHost) : ILocalHostRuntime
    {
        public List<string> Calls { get; } = [];

        public bool IsLocalHost(string hostName)
        {
            Calls.Add(hostName);
            return string.Equals(hostName, localHost, StringComparison.OrdinalIgnoreCase);
        }
    }
}
