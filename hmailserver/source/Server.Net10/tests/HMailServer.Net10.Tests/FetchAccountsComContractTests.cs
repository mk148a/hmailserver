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
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var accountsError = Assert.ThrowsExactly<COMException>(() => _ = new FetchAccounts().Count);
        var accountError = Assert.ThrowsExactly<COMException>(() => _ = new FetchAccount().Name);

        Assert.AreEqual(EAccessDenied, accountsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountError.ErrorCode);
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
    public void AccountFetchAccounts_UsesConfiguredRuntimeForSelectedAccount()
    {
        FetchAccountAdministrationRuntimeHost.Configure(
            new FixedFetchAccountAdministrationStore(
                new[]
                {
                    CreateSnapshot(10, 100, "External POP3"),
                    CreateSnapshot(20, 200, "Outside POP3")
                }));
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var fetchAccounts = account.FetchAccounts;

        Assert.AreEqual(1, fetchAccounts.Count);
        Assert.AreEqual("External POP3", fetchAccounts[0].Name);
    }

    private static FetchAccountAdministrationSnapshot CreateSnapshot(
        int id,
        int accountId,
        string name) =>
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
            ConnectionSecurity: (int)ComConnectionSecurity.Tls,
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

    private sealed class FixedFetchAccountAdministrationStore(
        IReadOnlyList<FetchAccountAdministrationSnapshot> accounts)
        : IFetchAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<FetchAccountAdministrationSnapshot>>(
                accounts.Where(account => account.AccountId == accountId).ToArray());
    }
}
