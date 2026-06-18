using HMailServer.Service;
using System.Net;
using HMailServer.Core.Abstractions;
using HMailServer.Delivery;
using HMailServer.Indexing;
using HMailServer.Protocols.Imap;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;
using HMailServer.Scripting;
using HMailServer.Search.SqlServer;
using HMailServer.Security;
using HMailServer.Storage.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography.X509Certificates;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "hMailServer");

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
        defaultValue: true)
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
builder.Services.AddSingleton(spamAssassinOptions);
if (scriptingOptions.Enabled)
{
    builder.Services.AddSingleton<WindowsScriptRuleExecutor>();
    builder.Services.AddSingleton<ISmtpRuleScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
    builder.Services.AddSingleton<ISmtpEventScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
    builder.Services.AddSingleton<IDeliveryEventScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
    builder.Services.AddSingleton<IExternalAccountDownloadScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
    builder.Services.AddSingleton<IClientPasswordValidationScriptExecutor>(static serviceProvider => serviceProvider.GetRequiredService<WindowsScriptRuleExecutor>());
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

builder.Services.AddSingleton(new MessageFileSearchDocumentSourceOptions(dataDirectory));
builder.Services.AddSingleton(MessageSearchBackfillOptions.Default(leaseOwner));
builder.Services.AddSingleton<SqlServerImapSearchPlanner>();
builder.Services.AddSingleton<SqlServerImapSortPlanner>();
builder.Services.AddSingleton<SqlServerFullTextSearchHealthCheck>();
builder.Services.AddSingleton<MessageFilePathResolver>();
builder.Services.AddSingleton<IMessageSearchIndex, SqlServerMessageSearchIndex>();
builder.Services.AddSingleton<IMessageSortIndex, SqlServerMessageSortIndex>();
builder.Services.AddSingleton<IAutoBanLogonFailureRecorder, SqlServerAutoBanLogonFailureRecorder>();
builder.Services.AddSingleton<IImapSequenceNumberResolver, SqlServerImapSequenceNumberResolver>();
builder.Services.AddSingleton<IImapAccountAuthenticator, SqlServerImapAccountAuthenticator>();
builder.Services.AddSingleton<SqlServerImapMailboxStore>();
builder.Services.AddSingleton<IImapMailboxStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
builder.Services.AddSingleton<IImapMailboxDiscoveryStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
builder.Services.AddSingleton<IImapAclStore>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerImapMailboxStore>());
builder.Services.AddSingleton<IImapMessageFetchStore, SqlServerImapMessageFetchStore>();
builder.Services.AddSingleton<IImapMessageMutationStore, SqlServerImapMessageMutationStore>();
builder.Services.AddSingleton<IImapMessageCopyStore, SqlServerImapMessageCopyStore>();
builder.Services.AddSingleton<IImapMessageAppendStore, SqlServerImapMessageAppendStore>();
builder.Services.AddSingleton<IImapIdleNotifier, PollingImapIdleNotifier>();
builder.Services.AddSingleton<IImapQuotaStore, SqlServerImapQuotaStore>();
builder.Services.AddSingleton<IImapRecentFlagStore, SqlServerImapRecentFlagStore>();
builder.Services.AddSingleton<IPop3MailboxStore, SqlServerPop3MailboxStore>();
builder.Services.AddSingleton<IPop3MailboxLockManager, InMemoryPop3MailboxLockManager>();
builder.Services.AddSingleton<IExternalFetchAccountStore, SqlServerExternalFetchAccountStore>();
builder.Services.AddSingleton<IExternalFetchSessionFactory, TcpExternalFetchSessionFactory>();
builder.Services.AddSingleton<SqlServerSmtpQueueWriter>();
builder.Services.AddSingleton<SqlServerSmtpRuleProcessor>();
builder.Services.AddSingleton<ISmtpRuleProcessor>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerSmtpRuleProcessor>());
builder.Services.AddSingleton<ISmtpAccountRuleProcessor>(static serviceProvider => serviceProvider.GetRequiredService<SqlServerSmtpRuleProcessor>());
builder.Services.AddSingleton<ISmtpMessageReceiver, SqlServerSmtpMessageReceiver>();
builder.Services.AddSingleton<ISmtpRecipientValidator, SqlServerSmtpRecipientValidator>();
builder.Services.AddSingleton<IDeliveryQueueLeaseStore, SqlServerDeliveryQueueLeaseStore>();
builder.Services.AddSingleton<IDeliveryQueueMessageStore, SqlServerDeliveryQueueMessageStore>();
builder.Services.AddSingleton<IDeliveryQueueRecipientStore, SqlServerDeliveryQueueRecipientStore>();
builder.Services.AddSingleton<IDeliveryTargetResolver, SqlServerDeliveryTargetResolver>();
builder.Services.AddSingleton<ILocalDeliveryStore, SqlServerLocalDeliveryStore>();
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
builder.Services.AddSingleton(deliveryStatusMaintenanceOptions);
builder.Services.AddSingleton<SqlServerDeliveryQueueStatusMaintenanceStore>();
if (deliveryStatusSqlEnabled)
{
    builder.Services.AddSingleton<IDeliveryQueueStatusObserver, SqlServerDeliveryQueueStatusObserver>();
    builder.Services.AddSingleton<IDeliveryQueueStatusMetricsStore, SqlServerDeliveryQueueStatusMetricsStore>();
}
else
{
    builder.Services.AddSingleton<IDeliveryQueueStatusObserver>(NullDeliveryQueueStatusObserver.Instance);
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
builder.Services.AddSingleton(static serviceProvider =>
    new ExternalFetchProcessor(
        serviceProvider.GetRequiredService<IExternalFetchAccountStore>(),
        serviceProvider.GetRequiredService<IExternalFetchSessionFactory>(),
        serviceProvider.GetRequiredService<ISmtpMessageReceiver>(),
        serviceProvider.GetService<IExternalAccountDownloadScriptExecutor>(),
        serviceProvider.GetService<IMessageAntivirusScanner>()));
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
builder.Services.AddSingleton<IMessageSearchDocumentSource, MessageFileSearchDocumentSource>();
builder.Services.AddSingleton<ImapSearchCommandParser>();
builder.Services.AddSingleton<ImapSearchExecutor>();
builder.Services.AddSingleton<ImapSearchCommandHandler>();
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
builder.Services.AddSingleton(serviceProvider => new ImapListCommandHandler(
    serviceProvider.GetRequiredService<IImapMailboxDiscoveryStore>(),
    mailboxOptions.HierarchyDelimiter));
builder.Services.AddSingleton<ImapSession>();
builder.Services.AddSingleton<ImapTcpListener>();
builder.Services.AddSingleton<Pop3Session>();
builder.Services.AddSingleton<Pop3TcpListener>();
builder.Services.AddSingleton<SmtpSession>();
builder.Services.AddSingleton<SmtpTcpListener>();
builder.Services.AddSingleton<MessageSearchBackfillProcessor>();
builder.Services.AddHostedService<ServerBootstrapper>();
builder.Services.AddHostedService<MessageSearchBackfillHostedService>();
builder.Services.AddHostedService<DeliveryQueueStatusMaintenanceHostedService>();
if (externalFetchEnabled)
{
    builder.Services.AddHostedService<ExternalFetchHostedService>();
}
builder.Services.AddHostedService<ImapTcpListenerHostedService>();
builder.Services.AddHostedService<Pop3TcpListenerHostedService>();
builder.Services.AddHostedService<SmtpTcpListenerHostedService>();

await builder.Build().RunAsync().ConfigureAwait(false);

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

static long ReadLong(string? value, long defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
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
