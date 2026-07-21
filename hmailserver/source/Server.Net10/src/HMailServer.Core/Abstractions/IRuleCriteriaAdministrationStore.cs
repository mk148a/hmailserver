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

    ValueTask SaveRuleCriteriaAsync(
        RuleCriteriaAdministrationSnapshot criterion,
        CancellationToken cancellationToken);
}
