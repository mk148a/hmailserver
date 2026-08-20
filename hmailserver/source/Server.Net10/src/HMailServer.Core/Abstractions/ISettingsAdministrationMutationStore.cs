namespace HMailServer.Core.Abstractions;

public interface ISettingsAdministrationMutationStore
{
    ValueTask<bool> UpdateDefaultDomainAsync(
        string defaultDomain,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMirrorEmailAddressAsync(
        string mirrorEmailAddress,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAllowSmtpAuthPlainAsync(
        bool allowSmtpAuthPlain,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpRelayerRequiresAuthenticationAsync(
        bool smtpRelayerRequiresAuthentication,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpRelayerAsync(
        string smtpRelayer,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpRelayerUsernameAsync(
        string smtpRelayerUsername,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpRelayerPasswordAsync(
        string smtpRelayerPassword,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpRelayerPortAsync(
        int smtpRelayerPort,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpRelayerConnectionSecurityAsync(
        int smtpRelayerConnectionSecurity,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpConnectionSecurityAsync(
        int smtpConnectionSecurity,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAllowMailFromNullAsync(
        bool allowMailFromNull,
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

    ValueTask<bool> UpdateTcpIpThreadsAsync(
        int tcpIpThreads,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpNoOfTriesAsync(
        int smtpNoOfTries,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpMinutesBetweenTryAsync(
        int smtpMinutesBetweenTry,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxSmtpConnectionsAsync(
        int maxSmtpConnections,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxPop3ConnectionsAsync(
        int maxPop3Connections,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxImapConnectionsAsync(
        int maxImapConnections,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxDeliveryThreadsAsync(
        int maxDeliveryThreads,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxAsynchronousThreadsAsync(
        int maxAsynchronousThreads,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxMessageSizeAsync(
        int maxMessageSize,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateRuleLoopLimitAsync(
        int ruleLoopLimit,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxSmtpRecipientsInBatchAsync(
        int maxSmtpRecipientsInBatch,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateDisconnectInvalidClientsAsync(
        bool disconnectInvalidClients,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAddDeliveredToHeaderAsync(
        bool addDeliveredToHeader,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAllowIncorrectLineEndingsAsync(
        bool allowIncorrectLineEndings,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxNumberOfInvalidCommandsAsync(
        int maxNumberOfInvalidCommands,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxNumberOfMXHostsAsync(
        int maxNumberOfMXHosts,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateVerifyRemoteSslCertificateAsync(
        bool verifyRemoteSslCertificate,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamUseSpfAsync(
        bool useSpf,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamUseSpfScoreAsync(
        int useSpfScore,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamUseMxChecksAsync(
        bool useMxChecks,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamUseMxChecksScoreAsync(
        int useMxChecksScore,
        CancellationToken cancellationToken);
}
