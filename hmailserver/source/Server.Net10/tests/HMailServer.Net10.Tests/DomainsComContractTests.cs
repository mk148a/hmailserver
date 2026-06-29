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
    public void DkimEnums_PreserveLegacyValuesAndGuids()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD13"), typeof(ComDkimCanonicalizationMethod).GUID);
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD14"), typeof(ComDkimAlgorithm).GUID);
        var canonicalizationValues = Enum.GetNames<ComDkimCanonicalizationMethod>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComDkimCanonicalizationMethod>(name)));
        var algorithmValues = Enum.GetNames<ComDkimAlgorithm>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComDkimAlgorithm>(name)));

        Assert.AreEqual(1, canonicalizationValues[nameof(ComDkimCanonicalizationMethod.Simple)]);
        Assert.AreEqual(2, canonicalizationValues[nameof(ComDkimCanonicalizationMethod.Relaxed)]);
        Assert.AreEqual(1, algorithmValues[nameof(ComDkimAlgorithm.SHA1)]);
        Assert.AreEqual(2, algorithmValues[nameof(ComDkimAlgorithm.SHA256)]);
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var domainsError = Assert.ThrowsExactly<COMException>(() => _ = new Domains().Count);
        var domainError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().Name);
        var dkimError = Assert.ThrowsExactly<COMException>(() => _ = new Domain().DKIMSignEnabled);

        Assert.AreEqual(EAccessDenied, domainsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, domainError.ErrorCode);
        Assert.AreEqual(EAccessDenied, dkimError.ErrorCode);
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
                    MaxSize: 2048,
                    MaxNumberOfAccounts: 100,
                    MaxNumberOfAliases: 25,
                    MaxNumberOfDistributionLists: 10,
                    MaxNumberOfAccountsEnabled: true,
                    MaxNumberOfAliasesEnabled: false,
                    MaxNumberOfDistributionListsEnabled: true,
                    MaxAccountSize: 512,
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
        AssertDomain(domains[0], 10, "alpha.example", true);
        AssertCoreScalars(domains[0]);
        AssertDkimScalars(domains[0]);
        AssertDomain(domains.get_ItemByName("BETA.EXAMPLE"), 20, "beta.example", false);
        AssertDomain(domains.get_ItemByDBID(10), 10, "alpha.example", true);
        AssertDkimDefaults(domains[1]);

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = domains[2]);
        var badName = Assert.ThrowsExactly<COMException>(() => _ = domains.get_ItemByName("missing.example"));
        var pendingRefresh = Assert.ThrowsExactly<COMException>(domains.Refresh);
        var pendingMutation = Assert.ThrowsExactly<COMException>(() => domains[0].Active = false);
        var pendingScalarMutation = Assert.ThrowsExactly<COMException>(() => domains[0].Postmaster = "changed@alpha.example");
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
        var pendingNonCoreScalar = Assert.ThrowsExactly<COMException>(() => _ = domains[0].ADDomainName);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badName.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingRefresh.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingScalarMutation.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimEnabled.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimSelector.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimKeyFile.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimHeader.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimBody.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimAlgorithm.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDkimAliases.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingNonCoreScalar.ErrorCode);
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
        Assert.AreEqual(2048, domain.MaxSize);
        Assert.AreEqual(100, domain.MaxNumberOfAccounts);
        Assert.AreEqual(25, domain.MaxNumberOfAliases);
        Assert.AreEqual(10, domain.MaxNumberOfDistributionLists);
        Assert.IsTrue(domain.MaxNumberOfAccountsEnabled);
        Assert.IsFalse(domain.MaxNumberOfAliasesEnabled);
        Assert.IsTrue(domain.MaxNumberOfDistributionListsEnabled);
        Assert.AreEqual(512, domain.MaxAccountSize);
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
