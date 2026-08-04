using System.Net;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Indexing;
using HMailServer.Protocols;
using HMailServer.Protocols.Imap;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;
using HMailServer.Scripting;
using HMailServer.Search.SqlServer;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace HMailServer.Service;

public static class Host
{
    public static HostBuildResult Build(string[] args)
    {
    var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(options => options.ServiceName = "hMailServer");

    var initializationFile = LegacyInitializationFile.ResolvePath(
        builder.Configuration["InitializationFile"]
            ?? builder.Configuration["HMAILSERVER_INITIALIZATION_FILE"],
        AppContext.BaseDirectory);
    var administratorPasswordHash = LegacyInitializationFile.LoadAdministratorPasswordHash(initializationFile);
    var databaseConfiguration = LegacyInitializationFile.LoadDatabaseConfiguration(initializationFile);
    var userInterfaceLanguage = LegacyInitializationFile.LoadUserInterfaceLanguage(initializationFile);
    var rewriteEnvelopeFromWhenForwarding =
        LegacyInitializationFile.LoadRewriteEnvelopeFromWhenForwarding(initializationFile);
    var backupMessagesDbOnly = LegacyInitializationFile.LoadBackupMessagesDbOnly(initializationFile);
    var applicationVersion = builder.Configuration["Application:Version"]
        ?? builder.Configuration["HMAILSERVER_VERSION"]
        ?? "1.0.0-B0";
    builder.Services.AddSingleton<IServerAdministratorAuthenticationProvider>(
        new LegacyServerAdministratorAuthenticationProvider(administratorPasswordHash));
    builder.Services.AddSingleton<ILoggerProvider, LoggingLiveLogLoggerProvider>();
    builder.Services.AddSingleton<BackupTaskQueue>();
    builder.Services.AddSingleton<IBackupTaskQueue>(
        serviceProvider => serviceProvider.GetRequiredService<BackupTaskQueue>());

    var connectionString = builder.Configuration["ConnectionStrings:hMailServer"]
        ?? builder.Configuration["HMAILSERVER_SQLSERVER_CONNECTION"]
        ?? throw new InvalidOperationException("Missing SQL Server connection string.");

    var dataDirectory = builder.Configuration["DataDirectory"]
        ?? builder.Configuration["HMAILSERVER_DATA_DIRECTORY"]
        ?? throw new InvalidOperationException("Missing hMailServer data directory.");

    var leaseOwner = $"{Environment.MachineName}-{Environment.ProcessId}";
    var deliveryStatusSqlEnabled = ReadBool(
        builder.Configuration["DeliveryQueue:StatusSqlEnabled"] ?? builder.Configuration["HMAILSERVER_DELIVERY_STATUS_SQL_ENABLED"],
        defaultValue: false);
    var deliveryStatusRetentionDays = Math.Max(
        0,
        ReadInt(
            builder.Configuration["DeliveryQueue:StatusRetentionDays"] ?? builder.Configuration["HMAILSERVER_DELIVERY_STATUS_RETENTION_DAYS"],
            defaultValue: 30));
    var deliveryStatusCleanupIntervalMinutes = Math.Max(
        1,
        ReadInt(
            builder.Configuration["DeliveryQueue:StatusCleanupIntervalMinutes"]
                ?? builder.Configuration["HMAILSERVER_DELIVERY_STATUS_CLEANUP_INTERVAL_MINUTES"],
            defaultValue: 60));
    var deliveryStatusCleanupBatchSize = Math.Max(
        1,
        ReadInt(
            builder.Configuration["DeliveryQueue:StatusCleanupBatchSize"] ?? builder.Configuration["HMAILSERVER_DELIVERY_STATUS_CLEANUP_BATCH_SIZE"],
            defaultValue: 5000));
    var deliveryStatusMaintenanceOptions = deliveryStatusSqlEnabled && deliveryStatusRetentionDays > 0
        ? new DeliveryQueueStatusMaintenanceOptions(
            Enabled: true,
            Retention: TimeSpan.FromDays(deliveryStatusRetentionDays),
            CleanupInterval: TimeSpan.FromMinutes(deliveryStatusCleanupIntervalMinutes),
            BatchSize: deliveryStatusCleanupBatchSize)
        : DeliveryQueueStatusMaintenanceOptions.Disabled;
    var imapOptions = new ImapTcpListenerOptions
    {
        Enabled = ReadBool(builder.Configuration["Imap:Enabled"] ?? builder.Configuration["HMAILSERVER_IMAP_ENABLED"], defaultValue: false),
        ListenAddress = IPAddress.Parse(builder.Configuration["Imap:BindAddress"] ?? builder.Configuration["HMAILSERVER_IMAP_BIND_ADDRESS"] ?? "0.0.0.0"),
        Port = ReadInt(builder.Configuration["Imap:Port"] ?? builder.Configuration["HMAILSERVER_IMAP_PORT"], defaultValue: 143),
        Backlog = ReadInt(builder.Configuration["Imap:Backlog"] ?? builder.Configuration["HMAILSERVER_IMAP_BACKLOG"], defaultValue: 512),
        MaxConcurrentConnections = ReadInt(
            builder.Configuration["Imap:MaxConcurrentConnections"] ?? builder.Configuration["HMAILSERVER_IMAP_MAX_CONNECTIONS"],
            defaultValue: 1000)
    };
    var smtpOptions = new SmtpTcpListenerOptions
    {
        Enabled = ReadBool(builder.Configuration["Smtp:Enabled"] ?? builder.Configuration["HMAILSERVER_SMTP_ENABLED"], defaultValue: false),
        ListenAddress = IPAddress.Parse(builder.Configuration["Smtp:BindAddress"] ?? builder.Configuration["HMAILSERVER_SMTP_BIND_ADDRESS"] ?? "0.0.0.0"),
        Port = ReadInt(builder.Configuration["Smtp:Port"] ?? builder.Configuration["HMAILSERVER_SMTP_PORT"], defaultValue: 25),
        Backlog = ReadInt(builder.Configuration["Smtp:Backlog"] ?? builder.Configuration["HMAILSERVER_SMTP_BACKLOG"], defaultValue: 512),
        MaxConcurrentConnections = ReadInt(
            builder.Configuration["Smtp:MaxConcurrentConnections"] ?? builder.Configuration["HMAILSERVER_SMTP_MAX_CONNECTIONS"],
            defaultValue: 1000)
    };
    var pop3Options = new Pop3TcpListenerOptions
    {
        Enabled = ReadBool(builder.Configuration["Pop3:Enabled"] ?? builder.Configuration["HMAILSERVER_POP3_ENABLED"], defaultValue: false),
        ListenAddress = IPAddress.Parse(builder.Configuration["Pop3:BindAddress"] ?? builder.Configuration["HMAILSERVER_POP3_BIND_ADDRESS"] ?? "0.0.0.0"),
        Port = ReadInt(builder.Configuration["Pop3:Port"] ?? builder.Configuration["HMAILSERVER_POP3_PORT"], defaultValue: 110),
        Backlog = ReadInt(builder.Configuration["Pop3:Backlog"] ?? builder.Configuration["HMAILSERVER_POP3_BACKLOG"], defaultValue: 512),
        MaxConcurrentConnections = ReadInt(
            builder.Configuration["Pop3:MaxConcurrentConnections"] ?? builder.Configuration["HMAILSERVER_POP3_MAX_CONNECTIONS"],
            defaultValue: 1000)
    };
    var externalFetchEnabled = ReadBool(
        builder.Configuration["ExternalFetch:Enabled"] ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_ENABLED"],
        defaultValue: true);
    var externalFetchProcessorOptions = new ExternalFetchProcessorOptions(
        BatchSize: Math.Max(
            1,
            ReadInt(
                builder.Configuration["ExternalFetch:BatchSize"] ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_BATCH_SIZE"],
                defaultValue: 10)),
        MaxMessagesPerAccount: Math.Max(
            1,
            ReadInt(
                builder.Configuration["ExternalFetch:MaxMessagesPerAccount"]
                    ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_MAX_MESSAGES_PER_ACCOUNT"],
                defaultValue: 100)));
    var externalFetchHostedServiceOptions = new ExternalFetchHostedServiceOptions(
        PollInterval: TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["ExternalFetch:PollIntervalSeconds"]
                        ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_POLL_INTERVAL_SECONDS"],
                    defaultValue: 30))));
    var externalFetchPop3ClientOptions = new ExternalFetchPop3ClientOptions
    {
        ReceiveBufferBytes = Math.Max(
            1024,
            ReadInt(
                builder.Configuration["ExternalFetch:ReceiveBufferBytes"]
                    ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_RECEIVE_BUFFER_BYTES"],
                defaultValue: 64 * 1024)),
        SendBufferBytes = Math.Max(
            1024,
            ReadInt(
                builder.Configuration["ExternalFetch:SendBufferBytes"]
                    ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_SEND_BUFFER_BYTES"],
                defaultValue: 64 * 1024)),
        NoDelay = ReadBool(
            builder.Configuration["ExternalFetch:NoDelay"] ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_NO_DELAY"],
            defaultValue: true),
        EnforceEgressPolicy = ReadBool(
            builder.Configuration["ExternalFetch:EgressEnforce"] ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_EGRESS_ENFORCE"],
            defaultValue: false),
        AllowedPrivateCidrs = ReadList(
            builder.Configuration["ExternalFetch:AllowedPrivateCidrs"] ?? builder.Configuration["HMAILSERVER_EXTERNAL_FETCH_ALLOWED_PRIVATE_CIDRS"])
    };
    var clamAvEnabled = ReadBool(
        builder.Configuration["Antivirus:ClamAv:Enabled"] ?? builder.Configuration["HMAILSERVER_CLAMAV_ENABLED"],
        defaultValue: false);
    var clamAvOptions = new ClamAvInstreamClientOptions
    {
        Host = builder.Configuration["Antivirus:ClamAv:Host"]
            ?? builder.Configuration["HMAILSERVER_CLAMAV_HOST"]
            ?? "127.0.0.1",
        Port = Math.Max(
            1,
            ReadInt(
                builder.Configuration["Antivirus:ClamAv:Port"] ?? builder.Configuration["HMAILSERVER_CLAMAV_PORT"],
                defaultValue: 3310)),
        Timeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["Antivirus:ClamAv:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_CLAMAV_TIMEOUT_SECONDS"],
                    defaultValue: 30))),
        ChunkSize = Math.Max(
            1024,
            ReadInt(
                builder.Configuration["Antivirus:ClamAv:ChunkSizeBytes"]
                    ?? builder.Configuration["HMAILSERVER_CLAMAV_CHUNK_SIZE_BYTES"],
                defaultValue: 64 * 1024))
    };
    var spamAssassinEnabled = ReadBool(
        builder.Configuration["AntiSpam:SpamAssassin:Enabled"] ?? builder.Configuration["HMAILSERVER_SPAMASSASSIN_ENABLED"],
        defaultValue: false);
    var spamAssassinOptions = new SpamAssassinClientOptions
    {
        Host = builder.Configuration["AntiSpam:SpamAssassin:Host"]
            ?? builder.Configuration["HMAILSERVER_SPAMASSASSIN_HOST"]
            ?? "127.0.0.1",
        Port = Math.Max(
            1,
            ReadInt(
                builder.Configuration["AntiSpam:SpamAssassin:Port"] ?? builder.Configuration["HMAILSERVER_SPAMASSASSIN_PORT"],
                defaultValue: 783)),
        Timeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiSpam:SpamAssassin:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_SPAMASSASSIN_TIMEOUT_SECONDS"],
                    defaultValue: 30))),
        MaxResponseHeaderBytes = Math.Max(
            1024,
            ReadInt(
                builder.Configuration["AntiSpam:SpamAssassin:MaxResponseHeaderBytes"]
                    ?? builder.Configuration["HMAILSERVER_SPAMASSASSIN_MAX_RESPONSE_HEADER_BYTES"],
                defaultValue: 16 * 1024)),
        MaxResponseBytes = Math.Max(
            1024,
            ReadInt(
                builder.Configuration["AntiSpam:SpamAssassin:MaxResponseBytes"]
                    ?? builder.Configuration["HMAILSERVER_SPAMASSASSIN_MAX_RESPONSE_BYTES"],
                defaultValue: 100 * 1024 * 1024))
    };
    var spamPolicyOptions = new MessageSpamPolicyOptions
    {
        AddSpamHeader = ReadBool(
            builder.Configuration["AntiSpam:Policy:AddSpamHeader"]
                ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_ADD_HEADER_SPAM"],
            defaultValue: false),
        AddReasonHeaders = ReadBool(
            builder.Configuration["AntiSpam:Policy:AddReasonHeaders"]
                ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_ADD_REASON_HEADERS"],
            defaultValue: false),
        PrependSubject = ReadBool(
            builder.Configuration["AntiSpam:Policy:PrependSubject"]
                ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_PREPEND_SUBJECT"],
            defaultValue: false),
        SpamMarkThreshold = Math.Max(
            0,
            ReadInt(
                builder.Configuration["AntiSpam:Policy:SpamMarkThreshold"]
                    ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_MARK_THRESHOLD"],
                defaultValue: 0)),
        SpamDeleteThreshold = Math.Max(
            0,
            ReadInt(
                builder.Configuration["AntiSpam:Policy:SpamDeleteThreshold"]
                    ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_DELETE_THRESHOLD"],
                defaultValue: 0)),
        SubjectPrefix = builder.Configuration["AntiSpam:Policy:SubjectPrefix"]
            ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_SUBJECT_PREFIX"]
            ?? "[SPAM]",
        MaxHeaderValueLength = Math.Max(
            64,
            ReadInt(
                builder.Configuration["AntiSpam:Policy:MaxHeaderValueLength"]
                    ?? builder.Configuration["HMAILSERVER_SPAM_POLICY_MAX_HEADER_VALUE_LENGTH"],
                defaultValue: 900))
    };
    var spamPolicyEnabled = spamPolicyOptions.AddSpamHeader
        || spamPolicyOptions.AddReasonHeaders
        || spamPolicyOptions.PrependSubject
        || spamPolicyOptions.SpamMarkThreshold > 0
        || spamPolicyOptions.SpamDeleteThreshold > 0;
    var spfPolicyOptions = new SmtpSpfPolicyOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiSpam:Spf:Enabled"]
                ?? builder.Configuration["HMAILSERVER_SPF_ENABLED"],
            defaultValue: false),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiSpam:Spf:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_SPF_SKIP_AUTHENTICATED"],
            defaultValue: true),
        FailScore = Math.Max(
            0,
            ReadInt(
                builder.Configuration["AntiSpam:Spf:FailScore"]
                    ?? builder.Configuration["HMAILSERVER_SPF_FAIL_SCORE"],
                defaultValue: 3))
    };
    var spfEvaluatorOptions = new SpfEvaluatorOptions
    {
        EvaluationTimeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiSpam:Spf:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_SPF_TIMEOUT_SECONDS"],
                    defaultValue: 20)))
    };
    var spfPolicyEnabled = spfPolicyOptions.Enabled;
    var dkimPolicyOptions = new SmtpDkimPolicyOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiSpam:Dkim:Enabled"]
                ?? builder.Configuration["HMAILSERVER_DKIM_ENABLED"],
            defaultValue: false),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiSpam:Dkim:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_DKIM_SKIP_AUTHENTICATED"],
            defaultValue: true),
        FailureScore = Math.Max(
            0,
            ReadInt(
                builder.Configuration["AntiSpam:Dkim:FailureScore"]
                    ?? builder.Configuration["HMAILSERVER_DKIM_FAILURE_SCORE"],
                defaultValue: 5))
    };
    var dkimPolicyEnabled = dkimPolicyOptions.Enabled;
    var dmarcPolicyOptions = new SmtpDmarcPolicyOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiSpam:Dmarc:Enabled"]
                ?? builder.Configuration["HMAILSERVER_DMARC_ENABLED"],
            defaultValue: false),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiSpam:Dmarc:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_DMARC_SKIP_AUTHENTICATED"],
            defaultValue: true),
        MarkPolicyFailuresAsSpam = ReadBool(
            builder.Configuration["AntiSpam:Dmarc:MarkPolicyFailuresAsSpam"]
                ?? builder.Configuration["HMAILSERVER_DMARC_MARK_FAILURES_AS_SPAM"],
            defaultValue: false),
        FailureScore = Math.Max(
            0,
            ReadInt(
                builder.Configuration["AntiSpam:Dmarc:FailureScore"]
                    ?? builder.Configuration["HMAILSERVER_DMARC_FAILURE_SCORE"],
                defaultValue: 5))
    };
    var dmarcPolicyEnabled = dmarcPolicyOptions.Enabled;
    var configuredDmarcPublicSuffixListPath =
        builder.Configuration["AntiSpam:Dmarc:PublicSuffixListPath"]
        ?? builder.Configuration["HMAILSERVER_DMARC_PUBLIC_SUFFIX_LIST"];
    var dmarcPublicSuffixListPath = ResolveOptionalPath(
        configuredDmarcPublicSuffixListPath,
        "public_suffix_list.dat");
    var dmarcOrganizationalDomainResolverEnabled = dmarcPolicyEnabled
        && dmarcPublicSuffixListPath is not null
        && (!string.IsNullOrWhiteSpace(configuredDmarcPublicSuffixListPath)
            || File.Exists(dmarcPublicSuffixListPath));
    var attachmentPolicyOptions = new MessageAttachmentPolicyOptions
    {
        Enabled = ReadBool(
            builder.Configuration["Antivirus:AttachmentBlocking:Enabled"]
                ?? builder.Configuration["HMAILSERVER_ATTACHMENT_BLOCKING_ENABLED"],
            defaultValue: false),
        BlockedWildcards = ReadList(
            builder.Configuration["Antivirus:AttachmentBlocking:Wildcards"]
                ?? builder.Configuration["HMAILSERVER_ATTACHMENT_BLOCKING_WILDCARDS"]),
        ReplacementTextTemplate = builder.Configuration["Antivirus:AttachmentBlocking:ReplacementTextTemplate"]
            ?? builder.Configuration["HMAILSERVER_ATTACHMENT_BLOCKING_REPLACEMENT_TEXT"]
            ?? "The attachment %MACRO_FILE% was removed because it matched an attachment blocking rule."
    };
    var attachmentPolicyEnabled = attachmentPolicyOptions.Enabled
        && attachmentPolicyOptions.BlockedWildcards.Count > 0;
    var dnsBlockListOptions = new SmtpDnsBlockListOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiAbuse:DnsBlockList:Enabled"]
                ?? builder.Configuration["HMAILSERVER_DNSBL_ENABLED"],
            defaultValue: false),
        Zones = ReadList(
            builder.Configuration["AntiAbuse:DnsBlockList:Zones"]
                ?? builder.Configuration["HMAILSERVER_DNSBL_ZONES"]),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiAbuse:DnsBlockList:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_DNSBL_SKIP_AUTHENTICATED"],
            defaultValue: true),
        Timeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiAbuse:DnsBlockList:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_DNSBL_TIMEOUT_SECONDS"],
                    defaultValue: 5))),
        RejectionMessageTemplate = builder.Configuration["AntiAbuse:DnsBlockList:RejectionMessageTemplate"]
            ?? builder.Configuration["HMAILSERVER_DNSBL_REJECTION_MESSAGE"]
            ?? "554 Rejected by DNS blocklist {ListHost}"
    };
    var dnsBlockListEnabled = dnsBlockListOptions.Enabled
        && dnsBlockListOptions.Zones.Count > 0;
    var reverseDnsOptions = new SmtpReverseDnsCheckOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiAbuse:ReverseDns:Enabled"]
                ?? builder.Configuration["HMAILSERVER_REVERSE_DNS_ENABLED"],
            defaultValue: false),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiAbuse:ReverseDns:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_REVERSE_DNS_SKIP_AUTHENTICATED"],
            defaultValue: true),
        RequireForwardConfirmed = ReadBool(
            builder.Configuration["AntiAbuse:ReverseDns:RequireForwardConfirmed"]
                ?? builder.Configuration["HMAILSERVER_REVERSE_DNS_REQUIRE_FORWARD_CONFIRMED"],
            defaultValue: true),
        Timeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiAbuse:ReverseDns:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_REVERSE_DNS_TIMEOUT_SECONDS"],
                    defaultValue: 5))),
        RejectionMessageTemplate = builder.Configuration["AntiAbuse:ReverseDns:RejectionMessageTemplate"]
            ?? builder.Configuration["HMAILSERVER_REVERSE_DNS_REJECTION_MESSAGE"]
            ?? "554 Rejected by reverse DNS check {Reason}"
    };
    var reverseDnsEnabled = reverseDnsOptions.Enabled;
    var senderDomainMxOptions = new SmtpSenderDomainMxCheckOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiAbuse:SenderDomainMx:Enabled"]
                ?? builder.Configuration["HMAILSERVER_SENDER_DOMAIN_MX_ENABLED"],
            defaultValue: false),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiAbuse:SenderDomainMx:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_SENDER_DOMAIN_MX_SKIP_AUTHENTICATED"],
            defaultValue: true),
        Timeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiAbuse:SenderDomainMx:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_SENDER_DOMAIN_MX_TIMEOUT_SECONDS"],
                    defaultValue: 5))),
        RejectionMessageTemplate = builder.Configuration["AntiAbuse:SenderDomainMx:RejectionMessageTemplate"]
            ?? builder.Configuration["HMAILSERVER_SENDER_DOMAIN_MX_REJECTION_MESSAGE"]
            ?? "554 Sender domain does not have any MX records"
    };
    var senderDomainMxEnabled = senderDomainMxOptions.Enabled;
    var greylistingOptions = new SmtpGreylistingOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiAbuse:Greylisting:Enabled"]
                ?? builder.Configuration["HMAILSERVER_GREYLISTING_ENABLED"],
            defaultValue: false),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiAbuse:Greylisting:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_GREYLISTING_SKIP_AUTHENTICATED"],
            defaultValue: true),
        BypassOnSpfPass = ReadBool(
            builder.Configuration["AntiAbuse:Greylisting:BypassOnSpfPass"]
                ?? builder.Configuration["HMAILSERVER_GREYLISTING_BYPASS_ON_SPF_PASS"],
            defaultValue: false),
        InitialDelay = TimeSpan.FromMinutes(
            Math.Max(
                0,
                ReadInt(
                    builder.Configuration["AntiAbuse:Greylisting:InitialDelayMinutes"]
                        ?? builder.Configuration["HMAILSERVER_GREYLISTING_INITIAL_DELAY_MINUTES"],
                    defaultValue: 30))),
        InitialRecordLifetime = TimeSpan.FromHours(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiAbuse:Greylisting:InitialRecordLifetimeHours"]
                        ?? builder.Configuration["HMAILSERVER_GREYLISTING_INITIAL_RECORD_LIFETIME_HOURS"],
                    defaultValue: 24))),
        PassedRecordLifetime = TimeSpan.FromHours(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiAbuse:Greylisting:PassedRecordLifetimeHours"]
                        ?? builder.Configuration["HMAILSERVER_GREYLISTING_PASSED_RECORD_LIFETIME_HOURS"],
                    defaultValue: 864))),
        FailureResponse = builder.Configuration["AntiAbuse:Greylisting:FailureResponse"]
            ?? builder.Configuration["HMAILSERVER_GREYLISTING_FAILURE_RESPONSE"]
            ?? "451 Please try again later."
    };
    var greylistingEnabled = greylistingOptions.Enabled;
    var urlBlockListOptions = new SmtpUrlBlockListOptions
    {
        Enabled = ReadBool(
            builder.Configuration["AntiAbuse:UrlBlockList:Enabled"]
                ?? builder.Configuration["HMAILSERVER_SURBL_ENABLED"],
            defaultValue: false),
        Zones = ReadList(
            builder.Configuration["AntiAbuse:UrlBlockList:Zones"]
                ?? builder.Configuration["HMAILSERVER_SURBL_ZONES"]),
        SkipAuthenticated = ReadBool(
            builder.Configuration["AntiAbuse:UrlBlockList:SkipAuthenticated"]
                ?? builder.Configuration["HMAILSERVER_SURBL_SKIP_AUTHENTICATED"],
            defaultValue: true),
        Timeout = TimeSpan.FromSeconds(
            Math.Max(
                1,
                ReadInt(
                    builder.Configuration["AntiAbuse:UrlBlockList:TimeoutSeconds"]
                        ?? builder.Configuration["HMAILSERVER_SURBL_TIMEOUT_SECONDS"],
                    defaultValue: 5))),
        MaxHosts = Math.Max(
            1,
            ReadInt(
                builder.Configuration["AntiAbuse:UrlBlockList:MaxHosts"]
                    ?? builder.Configuration["HMAILSERVER_SURBL_MAX_HOSTS"],
                defaultValue: 50)),
        MaxCandidateDomainsPerHost = Math.Max(
            1,
            ReadInt(
                builder.Configuration["AntiAbuse:UrlBlockList:MaxCandidateDomainsPerHost"]
                    ?? builder.Configuration["HMAILSERVER_SURBL_MAX_CANDIDATE_DOMAINS_PER_HOST"],
                defaultValue: 3)),
        RejectionMessageTemplate = builder.Configuration["AntiAbuse:UrlBlockList:RejectionMessageTemplate"]
            ?? builder.Configuration["HMAILSERVER_SURBL_REJECTION_MESSAGE"]
            ?? "554 Rejected by URL blocklist {ListHost}"
    };
    var urlBlockListEnabled = urlBlockListOptions.Enabled
        && urlBlockListOptions.Zones.Count > 0;
    var smtpSessionOptions = new SmtpSessionOptions
    {
        ServerName = builder.Configuration["Smtp:ServerName"]
            ?? builder.Configuration["HMAILSERVER_SMTP_SERVER_NAME"]
            ?? Environment.MachineName,
        MaxMessageBytes = ReadLong(
            builder.Configuration["Smtp:MaxMessageBytes"] ?? builder.Configuration["HMAILSERVER_SMTP_MAX_MESSAGE_BYTES"],
            defaultValue: 20L * 1024 * 1024),
        RequireTlsForAuthentication = ReadBool(
            builder.Configuration["Smtp:RequireTlsForAuthentication"] ?? builder.Configuration["HMAILSERVER_SMTP_REQUIRE_TLS_FOR_AUTH"],
            defaultValue: false),
        DisconnectInvalidClients = ReadBool(
            builder.Configuration["Smtp:DisconnectInvalidClients"] ?? builder.Configuration["HMAILSERVER_SMTP_DISCONNECT_INVALID_CLIENTS"],
            defaultValue: false),
        MaximumIncorrectCommands = ReadInt(
            builder.Configuration["Smtp:MaximumIncorrectCommands"] ?? builder.Configuration["HMAILSERVER_SMTP_MAXIMUM_INCORRECT_COMMANDS"],
            defaultValue: 100)
    };
    var smtpTlsCertificate = LoadCertificate(
        builder.Configuration["Smtp:TlsCertificatePath"] ?? builder.Configuration["HMAILSERVER_SMTP_TLS_CERTIFICATE_PATH"],
        builder.Configuration["Smtp:TlsCertificatePassword"] ?? builder.Configuration["HMAILSERVER_SMTP_TLS_CERTIFICATE_PASSWORD"]);
    var pop3TlsCertificate = LoadCertificate(
        builder.Configuration["Pop3:TlsCertificatePath"] ?? builder.Configuration["HMAILSERVER_POP3_TLS_CERTIFICATE_PATH"],
        builder.Configuration["Pop3:TlsCertificatePassword"] ?? builder.Configuration["HMAILSERVER_POP3_TLS_CERTIFICATE_PASSWORD"]);
    var smtpRuleOptions = new SmtpRuleProcessorOptions
    {
        RuleLoopLimit = ReadInt(
            builder.Configuration["Smtp:RuleLoopLimit"] ?? builder.Configuration["HMAILSERVER_SMTP_RULE_LOOP_LIMIT"],
            defaultValue: 5)
    };
    var defaultBounceOptions = DeliveryBounceOptions.Default(smtpSessionOptions.ServerName);
    var deliveryBounceOptions = defaultBounceOptions with
    {
        SubjectTemplate = builder.Configuration["DeliveryQueue:BounceSubjectTemplate"]
            ?? builder.Configuration["HMAILSERVER_DELIVERY_BOUNCE_SUBJECT_TEMPLATE"]
            ?? defaultBounceOptions.SubjectTemplate,
        BodyTemplate = builder.Configuration["DeliveryQueue:BounceBodyTemplate"]
            ?? builder.Configuration["HMAILSERVER_DELIVERY_BOUNCE_BODY_TEMPLATE"]
            ?? defaultBounceOptions.BodyTemplate,
        MaxFailureDescriptionLength = ReadInt(
            builder.Configuration["DeliveryQueue:BounceMaxFailureDescriptionLength"]
                ?? builder.Configuration["HMAILSERVER_DELIVERY_BOUNCE_MAX_FAILURE_DESCRIPTION_LENGTH"],
            defaultBounceOptions.MaxFailureDescriptionLength)
    };
    var scriptingOptions = new WindowsScriptRuleExecutorOptions
    {
        Enabled = ReadBool(
            builder.Configuration["Scripting:Enabled"] ?? builder.Configuration["HMAILSERVER_SCRIPTING_ENABLED"],
            defaultValue: false),
        Language = builder.Configuration["Scripting:Language"]
            ?? builder.Configuration["HMAILSERVER_SCRIPTING_LANGUAGE"]
            ?? "VBScript",
        EventDirectory = builder.Configuration["Scripting:EventDirectory"]
            ?? builder.Configuration["HMAILSERVER_SCRIPT_EVENT_DIRECTORY"]
            ?? Path.Combine(AppContext.BaseDirectory, "Events"),
        EventLogPath = builder.Configuration["Scripting:EventLogPath"]
            ?? builder.Configuration["HMAILSERVER_SCRIPT_EVENT_LOG_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "Logs", "hmailserver_events.log"),
        Timeout = TimeSpan.FromMilliseconds(
            ReadInt(
                builder.Configuration["Scripting:TimeoutMilliseconds"] ?? builder.Configuration["HMAILSERVER_SCRIPT_TIMEOUT_MS"],
                defaultValue: 5000))
    };
    var imapAccountId = ReadNullableInt(builder.Configuration["Imap:AccountId"] ?? builder.Configuration["HMAILSERVER_IMAP_ACCOUNT_ID"]);
    var imapFolderId = ReadNullableInt(builder.Configuration["Imap:FolderId"] ?? builder.Configuration["HMAILSERVER_IMAP_FOLDER_ID"]);
    var mailboxOptions = new SqlServerImapMailboxStoreOptions
    {
        HierarchyDelimiter = builder.Configuration["Imap:HierarchyDelimiter"]
            ?? builder.Configuration["HMAILSERVER_IMAP_HIERARCHY_DELIMITER"]
            ?? ".",
        PublicFolderName = builder.Configuration["Imap:PublicFolderName"]
            ?? builder.Configuration["HMAILSERVER_IMAP_PUBLIC_FOLDER_NAME"]
            ?? "#Public",
        UseAcl = ReadBool(builder.Configuration["Imap:UseAcl"] ?? builder.Configuration["HMAILSERVER_IMAP_USE_ACL"], defaultValue: true)
    };
    var idleOptions = new ImapIdlePollingOptions
    {
        PollInterval = TimeSpan.FromMilliseconds(
            ReadInt(builder.Configuration["Imap:IdlePollMilliseconds"] ?? builder.Configuration["HMAILSERVER_IMAP_IDLE_POLL_MS"], defaultValue: 5000))
    };
    var imapSessionOptions = new ImapSessionOptions
    {
        RequireTlsForAuthentication = ReadBool(
            builder.Configuration["Imap:RequireTlsForAuthentication"] ?? builder.Configuration["HMAILSERVER_IMAP_REQUIRE_TLS_FOR_AUTH"],
            defaultValue: false)
    };

    if ((imapAccountId is null) != (imapFolderId is null))
    {
        throw new InvalidOperationException("Imap:AccountId and Imap:FolderId must be provided together when a fixed preselected IMAP context is used.");
    }

    builder.Services.AddSingleton(new SqlServerConnectionFactory(connectionString));
    builder.Services.AddSingleton(imapOptions);
    builder.Services.AddSingleton(smtpOptions);
    builder.Services.AddSingleton(pop3Options);
    builder.Services.AddSingleton(externalFetchProcessorOptions);
    builder.Services.AddSingleton(externalFetchHostedServiceOptions);
    builder.Services.AddSingleton(externalFetchPop3ClientOptions);
    builder.Services.AddSingleton(clamAvOptions);
    builder.Services.AddSingleton(imapSessionOptions);
    builder.Services.AddSingleton(smtpSessionOptions);
    builder.Services.AddSingleton(new Pop3SessionOptions());
    builder.Services.AddSingleton(mailboxOptions);
    builder.Services.AddSingleton(idleOptions);
    builder.Services.AddSingleton(smtpRuleOptions);
    builder.Services.AddSingleton(scriptingOptions);
    builder.Services.AddSingleton<IScriptSyntaxChecker, WindowsScriptSyntaxChecker>();
    builder.Services.AddSingleton<IScriptRuntimeReloader, WindowsScriptRuntimeReloader>();
    builder.Services.AddSingleton(spamAssassinOptions);
    builder.Services.AddSingleton(spamPolicyOptions);
    builder.Services.AddSingleton(spfPolicyOptions);
    builder.Services.AddSingleton(spfEvaluatorOptions);
    builder.Services.AddSingleton(dkimPolicyOptions);
    builder.Services.AddSingleton(dmarcPolicyOptions);
    builder.Services.AddSingleton<IDkimTxtResolver, SystemDkimTxtResolver>();
    builder.Services.AddSingleton<IClamAvScannerTestRuntime, ClamAvScannerTestRuntime>();
    builder.Services.AddSingleton<IDkimVerificationRuntime>(static serviceProvider =>
        new FileDkimVerificationRuntime(serviceProvider.GetRequiredService<IDkimTxtResolver>()));
    builder.Services.AddSingleton<ISpamAssassinConnectionTestRuntime, SpamAssassinConnectionTestRuntime>();
    builder.Services.AddSingleton<ILegacyBlowfishCipher, LegacyBlowfishCipherRuntime>();
    builder.Services.AddSingleton<IDnsAddressResolver, SystemDnsAddressResolver>();
    builder.Services.AddSingleton<ILocalIpAddressProvider, SystemLocalIpAddressProvider>();
    builder.Services.AddSingleton<ILocalHostRuntime, SystemLocalHostRuntime>();
    builder.Services.AddSingleton<SystemSpfDnsResolver>();
    builder.Services.AddSingleton<IMailServerDnsResolver>(static serviceProvider =>
        serviceProvider.GetRequiredService<SystemSpfDnsResolver>());
    builder.Services.AddSingleton<IMailServerResolver, SystemMailServerResolver>();
    builder.Services.AddSingleton(attachmentPolicyOptions);
    builder.Services.AddSingleton(dnsBlockListOptions);
    builder.Services.AddSingleton(reverseDnsOptions);
    builder.Services.AddSingleton(senderDomainMxOptions);
    builder.Services.AddSingleton(greylistingOptions);
    builder.Services.AddSingleton(urlBlockListOptions);
    if (scriptingOptions.Enabled)
    {
        builder.Services.AddSingleton<WindowsScriptRuleExecutor>();
        builder.Services.AddSingleton<ISmtpRuleScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<ISmtpEventScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<IDeliveryEventScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<IExternalAccountDownloadScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<IClientPasswordValidationScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<IErrorEventScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<IBackupEventScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
        builder.Services.AddSingleton<ILoggerProvider, ScriptErrorLoggerProvider>();
    }
    if (clamAvEnabled)
    {
        builder.Services.AddSingleton<ClamAvInstreamClient>();
        builder.Services.AddSingleton<IMessageAntivirusScanner, ClamAvMessageAntivirusScanner>();
    }
    if (spamAssassinEnabled)
    {
        builder.Services.AddSingleton<SpamAssassinClient>();
        builder.Services.AddSingleton<IMessageSpamScanner, SpamAssassinMessageSpamScanner>();
    }
    if (spamPolicyEnabled)
    {
        builder.Services.AddSingleton<IMessageSpamPolicy, MessageSpamPolicy>();
    }
    if (spfPolicyEnabled || dmarcPolicyEnabled)
    {
        builder.Services.AddSingleton<ISpfDnsResolver>(static serviceProvider =>
            serviceProvider.GetRequiredService<SystemSpfDnsResolver>());
    }
    if (spfPolicyEnabled)
    {
        builder.Services.AddSingleton<SpfEvaluator>();
        builder.Services.AddSingleton<ISmtpSpfPolicy, SmtpSpfPolicy>();
    }
    if (dkimPolicyEnabled)
    {
        builder.Services.AddSingleton<ISmtpDkimPolicy, SmtpDkimPolicy>();
    }
    if (dmarcPolicyEnabled)
    {
        builder.Services.AddSingleton<IDmarcTxtResolver, SystemDmarcTxtResolver>();
        if (dmarcOrganizationalDomainResolverEnabled && dmarcPublicSuffixListPath is not null)
        {
            builder.Services.AddSingleton<IDmarcOrganizationalDomainResolver>(
                new PublicSuffixDmarcOrganizationalDomainResolver(dmarcPublicSuffixListPath));
        }
        builder.Services.AddSingleton<ISmtpDmarcPolicy, SmtpDmarcPolicy>();
    }
    if (attachmentPolicyEnabled)
    {
        builder.Services.AddSingleton<IMessageAttachmentPolicy, MimeMessageAttachmentPolicy>();
    }
    if (dnsBlockListEnabled)
    {
        builder.Services.AddSingleton<ISmtpDnsBlockListChecker, SmtpDnsBlockListChecker>();
    }
    if (reverseDnsEnabled)
    {
        builder.Services.AddSingleton<IDnsReverseResolver, SystemDnsReverseResolver>();
        builder.Services.AddSingleton<ISmtpReverseDnsChecker, SmtpReverseDnsChecker>();
    }
    if (senderDomainMxEnabled)
    {
        builder.Services.AddSingleton<ISmtpSenderDomainMxChecker, SmtpSenderDomainMxChecker>();
    }
    if (greylistingEnabled)
    {
        builder.Services.AddSingleton<ISmtpGreylistingChecker, SqlServerSmtpGreylistingChecker>();
    }
    if (urlBlockListEnabled)
    {
        builder.Services.AddSingleton<ISmtpUrlBlockListChecker, SmtpUrlBlockListChecker>();
    }

    builder.Services.AddSingleton(new MessageFileSearchDocumentSourceOptions(dataDirectory));
    builder.Services.AddSingleton(MessageSearchBackfillOptions.Default(leaseOwner));
    builder.Services.AddSingleton<SqlServerImapSearchPlanner>();
    builder.Services.AddSingleton<SqlServerImapSortPlanner>();
    builder.Services.AddSingleton<SqlServerFullTextSearchHealthCheck>();
    builder.Services.AddSingleton<MessageFilePathResolver>();
    builder.Services.AddSingleton<MessageFileDeletionRuntime>();
    builder.Services.AddSingleton<IMessageSearchIndex, SqlServerMessageSearchIndex>();
    builder.Services.AddSingleton<IMessageSortIndex, SqlServerMessageSortIndex>();
    builder.Services.AddSingleton<IAutoBanLogonFailureRecorder, SqlServerAutoBanLogonFailureRecorder>();
    builder.Services.AddSingleton<IImapSequenceNumberResolver, SqlServerImapSequenceNumberResolver>();
    builder.Services.AddSingleton<IImapAccountAuthenticator, SqlServerImapAccountAuthenticator>();
    builder.Services.AddSingleton<SqlServerImapMailboxStore>();
    builder.Services.AddSingleton<IImapMailboxStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
    builder.Services.AddSingleton<IImapMailboxDiscoveryStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
    builder.Services.AddSingleton<IImapAclStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
    builder.Services.AddSingleton<IImapMailboxSubscriptionStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
    builder.Services.AddSingleton<IImapMessageFetchStore, SqlServerImapMessageFetchStore>();
    builder.Services.AddSingleton<IImapMessageMutationStore>(static serviceProvider =>
        new SqlServerImapMessageMutationStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            serviceProvider.GetRequiredService<MessageFilePathResolver>(),
            AccountAdministrationRuntimeHost.InvalidateAccountSize));
    builder.Services.AddSingleton<IImapMessageCopyStore>(static serviceProvider =>
        new SqlServerImapMessageCopyStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            serviceProvider.GetRequiredService<MessageFilePathResolver>(),
            AccountAdministrationRuntimeHost.InvalidateAccountSize));
    builder.Services.AddSingleton<IImapMessageAppendStore>(static serviceProvider =>
        new SqlServerImapMessageAppendStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            serviceProvider.GetRequiredService<MessageFilePathResolver>(),
            AccountAdministrationRuntimeHost.InvalidateAccountSize));
    builder.Services.AddSingleton<IImapIdleNotifier, PollingImapIdleNotifier>();
    builder.Services.AddSingleton<IImapQuotaStore, SqlServerImapQuotaStore>();
    builder.Services.AddSingleton<IImapRecentFlagStore, SqlServerImapRecentFlagStore>();
    builder.Services.AddSingleton<IPop3MailboxStore, SqlServerPop3MailboxStore>();
    builder.Services.AddSingleton<IPop3MailboxLockManager, InMemoryPop3MailboxLockManager>();
    builder.Services.AddSingleton<IExternalFetchAccountStore, SqlServerExternalFetchAccountStore>();
    builder.Services.AddSingleton<IExternalFetchSessionFactory>(static serviceProvider =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ExternalFetchHostedService>>();
        return new TcpExternalFetchSessionFactory(
            serviceProvider.GetRequiredService<ExternalFetchPop3ClientOptions>(),
            endpointDecisionObserver: decision =>
            {
                if (decision.IsAllowed)
                {
                    logger.LogDebug(
                        "External fetch egress allowed endpoint {Endpoint}.",
                        decision.Endpoint);
                }
                else
                {
                    logger.LogWarning(
                        "External fetch egress denied endpoint {Endpoint}: {Reason}.",
                        decision.Endpoint,
                        decision.Reason);
                }
            });
    });
    builder.Services.AddSingleton<SqlServerSmtpQueueWriter>();
    builder.Services.AddSingleton<SqlServerSmtpRuleProcessor>();
    builder.Services.AddSingleton<ISmtpRuleProcessor>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerSmtpRuleProcessor>());
    builder.Services.AddSingleton<ISmtpAccountRuleProcessor>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerSmtpRuleProcessor>());
    builder.Services.AddSingleton<ISmtpQueueWriter>(static serviceProvider =>
        new SignalingSmtpQueueWriter(
            serviceProvider.GetRequiredService<SqlServerSmtpQueueWriter>(),
            serviceProvider.GetRequiredService<IDeliveryQueueWakeSignal>()));
    builder.Services.AddSingleton<ISmtpMessageReceiver, SqlServerSmtpMessageReceiver>();
    builder.Services.AddSingleton<ISmtpRecipientValidator, SqlServerSmtpRecipientValidator>();
    builder.Services.AddSingleton<IDeliveryQueueLeaseStore, SqlServerDeliveryQueueLeaseStore>();
    builder.Services.AddSingleton<IDeliveryQueueAdministrationStore, SqlServerDeliveryQueueAdministrationStore>();
    builder.Services.AddSingleton<IDeliveryQueueMessageStore, SqlServerDeliveryQueueMessageStore>();
    builder.Services.AddSingleton<IDeliveryQueueRecipientStore, SqlServerDeliveryQueueRecipientStore>();
    builder.Services.AddSingleton<IDeliveryTargetResolver, SqlServerDeliveryTargetResolver>();
    builder.Services.AddSingleton<IScriptMessageCopyStore>(static serviceProvider =>
        new SqlServerScriptMessageCopyStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            serviceProvider.GetRequiredService<MessageFilePathResolver>(),
            AccountAdministrationRuntimeHost.InvalidateAccountSize));
    builder.Services.AddSingleton<ILocalDeliveryStore>(static serviceProvider =>
        new SqlServerLocalDeliveryStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            serviceProvider.GetRequiredService<MessageFilePathResolver>(),
            serviceProvider.GetService<ISmtpAccountRuleProcessor>(),
            serviceProvider.GetService<IImapMailboxStore>(),
            serviceProvider.GetService<SqlServerSmtpQueueWriter>(),
            serviceProvider.GetService<IScriptMessageCopyStore>(),
            AccountAdministrationRuntimeHost.InvalidateAccountSize));
    builder.Services.AddSingleton(deliveryBounceOptions);
    builder.Services.AddSingleton<IDeliveryBounceStore, SqlServerDeliveryBounceStore>();
    builder.Services.AddSingleton<DeliveryMessageContentSource>();
    builder.Services.AddSingleton<IDeliveryMessageContentSource>(static serviceProvider => serviceProvider.GetRequiredService<DeliveryMessageContentSource>());
    builder.Services.AddSingleton<IDeliveryMessageContentStore>(static serviceProvider => serviceProvider.GetRequiredService<DeliveryMessageContentSource>());
    builder.Services.AddSingleton<IDnsMxResolver, SystemDnsMxResolver>();
    builder.Services.AddSingleton(RemoteSmtpEndpointResolverOptions.Default);
    builder.Services.AddSingleton(DomainConcurrencyOptions.Default);
    builder.Services.AddSingleton<IRemoteSmtpEndpointResolver, RemoteSmtpEndpointResolver>();
    builder.Services.AddSingleton<IRemoteSmtpTransportFactory, TcpRemoteSmtpTransportFactory>();
    builder.Services.AddSingleton<IRemoteSmtpClient, SmtpRemoteDeliveryClient>();
    builder.Services.AddSingleton(RemoteDeliveryOptions.Default(smtpSessionOptions.ServerName));
    builder.Services.AddSingleton(DeliveryQueueProcessorOptions.Default(leaseOwner));
    builder.Services.AddSingleton(DeliveryQueueWorkerOptions.Default);
    builder.Services.AddSingleton(DeliveryQueueClearOptions.Default);
    builder.Services.AddSingleton<DeliveryQueueWakeSignal>();
    builder.Services.AddSingleton<IDeliveryQueueWakeSignal>(static serviceProvider =>
        serviceProvider.GetRequiredService<DeliveryQueueWakeSignal>());
    builder.Services.AddSingleton<ExternalFetchWakeSignal>();
    builder.Services.AddSingleton<IExternalFetchWakeSignal>(static serviceProvider =>
        serviceProvider.GetRequiredService<ExternalFetchWakeSignal>());
    builder.Services.AddSingleton(deliveryStatusMaintenanceOptions);
    builder.Services.AddSingleton<SqlServerDeliveryQueueStatusMaintenanceStore>();
    if (deliveryStatusSqlEnabled)
    {
        builder.Services.AddSingleton<SqlServerDeliveryQueueStatusObserver>();
        builder.Services.AddSingleton<IDeliveryQueueStatusObserver>(
            serviceProvider => new ServerStatusDeliveryQueueStatusObserver(
                serviceProvider.GetRequiredService<SqlServerDeliveryQueueStatusObserver>(),
                serviceProvider.GetRequiredService<ServerStatusRuntimeState>()));
        builder.Services.AddSingleton<IDeliveryQueueStatusMetricsStore, SqlServerDeliveryQueueStatusMetricsStore>();
    }
    else
    {
        builder.Services.AddSingleton<IDeliveryQueueStatusObserver>(
            serviceProvider => new ServerStatusDeliveryQueueStatusObserver(
                NullDeliveryQueueStatusObserver.Instance,
                serviceProvider.GetRequiredService<ServerStatusRuntimeState>()));
    }

    builder.Services.AddSingleton<LocalDeliveryTargetDispatcher>();
    builder.Services.AddSingleton<RemoteDeliveryTargetDispatcher>();
    builder.Services.AddSingleton(static serviceProvider =>
        new DomainConcurrencyDeliveryTargetDispatcher(
            serviceProvider.GetRequiredService<RemoteDeliveryTargetDispatcher>(),
            serviceProvider.GetRequiredService<DomainConcurrencyOptions>()));
    builder.Services.AddSingleton<IDeliveryTargetDispatcher>(static serviceProvider =>
        new CompositeDeliveryTargetDispatcher(
            serviceProvider.GetRequiredService<LocalDeliveryTargetDispatcher>(),
            serviceProvider.GetRequiredService<DomainConcurrencyDeliveryTargetDispatcher>()));
    builder.Services.AddSingleton<DeliveryQueueProcessor>();
    builder.Services.AddSingleton<IDeliveryQueueBatchProcessor>(static serviceProvider =>
        serviceProvider.GetRequiredService<DeliveryQueueProcessor>());
    builder.Services.AddSingleton<IDeliveryQueueClearObserver, DeliveryQueueClearLogObserver>();
    builder.Services.AddSingleton<DeliveryQueueClearCoordinator>(static serviceProvider =>
        new DeliveryQueueClearCoordinator(
            serviceProvider.GetRequiredService<DeliveryQueueClearOptions>(),
            serviceProvider.GetRequiredService<IDeliveryQueueAdministrationStore>(),
            serviceProvider.GetRequiredService<IDeliveryQueueClearObserver>(),
            serviceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping));
    builder.Services.AddSingleton<IDeliveryQueueClearCoordinator>(static serviceProvider =>
        serviceProvider.GetRequiredService<DeliveryQueueClearCoordinator>());
    builder.Services.AddSingleton(static serviceProvider =>
        new ExternalFetchProcessor(
            serviceProvider.GetRequiredService<IExternalFetchAccountStore>(),
            serviceProvider.GetRequiredService<IExternalFetchSessionFactory>(),
            serviceProvider.GetRequiredService<ISmtpMessageReceiver>(),
            serviceProvider.GetService<IExternalAccountDownloadScriptExecutor>(),
            serviceProvider.GetService<IMessageAntivirusScanner>(),
            serviceProvider.GetService<ISmtpRecipientValidator>()));
    builder.Services.AddSingleton<IImapConnectionStreamFactory, PlainImapConnectionStreamFactory>();
    builder.Services.AddSingleton<IPop3ConnectionStreamFactory>(_ =>
        pop3TlsCertificate is null
            ? new PlainPop3ConnectionStreamFactory()
            : new ImplicitTlsPop3ConnectionStreamFactory(
                () => TlsServerAuthenticationOptionsFactory.Create(pop3TlsCertificate)));
    builder.Services.AddSingleton<ISmtpConnectionStreamFactory>(_ =>
        smtpTlsCertificate is null
            ? new PlainSmtpConnectionStreamFactory()
            : new StartTlsSmtpConnectionStreamFactory(
                () => TlsServerAuthenticationOptionsFactory.Create(smtpTlsCertificate)));
    builder.Services.AddSingleton<IImapSessionContextProvider>(
        new FixedImapSessionContextProvider(
            imapAccountId is { } accountId && imapFolderId is { } folderId
                ? new ImapSessionContext(accountId, folderId)
                : new ImapSessionContext()));
    builder.Services.AddSingleton<IMessageSearchBackfillStore, SqlServerMessageSearchBackfillStore>();
    builder.Services.AddSingleton<IMessageIndexingAdministrationStore, SqlServerMessageIndexingAdministrationStore>();
    builder.Services.AddSingleton<IDatabaseAdministrationStore>(
        serviceProvider => new SqlServerDatabaseAdministrationStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            databaseConfiguration));
    builder.Services.AddSingleton<IMessageFileNameLookup, SqlServerMessageFileNameLookup>();
    builder.Services.AddSingleton<IMessageIdResolver>(serviceProvider =>
        new StoreBackedMessageIdResolver(
            serviceProvider.GetRequiredService<IMessageFileNameLookup>(),
            dataDirectory));
    builder.Services.AddSingleton<IImapFolderUidMaintenanceStore, SqlServerImapFolderUidMaintenanceStore>();
    builder.Services.AddSingleton<IServiceDependencyRuntime, WindowsServiceDependencyRuntime>();
    builder.Services.AddSingleton<IEmailAllAccountsRecipientStore, SqlServerEmailAllAccountsRecipientStore>();
    builder.Services.AddSingleton<IEmailAllAccountsRuntime, StoreBackedEmailAllAccountsRuntime>();
    builder.Services.AddSingleton<IImportMessageFromFileStore>(static serviceProvider =>
        new SqlServerImportMessageFromFileStore(
            serviceProvider.GetRequiredService<SqlServerConnectionFactory>(),
            AccountAdministrationRuntimeHost.InvalidateAccountSize));
    builder.Services.AddSingleton<IImportMessageFromFileRuntime>(serviceProvider =>
        new StoreBackedImportMessageFromFileRuntime(
            serviceProvider.GetRequiredService<IImportMessageFromFileStore>(),
            serviceProvider.GetRequiredService<ISmtpRecipientValidator>(),
            serviceProvider.GetRequiredService<IDeliveryQueueWakeSignal>(),
            dataDirectory,
            mailboxOptions: mailboxOptions,
            aclStore: serviceProvider.GetRequiredService<IImapAclStore>()));
    builder.Services.AddSingleton<ServerStatusRuntimeState>();
    builder.Services.AddSingleton<IApplicationRuntimeStore>(
        serviceProvider => new ServerApplicationRuntimeStore(
            serviceProvider.GetRequiredService<ServerStatusRuntimeState>(),
            applicationVersion,
            initializationFile));
    builder.Services.AddSingleton<IServerStatusAdministrationStore, SqlServerServerStatusAdministrationStore>();
    builder.Services.AddSingleton<SqlServerSettingsAdministrationStore>();
    builder.Services.AddSingleton<ISettingsAdministrationStore>(serviceProvider =>
        serviceProvider.GetRequiredService<SqlServerSettingsAdministrationStore>());
    builder.Services.AddSingleton<IBackupSettingsPropertyStore>(serviceProvider =>
        serviceProvider.GetRequiredService<SqlServerSettingsAdministrationStore>());
    builder.Services.AddSingleton<IBackupPreflightAdministrationStore, SqlServerBackupPreflightAdministrationStore>();
    builder.Services.AddSingleton<ILogonFailureAdministrationStore, SqlServerLogonFailureAdministrationStore>();
    builder.Services.AddSingleton<IBlockedAttachmentAdministrationStore, SqlServerBlockedAttachmentAdministrationStore>();
    builder.Services.AddSingleton<IDnsBlackListAdministrationStore, SqlServerDnsBlackListAdministrationStore>();
    builder.Services.AddSingleton<ISurblServerAdministrationStore, SqlServerSurblServerAdministrationStore>();
    builder.Services.AddSingleton<IGreyListingWhiteAddressAdministrationStore, SqlServerGreyListingWhiteAddressAdministrationStore>();
    builder.Services.AddSingleton<IGreyListingTripletAdministrationStore, SqlServerGreyListingTripletAdministrationStore>();
    builder.Services.AddSingleton<IWhiteListAddressAdministrationStore, SqlServerWhiteListAddressAdministrationStore>();
    builder.Services.AddSingleton<IDomainAdministrationStore, SqlServerDomainAdministrationStore>();
    builder.Services.AddSingleton<SqlServerAccountAdministrationStore>();
    builder.Services.AddSingleton<IAccountAdministrationStore>(
        serviceProvider => serviceProvider.GetRequiredService<SqlServerAccountAdministrationStore>());
    builder.Services.AddSingleton<IBackupAccountAdministrationStore>(
        serviceProvider => serviceProvider.GetRequiredService<SqlServerAccountAdministrationStore>());
    builder.Services.AddSingleton<IMessageAdministrationStore, SqlServerMessageAdministrationStore>();
    builder.Services.AddSingleton<IMessageAdministrationContentSource, SqlServerMessageAdministrationContentSource>();
    builder.Services.AddSingleton<IFetchAccountAdministrationStore, SqlServerFetchAccountAdministrationStore>();
    builder.Services.AddSingleton<IBackupFetchAccountAdministrationStore, SqlServerBackupFetchAccountAdministrationStore>();
    builder.Services.AddSingleton<SqlServerRuleAdministrationStore>();
    builder.Services.AddSingleton<IRuleAdministrationStore>(serviceProvider =>
        serviceProvider.GetRequiredService<SqlServerRuleAdministrationStore>());
    builder.Services.AddSingleton<IBackupRuleAdministrationStore>(serviceProvider =>
        serviceProvider.GetRequiredService<SqlServerRuleAdministrationStore>());
    builder.Services.AddSingleton<IRuleCriteriaAdministrationStore, SqlServerRuleCriteriaAdministrationStore>();
    builder.Services.AddSingleton<IRuleActionAdministrationStore, SqlServerRuleActionAdministrationStore>();
    builder.Services.AddSingleton<IImapFolderAdministrationStore, SqlServerImapFolderAdministrationStore>();
    builder.Services.AddSingleton<IRouteAdministrationStore, SqlServerRouteAdministrationStore>();
    builder.Services.AddSingleton<IRouteAddressAdministrationStore, SqlServerRouteAddressAdministrationStore>();
    builder.Services.AddSingleton<IIncomingRelayAdministrationStore, SqlServerIncomingRelayAdministrationStore>();
    builder.Services.AddSingleton<ISecurityRangeAdministrationStore, SqlServerSecurityRangeAdministrationStore>();
    builder.Services.AddSingleton<ITcpIpPortAdministrationStore, SqlServerTcpIpPortAdministrationStore>();
    builder.Services.AddSingleton<ISslCertificateAdministrationStore, SqlServerSslCertificateAdministrationStore>();
    builder.Services.AddSingleton<IServerMessageAdministrationStore, SqlServerServerMessageAdministrationStore>();
    builder.Services.AddSingleton<IDirectoryAdministrationStore>(
        new LegacyDirectoryAdministrationStore(initializationFile));
    builder.Services.AddSingleton<ILanguageAdministrationStore>(
        new LegacyLanguageAdministrationStore(AppContext.BaseDirectory, initializationFile));
    builder.Services.AddSingleton<IGroupAdministrationStore, SqlServerGroupAdministrationStore>();
    builder.Services.AddSingleton<IGroupMemberAdministrationStore, SqlServerGroupMemberAdministrationStore>();
    builder.Services.AddSingleton<IAliasAdministrationStore, SqlServerAliasAdministrationStore>();
    builder.Services.AddSingleton<IDistributionListAdministrationStore, SqlServerDistributionListAdministrationStore>();
    builder.Services.AddSingleton<IDistributionListRecipientAdministrationStore, SqlServerDistributionListRecipientAdministrationStore>();
    builder.Services.AddSingleton<IDomainAliasAdministrationStore, SqlServerDomainAliasAdministrationStore>();
    builder.Services.AddSingleton<StoreBackedMessageIndexingRuntime>();
    builder.Services.AddSingleton<IMessageSearchDocumentSource, MessageFileSearchDocumentSource>();
    builder.Services.AddCallerAwareProtocolServices();
    builder.Services.AddSingleton<ImapSortCommandParser>();
    builder.Services.AddSingleton<ImapSortExecutor>();
    builder.Services.AddSingleton<ImapSortCommandHandler>();
    builder.Services.AddSingleton<ImapFetchCommandParser>();
    builder.Services.AddSingleton<ImapFetchCommandHandler>();
    builder.Services.AddSingleton<ImapStatusCommandParser>();
    builder.Services.AddSingleton<ImapStatusCommandHandler>();
    builder.Services.AddSingleton<ImapStoreCommandParser>();
    builder.Services.AddSingleton<ImapStoreCommandHandler>();
    builder.Services.AddSingleton<ImapExpungeCommandHandler>();
    builder.Services.AddSingleton<ImapCopyCommandParser>();
    builder.Services.AddSingleton<ImapCopyCommandHandler>();
    builder.Services.AddSingleton<ImapAppendCommandParser>();
    builder.Services.AddSingleton<ImapAppendCommandHandler>();
    builder.Services.AddSingleton<ImapAclCommandHandler>();
    builder.Services.AddSingleton<ImapQuotaCommandHandler>();
    builder.Services.AddSingleton(serviceProvider => new ImapSubscriptionCommandHandler(
        serviceProvider.GetRequiredService<IImapMailboxSubscriptionStore>(),
        mailboxOptions.PublicFolderName));
    builder.Services.AddSingleton(serviceProvider => new ImapListCommandHandler(
        serviceProvider.GetRequiredService<IImapMailboxDiscoveryStore>(),
        mailboxOptions.HierarchyDelimiter));
    builder.Services.AddSingleton<MessageSearchBackfillProcessor>();
    builder.Services.AddProductionHostedServices(externalFetchEnabled);
        return new HostBuildResult(
            builder.Build(),
            dataDirectory,
            backupMessagesDbOnly,
            userInterfaceLanguage,
            rewriteEnvelopeFromWhenForwarding);
    }

    static bool ReadBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.Parse(value);
    }

    static int ReadInt(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    static int? ReadNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    static IReadOnlyList<string> ReadList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static long ReadLong(string? value, long defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    static string? ResolveOptionalPath(string? configuredPath, string defaultFileName)
    {
        try
        {
            var candidate = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(AppContext.BaseDirectory, defaultFileName)
                : configuredPath.Trim();
            return Path.IsPathFullyQualified(candidate)
                ? Path.GetFullPath(candidate)
                : Path.GetFullPath(candidate, AppContext.BaseDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or NotSupportedException
                                          or PathTooLongException)
        {
            return null;
        }
    }

    static X509Certificate2? LoadCertificate(string? path, string? password)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet,
            loaderLimits: null);
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("TLS certificate must include a private key.");
        }

        return certificate;
    }
}

public sealed record HostBuildResult(
    IHost Host,
    string DataDirectory,
    bool BackupMessagesDbOnly,
    string UserInterfaceLanguage,
    bool RewriteEnvelopeFromWhenForwarding);
