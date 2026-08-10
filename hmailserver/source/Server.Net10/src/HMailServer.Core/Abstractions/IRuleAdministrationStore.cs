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
        throw new NotSupportedException("Rule deletion is not available in this store.");

    ValueTask<int> InsertRuleAsync(
        int accountId,
        RuleAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Rule insertion is not available in this store.");

    ValueTask<bool> UpdateRuleAsync(
        int accountId,
        RuleAdministrationSnapshot snapshot,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Rule update is not available in this store.");

    ValueTask<bool> MoveRuleAsync(
        int accountId,
        int ruleId,
        bool moveUp,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Rule reordering is not available in this store.");
}
