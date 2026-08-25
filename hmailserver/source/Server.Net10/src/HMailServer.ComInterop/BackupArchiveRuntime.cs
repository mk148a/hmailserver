using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

/// <summary>
/// Creates the bounded legacy archive and modeled scalar settings/domain metadata.
/// Remaining nested domain children and restore remain fenced.
/// </summary>
[ComVisible(false)]
public sealed class SevenZipBackupArchiveRuntime
{
    private readonly string _sevenZipExecutablePath;
    private readonly string _applicationVersion;
    private readonly Func<DateTime> _localNow;
    private readonly Func<BackupStartPlanEvidence, CancellationToken, ValueTask<BackupArchiveXmlPayload>>? _payloadProvider;
    private readonly string? _dataDirectory;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;
    private readonly Func<CancellationToken, ValueTask<IDisposable>>? _pauseDeliveryQueue;

    public SevenZipBackupArchiveRuntime(
        string sevenZipExecutablePath,
        string applicationVersion,
        Func<DateTime>? localNow = null,
        Func<BackupStartPlanEvidence, CancellationToken, ValueTask<BackupArchiveXmlPayload>>? payloadProvider = null,
        string? dataDirectory = null,
        Func<CancellationToken, ValueTask>? restoreReinitializer = null,
        Func<ProcessStartInfo, Process?>? processStarter = null,
        Func<CancellationToken, ValueTask<IDisposable>>? pauseDeliveryQueue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        ArgumentNullException.ThrowIfNull(applicationVersion);

        _sevenZipExecutablePath = sevenZipExecutablePath;
        _applicationVersion = applicationVersion;
        _localNow = localNow ?? (static () => DateTime.Now);
        _payloadProvider = payloadProvider;
        _dataDirectory = dataDirectory;
        _processStarter = processStarter ?? Process.Start;
        _pauseDeliveryQueue = pauseDeliveryQueue;
        if (_dataDirectory is not null
            && _payloadProvider?.Target is BackupXmlPayloadRuntime payloadRuntime)
        {
            payloadRuntime.ConfigureRestoreRuntime(
                _sevenZipExecutablePath,
                _dataDirectory,
                restoreReinitializer);
        }
    }

    public async ValueTask CreateAsync(
        BackupStartPlanEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        using var deliveryQueueLease = _pauseDeliveryQueue is null
            ? null
            : await _pauseDeliveryQueue(cancellationToken).ConfigureAwait(false);

        var includesMessages =
            (evidence.BackupOptions & BackupStartPlan.BackupMessagesFlag) != 0;
        var includesDomainMessages =
            (evidence.BackupOptions
                & (BackupStartPlan.BackupDomainsFlag | BackupStartPlan.BackupMessagesFlag))
            == (BackupStartPlan.BackupDomainsFlag | BackupStartPlan.BackupMessagesFlag);
        var stagesCompressedMessageData =
            (evidence.BackupOptions
                & (BackupStartPlan.BackupDomainsFlag
                    | BackupStartPlan.BackupMessagesFlag
                    | BackupStartPlan.BackupCompressionFlag))
            == (BackupStartPlan.BackupDomainsFlag
                | BackupStartPlan.BackupMessagesFlag
                | BackupStartPlan.BackupCompressionFlag)
            && !evidence.BackupMessagesDbOnly;
        var stagesRawMessageData =
            includesDomainMessages
            && (evidence.BackupOptions & BackupStartPlan.BackupCompressionFlag) == 0
            && !evidence.BackupMessagesDbOnly;
        var stagesPhysicalMessageData = stagesCompressedMessageData || stagesRawMessageData;
        if (includesDomainMessages
            && !evidence.BackupMessagesDbOnly
            && !stagesPhysicalMessageData)
        {
            throw new NotSupportedException(
                "Domain message backup staging is not implemented yet.");
        }

        if (stagesPhysicalMessageData && string.IsNullOrWhiteSpace(_dataDirectory))
        {
            throw new InvalidOperationException(
                "The data directory is required for physical message backup staging.");
        }

        var destination = BackupStartPlanRuntime.NormalizeDestination(evidence.Destination);
        if (!Directory.Exists(destination))
        {
            throw new InvalidOperationException(
                "The specified backup directory is not accessible: " + destination);
        }

        if (stagesPhysicalMessageData)
        {
            EnsureNoExistingAncestorReparsePoints(
                _dataDirectory!,
                "configured data directory");
            EnsureNoExistingAncestorReparsePoints(
                destination,
                "backup directory");
        }

        BackupArchiveXmlPayload? payload = null;
        if ((evidence.BackupOptions
                & (BackupStartPlan.BackupSettingsFlag | BackupStartPlan.BackupDomainsFlag)) != 0)
        {
            if (_payloadProvider is null)
            {
                throw new NotSupportedException(
                    "Backup settings and domain payload serialization is not configured.");
            }

            payload = await _payloadProvider(evidence, cancellationToken)
                .ConfigureAwait(false);
        }

        var timestamp = _localNow().ToString(
            "yyyy-MM-dd HHmmss",
            CultureInfo.InvariantCulture);
        var archivePath = Path.Combine(destination, $"HMBackup {timestamp}.7z");
        var temporaryArchivePath = Path.Combine(
            destination,
            $".{Path.GetFileName(archivePath)}.{Guid.NewGuid():N}.tmp");
        var metadataPath = Path.Combine(destination, SevenZipBackupArchiveMetadataReader.MetadataEntryName);
        var dataBackupPath = stagesPhysicalMessageData
            ? Path.Combine(destination, "DataBackup")
            : null;
        var dataBackupCreated = false;
        var backupCompleted = false;

        try
        {
            if (dataBackupPath is not null)
            {
                EnsureDataBackupPathIsSafe(_dataDirectory!, dataBackupPath);
                dataBackupCreated = true;
                StageDataDirectory(_dataDirectory!, dataBackupPath);
            }

            var metadata = CreateMetadataXml(evidence.BackupOptions, _applicationVersion, payload);
            await File.WriteAllTextAsync(
                metadataPath,
                metadata,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
            await AddFileAsync(
                temporaryArchivePath,
                metadataPath,
                cancellationToken).ConfigureAwait(false);
            if (stagesCompressedMessageData && dataBackupPath is not null)
            {
                await AddDirectoryAsync(
                    temporaryArchivePath,
                    dataBackupPath,
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryArchivePath, archivePath, overwrite: true);
            backupCompleted = true;
        }
        finally
        {
            if (File.Exists(temporaryArchivePath))
            {
                File.Delete(temporaryArchivePath);
            }

            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }

            if ((stagesCompressedMessageData || !backupCompleted)
                && dataBackupCreated
                && Directory.Exists(dataBackupPath))
            {
                Directory.Delete(dataBackupPath, recursive: true);
            }
        }
    }

    private static void EnsureDataBackupPathIsSafe(
        string dataDirectory,
        string dataBackupPath)
    {
        if (Directory.Exists(dataBackupPath) || File.Exists(dataBackupPath))
        {
            throw new InvalidOperationException(
                "The backup DataBackup staging path already exists: " + dataBackupPath);
        }

        var sourcePath = Path.GetFullPath(dataDirectory);
        var stagingPath = Path.GetFullPath(dataBackupPath);
        var relativePath = Path.GetRelativePath(sourcePath, stagingPath);
        if (relativePath == "."
            || (!Path.IsPathRooted(relativePath)
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(relativePath, "..", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The backup DataBackup staging path must be outside the data directory.");
        }
    }

    private static void EnsureNoExistingAncestorReparsePoints(
        string path,
        string pathDescription)
    {
        var currentPath = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(currentPath);
            }
            catch (FileNotFoundException)
            {
                attributes = 0;
            }
            catch (DirectoryNotFoundException)
            {
                attributes = 0;
            }
            catch (IOException exception)
            {
                throw new IOException(
                    "The " + pathDescription + " path could not be validated for reparse points.",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new IOException(
                    "The " + pathDescription + " path could not be validated for reparse points.",
                    exception);
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "The " + pathDescription + " path traverses a reparse point: "
                    + currentPath);
            }

            var parentPath = Path.GetDirectoryName(currentPath);
            if (string.Equals(parentPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            currentPath = parentPath!;
        }
    }

    private static void StageDataDirectory(
        string sourceDirectory,
        string dataBackupPath)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException(
                "The configured data directory is not accessible: " + sourceDirectory);
        }

        _ = BackupDataDirectoryIdentity.CopyStableSnapshot(sourceDirectory, dataBackupPath);
        foreach (var file in Directory.EnumerateFiles(dataBackupPath))
        {
            File.Delete(file);
        }
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "The backup data directory contains a reparse point: " + path);
        }
    }

    internal static string CreateMetadataXml(int backupOptions, string applicationVersion)
        => CreateMetadataXml(backupOptions, applicationVersion, payload: null);

    internal static string CreateMetadataXml(
        int backupOptions,
        string applicationVersion,
        BackupArchiveXmlPayload? payload)
    {
        ArgumentNullException.ThrowIfNull(applicationVersion);

        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };
        using (var writer = XmlWriter.Create(builder, settings))
        {
            writer.WriteStartElement("Backup");
            writer.WriteStartElement("BackupInformation");
            writer.WriteAttributeString(
                "Mode",
                backupOptions.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Version", applicationVersion);
            if ((backupOptions & BackupStartPlan.BackupMessagesFlag) != 0)
            {
                WriteDataFiles(writer, backupOptions);
            }

            writer.WriteEndElement();
            if ((backupOptions & BackupStartPlan.BackupDomainsFlag) != 0)
            {
                WriteDomains(
                    writer,
                    payload?.Domains,
                    payload?.DomainAliases,
                    payload?.Accounts,
                    payload?.BackupAccounts,
                    payload?.FetchAccounts,
                    payload?.Aliases,
                    payload?.DistributionLists,
                    payload?.DistributionListRecipients,
                    payload?.BackupFetchAccounts,
                    payload?.Rules,
                    payload?.RuleCriterias,
                    payload?.RuleActions,
                    payload?.Folders,
                    payload?.FolderMessages,
                    includeFolderMetadata: (backupOptions & BackupStartPlan.BackupMessagesFlag) != 0);
            }

            if ((backupOptions & BackupStartPlan.BackupSettingsFlag) != 0)
            {
                WriteSettings(writer, payload);
            }

            writer.WriteEndElement();
        }

        return builder.ToString();
    }

    private static void WriteDataFiles(XmlWriter writer, int backupOptions)
    {
        writer.WriteStartElement("DataFiles");
        if ((backupOptions & BackupStartPlan.BackupCompressionFlag) != 0)
        {
            writer.WriteAttributeString("Format", "7z");
            writer.WriteAttributeString("Size", "0");
        }
        else
        {
            writer.WriteAttributeString("Format", "Raw");
            writer.WriteAttributeString("FolderName", "DataBackup");
        }

        writer.WriteEndElement();
    }

    private static void WriteSettings(
        XmlWriter writer,
        BackupArchiveXmlPayload? payload)
    {
        if (payload?.SettingsProperties is not null)
        {
            WriteRawSettings(writer, payload.SettingsProperties);
        }
        else
        {
            WriteModeledSettings(writer, payload?.Settings);
        }

        WritePublicFolders(writer, payload?.PublicFolders);
        WriteGroups(writer, payload?.Groups);
    }

    private static void WriteRawSettings(
        XmlWriter writer,
        IReadOnlyList<BackupSettingsPropertySnapshot> properties)
    {
        var orderedProperties = new List<BackupSettingsPropertySnapshot>();
        foreach (var property in properties)
        {
            if (!string.Equals(
                    property.Name,
                    "smtprelayerpassword",
                    StringComparison.OrdinalIgnoreCase))
            {
                orderedProperties.Add(property);
            }
        }

        orderedProperties.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.Name, right.Name));

        writer.WriteStartElement("Properties");
        foreach (var property in orderedProperties)
        {
            WriteProperty(writer, property.Name, property.LongValue, property.StringValue);
        }

        writer.WriteEndElement();
    }

    private static void WriteModeledSettings(
        XmlWriter writer,
        SettingsAdministrationSnapshot? settings)
    {
        if (settings is null)
        {
            throw new InvalidOperationException("Backup settings payload was not supplied.");
        }

        var properties = new (string Name, long LongValue, string StringValue)[]
        {
            ("hostname", 0, settings.HostName),
            ("welcomesmtp", 0, settings.WelcomeSmtp),
            ("welcomepop3", 0, settings.WelcomePop3),
            ("welcomeimap", 0, settings.WelcomeImap),
            ("maxsmtpconnections", settings.MaxSmtpConnections, string.Empty),
            ("maxpop3connections", settings.MaxPop3Connections, string.Empty),
            ("maximapconnections", settings.MaxImapConnections, string.Empty),
            ("maxdelivertythreads", settings.MaxDeliveryThreads, string.Empty),
            ("protocolsmtp", settings.ServiceSmtp ? 1 : 0, string.Empty),
            ("protocolpop3", settings.ServicePop3 ? 1 : 0, string.Empty),
            ("protocolimap", settings.ServiceImap ? 1 : 0, string.Empty),
            ("smtpnoofretries", settings.SmtpNoOfTries, string.Empty),
            ("smtpminutesbetweenretries", settings.SmtpMinutesBetweenTry, string.Empty),
            ("maxmessagesize", settings.MaxMessageSize, string.Empty),
            ("maxsmtprecipientsinbatch", settings.MaxSmtpRecipientsInBatch, string.Empty),
            ("disconnectinvalidclients", settings.DisconnectInvalidClients ? 1 : 0, string.Empty),
            ("maximumincorrectcommands", settings.MaxNumberOfInvalidCommands, string.Empty),
            ("enableimapsort", settings.ImapSortEnabled ? 1 : 0, string.Empty),
            ("enableimapquota", settings.ImapQuotaEnabled ? 1 : 0, string.Empty),
            ("enableimapidle", settings.ImapIdleEnabled ? 1 : 0, string.Empty),
            ("enableimapacl", settings.ImapAclEnabled ? 1 : 0, string.Empty),
            ("EnableImapSASLPlain", settings.ImapSaslPlainEnabled ? 1 : 0, string.Empty),
            ("EnableImapSASLInitialResponse", settings.ImapSaslInitialResponseEnabled ? 1 : 0, string.Empty),
            ("imappublicfoldername", 0, settings.ImapPublicFolderName),
            ("IMAPHierarchyDelimiter", 0, settings.ImapHierarchyDelimiter),
            ("authallowplaintext", settings.AllowSmtpAuthPlain ? 1 : 0, string.Empty),
            ("allowmailfromnull", settings.AllowMailFromNull ? 1 : 0, string.Empty),
            ("smtpallowincorrectlineendings", settings.AllowIncorrectLineEndings ? 1 : 0, string.Empty),
            ("adddeliveredtoheader", settings.AddDeliveredToHeader ? 1 : 0, string.Empty),
            ("mirroremailaddress", 0, settings.MirrorEmailAddress),
            ("defaultdomain", 0, settings.DefaultDomain),
            ("smtpdeliverybindtoip", 0, settings.SmtpDeliveryBindToIp),
            ("rulelooplimit", settings.RuleLoopLimit, string.Empty),
            ("workerthreadpriority", settings.WorkerThreadPriority, string.Empty),
            ("tcpipthreads", settings.TcpIpThreads, string.Empty),
            ("MaxNumberOfMXHosts", settings.MaxNumberOfMxHosts, string.Empty),
            ("VerifyRemoteSslCertificate", settings.VerifyRemoteSslCertificate ? 1 : 0, string.Empty),
            ("SslCipherList", 0, settings.SslCipherList),
            ("IPv6Preferred", settings.Ipv6PreferredEnabled ? 1 : 0, string.Empty),
            ("AutoBanOnLogonFailureEnabled", settings.AutoBanOnLogonFailure ? 1 : 0, string.Empty),
            ("MaxInvalidLogonAttempts", settings.MaxInvalidLogonAttempts, string.Empty),
            ("LogonAttemptsWithinMinutes", settings.MaxInvalidLogonAttemptsWithin, string.Empty),
            ("AutoBanMinutes", settings.AutoBanMinutes, string.Empty),
            ("smtprelayer", 0, settings.SmtpRelayer),
            ("usesmtprelayerauthentication", settings.SmtpRelayerRequiresAuthentication ? 1 : 0, string.Empty),
            ("smtprelayerusername", 0, settings.SmtpRelayerUsername),
            ("smtprelayerport", settings.SmtpRelayerPort, string.Empty),
            ("smtprelayerconnectionsecurity", settings.SmtpRelayerConnectionSecurity, string.Empty),
            ("SmtpDeliveryConnectionSecurity", settings.SmtpConnectionSecurity, string.Empty),
            ("SslVersions", settings.SslVersions, string.Empty),
            ("TlsOptions", settings.TlsOptions, string.Empty),
            ("ImapMasterUser", 0, settings.ImapMasterUser),
            ("MaxNumberOfAsynchronousTasks", settings.MaxAsynchronousThreads, string.Empty),
            ("logging", settings.LoggingMask, string.Empty),
            ("logdevice", settings.LogDevice, string.Empty),
            ("logformat", settings.LogFormat, string.Empty),
            ("awstatsenabled", settings.AwStatsEnabled ? 1 : 0, string.Empty),
            ("usescriptserver", settings.UseScriptServer ? 1 : 0, string.Empty),
            ("scriptlanguage", 0, settings.ScriptLanguage),
            ("backupdestination", 0, settings.BackupDestination),
            ("backupoptions", settings.BackupOptions, string.Empty),
            ("avclamwinenable", settings.AntiVirusClamWinEnabled ? 1 : 0, string.Empty),
            ("avclamwinexec", 0, settings.AntiVirusClamWinExecutable),
            ("avclamwindb", 0, settings.AntiVirusClamWinDatabase),
            ("avaction", settings.AntiVirusAction, string.Empty),
            ("avnotifyreceiver", settings.AntiVirusNotifyReceiver ? 1 : 0, string.Empty),
            ("avnotifysender", settings.AntiVirusNotifySender ? 1 : 0, string.Empty),
            ("usecustomvirusscanner", settings.AntiVirusCustomScannerEnabled ? 1 : 0, string.Empty),
            ("customvirusscannerexecutable", 0, settings.AntiVirusCustomScannerExecutable),
            ("customviursscannerreturnvalue", settings.AntiVirusCustomScannerReturnValue, string.Empty),
            ("avmaxmsgsize", settings.AntiVirusMaximumMessageSize, string.Empty),
            ("enableattachmentblocking", settings.AntiVirusEnableAttachmentBlocking ? 1 : 0, string.Empty),
            ("ClamAVEnabled", settings.AntiVirusClamAvEnabled ? 1 : 0, string.Empty),
            ("ClamAVHost", 0, settings.AntiVirusClamAvHost),
            ("ClamAVPort", settings.AntiVirusClamAvPort, string.Empty),
            ("usegreylisting", settings.AntiSpamGreyListingEnabled ? 1 : 0, string.Empty),
            ("greylistinginitialdelay", settings.AntiSpamGreyListingInitialDelay, string.Empty),
            ("greylistinginitialdelete", settings.AntiSpamGreyListingInitialDelete, string.Empty),
            ("greylistingfinaldelete", settings.AntiSpamGreyListingFinalDelete, string.Empty),
            ("ascheckhostinhelo", settings.AntiSpamCheckHostInHelo ? 1 : 0, string.Empty),
            ("ascheckhostinheloscore", settings.AntiSpamCheckHostInHeloScore, string.Empty),
            ("ascheckptr", settings.AntiSpamCheckPtr ? 1 : 0, string.Empty),
            ("ascheckptrscore", settings.AntiSpamCheckPtrScore, string.Empty),
            ("antispamaddheaderspam", settings.AntiSpamAddHeaderSpam ? 1 : 0, string.Empty),
            ("antispamaddheaderreason", settings.AntiSpamAddHeaderReason ? 1 : 0, string.Empty),
            ("antispamprependsubject", settings.AntiSpamPrependSubject ? 1 : 0, string.Empty),
            ("antispamprependsubjecttext", 0, settings.AntiSpamPrependSubjectText),
            ("spammarkthreshold", settings.AntiSpamSpamMarkThreshold, string.Empty),
            ("spamdeletethreshold", settings.AntiSpamSpamDeleteThreshold, string.Empty),
            ("usespf", settings.AntiSpamUseSpf ? 1 : 0, string.Empty),
            ("usespfscore", settings.AntiSpamUseSpfScore, string.Empty),
            ("usemxchecks", settings.AntiSpamUseMxChecks ? 1 : 0, string.Empty),
            ("usemxchecksscore", settings.AntiSpamUseMxChecksScore, string.Empty),
            ("spamassassinenabled", settings.AntiSpamSpamAssassinEnabled ? 1 : 0, string.Empty),
            ("spamassassinscore", settings.AntiSpamSpamAssassinScore, string.Empty),
            ("spamassassinmergescore", settings.AntiSpamSpamAssassinMergeScore ? 1 : 0, string.Empty),
            ("spamassassinhost", 0, settings.AntiSpamSpamAssassinHost),
            ("spamassassinport", settings.AntiSpamSpamAssassinPort, string.Empty),
            ("antispammaxsize", settings.AntiSpamMaximumMessageSize, string.Empty),
            ("ASDKIMVerificationEnabled", settings.AntiSpamDkimVerificationEnabled ? 1 : 0, string.Empty),
            ("ASDKIMVerificationFailureScore", settings.AntiSpamDkimVerificationFailureScore, string.Empty),
            ("BypassGreylistingOnSPFSuccess", settings.AntiSpamBypassGreylistingOnSpfSuccess ? 1 : 0, string.Empty),
            ("BypassGreylistingOnMailFromMX", settings.AntiSpamBypassGreylistingOnMailFromMx ? 1 : 0, string.Empty),
            ("usecache", settings.CacheEnabled ? 1 : 0, string.Empty),
            ("domaincachettl", settings.DomainCacheTtl, string.Empty),
            ("accountcachettl", settings.AccountCacheTtl, string.Empty),
            ("aliascachettl", settings.AliasCacheTtl, string.Empty),
            ("distributionlistcachettl", settings.DistributionListCacheTtl, string.Empty)
        };

        Array.Sort(
            properties,
            static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));

        writer.WriteStartElement("Properties");
        foreach (var property in properties)
        {
            WriteProperty(writer, property.Name, property.LongValue, property.StringValue);
        }

        writer.WriteEndElement();
    }

    private static void WriteDomains(
        XmlWriter writer,
        IReadOnlyList<DomainAdministrationSnapshot>? domains,
        IReadOnlyDictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>? domainAliases,
        IReadOnlyDictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>? accounts,
        IReadOnlyDictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>? backupAccounts,
        IReadOnlyDictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>? fetchAccounts,
        IReadOnlyDictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>? normalAliases,
        IReadOnlyDictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>? distributionLists,
        IReadOnlyDictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>? distributionListRecipients,
        IReadOnlyDictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>? backupFetchAccounts,
        IReadOnlyDictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>? rules,
        IReadOnlyDictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? ruleCriterias,
        IReadOnlyDictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>? ruleActions,
        IReadOnlyDictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>? folders,
        IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>? folderMessages,
        bool includeFolderMetadata)
    {
        if (domains is null)
        {
            throw new InvalidOperationException("Backup domain payload was not supplied.");
        }

        if (domains.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("Domains");
        foreach (var domain in domains)
        {
            writer.WriteStartElement("Domain");
            writer.WriteAttributeString("Name", domain.Name);
            writer.WriteAttributeString("Postmaster", domain.Postmaster);
            writer.WriteAttributeString("ADDomainName", domain.AdDomainName);
            writer.WriteAttributeString("Active", (domain.Active ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("MaxMessageSize", domain.MaxMessageSize.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("MaxSize", domain.MaxSize.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("MaxAccountSize", domain.MaxAccountSize.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("UsePlusAddressing", (domain.PlusAddressingEnabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("PlusAddressingChar", domain.PlusAddressingCharacter);
            writer.WriteAttributeString("AntiSpamOptions", GetAntiSpamOptions(domain).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("EnableSignature", (domain.SignatureEnabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("SignatureMethod", domain.SignatureMethod.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("SignaturePlainText", domain.SignaturePlainText);
            writer.WriteAttributeString("SignatureHTML", domain.SignatureHtml);
            writer.WriteAttributeString("AddSignaturesToLocalMail", (domain.AddSignaturesToLocalMail ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("AddSignaturesToReplies", (domain.AddSignaturesToReplies ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("MaxNoOfAccounts", domain.MaxNumberOfAccounts.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("MaxNoOfAliases", domain.MaxNumberOfAliases.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("MaxNoOfLists", domain.MaxNumberOfDistributionLists.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("LimitationsEnabled", GetLimitations(domain).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("DKIMSelector", domain.DkimSelector);
            writer.WriteAttributeString("DKIMPrivateKeyFile", domain.DkimPrivateKeyFile);
            if (domainAliases is not null
                && domainAliases.TryGetValue(domain.Id, out var aliases)
                && aliases.Count > 0)
            {
                writer.WriteStartElement("DomainAliases");
                foreach (var alias in aliases)
                {
                    writer.WriteStartElement("DomainAlias");
                    writer.WriteAttributeString("Name", alias.AliasName);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            IReadOnlyList<AccountAdministrationSnapshot>? domainAccounts = null;
            var hasDomainAccounts = accounts is not null
                && accounts.TryGetValue(domain.Id, out domainAccounts);
            IReadOnlyList<AccountBackupAdministrationSnapshot>? selectedBackupAccounts = null;
            var hasBackupDomainAccounts = backupAccounts is not null
                && backupAccounts.TryGetValue(domain.Id, out selectedBackupAccounts);
            if ((hasDomainAccounts && domainAccounts!.Count > 0)
                || (hasBackupDomainAccounts && selectedBackupAccounts!.Count > 0))
            {
                writer.WriteStartElement("Accounts");
                var domainAccountsForXml = hasDomainAccounts
                    ? domainAccounts!
                    : selectedBackupAccounts!.Select(static account => account.Account).ToArray();
                foreach (var account in domainAccountsForXml)
                {
                    var backupAccount = hasBackupDomainAccounts
                        ? selectedBackupAccounts!.FirstOrDefault(
                            candidate => candidate.Account.Id == account.Id)
                        : null;
                    writer.WriteStartElement("Account");
                    writer.WriteAttributeString("Name", account.Address);
                    writer.WriteAttributeString("PersonFirstName", account.PersonFirstName);
                    writer.WriteAttributeString("PersonLastName", account.PersonLastName);
                    writer.WriteAttributeString("Active", (account.Active ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    if (backupAccount is not null)
                    {
                        writer.WriteAttributeString("Password", backupAccount.Password);
                        writer.WriteAttributeString(
                            "PasswordEncryption",
                            backupAccount.PasswordEncryption.ToString(CultureInfo.InvariantCulture));
                    }
                    writer.WriteAttributeString("MaxAccountSize", account.MaxSize.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("ADUsername", account.ActiveDirectoryUsername);
                    writer.WriteAttributeString("ADDomain", account.ActiveDirectoryDomain);
                    writer.WriteAttributeString("ADActive", (account.IsActiveDirectoryAccount ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("VacationMessageOn", (account.VacationMessageIsOn ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("VacationMessage", account.VacationMessage);
                    writer.WriteAttributeString("VacationSubject", account.VacationSubject);
                    writer.WriteAttributeString("VacationExpires", (account.VacationMessageExpires ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("VacationExpireDate", account.VacationMessageExpiresDate);
                    writer.WriteAttributeString("VacationAbortSpamFlagged", (account.VacationMessageAbortSpamFlagged ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("AdminLevel", account.AdminLevel.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("ForwardEnabled", (account.ForwardEnabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("ForwardAddress", account.ForwardAddress);
                    writer.WriteAttributeString("ForwardKeepOriginal", (account.ForwardKeepOriginal ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("ForwardAbortSpamFlagged", (account.ForwardAbortSpamFlagged ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("EnableSignature", (account.SignatureEnabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("SignaturePlainText", account.SignaturePlainText);
                    writer.WriteAttributeString("SignatureHTML", account.SignatureHtml);
                    writer.WriteAttributeString("LastLogonTime", account.LastLogonTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    IReadOnlyList<FetchAccountBackupAdministrationSnapshot>? accountBackupFetchAccounts = null;
                    var hasBackupFetchAccounts = backupFetchAccounts is not null
                        && backupFetchAccounts.TryGetValue(account.Id, out accountBackupFetchAccounts);
                    IReadOnlyList<FetchAccountAdministrationSnapshot>? accountFetchAccounts = null;
                    var hasNormalFetchAccounts = fetchAccounts is not null
                        && fetchAccounts.TryGetValue(account.Id, out accountFetchAccounts);
                    var selectedFetchAccounts = hasBackupFetchAccounts
                        ? accountBackupFetchAccounts!.Select(static fetchAccount => fetchAccount.Account).ToArray()
                        : hasNormalFetchAccounts
                            ? accountFetchAccounts!
                            : Array.Empty<FetchAccountAdministrationSnapshot>();
                    if (selectedFetchAccounts.Count > 0)
                    {
                        writer.WriteStartElement("FetchAccounts");
                        foreach (var fetchAccount in selectedFetchAccounts)
                        {
                            var backupFetchAccount = hasBackupFetchAccounts
                                ? accountBackupFetchAccounts!.First(
                                    candidate => candidate.Account.Id == fetchAccount.Id)
                                : null;
                            writer.WriteStartElement("FetchAccount");
                            writer.WriteAttributeString("Name", fetchAccount.Name);
                            writer.WriteAttributeString("ServerAddress", fetchAccount.ServerAddress);
                            writer.WriteAttributeString("ServerType", fetchAccount.ServerType.ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("Port", fetchAccount.Port.ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("Username", fetchAccount.Username);
                            if (backupFetchAccount is not null)
                            {
                                writer.WriteAttributeString("Password", backupFetchAccount.Password);
                            }
                            writer.WriteAttributeString("Minutes", fetchAccount.MinutesBetweenFetch.ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("DaysToKeep", fetchAccount.DaysToKeepMessages.ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("Active", (fetchAccount.Enabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("MIMERecipientHeaders", fetchAccount.MimeRecipientHeaders);
                            writer.WriteAttributeString("ProcessMIMERecipients", (fetchAccount.ProcessMimeRecipients ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("ProcessMIMEDate", (fetchAccount.ProcessMimeDate ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("UseAntiSpam", (fetchAccount.UseAntiSpam ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("UseAntiVirus", (fetchAccount.UseAntiVirus ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("EnableRouteRecipients", (fetchAccount.EnableRouteRecipients ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("ConnectionSecurity", fetchAccount.ConnectionSecurity.ToString(CultureInfo.InvariantCulture));
                            if (backupFetchAccount is not null && backupFetchAccount.Uids.Count > 0)
                            {
                                writer.WriteStartElement("FetchAccountUIDs");
                                foreach (var uid in backupFetchAccount.Uids)
                                {
                                    writer.WriteStartElement("UID");
                                    writer.WriteAttributeString("UID", uid.Value);
                                    writer.WriteAttributeString("Date", uid.Date);
                                    writer.WriteEndElement();
                                }

                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                    }

                    WriteRules(
                        writer,
                        account.Id,
                        rules,
                        ruleCriterias,
                        ruleActions);
                    WriteFolders(writer, account.Id, folders, folderMessages, includeFolderMetadata);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            if (normalAliases is not null
                && normalAliases.TryGetValue(domain.Id, out var domainAliasesSnapshot)
                && domainAliasesSnapshot.Count > 0)
            {
                writer.WriteStartElement("Aliases");
                foreach (var alias in domainAliasesSnapshot)
                {
                    writer.WriteStartElement("Alias");
                    writer.WriteAttributeString("Name", alias.Name);
                    writer.WriteAttributeString("Value", alias.Value);
                    writer.WriteAttributeString(
                        "Active",
                        (alias.Active ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            if (distributionLists is not null
                && distributionLists.TryGetValue(domain.Id, out var domainDistributionLists)
                && domainDistributionLists.Count > 0)
            {
                writer.WriteStartElement("DistributionLists");
                foreach (var distributionList in domainDistributionLists)
                {
                    writer.WriteStartElement("DistributionList");
                    writer.WriteAttributeString("Name", distributionList.Address);
                    writer.WriteAttributeString(
                        "Active",
                        (distributionList.Active ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString(
                        "RequiresAuth",
                        (distributionList.RequireSmtpAuth ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("RequiresAuthAddress", distributionList.RequireSenderAddress);
                    writer.WriteAttributeString(
                        "ListMode",
                        distributionList.Mode.ToString(CultureInfo.InvariantCulture));

                    if (distributionListRecipients is not null
                        && distributionListRecipients.TryGetValue(distributionList.Id, out var recipients)
                        && recipients.Count > 0)
                    {
                        writer.WriteStartElement("Recipients");
                        foreach (var recipient in recipients)
                        {
                            writer.WriteStartElement("Recipient");
                            writer.WriteAttributeString("Name", recipient.Address);
                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteFolders(
        XmlWriter writer,
        int accountId,
        IReadOnlyDictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>? folders,
        IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>? folderMessages,
        bool includeFolderMetadata)
    {
        if (!includeFolderMetadata
            || folders is null
            || !folders.TryGetValue(accountId, out var accountFolders)
            || accountFolders.Count == 0)
        {
            return;
        }

        ValidateFolderSnapshot(accountFolders);
        var foldersByParentId = accountFolders
            .GroupBy(static folder => folder.ParentId)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        if (!foldersByParentId.TryGetValue(-1, out var rootFolders)
            || rootFolders.Length == 0)
        {
            return;
        }

        writer.WriteStartElement("Folders");
        foreach (var folder in rootFolders)
        {
            WriteFolder(writer, folder, foldersByParentId, folderMessages);
        }

        writer.WriteEndElement();
    }

    internal static void ValidateFolderSnapshot(
        IReadOnlyList<ImapFolderAdministrationSnapshot> accountFolders)
    {
        var foldersById = new Dictionary<int, ImapFolderAdministrationSnapshot>();
        foreach (var folder in accountFolders)
        {
            if (!foldersById.TryAdd(folder.Id, folder))
            {
                throw new InvalidOperationException(
                    "The backup folder snapshot contains a duplicate folder ID: " + folder.Id);
            }
        }

        if (!accountFolders.Any(static folder => folder.ParentId == -1))
        {
            throw new InvalidOperationException(
                "The backup folder snapshot does not contain a root folder.");
        }

        foreach (var folder in accountFolders)
        {
            if (folder.ParentId != -1 && !foldersById.ContainsKey(folder.ParentId))
            {
                throw new InvalidOperationException(
                    "The backup folder snapshot contains an orphaned parent ID: "
                    + folder.ParentId);
            }
        }

        var visited = new HashSet<int>();
        var visiting = new HashSet<int>();
        foreach (var folder in accountFolders)
        {
            VisitFolder(folder.Id);
        }

        void VisitFolder(int folderId)
        {
            if (visited.Contains(folderId))
            {
                return;
            }

            if (!visiting.Add(folderId))
            {
                throw new InvalidOperationException(
                    "The backup folder snapshot contains a parent cycle at folder ID: "
                    + folderId);
            }

            var parentId = foldersById[folderId].ParentId;
            if (parentId != -1)
            {
                VisitFolder(parentId);
            }

            visiting.Remove(folderId);
            visited.Add(folderId);
        }
    }

    private static void WriteFolder(
        XmlWriter writer,
        ImapFolderAdministrationSnapshot folder,
        IReadOnlyDictionary<int, ImapFolderAdministrationSnapshot[]> foldersByParentId,
        IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>? folderMessages)
    {
        writer.WriteStartElement("Folder");
        WriteLegacyAttribute(writer, "Name", folder.Name);
        WriteLegacyAttribute(writer, "Subscribed", (folder.Subscribed ? 1 : 0).ToString(CultureInfo.InvariantCulture));
        WriteLegacyAttribute(writer, "CreateTime", folder.CreationTime);
        WriteLegacyAttribute(writer, "CurrentUID", folder.CurrentUid.ToString(CultureInfo.InvariantCulture));
        WriteMessages(writer, folder.Id, folderMessages);
        if (foldersByParentId.TryGetValue(folder.Id, out var childFolders)
            && childFolders.Length > 0)
        {
            writer.WriteStartElement("Folders");
            foreach (var childFolder in childFolders)
            {
                WriteFolder(writer, childFolder, foldersByParentId, folderMessages);
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteMessages(
        XmlWriter writer,
        int folderId,
        IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>? folderMessages)
    {
        if (folderMessages is null
            || !folderMessages.TryGetValue(folderId, out var messages)
            || messages.Count == 0)
        {
            return;
        }

        WriteMessages(writer, messages);
    }

    private static void WriteMessages(
        XmlWriter writer,
        IReadOnlyList<MessageAdministrationSnapshot> messages)
    {
        writer.WriteStartElement("Messages");
        foreach (var message in messages)
        {
            writer.WriteStartElement("Message");
            WriteLegacyAttribute(writer, "CreateTime", message.InternalDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "Filename", Path.GetFileName(message.FileName));
            WriteLegacyAttribute(writer, "FromAddress", message.FromAddress);
            WriteLegacyAttribute(writer, "State", message.State.ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "Size", message.SizeBytes.ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "NoOfRetries", message.CurrentNumberOfTries.ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "Flags", message.Flags.ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "ID", message.Id.ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "UID", message.Uid.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WritePublicFolders(
        XmlWriter writer,
        IReadOnlyList<BackupPublicFolderEntry>? publicFolders)
    {
        if (publicFolders is null || publicFolders.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("PublicFolders");
        foreach (var folder in publicFolders)
        {
            WritePublicFolder(writer, folder);
        }

        writer.WriteEndElement();
    }

    private static void WritePublicFolder(
        XmlWriter writer,
        BackupPublicFolderEntry entry)
    {
        writer.WriteStartElement("Folder");
        WriteLegacyAttribute(writer, "Name", entry.Folder.Name);
        WriteLegacyAttribute(writer, "Subscribed", (entry.Folder.Subscribed ? 1 : 0).ToString(CultureInfo.InvariantCulture));
        WriteLegacyAttribute(writer, "CreateTime", entry.Folder.CreationTime);
        WriteLegacyAttribute(writer, "CurrentUID", entry.Folder.CurrentUid.ToString(CultureInfo.InvariantCulture));
        WriteMessages(writer, entry.Messages);
        if (entry.Children.Count > 0)
        {
            writer.WriteStartElement("Folders");
            foreach (var child in entry.Children)
            {
                WritePublicFolder(writer, child);
            }

            writer.WriteEndElement();
        }

        if (entry.Permissions.Count > 0)
        {
            writer.WriteStartElement("ACLs");
            foreach (var permission in entry.Permissions)
            {
                writer.WriteStartElement("Permission");
                WriteLegacyAttribute(writer, "Type", permission.PermissionType.ToString(CultureInfo.InvariantCulture));
                WriteLegacyAttribute(writer, "Rights", permission.Rights.ToString(CultureInfo.InvariantCulture));
                WriteLegacyAttribute(writer, "Holder", permission.Holder);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteGroups(
        XmlWriter writer,
        IReadOnlyList<BackupGroupEntry>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("Groups");
        foreach (var entry in groups)
        {
            writer.WriteStartElement("Group");
            WriteLegacyAttribute(writer, "Name", entry.Group.Name);
            if (entry.MemberNames.Count > 0)
            {
                writer.WriteStartElement("GroupMembers");
                foreach (var memberName in entry.MemberNames)
                {
                    writer.WriteStartElement("Member");
                    WriteLegacyAttribute(writer, "Name", memberName);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteRules(
        XmlWriter writer,
        int accountId,
        IReadOnlyDictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>? rules,
        IReadOnlyDictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? ruleCriterias,
        IReadOnlyDictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>? ruleActions)
    {
        if (rules is null
            || !rules.TryGetValue(accountId, out var accountRules)
            || accountRules.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("Rules");
        foreach (var rule in accountRules)
        {
            writer.WriteStartElement("Rule");
            WriteLegacyAttribute(writer, "Name", rule.Name);
            WriteLegacyAttribute(writer, "Active", (rule.Active ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "UseAND", (rule.UseAnd ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            WriteLegacyAttribute(writer, "SortOrder", rule.SortOrder.ToString(CultureInfo.InvariantCulture));

            if (ruleCriterias is not null
                && ruleCriterias.TryGetValue(rule.Id, out var criterias)
                && criterias.Count > 0)
            {
                writer.WriteStartElement("RuleCriterias");
                foreach (var criteria in criterias)
                {
                    writer.WriteStartElement("Criteria");
                    WriteLegacyAttribute(writer, "MatchString", criteria.MatchValue);
                    WriteLegacyAttribute(writer, "FieldType", criteria.PredefinedField.ToString(CultureInfo.InvariantCulture));
                    WriteLegacyAttribute(writer, "MatchType", criteria.MatchType.ToString(CultureInfo.InvariantCulture));
                    WriteLegacyAttribute(writer, "HeaderField", criteria.HeaderField);
                    WriteLegacyAttribute(writer, "UsePredefinedField", (criteria.UsePredefined ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            if (ruleActions is not null
                && ruleActions.TryGetValue(rule.Id, out var actions)
                && actions.Count > 0)
            {
                writer.WriteStartElement("RuleActions");
                foreach (var action in actions)
                {
                    writer.WriteStartElement("Action");
                    WriteLegacyAttribute(writer, "Type", action.Type.ToString(CultureInfo.InvariantCulture));
                    WriteLegacyAttribute(writer, "Subject", action.Subject);
                    WriteLegacyAttribute(writer, "Body", action.Body);
                    WriteLegacyAttribute(writer, "FromAddress", action.FromAddress);
                    WriteLegacyAttribute(writer, "FromName", action.FromName);
                    WriteLegacyAttribute(writer, "IMAPFolder", action.ImapFolder);
                    WriteLegacyAttribute(writer, "FileName", action.Filename);
                    WriteLegacyAttribute(writer, "To", action.To);
                    WriteLegacyAttribute(writer, "ScriptFunction", action.ScriptFunction);
                    WriteLegacyAttribute(writer, "SortOrder", action.SortOrder.ToString(CultureInfo.InvariantCulture));
                    WriteLegacyAttribute(writer, "Header", action.HeaderName);
                    WriteLegacyAttribute(writer, "Value", action.Value);
                    WriteLegacyAttribute(writer, "RouteID", action.RouteId.ToString(CultureInfo.InvariantCulture));
                    WriteLegacyAttribute(writer, "AbortSpamFlagged", (action.AbortSpamFlagged ? 1 : 0).ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteLegacyAttribute(XmlWriter writer, string name, string? value)
    {
        writer.WriteStartAttribute(name);
        writer.WriteRaw(
            (value ?? string.Empty)
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&apos;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal));
        writer.WriteEndAttribute();
    }

    private static void WriteProperty(
        XmlWriter writer,
        string name,
        long longValue = 0,
        string stringValue = "")
    {
        writer.WriteStartElement(name);
        writer.WriteAttributeString("LongValue", longValue.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("StringValue", stringValue);
        writer.WriteEndElement();
    }

    private static int GetLimitations(DomainAdministrationSnapshot domain) =>
        (domain.MaxNumberOfAccountsEnabled ? 1 : 0)
        | (domain.MaxNumberOfAliasesEnabled ? 2 : 0)
        | (domain.MaxNumberOfDistributionListsEnabled ? 4 : 0);

    private static int GetAntiSpamOptions(DomainAdministrationSnapshot domain) =>
        (domain.AntiSpamEnableGreylisting ? 1 : 0)
        | (domain.DkimSignEnabled ? 2 : 0)
        | (domain.DkimHeaderCanonicalizationMethod == 1 ? 4 : 0)
        | (domain.DkimBodyCanonicalizationMethod == 1 ? 8 : 0)
        | (domain.DkimSigningAlgorithm == 1 ? 16 : 0)
        | (domain.DkimSignAliasesEnabled ? 32 : 0);

    private async ValueTask AddFileAsync(
        string archivePath,
        string metadataPath,
        CancellationToken cancellationToken)
        => await AddToArchiveAsync(
            archivePath,
            metadataPath,
            recurse: false,
            cancellationToken).ConfigureAwait(false);

    private async ValueTask AddDirectoryAsync(
        string archivePath,
        string dataBackupPath,
        CancellationToken cancellationToken)
        => await AddToArchiveAsync(
            archivePath,
            dataBackupPath + Path.DirectorySeparatorChar,
            recurse: true,
            cancellationToken).ConfigureAwait(false);

    private async ValueTask AddToArchiveAsync(
        string archivePath,
        string inputPath,
        bool recurse,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _sevenZipExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("a");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add(inputPath);
        if (recurse)
        {
            startInfo.ArgumentList.Add("-r");
        }

        startInfo.ArgumentList.Add("-t7z");
        startInfo.ArgumentList.Add("-mmt");
        startInfo.ArgumentList.Add("-mx1");

        using var process = _processStarter(startInfo)
            ?? throw new InvalidOperationException("The legacy 7z writer could not be started.");
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            _ = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode is not 0 and not 1)
            {
                throw new InvalidDataException(
                    $"The legacy 7z writer failed with exit code {process.ExitCode}: {error.Trim()}");
            }
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            throw;
        }
    }
}

[ComVisible(false)]
public sealed record BackupArchiveXmlPayload(
    SettingsAdministrationSnapshot? Settings,
    IReadOnlyList<DomainAdministrationSnapshot>? Domains,
    IReadOnlyList<BackupSettingsPropertySnapshot>? SettingsProperties = null,
    IReadOnlyDictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>? DomainAliases = null,
    IReadOnlyDictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>? Accounts = null,
    IReadOnlyDictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>? Aliases = null,
    IReadOnlyDictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>? DistributionLists = null,
    IReadOnlyDictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>? DistributionListRecipients = null,
    IReadOnlyDictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>? BackupAccounts = null,
    IReadOnlyDictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>? FetchAccounts = null,
    IReadOnlyDictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>? BackupFetchAccounts = null,
    IReadOnlyDictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>? Rules = null,
    IReadOnlyDictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? RuleCriterias = null,
    IReadOnlyDictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>? RuleActions = null,
    IReadOnlyDictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>? Folders = null,
    IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>? FolderMessages = null,
    IReadOnlyList<BackupPublicFolderEntry>? PublicFolders = null,
    IReadOnlyList<BackupGroupEntry>? Groups = null);

[ComVisible(false)]
public sealed record BackupPublicFolderPermission(
    int PermissionType,
    int Rights,
    string Holder);

[ComVisible(false)]
public sealed record BackupPublicFolderEntry(
    ImapFolderAdministrationSnapshot Folder,
    IReadOnlyList<BackupPublicFolderEntry> Children,
    IReadOnlyList<MessageAdministrationSnapshot> Messages,
    IReadOnlyList<BackupPublicFolderPermission> Permissions);

[ComVisible(false)]
public sealed record BackupGroupEntry(
    GroupAdministrationSnapshot Group,
    IReadOnlyList<string> MemberNames);

[ComVisible(false)]
public sealed class BackupXmlPayloadRuntime
{
    private readonly ISettingsAdministrationStore _settingsStore;
    private readonly IDomainAdministrationStore _domainStore;
    private readonly IDomainAliasAdministrationStore _domainAliasStore;
    private readonly IAccountAdministrationStore _accountStore;
    private readonly IBackupAccountAdministrationStore? _backupAccountStore;
    private readonly IFetchAccountAdministrationStore? _fetchAccountStore;
    private readonly IBackupFetchAccountAdministrationStore? _backupFetchAccountStore;
    private readonly IBackupRuleAdministrationStore? _backupRuleStore;
    private readonly IRuleAdministrationStore? _ruleStore;
    private readonly IRuleCriteriaAdministrationStore? _ruleCriteriaStore;
    private readonly IRuleActionAdministrationStore? _ruleActionStore;
    private readonly IImapFolderAdministrationStore? _folderStore;
    private readonly IImapFolderAdministrationRestoreStore? _folderRestoreStore;
    private readonly IMessageAdministrationStore? _messageStore;
    private readonly IMessageAdministrationBackupStore? _messageBackupStore;
    private readonly IAliasAdministrationStore _aliasStore;
    private readonly IDistributionListAdministrationStore? _distributionListStore;
    private readonly IDistributionListRecipientAdministrationStore? _distributionListRecipientStore;
    private readonly IGroupAdministrationStore? _groupStore;
    private readonly IGroupMemberAdministrationStore? _groupMemberStore;
    private readonly IBackupDomainProjectionSnapshotFactory? _domainProjectionSnapshotFactory;
    private readonly IBackupRestoreMetadataTransactionFactory? _metadataTransactionFactory;
    private readonly bool _requireSqlTransaction;

    public BackupXmlPayloadRuntime(
        ISettingsAdministrationStore settingsStore,
        IDomainAdministrationStore domainStore,
        IDomainAliasAdministrationStore domainAliasStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore)
        : this(
            settingsStore,
            domainStore,
            domainAliasStore,
            accountStore,
            aliasStore,
            distributionListStore: null,
            distributionListRecipientStore: null,
            backupAccountStore: null,
            fetchAccountStore: null,
            backupFetchAccountStore: null,
            backupRuleStore: null,
            ruleStore: null,
            ruleCriteriaStore: null,
            ruleActionStore: null,
            folderStore: null,
            folderRestoreStore: null,
            messageStore: null,
            metadataTransactionFactory: null,
            requireSqlTransaction: false,
            groupStore: null)
    {
    }

    public BackupXmlPayloadRuntime(
        ISettingsAdministrationStore settingsStore,
        IDomainAdministrationStore domainStore,
        IDomainAliasAdministrationStore domainAliasStore,
        IAccountAdministrationStore accountStore,
        IAliasAdministrationStore aliasStore,
        IDistributionListAdministrationStore? distributionListStore,
        IDistributionListRecipientAdministrationStore? distributionListRecipientStore,
        IBackupAccountAdministrationStore? backupAccountStore = null,
        IFetchAccountAdministrationStore? fetchAccountStore = null,
        IBackupFetchAccountAdministrationStore? backupFetchAccountStore = null,
        IBackupRuleAdministrationStore? backupRuleStore = null,
        IRuleAdministrationStore? ruleStore = null,
        IRuleCriteriaAdministrationStore? ruleCriteriaStore = null,
        IRuleActionAdministrationStore? ruleActionStore = null,
        IImapFolderAdministrationStore? folderStore = null,
        IImapFolderAdministrationRestoreStore? folderRestoreStore = null,
        IMessageAdministrationStore? messageStore = null,
        IBackupRestoreMetadataTransactionFactory? metadataTransactionFactory = null,
        bool requireSqlTransaction = false,
        IGroupAdministrationStore? groupStore = null,
        IGroupMemberAdministrationStore? groupMemberStore = null,
        IBackupDomainProjectionSnapshotFactory? domainProjectionSnapshotFactory = null)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(domainStore);
        ArgumentNullException.ThrowIfNull(domainAliasStore);
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(aliasStore);
        _settingsStore = settingsStore;
        _domainStore = domainStore;
        _domainAliasStore = domainAliasStore;
        _accountStore = accountStore;
        _backupAccountStore = backupAccountStore;
        _fetchAccountStore = fetchAccountStore;
        _backupFetchAccountStore = backupFetchAccountStore;
        _backupRuleStore = backupRuleStore;
        _ruleStore = ruleStore;
        _ruleCriteriaStore = ruleCriteriaStore;
        _ruleActionStore = ruleActionStore;
        _folderStore = folderStore;
        _folderRestoreStore = folderRestoreStore;
        _messageStore = messageStore;
        _messageBackupStore = messageStore as IMessageAdministrationBackupStore;
        _aliasStore = aliasStore;
        _distributionListStore = distributionListStore;
        _distributionListRecipientStore = distributionListRecipientStore;
        _groupStore = groupStore;
        _groupMemberStore = groupMemberStore;
        _domainProjectionSnapshotFactory = domainProjectionSnapshotFactory;
        _metadataTransactionFactory = metadataTransactionFactory;
        _requireSqlTransaction = requireSqlTransaction;
    }

    public async ValueTask<BackupArchiveXmlPayload> GetPayloadAsync(
        BackupStartPlanEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var includesDomainMessages =
            (evidence.BackupOptions & BackupStartPlan.BackupMessagesFlag) != 0;
        var includesSettings =
            (evidence.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0;
        var canSnapshotMessages = !includesDomainMessages || evidence.BackupMessagesDbOnly;
        if (_domainProjectionSnapshotFactory is not null
            && (evidence.BackupOptions & BackupStartPlan.BackupDomainsFlag) != 0
            && canSnapshotMessages)
        {
            return await GetDomainOnlyPayloadFromSnapshotAsync(
                includesDomainMessages,
                includesSettings,
                cancellationToken).ConfigureAwait(false);
        }

        var settings = (evidence.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0
            ? evidence.Settings
                ?? await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var settingsProperties =
            (evidence.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0
                ? evidence.BackupSettingsProperties
                : null;
        var domains = (evidence.BackupOptions & BackupStartPlan.BackupDomainsFlag) != 0
            ? await _domainStore.GetDomainsAsync(cancellationToken).ConfigureAwait(false)
            : null;
        IReadOnlyDictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>? domainAliases = null;
        IReadOnlyDictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>? accounts = null;
        IReadOnlyDictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>? backupAccounts = null;
        IReadOnlyDictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>? fetchAccounts = null;
        IReadOnlyDictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>? backupFetchAccounts = null;
        IReadOnlyDictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>? rules = null;
        IReadOnlyDictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>? ruleCriterias = null;
        IReadOnlyDictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>? ruleActions = null;
        IReadOnlyDictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>? folders = null;
        IReadOnlyDictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>? folderMessages = null;
        IReadOnlyDictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>? aliases = null;
        IReadOnlyDictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>? distributionLists = null;
        IReadOnlyDictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>? distributionListRecipients = null;
        IReadOnlyList<BackupPublicFolderEntry>? publicFolders = null;
        IReadOnlyList<BackupGroupEntry>? backupGroups = null;
        if (domains is not null)
        {
            var aliasesByDomainId = new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>();
            var accountsByDomainId = new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>();
            var backupAccountsByDomainId = _backupAccountStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>();
            var fetchAccountsByAccountId = _fetchAccountStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<FetchAccountAdministrationSnapshot>>();
            var backupFetchAccountsByAccountId = _backupFetchAccountStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>();
            var rulesByAccountId = _backupRuleStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>();
            var ruleCriteriasByRuleId = _ruleCriteriaStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>();
            var ruleActionsByRuleId = _ruleActionStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>();
            var foldersByAccountId = _folderStore is null
                || (evidence.BackupOptions & BackupStartPlan.BackupMessagesFlag) == 0
                ? null
                : new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>();
            var folderMessagesByFolderId = _messageStore is null
                || (evidence.BackupOptions & BackupStartPlan.BackupMessagesFlag) == 0
                ? null
                : new Dictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>();
            var normalAliasesByDomainId = new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>();
            var distributionListsByDomainId = _distributionListStore is null
                ? null
                : new Dictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>();
            var selectedDistributionListIds = new List<int>();
            var selectedDistributionListIdSet = new HashSet<int>();
            foreach (var domain in domains)
            {
                if (!aliasesByDomainId.ContainsKey(domain.Id))
                {
                    aliasesByDomainId[domain.Id] = await _domainAliasStore
                        .GetDomainAliasesAsync(domain.Id, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (backupAccountsByDomainId is not null
                    && !backupAccountsByDomainId.ContainsKey(domain.Id))
                {
                    var domainBackupAccounts = await _backupAccountStore!
                        .GetBackupAccountsAsync(domain.Id, cancellationToken)
                        .ConfigureAwait(false);
                    backupAccountsByDomainId[domain.Id] = domainBackupAccounts;
                    accountsByDomainId[domain.Id] = domainBackupAccounts
                        .Select(static account => account.Account)
                        .ToArray();
                }
                else if (!accountsByDomainId.ContainsKey(domain.Id))
                {
                    accountsByDomainId[domain.Id] = await _accountStore
                        .GetAccountsAsync(domain.Id, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (backupFetchAccountsByAccountId is not null)
                {
                    foreach (var account in accountsByDomainId[domain.Id])
                    {
                        if (!backupFetchAccountsByAccountId.ContainsKey(account.Id))
                        {
                            backupFetchAccountsByAccountId[account.Id] = await _backupFetchAccountStore!
                                .GetBackupFetchAccountsAsync(account.Id, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                }
                else if (fetchAccountsByAccountId is not null)
                {
                    foreach (var account in accountsByDomainId[domain.Id])
                    {
                        if (!fetchAccountsByAccountId.ContainsKey(account.Id))
                        {
                            fetchAccountsByAccountId[account.Id] = await _fetchAccountStore!
                                .GetFetchAccountsAsync(account.Id, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                }

                if (rulesByAccountId is not null)
                {
                    foreach (var account in accountsByDomainId[domain.Id])
                    {
                        if (rulesByAccountId.ContainsKey(account.Id))
                        {
                            continue;
                        }

                        var accountRules = (await _backupRuleStore!
                                .GetBackupRulesAsync(account.Id, cancellationToken)
                                .ConfigureAwait(false))
                            .Where(rule => rule.AccountId == account.Id)
                            .ToArray();
                        rulesByAccountId[account.Id] = accountRules;
                        foreach (var rule in accountRules)
                        {
                            if (ruleCriteriasByRuleId is not null && !ruleCriteriasByRuleId.ContainsKey(rule.Id))
                            {
                                ruleCriteriasByRuleId[rule.Id] = await _ruleCriteriaStore!
                                    .GetRuleCriteriaAsync(rule.Id, cancellationToken)
                                    .ConfigureAwait(false);
                            }

                            if (ruleActionsByRuleId is not null && !ruleActionsByRuleId.ContainsKey(rule.Id))
                            {
                                ruleActionsByRuleId[rule.Id] = await _ruleActionStore!
                                    .GetRuleActionsAsync(rule.Id, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                }

                if (foldersByAccountId is not null)
                {
                    foreach (var account in accountsByDomainId[domain.Id])
                    {
                        if (!foldersByAccountId.ContainsKey(account.Id))
                        {
                            var accountFolders = await _folderStore!
                                .GetFoldersForAccountAsync(account.Id, cancellationToken)
                                .ConfigureAwait(false);
                            foldersByAccountId[account.Id] = accountFolders;
                            if (folderMessagesByFolderId is not null)
                            {
                                foreach (var folder in accountFolders)
                                {
                                    if (!folderMessagesByFolderId.ContainsKey(folder.Id))
                                    {
                                        folderMessagesByFolderId[folder.Id] = await GetFolderMessagesForBackupAsync(
                                                account.Id,
                                                folder.Id,
                                                cancellationToken)
                                            .ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                }

                if (!normalAliasesByDomainId.ContainsKey(domain.Id))
                {
                    normalAliasesByDomainId[domain.Id] = await _aliasStore
                        .GetAliasesAsync(domain.Id, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (distributionListsByDomainId is not null
                    && !distributionListsByDomainId.ContainsKey(domain.Id))
                {
                    var domainDistributionLists = await _distributionListStore!
                        .GetDistributionListsAsync(domain.Id, cancellationToken)
                        .ConfigureAwait(false);
                    distributionListsByDomainId[domain.Id] = domainDistributionLists;
                    foreach (var distributionList in domainDistributionLists)
                    {
                        if (selectedDistributionListIdSet.Add(distributionList.Id))
                        {
                            selectedDistributionListIds.Add(distributionList.Id);
                        }
                    }
                }
            }

            domainAliases = aliasesByDomainId;
            accounts = accountsByDomainId;
            backupAccounts = backupAccountsByDomainId;
            fetchAccounts = fetchAccountsByAccountId;
            backupFetchAccounts = backupFetchAccountsByAccountId;
            rules = rulesByAccountId;
            ruleCriterias = ruleCriteriasByRuleId;
            ruleActions = ruleActionsByRuleId;
            folders = foldersByAccountId;
            folderMessages = folderMessagesByFolderId;
            aliases = normalAliasesByDomainId;
            distributionLists = distributionListsByDomainId;

            if (_distributionListRecipientStore is not null && distributionListsByDomainId is not null)
            {
                var recipientsByDistributionListId = new Dictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>();
                foreach (var distributionListId in selectedDistributionListIds)
                {
                    recipientsByDistributionListId[distributionListId] = await _distributionListRecipientStore
                        .GetRecipientsAsync(distributionListId, cancellationToken)
                        .ConfigureAwait(false);
                }

                distributionListRecipients = recipientsByDistributionListId;
            }
        }

        if ((evidence.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0
            && _groupStore is not null)
        {
            var groupSnapshots = await _groupStore
                .GetGroupsAsync(cancellationToken)
                .ConfigureAwait(false);
            backupGroups = await BuildGroupsAsync(groupSnapshots, cancellationToken).ConfigureAwait(false);
        }

        if ((evidence.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0
            && _folderStore is not null)
        {
            var publicFolderSnapshots = await _folderStore
                .GetFoldersForAccountAsync(0, cancellationToken)
                .ConfigureAwait(false);
            if (publicFolderSnapshots.Count > 0)
            {
                var groups = _groupStore is null
                    ? Array.Empty<GroupAdministrationSnapshot>()
                    : await _groupStore.GetGroupsAsync(cancellationToken).ConfigureAwait(false);
                publicFolders = await BuildPublicFoldersAsync(
                    publicFolderSnapshots,
                    groups,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return new BackupArchiveXmlPayload(
            settings,
            domains,
            settingsProperties,
            domainAliases,
            accounts,
            aliases,
            distributionLists,
            distributionListRecipients,
            backupAccounts,
            fetchAccounts,
            backupFetchAccounts,
            rules,
            ruleCriterias,
            ruleActions,
            folders,
            folderMessages,
            publicFolders,
            backupGroups);
    }

    private async ValueTask<BackupArchiveXmlPayload> GetDomainOnlyPayloadFromSnapshotAsync(
        bool includeMessages,
        bool includeSettings,
        CancellationToken cancellationToken)
    {
        await using var snapshot = await _domainProjectionSnapshotFactory!
            .BeginAsync(cancellationToken)
            .ConfigureAwait(false);

        var domains = await snapshot.DomainStore
            .GetDomainsAsync(cancellationToken)
            .ConfigureAwait(false);
        var settings = includeSettings
            ? await snapshot.SettingsStore
                .GetSettingsAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;
        var settingsProperties = includeSettings
            ? await snapshot.BackupSettingsPropertyStore
                .GetBackupSettingsPropertiesAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;
        var domainAliases = new Dictionary<int, IReadOnlyList<DomainAliasAdministrationSnapshot>>();
        var accounts = new Dictionary<int, IReadOnlyList<AccountAdministrationSnapshot>>();
        var backupAccounts = new Dictionary<int, IReadOnlyList<AccountBackupAdministrationSnapshot>>();
        var backupFetchAccounts = new Dictionary<int, IReadOnlyList<FetchAccountBackupAdministrationSnapshot>>();
        var rules = new Dictionary<int, IReadOnlyList<RuleAdministrationSnapshot>>();
        var ruleCriterias = new Dictionary<int, IReadOnlyList<RuleCriteriaAdministrationSnapshot>>();
        var ruleActions = new Dictionary<int, IReadOnlyList<RuleActionAdministrationSnapshot>>();
        var folders = includeMessages
            ? new Dictionary<int, IReadOnlyList<ImapFolderAdministrationSnapshot>>()
            : null;
        var folderMessages = includeMessages
            ? new Dictionary<int, IReadOnlyList<MessageAdministrationSnapshot>>()
            : null;
        var aliases = new Dictionary<int, IReadOnlyList<AliasAdministrationSnapshot>>();
        var distributionLists = new Dictionary<int, IReadOnlyList<DistributionListAdministrationSnapshot>>();
        var recipients = new Dictionary<int, IReadOnlyList<DistributionListRecipientAdministrationSnapshot>>();
        IReadOnlyList<BackupGroupEntry>? backupGroups = null;
        IReadOnlyList<BackupPublicFolderEntry>? publicFolders = null;

        foreach (var domain in domains)
        {
            domainAliases[domain.Id] = await snapshot.DomainAliasStore
                .GetDomainAliasesAsync(domain.Id, cancellationToken)
                .ConfigureAwait(false);
            var domainBackupAccounts = await snapshot.BackupAccountStore
                .GetBackupAccountsAsync(domain.Id, cancellationToken)
                .ConfigureAwait(false);
            backupAccounts[domain.Id] = domainBackupAccounts;
            accounts[domain.Id] = domainBackupAccounts
                .Select(static account => account.Account)
                .ToArray();
            foreach (var account in accounts[domain.Id])
            {
                backupFetchAccounts[account.Id] = await snapshot.BackupFetchAccountStore
                    .GetBackupFetchAccountsAsync(account.Id, cancellationToken)
                    .ConfigureAwait(false);
                var accountRules = await snapshot.BackupRuleStore
                    .GetBackupRulesAsync(account.Id, cancellationToken)
                    .ConfigureAwait(false);
                rules[account.Id] = accountRules;
                foreach (var rule in accountRules)
                {
                    ruleCriterias[rule.Id] = await snapshot.RuleCriteriaStore
                        .GetRuleCriteriaAsync(rule.Id, cancellationToken)
                        .ConfigureAwait(false);
                    ruleActions[rule.Id] = await snapshot.RuleActionStore
                        .GetRuleActionsAsync(rule.Id, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (includeMessages)
                {
                    var accountFolders = await snapshot.FolderStore
                        .GetFoldersForAccountAsync(account.Id, cancellationToken)
                        .ConfigureAwait(false);
                    folders![account.Id] = accountFolders;
                    foreach (var folder in accountFolders)
                    {
                        folderMessages![folder.Id] = await snapshot.MessageBackupStore
                            .GetFolderMessagesForBackupAsync(account.Id, folder.Id, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            aliases[domain.Id] = await snapshot.AliasStore
                .GetAliasesAsync(domain.Id, cancellationToken)
                .ConfigureAwait(false);
            var domainLists = await snapshot.DistributionListStore
                .GetDistributionListsAsync(domain.Id, cancellationToken)
                .ConfigureAwait(false);
            distributionLists[domain.Id] = domainLists;
            foreach (var distributionList in domainLists)
            {
                recipients[distributionList.Id] = await snapshot.RecipientStore
                    .GetRecipientsAsync(distributionList.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (includeSettings)
        {
            var groups = await snapshot.GroupStore
                .GetGroupsAsync(cancellationToken)
                .ConfigureAwait(false);
            backupGroups = await BuildGroupsAsync(
                groups,
                cancellationToken,
                snapshot.GroupMemberStore,
                snapshot.AccountStore).ConfigureAwait(false);

            var publicFolderSnapshots = await snapshot.FolderStore
                .GetFoldersForAccountAsync(0, cancellationToken)
                .ConfigureAwait(false);
            if (publicFolderSnapshots.Count > 0)
            {
                publicFolders = await BuildPublicFoldersAsync(
                    publicFolderSnapshots,
                    groups,
                    cancellationToken,
                    snapshot.FolderStore,
                    snapshot.MessageBackupStore,
                    snapshot.AccountStore).ConfigureAwait(false);
            }
        }

        return new BackupArchiveXmlPayload(
            Settings: settings,
            Domains: domains,
            SettingsProperties: settingsProperties,
            DomainAliases: domainAliases,
            Accounts: accounts,
            Aliases: aliases,
            DistributionLists: distributionLists,
            DistributionListRecipients: recipients,
            BackupAccounts: backupAccounts,
            BackupFetchAccounts: backupFetchAccounts,
            Rules: rules,
            RuleCriterias: ruleCriterias,
            RuleActions: ruleActions,
            Folders: folders,
            FolderMessages: folderMessages,
            PublicFolders: publicFolders,
            Groups: backupGroups);
    }

    private async ValueTask<IReadOnlyList<BackupGroupEntry>> BuildGroupsAsync(
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        CancellationToken cancellationToken,
        IGroupMemberAdministrationStore? groupMemberStore = null,
        IAccountAdministrationStore? accountStore = null)
    {
        groupMemberStore ??= _groupMemberStore;
        accountStore ??= _accountStore;
        var result = new List<BackupGroupEntry>(groups.Count);
        foreach (var group in groups)
        {
            var memberNames = new List<string>();
            if (groupMemberStore is not null)
            {
                var members = await groupMemberStore
                    .GetGroupMembersAsync(group.Id, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var member in members)
                {
                    var account = await accountStore
                        .GetAccountByIdAsync(member.AccountId, cancellationToken)
                        .ConfigureAwait(false);
                    if (account is null)
                    {
                        throw new InvalidDataException(
                            $"The group member account {member.AccountId} could not be resolved for group '{group.Name}'.");
                    }

                    memberNames.Add(account.Address);
                }
            }

            result.Add(new BackupGroupEntry(group, memberNames));
        }

        return result;
    }

    private async ValueTask<IReadOnlyList<BackupPublicFolderEntry>> BuildPublicFoldersAsync(
        IReadOnlyList<ImapFolderAdministrationSnapshot> folders,
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        CancellationToken cancellationToken,
        IImapFolderAdministrationStore? folderStore = null,
        IMessageAdministrationBackupStore? messageBackupStore = null,
        IAccountAdministrationStore? accountStore = null)
    {
        SevenZipBackupArchiveRuntime.ValidateFolderSnapshot(folders);
        if (folders.Any(static folder => folder.AccountId != 0))
        {
            throw new InvalidDataException(
                "The public-folder backup snapshot contains a non-public account ID.");
        }

        var foldersByParentId = folders
            .GroupBy(static folder => folder.ParentId)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var accountCache = new Dictionary<int, AccountAdministrationSnapshot?>();
        var roots = foldersByParentId.TryGetValue(-1, out var rootFolders)
            ? rootFolders
            : Array.Empty<ImapFolderAdministrationSnapshot>();
        var result = new List<BackupPublicFolderEntry>(roots.Length);
        foreach (var root in roots)
        {
            result.Add(await BuildPublicFolderAsync(
                root,
                foldersByParentId,
                groups,
                accountCache,
                cancellationToken,
                folderStore,
                messageBackupStore,
                accountStore).ConfigureAwait(false));
        }

        return result;
    }

    private async ValueTask<BackupPublicFolderEntry> BuildPublicFolderAsync(
        ImapFolderAdministrationSnapshot folder,
        IReadOnlyDictionary<int, ImapFolderAdministrationSnapshot[]> foldersByParentId,
        IReadOnlyList<GroupAdministrationSnapshot> groups,
        IDictionary<int, AccountAdministrationSnapshot?> accountCache,
        CancellationToken cancellationToken,
        IImapFolderAdministrationStore? folderStore = null,
        IMessageAdministrationBackupStore? messageBackupStore = null,
        IAccountAdministrationStore? accountStore = null)
    {
        folderStore ??= _folderStore;
        messageBackupStore ??= _messageBackupStore;
        accountStore ??= _accountStore;
        var messages = messageBackupStore is null
            ? Array.Empty<MessageAdministrationSnapshot>()
            : await messageBackupStore.GetFolderMessagesForBackupAsync(0, folder.Id, cancellationToken).ConfigureAwait(false);
        var permissions = await folderStore!
            .GetFolderPermissionsAsync(folder.Id, cancellationToken)
            .ConfigureAwait(false);
        var publicPermissions = new List<BackupPublicFolderPermission>(permissions.Count);
        foreach (var permission in permissions)
        {
            var holder = permission.PermissionType switch
            {
                0 => await ResolveAccountHolderAsync(permission.PermissionAccountId, accountCache, cancellationToken, accountStore).ConfigureAwait(false),
                1 => groups.FirstOrDefault(group => group.Id == permission.PermissionGroupId)?.Name,
                2 => "Anyone",
                _ => null
            };
            if (string.IsNullOrWhiteSpace(holder))
            {
                throw new InvalidDataException(
                    "The public-folder ACL holder could not be resolved for permission type "
                    + permission.PermissionType + ".");
            }

            publicPermissions.Add(new BackupPublicFolderPermission(
                permission.PermissionType,
                permission.Value,
                holder));
        }

        var children = new List<BackupPublicFolderEntry>();
        if (foldersByParentId.TryGetValue(folder.Id, out var childFolders))
        {
            foreach (var child in childFolders)
            {
                children.Add(await BuildPublicFolderAsync(
                    child,
                    foldersByParentId,
                    groups,
                    accountCache,
                    cancellationToken,
                    folderStore,
                    messageBackupStore,
                    accountStore).ConfigureAwait(false));
            }
        }

        return new BackupPublicFolderEntry(folder, children, messages, publicPermissions);
    }

    private ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesForBackupAsync(
        int accountId,
        int folderId,
        CancellationToken cancellationToken)
    {
        if (_messageBackupStore is not null)
        {
            return _messageBackupStore.GetFolderMessagesForBackupAsync(accountId, folderId, cancellationToken);
        }

        return _messageStore!.GetFolderMessagesAsync(accountId, folderId, cancellationToken);
    }

    private async ValueTask<string?> ResolveAccountHolderAsync(
        int accountId,
        IDictionary<int, AccountAdministrationSnapshot?> accountCache,
        CancellationToken cancellationToken,
        IAccountAdministrationStore? accountStore = null)
    {
        accountStore ??= _accountStore;
        if (!accountCache.TryGetValue(accountId, out var account))
        {
            account = await accountStore.GetAccountByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
            accountCache[accountId] = account;
        }

        return account?.Address;
    }

    internal void ConfigureRestoreRuntime(
        string sevenZipExecutablePath,
        string dataDirectory,
        Func<CancellationToken, ValueTask>? restoreReinitializer = null)
    {
        if (_distributionListStore is null || _distributionListRecipientStore is null)
        {
            return;
        }

        BackupRestoreRuntimeHost.Configure(
            new MetadataBackupRestoreExecutor(
                sevenZipExecutablePath,
                dataDirectory,
                _domainStore,
                _accountStore,
                _aliasStore,
                _distributionListStore,
                _distributionListRecipientStore,
                metadataTransactionFactory: _metadataTransactionFactory,
                requireSqlTransaction: _requireSqlTransaction,
                fetchAccountStore: _fetchAccountStore,
                ruleStore: _ruleStore,
                ruleCriteriaStore: _ruleCriteriaStore,
                ruleActionStore: _ruleActionStore,
                folderRestoreStore: _folderRestoreStore,
                folderRestoreDeletionStore: _folderStore as IImapFolderAdministrationRestoreDeletionStore,
                messageRestoreStore: _messageStore as IMessageAdministrationRestoreStore,
                 messageStore: _messageStore,
                 groupStore: _groupStore,
                 groupMemberStore: _groupMemberStore,
                 reinitialize: restoreReinitializer,
                requireReinitialize: true));
    }
}
