using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class SpamAssassinConnectionTestRuntime : ISpamAssassinConnectionTestRuntime
{
    private static readonly byte[] TestMessage = Encoding.ASCII.GetBytes(
        "From: SpamAssassinTest@example.com\r\n" +
        "\r\n" +
        "XJS*C4JDBQADN1.NSBN3*2IDNEN*GTUBE-STANDARD-ANTI-UBE-TEST-EMAIL*C.34X.\r\n");

    private readonly SpamAssassinClientOptions _options;

    public SpamAssassinConnectionTestRuntime(SpamAssassinClientOptions? options = null)
    {
        _options = options ?? new SpamAssassinClientOptions();
    }

    public SpamAssassinConnectionTestResult TestConnection(
        string hostname,
        int port)
    {
        try
        {
            var client = new SpamAssassinClient(
                _options with
                {
                    Host = hostname,
                    Port = port
                });
            var result = client
                .ProcessAsync(TestMessage, string.Empty, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            return result.Succeeded
                ? new SpamAssassinConnectionTestResult(
                    true,
                    Encoding.Latin1.GetString(result.MessageData))
                : new SpamAssassinConnectionTestResult(false, result.Details);
        }
        catch (ArgumentException ex)
        {
            return new SpamAssassinConnectionTestResult(false, ex.Message);
        }
        catch (OverflowException ex)
        {
            return new SpamAssassinConnectionTestResult(false, ex.Message);
        }
    }
}
