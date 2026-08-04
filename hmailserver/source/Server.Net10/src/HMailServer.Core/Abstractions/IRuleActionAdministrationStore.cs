namespace HMailServer.Core.Abstractions;

public interface IRuleActionAdministrationStore
{
    ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
        int ruleId,
        CancellationToken cancellationToken);

    ValueTask DeleteRuleActionByIdAsync(
        int ruleId,
        int databaseId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertRuleActionAsync(
        int owningRuleId,
        RuleActionAdministrationSnapshot action,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Rule action insertion is not implemented by this store.");

    ValueTask SaveRuleActionAsync(
        int owningRuleId,
        RuleActionAdministrationSnapshot action,
        CancellationToken cancellationToken);

    ValueTask SaveRuleActionOrderAsync(
        int owningRuleId,
        IReadOnlyList<RuleActionAdministrationSnapshot> actions,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Rule action ordering is not implemented by this store.");
}
