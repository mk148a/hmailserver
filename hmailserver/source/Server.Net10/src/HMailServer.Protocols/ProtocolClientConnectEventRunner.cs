using HMailServer.Core.Abstractions;

namespace HMailServer.Protocols;

internal static class ProtocolClientConnectEventRunner
{
    private static readonly byte[] EmptyEventMessageData = "Subject: hMailServer event\r\n\r\n"u8.ToArray();

    public static bool Run(
        ISmtpEventScriptExecutor? eventScriptExecutor,
        string clientIPAddress,
        int clientPort,
        long sessionId,
        CancellationToken cancellationToken)
    {
        if (eventScriptExecutor is null)
        {
            return true;
        }

        SmtpRuleScriptExecutionResult result;
        try
        {
            result = eventScriptExecutor.Execute(
                new SmtpEventScriptExecutionRequest(
                    "OnClientConnect",
                    new SmtpEventScriptClient(
                        Username: string.Empty,
                        IPAddress: clientIPAddress,
                        Port: clientPort,
                        SessionId: sessionId,
                        HeloHost: string.Empty,
                        IsAuthenticated: false,
                        IsEncryptedConnection: false),
                    MailFrom: string.Empty,
                    Recipients: [],
                    EmptyEventMessageData,
                    SmtpEventScriptArgumentShape.ClientOnly),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return true;
        }

        return result.Accepted ||
            !string.Equals(result.FailureResponse, "554 Rejected", StringComparison.OrdinalIgnoreCase);
    }
}
