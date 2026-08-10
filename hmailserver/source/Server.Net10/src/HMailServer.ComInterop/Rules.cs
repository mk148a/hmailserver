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
    private const int EFail = unchecked((int)0x80004005);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private readonly RuleAdministrationState? _state;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<bool>? _isAuthenticated;

    public Rules()
    {
    }

    private Rules(
        IReadOnlyList<RuleAdministrationSnapshot> rules,
        Func<IReadOnlyList<RuleAdministrationSnapshot>>? reload,
        Func<int, int, ValueTask<bool>>? delete,
        Func<bool>? isServerAdministrator,
        Func<bool>? isAuthenticated,
        int accountId = 0,
        Func<RuleAdministrationSnapshot, int>? insert = null,
        Func<RuleAdministrationSnapshot, bool>? update = null,
        Func<int, int, bool, ValueTask<bool>>? move = null)
    {
        _state = RuleAdministrationState.CreateLoaded(rules, reload, delete, accountId, insert, update, move);
        _isServerAdministrator = isServerAdministrator;
        _isAuthenticated = isAuthenticated;
    }

    private Rules(RuleAdministrationState state, Func<bool>? isServerAdministrator, Func<bool>? isAuthenticated)
    {
        _state = state;
        _isServerAdministrator = isServerAdministrator;
        _isAuthenticated = isAuthenticated;
    }

    public int Count
    {
        get
        {
            EnsureAuthenticated();
            return GetRules().Count;
        }
    }

    internal static Rules CreateAuthorized(
        IReadOnlyList<RuleAdministrationSnapshot> rules,
        Func<IReadOnlyList<RuleAdministrationSnapshot>>? reload = null,
        Func<int, int, ValueTask<bool>>? delete = null,
        Func<bool>? isServerAdministrator = null,
        Func<bool>? isAuthenticated = null,
        int accountId = 0,
        Func<RuleAdministrationSnapshot, int>? insert = null,
        Func<RuleAdministrationSnapshot, bool>? update = null,
        Func<int, int, bool, ValueTask<bool>>? move = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new Rules(rules, reload, delete, isServerAdministrator, isAuthenticated, accountId, insert, update, move);
    }

    internal static Rules CreateAuthorized(
        RuleAdministrationState state,
        Func<bool>? isServerAdministrator = null,
        Func<bool>? isAuthenticated = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new Rules(state, isServerAdministrator, isAuthenticated);
    }

    public IInterfaceRule this[int index]
    {
        get
        {
            EnsureAuthenticated();
            var generation = GetGeneration();
            var rules = generation.Rules;
            if (index < 0 || index >= rules.Count)
            {
                throw new COMException("Rule index was outside the collection.", DispEBadIndex);
            }

            return Rule.CreateAuthorized(rules[index], generation, GetState(), _isServerAdministrator, _isAuthenticated, update: GetState().CanUpdate ? GetState().UpdateRule : null);
        }
    }

    public IInterfaceRule get_ItemByDBID(int databaseId)
    {
        EnsureAuthenticated();
        var generation = GetGeneration();
        var match = generation.Rules.FirstOrDefault(rule => rule.Id == databaseId);

        return match is null
            ? throw new COMException("No rule with the specified database identifier exists.", DispEBadIndex)
            : Rule.CreateAuthorized(match, generation, GetState(), _isServerAdministrator, _isAuthenticated, update: GetState().CanUpdate ? GetState().UpdateRule : null);
    }

        public IInterfaceRule Add()
    {
        EnsureAuthenticated();
        var state = GetState();
        _ = state.GetGeneration();
        if (state.AccountId == 0 || !state.CanInsert)
        {
            return Unavailable<IInterfaceRule>();
        }

        return Rule.CreateAuthorized(
            new RuleAdministrationSnapshot(
                Id: 0,
                AccountId: state.AccountId,
                Name: string.Empty,
                Active: true,
                UseAnd: true,
                SortOrder: 0),
            generation: GetGeneration(),
            state: state,
            save: state.InsertRule,
            isServerAdministrator: _isServerAdministrator,
            isAuthenticated: _isAuthenticated);
    }



    public void DeleteByDBID(int databaseId)
    {
        EnsureAuthenticated();
        var state = GetState();
        var generation = state.GetGeneration();
        var selected = generation.Rules.FirstOrDefault(rule => rule.Id == databaseId);
        if (selected is not null)
        {
            state.Delete(selected);
        }
    }

    public void Refresh()
    {
        EnsureAuthenticated();
        var state = GetState();
        _ = state.GetGeneration();
        if (!state.CanRefresh)
        {
            Unavailable();
            return;
        }

        try
        {
            state.Refresh();
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to retrieve a list of rules from the database.",
                EFail);
        }
    }

    private RuleAdministrationGeneration GetGeneration()
    {
        return GetState().GetGeneration();
    }

    private IReadOnlyList<RuleAdministrationSnapshot> GetRules() => GetGeneration().Rules;

    private RuleAdministrationState GetState() =>
        _state ?? throw new COMException(
            "Rules access requires an authenticated server administrator.",
            EAccessDenied);

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Rules access requires an authenticated server administrator.",
                EAccessDenied);
        }
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
    private const int EObjectNotYetSaved = unchecked((int)0x800403E9);
    private const int ENotImplemented = unchecked((int)0x80004001);

    private RuleAdministrationSnapshot? _rule;
    private readonly RuleAdministrationGeneration? _generation;
    private readonly RuleAdministrationState? _state;
    private readonly Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot>? _save;
    private readonly Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot>? _update;
    private readonly Func<bool>? _isServerAdministrator;
    private readonly Func<bool>? _isAuthenticated;

    public Rule()
    {
    }

    private Rule(
        RuleAdministrationSnapshot rule,
        RuleAdministrationGeneration generation,
        RuleAdministrationState? state,
        Func<bool>? isServerAdministrator,
        Func<bool>? isAuthenticated,
        Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot>? save = null,
        Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot>? update = null)
    {
        _rule = rule;
        _generation = generation;
        _state = state;
        _isServerAdministrator = isServerAdministrator;
        _isAuthenticated = isAuthenticated;
        _save = save;
        _update = update;
    }

    public int ID => Snapshot.Id;

    public int AccountID { get => Snapshot.AccountId; set => Mutate(rule => rule with { AccountId = value }); }

    public string Name { get => Snapshot.Name; set => Mutate(rule => rule with { Name = value ?? string.Empty }); }

    public bool Active { get => Snapshot.Active; set => Mutate(rule => rule with { Active = value }); }

    public bool UseAND { get => Snapshot.UseAnd; set => Mutate(rule => rule with { UseAnd = value }); }

    public IInterfaceRuleCriterias Criterias
    {
        get
        {
            EnsureAuthenticated();
            return RuleCriteriaAdministrationRuntimeHost.CreateAuthorizedAdapter(Snapshot.Id, _isAuthenticated);
        }
    }

    public IInterfaceRuleActions Actions
    {
        get
        {
            EnsureAuthenticated();
            return RuleActionAdministrationRuntimeHost.CreateAuthorizedAdapter(
                Snapshot.Id,
                _generation,
                _isServerAdministrator,
                _isAuthenticated);
        }
    }

    internal static Rule CreateAuthorized(RuleAdministrationSnapshot rule) =>
        new(rule, new RuleAdministrationGeneration(new[] { rule }), null, null, null);

    internal static Rule CreateAuthorized(
        RuleAdministrationSnapshot rule,
        RuleAdministrationGeneration generation,
        RuleAdministrationState state,
        Func<bool>? isServerAdministrator = null,
        Func<bool>? isAuthenticated = null,
        Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot>? save = null,
        Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot>? update = null) =>
        new(rule, generation, state, isServerAdministrator, isAuthenticated, save, update);

        public void Save()
    {
        EnsureAuthenticated();
        var snapshot = Snapshot;
        if (snapshot.Id == 0)
        {
            if (_save is null)
            {
                Unavailable();
                return;
            }

            try
            {
                _rule = _save(snapshot);
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the rule to the database.",
                    unchecked((int)0x80004005));
            }

            return;
        }

        if (_update is null)
        {
            Unavailable();
            return;
        }

        try
        {
            _rule = _update(snapshot);
        }
        catch (COMException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new COMException(
                "It was not possible to save the rule to the database.",
                unchecked((int)0x80004005));
        }
    }

    public void MoveUp()
    {
        var snapshot = Snapshot;
        if (snapshot.Id <= 0)
        {
            throw new COMException("Object not yet saved.", EObjectNotYetSaved);
        }

        if (_state is null)
        {
            Unavailable();
            return;
        }

        _state.Move(snapshot, moveUp: true);
    }

    public void MoveDown()
    {
        var snapshot = Snapshot;
        if (snapshot.Id <= 0)
        {
            throw new COMException("Object not yet saved.", EObjectNotYetSaved);
        }

        if (_state is null)
        {
            Unavailable();
            return;
        }

        _state.Move(snapshot, moveUp: false);
    }

    public void Delete()
    {
        var snapshot = Snapshot;
        if (_state is null)
        {
            Unavailable();
            return;
        }

        _state.Delete(snapshot);
    }

    private RuleAdministrationSnapshot Snapshot =>
        GetAuthenticatedSnapshot();

    private RuleAdministrationSnapshot GetAuthenticatedSnapshot()
    {
        EnsureAuthenticated();
        return _rule ?? throw new COMException(
            "Rule access requires an authenticated server administrator.",
            EAccessDenied);
    }

    private void EnsureAuthenticated()
    {
        if (_isAuthenticated is not null && !_isAuthenticated())
        {
            throw new COMException(
                "Rules access requires an authenticated server administrator.",
                EAccessDenied);
        }
    }

    private void Mutate(Func<RuleAdministrationSnapshot, RuleAdministrationSnapshot> mutation)
    {
        EnsureAuthenticated();
        if (_save is null && _update is null)
        {
            Unavailable();
            return;
        }

        _rule = mutation(Snapshot);
    }
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
internal sealed class RuleAdministrationGeneration
{
    private readonly object _actionStateGate = new();
    private readonly Dictionary<int, RuleActionAdministrationState> _actionStates = [];

    internal RuleAdministrationGeneration(IReadOnlyList<RuleAdministrationSnapshot> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules.ToArray();
    }

    internal IReadOnlyList<RuleAdministrationSnapshot> Rules { get; }

    internal RuleActionAdministrationState GetActionState(int ruleId)
    {
        lock (_actionStateGate)
        {
            if (!_actionStates.TryGetValue(ruleId, out var state))
            {
                state = new RuleActionAdministrationState();
                _actionStates.Add(ruleId, state);
            }

            return state;
        }
    }
}

[ComVisible(false)]
internal sealed class RuleAdministrationState
{
    private readonly object _gate = new();
    private readonly Func<IReadOnlyList<RuleAdministrationSnapshot>>? _load;
    private readonly Func<IReadOnlyList<RuleAdministrationSnapshot>>? _reload;
    private RuleAdministrationGeneration? _generation;
    private readonly Func<int, int, ValueTask<bool>>? _delete;
    private readonly int _accountId;
    private readonly Func<RuleAdministrationSnapshot, int>? _insert;
    private readonly Func<RuleAdministrationSnapshot, bool>? _update;
    private readonly Func<int, int, bool, ValueTask<bool>>? _move;

    private RuleAdministrationState(
        Func<IReadOnlyList<RuleAdministrationSnapshot>> load,
        IReadOnlyList<RuleAdministrationSnapshot>? rules,
        Func<IReadOnlyList<RuleAdministrationSnapshot>>? reload,
        Func<int, int, ValueTask<bool>>? delete,
        int accountId = 0,
        Func<RuleAdministrationSnapshot, int>? insert = null,
        Func<RuleAdministrationSnapshot, bool>? update = null,
        Func<int, int, bool, ValueTask<bool>>? move = null)
    {
        _load = load;
        _reload = reload;
        _delete = delete;
        _accountId = accountId;
        _insert = insert;
        _update = update;
        _move = move;
        _generation = rules is null ? null : new RuleAdministrationGeneration(rules);
    }

    internal bool CanRefresh => _reload is not null;

    internal int AccountId => _accountId;

    internal bool CanInsert => _insert is not null;
    internal bool CanUpdate => _update is not null;

    internal static RuleAdministrationState CreateLazy(
        Func<IReadOnlyList<RuleAdministrationSnapshot>> load,
        Func<int, int, ValueTask<bool>>? delete = null,
        int accountId = 0,
        Func<RuleAdministrationSnapshot, int>? insert = null,
        Func<RuleAdministrationSnapshot, bool>? update = null,
        Func<int, int, bool, ValueTask<bool>>? move = null) =>
        new(load, null, load, delete, accountId, insert, update, move);

    internal static RuleAdministrationState CreateLoaded(
        IReadOnlyList<RuleAdministrationSnapshot> rules,
        Func<IReadOnlyList<RuleAdministrationSnapshot>>? reload,
        Func<int, int, ValueTask<bool>>? delete = null,
        int accountId = 0,
        Func<RuleAdministrationSnapshot, int>? insert = null,
        Func<RuleAdministrationSnapshot, bool>? update = null,
        Func<int, int, bool, ValueTask<bool>>? move = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new(reload ?? (() => rules), rules, reload, delete, accountId, insert, update, move);
    }

    internal RuleAdministrationGeneration GetGeneration()
    {
        var generation = Volatile.Read(ref _generation);
        if (generation is not null)
        {
            return generation;
        }

        lock (_gate)
        {
            generation = _generation;
            if (generation is null)
            {
                var rules = _load!();
                ArgumentNullException.ThrowIfNull(rules);
                generation = new RuleAdministrationGeneration(rules);
                Volatile.Write(ref _generation, generation);
            }

            return generation;
        }
    }

    internal void Refresh()
    {
        var rules = _reload!();
        ArgumentNullException.ThrowIfNull(rules);
        Volatile.Write(ref _generation, new RuleAdministrationGeneration(rules));
    }

    internal RuleAdministrationSnapshot InsertRule(RuleAdministrationSnapshot selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        lock (_gate)
        {
            var generation = GetGeneration();
            if (_insert is null)
            {
                throw new COMException(
                    "This Rule member is not implemented by the .NET 10 rewrite yet.",
                    unchecked((int)0x80004001));
            }

            int insertedId;
            try
            {
                insertedId = _insert(selected);
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the rule to the database.",
                    unchecked((int)0x80004005));
            }

            if (insertedId <= 0)
            {
                throw new COMException(
                    "It was not possible to save the rule to the database.",
                    unchecked((int)0x80004005));
            }

            var inserted = selected with { Id = insertedId };
            Volatile.Write(
                ref _generation,
                new RuleAdministrationGeneration(generation.Rules.Append(inserted).ToArray()));
            return inserted;
        }
    }
    internal RuleAdministrationSnapshot UpdateRule(RuleAdministrationSnapshot selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        lock (_gate)
        {
            var generation = GetGeneration();
            if (_update is null)
            {
                throw new COMException(
                    "This Rule member is not implemented by the .NET 10 rewrite yet.",
                    unchecked((int)0x80004001));
            }

            var current = generation.Rules.FirstOrDefault(rule => rule.Id == selected.Id);
            var persisted = current is null
                ? selected
                : selected with { SortOrder = current.SortOrder };

            bool updated;
            try
            {
                updated = _update(persisted);
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to save the rule to the database.",
                    unchecked((int)0x80004005));
            }

            if (!updated)
            {
                throw new COMException(
                    "It was not possible to save the rule to the database.",
                    unchecked((int)0x80004005));
            }

            var matchingIndex = Array.FindIndex(
                generation.Rules.ToArray(),
                current => current.Id == selected.Id);
            if (matchingIndex >= 0)
            {
                var replacedRules = generation.Rules.ToArray();
                replacedRules[matchingIndex] = persisted;
                Volatile.Write(ref _generation, new RuleAdministrationGeneration(replacedRules));
            }

            return persisted;
        }
    }

    internal void Move(RuleAdministrationSnapshot selected, bool moveUp)
    {
        ArgumentNullException.ThrowIfNull(selected);

        lock (_gate)
        {
            var generation = GetGeneration();
            var currentIndex = generation.Rules.ToList().FindIndex(rule => rule.Id == selected.Id);
            if (currentIndex < 0)
            {
                return;
            }

            var targetIndex = moveUp ? currentIndex - 1 : currentIndex + 1;
            if (targetIndex < 0 || targetIndex >= generation.Rules.Count)
            {
                return;
            }

            if (_move is null)
            {
                throw new COMException(
                    "This Rule member is not implemented by the .NET 10 rewrite yet.",
                    unchecked((int)0x80004001));
            }

            bool moved;
            try
            {
                moved = _move(_accountId, selected.Id, moveUp).GetAwaiter().GetResult();
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to move the rule in the database.",
                    unchecked((int)0x80004005));
            }

            if (!moved)
            {
                return;
            }

            var reordered = generation.Rules.ToArray();
            (reordered[currentIndex], reordered[targetIndex]) = (reordered[targetIndex], reordered[currentIndex]);
            for (var index = 0; index < reordered.Length; index++)
            {
                if (reordered[index].SortOrder != index + 1)
                {
                    reordered[index] = reordered[index] with { SortOrder = index + 1 };
                }
            }

            Volatile.Write(ref _generation, new RuleAdministrationGeneration(reordered));
        }
    }

    internal void Delete(RuleAdministrationSnapshot selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        lock (_gate)
        {
            var generation = GetGeneration();
            var current = generation.Rules.FirstOrDefault(rule => ReferenceEquals(rule, selected));
            if (current is null)
            {
                return;
            }

            if (_delete is null)
            {
                throw new COMException(
                    "This Rule member is not implemented by the .NET 10 rewrite yet.",
                    unchecked((int)0x80004001));
            }

            bool deleted;
            try
            {
                deleted = _delete(current.AccountId, current.Id).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                throw new COMException(
                    "It was not possible to delete the rule from the database.",
                    unchecked((int)0x80004005));
            }

            if (!deleted)
            {
                return;
            }

            Volatile.Write(
                ref _generation,
                new RuleAdministrationGeneration(
                    generation.Rules.Where(rule => !ReferenceEquals(rule, current)).ToArray()));
        }
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

    internal static Rules CreateAuthorizedAdapter(
        int accountId,
        Func<bool>? isServerAdministrator = null,
        Func<bool>? isAuthenticated = null)
    {
        var store = Volatile.Read(ref _store)
            ?? throw new COMException(
                "The hMailServer rule administration runtime has not been initialized.",
                CoENotInitialized);

        IReadOnlyList<RuleAdministrationSnapshot> LoadRules() => store
            .GetRulesAsync(accountId, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        ValueTask<bool> DeleteRuleAsync(int ownerAccountId, int ruleId) =>
            store.DeleteRuleAsync(ownerAccountId, ruleId, CancellationToken.None);
        int InsertRule(RuleAdministrationSnapshot rule) =>
            store
                .InsertRuleAsync(accountId, rule, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        bool UpdateRule(RuleAdministrationSnapshot rule) =>
            store
                .UpdateRuleAsync(accountId, rule, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        ValueTask<bool> MoveRuleAsync(int ownerAccountId, int ruleId, bool moveUp) =>
            store.MoveRuleAsync(ownerAccountId, ruleId, moveUp, CancellationToken.None);
        return Rules.CreateAuthorized(LoadRules(), LoadRules, DeleteRuleAsync, isServerAdministrator, isAuthenticated, accountId, InsertRule, UpdateRule, MoveRuleAsync);
    }

    internal static RuleAdministrationState CreateAuthorizedState(int accountId)
    {
        IReadOnlyList<RuleAdministrationSnapshot> LoadRules()
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer rule administration runtime has not been initialized.",
                    CoENotInitialized);

            return store
                .GetRulesAsync(accountId, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        ValueTask<bool> DeleteRuleAsync(int ownerAccountId, int ruleId)
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer rule administration runtime has not been initialized.",
                    CoENotInitialized);

            return store.DeleteRuleAsync(ownerAccountId, ruleId, CancellationToken.None);
        }
        int InsertRule(RuleAdministrationSnapshot rule)
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer rule administration runtime has not been initialized.",
                    CoENotInitialized);

            return store
                .InsertRuleAsync(accountId, rule, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        bool UpdateRule(RuleAdministrationSnapshot rule)
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer rule administration runtime has not been initialized.",
                    CoENotInitialized);

            return store
                .UpdateRuleAsync(accountId, rule, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        ValueTask<bool> MoveRuleAsync(int ownerAccountId, int ruleId, bool moveUp)
        {
            var store = Volatile.Read(ref _store)
                ?? throw new COMException(
                    "The hMailServer rule administration runtime has not been initialized.",
                    CoENotInitialized);

            return store.MoveRuleAsync(ownerAccountId, ruleId, moveUp, CancellationToken.None);
        }
        return RuleAdministrationState.CreateLazy(LoadRules, DeleteRuleAsync, accountId, InsertRule, UpdateRule, MoveRuleAsync);
    }
}
