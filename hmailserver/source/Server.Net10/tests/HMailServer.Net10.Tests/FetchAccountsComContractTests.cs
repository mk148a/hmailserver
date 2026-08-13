using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class FetchAccountsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
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
    public void RegisteredFetchAccountActivation_DeniesEveryMemberBeforeSnapshotAccess()
    {
        var account = (IInterfaceFetchAccount)Activator.CreateInstance(typeof(FetchAccount))!;
        var accesses = new (string Member, Action Access)[]
        {
            ("ID.get", () => _ = account.ID),
            ("Name.get", () => _ = account.Name),
            ("Name.set", () => account.Name = "changed"),
            ("ServerAddress.get", () => _ = account.ServerAddress),
            ("ServerAddress.set", () => account.ServerAddress = "changed.example"),
            ("Port.get", () => _ = account.Port),
            ("Port.set", () => account.Port = 110),
            ("ServerType.get", () => _ = account.ServerType),
            ("ServerType.set", () => account.ServerType = 0),
            ("Username.get", () => _ = account.Username),
            ("Username.set", () => account.Username = "changed-user"),
            ("Password.get", () => _ = account.Password),
            ("Password.set", () => account.Password = "secret"),
            ("MinutesBetweenFetch.get", () => _ = account.MinutesBetweenFetch),
            ("MinutesBetweenFetch.set", () => account.MinutesBetweenFetch = 30),
            ("DaysToKeepMessages.get", () => _ = account.DaysToKeepMessages),
            ("DaysToKeepMessages.set", () => account.DaysToKeepMessages = 30),
            ("AccountID.get", () => _ = account.AccountID),
            ("AccountID.set", () => account.AccountID = 100),
            ("Enabled.get", () => _ = account.Enabled),
            ("Enabled.set", () => account.Enabled = false),
            ("ProcessMIMERecipients.get", () => _ = account.ProcessMIMERecipients),
            ("ProcessMIMERecipients.set", () => account.ProcessMIMERecipients = false),
            ("ProcessMIMEDate.get", () => _ = account.ProcessMIMEDate),
            ("ProcessMIMEDate.set", () => account.ProcessMIMEDate = false),
            ("UseSSL.get", () => _ = account.UseSSL),
            ("UseSSL.set", () => account.UseSSL = false),
            ("NextDownloadTime.get", () => _ = account.NextDownloadTime),
            ("UseAntiSpam.get", () => _ = account.UseAntiSpam),
            ("UseAntiSpam.set", () => account.UseAntiSpam = false),
            ("UseAntiVirus.get", () => _ = account.UseAntiVirus),
            ("UseAntiVirus.set", () => account.UseAntiVirus = false),
            ("EnableRouteRecipients.get", () => _ = account.EnableRouteRecipients),
            ("EnableRouteRecipients.set", () => account.EnableRouteRecipients = false),
            ("IsLocked.get", () => _ = account.IsLocked),
            ("ConnectionSecurity.get", () => _ = account.ConnectionSecurity),
            ("ConnectionSecurity.set", () => account.ConnectionSecurity = ComConnectionSecurity.None),
            ("MIMERecipientHeaders.get", () => _ = account.MIMERecipientHeaders),
            ("MIMERecipientHeaders.set", () => account.MIMERecipientHeaders = "To"),
            (nameof(IInterfaceFetchAccount.Save), account.Save),
            (nameof(IInterfaceFetchAccount.Delete), account.Delete),
            (nameof(IInterfaceFetchAccount.DownloadNow), account.DownloadNow)
        };

        foreach (var (member, access) in accesses)
        {
            AssertAccessDenied(member, access);
        }
    }

    [TestMethod]
    public void DirectlyConstructedFetchAccounts_DeniesEveryMemberBeforeSnapshotOrStoreAccess()
    {
        var accounts = new FetchAccounts();
        var accesses = new (string Member, Action Access)[]
        {
            ("Count.get", () => _ = accounts.Count),
            (nameof(IInterfaceFetchAccounts.get_ItemByDBID), () => _ = accounts.get_ItemByDBID(1)),
            ("Item.get", () => _ = accounts[0]),
            (nameof(IInterfaceFetchAccounts.Refresh), accounts.Refresh),
            (nameof(IInterfaceFetchAccounts.Delete), () => accounts.Delete(0)),
            (nameof(IInterfaceFetchAccounts.DeleteByDBID), () => accounts.DeleteByDBID(1)),
            (nameof(IInterfaceFetchAccounts.Add), () => _ = accounts.Add())
        };

        foreach (var (member, access) in accesses)
        {
            AssertAccessDenied(member, access);
        }
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
                     pendingServerAddress, pendingPort, pendingServerType,
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
    public void AuthorizedCollection_DeleteUsesOwningSnapshotAndLegacyMissingItemNoOp()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[]
            {
                CreateSnapshot(10, 100, "First POP3"),
                CreateSnapshot(20, 100, "Second POP3"),
                CreateSnapshot(30, 200, "Outside POP3")
            });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccounts = account.FetchAccounts;

        fetchAccounts.Delete(1);
        fetchAccounts.DeleteByDBID(10);
        fetchAccounts.Delete(-1);
        fetchAccounts.Delete(10);
        fetchAccounts.DeleteByDBID(999);

        CollectionAssert.AreEqual(new[] { (100, 20), (100, 10) }, store.DeleteCalls.ToArray());
        Assert.AreEqual(0, fetchAccounts.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteFailureRetainsSnapshotAndMapsStoreFailure()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "First POP3") })
        {
            FailDelete = true
        };
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var failure = Assert.ThrowsExactly<COMException>(() => account.FetchAccounts.Delete(0));

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(1, account.FetchAccounts.Count);
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
        Assert.AreEqual("External POP3", fetchAccounts.get_ItemByDBID(10).Name);

        store.Accounts =
        [
            CreateSnapshot(30, 100, "Updated POP3"),
            CreateSnapshot(40, 200, "Still Outside POP3")
        ];
        fetchAccounts.Refresh();

        Assert.AreEqual(1, fetchAccounts.Count);
        Assert.AreEqual("Updated POP3", fetchAccounts[0].Name);
        Assert.AreEqual("Updated POP3", fetchAccounts.get_ItemByDBID(30).Name);
        Assert.AreEqual(2, store.ReadCount);
    }

    [TestMethod]
    public void AccountFetchAccounts_RetainedCollectionRechecksAuthenticationAfterReauthentication()
    {
        FetchAccountAdministrationRuntimeHost.Configure(
            new MutableFetchAccountAdministrationStore(
                new[] { CreateSnapshot(10, 100, "External POP3") }));
        var authenticated = true;
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2),
            () => authenticated);
        var fetchAccounts = account.FetchAccounts;

        Assert.AreEqual(1, fetchAccounts.Count);
        authenticated = false;

        var readFailure = Assert.ThrowsExactly<COMException>(() => _ = fetchAccounts.Count);
        var mutationFailure = Assert.ThrowsExactly<COMException>(() => fetchAccounts.Delete(0));
        Assert.AreEqual(EAccessDenied, readFailure.ErrorCode);
        Assert.AreEqual(EAccessDenied, mutationFailure.ErrorCode);

        authenticated = true;
        Assert.AreEqual(1, fetchAccounts.Count);
    }

    [TestMethod]
    public void FetchAccount_RetainedChildRechecksAuthenticationAfterReauthentication()
    {
        FetchAccountAdministrationRuntimeHost.Configure(
            new MutableFetchAccountAdministrationStore(
                new[] { CreateSnapshot(10, 100, "External POP3") }));
        var authenticated = true;
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2),
            () => authenticated);
        var fetchAccount = account.FetchAccounts[0];

        Assert.AreEqual(10, fetchAccount.ID);
        authenticated = false;

        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => _ = fetchAccount.ID).ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(fetchAccount.Save).ErrorCode);

        authenticated = true;
        Assert.AreEqual(10, fetchAccount.ID);
    }

    [TestMethod]
    public void AuthorizedExistingFetchAccountSave_UpdatesOnlyOwningRowAndPublishesStagedValues()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "External POP3") });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccount = account.FetchAccounts[0];

        fetchAccount.Name = "Updated POP3";
        fetchAccount.Port = 995;
        fetchAccount.ConnectionSecurity = ComConnectionSecurity.StartTlsRequired;
        fetchAccount.Password = "new-secret";
        fetchAccount.Save();

        Assert.AreEqual(1, store.UpdateCalls.Count);
        Assert.AreEqual((10, 100), (store.UpdateCalls[0].FetchAccountId, store.UpdateCalls[0].Account.AccountId));
        Assert.AreEqual("Updated POP3", store.UpdateCalls[0].Account.Name);
        Assert.AreEqual("new-secret", store.UpdateCalls[0].Password);
        Assert.AreEqual("Updated POP3", fetchAccount.Name);
        Assert.AreEqual("Updated POP3", account.FetchAccounts[0].Name);
        Assert.AreEqual(995, fetchAccount.Port);
        Assert.AreEqual(ComConnectionSecurity.StartTlsRequired, fetchAccount.ConnectionSecurity);
    }

    [TestMethod]
    public void AuthorizedExistingFetchAccountSaveFailureRetainsStagedValuesAndSnapshot()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "External POP3") })
        {
            FailUpdate = true
        };
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccount = account.FetchAccounts[0];
        fetchAccount.Name = "Retry POP3";

        var failure = Assert.ThrowsExactly<COMException>(fetchAccount.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual("Retry POP3", fetchAccount.Name);
        Assert.AreEqual("External POP3", account.FetchAccounts[0].Name);
    }

    [TestMethod]
    public void AuthorizedExistingFetchAccountSave_RechecksAuthenticationBeforeStore()
    {
        var authenticated = true;
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "External POP3") });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2),
            () => authenticated);
        var fetchAccount = account.FetchAccounts[0];
        fetchAccount.Name = "Updated POP3";
        authenticated = false;

        var failure = Assert.ThrowsExactly<COMException>(fetchAccount.Save);

        Assert.AreEqual(EAccessDenied, failure.ErrorCode);
        Assert.AreEqual(0, store.UpdateCalls.Count);
    }

    [TestMethod]
    public void AuthorizedExistingFetchAccountSave_RejectsCrossParentAccountBeforeStore()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "External POP3") });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccount = account.FetchAccounts[0];
        fetchAccount.AccountID = 200;

        var failure = Assert.ThrowsExactly<COMException>(fetchAccount.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(0, store.UpdateCalls.Count);
    }

    [TestMethod]
    public void FetchAccountDraft_SettersRecheckAuthenticationBeforeStaging()
    {
        var authenticated = true;
        IInterfaceFetchAccounts accounts = FetchAccounts.CreateAuthorized(
            Array.Empty<FetchAccountAdministrationSnapshot>(),
            insert: _ => ValueTask.FromResult(100),
            accountId: 100,
            isAuthenticated: () => authenticated);
        var draft = accounts.Add();
        authenticated = false;

        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(() => draft.Name = "blocked").ErrorCode);
        Assert.AreEqual(EAccessDenied, Assert.ThrowsExactly<COMException>(draft.Save).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedDownloadNow_UsesOwningParentAndSelectedFetchAccountIds()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "External POP3") });
        var wakeSignal = new RecordingExternalFetchWakeSignal();
        FetchAccountAdministrationRuntimeHost.Configure(store, wakeSignal);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        account.FetchAccounts[0].DownloadNow();

        Assert.AreEqual(100, store.RetryAccountId);
        Assert.AreEqual(10, store.RetryFetchAccountId);
        Assert.AreEqual(1, wakeSignal.SignalCount);
    }

    [TestMethod]
    public void AuthorizedDownloadNow_MapsStoreFailureToComFailure()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "External POP3") })
        {
            FailRetryNow = true
        };
        var wakeSignal = new RecordingExternalFetchWakeSignal();
        FetchAccountAdministrationRuntimeHost.Configure(store, wakeSignal);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var failure = Assert.ThrowsExactly<COMException>(() => account.FetchAccounts[0].DownloadNow());

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(0, wakeSignal.SignalCount);
    }

    [TestMethod]
    public void AuthorizedDelete_RemovesOnlySelectedItemAndPropagatesOwningAccountId()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[]
            {
                CreateSnapshot(10, 100, "Keep POP3"),
                CreateSnapshot(20, 100, "Delete POP3"),
                CreateSnapshot(30, 200, "Outside POP3")
        });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccounts = account.FetchAccounts;
        var selected = fetchAccounts[1];

        selected.Delete();
        selected.Delete();

        Assert.AreEqual(1, store.DeleteCalls.Count);
        Assert.AreEqual((100, 20), store.DeleteCalls[0]);
        Assert.AreEqual(1, fetchAccounts.Count);
        Assert.AreEqual(10, fetchAccounts[0].ID);
        Assert.AreEqual("Keep POP3", fetchAccounts[0].Name);
    }

    [TestMethod]
    public void AuthorizedDelete_MapsStoreFailureToComFailureAndRetainsSnapshot()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[]
            {
                CreateSnapshot(10, 100, "Keep POP3"),
                CreateSnapshot(20, 100, "Delete POP3")
            })
        {
            FailDelete = true
        };
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccounts = account.FetchAccounts;

        var failure = Assert.ThrowsExactly<COMException>(() => fetchAccounts[1].Delete());

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(2, fetchAccounts.Count);
        Assert.AreEqual("Delete POP3", fetchAccounts[1].Name);
    }

    [TestMethod]
    public void AuthorizedAddStagesOwningDraftAndAppendsOnlyAfterInsert()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "Existing POP3") });
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccounts = account.FetchAccounts;
        var added = fetchAccounts.Add();

        Assert.AreEqual(0, added.ID);
        added.Name = "Added POP3";
        added.ServerAddress = "pop3-added.example.test";
        added.Port = 995;
        added.Username = "added-user";
        added.Password = "added-secret";
        added.MinutesBetweenFetch = 45;
        added.DaysToKeepMessages = 7;
        added.Enabled = false;
        added.ProcessMIMERecipients = true;
        added.ProcessMIMEDate = true;
        added.ConnectionSecurity = ComConnectionSecurity.Tls;
        added.MIMERecipientHeaders = "To,X-RCPT-TO";
        added.UseAntiSpam = true;
        added.UseAntiVirus = true;
        added.EnableRouteRecipients = true;

        added.Save();

        Assert.AreEqual(1, store.InsertCalls.Count);
        var draft = store.InsertCalls[0];
        Assert.AreEqual(100, draft.AccountId);
        Assert.AreEqual("Added POP3", draft.Name);
        Assert.AreEqual("pop3-added.example.test", draft.ServerAddress);
        Assert.AreEqual(995, draft.Port);
        Assert.AreEqual("added-user", draft.Username);
        Assert.AreEqual("added-secret", draft.Password);
        Assert.IsFalse(draft.Enabled);
        Assert.AreEqual((int)ComConnectionSecurity.Tls, draft.ConnectionSecurity);
        Assert.AreEqual(2, fetchAccounts.Count);
        Assert.AreEqual(1000, added.ID);
        Assert.AreEqual(1000, fetchAccounts.get_ItemByDBID(1000).ID);
    }

    [TestMethod]
    public void AuthorizedAddSaveFailureRetainsDraftAndDoesNotPublishSnapshot()
    {
        var store = new MutableFetchAccountAdministrationStore(
            new[] { CreateSnapshot(10, 100, "Existing POP3") })
        {
            FailInsert = true
        };
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var fetchAccounts = account.FetchAccounts;
        var added = fetchAccounts.Add();
        added.Name = "Retry POP3";

        var failure = Assert.ThrowsExactly<COMException>(added.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(0, added.ID);
        Assert.AreEqual("Retry POP3", added.Name);
        Assert.AreEqual(1, fetchAccounts.Count);
    }

    [TestMethod]
    public void AuthorizedAddRejectsCrossParentAccountMutationBeforeStore()
    {
        var store = new MutableFetchAccountAdministrationStore(Array.Empty<FetchAccountAdministrationSnapshot>());
        FetchAccountAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var added = account.FetchAccounts.Add();
        added.AccountID = 200;

        var failure = Assert.ThrowsExactly<COMException>(added.Save);

        Assert.AreEqual(EFail, failure.ErrorCode);
        Assert.AreEqual(0, store.InsertCalls.Count);
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

    private static void AssertAccessDenied(string member, Action access)
    {
        var error = Assert.ThrowsExactly<COMException>(access, $"{member} should reject direct activation.");

        Assert.AreEqual(EAccessDenied, error.ErrorCode, $"{member} should return E_ACCESSDENIED, not E_NOTIMPL.");
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

        public int? RetryAccountId { get; private set; }

        public int? RetryFetchAccountId { get; private set; }

        public bool FailRetryNow { get; set; }

        public bool FailDelete { get; set; }

        public bool FailInsert { get; set; }

        public bool FailUpdate { get; set; }

        public int NextFetchAccountId { get; set; } = 1000;

        public List<FetchAccountAdministrationDraft> InsertCalls { get; } = [];

        public List<(int AccountId, int FetchAccountId)> DeleteCalls { get; } = [];

        public List<(int FetchAccountId, FetchAccountAdministrationDraft Account, string? Password)> UpdateCalls { get; } = [];

        public ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<FetchAccountAdministrationSnapshot>>(
                Accounts.Where(account => account.AccountId == accountId).ToArray());
        }

        public ValueTask SetRetryNowAsync(
            int accountId,
            int fetchAccountId,
            CancellationToken cancellationToken)
        {
            if (FailRetryNow)
            {
                throw new InvalidOperationException("store failed");
            }

            RetryAccountId = accountId;
            RetryFetchAccountId = fetchAccountId;
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> InsertFetchAccountAsync(
            FetchAccountAdministrationDraft account,
            CancellationToken cancellationToken)
        {
            if (FailInsert)
            {
                throw new InvalidOperationException("store failed");
            }

            InsertCalls.Add(account);
            return ValueTask.FromResult(NextFetchAccountId++);
        }

        public ValueTask<bool> UpdateFetchAccountAsync(
            int fetchAccountId,
            FetchAccountAdministrationDraft account,
            string? password,
            CancellationToken cancellationToken)
        {
            if (FailUpdate)
            {
                throw new InvalidOperationException("store failed");
            }

            UpdateCalls.Add((fetchAccountId, account, password));
            Accounts = Accounts
                .Select(current => current.Id == fetchAccountId && current.AccountId == account.AccountId
                    ? current with
                    {
                        Name = account.Name,
                        ServerAddress = account.ServerAddress,
                        Port = account.Port,
                        ServerType = account.ServerType,
                        Username = account.Username,
                        MinutesBetweenFetch = account.MinutesBetweenFetch,
                        DaysToKeepMessages = account.DaysToKeepMessages,
                        Enabled = account.Enabled,
                        ProcessMimeRecipients = account.ProcessMimeRecipients,
                        ProcessMimeDate = account.ProcessMimeDate,
                        ConnectionSecurity = account.ConnectionSecurity,
                        UseAntiSpam = account.UseAntiSpam,
                        UseAntiVirus = account.UseAntiVirus,
                        EnableRouteRecipients = account.EnableRouteRecipients,
                        MimeRecipientHeaders = account.MimeRecipientHeaders
                    }
                    : current)
                .ToArray();
            return ValueTask.FromResult(true);
        }

        public ValueTask DeleteFetchAccountAsync(
            int accountId,
            int fetchAccountId,
            CancellationToken cancellationToken)
        {
            if (FailDelete)
            {
                throw new InvalidOperationException("store failed");
            }

            DeleteCalls.Add((accountId, fetchAccountId));
            Accounts = Accounts
                .Where(account => account.AccountId != accountId || account.Id != fetchAccountId)
                .ToArray();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingExternalFetchWakeSignal : IExternalFetchWakeSignal
    {
        public int SignalCount { get; private set; }

        public void Signal() => SignalCount++;

        public ValueTask<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);
    }
}
