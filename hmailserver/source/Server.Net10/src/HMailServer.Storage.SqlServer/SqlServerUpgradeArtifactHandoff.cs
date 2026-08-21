using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMailServer.Storage.SqlServer;

public enum SqlServerUpgradeHandoffStatus
{
    ReadyForServiceMutation,
    Refused
}

public sealed record SqlServerUpgradeArtifactHandoffManifest(
    SqlServerUpgradeHandoffStatus Status,
    bool ServiceMutationAllowed,
    string TargetIdentity,
    string BackupArtifactPath,
    string BackupArtifactSha256,
    string UpgradeReportPath,
    string UpgradeReportSha256,
    DateTimeOffset CreatedAtUtc,
    string? RefusalReason);

public sealed record SqlServerUpgradeArtifactHandoffResult(
    SqlServerUpgradeHandoffStatus Status,
    SqlServerUpgradeArtifactHandoffManifest Manifest);

public sealed class SqlServerUpgradeArtifactHandoff
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static SqlServerUpgradeArtifactHandoff()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async ValueTask<SqlServerUpgradeArtifactHandoffResult> PrepareAsync(
        SqlServerVerifiedBackupCheckpoint? backupCheckpoint,
        SqlServerUpgradeRunResult? upgradeResult,
        string upgradeReportPath,
        string handoffManifestPath,
        CancellationToken cancellationToken)
    {
        var targetIdentity = backupCheckpoint?.TargetIdentity ?? string.Empty;
        var backupArtifactPath = backupCheckpoint?.ArtifactPath ?? string.Empty;
        var backupArtifactSha256 = backupCheckpoint?.ArtifactSha256 ?? string.Empty;
        var reportSha256 = string.Empty;
        string? refusalReason = null;

        if (backupCheckpoint is null)
        {
            refusalReason = "A verified backup checkpoint is required before service mutation.";
        }
        else if (upgradeResult is null)
        {
            refusalReason = "A completed isolated upgrade result is required before service mutation.";
        }
        else if (upgradeResult.Status != SqlServerUpgradeRunStatus.Completed)
        {
            refusalReason = $"The isolated upgrade ended with status {upgradeResult.Status}.";
        }
        else if (string.IsNullOrWhiteSpace(targetIdentity))
        {
            refusalReason = "The upgrade target identity is missing.";
        }
        else if (!File.Exists(backupArtifactPath))
        {
            refusalReason = "The verified backup artifact does not exist.";
        }
        else if (!MatchesCheckpoint(upgradeResult.BackupCheckpoint, backupCheckpoint))
        {
            refusalReason = "The upgrade result backup checkpoint does not match the handoff checkpoint.";
        }
        else if (!IsSha256(backupArtifactSha256))
        {
            refusalReason = "The verified backup checkpoint does not contain a valid SHA-256 digest.";
        }
        else if (!File.Exists(upgradeReportPath))
        {
            refusalReason = "The completed upgrade report does not exist.";
        }
        else
        {
            var actualBackupSha256 = await ComputeSha256Async(backupArtifactPath, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(actualBackupSha256, backupArtifactSha256, StringComparison.OrdinalIgnoreCase))
            {
                refusalReason = "The verified backup artifact digest does not match the handoff checkpoint.";
            }
            else
            {
                try
                {
                    using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
                        upgradeReportPath,
                        cancellationToken).ConfigureAwait(false));
                    var reportStatus = report.RootElement.GetProperty("status").GetString();
                    var migrationStatus = report.RootElement
                        .GetProperty("migration")
                        .GetProperty("status")
                        .GetString();
                    if (!string.Equals(reportStatus, nameof(SqlServerUpgradeRunStatus.Completed), StringComparison.Ordinal)
                        || !string.Equals(migrationStatus, nameof(SqlServerDatabaseMigrationStatus.Completed), StringComparison.Ordinal))
                    {
                        refusalReason = "The upgrade report does not prove completed migration and reinitialization.";
                    }
                    else
                    {
                        reportSha256 = await ComputeSha256Async(upgradeReportPath, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (JsonException)
                {
                    refusalReason = "The upgrade report is not valid JSON evidence.";
                }
                catch (KeyNotFoundException)
                {
                    refusalReason = "The upgrade report is missing required completion evidence.";
                }
            }
        }

        var status = refusalReason is null
            ? SqlServerUpgradeHandoffStatus.ReadyForServiceMutation
            : SqlServerUpgradeHandoffStatus.Refused;
        var manifest = new SqlServerUpgradeArtifactHandoffManifest(
            status,
            ServiceMutationAllowed: status == SqlServerUpgradeHandoffStatus.ReadyForServiceMutation,
            targetIdentity,
            backupArtifactPath,
            backupArtifactSha256,
            Path.GetFullPath(upgradeReportPath),
            reportSha256,
            DateTimeOffset.UtcNow,
            refusalReason);
        await WriteManifestAsync(handoffManifestPath, manifest, cancellationToken).ConfigureAwait(false);
        return new SqlServerUpgradeArtifactHandoffResult(status, manifest);
    }

    private static bool MatchesCheckpoint(
        SqlServerVerifiedBackupCheckpoint? resultCheckpoint,
        SqlServerVerifiedBackupCheckpoint checkpoint) =>
        resultCheckpoint is not null
        && string.Equals(resultCheckpoint.ArtifactPath, checkpoint.ArtifactPath, StringComparison.Ordinal)
        && string.Equals(resultCheckpoint.ArtifactSha256, checkpoint.ArtifactSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(resultCheckpoint.TargetIdentity, checkpoint.TargetIdentity, StringComparison.Ordinal);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static async ValueTask<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static async ValueTask WriteManifestAsync(
        string path,
        SqlServerUpgradeArtifactHandoffManifest manifest,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
