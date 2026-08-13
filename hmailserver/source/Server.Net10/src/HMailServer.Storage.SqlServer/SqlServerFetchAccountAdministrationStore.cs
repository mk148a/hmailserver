using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using HMailServer.Security;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerFetchAccountAdministrationStore : IFetchAccountAdministrationStore
{
    public const string GetFetchAccountsSql = """
SELECT
    faid,
    faaccountid,
    faaccountname,
    faserveraddress,
    faserverport,
    faservertype,
    fausername,
    faminutes,
    fadaystokeep,
    faactive,
    faprocessmimerecipients,
    faprocessmimedate,
    faconnectionsecurity,
    fauseantispam,
    fauseantivirus,
    faenablerouterecipients,
    famimerecipientheaders,
    CONVERT(varchar(19), fanexttry, 120) AS fanexttry,
    falocked
FROM hm_fetchaccounts
WHERE faaccountid = @AccountID
ORDER BY faid ASC;
""";

    public const string SetRetryNowSql = """
UPDATE hm_fetchaccounts
SET fanexttry = GETDATE()
WHERE faid = @FetchAccountID
  AND faaccountid = @AccountID;
""";

    public const string InsertFetchAccountSql = """
INSERT INTO hm_fetchaccounts
(
    faactive,
    faaccountid,
    faaccountname,
    faserveraddress,
    faserverport,
    faservertype,
    fausername,
    fapassword,
    faminutes,
    fanexttry,
    fadaystokeep,
    falocked,
    faprocessmimerecipients,
    faprocessmimedate,
    faconnectionsecurity,
    fauseantispam,
    fauseantivirus,
    faenablerouterecipients,
    famimerecipientheaders
)
OUTPUT INSERTED.faid
VALUES
(
    @Active,
    @AccountID,
    @Name,
    @ServerAddress,
    @Port,
    @ServerType,
    @Username,
    @Password,
    @Minutes,
    GETDATE(),
    @DaysToKeep,
    0,
    @ProcessMimeRecipients,
    @ProcessMimeDate,
    @ConnectionSecurity,
    @UseAntiSpam,
    @UseAntiVirus,
    @EnableRouteRecipients,
    @MimeRecipientHeaders
);
""";

    public const string UpdateFetchAccountSql = """
UPDATE hm_fetchaccounts
SET
    faactive = @Active,
    faaccountid = @AccountID,
    faaccountname = @Name,
    faserveraddress = @ServerAddress,
    faserverport = @Port,
    faservertype = @ServerType,
    fausername = @Username,
    fapassword = CASE WHEN @Password IS NULL THEN fapassword ELSE @Password END,
    faminutes = @Minutes,
    fanexttry = GETDATE(),
    fadaystokeep = @DaysToKeep,
    faprocessmimerecipients = @ProcessMimeRecipients,
    faprocessmimedate = @ProcessMimeDate,
    faconnectionsecurity = @ConnectionSecurity,
    fauseantispam = @UseAntiSpam,
    fauseantivirus = @UseAntiVirus,
    faenablerouterecipients = @EnableRouteRecipients,
    famimerecipientheaders = @MimeRecipientHeaders
WHERE faid = @FetchAccountID
  AND faaccountid = @AccountID;
SELECT @@ROWCOUNT;
""";

    public const string DeleteFetchAccountSql = """
DELETE FROM hm_fetchaccounts
WHERE faid = @FetchAccountID
  AND faaccountid = @AccountID;
""";

    public const string DeleteFetchAccountUidsSql = """
DELETE FROM hm_fetchaccounts_uids
WHERE uidfaid = @FetchAccountID;
""";

    public const string InsertFetchAccountUidSql = """
INSERT INTO hm_fetchaccounts_uids
(
    uidfaid,
    uidvalue,
    uidtime
)
VALUES
(
    @FetchAccountID,
    @UID,
    @Date
);
""";

    private readonly SqlServerConnectionFactory? _connectionFactory;
    private readonly SqlServerBackupRestoreTransactionContext? _transactionContext;

    public SqlServerFetchAccountAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    internal SqlServerFetchAccountAdministrationStore(SqlServerBackupRestoreTransactionContext transactionContext)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        _transactionContext = transactionContext;
    }

    public async ValueTask<IReadOnlyList<FetchAccountAdministrationSnapshot>> GetFetchAccountsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory!.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetFetchAccountsSql, connection);
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var accounts = new List<FetchAccountAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(
                new FetchAccountAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    AccountId: reader.GetInt32(1),
                    Name: reader.GetString(2),
                    ServerAddress: reader.GetString(3),
                    Port: reader.GetInt32(4),
                    ServerType: Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Username: reader.GetString(6),
                    MinutesBetweenFetch: reader.GetInt32(7),
                    DaysToKeepMessages: reader.GetInt32(8),
                    Enabled: ReadLegacyBoolean(reader, 9),
                    ProcessMimeRecipients: ReadLegacyBoolean(reader, 10),
                    ProcessMimeDate: ReadLegacyBoolean(reader, 11),
                    ConnectionSecurity: Convert.ToInt32(reader.GetValue(12), CultureInfo.InvariantCulture),
                    UseAntiSpam: ReadLegacyBoolean(reader, 13),
                    UseAntiVirus: ReadLegacyBoolean(reader, 14),
                    EnableRouteRecipients: ReadLegacyBoolean(reader, 15),
                    MimeRecipientHeaders: reader.GetString(16),
                    NextDownloadTime: reader.GetString(17),
                    IsLocked: ReadLegacyBoolean(reader, 18)));
        }

        return accounts;
    }

    public async ValueTask SetRetryNowAsync(
        int accountId,
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory!.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SetRetryNowSql, connection);
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> InsertFetchAccountAsync(
        FetchAccountAdministrationDraft account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var connection = await _connectionFactory!.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertFetchAccountSql, connection);
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = account.Enabled ? 1 : 0;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = account.AccountId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = account.Name;
        command.Parameters.Add("@ServerAddress", SqlDbType.NVarChar, 255).Value = account.ServerAddress;
        command.Parameters.Add("@Port", SqlDbType.Int).Value = account.Port;
        command.Parameters.Add("@ServerType", SqlDbType.TinyInt).Value = account.ServerType;
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 255).Value = account.Username;
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = LegacyBlowfishPasswordCipher.Encrypt(account.Password);
        command.Parameters.Add("@Minutes", SqlDbType.Int).Value = account.MinutesBetweenFetch;
        command.Parameters.Add("@DaysToKeep", SqlDbType.Int).Value = account.DaysToKeepMessages;
        command.Parameters.Add("@ProcessMimeRecipients", SqlDbType.TinyInt).Value = account.ProcessMimeRecipients ? 1 : 0;
        command.Parameters.Add("@ProcessMimeDate", SqlDbType.TinyInt).Value = account.ProcessMimeDate ? 1 : 0;
        command.Parameters.Add("@ConnectionSecurity", SqlDbType.TinyInt).Value = account.ConnectionSecurity;
        command.Parameters.Add("@UseAntiSpam", SqlDbType.TinyInt).Value = account.UseAntiSpam ? 1 : 0;
        command.Parameters.Add("@UseAntiVirus", SqlDbType.TinyInt).Value = account.UseAntiVirus ? 1 : 0;
        command.Parameters.Add("@EnableRouteRecipients", SqlDbType.TinyInt).Value = account.EnableRouteRecipients ? 1 : 0;
        command.Parameters.Add("@MimeRecipientHeaders", SqlDbType.NVarChar, 255).Value = account.MimeRecipientHeaders;

        var generatedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(generatedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask<bool> UpdateFetchAccountAsync(
        int fetchAccountId,
        FetchAccountAdministrationDraft account,
        string? password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using var connection = await _connectionFactory!.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(UpdateFetchAccountSql, connection);
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = account.AccountId;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = account.Enabled ? 1 : 0;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = account.Name;
        command.Parameters.Add("@ServerAddress", SqlDbType.NVarChar, 255).Value = account.ServerAddress;
        command.Parameters.Add("@Port", SqlDbType.Int).Value = account.Port;
        command.Parameters.Add("@ServerType", SqlDbType.TinyInt).Value = account.ServerType;
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 255).Value = account.Username;
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = password is null
            ? DBNull.Value
            : LegacyBlowfishPasswordCipher.Encrypt(password);
        command.Parameters.Add("@Minutes", SqlDbType.Int).Value = account.MinutesBetweenFetch;
        command.Parameters.Add("@DaysToKeep", SqlDbType.Int).Value = account.DaysToKeepMessages;
        command.Parameters.Add("@ProcessMimeRecipients", SqlDbType.TinyInt).Value = account.ProcessMimeRecipients ? 1 : 0;
        command.Parameters.Add("@ProcessMimeDate", SqlDbType.TinyInt).Value = account.ProcessMimeDate ? 1 : 0;
        command.Parameters.Add("@ConnectionSecurity", SqlDbType.TinyInt).Value = account.ConnectionSecurity;
        command.Parameters.Add("@UseAntiSpam", SqlDbType.TinyInt).Value = account.UseAntiSpam ? 1 : 0;
        command.Parameters.Add("@UseAntiVirus", SqlDbType.TinyInt).Value = account.UseAntiVirus ? 1 : 0;
        command.Parameters.Add("@EnableRouteRecipients", SqlDbType.TinyInt).Value = account.EnableRouteRecipients ? 1 : 0;
        command.Parameters.Add("@MimeRecipientHeaders", SqlDbType.NVarChar, 255).Value = account.MimeRecipientHeaders;

        var affected = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(affected, CultureInfo.InvariantCulture) == 1;
    }

    public async ValueTask<int> InsertFetchAccountForRestoreAsync(
        FetchAccountAdministrationDraft account,
        string encryptedPassword,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(encryptedPassword);
        if (!LegacyBlowfishPasswordCipher.TryDecrypt(encryptedPassword, out _))
        {
            throw new InvalidDataException("The fetch-account password is not a valid legacy Blowfish value.");
        }

        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertFetchAccountSql,
            cancellationToken).ConfigureAwait(false);
        await using var command = commandLease.Command;
        command.Parameters.Add("@Active", SqlDbType.TinyInt).Value = account.Enabled ? 1 : 0;
        command.Parameters.Add("@AccountID", SqlDbType.Int).Value = account.AccountId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = account.Name;
        command.Parameters.Add("@ServerAddress", SqlDbType.NVarChar, 255).Value = account.ServerAddress;
        command.Parameters.Add("@Port", SqlDbType.Int).Value = account.Port;
        command.Parameters.Add("@ServerType", SqlDbType.TinyInt).Value = account.ServerType;
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 255).Value = account.Username;
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = encryptedPassword;
        command.Parameters.Add("@Minutes", SqlDbType.Int).Value = account.MinutesBetweenFetch;
        command.Parameters.Add("@DaysToKeep", SqlDbType.Int).Value = account.DaysToKeepMessages;
        command.Parameters.Add("@ProcessMimeRecipients", SqlDbType.TinyInt).Value = account.ProcessMimeRecipients ? 1 : 0;
        command.Parameters.Add("@ProcessMimeDate", SqlDbType.TinyInt).Value = account.ProcessMimeDate ? 1 : 0;
        command.Parameters.Add("@ConnectionSecurity", SqlDbType.TinyInt).Value = account.ConnectionSecurity;
        command.Parameters.Add("@UseAntiSpam", SqlDbType.TinyInt).Value = account.UseAntiSpam ? 1 : 0;
        command.Parameters.Add("@UseAntiVirus", SqlDbType.TinyInt).Value = account.UseAntiVirus ? 1 : 0;
        command.Parameters.Add("@EnableRouteRecipients", SqlDbType.TinyInt).Value = account.EnableRouteRecipients ? 1 : 0;
        command.Parameters.Add("@MimeRecipientHeaders", SqlDbType.NVarChar, 255).Value = account.MimeRecipientHeaders;

        var generatedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(generatedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask DeleteFetchAccountAsync(
        int accountId,
        int fetchAccountId,
        CancellationToken cancellationToken)
    {
        await using var accountCommandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            DeleteFetchAccountSql,
            cancellationToken).ConfigureAwait(false);
        await using var accountCommand = accountCommandLease.Command;
        accountCommand.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        accountCommand.Parameters.Add("@AccountID", SqlDbType.Int).Value = accountId;
        var deletedRows = await accountCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (deletedRows != 1)
        {
            return;
        }

        await using var uidCommandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            DeleteFetchAccountUidsSql,
            cancellationToken).ConfigureAwait(false);
        await using var uidCommand = uidCommandLease.Command;
        uidCommand.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        await uidCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask InsertFetchAccountUidAsync(
        int fetchAccountId,
        string uidValue,
        string uidTime,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(uidValue);
        ArgumentException.ThrowIfNullOrEmpty(uidTime);
        if (!DateTime.TryParse(uidTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormatException($"The fetch-account UID date '{uidTime}' is not a valid legacy date.");
        }

        await using var commandLease = await SqlServerCommandLease.OpenAsync(
            _connectionFactory,
            _transactionContext,
            InsertFetchAccountUidSql,
            cancellationToken).ConfigureAwait(false);
        await using var command = commandLease.Command;
        command.Parameters.Add("@FetchAccountID", SqlDbType.Int).Value = fetchAccountId;
        command.Parameters.Add("@UID", SqlDbType.NVarChar, 255).Value = uidValue;
        command.Parameters.Add("@Date", SqlDbType.DateTime).Value = date;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
