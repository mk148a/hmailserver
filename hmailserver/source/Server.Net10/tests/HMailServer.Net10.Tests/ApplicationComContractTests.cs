using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ApplicationComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    [TestMethod]
    public void ApplicationInterface_PreservesLegacyIidDispatchIdsAndVtableOrder()
    {
        var contract = typeof(IInterfaceApplication);

        Assert.AreEqual(new Guid("0005B084-4C3A-11D9-8530-B8CDE3157849"), typeof(ComServerState).GUID);
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3, 4 },
            Enum.GetValues<ComServerState>().Select(static value => (int)value).ToArray());

        Assert.AreEqual(new Guid("2C1A3EF1-115F-4029-BB33-D9CCA4BB0DE8"), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        CollectionAssert.AreEqual(
            new[]
            {
                "Start", "Stop", "get_Settings", "get_Domains", "get_ServerState", "get_Database",
                "get_Utilities", "SubmitEMail", "get_Status", "get_Version", "Connect",
                "get_InitializationFile", "Reinitialize", "get_Rules", "get_BackupManager",
                "get_GlobalObjects", "Authenticate", "get_Links", "get_Diagnostics",
                "get_VersionArchitecture"
            },
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());

        Assert.AreEqual(3, contract.GetProperty(nameof(IInterfaceApplication.Settings))?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(17, contract.GetMethod(nameof(IInterfaceApplication.Authenticate))?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void AccountInterface_PreservesLegacyIidAndCompleteVtableOrder()
    {
        var contract = typeof(IInterfaceAccount);

        Assert.AreEqual(new Guid("E5EDC050-0899-4A3B-BF4C-420212FC3895"), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                "get_Active", "set_Active", "get_ADDomain", "set_ADDomain", "get_Address", "set_Address",
                "get_DomainID", "set_DomainID", "get_ID", "get_IsAD", "set_IsAD", "get_Password",
                "set_Password", "get_Size", "Save", "get_ADUsername", "set_ADUsername", "DeleteMessages",
                "get_Messages", "get_MaxSize", "set_MaxSize", "get_VacationMessageIsOn",
                "set_VacationMessageIsOn", "get_VacationMessage", "set_VacationMessage",
                "get_VacationSubject", "set_VacationSubject", "get_FetchAccounts", "get_AdminLevel",
                "set_AdminLevel", "get_Rules", "ValidatePassword", "UnlockMailbox", "get_IMAPFolders",
                "get_QuotaUsed", "get_ForwardEnabled", "set_ForwardEnabled", "get_ForwardAddress",
                "set_ForwardAddress", "get_ForwardKeepOriginal", "set_ForwardKeepOriginal",
                "get_SignatureEnabled", "set_SignatureEnabled", "get_SignaturePlainText",
                "set_SignaturePlainText", "get_SignatureHTML", "set_SignatureHTML", "get_LastLogonTime",
                "get_VacationMessageExpires", "set_VacationMessageExpires", "get_VacationMessageExpiresDate",
                "set_VacationMessageExpiresDate", "get_PersonFirstName", "set_PersonFirstName",
                "get_PersonLastName", "set_PersonLastName", "Delete", "get_VacationMessageAbortSpamFlagged",
                "set_VacationMessageAbortSpamFlagged", "get_ForwardAbortSpamFlagged",
                "set_ForwardAbortSpamFlagged"
            },
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
        Assert.AreEqual(20, contract.GetProperty(nameof(IInterfaceAccount.AdminLevel))?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Application>(
            "D6567EF8-0A6C-48E7-9288-A2463123C2F3",
            "hMailServer.Application.1",
            typeof(IInterfaceApplication));
        AssertComClass<Account>(
            "369BE902-9F27-4722-A29F-3059E4D7021D",
            "hMailServer.Account.1",
            typeof(IInterfaceAccount));
    }

    [TestMethod]
    public void Application_AuthenticationPreservesLegacyAdministratorBoundary()
    {
        SettingsAdministrationRuntimeHost.Configure(
            new FixedSettingsAdministrationStore(
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
                    SslCipherList: "TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256")));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Settings);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        var account = application.Authenticate("administrator", "secret");

        Assert.IsNotNull(account);
        Assert.AreEqual(ComAdminLevel.ServerAdministrator, account.AdminLevel);
        var settings = application.Settings;
        Assert.IsInstanceOfType<Settings>(settings);
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
    }

    [TestMethod]
    public void Application_CoreScalarsPreserveLegacyAuthBoundariesAndUseConfiguredRuntime()
    {
        ApplicationRuntimeHost.Configure(
            new FixedApplicationRuntimeStore(
                new ApplicationRuntimeSnapshot(
                    ServerState: (int)ComServerState.Running,
                    Version: "5.7.0-B2643",
                    InitializationFile: @"C:\Program Files\hMailServer\Bin\hMailServer.ini",
                    VersionArchitecture: Environment.Is64BitProcess ? "x64" : "x86")));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        Assert.AreEqual("5.7.0-B2643", application.Version);
        Assert.AreEqual(Environment.Is64BitProcess ? "x64" : "x86", application.VersionArchitecture);

        var stateDenied = Assert.ThrowsExactly<COMException>(() => _ = application.ServerState);
        var iniDenied = Assert.ThrowsExactly<COMException>(() => _ = application.InitializationFile);
        Assert.AreEqual(EAccessDenied, stateDenied.ErrorCode);
        Assert.AreEqual(EAccessDenied, iniDenied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        Assert.AreEqual(ComServerState.Running, application.ServerState);
        Assert.AreEqual(@"C:\Program Files\hMailServer\Bin\hMailServer.ini", application.InitializationFile);
    }

    [TestMethod]
    public void Application_ServiceControlOperationsRemainExplicitlyPending()
    {
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        AssertOperationPending(application.Start);
        AssertOperationPending(application.Stop);
        AssertOperationPending(application.Connect);
        AssertOperationPending(application.Reinitialize);
        AssertOperationPending(application.SubmitEMail);
    }

    [TestMethod]
    public void Application_DomainsPreserveAdministratorBoundaryAndUseConfiguredRuntime()
    {
        DomainAdministrationRuntimeHost.Configure(
            new FixedDomainAdministrationStore(
                new[]
                {
                    new DomainAdministrationSnapshot(10, "alpha.example", true)
                }));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Domains);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var domains = application.Domains;

        Assert.AreEqual(1, domains.Count);
        Assert.AreEqual("alpha.example", domains[0].Name);
    }

    [TestMethod]
    public void Application_RulesPreserveAdministratorBoundaryAndUseConfiguredGlobalRuntime()
    {
        RuleAdministrationRuntimeHost.Configure(
            new FixedRuleAdministrationStore(
                new[]
                {
                    new RuleAdministrationSnapshot(10, 0, "Global first", true, true, 1),
                    new RuleAdministrationSnapshot(20, 100, "Account rule", true, true, 1),
                    new RuleAdministrationSnapshot(30, 0, "Global second", false, false, 2)
                }));
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Rules);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var rules = application.Rules;

        Assert.AreEqual(2, rules.Count);
        Assert.AreEqual("Global first", rules[0].Name);
        Assert.AreEqual(0, rules[0].AccountID);
        Assert.AreEqual("Global second", rules.get_ItemByDBID(30).Name);
    }

    [TestMethod]
    public void Application_DoesNotAttemptAnonymousAdministratorAccess()
    {
        var application = new Application(new RecordingAdministratorAuthenticationProvider(string.Empty));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Settings);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
    }

    [TestMethod]
    public void Account_DirectActivationPreservesLegacyAccessDeniedBoundary()
    {
        var account = new Account();

        var adminLevelError = Assert.ThrowsExactly<COMException>(() => _ = account.AdminLevel);
        var lastLogonError = Assert.ThrowsExactly<COMException>(() => _ = account.LastLogonTime);

        Assert.AreEqual(EAccessDenied, adminLevelError.ErrorCode);
        Assert.AreEqual(EAccessDenied, lastLogonError.ErrorCode);
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
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

    private sealed class FixedDomainAdministrationStore(IReadOnlyList<DomainAdministrationSnapshot> domains)
        : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(domains);
    }

    private sealed class FixedSettingsAdministrationStore(SettingsAdministrationSnapshot snapshot)
        : ISettingsAdministrationStore
    {
        public ValueTask<SettingsAdministrationSnapshot> GetSettingsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(snapshot);
    }

    private sealed class FixedRuleAdministrationStore(IReadOnlyList<RuleAdministrationSnapshot> rules)
        : IRuleAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RuleAdministrationSnapshot>>(
                rules.Where(rule => rule.AccountId == accountId).OrderBy(rule => rule.SortOrder).ToArray());
    }

    private sealed class FixedApplicationRuntimeStore(ApplicationRuntimeSnapshot snapshot)
        : IApplicationRuntimeStore
    {
        public ApplicationRuntimeSnapshot GetSnapshot() => snapshot;
    }
}
