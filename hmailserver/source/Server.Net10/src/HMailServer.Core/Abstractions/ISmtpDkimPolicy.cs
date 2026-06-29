namespace HMailServer.Core.Abstractions;

public interface ISmtpDkimPolicy
{
    ValueTask<SmtpDkimPolicyResult> CheckAsync(
        SmtpReceiveRequest request,
        CancellationToken cancellationToken);
}
