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
        var refreshError = Assert.ThrowsExactly<COMException>(new Accounts().Refresh);
        var accountError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Address);
        var adDomainError = Assert.ThrowsExactly<COMException>(() => _ = new Account().ADDomain);
        var isAdError = Assert.ThrowsExactly<COMException>(() => _ = new Account().IsAD);
        var adUsernameError = Assert.ThrowsExactly<COMException>(() => _ = new Account().ADUsername);
        var sizeError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Size);
        var quotaUsedError = Assert.ThrowsExactly<COMException>(() => _ = new Account().QuotaUsed);
        var lastLogonError = Assert.ThrowsExactly<COMException>(() => _ = new Account().LastLogonTime);
        var rulesError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Rules);
        var validatePasswordError = Assert.ThrowsExactly<COMException>(
            () => new Account().ValidatePassword("candidate-password"));

        Assert.AreEqual(EAccessDenied, accountsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, refreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountError.ErrorCode);
        Assert.AreEqual(EAccessDenied, adDomainError.ErrorCode);
        Assert.AreEqual(EAccessDenied, isAdError.ErrorCode);
        Assert.AreEqual(EAccessDenied, adUsernameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, sizeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, quotaUsedError.ErrorCode);
        Assert.AreEqual(EAccessDenied, lastLogonError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rulesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, validatePasswordError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var refreshed = new[]
        {
            new AccountAdministrationSnapshot(20, 100, "beta@example.test", false, 0),
            new AccountAdministrationSnapshot(30, 100, "gamma@example.test", true, 1)
        };
        var failRefresh = false;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2) },
            () => failRefresh
                ? throw new InvalidOperationException("store failed")
                : refreshed);

        accounts.Refresh();

        Assert.AreEqual(2, accounts.Count);
        Assert.AreEqual("beta@example.test", accounts[0].Address);
        Assert.AreEqual("gamma@example.test", accounts.get_ItemByDBID(30).Address);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => accounts.get_ItemByDBID(10)).ErrorCode);

        failRefresh = true;
        var failure = Assert.ThrowsExactly<COMException>(accounts.Refresh);

        Assert.AreEqual(unchecked((int)0x80004005), failure.ErrorCode);
        Assert.AreEqual(2, accounts.Count);
        Assert.AreEqual("beta@example.test", accounts[0].Address);
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
                    IsActiveDirectoryAccount: true,
                    ActiveDirectoryDomain: "corp.example.test",
                    ActiveDirectoryUsername: "ada.lovelace",
                    MaxSize: 2,
                    Size: 2.5f,
                    QuotaUsed: 125,
                    LastLogonTime: new DateTime(2026, 3, 4, 5, 6, 7),
                    PersonFirstName: "Ada",
                    PersonLastName: "Lovelace",
                    VacationMessageIsOn: true,
                    VacationMessage: "Away until Monday",
                    VacationSubject: "Auto reply",
                    VacationMessageExpires: true,
                    VacationMessageExpiresDate: "2026-12-31",
                    VacationMessageAbortSpamFlagged: true,
                    ForwardEnabled: true,
                    ForwardAddress: "archive@example.test",
                    ForwardKeepOriginal: true,
                    ForwardAbortSpamFlagged: true,
                    SignatureEnabled: true,
                    SignaturePlainText: "Regards,\r\nAda",
                    SignatureHtml: "<p>Regards,<br>Ada</p>"),
                new AccountAdministrationSnapshot(
                    20,
                    100,
                    "user@example.test",
                    false,
                    0,
                    IsActiveDirectoryAccount: false,
                    ActiveDirectoryDomain: "",
                    ActiveDirectoryUsername: "",
                    MaxSize: 0,
                    Size: 0.125f,
                    QuotaUsed: 0,
                    LastLogonTime: new DateTime(2026, 2, 3, 4, 5, 6),
                    PersonFirstName: "Grace",
                    PersonLastName: "Hopper")
            });

        Assert.AreEqual(2, accounts.Count);
        AssertAccount(accounts[0], 10, 100, "admin@example.test", true, ComAdminLevel.ServerAdministrator, true, "corp.example.test", "ada.lovelace", 2, 2.5f, 125, new DateTime(2026, 3, 4, 5, 6, 7), "Ada", "Lovelace");
        AssertAccount(accounts.get_ItemByAddress("USER@EXAMPLE.TEST"), 20, 100, "user@example.test", false, ComAdminLevel.Normal, false, "", "", 0, 0.125f, 0, new DateTime(2026, 2, 3, 4, 5, 6), "Grace", "Hopper");
        AssertAccount(accounts.get_ItemByDBID(10), 10, 100, "admin@example.test", true, ComAdminLevel.ServerAdministrator, true, "corp.example.test", "ada.lovelace", 2, 2.5f, 125, new DateTime(2026, 3, 4, 5, 6, 7), "Ada", "Lovelace");
        AssertAccountDeliveryDetailScalars(accounts[0]);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = accounts[2]);
        var badAddress = Assert.ThrowsExactly<COMException>(() => _ = accounts.get_ItemByAddress("missing@example.test"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(accounts.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].Address = "renamed@example.test");
        var pendingAdFlagMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].IsAD = false);
        var pendingAdDomainMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].ADDomain = "other.example.test");
        var pendingAdUsernameMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].ADUsername = "other-user");
        var pendingCoreScalarMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].MaxSize = 4096);
        var pendingDeliveryScalarMutation = Assert.ThrowsExactly<COMException>(() => accounts[0].ForwardEnabled = false);
        var pendingSensitiveRead = Assert.ThrowsExactly<COMException>(() => _ = accounts[0].Password);
        var pendingPasswordValidation = Assert.ThrowsExactly<COMException>(
            () => accounts[0].ValidatePassword("candidate-password"));

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badAddress.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdFlagMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdDomainMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdUsernameMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingCoreScalarMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDeliveryScalarMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSensitiveRead.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingPasswordValidation.ErrorCode);
    }

    [TestMethod]
    public void DomainAccounts_UsesConfiguredRuntimeForSelectedDomain()
    {
        var store = new MutableAccountAdministrationStore(
            new[]
            {
                new AccountAdministrationSnapshot(10, 100, "admin@example.test", true, 2),
                new AccountAdministrationSnapshot(20, 200, "outside@other.test", true, 0)
            });
        AccountAdministrationRuntimeHost.Configure(store);
        var domain = Domain.CreateAuthorized(new DomainAdministrationSnapshot(100, "example.test", true));

        var accounts = domain.Accounts;

        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual("admin@example.test", accounts[0].Address);

        store.Accounts =
        [
            new AccountAdministrationSnapshot(30, 100, "user@example.test", false, 0),
            new AccountAdministrationSnapshot(40, 200, "still-outside@other.test", true, 0)
        ];
        accounts.Refresh();

        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual("user@example.test", accounts[0].Address);
        Assert.AreEqual(2, store.ReadCount);
    }

    [TestMethod]
    public void AccountWrappersFromOneSnapshotShareRulesAndRefreshCreatesNewState()
    {
        var account = new AccountAdministrationSnapshot(10, 100, "old@example.test", true, 0);
        var refreshedAccount = new AccountAdministrationSnapshot(10, 100, "new@example.test", true, 0);
        var failRefresh = false;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { account },
            () => failRefresh
                ? throw new InvalidOperationException("store failed")
                : new[] { refreshedAccount });
        var rulesStore = new MutableRuleAdministrationStore(
            new[] { new RuleAdministrationSnapshot(1, 10, "initial", true, true, 1) });
        RuleAdministrationRuntimeHost.Configure(rulesStore);

        var oldAccount = accounts[0];
        var sameAccount = accounts.get_ItemByDBID(10);
        var oldRules = oldAccount.Rules;
        var sameRules = sameAccount.Rules;

        Assert.AreNotSame(oldAccount, sameAccount);
        Assert.AreNotSame(oldRules, sameRules);
        Assert.AreEqual(1, rulesStore.ReadCount);

        rulesStore.Rules = [new RuleAdministrationSnapshot(2, 10, "shared refresh", true, true, 1)];
        sameRules.Refresh();
        Assert.AreEqual("shared refresh", oldRules[0].Name);
        Assert.AreEqual(2, rulesStore.ReadCount);

        rulesStore.Rules = [new RuleAdministrationSnapshot(3, 10, "new snapshot", true, true, 1)];
        accounts.Refresh();

        var newAccount = accounts[0];
        var newRules = newAccount.Rules;
        Assert.AreEqual("new@example.test", newAccount.Address);
        Assert.AreEqual("new snapshot", newRules[0].Name);
        Assert.AreEqual("shared refresh", oldRules[0].Name);
        Assert.AreEqual(3, rulesStore.ReadCount);
    }

    [TestMethod]
    public void AccountMessages_RefreshPublishesNewStateAndOldWrappersRetainTheirSnapshot()
    {
        var messageStore = new MutableMessageAdministrationStore(
            new[] { MessageSnapshot(1000, "old.eml") });
        MessageAdministrationRuntimeHost.Configure(messageStore);

        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "old@example.test", true, 0) },
            () => new[] { new AccountAdministrationSnapshot(10, 100, "new@example.test", true, 0) });

        var oldAccount = accounts[0];
        var oldMessages = oldAccount.Messages;
        var sameEntryAccount = accounts.get_ItemByDBID(10);
        var sameEntryMessages = sameEntryAccount.Messages;

        Assert.AreNotSame(oldAccount, sameEntryAccount);
        Assert.AreNotSame(oldMessages, sameEntryMessages);
        Assert.AreEqual("old.eml", oldMessages[0].Filename);
        Assert.AreEqual("old.eml", sameEntryMessages[0].Filename);
        Assert.AreEqual(1, messageStore.AccountReadCount);

        messageStore.Messages = new[] { MessageSnapshot(2000, "new.eml") };
        accounts.Refresh();

        var newAccount = accounts[0];
        var newMessages = newAccount.Messages;

        Assert.AreNotSame(oldAccount, newAccount);
        Assert.AreNotSame(oldMessages, newMessages);
        Assert.AreEqual("old.eml", oldAccount.Messages[0].Filename);
        Assert.AreEqual("new.eml", newMessages[0].Filename);
        Assert.AreEqual(2, messageStore.AccountReadCount);
    }

    private static void AssertAccount(
        IInterfaceAccount account,
        int id,
        int domainId,
        string address,
        bool active,
        ComAdminLevel adminLevel,
        bool isActiveDirectoryAccount,
        string activeDirectoryDomain,
        string activeDirectoryUsername,
        int maxSize,
        float size,
        int quotaUsed,
        DateTime lastLogonTime,
        string personFirstName,
        string personLastName)
    {
        Assert.AreEqual(id, account.ID);
        Assert.AreEqual(domainId, account.DomainID);
        Assert.AreEqual(address, account.Address);
        Assert.AreEqual(active, account.Active);
        Assert.AreEqual(adminLevel, account.AdminLevel);
        Assert.AreEqual(isActiveDirectoryAccount, account.IsAD);
        Assert.AreEqual(activeDirectoryDomain, account.ADDomain);
        Assert.AreEqual(activeDirectoryUsername, account.ADUsername);
        Assert.AreEqual(maxSize, account.MaxSize);
        Assert.AreEqual(size, account.Size, 0.0001f);
        Assert.AreEqual(quotaUsed, account.QuotaUsed);
        Assert.AreEqual(lastLogonTime, account.LastLogonTime);
        Assert.AreEqual(personFirstName, account.PersonFirstName);
        Assert.AreEqual(personLastName, account.PersonLastName);
    }

    private static MessageAdministrationSnapshot MessageSnapshot(long id, string fileName) =>
        new(id, 10, 50, fileName, 2, "sender@example.test", 1024, 0, 1, new DateTime(2026, 7, 1), id);

    private static void AssertAccountDeliveryDetailScalars(IInterfaceAccount account)
    {
        Assert.IsTrue(account.VacationMessageIsOn);
        Assert.AreEqual("Away until Monday", account.VacationMessage);
        Assert.AreEqual("Auto reply", account.VacationSubject);
        Assert.IsTrue(account.VacationMessageExpires);
        Assert.AreEqual("2026-12-31", account.VacationMessageExpiresDate);
        Assert.IsTrue(account.VacationMessageAbortSpamFlagged);
        Assert.IsTrue(account.ForwardEnabled);
        Assert.AreEqual("archive@example.test", account.ForwardAddress);
        Assert.IsTrue(account.ForwardKeepOriginal);
        Assert.IsTrue(account.ForwardAbortSpamFlagged);
        Assert.IsTrue(account.SignatureEnabled);
        Assert.AreEqual("Regards,\r\nAda", account.SignaturePlainText);
        Assert.AreEqual("<p>Regards,<br>Ada</p>", account.SignatureHTML);
    }

    private sealed class MutableAccountAdministrationStore(IReadOnlyList<AccountAdministrationSnapshot> accounts)
        : IAccountAdministrationStore
    {
        public IReadOnlyList<AccountAdministrationSnapshot> Accounts { get; set; } = accounts;

        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                Accounts.Where(account => account.DomainId == domainId).ToArray());
        }

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Accounts.FirstOrDefault(account => account.Id == accountId));
    }

    private sealed class MutableRuleAdministrationStore(IReadOnlyList<RuleAdministrationSnapshot> rules)
        : IRuleAdministrationStore
    {
        public IReadOnlyList<RuleAdministrationSnapshot> Rules { get; set; } = rules;

        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<RuleAdministrationSnapshot>>(
                Rules.Where(rule => rule.AccountId == accountId).OrderBy(rule => rule.SortOrder).ToArray());
        }
    }

    private sealed class MutableMessageAdministrationStore(IReadOnlyList<MessageAdministrationSnapshot> messages)
        : IMessageAdministrationStore
    {
        public IReadOnlyList<MessageAdministrationSnapshot> Messages { get; set; } = messages;

        public int AccountReadCount { get; private set; }

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            AccountReadCount++;
            return ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                Messages.Where(message => message.AccountId == accountId).OrderBy(message => message.Id).ToArray());
        }

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(Array.Empty<MessageAdministrationSnapshot>());
    }
}
