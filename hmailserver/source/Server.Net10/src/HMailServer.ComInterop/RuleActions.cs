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

[ComVisible(false)]
internal sealed class RuleActionAdministrationEntry
{
    public RuleActionAdministrationEntry(RuleActionAdministrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
    }

    public RuleActionAdministrationSnapshot Snapshot { get; set; }
}

[ComVisible(false)]
internal sealed class RuleActionAdministrationState
{
    private readonly object _gate = new();
    private RuleActionAdministrationEntry[]? _actions;

    internal RuleActionAdministrationState()
    {
    }

    internal RuleActionAdministrationState(IReadOnlyList<RuleActionAdministrationSnapshot> actions)
    {
        Replace(actions);
    }

    internal void Initialize(Func<IReadOnlyList<RuleActionAdministrationSnapshot>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (Volatile.Read(ref _actions) is not null)
        {
            return;
        }

        lock (_gate)
        {
            if (Volatile.Read(ref _actions) is null)
            {
                Replace(load());
            }
        }
    }

    internal IReadOnlyList<RuleActionAdministrationEntry> GetActions() =>
        Volatile.Read(ref _actions)
        ?? throw new InvalidOperationException("Rule action state has not been initialized.");

    internal void Replace(IReadOnlyList<RuleActionAdministrationSnapshot> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Volatile.Write(
            ref _actions,
            actions.Select(static action => new RuleActionAdministrationEntry(action)).ToArray());
    }

    internal void RemoveByDBID(int databaseId)
    {
        var actions = GetActions();
        Volatile.Write(
            ref _actions,
            actions.Where(action => action.Snapshot.Id != databaseId).ToArray());
    }

    internal void RemoveAt(int index)
    {
        var actions = GetActions();
        Volatile.Write(
            ref _actions,
            actions.Where((_, candidateIndex) => candidateIndex != index).ToArray());
    }

    internal void Append(RuleActionAdministrationSnapshot action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var actions = GetActions();
        Volatile.Write(
            ref _actions,
            actions.Concat([new RuleActionAdministrationEntry(action)]).ToArray());
    }

    internal void ApplyOrder(
        IReadOnlyList<(RuleActionAdministrationEntry Entry, RuleActionAdministrationSnapshot Snapshot)> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        foreach (var item in ordered)
        {
            item.Entry.Snapshot = item.Snapshot;
        }

        Volatile.Write(ref _actions, ordered.Select(static item => item.Entry).ToArray());
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

    private readonly RuleActionAdministrationState? _state;
    private readonly Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? _reload;
    private readonly Action<int>? _deleteById;
    private readonly Func<int, RuleActionAdministrationSnapshot, int>? _insert;
    private readonly Action<RuleActionAdministrationSnapshot>? _save;
    private readonly Action<IReadOnlyList<RuleActionAdministrationSnapshot>>? _saveOrder;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly int? _owningRuleId;
    private readonly Func<bool>? _isAuthenticated;

    public RuleActions()
    {
    }

    private RuleActions(
        IReadOnlyList<RuleActionAdministrationSnapshot> actions,
        Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? reload,
        Action<int>? deleteById,
        Func<int, RuleActionAdministrationSnapshot, int>? insert,
        Action<RuleActionAdministrationSnapshot>? save,
        Action<IReadOnlyList<RuleActionAdministrationSnapshot>>? saveOrder,
        Func<bool>? isServerAdministrator,
        int? owningRuleId,
        Func<bool>? isAuthenticated)
        : this(
            new RuleActionAdministrationState(actions),
            reload,
            deleteById,
            insert,
            save,
            saveOrder,
            isServerAdministrator,
            owningRuleId ?? actions.FirstOrDefault()?.RuleId,
            isAuthenticated)
    {
    }

    private RuleActions(
        RuleActionAdministrationState state,
        Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? reload,
        Action<int>? deleteById,
        Func<int, RuleActionAdministrationSnapshot, int>? insert,
        Action<RuleActionAdministrationSnapshot>? save,
        Action<IReadOnlyList<RuleActionAdministrationSnapshot>>? saveOrder,
        Func<bool>? isServerAdministrator,
        int? owningRuleId,
        Func<bool>? isAuthenticated)
    {
        _state = state;
        _reload = reload;
        _deleteById = deleteById;
        _insert = insert;
        _save = save;
        _saveOrder = saveOrder;
        _isServerAdministrator = isServerAdministrator;
        _owningRuleId = owningRuleId;
        _isAuthenticated = isAuthenticated;
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

            var action = actions[index];
            return RuleAction.CreateAuthorized(
                action,
                () => DeleteByDBID(action.Snapshot.Id),
                _save is null ? null : SaveAction,
                move: direction => MoveAction(action.Snapshot.Id, direction),
                isServerAdministrator: _isServerAdministrator,
                isAuthenticated: _isAuthenticated);
        }
    }

    public IInterfaceRuleAction get_ItemByDBID(int databaseId)
    {
        var match = GetActions().FirstOrDefault(action => action.Snapshot.Id == databaseId);

        return match is null
            ? throw new COMException(
                "No rule action with the specified database identifier exists.",
                DispEBadIndex)
            : RuleAction.CreateAuthorized(
                match,
                () => DeleteByDBID(match.Snapshot.Id),
                _save is null ? null : SaveAction,
                move: direction => MoveAction(match.Snapshot.Id, direction),
                isServerAdministrator: _isServerAdministrator,
                isAuthenticated: _isAuthenticated);
    }

    public int Count => GetActions().Count;

    public IInterfaceRuleAction Add()
    {
        EnsureAuthenticated();
        _ = GetActions();
        if (_insert is null || _owningRuleId is null)
        {
            return Unavailable<IInterfaceRuleAction>();
        }

        var entry = new RuleActionAdministrationEntry(
            new RuleActionAdministrationSnapshot(
                Id: 0,
                RuleId: _owningRuleId.Value,
                Type: (int)ComRuleActionType.Unknown,
                Subject: string.Empty,
                Body: string.Empty,
                FromName: string.Empty,
                FromAddress: string.Empty,
                Filename: string.Empty,
                To: string.Empty,
                ImapFolder: string.Empty,
                ScriptFunction: string.Empty,
                HeaderName: string.Empty,
                Value: string.Empty,
                RouteId: 0,
                AbortSpamFlagged: false,
                SortOrder: 0));

        return RuleAction.CreateAuthorized(
            entry,
            save: action => SaveAddedAction(entry, action),
            move: direction => MoveAction(entry.Snapshot.Id, direction),
            isServerAdministrator: _isServerAdministrator,
            isAuthenticated: _isAuthenticated);
    }

    public void DeleteByDBID(int databaseId)
    {
        EnsureAuthenticated();
        var actions = GetActions();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (!actions.Any(action => action.Snapshot.Id == databaseId))
        {
            return;
        }

        try
        {
            _deleteById(databaseId);
            _state!.RemoveByDBID(databaseId);
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
        EnsureAuthenticated();
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
            _state!.Replace(actions);
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of rule actions from the database.",
                EFail);
        }
    }

    public void Delete(int index)
    {
        EnsureAuthenticated();
        var actions = GetActions();
        if (_deleteById is null)
        {
            Unavailable();
            return;
        }

        if (index < 0 || index >= actions.Count)
        {
            return;
        }

        var action = actions[index];
        try
        {
            _deleteById(action.Snapshot.Id);
            _state!.RemoveAt(index);
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to delete the rule action from the database.",
                EFail);
        }
    }

    internal static RuleActions CreateAuthorized(
        IReadOnlyList<RuleActionAdministrationSnapshot> actions,
        Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null,
        Action<RuleActionAdministrationSnapshot>? save = null,
        Action<IReadOnlyList<RuleActionAdministrationSnapshot>>? saveOrder = null,
        Func<bool>? isServerAdministrator = null,
        int? owningRuleId = null,
        Func<int, RuleActionAdministrationSnapshot, int>? insert = null,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return new RuleActions(actions, reload, deleteById, insert, save, saveOrder, isServerAdministrator, owningRuleId, isAuthenticated);
    }

    internal static RuleActions CreateAuthorized(
        RuleActionAdministrationState state,
        Func<IReadOnlyList<RuleActionAdministrationSnapshot>>? reload = null,
        Action<int>? deleteById = null,
        Action<RuleActionAdministrationSnapshot>? save = null,
        Action<IReadOnlyList<RuleActionAdministrationSnapshot>>? saveOrder = null,
        Func<bool>? isServerAdministrator = null,
        int? owningRuleId = null,
        Func<int, RuleActionAdministrationSnapshot, int>? insert = null,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new RuleActions(state, reload, deleteById, insert, save, saveOrder, isServerAdministrator, owningRuleId, isAuthenticated);
    }

    private void SaveAction(RuleActionAdministrationSnapshot action)
    {
        if (_save is null)
        {
            Unavailable();
            return;
        }

        _save(action);
    }

    private void MoveAction(int databaseId, bool moveUp)
    {
        EnsureAuthenticated();
        var actions = GetActions();
        var index = actions.ToList().FindIndex(action => action.Snapshot.Id == databaseId);
        if (index < 0)
        {
            return;
        }

        if (_saveOrder is null || _owningRuleId is null)
        {
            Unavailable();
            return;
        }

        var targetIndex = moveUp ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= actions.Count)
        {
            return;
        }

        var ordered = actions.ToArray();
        (ordered[index], ordered[targetIndex]) = (ordered[targetIndex], ordered[index]);
        var normalized = ordered
            .Select(
                static (entry, position) =>
                    (Entry: entry, Snapshot: entry.Snapshot with { SortOrder = position + 1 }))
            .ToArray();
        var updates = normalized
            .Where(static item => item.Entry.Snapshot.Id > 0 && item.Entry.Snapshot.SortOrder != item.Snapshot.SortOrder)
            .Select(static item => item.Snapshot)
            .ToArray();

        try
        {
            _saveOrder(updates);
            _state!.ApplyOrder(normalized);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to move the rule action in the database.",
                EFail);
        }
    }

    private RuleActionAdministrationSnapshot SaveNewAction(
        RuleActionAdministrationEntry entry,
        RuleActionAdministrationSnapshot action)
    {
        var actions = GetActions();
        if (_insert is null || _owningRuleId is null)
        {
            Unavailable();
        }

        var prepared = action with { RuleId = _owningRuleId.GetValueOrDefault() };
        if (prepared.SortOrder == 0)
        {
            var sortOrder = actions.Count == 0
                ? 1
                : actions[^1].Snapshot.SortOrder + 1;
            prepared = prepared with { SortOrder = sortOrder };
            entry.Snapshot = prepared;
        }

        var owningRuleId = _owningRuleId.GetValueOrDefault();
        var generatedId = _insert!(owningRuleId, prepared);
        if (generatedId <= 0)
        {
            throw new InvalidOperationException("The rule action insert did not return a valid generated identity.");
        }

        var persisted = prepared with { Id = generatedId };
        entry.Snapshot = persisted;
        _state!.Append(persisted);
        return persisted;
    }

    private void SaveAddedAction(
        RuleActionAdministrationEntry entry,
        RuleActionAdministrationSnapshot action)
    {
        if (action.Id == 0)
        {
            _ = SaveNewAction(entry, action);
            return;
        }

        SaveAction(action);
    }

    private IReadOnlyList<RuleActionAdministrationEntry> GetActions()
    {
        EnsureAuthenticated();
        return _state?.GetActions()
            ?? throw new COMException(
                "RuleActions access requires an authenticated server administrator.",
                EAccessDenied);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "RuleActions access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "RuleActions access requires an authenticated server administrator.",
                EAccessDenied);
        }
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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RuleActionAdministrationEntry? _entry;
    private readonly Action? _delete;
    private readonly Action<RuleActionAdministrationSnapshot>? _save;
    private readonly Action<bool>? _move;
    private readonly Func<RuleActionAdministrationSnapshot, RuleActionAdministrationSnapshot>? _saveNew;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<bool>? _isAuthenticated;

    public RuleAction()
    {
    }

    private RuleAction(
        RuleActionAdministrationEntry entry,
        Action? delete,
        Action<RuleActionAdministrationSnapshot>? save,
        Action<bool>? move,
        Func<bool>? isServerAdministrator,
        Func<RuleActionAdministrationSnapshot, RuleActionAdministrationSnapshot>? saveNew,
        Func<bool>? isAuthenticated)
    {
        _entry = entry;
        _delete = delete;
        _save = save;
        _move = move;
        _saveNew = saveNew;
        _isServerAdministrator = isServerAdministrator;
        _isAuthenticated = isAuthenticated;
    }

    public int ID => Snapshot.Id;

    public int RuleID { get => Snapshot.RuleId; set => Mutate(snapshot => snapshot with { RuleId = value }); }

    public ComRuleActionType Type
    {
        get => (ComRuleActionType)Snapshot.Type;
        set
        {
            if (value == ComRuleActionType.RunScriptFunction)
            {
                EnsureServerAdministrator();
            }

            Mutate(snapshot => snapshot with { Type = (int)value });
        }
    }

    public string Subject { get => Snapshot.Subject; set => Mutate(snapshot => snapshot with { Subject = value }); }

    public string Body { get => Snapshot.Body; set => Mutate(snapshot => snapshot with { Body = value }); }

    public string FromName { get => Snapshot.FromName; set => Mutate(snapshot => snapshot with { FromName = value }); }

    public string FromAddress { get => Snapshot.FromAddress; set => Mutate(snapshot => snapshot with { FromAddress = value }); }

    public string Filename { get => Snapshot.Filename; set => Mutate(snapshot => snapshot with { Filename = value }); }

    public string To { get => Snapshot.To; set => Mutate(snapshot => snapshot with { To = value }); }

    public string IMAPFolder
    {
        get => LegacyModifiedUtf7.Decode(Snapshot.ImapFolder);
        set => Mutate(snapshot => snapshot with { ImapFolder = LegacyModifiedUtf7.Encode(value) });
    }

    public string ScriptFunction
    {
        get => Snapshot.ScriptFunction;
        set
        {
            _ = Snapshot;
            EnsureServerAdministrator();
            Mutate(snapshot => snapshot with { ScriptFunction = value });
        }
    }

    public string HeaderName { get => Snapshot.HeaderName; set => Mutate(snapshot => snapshot with { HeaderName = value }); }

    public string Value { get => Snapshot.Value; set => Mutate(snapshot => snapshot with { Value = value }); }

    public int RouteID { get => Snapshot.RouteId; set => Mutate(snapshot => snapshot with { RouteId = value }); }

    public bool AbortSpamFlagged { get => Snapshot.AbortSpamFlagged; set => Mutate(snapshot => snapshot with { AbortSpamFlagged = value }); }

    public void Save()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (snapshot.Type == (int)ComRuleActionType.RunScriptFunction)
        {
            EnsureServerAdministrator();
        }

        if (snapshot.Id == 0)
        {
            if (_saveNew is not null)
            {
                try
                {
                    _entry!.Snapshot = _saveNew(snapshot);
                }
                catch (COMException)
                {
                    throw;
                }
                catch (Exception)
                {
                    throw new COMException(
                        "It was not possible to save the rule action to the database.",
                        EFail);
                }

                return;
            }
        }

        if (_save is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _save(Snapshot);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the rule action to the database.",
                EFail);
        }
    }

    public void MoveUp()
    {
        EnsureAuthenticated();
        if (_move is null)
        {
            Unavailable();
            return;
        }

        _move(true);
    }

    public void MoveDown()
    {
        EnsureAuthenticated();
        if (_move is null)
        {
            Unavailable();
            return;
        }

        _move(false);
    }

    public void Delete()
    {
        EnsureAuthenticated();
        _ = Snapshot;
        if (_delete is null)
        {
            Unavailable();
            return;
        }

        _delete();
    }

    internal static RuleAction CreateAuthorized(
        RuleActionAdministrationEntry entry,
        Action? delete = null,
        Action<RuleActionAdministrationSnapshot>? save = null,
        Action<bool>? move = null,
        Func<bool>? isServerAdministrator = null,
        Func<RuleActionAdministrationSnapshot, RuleActionAdministrationSnapshot>? saveNew = null,
        Func<bool>? isAuthenticated = null) =>
        new(entry, delete, save, move, isServerAdministrator, saveNew, isAuthenticated);

    private void Mutate(Func<RuleActionAdministrationSnapshot, RuleActionAdministrationSnapshot> mutation)
    {
        if (_save is null && _saveNew is null)
        {
            Unavailable();
            return;
        }

        _entry!.Snapshot = mutation(Snapshot);
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is not null && !_isServerAdministrator())
        {
            throw new COMException(
                "RuleAction access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "RuleAction access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private RuleActionAdministrationSnapshot Snapshot
    {
        get
        {
            EnsureAuthenticated();
            return _entry?.Snapshot ?? throw new COMException(
                "RuleAction access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

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

    internal static RuleActions CreateAuthorizedAdapter(
        int ruleId,
        RuleAdministrationGeneration? generation = null,
        Func<bool>? isServerAdministrator = null,
        Func<bool>? isAuthenticated = null)
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

        void SaveAction(RuleActionAdministrationSnapshot action) => store
            .SaveRuleActionAsync(ruleId, action, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        void SaveActionOrder(IReadOnlyList<RuleActionAdministrationSnapshot> actions) => store
            .SaveRuleActionOrderAsync(ruleId, actions, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        int InsertAction(int owningRuleId, RuleActionAdministrationSnapshot action) => store
            .InsertRuleActionAsync(owningRuleId, action, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (generation is null)
        {
            return RuleActions.CreateAuthorized(
                LoadActions(),
                LoadActions,
                DeleteActionById,
                SaveAction,
                saveOrder: SaveActionOrder,
                isServerAdministrator: isServerAdministrator ?? (static () => true),
                owningRuleId: ruleId,
                insert: InsertAction,
                isAuthenticated: isAuthenticated);
        }

        var state = generation.GetActionState(ruleId);
        state.Initialize(LoadActions);
        return RuleActions.CreateAuthorized(
            state,
            LoadActions,
            DeleteActionById,
            SaveAction,
            saveOrder: SaveActionOrder,
            isServerAdministrator: isServerAdministrator ?? (static () => true),
            owningRuleId: ruleId,
            insert: InsertAction,
            isAuthenticated: isAuthenticated);
    }
}
