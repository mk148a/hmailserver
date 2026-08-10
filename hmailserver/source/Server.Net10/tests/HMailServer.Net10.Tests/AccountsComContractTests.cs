using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;

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
        var idError = Assert.ThrowsExactly<COMException>(() => _ = new Account().ID);
        var adDomainError = Assert.ThrowsExactly<COMException>(() => _ = new Account().ADDomain);
        var isAdError = Assert.ThrowsExactly<COMException>(() => _ = new Account().IsAD);
        var adUsernameError = Assert.ThrowsExactly<COMException>(() => _ = new Account().ADUsername);
        var sizeError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Size);
        var quotaUsedError = Assert.ThrowsExactly<COMException>(() => _ = new Account().QuotaUsed);
        var lastLogonError = Assert.ThrowsExactly<COMException>(() => _ = new Account().LastLogonTime);
        var rulesError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Rules);
        var imapFoldersError = Assert.ThrowsExactly<COMException>(() => _ = new Account().IMAPFolders);
        var validatePasswordError = Assert.ThrowsExactly<COMException>(
            () => new Account().ValidatePassword("candidate-password"));
        var unlockMailboxError = Assert.ThrowsExactly<COMException>(new Account().UnlockMailbox);

        Assert.AreEqual(EAccessDenied, accountsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, refreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountError.ErrorCode);
        Assert.AreEqual(EAccessDenied, idError.ErrorCode);
        Assert.AreEqual(EAccessDenied, adDomainError.ErrorCode);
        Assert.AreEqual(EAccessDenied, isAdError.ErrorCode);
        Assert.AreEqual(EAccessDenied, adUsernameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, sizeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, quotaUsedError.ErrorCode);
        Assert.AreEqual(EAccessDenied, lastLogonError.ErrorCode);
        Assert.AreEqual(EAccessDenied, rulesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, imapFoldersError.ErrorCode);
        Assert.AreEqual(EAccessDenied, validatePasswordError.ErrorCode);
        Assert.AreEqual(EAccessDenied, unlockMailboxError.ErrorCode);
    }

    [TestMethod]
    public void ValidatePassword_UsesAuthorizedVerifierResultsAndOwningAccountId()
    {
        var calls = new List<(int AccountId, string Password)>();
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(10, 100, "account@example.test", true, 0)],
            passwordVerifier: (accountId, password) =>
            {
                calls.Add((accountId, password));
                return password == "accepted";
            });

        var account = accounts[0];

        Assert.IsTrue(account.ValidatePassword("accepted"));
        Assert.IsFalse(account.ValidatePassword("rejected"));
        CollectionAssert.AreEqual(
            new[] { (10, "accepted"), (10, "rejected") },
            calls);
    }

    [TestMethod]
    public void ValidatePassword_RuntimeHostPassesOwningIdAfterMutableFieldChanges()
    {
        var store = new FixedAccountAdministrationStore(
            [
                new AccountAdministrationSnapshot(10, 100, "first@example.test", true, 0),
                new AccountAdministrationSnapshot(20, 100, "second@example.test", true, 0)
            ]);
        var calls = new List<(int AccountId, string Password)>();
        AccountAdministrationRuntimeHost.Configure(
            store,
            passwordVerifier: (accountId, password) =>
            {
                calls.Add((accountId, password));
                return accountId == 20 && password == "accepted";
            });

        var account = AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(100).get_ItemByDBID(20);
        account.Address = "mutated@example.test";

        Assert.IsTrue(account.ValidatePassword("accepted"));
        Assert.AreEqual((20, "accepted"), calls.Single());
    }

    [TestMethod]
    public void ValidatePassword_RetainedAccountRechecksAuthenticationBeforeVerifier()
    {
        var authenticated = true;
        var verifierCalls = 0;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(10, 100, "account@example.test", true, 0)],
            isAuthenticated: () => authenticated,
            passwordVerifier: (_, _) =>
            {
                verifierCalls++;
                return true;
            });
        var account = accounts[0];

        authenticated = false;
        var denied = Assert.ThrowsExactly<COMException>(() => account.ValidatePassword("candidate"));

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        Assert.AreEqual(0, verifierCalls);

        authenticated = true;
        Assert.IsTrue(account.ValidatePassword("candidate"));
        Assert.AreEqual(1, verifierCalls);
    }

    [TestMethod]
    public void ValidatePassword_WithoutVerifierRemainsNotImplemented()
    {
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(10, 100, "account@example.test", true, 0)]);

        var error = Assert.ThrowsExactly<COMException>(() => accounts[0].ValidatePassword("candidate"));

        Assert.AreEqual(ENotImplemented, error.ErrorCode);
    }

    [TestMethod]
    public void AccountComContract_PreservesLegacyIdentityAndValidatePasswordDispId()
    {
        var type = typeof(Account);
        var contract = typeof(IInterfaceAccount);

        Assert.AreEqual(new Guid("369BE902-9F27-4722-A29F-3059E4D7021D"), type.GUID);
        Assert.AreEqual("hMailServer.Account.1", type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(new Guid("E5EDC050-0899-4A3B-BF4C-420212FC3895"), contract.GUID);
        Assert.AreEqual(22, contract.GetMethod(nameof(IInterfaceAccount.ValidatePassword))?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public async Task UnlockMailbox_AuthorizedAccountReleasesLockAndAllowsNoLockNoOp()
    {
        var store = new MutableAccountAdministrationStore(
            [new AccountAdministrationSnapshot(10, 100, "account@example.test", true, 0)]);
        var lockManager = new InMemoryPop3MailboxLockManager();
        AccountAdministrationRuntimeHost.Configure(store, lockManager.Unlock);

        var account = AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(100)[0];
        account.UnlockMailbox();

        var lease = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(10, "account@example.test"),
            CancellationToken.None);
        Assert.IsNotNull(lease);

        account.UnlockMailbox();
        var reacquiredLease = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(10, "account@example.test"),
            CancellationToken.None);
        Assert.IsNotNull(reacquiredLease);

        await lease.DisposeAsync();
        var stillLocked = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(10, "account@example.test"),
            CancellationToken.None);
        Assert.IsNull(stillLocked);

        await reacquiredLease.DisposeAsync();
        account.UnlockMailbox();
    }

    [TestMethod]
    public async Task UnlockMailbox_RetainedAccountRechecksAuthenticationBeforeRelease()
    {
        var authenticated = true;
        var lockManager = new InMemoryPop3MailboxLockManager();
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(10, 100, "account@example.test", true, 0)],
            isAuthenticated: () => authenticated,
            unlockMailbox: lockManager.Unlock);
        var account = accounts[0];
        var lease = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(10, "account@example.test"),
            CancellationToken.None);

        authenticated = false;
        var denied = Assert.ThrowsExactly<COMException>(account.UnlockMailbox);

        Assert.AreEqual(EAccessDenied, denied.ErrorCode);
        var stillLocked = await lockManager.TryAcquireAsync(
            new ImapAuthenticatedAccount(10, "account@example.test"),
            CancellationToken.None);
        Assert.IsNull(stillLocked);

        authenticated = true;
        account.UnlockMailbox();
        await lease!.DisposeAsync();
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
        accounts[0].Address = "renamed@example.test";
        accounts[0].MaxSize = 4096;
        var pendingSensitiveRead = Assert.ThrowsExactly<COMException>(() => _ = accounts[0].Password);
        var pendingPasswordValidation = Assert.ThrowsExactly<COMException>(
            () => accounts[0].ValidatePassword("candidate-password"));

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badAddress.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
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
    public void RetainedAccountSize_ReadsBackOnlyAfterInvalidation()
    {
        var store = new MutableAccountAdministrationStore(
            [new AccountAdministrationSnapshot(
                10,
                100,
                "old@example.test",
                true,
                0,
                MaxSize: 100,
                Size: 1.25f,
                QuotaUsed: 10)]);
        AccountAdministrationRuntimeHost.Configure(store);

        var account = AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(100)[0];
        Assert.AreEqual(1.25f, account.Size, 0.0001f);
        Assert.AreEqual(10, account.QuotaUsed);
        Assert.AreEqual(0, store.AccountReadCount);

        store.Accounts =
        [
            new AccountAdministrationSnapshot(
                10,
                100,
                "new@example.test",
                true,
                0,
                MaxSize: 200,
                Size: 3.5f,
                QuotaUsed: 35)
        ];

        Assert.AreEqual(1.25f, account.Size, 0.0001f);
        AccountAdministrationRuntimeHost.InvalidateAccountSize(10);

        Assert.AreEqual(3.5f, account.Size, 0.0001f);
        Assert.AreEqual(35, account.QuotaUsed);
        Assert.AreEqual("old@example.test", account.Address);
        Assert.AreEqual(100, account.MaxSize);
        Assert.AreEqual(1, store.AccountReadCount);
    }

    [TestMethod]
    public void AccountSizeInvalidator_OnlyMarksPositiveAccountIds()
    {
        var invalidator = new AccountSizeInvalidator();

        invalidator.Invalidate(0);
        invalidator.Invalidate(-1);

        Assert.AreEqual(0, invalidator.GetVersion(0));
        Assert.AreEqual(0, invalidator.GetVersion(-1));
        Assert.AreEqual(0, invalidator.GetVersion(10));

        invalidator.Register(10);
        invalidator.Invalidate(10);

        Assert.AreEqual(1, invalidator.GetVersion(10));
        Assert.AreEqual(0, invalidator.GetVersion(20));
    }

    [TestMethod]
    public void AccountsRefresh_RemovedAccountIdsNoLongerReceiveInvalidation()
    {
        var invalidator = new AccountSizeInvalidator();
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(10, 100, "account@example.test", true, 0)],
            () => [],
            accountSizeInvalidator: invalidator);

        accounts.Refresh();
        invalidator.Invalidate(10);

        Assert.AreEqual(0, invalidator.GetVersion(10));
    }

    [TestMethod]
    public void AccountsRefresh_ReaddedAccountGetsNewGenerationAndReadback()
    {
        var initial = new AccountAdministrationSnapshot(
            10,
            100,
            "account@example.test",
            true,
            0,
            Size: 1.25f,
            QuotaUsed: 10);
        var refreshed = initial with { Size = 3.5f, QuotaUsed = 35 };
        IReadOnlyList<AccountAdministrationSnapshot> current = [initial];
        var invalidator = new AccountSizeInvalidator();
        var readbackCount = 0;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [initial],
            () => current,
            accountSizeInvalidator: invalidator,
            accountSizeReadback: _ =>
            {
                readbackCount++;
                return refreshed;
            });
        var retainedAccount = accounts[0];

        Assert.AreEqual(1.25f, retainedAccount.Size, 0.0001f);
        current = [];
        accounts.Refresh();
        current = [refreshed];
        accounts.Refresh();

        Assert.AreEqual(1, invalidator.GetVersion(10));
        Assert.AreEqual(3.5f, retainedAccount.Size, 0.0001f);
        Assert.AreEqual(1, readbackCount);
    }

    [TestMethod]
    public void AccountsRefresh_RetainedAccountDoesNotForceSizeReadback()
    {
        var initial = new AccountAdministrationSnapshot(
            10,
            100,
            "account@example.test",
            true,
            0,
            Size: 1.25f,
            QuotaUsed: 10);
        var refreshed = initial with { Size = 3.5f, QuotaUsed = 35 };
        var invalidator = new AccountSizeInvalidator();
        var readbackCount = 0;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            [initial],
            () => [refreshed],
            accountSizeInvalidator: invalidator,
            accountSizeReadback: _ =>
            {
                readbackCount++;
                return refreshed;
            });
        var retainedAccount = accounts[0];

        accounts.Refresh();

        Assert.AreEqual(1.25f, retainedAccount.Size, 0.0001f);
        Assert.AreEqual(0, readbackCount);
    }

    [TestMethod]
    public void AccountsRefresh_DoesNotRemoveAnotherCollectionRegistration()
    {
        var invalidator = new AccountSizeInvalidator();
        IInterfaceAccounts first = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(10, 100, "first@example.test", true, 0)],
            () => [],
            accountSizeInvalidator: invalidator);
        _ = Accounts.CreateAuthorized(
            [new AccountAdministrationSnapshot(20, 100, "second@example.test", true, 0)],
            accountSizeInvalidator: invalidator);

        first.Refresh();
        invalidator.Invalidate(20);

        Assert.AreEqual(1, invalidator.GetVersion(20));
    }

    [TestMethod]
    public void RetainedAccountSize_MissingReadbackAccountIsNoOp()
    {
        var store = new MutableAccountAdministrationStore(
            [new AccountAdministrationSnapshot(
                10,
                100,
                "account@example.test",
                true,
                0,
                Size: 1.25f,
                QuotaUsed: 10)]);
        AccountAdministrationRuntimeHost.Configure(store);

        var account = AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(100)[0];
        store.Accounts = [];
        AccountAdministrationRuntimeHost.InvalidateAccountSize(10);

        Assert.AreEqual(1.25f, account.Size, 0.0001f);
        Assert.AreEqual(10, account.QuotaUsed);
        Assert.AreEqual(1, store.AccountReadCount);
    }

    [TestMethod]
    public void RetainedAccountSize_ReadbackFailureUsesComFailureBoundary()
    {
        var store = new MutableAccountAdministrationStore(
            [new AccountAdministrationSnapshot(
                10,
                100,
                "account@example.test",
                true,
                0,
                Size: 1.25f,
                QuotaUsed: 10)]);
        AccountAdministrationRuntimeHost.Configure(store);

        var account = AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(100)[0];
        store.AccountReadException = new InvalidOperationException("readback failed");
        AccountAdministrationRuntimeHost.InvalidateAccountSize(10);

        var exception = Assert.ThrowsExactly<COMException>(() => _ = account.Size);
        Assert.AreEqual(unchecked((int)0x80004005), exception.ErrorCode);
    }

    [TestMethod]
    public void RetainedAccountSize_SerializesConcurrentReadback()
    {
        var store = new MutableAccountAdministrationStore(
            [new AccountAdministrationSnapshot(
                10,
                100,
                "account@example.test",
                true,
                0,
                Size: 1.25f,
                QuotaUsed: 10)]);
        var firstReadbackEntered = new ManualResetEventSlim();
        var releaseFirstReadback = new ManualResetEventSlim();
        var readbackCount = 0;
        store.AccountReadOverride = _ =>
        {
            var call = Interlocked.Increment(ref readbackCount);
            if (call == 1)
            {
                firstReadbackEntered.Set();
                releaseFirstReadback.Wait();
            }

            return new AccountAdministrationSnapshot(
                10,
                100,
                "account@example.test",
                true,
                0,
                Size: call,
                QuotaUsed: call);
        };
        AccountAdministrationRuntimeHost.Configure(store);

        var account = AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(100)[0];
        AccountAdministrationRuntimeHost.InvalidateAccountSize(10);
        var first = Task.Run(() => account.Size);
        Assert.IsTrue(firstReadbackEntered.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => account.Size);

        try
        {
            Thread.Sleep(100);
            Assert.AreEqual(1, readbackCount);
        }
        finally
        {
            releaseFirstReadback.Set();
        }

        Task.WaitAll(first, second);
        Assert.AreEqual(1f, first.Result);
        Assert.AreEqual(1f, second.Result);
        Assert.AreEqual(1, readbackCount);
    }

    [TestMethod]
    public void AccountSizeInvalidationCallback_IsNonThrowing()
    {
        try
        {
            AccountAdministrationRuntimeHost.InvalidateAccountSize(0);
            AccountAdministrationRuntimeHost.InvalidateAccountSize(-10);
            AccountAdministrationRuntimeHost.InvalidateAccountSize(10);
        }
        catch (Exception exception)
        {
            Assert.Fail($"Account-size invalidation callback threw: {exception}");
        }
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

    [TestMethod]
    public void AccountImapFolders_RefreshRetainsLegacyAccountScopedStateForOldAndNewWrappers()
    {
        var folderStore = new MutableImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(100, 10, -1, "Old", true, 1, "2026-07-01 01:02:03")
            });
        ImapFolderAdministrationRuntimeHost.Configure(folderStore);

        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "old@example.test", true, 0) },
            () => new[] { new AccountAdministrationSnapshot(10, 100, "new@example.test", true, 0) });

        var oldAccount = accounts[0];
        var oldFolders = oldAccount.IMAPFolders;
        var sameEntryAccount = accounts.get_ItemByDBID(10);
        var sameEntryFolders = sameEntryAccount.IMAPFolders;

        Assert.AreNotSame(oldAccount, sameEntryAccount);
        Assert.AreNotSame(oldFolders, sameEntryFolders);
        Assert.AreEqual("Old", oldFolders[0].Name);
        Assert.AreEqual("Old", sameEntryFolders[0].Name);
        Assert.AreEqual(1, folderStore.ReadCount);

        folderStore.Folders =
        [
            new ImapFolderAdministrationSnapshot(200, 10, -1, "New", true, 2, "2026-07-02 01:02:03")
        ];
        accounts.Refresh();

        var newAccount = accounts[0];
        var newFolders = newAccount.IMAPFolders;

        Assert.AreNotSame(oldAccount, newAccount);
        Assert.AreNotSame(oldFolders, newFolders);
        Assert.AreEqual("Old", oldAccount.IMAPFolders[0].Name);
        Assert.AreEqual("Old", sameEntryFolders[0].Name);
        Assert.AreEqual("Old", newFolders[0].Name);
        Assert.AreEqual(1, folderStore.ReadCount);
    }

    [TestMethod]
    public void AuthenticatedApplication_TraversesAccountImapSubFoldersWithSharedAccountScopedState()
    {
        var folderStore = new MutableImapFolderAdministrationStore(
            new[]
            {
                new ImapFolderAdministrationSnapshot(100, 10, -1, "Projects", true, 1, "2026-07-01 01:02:03"),
                new ImapFolderAdministrationSnapshot(101, 10, 100, "2026", true, 2, "2026-07-01 01:02:04"),
                new ImapFolderAdministrationSnapshot(200, 20, -1, "OtherAccountOnly", true, 1, "2026-07-01 01:02:05")
            });
        ImapFolderAdministrationRuntimeHost.Configure(folderStore);
        AccountAdministrationRuntimeHost.Configure(new FixedAccountAdministrationStore(
            new[]
            {
                new AccountAdministrationSnapshot(10, 100, "target@example.test", true, 0),
                new AccountAdministrationSnapshot(20, 100, "other@example.test", true, 0)
            }));
        DomainAdministrationRuntimeHost.Configure(new FixedDomainAdministrationStore(
            new[] { new DomainAdministrationSnapshot(100, "example.test", true) }));

        var application = new Application(new FixedAdministratorAuthenticationProvider("secret"));
        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));

        var accounts = application.Domains[0].Accounts;
        var target = accounts.get_ItemByAddress("TARGET@EXAMPLE.TEST");
        var targetRoot = target.IMAPFolders.get_ItemByName("Projects");
        var targetChild = targetRoot.SubFolders.get_ItemByName("2026");

        var freshAccounts = application.Domains[0].Accounts;
        var freshTarget = freshAccounts.get_ItemByAddress("target@example.test");
        var freshChild = freshTarget.IMAPFolders.get_ItemByName("Projects").SubFolders[0];

        Assert.AreNotSame(target, freshTarget);
        Assert.AreEqual(1, target.IMAPFolders.Count);
        Assert.AreEqual(101, targetChild.ID);
        Assert.AreEqual("2026", targetChild.Name);
        Assert.AreEqual(101, freshChild.ID);
        Assert.AreEqual("2026", freshChild.Name);
        Assert.AreEqual(1, folderStore.ReadCount);
    }

    [TestMethod]
    public void AddStagesDraftAndSavePublishesInsertedIdentity()
    {
        AccountAdministrationSnapshot? inserted = null;
        string? insertedPassword = null;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2) },
            domainId: 100,
            insert: (account, password) =>
            {
                inserted = account;
                insertedPassword = password;
                return 20;
            });

        var draft = accounts.Add();

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(string.Empty, draft.Address);
        Assert.AreEqual(100, draft.DomainID);
        Assert.AreEqual(ComAdminLevel.Normal, draft.AdminLevel);
        Assert.IsFalse(draft.Active);

        draft.Address = "beta@example.test";
        draft.Password = "secret";
        draft.Active = true;
        draft.AdminLevel = ComAdminLevel.DomainAdministrator;
        draft.MaxSize = 1024;
        draft.VacationMessageIsOn = true;
        draft.VacationMessage = "away";
        draft.ForwardEnabled = true;
        draft.ForwardAddress = "fwd@example.test";
        draft.SignatureEnabled = true;
        draft.SignaturePlainText = "sig";

        Assert.AreEqual(1, accounts.Count);
        draft.Save();

        Assert.AreEqual(2, accounts.Count);
        Assert.AreEqual(20, draft.ID);
        Assert.IsNotNull(inserted);
        Assert.AreEqual(0, inserted.Id);
        Assert.AreEqual(100, inserted.DomainId);
        Assert.AreEqual("beta@example.test", inserted.Address);
        Assert.IsTrue(inserted.Active);
        Assert.AreEqual((int)ComAdminLevel.DomainAdministrator, inserted.AdminLevel);
        Assert.AreEqual(1024, inserted.MaxSize);
        Assert.IsTrue(inserted.VacationMessageIsOn);
        Assert.IsTrue(inserted.ForwardEnabled);
        Assert.IsTrue(inserted.SignatureEnabled);
        Assert.AreEqual("secret", insertedPassword);
        Assert.AreEqual("beta@example.test", accounts.get_ItemByDBID(20).Address);
    }

    [TestMethod]
    public void FailedInsert_MapsToEFailAndRetainsDraftWithoutPublishing()
    {
        var fail = true;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            Array.Empty<AccountAdministrationSnapshot>(),
            domainId: 100,
            insert: (_, _) => fail
                ? throw new InvalidOperationException("Simulated store failure.")
                : 1);

        var draft = accounts.Add();
        draft.Address = "beta@example.test";

        var saveFailure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual(0, accounts.Count);
        Assert.AreEqual(0, draft.ID);

        draft.Address = "gamma@example.test";
        fail = false;
        draft.Save();

        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual(1, draft.ID);
        Assert.AreEqual("gamma@example.test", accounts.get_ItemByDBID(1).Address);
    }

    [TestMethod]
    public void AddAndSave_RecheckLiveAuthentication()
    {
        var authenticated = true;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2) },
            domainId: 100,
            insert: (_, _) => 11,
            isAuthenticated: () => authenticated);

        var draft = accounts.Add();
        draft.Address = "beta@example.test";
        authenticated = false;

        var deniedAdd = Assert.ThrowsExactly<COMException>(() => accounts.Add());
        var deniedSave = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80070005), deniedAdd.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80070005), deniedSave.ErrorCode);
    }
    [TestMethod]
    public void DeleteByDBID_RemovesOnlyMatchingSnapshotAndTreatsUnknownAsNoOp()
    {
        var deletedIds = new List<int>();
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[]
            {
                new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2),
                new AccountAdministrationSnapshot(20, 100, "beta@example.test", false, 0)
            },
            domainId: 100,
            delete: accountId =>
            {
                deletedIds.Add(accountId);
                return true;
            });

        accounts.DeleteByDBID(10);

        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual(20, accounts[0].ID);

        accounts.DeleteByDBID(999);
        Assert.AreEqual(1, accounts.Count);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
    }

    [TestMethod]
    public void FailedDelete_MapsToEFailAndRetainsSnapshot()
    {
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2) },
            domainId: 100,
            delete: _ => false);

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => accounts.DeleteByDBID(10));

        Assert.AreEqual(unchecked((int)0x80004005), deleteFailure.ErrorCode);
        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual("alpha@example.test", accounts[0].Address);
    }

    [TestMethod]
    public void ItemDelete_RoutesThroughOwningCollectionAndRechecksAuthentication()
    {
        var deletedIds = new List<int>();
        var authenticated = true;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2) },
            domainId: 100,
            delete: accountId =>
            {
                deletedIds.Add(accountId);
                return true;
            },
            isAuthenticated: () => authenticated);

        accounts[0].Delete();

        Assert.AreEqual(0, accounts.Count);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);

        var second = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(20, 100, "beta@example.test", true, 2) },
            domainId: 100,
            insert: (_, _) => 30,
            delete: _ => true,
            isAuthenticated: () => authenticated);
        var draft = second.Add();
        draft.Delete();

        authenticated = false;
        var deniedDelete = Assert.ThrowsExactly<COMException>(() => second[0].Delete());
        Assert.AreEqual(unchecked((int)0x80070005), deniedDelete.ErrorCode);
    }

    [TestMethod]
    public void DeleteWithoutConfiguredDelegate_RemainsNotImplemented()
    {
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[] { new AccountAdministrationSnapshot(10, 100, "alpha@example.test", true, 2) });

        var pendingCollectionDelete = Assert.ThrowsExactly<COMException>(() => accounts.DeleteByDBID(10));
        var pendingItemDelete = Assert.ThrowsExactly<COMException>(accounts[0].Delete);

        Assert.AreEqual(unchecked((int)0x80004001), pendingCollectionDelete.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80004001), pendingItemDelete.ErrorCode);
    }

    [TestMethod]
    public void ExistingRowSave_PersistsStagedSettersAndReplacesCollectionSnapshot()
    {
        AccountAdministrationSnapshot? updated = null;
        string? updatedPassword = null;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[]
            {
                new AccountAdministrationSnapshot(
                    10, 100, "admin@example.test", true, 2,
                    PersonFirstName: "Ada", PersonLastName: "Lovelace")
            },
            domainId: 100,
            update: (account, password) =>
            {
                updated = account;
                updatedPassword = password;
                return true;
            });

        var existing = accounts[0];
        existing.Address = "renamed@example.test";
        existing.Active = false;
        existing.MaxSize = 2048;
        existing.PersonFirstName = "Grace";
        existing.PersonLastName = "Hopper";

        existing.Save();

        Assert.IsNotNull(updated);
        Assert.AreEqual(10, updated.Id);
        Assert.AreEqual(100, updated.DomainId);
        Assert.AreEqual("renamed@example.test", updated.Address);
        Assert.IsFalse(updated.Active);
        Assert.AreEqual(2048, updated.MaxSize);
        Assert.AreEqual("Grace", updated.PersonFirstName);
        Assert.AreEqual("Hopper", updated.PersonLastName);
        Assert.IsNull(updatedPassword);
        Assert.AreEqual("renamed@example.test", accounts.get_ItemByDBID(10).Address);
    }

    [TestMethod]
    public void FailedUpdate_MapsToEFailAndRetainsStagedStateWithoutReplacingSnapshot()
    {
        var failUpdate = true;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[]
            {
                new AccountAdministrationSnapshot(10, 100, "admin@example.test", true, 2)
            },
            domainId: 100,
            update: (_, _) => failUpdate
                ? throw new InvalidOperationException("Simulated store failure.")
                : true);

        var existing = accounts[0];
        existing.Address = "changed@example.test";

        var saveFailure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual("admin@example.test", accounts[0].Address);

        existing.Address = "other@example.test";
        failUpdate = false;
        existing.Save();

        Assert.AreEqual("other@example.test", accounts[0].Address);
    }

    [TestMethod]
    public void ExistingRowSave_WithoutUpdateDelegate_RemainsNotImplemented()
    {
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[]
            {
                new AccountAdministrationSnapshot(10, 100, "admin@example.test", true, 2)
            },
            domainId: 100);

        var pendingSave = Assert.ThrowsExactly<COMException>(accounts[0].Save);

        Assert.AreEqual(unchecked((int)0x80004001), pendingSave.ErrorCode);
    }

    [TestMethod]
    public void ExistingRowPasswordSetter_MarksPasswordForUpdate()
    {
        string? updatedPassword = null;
        IInterfaceAccounts accounts = Accounts.CreateAuthorized(
            new[]
            {
                new AccountAdministrationSnapshot(10, 100, "admin@example.test", true, 2)
            },
            domainId: 100,
            update: (_, password) =>
            {
                updatedPassword = password;
                return true;
            });

        var existing = accounts[0];
        existing.Password = "new-secret";
        existing.Save();

        Assert.AreEqual("new-secret", updatedPassword);
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

        public int AccountReadCount { get; private set; }

        public Exception? AccountReadException { get; set; }

        public Func<int, AccountAdministrationSnapshot?>? AccountReadOverride { get; set; }

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
            CancellationToken cancellationToken)
        {
            AccountReadCount++;
            if (AccountReadException is not null)
            {
                throw AccountReadException;
            }

            if (AccountReadOverride is not null)
            {
                return ValueTask.FromResult(AccountReadOverride(accountId));
            }

            return ValueTask.FromResult(Accounts.FirstOrDefault(account => account.Id == accountId));
        }
    }

    private sealed class FixedAccountAdministrationStore(IReadOnlyList<AccountAdministrationSnapshot> accounts)
        : IAccountAdministrationStore
    {
        public ValueTask<IReadOnlyList<AccountAdministrationSnapshot>> GetAccountsAsync(
            int domainId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AccountAdministrationSnapshot>>(
                accounts.Where(account => account.DomainId == domainId).ToArray());

        public ValueTask<AccountAdministrationSnapshot?> GetAccountByIdAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(accounts.FirstOrDefault(account => account.Id == accountId));
    }

    private sealed class FixedDomainAdministrationStore(IReadOnlyList<DomainAdministrationSnapshot> domains)
        : IDomainAdministrationStore
    {
        public ValueTask<IReadOnlyList<DomainAdministrationSnapshot>> GetDomainsAsync(
            CancellationToken cancellationToken) => ValueTask.FromResult(domains);
    }

    private sealed class FixedAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            username.Equals("Administrator", StringComparison.OrdinalIgnoreCase)
            && attemptedPassword == password;
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
            int accountId,
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(Array.Empty<MessageAdministrationSnapshot>());
    }

    private sealed class MutableImapFolderAdministrationStore(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders) : IImapFolderAdministrationStore
    {
        public IReadOnlyList<ImapFolderAdministrationSnapshot> Folders { get; set; } = folders;

        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetFoldersForAccountAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                Folders.Where(folder => folder.AccountId == accountId).OrderBy(folder => folder.Id).ToArray());
        }

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetRootFoldersAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                Folders.Where(folder => folder.AccountId == accountId && folder.ParentId == -1)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderAdministrationSnapshot>> GetChildFoldersAsync(
            int parentFolderId,
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderAdministrationSnapshot>>(
                Folders.Where(folder => folder.AccountId == accountId && folder.ParentId == parentFolderId)
                    .OrderBy(folder => folder.Id)
                    .ToArray());

        public ValueTask<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>> GetFolderPermissionsAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ImapFolderPermissionAdministrationSnapshot>>(
                Array.Empty<ImapFolderPermissionAdministrationSnapshot>());
    }
}
