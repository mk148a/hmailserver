using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class RuleActionsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsMarshalingAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceRuleActions),
            "DBFD3E11-9121-4DDD-944B-5AF29BF3D2DF",
            new[]
            {
                "get_Item", "get_ItemByDBID", "get_Count", "Add", "DeleteByDBID",
                "Refresh", "Delete"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceRuleActions).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            6,
            typeof(IInterfaceRuleActions).GetMethod(nameof(IInterfaceRuleActions.Delete))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceRuleAction),
            "F3F4A3E1-695E-499E-9F31-712DA8126982",
            new[]
            {
                "get_ID", "get_RuleID", "set_RuleID", "get_Type", "set_Type",
                "get_Subject", "set_Subject", "get_Body", "set_Body", "get_FromName",
                "set_FromName", "get_FromAddress", "set_FromAddress", "get_Filename",
                "set_Filename", "get_To", "set_To", "get_IMAPFolder", "set_IMAPFolder",
                "Save", "get_ScriptFunction", "set_ScriptFunction", "MoveUp", "MoveDown",
                "get_HeaderName", "set_HeaderName", "get_Value", "set_Value", "Delete",
                "get_RouteID", "set_RouteID", "get_AbortSpamFlagged", "set_AbortSpamFlagged"
            });
        foreach (var propertyName in new[]
                 {
                     nameof(IInterfaceRuleAction.Subject), nameof(IInterfaceRuleAction.Body),
                     nameof(IInterfaceRuleAction.FromName), nameof(IInterfaceRuleAction.FromAddress),
                     nameof(IInterfaceRuleAction.Filename), nameof(IInterfaceRuleAction.To),
                     nameof(IInterfaceRuleAction.IMAPFolder), nameof(IInterfaceRuleAction.ScriptFunction),
                     nameof(IInterfaceRuleAction.HeaderName), nameof(IInterfaceRuleAction.Value)
                 })
        {
            AssertStringPropertyMarshaling(propertyName);
        }

        var abortSpamFlagged = typeof(IInterfaceRuleAction).GetProperty(
            nameof(IInterfaceRuleAction.AbortSpamFlagged));
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            abortSpamFlagged?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.VariantBool,
            abortSpamFlagged?.SetMethod?.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void RuleActionEnum_PreservesLegacyGuidNamesAndValues()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD04"), typeof(ComRuleActionType).GUID);
        CollectionAssert.AreEqual(
            new[]
            {
                "Unknown", "DeleteEmail", "ForwardEmail", "Reply", "MoveToImapFolder",
                "RunScriptFunction", "StopRuleProcessing", "SetHeaderValue", "SendUsingRoute",
                "CreateCopy", "BindToAddress"
            },
            Enum.GetNames<ComRuleActionType>());
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 11).ToArray(),
            Enum.GetValues<ComRuleActionType>().Select(static value => (int)value).ToArray());
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<RuleActions>(
            "32A21952-5421-4A6C-835A-41050D0493C1",
            "hMailServer.RuleActions.1",
            typeof(IInterfaceRuleActions));
        AssertComClass<RuleAction>(
            "35548CC2-14AE-4795-8A19-C78FDE208504",
            "hMailServer.RuleAction.1",
            typeof(IInterfaceRuleAction));
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var store = new MutableRuleActionAdministrationStore([]);
        RuleActionAdministrationRuntimeHost.Configure(store);
        var actionsError = Assert.ThrowsExactly<COMException>(() => _ = new RuleActions().Count);
        var actionsRefreshError = Assert.ThrowsExactly<COMException>(new RuleActions().Refresh);
        var actionsDeleteError = Assert.ThrowsExactly<COMException>(() => new RuleActions().DeleteByDBID(100));
        var actionsIndexDeleteError = Assert.ThrowsExactly<COMException>(() => new RuleActions().Delete(0));
        var actionError = Assert.ThrowsExactly<COMException>(() => _ = new RuleAction().Type);
        var actionSubjectError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().Subject = "Detached");
        var actionBodyError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().Body = "Detached");
        var actionFromNameError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().FromName = "Detached");
        var actionFromAddressError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().FromAddress = "Detached");
        var actionFilenameError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().Filename = "Detached");
        var actionToError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().To = "Detached");
        var actionScriptFunctionError = Assert.ThrowsExactly<COMException>(
            () => new RuleAction().ScriptFunction = "Detached");
        var actionSaveError = Assert.ThrowsExactly<COMException>(new RuleAction().Save);
        var actionDeleteError = Assert.ThrowsExactly<COMException>(new RuleAction().Delete);

        Assert.AreEqual(EAccessDenied, actionsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionsRefreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionsDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionsIndexDeleteError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionSubjectError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionBodyError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionFromNameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionFromAddressError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionFilenameError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionToError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionScriptFunctionError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionSaveError.ErrorCode);
        Assert.AreEqual(EAccessDenied, actionDeleteError.ErrorCode);
        Assert.AreEqual(0, store.ReadCount);
        Assert.AreEqual(0, store.SavedActions.Count);
        Assert.AreEqual(0, store.DeletedActions.Count);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlySnapshotsAndLegacyLookupErrors()
    {
        IInterfaceRuleActions actions = RuleActions.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 10, ComRuleActionType.SendUsingRoute, 2)
            });

        Assert.AreEqual(2, actions.Count);
        AssertAction(actions[0], 100, 10, ComRuleActionType.Reply);
        AssertAction(actions.get_ItemByDBID(200), 200, 10, ComRuleActionType.SendUsingRoute);

        AssertError(DispEBadIndex, () => _ = actions[2]);
        AssertError(DispEBadIndex, () => _ = actions.get_ItemByDBID(300));
        AssertError(ENotImplemented, () => actions.Add());
        AssertError(ENotImplemented, () => actions.DeleteByDBID(100));
        AssertError(ENotImplemented, actions.Refresh);
        AssertError(ENotImplemented, () => actions.Delete(0));

        var action = actions[0];
        foreach (var mutation in new Action[]
                 {
                     () => action.RuleID = 20,
                     () => action.Type = ComRuleActionType.DeleteEmail,
                     () => action.Subject = "changed",
                     () => action.Body = "changed",
                     () => action.FromName = "changed",
                     () => action.FromAddress = "changed@example.test",
                     () => action.Filename = "changed.eml",
                     () => action.To = "changed@example.test",
                     () => action.IMAPFolder = "Changed",
                     () => action.HeaderName = "X-Changed",
                     () => action.Value = "changed",
                     () => action.RouteID = 200,
                     () => action.AbortSpamFlagged = false,
                     action.Save,
                     action.MoveUp,
                     action.MoveDown,
                     action.Delete
                 })
        {
            AssertError(ENotImplemented, mutation);
        }
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var failReload = false;
        var reloads = 0;
        IInterfaceRuleActions actions = RuleActions.CreateAuthorized(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
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
                    Snapshot(200, 10, ComRuleActionType.SetHeaderValue, 1),
                    Snapshot(300, 10, ComRuleActionType.SendUsingRoute, 2)
                };
            });

        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(ComRuleActionType.Reply, actions[0].Type);

        actions.Refresh();

        Assert.AreEqual(1, reloads);
        Assert.AreEqual(2, actions.Count);
        AssertAction(actions[0], 200, 10, ComRuleActionType.SetHeaderValue);
        Assert.AreEqual(ComRuleActionType.SendUsingRoute, actions.get_ItemByDBID(300).Type);
        AssertError(DispEBadIndex, () => _ = actions.get_ItemByDBID(100));

        failReload = true;
        AssertError(EFail, actions.Refresh);

        Assert.AreEqual(2, reloads);
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(ComRuleActionType.SetHeaderValue, actions.get_ItemByDBID(200).Type);
    }

    [TestMethod]
    public void AuthorizedRule_UsesConfiguredRuleScopedRuntimeAndLegacyOrdering()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(300, 20, ComRuleActionType.DeleteEmail, 1),
                Snapshot(200, 10, ComRuleActionType.ForwardEmail, 2),
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });

        var actions = rules[0].Actions;

        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(100, actions[0].ID);
        Assert.AreEqual(ComRuleActionType.Reply, actions[0].Type);
        AssertError(DispEBadIndex, () => _ = actions.get_ItemByDBID(300));
        Assert.AreEqual(1, store.ReadCount);

        store.Replace(
            new[]
            {
                Snapshot(500, 20, ComRuleActionType.DeleteEmail, 1),
                Snapshot(400, 10, ComRuleActionType.BindToAddress, 2),
                Snapshot(450, 10, ComRuleActionType.StopRuleProcessing, 1)
            });

        actions.Refresh();

        Assert.AreEqual(2, store.ReadCount);
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(450, actions[0].ID);
        Assert.AreEqual(ComRuleActionType.StopRuleProcessing, actions[0].Type);
        Assert.AreEqual(ComRuleActionType.BindToAddress, actions.get_ItemByDBID(400).Type);
        AssertError(DispEBadIndex, () => _ = actions.get_ItemByDBID(100));
        AssertError(DispEBadIndex, () => _ = actions.get_ItemByDBID(500));
    }

    [TestMethod]
    public void AuthorizedRule_DeleteByIndexDeletesOnlySelectedActionAndNoOpsForInvalidIndices()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 10, ComRuleActionType.SendUsingRoute, 2),
                Snapshot(300, 10, ComRuleActionType.DeleteEmail, 3),
                Snapshot(400, 20, ComRuleActionType.StopRuleProcessing, 1)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var actions = rules[0].Actions;

        actions.Delete(-1);
        actions.Delete(3);

        Assert.AreEqual(3, actions.Count);
        Assert.AreEqual(0, store.DeletedActions.Count);

        actions.Delete(1);

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 200) },
            store.DeletedActions);
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(100, actions[0].ID);
        Assert.AreEqual(300, actions[1].ID);
        Assert.AreEqual(10, actions[1].RuleID);

        actions.Delete(1);

        CollectionAssert.AreEqual(
            new[]
            {
                (RuleId: 10, DatabaseId: 200),
                (RuleId: 10, DatabaseId: 300)
            },
            store.DeletedActions);
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(100, actions[0].ID);
    }

    [TestMethod]
    public void AuthorizedRule_DeleteByIndexMapsStoreFailureToEFailAndRetainsSnapshot()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 10, ComRuleActionType.SendUsingRoute, 2)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1)
            });
        var actions = rules[0].Actions;
        store.FailDelete = true;

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => actions.Delete(0));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedActions);
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(100, actions[0].ID);
        Assert.AreEqual(200, actions[1].ID);
    }

    [TestMethod]
    public void AuthorizedRule_DeleteByDBIDScopesStoreCallAndRetainsSnapshotOnFailure()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 10, ComRuleActionType.SendUsingRoute, 2),
                Snapshot(300, 20, ComRuleActionType.DeleteEmail, 1)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var actions = rules[0].Actions;

        actions.DeleteByDBID(300);
        actions.DeleteByDBID(999);

        Assert.AreEqual(0, store.DeletedActions.Count);
        Assert.AreEqual(2, actions.Count);

        store.FailDelete = true;
        var deleteFailure = Assert.ThrowsExactly<COMException>(() => actions.DeleteByDBID(100));

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedActions);
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(100, actions[0].ID);

        store.FailDelete = false;
        actions.DeleteByDBID(100);

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100), (RuleId: 10, DatabaseId: 100) },
            store.DeletedActions);
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(200, actions[0].ID);

        actions.DeleteByDBID(100);
        actions.DeleteByDBID(999);

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100), (RuleId: 10, DatabaseId: 100) },
            store.DeletedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_DeleteUsesOwningRuleScopeAndNoOpsWhenRepeatedOrStale()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 20, ComRuleActionType.DeleteEmail, 1)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var actions = rules[0].Actions;
        var indexItem = actions[0];
        var dbidItem = actions.get_ItemByDBID(100);

        indexItem.Delete();
        dbidItem.Delete();

        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedActions);
        Assert.AreEqual(0, actions.Count);
        AssertError(DispEBadIndex, () => _ = actions.get_ItemByDBID(100));
    }

    [TestMethod]
    public void AuthorizedRuleAction_SaveUsesOwningRuleScopeAndPersistsSnapshot()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 20, ComRuleActionType.DeleteEmail, 1)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });

        rules[0].Actions[0].Save();

        CollectionAssert.AreEqual(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_SaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        const string rawSubject = "  Raw\tSubject \r\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) })
        {
            FailSave = true
        };
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];
        action.Subject = rawSubject;

        var saveFailure = Assert.ThrowsExactly<COMException>(action.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedActions.Count);
        Assert.AreEqual(100, action.ID);

        store.FailSave = false;
        action.Save();

        Assert.AreEqual(2, store.SavedActions.Count);
        Assert.AreEqual(rawSubject, store.SavedActions[0].Subject);
        Assert.AreEqual(rawSubject, store.SavedActions[1].Subject);
    }

    [TestMethod]
    public void AuthorizedRuleAction_TypeSetterStagesForOwningActionAndSavePersistsIt()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1),
                Snapshot(200, 20, ComRuleActionType.DeleteEmail, 1)
            });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1),
                new RuleAdministrationSnapshot(20, 1000, "Second rule", true, true, 2)
            });
        var action = rules[0].Actions[0];

        action.Type = ComRuleActionType.RunScriptFunction;

        Assert.AreEqual(ComRuleActionType.RunScriptFunction, action.Type);
        action.Save();

        CollectionAssert.AreEqual(
            new[] { Snapshot(100, 10, ComRuleActionType.RunScriptFunction, 1) },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_TypeSetterDeniesScriptFunctionForNonAdministrator()
    {
        var saved = new List<RuleActionAdministrationSnapshot>();
        IInterfaceRuleActions actions = RuleActions.CreateAuthorized(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) },
            save: saved.Add,
            isServerAdministrator: static () => false);
        var action = actions[0];

        var error = Assert.ThrowsExactly<COMException>(
            () => action.Type = ComRuleActionType.RunScriptFunction);

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual(ComRuleActionType.Reply, action.Type);
        Assert.AreEqual(0, saved.Count);
    }

    [TestMethod]
    public void AuthorizedRuleAction_ScriptFunctionSetterStagesRawValueAndSavePersistsIt()
    {
        const string rawScriptFunction = "  Raw.Function \t";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.RunScriptFunction, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.ScriptFunction = rawScriptFunction;

        Assert.AreEqual(rawScriptFunction, action.ScriptFunction);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.RunScriptFunction, 1)
                    with { ScriptFunction = rawScriptFunction }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_SubjectSetterStagesRawValueAndSavePersistsIt()
    {
        const string rawSubject = "  Raw\tSubject \r\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.Subject = rawSubject;

        Assert.AreEqual(rawSubject, action.Subject);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
                    with { Subject = rawSubject }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_BodySetterStagesRawValueAndSavePersistsIt()
    {
        const string rawBody = "  Raw\tBody \r\nNext\rLine\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.Body = rawBody;

        Assert.AreEqual(rawBody, action.Body);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
                    with { Body = rawBody }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_FromNameSetterStagesRawValueAndSavePersistsIt()
    {
        const string rawFromName = "  Raw\tFrom Name \r\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.FromName = rawFromName;

        Assert.AreEqual(rawFromName, action.FromName);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
                    with { FromName = rawFromName }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_FromAddressSetterStagesRawValueAndSavePersistsIt()
    {
        const string rawFromAddress = "  raw.sender+tag@example.test\t\r\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.FromAddress = rawFromAddress;

        Assert.AreEqual(rawFromAddress, action.FromAddress);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
                    with { FromAddress = rawFromAddress }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_FilenameSetterStagesOpaqueRawValueAndSavePersistsIt()
    {
        const string rawFilename = @"  \\?\UNC\opaque-share\..\drop\message?.eml	 ";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.Filename = rawFilename;

        Assert.AreEqual(rawFilename, action.Filename);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
                    with { Filename = rawFilename }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_ToSetterStagesOpaqueRawValueAndSavePersistsIt()
    {
        const string rawTo = "Forward Name <forward@example.test>; \\\\127.0.0.1\\\\drop; http://127.0.0.1/";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];

        action.To = rawTo;

        Assert.AreEqual(rawTo, action.To);
        action.Save();

        CollectionAssert.AreEqual(
            new[]
            {
                Snapshot(100, 10, ComRuleActionType.Reply, 1)
                    with { To = rawTo }
            },
            store.SavedActions);
    }

    [TestMethod]
    public void AuthorizedRuleAction_BodySaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        const string rawBody = "  Retry\tBody\r\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) })
        {
            FailSave = true
        };
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];
        action.Body = rawBody;

        var saveFailure = Assert.ThrowsExactly<COMException>(action.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedActions.Count);
        Assert.AreEqual(rawBody, store.SavedActions[0].Body);

        store.FailSave = false;
        action.Save();

        Assert.AreEqual(2, store.SavedActions.Count);
        Assert.AreEqual(rawBody, store.SavedActions[1].Body);
        Assert.AreEqual(rawBody, action.Body);
    }

    [TestMethod]
    public void AuthorizedRuleAction_FilenameSaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        const string rawFilename = @"\\?\UNC\retry-share\..\opaque\retry.eml";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) })
        {
            FailSave = true
        };
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];
        action.Filename = rawFilename;

        var saveFailure = Assert.ThrowsExactly<COMException>(action.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedActions.Count);
        Assert.AreEqual(rawFilename, store.SavedActions[0].Filename);

        store.FailSave = false;
        action.Save();

        Assert.AreEqual(2, store.SavedActions.Count);
        Assert.AreEqual(rawFilename, store.SavedActions[1].Filename);
        Assert.AreEqual(rawFilename, action.Filename);
    }

    [TestMethod]
    public void AuthorizedRuleAction_ToSaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        const string rawTo = "Forward Name <forward@example.test>; \\\\127.0.0.1\\\\drop; http://127.0.0.1/";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) })
        {
            FailSave = true
        };
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];
        action.To = rawTo;

        var saveFailure = Assert.ThrowsExactly<COMException>(action.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedActions.Count);
        Assert.AreEqual(rawTo, store.SavedActions[0].To);
        Assert.AreEqual(rawTo, action.To);

        store.FailSave = false;
        action.Save();

        Assert.AreEqual(2, store.SavedActions.Count);
        Assert.AreEqual(rawTo, store.SavedActions[1].To);
        Assert.AreEqual(rawTo, action.To);
    }

    [TestMethod]
    public void AuthorizedRuleAction_FromNameSaveMapsStoreFailureToEFailAndAllowsRetry()
    {
        const string rawFromName = "  Retry\tFrom Name\r\n";
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) })
        {
            FailSave = true
        };
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[] { new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1) });
        var action = rules[0].Actions[0];
        action.FromName = rawFromName;

        var saveFailure = Assert.ThrowsExactly<COMException>(action.Save);

        Assert.AreEqual(EFail, saveFailure.ErrorCode);
        Assert.AreEqual(1, store.SavedActions.Count);
        Assert.AreEqual(rawFromName, store.SavedActions[0].FromName);

        store.FailSave = false;
        action.Save();

        Assert.AreEqual(2, store.SavedActions.Count);
        Assert.AreEqual(rawFromName, store.SavedActions[1].FromName);
        Assert.AreEqual(rawFromName, action.FromName);
    }

    [TestMethod]
    public void AuthorizedRuleAction_ScriptFunctionSetterDeniesNonAdministrator()
    {
        var saved = new List<RuleActionAdministrationSnapshot>();
        IInterfaceRuleActions actions = RuleActions.CreateAuthorized(
            new[] { Snapshot(100, 10, ComRuleActionType.RunScriptFunction, 1) },
            save: saved.Add,
            isServerAdministrator: static () => false);
        var action = actions[0];

        var error = Assert.ThrowsExactly<COMException>(
            () => action.ScriptFunction = "Changed");

        Assert.AreEqual(EAccessDenied, error.ErrorCode);
        Assert.AreEqual("HandleMessage", action.ScriptFunction);
        Assert.AreEqual(0, saved.Count);
    }

    [TestMethod]
    public void AuthorizedRuleAction_DeleteMapsStoreFailureToEFailAndRetainsSnapshot()
    {
        var store = new MutableRuleActionAdministrationStore(
            new[] { Snapshot(100, 10, ComRuleActionType.Reply, 1) });
        RuleActionAdministrationRuntimeHost.Configure(store);
        var rules = Rules.CreateAuthorized(
            new[]
            {
                new RuleAdministrationSnapshot(10, 1000, "First rule", true, true, 1)
            });
        var actions = rules[0].Actions;
        var action = actions[0];
        store.FailDelete = true;

        var deleteFailure = Assert.ThrowsExactly<COMException>(action.Delete);

        Assert.AreEqual(EFail, deleteFailure.ErrorCode);
        CollectionAssert.AreEqual(
            new[] { (RuleId: 10, DatabaseId: 100) },
            store.DeletedActions);
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(100, actions[0].ID);

        store.FailDelete = false;
        action.Delete();

        Assert.AreEqual(0, actions.Count);
    }

    private static RuleActionAdministrationSnapshot Snapshot(
        int id,
        int ruleId,
        ComRuleActionType type,
        int sortOrder) =>
        new(
            id,
            ruleId,
            (int)type,
            "Reply subject",
            "Reply body",
            "Example Sender",
            "sender@example.test",
            "message.eml",
            "recipient@example.test",
            "Archive",
            "HandleMessage",
            "X-Rule",
            "matched",
            500,
            true,
            sortOrder);

    private static void AssertAction(
        IInterfaceRuleAction action,
        int id,
        int ruleId,
        ComRuleActionType type)
    {
        Assert.AreEqual(id, action.ID);
        Assert.AreEqual(ruleId, action.RuleID);
        Assert.AreEqual(type, action.Type);
        Assert.AreEqual("Reply subject", action.Subject);
        Assert.AreEqual("Reply body", action.Body);
        Assert.AreEqual("Example Sender", action.FromName);
        Assert.AreEqual("sender@example.test", action.FromAddress);
        Assert.AreEqual("message.eml", action.Filename);
        Assert.AreEqual("recipient@example.test", action.To);
        Assert.AreEqual("Archive", action.IMAPFolder);
        Assert.AreEqual("HandleMessage", action.ScriptFunction);
        Assert.AreEqual("X-Rule", action.HeaderName);
        Assert.AreEqual("matched", action.Value);
        Assert.AreEqual(500, action.RouteID);
        Assert.IsTrue(action.AbortSpamFlagged);
    }

    private static void AssertError(int expectedError, Action action)
    {
        var error = Assert.ThrowsExactly<COMException>(action);
        Assert.AreEqual(expectedError, error.ErrorCode);
    }

    private static void AssertStringPropertyMarshaling(string propertyName)
    {
        var property = typeof(IInterfaceRuleAction).GetProperty(propertyName);
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

    private sealed class MutableRuleActionAdministrationStore(
        IReadOnlyList<RuleActionAdministrationSnapshot> actions)
        : IRuleActionAdministrationStore
    {
        private IReadOnlyList<RuleActionAdministrationSnapshot> _actions = actions;

        public int ReadCount { get; private set; }

        public bool FailDelete { get; set; }

        public bool FailSave { get; set; }

        public List<(int RuleId, int DatabaseId)> DeletedActions { get; } = [];

        public List<RuleActionAdministrationSnapshot> SavedActions { get; } = [];

        public void Replace(IReadOnlyList<RuleActionAdministrationSnapshot> actions)
        {
            _actions = actions;
        }

        public ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
            int ruleId,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<IReadOnlyList<RuleActionAdministrationSnapshot>>(
                _actions.Where(action => action.RuleId == ruleId)
                    .OrderBy(static action => action.SortOrder)
                    .ToArray());
        }

        public ValueTask DeleteRuleActionByIdAsync(
            int ruleId,
            int databaseId,
            CancellationToken cancellationToken)
        {
            DeletedActions.Add((ruleId, databaseId));
            if (FailDelete)
            {
                throw new InvalidOperationException("Simulated store failure.");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask SaveRuleActionAsync(
            RuleActionAdministrationSnapshot action,
            CancellationToken cancellationToken)
        {
            SavedActions.Add(action);
            if (FailSave)
            {
                throw new InvalidOperationException("Simulated store failure.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
