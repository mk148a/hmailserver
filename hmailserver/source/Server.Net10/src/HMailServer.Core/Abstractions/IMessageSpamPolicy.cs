namespace HMailServer.Core.Abstractions;

public interface IMessageSpamPolicy
{
    MessageSpamPolicyResult Apply(
        byte[] messageData,
        MessageSpamScanResult scanResult);
}
