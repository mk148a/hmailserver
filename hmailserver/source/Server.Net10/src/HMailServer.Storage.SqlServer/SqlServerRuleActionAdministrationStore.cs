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

    private static bool ReadLegacyBoolean(SqlDataReader reader, int ordinal) =>
        Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
}
