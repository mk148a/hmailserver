namespace HMailServer.Core.Abstractions;

public interface ISmtpSpfPolicy
{
    ValueTask<SmtpSpfPolicyResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
