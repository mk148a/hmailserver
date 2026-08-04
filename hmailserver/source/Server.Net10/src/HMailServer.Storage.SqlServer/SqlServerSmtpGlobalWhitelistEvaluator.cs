using HMailServer.Core;
using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSmtpGlobalWhitelistEvaluator : ISmtpGlobalWhitelistEvaluator
{
    private readonly IWhiteListAddressAdministrationStore _store;

    public SqlServerSmtpGlobalWhitelistEvaluator(IWhiteListAddressAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<bool> EvaluateAsync(
        string mailFrom,
        string clientIPAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await _store
                .GetWhiteListAddressesAsync(cancellationToken)
                .ConfigureAwait(false);
            return WhiteListMatcher.IsMatch(clientIPAddress, mailFrom, addresses);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
