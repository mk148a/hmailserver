namespace HMailServer.Core.Abstractions;

public interface IRuleAdministrationStore
{
    ValueTask<IReadOnlyList<RuleAdministrationSnapshot>> GetRulesAsync(
        int accountId,
        CancellationToken cancellationToken);
}
