using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Smtp;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class SmtpAcceptLatencyIntegrationTests
{
    [TestMethod]
    [TestCategory("LiveProtocolAcceptance")]
    public async Task LoopbackAcceptLatency_ServesBannerWithinBudget()
    {
        const int clientCount = 200;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var listener = new SmtpTcpListener(
            new SmtpSession(
                new SmtpSessionOptions { ServerName = "mx.example.test" }),
            new PlainSmtpConnectionStreamFactory(),
            new SmtpTcpListenerOptions
            {
                ListenAddress = IPAddress.Loopback,
                Port = 0,
                Backlog = 32,
                MaxConcurrentConnections = 64,
                ShutdownGracePeriod = TimeSpan.FromSeconds(1)
            });
        var runTask = listener.RunAsync(cts.Token);
        var endpoint = await listener.Started.WaitAsync(cts.Token);

        var latencies = new List<double>(clientCount);
        try
        {
            for (var i = 0; i < clientCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(
                    stream,
                    Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);
                var banner = await reader.ReadLineAsync().WaitAsync(cts.Token).ConfigureAwait(false);
                stopwatch.Stop();
                Assert.AreEqual("220 hMailServer .NET 10 ESMTP ready", banner);
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            latencies.Sort();
            var p50 = Percentile(latencies, 0.50);
            var p95 = Percentile(latencies, 0.95);
            var p99 = Percentile(latencies, 0.99);
            var budget = TimeSpan.FromSeconds(5).TotalMilliseconds;

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SMTP accept latency: clients={0} p50={1:0.###}ms p95={2:0.###}ms p99={3:0.###}ms max={4:0.###}ms",
                    latencies.Count,
                    p50,
                    p95,
                    p99,
                    latencies[^1]));

            Assert.AreEqual(clientCount, latencies.Count);
            Assert.IsTrue(p95 < budget, $"SMTP accept p95 ({p95:0.###}ms) exceeded budget {budget:0}ms.");
        }
        finally
        {
            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    private static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 1)
        {
            return orderedValues[0];
        }

        var index = (int)Math.Ceiling(percentile * orderedValues.Count) - 1;
        return orderedValues[Math.Clamp(index, 0, orderedValues.Count - 1)];
    }
}