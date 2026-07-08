using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed class ClamAvScannerTestRuntime : IClamAvScannerTestRuntime
{
    private static readonly byte[] CleanTestMessage = "Test"u8.ToArray();

    private const string ReversedEicarTestString =
        " *H+H$!ELIF-TSET-SURIVITNA-DRADNATS-RACIE$}7)CC7)^P(45XZP\\4[PA@%P!O5X";

    private readonly ClamAvInstreamClientOptions _options;

    public ClamAvScannerTestRuntime(ClamAvInstreamClientOptions? options = null)
    {
        _options = options ?? new ClamAvInstreamClientOptions();
    }

    public ClamAvScannerTestResult TestConnection(
        string hostname,
        int port)
    {
        try
        {
            var client = new ClamAvInstreamClient(
                _options with
                {
                    Host = hostname,
                    Port = port
                });
            var cleanResult = client
                .ScanAsync(CleanTestMessage, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (!cleanResult.Succeeded)
            {
                return new ClamAvScannerTestResult(false, cleanResult.Details);
            }

            if (cleanResult.IsInfected)
            {
                return new ClamAvScannerTestResult(
                    false,
                    "False positive: " + cleanResult.Details);
            }

            var eicarResult = client
                .ScanAsync(CreateEicarTestMessage(), CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            return new ClamAvScannerTestResult(
                eicarResult.Succeeded && eicarResult.IsInfected,
                eicarResult.Details);
        }
        catch (ArgumentException ex)
        {
            return new ClamAvScannerTestResult(false, ex.Message);
        }
        catch (OverflowException ex)
        {
            return new ClamAvScannerTestResult(false, ex.Message);
        }
    }

    private static byte[] CreateEicarTestMessage()
    {
        var characters = ReversedEicarTestString.ToCharArray();
        Array.Reverse(characters);
        return Encoding.ASCII.GetBytes(characters);
    }
}
