namespace HMailServer.Core.Abstractions;

public interface ISettingsAdministrationMutationStore
{
    ValueTask<bool> UpdateDefaultDomainAsync(
        string defaultDomain,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSmtpDeliveryBindToIpAsync(
        string smtpDeliveryBindToIp,
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

    ValueTask<bool> UpdateServiceSmtpAsync(
        bool serviceSmtp,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateServicePop3Async(
        bool servicePop3,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateServiceImapAsync(
        bool serviceImap,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapSortEnabledAsync(
        bool imapSortEnabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapQuotaEnabledAsync(
        bool imapQuotaEnabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapIdleEnabledAsync(
        bool imapIdleEnabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapAclEnabledAsync(
        bool imapAclEnabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapSaslPlainEnabledAsync(
        bool imapSaslPlainEnabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapSaslInitialResponseEnabledAsync(
        bool imapSaslInitialResponseEnabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapPublicFolderNameAsync(
        string imapPublicFolderName,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapMasterUserAsync(
        string imapMasterUser,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateHostNameAsync(
        string hostName,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateImapHierarchyDelimiterAsync(
        string imapHierarchyDelimiter,
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

    ValueTask<bool> UpdateIpv6PreferredAsync(
        bool ipv6Preferred,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSslCipherListAsync(
        string sslCipherList,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAutoBanOnLogonFailureAsync(
        bool autoBanOnLogonFailure,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxInvalidLogonAttemptsAsync(
        int maxInvalidLogonAttempts,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateMaxInvalidLogonAttemptsWithinAsync(
        int maxInvalidLogonAttemptsWithin,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAutoBanMinutesAsync(
        int autoBanMinutes,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateSslVersionsAsync(
        int sslVersions,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateTlsOptionsAsync(
        int tlsOptions,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiVirusClamWinEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiVirusClamWinExecutableAsync(
        string executable,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiVirusClamWinDatabaseAsync(
        string database,
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

    ValueTask<bool> UpdateAntiSpamSpamAssassinEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamSpamAssassinScoreAsync(
        int score,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamSpamAssassinMergeScoreAsync(
        bool mergeScore,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamSpamAssassinHostAsync(
        string host,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamSpamAssassinPortAsync(
        int port,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamMaximumMessageSizeAsync(
        int maximumMessageSize,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamDkimVerificationEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamDkimVerificationFailureScoreAsync(
        int score,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamBypassGreylistingOnSpfSuccessAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamBypassGreylistingOnMailFromMxAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamCheckHostInHeloAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamCheckHostInHeloScoreAsync(
        int score,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamCheckPtrAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamCheckPtrScoreAsync(
        int score,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamGreyListingEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamGreyListingInitialDelayAsync(
        int minutes,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamGreyListingInitialDeleteAsync(
        int hours,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamGreyListingFinalDeleteAsync(
        int hours,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamAddHeaderSpamAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamAddHeaderReasonAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamPrependSubjectAsync(
        bool enabled,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamPrependSubjectTextAsync(
        string text,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamSpamMarkThresholdAsync(
        int threshold,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateAntiSpamSpamDeleteThresholdAsync(
        int threshold,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateBackupDestinationAsync(
        string backupDestination,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateBackupSettingsAsync(
        bool backupSettings,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateBackupDomainsAsync(
        bool backupDomains,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateBackupMessagesAsync(
        bool backupMessages,
        CancellationToken cancellationToken);

    ValueTask<bool> UpdateBackupCompressionAsync(
        bool backupCompression,
        CancellationToken cancellationToken);
}
