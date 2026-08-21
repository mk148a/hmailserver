using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace HMailServer.Storage.SqlServer;

public sealed record SqlServerVerifiedBackupCheckpoint(
    string ArtifactPath,
    string ArtifactSha256,
    DateTimeOffset VerifiedAtUtc,
    string TargetIdentity);

public enum SqlServerUpgradeRunStatus
{
    Completed,
    RefusedUnverifiedBackup,
    MigrationFailed,
    ReinitializeFailed
}

public sealed record SqlServerUpgradeRunResult(
    SqlServerUpgradeRunStatus Status,
    SqlServerVerifiedBackupCheckpoint? BackupCheckpoint,
    SqlServerDatabaseMigrationResult? Migration,
    string? Error);

public sealed class SqlServerIsolatedUpgradeRunner
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SqlServerDatabaseMigrationExecutor _migrationExecutor;
    private readonly Func<CancellationToken, ValueTask> _reinitializeAsync;

    static SqlServerIsolatedUpgradeRunner()
    {
        ReportJsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public SqlServerIsolatedUpgradeRunner(
        SqlServerDatabaseMigrationExecutor migrationExecutor,
        Func<CancellationToken, ValueTask> reinitializeAsync)
    {
        ArgumentNullException.ThrowIfNull(migrationExecutor);
        ArgumentNullException.ThrowIfNull(reinitializeAsync);
        _migrationExecutor = migrationExecutor;
        _reinitializeAsync = reinitializeAsync;
    }

    public async ValueTask<SqlServerUpgradeRunResult> RunAsync(
        SqlServerVerifiedBackupCheckpoint? backupCheckpoint,
        string scriptPath,
        string reportPath,
        CancellationToken cancellationToken)
    {
        var checkpointError = await ValidateCheckpointAsync(backupCheckpoint, cancellationToken).ConfigureAwait(false);
        if (checkpointError is not null)
        {
            var refused = new SqlServerUpgradeRunResult(
                SqlServerUpgradeRunStatus.RefusedUnverifiedBackup,
                backupCheckpoint,
                null,
                checkpointError);
            await WriteReportAsync(reportPath, refused, cancellationToken).ConfigureAwait(false);
            return refused;
        }

        SqlServerDatabaseMigrationResult migration;
        try
        {
            migration = await _migrationExecutor
                .Execute5708To6000Async(scriptPath, reportPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var failed = new SqlServerUpgradeRunResult(
                SqlServerUpgradeRunStatus.MigrationFailed,
                backupCheckpoint,
                null,
                exception.Message);
            await WriteReportAsync(reportPath, failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }

        if (migration.Status != SqlServerDatabaseMigrationStatus.Completed)
        {
            var failed = new SqlServerUpgradeRunResult(
                SqlServerUpgradeRunStatus.MigrationFailed,
                backupCheckpoint,
                migration,
                migration.Error ?? $"Migration ended with status {migration.Status}.");
            await WriteReportAsync(reportPath, failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }

        try
        {
            await _reinitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var failed = new SqlServerUpgradeRunResult(
                SqlServerUpgradeRunStatus.ReinitializeFailed,
                backupCheckpoint,
                migration,
                exception.Message);
            await WriteReportAsync(reportPath, failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }

        var completed = new SqlServerUpgradeRunResult(
            SqlServerUpgradeRunStatus.Completed,
            backupCheckpoint,
            migration,
            null);
        await WriteReportAsync(reportPath, completed, cancellationToken).ConfigureAwait(false);
        return completed;
    }

    private static async ValueTask<string?> ValidateCheckpointAsync(
        SqlServerVerifiedBackupCheckpoint? checkpoint,
        CancellationToken cancellationToken)
    {
        if (checkpoint is null)
        {
            return "A verified backup checkpoint is required before database upgrade.";
        }

        if (!File.Exists(checkpoint.ArtifactPath))
        {
            return "The verified backup artifact does not exist.";
        }

        if (string.IsNullOrWhiteSpace(checkpoint.ArtifactSha256)
            || checkpoint.ArtifactSha256.Length != 64
            || checkpoint.ArtifactSha256.Any(static character => !Uri.IsHexDigit(character)))
        {
            return "The verified backup checkpoint does not contain a valid SHA-256 digest.";
        }

        if (checkpoint.VerifiedAtUtc > DateTimeOffset.UtcNow)
        {
            return "The verified backup checkpoint timestamp is in the future.";
        }

        if (string.IsNullOrWhiteSpace(checkpoint.TargetIdentity))
        {
            return "The verified backup checkpoint does not identify its target.";
        }

        await using var artifact = File.OpenRead(checkpoint.ArtifactPath);
        var actualDigest = Convert.ToHexString(
            await SHA256.HashDataAsync(artifact, cancellationToken).ConfigureAwait(false));
        if (!string.Equals(actualDigest, checkpoint.ArtifactSha256, StringComparison.OrdinalIgnoreCase))
        {
            return "The verified backup artifact digest does not match its checkpoint.";
        }

        return null;
    }

    private static async ValueTask WriteReportAsync(
        string reportPath,
        SqlServerUpgradeRunResult result,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{fullReportPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(result, ReportJsonOptions),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, fullReportPath, true);
    }
}
