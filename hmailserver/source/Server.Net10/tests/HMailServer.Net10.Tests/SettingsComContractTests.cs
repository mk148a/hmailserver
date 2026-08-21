using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SettingsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
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
    public void AuthorizedSettings_AntiVirusClamWinEnabledPersistsWithAuthorizationLease()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            UpdateResult = true,
            AntiVirusClamWinEnabledMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinEnabled: true),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.AntiVirus.ClamWinEnabled = false;

        Assert.AreEqual(1, store.AntiVirusClamWinEnabledUpdateCount);
        Assert.IsFalse(store.UpdatedAntiVirusClamWinEnabled);
        Assert.IsTrue(store.AntiVirusClamWinEnabledLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.IsFalse(settings.AntiVirus.ClamWinEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusClamWinEnabledFailureDoesNotPublishSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            UpdateResult = false
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinEnabled: true),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.AntiVirus.ClamWinEnabled = false);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.IsTrue(settings.AntiVirus.ClamWinEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusClamWinExecutablePersistsAndPublishesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = true };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinExecutable: "old-clamwin.exe"),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.AntiVirus.ClamWinExecutable = "new-clamwin.exe";

        Assert.AreEqual(1, store.AntiVirusClamWinExecutableUpdateCount);
        Assert.AreEqual("new-clamwin.exe", store.UpdatedAntiVirusClamWinExecutable);
        Assert.AreEqual("new-clamwin.exe", settings.AntiVirus.ClamWinExecutable);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusClamWinExecutableFailureDoesNotPublishSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = false };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinExecutable: "old-clamwin.exe"),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.AntiVirus.ClamWinExecutable = "new-clamwin.exe");

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual("old-clamwin.exe", settings.AntiVirus.ClamWinExecutable);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusClamWinDbFolderPersistsAndPublishesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = true };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinDatabase: "old-clamwin-db"),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.AntiVirus.ClamWinDBFolder = "new-clamwin-db";

        Assert.AreEqual(1, store.AntiVirusClamWinDatabaseUpdateCount);
        Assert.AreEqual("new-clamwin-db", store.UpdatedAntiVirusClamWinDatabase);
        Assert.AreEqual("new-clamwin-db", settings.AntiVirus.ClamWinDBFolder);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusClamWinDbFolderFailureDoesNotPublishSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = false };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusClamWinDatabase: "old-clamwin-db"),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.AntiVirus.ClamWinDBFolder = "new-clamwin-db");

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual("old-clamwin-db", settings.AntiVirus.ClamWinDBFolder);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusActionPersistsAndPublishesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = true };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusAction: (int)ComAntivirusAction.DeleteEmail),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.AntiVirus.Action = ComAntivirusAction.DeleteAttachments;

        Assert.AreEqual(1, store.AntiVirusActionUpdateCount);
        Assert.AreEqual((int)ComAntivirusAction.DeleteAttachments, store.UpdatedAntiVirusAction);
        Assert.AreEqual(ComAntivirusAction.DeleteAttachments, settings.AntiVirus.Action);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusActionFailureDoesNotPublishSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = false };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusAction: (int)ComAntivirusAction.DeleteEmail),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.AntiVirus.Action = ComAntivirusAction.DeleteAttachments);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(ComAntivirusAction.DeleteEmail, settings.AntiVirus.Action);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusNotifyReceiverPersistsAndPublishesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = true };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusNotifyReceiver: true),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.AntiVirus.NotifyReceiver = false;

        Assert.AreEqual(1, store.AntiVirusNotifyReceiverUpdateCount);
        Assert.IsFalse(store.UpdatedAntiVirusNotifyReceiver);
        Assert.IsFalse(settings.AntiVirus.NotifyReceiver);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusNotifyReceiverFailureDoesNotPublishSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = false };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusNotifyReceiver: true),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.AntiVirus.NotifyReceiver = false);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.IsTrue(settings.AntiVirus.NotifyReceiver);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusNotifySenderPersistsAndPublishesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = true };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusNotifySender: true),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.AntiVirus.NotifySender = false;

        Assert.AreEqual(1, store.AntiVirusNotifySenderUpdateCount);
        Assert.IsFalse(store.UpdatedAntiVirusNotifySender);
        Assert.IsFalse(settings.AntiVirus.NotifySender);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiVirusNotifySenderFailureDoesNotPublishSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore { UpdateResult = false };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiVirusNotifySender: true),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.AntiVirus.NotifySender = false);

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.IsTrue(settings.AntiVirus.NotifySender);
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
        var relayerPasswordError = Assert.ThrowsExactly<COMException>(
            () => settings.SetSMTPRelayerPassword("direct-activation password"));
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
        Assert.AreEqual(EAccessDenied, relayerPasswordError.ErrorCode);
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
    public void Configure_PublishesPersistedWelcomeSmtpWithoutSettingsComAccess()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: "bootstrap-only SMTP greeting",
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };

        SettingsAdministrationRuntimeHost.Configure(store);

        Assert.AreEqual(
            "bootstrap-only SMTP greeting",
            SettingsAdministrationRuntimeHost.GetSmtpGreeting());
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
    public void AuthorizedSettings_DefaultDomainSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.DefaultDomain = "new.example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.UpdateCount);
        Assert.AreEqual("old.example.test", settings.DefaultDomain);
    }

    [TestMethod]
    public void AuthorizedSettings_DefaultDomainSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            UpdateResult = true,
            DefaultDomainMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                DefaultDomain: "old.example.test"),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.DefaultDomain = "new.example.test";

        Assert.IsTrue(store.DefaultDomainLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.AreEqual("new.example.test", settings.DefaultDomain);
    }

    [TestMethod]
    public void AuthorizedSettings_ServiceSmtpSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ServiceSmtpUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ServiceSmtp: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.ServiceSMTP = false;

        Assert.AreEqual(1, store.ServiceSmtpUpdateCount);
        Assert.IsFalse(store.UpdatedServiceSmtp);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.ServiceSMTP);

        store.ServiceSmtpUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.ServiceSMTP = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ServiceSmtpUpdateCount);
        Assert.IsFalse(settings.ServiceSMTP);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.ServiceSMTP = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ServiceSmtpUpdateCount);
        Assert.IsFalse(settings.ServiceSMTP);
    }

    [TestMethod]
    public void AuthorizedSettings_ServicePop3SetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ServicePop3UpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ServicePop3: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.ServicePOP3 = false;

        Assert.AreEqual(1, store.ServicePop3UpdateCount);
        Assert.IsFalse(store.UpdatedServicePop3);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.ServicePOP3);

        store.ServicePop3UpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.ServicePOP3 = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ServicePop3UpdateCount);
        Assert.IsFalse(settings.ServicePOP3);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.ServicePOP3 = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ServicePop3UpdateCount);
        Assert.IsFalse(settings.ServicePOP3);
    }

    [TestMethod]
    public void AuthorizedSettings_ServiceImapSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ServiceImapUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ServiceImap: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.ServiceIMAP = false;

        Assert.AreEqual(1, store.ServiceImapUpdateCount);
        Assert.IsFalse(store.UpdatedServiceImap);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.ServiceIMAP);

        store.ServiceImapUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.ServiceIMAP = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ServiceImapUpdateCount);
        Assert.IsFalse(settings.ServiceIMAP);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.ServiceIMAP = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ServiceImapUpdateCount);
        Assert.IsFalse(settings.ServiceIMAP);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpDeliveryBindToIpSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpDeliveryBindToIpUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpDeliveryBindToIp: "192.0.2.25"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPDeliveryBindToIP = "192.0.2.26";

        Assert.AreEqual(1, store.SmtpDeliveryBindToIpUpdateCount);
        Assert.AreEqual("192.0.2.26", store.UpdatedSmtpDeliveryBindToIp);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("192.0.2.26", settings.SMTPDeliveryBindToIP);

        store.SmtpDeliveryBindToIpUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPDeliveryBindToIP = "192.0.2.27");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpDeliveryBindToIpUpdateCount);
        Assert.AreEqual("192.0.2.26", settings.SMTPDeliveryBindToIP);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPDeliveryBindToIP = "192.0.2.28");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpDeliveryBindToIpUpdateCount);
        Assert.AreEqual("192.0.2.26", settings.SMTPDeliveryBindToIP);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapSortEnabledSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapSortEnabledUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapSortEnabled: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPSortEnabled = false;

        Assert.AreEqual(1, store.ImapSortEnabledUpdateCount);
        Assert.IsFalse(store.UpdatedImapSortEnabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.IMAPSortEnabled);

        store.ImapSortEnabledUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPSortEnabled = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapSortEnabledUpdateCount);
        Assert.IsFalse(settings.IMAPSortEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPSortEnabled = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapSortEnabledUpdateCount);
        Assert.IsFalse(settings.IMAPSortEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapQuotaEnabledSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapQuotaEnabledUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapQuotaEnabled: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPQuotaEnabled = true;

        Assert.AreEqual(1, store.ImapQuotaEnabledUpdateCount);
        Assert.IsTrue(store.UpdatedImapQuotaEnabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.IMAPQuotaEnabled);

        store.ImapQuotaEnabledUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPQuotaEnabled = false);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapQuotaEnabledUpdateCount);
        Assert.IsTrue(settings.IMAPQuotaEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPQuotaEnabled = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapQuotaEnabledUpdateCount);
        Assert.IsTrue(settings.IMAPQuotaEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapIdleEnabledSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapIdleEnabledUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapIdleEnabled: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPIdleEnabled = false;

        Assert.AreEqual(1, store.ImapIdleEnabledUpdateCount);
        Assert.IsFalse(store.UpdatedImapIdleEnabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.IMAPIdleEnabled);

        store.ImapIdleEnabledUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPIdleEnabled = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapIdleEnabledUpdateCount);
        Assert.IsFalse(settings.IMAPIdleEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPIdleEnabled = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapIdleEnabledUpdateCount);
        Assert.IsFalse(settings.IMAPIdleEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapAclEnabledSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapAclEnabledUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapAclEnabled: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPACLEnabled = true;

        Assert.AreEqual(1, store.ImapAclEnabledUpdateCount);
        Assert.IsTrue(store.UpdatedImapAclEnabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.IMAPACLEnabled);

        store.ImapAclEnabledUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPACLEnabled = false);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapAclEnabledUpdateCount);
        Assert.IsTrue(settings.IMAPACLEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPACLEnabled = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapAclEnabledUpdateCount);
        Assert.IsTrue(settings.IMAPACLEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapSaslPlainEnabledSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapSaslPlainEnabledUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapSaslPlainEnabled: true),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPSASLPlainEnabled = false;

        Assert.AreEqual(1, store.ImapSaslPlainEnabledUpdateCount);
        Assert.IsFalse(store.UpdatedImapSaslPlainEnabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsFalse(settings.IMAPSASLPlainEnabled);

        store.ImapSaslPlainEnabledUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPSASLPlainEnabled = true);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapSaslPlainEnabledUpdateCount);
        Assert.IsFalse(settings.IMAPSASLPlainEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPSASLPlainEnabled = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapSaslPlainEnabledUpdateCount);
        Assert.IsFalse(settings.IMAPSASLPlainEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapSaslInitialResponseEnabledSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapSaslInitialResponseEnabledUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapSaslInitialResponseEnabled: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPSASLInitialResponseEnabled = true;

        Assert.AreEqual(1, store.ImapSaslInitialResponseEnabledUpdateCount);
        Assert.IsTrue(store.UpdatedImapSaslInitialResponseEnabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.IMAPSASLInitialResponseEnabled);

        store.ImapSaslInitialResponseEnabledUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPSASLInitialResponseEnabled = false);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapSaslInitialResponseEnabledUpdateCount);
        Assert.IsTrue(settings.IMAPSASLInitialResponseEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPSASLInitialResponseEnabled = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapSaslInitialResponseEnabledUpdateCount);
        Assert.IsTrue(settings.IMAPSASLInitialResponseEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapPublicFolderNameSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapPublicFolderNameUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapPublicFolderName: "#Public"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        const string newName = "#Shared";
        settings.IMAPPublicFolderName = newName;

        Assert.AreEqual(1, store.ImapPublicFolderNameUpdateCount);
        Assert.AreEqual(newName, store.UpdatedImapPublicFolderName);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(newName, settings.IMAPPublicFolderName);

        store.ImapPublicFolderNameUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPPublicFolderName = "#Failed");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapPublicFolderNameUpdateCount);
        Assert.AreEqual(newName, settings.IMAPPublicFolderName);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPPublicFolderName = "#Denied");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapPublicFolderNameUpdateCount);
        Assert.AreEqual(newName, settings.IMAPPublicFolderName);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapMasterUserSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapMasterUserUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapMasterUser: "old-master"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        const string newMasterUser = "new-master";
        settings.IMAPMasterUser = newMasterUser;

        Assert.AreEqual(1, store.ImapMasterUserUpdateCount);
        Assert.AreEqual(newMasterUser, store.UpdatedImapMasterUser);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(newMasterUser, settings.IMAPMasterUser);

        store.ImapMasterUserUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPMasterUser = "failed-master");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.ImapMasterUserUpdateCount);
        Assert.AreEqual(newMasterUser, settings.IMAPMasterUser);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPMasterUser = "denied-master");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapMasterUserUpdateCount);
        Assert.AreEqual(newMasterUser, settings.IMAPMasterUser);
    }

    [TestMethod]
    public void AuthorizedSettings_HostNameSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            HostNameUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: "old.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        const string newHostName = "mail.example.test";
        settings.HostName = newHostName;

        Assert.AreEqual(1, store.HostNameUpdateCount);
        Assert.AreEqual(newHostName, store.UpdatedHostName);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(newHostName, settings.HostName);

        store.HostNameUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.HostName = "failed.example.test");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.HostNameUpdateCount);
        Assert.AreEqual(newHostName, settings.HostName);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.HostName = "denied.example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.HostNameUpdateCount);
        Assert.AreEqual(newHostName, settings.HostName);
    }

    [TestMethod]
    public void AuthorizedSettings_UserInterfaceLanguageWritesIniAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var language = "English";
        var writeCount = 0;
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                UserInterfaceLanguage: language,
                UserInterfaceLanguageReader: () => language,
                UserInterfaceLanguageWriter: value =>
                {
                    writeCount++;
                    language = value;
                }),
            isServerAdministrator: () => isServerAdministrator);

        settings.UserInterfaceLanguage = "Turkish";

        Assert.AreEqual(1, writeCount);
        Assert.AreEqual("Turkish", settings.UserInterfaceLanguage);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.UserInterfaceLanguage = "Swedish");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, writeCount);
        Assert.AreEqual("Turkish", settings.UserInterfaceLanguage);
    }

    [TestMethod]
    public void AuthorizedSettings_RewriteEnvelopeFromWhenForwardingWritesIniAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var rewriteEnvelope = true;
        var writeCount = 0;
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                RewriteEnvelopeFromWhenForwarding: rewriteEnvelope,
                RewriteEnvelopeFromWhenForwardingWriter: value =>
                {
                    writeCount++;
                    rewriteEnvelope = value;
                }),
            isServerAdministrator: () => isServerAdministrator);

        settings.RewriteEnvelopeFromWhenForwarding = false;

        Assert.AreEqual(1, writeCount);
        Assert.IsFalse(rewriteEnvelope);
        Assert.IsFalse(settings.RewriteEnvelopeFromWhenForwarding);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.RewriteEnvelopeFromWhenForwarding = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, writeCount);
        Assert.IsFalse(settings.RewriteEnvelopeFromWhenForwarding);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsVersion10SetterPreservesOtherBitsAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SslVersionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SslVersions: 8),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.TlsVersion10Enabled = true;

        Assert.AreEqual(1, store.SslVersionsUpdateCount);
        Assert.AreEqual(10, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion10Enabled);
        Assert.IsTrue(settings.TlsVersion12Enabled);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);

        store.SslVersionsUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion10Enabled = false).ErrorCode);
        Assert.AreEqual(8, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion10Enabled);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion10Enabled = false).ErrorCode);
        Assert.AreEqual(2, store.SslVersionsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsVersion11SetterPreservesOtherBitsAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SslVersionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SslVersions: 2),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.TlsVersion11Enabled = true;

        Assert.AreEqual(1, store.SslVersionsUpdateCount);
        Assert.AreEqual(6, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion11Enabled);
        Assert.IsTrue(settings.TlsVersion10Enabled);

        store.SslVersionsUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion11Enabled = false).ErrorCode);
        Assert.AreEqual(2, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion11Enabled);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion11Enabled = false).ErrorCode);
        Assert.AreEqual(2, store.SslVersionsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsVersion12SetterPreservesOtherBitsAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SslVersionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SslVersions: 6),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.TlsVersion12Enabled = true;

        Assert.AreEqual(1, store.SslVersionsUpdateCount);
        Assert.AreEqual(14, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion12Enabled);
        Assert.IsTrue(settings.TlsVersion10Enabled);
        Assert.IsTrue(settings.TlsVersion11Enabled);

        store.SslVersionsUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion12Enabled = false).ErrorCode);
        Assert.AreEqual(6, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion12Enabled);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion12Enabled = false).ErrorCode);
        Assert.AreEqual(2, store.SslVersionsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsVersion13SetterPreservesOtherBitsAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SslVersionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SslVersions: 14),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.TlsVersion13Enabled = true;

        Assert.AreEqual(1, store.SslVersionsUpdateCount);
        Assert.AreEqual(30, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion13Enabled);
        Assert.IsTrue(settings.TlsVersion10Enabled);
        Assert.IsTrue(settings.TlsVersion11Enabled);
        Assert.IsTrue(settings.TlsVersion12Enabled);

        store.SslVersionsUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion13Enabled = false).ErrorCode);
        Assert.AreEqual(14, store.UpdatedSslVersions);
        Assert.IsTrue(settings.TlsVersion13Enabled);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.TlsVersion13Enabled = false).ErrorCode);
        Assert.AreEqual(2, store.SslVersionsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsOptionPreferServerCiphersSetterPreservesOtherBitsAndRetainsFailedSnapshot()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            TlsOptionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TlsOptions: 4),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.TlsOptionPreferServerCiphersEnabled = true;

        Assert.AreEqual(1, store.TlsOptionsUpdateCount);
        Assert.AreEqual(6, store.UpdatedTlsOptions);
        Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);
        Assert.IsTrue(settings.TlsOptionPrioritizeChaChaEnabled);

        store.TlsOptionsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.TlsOptionPreferServerCiphersEnabled = false);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(4, store.UpdatedTlsOptions);
        Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);
        Assert.IsTrue(settings.TlsOptionPrioritizeChaChaEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.TlsOptionPreferServerCiphersEnabled = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.TlsOptionsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsOptionPreferServerCiphersSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            TlsOptionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TlsOptions: 4),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.TlsOptionPreferServerCiphersEnabled = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.TlsOptionsUpdateCount);
        Assert.IsFalse(settings.TlsOptionPreferServerCiphersEnabled);
        Assert.IsTrue(settings.TlsOptionPrioritizeChaChaEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsOptionPrioritizeChaChaSetterPreservesOtherBitsForBothTransitions()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            TlsOptionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TlsOptions: 2),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.TlsOptionPrioritizeChaChaEnabled = true;

        Assert.AreEqual(1, store.TlsOptionsUpdateCount);
        Assert.AreEqual(6, store.UpdatedTlsOptions);
        Assert.IsTrue(settings.TlsOptionPrioritizeChaChaEnabled);
        Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);

        settings.TlsOptionPrioritizeChaChaEnabled = false;

        Assert.AreEqual(2, store.TlsOptionsUpdateCount);
        Assert.AreEqual(2, store.UpdatedTlsOptions);
        Assert.IsFalse(settings.TlsOptionPrioritizeChaChaEnabled);
        Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsOptionPrioritizeChaChaSetterRetainsFailedSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            TlsOptionsUpdateResult = false
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TlsOptions: 2),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.TlsOptionPrioritizeChaChaEnabled = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.TlsOptionsUpdateCount);
        Assert.AreEqual(6, store.UpdatedTlsOptions);
        Assert.IsFalse(settings.TlsOptionPrioritizeChaChaEnabled);
        Assert.IsTrue(settings.TlsOptionPreferServerCiphersEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_TlsOptionPrioritizeChaChaSetterDeniesUnavailableLeaseAndNonAdministratorBeforeMutation()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            TlsOptionsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TlsOptions: 2),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var unavailableLease = Assert.ThrowsExactly<COMException>(
            () => settings.TlsOptionPrioritizeChaChaEnabled = true);

        Assert.AreEqual(EAccessDenied, unavailableLease.ErrorCode);
        Assert.AreEqual(0, store.TlsOptionsUpdateCount);
        Assert.IsFalse(settings.TlsOptionPrioritizeChaChaEnabled);

        isServerAdministrator = false;
        var nonAdministrator = Assert.ThrowsExactly<COMException>(
            () => settings.TlsOptionPrioritizeChaChaEnabled = true);

        Assert.AreEqual(EAccessDenied, nonAdministrator.ErrorCode);
        Assert.AreEqual(0, store.TlsOptionsUpdateCount);
        Assert.IsFalse(settings.TlsOptionPrioritizeChaChaEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_ImapHierarchyDelimiterSetterPersistsBeforePublishingAndRetainsRejectedState()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            ImapHierarchyDelimiterUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                ImapHierarchyDelimiter: "."),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IMAPHierarchyDelimiter = "/";

        Assert.AreEqual(1, store.ImapHierarchyDelimiterUpdateCount);
        Assert.AreEqual("/", store.UpdatedImapHierarchyDelimiter);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("/", settings.IMAPHierarchyDelimiter);

        store.ImapHierarchyDelimiterUpdateResult = false;
        var rejected = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPHierarchyDelimiter = "-");

        Assert.AreEqual(unchecked((int)0x80004005), rejected.ErrorCode);
        Assert.AreEqual(2, store.ImapHierarchyDelimiterUpdateCount);
        Assert.AreEqual("/", settings.IMAPHierarchyDelimiter);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IMAPHierarchyDelimiter = "-");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.ImapHierarchyDelimiterUpdateCount);
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
    public void AuthorizedSettings_SmtpRelayerSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SMTPRelayer = "new-relay.example.test";

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpRelayerUpdateCount);
        Assert.AreEqual("new-relay.example.test", settings.SMTPRelayer);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayer = "new-relay.example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpRelayerUpdateCount);
        Assert.AreEqual("old-relay.example.test", settings.SMTPRelayer);
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
    public void AuthorizedSettings_SmtpRelayerRequiresAuthenticationSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SMTPRelayerRequiresAuthentication = true;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpRelayerRequiresAuthenticationUpdateCount);
        Assert.IsTrue(settings.SMTPRelayerRequiresAuthentication);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerRequiresAuthenticationSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerRequiresAuthentication = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpRelayerRequiresAuthenticationUpdateCount);
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
    public void AuthorizedSettings_SmtpRelayerUsernameSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SMTPRelayerUsername = "new-user";

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpRelayerUsernameUpdateCount);
        Assert.AreEqual("new-user", settings.SMTPRelayerUsername);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerUsernameSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerUsername = "new-user");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpRelayerUsernameUpdateCount);
        Assert.AreEqual("old-user", settings.SMTPRelayerUsername);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerPasswordMutationRetainsAuthorizationAndFailedWriteState()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerPasswordUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SetSMTPRelayerPassword("new-password");

        Assert.AreEqual(1, store.SmtpRelayerPasswordUpdateCount);
        Assert.AreEqual("new-password", store.UpdatedSmtpRelayerPassword);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);

        store.SmtpRelayerPasswordUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SetSMTPRelayerPassword("failed-password"));

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerPasswordUpdateCount);
        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SetSMTPRelayerPassword("denied-password"));

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SmtpRelayerPasswordUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerPasswordMutationAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerPasswordUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SetSMTPRelayerPassword("new-password");

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpRelayerPasswordUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerPasswordMutationUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpRelayerPasswordUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SetSMTPRelayerPassword("new-password"));

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpRelayerPasswordUpdateCount);
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
    public void AuthorizedSettings_SmtpRelayerPortSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SMTPRelayerPort = 587;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpRelayerPortUpdateCount);
        Assert.AreEqual(587, settings.SMTPRelayerPort);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpRelayerPortSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPRelayerPort = 587);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpRelayerPortUpdateCount);
        Assert.AreEqual(25, settings.SMTPRelayerPort);
    }

    [TestMethod]
    public async Task ApplicationSettings_SMTPRelayerPortMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpRelayerPort: 25),
            SmtpRelayerPortUpdateResult = true,
            GateSmtpRelayerPortMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.SMTPRelayerPort = 587);
        await store.SmtpRelayerPortMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.SmtpRelayerPortMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual(587, store.UpdatedSmtpRelayerPort);
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
    public void AuthorizedSettings_SmtpConnectionSecuritySetterPersistsBeforePublishing()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpConnectionSecurityUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpConnectionSecurity: (int)ComConnectionSecurity.None),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        settings.SMTPConnectionSecurity = ComConnectionSecurity.StartTlsOptional;

        Assert.AreEqual(1, store.SmtpConnectionSecurityUpdateCount);
        Assert.AreEqual((int)ComConnectionSecurity.StartTlsOptional, store.UpdatedSmtpConnectionSecurity);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(ComConnectionSecurity.StartTlsOptional, settings.SMTPConnectionSecurity);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpConnectionSecuritySetterFailedRowRetainsSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpConnectionSecurityUpdateResult = false
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpConnectionSecurity: (int)ComConnectionSecurity.None),
            isServerAdministrator: static () => true,
            settingsMutationStore: store);

        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPConnectionSecurity = ComConnectionSecurity.Tls);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(1, store.SmtpConnectionSecurityUpdateCount);
        Assert.AreEqual((int)ComConnectionSecurity.Tls, store.UpdatedSmtpConnectionSecurity);
        Assert.AreEqual(ComConnectionSecurity.None, settings.SMTPConnectionSecurity);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpConnectionSecuritySetterAdminRevocationDeniesBeforeMutation()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SmtpConnectionSecurityUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SmtpConnectionSecurity: (int)ComConnectionSecurity.None),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SMTPConnectionSecurity = ComConnectionSecurity.StartTlsRequired;
        isServerAdministrator = false;

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPConnectionSecurity = ComConnectionSecurity.Tls);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, store.SmtpConnectionSecurityUpdateCount);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, settings.SMTPConnectionSecurity);
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
    public void AuthorizedSettings_MirrorEmailSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.MirrorEMailAddress = "new@example.test";

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.MirrorUpdateCount);
        Assert.AreEqual("new@example.test", store.UpdatedMirrorEmailAddress);
        Assert.AreEqual("new@example.test", settings.MirrorEMailAddress);
    }

    [TestMethod]
    public void AuthorizedSettings_MirrorEmailSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MirrorEMailAddress = "new@example.test");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MirrorUpdateCount);
        Assert.AreEqual("old@example.test", settings.MirrorEMailAddress);
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
    public void AuthorizedSettings_WelcomePop3SetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.WelcomePOP3 = "new POP3 greeting";

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.WelcomePop3UpdateCount);
        Assert.AreEqual("new POP3 greeting", settings.WelcomePOP3);
    }

    [TestMethod]
    public void AuthorizedSettings_WelcomePop3SetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomePOP3 = "new POP3 greeting");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.WelcomePop3UpdateCount);
        Assert.AreEqual("old POP3 greeting", settings.WelcomePOP3);
    }

    [TestMethod]
    public async Task ApplicationSettings_WelcomePop3MutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: "old POP3 greeting",
                WelcomeImap: string.Empty),
            WelcomePop3UpdateResult = true,
            GateWelcomePop3Mutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.WelcomePOP3 = "new POP3 greeting");
        await store.WelcomePop3MutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.WelcomePop3MutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual("new POP3 greeting", store.UpdatedWelcomePop3);
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
    public void AuthorizedSettings_WelcomeSmtpRejectsLineBreakBeforeMutationOrPublication()
    {
        foreach (var unsafeWelcome in new[]
        {
            "malicious\r250 injected",
            "malicious\n250 injected"
        })
        {
            var store = new FakeSettingsAdministrationMutationStore
            {
                Snapshot = new SettingsAdministrationSnapshot(
                    HostName: "mail.example.test",
                    WelcomeSmtp: "old SMTP greeting",
                    WelcomePop3: string.Empty,
                    WelcomeImap: string.Empty)
            };
            SettingsAdministrationRuntimeHost.Configure(store);
            IInterfaceSettings settings = Settings.CreateAuthorized(
                store.Snapshot,
                isServerAdministrator: static () => true,
                settingsMutationStore: store);

            var error = Assert.ThrowsExactly<COMException>(
                () => settings.WelcomeSMTP = unsafeWelcome);

            Assert.AreEqual(EInvalidArg, error.ErrorCode);
            Assert.AreEqual(0, store.WelcomeSmtpUpdateCount);
            Assert.AreEqual("old SMTP greeting", settings.WelcomeSMTP);
            Assert.AreEqual("old SMTP greeting", SettingsAdministrationRuntimeHost.GetSmtpGreeting());
        }
    }

    [TestMethod]
    public void AuthorizedSettings_WelcomeSmtpSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.WelcomeSMTP = "new SMTP greeting";

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.WelcomeSmtpUpdateCount);
        Assert.AreEqual("new SMTP greeting", settings.WelcomeSMTP);
    }

    [TestMethod]
    public void AuthorizedSettings_WelcomeSmtpSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeSMTP = "new SMTP greeting");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.WelcomeSmtpUpdateCount);
        Assert.AreEqual("old SMTP greeting", settings.WelcomeSMTP);
    }

    [TestMethod]
    public async Task ApplicationSettings_WelcomeSmtpMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: "old SMTP greeting",
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty),
            WelcomeSmtpUpdateResult = true,
            GateWelcomeSmtpMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.WelcomeSMTP = "new SMTP greeting");
        await store.WelcomeSmtpMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.WelcomeSmtpMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual("new SMTP greeting", store.UpdatedWelcomeSmtp);
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
    public void AuthorizedSettings_WelcomeImapSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.WelcomeIMAP = "new IMAP greeting";

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.WelcomeImapUpdateCount);
        Assert.AreEqual("new IMAP greeting", settings.WelcomeIMAP);
    }

    [TestMethod]
    public void AuthorizedSettings_WelcomeImapSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WelcomeIMAP = "new IMAP greeting");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.WelcomeImapUpdateCount);
        Assert.AreEqual("old IMAP greeting", settings.WelcomeIMAP);
    }

    [TestMethod]
    public async Task ApplicationSettings_WelcomeImapMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: "old IMAP greeting"),
            WelcomeImapUpdateResult = true,
            GateWelcomeImapMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.WelcomeIMAP = "new IMAP greeting");
        await store.WelcomeImapMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.WelcomeImapMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual("new IMAP greeting", store.UpdatedWelcomeImap);
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
    public void AuthorizedSettings_WorkerThreadPrioritySetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.WorkerThreadPriority = 4;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.WorkerThreadPriorityUpdateCount);
        Assert.AreEqual(4, settings.WorkerThreadPriority);
    }

    [TestMethod]
    public void AuthorizedSettings_WorkerThreadPrioritySetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.WorkerThreadPriority = 4);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.WorkerThreadPriorityUpdateCount);
        Assert.AreEqual(1, settings.WorkerThreadPriority);
    }

    [TestMethod]
    public async Task ApplicationSettings_WorkerThreadPriorityMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                WorkerThreadPriority: 1),
            WorkerThreadPriorityUpdateResult = true,
            GateWorkerThreadPriorityMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.WorkerThreadPriority = 4);
        await store.WorkerThreadPriorityMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.WorkerThreadPriorityMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual(4, store.UpdatedWorkerThreadPriority);
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
    public void AuthorizedSettings_TcpIpThreadsSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.TCPIPThreads = 24;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.TcpIpThreadsUpdateCount);
        Assert.AreEqual(24, settings.TCPIPThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_TcpIpThreadsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.TCPIPThreads = 24);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.TcpIpThreadsUpdateCount);
        Assert.AreEqual(15, settings.TCPIPThreads);
    }

    [TestMethod]
    public async Task ApplicationSettings_TcpIpThreadsMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                TcpIpThreads: 15),
            TcpIpThreadsUpdateResult = true,
            GateTcpIpThreadsMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.TCPIPThreads = 24);
        await store.TcpIpThreadsMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.TcpIpThreadsMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual(24, store.UpdatedTcpIpThreads);
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
    public void AuthorizedSettings_SmtpMinutesBetweenTrySetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SMTPMinutesBetweenTry = 25;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpMinutesBetweenTryUpdateCount);
        Assert.AreEqual(25, settings.SMTPMinutesBetweenTry);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpMinutesBetweenTrySetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPMinutesBetweenTry = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpMinutesBetweenTryUpdateCount);
        Assert.AreEqual(60, settings.SMTPMinutesBetweenTry);
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
    public void AuthorizedSettings_SmtpNoOfTriesSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.SMTPNoOfTries = 8;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.SmtpNoOfTriesUpdateCount);
        Assert.AreEqual(8, settings.SMTPNoOfTries);
    }

    [TestMethod]
    public void AuthorizedSettings_SmtpNoOfTriesSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SMTPNoOfTries = 8);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.SmtpNoOfTriesUpdateCount);
        Assert.AreEqual(4, settings.SMTPNoOfTries);
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
    public void AuthorizedSettings_MaxSmtpConnectionsSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.MaxSMTPConnections = 25;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.MaxSmtpConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxSMTPConnections);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxSmtpConnectionsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxSMTPConnections = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxSmtpConnectionsUpdateCount);
        Assert.AreEqual(10, settings.MaxSMTPConnections);
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
    public void AuthorizedSettings_MaxPop3ConnectionsSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.MaxPOP3Connections = 25;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.MaxPop3ConnectionsUpdateCount);
        Assert.AreEqual(25, settings.MaxPOP3Connections);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxPop3ConnectionsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxPOP3Connections = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxPop3ConnectionsUpdateCount);
        Assert.AreEqual(10, settings.MaxPOP3Connections);
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
    public void AuthorizedSettings_MaxMessageSizeSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxMessageSize = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxMessageSizeUpdateCount);
        Assert.AreEqual(10, settings.MaxMessageSize);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxMessageSizeSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxMessageSizeUpdateResult = true,
            MaxMessageSizeMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxMessageSize: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.MaxMessageSize = 25;

        Assert.IsTrue(store.MaxMessageSizeLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
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
    public void AuthorizedSettings_MaxDeliveryThreadsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxDeliveryThreads = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxDeliveryThreadsUpdateCount);
        Assert.AreEqual(10, settings.MaxDeliveryThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxDeliveryThreadsSetterAcquiresLeaseThroughStoreUpdateAndDisposesOnSuccessAndFailure()
    {
        TrackingAuthorizationLease? activeLease = null;
        var leases = new List<TrackingAuthorizationLease>();
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxDeliveryThreadsUpdateResult = true,
            MaxDeliveryThreadsMutationProbe = () => activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxDeliveryThreads: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                leases.Add(activeLease);
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.MaxDeliveryThreads = 25;

        Assert.AreEqual(1, store.MaxDeliveryThreadsUpdateCount);
        Assert.IsTrue(store.MaxDeliveryThreadsLeaseHeldDuringUpdate);
        Assert.IsTrue(leases[0].Disposed);
        Assert.AreEqual(25, settings.MaxDeliveryThreads);

        store.MaxDeliveryThreadsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxDeliveryThreads = 30);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(2, store.MaxDeliveryThreadsUpdateCount);
        Assert.IsTrue(store.MaxDeliveryThreadsLeaseHeldDuringUpdate);
        Assert.IsTrue(leases[1].Disposed);
        Assert.AreEqual(25, settings.MaxDeliveryThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxAsynchronousThreadsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxAsynchronousThreadsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxAsynchronousThreads: 10),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxAsynchronousThreads = 25;

        Assert.AreEqual(1, store.MaxAsynchronousThreadsUpdateCount);
        Assert.AreEqual(25, store.UpdatedMaxAsynchronousThreads);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual(25, settings.MaxAsynchronousThreads);

        store.MaxAsynchronousThreadsUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxAsynchronousThreads = 30);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.MaxAsynchronousThreadsUpdateCount);
        Assert.AreEqual(25, settings.MaxAsynchronousThreads);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxAsynchronousThreads = 35);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.MaxAsynchronousThreadsUpdateCount);
        Assert.AreEqual(25, settings.MaxAsynchronousThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxAsynchronousThreadsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxAsynchronousThreadsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxAsynchronousThreads: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxAsynchronousThreads = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxAsynchronousThreadsUpdateCount);
        Assert.AreEqual(10, settings.MaxAsynchronousThreads);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxAsynchronousThreadsSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxAsynchronousThreadsUpdateResult = true,
            MaxAsynchronousThreadsMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxAsynchronousThreads: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.MaxAsynchronousThreads = 25;

        Assert.IsTrue(store.MaxAsynchronousThreadsLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.AreEqual(25, settings.MaxAsynchronousThreads);
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
    public void AuthorizedSettings_RuleLoopLimitSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.RuleLoopLimit = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.RuleLoopLimitUpdateCount);
        Assert.AreEqual(10, settings.RuleLoopLimit);
    }

    [TestMethod]
    public void AuthorizedSettings_RuleLoopLimitSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            RuleLoopLimitUpdateResult = true,
            RuleLoopLimitMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                RuleLoopLimit: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.RuleLoopLimit = 25;

        Assert.IsTrue(store.RuleLoopLimitLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
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
    public void AuthorizedSettings_MaxImapConnectionsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxIMAPConnections = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxImapConnectionsUpdateCount);
        Assert.AreEqual(10, settings.MaxIMAPConnections);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxImapConnectionsSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxImapConnectionsUpdateResult = true,
            MaxImapConnectionsMutationProbe = () => activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxImapConnections: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.MaxIMAPConnections = 25;

        Assert.IsTrue(store.MaxImapConnectionsLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
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
    public void AuthorizedSettings_MaxNumberOfMXHostsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfMXHosts = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxNumberOfMXHostsUpdateCount);
        Assert.AreEqual(10, settings.MaxNumberOfMXHosts);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxNumberOfMXHostsSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxNumberOfMXHostsUpdateResult = true,
            MaxNumberOfMXHostsMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxNumberOfMxHosts: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.MaxNumberOfMXHosts = 25;

        Assert.IsTrue(store.MaxNumberOfMXHostsLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.AreEqual(25, settings.MaxNumberOfMXHosts);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxNumberOfMXHostsSetterDisposesLeaseAndPreservesSnapshotWhenStoreUpdateFails()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxNumberOfMXHostsUpdateResult = false,
            MaxNumberOfMXHostsMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxNumberOfMxHosts: 10),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfMXHosts = 25);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.MaxNumberOfMXHostsUpdateCount);
        Assert.IsTrue(store.MaxNumberOfMXHostsLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.AreEqual(10, settings.MaxNumberOfMXHosts);
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
    public void AuthorizedSettings_VerifyRemoteSslCertificateSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.VerifyRemoteSslCertificate = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.VerifyRemoteSslCertificateUpdateCount);
        Assert.IsFalse(settings.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public void AuthorizedSettings_VerifyRemoteSslCertificateSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            VerifyRemoteSslCertificateUpdateResult = true,
            VerifyRemoteSslCertificateMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                VerifyRemoteSslCertificate: false),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.VerifyRemoteSslCertificate = true;

        Assert.IsTrue(store.VerifyRemoteSslCertificateLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.IsTrue(settings.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public void AuthorizedSettings_VerifyRemoteSslCertificateSetterDisposesLeaseAndPreservesSnapshotWhenStoreUpdateFails()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            VerifyRemoteSslCertificateUpdateResult = false,
            VerifyRemoteSslCertificateMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                VerifyRemoteSslCertificate: false),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.VerifyRemoteSslCertificate = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.VerifyRemoteSslCertificateUpdateCount);
        Assert.IsTrue(store.VerifyRemoteSslCertificateLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.IsFalse(settings.VerifyRemoteSslCertificate);
    }

    [TestMethod]
    public void AuthorizedSettings_Ipv6PreferredSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            Ipv6PreferredUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                Ipv6PreferredEnabled: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.IPv6PreferredEnabled = true;

        Assert.AreEqual(1, store.Ipv6PreferredUpdateCount);
        Assert.IsTrue(store.UpdatedIpv6Preferred);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.IPv6PreferredEnabled);

        store.Ipv6PreferredUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.IPv6PreferredEnabled = false);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.Ipv6PreferredUpdateCount);
        Assert.IsTrue(settings.IPv6PreferredEnabled);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.IPv6PreferredEnabled = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.Ipv6PreferredUpdateCount);
        Assert.IsTrue(settings.IPv6PreferredEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_SslCipherListSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            SslCipherListUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                SslCipherList: "OLD"),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.SslCipherList = "DEFAULT";

        Assert.AreEqual(1, store.SslCipherListUpdateCount);
        Assert.AreEqual("DEFAULT", store.UpdatedSslCipherList);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.AreEqual("DEFAULT", settings.SslCipherList);

        store.SslCipherListUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.SslCipherList = "FAILED");

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.SslCipherListUpdateCount);
        Assert.AreEqual("DEFAULT", settings.SslCipherList);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.SslCipherList = "DENIED");

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.SslCipherListUpdateCount);
        Assert.AreEqual("DEFAULT", settings.SslCipherList);
    }

    [TestMethod]
    public void AuthorizedSettings_AutoBanOnLogonFailureSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AutoBanOnLogonFailureUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AutoBanOnLogonFailure: false),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.AutoBanOnLogonFailure = true;

        Assert.AreEqual(1, store.AutoBanOnLogonFailureUpdateCount);
        Assert.IsTrue(store.UpdatedAutoBanOnLogonFailure);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);
        Assert.IsTrue(settings.AutoBanOnLogonFailure);

        store.AutoBanOnLogonFailureUpdateResult = false;
        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.AutoBanOnLogonFailure = false);

        Assert.AreEqual(unchecked((int)0x80004005), failed.ErrorCode);
        Assert.AreEqual(2, store.AutoBanOnLogonFailureUpdateCount);
        Assert.IsTrue(settings.AutoBanOnLogonFailure);

        isServerAdministrator = false;
        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AutoBanOnLogonFailure = false);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(2, store.AutoBanOnLogonFailureUpdateCount);
        Assert.IsTrue(settings.AutoBanOnLogonFailure);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxInvalidLogonAttemptsSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxInvalidLogonAttemptsUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxInvalidLogonAttempts: 3),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxInvalidLogonAttempts = 4;

        Assert.AreEqual(1, store.MaxInvalidLogonAttemptsUpdateCount);
        Assert.AreEqual(4, store.UpdatedMaxInvalidLogonAttempts);
        Assert.AreEqual(4, settings.MaxInvalidLogonAttempts);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);

        store.MaxInvalidLogonAttemptsUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.MaxInvalidLogonAttempts = 5).ErrorCode);
        Assert.AreEqual(4, settings.MaxInvalidLogonAttempts);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.MaxInvalidLogonAttempts = 6).ErrorCode);
        Assert.AreEqual(2, store.MaxInvalidLogonAttemptsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxInvalidLogonAttemptsWithinSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            MaxInvalidLogonAttemptsWithinUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxInvalidLogonAttemptsWithin: 30),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.MaxInvalidLogonAttemptsWithin = 45;

        Assert.AreEqual(1, store.MaxInvalidLogonAttemptsWithinUpdateCount);
        Assert.AreEqual(45, store.UpdatedMaxInvalidLogonAttemptsWithin);
        Assert.AreEqual(45, settings.MaxInvalidLogonAttemptsWithin);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);

        store.MaxInvalidLogonAttemptsWithinUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.MaxInvalidLogonAttemptsWithin = 60).ErrorCode);
        Assert.AreEqual(45, settings.MaxInvalidLogonAttemptsWithin);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.MaxInvalidLogonAttemptsWithin = 75).ErrorCode);
        Assert.AreEqual(2, store.MaxInvalidLogonAttemptsWithinUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AutoBanMinutesSetterPersistsBeforePublishingAndRechecksAdministrator()
    {
        var isServerAdministrator = true;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AutoBanMinutesUpdateResult = true
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AutoBanMinutes: 60),
            isServerAdministrator: () => isServerAdministrator,
            settingsMutationStore: store);

        settings.AutoBanMinutes = 120;

        Assert.AreEqual(1, store.AutoBanMinutesUpdateCount);
        Assert.AreEqual(120, store.UpdatedAutoBanMinutes);
        Assert.AreEqual(120, settings.AutoBanMinutes);
        Assert.IsFalse(store.CancellationToken.CanBeCanceled);

        store.AutoBanMinutesUpdateResult = false;
        Assert.AreEqual(
            EFail,
            Assert.ThrowsExactly<COMException>(() => settings.AutoBanMinutes = 180).ErrorCode);
        Assert.AreEqual(120, settings.AutoBanMinutes);

        isServerAdministrator = false;
        Assert.AreEqual(
            EAccessDenied,
            Assert.ThrowsExactly<COMException>(() => settings.AutoBanMinutes = 240).ErrorCode);
        Assert.AreEqual(2, store.AutoBanMinutesUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpfSettersPersistAndRefreshRetainedSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamUseSpfUpdateResult = true,
            AntiSpamUseSpfScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamUseSpf: false,
                AntiSpamUseSpfScore: 2)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.UseSPF = true;
        antiSpam.UseSPFScore = 7;

        Assert.AreEqual(1, store.AntiSpamUseSpfUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamUseSpf);
        Assert.AreEqual(1, store.AntiSpamUseSpfScoreUpdateCount);
        Assert.AreEqual(7, store.UpdatedAntiSpamUseSpfScore);
        Assert.IsTrue(antiSpam.UseSPF);
        Assert.AreEqual(7, antiSpam.UseSPFScore);
        Assert.IsTrue(settings.AntiSpam.UseSPF);
        Assert.AreEqual(7, settings.AntiSpam.UseSPFScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpfSettersFailClosedAndRecheckAdministrator()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamUseSpfUpdateResult = false,
            AntiSpamUseSpfScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        var isAdministrator = true;
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => isAdministrator);

        var failedSpf = Assert.ThrowsExactly<COMException>(() => settings.AntiSpam.UseSPF = true);
        var failedScore = Assert.ThrowsExactly<COMException>(() => settings.AntiSpam.UseSPFScore = 7);
        Assert.AreEqual(EFail, failedSpf.ErrorCode);
        Assert.AreEqual(EFail, failedScore.ErrorCode);
        Assert.IsFalse(settings.AntiSpam.UseSPF);
        Assert.AreEqual(0, settings.AntiSpam.UseSPFScore);

        isAdministrator = false;
        var deniedSpf = Assert.ThrowsExactly<COMException>(() => settings.AntiSpam.UseSPF = true);
        Assert.AreEqual(EAccessDenied, deniedSpf.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamUseSpfUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamMxChecksSettersPersistAndRefreshRetainedSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamUseMxChecksUpdateResult = true,
            AntiSpamUseMxChecksScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamUseMxChecks: false,
                AntiSpamUseMxChecksScore: 2)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.UseMXChecks = true;
        antiSpam.UseMXChecksScore = 8;

        Assert.AreEqual(1, store.AntiSpamUseMxChecksUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamUseMxChecks);
        Assert.AreEqual(1, store.AntiSpamUseMxChecksScoreUpdateCount);
        Assert.AreEqual(8, store.UpdatedAntiSpamUseMxChecksScore);
        Assert.IsTrue(antiSpam.UseMXChecks);
        Assert.AreEqual(8, antiSpam.UseMXChecksScore);
        Assert.IsTrue(settings.AntiSpam.UseMXChecks);
        Assert.AreEqual(8, settings.AntiSpam.UseMXChecksScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamMxChecksSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamUseMxChecksUpdateResult = false,
            AntiSpamUseMxChecksScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedMxChecks = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.UseMXChecks = true);
        var failedScore = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.UseMXChecksScore = 8);

        Assert.AreEqual(EFail, failedMxChecks.ErrorCode);
        Assert.AreEqual(EFail, failedScore.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamUseMxChecksUpdateCount);
        Assert.AreEqual(1, store.AntiSpamUseMxChecksScoreUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpamAssassinSettersPersistAndRefreshRetainedSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamAssassinEnabledUpdateResult = true,
            AntiSpamSpamAssassinScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamSpamAssassinEnabled: false,
                AntiSpamSpamAssassinScore: 2)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.SpamAssassinEnabled = true;
        antiSpam.SpamAssassinScore = 9;

        Assert.AreEqual(1, store.AntiSpamSpamAssassinEnabledUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamSpamAssassinEnabled);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinScoreUpdateCount);
        Assert.AreEqual(9, store.UpdatedAntiSpamSpamAssassinScore);
        Assert.IsTrue(antiSpam.SpamAssassinEnabled);
        Assert.AreEqual(9, antiSpam.SpamAssassinScore);
        Assert.IsTrue(settings.AntiSpam.SpamAssassinEnabled);
        Assert.AreEqual(9, settings.AntiSpam.SpamAssassinScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpamAssassinSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamAssassinEnabledUpdateResult = false,
            AntiSpamSpamAssassinScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedEnabled = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamAssassinEnabled = true);
        var failedScore = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamAssassinScore = 9);

        Assert.AreEqual(EFail, failedEnabled.ErrorCode);
        Assert.AreEqual(EFail, failedScore.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinEnabledUpdateCount);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinScoreUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpamAssassinMergeScoreSetterPersistsAndRefreshesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamAssassinMergeScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamSpamAssassinMergeScore: false)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.SpamAssassinMergeScore = true;

        Assert.AreEqual(1, store.AntiSpamSpamAssassinMergeScoreUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamSpamAssassinMergeScore);
        Assert.IsTrue(antiSpam.SpamAssassinMergeScore);
        Assert.IsTrue(settings.AntiSpam.SpamAssassinMergeScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpamAssassinMergeScoreSetterFailsClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamAssassinMergeScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamAssassinMergeScore = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinMergeScoreUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpamAssassinHostAndPortSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamAssassinHostUpdateResult = true,
            AntiSpamSpamAssassinPortUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamSpamAssassinHost: "127.0.0.1",
                AntiSpamSpamAssassinPort: 783)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.SpamAssassinHost = "scanner.example.test";
        antiSpam.SpamAssassinPort = 1783;

        Assert.AreEqual(1, store.AntiSpamSpamAssassinHostUpdateCount);
        Assert.AreEqual("scanner.example.test", store.UpdatedAntiSpamSpamAssassinHost);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinPortUpdateCount);
        Assert.AreEqual(1783, store.UpdatedAntiSpamSpamAssassinPort);
        Assert.AreEqual("scanner.example.test", antiSpam.SpamAssassinHost);
        Assert.AreEqual(1783, antiSpam.SpamAssassinPort);
        Assert.AreEqual("scanner.example.test", settings.AntiSpam.SpamAssassinHost);
        Assert.AreEqual(1783, settings.AntiSpam.SpamAssassinPort);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamSpamAssassinHostAndPortSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamAssassinHostUpdateResult = false,
            AntiSpamSpamAssassinPortUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedHost = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamAssassinHost = "scanner.example.test");
        var failedPort = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamAssassinPort = 1783);

        Assert.AreEqual(EFail, failedHost.ErrorCode);
        Assert.AreEqual(EFail, failedPort.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinHostUpdateCount);
        Assert.AreEqual(1, store.AntiSpamSpamAssassinPortUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamMaximumMessageSizeSetterPersistsAndRefreshesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamMaximumMessageSizeUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamMaximumMessageSize: 2048)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.MaximumMessageSize = 4096;

        Assert.AreEqual(1, store.AntiSpamMaximumMessageSizeUpdateCount);
        Assert.AreEqual(4096, store.UpdatedAntiSpamMaximumMessageSize);
        Assert.AreEqual(4096, antiSpam.MaximumMessageSize);
        Assert.AreEqual(4096, settings.AntiSpam.MaximumMessageSize);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamMaximumMessageSizeSetterFailsClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamMaximumMessageSizeUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failed = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.MaximumMessageSize = 4096);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamMaximumMessageSizeUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamDkimVerificationSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamDkimVerificationEnabledUpdateResult = true,
            AntiSpamDkimVerificationFailureScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamDkimVerificationEnabled: false,
                AntiSpamDkimVerificationFailureScore: 2)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.DKIMVerificationEnabled = true;
        antiSpam.DKIMVerificationFailureScore = 9;

        Assert.AreEqual(1, store.AntiSpamDkimVerificationEnabledUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamDkimVerificationEnabled);
        Assert.AreEqual(1, store.AntiSpamDkimVerificationFailureScoreUpdateCount);
        Assert.AreEqual(9, store.UpdatedAntiSpamDkimVerificationFailureScore);
        Assert.IsTrue(antiSpam.DKIMVerificationEnabled);
        Assert.AreEqual(9, antiSpam.DKIMVerificationFailureScore);
        Assert.IsTrue(settings.AntiSpam.DKIMVerificationEnabled);
        Assert.AreEqual(9, settings.AntiSpam.DKIMVerificationFailureScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamDkimVerificationSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamDkimVerificationEnabledUpdateResult = false,
            AntiSpamDkimVerificationFailureScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedEnabled = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.DKIMVerificationEnabled = true);
        var failedScore = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.DKIMVerificationFailureScore = 9);

        Assert.AreEqual(EFail, failedEnabled.ErrorCode);
        Assert.AreEqual(EFail, failedScore.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamDkimVerificationEnabledUpdateCount);
        Assert.AreEqual(1, store.AntiSpamDkimVerificationFailureScoreUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamBypassGreylistingSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamBypassGreylistingOnSpfSuccessUpdateResult = true,
            AntiSpamBypassGreylistingOnMailFromMxUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamBypassGreylistingOnSpfSuccess: false,
                AntiSpamBypassGreylistingOnMailFromMx: true)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.BypassGreylistingOnSPFSuccess = true;
        antiSpam.BypassGreylistingOnMailFromMX = false;

        Assert.AreEqual(1, store.AntiSpamBypassGreylistingOnSpfSuccessUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamBypassGreylistingOnSpfSuccess);
        Assert.AreEqual(1, store.AntiSpamBypassGreylistingOnMailFromMxUpdateCount);
        Assert.IsFalse(store.UpdatedAntiSpamBypassGreylistingOnMailFromMx);
        Assert.IsTrue(antiSpam.BypassGreylistingOnSPFSuccess);
        Assert.IsFalse(antiSpam.BypassGreylistingOnMailFromMX);
        Assert.IsTrue(settings.AntiSpam.BypassGreylistingOnSPFSuccess);
        Assert.IsFalse(settings.AntiSpam.BypassGreylistingOnMailFromMX);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamBypassGreylistingSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamBypassGreylistingOnSpfSuccessUpdateResult = false,
            AntiSpamBypassGreylistingOnMailFromMxUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedSpf = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.BypassGreylistingOnSPFSuccess = true);
        var failedMx = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.BypassGreylistingOnMailFromMX = true);

        Assert.AreEqual(EFail, failedSpf.ErrorCode);
        Assert.AreEqual(EFail, failedMx.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamBypassGreylistingOnSpfSuccessUpdateCount);
        Assert.AreEqual(1, store.AntiSpamBypassGreylistingOnMailFromMxUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamCheckHostInHeloSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamCheckHostInHeloUpdateResult = true,
            AntiSpamCheckHostInHeloScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamCheckHostInHelo: false,
                AntiSpamCheckHostInHeloScore: 2)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);
        var antiSpam = settings.AntiSpam;

        antiSpam.CheckHostInHelo = true;
        antiSpam.CheckHostInHeloScore = 7;

        Assert.AreEqual(1, store.AntiSpamCheckHostInHeloUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamCheckHostInHelo);
        Assert.AreEqual(1, store.AntiSpamCheckHostInHeloScoreUpdateCount);
        Assert.AreEqual(7, store.UpdatedAntiSpamCheckHostInHeloScore);
        Assert.IsTrue(antiSpam.CheckHostInHelo);
        Assert.AreEqual(7, antiSpam.CheckHostInHeloScore);
        Assert.IsTrue(settings.AntiSpam.CheckHostInHelo);
        Assert.AreEqual(7, settings.AntiSpam.CheckHostInHeloScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamCheckHostInHeloSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamCheckHostInHeloUpdateResult = false,
            AntiSpamCheckHostInHeloScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedEnabled = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.CheckHostInHelo = true);
        var failedScore = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.CheckHostInHeloScore = 7);

        Assert.AreEqual(EFail, failedEnabled.ErrorCode);
        Assert.AreEqual(EFail, failedScore.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamCheckHostInHeloUpdateCount);
        Assert.AreEqual(1, store.AntiSpamCheckHostInHeloScoreUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamAddHeaderSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamAddHeaderSpamUpdateResult = true,
            AntiSpamAddHeaderReasonUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamAddHeaderSpam: false,
                AntiSpamAddHeaderReason: false)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.AddHeaderSpam = true;
        settings.AntiSpam.AddHeaderReason = true;

        Assert.AreEqual(1, store.AntiSpamAddHeaderSpamUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamAddHeaderSpam);
        Assert.AreEqual(1, store.AntiSpamAddHeaderReasonUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamAddHeaderReason);
        Assert.IsTrue(settings.AntiSpam.AddHeaderSpam);
        Assert.IsTrue(settings.AntiSpam.AddHeaderReason);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamAddHeaderSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamAddHeaderSpamUpdateResult = false,
            AntiSpamAddHeaderReasonUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedSpam = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.AddHeaderSpam = true);
        var failedReason = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.AddHeaderReason = true);

        Assert.AreEqual(EFail, failedSpam.ErrorCode);
        Assert.AreEqual(EFail, failedReason.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamAddHeaderSpamUpdateCount);
        Assert.AreEqual(1, store.AntiSpamAddHeaderReasonUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamPrependSubjectSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamPrependSubjectUpdateResult = true,
            AntiSpamPrependSubjectTextUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamPrependSubject: false,
                AntiSpamPrependSubjectText: "[old]")
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.PrependSubject = true;
        settings.AntiSpam.PrependSubjectText = "[spam]";

        Assert.AreEqual(1, store.AntiSpamPrependSubjectUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamPrependSubject);
        Assert.AreEqual(1, store.AntiSpamPrependSubjectTextUpdateCount);
        Assert.AreEqual("[spam]", store.UpdatedAntiSpamPrependSubjectText);
        Assert.IsTrue(settings.AntiSpam.PrependSubject);
        Assert.AreEqual("[spam]", settings.AntiSpam.PrependSubjectText);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamPrependSubjectSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamPrependSubjectUpdateResult = false,
            AntiSpamPrependSubjectTextUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedEnabled = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.PrependSubject = true);
        var failedText = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.PrependSubjectText = "[spam]");

        Assert.AreEqual(EFail, failedEnabled.ErrorCode);
        Assert.AreEqual(EFail, failedText.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamPrependSubjectUpdateCount);
        Assert.AreEqual(1, store.AntiSpamPrependSubjectTextUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupDestinationSetterPersistsAndRefreshesSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupDestinationUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupDestination: @"D:\Backups")
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.Backup.Destination = @"E:\Other";

        Assert.AreEqual(1, store.BackupDestinationUpdateCount);
        Assert.AreEqual(@"E:\Other", store.UpdatedBackupDestination);
        Assert.AreEqual(@"E:\Other", settings.Backup.Destination);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupDestinationSetterFailsClosedWithoutSnapshotChange()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupDestinationUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupDestination: @"D:\Backups")
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var error = Assert.ThrowsExactly<COMException>(
            () => settings.Backup.Destination = @"E:\Other");

        Assert.AreEqual(EFail, error.ErrorCode);
        Assert.AreEqual(1, store.BackupDestinationUpdateCount);
        Assert.AreEqual(@"D:\Backups", settings.Backup.Destination);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupSettingsSetterPersistsTransitionsAndPreservesOtherBits()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupSettingsUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 14)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        settings.Backup.BackupSettings = true;
        Assert.IsTrue(settings.Backup.BackupSettings);
        Assert.IsTrue(settings.Backup.BackupDomains);
        Assert.IsTrue(settings.Backup.BackupMessages);
        Assert.IsTrue(settings.Backup.CompressDestinationFiles);

        settings.Backup.BackupSettings = false;
        Assert.IsFalse(settings.Backup.BackupSettings);
        Assert.AreEqual(2, store.BackupSettingsUpdateCount);
        CollectionAssert.AreEqual(new[] { true, false }, store.UpdatedBackupSettings);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupSettingsSetterFailureAndExpiredLeaseDoNotPublishOrStore()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupSettingsUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 14)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        var failed = Assert.ThrowsExactly<COMException>(() => settings.Backup.BackupSettings = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.BackupSettingsUpdateCount);
        Assert.IsFalse(settings.Backup.BackupSettings);

        settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(() => settings.Backup.BackupSettings = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, store.BackupSettingsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupDomainsSetterPersistsTransitionsAndPreservesOtherBits()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupDomainsUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 13)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        settings.Backup.BackupDomains = true;
        Assert.IsTrue(settings.Backup.BackupDomains);
        Assert.IsTrue(settings.Backup.BackupSettings);
        Assert.IsTrue(settings.Backup.BackupMessages);
        Assert.IsTrue(settings.Backup.CompressDestinationFiles);

        settings.Backup.BackupDomains = false;
        Assert.IsFalse(settings.Backup.BackupDomains);
        Assert.AreEqual(2, store.BackupDomainsUpdateCount);
        CollectionAssert.AreEqual(new[] { true, false }, store.UpdatedBackupDomains);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupDomainsSetterFailureAndExpiredLeaseDoNotPublishOrStore()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupDomainsUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 13)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        var failed = Assert.ThrowsExactly<COMException>(() => settings.Backup.BackupDomains = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.BackupDomainsUpdateCount);
        Assert.IsFalse(settings.Backup.BackupDomains);

        settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(() => settings.Backup.BackupDomains = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, store.BackupDomainsUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupMessagesSetterPersistsTransitionsAndPreservesOtherBits()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupMessagesUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 11)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        settings.Backup.BackupMessages = true;
        Assert.IsTrue(settings.Backup.BackupMessages);
        Assert.IsTrue(settings.Backup.BackupSettings);
        Assert.IsTrue(settings.Backup.BackupDomains);
        Assert.IsTrue(settings.Backup.CompressDestinationFiles);

        settings.Backup.BackupMessages = false;
        Assert.IsFalse(settings.Backup.BackupMessages);
        Assert.AreEqual(2, store.BackupMessagesUpdateCount);
        CollectionAssert.AreEqual(new[] { true, false }, store.UpdatedBackupMessages);
    }

    [TestMethod]
    public void AuthorizedSettings_BackupMessagesSetterFailureExpiredLeaseAndRetainedChildPreserveSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupMessagesUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 11)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        var failed = Assert.ThrowsExactly<COMException>(() => settings.Backup.BackupMessages = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.BackupMessagesUpdateCount);
        Assert.IsFalse(settings.Backup.BackupMessages);

        settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(() => settings.Backup.BackupMessages = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, store.BackupMessagesUpdateCount);

        store.BackupMessagesUpdateResult = true;
        store.BackupSettingsUpdateResult = true;
        settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 14),
            settingsMutationStore: store,
            isServerAdministrator: static () => true);
        var settingsChild = settings.Backup;
        var messagesChild = settings.Backup;

        settingsChild.BackupSettings = true;
        messagesChild.BackupMessages = false;

        Assert.IsTrue(settings.Backup.BackupSettings);
        Assert.IsFalse(settings.Backup.BackupMessages);
        Assert.IsTrue(settings.Backup.BackupDomains);
        Assert.IsTrue(settings.Backup.CompressDestinationFiles);
    }

    [TestMethod]
    public void AuthorizedSettings_CompressionSetterPersistsTransitionsAndPreservesOtherBits()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupCompressionUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 15)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        settings.Backup.CompressDestinationFiles = false;
        Assert.IsFalse(settings.Backup.CompressDestinationFiles);
        Assert.IsTrue(settings.Backup.BackupSettings);
        Assert.IsTrue(settings.Backup.BackupDomains);
        Assert.IsTrue(settings.Backup.BackupMessages);

        settings.Backup.CompressDestinationFiles = true;
        Assert.IsTrue(settings.Backup.CompressDestinationFiles);
        Assert.AreEqual(2, store.BackupCompressionUpdateCount);
        CollectionAssert.AreEqual(new[] { false, true }, store.UpdatedBackupCompression);
    }

    [TestMethod]
    public void AuthorizedSettings_CompressionSetterFailureAndExpiredLeaseDoNotPublishOrStore()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            BackupCompressionUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                BackupOptions: 7)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true);

        var failed = Assert.ThrowsExactly<COMException>(() => settings.Backup.CompressDestinationFiles = true);

        Assert.AreEqual(EFail, failed.ErrorCode);
        Assert.AreEqual(1, store.BackupCompressionUpdateCount);
        Assert.IsFalse(settings.Backup.CompressDestinationFiles);

        settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(() => settings.Backup.CompressDestinationFiles = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(1, store.BackupCompressionUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamThresholdSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamMarkThresholdUpdateResult = true,
            AntiSpamSpamDeleteThresholdUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamSpamMarkThreshold: 5,
                AntiSpamSpamDeleteThreshold: 20)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.SpamMarkThreshold = 7;
        settings.AntiSpam.SpamDeleteThreshold = 21;

        Assert.AreEqual(1, store.AntiSpamSpamMarkThresholdUpdateCount);
        Assert.AreEqual(7, store.UpdatedAntiSpamSpamMarkThreshold);
        Assert.AreEqual(1, store.AntiSpamSpamDeleteThresholdUpdateCount);
        Assert.AreEqual(21, store.UpdatedAntiSpamSpamDeleteThreshold);
        Assert.AreEqual(7, settings.AntiSpam.SpamMarkThreshold);
        Assert.AreEqual(21, settings.AntiSpam.SpamDeleteThreshold);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamThresholdSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamSpamMarkThresholdUpdateResult = false,
            AntiSpamSpamDeleteThresholdUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedMark = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamMarkThreshold = 7);
        var failedDelete = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.SpamDeleteThreshold = 21);

        Assert.AreEqual(EFail, failedMark.ErrorCode);
        Assert.AreEqual(EFail, failedDelete.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamSpamMarkThresholdUpdateCount);
        Assert.AreEqual(1, store.AntiSpamSpamDeleteThresholdUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamCheckPtrSettersPersistAndRefreshSnapshot()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamCheckPtrUpdateResult = true,
            AntiSpamCheckPtrScoreUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamCheckPtr: false,
                AntiSpamCheckPtrScore: 1)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.CheckPTR = true;
        settings.AntiSpam.CheckPTRScore = 7;

        Assert.AreEqual(1, store.AntiSpamCheckPtrUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamCheckPtr);
        Assert.AreEqual(1, store.AntiSpamCheckPtrScoreUpdateCount);
        Assert.AreEqual(7, store.UpdatedAntiSpamCheckPtrScore);
        Assert.IsTrue(settings.AntiSpam.CheckPTR);
        Assert.AreEqual(7, settings.AntiSpam.CheckPTRScore);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamCheckPtrSettersFailClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamCheckPtrUpdateResult = false,
            AntiSpamCheckPtrScoreUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failedCheck = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.CheckPTR = true);
        var failedScore = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.CheckPTRScore = 7);

        Assert.AreEqual(EFail, failedCheck.ErrorCode);
        Assert.AreEqual(EFail, failedScore.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamCheckPtrUpdateCount);
        Assert.AreEqual(1, store.AntiSpamCheckPtrScoreUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingEnabledPersistsRefreshesAndPublishesRuntimeState()
    {
        var published = new List<bool>();
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingEnabledUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingEnabled: false)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                GreyListingEnabledPublisher: published.Add),
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.GreyListingEnabled = true;

        Assert.AreEqual(1, store.AntiSpamGreyListingEnabledUpdateCount);
        Assert.IsTrue(store.UpdatedAntiSpamGreyListingEnabled);
        Assert.IsTrue(settings.AntiSpam.GreyListingEnabled);
        CollectionAssert.AreEqual(new[] { true }, published);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingEnabledFailsClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingEnabledUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failure = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingEnabled = true);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamGreyListingEnabledUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingEnabledUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingEnabledUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingEnabled: false)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingEnabled = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AntiSpamGreyListingEnabledUpdateCount);
        Assert.IsFalse(settings.AntiSpam.GreyListingEnabled);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingInitialDelayPersistsRefreshesAndPublishesRuntimeState()
    {
        var published = new List<int>();
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingInitialDelayUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingInitialDelay: 30)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                GreyListingInitialDelayPublisher: published.Add),
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.GreyListingInitialDelay = 10;

        Assert.AreEqual(1, store.AntiSpamGreyListingInitialDelayUpdateCount);
        Assert.AreEqual(10, store.UpdatedAntiSpamGreyListingInitialDelay);
        Assert.AreEqual(10, settings.AntiSpam.GreyListingInitialDelay);
        CollectionAssert.AreEqual(new[] { 10 }, published);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingInitialDelayFailsClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingInitialDelayUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingInitialDelay: 30)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failure = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingInitialDelay = 10);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamGreyListingInitialDelayUpdateCount);
        Assert.AreEqual(30, settings.AntiSpam.GreyListingInitialDelay);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingInitialDelayUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingInitialDelayUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingInitialDelay: 30)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingInitialDelay = 10);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AntiSpamGreyListingInitialDelayUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingInitialDeletePersistsRefreshesAndPublishesRuntimeState()
    {
        var published = new List<int>();
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingInitialDeleteUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingInitialDelete: 24)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                GreyListingInitialDeletePublisher: published.Add),
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.GreyListingInitialDelete = 10;

        Assert.AreEqual(1, store.AntiSpamGreyListingInitialDeleteUpdateCount);
        Assert.AreEqual(10, store.UpdatedAntiSpamGreyListingInitialDelete);
        Assert.AreEqual(10, settings.AntiSpam.GreyListingInitialDelete);
        CollectionAssert.AreEqual(new[] { 10 }, published);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingInitialDeleteFailsClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingInitialDeleteUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingInitialDelete: 24)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failure = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingInitialDelete = 10);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamGreyListingInitialDeleteUpdateCount);
        Assert.AreEqual(24, settings.AntiSpam.GreyListingInitialDelete);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingInitialDeleteUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingInitialDeleteUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingInitialDelete: 24)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingInitialDelete = 10);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AntiSpamGreyListingInitialDeleteUpdateCount);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingFinalDeletePersistsRefreshesAndPublishesRuntimeState()
    {
        var published = new List<int>();
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingFinalDeleteUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingFinalDelete: 864)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            runtimeConfiguration: new SettingsRuntimeConfiguration(
                GreyListingFinalDeletePublisher: published.Add),
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        settings.AntiSpam.GreyListingFinalDelete = 720;

        Assert.AreEqual(1, store.AntiSpamGreyListingFinalDeleteUpdateCount);
        Assert.AreEqual(720, store.UpdatedAntiSpamGreyListingFinalDelete);
        Assert.AreEqual(720, settings.AntiSpam.GreyListingFinalDelete);
        CollectionAssert.AreEqual(new[] { 720 }, published);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingFinalDeleteFailsClosed()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingFinalDeleteUpdateResult = false,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingFinalDelete: 864)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: () => true);

        var failure = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingFinalDelete = 720);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(1, store.AntiSpamGreyListingFinalDeleteUpdateCount);
        Assert.AreEqual(864, settings.AntiSpam.GreyListingFinalDelete);
    }

    [TestMethod]
    public void AuthorizedSettings_AntiSpamGreyListingFinalDeleteUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            AntiSpamGreyListingFinalDeleteUpdateResult = true,
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: "mail.example.test",
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AntiSpamGreyListingFinalDelete: 864)
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            store.Snapshot,
            settingsMutationStore: store,
            isServerAdministrator: static () => true,
            authorizationLeaseFactory: static _ => ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AntiSpam.GreyListingFinalDelete = 720);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AntiSpamGreyListingFinalDeleteUpdateCount);
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
    public void AuthorizedSettings_MaxSmtpRecipientsInBatchSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
                MaxSmtpRecipientsInBatch: 100),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.MaxSMTPRecipientsInBatch = 25;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.MaxSmtpRecipientsInBatchUpdateCount);
        Assert.AreEqual(25, settings.MaxSMTPRecipientsInBatch);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxSmtpRecipientsInBatchSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
                MaxSmtpRecipientsInBatch: 100),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxSMTPRecipientsInBatch = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxSmtpRecipientsInBatchUpdateCount);
        Assert.AreEqual(100, settings.MaxSMTPRecipientsInBatch);
    }

    [TestMethod]
    public async Task ApplicationSettings_MaxSmtpRecipientsInBatchMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxSmtpRecipientsInBatch: 100),
            MaxSmtpRecipientsInBatchUpdateResult = true,
            GateMaxSmtpRecipientsInBatchMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.MaxSMTPRecipientsInBatch = 25);
        await store.MaxSmtpRecipientsInBatchMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.MaxSmtpRecipientsInBatchMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual(25, store.UpdatedMaxSmtpRecipientsInBatch);
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
    public void AuthorizedSettings_MaxNumberOfInvalidCommandsSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
                MaxNumberOfInvalidCommands: 100),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.MaxNumberOfInvalidCommands = 25;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.MaxNumberOfInvalidCommandsUpdateCount);
        Assert.AreEqual(25, settings.MaxNumberOfInvalidCommands);
    }

    [TestMethod]
    public void AuthorizedSettings_MaxNumberOfInvalidCommandsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
                MaxNumberOfInvalidCommands: 100),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.MaxNumberOfInvalidCommands = 25);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.MaxNumberOfInvalidCommandsUpdateCount);
        Assert.AreEqual(100, settings.MaxNumberOfInvalidCommands);
    }

    [TestMethod]
    public async Task ApplicationSettings_MaxNumberOfInvalidCommandsMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                MaxNumberOfInvalidCommands: 100),
            MaxNumberOfInvalidCommandsUpdateResult = true,
            GateMaxNumberOfInvalidCommandsMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.MaxNumberOfInvalidCommands = 25);
        await store.MaxNumberOfInvalidCommandsMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.MaxNumberOfInvalidCommandsMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.AreEqual(25, store.UpdatedMaxNumberOfInvalidCommands);
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
    public void AuthorizedSettings_DisconnectInvalidClientsSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.DisconnectInvalidClients = true;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.DisconnectInvalidClientsUpdateCount);
        Assert.IsTrue(settings.DisconnectInvalidClients);
    }

    [TestMethod]
    public void AuthorizedSettings_DisconnectInvalidClientsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.DisconnectInvalidClients = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.DisconnectInvalidClientsUpdateCount);
        Assert.IsFalse(settings.DisconnectInvalidClients);
    }

    [TestMethod]
    public async Task ApplicationSettings_DisconnectInvalidClientsMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                DisconnectInvalidClients: false),
            DisconnectInvalidClientsUpdateResult = true,
            GateDisconnectInvalidClientsMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.DisconnectInvalidClients = true);
        await store.DisconnectInvalidClientsMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.DisconnectInvalidClientsMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.IsTrue(store.UpdatedDisconnectInvalidClients);
        Assert.IsTrue(settings.DisconnectInvalidClients);
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
    public void AuthorizedSettings_AddDeliveredToHeaderSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: static _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AddDeliveredToHeader = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AddDeliveredToHeaderUpdateCount);
        Assert.IsFalse(settings.AddDeliveredToHeader);
    }

    [TestMethod]
    public void AuthorizedSettings_AddDeliveredToHeaderSetterHoldsAuthorizationLeaseDuringStoreUpdate()
    {
        TrackingAuthorizationLease? activeLease = null;
        var store = new FakeSettingsAdministrationMutationStore
        {
            AddDeliveredToHeaderUpdateResult = true,
            AddDeliveredToHeaderMutationProbe = () =>
                activeLease is not null && !activeLease.Disposed
        };
        IInterfaceSettings settings = Settings.CreateAuthorized(
            new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AddDeliveredToHeader: false),
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                activeLease = new TrackingAuthorizationLease();
                return ValueTask.FromResult<IDisposable?>(activeLease);
            });

        settings.AddDeliveredToHeader = true;

        Assert.IsTrue(store.AddDeliveredToHeaderLeaseHeldDuringUpdate);
        Assert.IsTrue(activeLease!.Disposed);
        Assert.IsTrue(settings.AddDeliveredToHeader);
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
    public void AuthorizedSettings_AllowIncorrectLineEndingsSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.AllowIncorrectLineEndings = true;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.AllowIncorrectLineEndingsUpdateCount);
        Assert.IsTrue(settings.AllowIncorrectLineEndings);
    }

    [TestMethod]
    public void AuthorizedSettings_AllowIncorrectLineEndingsSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AllowIncorrectLineEndings = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AllowIncorrectLineEndingsUpdateCount);
        Assert.IsFalse(settings.AllowIncorrectLineEndings);
    }

    [TestMethod]
    public async Task ApplicationSettings_AllowIncorrectLineEndingsMutationLeaseBlocksReauthenticationUntilMutationCompletes()
    {
        var store = new FakeSettingsAdministrationMutationStore
        {
            Snapshot = new SettingsAdministrationSnapshot(
                HostName: string.Empty,
                WelcomeSmtp: string.Empty,
                WelcomePop3: string.Empty,
                WelcomeImap: string.Empty,
                AllowIncorrectLineEndings: false),
            AllowIncorrectLineEndingsUpdateResult = true,
            GateAllowIncorrectLineEndingsMutation = true
        };
        SettingsAdministrationRuntimeHost.Configure(store);
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var settings = application.Settings;

        var mutation = Task.Run(() => settings.AllowIncorrectLineEndings = true);
        await store.AllowIncorrectLineEndingsMutationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reauthentication = Task.Run(() => application.Authenticate("Administrator", "wrong"));
        await Task.Delay(100);
        Assert.IsFalse(reauthentication.IsCompleted);

        store.AllowIncorrectLineEndingsMutationRelease.TrySetResult(true);
        await mutation;
        Assert.IsNull(await reauthentication);
        Assert.IsTrue(store.UpdatedAllowIncorrectLineEndings);
        Assert.IsTrue(settings.AllowIncorrectLineEndings);
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
    public void AuthorizedSettings_AllowSmtpAuthPlainSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.AllowSMTPAuthPlain = true;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.AllowSmtpAuthPlainUpdateCount);
        Assert.IsTrue(settings.AllowSMTPAuthPlain);
    }

    [TestMethod]
    public void AuthorizedSettings_AllowSmtpAuthPlainSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.AllowSMTPAuthPlain = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AllowSmtpAuthPlainUpdateCount);
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
    public void AuthorizedSettings_DenyMailFromNullSetterAcquiresAndDisposesAuthorizationLease()
    {
        var lease = new TrackingAuthorizationLease();
        var leaseAcquireCount = 0;
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
            {
                leaseAcquireCount++;
                return ValueTask.FromResult<IDisposable?>(lease);
            });

        settings.DenyMailFromNull = true;

        Assert.AreEqual(1, leaseAcquireCount);
        Assert.IsTrue(lease.Disposed);
        Assert.AreEqual(1, store.AllowMailFromNullUpdateCount);
        Assert.IsFalse(store.UpdatedAllowMailFromNull);
        Assert.IsTrue(settings.DenyMailFromNull);
    }

    [TestMethod]
    public void AuthorizedSettings_DenyMailFromNullSetterUnavailableAuthorizationLeaseFailsBeforeMutation()
    {
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
            isServerAdministrator: static () => true,
            settingsMutationStore: store,
            authorizationLeaseFactory: _ =>
                ValueTask.FromResult<IDisposable?>(null));

        var denied = Assert.ThrowsExactly<COMException>(
            () => settings.DenyMailFromNull = true);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, store.AllowMailFromNullUpdateCount);
        Assert.IsFalse(store.UpdatedAllowMailFromNull);
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

        public int AntiVirusClamWinEnabledUpdateCount { get; private set; }

        public bool UpdatedAntiVirusClamWinEnabled { get; private set; }

        public int AntiVirusClamWinExecutableUpdateCount { get; private set; }

        public string? UpdatedAntiVirusClamWinExecutable { get; private set; }

        public int AntiVirusClamWinDatabaseUpdateCount { get; private set; }

        public string? UpdatedAntiVirusClamWinDatabase { get; private set; }

        public int AntiVirusActionUpdateCount { get; private set; }

        public int UpdatedAntiVirusAction { get; private set; }

        public int AntiVirusNotifyReceiverUpdateCount { get; private set; }

        public bool UpdatedAntiVirusNotifyReceiver { get; private set; }

        public int AntiVirusNotifySenderUpdateCount { get; private set; }

        public bool UpdatedAntiVirusNotifySender { get; private set; }

        public Func<bool>? AntiVirusClamWinEnabledMutationProbe { get; set; }

        public bool AntiVirusClamWinEnabledLeaseHeldDuringUpdate { get; private set; }

        public Func<bool>? DefaultDomainMutationProbe { get; set; }

        public bool DefaultDomainLeaseHeldDuringUpdate { get; private set; }

        public bool BackupDestinationUpdateResult { get; set; }

        public bool BackupSettingsUpdateResult { get; set; }

        public bool BackupDomainsUpdateResult { get; set; }

        public bool BackupMessagesUpdateResult { get; set; }

        public bool BackupCompressionUpdateResult { get; set; }

        public int BackupDestinationUpdateCount { get; private set; }

        public int BackupSettingsUpdateCount { get; private set; }

        public int BackupDomainsUpdateCount { get; private set; }

        public int BackupMessagesUpdateCount { get; private set; }

        public int BackupCompressionUpdateCount { get; private set; }

        public string UpdatedBackupDestination { get; private set; } = string.Empty;

        public List<bool> UpdatedBackupSettings { get; } = [];

        public List<bool> UpdatedBackupDomains { get; } = [];

        public List<bool> UpdatedBackupMessages { get; } = [];

        public List<bool> UpdatedBackupCompression { get; } = [];

        public bool MirrorUpdateResult { get; set; }

        public bool SmtpRelayerRequiresAuthenticationUpdateResult { get; set; }

        public bool SmtpRelayerUpdateResult { get; set; }

        public bool SmtpRelayerUsernameUpdateResult { get; set; }

        public bool SmtpRelayerPasswordUpdateResult { get; set; }

        public bool SmtpRelayerPortUpdateResult { get; set; }

        public bool GateSmtpRelayerPortMutation { get; set; }

        public TaskCompletionSource<bool> SmtpRelayerPortMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SmtpRelayerPortMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool SmtpRelayerConnectionSecurityUpdateResult { get; set; }

        public bool SmtpConnectionSecurityUpdateResult { get; set; }

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

        public int SmtpRelayerPasswordUpdateCount { get; private set; }

        public string? UpdatedSmtpRelayerPassword { get; private set; }

        public int SmtpRelayerPortUpdateCount { get; private set; }

        public int UpdatedSmtpRelayerPort { get; private set; }

        public int SmtpRelayerConnectionSecurityUpdateCount { get; private set; }

        public int UpdatedSmtpRelayerConnectionSecurity { get; private set; }

        public int SmtpConnectionSecurityUpdateCount { get; private set; }

        public int UpdatedSmtpConnectionSecurity { get; private set; }

        public int UpdateCount { get; private set; }

        public string? UpdatedDefaultDomain { get; private set; }

        public int MirrorUpdateCount { get; private set; }

        public string? UpdatedMirrorEmailAddress { get; private set; }

        public bool WelcomePop3UpdateResult { get; set; }

        public bool GateWelcomePop3Mutation { get; set; }

        public TaskCompletionSource<bool> WelcomePop3MutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WelcomePop3MutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WelcomePop3UpdateCount { get; private set; }

        public string? UpdatedWelcomePop3 { get; private set; }

        public bool WelcomeSmtpUpdateResult { get; set; }

        public bool GateWelcomeSmtpMutation { get; set; }

        public TaskCompletionSource<bool> WelcomeSmtpMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WelcomeSmtpMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WelcomeSmtpUpdateCount { get; private set; }

        public string? UpdatedWelcomeSmtp { get; private set; }

        public bool WelcomeImapUpdateResult { get; set; }

        public bool GateWelcomeImapMutation { get; set; }

        public TaskCompletionSource<bool> WelcomeImapMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WelcomeImapMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WelcomeImapUpdateCount { get; private set; }

        public string? UpdatedWelcomeImap { get; private set; }

        public bool ServiceSmtpUpdateResult { get; set; }

        public int ServiceSmtpUpdateCount { get; private set; }

        public bool UpdatedServiceSmtp { get; private set; }

        public bool ServicePop3UpdateResult { get; set; }

        public int ServicePop3UpdateCount { get; private set; }

        public bool UpdatedServicePop3 { get; private set; }

        public bool ServiceImapUpdateResult { get; set; }

        public int ServiceImapUpdateCount { get; private set; }

        public bool UpdatedServiceImap { get; private set; }

        public bool SmtpDeliveryBindToIpUpdateResult { get; set; }

        public int SmtpDeliveryBindToIpUpdateCount { get; private set; }

        public string? UpdatedSmtpDeliveryBindToIp { get; private set; }

        public bool ImapSortEnabledUpdateResult { get; set; }

        public int ImapSortEnabledUpdateCount { get; private set; }

        public bool UpdatedImapSortEnabled { get; private set; }

        public bool ImapQuotaEnabledUpdateResult { get; set; }

        public int ImapQuotaEnabledUpdateCount { get; private set; }

        public bool UpdatedImapQuotaEnabled { get; private set; }

        public bool ImapIdleEnabledUpdateResult { get; set; }

        public int ImapIdleEnabledUpdateCount { get; private set; }

        public bool UpdatedImapIdleEnabled { get; private set; }

        public bool ImapAclEnabledUpdateResult { get; set; }

        public int ImapAclEnabledUpdateCount { get; private set; }

        public bool UpdatedImapAclEnabled { get; private set; }

        public bool ImapSaslPlainEnabledUpdateResult { get; set; }

        public int ImapSaslPlainEnabledUpdateCount { get; private set; }

        public bool UpdatedImapSaslPlainEnabled { get; private set; }

        public bool ImapSaslInitialResponseEnabledUpdateResult { get; set; }

        public int ImapSaslInitialResponseEnabledUpdateCount { get; private set; }

        public bool UpdatedImapSaslInitialResponseEnabled { get; private set; }

        public bool ImapPublicFolderNameUpdateResult { get; set; }

        public int ImapPublicFolderNameUpdateCount { get; private set; }

        public string? UpdatedImapPublicFolderName { get; private set; }

        public bool ImapMasterUserUpdateResult { get; set; }

        public int ImapMasterUserUpdateCount { get; private set; }

        public string? UpdatedImapMasterUser { get; private set; }

        public bool HostNameUpdateResult { get; set; }

        public int HostNameUpdateCount { get; private set; }

        public string? UpdatedHostName { get; private set; }

        public bool ImapHierarchyDelimiterUpdateResult { get; set; }

        public int ImapHierarchyDelimiterUpdateCount { get; private set; }

        public string? UpdatedImapHierarchyDelimiter { get; private set; }

        public bool WorkerThreadPriorityUpdateResult { get; set; }

        public bool GateWorkerThreadPriorityMutation { get; set; }

        public TaskCompletionSource<bool> WorkerThreadPriorityMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WorkerThreadPriorityMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WorkerThreadPriorityUpdateCount { get; private set; }

        public int UpdatedWorkerThreadPriority { get; private set; }

        public bool TcpIpThreadsUpdateResult { get; set; }

        public bool GateTcpIpThreadsMutation { get; set; }

        public TaskCompletionSource<bool> TcpIpThreadsMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> TcpIpThreadsMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public Func<bool>? MaxImapConnectionsMutationProbe { get; set; }

        public bool MaxImapConnectionsLeaseHeldDuringUpdate { get; private set; }

        public bool MaxMessageSizeUpdateResult { get; set; }

        public int MaxMessageSizeUpdateCount { get; private set; }

        public int UpdatedMaxMessageSize { get; private set; }

        public Func<bool>? MaxMessageSizeMutationProbe { get; set; }

        public bool MaxMessageSizeLeaseHeldDuringUpdate { get; private set; }

        public bool MaxDeliveryThreadsUpdateResult { get; set; }

        public Func<bool>? MaxDeliveryThreadsMutationProbe { get; set; }

        public bool MaxDeliveryThreadsLeaseHeldDuringUpdate { get; private set; }

        public int MaxDeliveryThreadsUpdateCount { get; private set; }

        public int UpdatedMaxDeliveryThreads { get; private set; }

        public bool MaxAsynchronousThreadsUpdateResult { get; set; }

        public int MaxAsynchronousThreadsUpdateCount { get; private set; }

        public int UpdatedMaxAsynchronousThreads { get; private set; }

        public Func<bool>? MaxAsynchronousThreadsMutationProbe { get; set; }

        public bool MaxAsynchronousThreadsLeaseHeldDuringUpdate { get; private set; }

        public bool RuleLoopLimitUpdateResult { get; set; }

        public int RuleLoopLimitUpdateCount { get; private set; }

        public int UpdatedRuleLoopLimit { get; private set; }

        public Func<bool>? RuleLoopLimitMutationProbe { get; set; }

        public bool RuleLoopLimitLeaseHeldDuringUpdate { get; private set; }

        public bool MaxNumberOfMXHostsUpdateResult { get; set; }

        public int MaxNumberOfMXHostsUpdateCount { get; private set; }

        public int UpdatedMaxNumberOfMXHosts { get; private set; }

        public Func<bool>? MaxNumberOfMXHostsMutationProbe { get; set; }

        public bool MaxNumberOfMXHostsLeaseHeldDuringUpdate { get; private set; }

        public bool MaxSmtpRecipientsInBatchUpdateResult { get; set; }

        public bool GateMaxSmtpRecipientsInBatchMutation { get; set; }

        public TaskCompletionSource<bool> MaxSmtpRecipientsInBatchMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> MaxSmtpRecipientsInBatchMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaxSmtpRecipientsInBatchUpdateCount { get; private set; }

        public int UpdatedMaxSmtpRecipientsInBatch { get; private set; }

        public bool MaxNumberOfInvalidCommandsUpdateResult { get; set; }

        public bool GateMaxNumberOfInvalidCommandsMutation { get; set; }

        public TaskCompletionSource<bool> MaxNumberOfInvalidCommandsMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> MaxNumberOfInvalidCommandsMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaxNumberOfInvalidCommandsUpdateCount { get; private set; }

        public int UpdatedMaxNumberOfInvalidCommands { get; private set; }

        public bool DisconnectInvalidClientsUpdateResult { get; set; }

        public bool GateDisconnectInvalidClientsMutation { get; set; }

        public TaskCompletionSource<bool> DisconnectInvalidClientsMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> DisconnectInvalidClientsMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisconnectInvalidClientsUpdateCount { get; private set; }

        public bool UpdatedDisconnectInvalidClients { get; private set; }

        public bool AddDeliveredToHeaderUpdateResult { get; set; }

        public int AddDeliveredToHeaderUpdateCount { get; private set; }

        public bool UpdatedAddDeliveredToHeader { get; private set; }

        public Func<bool>? AddDeliveredToHeaderMutationProbe { get; set; }

        public bool AddDeliveredToHeaderLeaseHeldDuringUpdate { get; private set; }

        public bool AllowIncorrectLineEndingsUpdateResult { get; set; }

        public bool GateAllowIncorrectLineEndingsMutation { get; set; }

        public TaskCompletionSource<bool> AllowIncorrectLineEndingsMutationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowIncorrectLineEndingsMutationRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int AllowIncorrectLineEndingsUpdateCount { get; private set; }

        public bool UpdatedAllowIncorrectLineEndings { get; private set; }

        public bool AllowSmtpAuthPlainUpdateResult { get; set; }

        public int AllowSmtpAuthPlainUpdateCount { get; private set; }

        public bool UpdatedAllowSmtpAuthPlain { get; private set; }

        public bool AllowMailFromNullUpdateResult { get; set; }

        public int AllowMailFromNullUpdateCount { get; private set; }

        public bool UpdatedAllowMailFromNull { get; private set; }

        public bool VerifyRemoteSslCertificateUpdateResult { get; set; }

        public Func<bool>? VerifyRemoteSslCertificateMutationProbe { get; set; }

        public bool VerifyRemoteSslCertificateLeaseHeldDuringUpdate { get; private set; }

        public bool Ipv6PreferredUpdateResult { get; set; }

        public int Ipv6PreferredUpdateCount { get; private set; }

        public bool UpdatedIpv6Preferred { get; private set; }

        public bool SslCipherListUpdateResult { get; set; }

        public int SslCipherListUpdateCount { get; private set; }

        public string? UpdatedSslCipherList { get; private set; }

        public bool AutoBanOnLogonFailureUpdateResult { get; set; }

        public int AutoBanOnLogonFailureUpdateCount { get; private set; }

        public bool UpdatedAutoBanOnLogonFailure { get; private set; }

        public bool MaxInvalidLogonAttemptsUpdateResult { get; set; }

        public int MaxInvalidLogonAttemptsUpdateCount { get; private set; }

        public int UpdatedMaxInvalidLogonAttempts { get; private set; }

        public bool MaxInvalidLogonAttemptsWithinUpdateResult { get; set; }

        public int MaxInvalidLogonAttemptsWithinUpdateCount { get; private set; }

        public int UpdatedMaxInvalidLogonAttemptsWithin { get; private set; }

        public bool AutoBanMinutesUpdateResult { get; set; }

        public int AutoBanMinutesUpdateCount { get; private set; }

        public int UpdatedAutoBanMinutes { get; private set; }

        public bool SslVersionsUpdateResult { get; set; }

        public int SslVersionsUpdateCount { get; private set; }

        public int UpdatedSslVersions { get; private set; }

        public bool TlsOptionsUpdateResult { get; set; }

        public int TlsOptionsUpdateCount { get; private set; }

        public int UpdatedTlsOptions { get; private set; }

        public int VerifyRemoteSslCertificateUpdateCount { get; private set; }

        public bool UpdatedVerifyRemoteSslCertificate { get; private set; }

        public bool AntiSpamUseSpfUpdateResult { get; set; }

        public int AntiSpamUseSpfUpdateCount { get; private set; }

        public bool UpdatedAntiSpamUseSpf { get; private set; }

        public bool AntiSpamUseSpfScoreUpdateResult { get; set; }

        public int AntiSpamUseSpfScoreUpdateCount { get; private set; }

        public int UpdatedAntiSpamUseSpfScore { get; private set; }

        public bool AntiSpamUseMxChecksUpdateResult { get; set; }

        public int AntiSpamUseMxChecksUpdateCount { get; private set; }

        public bool UpdatedAntiSpamUseMxChecks { get; private set; }

        public bool AntiSpamUseMxChecksScoreUpdateResult { get; set; }

        public int AntiSpamUseMxChecksScoreUpdateCount { get; private set; }

        public int UpdatedAntiSpamUseMxChecksScore { get; private set; }

        public bool AntiSpamSpamAssassinEnabledUpdateResult { get; set; }

        public int AntiSpamSpamAssassinEnabledUpdateCount { get; private set; }

        public bool UpdatedAntiSpamSpamAssassinEnabled { get; private set; }

        public bool AntiSpamSpamAssassinScoreUpdateResult { get; set; }

        public int AntiSpamSpamAssassinScoreUpdateCount { get; private set; }

        public int UpdatedAntiSpamSpamAssassinScore { get; private set; }

        public bool AntiSpamSpamAssassinMergeScoreUpdateResult { get; set; }

        public int AntiSpamSpamAssassinMergeScoreUpdateCount { get; private set; }

        public bool UpdatedAntiSpamSpamAssassinMergeScore { get; private set; }

        public bool AntiSpamSpamAssassinHostUpdateResult { get; set; }

        public int AntiSpamSpamAssassinHostUpdateCount { get; private set; }

        public string? UpdatedAntiSpamSpamAssassinHost { get; private set; }

        public bool AntiSpamSpamAssassinPortUpdateResult { get; set; }

        public int AntiSpamSpamAssassinPortUpdateCount { get; private set; }

        public int UpdatedAntiSpamSpamAssassinPort { get; private set; }

        public bool AntiSpamMaximumMessageSizeUpdateResult { get; set; }

        public int AntiSpamMaximumMessageSizeUpdateCount { get; private set; }

        public int UpdatedAntiSpamMaximumMessageSize { get; private set; }

        public bool AntiSpamDkimVerificationEnabledUpdateResult { get; set; }

        public int AntiSpamDkimVerificationEnabledUpdateCount { get; private set; }

        public bool UpdatedAntiSpamDkimVerificationEnabled { get; private set; }

        public bool AntiSpamDkimVerificationFailureScoreUpdateResult { get; set; }

        public int AntiSpamDkimVerificationFailureScoreUpdateCount { get; private set; }

        public int UpdatedAntiSpamDkimVerificationFailureScore { get; private set; }

        public bool AntiSpamBypassGreylistingOnSpfSuccessUpdateResult { get; set; }

        public int AntiSpamBypassGreylistingOnSpfSuccessUpdateCount { get; private set; }

        public bool UpdatedAntiSpamBypassGreylistingOnSpfSuccess { get; private set; }

        public bool AntiSpamBypassGreylistingOnMailFromMxUpdateResult { get; set; }

        public int AntiSpamBypassGreylistingOnMailFromMxUpdateCount { get; private set; }

        public bool UpdatedAntiSpamBypassGreylistingOnMailFromMx { get; private set; }

        public bool AntiSpamCheckHostInHeloUpdateResult { get; set; }

        public int AntiSpamCheckHostInHeloUpdateCount { get; private set; }

        public bool UpdatedAntiSpamCheckHostInHelo { get; private set; }

        public bool AntiSpamCheckHostInHeloScoreUpdateResult { get; set; }

        public int AntiSpamCheckHostInHeloScoreUpdateCount { get; private set; }

        public int UpdatedAntiSpamCheckHostInHeloScore { get; private set; }

        public bool AntiSpamCheckPtrUpdateResult { get; set; }

        public int AntiSpamCheckPtrUpdateCount { get; private set; }

        public bool UpdatedAntiSpamCheckPtr { get; private set; }

        public bool AntiSpamCheckPtrScoreUpdateResult { get; set; }

        public int AntiSpamCheckPtrScoreUpdateCount { get; private set; }

        public int UpdatedAntiSpamCheckPtrScore { get; private set; }

        public bool AntiSpamGreyListingEnabledUpdateResult { get; set; }

        public int AntiSpamGreyListingEnabledUpdateCount { get; private set; }

        public bool UpdatedAntiSpamGreyListingEnabled { get; private set; }

        public bool AntiSpamGreyListingInitialDelayUpdateResult { get; set; }

        public int AntiSpamGreyListingInitialDelayUpdateCount { get; private set; }

        public int UpdatedAntiSpamGreyListingInitialDelay { get; private set; }

        public bool AntiSpamGreyListingInitialDeleteUpdateResult { get; set; }

        public int AntiSpamGreyListingInitialDeleteUpdateCount { get; private set; }

        public int UpdatedAntiSpamGreyListingInitialDelete { get; private set; }

        public bool AntiSpamGreyListingFinalDeleteUpdateResult { get; set; }

        public int AntiSpamGreyListingFinalDeleteUpdateCount { get; private set; }

        public int UpdatedAntiSpamGreyListingFinalDelete { get; private set; }

        public bool AntiSpamAddHeaderSpamUpdateResult { get; set; }

        public int AntiSpamAddHeaderSpamUpdateCount { get; private set; }

        public bool UpdatedAntiSpamAddHeaderSpam { get; private set; }

        public bool AntiSpamAddHeaderReasonUpdateResult { get; set; }

        public int AntiSpamAddHeaderReasonUpdateCount { get; private set; }

        public bool UpdatedAntiSpamAddHeaderReason { get; private set; }

        public bool AntiSpamPrependSubjectUpdateResult { get; set; }

        public int AntiSpamPrependSubjectUpdateCount { get; private set; }

        public bool UpdatedAntiSpamPrependSubject { get; private set; }

        public bool AntiSpamPrependSubjectTextUpdateResult { get; set; }

        public int AntiSpamPrependSubjectTextUpdateCount { get; private set; }

        public string UpdatedAntiSpamPrependSubjectText { get; private set; } = string.Empty;

        public bool AntiSpamSpamMarkThresholdUpdateResult { get; set; }

        public int AntiSpamSpamMarkThresholdUpdateCount { get; private set; }

        public int UpdatedAntiSpamSpamMarkThreshold { get; private set; }

        public bool AntiSpamSpamDeleteThresholdUpdateResult { get; set; }

        public int AntiSpamSpamDeleteThresholdUpdateCount { get; private set; }

        public int UpdatedAntiSpamSpamDeleteThreshold { get; private set; }

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
            DefaultDomainLeaseHeldDuringUpdate =
                DefaultDomainMutationProbe?.Invoke() ?? false;
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

        public ValueTask<bool> UpdateSmtpRelayerPasswordAsync(
            string smtpRelayerPassword,
            CancellationToken cancellationToken)
        {
            SmtpRelayerPasswordUpdateCount++;
            UpdatedSmtpRelayerPassword = smtpRelayerPassword;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpRelayerPasswordUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpRelayerPortAsync(
            int smtpRelayerPort,
            CancellationToken cancellationToken)
        {
            SmtpRelayerPortUpdateCount++;
            UpdatedSmtpRelayerPort = smtpRelayerPort;
            CancellationToken = cancellationToken;
            if (GateSmtpRelayerPortMutation)
            {
                SmtpRelayerPortMutationEntered.TrySetResult(true);
                return WaitForSmtpRelayerPortMutationAsync();
            }

            return ValueTask.FromResult(SmtpRelayerPortUpdateResult);
        }

        private async ValueTask<bool> WaitForSmtpRelayerPortMutationAsync()
        {
            await SmtpRelayerPortMutationRelease.Task;
            return SmtpRelayerPortUpdateResult;
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

        public ValueTask<bool> UpdateSmtpConnectionSecurityAsync(
            int smtpConnectionSecurity,
            CancellationToken cancellationToken)
        {
            SmtpConnectionSecurityUpdateCount++;
            UpdatedSmtpConnectionSecurity = smtpConnectionSecurity;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpConnectionSecurityUpdateResult);
        }

        public ValueTask<bool> UpdateWelcomePop3Async(
            string welcomePop3,
            CancellationToken cancellationToken)
        {
            WelcomePop3UpdateCount++;
            UpdatedWelcomePop3 = welcomePop3;
            CancellationToken = cancellationToken;
            if (GateWelcomePop3Mutation)
            {
                WelcomePop3MutationEntered.TrySetResult(true);
                return WaitForWelcomePop3MutationAsync();
            }

            return ValueTask.FromResult(WelcomePop3UpdateResult);
        }

        private async ValueTask<bool> WaitForWelcomePop3MutationAsync()
        {
            await WelcomePop3MutationRelease.Task;
            return WelcomePop3UpdateResult;
        }

        public ValueTask<bool> UpdateWelcomeSmtpAsync(
            string welcomeSmtp,
            CancellationToken cancellationToken)
        {
            WelcomeSmtpUpdateCount++;
            UpdatedWelcomeSmtp = welcomeSmtp;
            CancellationToken = cancellationToken;
            if (GateWelcomeSmtpMutation)
            {
                WelcomeSmtpMutationEntered.TrySetResult(true);
                return WaitForWelcomeSmtpMutationAsync();
            }

            return ValueTask.FromResult(WelcomeSmtpUpdateResult);
        }

        private async ValueTask<bool> WaitForWelcomeSmtpMutationAsync()
        {
            await WelcomeSmtpMutationRelease.Task;
            return WelcomeSmtpUpdateResult;
        }

        public ValueTask<bool> UpdateWelcomeImapAsync(
            string welcomeImap,
            CancellationToken cancellationToken)
        {
            WelcomeImapUpdateCount++;
            UpdatedWelcomeImap = welcomeImap;
            CancellationToken = cancellationToken;
            if (GateWelcomeImapMutation)
            {
                WelcomeImapMutationEntered.TrySetResult(true);
                return WaitForWelcomeImapMutationAsync();
            }

            return ValueTask.FromResult(WelcomeImapUpdateResult);
        }

        private async ValueTask<bool> WaitForWelcomeImapMutationAsync()
        {
            await WelcomeImapMutationRelease.Task;
            return WelcomeImapUpdateResult;
        }

        public ValueTask<bool> UpdateServiceSmtpAsync(
            bool serviceSmtp,
            CancellationToken cancellationToken)
        {
            ServiceSmtpUpdateCount++;
            UpdatedServiceSmtp = serviceSmtp;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ServiceSmtpUpdateResult);
        }

        public ValueTask<bool> UpdateServicePop3Async(
            bool servicePop3,
            CancellationToken cancellationToken)
        {
            ServicePop3UpdateCount++;
            UpdatedServicePop3 = servicePop3;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ServicePop3UpdateResult);
        }

        public ValueTask<bool> UpdateServiceImapAsync(
            bool serviceImap,
            CancellationToken cancellationToken)
        {
            ServiceImapUpdateCount++;
            UpdatedServiceImap = serviceImap;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ServiceImapUpdateResult);
        }

        public ValueTask<bool> UpdateSmtpDeliveryBindToIpAsync(
            string smtpDeliveryBindToIp,
            CancellationToken cancellationToken)
        {
            SmtpDeliveryBindToIpUpdateCount++;
            UpdatedSmtpDeliveryBindToIp = smtpDeliveryBindToIp;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SmtpDeliveryBindToIpUpdateResult);
        }

        public ValueTask<bool> UpdateImapSortEnabledAsync(
            bool imapSortEnabled,
            CancellationToken cancellationToken)
        {
            ImapSortEnabledUpdateCount++;
            UpdatedImapSortEnabled = imapSortEnabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapSortEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateImapQuotaEnabledAsync(
            bool imapQuotaEnabled,
            CancellationToken cancellationToken)
        {
            ImapQuotaEnabledUpdateCount++;
            UpdatedImapQuotaEnabled = imapQuotaEnabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapQuotaEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateImapIdleEnabledAsync(
            bool imapIdleEnabled,
            CancellationToken cancellationToken)
        {
            ImapIdleEnabledUpdateCount++;
            UpdatedImapIdleEnabled = imapIdleEnabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapIdleEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateImapAclEnabledAsync(
            bool imapAclEnabled,
            CancellationToken cancellationToken)
        {
            ImapAclEnabledUpdateCount++;
            UpdatedImapAclEnabled = imapAclEnabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapAclEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateImapSaslPlainEnabledAsync(
            bool imapSaslPlainEnabled,
            CancellationToken cancellationToken)
        {
            ImapSaslPlainEnabledUpdateCount++;
            UpdatedImapSaslPlainEnabled = imapSaslPlainEnabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapSaslPlainEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateImapSaslInitialResponseEnabledAsync(
            bool imapSaslInitialResponseEnabled,
            CancellationToken cancellationToken)
        {
            ImapSaslInitialResponseEnabledUpdateCount++;
            UpdatedImapSaslInitialResponseEnabled = imapSaslInitialResponseEnabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapSaslInitialResponseEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateImapPublicFolderNameAsync(
            string imapPublicFolderName,
            CancellationToken cancellationToken)
        {
            ImapPublicFolderNameUpdateCount++;
            UpdatedImapPublicFolderName = imapPublicFolderName;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapPublicFolderNameUpdateResult);
        }

        public ValueTask<bool> UpdateImapMasterUserAsync(
            string imapMasterUser,
            CancellationToken cancellationToken)
        {
            ImapMasterUserUpdateCount++;
            UpdatedImapMasterUser = imapMasterUser;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapMasterUserUpdateResult);
        }

        public ValueTask<bool> UpdateHostNameAsync(
            string hostName,
            CancellationToken cancellationToken)
        {
            HostNameUpdateCount++;
            UpdatedHostName = hostName;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(HostNameUpdateResult);
        }

        public ValueTask<bool> UpdateImapHierarchyDelimiterAsync(
            string imapHierarchyDelimiter,
            CancellationToken cancellationToken)
        {
            ImapHierarchyDelimiterUpdateCount++;
            UpdatedImapHierarchyDelimiter = imapHierarchyDelimiter;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(ImapHierarchyDelimiterUpdateResult);
        }

        public ValueTask<bool> UpdateWorkerThreadPriorityAsync(
            int workerThreadPriority,
            CancellationToken cancellationToken)
        {
            WorkerThreadPriorityUpdateCount++;
            UpdatedWorkerThreadPriority = workerThreadPriority;
            CancellationToken = cancellationToken;
            if (GateWorkerThreadPriorityMutation)
            {
                WorkerThreadPriorityMutationEntered.TrySetResult(true);
                return WaitForWorkerThreadPriorityMutationAsync();
            }

            return ValueTask.FromResult(WorkerThreadPriorityUpdateResult);
        }

        private async ValueTask<bool> WaitForWorkerThreadPriorityMutationAsync()
        {
            await WorkerThreadPriorityMutationRelease.Task;
            return WorkerThreadPriorityUpdateResult;
        }

        public ValueTask<bool> UpdateTcpIpThreadsAsync(
            int tcpIpThreads,
            CancellationToken cancellationToken)
        {
            TcpIpThreadsUpdateCount++;
            UpdatedTcpIpThreads = tcpIpThreads;
            CancellationToken = cancellationToken;
            if (GateTcpIpThreadsMutation)
            {
                TcpIpThreadsMutationEntered.TrySetResult(true);
                return WaitForTcpIpThreadsMutationAsync();
            }

            return ValueTask.FromResult(TcpIpThreadsUpdateResult);
        }

        private async ValueTask<bool> WaitForTcpIpThreadsMutationAsync()
        {
            await TcpIpThreadsMutationRelease.Task;
            return TcpIpThreadsUpdateResult;
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
            MaxImapConnectionsLeaseHeldDuringUpdate = MaxImapConnectionsMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(MaxImapConnectionsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxMessageSizeAsync(
            int maxMessageSize,
            CancellationToken cancellationToken)
        {
            MaxMessageSizeUpdateCount++;
            UpdatedMaxMessageSize = maxMessageSize;
            CancellationToken = cancellationToken;
            MaxMessageSizeLeaseHeldDuringUpdate =
                MaxMessageSizeMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(MaxMessageSizeUpdateResult);
        }

        public ValueTask<bool> UpdateMaxDeliveryThreadsAsync(
            int maxDeliveryThreads,
            CancellationToken cancellationToken)
        {
            MaxDeliveryThreadsUpdateCount++;
            UpdatedMaxDeliveryThreads = maxDeliveryThreads;
            CancellationToken = cancellationToken;
            MaxDeliveryThreadsLeaseHeldDuringUpdate = MaxDeliveryThreadsMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(MaxDeliveryThreadsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxAsynchronousThreadsAsync(
            int maxAsynchronousThreads,
            CancellationToken cancellationToken)
        {
            MaxAsynchronousThreadsUpdateCount++;
            UpdatedMaxAsynchronousThreads = maxAsynchronousThreads;
            CancellationToken = cancellationToken;
            MaxAsynchronousThreadsLeaseHeldDuringUpdate =
                MaxAsynchronousThreadsMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(MaxAsynchronousThreadsUpdateResult);
        }

        public ValueTask<bool> UpdateRuleLoopLimitAsync(
            int ruleLoopLimit,
            CancellationToken cancellationToken)
        {
            RuleLoopLimitUpdateCount++;
            UpdatedRuleLoopLimit = ruleLoopLimit;
            CancellationToken = cancellationToken;
            RuleLoopLimitLeaseHeldDuringUpdate =
                RuleLoopLimitMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(RuleLoopLimitUpdateResult);
        }

        public ValueTask<bool> UpdateMaxNumberOfMXHostsAsync(
            int maxNumberOfMXHosts,
            CancellationToken cancellationToken)
        {
            MaxNumberOfMXHostsUpdateCount++;
            UpdatedMaxNumberOfMXHosts = maxNumberOfMXHosts;
            CancellationToken = cancellationToken;
            MaxNumberOfMXHostsLeaseHeldDuringUpdate =
                MaxNumberOfMXHostsMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(MaxNumberOfMXHostsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxSmtpRecipientsInBatchAsync(
            int maxSmtpRecipientsInBatch,
            CancellationToken cancellationToken)
        {
            MaxSmtpRecipientsInBatchUpdateCount++;
            UpdatedMaxSmtpRecipientsInBatch = maxSmtpRecipientsInBatch;
            CancellationToken = cancellationToken;
            if (GateMaxSmtpRecipientsInBatchMutation)
            {
                MaxSmtpRecipientsInBatchMutationEntered.TrySetResult(true);
                return WaitForMaxSmtpRecipientsInBatchMutationAsync();
            }

            return ValueTask.FromResult(MaxSmtpRecipientsInBatchUpdateResult);
        }

        private async ValueTask<bool> WaitForMaxSmtpRecipientsInBatchMutationAsync()
        {
            await MaxSmtpRecipientsInBatchMutationRelease.Task;
            return MaxSmtpRecipientsInBatchUpdateResult;
        }

        public ValueTask<bool> UpdateMaxNumberOfInvalidCommandsAsync(
            int maxNumberOfInvalidCommands,
            CancellationToken cancellationToken)
        {
            MaxNumberOfInvalidCommandsUpdateCount++;
            UpdatedMaxNumberOfInvalidCommands = maxNumberOfInvalidCommands;
            CancellationToken = cancellationToken;
            if (GateMaxNumberOfInvalidCommandsMutation)
            {
                MaxNumberOfInvalidCommandsMutationEntered.TrySetResult(true);
                return WaitForMaxNumberOfInvalidCommandsMutationAsync();
            }

            return ValueTask.FromResult(MaxNumberOfInvalidCommandsUpdateResult);
        }

        private async ValueTask<bool> WaitForMaxNumberOfInvalidCommandsMutationAsync()
        {
            await MaxNumberOfInvalidCommandsMutationRelease.Task;
            return MaxNumberOfInvalidCommandsUpdateResult;
        }

        public ValueTask<bool> UpdateDisconnectInvalidClientsAsync(
            bool disconnectInvalidClients,
            CancellationToken cancellationToken)
        {
            DisconnectInvalidClientsUpdateCount++;
            UpdatedDisconnectInvalidClients = disconnectInvalidClients;
            CancellationToken = cancellationToken;
            if (GateDisconnectInvalidClientsMutation)
            {
                DisconnectInvalidClientsMutationEntered.TrySetResult(true);
                return WaitForDisconnectInvalidClientsMutationAsync();
            }

            return ValueTask.FromResult(DisconnectInvalidClientsUpdateResult);
        }

        private async ValueTask<bool> WaitForDisconnectInvalidClientsMutationAsync()
        {
            await DisconnectInvalidClientsMutationRelease.Task;
            return DisconnectInvalidClientsUpdateResult;
        }

        public ValueTask<bool> UpdateAddDeliveredToHeaderAsync(
            bool addDeliveredToHeader,
            CancellationToken cancellationToken)
        {
            AddDeliveredToHeaderUpdateCount++;
            UpdatedAddDeliveredToHeader = addDeliveredToHeader;
            CancellationToken = cancellationToken;
            AddDeliveredToHeaderLeaseHeldDuringUpdate =
                AddDeliveredToHeaderMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(AddDeliveredToHeaderUpdateResult);
        }

        public ValueTask<bool> UpdateAllowIncorrectLineEndingsAsync(
            bool allowIncorrectLineEndings,
            CancellationToken cancellationToken)
        {
            AllowIncorrectLineEndingsUpdateCount++;
            UpdatedAllowIncorrectLineEndings = allowIncorrectLineEndings;
            CancellationToken = cancellationToken;
            if (GateAllowIncorrectLineEndingsMutation)
            {
                AllowIncorrectLineEndingsMutationEntered.TrySetResult(true);
                return WaitForAllowIncorrectLineEndingsMutationAsync();
            }

            return ValueTask.FromResult(AllowIncorrectLineEndingsUpdateResult);
        }

        private async ValueTask<bool> WaitForAllowIncorrectLineEndingsMutationAsync()
        {
            await AllowIncorrectLineEndingsMutationRelease.Task;
            return AllowIncorrectLineEndingsUpdateResult;
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
            VerifyRemoteSslCertificateLeaseHeldDuringUpdate =
                VerifyRemoteSslCertificateMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(VerifyRemoteSslCertificateUpdateResult);
        }

        public ValueTask<bool> UpdateIpv6PreferredAsync(
            bool ipv6Preferred,
            CancellationToken cancellationToken)
        {
            Ipv6PreferredUpdateCount++;
            UpdatedIpv6Preferred = ipv6Preferred;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(Ipv6PreferredUpdateResult);
        }

        public ValueTask<bool> UpdateSslCipherListAsync(
            string sslCipherList,
            CancellationToken cancellationToken)
        {
            SslCipherListUpdateCount++;
            UpdatedSslCipherList = sslCipherList;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SslCipherListUpdateResult);
        }

        public ValueTask<bool> UpdateAutoBanOnLogonFailureAsync(
            bool autoBanOnLogonFailure,
            CancellationToken cancellationToken)
        {
            AutoBanOnLogonFailureUpdateCount++;
            UpdatedAutoBanOnLogonFailure = autoBanOnLogonFailure;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AutoBanOnLogonFailureUpdateResult);
        }

        public ValueTask<bool> UpdateMaxInvalidLogonAttemptsAsync(
            int maxInvalidLogonAttempts,
            CancellationToken cancellationToken)
        {
            MaxInvalidLogonAttemptsUpdateCount++;
            UpdatedMaxInvalidLogonAttempts = maxInvalidLogonAttempts;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxInvalidLogonAttemptsUpdateResult);
        }

        public ValueTask<bool> UpdateMaxInvalidLogonAttemptsWithinAsync(
            int maxInvalidLogonAttemptsWithin,
            CancellationToken cancellationToken)
        {
            MaxInvalidLogonAttemptsWithinUpdateCount++;
            UpdatedMaxInvalidLogonAttemptsWithin = maxInvalidLogonAttemptsWithin;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(MaxInvalidLogonAttemptsWithinUpdateResult);
        }

        public ValueTask<bool> UpdateAutoBanMinutesAsync(
            int autoBanMinutes,
            CancellationToken cancellationToken)
        {
            AutoBanMinutesUpdateCount++;
            UpdatedAutoBanMinutes = autoBanMinutes;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AutoBanMinutesUpdateResult);
        }

        public ValueTask<bool> UpdateSslVersionsAsync(
            int sslVersions,
            CancellationToken cancellationToken)
        {
            SslVersionsUpdateCount++;
            UpdatedSslVersions = sslVersions;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(SslVersionsUpdateResult);
        }

        public ValueTask<bool> UpdateTlsOptionsAsync(
            int tlsOptions,
            CancellationToken cancellationToken)
        {
            TlsOptionsUpdateCount++;
            UpdatedTlsOptions = tlsOptions;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(TlsOptionsUpdateResult);
        }

        public ValueTask<bool> UpdateAntiVirusClamWinEnabledAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiVirusClamWinEnabledUpdateCount++;
            UpdatedAntiVirusClamWinEnabled = enabled;
            CancellationToken = cancellationToken;
            AntiVirusClamWinEnabledLeaseHeldDuringUpdate =
                AntiVirusClamWinEnabledMutationProbe?.Invoke() ?? false;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateAntiVirusClamWinExecutableAsync(
            string executable,
            CancellationToken cancellationToken)
        {
            AntiVirusClamWinExecutableUpdateCount++;
            UpdatedAntiVirusClamWinExecutable = executable;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateAntiVirusClamWinDatabaseAsync(
            string database,
            CancellationToken cancellationToken)
        {
            AntiVirusClamWinDatabaseUpdateCount++;
            UpdatedAntiVirusClamWinDatabase = database;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateAntiVirusActionAsync(
            int action,
            CancellationToken cancellationToken)
        {
            AntiVirusActionUpdateCount++;
            UpdatedAntiVirusAction = action;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateAntiVirusNotifyReceiverAsync(
            bool notifyReceiver,
            CancellationToken cancellationToken)
        {
            AntiVirusNotifyReceiverUpdateCount++;
            UpdatedAntiVirusNotifyReceiver = notifyReceiver;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateAntiVirusNotifySenderAsync(
            bool notifySender,
            CancellationToken cancellationToken)
        {
            AntiVirusNotifySenderUpdateCount++;
            UpdatedAntiVirusNotifySender = notifySender;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(UpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamUseSpfAsync(
            bool useSpf,
            CancellationToken cancellationToken)
        {
            AntiSpamUseSpfUpdateCount++;
            UpdatedAntiSpamUseSpf = useSpf;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamUseSpfUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamUseSpfScoreAsync(
            int useSpfScore,
            CancellationToken cancellationToken)
        {
            AntiSpamUseSpfScoreUpdateCount++;
            UpdatedAntiSpamUseSpfScore = useSpfScore;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamUseSpfScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamUseMxChecksAsync(
            bool useMxChecks,
            CancellationToken cancellationToken)
        {
            AntiSpamUseMxChecksUpdateCount++;
            UpdatedAntiSpamUseMxChecks = useMxChecks;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamUseMxChecksUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamUseMxChecksScoreAsync(
            int useMxChecksScore,
            CancellationToken cancellationToken)
        {
            AntiSpamUseMxChecksScoreUpdateCount++;
            UpdatedAntiSpamUseMxChecksScore = useMxChecksScore;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamUseMxChecksScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamAssassinEnabledAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamAssassinEnabledUpdateCount++;
            UpdatedAntiSpamSpamAssassinEnabled = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamAssassinEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamAssassinScoreAsync(
            int score,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamAssassinScoreUpdateCount++;
            UpdatedAntiSpamSpamAssassinScore = score;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamAssassinScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamAssassinMergeScoreAsync(
            bool mergeScore,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamAssassinMergeScoreUpdateCount++;
            UpdatedAntiSpamSpamAssassinMergeScore = mergeScore;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamAssassinMergeScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamAssassinHostAsync(
            string host,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamAssassinHostUpdateCount++;
            UpdatedAntiSpamSpamAssassinHost = host;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamAssassinHostUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamAssassinPortAsync(
            int port,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamAssassinPortUpdateCount++;
            UpdatedAntiSpamSpamAssassinPort = port;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamAssassinPortUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamMaximumMessageSizeAsync(
            int maximumMessageSize,
            CancellationToken cancellationToken)
        {
            AntiSpamMaximumMessageSizeUpdateCount++;
            UpdatedAntiSpamMaximumMessageSize = maximumMessageSize;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamMaximumMessageSizeUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamDkimVerificationEnabledAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamDkimVerificationEnabledUpdateCount++;
            UpdatedAntiSpamDkimVerificationEnabled = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamDkimVerificationEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamDkimVerificationFailureScoreAsync(
            int score,
            CancellationToken cancellationToken)
        {
            AntiSpamDkimVerificationFailureScoreUpdateCount++;
            UpdatedAntiSpamDkimVerificationFailureScore = score;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamDkimVerificationFailureScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamBypassGreylistingOnSpfSuccessAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamBypassGreylistingOnSpfSuccessUpdateCount++;
            UpdatedAntiSpamBypassGreylistingOnSpfSuccess = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamBypassGreylistingOnSpfSuccessUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamBypassGreylistingOnMailFromMxAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamBypassGreylistingOnMailFromMxUpdateCount++;
            UpdatedAntiSpamBypassGreylistingOnMailFromMx = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamBypassGreylistingOnMailFromMxUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamCheckHostInHeloAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamCheckHostInHeloUpdateCount++;
            UpdatedAntiSpamCheckHostInHelo = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamCheckHostInHeloUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamCheckHostInHeloScoreAsync(
            int score,
            CancellationToken cancellationToken)
        {
            AntiSpamCheckHostInHeloScoreUpdateCount++;
            UpdatedAntiSpamCheckHostInHeloScore = score;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamCheckHostInHeloScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamCheckPtrAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamCheckPtrUpdateCount++;
            UpdatedAntiSpamCheckPtr = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamCheckPtrUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamCheckPtrScoreAsync(
            int score,
            CancellationToken cancellationToken)
        {
            AntiSpamCheckPtrScoreUpdateCount++;
            UpdatedAntiSpamCheckPtrScore = score;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamCheckPtrScoreUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamGreyListingEnabledAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamGreyListingEnabledUpdateCount++;
            UpdatedAntiSpamGreyListingEnabled = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamGreyListingEnabledUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamGreyListingInitialDelayAsync(
            int minutes,
            CancellationToken cancellationToken)
        {
            AntiSpamGreyListingInitialDelayUpdateCount++;
            UpdatedAntiSpamGreyListingInitialDelay = minutes;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamGreyListingInitialDelayUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamGreyListingInitialDeleteAsync(
            int hours,
            CancellationToken cancellationToken)
        {
            AntiSpamGreyListingInitialDeleteUpdateCount++;
            UpdatedAntiSpamGreyListingInitialDelete = hours;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamGreyListingInitialDeleteUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamGreyListingFinalDeleteAsync(
            int hours,
            CancellationToken cancellationToken)
        {
            AntiSpamGreyListingFinalDeleteUpdateCount++;
            UpdatedAntiSpamGreyListingFinalDelete = hours;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamGreyListingFinalDeleteUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamAddHeaderSpamAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamAddHeaderSpamUpdateCount++;
            UpdatedAntiSpamAddHeaderSpam = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamAddHeaderSpamUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamAddHeaderReasonAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamAddHeaderReasonUpdateCount++;
            UpdatedAntiSpamAddHeaderReason = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamAddHeaderReasonUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamPrependSubjectAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            AntiSpamPrependSubjectUpdateCount++;
            UpdatedAntiSpamPrependSubject = enabled;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamPrependSubjectUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamPrependSubjectTextAsync(
            string text,
            CancellationToken cancellationToken)
        {
            AntiSpamPrependSubjectTextUpdateCount++;
            UpdatedAntiSpamPrependSubjectText = text;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamPrependSubjectTextUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamMarkThresholdAsync(
            int threshold,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamMarkThresholdUpdateCount++;
            UpdatedAntiSpamSpamMarkThreshold = threshold;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamMarkThresholdUpdateResult);
        }

        public ValueTask<bool> UpdateAntiSpamSpamDeleteThresholdAsync(
            int threshold,
            CancellationToken cancellationToken)
        {
            AntiSpamSpamDeleteThresholdUpdateCount++;
            UpdatedAntiSpamSpamDeleteThreshold = threshold;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(AntiSpamSpamDeleteThresholdUpdateResult);
        }

        public ValueTask<bool> UpdateBackupDestinationAsync(
            string backupDestination,
            CancellationToken cancellationToken)
        {
            BackupDestinationUpdateCount++;
            UpdatedBackupDestination = backupDestination;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(BackupDestinationUpdateResult);
        }

        public ValueTask<bool> UpdateBackupSettingsAsync(
            bool backupSettings,
            CancellationToken cancellationToken)
        {
            BackupSettingsUpdateCount++;
            UpdatedBackupSettings.Add(backupSettings);
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(BackupSettingsUpdateResult);
        }

        public ValueTask<bool> UpdateBackupDomainsAsync(
            bool backupDomains,
            CancellationToken cancellationToken)
        {
            BackupDomainsUpdateCount++;
            UpdatedBackupDomains.Add(backupDomains);
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(BackupDomainsUpdateResult);
        }

        public ValueTask<bool> UpdateBackupMessagesAsync(
            bool backupMessages,
            CancellationToken cancellationToken)
        {
            BackupMessagesUpdateCount++;
            UpdatedBackupMessages.Add(backupMessages);
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(BackupMessagesUpdateResult);
        }

        public ValueTask<bool> UpdateBackupCompressionAsync(
            bool backupCompression,
            CancellationToken cancellationToken)
        {
            BackupCompressionUpdateCount++;
            UpdatedBackupCompression.Add(backupCompression);
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(BackupCompressionUpdateResult);
        }

    }

    private sealed class TrackingAuthorizationLease : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
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
