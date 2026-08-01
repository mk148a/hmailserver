using System.Data;
using System.Globalization;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerRuleActionAdministrationStore : IRuleActionAdministrationStore
{
    public const string GetRuleActionsSql = """
SELECT
    actionid,
    actionruleid,
    actiontype,
    actionsubject,
    actionbody,
    actionfromname,
    actionfromaddress,
    actionfilename,
    actionto,
    actionimapfolder,
    actionscriptfunction,
    actionheader,
    actionvalue,
    actionrouteid,
    actionabortspamflagged,
    actionsortorder
FROM hm_rule_actions
WHERE actionruleid = @RuleId
ORDER BY actionsortorder ASC;
""";

    public const string DeleteRuleActionByIdSql = """
DELETE FROM hm_rule_actions
WHERE actionruleid = @RuleId
  AND actionid = @ActionId;
""";

    public const string InsertRuleActionSql = """
INSERT INTO hm_rule_actions
    (actionruleid, actiontype, actionimapfolder, actionsubject, actionfromname,
     actionfromaddress, actionto, actionbody, actionfilename, actionsortorder,
     actionscriptfunction, actionheader, actionvalue, actionrouteid, actionabortspamflagged)
OUTPUT INSERTED.actionid
VALUES
    (@RuleId, @Type, @ImapFolder, @Subject, @FromName,
     @FromAddress, @To, @Body, @Filename, @SortOrder,
     @ScriptFunction, @HeaderName, @Value, @RouteId, @AbortSpamFlagged);
""";

    public const string SaveRuleActionSql = """
UPDATE hm_rule_actions
SET actionruleid = @RuleId,
    actiontype = @Type,
    actionimapfolder = @ImapFolder,
    actionsubject = @Subject,
    actionfromname = @FromName,
    actionfromaddress = @FromAddress,
    actionto = @To,
    actionbody = @Body,
    actionfilename = @Filename,
    actionsortorder = @SortOrder,
    actionscriptfunction = @ScriptFunction,
    actionheader = @HeaderName,
    actionvalue = @Value,
    actionrouteid = @RouteId,
    actionabortspamflagged = @AbortSpamFlagged
WHERE actionruleid = @OwningRuleId
  AND actionid = @ActionId;
""";

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerRuleActionAdministrationStore(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async ValueTask<IReadOnlyList<RuleActionAdministrationSnapshot>> GetRuleActionsAsync(
        int ruleId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(GetRuleActionsSql, connection);
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken).ConfigureAwait(false);

        var actions = new List<RuleActionAdministrationSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            actions.Add(
                new RuleActionAdministrationSnapshot(
                    Id: reader.GetInt32(0),
                    RuleId: reader.GetInt32(1),
                    Type: Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Subject: reader.GetString(3),
                    Body: reader.GetString(4),
                    FromName: reader.GetString(5),
                    FromAddress: reader.GetString(6),
                    Filename: reader.GetString(7),
                    To: reader.GetString(8),
                    ImapFolder: reader.GetString(9),
                    ScriptFunction: reader.GetString(10),
                    HeaderName: reader.GetString(11),
                    Value: reader.GetString(12),
                    RouteId: reader.GetInt32(13),
                    AbortSpamFlagged: ReadLegacyBoolean(reader, 14),
                    SortOrder: reader.GetInt32(15)));
        }

        return actions;
    }

    public async ValueTask DeleteRuleActionByIdAsync(
        int ruleId,
        int databaseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(DeleteRuleActionByIdSql, connection);
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = ruleId;
        command.Parameters.Add("@ActionId", SqlDbType.Int).Value = databaseId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> InsertRuleActionAsync(
        int owningRuleId,
        RuleActionAdministrationSnapshot action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(owningRuleId);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(InsertRuleActionSql, connection);
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = owningRuleId;
        command.Parameters.Add("@Type", SqlDbType.TinyInt).Value = action.Type;
        command.Parameters.Add("@ImapFolder", SqlDbType.NVarChar, 255).Value = action.ImapFolder;
        command.Parameters.Add("@Subject", SqlDbType.NVarChar, 255).Value = action.Subject;
        command.Parameters.Add("@FromName", SqlDbType.NVarChar, 255).Value = action.FromName;
        command.Parameters.Add("@FromAddress", SqlDbType.NVarChar, 255).Value = action.FromAddress;
        command.Parameters.Add("@To", SqlDbType.NVarChar, 255).Value = action.To;
        command.Parameters.Add("@Body", SqlDbType.NVarChar, -1).Value = action.Body;
        command.Parameters.Add("@Filename", SqlDbType.NVarChar, 255).Value = action.Filename;
        command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = action.SortOrder;
        command.Parameters.Add("@ScriptFunction", SqlDbType.NVarChar, 255).Value = action.ScriptFunction;
        command.Parameters.Add("@HeaderName", SqlDbType.NVarChar, 80).Value = action.HeaderName;
        command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = action.Value;
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = action.RouteId;
        command.Parameters.Add("@AbortSpamFlagged", SqlDbType.TinyInt).Value = action.AbortSpamFlagged ? 1 : 0;
        var insertedId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(insertedId, CultureInfo.InvariantCulture);
    }

    public async ValueTask SaveRuleActionAsync(
        int owningRuleId,
        RuleActionAdministrationSnapshot action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(SaveRuleActionSql, connection);
        command.Parameters.Add("@OwningRuleId", SqlDbType.Int).Value = owningRuleId;
        command.Parameters.Add("@RuleId", SqlDbType.Int).Value = owningRuleId;
        command.Parameters.Add("@ActionId", SqlDbType.Int).Value = action.Id;
        command.Parameters.Add("@Type", SqlDbType.TinyInt).Value = action.Type;
        command.Parameters.Add("@ImapFolder", SqlDbType.NVarChar, 255).Value = action.ImapFolder;
        command.Parameters.Add("@Subject", SqlDbType.NVarChar, 255).Value = action.Subject;
        command.Parameters.Add("@FromName", SqlDbType.NVarChar, 255).Value = action.FromName;
        command.Parameters.Add("@FromAddress", SqlDbType.NVarChar, 255).Value = action.FromAddress;
        command.Parameters.Add("@To", SqlDbType.NVarChar, 255).Value = action.To;
        command.Parameters.Add("@Body", SqlDbType.NVarChar, -1).Value = action.Body;
        command.Parameters.Add("@Filename", SqlDbType.NVarChar, 255).Value = action.Filename;
        command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = action.SortOrder;
        command.Parameters.Add("@ScriptFunction", SqlDbType.NVarChar, 255).Value = action.ScriptFunction;
        command.Parameters.Add("@HeaderName", SqlDbType.NVarChar, 80).Value = action.HeaderName;
        command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = action.Value;
        command.Parameters.Add("@RouteId", SqlDbType.Int).Value = action.RouteId;
        command.Parameters.Add("@AbortSpamFlagged", SqlDbType.TinyInt).Value = action.AbortSpamFlagged ? 1 : 0;
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Saving rule action {action.Id} for owning rule {owningRuleId} affected {affectedRows} rows instead of exactly one.");
        }
    }

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
