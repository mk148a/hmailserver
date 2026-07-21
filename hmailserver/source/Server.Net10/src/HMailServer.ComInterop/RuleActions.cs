using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("DBFD3E11-9121-4DDD-944B-5AF29BF3D2DF")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRuleActions
{
    [DispId(0)]
    IInterfaceRuleAction this[int index] { get; }

    [DispId(1)]
    [SpecialName]
    IInterfaceRuleAction get_ItemByDBID(int databaseId);

    [DispId(2)]
    int Count { get; }

    [DispId(3)]
    IInterfaceRuleAction Add();

    [DispId(4)]
    void DeleteByDBID(int databaseId);

    [DispId(5)]
    void Refresh();

    [DispId(6)]
    void Delete(int databaseId);
}

[ComVisible(true)]
[Guid("F3F4A3E1-695E-499E-9F31-712DA8126982")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceRuleAction
{
    [DispId(1)]
    int ID { get; }

    [DispId(2)]
    int RuleID { get; set; }

    [DispId(3)]
    ComRuleActionType Type { get; set; }

    [DispId(4)]
    string Subject { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(5)]
    string Body { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(6)]
    string FromName { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(7)]
    string FromAddress { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(8)]
    string Filename { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(9)]
    string To { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(10)]
    string IMAPFolder { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(11)]
    void Save();

    [DispId(12)]
    string ScriptFunction { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(13)]
    void MoveUp();

    [DispId(14)]
    void MoveDown();

    [DispId(15)]
    string HeaderName { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(16)]
    string Value { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(17)]
    void Delete();

    [DispId(18)]
    int RouteID { get; set; }

    [DispId(19)]
    bool AbortSpamFlagged
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }
}

[ComVisible(true)]
[Guid("32A21952-5421-4A6C-835A-41050D0493C1")]
[ProgId("hMailServer.RuleActions.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRuleActions))]
public sealed class RuleActions : IInterfaceRuleActions
{
    private const int DispEBadIndex = unchecked((int)0x8002000B);
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RuleActionAdministrationSnapshot[]? _actions;
    private readonly Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? _reload;
    private readonly Action<int>? _deleteById;

    public RuleActions()
    {
    }

    private RuleActions(
        IReadOnlyList<RuleActionAdministrationSnapshot> actions,
        Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? reload,
        Action<int>? deleteById)
    {
        _actions = actions.ToArray();
        _reload = reload;
        _deleteById = deleteById;
    }

    public IInterfaceRuleAction this[int index]
    {
        get
        {
            var actions = GetActions();
            if (index < 0 || index >= actions.Count)
            {
                throw new COMException("Rule action index was outside the collection.", DispEBadIndex);
            }

            return RuleAction.CreateAuthorized(actions[index]);
        }
    }

    public IInterfaceRuleAction get_ItemByDBID(int databaseId)
    {
        var match = GetActions().FirstOrDefault(action => action.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No rule action with the specified database identifier exists.",
                DispEBadIndex)
            : RuleAction.CreateAuthorized(match);
    }

    public int Count => GetActions().Count;

    public IInterfaceRuleAction Add() => Unavailable<IInterfaceRuleAction>();

    public void DeleteByDBID(int databaseId)
    {
        var actions = GetActions();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!actions.Any(action => action.Id == databaseId))
        {
            return;
        }

        try
        {
            _deleteById(databaseId);
            Volatile.Write(
                ref _actions,
                actions.Where(action => action.Id != databaseId).ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the rule action from the database.",
                EFail);
        }
    }

    public void Refresh()
    {
        _ = GetActions();
        if (_reload is null)
        {
            Unavailable();
            return;
        }

        try
        {
            var actions = _reload();
            ArgumentNullException.ThrowIfNull(actions);
            Volatile.Write(ref _actions, actions.ToArray());
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of rule actions from the database.",
                EFail);
        }
    }

    public void Delete(int databaseId) => Unavailable();

    internal static RuleActions CreateAuthorized(
        IReadOnlyList<RuleActionAdministrationSnapshot> actions,
        Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return new RuleActions(actions, reload, deleteById);
    }

    private IReadOnlyList<RuleActionAdministrationSnapshot> GetActions()
    {
        return Volatile.Read(ref _actions)
            ?? throw new COMException(
                "RuleActions access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private T Unavailable<T>()
    {
        _ = GetActions();
        throw new COMException(
            "This RuleActions member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void Unavailable()
    {
        _ = GetActions();
        throw new COMException(
            "This RuleActions member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(true)]
[Guid("35548CC2-14AE-4795-8A19-C78FDE208504")]
[ProgId("hMailServer.RuleAction.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceRuleAction))]
public sealed class RuleAction : IInterfaceRuleAction
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RuleActionAdministrationSnapshot? _action;

    public RuleAction()
    {
    }

    private RuleAction(RuleActionAdministrationSnapshot action)
    {
        _action = action;
    }

    public int ID => Snapshot.Id;

    public int RuleID { get => Snapshot.RuleId; set => Unavailable(); }

    public ComRuleActionType Type { get => (ComRuleActionType)Snapshot.Type; set => Unavailable(); }

    public string Subject { get => Snapshot.Subject; set => Unavailable(); }

    public string Body { get => Snapshot.Body; set => Unavailable(); }

    public string FromName { get => Snapshot.FromName; set => Unavailable(); }

    public string FromAddress { get => Snapshot.FromAddress; set => Unavailable(); }

    public string Filename { get => Snapshot.Filename; set => Unavailable(); }

    public string To { get => Snapshot.To; set => Unavailable(); }

    public string IMAPFolder { get => Snapshot.ImapFolder; set => Unavailable(); }

    public string ScriptFunction { get => Snapshot.ScriptFunction; set => Unavailable(); }

    public string HeaderName { get => Snapshot.HeaderName; set => Unavailable(); }

    public string Value { get => Snapshot.Value; set => Unavailable(); }

    public int RouteID { get => Snapshot.RouteId; set => Unavailable(); }

    public bool AbortSpamFlagged { get => Snapshot.AbortSpamFlagged; set => Unavailable(); }

    public void Save() => Unavailable();

    public void MoveUp() => Unavailable();

    public void MoveDown() => Unavailable();

    public void Delete() => Unavailable();

    internal static RuleAction CreateAuthorized(RuleActionAdministrationSnapshot action) => new(action);

    private RuleActionAdministrationSnapshot Snapshot =>
        _action ?? throw new COMException(
            "RuleAction access requires an authenticated server administrator.",
            EAccessDenied);

    private void Unavailable()
    {
        _ = Snapshot;
        throw new COMException(
            "This RuleAction member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }
}

[ComVisible(false)]
public static class RuleActionAdministrationRuntimeHost
{
    private const int CoENotInitialized = unchecked((int)0x800401F0);

    private static IRuleActionAdministrationStore? _store;

    public static void Configure(IRuleActionAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Volatile.Write(ref _store, store);
    }

    internal static RuleActions CreateAuthorizedAdapter(int ruleId)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer rule action administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<RuleActionAdministrationSnapshot> LoadActions() => store
            .GetRuleActionsAsync(ruleId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void DeleteActionById(int databaseId) => store
            .DeleteRuleActionByIdAsync(ruleId, databaseId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return RuleActions.CreateAuthorized(LoadActions(), LoadActions, DeleteActionById);
    }
}
