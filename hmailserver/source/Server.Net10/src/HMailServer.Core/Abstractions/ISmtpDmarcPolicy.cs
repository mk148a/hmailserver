namespace HMailServer.Core.Abstractions;

public interface ISmtpDmarcPolicy
{
    ValueTask<SmtpDmarcPolicyResult> CheckAsync(
        SmtpReceiveRequest request,
        SmtpSpfPolicyResult spfPolicyResult,
        SmtpDkimPolicyResult dkimPolicyResult,
        CancellationToken cancellationToken);
}
