namespace HMailServer.Core.Abstractions;

public interface IRuleActionAdministrationStore
{
    ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
        int ruleId,
        CancellationToken cancellationToken);
}
