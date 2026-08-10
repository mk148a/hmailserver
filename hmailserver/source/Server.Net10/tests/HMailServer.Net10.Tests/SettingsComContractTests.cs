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
            (Name: nameof(IInterfaceSettings.TlsVersion10Enabled), DispId: 96),
            (Name: nameof(IInterfaceSettings.TlsVersion11Enabled), DispId: 97),
            (Name: nameof(IInterfaceSettings.TlsVersion12Enabled), DispId: 98),
            (Name: nameof(IInterfaceSettings.IMAPSASLPlainEnabled), DispId: 101),
            (Name: nameof(IInterfaceSettings.IMAPSASLInitialResponseEnabled), DispId: 102),
            (Name: nameof(IInterfaceSettings.TlsVersion13Enabled), DispId: 103),
            (Name: nameof(IInterfaceSettings.IPv6PreferredEnabled), DispId: 104),
            (Name: nameof(IInterfaceSettings.TlsOptionPreferServerCiphersEnabled), DispId: 105),
            (Name: nameof(IInterfaceSettings.TlsOptionPrioritizeChaChaEnabled), DispId: 106),
            (Name: nameof(IInterfaceSettings.RewriteEnvelopeFromWhenForwarding), DispId: 107)
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
            (Name: nameof(IInterfaceSettings.MaxAsynchronousThreads), DispId: 88),
            (Name: nameof(IInterfaceSettings.MaxNumberOfMXHosts), DispId: 90),
            (Name: nameof(IInterfaceSettings.CrashSimulationMode), DispId: 99)
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
            (Name: nameof(IInterfaceSettings.UserInterfaceLanguage), DispId: 42),
            (Name: nameof(IInterfaceSettings.DefaultDomain), DispId: 50),
            (Name: nameof(IInterfaceSettings.SMTPDeliveryBindToIP), DispId: 51),
            (Name: nameof(IInterfaceSettings.IMAPPublicFolderName), DispId: 74),
            (Name: nameof(IInterfaceSettings.IMAPHierarchyDelimiter), DispId: 87),
            (Name: nameof(IInterfaceSettings.SslCipherList), DispId: 94),
            (Name: nameof(IInterfaceSettings.IMAPMasterUser), DispId: 100)
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
    public void PublicFolderDiskName_PreservesGetterOnlyDispidAndBstrMarshaling()
    {
        var property = typeof(IInterfaceSettings).GetProperty(nameof(IInterfaceSettings.PublicFolderDiskName));

        Assert.IsNotNull(property);
        Assert.AreEqual(79, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            property.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.IsNull(property.SetMethod);
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
        var scalarSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxSMTPConnections = 1);
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
        var clearLogonFailuresError = Assert.ThrowsExactly<COMException>(settings.ClearLogonFailureList);
        var relayerError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayer);
        var relayerUseSslError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayerUseSSL);
        var smtpConnectionSecurityError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPConnectionSecurity);
        var tlsVersionError = Assert.ThrowsExactly<COMException>(() => _ = settings.TlsVersion10Enabled);
        var imapMasterUserError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPMasterUser);
        var asynchronousThreadsError = Assert.ThrowsExactly<COMException>(() => _ = settings.MaxAsynchronousThreads);
        var publicFolderDiskNameError = Assert.ThrowsExactly<COMException>(() => _ = settings.PublicFolderDiskName);
        var userInterfaceLanguageError = Assert.ThrowsExactly<COMException>(() => _ = settings.UserInterfaceLanguage);
        var rewriteEnvelopeError = Assert.ThrowsExactly<COMException>(() => _ = settings.RewriteEnvelopeFromWhenForwarding);
        var crashSimulationError = Assert.ThrowsExactly<COMException>(() => _ = settings.CrashSimulationMode);
        var defaultDomainSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.DefaultDomain = "direct-activation.example.test");
        var workerThreadPrioritySetterError = Assert.ThrowsExactly<COMException>(
            () => settings.WorkerThreadPriority = 1);

        Assert.AreEqual(EAccessDenied, indexingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarSetterError.ErrorCode);
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
        Assert.AreEqual(EAccessDenied, clearLogonFailuresError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerUseSslError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpConnectionSecurityError.ErrorCode);
        Assert.AreEqual(EAccessDenied, tlsVersionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapMasterUserError.ErrorCode);
        Assert.AreEqual(EAccessDenied, asynchronousThreadsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, publicFolderDiskNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, userInterfaceLanguageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rewriteEnvelopeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, crashSimulationError.ErrorCode);
        Assert.AreEqual(EAccessDenied, defaultDomainSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, workerThreadPrioritySetterError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ReturnsRuntimeBoundMessageIndexingAndKeepsOtherMembersExplicit()
    {
        MessageIndexingRuntimeHost.Configure(new FixedMessageIndexingRuntime(42));
        IInterfaceSettings settings = Settings.CreateAuthorized();

        Assert.AreEqual(42, settings.MessageIndexing.TotalMessageCount);
        Assert.AreEqual(0, settings.CrashSimulationMode);
        var unimplemented = Assert.ThrowsExactly<COMException>(() => _ = settings.MaxSMTPConnections);
        var unimplementedClear = Assert.ThrowsExactly<COMException>(settings.ClearLogonFailureList);
        Assert.AreEqual(ENotImplemented, unimplemented.ErrorCode);
        Assert.AreEqual(ENotImplemented, unimplementedClear.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_ExposesReadOnlyBoundedAdministrationScalars()
    {
        var logonFailureStore = new FakeLogonFailureAdministrationStore();
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
                SmtpConnectionSecurity: (int)ComConnectionSecurity.StartTlsOptional,
                SslVersions: 26,
                TlsOptions: 2,
                ImapMasterUser: "master-user",
                MaxAsynchronousThreads: 15),
            new SettingsRuntimeConfiguration(
                UserInterfaceLanguage: "Swedish",
                RewriteEnvelopeFromWhenForwarding: true,
                CrashSimulationMode: 3,
                LogonFailureAdministrationStore: logonFailureStore));

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
        Assert.IsTrue(settings.TlsVersion10Enabled);
        Assert.IsFalse(settings.TlsVersion11Enabled);
        Assert.IsTrue(settings.TlsVersion12Enabled);
        Assert.IsTrue(settings.TlsVersion13Enabled);
        Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);
        Assert.IsFalse(settings.TlsOptionPrioritizeChaChaEnabled);
        Assert.AreEqual("master-user", settings.IMAPMasterUser);
        Assert.AreEqual(15, settings.MaxAsynchronousThreads);
        Assert.AreEqual("#Public", settings.PublicFolderDiskName);
        Assert.AreEqual("Swedish", settings.UserInterfaceLanguage);
        Assert.IsTrue(settings.RewriteEnvelopeFromWhenForwarding);
        Assert.AreEqual(3, settings.CrashSimulationMode);
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
        settings.ClearLogonFailureList();
        Assert.AreEqual(1, logonFailureStore.CallCount);
        Assert.IsFalse(logonFailureStore.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.SMTPRelayerUseSSL);

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
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion10Enabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion11Enabled = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion12Enabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion13Enabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.TlsOptionPreferServerCiphersEnabled = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.TlsOptionPrioritizeChaChaEnabled = true).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.IMAPMasterUser = "changed-master").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.MaxAsynchronousThreads = 20).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.UserInterfaceLanguage = "English").ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.RewriteEnvelopeFromWhenForwarding = false).ErrorCode);
        Assert.AreEqual(
            ENotImplemented,
            Assert.ThrowsExactly<COMException>(() => settings.CrashSimulationMode = 1).ErrorCode);
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
    public void AuthorizedSettings_DefaultDomainSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            UpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                DefaultDomain: "old.example.test"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.DefaultDomain = "new.example.test";

        Assert.AreEqual(1, store.UpdateCount);
        Assert.AreEqual("new.example.test", store.UpdatedDefaultDomain);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("new.example.test", settings.DefaultDomain);

        store.UpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.DefaultDomain = "failed.example.test");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.UpdateCount);
        Assert.AreEqual("new.example.test", settings.DefaultDomain);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.DefaultDomain = "denied.example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.UpdateCount);
        Assert.AreEqual("new.example.test", settings.DefaultDomain);
    }

    [TestMethod]
    public void AuthorizedSettings_MirrorEmailSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MirrorUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MirrorEmailAddress: "old@example.test"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MirrorEMailAddress = "new@example.test";

        Assert.AreEqual(1, store.MirrorUpdateCount);
        Assert.AreEqual("new@example.test", store.UpdatedMirrorEmailAddress);
        Assert.AreEqual("new@example.test", settings.MirrorEMailAddress);

        store.MirrorUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MirrorEMailAddress = "failed@example.test");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MirrorUpdateCount);
        Assert.AreEqual("new@example.test", settings.MirrorEMailAddress);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MirrorEMailAddress = "denied@example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MirrorUpdateCount);
        Assert.AreEqual("new@example.test", settings.MirrorEMailAddress);
    }

    [TestMethod]
    public void AuthorizedSettings_WorkerThreadPrioritySetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            WorkerThreadPriorityUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                WorkerThreadPriority: 1),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.WorkerThreadPriority = 4;

        Assert.AreEqual(1, store.WorkerThreadPriorityUpdateCount);
        Assert.AreEqual(4, store.UpdatedWorkerThreadPriority);
        Assert.AreEqual(4, settings.WorkerThreadPriority);

        store.WorkerThreadPriorityUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.WorkerThreadPriority = 7);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.WorkerThreadPriorityUpdateCount);
        Assert.AreEqual(4, settings.WorkerThreadPriority);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WorkerThreadPriority = 9);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.WorkerThreadPriorityUpdateCount);
        Assert.AreEqual(4, settings.WorkerThreadPriority);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxSmtpConnectionsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxSmtpConnectionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxSmtpConnections: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxSMTPConnections = 25;

        Assert.AreEqual(1, store.MaxSmtpConnectionsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxSmtpConnections);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxSMTPConnections);

        store.MaxSmtpConnectionsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxSMTPConnections = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxSmtpConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxSMTPConnections);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxSMTPConnections = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxSmtpConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxSMTPConnections);
    }

    [TestMethod]
    public void AuthorizedSettings_SMTPRelayerUseSSLOnlyMapsLegacyTlsMode()
    {
        var cases = new[]
        {
            (ComConnectionSecurity.None, false),
            (ComConnectionSecurity.Tls, true),
            (ComConnectionSecurity.StartTlsOptional, false),
            (ComConnectionSecurity.StartTlsRequired, false)
        };

        foreach (var (connectionSecurity, expected) in cases)
        {
            IInterfaceSettings settings = Settings.CreateAuthorized(
                new SettingsAdministrationSnapshot(
                    HostName: string.Empty,
                    WelcomeSmtp: string.Empty,
                    WelcomePop3: string.Empty,
                    WelcomeImap: string.Empty,
                    SmtpRelayerConnectionSecurity: (int)connectionSecurity));

            Assert.AreEqual(expected, settings.SMTPRelayerUseSSL, connectionSecurity.ToString());
        }
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

    private sealed class FakeLogonFailureAdministrationStore : ILogonFailureAdministrationStore
    {
        public int CallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask ClearLegacyListAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            CancellationToken = cancellationToken;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSettingsAdministrationMutationStore : ISettingsAdministrationMutationStore
    {
        public bool UpdateResult { get; set; }

        public bool MirrorUpdateResult { get; set; }

        public int UpdateCount { get; private set; }

        public string? UpdatedDefaultDomain { get; private set; }

        public int MirrorUpdateCount { get; private set; }

        public string? UpdatedMirrorEmailAddress { get; private set; }

        public bool WorkerThreadPriorityUpdateResult { get; set; }

        public int WorkerThreadPriorityUpdateCount { get; private set; }

        public int UpdatedWorkerThreadPriority { get; private set; }

        public bool MaxSmtpConnectionsUpdateResult { get; set; }

        public int MaxSmtpConnectionsUpdateCount { get; private set; }

        public int UpdatedMaxSmtpConnections { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<bool> UpdateDefaultDomainAsync(
            string defaultDomain,
            CancellationToken cancellationToken)
        {
            UpdateCount++;
            UpdatedDefaultDomain = defaultDomain;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateMirrorEmailAddressAsync(
            string mirrorEmailAddress,
            CancellationToken cancellationToken)
        {
            MirrorUpdateCount++;
            UpdatedMirrorEmailAddress = mirrorEmailAddress;
            return ValueTask.FromResult(MirrorUpdateResult);
        }

        public ValueTask<bool> UpdateWorkerThreadPriorityAsync(
            int workerThreadPriority,
            CancellationToken cancellationToken)
        {
            WorkerThreadPriorityUpdateCount++;
            UpdatedWorkerThreadPriority = workerThreadPriority;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(WorkerThreadPriorityUpdateResult);
        }

        public ValueTask<bool> UpdateMaxSmtpConnectionsAsync(
            int maxSmtpConnections,
            CancellationToken cancellationToken)
        {
            MaxSmtpConnectionsUpdateCount++;
            UpdatedMaxSmtpConnections = maxSmtpConnections;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxSmtpConnectionsUpdateResult);
        }

    }

    private sealed class FixedImapFolderAdministrationStore(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders) : IImapFolderAdministrationStore
    {
        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                folders.Where(folder => folder.AccountId == accountId)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                folders.Where(folder => folder.AccountId == accountId && folder.ParentId == -1)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
            int parentFolderId,
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                folders.Where(folder => folder.AccountId == accountId && folder.ParentId == parentFolderId)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>(
                Array.Empty<ImapFolderPermissionAdministrationSnapshot>());
    }
}
