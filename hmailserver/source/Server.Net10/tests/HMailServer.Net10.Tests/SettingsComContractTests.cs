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
            (Name: nameof(IInterfaceSettings.AllowSMTPAuthPlain), DispId: 8),
            (Name: nameof(IInterfaceSettings.DenyMailFromNull), DispId: 11),
            (Name: nameof(IInterfaceSettings.ServiceSMTP), DispId: 26),
            (Name: nameof(IInterfaceSettings.ServicePOP3), DispId: 27),
            (Name: nameof(IInterfaceSettings.ServiceIMAP), DispId: 28),
            (Name: nameof(IInterfaceSettings.SMTPRelayerRequiresAuthentication), DispId: 34),
            (Name: nameof(IInterfaceSettings.IMAPSortEnabled), DispId: 54),
            (Name: nameof(IInterfaceSettings.IMAPQuotaEnabled), DispId: 55),
            (Name: nameof(IInterfaceSettings.IMAPIdleEnabled), DispId: 56),
            (Name: nameof(IInterfaceSettings.AllowIncorrectLineEndings), DispId: 61),
            (Name: nameof(IInterfaceSettings.DisconnectInvalidClients), DispId: 64),
            (Name: nameof(IInterfaceSettings.AddDeliveredToHeader), DispId: 73),
            (Name: nameof(IInterfaceSettings.IMAPACLEnabled), DispId: 75),
            (Name: nameof(IInterfaceSettings.AutoBanOnLogonFailure), DispId: 82),
            (Name: nameof(IInterfaceSettings.VerifyRemoteSslCertificate), DispId: 93),
            (Name: nameof(IInterfaceSettings.IMAPSASLPlainEnabled), DispId: 101),
            (Name: nameof(IInterfaceSettings.IMAPSASLInitialResponseEnabled), DispId: 102),
            (Name: nameof(IInterfaceSettings.IPv6PreferredEnabled), DispId: 104)
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
    public void IntegerProperties_PreserveLegacyDispids()
    {
        var expected = new[]
        {
            (Name: nameof(IInterfaceSettings.SMTPRelayerPort), DispId: 37),
            (Name: nameof(IInterfaceSettings.RuleLoopLimit), DispId: 48),
            (Name: nameof(IInterfaceSettings.WorkerThreadPriority), DispId: 57),
            (Name: nameof(IInterfaceSettings.TCPIPThreads), DispId: 60),
            (Name: nameof(IInterfaceSettings.MaxInvalidLogonAttempts), DispId: 83),
            (Name: nameof(IInterfaceSettings.MaxInvalidLogonAttemptsWithin), DispId: 84),
            (Name: nameof(IInterfaceSettings.AutoBanMinutes), DispId: 85),
            (Name: nameof(IInterfaceSettings.MaxNumberOfMXHosts), DispId: 90)
        };

        foreach (var item in expected)
        {
            var property = typeof(IInterfaceSettings).GetProperty(item.Name);

            Assert.IsNotNull(property);
            Assert.AreEqual(item.DispId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
            Assert.AreEqual(typeof(int), property.PropertyType);
        }
    }

    [TestMethod]
    public void StringProperties_PreserveLegacyDispidsAndBstrMarshaling()
    {
        var expected = new[]
        {
            (Name: nameof(IInterfaceSettings.MirrorEMailAddress), DispId: 7),
            (Name: nameof(IInterfaceSettings.SMTPRelayer), DispId: 22),
            (Name: nameof(IInterfaceSettings.SMTPRelayerUsername), DispId: 35),
            (Name: nameof(IInterfaceSettings.DefaultDomain), DispId: 50),
            (Name: nameof(IInterfaceSettings.SMTPDeliveryBindToIP), DispId: 51),
            (Name: nameof(IInterfaceSettings.IMAPPublicFolderName), DispId: 74),
            (Name: nameof(IInterfaceSettings.IMAPHierarchyDelimiter), DispId: 87),
            (Name: nameof(IInterfaceSettings.SslCipherList), DispId: 94)
        };

        foreach (var item in expected)
        {
            var property = typeof(IInterfaceSettings).GetProperty(item.Name);

            Assert.IsNotNull(property);
            Assert.AreEqual(item.DispId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
            Assert.AreEqual(
                UnmanagedType.BStr,
                property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
            Assert.AreEqual(
                UnmanagedType.BStr,
                property.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        }
    }

    [TestMethod]
    public void EnumProperties_PreserveLegacyDispidsAndTypes()
    {
        var expected = new[]
        {
            (Name: nameof(IInterfaceSettings.SMTPRelayerConnectionSecurity), DispId: 91),
            (Name: nameof(IInterfaceSettings.SMTPConnectionSecurity), DispId: 92)
        };

        foreach (var item in expected)
        {
            var property = typeof(IInterfaceSettings).GetProperty(item.Name);

            Assert.IsNotNull(property);
            Assert.AreEqual(item.DispId, property.GetCustomAttribute<DispIdAttribute>()?.Value);
            Assert.AreEqual(typeof(ComConnectionSecurity), property.PropertyType);
        }
    }

    [TestMethod]
    public void DirectActivation_DeniesLegacySettingsAccess()
    {
        var settings = new Settings();

        var indexingError = Assert.ThrowsExactly<COMException>(() => _ = settings.MessageIndexing);
        var scalarError = Assert.ThrowsExactly<COMException>(() => _ = ((IInterfaceSettings)settings).MaxSMTPConnections);
        var hostNameError = Assert.ThrowsExactly<COMException>(() => _ = settings.HostName);
        var imapCapabilityError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPSortEnabled);
        var imapSaslError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPSASLPlainEnabled);
        var imapNamingError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPPublicFolderName);
        var smtpPolicyError = Assert.ThrowsExactly<COMException>(() => _ = settings.AllowSMTPAuthPlain);
        var smtpRoutingError = Assert.ThrowsExactly<COMException>(() => _ = settings.MirrorEMailAddress);
        var numericRuntimeError = Assert.ThrowsExactly<COMException>(() => _ = settings.RuleLoopLimit);
        var sslScalarError = Assert.ThrowsExactly<COMException>(() => _ = settings.VerifyRemoteSslCertificate);
        var networkPreferenceError = Assert.ThrowsExactly<COMException>(() => _ = settings.IPv6PreferredEnabled);
        var autoBanError = Assert.ThrowsExactly<COMException>(() => _ = settings.AutoBanOnLogonFailure);
        var relayerError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayer);
        var smtpConnectionSecurityError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPConnectionSecurity);

        Assert.AreEqual(EAccessDenied, indexingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, hostNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapCapabilityError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapSaslError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapNamingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpPolicyError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpRoutingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, numericRuntimeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, sslScalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, networkPreferenceError.ErrorCode);
        Assert.AreEqual(EAccessDenied, autoBanError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpConnectionSecurityError.ErrorCode);
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
                MaxNumberOfInvalidCommands: 12,
                ImapSortEnabled: true,
                ImapQuotaEnabled: false,
                ImapIdleEnabled: true,
                ImapAclEnabled: false,
                ImapSaslPlainEnabled: true,
                ImapSaslInitialResponseEnabled: false,
                ImapPublicFolderName: "#Shared",
                ImapHierarchyDelimiter: "/",
                AllowSmtpAuthPlain: true,
                AllowMailFromNull: false,
                AllowIncorrectLineEndings: true,
                AddDeliveredToHeader: false,
                MirrorEmailAddress: "archive@example.test",
                DefaultDomain: "example.test",
                SmtpDeliveryBindToIp: "192.0.2.25",
                RuleLoopLimit: 9,
                WorkerThreadPriority: -1,
                TcpIpThreads: 16,
                MaxNumberOfMxHosts: 22,
                VerifyRemoteSslCertificate: true,
                SslCipherList: "TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256",
                Ipv6PreferredEnabled: true,
                AutoBanOnLogonFailure: true,
                MaxInvalidLogonAttempts: 3,
                MaxInvalidLogonAttemptsWithin: 30,
                AutoBanMinutes: 60,
                SmtpRelayer: "relay.example.test",
                SmtpRelayerRequiresAuthentication: true,
                SmtpRelayerUsername: "relay-user",
                SmtpRelayerPort: 587,
                SmtpRelayerConnectionSecurity: (int)ComConnectionSecurity.StartTlsRequired,
                SmtpConnectionSecurity: (int)ComConnectionSecurity.StartTlsOptional));

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
        Assert.AreEqual("relay.example.test", settings.SMTPRelayer);
        Assert.IsTrue(settings.SMTPRelayerRequiresAuthentication);
        Assert.AreEqual("relay-user", settings.SMTPRelayerUsername);
        Assert.AreEqual(587, settings.SMTPRelayerPort);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, settings.SMTPRelayerConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.StartTlsOptional, settings.SMTPConnectionSecurity);
        Assert.AreEqual(20480, settings.MaxMessageSize);
        Assert.AreEqual(100, settings.MaxSMTPRecipientsInBatch);
        Assert.IsTrue(settings.DisconnectInvalidClients);
        Assert.AreEqual(12, settings.MaxNumberOfInvalidCommands);
        Assert.IsTrue(settings.IMAPSortEnabled);
        Assert.IsFalse(settings.IMAPQuotaEnabled);
        Assert.IsTrue(settings.IMAPIdleEnabled);
        Assert.IsFalse(settings.IMAPACLEnabled);
        Assert.IsTrue(settings.IMAPSASLPlainEnabled);
        Assert.IsFalse(settings.IMAPSASLInitialResponseEnabled);
        Assert.AreEqual("#Shared", settings.IMAPPublicFolderName);
        Assert.AreEqual("/", settings.IMAPHierarchyDelimiter);
        Assert.IsTrue(settings.AllowSMTPAuthPlain);
        Assert.IsTrue(settings.DenyMailFromNull);
        Assert.IsTrue(settings.AllowIncorrectLineEndings);
        Assert.IsFalse(settings.AddDeliveredToHeader);
        Assert.AreEqual("archive@example.test", settings.MirrorEMailAddress);
        Assert.AreEqual("example.test", settings.DefaultDomain);
        Assert.AreEqual("192.0.2.25", settings.SMTPDeliveryBindToIP);
        Assert.AreEqual(9, settings.RuleLoopLimit);
        Assert.AreEqual(-1, settings.WorkerThreadPriority);
        Assert.AreEqual(16, settings.TCPIPThreads);
        Assert.AreEqual(22, settings.MaxNumberOfMXHosts);
        Assert.IsTrue(settings.VerifyRemoteSslCertificate);
        Assert.AreEqual("TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256", settings.SslCipherList);
        Assert.IsTrue(settings.IPv6PreferredEnabled);
        Assert.IsTrue(settings.AutoBanOnLogonFailure);
        Assert.AreEqual(3, settings.MaxInvalidLogonAttempts);
        Assert.AreEqual(30, settings.MaxInvalidLogonAttemptsWithin);
        Assert.AreEqual(60, settings.AutoBanMinutes);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayerUseSSL).ErrorCode);

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
            Assert.ThrowsExactly<COMException>(() => settings.SMTPRelayer = "other-relay.example.test").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPRelayerRequiresAuthentication = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPRelayerUsername = "other-user").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SetSMTPRelayerPassword("secret")).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPRelayerPort = 25).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPRelayerConnectionSecurity = ComConnectionSecurity.None).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPConnectionSecurity = ComConnectionSecurity.None).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPRelayerUseSSL = true).ErrorCode);
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
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPSortEnabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPQuotaEnabled = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPIdleEnabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPACLEnabled = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPSASLPlainEnabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPSASLInitialResponseEnabled = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPPublicFolderName = "#Changed").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPHierarchyDelimiter = ".").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.AllowSMTPAuthPlain = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.DenyMailFromNull = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.AllowIncorrectLineEndings = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.AddDeliveredToHeader = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MirrorEMailAddress = "other@example.test").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.DefaultDomain = "other.test").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SMTPDeliveryBindToIP = "192.0.2.26").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.RuleLoopLimit = 10).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.WorkerThreadPriority = 1).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.TCPIPThreads = 20).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxNumberOfMXHosts = 30).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.VerifyRemoteSslCertificate = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.SslCipherList = "DEFAULT").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IPv6PreferredEnabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.AutoBanOnLogonFailure = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxInvalidLogonAttempts = 4).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxInvalidLogonAttemptsWithin = 45).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.AutoBanMinutes = 120).ErrorCode);
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
