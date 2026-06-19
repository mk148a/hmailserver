using System.Data;
using System.Runtime.CompilerServices;
using HMailServer.Core.Abstractions;
using HMailServer.Search.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerMessageSortIndex : IMessageSortIndex
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlServerImapSortPlanner _sortPlanner;

    public SqlServerMessageSortIndex(
        SqlServerConnectionFactory connectionFactory,
        SqlServerImapSortPlanner sortPlanner)
    {
        _connectionFactory = connectionFactory;
        _sortPlanner = sortPlanner;
    }

    public async IAsyncEnumerable<MessageIdentity> SortAsync(
        ImapSortRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = _sortPlanner.Plan(request);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(plan.CommandText, connection);

        foreach (var parameter in plan.Parameters)
        {
            AddPlanParameter(command, parameter.Key, parameter.Value);
        }

        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new MessageIdentity(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3));
        }
    }

    private static void AddPlanParameter(SqlCommand command, string name, object value)
    {
        var parameter = value switch
        {
            int typed => new SqlParameter(name, SqlDbType.Int) { Value = typed },
            long typed => new SqlParameter(name, SqlDbType.BigInt) { Value = typed },
            byte typed => new SqlParameter(name, SqlDbType.TinyInt) { Value = typed },
            DateTime typed => new SqlParameter(name, SqlDbType.DateTime2) { Value = typed },
            string typed => new SqlParameter(name, SqlDbType.NVarChar, 4000) { Value = typed },
            _ => throw new NotSupportedException($"Unsupported SQL SORT parameter type {value.GetType().FullName}.")
        };

        command.Parameters.Add(parameter);
    }
}
