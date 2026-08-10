namespace HMailServer.Core.Abstractions;

public interface ISettingsAdministrationMutationStore
{
    ValueTask<bool> UpdateDefaultDomainAsync(
        string defaultDomain,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMirrorEmailAddressAsync(
        string mirrorEmailAddress,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateWelcomePop3Async(
        string welcomePop3,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateWelcomeSmtpAsync(
        string welcomeSmtp,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateWelcomeImapAsync(
        string welcomeImap,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateWorkerThreadPriorityAsync(
        int workerThreadPriority,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxSmtpConnectionsAsync(
        int maxSmtpConnections,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxPop3ConnectionsAsync(
        int maxPop3Connections,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxSmtpRecipientsInBatchAsync(
        int maxSmtpRecipientsInBatch,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxNumberOfInvalidCommandsAsync(
        int maxNumberOfInvalidCommands,
        CancellationToken cancellationToken);
}
