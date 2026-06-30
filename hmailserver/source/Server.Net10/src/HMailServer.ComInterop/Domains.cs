using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("2CDFD68F-62F2-49CF-A14A-505E7F68EE9C")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDomains
{
    [DispId(0)]
    IInterfaceDomain this[int index] { get; }

    [DispId(1)]
    void Refresh();

    [DispId(2)]
    int Count { get; }

    [DispId(3)]
    IInterfaceDomain Add();

    [DispId(4)]
    [SpecialName]
    IInterfaceDomain get_ItemByName([MarshalAs(UnmanagedType.BStr)] string itemName);

    [DispId(5)]
    [SpecialName]
    IInterfaceDomain get_ItemByDBID(int databaseId);

    [DispId(6)]
    string Names { [return: MarshalAs(UnmanagedType.BStr)] get; }

    [DispId(7)]
    void DeleteByDBID(int databaseId);
}

[ComVisible(true)]
[Guid("3F50C3AF-67C0-4628-91D6-E2EAC7786830")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceDomain
{
    [DispId(1)]
    string Name
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(2)]
    void Save();

    [DispId(3)]
    int ID { get; }

    [DispId(4)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(5)]
    IInterfaceAccounts Accounts { get; }

    [DispId(6)]
    void Delete();

    [DispId(7)]
    IInterfaceAliases Aliases { get; }

    [DispId(9)]
    IInterfaceDistributionLists DistributionLists { get; }

    [DispId(10)]
    string Postmaster
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(11)]
    IInterfaceDomainAliases DomainAliases { get; }

    [DispId(12)]
    string ADDomainName
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(13)]
    void SynchronizeDirectory();

    [DispId(14)]
    int MaxMessageSize { get; set; }

    [DispId(15)]
    bool PlusAddressingEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(16)]
    string PlusAddressingCharacter
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(17)]
    bool AntiSpamEnableGreylisting
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(18)]
    int MaxSize { get; set; }

    [DispId(19)]
    int Size { get; }

    [DispId(20)]
    long AllocatedSize { get; }

    [DispId(21)]
    bool SignatureEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(22)]
    ComDomainSignatureMethod SignatureMethod { get; set; }

    [DispId(23)]
    string SignaturePlainText
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(24)]
    string SignatureHTML
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(25)]
    bool AddSignaturesToReplies
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(26)]
    bool AddSignaturesToLocalMail
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(27)]
    int MaxNumberOfAccounts { get; set; }

    [DispId(28)]
    int MaxNumberOfAliases { get; set; }

    [DispId(29)]
    int MaxNumberOfDistributionLists { get; set; }

    [DispId(30)]
    bool MaxNumberOfAccountsEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(31)]
    bool MaxNumberOfAliasesEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(32)]
    bool MaxNumberOfDistributionListsEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(33)]
    int MaxAccountSize { get; set; }

    [DispId(34)]
    bool DKIMSignEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(35)]
    string DKIMSelector
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(36)]
    string DKIMPrivateKeyFile
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        get;

        [param: MarshalAs(UnmanagedType.BStr)]
        set;
    }

    [DispId(37)]
    ComDkimCanonicalizationMethod DKIMHeaderCanonicalizationMethod { get; set; }

    [DispId(38)]
    ComDkimCanonicalizationMethod DKIMBodyCanonicalizationMethod { get; set; }

    [DispId(39)]
    ComDkimAlgorithm DKIMSigningAlgorithm { get; set; }

    [DispId(40)]
    bool DKIMSignAliasesEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }
}

[ComVisible(true)]
[Guid("82AFD03C-58A4-4F04-8277-6B2812780E45")]
[ProgId("hMailServer.Domains.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDomains))]
public sealed class Domains : IInterfaceDomains
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<DomainAdministrationSnapshot>? _domains;

    public Domains()
    {
    }

    private Domains(IReadOnlyList<DomainAdministrationSnapshot> domains)
    {
        _domains = domains.ToArray();
    }

    public int Count => GetDomains().Count;

    public string Names => string.Concat(
        GetDomains().Select(static domain => $"{domain.Id}\t{domain.Name}\t{(domain.Active ? 1 : 0)}\r\n"));

    internal static Domains CreateAuthorized(IReadOnlyList<DomainAdministrationSnapshot> domains)
    {
        ArgumentNullException.ThrowIfNull(domains);
        return new Domains(domains);
    }

    public IInterfaceDomain this[int index]
    {
        get
        {
            var domains = GetDomains();
            if (index < 0 || index >= domains.Count)
            {
                throw new COMException("Domain index was outside the collection.", DispEBadIndex);
            }

            return Domain.CreateAuthorized(domains[index]);
        }
    }

    public void Refresh() => Unavailable();

    public IInterfaceDomain Add() => Unavailable<IInterfaceDomain>();

    public IInterfaceDomain get_ItemByName(string itemName)
    {
        var match = GetDomains()
            .FirstOrDefault(domain => domain.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? throw new COMException("No domain with the specified name exists.", DispEBadIndex)
            : Domain.CreateAuthorized(match);
    }

    public IInterfaceDomain get_ItemByDBID(int databaseId)
    {
        var match = GetDomains().FirstOrDefault(domain => domain.Id == databaseId);

        return match is null
            ? throw new COMException("No domain with the specified database identifier exists.", DispEBadIndex)
            : Domain.CreateAuthorized(match);
    }

    public void DeleteByDBID(int databaseId) => Unavailable();

    private IReadOnlyList<DomainAdministrationSnapshot> GetDomains()
    {
        return _domains
            ?? throw new COMException("Domains access requires an authenticated server administrator.", EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetDomains();
        throw new COMException("This Domains member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetDomains();
        throw new COMException("This Domains member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("C535E4AF-9DB3-41FC-B434-FFCDAE0EFBD5")]
[ProgId("hMailServer.Domain.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceDomain))]
public sealed class Domain : DomainComAdapter, IDomainAuthorizationBoundary
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly DomainAdministrationSnapshot? _domain;
    private readonly bool _authorized;

    public Domain()
    {
    }

    private Domain(DomainAdministrationSnapshot domain)
    {
        _domain = domain;
        _authorized = true;
    }

    public override string Name
    {
        get => Snapshot.Name;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int ID => Snapshot.Id;

    public override bool Active
    {
        get => Snapshot.Active;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string Postmaster
    {
        get => Snapshot.Postmaster;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int MaxMessageSize
    {
        get => Snapshot.MaxMessageSize;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool PlusAddressingEnabled
    {
        get => Snapshot.PlusAddressingEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string PlusAddressingCharacter
    {
        get => Snapshot.PlusAddressingCharacter;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool AntiSpamEnableGreylisting
    {
        get => Snapshot.AntiSpamEnableGreylisting;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string ADDomainName
    {
        get => Snapshot.AdDomainName;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int MaxSize
    {
        get => Snapshot.MaxSize;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int Size => Snapshot.Size;

    public override long AllocatedSize => Snapshot.AllocatedSize;

    public override int MaxNumberOfAccounts
    {
        get => Snapshot.MaxNumberOfAccounts;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int MaxNumberOfAliases
    {
        get => Snapshot.MaxNumberOfAliases;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int MaxNumberOfDistributionLists
    {
        get => Snapshot.MaxNumberOfDistributionLists;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool MaxNumberOfAccountsEnabled
    {
        get => Snapshot.MaxNumberOfAccountsEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool MaxNumberOfAliasesEnabled
    {
        get => Snapshot.MaxNumberOfAliasesEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool MaxNumberOfDistributionListsEnabled
    {
        get => Snapshot.MaxNumberOfDistributionListsEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override int MaxAccountSize
    {
        get => Snapshot.MaxAccountSize;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool SignatureEnabled
    {
        get => Snapshot.SignatureEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override ComDomainSignatureMethod SignatureMethod
    {
        get => (ComDomainSignatureMethod)Snapshot.SignatureMethod;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string SignaturePlainText
    {
        get => Snapshot.SignaturePlainText;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string SignatureHTML
    {
        get => Snapshot.SignatureHtml;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool AddSignaturesToReplies
    {
        get => Snapshot.AddSignaturesToReplies;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool AddSignaturesToLocalMail
    {
        get => Snapshot.AddSignaturesToLocalMail;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool DKIMSignEnabled
    {
        get => Snapshot.DkimSignEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string DKIMSelector
    {
        get => Snapshot.DkimSelector;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override string DKIMPrivateKeyFile
    {
        get => Snapshot.DkimPrivateKeyFile;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override ComDkimCanonicalizationMethod DKIMHeaderCanonicalizationMethod
    {
        get => (ComDkimCanonicalizationMethod)Snapshot.DkimHeaderCanonicalizationMethod;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override ComDkimCanonicalizationMethod DKIMBodyCanonicalizationMethod
    {
        get => (ComDkimCanonicalizationMethod)Snapshot.DkimBodyCanonicalizationMethod;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override ComDkimAlgorithm DKIMSigningAlgorithm
    {
        get => (ComDkimAlgorithm)Snapshot.DkimSigningAlgorithm;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override bool DKIMSignAliasesEnabled
    {
        get => Snapshot.DkimSignAliasesEnabled;
        set => DomainComAuthorization.Unavailable(this);
    }

    public override IInterfaceAccounts Accounts =>
        AccountAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    public override IInterfaceAliases Aliases =>
        AliasAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    public override IInterfaceDistributionLists DistributionLists =>
        DistributionListAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    public override IInterfaceDomainAliases DomainAliases =>
        DomainAliasAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    internal static Domain CreateAuthorized(DomainAdministrationSnapshot domain) => new(domain);

    void IDomainAuthorizationBoundary.EnsureAuthorized() => EnsureAuthorized();

    private DomainAdministrationSnapshot Snapshot
    {
        get
        {
            EnsureAuthorized();
            return _domain ?? throw new InvalidOperationException("Authorized domain adapter is missing its snapshot.");
        }
    }

    private void EnsureAuthorized()
    {
        if (!_authorized)
        {
            throw new COMException("Domain access requires an authenticated server administrator.", EAccessDenied);
        }
    }
}

[ComVisible(false)]
public abstract class DomainComAdapter : IInterfaceDomain
{
    public virtual string Name { get => Unavailable<string>(); set => Unavailable(); }
    public void Save() => Unavailable();
    public virtual int ID => Unavailable<int>();
    public virtual bool Active { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual IInterfaceAccounts Accounts => Unavailable<IInterfaceAccounts>();
    public void Delete() => Unavailable();
    public virtual IInterfaceAliases Aliases => Unavailable<IInterfaceAliases>();
    public virtual IInterfaceDistributionLists DistributionLists => Unavailable<IInterfaceDistributionLists>();
    public virtual string Postmaster { get => Unavailable<string>(); set => Unavailable(); }
    public virtual IInterfaceDomainAliases DomainAliases => Unavailable<IInterfaceDomainAliases>();
    public virtual string ADDomainName { get => Unavailable<string>(); set => Unavailable(); }
    public void SynchronizeDirectory() => Unavailable();
    public virtual int MaxMessageSize { get => Unavailable<int>(); set => Unavailable(); }
    public virtual bool PlusAddressingEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string PlusAddressingCharacter { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool AntiSpamEnableGreylisting { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxSize { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int Size => Unavailable<int>();
    public virtual long AllocatedSize => Unavailable<long>();
    public virtual bool SignatureEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual ComDomainSignatureMethod SignatureMethod { get => Unavailable<ComDomainSignatureMethod>(); set => Unavailable(); }
    public virtual string SignaturePlainText { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string SignatureHTML { get => Unavailable<string>(); set => Unavailable(); }
    public virtual bool AddSignaturesToReplies { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool AddSignaturesToLocalMail { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxNumberOfAccounts { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int MaxNumberOfAliases { get => Unavailable<int>(); set => Unavailable(); }
    public virtual int MaxNumberOfDistributionLists { get => Unavailable<int>(); set => Unavailable(); }
    public virtual bool MaxNumberOfAccountsEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool MaxNumberOfAliasesEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual bool MaxNumberOfDistributionListsEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual int MaxAccountSize { get => Unavailable<int>(); set => Unavailable(); }
    public virtual bool DKIMSignEnabled { get => Unavailable<bool>(); set => Unavailable(); }
    public virtual string DKIMSelector { get => Unavailable<string>(); set => Unavailable(); }
    public virtual string DKIMPrivateKeyFile { get => Unavailable<string>(); set => Unavailable(); }
    public virtual ComDkimCanonicalizationMethod DKIMHeaderCanonicalizationMethod { get => Unavailable<ComDkimCanonicalizationMethod>(); set => Unavailable(); }
    public virtual ComDkimCanonicalizationMethod DKIMBodyCanonicalizationMethod { get => Unavailable<ComDkimCanonicalizationMethod>(); set => Unavailable(); }
    public virtual ComDkimAlgorithm DKIMSigningAlgorithm { get => Unavailable<ComDkimAlgorithm>(); set => Unavailable(); }
    public virtual bool DKIMSignAliasesEnabled { get => Unavailable<bool>(); set => Unavailable(); }

    private T Unavailable<T>() => DomainComAuthorization.Unavailable<T>(this);

    private void Unavailable() => DomainComAuthorization.Unavailable(this);
}

[ComVisible(false)]
internal interface IDomainAuthorizationBoundary
{
    void EnsureAuthorized();
}

[ComVisible(false)]
internal static class DomainComAuthorization
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    internal static T Unavailable<T>(IInterfaceDomain domain)
    {
        EnsureAuthorized(domain);
        throw new COMException("This Domain member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    internal static void Unavailable(IInterfaceDomain domain)
    {
        EnsureAuthorized(domain);
        throw new COMException("This Domain member is not implemented by the .NET 10 rewrite yet.", ENotImplemented);
    }

    private static void EnsureAuthorized(IInterfaceDomain domain)
    {
        if (domain is not IDomainAuthorizationBoundary boundary)
        {
            throw new COMException("Domain access requires an authenticated server administrator.", EAccessDenied);
        }

        boundary.EnsureAuthorized();
    }
}

[ComVisible(false)]
public static class DomainAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IDomainAdministrationStore? _store;

    public static void Configure(IDomainAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Domains CreateAuthorizedAdapter()
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer domain administration runtime has not been initialized.",
                CoENotInitialized);

        var domains = store
            .GetDomainsAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Domains.CreateAuthorized(domains);
    }
}
