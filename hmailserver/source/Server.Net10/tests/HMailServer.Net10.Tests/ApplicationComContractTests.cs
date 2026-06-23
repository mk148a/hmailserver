using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ApplicationComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    [TestMethod]
    public void ApplicationInterface_PreservesLegacyIidDispatchIdsAndVtableOrder()
    {
        var contract = typeof(IInterfaceApplication);

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
        var application = new Application(new RecordingAdministratorAuthenticationProvider("secret"));

        var denied = Assert.ThrowsExactly<COMException>(() => _ = application.Settings);
        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        var account = application.Authenticate("administrator", "secret");

        Assert.IsNotNull(account);
        Assert.AreEqual(ComAdminLevel.ServerAdministrator, account.AdminLevel);
        Assert.IsInstanceOfType<Settings>(application.Settings);
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
    public void Application_EmptyAdministratorPasswordPreservesLegacyAnonymousAccess()
    {
        var application = new Application(new RecordingAdministratorAuthenticationProvider(string.Empty));

        Assert.IsInstanceOfType<Settings>(application.Settings);
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
}
