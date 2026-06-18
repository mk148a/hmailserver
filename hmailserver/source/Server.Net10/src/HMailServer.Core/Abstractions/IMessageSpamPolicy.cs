namespace HMailServer.Core.Abstractions;

public interface IMessageSpamPolicy
{
    byte[] Apply(
        byte[] messageData,
        MessageSpamScanResult scanResult);
}
