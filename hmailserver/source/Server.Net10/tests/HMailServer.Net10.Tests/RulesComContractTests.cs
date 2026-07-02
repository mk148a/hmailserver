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
        var ruleError = Assert.ThrowsExactly<COMException>(() => _ = new Rule().Name);

        Assert.AreEqual(EAccessDenied, rulesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, ruleError.ErrorCode);
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
        var pendingActions = Assert.ThrowsExactly<COMException>(() => _ = rules[0].Actions);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingActions.ErrorCode);
    }

    [TestMethod]
    public void AccountRules_UsesConfiguredRuntimeForSelectedAccount()
    {
        RuleAdministrationRuntimeHost.Configure(
            new FixedRuleAdministrationStore(
                new[]
                {
                    new RuleAdministrationSnapshot(10, 100, "First rule", true, true, 1),
                    new RuleAdministrationSnapshot(20, 200, "Outside rule", true, true, 1)
                }));
        var account = Account.CreateAuthorized(new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var rules = account.Rules;

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual("First rule", rules[0].Name);
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

    private sealed class FixedRuleAdministrationStore(IReadOnlyList<RuleAdministrationSnapshot> rules)
        : IRuleAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RuleAdministrationSnapshot>>(
                rules.Where(rule => rule.AccountId == accountId).OrderBy(rule => rule.SortOrder).ToArray());
    }
}
