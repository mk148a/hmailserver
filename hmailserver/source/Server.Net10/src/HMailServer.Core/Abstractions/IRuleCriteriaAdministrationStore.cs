namespace HMailServer.Core.Abstractions;

public interface IRuleCriteriaAdministrationStore
{
    ValueTask<IReadOnlyList<RuleCriteriaAdministrationSnapshot>> GetRuleCriteriaAsync(
        int ruleId,
        CancellationToken cancellationToken);
}
