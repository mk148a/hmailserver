using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SettingsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void Interface_PreservesLegacyIidCompleteVtableAndMessageIndexingSlot()
    {
        var contract = typeof(IInterfaceSettings);
        var methods = contract.GetMethods().OrderBy(static method => method.MetadataToken).ToArray();

        Assert.AreEqual(new Guid("A4C709A3-98B2-410D-84F4-EDA999BF0CB2"), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(ExpectedMethodNames(), methods.Select(static method => method.Name).ToArray());
        Assert.AreEqual(
            89,
            contract.GetProperty(nameof(IInterfaceSettings.MessageIndexing))?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Settings);

        Assert.AreEqual(new Guid("FDF084A7-82DE-4EBE-8455-E506ACE01D63"), type.GUID);
        Assert.AreEqual("hMailServer.Settings.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceSettings), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_DeniesLegacySettingsAccess()
    {
        var settings = new Settings();

        var indexingError = Assert.ThrowsExactly<COMException>(() => _ = settings.MessageIndexing);
        var scalarError = Assert.ThrowsExactly<COMException>(() => _ = ((IInterfaceSettings)settings).MaxSMTPConnections);

        Assert.AreEqual(EAccessDenied, indexingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ReturnsRuntimeBoundMessageIndexingAndKeepsOtherMembersExplicit()
    {
        MessageIndexingRuntimeHost.Configure(new FixedMessageIndexingRuntime(42));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        Assert.AreEqual(42, settings.MessageIndexing.TotalMessageCount);
        var unimplemented = Assert.ThrowsExactly<COMException>(() => _ = settings.MaxSMTPConnections);
        Assert.AreEqual(ENotImplemented, unimplemented.ErrorCode);
    }

    private static string[] ExpectedMethodNames()
    {
        var members = new (string Name, bool Property, bool Writable)[]
        {
            ("MaxSMTPConnections", true, true),
            ("MaxPOP3Connections", true, true),
            ("MirrorEMailAddress", true, true),
            ("AllowSMTPAuthPlain", true, true),
            ("DenyMailFromNull", true, true),
            ("Logging", true, false),
            ("SecurityRanges", true, false),
            ("SMTPNoOfTries", true, true),
            ("SMTPMinutesBetweenTry", true, true),
            ("SMTPRelayer", true, true),
            ("WelcomeSMTP", true, true),
            ("WelcomePOP3", true, true),
            ("WelcomeIMAP", true, true),
            ("ServiceSMTP", true, true),
            ("ServicePOP3", true, true),
            ("ServiceIMAP", true, true),
            ("MaxDeliveryThreads", true, true),
            ("AntiVirus", true, false),
            ("Routes", true, false),
            ("HostName", true, true),
            ("SMTPRelayerRequiresAuthentication", true, true),
            ("SMTPRelayerUsername", true, true),
            ("SetSMTPRelayerPassword", false, false),
            ("SMTPRelayerPort", true, true),
            ("UserInterfaceLanguage", true, true),
            ("Scripting", true, false),
            ("MaxMessageSize", true, true),
            ("Cache", true, false),
            ("RuleLoopLimit", true, true),
            ("Backup", true, false),
            ("DefaultDomain", true, true),
            ("SMTPDeliveryBindToIP", true, true),
            ("MaxIMAPConnections", true, true),
            ("IMAPSortEnabled", true, true),
            ("IMAPQuotaEnabled", true, true),
            ("IMAPIdleEnabled", true, true),
            ("WorkerThreadPriority", true, true),
            ("TCPIPThreads", true, true),
            ("AllowIncorrectLineEndings", true, true),
            ("MaxSMTPRecipientsInBatch", true, true),
            ("AntiSpam", true, false),
            ("DisconnectInvalidClients", true, true),
            ("MaxNumberOfInvalidCommands", true, true),
            ("ServerMessages", true, false),
            ("TCPIPPorts", true, false),
            ("SMTPRelayerUseSSL", true, true),
            ("SSLCertificates", true, false),
            ("AddDeliveredToHeader", true, true),
            ("IMAPPublicFolderName", true, true),
            ("IMAPACLEnabled", true, true),
            ("SetAdministratorPassword", false, false),
            ("Directories", true, false),
            ("PublicFolders", true, false),
            ("PublicFolderDiskName", true, false),
            ("Groups", true, false),
            ("IncomingRelays", true, false),
            ("AutoBanOnLogonFailure", true, true),
            ("MaxInvalidLogonAttempts", true, true),
            ("MaxInvalidLogonAttemptsWithin", true, true),
            ("AutoBanMinutes", true, true),
            ("ClearLogonFailureList", false, false),
            ("IMAPHierarchyDelimiter", true, true),
            ("MaxAsynchronousThreads", true, true),
            ("MessageIndexing", true, false),
            ("MaxNumberOfMXHosts", true, true),
            ("SMTPRelayerConnectionSecurity", true, true),
            ("SMTPConnectionSecurity", true, true),
            ("VerifyRemoteSslCertificate", true, true),
            ("SslCipherList", true, true),
            ("TlsVersion10Enabled", true, true),
            ("TlsVersion11Enabled", true, true),
            ("TlsVersion12Enabled", true, true),
            ("CrashSimulationMode", true, true),
            ("IMAPMasterUser", true, true),
            ("IMAPSASLPlainEnabled", true, true),
            ("IMAPSASLInitialResponseEnabled", true, true),
            ("TlsVersion13Enabled", true, true),
            ("IPv6PreferredEnabled", true, true),
            ("TlsOptionPreferServerCiphersEnabled", true, true),
            ("TlsOptionPrioritizeChaChaEnabled", true, true),
            ("RewriteEnvelopeFromWhenForwarding", true, true)
        };

        return members
            .SelectMany(static member => member.Property
                ? member.Writable
                    ? new[] { $"get_{member.Name}", $"set_{member.Name}" }
                    : new[] { $"get_{member.Name}" }
                : new[] { member.Name })
            .ToArray();
    }

    private sealed class FixedMessageIndexingRuntime(int totalMessageCount) : IMessageIndexingRuntime
    {
        public int TotalMessageCount => totalMessageCount;
        public int TotalIndexedCount => 0;
        public bool Enabled { get; set; }
        public string Backend => string.Empty;
        public bool IsFullTextReady => false;
        public string BackfillStatus => string.Empty;
        public string LastError => string.Empty;
        public void Clear() { }
        public void Index() { }
        public void Rebuild() { }
    }
}
