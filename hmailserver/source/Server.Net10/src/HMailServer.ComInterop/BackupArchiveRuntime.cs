using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

/// <summary>
/// Creates the legacy metadata-only backup archive. Payload serialization is
/// deliberately fenced until the corresponding read-only providers exist.
/// </summary>
[ComVisible(false)]
public sealed class SevenZipBackupArchiveRuntime
{
    private readonly string _sevenZipExecutablePath;
    private readonly string _applicationVersion;
    private readonly Func<DateTime> _localNow;
    private readonly Func<BackupStartPlanEvidence, CancellationToken, ValueTask<BackupArchiveXmlPayload>>? _payloadProvider;

    public SevenZipBackupArchiveRuntime(
        string sevenZipExecutablePath,
        string applicationVersion,
        Func<DateTime>? localNow = null,
        Func<BackupStartPlanEvidence, CancellationToken, ValueTask<BackupArchiveXmlPayload>>? payloadProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        ArgumentNullException.ThrowIfNull(applicationVersion);

        _sevenZipExecutablePath = sevenZipExecutablePath;
        _applicationVersion = applicationVersion;
        _localNow = localNow ?? (static () => DateTime.Now);
        _payloadProvider = payloadProvider;
    }

    public async ValueTask CreateAsync(
        BackupStartPlanEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if ((evidence.BackupOptions & BackupStartPlan.BackupMessagesFlag) != 0)
        {
            throw new NotSupportedException(
                "Backup message payload serialization is not implemented yet.");
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

        var destination = BackupStartPlanRuntime.NormalizeDestination(evidence.Destination);
        if (!Directory.Exists(destination))
        {
            throw new InvalidOperationException(
                "The specified backup directory is not accessible: " + destination);
        }

        var timestamp = _localNow().ToString(
            "yyyy-MM-dd HHmmss",
            CultureInfo.InvariantCulture);
        var archivePath = Path.Combine(destination, $"HMBackup {timestamp}.7z");
        var metadataPath = Path.Combine(destination, SevenZipBackupArchiveMetadataReader.MetadataEntryName);

        try
        {
            var metadata = CreateMetadataXml(evidence.BackupOptions, _applicationVersion, payload);
            await File.WriteAllTextAsync(
                metadataPath,
                metadata,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
            await AddFileAsync(
                archivePath,
                metadataPath,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }
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
            writer.WriteEndElement();
            if ((backupOptions & BackupStartPlan.BackupSettingsFlag) != 0)
            {
                WriteSettings(writer, payload?.Settings);
            }

            if ((backupOptions & BackupStartPlan.BackupDomainsFlag) != 0)
            {
                WriteDomains(writer, payload?.Domains);
            }

            writer.WriteEndElement();
        }

        return builder.ToString();
    }

    private static void WriteSettings(XmlWriter writer, SettingsAdministrationSnapshot? settings)
    {
        if (settings is null)
        {
            throw new InvalidOperationException("Backup settings payload was not supplied.");
        }

        writer.WriteStartElement("Properties");
        WriteProperty(writer, "hostname", stringValue: settings.HostName);
        WriteProperty(writer, "welcomesmtp", stringValue: settings.WelcomeSmtp);
        WriteProperty(writer, "welcomepop3", stringValue: settings.WelcomePop3);
        WriteProperty(writer, "welcomeimap", stringValue: settings.WelcomeImap);
        WriteProperty(writer, "backupdestination", stringValue: settings.BackupDestination);
        WriteProperty(writer, "backupoptions", longValue: settings.BackupOptions);
        WriteProperty(writer, "maxsmtpconnections", longValue: settings.MaxSmtpConnections);
        WriteProperty(writer, "maxpop3connections", longValue: settings.MaxPop3Connections);
        WriteProperty(writer, "maximapconnections", longValue: settings.MaxImapConnections);
        WriteProperty(writer, "maxdelivertythreads", longValue: settings.MaxDeliveryThreads);
        WriteProperty(writer, "maxmessagesize", longValue: settings.MaxMessageSize);
        WriteProperty(writer, "maxsmtprecipientsinbatch", longValue: settings.MaxSmtpRecipientsInBatch);
        WriteProperty(writer, "defaultdomain", stringValue: settings.DefaultDomain);
        WriteProperty(writer, "smtprelayer", stringValue: settings.SmtpRelayer);
        WriteProperty(writer, "smtprelayerusername", stringValue: settings.SmtpRelayerUsername);
        WriteProperty(writer, "smtprelayerport", longValue: settings.SmtpRelayerPort);
        WriteProperty(writer, "smtprelayerconnectionsecurity", longValue: settings.SmtpRelayerConnectionSecurity);
        WriteProperty(writer, "usecache", longValue: settings.CacheEnabled ? 1 : 0);
        WriteProperty(writer, "domaincachettl", longValue: settings.DomainCacheTtl);
        WriteProperty(writer, "accountcachettl", longValue: settings.AccountCacheTtl);
        WriteProperty(writer, "aliascachettl", longValue: settings.AliasCacheTtl);
        WriteProperty(writer, "distributionlistcachettl", longValue: settings.DistributionListCacheTtl);
        writer.WriteEndElement();
    }

    private static void WriteDomains(
        XmlWriter writer,
        IReadOnlyList<DomainAdministrationSnapshot>? domains)
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
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
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
        startInfo.ArgumentList.Add(metadataPath);
        startInfo.ArgumentList.Add("-t7z");
        startInfo.ArgumentList.Add("-mmt");
        startInfo.ArgumentList.Add("-mx1");

        using var process = Process.Start(startInfo)
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
    IReadOnlyList<DomainAdministrationSnapshot>? Domains);

[ComVisible(false)]
public sealed class BackupXmlPayloadRuntime
{
    private readonly ISettingsAdministrationStore _settingsStore;
    private readonly IDomainAdministrationStore _domainStore;

    public BackupXmlPayloadRuntime(
        ISettingsAdministrationStore settingsStore,
        IDomainAdministrationStore domainStore)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(domainStore);
        _settingsStore = settingsStore;
        _domainStore = domainStore;
    }

    public async ValueTask<BackupArchiveXmlPayload> GetPayloadAsync(
        BackupStartPlanEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var settings = (evidence.BackupOptions & BackupStartPlan.BackupSettingsFlag) != 0
            ? evidence.Settings
                ?? await _settingsStore.GetSettingsAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var domains = (evidence.BackupOptions & BackupStartPlan.BackupDomainsFlag) != 0
            ? await _domainStore.GetDomainsAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return new BackupArchiveXmlPayload(settings, domains);
    }
}
