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
    private readonly Action<int>? _deleteById;
    private readonly Func<int, RuleCriteriaAdministrationSnapshot, int>? _insert;
    private readonly Action<RuleCriteriaAdministrationSnapshot>? _save;
    private readonly int? _owningRuleId;

    public RuleCriterias()
    {
    }

    private RuleCriterias(
        IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria,
        Func<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? reload,
        Action<int>? deleteById,
        Action<RuleCriteriaAdministrationSnapshot>? save,
        int? owningRuleId,
        Func<int, RuleCriteriaAdministrationSnapshot, int>? insert)
    {
        _criteria = criteria.ToArray();
        _reload = reload;
        _deleteById = deleteById;
        _insert = insert;
        _save = save;
        _owningRuleId = owningRuleId ?? criteria.FirstOrDefault()?.RuleId;
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

            var criterion = criteria[index];
            return RuleCriteria.CreateAuthorized(
                criterion,
                () => DeleteByDBID(criterion.Id),
                _save is null ? null : SaveCriterion);
        }
    }

    public IInterfaceRuleCriteria get_ItemByDBID(int databaseId)
    {
        var match = GetCriteria().FirstOrDefault(criterion => criterion.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No rule criteria with the specified database identifier exists.",
                DispEBadIndex)
            : RuleCriteria.CreateAuthorized(
                match,
                () => DeleteByDBID(match.Id),
                _save is null ? null : SaveCriterion);
    }

    public int Count => GetCriteria().Count;

    public IInterfaceRuleCriteria Add()
    {
        _ = GetCriteria();
        if (_insert is null || _owningRuleId is null)
        {
            return Unavailable<IInterfaceRuleCriteria>();
        }

        var criterion = RuleCriteria.CreateAuthorized(
            new RuleCriteriaAdministrationSnapshot(
                Id: 0,
                RuleId: _owningRuleId.Value,
                MatchValue: string.Empty,
                UsePredefined: true,
                PredefinedField: (int)ComRulePredefinedField.From,
                MatchType: (int)ComRuleMatchType.Equals,
                HeaderField: string.Empty),
            save: _save is null ? null : SaveCriterion,
            saveNew: SaveNewCriterion);

        return criterion;
    }

    public void DeleteByDBID(int databaseId)
    {
        var criteria = GetCriteria();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!criteria.Any(criterion => criterion.Id == databaseId))
        {
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _criteria,
                criteria.Where(criterion => criterion.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the rule criteria from the database.",
                EFail);
        }
    }

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

    public void Delete(int index)
    {
        var criteria = GetCriteria();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (index < 0 || index >= criteria.Count)
        {
            return;
        }

        var criterion = criteria[index];
        try
        {
            _deleteById(criterion.Id);
            var remaining = criteria
                .Where((_, candidateIndex) => candidateIndex != index)
                .ToArray();
            Volatile.Write(ref _criteria, remaining);
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the rule criteria from the database.",
                EFail);
        }
    }

    internal static RuleCriterias CreateAuthorized(
        IReadOnlyList<RuleCriteriaAdministrationSnapshot> criteria,
        Func<IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null,
        Action<RuleCriteriaAdministrationSnapshot>? save = null,
        int? owningRuleId = null,
        Func<int, RuleCriteriaAdministrationSnapshot, int>? insert = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return new RuleCriterias(criteria, reload, deleteById, save, owningRuleId, insert);
    }

    private void SaveCriterion(RuleCriteriaAdministrationSnapshot criterion)
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _save(criterion);
    }

    private RuleCriteriaAdministrationSnapshot SaveNewCriterion(RuleCriteriaAdministrationSnapshot criterion)
    {
        if (_insert is null || _owningRuleId is null)
        {
            Unavailable();
        }

        var owningRuleId = _owningRuleId.GetValueOrDefault();
        var prepared = criterion with { RuleId = owningRuleId };
        var generatedId = _insert!(owningRuleId, prepared);
        if (generatedId <= 0)
        {
            throw new InvalidOperationException("The rule criteria insert did not return a valid generated identity.");
        }

        var persisted = prepared with { Id = generatedId };
        var criteria = GetCriteria();
        Volatile.Write(ref _criteria, criteria.Append(persisted).ToArray());
        return persisted;
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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RuleCriteriaAdministrationSnapshot? _criterion;
    private readonly Action? _delete;
    private readonly Action<RuleCriteriaAdministrationSnapshot>? _save;
    private readonly Func<RuleCriteriaAdministrationSnapshot, RuleCriteriaAdministrationSnapshot>? _saveNew;

    public RuleCriteria()
    {
    }

    private RuleCriteria(
        RuleCriteriaAdministrationSnapshot criterion,
        Action? delete,
        Action<RuleCriteriaAdministrationSnapshot>? save,
        Func<RuleCriteriaAdministrationSnapshot, RuleCriteriaAdministrationSnapshot>? saveNew)
    {
        _criterion = criterion;
        _delete = delete;
        _save = save;
        _saveNew = saveNew;
    }

    public int ID => Snapshot.Id;

    public int RuleID { get => Snapshot.RuleId; set => Mutate(snapshot => snapshot with { RuleId = value }); }

    public string MatchValue { get => Snapshot.MatchValue; set => Mutate(snapshot => snapshot with { MatchValue = value }); }

    public bool UsePredefined { get => Snapshot.UsePredefined; set => Mutate(snapshot => snapshot with { UsePredefined = value }); }

    public ComRulePredefinedField PredefinedField
    {
        get => (ComRulePredefinedField)Snapshot.PredefinedField;
        set => Mutate(snapshot => snapshot with { PredefinedField = (int)value });
    }

    public ComRuleMatchType MatchType
    {
        get => (ComRuleMatchType)Snapshot.MatchType;
        set => Mutate(snapshot => snapshot with { MatchType = (int)value });
    }

    public string HeaderField { get => Snapshot.HeaderField; set => Mutate(snapshot => snapshot with { HeaderField = value }); }

    public void Save()
    {
        var snapshot = Snapshot;

        try
        {
            if (_saveNew is not null && snapshot.Id == 0)
            {
                _criterion = _saveNew(snapshot);
                return;
            }

            if (_save is null)
            {
                Unavailable();
                return;
            }

            _save(snapshot);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the rule criteria to the database.",
                EFail);
        }
    }

    public void Delete()
    {
        _ = Snapshot;
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete();
    }

    internal static RuleCriteria CreateAuthorized(
        RuleCriteriaAdministrationSnapshot criterion,
        Action? delete = null,
        Action<RuleCriteriaAdministrationSnapshot>? save = null,
        Func<RuleCriteriaAdministrationSnapshot, RuleCriteriaAdministrationSnapshot>? saveNew = null) =>
        new(criterion, delete, save, saveNew);

    private void Mutate(Func<RuleCriteriaAdministrationSnapshot, RuleCriteriaAdministrationSnapshot> mutation)
    {
        if (_save is null && _saveNew is null)
        {
            Unavailable();
            return;
        }

        _criterion = mutation(Snapshot);
    }

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

        void DeleteCriterionById(int databaseId) => store
            .DeleteRuleCriteriaByIdAsync(ruleId, databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void SaveCriterion(RuleCriteriaAdministrationSnapshot criterion) => store
            .SaveRuleCriteriaAsync(ruleId, criterion, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertCriterion(int owningRuleId, RuleCriteriaAdministrationSnapshot criterion) => store
            .InsertRuleCriteriaAsync(owningRuleId, criterion, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return RuleCriterias.CreateAuthorized(
            LoadCriteria(),
            LoadCriteria,
            DeleteCriterionById,
            SaveCriterion,
            owningRuleId: ruleId,
            insert: InsertCriterion);
    }
}
