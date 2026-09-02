using System.Security.Cryptography;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace HMailServer.Service;

internal static class LegacyUpgradeCommand
{
    public static async ValueTask<int?> TryRunAsync(
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        if (HasSwitch(arguments, "--upgrade-database"))
        {
            return await RunUpgradeAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        if (HasSwitch(arguments, "--restore-upgrade-database"))
        {
            return await RunRestoreAsync(arguments, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static async ValueTask<int> RunUpgradeAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        RequireSwitch(arguments, "--AllowOfflineDatabaseMutation");
        var backupArchive = RequireValue(arguments, "--BackupArchive");
        var upgradeScriptPath = RequireValue(arguments, "--UpgradeScriptPath");
        var upgradeReportPath = RequireValue(arguments, "--UpgradeReportPath");
        var handoffManifestPath = RequireValue(arguments, "--HandoffManifestPath");
        var targetIdentity = RequireValue(arguments, "--TargetIdentity");
        var sqlRollbackBackupPath = RequireValue(arguments, "--SqlRollbackBackupPath");
        RequireFile(backupArchive, "Verified backup archive");
        RequireFile(upgradeScriptPath, "SQL upgrade script");

        var composition = Host.Build(arguments);
        using var host = composition.Host;
        var store = host.Services.GetRequiredService<IDatabaseAdministrationStore>()
            as SqlServerDatabaseAdministrationStore
            ?? throw new InvalidOperationException(
                "The configured database administration store is not SQL Server-backed.");
        var rollbackStore = new SqlServerDatabaseRollbackStore(
            host.Services.GetRequiredService<SqlServerConnectionFactory>());
        var checkpoint = new SqlServerVerifiedBackupCheckpoint(
            Path.GetFullPath(backupArchive),
            await ComputeSha256Async(backupArchive, cancellationToken).ConfigureAwait(false),
            DateTimeOffset.UtcNow,
            targetIdentity);
        var rollbackCreated = false;

        try
        {
            await rollbackStore
                .CreateCopyOnlyBackupAsync(sqlRollbackBackupPath, cancellationToken)
                .ConfigureAwait(false);
            rollbackCreated = true;

            var runner = new SqlServerIsolatedUpgradeRunner(
                new SqlServerDatabaseMigrationExecutor(store),
                static _ => ValueTask.CompletedTask);
            var result = await runner
                .RunOfflineAsync(
                    checkpoint,
                    upgradeScriptPath,
                    upgradeReportPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var handoff = await new SqlServerUpgradeArtifactHandoff()
                .PrepareAsync(
                    checkpoint,
                    result,
                    upgradeReportPath,
                    handoffManifestPath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.Status == SqlServerUpgradeRunStatus.Completed
                && handoff.Status == SqlServerUpgradeHandoffStatus.ReadyForServiceMutation)
            {
                Console.WriteLine(
                    $"Legacy database migration completed for '{rollbackStore.DatabaseName}'. "
                    + "The handoff manifest authorizes the guarded service replacement.");
                return 0;
            }

            await RestoreAfterFailureAsync(
                rollbackStore,
                sqlRollbackBackupPath,
                rollbackCreated,
                cancellationToken).ConfigureAwait(false);
            Console.Error.WriteLine(
                $"Legacy database migration did not authorize service replacement: {result.Error ?? handoff.Manifest.RefusalReason}");
            return 1;
        }
        catch (Exception exception)
        {
            try
            {
                await RestoreAfterFailureAsync(
                    rollbackStore,
                    sqlRollbackBackupPath,
                    rollbackCreated,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception restoreException)
            {
                Console.Error.WriteLine(
                    $"Upgrade failed and SQL rollback also failed: {exception.Message} | {restoreException.Message}");
                return 1;
            }

            Console.Error.WriteLine($"Upgrade failed; SQL rollback completed: {exception.Message}");
            return 1;
        }
    }

    private static async ValueTask<int> RunRestoreAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        RequireSwitch(arguments, "--AllowOfflineDatabaseMutation");
        var sqlRollbackBackupPath = RequireValue(arguments, "--SqlRollbackBackupPath");
        var composition = Host.Build(arguments);
        using var host = composition.Host;
        var rollbackStore = new SqlServerDatabaseRollbackStore(
            host.Services.GetRequiredService<SqlServerConnectionFactory>());
        await rollbackStore
            .RestoreCopyOnlyBackupAsync(sqlRollbackBackupPath, cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"SQL rollback completed for '{rollbackStore.DatabaseName}'.");
        return 0;
    }

    private static async ValueTask RestoreAfterFailureAsync(
        SqlServerDatabaseRollbackStore rollbackStore,
        string backupPath,
        bool backupCreated,
        CancellationToken cancellationToken)
    {
        if (backupCreated)
        {
            await rollbackStore
                .RestoreCopyOnlyBackupAsync(backupPath, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{description} was not found.", path);
        }
    }

    private static bool HasSwitch(string[] arguments, string name) =>
        arguments.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static void RequireSwitch(string[] arguments, string name)
    {
        if (!HasSwitch(arguments, name))
        {
            throw new ArgumentException($"{name} is required for this mutating command.");
        }
    }

    private static string RequireValue(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                var inlineValue = argument[(name.Length + 1)..];
                if (!string.IsNullOrWhiteSpace(inlineValue))
                {
                    return inlineValue;
                }
            }

            if (string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)
                && index + 1 < arguments.Length
                && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return arguments[index + 1];
            }
        }

        throw new ArgumentException($"{name} requires a value.");
    }
}
