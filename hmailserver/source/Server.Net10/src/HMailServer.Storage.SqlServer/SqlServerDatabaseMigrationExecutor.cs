using System.Text.Json;
using System.Text.Json.Serialization;
using HMailServer.Core.Abstractions;

namespace HMailServer.Storage.SqlServer;

public enum SqlServerDatabaseMigrationStatus
{
    Running,
    Completed,
    RejectedCurrentVersion,
    FailedAndRolledBack,
    FailedAfterCommittedBoundary
}

public sealed record SqlServerDatabaseMigrationCheckpoint(
    int Segment,
    string Kind,
    string State,
    int CommittedSegments,
    string? Error = null);

public sealed record SqlServerDatabaseMigrationResult(
    SqlServerDatabaseMigrationStatus Status,
    int? InitialVersion,
    int? FinalVersion,
    int SegmentCount,
    int CommittedSegments,
    string? Error,
    IReadOnlyList<SqlServerDatabaseMigrationCheckpoint> Checkpoints);

public sealed class SqlServerDatabaseMigrationExecutor
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static SqlServerDatabaseMigrationExecutor()
    {
        ReportJsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly SqlServerDatabaseAdministrationStore _store;

    public SqlServerDatabaseMigrationExecutor(SqlServerDatabaseAdministrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<SqlServerDatabaseMigrationResult> Execute5708To6000Async(
        string scriptPath,
        string reportPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var checkpoints = new List<SqlServerDatabaseMigrationCheckpoint>();
        var snapshot = await _store.GetDatabaseAsync(cancellationToken).ConfigureAwait(false);
        var initialVersion = snapshot.CurrentVersion;
        if (initialVersion != 5708)
        {
            var rejected = new SqlServerDatabaseMigrationResult(
                SqlServerDatabaseMigrationStatus.RejectedCurrentVersion,
                initialVersion,
                initialVersion,
                0,
                0,
                $"Expected database version 5708, got {initialVersion?.ToString() ?? "null"}.",
                checkpoints);
            await WriteReportAsync(reportPath, rejected, cancellationToken).ConfigureAwait(false);
            return rejected;
        }

        IReadOnlyList<string> commands;
        try
        {
            commands = await SqlServerLegacySqlScript
                .ReadCommandsAsync(scriptPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var failed = new SqlServerDatabaseMigrationResult(
                SqlServerDatabaseMigrationStatus.FailedAndRolledBack,
                initialVersion,
                initialVersion,
                0,
                0,
                exception.Message,
                checkpoints);
            await WriteReportAsync(reportPath, failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }

        var segments = Partition(commands);
        var committedSegments = 0;
        await WriteReportAsync(
            reportPath,
            new SqlServerDatabaseMigrationResult(
                SqlServerDatabaseMigrationStatus.Running,
                initialVersion,
                initialVersion,
                segments.Count,
                committedSegments,
                null,
                checkpoints),
            cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var segmentNumber = index + 1;
            var kind = segment.IsFullText ? "FullText" : "Transactional";
            checkpoints.Add(
                new SqlServerDatabaseMigrationCheckpoint(
                    segmentNumber,
                    kind,
                    "Started",
                    committedSegments));

            try
            {
                var segmentPath = await WriteSegmentAsync(segment.Commands, cancellationToken).ConfigureAwait(false);
                try
                {
                    if (segment.IsFullText)
                    {
                        await _store.ExecuteScriptAsync(segmentPath, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await using var transaction = await _store
                            .BeginTransactionAsync(cancellationToken)
                            .ConfigureAwait(false);
                        try
                        {
                            await transaction.ExecuteScriptAsync(segmentPath, cancellationToken).ConfigureAwait(false);
                            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            try
                            {
                                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Preserve the original migration failure in the durable report.
                            }

                            throw;
                        }
                    }
                }
                finally
                {
                    File.Delete(segmentPath);
                }

                committedSegments++;
                checkpoints.Add(
                    new SqlServerDatabaseMigrationCheckpoint(
                        segmentNumber,
                        kind,
                        "Committed",
                        committedSegments));
                await WriteReportAsync(
                    reportPath,
                    new SqlServerDatabaseMigrationResult(
                        SqlServerDatabaseMigrationStatus.Running,
                        initialVersion,
                        null,
                        segments.Count,
                        committedSegments,
                        null,
                        checkpoints),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var failedStatus = committedSegments == 0
                    ? SqlServerDatabaseMigrationStatus.FailedAndRolledBack
                    : SqlServerDatabaseMigrationStatus.FailedAfterCommittedBoundary;
                checkpoints.Add(
                    new SqlServerDatabaseMigrationCheckpoint(
                        segmentNumber,
                        kind,
                        "Failed",
                        committedSegments,
                        exception.Message));
                var failed = new SqlServerDatabaseMigrationResult(
                    failedStatus,
                    initialVersion,
                    (await _store.GetDatabaseAsync(cancellationToken).ConfigureAwait(false)).CurrentVersion,
                    segments.Count,
                    committedSegments,
                    exception.Message,
                    checkpoints);
                await WriteReportAsync(reportPath, failed, cancellationToken).ConfigureAwait(false);
                return failed;
            }
        }

        var finalSnapshot = await _store.GetDatabaseAsync(cancellationToken).ConfigureAwait(false);
        var finalStatus = finalSnapshot.CurrentVersion == 6000
            ? SqlServerDatabaseMigrationStatus.Completed
            : SqlServerDatabaseMigrationStatus.FailedAfterCommittedBoundary;
        var result = new SqlServerDatabaseMigrationResult(
            finalStatus,
            initialVersion,
            finalSnapshot.CurrentVersion,
            segments.Count,
            committedSegments,
            finalSnapshot.CurrentVersion == 6000
                ? null
                : $"Expected database version 6000, got {finalSnapshot.CurrentVersion?.ToString() ?? "null"}.",
            checkpoints);
        await WriteReportAsync(reportPath, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static IReadOnlyList<MigrationSegment> Partition(IReadOnlyList<string> commands)
    {
        var segments = new List<MigrationSegment>();
        foreach (var command in commands)
        {
            var isFullText = SqlServerLegacySqlScript.IsFullTextCommand(command);
            if (segments.Count == 0 || segments[^1].IsFullText != isFullText)
            {
                segments.Add(new MigrationSegment(isFullText, new List<string>()));
            }

            segments[^1].Commands.Add(command);
        }

        return segments;
    }

    private static async ValueTask<string> WriteSegmentAsync(
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hmailserver-net10-migration-{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(
            path,
            string.Join("\r\n\r\n", commands),
            cancellationToken).ConfigureAwait(false);
        return path;
    }

    private static async ValueTask WriteReportAsync(
        string reportPath,
        SqlServerDatabaseMigrationResult result,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{reportPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(result, ReportJsonOptions),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, reportPath, true);
    }

    private sealed record MigrationSegment(bool IsFullText, List<string> Commands);
}
