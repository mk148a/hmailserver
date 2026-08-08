using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerDistributionListAdministrationStore : IDistributionListAdministrationStore
{
    public const string GetDistributionListsSql = """
SELECT
    distributionlistid,
    distributionlistdomainid,
    distributionlistaddress,
    distributionlistenabled,
    distributionlistrequireauth,
    distributionlistrequireaddress,
    distributionlistmode
FROM hm_distributionlists
WHERE distributionlistdomainid = @DomainID
ORDER BY distributionlistaddress ASC;
""";

    public const string InsertDistributionListSql = """
INSERT INTO hm_distributionlists
    (distributionlistdomainid, distributionlistenabled, distributionlistaddress,
     distributionlistrequireauth, distributionlistrequireaddress, distributionlistmode)
OUTPUT INSERTED.distributionlistid
VALUES (@DomainID, @Active, @Address, @RequireSMTPAuth, @RequireSenderAddress, @Mode);
""";

    public const string UpdateDistributionListSql = """
UPDATE hm_distributionlists
SET
    distributionlistdomainid = @DomainID,
    distributionlistenabled = @Active,
    distributionlistaddress = @Address,
    distributionlistrequireauth = @RequireSMTPAuth,
    distributionlistrequireaddress = @RequireSenderAddress,
    distributionlistmode = @Mode
WHERE distributionlistid = @ID;
""";

    public const string DeleteDistributionListRecipientsSql = """
DELETE FROM hm_distributionlistsrecipients
WHERE distributionlistrecipientlistid = @LISTID;
""";

    public const string DeleteDistributionListSql = """
DELETE FROM hm_distributionlists
WHERE distributionlistdomainid = @DomainID
  AND distributionlistid = @LISTID;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerDistributionListAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerDistributionListAdministrationStore(
        SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _connectionFactory = null!;
        _transactionContext = transactionContext;
    }

    private SqlServerConnectionFactory GetStandaloneConnectionFactory() =>
        _transactionContext is not null
            ? throw new InvalidOperationException(
                "Non-transaction-aware operations are not supported on transaction-scoped SQL administration stores.")
            : _connectionFactory;

    public async ValueTask<IReadOnlyList<DistributionListAdministrationSnapshot>> GetDistributionListsAsync(
        int domainId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetDistributionListsSql, connection);
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = domainId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var lists = new List<DistributionListAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lists.Add(
                new DistributionListAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    DomainId: reader.GetInt32(1),
                    Address: reader.GetString(2),
                    Active: Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    RequireSmtpAuth: Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                    RequireSenderAddress: reader.GetString(5),
                    Mode: Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
        }

        return lists;
    }

    public async ValueTask<int> InsertDistributionListAsync(
        DistributionListAdministrationSnapshot distributionList,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(distributionList);

        await using var commandLease = await SqlServerCommandLease
            .OpenAsync(_connectionFactory, _transactionContext, InsertDistributionListSql, cancellationToken)
            .ConfigureAwait(false);
        var command = commandLease.Command;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = distributionList.DomainId;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = distributionList.Active ? 1 : 0;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = distributionList.Address;
        command.Parameters.Add("@RequireSMTPAuth", SqlDbType.TinyInt).Value = distributionList.RequireSmtpAuth ? 1 : 0;
        command.Parameters.Add("@RequireSenderAddress", SqlDbType.NVarChar, 255).Value = distributionList.RequireSenderAddress;
        command.Parameters.Add("@Mode", SqlDbType.TinyInt).Value = distributionList.Mode;

        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateDistributionListAsync(
        DistributionListAdministrationSnapshot distributionList,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        ArgumentNullException.ThrowIfNull(distributionList);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateDistributionListSql, connection);
        command.Parameters.Add("@ID", SqlDbType.Int).Value = distributionList.Id;
        command.Parameters.Add("@DomainID", SqlDbType.Int).Value = distributionList.DomainId;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = distributionList.Active ? 1 : 0;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = distributionList.Address;
        command.Parameters.Add("@RequireSMTPAuth", SqlDbType.TinyInt).Value = distributionList.RequireSmtpAuth ? 1 : 0;
        command.Parameters.Add("@RequireSenderAddress", SqlDbType.NVarChar, 255).Value = distributionList.RequireSenderAddress;
        command.Parameters.Add("@Mode", SqlDbType.TinyInt).Value = distributionList.Mode;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask<bool> DeleteDistributionListAsync(
        int owningDomainId,
        int distributionListId,
        CancellationToken cancellationToken)
    {
        var connectionFactory = GetStandaloneConnectionFactory();
        if (distributionListId == 0)
        {
            return false;
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var recipientsCommand = new SqlCommand(DeleteDistributionListRecipientsSql, connection);
        recipientsCommand.Parameters.Add("@LISTID", SqlDbType.Int).Value = distributionListId;
        _ = await recipientsCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var listCommand = new SqlCommand(DeleteDistributionListSql, connection);
        listCommand.Parameters.Add("@DomainID", SqlDbType.Int).Value = owningDomainId;
        listCommand.Parameters.Add("@LISTID", SqlDbType.Int).Value = distributionListId;
        return await listCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}
