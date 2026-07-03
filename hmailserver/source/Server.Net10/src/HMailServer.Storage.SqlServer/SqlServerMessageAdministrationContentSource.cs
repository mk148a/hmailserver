using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageAdministrationContentSource : IMessageAdministrationContentSource
{
    public const string GetAccountAddressSql = """
SELECT accountaddress
FROM hm_accounts
WHERE accountid = @AccountID;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly MessageFilePathResolver _pathResolver;

    public SqlServerMessageAdministrationContentSource(
        SqlServerConnectionFactory connectionFactory,
        MessageFilePathResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(pathResolver);
        _connectionFactory = connectionFactory;
        _pathResolver = pathResolver;
    }

    public async ValueTask<byte[]?> TryLoadMessageAsync(
        MessageAdministrationSnapshot message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var accountAddress = message.AccountId > 0
            ? await TryGetAccountAddressAsync(message.AccountId, cancellationToken).ConfigureAwait(false)
            : null;
        var path = _pathResolver.Resolve(
            message.FileName,
            message.AccountId,
            message.FolderId,
            accountAddress);

        if (path is null || !File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<string?> TryGetAccountAddressAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetAccountAddressSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;

        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }
}
