namespace HMailServer.Core.Abstractions;

public interface IBackupRuleAdministrationStore
{
    ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetBackupRulesAsync(
        int accountId,
        CancellationToken cancellationToken);
}
