using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("995F9181-E761-42FA-9057-FE070B37D0F3")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRules
{
    [DispId(0)]
    IInterfaceRule this[int index] { get; }

    [DispId(1)]
    [SpecialName]
    IInterfaceRule get_ItemByDBID(int databaseId);

    [DispId(2)]
    int Count { get; }

    [DispId(3)]
    IInterfaceRule Add();

    [DispId(4)]
    void DeleteByDBID(int databaseId);

    [DispId(5)]
    void Refresh();
}

[ComVisible(true)]
[Guid("41CCD467-9ADE-4ADA-AE14-760E94BA53E8")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRule
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    int AccountID { get; set; }

    [DispId(3)]
    string Name { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(5)]
    bool UseAND
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        get;

        [param: MarshalAs(UnmanagedType.VariantBool)]
        set;
    }

    [DispId(6)]
    IInterfaceRuleCriterias Criterias { get; }

    [DispId(7)]
    IInterfaceRuleActions Actions { get; }

    [DispId(8)]
    void Save();

    [DispId(9)]
    void MoveUp();

    [DispId(10)]
    void MoveDown();

    [DispId(11)]
    void Delete();
}

[ComVisible(true)]
[Guid("624F494B-347A-4285-9506-C54154D50B2A")]
[ProgId("hMailServer.Rules.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRules))]
public sealed class Rules : IInterfaceRules
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly IReadOnlyList<RuleAdministrationSnapshot>? _rules;

    public Rules()
    {
    }

    private Rules(IReadOnlyList<RuleAdministrationSnapshot> rules)
    {
        _rules = rules.ToArray();
    }

    public int Count => GetRules().Count;

    internal static Rules CreateAuthorized(IReadOnlyList<RuleAdministrationSnapshot> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new Rules(rules);
    }

    public IInterfaceRule this[int index]
    {
        get
        {
            var rules = GetRules();
            if (index < 0 || index >= rules.Count)
            {
                throw new COMException("Rule index was outside the collection.", DispEBadIndex);
            }

            return Rule.CreateAuthorized(rules[index]);
        }
    }

    public IInterfaceRule get_ItemByDBID(int databaseId)
    {
        var match = GetRules().FirstOrDefault(rule => rule.Id == databaseId);

        return match is null
            ? throw new COMException("No rule with the specified database identifier exists.", DispEBadIndex)
            : Rule.CreateAuthorized(match);
    }

    public IInterfaceRule Add() => Unavailable<IInterfaceRule>();

    public void DeleteByDBID(int databaseId) => Unavailable();

    public void Refresh() => Unavailable();

    private IReadOnlyList<RuleAdministrationSnapshot> GetRules()
    {
        return _rules
            ?? throw new COMException(
                "Rules access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetRules();
        throw new COMException(
            "This Rules member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetRules();
        throw new COMException(
            "This Rules member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("D5D7927A-7D05-40F3-91DD-968FC14316C7")]
[ProgId("hMailServer.Rule.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRule))]
public sealed class Rule : IInterfaceRule
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RuleAdministrationSnapshot? _rule;

    public Rule()
    {
    }

    private Rule(RuleAdministrationSnapshot rule)
    {
        _rule = rule;
    }

    public int ID => Snapshot.Id;

    public int AccountID { get => Snapshot.AccountId; set => Unavailable(); }

    public string Name { get => Snapshot.Name; set => Unavailable(); }

    public bool Active { get => Snapshot.Active; set => Unavailable(); }

    public bool UseAND { get => Snapshot.UseAnd; set => Unavailable(); }

    public IInterfaceRuleCriterias Criterias =>
        RuleCriteriaAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    public IInterfaceRuleActions Actions =>
        RuleActionAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id);

    internal static Rule CreateAuthorized(RuleAdministrationSnapshot rule) => new(rule);

    public void Save() => Unavailable();

    public void MoveUp() => Unavailable();

    public void MoveDown() => Unavailable();

    public void Delete() => Unavailable();

    private RuleAdministrationSnapshot Snapshot =>
        _rule ?? throw new COMException(
            "Rule access requires an authenticated server administrator.",
            EAccessDenied);

    private T Unavailable<T>()
    {
        _ = Snapshot;
        throw new COMException(
            "This Rule member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This Rule member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class RuleAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IRuleAdministrationStore? _store;

    public static void Configure(IRuleAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static Rules CreateAuthorizedAdapter(int accountId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer rule administration runtime has not been initialized.",
                CoENotInitialized);

        var rules = store
            .GetRulesAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Rules.CreateAuthorized(rules);
    }
}
