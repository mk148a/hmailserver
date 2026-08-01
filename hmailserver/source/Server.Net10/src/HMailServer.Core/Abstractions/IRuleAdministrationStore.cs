namespace HMailServer.Core.Abstractions;

public interface IRuleAdministrationStore
{
    ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
        int accountId,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteRuleAsync(
        int accountId,
        int ruleId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
