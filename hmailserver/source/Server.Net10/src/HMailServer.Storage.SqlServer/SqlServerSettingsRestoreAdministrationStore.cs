using System.Data;
using HMailServer.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace HMailServer.Storage.SqlServer;

public sealed class SqlServerSettingsRestoreAdministrationStore : ISettingsRestoreAdministrationStore
{
    public const string RestoreSettingsPropertySql = """
UPDATE hm_settings
SET settingstring = @StringValue,
    settinginteger = @LongValue
WHERE settingname = @Name
""";

    private readonly SqlServerBackupRestoreTransactionContext _context;

    internal SqlServerSettingsRestoreAdministrationStore(
        SqlServerBackupRestoreTransactionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async ValueTask RestoreSettingsPropertiesAsync(
        IReadOnlyList<BackupSettingsPropertySnapshot> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(properties);

        foreach (var property in properties)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var command = new SqlCommand(
                RestoreSettingsPropertySql,
                _context.Connection,
                _context.Transaction);
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = property.Name;
            command.Parameters.Add("@LongValue", SqlDbType.BigInt).Value = property.LongValue;
            command.Parameters.Add("@StringValue", SqlDbType.NVarChar, -1).Value = property.StringValue;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
