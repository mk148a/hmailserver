using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class DomainsComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceDomains),
            "2CDFD68F-62F2-49CF-A14A-505E7F68EE9C",
            new[]
            {
                "get_Item", "Refresh", "get_Count", "Add", "get_ItemByName", "get_ItemByDBID",
                "get_Names", "DeleteByDBID"
            });
        Assert.AreEqual(
            0,
            typeof(IInterfaceDomains).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceDomain),
            "3F50C3AF-67C0-4628-91D6-E2EAC7786830",
            new[]
            {
                "get_Name", "set_Name", "Save", "get_ID", "get_Active", "set_Active", "get_Accounts",
                "Delete", "get_Aliases", "get_DistributionLists", "get_Postmaster", "set_Postmaster",
                "get_DomainAliases", "get_ADDomainName", "set_ADDomainName", "SynchronizeDirectory",
                "get_MaxMessageSize", "set_MaxMessageSize", "get_PlusAddressingEnabled",
                "set_PlusAddressingEnabled", "get_PlusAddressingCharacter", "set_PlusAddressingCharacter",
                "get_AntiSpamEnableGreylisting", "set_AntiSpamEnableGreylisting", "get_MaxSize", "set_MaxSize",
                "get_Size", "get_AllocatedSize", "get_SignatureEnabled", "set_SignatureEnabled",
                "get_SignatureMethod", "set_SignatureMethod", "get_SignaturePlainText",
                "set_SignaturePlainText", "get_SignatureHTML", "set_SignatureHTML",
                "get_AddSignaturesToReplies", "set_AddSignaturesToReplies", "get_AddSignaturesToLocalMail",
                "set_AddSignaturesToLocalMail", "get_MaxNumberOfAccounts", "set_MaxNumberOfAccounts",
                "get_MaxNumberOfAliases", "set_MaxNumberOfAliases", "get_MaxNumberOfDistributionLists",
                "set_MaxNumberOfDistributionLists", "get_MaxNumberOfAccountsEnabled",
                "set_MaxNumberOfAccountsEnabled", "get_MaxNumberOfAliasesEnabled",
                "set_MaxNumberOfAliasesEnabled", "get_MaxNumberOfDistributionListsEnabled",
                "set_MaxNumberOfDistributionListsEnabled", "get_MaxAccountSize", "set_MaxAccountSize",
                "get_DKIMSignEnabled", "set_DKIMSignEnabled", "get_DKIMSelector", "set_DKIMSelector",
                "get_DKIMPrivateKeyFile", "set_DKIMPrivateKeyFile", "get_DKIMHeaderCanonicalizationMethod",
                "set_DKIMHeaderCanonicalizationMethod", "get_DKIMBodyCanonicalizationMethod",
                "set_DKIMBodyCanonicalizationMethod", "get_DKIMSigningAlgorithm", "set_DKIMSigningAlgorithm",
                "get_DKIMSignAliasesEnabled", "set_DKIMSignAliasesEnabled"
            });
        Assert.AreEqual(
            1,
            typeof(IInterfaceDomain).GetProperty(nameof(IInterfaceDomain.Name))?.GetCustomAttribute<DispIdAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Domains>(
            "82AFD03C-58A4-4F04-8277-6B2812780E45",
            "hMailServer.Domains.1",
            typeof(IInterfaceDomains));
        AssertComClass<Domain>(
            "C535E4AF-9DB3-41FC-B434-FFCDAE0EFBD5",
            "hMailServer.Domain.1",
            typeof(IInterfaceDomain));
    }

    [TestMethod]
    public void DomainEnums_PreserveLegacyValuesAndGuids()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD08"), typeof(ComDomainSignatureMethod).GUID);
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD13"), typeof(ComDkimCanonicalizationMethod).GUID);
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD14"), typeof(ComDkimAlgorithm).GUID);
        var signatureMethodValues = Enum.GetNames<ComDomainSignatureMethod>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComDomainSignatureMethod>(name)));
        var canonicalizationValues = Enum.GetNames<ComDkimCanonicalizationMethod>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComDkimCanonicalizationMethod>(name)));
        var algorithmValues = Enum.GetNames<ComDkimAlgorithm>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComDkimAlgorithm>(name)));

        Assert.AreEqual(0, signatureMethodValues[nameof(ComDomainSignatureMethod.Unknown)]);
        Assert.AreEqual(1, signatureMethodValues[nameof(ComDomainSignatureMethod.SetIfNotSpecifiedInAccount)]);
        Assert.AreEqual(2, signatureMethodValues[nameof(ComDomainSignatureMethod.OverwriteAccountSignature)]);
        Assert.AreEqual(3, signatureMethodValues[nameof(ComDomainSignatureMethod.AppendToAccountSignature)]);
        Assert.AreEqual(1, canonicalizationValues[nameof(ComDkimCanonicalizationMethod.Simple)]);
        Assert.AreEqual(2, canonicalizationValues[nameof(ComDkimCanonicalizationMethod.Relaxed)]);
        Assert.AreEqual(1, algorithmValues[nameof(ComDkimAlgorithm.SHA1)]);
        Assert.AreEqual(2, algorithmValues[nameof(ComDkimAlgorithm.SHA256)]);
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var domainsError = Assert.ThrowsExactly<COMException>(() => _ = new Domains().Count);
        var namesError = Assert.ThrowsExactly<COMException>(() => _ = new Domains().Names);
        var refreshError = Assert.ThrowsExactly<COMException>(new Domains().Refresh);
        var domainError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().Name);
        var adDomainError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().ADDomainName);
        var sizeError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().Size);
        var allocatedSizeError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().AllocatedSize);
        var greylistingError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().AntiSpamEnableGreylisting);
        var signatureError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().SignatureEnabled);
        var dkimError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().DKIMSignEnabled);

        Assert.AreEqual(EAccessDenied, domainsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, namesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, refreshError.ErrorCode);
        Assert.AreEqual(EAccessDenied, domainError.ErrorCode);
        Assert.AreEqual(EAccessDenied, adDomainError.ErrorCode);
        Assert.AreEqual(EAccessDenied, sizeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, allocatedSizeError.ErrorCode);
        Assert.AreEqual(EAccessDenied, greylistingError.ErrorCode);
        Assert.AreEqual(EAccessDenied, signatureError.ErrorCode);
        Assert.AreEqual(EAccessDenied, dkimError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_RefreshAtomicallyReplacesSnapshotAndRetainsItOnFailure()
    {
        var refreshed = new[]
        {
            new DomainAdministrationSnapshot(20, "beta.example", false),
            new DomainAdministrationSnapshot(30, "gamma.example", true)
        };
        var failRefresh = false;
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            () => failRefresh
                ? throw new InvalidOperationException("store failed")
                : refreshed);

        domains.Refresh();

        Assert.AreEqual(2, domains.Count);
        Assert.AreEqual("20\tbeta.example\t0\r\n30\tgamma.example\t1\r\n", domains.Names);
        Assert.AreEqual("gamma.example", domains.get_ItemByDBID(30).Name);
        Assert.AreEqual(
            DispEBadIndex,
            Assert.ThrowsExactly<COMException>(() => domains.get_ItemByDBID(10)).ErrorCode);

        failRefresh = true;
        var failure = Assert.ThrowsExactly<COMException>(domains.Refresh);

        Assert.AreEqual(unchecked((int)0x80004005), failure.ErrorCode);
        Assert.AreEqual(2, domains.Count);
        Assert.AreEqual("beta.example", domains[0].Name);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesOnlyReadOnlySnapshotAndLegacyLookupErrors()
    {
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[]
            {
                new DomainAdministrationSnapshot(
                    10,
                    "alpha.example",
                    true,
                    Postmaster: "postmaster@alpha.example",
                    MaxMessageSize: 1024,
                    PlusAddressingEnabled: true,
                    PlusAddressingCharacter: "+",
                    AntiSpamEnableGreylisting: true,
                    AdDomainName: "corp.alpha.example",
                    MaxSize: 2048,
                    Size: 2,
                    AllocatedSize: 1536,
                    MaxNumberOfAccounts: 100,
                    MaxNumberOfAliases: 25,
                    MaxNumberOfDistributionLists: 10,
                    MaxNumberOfAccountsEnabled: true,
                    MaxNumberOfAliasesEnabled: false,
                    MaxNumberOfDistributionListsEnabled: true,
                    MaxAccountSize: 512,
                    SignatureEnabled: true,
                    SignatureMethod: (int)ComDomainSignatureMethod.AppendToAccountSignature,
                    SignaturePlainText: "Alpha plain signature",
                    SignatureHtml: "<p>Alpha HTML signature</p>",
                    AddSignaturesToReplies: true,
                    AddSignaturesToLocalMail: false,
                    DkimSignEnabled: true,
                    DkimSelector: "alpha-selector",
                    DkimPrivateKeyFile: @"C:\keys\alpha.pem",
                    DkimHeaderCanonicalizationMethod: (int)ComDkimCanonicalizationMethod.Simple,
                    DkimBodyCanonicalizationMethod: (int)ComDkimCanonicalizationMethod.Relaxed,
                    DkimSigningAlgorithm: (int)ComDkimAlgorithm.SHA1,
                    DkimSignAliasesEnabled: true),
                new DomainAdministrationSnapshot(20, "beta.example", false)
            });

        Assert.AreEqual(2, domains.Count);
        Assert.AreEqual("10\talpha.example\t1\r\n20\tbeta.example\t0\r\n", domains.Names);
        AssertDomain(domains[0], 10, "alpha.example", true);
        AssertCoreScalars(domains[0]);
        AssertSignatureScalars(domains[0]);
        AssertDkimScalars(domains[0]);
        AssertDomain(domains.get_ItemByName("BETA.EXAMPLE"), 20, "beta.example", false);
        AssertDomain(domains.get_ItemByDBID(10), 10, "alpha.example", true);
        Assert.IsFalse(domains[1].AntiSpamEnableGreylisting);
        Assert.AreEqual(0, domains[1].Size);
        Assert.AreEqual(0L, domains[1].AllocatedSize);
        AssertSignatureDefaults(domains[1]);
        AssertDkimDefaults(domains[1]);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = domains[2]);
        var badName = Assert.ThrowsExactly<COMException>(() => _ = domains.get_ItemByName("missing.example"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(domains.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => domains[0].Active = false);
        var pendingScalarMutation = Assert.ThrowsExactly<COMException>(() => domains[0].Postmaster = "changed@alpha.example");
        var pendingGreylisting = Assert.ThrowsExactly<COMException>(
            () => domains[0].AntiSpamEnableGreylisting = false);
        var pendingAdDomainMutation = Assert.ThrowsExactly<COMException>(
            () => domains[0].ADDomainName = "changed.example");
        var pendingSignatureEnabled = Assert.ThrowsExactly<COMException>(() => domains[0].SignatureEnabled = false);
        var pendingSignatureMethod = Assert.ThrowsExactly<COMException>(
            () => domains[0].SignatureMethod = ComDomainSignatureMethod.OverwriteAccountSignature);
        var pendingSignaturePlain = Assert.ThrowsExactly<COMException>(
            () => domains[0].SignaturePlainText = "changed");
        var pendingSignatureHtml = Assert.ThrowsExactly<COMException>(
            () => domains[0].SignatureHTML = "<p>changed</p>");
        var pendingSignatureReplies = Assert.ThrowsExactly<COMException>(
            () => domains[0].AddSignaturesToReplies = false);
        var pendingSignatureLocal = Assert.ThrowsExactly<COMException>(
            () => domains[0].AddSignaturesToLocalMail = true);
        var pendingDkimEnabled = Assert.ThrowsExactly<COMException>(() => domains[0].DKIMSignEnabled = false);
        var pendingDkimSelector = Assert.ThrowsExactly<COMException>(() => domains[0].DKIMSelector = "changed");
        var pendingDkimKeyFile = Assert.ThrowsExactly<COMException>(() => domains[0].DKIMPrivateKeyFile = @"C:\keys\changed.pem");
        var pendingDkimHeader = Assert.ThrowsExactly<COMException>(
            () => domains[0].DKIMHeaderCanonicalizationMethod = ComDkimCanonicalizationMethod.Relaxed);
        var pendingDkimBody = Assert.ThrowsExactly<COMException>(
            () => domains[0].DKIMBodyCanonicalizationMethod = ComDkimCanonicalizationMethod.Simple);
        var pendingDkimAlgorithm = Assert.ThrowsExactly<COMException>(
            () => domains[0].DKIMSigningAlgorithm = ComDkimAlgorithm.SHA256);
        var pendingDkimAliases = Assert.ThrowsExactly<COMException>(() => domains[0].DKIMSignAliasesEnabled = false);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingScalarMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingGreylisting.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdDomainMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSignatureEnabled.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSignatureMethod.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSignaturePlain.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSignatureHtml.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSignatureReplies.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingSignatureLocal.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimEnabled.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimSelector.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimKeyFile.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimHeader.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimBody.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimAlgorithm.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimAliases.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_AddStagesLegacyDefaultsAndSavePublishesInsertedIdentity()
    {
        var inserted = new List<DomainAdministrationSnapshot>();
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            insert: domain =>
            {
                inserted.Add(domain);
                return 20;
            });

        var draft = domains.Add();

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(string.Empty, draft.Name);
        Assert.IsFalse(draft.Active);
        Assert.AreEqual("+", draft.PlusAddressingCharacter);
        Assert.AreEqual(ComDomainSignatureMethod.SetIfNotSpecifiedInAccount, draft.SignatureMethod);
        Assert.IsTrue(draft.AddSignaturesToLocalMail);
        Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, draft.DKIMHeaderCanonicalizationMethod);
        Assert.AreEqual(ComDkimAlgorithm.SHA256, draft.DKIMSigningAlgorithm);

        draft.Name = "beta.example";
        draft.Postmaster = "postmaster@beta.example";
        draft.Active = true;
        draft.MaxMessageSize = 2048;
        draft.PlusAddressingEnabled = true;
        draft.SignatureEnabled = true;

        Assert.AreEqual(1, domains.Count);
        draft.Save();

        Assert.AreEqual(2, domains.Count);
        Assert.AreEqual(20, draft.ID);
        Assert.AreEqual(1, inserted.Count);
        var persisted = inserted[0];
        Assert.AreEqual(0, persisted.Id);
        Assert.AreEqual("beta.example", persisted.Name);
        Assert.AreEqual("postmaster@beta.example", persisted.Postmaster);
        Assert.IsTrue(persisted.Active);
        Assert.AreEqual(2048, persisted.MaxMessageSize);
        Assert.IsTrue(persisted.PlusAddressingEnabled);
        Assert.AreEqual("beta.example", domains.get_ItemByDBID(20).Name);
    }

    [TestMethod]
    public void FailedInsert_MapsToEFailAndRetainsDraftWithoutPublishing()
    {
        var fail = true;
        IInterfaceDomains domains = Domains.CreateAuthorized(
            Array.Empty<DomainAdministrationSnapshot>(),
            insert: _ => fail
                ? throw new InvalidOperationException("Simulated store failure.")
                : 1);

        var draft = domains.Add();
        draft.Name = "beta.example";

        var saveFailure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual(0, domains.Count);
        Assert.AreEqual(0, draft.ID);

        draft.Name = "gamma.example";
        fail = false;
        draft.Save();

        Assert.AreEqual(1, domains.Count);
        Assert.AreEqual(1, draft.ID);
        Assert.AreEqual("gamma.example", domains.get_ItemByDBID(1).Name);
    }

    [TestMethod]
    public void AddAndMutate_RecheckLiveAuthentication()
    {
        var authenticated = true;
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            insert: _ => 11,
            isAuthenticated: () => authenticated);

        var draft = domains.Add();
        authenticated = false;

        var deniedAdd = Assert.ThrowsExactly<COMException>(() => domains.Add());
        var deniedSetter = Assert.ThrowsExactly<COMException>(() => draft.Name = "x");
        var deniedSave = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80070005), deniedAdd.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80070005), deniedSetter.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80070005), deniedSave.ErrorCode);
    }

    [TestMethod]
    public void ExistingRowSave_RemainsNotImplementedUntilUpdateParity()
    {
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            insert: _ => 11);

        var existing = domains[0];
        var pendingSave = Assert.ThrowsExactly<COMException>(existing.Save);
        var pendingDelete = Assert.ThrowsExactly<COMException>(existing.Delete);

        Assert.AreEqual(unchecked((int)0x80004001), pendingSave.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80004001), pendingDelete.ErrorCode);
    }
    [TestMethod]
    public void ExistingRowSave_PersistsStagedSettersAndReplacesCollectionSnapshot()
    {
        var updates = new List<DomainAdministrationSnapshot>();
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            insert: _ => 11,
            update: domain =>
            {
                updates.Add(domain);
                return true;
            });

        var existing = domains[0];
        existing.Name = "renamed.example";
        existing.Postmaster = "postmaster@renamed.example";
        existing.Active = false;
        existing.MaxMessageSize = 4096;
        existing.PlusAddressingEnabled = true;

        existing.Save();

        Assert.AreEqual(1, updates.Count);
        var persisted = updates[0];
        Assert.AreEqual(10, persisted.Id);
        Assert.AreEqual("renamed.example", persisted.Name);
        Assert.AreEqual("postmaster@renamed.example", persisted.Postmaster);
        Assert.IsFalse(persisted.Active);
        Assert.AreEqual(4096, persisted.MaxMessageSize);
        Assert.IsTrue(persisted.PlusAddressingEnabled);
        Assert.AreEqual("renamed.example", domains[0].Name);
        Assert.AreEqual("renamed.example", domains.get_ItemByName("RENAMED.EXAMPLE").Name);
    }

    [TestMethod]
    public void FailedUpdate_MapsToEFailAndRetainsStagedStateWithoutReplacingSnapshot()
    {
        var failUpdate = true;
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            update: _ => failUpdate
                ? throw new InvalidOperationException("Simulated store failure.")
                : true);

        var existing = domains[0];
        existing.Name = "changed.example";

        var saveFailure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual("alpha.example", domains[0].Name);

        existing.Name = "other.example";
        failUpdate = false;
        existing.Save();

        Assert.AreEqual("other.example", domains[0].Name);
    }

    [TestMethod]
    public void UnknownIdUpdate_MapsToEFailWhenStoreReportsNoAffectedRow()
    {
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            update: _ => false);

        var existing = domains[0];
        existing.Name = "changed.example";

        var saveFailure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual("alpha.example", domains[0].Name);
    }
    [TestMethod]
    public void DeleteByDBID_RemovesOnlyMatchingSnapshotAndTreatsUnknownAsNoOp()
    {
        var deletedIds = new List<int>();
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[]
            {
                new DomainAdministrationSnapshot(10, "alpha.example", true),
                new DomainAdministrationSnapshot(20, "beta.example", false)
            },
            delete: domainId =>
            {
                deletedIds.Add(domainId);
                return true;
            });

        domains.DeleteByDBID(10);

        Assert.AreEqual(1, domains.Count);
        Assert.AreEqual(20, domains[0].ID);
        Assert.AreEqual(DispEBadIndex, Assert.ThrowsExactly<COMException>(() => domains.get_ItemByDBID(10)).ErrorCode);

        domains.DeleteByDBID(999);
        Assert.AreEqual(1, domains.Count);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);
    }

    [TestMethod]
    public void FailedDelete_MapsToEFailAndRetainsSnapshot()
    {
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            delete: _ => false);

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => domains.DeleteByDBID(10));

        Assert.AreEqual(unchecked((int)0x80004005), deleteFailure.ErrorCode);
        Assert.AreEqual(1, domains.Count);
        Assert.AreEqual("alpha.example", domains[0].Name);
    }

    [TestMethod]
    public void ItemDelete_RoutesThroughOwningCollectionAndRechecksAuthentication()
    {
        var deletedIds = new List<int>();
        var authenticated = true;
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) },
            delete: domainId =>
            {
                deletedIds.Add(domainId);
                return true;
            },
            isAuthenticated: () => authenticated);

        domains[0].Delete();

        Assert.AreEqual(0, domains.Count);
        CollectionAssert.AreEqual(new[] { 10 }, deletedIds);

        var second = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(20, "beta.example", true) },
            insert: _ => 30,
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
        IInterfaceDomains domains = Domains.CreateAuthorized(
            new[] { new DomainAdministrationSnapshot(10, "alpha.example", true) });

        var pendingCollectionDelete = Assert.ThrowsExactly<COMException>(() => domains.DeleteByDBID(10));
        var pendingItemDelete = Assert.ThrowsExactly<COMException>(domains[0].Delete);

        Assert.AreEqual(unchecked((int)0x80004001), pendingCollectionDelete.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80004001), pendingItemDelete.ErrorCode);
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

    private static void AssertDomain(IInterfaceDomain domain, int id, string name, bool active)
    {
        Assert.AreEqual(id, domain.ID);
        Assert.AreEqual(name, domain.Name);
        Assert.AreEqual(active, domain.Active);
    }

    private static void AssertCoreScalars(IInterfaceDomain domain)
    {
        Assert.AreEqual("postmaster@alpha.example", domain.Postmaster);
        Assert.AreEqual(1024, domain.MaxMessageSize);
        Assert.IsTrue(domain.PlusAddressingEnabled);
        Assert.AreEqual("+", domain.PlusAddressingCharacter);
        Assert.IsTrue(domain.AntiSpamEnableGreylisting);
        Assert.AreEqual("corp.alpha.example", domain.ADDomainName);
        Assert.AreEqual(2048, domain.MaxSize);
        Assert.AreEqual(2, domain.Size);
        Assert.AreEqual(1536L, domain.AllocatedSize);
        Assert.AreEqual(100, domain.MaxNumberOfAccounts);
        Assert.AreEqual(25, domain.MaxNumberOfAliases);
        Assert.AreEqual(10, domain.MaxNumberOfDistributionLists);
        Assert.IsTrue(domain.MaxNumberOfAccountsEnabled);
        Assert.IsFalse(domain.MaxNumberOfAliasesEnabled);
        Assert.IsTrue(domain.MaxNumberOfDistributionListsEnabled);
        Assert.AreEqual(512, domain.MaxAccountSize);
    }

    private static void AssertSignatureScalars(IInterfaceDomain domain)
    {
        Assert.IsTrue(domain.SignatureEnabled);
        Assert.AreEqual(ComDomainSignatureMethod.AppendToAccountSignature, domain.SignatureMethod);
        Assert.AreEqual("Alpha plain signature", domain.SignaturePlainText);
        Assert.AreEqual("<p>Alpha HTML signature</p>", domain.SignatureHTML);
        Assert.IsTrue(domain.AddSignaturesToReplies);
        Assert.IsFalse(domain.AddSignaturesToLocalMail);
    }

    private static void AssertSignatureDefaults(IInterfaceDomain domain)
    {
        Assert.IsFalse(domain.SignatureEnabled);
        Assert.AreEqual(ComDomainSignatureMethod.SetIfNotSpecifiedInAccount, domain.SignatureMethod);
        Assert.AreEqual(string.Empty, domain.SignaturePlainText);
        Assert.AreEqual(string.Empty, domain.SignatureHTML);
        Assert.IsFalse(domain.AddSignaturesToReplies);
        Assert.IsTrue(domain.AddSignaturesToLocalMail);
    }

    private static void AssertDkimScalars(IInterfaceDomain domain)
    {
        Assert.IsTrue(domain.DKIMSignEnabled);
        Assert.AreEqual("alpha-selector", domain.DKIMSelector);
        Assert.AreEqual(@"C:\keys\alpha.pem", domain.DKIMPrivateKeyFile);
        Assert.AreEqual(ComDkimCanonicalizationMethod.Simple, domain.DKIMHeaderCanonicalizationMethod);
        Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, domain.DKIMBodyCanonicalizationMethod);
        Assert.AreEqual(ComDkimAlgorithm.SHA1, domain.DKIMSigningAlgorithm);
        Assert.IsTrue(domain.DKIMSignAliasesEnabled);
    }

    private static void AssertDkimDefaults(IInterfaceDomain domain)
    {
        Assert.IsFalse(domain.DKIMSignEnabled);
        Assert.AreEqual(string.Empty, domain.DKIMSelector);
        Assert.AreEqual(string.Empty, domain.DKIMPrivateKeyFile);
        Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, domain.DKIMHeaderCanonicalizationMethod);
        Assert.AreEqual(ComDkimCanonicalizationMethod.Relaxed, domain.DKIMBodyCanonicalizationMethod);
        Assert.AreEqual(ComDkimAlgorithm.SHA256, domain.DKIMSigningAlgorithm);
        Assert.IsFalse(domain.DKIMSignAliasesEnabled);
    }
}
