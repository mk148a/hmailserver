using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

internal static class SqlServerLegacySqlScript
{
    public static async ValueTask<IReadOnlyList<string>> ReadCommandsAsync(
        string filename,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        var script = await File.ReadAllTextAsync(filename, cancellationToken).ConfigureAwait(false);
        if (script.Length == 0)
        {
            throw new InvalidOperationException($"Unable to read from file {filename}");
        }

        var commands = ParseCommands(script);
        if (commands.Count == 0)
        {
            throw new InvalidOperationException($"Found no SQL commands in file : {filename}");
        }

        return commands;
    }

    public static IReadOnlyList<string> ParseCommands(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return script
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.None)
            .Select(static command => command.TrimStart('\n', ' ', '\t'))
            .Where(static command => command.Length > 0)
            .ToArray();
    }

    public static bool IsFullTextCommand(string command) =>
        command.Contains("create fulltext catalog", StringComparison.OrdinalIgnoreCase)
        || command.Contains("create fulltext index", StringComparison.OrdinalIgnoreCase);

    public static async ValueTask ExecuteCommandsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(commands);

        foreach (var commandText in commands)
        {
            await using var command = new SqlCommand(commandText, connection, transaction)
            {
                CommandTimeout = 60 * 30
            };
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
