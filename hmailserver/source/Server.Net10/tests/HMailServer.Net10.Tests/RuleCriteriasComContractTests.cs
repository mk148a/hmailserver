using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RuleCriteriasComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
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
        var store = new MutableRuleCriteriaAdministrationStore([]);
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var criteriaError = Assert.ThrowsExactly<COMException>(() => _ = new RuleCriterias().Count);
        var criteriaRefreshError = Assert.ThrowsExactly<COMException>(new RuleCriterias().Refresh);
        var criteriaDeleteError = Assert.ThrowsExactly<COMException>(() => new RuleCriterias().DeleteByDBID(100));
        var criteriaIndexDeleteError = Assert.ThrowsExactly<COMException>(() => new RuleCriterias().Delete(0));
        var criterionError = Assert.ThrowsExactly<COMException>(() => _ = new RuleCriteria().MatchValue);
        var criterionMatchValueError = Assert.ThrowsExactly<COMException>(() => new RuleCriteria().MatchValue = "X-Detached");
        var criterionUsePredefinedError = Assert.ThrowsExactly<COMException>(() => new RuleCriteria().UsePredefined = false);
        var criterionPredefinedFieldError = Assert.ThrowsExactly<COMException>(
            () => new RuleCriteria().PredefinedField = ComRulePredefinedField.Body);
        var criterionHeaderFieldError = Assert.ThrowsExactly<COMException>(() => new RuleCriteria().HeaderField = "X-Detached");
        var criterionSaveError = Assert.ThrowsExactly<COMException>(new RuleCriteria().Save);
        var criterionDeleteError = Assert.ThrowsExactly<COMException>(new RuleCriteria().Delete);

        Assert.AreEqual(EAccessDenied, criteriaError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criteriaRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criteriaDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criteriaIndexDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionMatchValueError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionUsePredefinedError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionPredefinedFieldError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionHeaderFieldError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionSaveError.ErrorCode);
        Assert.AreEqual(EAccessDenied, criterionDeleteError.ErrorCode);
        Assert.AreEqual(0, store.ReadCount);
        Assert.AreEqual(0, store.DeletedCriteria.Count);
        Assert.AreEqual(0, store.SavedCriteria.Count);
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
        var pendingHeaderField = Assert.ThrowsExactly<COMException>(
            () => criteria[0].HeaderField = "X-ReadOnly");
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
        Assert.AreEqual(ENotImplemented, pendingHeaderField.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingItemDelete.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceRuleCriterias criteria = RuleCriterias.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, "initial", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty)
            },
            () =>
            {
                reloads++;
                if (failReload)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }

                return new[]
                {
                    Snapshot(200, 10, "updated", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test"),
                    Snapshot(300, 10, "second", true, ComRulePredefinedField.From, ComRuleMatchType.NotEquals, string.Empty)
                };
            });

        Assert.AreEqual(1, criteria.Count);
        Assert.AreEqual("initial", criteria[0].MatchValue);

        criteria.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, criteria.Count);
        AssertCriterion(
            criteria[0],
            200,
            10,
            "updated",
            false,
            ComRulePredefinedField.Unknown,
            ComRuleMatchType.Equals,
            "X-Test");
        Assert.AreEqual("second", criteria.get_ItemByDBID(300).MatchValue);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(100)).ErrorCode);

        failReload = true;
        var refreshFailure = Assert.ThrowsExactly<COMException>(criteria.Refresh);

        Assert.AreEqual(EFail, refreshFailure.ErrorCode);
        Assert.AreEqual(2, criteria.Count);
        Assert.AreEqual("updated", criteria.get_ItemByDBID(200).MatchValue);
        Assert.AreEqual(2, reloads);
    }

    [TestMethod]
    public void AuthorizedCollection_DeleteByDBIDRemovesOnlyMemberAndRetainsSnapshotOnFailure()
    {
        var failDelete = true;
        var deletedIds = new List<int>();
        IInterfaceRuleCriterias criteria = RuleCriterias.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 10, "second", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
            },
            deleteById: databaseId =>
            {
                deletedIds.Add(databaseId);
                if (failDelete)
                {
                    throw new InvalidOperationException("Simulated store failure.");
                }
            });

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => criteria.DeleteByDBID(100));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(new[] { 100 }, deletedIds);
        Assert.AreEqual(2, criteria.Count);
        Assert.AreEqual("first", criteria.get_ItemByDBID(100).MatchValue);

        failDelete = false;
        criteria.DeleteByDBID(100);

        CollectionAssert.AreEqual(new[] { 100, 100 }, deletedIds);
        Assert.AreEqual(1, criteria.Count);
        Assert.AreEqual(200, criteria[0].ID);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(100)).ErrorCode);

        criteria.DeleteByDBID(100);
        criteria.DeleteByDBID(999);

        CollectionAssert.AreEqual(new[] { 100, 100 }, deletedIds);
        Assert.AreEqual(1, criteria.Count);
    }

    [TestMethod]
    public void AuthorizedRule_DeleteByDBIDScopesStoreCallAndNoOpsForForeignOrUnknownIds()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 10, "second", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test"),
                Snapshot(300, 20, "foreign", true, ComRulePredefinedField.To, ComRuleMatchType.Equals, string.Empty)
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var criteria = rules[0].Criterias;

        criteria.DeleteByDBID(300);
        criteria.DeleteByDBID(999);

        Assert.AreEqual(0, store.DeletedCriteria.Count);
        Assert.AreEqual(2, criteria.Count);

        criteria.DeleteByDBID(100);
        criteria.DeleteByDBID(100);

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedCriteria);
        Assert.AreEqual(1, criteria.Count);
        Assert.AreEqual(200, criteria[0].ID);
    }

    [TestMethod]
    public void AuthorizedRule_DeleteByIndexDeletesOnlySelectedCriterionAndNoOpsForInvalidIndices()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 10, "second", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test"),
                Snapshot(300, 10, "third", true, ComRulePredefinedField.From, ComRuleMatchType.NotEquals, string.Empty),
                Snapshot(400, 20, "foreign", true, ComRulePredefinedField.To, ComRuleMatchType.Equals, string.Empty)
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var criteria = rules[0].Criterias;

        criteria.Delete(-1);
        criteria.Delete(3);

        Assert.AreEqual(0, store.DeletedCriteria.Count);
        Assert.AreEqual(3, criteria.Count);

        criteria.Delete(1);

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 200) },
            store.DeletedCriteria);
        Assert.AreEqual(2, criteria.Count);
        Assert.AreEqual(100, criteria[0].ID);
        Assert.AreEqual(300, criteria[1].ID);
        Assert.AreEqual(10, criteria[1].RuleID);
    }

    [TestMethod]
    public void AuthorizedRule_DeleteByIndexMapsStoreFailureToEFailAndRetainsSnapshotForRetry()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 10, "second", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1)
            });
        var criteria = rules[0].Criterias;
        store.FailDelete = true;

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => criteria.Delete(0));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedCriteria);
        Assert.AreEqual(2, criteria.Count);
        Assert.AreEqual(100, criteria[0].ID);
        Assert.AreEqual(200, criteria[1].ID);

        store.FailDelete = false;
        criteria.Delete(0);

        CollectionAssert.AreEqual(
            new[]
            {
                (RuleId: 10, DatabaseId: 100),
                (RuleId: 10, DatabaseId: 100)
            },
            store.DeletedCriteria);
        Assert.AreEqual(1, criteria.Count);
        Assert.AreEqual(200, criteria[0].ID);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_HeaderFieldStagesRawValueAndSaveUsesOwningRuleScope()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 20, "foreign", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });

        var criterion = rules[0].Criterias[0];
        const string rawHeaderField = " X-Raw-Header\t";

        criterion.HeaderField = rawHeaderField;

        Assert.AreEqual(rawHeaderField, criterion.HeaderField);
        Assert.AreEqual(0, store.SavedCriteria.Count);

        criterion.Save();

        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[0].Id);
        Assert.AreEqual(10, store.SavedCriteria[0].RuleId);
        Assert.AreEqual("first", store.SavedCriteria[0].MatchValue);
        Assert.AreEqual(rawHeaderField, store.SavedCriteria[0].HeaderField);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_MatchValueStagesRawValueAndSaveUsesOwningRuleScope()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 20, "foreign", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });

        var criterion = rules[0].Criterias[0];
        const string rawMatchValue = "  raw match\t";

        criterion.MatchValue = rawMatchValue;

        Assert.AreEqual(rawMatchValue, criterion.MatchValue);
        Assert.AreEqual(0, store.SavedCriteria.Count);

        criterion.Save();

        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[0].Id);
        Assert.AreEqual(10, store.SavedCriteria[0].RuleId);
        Assert.AreEqual(rawMatchValue, store.SavedCriteria[0].MatchValue);
        Assert.AreEqual(string.Empty, store.SavedCriteria[0].HeaderField);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_UsePredefinedStagesWithoutStoreCallAndSavePreservesExistingRow()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, "X-Header"),
                Snapshot(200, 20, "foreign", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Foreign")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });

        var criterion = rules[0].Criterias[0];

        criterion.UsePredefined = false;

        Assert.IsFalse(criterion.UsePredefined);
        Assert.AreEqual(0, store.SavedCriteria.Count);

        criterion.Save();

        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[0].Id);
        Assert.AreEqual(10, store.SavedCriteria[0].RuleId);
        Assert.AreEqual("first", store.SavedCriteria[0].MatchValue);
        Assert.IsFalse(store.SavedCriteria[0].UsePredefined);
        Assert.AreEqual((int)ComRulePredefinedField.Subject, store.SavedCriteria[0].PredefinedField);
        Assert.AreEqual((int)ComRuleMatchType.Contains, store.SavedCriteria[0].MatchType);
        Assert.AreEqual("X-Header", store.SavedCriteria[0].HeaderField);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_UsePredefinedSaveFailureRetainsStagedValueForRetry()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, "X-Header")
            })
        {
            FailSave = true
        };
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var criterion = rules[0].Criterias[0];
        criterion.UsePredefined = false;

        var saveFailure = Assert.ThrowsExactly<COMException>(criterion.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[0].Id);
        Assert.AreEqual(10, store.SavedCriteria[0].RuleId);
        Assert.IsFalse(criterion.UsePredefined);
        Assert.IsFalse(store.SavedCriteria[0].UsePredefined);

        store.FailSave = false;
        criterion.Save();

        Assert.AreEqual(2, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[1].Id);
        Assert.AreEqual(10, store.SavedCriteria[1].RuleId);
        Assert.IsFalse(store.SavedCriteria[1].UsePredefined);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_PredefinedFieldStagesWithoutStoreCallAndPreservesOwningAndUnrelatedFields()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, "X-Header"),
                Snapshot(200, 20, "foreign", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Foreign")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var criterion = rules[0].Criterias[0];
        var rawUnnamedValue = (ComRulePredefinedField)12345;

        criterion.PredefinedField = rawUnnamedValue;

        Assert.AreEqual(rawUnnamedValue, criterion.PredefinedField);
        Assert.AreEqual(100, criterion.ID);
        Assert.AreEqual(10, criterion.RuleID);
        Assert.AreEqual(0, store.SavedCriteria.Count);

        criterion.Save();

        Assert.AreEqual(1, store.SavedCriteria.Count);
        var saved = store.SavedCriteria[0];
        Assert.AreEqual(100, saved.Id);
        Assert.AreEqual(10, saved.RuleId);
        Assert.AreEqual("first", saved.MatchValue);
        Assert.IsTrue(saved.UsePredefined);
        Assert.AreEqual(12345, saved.PredefinedField);
        Assert.AreEqual((int)ComRuleMatchType.Contains, saved.MatchType);
        Assert.AreEqual("X-Header", saved.HeaderField);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_PredefinedFieldSaveFailureRetainsStagedValueForRetry()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, "X-Header")
            })
        {
            FailSave = true
        };
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var criterion = rules[0].Criterias[0];
        criterion.PredefinedField = ComRulePredefinedField.Body;

        var saveFailure = Assert.ThrowsExactly<COMException>(criterion.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, criterion.ID);
        Assert.AreEqual(10, criterion.RuleID);
        Assert.AreEqual(ComRulePredefinedField.Body, criterion.PredefinedField);
        Assert.AreEqual((int)ComRulePredefinedField.Body, store.SavedCriteria[0].PredefinedField);

        store.FailSave = false;
        criterion.Save();

        Assert.AreEqual(2, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[1].Id);
        Assert.AreEqual(10, store.SavedCriteria[1].RuleId);
        Assert.AreEqual((int)ComRulePredefinedField.Body, store.SavedCriteria[1].PredefinedField);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_SaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty)
            })
        {
            FailSave = true
        };
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var criterion = rules[0].Criterias[0];
        const string retryHeaderField = "X-Retry-Header";
        criterion.HeaderField = retryHeaderField;

        var saveFailure = Assert.ThrowsExactly<COMException>(criterion.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, criterion.ID);
        Assert.AreEqual(retryHeaderField, criterion.HeaderField);

        store.FailSave = false;
        criterion.Save();

        Assert.AreEqual(2, store.SavedCriteria.Count);
        Assert.AreEqual(retryHeaderField, store.SavedCriteria[1].HeaderField);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_MatchValueSaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty)
            })
        {
            FailSave = true
        };
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var criterion = rules[0].Criterias[0];
        const string retryMatchValue = "X-Retry-Match\t";
        criterion.MatchValue = retryMatchValue;

        var saveFailure = Assert.ThrowsExactly<COMException>(criterion.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedCriteria.Count);
        Assert.AreEqual(100, criterion.ID);
        Assert.AreEqual(retryMatchValue, criterion.MatchValue);

        store.FailSave = false;
        criterion.Save();

        Assert.AreEqual(2, store.SavedCriteria.Count);
        Assert.AreEqual(100, store.SavedCriteria[1].Id);
        Assert.AreEqual(10, store.SavedCriteria[1].RuleId);
        Assert.AreEqual(retryMatchValue, store.SavedCriteria[1].MatchValue);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_DeleteUsesOwningRuleScopeAndNoOpsWhenRepeatedOrStale()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(200, 20, "foreign", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var criteria = rules[0].Criterias;
        var indexItem = criteria[0];
        var dbidItem = criteria.get_ItemByDBID(100);

        indexItem.Delete();
        dbidItem.Delete();

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedCriteria);
        Assert.AreEqual(0, criteria.Count);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(100)).ErrorCode);
    }

    [TestMethod]
    public void AuthorizedRuleCriteria_DeleteMapsStoreFailureToEFailAndRetainsSnapshot()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[] { Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty) });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1)
            });
        var criteria = rules[0].Criterias;
        var criterion = criteria[0];
        store.FailDelete = true;

        var deleteFailure = Assert.ThrowsExactly<COMException>(criterion.Delete);

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedCriteria);
        Assert.AreEqual(1, criteria.Count);
        Assert.AreEqual(100, criteria[0].ID);

        store.FailDelete = false;
        criterion.Delete();

        Assert.AreEqual(0, criteria.Count);
    }

    [TestMethod]
    public void FailedReauthentication_DeniesNewRulesAccessButRetainedCriteriasCanDelete()
    {
        var ruleStore = new FixedRuleAdministrationStore(
            new[]
            {
                new RuleAdministrationSnapshot(10, 0, "Global rule", true, true, 1)
            });
        var criteriaStore = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(100, 10, "first", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty)
            });
        RuleAdministrationRuntimeHost.Configure(ruleStore);
        RuleCriteriaAdministrationRuntimeHost.Configure(criteriaStore);
        var application = Application.CreateForRuntime(new TestAdministratorAuthenticationProvider("secret"));

        Assert.IsNotNull(application.Authenticate("Administrator", "secret"));
        var rules = application.Rules;
        var criteria = rules[0].Criterias;

        Assert.AreEqual(1, ruleStore.ReadCount);
        Assert.AreEqual(1, criteriaStore.ReadCount);
        Assert.IsNull(application.Authenticate("Administrator", "wrong"));

        var newRulesError = Assert.ThrowsExactly<COMException>(() => _ = application.Rules);

        Assert.AreEqual(EAccessDenied, newRulesError.ErrorCode);
        Assert.AreEqual(1, ruleStore.ReadCount);
        criteria.DeleteByDBID(100);
        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            criteriaStore.DeletedCriteria);
        Assert.AreEqual(0, criteria.Count);
    }

    [TestMethod]
    public void AuthorizedRule_UsesConfiguredRuleScopedRuntime()
    {
        var store = new MutableRuleCriteriaAdministrationStore(
            new[]
            {
                Snapshot(300, 20, "outside", true, ComRulePredefinedField.To, ComRuleMatchType.Equals, string.Empty),
                Snapshot(200, 10, "second", true, ComRulePredefinedField.Subject, ComRuleMatchType.Contains, string.Empty),
                Snapshot(100, 10, "first", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Test")
            });
        RuleCriteriaAdministrationRuntimeHost.Configure(store);
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
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(400, 10, "refreshed", false, ComRulePredefinedField.Unknown, ComRuleMatchType.Equals, "X-Refresh"),
                Snapshot(500, 20, "outside refreshed", true, ComRulePredefinedField.To, ComRuleMatchType.Equals, string.Empty)
            });

        criteria.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(1, criteria.Count);
        AssertCriterion(
            criteria[0],
            400,
            10,
            "refreshed",
            false,
            ComRulePredefinedField.Unknown,
            ComRuleMatchType.Equals,
            "X-Refresh");
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(100)).ErrorCode);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => _ = criteria.get_ItemByDBID(500)).ErrorCode);
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

    private sealed class MutableRuleCriteriaAdministrationStore(
        IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria)
        : IRuleCriteriaAdministrationStore
    {
        private IReadOnlyList<RuleCriteriaAdministrationSnapshot> _criteria = criteria;

        public int ReadCount { get; private set; }

        public List<(int RuleId, int DatabaseId)> DeletedCriteria { get; } = [];

        public List<RuleCriteriaAdministrationSnapshot> SavedCriteria { get; } = [];

        public bool FailDelete { get; set; }

        public bool FailSave { get; set; }

        public void Replace(IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria)
        {
            _criteria = criteria;
        }

        public ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
            int ruleId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>(
                _criteria.Where(criterion => criterion.RuleId == ruleId)
                    .OrderBy(static criterion => criterion.Id)
                    .ToArray());
        }

        public ValueTask DeleteRuleCriteriaByIdAsync(
            int ruleId,
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedCriteria.Add((ruleId, databaseId));
            if (FailDelete)
            {
                throw new InvalidOperationException("Simulated store failure.");
            }

            _criteria = _criteria
                .Where(criterion => criterion.RuleId != ruleId || criterion.Id != databaseId)
                .ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask SaveRuleCriteriaAsync(
            RuleCriteriaAdministrationSnapshot criterion,
            CancellationToken cancellationToken)
        {
            SavedCriteria.Add(criterion);
            if (FailSave)
            {
                throw new InvalidOperationException("Simulated store failure.");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedRuleAdministrationStore(IReadOnlyList<RuleAdministrationSnapshot> rules)
        : IRuleAdministrationStore
    {
        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<RuleAdministrationSnapshot>>(
                rules.Where(rule => rule.AccountId == accountId)
                    .OrderBy(static rule => rule.SortOrder)
                    .ToArray());
        }
    }

    private sealed class TestAdministratorAuthenticationProvider(string password)
        : IServerAdministratorAuthenticationProvider
    {
        public bool Authenticate(string username, string attemptedPassword) =>
            string.Equals(username, "Administrator", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(attemptedPassword, password, StringComparison.Ordinal);
    }
}
