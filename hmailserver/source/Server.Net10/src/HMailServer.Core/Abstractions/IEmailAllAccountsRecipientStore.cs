namespace HMailServer.Core.Abstractions;

public interface IEmailAllAccountsRecipientStore
{
    ValueTask<IReadOnlyList<EmailAllAccountsRecipient>> GetActiveRecipientsAsync(
        CancellationToken cancellationToken);
}

public sealed record EmailAllAccountsRecipient(int AccountId, string Address);
