using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RulesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceRules),
            "995F9181-E761-42FA-9057-FE070B37D0F3",
            new[]
            {
                "get_Item", "get_ItemByDBID", "get_Count", "Add", "DeleteByDBID", "Refresh"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceRules).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            typeof(IInterfaceRules).GetMethod("Refresh")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceRule),
            "41CCD467-9ADE-4ADA-AE14-760E94BA53E8",
            new[]
            {
                "get_ID", "get_AccountID", "set_AccountID", "get_Name", "set_Name",
                "get_Active", "set_Active", "get_UseAND", "set_UseAND", "get_Criterias",
                "get_Actions", "Save", "MoveUp", "MoveDown", "Delete"
            });
        Assert.AreEqual(
            7,
            typeof(IInterfaceRule).GetProperty(nameof(IInterfaceRule.Actions))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Rules>(
            "624F494B-347A-4285-9506-C54154D50B2A",
            "hMailServer.Rules.1",
            typeof(IInterfaceRules));
        AssertComClass<Rule>(
            "D5D7927A-7D05-40F3-91DD-968FC14316C7",
            "hMailServer.Rule.1",
            typeof(IInterfaceRule));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var rulesError = Assert.ThrowsExactly<COMException>(() => _ = new Rules().Count);
        var refreshError = Assert.ThrowsExactly<COMException>(new Rules().Refresh);
        var ruleError = Assert.ThrowsExactly<COMException>(() => _ = new Rule().Name);

        Assert.AreEqual(EAccessDenied, rulesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, refreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, ruleError.ErrorCode);
    }

    [TestMethod]
    public void DirectActivation_DeleteMembersDenyBeforeCallingConfiguredStore()
    {
        var store = new MutableRuleAdministrationStore(
            new[] { new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1) });
        RuleAdministrationRuntimeHost.Configure(store);

        var rulesError = Assert.ThrowsExactly<COMException>(() => new Rules().DeleteByDBID(10));
        var ruleError = Assert.ThrowsExactly<COMException>(new Rule().Delete);

        Assert.AreEqual(EAccessDenied, rulesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, ruleError.ErrorCode);
        Assert.AreEqual(0, store.DeleteCalls.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var refreshed = new[]
        {
            new RuleAdministrationSnapshot(20, 100, "Second rule", false, false, 2),
            new RuleAdministrationSnapshot(30, 100, "Third rule", true, false, 3)
        };
        var failRefresh = false;
        IInterfaceRules rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1) },
            () => failRefresh
                ? throw new InvalidOperationException("store failed")
                : refreshed);

        rules.Refresh();

        Assert.AreEqual(2, rules.Count);
        Assert.AreEqual("Second rule", rules[0].Name);
        Assert.AreEqual("Third rule", rules.get_ItemByDBID(30).Name);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => rules.get_ItemByDBID(10)).ErrorCode);

        failRefresh = true;
        var failure = Assert.ThrowsExactly<COMException>(rules.Refresh);

        Assert.AreEqual(unchecked((int)0x80004005), failure.ErrorCode);
        Assert.AreEqual(2, rules.Count);
        Assert.AreEqual("Second rule", rules[0].Name);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceRules rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 100, "Second rule", false, false, 2)
            });

        Assert.AreEqual(2, rules.Count);
        AssertRule(rules[0], 10, 100, "First rule", true, true);
        AssertRule(rules.get_ItemByDBID(20), 20, 100, "Second rule", false, false);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = rules[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = rules.get_ItemByDBID(30));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(rules.Refresh);
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => rules.Add());
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => rules[0].Name = "changed");

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
    }

    [TestMethod]
    public void AccountRules_ReturnFreshFacadesOverSharedState()
    {
        var store = new MutableRuleAdministrationStore(
            new[]
            {
                new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 200, "Outside rule", true, true, 1)
            });
        RuleAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var rules = account.Rules;
        var secondRules = account.Rules;

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual("First rule", rules[0].Name);
        Assert.AreNotSame(rules, secondRules);
        Assert.AreEqual(1, store.ReadCount);

        store.Rules =
        [
            new RuleAdministrationSnapshot(30, 100, "Updated rule", false, false, 1),
            new RuleAdministrationSnapshot(40, 200, "Still outside rule", true, true, 1)
        ];
        secondRules.Refresh();

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual("Updated rule", rules[0].Name);
        Assert.AreEqual(2, store.ReadCount);
    }

    [TestMethod]
    public void AuthenticatedAdministratorAccountRules_LoadGlobalRules()
    {
        var store = new MutableRuleAdministrationStore(
            new[]
            {
                new RuleAdministrationSnapshot(10, 0, "Global rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 100, "Account rule", true, true, 1)
            });
        RuleAdministrationRuntimeHost.Configure(store);

        var account = Account.CreateServerAdministrator();
        var rules = account.Rules;

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual(0, rules[0].AccountID);
        Assert.AreEqual("Global rule", rules[0].Name);
        Assert.AreEqual(1, store.ReadCount);
    }

    [TestMethod]
    public void AuthorizedRuleDelete_UsesOwningCallbackAndUpdatesAllSharedFacades()
    {
        var store = new MutableRuleAdministrationStore(
            new[]
            {
                new RuleAdministrationSnapshot(10, 100, "Keep rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 100, "Delete rule", false, false, 2),
                new RuleAdministrationSnapshot(30, 200, "Foreign rule", true, true, 1)
            });
        RuleAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var rules = account.Rules;
        var secondRules = account.Rules;
        var retained = rules[1];

        retained.Delete();
        retained.Delete();

        Assert.AreEqual(1, store.DeleteCalls.Count);
        Assert.AreEqual((100, 20), store.DeleteCalls[0]);
        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual(1, secondRules.Count);
        Assert.AreEqual(10, rules[0].ID);
        Assert.AreEqual(10, secondRules[0].ID);
    }

    [TestMethod]
    public void AuthorizedRuleDelete_UnknownForeignRepeatedAndStaleIdsAreNoOps()
    {
        var store = new MutableRuleAdministrationStore(
            new[]
            {
                new RuleAdministrationSnapshot(10, 100, "Current rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 200, "Foreign rule", true, true, 1)
            });
        RuleAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var rules = account.Rules;
        var stale = rules[0];

        rules.DeleteByDBID(999);
        rules.DeleteByDBID(20);
        store.Rules = [new RuleAdministrationSnapshot(30, 100, "Reloaded rule", true, true, 1)];
        rules.Refresh();
        stale.Delete();

        Assert.AreEqual(0, store.DeleteCalls.Count);
        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual(30, rules[0].ID);
    }

    [TestMethod]
    public void AuthorizedRuleDelete_MapsStoreFailureToComFailureAndRetainsSnapshot()
    {
        var store = new MutableRuleAdministrationStore(
            new[] { new RuleAdministrationSnapshot(10, 100, "Retained rule", true, true, 1) })
        {
            FailDelete = true
        };
        RuleAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));
        var rules = account.Rules;

        var failure = Assert.ThrowsExactly<COMException>(() => rules.DeleteByDBID(10));

        Assert.AreEqual(unchecked((int)0x80004005), failure.ErrorCode);
        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual("Retained rule", rules[0].Name);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesLegacyDefaultsAndSavePublishesInsertedIdentity()
    {
        var inserted = new List<RuleAdministrationSnapshot>();
        IInterfaceRules rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 100, "Second rule", false, false, 2)
            },
            accountId: 100,
            insert: rule =>
            {
                inserted.Add(rule);
                return 30;
            });

        var draft = rules.Add();

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(100, draft.AccountID);
        Assert.AreEqual(string.Empty, draft.Name);
        Assert.IsTrue(draft.Active);
        Assert.IsTrue(draft.UseAND);

        draft.Name = "New rule";
        draft.Active = false;
        draft.UseAND = false;

        Assert.AreEqual(2, rules.Count);
        draft.Save();

        Assert.AreEqual(3, rules.Count);
        Assert.AreEqual(30, draft.ID);
        Assert.AreEqual(1, inserted.Count);
        var persisted = inserted[0];
        Assert.AreEqual(0, persisted.Id);
        Assert.AreEqual(100, persisted.AccountId);
        Assert.AreEqual("New rule", persisted.Name);
        Assert.IsFalse(persisted.Active);
        Assert.IsFalse(persisted.UseAnd);
        Assert.AreEqual(0, persisted.SortOrder);
        Assert.AreEqual("New rule", rules.get_ItemByDBID(30).Name);
    }

    [TestMethod]
    public void FailedInsert_MapsToEFailAndRetainsDraftWithoutPublishing()
    {
        var fail = true;
        IInterfaceRules rules = Rules.CreateAuthorized(
            Array.Empty<RuleAdministrationSnapshot>(),
            accountId: 100,
            insert: _ => fail
                ? throw new InvalidOperationException("Simulated store failure.")
                : 1);

        var draft = rules.Add();
        draft.Name = "New rule";

        var saveFailure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual(0, rules.Count);
        Assert.AreEqual(0, draft.ID);

        draft.Name = "Other rule";
        fail = false;
        draft.Save();

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual(1, draft.ID);
        Assert.AreEqual("Other rule", rules.get_ItemByDBID(1).Name);
    }

    [TestMethod]
    public void AddAndMutate_RecheckLiveAuthentication()
    {
        var authenticated = true;
        IInterfaceRules rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1) },
            isAuthenticated: () => authenticated,
            accountId: 100,
            insert: _ => 11);

        var draft = rules.Add();
        authenticated = false;

        var deniedAdd = Assert.ThrowsExactly<COMException>(() => rules.Add());
        var deniedSetter = Assert.ThrowsExactly<COMException>(() => draft.Name = "x");
        var deniedSave = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80070005), deniedAdd.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80070005), deniedSetter.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80070005), deniedSave.ErrorCode);
    }

    [TestMethod]
    public void ExistingRowSaveAndSetters_RemainNotImplementedUntilUpdateParity()
    {
        IInterfaceRules rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1) },
            accountId: 100,
            insert: _ => 11);

        var existing = rules[0];
        var pendingSetter = Assert.ThrowsExactly<COMException>(() => existing.Name = "changed");
        var pendingSave = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(unchecked((int)0x80004001), pendingSetter.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80004001), pendingSave.ErrorCode);
    }
    private static void AssertRule(
        IInterfaceRule rule,
        int id,
        int accountId,
        string name,
        bool active,
        bool useAnd)
    {
        Assert.AreEqual(id, rule.ID);
        Assert.AreEqual(accountId, rule.AccountID);
        Assert.AreEqual(name, rule.Name);
        Assert.AreEqual(active, rule.Active);
        Assert.AreEqual(useAnd, rule.UseAND);
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

    private sealed class MutableRuleAdministrationStore(IReadOnlyList<RuleAdministrationSnapshot> rules)
        : IRuleAdministrationStore
    {
        public IReadOnlyList<RuleAdministrationSnapshot> Rules { get; set; } = rules;

        public int ReadCount { get; private set; }

        public bool FailDelete { get; set; }

        public List<(int AccountId, int RuleId)> DeleteCalls { get; } = [];

        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<RuleAdministrationSnapshot>>(
                Rules.Where(rule => rule.AccountId == accountId).OrderBy(rule => rule.SortOrder).ToArray());
        }

        public ValueTask<bool> DeleteRuleAsync(
            int accountId,
            int ruleId,
            CancellationToken cancellationToken)
        {
            if (FailDelete)
            {
                throw new InvalidOperationException("store failed");
            }

            var match = Rules.FirstOrDefault(rule => rule.AccountId == accountId && rule.Id == ruleId);
            if (match is null)
            {
                return ValueTask.FromResult(false);
            }

            DeleteCalls.Add((accountId, ruleId));
            Rules = Rules.Where(rule => !ReferenceEquals(rule, match)).ToArray();
            return ValueTask.FromResult(true);
        }
    }
}
