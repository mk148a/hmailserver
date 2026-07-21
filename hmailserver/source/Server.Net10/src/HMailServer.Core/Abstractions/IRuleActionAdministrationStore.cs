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

    ValueTask SaveRuleActionAsync(
        RuleActionAdministrationSnapshot action,
        CancellationToken cancellationToken);
}
