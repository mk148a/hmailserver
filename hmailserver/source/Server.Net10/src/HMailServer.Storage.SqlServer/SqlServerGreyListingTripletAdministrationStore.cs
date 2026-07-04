using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerGreyListingTripletAdministrationStore
    : IGreyListingTripletAdministrationStore
{
    public const string ClearAllSql = "DELETE FROM hm_greylisting_triplets;";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerGreyListingTripletAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask ClearAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(ClearAllSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
