using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Pop3;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Data.SqlClient;

namespace HMailServer.Net10.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LiveExternalFetchIntegrationTests
{
    private const string EnableVariable = "HMAILSERVER_NET10_LIVE_EXTERNAL_FETCH";
    private const string ConnectionVariable = "HMAILSERVER_NET10_LIVE_SQL_CONNECTION";
    private const string DataRootVariable = "HMAILSERVER_NET10_LIVE_SQL_DATA_ROOT";
    private const int Cycles = 5;
    private const int MessagesPerCycle = 10;

    [TestMethod]
    [TestCategory("LiveExternalFetchAcceptance")]
    public async Task DisposableSqlAndTcpExternalFetch_CompletesRepeatedBatchesAndReleasesLease()
    {
        var connectionString = GetApprovedConnectionOrInconclusive();
        var dataRoot = Environment.GetEnvironmentVariable(DataRootVariable)!;
        var fixture = await ReadFixtureAccountAsync(connectionString).ConfigureAwait(false);
        await using var pop3 = new FixturePop3Server(MessagesPerCycle);
        await pop3.StartAsync().ConfigureAwait(false);

        var fetchAccountId = 0;
        var started = Stopwatch.StartNew();
        try
        {
            fetchAccountId = await InsertFetchAccountAsync(
                connectionString,
                fixture.AccountId,
                pop3.Port).ConfigureAwait(false);

            var store = new SqlServerExternalFetchAccountStore(new SqlServerConnectionFactory(connectionString));
            var endpointDecisions = new List<ExternalFetchEndpointDecision>();
            var sessionFactory = new TcpExternalFetchSessionFactory(
                new ExternalFetchPop3ClientOptions
                {
                    OperationTimeout = TimeSpan.FromSeconds(10),
                    EnforceEgressPolicy = true,
                    AllowedPrivateCidrs = ["127.0.0.0/8"]
                },
                new LoopbackResolver(),
                endpointDecisions.Add);
            var receiver = new RecordingReceiver();
            var processor = new ExternalFetchProcessor(store, sessionFactory, receiver);
            var cycleLatencies = new List<double>(Cycles);

            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var cycleWatch = Stopwatch.StartNew();
                var result = await processor.RunBatchAsync(
                    new ExternalFetchProcessorOptions(1, MessagesPerCycle),
                    CancellationToken.None).ConfigureAwait(false);
                cycleWatch.Stop();
                cycleLatencies.Add(cycleWatch.Elapsed.TotalMilliseconds);

                Assert.AreEqual(1, result.AccountsLeased, $"cycle {cycle + 1}");
                Assert.AreEqual(1, result.AccountsCompleted, $"cycle {cycle + 1}");
                Assert.AreEqual(0, result.AccountsFailed, $"cycle {cycle + 1}");
                Assert.AreEqual(MessagesPerCycle, result.MessagesDownloaded, $"cycle {cycle + 1}");
                Assert.AreEqual(MessagesPerCycle, result.MessagesAccepted, $"cycle {cycle + 1}");
                if (cycle + 1 < Cycles)
                {
                    await MakeDueAsync(connectionString, fetchAccountId).ConfigureAwait(false);
                }
            }

            var knownUids = await LoadKnownUidsAsync(connectionString, fetchAccountId).ConfigureAwait(false);
            var leaseState = await ReadLeaseStateAsync(connectionString, fetchAccountId).ConfigureAwait(false);
            Assert.AreEqual(MessagesPerCycle, knownUids);
            Assert.AreEqual(0, leaseState.Locked);
            Assert.IsTrue(leaseState.NextTry > DateTime.Now.AddSeconds(-10));
            Assert.AreEqual(Cycles * MessagesPerCycle, receiver.Requests.Count);
            Assert.IsTrue(receiver.Requests.All(static request => request.IsExternalFetch));
            Assert.AreEqual(Cycles, pop3.ConnectionCount);
            Assert.AreEqual(Cycles, endpointDecisions.Count);
            Assert.IsTrue(endpointDecisions.All(static decision => decision.IsAllowed));

            var report = new
            {
                schema = "live-net10-external-fetch-v1",
                status = "PASS",
                implementation = "net10",
                database = new SqlConnectionStringBuilder(connectionString).InitialCatalog,
                dataRoot,
                cycles = Cycles,
                messagesPerCycle = MessagesPerCycle,
                messagesDownloaded = receiver.Requests.Count,
                messagesAccepted = receiver.Requests.Count,
                knownUids,
                loopbackEndpoint = $"127.0.0.1:{pop3.Port}",
                cycleP50Ms = Percentile(cycleLatencies, 0.50),
                cycleP95Ms = Percentile(cycleLatencies, 0.95),
                cycleP99Ms = Percentile(cycleLatencies, 0.99),
                elapsedMs = started.Elapsed.TotalMilliseconds,
                egressPolicy = "127.0.0.0/8 explicitly allowed; all observed decisions allowed",
                productionSafety = "approved hmail_perf_* database and C:\\hmail-perf-* Data root only; receiver does not persist message files"
            };
            WriteReport(report);
        }
        finally
        {
            if (fetchAccountId != 0)
            {
                await DeleteFetchAccountAsync(connectionString, fetchAccountId).ConfigureAwait(false);
            }
        }
    }

    private static string GetApprovedConnectionOrInconclusive()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive($"Set {EnableVariable}=1 to run the disposable external-fetch acceptance.");
        }

        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        var dataRoot = Environment.GetEnvironmentVariable(DataRootVariable);
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(dataRoot))
        {
            Assert.Inconclusive($"{ConnectionVariable} and {DataRootVariable} are required.");
        }

        var builder = new SqlConnectionStringBuilder(connection);
        if (builder.InitialCatalog is null || !builder.InitialCatalog.StartsWith("hmail_perf_", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("External-fetch acceptance requires an hmail_perf_* database.");
        }

        var fullRoot = Path.GetFullPath(dataRoot);
        if (!fullRoot.StartsWith("C:\\hmail-perf-", StringComparison.OrdinalIgnoreCase) ||
            fullRoot.Contains("hmailserver57", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("External-fetch acceptance requires a disposable C:\\hmail-perf-* Data root.");
        }

        return connection;
    }

    private static async Task<(int AccountId, string Address)> ReadFixtureAccountAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("""
SELECT TOP (1) a.accountid, a.accountaddress
FROM hm_accounts AS a
INNER JOIN hm_domains AS d ON d.domainid = a.accountdomainid
WHERE a.accountaddress = N'test@perf.test'
  AND a.accountactive <> 0
  AND d.domainactive <> 0;
""", connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            Assert.Inconclusive("The disposable perf.test fixture account is missing.");
        }

        return (reader.GetInt32(0), reader.GetString(1));
    }

    private static async Task<int> InsertFetchAccountAsync(string connectionString, int accountId, int port)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("""
INSERT INTO hm_fetchaccounts
(
    faactive, faaccountid, faaccountname, faserveraddress, faserverport,
    faservertype, fausername, fapassword, faminutes, fanexttry, fadaystokeep,
    falocked, faprocessmimerecipients, faprocessmimedate, faconnectionsecurity,
    fauseantispam, fauseantivirus, faenablerouterecipients, famimerecipientheaders
)
OUTPUT INSERTED.faid
VALUES
(
    1, @AccountId, N'live-external-fetch', N'127.0.0.1', @Port,
    0, N'fixture-user', @Password, 1, DATEADD(minute, -1, GETDATE()), 7,
    0, 1, 1, 0, 0, 0, 0, N'To,CC,X-RCPT-TO,X-Envelope-To'
);
""", connection);
        command.Parameters.Add("@AccountId", System.Data.SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@Port", System.Data.SqlDbType.Int).Value = port;
        command.Parameters.Add("@Password", System.Data.SqlDbType.NVarChar, 255).Value = LegacyBlowfishPasswordCipher.Encrypt("fixture-password");
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task<int> LoadKnownUidsAsync(string connectionString, int fetchAccountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM hm_fetchaccounts_uids WHERE uidfaid = @Id", connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = fetchAccountId;
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task MakeDueAsync(string connectionString, int fetchAccountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(
            "UPDATE hm_fetchaccounts SET fanexttry = DATEADD(second, -1, GETDATE()) WHERE faid = @Id",
            connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = fetchAccountId;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<(int Locked, DateTime NextTry)> ReadLeaseStateAsync(string connectionString, int fetchAccountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("SELECT falocked, fanexttry FROM hm_fetchaccounts WHERE faid = @Id", connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = fetchAccountId;
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        Assert.IsTrue(await reader.ReadAsync().ConfigureAwait(false));
        return (Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetDateTime(1));
    }

    private static async Task DeleteFetchAccountAsync(string connectionString, int fetchAccountId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand("""
DELETE FROM hm_fetchaccounts_uids WHERE uidfaid = @Id;
DELETE FROM hm_fetchaccounts WHERE faid = @Id;
""", connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = fetchAccountId;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        SqlConnection.ClearPool(connection);
    }

    private static void WriteReport(object report)
    {
        var path = Environment.GetEnvironmentVariable("HMAILSERVER_NET10_LIVE_EXTERNAL_FETCH_REPORT");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        var sorted = values.OrderBy(static value => value).ToArray();
        return Math.Round(sorted[(int)Math.Floor((sorted.Length - 1) * percentile)], 3);
    }

    private sealed class LoopbackResolver : IExternalFetchAddressResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAddressesAsync(string hostName, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }

    private sealed class RecordingReceiver : ISmtpMessageReceiver
    {
        public List<SmtpReceiveRequest> Requests { get; } = [];

        public ValueTask<SmtpReceiveResult> ReceiveAsync(SmtpReceiveRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(SmtpReceiveResult.Success());
        }
    }

    private sealed class FixturePop3Server : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly int _messagesPerConnection;
        private readonly CancellationTokenSource _cancellation = new();
        private Task? _acceptTask;
        private int _connectionCount;

        public FixturePop3Server(int messagesPerConnection) => _messagesPerConnection = messagesPerConnection;

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public int ConnectionCount => Volatile.Read(ref _connectionCount);

        public Task StartAsync()
        {
            _listener.Start();
            _acceptTask = AcceptLoopAsync();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            _listener.Stop();
            if (_acceptTask is not null)
            {
                try { await _acceptTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            _cancellation.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_cancellation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                Interlocked.Increment(ref _connectionCount);
                _ = HandleClientAsync(client, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            }
        }

        private async Task HandleClientAsync(TcpClient client, string connection)
        {
            using (client)
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
                await WriteLineAsync(stream, "+OK disposable external fetch fixture").ConfigureAwait(false);
                while (true)
                {
                var command = await reader.ReadLineAsync().ConfigureAwait(false);
                if (command is null) return;
                var verb = command.Split(' ', 2)[0].ToUpperInvariant();
                switch (verb)
                {
                    case "USER": await WriteLineAsync(stream, "+OK user").ConfigureAwait(false); break;
                    case "PASS": await WriteLineAsync(stream, "+OK pass").ConfigureAwait(false); break;
                    case "UIDL":
                        await WriteLineAsync(stream, "+OK uid listing").ConfigureAwait(false);
                        for (var index = 1; index <= _messagesPerConnection; index++)
                        {
                            await WriteLineAsync(stream, $"{index} uid-{connection}-{index}").ConfigureAwait(false);
                        }
                        await WriteLineAsync(stream, ".").ConfigureAwait(false);
                        break;
                    case "RETR":
                        var sequence = command.Split(' ', 2).Length == 2 ? command.Split(' ', 2)[1] : "0";
                        await WriteLineAsync(stream, "+OK message follows").ConfigureAwait(false);
                        await WriteLineAsync(stream, $"From: sender@example.net").ConfigureAwait(false);
                        await WriteLineAsync(stream, "To: test@perf.test").ConfigureAwait(false);
                        await WriteLineAsync(stream, $"Subject: external-{connection}-{sequence}").ConfigureAwait(false);
                        await WriteLineAsync(stream, "").ConfigureAwait(false);
                        await WriteLineAsync(stream, $"Body {connection}-{sequence}").ConfigureAwait(false);
                        await WriteLineAsync(stream, ".").ConfigureAwait(false);
                        break;
                    case "QUIT": await WriteLineAsync(stream, "+OK bye").ConfigureAwait(false); return;
                    default: await WriteLineAsync(stream, "-ERR unsupported").ConfigureAwait(false); break;
                }
                }
            }
        }

        private static async Task WriteLineAsync(NetworkStream stream, string line)
        {
            var bytes = Encoding.ASCII.GetBytes(line + "\r\n");
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }
    }
}
