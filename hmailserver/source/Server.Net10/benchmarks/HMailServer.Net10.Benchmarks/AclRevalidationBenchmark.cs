using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HMailServer.Core.Abstractions;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Benchmarks;

public sealed record AclRevalidationBenchmarkReport(
    string Scenario,
    string Implementation,
    string Backend,
    string Status,
    string? Reason,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc,
    string GitCommit,
    string OsDescription,
    string RuntimeDescription,
    int WarmupIterations,
    int MeasuredIterations,
    int SuccessfulIterations,
    int ErrorCount,
    double? MinMilliseconds,
    double? MeanMilliseconds,
    double? P50Milliseconds,
    double? P95Milliseconds,
    double? P99Milliseconds,
    double? MaxMilliseconds,
    bool Correct,
    bool ThresholdPassed,
    double? P95ThresholdMilliseconds,
    IReadOnlyList<double> SamplesMilliseconds);

public static class AclRevalidationBenchmark
{
    private const string DisposableDataEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_DATA_DIRECTORY";
    private const string AllowCreateEnvironmentVariable = "HMAILSERVER_NET10_SQLSERVER_INTEGRATION_ALLOW_ISOLATED_CREATE";

    public static AclRevalidationBenchmarkReport CreateNotRunReport(string gitCommit, string reason) =>
        new(
            Scenario: "imap-acl-command-boundary-revalidation",
            Implementation: "net10",
            Backend: "offline",
            Status: "not-run",
            Reason: reason,
            StartedUtc: DateTimeOffset.UtcNow,
            EndedUtc: DateTimeOffset.UtcNow,
            GitCommit: gitCommit,
            OsDescription: System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            RuntimeDescription: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            WarmupIterations: 0,
            MeasuredIterations: 0,
            SuccessfulIterations: 0,
            ErrorCount: 0,
            MinMilliseconds: null,
            MeanMilliseconds: null,
            P50Milliseconds: null,
            P95Milliseconds: null,
            P99Milliseconds: null,
            MaxMilliseconds: null,
            Correct: false,
            ThresholdPassed: false,
            P95ThresholdMilliseconds: null,
            SamplesMilliseconds: Array.Empty<double>());

    public static async Task<AclRevalidationBenchmarkReport> RunSqlAsync(
        string connectionString,
        int warmupIterations,
        int measuredIterations,
        string gitCommit,
        CancellationToken cancellationToken = default)
    {
        if (warmupIterations < 0)
            throw new ArgumentOutOfRangeException(nameof(warmupIterations));
        if (measuredIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(measuredIterations));

        ValidateDisposableEnvironment(connectionString);
        var startedUtc = DateTimeOffset.UtcNow;
        var databaseName = "hmailserver_net10_acl_" + Guid.NewGuid().ToString("N");
        var databaseConnectionString = WithDatabase(connectionString, databaseName);
        var masterConnectionString = WithDatabase(connectionString, "master");
        var samples = new List<double>(measuredIterations);
        var errors = 0;
        var correct = true;

        try
        {
            await CreateDatabaseAsync(masterConnectionString, databaseName, cancellationToken).ConfigureAwait(false);
            await CreateSchemaAndSeedAsync(databaseConnectionString, cancellationToken).ConfigureAwait(false);
            var store = new SqlServerImapMailboxStore(new SqlServerConnectionFactory(databaseConnectionString));

            foreach (var scenario in Scenarios)
            {
                var selected = new ImapMailboxSelection(0, scenario.FolderId, scenario.Name, 0, 0, 1, 1, null, false);
                for (var iteration = 0; iteration < warmupIterations; iteration++)
                {
                    var warmupResult = await store.RevalidateSelectedMailboxAsync(42, selected, cancellationToken).ConfigureAwait(false);
                    correct &= scenario.ExpectedRead == (warmupResult is not null);
                }

                for (var iteration = 0; iteration < measuredIterations; iteration++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await store.RevalidateSelectedMailboxAsync(42, selected, cancellationToken).ConfigureAwait(false);
                    stopwatch.Stop();
                    correct &= scenario.ExpectedRead == (result is not null);
                    samples.Add(stopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }
        catch
        {
            errors++;
            correct = false;
        }
        finally
        {
            SqlConnection.ClearAllPools();
            try
            {
                await DropDatabaseAsync(masterConnectionString, databaseName, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                errors++;
                correct = false;
            }
        }

        var endedUtc = DateTimeOffset.UtcNow;
        var p50 = Percentile(samples, 0.50);
        var p95 = Percentile(samples, 0.95);
        var p99 = Percentile(samples, 0.99);
        const double p95ThresholdMilliseconds = 250;
        var completed = errors == 0 && samples.Count == Scenarios.Length * measuredIterations;
        return new AclRevalidationBenchmarkReport(
            "imap-acl-command-boundary-revalidation",
            "net10",
            "sql-localdb-disposable",
            completed ? "completed" : "failed",
            completed ? null : "One or more SQL fixture operations failed.",
            startedUtc,
            endedUtc,
            gitCommit,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            warmupIterations,
            measuredIterations,
            samples.Count,
            errors,
            samples.Count == 0 ? double.NaN : samples.Min(),
            samples.Count == 0 ? double.NaN : samples.Average(),
            p50,
            p95,
            p99,
            samples.Count == 0 ? double.NaN : samples.Max(),
            completed && correct,
            completed && correct && p95 <= p95ThresholdMilliseconds,
            p95ThresholdMilliseconds,
            samples);
    }

    private static readonly Scenario[] Scenarios =
    [
        new("direct", 10, true),
        new("group", 11, true),
        new("inherited", 12, true),
        new("denied", 13, false)
    ];

    private sealed record Scenario(string Name, int FolderId, bool ExpectedRead);

    private static void ValidateDisposableEnvironment(string connectionString)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(AllowCreateEnvironmentVariable), "1", StringComparison.Ordinal))
            throw new InvalidOperationException($"{AllowCreateEnvironmentVariable}=1 is required.");

        var dataRoot = Environment.GetEnvironmentVariable(DisposableDataEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(dataRoot) || !File.Exists(Path.Combine(dataRoot, ".net10-disposable-data-root")))
            throw new InvalidOperationException("A marked disposable Data directory is required; production Data directories are rejected.");

        var fullDataRoot = Path.GetFullPath(dataRoot);
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullDataRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullDataRoot).StartsWith("hmailserver-net10-disposable-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The disposable Data directory must be a marked hmailserver-net10-disposable-* directory under TEMP.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!builder.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
            throw new InvalidOperationException("ACL benchmark accepts only a user-owned LocalDB data source without AttachDbFilename.");
    }

    private static string WithDatabase(string connectionString, string databaseName) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName }.ConnectionString;

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateSchemaAndSeedAsync(string connectionString, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE dbo.hm_imapfolders (folderid int NOT NULL PRIMARY KEY, folderaccountid int NOT NULL, folderparentid int NOT NULL, foldername nvarchar(255) NOT NULL, folderissubscribed tinyint NOT NULL, foldercreationtime datetime NOT NULL, foldercurrentuid bigint NOT NULL);
            CREATE TABLE dbo.hm_acl (aclid bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, aclsharefolderid bigint NOT NULL, aclpermissiontype tinyint NOT NULL, aclpermissiongroupid bigint NOT NULL, aclpermissionaccountid bigint NOT NULL, aclvalue bigint NOT NULL);
            CREATE TABLE dbo.hm_group_members (membergroupid bigint NOT NULL, memberaccountid bigint NOT NULL);
            INSERT dbo.hm_imapfolders VALUES (10,0,-1,N'direct',0,GETDATE(),1),(11,0,-1,N'group',0,GETDATE(),1),(12,0,3,N'inherited',0,GETDATE(),1),(13,0,-1,N'denied',0,GETDATE(),1),(3,0,-1,N'parent',0,GETDATE(),1);
            INSERT dbo.hm_acl (aclsharefolderid,aclpermissiontype,aclpermissiongroupid,aclpermissionaccountid,aclvalue) VALUES (10,0,0,42,6),(11,1,77,0,6),(3,0,0,42,6);
            INSERT dbo.hm_group_members (membergroupid,memberaccountid) VALUES (77,42);
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
            return double.NaN;
        var ordered = values.OrderBy(static value => value).ToArray();
        var position = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper ? ordered[lower] : ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower);
    }
}

public static class AclRevalidationArtifactWriter
{
    public static void Write(AclRevalidationBenchmarkReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(outputDirectory, "acl-revalidation.json"), JsonSerializer.Serialize(report, options));
        File.WriteAllText(Path.Combine(outputDirectory, "acl-revalidation.csv"), CreateCsv(report));
        File.WriteAllText(Path.Combine(outputDirectory, "acl-revalidation.md"), CreateMarkdown(report));
    }

    private static string CreateCsv(AclRevalidationBenchmarkReport report) => string.Join(
        Environment.NewLine,
        "scenario,implementation,backend,status,git_commit,warmup_iterations,measured_iterations,successful_iterations,error_count,min_ms,mean_ms,p50_ms,p95_ms,p99_ms,max_ms,correct,threshold_passed",
        string.Join(",", Csv(report.Scenario), Csv(report.Implementation), Csv(report.Backend), Csv(report.Status), Csv(report.GitCommit), report.WarmupIterations, report.MeasuredIterations, report.SuccessfulIterations, report.ErrorCount, Number(report.MinMilliseconds), Number(report.MeanMilliseconds), Number(report.P50Milliseconds), Number(report.P95Milliseconds), Number(report.P99Milliseconds), Number(report.MaxMilliseconds), report.Correct, report.ThresholdPassed));

    private static string CreateMarkdown(AclRevalidationBenchmarkReport report) => string.Join(
        Environment.NewLine,
        "# IMAP ACL Revalidation Benchmark",
        string.Empty,
        $"- Status: `{report.Status}`",
        $"- Backend: `{report.Backend}`",
        $"- Implementation: `{report.Implementation}`",
        $"- p50 / p95 / p99: `{Number(report.P50Milliseconds)}` / `{Number(report.P95Milliseconds)}` / `{Number(report.P99Milliseconds)}` ms",
        $"- Correct: `{report.Correct}`; threshold: `{report.ThresholdPassed}`",
        $"- Reason: {report.Reason ?? "none"}",
        string.Empty,
        "This measures selected-mailbox ACL revalidation only. It is not a C++ comparison, protocol benchmark, SQL Server production forecast, or release-readiness result.");

    private static string Number(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
