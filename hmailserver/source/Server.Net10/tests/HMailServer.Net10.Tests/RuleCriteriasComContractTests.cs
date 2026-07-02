using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RuleCriteriasComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsMarshalingAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceRuleCriterias),
            "D79148F6-78A9-4F60-B8E8-48C33D888FC5",
            new[]
            {
                "get_Item", "get_ItemByDBID", "get_Count", "Add", "DeleteByDBID",
                "Refresh", "Delete"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceRuleCriterias).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            6,
            typeof(IInterfaceRuleCriterias).GetMethod(nameof(IInterfaceRuleCriterias.Delete))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceRuleCriteria),
            "2D8AA7DE-6155-44A5-802D-9FEC611A50A9",
            new[]
            {
                "get_ID", "get_RuleID", "set_RuleID", "get_MatchValue", "set_MatchValue",
                "get_UsePredefined", "set_UsePredefined", "get_PredefinedField",
                "set_PredefinedField", "get_MatchType", "set_MatchType", "get_HeaderField",
                "set_HeaderField", "Save", "Delete"
            });
        AssertStringPropertyMarshaling(nameof(IInterfaceRuleCriteria.MatchValue));
        AssertStringPropertyMarshaling(nameof(IInterfaceRuleCriteria.HeaderField));
        var usePredefined = typeof(IInterfaceRuleCriteria).GetProperty(nameof(IInterfaceRuleCriteria.UsePredefined));
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            usePredefined?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            usePredefined?.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void RuleEnums_PreserveLegacyGuidsAndValues()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD02"), typeof(ComRulePredefinedField).GUID);
        CollectionAssert.AreEqual(
            new[]
            {
                "Unknown", "From", "To", "Cc", "Subject", "Body", "MessageSize",
                "RecipientList", "DeliveryAttempts"
            },
            Enum.GetNames<ComRulePredefinedField>());
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 9).ToArray(),
            Enum.GetValues<ComRulePredefinedField>().Select(static value => (int)value).ToArray());

        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD03"), typeof(ComRuleMatchType).GUID);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 9).ToArray(),
            Enum.GetValues<ComRuleMatchType>().Select(static value => (int)value).ToArray());
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<RuleCriterias>(
            "E90022A1-61CF-4152-B9D9-27D04D0BA362",
            "hMailServer.RuleCriterias.1",
            typeof(IInterfaceRuleCriterias));
        AssertComClass<RuleCriteria>(
            "3F0EB97B-C698-498C-965A-06ED393AC50C",
            "hMailServer.RuleCriteria.1",
            typeof(IInterfaceRuleCriteria));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var criteriaError = Assert.ThrowsExactly<COMException>(() => _ = new RuleCriterias().Count);
        var criterionError = Assert.ThrowsExactly<COMException>(() => _ = new RuleCriteria().MatchValue);

        Assert.AreEqual(EAccessDenied, criteriaError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceRuleCriterias criteria = RuleCriterias.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, "invoice", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 10, "high", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Priority")
            });

        Assert.AreEqual(2, criteria.Count);
        AssertCriterion(
            criteria[0],
            100,
            10,
            "invoice",
            true,
            ComRulePredefinedField.Subject,
            ComRuleMatchType.Contains,
            string.Empty);
        AssertCriterion(
            criteria.get_ItemByDBID(200),
            200,
            10,
            "high",
            false,
            ComRulePredefinedField.Unknown,
            ComRuleMatchType.Equals,
            "X-Priority");

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = criteria[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(300));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => criteria.Add());
        var pendingDeleteByDbId = Assert.ThrowsExactly<COMException>(() => criteria.DeleteByDBID(100));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(criteria.Refresh);
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => criteria.Delete(0));
        var pendingMatchValue = Assert.ThrowsExactly<COMException>(() => criteria[0].MatchValue = "changed");
        var pendingUsePredefined = Assert.ThrowsExactly<COMException>(() => criteria[0].UsePredefined = false);
        var pendingPredefinedField = Assert.ThrowsExactly<COMException>(
            () => criteria[0].PredefinedField = ComRulePredefinedField.Body);
        var pendingMatchType = Assert.ThrowsExactly<COMException>(
            () => criteria[0].MatchType = ComRuleMatchType.NotEquals);
        var pendingSave = Assert.ThrowsExactly<COMException>(criteria[0].Save);
        var pendingItemDelete = Assert.ThrowsExactly<COMException>(criteria[0].Delete);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDeleteByDbId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMatchValue.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingUsePredefined.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingPredefinedField.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMatchType.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingItemDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedRule_UsesConfiguredRuleScopedRuntime()
    {
        RuleCriteriaAdministrationRuntimeHost.Configure(
            new FixedRuleCriteriaAdministrationStore(
                new[]
                {
                    Snapshot(300, 20, "outside", true, ComRulePredefinedField.To, ComRuleMatchType.Equals, string.Empty),
                    Snapshot(200, 10, "second", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                    Snapshot(100, 10, "first", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
                }));
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });

        var criteria = rules[0].Criterias;

        Assert.AreEqual(2, criteria.Count);
        Assert.AreEqual(100, criteria[0].ID);
        Assert.AreEqual("first", criteria[0].MatchValue);
        var outsideRule = Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(300));
        Assert.AreEqual(DispEBadIndex, outsideRule.ErrorCode);
    }

    private static RuleCriteriaAdministrationSnapshot Snapshot(
        int id,
        int ruleId,
        string matchValue,
        bool usePredefined,
        ComRulePredefinedField predefinedField,
        ComRuleMatchType matchType,
        string headerField) =>
        new(id, ruleId, matchValue, usePredefined, (int)predefinedField, (int)matchType, headerField);

    private static void AssertCriterion(
        IInterfaceRuleCriteria criterion,
        int id,
        int ruleId,
        string matchValue,
        bool usePredefined,
        ComRulePredefinedField predefinedField,
        ComRuleMatchType matchType,
        string headerField)
    {
        Assert.AreEqual(id, criterion.ID);
        Assert.AreEqual(ruleId, criterion.RuleID);
        Assert.AreEqual(matchValue, criterion.MatchValue);
        Assert.AreEqual(usePredefined, criterion.UsePredefined);
        Assert.AreEqual(predefinedField, criterion.PredefinedField);
        Assert.AreEqual(matchType, criterion.MatchType);
        Assert.AreEqual(headerField, criterion.HeaderField);
    }

    private static void AssertStringPropertyMarshaling(string propertyName)
    {
        var property = typeof(IInterfaceRuleCriteria).GetProperty(propertyName);
        Assert.AreEqual(
            UnmanagedType.BStr,
            property?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.BStr,
            property?.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
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

    private sealed class FixedRuleCriteriaAdministrationStore(
        IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria)
        : IRuleCriteriaAdministrationStore
    {
        public ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
            int ruleId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>(
                criteria.Where(criterion => criterion.RuleId == ruleId)
                    .OrderBy(static criterion => criterion.Id)
                    .ToArray());
    }
}
