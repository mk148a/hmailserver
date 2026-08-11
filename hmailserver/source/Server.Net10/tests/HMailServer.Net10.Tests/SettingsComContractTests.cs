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
            (Name: nameof(IInterfaceSettings.SMTPRelayerUseSSL), DispId: 71),
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
            (Name: nameof(IInterfaceSettings.SMTPNoOfTries), DispId: 19),
            (Name: nameof(IInterfaceSettings.SMTPMinutesBetweenTry), DispId: 20),
            (Name: nameof(IInterfaceSettings.MaxDeliveryThreads), DispId: 29),
            (Name: nameof(IInterfaceSettings.SMTPRelayerPort), DispId: 37),
            (Name: nameof(IInterfaceSettings.MaxMessageSize), DispId: 44),
            (Name: nameof(IInterfaceSettings.RuleLoopLimit), DispId: 48),
            (Name: nameof(IInterfaceSettings.MaxIMAPConnections), DispId: 53),
            (Name: nameof(IInterfaceSettings.WorkerThreadPriority), DispId: 57),
            (Name: nameof(IInterfaceSettings.TCPIPThreads), DispId: 60),
            (Name: nameof(IInterfaceSettings.MaxSMTPRecipientsInBatch), DispId: 62),
            (Name: nameof(IInterfaceSettings.MaxNumberOfInvalidCommands), DispId: 65),
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
    public void MaxNumberOfMXHosts_PreservesDispId90IntegerContract()
    {
        var property = typeof(IInterfaceSettings).GetProperty(nameof(IInterfaceSettings.MaxNumberOfMXHosts));

        Assert.IsNotNull(property);
        Assert.AreEqual(90, property.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(typeof(int), property.PropertyType);
        Assert.IsTrue(property.CanRead);
        Assert.IsTrue(property.CanWrite);
    }

    [TestMethod]
    public void StringProperties_PreserveLegacyDispidsAndBstrMarshaling()
    {
        var expected = new[]
        {
            (Name: nameof(IInterfaceSettings.MirrorEMailAddress), DispId: 7),
            (Name: nameof(IInterfaceSettings.SMTPRelayer), DispId: 22),
            (Name: nameof(IInterfaceSettings.WelcomeSMTP), DispId: 23),
            (Name: nameof(IInterfaceSettings.WelcomePOP3), DispId: 24),
            (Name: nameof(IInterfaceSettings.WelcomeIMAP), DispId: 25),
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

        var values = Enum.GetNames<ComConnectionSecurity>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComConnectionSecurity>(name)));

        Assert.AreEqual(0, values[nameof(ComConnectionSecurity.None)]);
        Assert.AreEqual(1, values[nameof(ComConnectionSecurity.Tls)]);
        Assert.AreEqual(2, values[nameof(ComConnectionSecurity.StartTlsOptional)]);
        Assert.AreEqual(3, values[nameof(ComConnectionSecurity.StartTlsRequired)]);
    }

    [TestMethod]
    public void DirectActivation_DeniesLegacySettingsAccess()
    {
        var settings = new Settings();

        var indexingError = Assert.ThrowsExactly<COMException>(() => _ = settings.MessageIndexing);
        var scalarError = Assert.ThrowsExactly<COMException>(() => _ = ((IInterfaceSettings)settings).MaxSMTPConnections);
        var scalarSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxSMTPConnections = 1);
        var smtpNoOfTriesError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).SMTPNoOfTries);
        var smtpNoOfTriesSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).SMTPNoOfTries = 1);
        var smtpMinutesBetweenTryError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).SMTPMinutesBetweenTry);
        var smtpMinutesBetweenTrySetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).SMTPMinutesBetweenTry = 1);
        var maxMessageSizeError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).MaxMessageSize);
        var maxMessageSizeSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxMessageSize = 1);
        var maxDeliveryThreadsError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).MaxDeliveryThreads);
        var maxDeliveryThreadsSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxDeliveryThreads = 1);
        var maxImapConnectionsError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).MaxIMAPConnections);
        var maxImapConnectionsSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxIMAPConnections = 1);
        var maxNumberOfMxHostsError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).MaxNumberOfMXHosts);
        var maxNumberOfMxHostsSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxNumberOfMXHosts = 1);
        var pop3ScalarError = Assert.ThrowsExactly<COMException>(() => _ = ((IInterfaceSettings)settings).MaxPOP3Connections);
        var pop3ScalarSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxPOP3Connections = 1);
        var recipientsScalarError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).MaxSMTPRecipientsInBatch);
        var recipientsScalarSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxSMTPRecipientsInBatch = 1);
        var invalidCommandsError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).MaxNumberOfInvalidCommands);
        var invalidCommandsSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).MaxNumberOfInvalidCommands = 1);
        var disconnectInvalidClientsError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).DisconnectInvalidClients);
        var disconnectInvalidClientsSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).DisconnectInvalidClients = true);
        var allowIncorrectLineEndingsError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).AllowIncorrectLineEndings);
        var allowIncorrectLineEndingsSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).AllowIncorrectLineEndings = true);
        var addDeliveredToHeaderError = Assert.ThrowsExactly<COMException>(
            () => _ = ((IInterfaceSettings)settings).AddDeliveredToHeader);
        var addDeliveredToHeaderSetterError = Assert.ThrowsExactly<COMException>(
            () => ((IInterfaceSettings)settings).AddDeliveredToHeader = true);
        var hostNameError = Assert.ThrowsExactly<COMException>(() => _ = settings.HostName);
        var imapCapabilityError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPSortEnabled);
        var imapSaslError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPSASLPlainEnabled);
        var imapNamingError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPPublicFolderName);
        var smtpPolicyError = Assert.ThrowsExactly<COMException>(() => _ = settings.AllowSMTPAuthPlain);
        var smtpPolicySetterError = Assert.ThrowsExactly<COMException>(
            () => settings.AllowSMTPAuthPlain = true);
        var denyMailFromNullError = Assert.ThrowsExactly<COMException>(() => _ = settings.DenyMailFromNull);
        var denyMailFromNullSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.DenyMailFromNull = true);
        var smtpRoutingError = Assert.ThrowsExactly<COMException>(() => _ = settings.MirrorEMailAddress);
        var welcomeSmtpError = Assert.ThrowsExactly<COMException>(() => _ = settings.WelcomeSMTP);
        var welcomeSmtpSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeSMTP = "direct-activation SMTP");
        var welcomePop3Error = Assert.ThrowsExactly<COMException>(() => _ = settings.WelcomePOP3);
        var welcomePop3SetterError = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomePOP3 = "direct-activation POP3");
        var welcomeImapError = Assert.ThrowsExactly<COMException>(() => _ = settings.WelcomeIMAP);
        var welcomeImapSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeIMAP = "direct-activation IMAP");
        var numericRuntimeError = Assert.ThrowsExactly<COMException>(() => _ = settings.RuleLoopLimit);
        var numericRuntimeSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.RuleLoopLimit = 1);
        var sslScalarError = Assert.ThrowsExactly<COMException>(() => _ = settings.VerifyRemoteSslCertificate);
        var sslScalarSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.VerifyRemoteSslCertificate = true);
        var networkPreferenceError = Assert.ThrowsExactly<COMException>(() => _ = settings.IPv6PreferredEnabled);
        var autoBanError = Assert.ThrowsExactly<COMException>(() => _ = settings.AutoBanOnLogonFailure);
        var clearLogonFailuresError = Assert.ThrowsExactly<COMException>(settings.ClearLogonFailureList);
        var relayerError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayer);
        var relayerSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayer = "direct-activation relay");
        var relayerUsernameError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayerUsername);
        var relayerUsernameSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUsername = "direct-activation user");
        var relayerPortError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayerPort);
        var relayerPortSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerPort = 25);
        var relayerConnectionSecurityError = Assert.ThrowsExactly<COMException>(
            () => _ = settings.SMTPRelayerConnectionSecurity);
        var relayerConnectionSecuritySetterError = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerConnectionSecurity = ComConnectionSecurity.None);
        var relayerUseSslError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPRelayerUseSSL);
        var relayerUseSslSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUseSSL = true);
        var smtpConnectionSecurityError = Assert.ThrowsExactly<COMException>(() => _ = settings.SMTPConnectionSecurity);
        var tlsVersionError = Assert.ThrowsExactly<COMException>(() => _ = settings.TlsVersion10Enabled);
        var imapMasterUserError = Assert.ThrowsExactly<COMException>(() => _ = settings.IMAPMasterUser);
        var asynchronousThreadsError = Assert.ThrowsExactly<COMException>(() => _ = settings.MaxAsynchronousThreads);
        var publicFolderDiskNameError = Assert.ThrowsExactly<COMException>(() => _ = settings.PublicFolderDiskName);
        var userInterfaceLanguageError = Assert.ThrowsExactly<COMException>(() => _ = settings.UserInterfaceLanguage);
        var rewriteEnvelopeError = Assert.ThrowsExactly<COMException>(() => _ = settings.RewriteEnvelopeFromWhenForwarding);
        var crashSimulationError = Assert.ThrowsExactly<COMException>(() => _ = settings.CrashSimulationMode);
        var relayerAuthenticationError = Assert.ThrowsExactly<COMException>(
            () => _ = settings.SMTPRelayerRequiresAuthentication);
        var relayerAuthenticationSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerRequiresAuthentication = true);
        var defaultDomainSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.DefaultDomain = "direct-activation.example.test");
        var workerThreadPrioritySetterError = Assert.ThrowsExactly<COMException>(
            () => settings.WorkerThreadPriority = 1);
        var tcpIpThreadsError = Assert.ThrowsExactly<COMException>(() => _ = settings.TCPIPThreads);
        var tcpIpThreadsSetterError = Assert.ThrowsExactly<COMException>(
            () => settings.TCPIPThreads = 1);

        Assert.AreEqual(EAccessDenied, indexingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, scalarSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpNoOfTriesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpNoOfTriesSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpMinutesBetweenTryError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpMinutesBetweenTrySetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxMessageSizeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxMessageSizeSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxDeliveryThreadsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxDeliveryThreadsSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxImapConnectionsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxImapConnectionsSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxNumberOfMxHostsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, maxNumberOfMxHostsSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, pop3ScalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, pop3ScalarSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, recipientsScalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, recipientsScalarSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, invalidCommandsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, invalidCommandsSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, disconnectInvalidClientsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, disconnectInvalidClientsSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, allowIncorrectLineEndingsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, allowIncorrectLineEndingsSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, addDeliveredToHeaderError.ErrorCode);
        Assert.AreEqual(EAccessDenied, addDeliveredToHeaderSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, hostNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapCapabilityError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapSaslError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapNamingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpPolicyError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpPolicySetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, denyMailFromNullError.ErrorCode);
        Assert.AreEqual(EAccessDenied, denyMailFromNullSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpRoutingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, welcomeSmtpError.ErrorCode);
        Assert.AreEqual(EAccessDenied, welcomeSmtpSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, welcomePop3Error.ErrorCode);
        Assert.AreEqual(EAccessDenied, welcomePop3SetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, welcomeImapError.ErrorCode);
        Assert.AreEqual(EAccessDenied, welcomeImapSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, numericRuntimeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, numericRuntimeSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, sslScalarError.ErrorCode);
        Assert.AreEqual(EAccessDenied, sslScalarSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, networkPreferenceError.ErrorCode);
        Assert.AreEqual(EAccessDenied, autoBanError.ErrorCode);
        Assert.AreEqual(EAccessDenied, clearLogonFailuresError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerUsernameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerUsernameSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerPortError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerPortSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerConnectionSecurityError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerConnectionSecuritySetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerUseSslError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerUseSslSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, smtpConnectionSecurityError.ErrorCode);
        Assert.AreEqual(EAccessDenied, tlsVersionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapMasterUserError.ErrorCode);
        Assert.AreEqual(EAccessDenied, asynchronousThreadsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, publicFolderDiskNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, userInterfaceLanguageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rewriteEnvelopeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, crashSimulationError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerAuthenticationError.ErrorCode);
        Assert.AreEqual(EAccessDenied, relayerAuthenticationSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, defaultDomainSetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, workerThreadPrioritySetterError.ErrorCode);
        Assert.AreEqual(EAccessDenied, tcpIpThreadsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, tcpIpThreadsSetterError.ErrorCode);
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
    public void AuthorizedSettings_SmtpRelayerSetterPersistsBstrBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayer: "old-relay.example.test"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        const string newRelayer = "relay.example.test:587/unchanged";
        settings.SMTPRelayer = newRelayer;

        Assert.AreEqual(1, store.SmtpRelayerUpdateCount);
        Assert.AreEqual(newRelayer, store.UpdatedSmtpRelayer);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(newRelayer, settings.SMTPRelayer);

        store.SmtpRelayerUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayer = "failed-relay.example.test");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerUpdateCount);
        Assert.AreEqual(newRelayer, settings.SMTPRelayer);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayer = "denied-relay.example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerUpdateCount);
        Assert.AreEqual(newRelayer, settings.SMTPRelayer);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerRequiresAuthenticationSetterPersistsDirectBooleanBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerRequiresAuthenticationUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerRequiresAuthentication: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPRelayerRequiresAuthentication = true;

        Assert.AreEqual(1, store.SmtpRelayerRequiresAuthenticationUpdateCount);
        Assert.IsTrue(store.UpdatedSmtpRelayerRequiresAuthentication);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.SMTPRelayerRequiresAuthentication);

        settings.SMTPRelayerRequiresAuthentication = false;

        Assert.AreEqual(2, store.SmtpRelayerRequiresAuthenticationUpdateCount);
        Assert.IsFalse(store.UpdatedSmtpRelayerRequiresAuthentication);
        Assert.IsFalse(settings.SMTPRelayerRequiresAuthentication);

        store.SmtpRelayerRequiresAuthenticationUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerRequiresAuthentication = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.SmtpRelayerRequiresAuthenticationUpdateCount);
        Assert.IsFalse(settings.SMTPRelayerRequiresAuthentication);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerRequiresAuthentication = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.SmtpRelayerRequiresAuthenticationUpdateCount);
        Assert.IsFalse(settings.SMTPRelayerRequiresAuthentication);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerUsernameSetterPersistsBstrBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerUsernameUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerUsername: "old-user"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPRelayerUsername = "new-user";

        Assert.AreEqual(1, store.SmtpRelayerUsernameUpdateCount);
        Assert.AreEqual("new-user", store.UpdatedSmtpRelayerUsername);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("new-user", settings.SMTPRelayerUsername);

        store.SmtpRelayerUsernameUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUsername = "failed-user");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerUsernameUpdateCount);
        Assert.AreEqual("new-user", settings.SMTPRelayerUsername);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUsername = "denied-user");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerUsernameUpdateCount);
        Assert.AreEqual("new-user", settings.SMTPRelayerUsername);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerPortSetterPersistsIntBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerPortUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerPort: 25),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPRelayerPort = 587;

        Assert.AreEqual(1, store.SmtpRelayerPortUpdateCount);
        Assert.AreEqual(587, store.UpdatedSmtpRelayerPort);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(587, settings.SMTPRelayerPort);

        store.SmtpRelayerPortUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerPort = 2525);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerPortUpdateCount);
        Assert.AreEqual(587, settings.SMTPRelayerPort);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerPort = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerPortUpdateCount);
        Assert.AreEqual(587, settings.SMTPRelayerPort);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerConnectionSecuritySetterPersistsIntBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerConnectionSecurityUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerConnectionSecurity: (int)ComConnectionSecurity.None),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPRelayerConnectionSecurity = ComConnectionSecurity.StartTlsRequired;

        Assert.AreEqual(1, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual((int)ComConnectionSecurity.StartTlsRequired, store.UpdatedSmtpRelayerConnectionSecurity);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, settings.SMTPRelayerConnectionSecurity);
        Assert.IsFalse(settings.SMTPRelayerUseSSL);

        store.SmtpRelayerConnectionSecurityUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerConnectionSecurity = ComConnectionSecurity.Tls);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, settings.SMTPRelayerConnectionSecurity);
        Assert.IsFalse(settings.SMTPRelayerUseSSL);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerConnectionSecurity = ComConnectionSecurity.None);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, settings.SMTPRelayerConnectionSecurity);
    }

    [TestMethod]
    public void AuthorizedSettings_SMTPRelayerUseSSLSetterPersistsLegacyTlsModeBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerConnectionSecurityUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerConnectionSecurity: (int)ComConnectionSecurity.None),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPRelayerUseSSL = true;

        Assert.AreEqual(1, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual((int)ComConnectionSecurity.Tls, store.UpdatedSmtpRelayerConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.Tls, settings.SMTPRelayerConnectionSecurity);
        Assert.IsTrue(settings.SMTPRelayerUseSSL);

        settings.SMTPRelayerUseSSL = false;

        Assert.AreEqual(2, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual((int)ComConnectionSecurity.None, store.UpdatedSmtpRelayerConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.None, settings.SMTPRelayerConnectionSecurity);
        Assert.IsFalse(settings.SMTPRelayerUseSSL);

        store.SmtpRelayerConnectionSecurityUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUseSSL = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual(ComConnectionSecurity.None, settings.SMTPRelayerConnectionSecurity);
        Assert.IsFalse(settings.SMTPRelayerUseSSL);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUseSSL = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.SmtpRelayerConnectionSecurityUpdateCount);
        Assert.AreEqual(ComConnectionSecurity.None, settings.SMTPRelayerConnectionSecurity);
        Assert.IsFalse(settings.SMTPRelayerUseSSL);
    }

    [TestMethod]
    public async Task ApplicationSettings_SMTPRelayerMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerConnectionSecurity: (int)ComConnectionSecurity.None),
            SmtpRelayerConnectionSecurityUpdateResult = true,
            GateSmtpRelayerConnectionSecurityMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.SMTPRelayerUseSSL = true);
        await store.SmtpRelayerConnectionSecurityMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.SmtpRelayerConnectionSecurityMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual((int)ComConnectionSecurity.Tls, store.UpdatedSmtpRelayerConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.Tls, settings.SMTPRelayerConnectionSecurity);
        Assert.IsTrue(settings.SMTPRelayerUseSSL);
        var retainedMutationDenied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUseSSL = false);
        Assert.AreEqual(EAccessDenied, retainedMutationDenied.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedSettings_UnavailableAuthorizationLeaseReturnsAccessDeniedBeforeMutation()
    {
        var authority = new ApplicationAuthorizationAuthority();
        var attempt = authority.BeginAuthentication();
        Assert.IsTrue(authority.CompleteAuthentication(attempt, isServerAdministrator: true));
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerConnectionSecurityUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerConnectionSecurity: (int)ComConnectionSecurity.None),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: cancellationToken =>
                authority.AcquireLeaseAsync(attempt.Generation, cancellationToken));

        authority.BeginAuthentication();
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerConnectionSecurity = ComConnectionSecurity.Tls);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpRelayerConnectionSecurityUpdateCount);
    }

    [TestMethod]
    public void ApplicationSettings_SMTPRelayerUseSSLRetainsLegacyTlsMapping()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerConnectionSecurity: (int)ComConnectionSecurity.None),
            SmtpRelayerConnectionSecurityUpdateResult = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        settings.SMTPRelayerUseSSL = true;

        Assert.AreEqual((int)ComConnectionSecurity.Tls, store.UpdatedSmtpRelayerConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.Tls, settings.SMTPRelayerConnectionSecurity);
        Assert.IsTrue(settings.SMTPRelayerUseSSL);
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
    public void AuthorizedSettings_WelcomePop3SetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            WelcomePop3UpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: "old POP3 greeting",
                WelcomeImap: string.Empty),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.WelcomePOP3 = "new POP3 greeting";

        Assert.AreEqual(1, store.WelcomePop3UpdateCount);
        Assert.AreEqual("new POP3 greeting", store.UpdatedWelcomePop3);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("new POP3 greeting", settings.WelcomePOP3);

        store.WelcomePop3UpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomePOP3 = "failed POP3 greeting");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.WelcomePop3UpdateCount);
        Assert.AreEqual("new POP3 greeting", settings.WelcomePOP3);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomePOP3 = "denied POP3 greeting");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.WelcomePop3UpdateCount);
        Assert.AreEqual("new POP3 greeting", settings.WelcomePOP3);
    }

    [TestMethod]
    public void AuthorizedSettings_WelcomeSmtpSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            WelcomeSmtpUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: "old SMTP greeting",
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.WelcomeSMTP = "new SMTP greeting";

        Assert.AreEqual(1, store.WelcomeSmtpUpdateCount);
        Assert.AreEqual("new SMTP greeting", store.UpdatedWelcomeSmtp);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("new SMTP greeting", settings.WelcomeSMTP);

        store.WelcomeSmtpUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeSMTP = "failed SMTP greeting");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.WelcomeSmtpUpdateCount);
        Assert.AreEqual("new SMTP greeting", settings.WelcomeSMTP);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeSMTP = "denied SMTP greeting");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.WelcomeSmtpUpdateCount);
        Assert.AreEqual("new SMTP greeting", settings.WelcomeSMTP);
    }

    [TestMethod]
    public void AuthorizedSettings_WelcomeImapSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            WelcomeImapUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: "old IMAP greeting"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.WelcomeIMAP = "new IMAP greeting";

        Assert.AreEqual(1, store.WelcomeImapUpdateCount);
        Assert.AreEqual("new IMAP greeting", store.UpdatedWelcomeImap);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("new IMAP greeting", settings.WelcomeIMAP);

        store.WelcomeImapUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeIMAP = "failed IMAP greeting");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.WelcomeImapUpdateCount);
        Assert.AreEqual("new IMAP greeting", settings.WelcomeIMAP);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeIMAP = "denied IMAP greeting");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.WelcomeImapUpdateCount);
        Assert.AreEqual("new IMAP greeting", settings.WelcomeIMAP);
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
    public void AuthorizedSettings_TcpIpThreadsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            TcpIpThreadsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TcpIpThreads: 15),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.TCPIPThreads = 24;

        Assert.AreEqual(1, store.TcpIpThreadsUpdateCount);
        Assert.AreEqual(24, store.UpdatedTcpIpThreads);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(24, settings.TCPIPThreads);

        store.TcpIpThreadsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.TCPIPThreads = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.TcpIpThreadsUpdateCount);
        Assert.AreEqual(24, settings.TCPIPThreads);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.TCPIPThreads = 36);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.TcpIpThreadsUpdateCount);
        Assert.AreEqual(24, settings.TCPIPThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpMinutesBetweenTrySetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpMinutesBetweenTryUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpMinutesBetweenTry: 60),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPMinutesBetweenTry = 25;

        Assert.AreEqual(1, store.SmtpMinutesBetweenTryUpdateCount);
        Assert.AreEqual(25, store.UpdatedSmtpMinutesBetweenTry);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.SMTPMinutesBetweenTry);

        store.SmtpMinutesBetweenTryUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPMinutesBetweenTry = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpMinutesBetweenTryUpdateCount);
        Assert.AreEqual(25, settings.SMTPMinutesBetweenTry);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPMinutesBetweenTry = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpMinutesBetweenTryUpdateCount);
        Assert.AreEqual(25, settings.SMTPMinutesBetweenTry);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpNoOfTriesSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpNoOfTriesUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpNoOfTries: 4),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPNoOfTries = 8;

        Assert.AreEqual(1, store.SmtpNoOfTriesUpdateCount);
        Assert.AreEqual(8, store.UpdatedSmtpNoOfTries);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(8, settings.SMTPNoOfTries);

        store.SmtpNoOfTriesUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPNoOfTries = 12);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpNoOfTriesUpdateCount);
        Assert.AreEqual(8, settings.SMTPNoOfTries);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPNoOfTries = 16);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpNoOfTriesUpdateCount);
        Assert.AreEqual(8, settings.SMTPNoOfTries);
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
    public void AuthorizedSettings_MaxPop3ConnectionsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxPop3ConnectionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxPop3Connections: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxPOP3Connections = 25;

        Assert.AreEqual(1, store.MaxPop3ConnectionsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxPop3Connections);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxPOP3Connections);

        store.MaxPop3ConnectionsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxPOP3Connections = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxPop3ConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxPOP3Connections);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxPOP3Connections = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxPop3ConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxPOP3Connections);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxMessageSizeSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxMessageSizeUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxMessageSize: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxMessageSize = 25;

        Assert.AreEqual(1, store.MaxMessageSizeUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxMessageSize);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxMessageSize);

        store.MaxMessageSizeUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxMessageSize = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxMessageSizeUpdateCount);
        Assert.AreEqual(25, settings.MaxMessageSize);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxMessageSize = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxMessageSizeUpdateCount);
        Assert.AreEqual(25, settings.MaxMessageSize);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxDeliveryThreadsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxDeliveryThreadsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxDeliveryThreads: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxDeliveryThreads = 25;

        Assert.AreEqual(1, store.MaxDeliveryThreadsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxDeliveryThreads);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxDeliveryThreads);

        store.MaxDeliveryThreadsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxDeliveryThreads = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxDeliveryThreadsUpdateCount);
        Assert.AreEqual(25, settings.MaxDeliveryThreads);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxDeliveryThreads = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxDeliveryThreadsUpdateCount);
        Assert.AreEqual(25, settings.MaxDeliveryThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_RuleLoopLimitSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            RuleLoopLimitUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                RuleLoopLimit: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.RuleLoopLimit = 25;

        Assert.AreEqual(1, store.RuleLoopLimitUpdateCount);
        Assert.AreEqual(25, store.UpdatedRuleLoopLimit);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.RuleLoopLimit);

        store.RuleLoopLimitUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.RuleLoopLimit = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.RuleLoopLimitUpdateCount);
        Assert.AreEqual(25, settings.RuleLoopLimit);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.RuleLoopLimit = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.RuleLoopLimitUpdateCount);
        Assert.AreEqual(25, settings.RuleLoopLimit);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxImapConnectionsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxImapConnectionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxImapConnections: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxIMAPConnections = 25;

        Assert.AreEqual(1, store.MaxImapConnectionsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxImapConnections);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxIMAPConnections);

        store.MaxImapConnectionsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxIMAPConnections = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxImapConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxIMAPConnections);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxIMAPConnections = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxImapConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxIMAPConnections);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxNumberOfMXHostsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxNumberOfMXHostsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxNumberOfMxHosts: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxNumberOfMXHosts = 25;

        Assert.AreEqual(1, store.MaxNumberOfMXHostsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxNumberOfMXHosts);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxNumberOfMXHosts);

        store.MaxNumberOfMXHostsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfMXHosts = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxNumberOfMXHostsUpdateCount);
        Assert.AreEqual(25, settings.MaxNumberOfMXHosts);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfMXHosts = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxNumberOfMXHostsUpdateCount);
        Assert.AreEqual(25, settings.MaxNumberOfMXHosts);
    }

    [TestMethod]
    public void AuthorizedSettings_VerifyRemoteSslCertificateSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            VerifyRemoteSslCertificateUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                VerifyRemoteSslCertificate: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.VerifyRemoteSslCertificate = true;

        Assert.AreEqual(1, store.VerifyRemoteSslCertificateUpdateCount);
        Assert.IsTrue(store.UpdatedVerifyRemoteSslCertificate);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.VerifyRemoteSslCertificate);

        store.VerifyRemoteSslCertificateUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.VerifyRemoteSslCertificate = false);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.VerifyRemoteSslCertificateUpdateCount);
        Assert.IsTrue(settings.VerifyRemoteSslCertificate);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.VerifyRemoteSslCertificate = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.VerifyRemoteSslCertificateUpdateCount);
        Assert.IsTrue(settings.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxSmtpRecipientsInBatchSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxSmtpRecipientsInBatchUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxSmtpRecipientsInBatch: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxSMTPRecipientsInBatch = 25;

        Assert.AreEqual(1, store.MaxSmtpRecipientsInBatchUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxSmtpRecipientsInBatch);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxSMTPRecipientsInBatch);

        store.MaxSmtpRecipientsInBatchUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxSMTPRecipientsInBatch = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxSmtpRecipientsInBatchUpdateCount);
        Assert.AreEqual(25, settings.MaxSMTPRecipientsInBatch);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxSMTPRecipientsInBatch = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxSmtpRecipientsInBatchUpdateCount);
        Assert.AreEqual(25, settings.MaxSMTPRecipientsInBatch);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxNumberOfInvalidCommandsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxNumberOfInvalidCommandsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxNumberOfInvalidCommands: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxNumberOfInvalidCommands = 25;

        Assert.AreEqual(1, store.MaxNumberOfInvalidCommandsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxNumberOfInvalidCommands);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxNumberOfInvalidCommands);

        store.MaxNumberOfInvalidCommandsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfInvalidCommands = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxNumberOfInvalidCommandsUpdateCount);
        Assert.AreEqual(25, settings.MaxNumberOfInvalidCommands);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfInvalidCommands = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxNumberOfInvalidCommandsUpdateCount);
        Assert.AreEqual(25, settings.MaxNumberOfInvalidCommands);
    }

    [TestMethod]
    public void AuthorizedSettings_DisconnectInvalidClientsSetterPersistsTrueAndFalseBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            DisconnectInvalidClientsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                DisconnectInvalidClients: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.DisconnectInvalidClients = true;

        Assert.AreEqual(1, store.DisconnectInvalidClientsUpdateCount);
        Assert.IsTrue(store.UpdatedDisconnectInvalidClients);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.DisconnectInvalidClients);

        settings.DisconnectInvalidClients = false;

        Assert.AreEqual(2, store.DisconnectInvalidClientsUpdateCount);
        Assert.IsFalse(store.UpdatedDisconnectInvalidClients);
        Assert.IsFalse(settings.DisconnectInvalidClients);

        store.DisconnectInvalidClientsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.DisconnectInvalidClients = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.DisconnectInvalidClientsUpdateCount);
        Assert.IsFalse(settings.DisconnectInvalidClients);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.DisconnectInvalidClients = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.DisconnectInvalidClientsUpdateCount);
        Assert.IsFalse(settings.DisconnectInvalidClients);
    }

    [TestMethod]
    public void AuthorizedSettings_AddDeliveredToHeaderSetterPersistsTrueAndFalseBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AddDeliveredToHeaderUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AddDeliveredToHeader: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.AddDeliveredToHeader = true;

        Assert.AreEqual(1, store.AddDeliveredToHeaderUpdateCount);
        Assert.IsTrue(store.UpdatedAddDeliveredToHeader);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.AddDeliveredToHeader);

        settings.AddDeliveredToHeader = false;

        Assert.AreEqual(2, store.AddDeliveredToHeaderUpdateCount);
        Assert.IsFalse(store.UpdatedAddDeliveredToHeader);
        Assert.IsFalse(settings.AddDeliveredToHeader);

        store.AddDeliveredToHeaderUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.AddDeliveredToHeader = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.AddDeliveredToHeaderUpdateCount);
        Assert.IsFalse(settings.AddDeliveredToHeader);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AddDeliveredToHeader = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.AddDeliveredToHeaderUpdateCount);
        Assert.IsFalse(settings.AddDeliveredToHeader);
    }

    [TestMethod]
    public void AuthorizedSettings_AllowIncorrectLineEndingsSetterPersistsTrueAndFalseBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AllowIncorrectLineEndingsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AllowIncorrectLineEndings: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.AllowIncorrectLineEndings = true;

        Assert.AreEqual(1, store.AllowIncorrectLineEndingsUpdateCount);
        Assert.IsTrue(store.UpdatedAllowIncorrectLineEndings);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.AllowIncorrectLineEndings);

        settings.AllowIncorrectLineEndings = false;

        Assert.AreEqual(2, store.AllowIncorrectLineEndingsUpdateCount);
        Assert.IsFalse(store.UpdatedAllowIncorrectLineEndings);
        Assert.IsFalse(settings.AllowIncorrectLineEndings);

        store.AllowIncorrectLineEndingsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.AllowIncorrectLineEndings = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.AllowIncorrectLineEndingsUpdateCount);
        Assert.IsFalse(settings.AllowIncorrectLineEndings);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AllowIncorrectLineEndings = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.AllowIncorrectLineEndingsUpdateCount);
        Assert.IsFalse(settings.AllowIncorrectLineEndings);
    }

    [TestMethod]
    public void AuthorizedSettings_AllowSmtpAuthPlainSetterPersistsTrueAndFalseBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AllowSmtpAuthPlainUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AllowSmtpAuthPlain: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.AllowSMTPAuthPlain = true;

        Assert.AreEqual(1, store.AllowSmtpAuthPlainUpdateCount);
        Assert.IsTrue(store.UpdatedAllowSmtpAuthPlain);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.AllowSMTPAuthPlain);

        settings.AllowSMTPAuthPlain = false;

        Assert.AreEqual(2, store.AllowSmtpAuthPlainUpdateCount);
        Assert.IsFalse(store.UpdatedAllowSmtpAuthPlain);
        Assert.IsFalse(settings.AllowSMTPAuthPlain);

        store.AllowSmtpAuthPlainUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.AllowSMTPAuthPlain = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.AllowSmtpAuthPlainUpdateCount);
        Assert.IsFalse(settings.AllowSMTPAuthPlain);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AllowSMTPAuthPlain = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.AllowSmtpAuthPlainUpdateCount);
        Assert.IsFalse(settings.AllowSMTPAuthPlain);
    }

    [TestMethod]
    public void AuthorizedSettings_DenyMailFromNullSetterPersistsInvertedTrueAndFalseBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AllowMailFromNullUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AllowMailFromNull: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.DenyMailFromNull = true;

        Assert.AreEqual(1, store.AllowMailFromNullUpdateCount);
        Assert.IsFalse(store.UpdatedAllowMailFromNull);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.DenyMailFromNull);

        settings.DenyMailFromNull = false;

        Assert.AreEqual(2, store.AllowMailFromNullUpdateCount);
        Assert.IsTrue(store.UpdatedAllowMailFromNull);
        Assert.IsFalse(settings.DenyMailFromNull);

        store.AllowMailFromNullUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.DenyMailFromNull = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(3, store.AllowMailFromNullUpdateCount);
        Assert.IsFalse(settings.DenyMailFromNull);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.DenyMailFromNull = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(3, store.AllowMailFromNullUpdateCount);
        Assert.IsFalse(settings.DenyMailFromNull);
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

    private sealed class RecordingAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
    }

    private sealed class FakeSettingsAdministrationMutationStore :
        ISettingsAdministrationStore,
        ISettingsAdministrationMutationStore
    {
        public SettingsAdministrationSnapshot Snapshot { get; set; } = new(
            HostName: string.Empty,
            WelcomeSmtp: string.Empty,
            WelcomePop3: string.Empty,
            WelcomeImap: string.Empty);

        public bool UpdateResult { get; set; }

        public bool MirrorUpdateResult { get; set; }

        public bool SmtpRelayerRequiresAuthenticationUpdateResult { get; set; }

        public bool SmtpRelayerUpdateResult { get; set; }

        public bool SmtpRelayerUsernameUpdateResult { get; set; }

        public bool SmtpRelayerPortUpdateResult { get; set; }

        public bool SmtpRelayerConnectionSecurityUpdateResult { get; set; }

        public bool GateSmtpRelayerConnectionSecurityMutation { get; set; }

        public TaskCompletionSource<bool> SmtpRelayerConnectionSecurityMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SmtpRelayerConnectionSecurityMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SmtpRelayerRequiresAuthenticationUpdateCount { get; private set; }

        public bool UpdatedSmtpRelayerRequiresAuthentication { get; private set; }

        public int SmtpRelayerUpdateCount { get; private set; }

        public string? UpdatedSmtpRelayer { get; private set; }

        public int SmtpRelayerUsernameUpdateCount { get; private set; }

        public string? UpdatedSmtpRelayerUsername { get; private set; }

        public int SmtpRelayerPortUpdateCount { get; private set; }

        public int UpdatedSmtpRelayerPort { get; private set; }

        public int SmtpRelayerConnectionSecurityUpdateCount { get; private set; }

        public int UpdatedSmtpRelayerConnectionSecurity { get; private set; }

        public int UpdateCount { get; private set; }

        public string? UpdatedDefaultDomain { get; private set; }

        public int MirrorUpdateCount { get; private set; }

        public string? UpdatedMirrorEmailAddress { get; private set; }

        public bool WelcomePop3UpdateResult { get; set; }

        public int WelcomePop3UpdateCount { get; private set; }

        public string? UpdatedWelcomePop3 { get; private set; }

        public bool WelcomeSmtpUpdateResult { get; set; }

        public int WelcomeSmtpUpdateCount { get; private set; }

        public string? UpdatedWelcomeSmtp { get; private set; }

        public bool WelcomeImapUpdateResult { get; set; }

        public int WelcomeImapUpdateCount { get; private set; }

        public string? UpdatedWelcomeImap { get; private set; }

        public bool WorkerThreadPriorityUpdateResult { get; set; }

        public int WorkerThreadPriorityUpdateCount { get; private set; }

        public int UpdatedWorkerThreadPriority { get; private set; }

        public bool TcpIpThreadsUpdateResult { get; set; }

        public int TcpIpThreadsUpdateCount { get; private set; }

        public int UpdatedTcpIpThreads { get; private set; }

        public bool SmtpNoOfTriesUpdateResult { get; set; }

        public int SmtpNoOfTriesUpdateCount { get; private set; }

        public int UpdatedSmtpNoOfTries { get; private set; }

        public bool SmtpMinutesBetweenTryUpdateResult { get; set; }

        public int SmtpMinutesBetweenTryUpdateCount { get; private set; }

        public int UpdatedSmtpMinutesBetweenTry { get; private set; }

        public bool MaxSmtpConnectionsUpdateResult { get; set; }

        public int MaxSmtpConnectionsUpdateCount { get; private set; }

        public int UpdatedMaxSmtpConnections { get; private set; }

        public bool MaxPop3ConnectionsUpdateResult { get; set; }

        public int MaxPop3ConnectionsUpdateCount { get; private set; }

        public int UpdatedMaxPop3Connections { get; private set; }

        public bool MaxImapConnectionsUpdateResult { get; set; }

        public int MaxImapConnectionsUpdateCount { get; private set; }

        public int UpdatedMaxImapConnections { get; private set; }

        public bool MaxMessageSizeUpdateResult { get; set; }

        public int MaxMessageSizeUpdateCount { get; private set; }

        public int UpdatedMaxMessageSize { get; private set; }

        public bool MaxDeliveryThreadsUpdateResult { get; set; }

        public int MaxDeliveryThreadsUpdateCount { get; private set; }

        public int UpdatedMaxDeliveryThreads { get; private set; }

        public bool RuleLoopLimitUpdateResult { get; set; }

        public int RuleLoopLimitUpdateCount { get; private set; }

        public int UpdatedRuleLoopLimit { get; private set; }

        public bool MaxNumberOfMXHostsUpdateResult { get; set; }

        public int MaxNumberOfMXHostsUpdateCount { get; private set; }

        public int UpdatedMaxNumberOfMXHosts { get; private set; }

        public bool MaxSmtpRecipientsInBatchUpdateResult { get; set; }

        public int MaxSmtpRecipientsInBatchUpdateCount { get; private set; }

        public int UpdatedMaxSmtpRecipientsInBatch { get; private set; }

        public bool MaxNumberOfInvalidCommandsUpdateResult { get; set; }

        public int MaxNumberOfInvalidCommandsUpdateCount { get; private set; }

        public int UpdatedMaxNumberOfInvalidCommands { get; private set; }

        public bool DisconnectInvalidClientsUpdateResult { get; set; }

        public int DisconnectInvalidClientsUpdateCount { get; private set; }

        public bool UpdatedDisconnectInvalidClients { get; private set; }

        public bool AddDeliveredToHeaderUpdateResult { get; set; }

        public int AddDeliveredToHeaderUpdateCount { get; private set; }

        public bool UpdatedAddDeliveredToHeader { get; private set; }

        public bool AllowIncorrectLineEndingsUpdateResult { get; set; }

        public int AllowIncorrectLineEndingsUpdateCount { get; private set; }

        public bool UpdatedAllowIncorrectLineEndings { get; private set; }

        public bool AllowSmtpAuthPlainUpdateResult { get; set; }

        public int AllowSmtpAuthPlainUpdateCount { get; private set; }

        public bool UpdatedAllowSmtpAuthPlain { get; private set; }

        public bool AllowMailFromNullUpdateResult { get; set; }

        public int AllowMailFromNullUpdateCount { get; private set; }

        public bool UpdatedAllowMailFromNull { get; private set; }

        public bool VerifyRemoteSslCertificateUpdateResult { get; set; }

        public int VerifyRemoteSslCertificateUpdateCount { get; private set; }

        public bool UpdatedVerifyRemoteSslCertificate { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Snapshot);

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

        public ValueTask<bool> UpdateSmtpRelayerRequiresAuthenticationAsync(
            bool smtpRelayerRequiresAuthentication,
            CancellationToken cancellationToken)
        {
            SmtpRelayerRequiresAuthenticationUpdateCount++;
            UpdatedSmtpRelayerRequiresAuthentication = smtpRelayerRequiresAuthentication;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpRelayerRequiresAuthenticationUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpRelayerAsync(
            string smtpRelayer,
            CancellationToken cancellationToken)
        {
            SmtpRelayerUpdateCount++;
            UpdatedSmtpRelayer = smtpRelayer;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpRelayerUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpRelayerUsernameAsync(
            string smtpRelayerUsername,
            CancellationToken cancellationToken)
        {
            SmtpRelayerUsernameUpdateCount++;
            UpdatedSmtpRelayerUsername = smtpRelayerUsername;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpRelayerUsernameUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpRelayerPortAsync(
            int smtpRelayerPort,
            CancellationToken cancellationToken)
        {
            SmtpRelayerPortUpdateCount++;
            UpdatedSmtpRelayerPort = smtpRelayerPort;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpRelayerPortUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpRelayerConnectionSecurityAsync(
            int smtpRelayerConnectionSecurity,
            CancellationToken cancellationToken)
        {
            SmtpRelayerConnectionSecurityUpdateCount++;
            UpdatedSmtpRelayerConnectionSecurity = smtpRelayerConnectionSecurity;
            CancellationToken = cancellationToken;
            if (GateSmtpRelayerConnectionSecurityMutation)
            {
                SmtpRelayerConnectionSecurityMutationEntered.TrySetResult(true);
                return WaitForSmtpRelayerConnectionSecurityMutationAsync();
            }

            return ValueTask.FromResult(SmtpRelayerConnectionSecurityUpdateResult);
        }

        private async ValueTask<bool> WaitForSmtpRelayerConnectionSecurityMutationAsync()
        {
            await SmtpRelayerConnectionSecurityMutationRelease.Task;
            return SmtpRelayerConnectionSecurityUpdateResult;
        }

        public ValueTask<bool> UpdateWelcomePop3Async(
            string welcomePop3,
            CancellationToken cancellationToken)
        {
            WelcomePop3UpdateCount++;
            UpdatedWelcomePop3 = welcomePop3;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(WelcomePop3UpdateResult);
        }

        public ValueTask<bool> UpdateWelcomeSmtpAsync(
            string welcomeSmtp,
            CancellationToken cancellationToken)
        {
            WelcomeSmtpUpdateCount++;
            UpdatedWelcomeSmtp = welcomeSmtp;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(WelcomeSmtpUpdateResult);
        }

        public ValueTask<bool> UpdateWelcomeImapAsync(
            string welcomeImap,
            CancellationToken cancellationToken)
        {
            WelcomeImapUpdateCount++;
            UpdatedWelcomeImap = welcomeImap;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(WelcomeImapUpdateResult);
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

        public ValueTask<bool> UpdateTcpIpThreadsAsync(
            int tcpIpThreads,
            CancellationToken cancellationToken)
        {
            TcpIpThreadsUpdateCount++;
            UpdatedTcpIpThreads = tcpIpThreads;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(TcpIpThreadsUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpNoOfTriesAsync(
            int smtpNoOfTries,
            CancellationToken cancellationToken)
        {
            SmtpNoOfTriesUpdateCount++;
            UpdatedSmtpNoOfTries = smtpNoOfTries;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpNoOfTriesUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpMinutesBetweenTryAsync(
            int smtpMinutesBetweenTry,
            CancellationToken cancellationToken)
        {
            SmtpMinutesBetweenTryUpdateCount++;
            UpdatedSmtpMinutesBetweenTry = smtpMinutesBetweenTry;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpMinutesBetweenTryUpdateResult);
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

        public ValueTask<bool> UpdateMaxPop3ConnectionsAsync(
            int maxPop3Connections,
            CancellationToken cancellationToken)
        {
            MaxPop3ConnectionsUpdateCount++;
            UpdatedMaxPop3Connections = maxPop3Connections;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxPop3ConnectionsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxImapConnectionsAsync(
            int maxImapConnections,
            CancellationToken cancellationToken)
        {
            MaxImapConnectionsUpdateCount++;
            UpdatedMaxImapConnections = maxImapConnections;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxImapConnectionsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxMessageSizeAsync(
            int maxMessageSize,
            CancellationToken cancellationToken)
        {
            MaxMessageSizeUpdateCount++;
            UpdatedMaxMessageSize = maxMessageSize;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxMessageSizeUpdateResult);
        }

        public ValueTask<bool> UpdateMaxDeliveryThreadsAsync(
            int maxDeliveryThreads,
            CancellationToken cancellationToken)
        {
            MaxDeliveryThreadsUpdateCount++;
            UpdatedMaxDeliveryThreads = maxDeliveryThreads;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxDeliveryThreadsUpdateResult);
        }

        public ValueTask<bool> UpdateRuleLoopLimitAsync(
            int ruleLoopLimit,
            CancellationToken cancellationToken)
        {
            RuleLoopLimitUpdateCount++;
            UpdatedRuleLoopLimit = ruleLoopLimit;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(RuleLoopLimitUpdateResult);
        }

        public ValueTask<bool> UpdateMaxNumberOfMXHostsAsync(
            int maxNumberOfMXHosts,
            CancellationToken cancellationToken)
        {
            MaxNumberOfMXHostsUpdateCount++;
            UpdatedMaxNumberOfMXHosts = maxNumberOfMXHosts;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxNumberOfMXHostsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxSmtpRecipientsInBatchAsync(
            int maxSmtpRecipientsInBatch,
            CancellationToken cancellationToken)
        {
            MaxSmtpRecipientsInBatchUpdateCount++;
            UpdatedMaxSmtpRecipientsInBatch = maxSmtpRecipientsInBatch;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxSmtpRecipientsInBatchUpdateResult);
        }

        public ValueTask<bool> UpdateMaxNumberOfInvalidCommandsAsync(
            int maxNumberOfInvalidCommands,
            CancellationToken cancellationToken)
        {
            MaxNumberOfInvalidCommandsUpdateCount++;
            UpdatedMaxNumberOfInvalidCommands = maxNumberOfInvalidCommands;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxNumberOfInvalidCommandsUpdateResult);
        }

        public ValueTask<bool> UpdateDisconnectInvalidClientsAsync(
            bool disconnectInvalidClients,
            CancellationToken cancellationToken)
        {
            DisconnectInvalidClientsUpdateCount++;
            UpdatedDisconnectInvalidClients = disconnectInvalidClients;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(DisconnectInvalidClientsUpdateResult);
        }

        public ValueTask<bool> UpdateAddDeliveredToHeaderAsync(
            bool addDeliveredToHeader,
            CancellationToken cancellationToken)
        {
            AddDeliveredToHeaderUpdateCount++;
            UpdatedAddDeliveredToHeader = addDeliveredToHeader;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AddDeliveredToHeaderUpdateResult);
        }

        public ValueTask<bool> UpdateAllowIncorrectLineEndingsAsync(
            bool allowIncorrectLineEndings,
            CancellationToken cancellationToken)
        {
            AllowIncorrectLineEndingsUpdateCount++;
            UpdatedAllowIncorrectLineEndings = allowIncorrectLineEndings;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AllowIncorrectLineEndingsUpdateResult);
        }

        public ValueTask<bool> UpdateAllowSmtpAuthPlainAsync(
            bool allowSmtpAuthPlain,
            CancellationToken cancellationToken)
        {
            AllowSmtpAuthPlainUpdateCount++;
            UpdatedAllowSmtpAuthPlain = allowSmtpAuthPlain;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AllowSmtpAuthPlainUpdateResult);
        }

        public ValueTask<bool> UpdateAllowMailFromNullAsync(
            bool allowMailFromNull,
            CancellationToken cancellationToken)
        {
            AllowMailFromNullUpdateCount++;
            UpdatedAllowMailFromNull = allowMailFromNull;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AllowMailFromNullUpdateResult);
        }

        public ValueTask<bool> UpdateVerifyRemoteSslCertificateAsync(
            bool verifyRemoteSslCertificate,
            CancellationToken cancellationToken)
        {
            VerifyRemoteSslCertificateUpdateCount++;
            UpdatedVerifyRemoteSslCertificate = verifyRemoteSslCertificate;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(VerifyRemoteSslCertificateUpdateResult);
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
