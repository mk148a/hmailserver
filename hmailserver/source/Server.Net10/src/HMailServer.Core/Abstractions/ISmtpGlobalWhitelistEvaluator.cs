namespace HMailServer.Core.Abstractions;

public interface ISmtpGlobalWhitelistEvaluator
{
    ValueTask<bool> EvaluateAsync(
        string mailFrom,
        string clientIPAddress,
        CancellationToken cancellationToken);
}
