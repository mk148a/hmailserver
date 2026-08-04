namespace HMailServer.Core.Abstractions;

public interface IRuleCriteriaAdministrationStore
{
    ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
        int ruleId,
        CancellationToken cancellationToken);

    ValueTask DeleteRuleCriteriaByIdAsync(
        int ruleId,
        int databaseId,
        CancellationToken cancellationToken);

    ValueTask<int> InsertRuleCriteriaAsync(
        int owningRuleId,
        RuleCriteriaAdministrationSnapshot criterion,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    ValueTask SaveRuleCriteriaAsync(
        int owningRuleId,
        RuleCriteriaAdministrationSnapshot criterion,
        CancellationToken cancellationToken);
}
