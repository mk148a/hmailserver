using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class FetchAccountsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceFetchAccount),
            "752C1F5E-74DD-424F-AB60-07D9ABB5B7A4",
            new[]
            {
                "get_ID", "get_Name", "set_Name", "get_ServerAddress", "set_ServerAddress",
                "get_Port", "set_Port", "get_ServerType", "set_ServerType", "get_Username",
                "set_Username", "get_Password", "set_Password", "get_MinutesBetweenFetch",
                "set_MinutesBetweenFetch", "get_DaysToKeepMessages", "set_DaysToKeepMessages",
                "Save", "get_AccountID", "set_AccountID", "get_Enabled", "set_Enabled",
                "get_ProcessMIMERecipients", "set_ProcessMIMERecipients", "DownloadNow",
                "get_ProcessMIMEDate", "set_ProcessMIMEDate", "get_UseSSL", "set_UseSSL",
                "Delete", "get_NextDownloadTime", "get_UseAntiSpam", "set_UseAntiSpam",
                "get_UseAntiVirus", "set_UseAntiVirus", "get_EnableRouteRecipients",
                "set_EnableRouteRecipients", "get_IsLocked", "get_ConnectionSecurity",
                "set_ConnectionSecurity", "get_MIMERecipientHeaders", "set_MIMERecipientHeaders"
            });
        Assert.AreEqual(
            24,
            typeof(IInterfaceFetchAccount)
                .GetProperty(nameof(IInterfaceFetchAccount.MIMERecipientHeaders))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceFetchAccounts),
            "1517E0BE-5226-46CC-8C2A-BB16B680FF48",
            new[]
            {
                "get_Count", "get_ItemByDBID", "get_Item", "Refresh", "Delete",
                "DeleteByDBID", "Add"
            });
        Assert.AreEqual(
            3,
            typeof(IInterfaceFetchAccounts).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            8,
            typeof(IInterfaceFetchAccounts).GetMethod("Add")?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<FetchAccount>(
            "6F5E2977-2F51-40B0-847B-DD44C9ACC5A5",
            "hMailServer.FetchAccount.1",
            typeof(IInterfaceFetchAccount));
        AssertComClass<FetchAccounts>(
            "F17C3A00-A7A0-4519-AEDD-DCC3B8DE6A3D",
            "hMailServer.FetchAccounts.1",
            typeof(IInterfaceFetchAccounts));
    }

    [TestMethod]
    public void ConnectionSecurityEnum_PreservesLegacyValuesAndGuid()
    {
        Assert.AreEqual(new Guid("122C5B58-9A23-40F5-83C0-7B683D156522"), typeof(ComConnectionSecurity).GUID);
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
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var accountsError = Assert.ThrowsExactly<COMException>(() => _ = new FetchAccounts().Count);
        var refreshError = Assert.ThrowsExactly<COMException>(new FetchAccounts().Refresh);
        var accountError = Assert.ThrowsExactly<COMException>(() => _ = new FetchAccount().Name);

        Assert.AreEqual(EAccessDenied, accountsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, refreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var refreshed = new[]
        {
            CreateSnapshot(20, 100, "Backup POP3"),
            CreateSnapshot(30, 100, "Archive POP3")
        };
        var failRefresh = false;
        IInterfaceFetchAccounts accounts = FetchAccounts.CreateAuthorized(
            new[] { CreateSnapshot(10, 100, "External POP3") },
            () => failRefresh
                ? throw new InvalidOperationException("store failed")
                : refreshed);

        accounts.Refresh();

        Assert.AreEqual(2, accounts.Count);
        Assert.AreEqual("Backup POP3", accounts[0].Name);
        Assert.AreEqual("Archive POP3", accounts.get_ItemByDBID(30).Name);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => accounts.get_ItemByDBID(10)).ErrorCode);

        failRefresh = true;
        var failure = Assert.ThrowsExactly<COMException>(accounts.Refresh);

        Assert.AreEqual(unchecked((int)0x80004005), failure.ErrorCode);
        Assert.AreEqual(2, accounts.Count);
        Assert.AreEqual("Backup POP3", accounts[0].Name);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceFetchAccounts accounts = FetchAccounts.CreateAuthorized(
            new[]
            {
                CreateSnapshot(10, 100, "External POP3"),
                CreateSnapshot(20, 100, "Backup POP3")
            });

        Assert.AreEqual(2, accounts.Count);
        AssertFetchAccount(accounts[0], 10, "External POP3");
        AssertFetchAccount(accounts.get_ItemByDBID(20), 20, "Backup POP3");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = accounts[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = accounts.get_ItemByDBID(30));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(accounts.Refresh);
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => accounts.Add());
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].Name = "changed");
        var pendingSensitiveRead = Assert.ThrowsExactly<COMException>(() => _ = accounts[0].Password);
        var pendingExecution = Assert.ThrowsExactly<COMException>(accounts[0].DownloadNow);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSensitiveRead.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingExecution.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_KeepsLegacyUseSslAliasLimitedToDirectTls()
    {
        IInterfaceFetchAccounts accounts = FetchAccounts.CreateAuthorized(
            new[]
            {
                CreateSnapshot(10, 100, "Plain POP3", ComConnectionSecurity.None),
                CreateSnapshot(20, 100, "Implicit TLS POP3", ComConnectionSecurity.Tls),
                CreateSnapshot(30, 100, "Optional STARTTLS POP3", ComConnectionSecurity.StartTlsOptional),
                CreateSnapshot(40, 100, "Required STARTTLS POP3", ComConnectionSecurity.StartTlsRequired)
            });

        Assert.IsFalse(accounts[0].UseSSL);
        Assert.AreEqual(ComConnectionSecurity.None, accounts[0].ConnectionSecurity);
        Assert.IsTrue(accounts[1].UseSSL);
        Assert.AreEqual(ComConnectionSecurity.Tls, accounts[1].ConnectionSecurity);
        Assert.IsFalse(accounts[2].UseSSL);
        Assert.AreEqual(ComConnectionSecurity.StartTlsOptional, accounts[2].ConnectionSecurity);
        Assert.IsFalse(accounts[3].UseSSL);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, accounts[3].ConnectionSecurity);
    }

    [TestMethod]
    public void AuthorizedCollection_PreservesReadOnlyAndPendingMutationBoundaries()
    {
        IInterfaceFetchAccounts accounts = FetchAccounts.CreateAuthorized(
            new[] { CreateSnapshot(10, 100, "External POP3") });

        var pendingDelete = Assert.ThrowsExactly<COMException>(() => accounts.Delete(0));
        var pendingDeleteByDbId = Assert.ThrowsExactly<COMException>(() => accounts.DeleteByDBID(10));
        var pendingServerAddress = Assert.ThrowsExactly<COMException>(() => accounts[0].ServerAddress = "changed.example");
        var pendingPort = Assert.ThrowsExactly<COMException>(() => accounts[0].Port = 110);
        var pendingServerType = Assert.ThrowsExactly<COMException>(() => accounts[0].ServerType = 1);
        var pendingUsername = Assert.ThrowsExactly<COMException>(() => accounts[0].Username = "changed-user");
        var pendingPasswordWrite = Assert.ThrowsExactly<COMException>(() => accounts[0].Password = "secret");
        var pendingMinutes = Assert.ThrowsExactly<COMException>(() => accounts[0].MinutesBetweenFetch = 30);
        var pendingDays = Assert.ThrowsExactly<COMException>(() => accounts[0].DaysToKeepMessages = 30);
        var pendingAccountId = Assert.ThrowsExactly<COMException>(() => accounts[0].AccountID = 200);
        var pendingEnabled = Assert.ThrowsExactly<COMException>(() => accounts[0].Enabled = false);
        var pendingMimeRecipients = Assert.ThrowsExactly<COMException>(() => accounts[0].ProcessMIMERecipients = false);
        var pendingMimeDate = Assert.ThrowsExactly<COMException>(() => accounts[0].ProcessMIMEDate = false);
        var pendingUseSsl = Assert.ThrowsExactly<COMException>(() => accounts[0].UseSSL = false);
        var pendingSpam = Assert.ThrowsExactly<COMException>(() => accounts[0].UseAntiSpam = false);
        var pendingVirus = Assert.ThrowsExactly<COMException>(() => accounts[0].UseAntiVirus = false);
        var pendingRoutes = Assert.ThrowsExactly<COMException>(() => accounts[0].EnableRouteRecipients = false);
        var pendingSecurity = Assert.ThrowsExactly<COMException>(
            () => accounts[0].ConnectionSecurity = ComConnectionSecurity.None);
        var pendingHeaders = Assert.ThrowsExactly<COMException>(() => accounts[0].MIMERecipientHeaders = "To");
        var pendingSave = Assert.ThrowsExactly<COMException>(accounts[0].Save);
        var pendingDownload = Assert.ThrowsExactly<COMException>(accounts[0].DownloadNow);
        var pendingAccountDelete = Assert.ThrowsExactly<COMException>(accounts[0].Delete);

        foreach (var error in new[]
                 {
                     pendingDelete, pendingDeleteByDbId, pendingServerAddress, pendingPort, pendingServerType,
                     pendingUsername, pendingPasswordWrite, pendingMinutes, pendingDays, pendingAccountId,
                     pendingEnabled, pendingMimeRecipients, pendingMimeDate, pendingUseSsl, pendingSpam,
                     pendingVirus, pendingRoutes, pendingSecurity, pendingHeaders, pendingSave, pendingDownload,
                     pendingAccountDelete
                 })
        {
            Assert.AreEqual(ENotImplemented, error.ErrorCode);
        }
    }

    [TestMethod]
    public void AccountFetchAccounts_UsesConfiguredRuntimeForSelectedAccount()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[]
            {
                CreateSnapshot(10, 100, "External POP3"),
                CreateSnapshot(20, 200, "Outside POP3")
            });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var fetchAccounts = account.FetchAccounts;

        Assert.AreEqual(1, fetchAccounts.Count);
        Assert.AreEqual("External POP3", fetchAccounts[0].Name);

        store.Accounts =
        [
            CreateSnapshot(30, 100, "Updated POP3"),
            CreateSnapshot(40, 200, "Still Outside POP3")
        ];
        fetchAccounts.Refresh();

        Assert.AreEqual(1, fetchAccounts.Count);
        Assert.AreEqual("Updated POP3", fetchAccounts[0].Name);
        Assert.AreEqual(2, store.ReadCount);
    }

    private static FetchAccountAdministrationSnapshot CreateSnapshot(
        int id,
        int accountId,
        string name) =>
        CreateSnapshot(id, accountId, name, ComConnectionSecurity.Tls);

    private static FetchAccountAdministrationSnapshot CreateSnapshot(
        int id,
        int accountId,
        string name,
        ComConnectionSecurity connectionSecurity) =>
        new(
            Id: id,
            AccountId: accountId,
            Name: name,
            ServerAddress: "pop3.example.test",
            Port: 995,
            ServerType: 0,
            Username: "external-user",
            MinutesBetweenFetch: 15,
            DaysToKeepMessages: 14,
            Enabled: true,
            ProcessMimeRecipients: true,
            ProcessMimeDate: true,
            ConnectionSecurity: (int)connectionSecurity,
            UseAntiSpam: true,
            UseAntiVirus: true,
            EnableRouteRecipients: true,
            MimeRecipientHeaders: "To,CC,X-RCPT-TO",
            NextDownloadTime: "2026-07-01 02:03:04",
            IsLocked: true);

    private static void AssertFetchAccount(IInterfaceFetchAccount account, int id, string name)
    {
        Assert.AreEqual(id, account.ID);
        Assert.AreEqual(name, account.Name);
        Assert.AreEqual(100, account.AccountID);
        Assert.AreEqual("pop3.example.test", account.ServerAddress);
        Assert.AreEqual(995, account.Port);
        Assert.AreEqual(0, account.ServerType);
        Assert.AreEqual("external-user", account.Username);
        Assert.AreEqual(15, account.MinutesBetweenFetch);
        Assert.AreEqual(14, account.DaysToKeepMessages);
        Assert.IsTrue(account.Enabled);
        Assert.IsTrue(account.ProcessMIMERecipients);
        Assert.IsTrue(account.ProcessMIMEDate);
        Assert.IsTrue(account.UseSSL);
        Assert.AreEqual(ComConnectionSecurity.Tls, account.ConnectionSecurity);
        Assert.IsTrue(account.UseAntiSpam);
        Assert.IsTrue(account.UseAntiVirus);
        Assert.IsTrue(account.EnableRouteRecipients);
        Assert.AreEqual("To,CC,X-RCPT-TO", account.MIMERecipientHeaders);
        Assert.AreEqual("2026-07-01 02:03:04", account.NextDownloadTime);
        Assert.IsTrue(account.IsLocked);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
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

    private sealed class MutableFetchAccountAdministrationStore(
        IReadOnlyList<FetchAccountAdministrationSnapshot> accounts)
        : IFetchAccountAdministrationStore
    {
        public IReadOnlyList<FetchAccountAdministrationSnapshot> Accounts { get; set; } = accounts;

        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<FetchAccountAdministrationSnapshot>>(
                Accounts.Where(account => account.AccountId == accountId).ToArray());
        }
    }
}
