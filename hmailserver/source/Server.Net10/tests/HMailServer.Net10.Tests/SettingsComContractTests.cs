using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

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
    public void BooleanProperties_PreserveLegacyDispidsAndVariantBoolMarshaling()
    {
        var expected = new[]
        {
            (Name: nameof(IInterfaceSettings.ServiceSMTP), DispId: 26),
            (Name: nameof(IInterfaceSettings.ServicePOP3), DispId: 27),
            (Name: nameof(IInterfaceSettings.ServiceIMAP), DispId: 28),
            (Name: nameof(IInterfaceSettings.DisconnectInvalidClients), DispId: 64)
        };

        foreach (var item in expected)
        {
            var property = typeof(IInterfaceSettings).GetProperty(item.Name);

            Assert.IsNotNull(property);
            Assert.AreEqual(item.DispId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
            Assert.AreEqual(
                UnmanagedType.VariantBool,
                property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
            Assert.AreEqual(
                UnmanagedType.VariantBool,
                property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    [TestMethod]
    public void DirectActivation_DeniesLegacySettingsAccess()
    {
        var settings = new Settings();

        var indexingError = Assert.ThrowsExactly<COMException>(() => _ = settings.MessageIndexing);
        var scalarError = Assert.ThrowsExactly<COMException>(() => _ = ((IInterfaceSettings)settings).MaxSMTPConnections);
        var hostNameError = Assert.ThrowsExactly<COMException>(() => _ = settings.HostName);

        Assert.AreEqual(EAccessDenied, indexingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, hostNameError.ErrorCode);
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

    [TestMethod]
    public void AuthorizedSettings_ExposesReadOnlyBoundedAdministrationScalars()
    {
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: "SMTP ready",
                WelcomePop3: "POP3 ready",
                WelcomeImap: "IMAP ready",
                MaxSmtpConnections: 100,
                MaxPop3Connections: 50,
                MaxImapConnections: 75,
                MaxDeliveryThreads: 10,
                ServiceSmtp: true,
                ServicePop3: false,
                ServiceImap: true,
                SmtpNoOfTries: 4,
                SmtpMinutesBetweenTry: 60,
                MaxMessageSize: 20480,
                MaxSmtpRecipientsInBatch: 100,
                DisconnectInvalidClients: true,
                MaxNumberOfInvalidCommands: 12));

        Assert.AreEqual("mail.example.test", settings.HostName);
        Assert.AreEqual("SMTP ready", settings.WelcomeSMTP);
        Assert.AreEqual("POP3 ready", settings.WelcomePOP3);
        Assert.AreEqual("IMAP ready", settings.WelcomeIMAP);
        Assert.AreEqual(100, settings.MaxSMTPConnections);
        Assert.AreEqual(50, settings.MaxPOP3Connections);
        Assert.AreEqual(75, settings.MaxIMAPConnections);
        Assert.AreEqual(10, settings.MaxDeliveryThreads);
        Assert.IsTrue(settings.ServiceSMTP);
        Assert.IsFalse(settings.ServicePOP3);
        Assert.IsTrue(settings.ServiceIMAP);
        Assert.AreEqual(4, settings.SMTPNoOfTries);
        Assert.AreEqual(60, settings.SMTPMinutesBetweenTry);
        Assert.AreEqual(20480, settings.MaxMessageSize);
        Assert.AreEqual(100, settings.MaxSMTPRecipientsInBatch);
        Assert.IsTrue(settings.DisconnectInvalidClients);
        Assert.AreEqual(12, settings.MaxNumberOfInvalidCommands);

        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.HostName = "changed.example.test").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.WelcomeSMTP = "changed").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.WelcomePOP3 = "changed").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.WelcomeIMAP = "changed").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxSMTPConnections = 200).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxPOP3Connections = 200).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxIMAPConnections = 200).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxDeliveryThreads = 20).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.ServiceSMTP = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.ServicePOP3 = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.ServiceIMAP = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPNoOfTries = 8).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPMinutesBetweenTry = 30).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxMessageSize = 10240).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxSMTPRecipientsInBatch = 50).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.DisconnectInvalidClients = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxNumberOfInvalidCommands = 6).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ReturnsOnlyConfiguredPublicRootFolders()
    {
        ImapFolderAdministrationRuntimeHost.Configure(
            new FixedImapFolderAdministrationStore(
                new[]
                {
                    new ImapFolderAdministrationSnapshot(10, 0, -1, "Public", true, 4, "2026-06-27 01:02:03"),
                    new ImapFolderAdministrationSnapshot(20, 100, -1, "Account", true, 1, "2026-06-27 01:02:03")
                }));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        var publicFolders = settings.PublicFolders;

        Assert.AreEqual(1, publicFolders.Count);
        Assert.AreEqual("Public", publicFolders[0].Name);
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

    private sealed class FixedImapFolderAdministrationStore(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders) : IImapFolderAdministrationStore
    {
        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                folders.Where(folder => folder.AccountId == accountId && folder.ParentId == -1)
                    .OrderBy(folder => folder.Id)
                    .ToArray());
    }
}
