using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class AccountsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interface_PreservesLegacyIidDispatchIdsAndCompleteVtableOrder()
    {
        var contract = typeof(IInterfaceAccounts);

        Assert.AreEqual(new Guid("0AD49AE7-05ED-45F2-8D5A-68FC964EB7EA"), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);

        CollectionAssert.AreEqual(
            new[]
            {
                "get_Item", "get_Count", "Add", "Delete", "Refresh", "get_ItemByDBID",
                "get_ItemByAddress", "DeleteByDBID"
            },
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
        Assert.AreEqual(0, contract.GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(6, contract.GetMethod("get_ItemByAddress")?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClass_PreservesLegacyIdentityAndDefaultInterface()
    {
        var type = typeof(Accounts);

        Assert.AreEqual(new Guid("403A75B8-499A-44C1-93D3-6A8A460AA88D"), type.GUID);
        Assert.AreEqual("hMailServer.Accounts.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(typeof(IInterfaceAccounts), type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var accountsError = Assert.ThrowsExactly<COMException>(() => _ = new Accounts().Count);
        var accountError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Address);

        Assert.AreEqual(EAccessDenied, accountsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[]
            {
                new AccountAdministrationSnapshot(
                    10,
                    100,
                    "admin@example.test",
                    true,
                    2,
                    MaxSize: 2048,
                    PersonFirstName: "Ada",
                    PersonLastName: "Lovelace"),
                new AccountAdministrationSnapshot(
                    20,
                    100,
                    "user@example.test",
                    false,
                    0,
                    MaxSize: 1024,
                    PersonFirstName: "Grace",
                    PersonLastName: "Hopper")
            });

        Assert.AreEqual(2, accounts.Count);
        AssertAccount(accounts[0], 10, 100, "admin@example.test", true, ComAdminLevel.ServerAdministrator, 2048, "Ada", "Lovelace");
        AssertAccount(accounts.get_ItemByAddress("USER@EXAMPLE.TEST"), 20, 100, "user@example.test", false, ComAdminLevel.Normal, 1024, "Grace", "Hopper");
        AssertAccount(accounts.get_ItemByDBID(10), 10, 100, "admin@example.test", true, ComAdminLevel.ServerAdministrator, 2048, "Ada", "Lovelace");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = accounts[2]);
        var badAddress = Assert.ThrowsExactly<COMException>(() => _ = accounts.get_ItemByAddress("missing@example.test"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(accounts.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].Address = "renamed@example.test");
        var pendingCoreScalarMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].MaxSize = 4096);
        var pendingSensitiveRead = Assert.ThrowsExactly<COMException>(() => _ = accounts[0].Password);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badAddress.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingCoreScalarMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSensitiveRead.ErrorCode);
    }

    [TestMethod]
    public void DomainAccounts_UsesConfiguredRuntimeForSelectedDomain()
    {
        AccountAdministrationRuntimeHost.Configure(
            new FixedAccountAdministrationStore(
                new[]
                {
                    new AccountAdministrationSnapshot(10, 100, "admin@example.test", true, 2)
                }));
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));

        var accounts = domain.Accounts;

        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual("admin@example.test", accounts[0].Address);
    }

    private static void AssertAccount(
        IInterfaceAccount account,
        int id,
        int domainId,
        string address,
        bool active,
        ComAdminLevel adminLevel,
        int maxSize,
        string personFirstName,
        string personLastName)
    {
        Assert.AreEqual(id, account.ID);
        Assert.AreEqual(domainId, account.DomainID);
        Assert.AreEqual(address, account.Address);
        Assert.AreEqual(active, account.Active);
        Assert.AreEqual(adminLevel, account.AdminLevel);
        Assert.AreEqual(maxSize, account.MaxSize);
        Assert.AreEqual(personFirstName, account.PersonFirstName);
        Assert.AreEqual(personLastName, account.PersonLastName);
    }

    private sealed class FixedAccountAdministrationStore(IReadOnlyList<AccountAdministrationSnapshot> accounts)
        : IAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                accounts.Where(account => account.DomainId == domainId).ToArray());
    }
}
