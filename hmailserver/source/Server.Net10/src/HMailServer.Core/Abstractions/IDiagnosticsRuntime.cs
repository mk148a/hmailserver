namespace HMailServer.Core.Abstractions;

public interface IDiagnosticsRuntime
{
    ValueTask<IReadOnlyList<DiagnosticResultSnapshot>> PerformTestsAsync(
        string localDomainName,
        string testDomainName,
        CancellationToken cancellationToken);
}
