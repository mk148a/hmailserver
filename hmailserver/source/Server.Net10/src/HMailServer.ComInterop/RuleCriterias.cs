using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("D79148F6-78A9-4F60-B8E8-48C33D888FC5")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRuleCriterias
{
    [DispId(0)]
    IInterfaceRuleCriteria this[int index] { get; }

    [DispId(1)]
    [SpecialName]
    IInterfaceRuleCriteria get_ItemByDBID(int databaseId);

    [DispId(2)]
    int Count { get; }

    [DispId(3)]
    IInterfaceRuleCriteria Add();

    [DispId(4)]
    void DeleteByDBID(int databaseId);

    [DispId(5)]
    void Refresh();

    [DispId(6)]
    void Delete(int databaseId);
}

[ComVisible(true)]
[Guid("2D8AA7DE-6155-44A5-802D-9FEC611A50A9")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRuleCriteria
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    int RuleID { get; set; }

    [DispId(4)]
    string MatchValue
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(5)]
    bool UsePredefined
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(6)]
    ComRulePredefinedField PredefinedField { get; set; }

    [DispId(7)]
    ComRuleMatchType MatchType { get; set; }

    [DispId(8)]
    string HeaderField
    {
        [return: MarshalAs(UnmanagedType.BStr)] get;
        [param: MarshalAs(UnmanagedType.BStr)] set;
    }

    [DispId(9)]
    void Save();

    [DispId(10)]
    void Delete();
}

[ComVisible(true)]
[Guid("E90022A1-61CF-4152-B9D9-27D04D0BA362")]
[ProgId("hMailServer.RuleCriterias.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRuleCriterias))]
public sealed class RuleCriterias : IInterfaceRuleCriterias
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RuleCriteriaAdministrationSnapshot[]? _criteria;
    private readonly Func<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? _reload;

    public RuleCriterias()
    {
    }

    private RuleCriterias(
        IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria,
        Func<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? reload)
    {
        _criteria = criteria.ToArray();
        _reload = reload;
    }

    public IInterfaceRuleCriteria this[int index]
    {
        get
        {
            var criteria = GetCriteria();
            if (index < 0 || index >= criteria.Count)
            {
                throw new COMException("Rule criteria index was outside the collection.", DispEBadIndex);
            }

            return RuleCriteria.CreateAuthorized(criteria[index]);
        }
    }

    public IInterfaceRuleCriteria get_ItemByDBID(int databaseId)
    {
        var match = GetCriteria().FirstOrDefault(criterion => criterion.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No rule criteria with the specified database identifier exists.",
                DispEBadIndex)
            : RuleCriteria.CreateAuthorized(match);
    }

    public int Count => GetCriteria().Count;

    public IInterfaceRuleCriteria Add() => Unavailable<IInterfaceRuleCriteria>();

    public void DeleteByDBID(int databaseId) => Unavailable();

    public void Refresh()
    {
        _ = GetCriteria();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var criteria = _reload();
            ArgumentNullException.ThrowIfNull(criteria);
            Volatile.Write(ref _criteria, criteria.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of rule criterias from the database.",
                EFail);
        }
    }

    public void Delete(int databaseId) => Unavailable();

    internal static RuleCriterias CreateAuthorized(
        IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria,
        Func<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? reload = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return new RuleCriterias(criteria, reload);
    }

    private IReadOnlyList<RuleCriteriaAdministrationSnapshot> GetCriteria()
    {
        return Volatile.Read(ref _criteria)
            ?? throw new COMException(
                "RuleCriterias access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetCriteria();
        throw new COMException(
            "This RuleCriterias member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetCriteria();
        throw new COMException(
            "This RuleCriterias member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("3F0EB97B-C698-498C-965A-06ED393AC50C")]
[ProgId("hMailServer.RuleCriteria.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRuleCriteria))]
public sealed class RuleCriteria : IInterfaceRuleCriteria
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RuleCriteriaAdministrationSnapshot? _criterion;

    public RuleCriteria()
    {
    }

    private RuleCriteria(RuleCriteriaAdministrationSnapshot criterion)
    {
        _criterion = criterion;
    }

    public int ID => Snapshot.Id;

    public int RuleID { get => Snapshot.RuleId; set => Unavailable(); }

    public string MatchValue { get => Snapshot.MatchValue; set => Unavailable(); }

    public bool UsePredefined { get => Snapshot.UsePredefined; set => Unavailable(); }

    public ComRulePredefinedField PredefinedField
    {
        get => (ComRulePredefinedField)Snapshot.PredefinedField;
        set => Unavailable();
    }

    public ComRuleMatchType MatchType
    {
        get => (ComRuleMatchType)Snapshot.MatchType;
        set => Unavailable();
    }

    public string HeaderField { get => Snapshot.HeaderField; set => Unavailable(); }

    public void Save() => Unavailable();

    public void Delete() => Unavailable();

    internal static RuleCriteria CreateAuthorized(RuleCriteriaAdministrationSnapshot criterion) => new(criterion);

    private RuleCriteriaAdministrationSnapshot Snapshot =>
        _criterion ?? throw new COMException(
            "RuleCriteria access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This RuleCriteria member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class RuleCriteriaAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IRuleCriteriaAdministrationStore? _store;

    public static void Configure(IRuleCriteriaAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static RuleCriterias CreateAuthorizedAdapter(int ruleId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer rule criteria administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<RuleCriteriaAdministrationSnapshot> LoadCriteria() => store
            .GetRuleCriteriaAsync(ruleId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return RuleCriterias.CreateAuthorized(LoadCriteria(), LoadCriteria);
    }
}
