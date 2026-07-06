using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;
using MimeKit;
using MimeKit.Utils;

namespace HMailServer.Storage.SqlServer;

public sealed class StoreBackedEmailAllAccountsRuntime : IEmailAllAccountsRuntime
{
    private static readonly TimeSpan WildcardTimeout = TimeSpan.FromMilliseconds(250);

    private readonly IEmailAllAccountsRecipientStore _recipientStore;
    private readonly ISmtpQueueWriter _queueWriter;
    private readonly TimeProvider _timeProvider;

    public StoreBackedEmailAllAccountsRuntime(
        IEmailAllAccountsRecipientStore recipientStore,
        ISmtpQueueWriter queueWriter,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(recipientStore);
        ArgumentNullException.ThrowIfNull(queueWriter);
        _recipientStore = recipientStore;
        _queueWriter = queueWriter;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<bool> EmailAllAccountsAsync(
        string recipientWildcard,
        string fromAddress,
        string fromName,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await _recipientStore
                .GetActiveRecipientsAsync(cancellationToken)
                .ConfigureAwait(false);
            var recipients = candidates
                .Where(candidate => WildcardMatches(recipientWildcard ?? string.Empty, candidate.Address))
                .Select(static candidate => new SmtpResolvedRecipient(
                    Address: candidate.Address,
                    OriginalAddress: string.Empty,
                    LocalAccountId: candidate.AccountId,
                    IsLocal: true))
                .ToArray();
            var now = _timeProvider.GetUtcNow();
            var messageData = CreateMessageData(
                fromAddress ?? string.Empty,
                fromName ?? string.Empty,
                subject ?? string.Empty,
                body ?? string.Empty,
                _timeProvider.GetLocalNow());

            await _queueWriter
                .EnqueueAsync(
                    new SmtpQueueWriteRequest(
                        MailFrom: string.Empty,
                        Recipients: recipients,
                        MessageData: messageData,
                        ReceivedUtc: now,
                        AllowEmptyRecipients: true),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static byte[] CreateMessageData(
        string fromAddress,
        string fromName,
        string subject,
        string body,
        DateTimeOffset sentTime)
    {
        var message = new MimeMessage
        {
            Date = sentTime,
            MessageId = MimeUtils.GenerateMessageId(),
            Subject = subject,
            Body = new TextPart("plain") { Text = body }
        };
        message.Headers.Add(HeaderId.From, $"\"{fromName}\" <{fromAddress}>");

        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }

    private static bool WildcardMatches(string pattern, string value)
    {
        var expression = new StringBuilder(@"\A");
        foreach (var character in pattern)
        {
            expression.Append(
                character switch
                {
                    '*' => ".*",
                    '?' => ".",
                    _ => Regex.Escape(character.ToString())
                });
        }

        expression.Append(@"\z");
        try
        {
            return Regex.IsMatch(
                value,
                expression.ToString(),
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
                WildcardTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
