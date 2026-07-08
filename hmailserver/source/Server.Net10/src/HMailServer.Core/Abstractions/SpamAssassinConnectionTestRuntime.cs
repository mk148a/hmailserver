namespace HMailServer.Core.Abstractions;

public sealed record SpamAssassinConnectionTestResult(
    bool Succeeded,
    string ResultText);

public interface ISpamAssassinConnectionTestRuntime
{
    SpamAssassinConnectionTestResult TestConnection(
        string hostname,
        int port);
}
