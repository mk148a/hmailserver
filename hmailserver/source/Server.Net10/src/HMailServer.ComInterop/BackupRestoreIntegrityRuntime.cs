using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(false)]
internal sealed class BackupRestoreIntegrityRuntime
{
    private const long MaximumMetadataCharacters = 1024 * 1024;
    private const int MaximumListingCharacters = 4 * 1024 * 1024;
    private static readonly TimeSpan MaximumCommandDuration = TimeSpan.FromSeconds(30);
    private const string DataBackupFolderName = "DataBackup";

    private readonly string _sevenZipExecutablePath;

    internal BackupRestoreIntegrityRuntime(string sevenZipExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sevenZipExecutablePath);
        _sevenZipExecutablePath = sevenZipExecutablePath;
    }

    internal async ValueTask<BackupRestoreIntegrityEvidence> InspectAsync(
        string archivePath,
        CancellationToken cancellationToken,
        bool backupMessagesDbOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        var fullArchivePath = Path.GetFullPath(archivePath);
        var evidence = new BackupRestoreIntegrityEvidence(fullArchivePath)
        {
            BackupMessagesDbOnly = backupMessagesDbOnly
        };
        if (Directory.Exists(fullArchivePath))
        {
            return evidence.Invalid("The restore payload must be a file, not a directory.");
        }

        if (!File.Exists(fullArchivePath))
        {
            return evidence.Invalid("The restore payload file does not exist.");
        }

        var testResult = await RunAsync(
                CreateStartInfo("t", fullArchivePath),
                MaximumListingCharacters,
                cancellationToken)
            .ConfigureAwait(false);
        evidence = evidence with { ArchiveTestPassed = testResult.ExitCode == 0 };
        if (testResult.ExitCode != 0)
        {
            return evidence.Invalid("The 7z archive integrity test failed.");
        }

        var listResult = await RunAsync(
                CreateStartInfo("l", fullArchivePath, "-slt"),
                MaximumListingCharacters,
                cancellationToken)
            .ConfigureAwait(false);
        if (listResult.ExitCode != 0)
        {
            return evidence.Invalid("The 7z archive contents could not be listed.");
        }

        var entries = ParseArchiveEntries(listResult.StandardOutput);
        var directoryEntries = ParseArchiveDirectoryEntries(listResult.StandardOutput);
        evidence = evidence with { ArchiveEntries = entries };
        var unsafeEntry = FindUnsafeArchiveEntry(entries);
        if (unsafeEntry is not null)
        {
            return evidence.Invalid("The archive contains an unsafe entry: " + unsafeEntry);
        }

        var metadataEntry = entries.FirstOrDefault(
            static entry => string.Equals(
                NormalizeArchiveEntry(entry),
                SevenZipBackupArchiveMetadataReader.MetadataEntryName,
                StringComparison.OrdinalIgnoreCase));
        if (metadataEntry is null)
        {
            return evidence.Invalid("The archive does not contain hMailServerBackup.xml.");
        }

        var metadataResult = await RunAsync(
                SevenZipBackupArchiveMetadataReader.CreateStartInfo(
                    _sevenZipExecutablePath,
                    fullArchivePath),
                (int)MaximumMetadataCharacters,
                cancellationToken)
            .ConfigureAwait(false);
        if (metadataResult.ExitCode != 0 || string.IsNullOrWhiteSpace(metadataResult.StandardOutput))
        {
            return evidence with
            {
                MetadataPresent = true,
                FailureReason = "hMailServerBackup.xml could not be read from the archive."
            };
        }

        BackupRestoreMetadata metadata;
        try
        {
            metadata = ParseMetadata(metadataResult.StandardOutput);
        }
        catch (XmlException)
        {
            return evidence with
            {
                MetadataPresent = true,
                FailureReason = "hMailServerBackup.xml is malformed or uses a prohibited DTD."
            };
        }

        evidence = evidence with
        {
            MetadataPresent = true,
            MetadataXmlValid = true,
            BackupOptions = metadata.BackupOptions,
            DataFilesPresent = metadata.DataFilesPresent,
            DataFilesFormat = metadata.DataFilesFormat,
            RawFolderName = metadata.RawFolderName
        };

        var containsMessages = (metadata.BackupOptions & BackupStartPlan.BackupMessagesFlag) != 0;
        if (containsMessages != metadata.DataFilesPresent)
        {
            return evidence.Invalid(
                containsMessages
                    ? "BackupInformation Mode contains BOMessages but DataFiles is missing."
                    : "DataFiles is present but BackupInformation Mode does not contain BOMessages.");
        }

        if (metadata.DataFilesPresent
            && ((metadata.BackupOptions & BackupStartPlan.BackupCompressionFlag) != 0)
                != string.Equals(metadata.DataFilesFormat, "7z", StringComparison.OrdinalIgnoreCase))
        {
            return evidence.Invalid("The DataFiles format is inconsistent with the backup compression mode.");
        }

        if (metadata.DomainAccountGraphFailureReason is not null)
        {
            return evidence.Invalid(metadata.DomainAccountGraphFailureReason);
        }

        if (metadata.DataFilesFormat is null)
        {
            return ValidateArchiveShape(evidence, metadata.DataFilesFormat);
        }

        if (string.Equals(metadata.DataFilesFormat, "7z", StringComparison.OrdinalIgnoreCase))
        {
            var containsDomains = (metadata.BackupOptions & BackupStartPlan.BackupDomainsFlag) != 0;
            var permitsMissingDataBackup =
                backupMessagesDbOnly
                && (metadata.BackupOptions & BackupStartPlan.BackupMessagesFlag) != 0;
            var containsDataBackup = directoryEntries.Contains(DataBackupFolderName, StringComparer.OrdinalIgnoreCase)
                || entries.Any(static entry => IsDataBackupEntry(entry));
            if (containsDataBackup && (backupMessagesDbOnly || !containsDomains))
            {
                return evidence.Invalid(
                    backupMessagesDbOnly
                        ? "The DB-only compressed payload must not contain DataBackup entries."
                        : "The compressed DataBackup payload requires BODomains and BOMessages.");
            }

            if (!permitsMissingDataBackup
                && !directoryEntries.Contains(DataBackupFolderName, StringComparer.OrdinalIgnoreCase))
            {
                return evidence.Invalid("The compressed payload does not contain a DataBackup directory.");
            }

            if (entries.Any(static entry =>
                    !string.Equals(
                        NormalizeArchiveEntry(entry),
                        SevenZipBackupArchiveMetadataReader.MetadataEntryName,
                        StringComparison.OrdinalIgnoreCase)
                    && !IsDataBackupEntry(entry)))
            {
                return evidence.Invalid("The compressed payload contains an entry outside DataBackup.");
            }

            return evidence with { IsValid = true };
        }

        if (string.Equals(metadata.DataFilesFormat, "Raw", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryResolveRawSibling(
                    fullArchivePath,
                    metadata.RawFolderName,
                    allowMissing: backupMessagesDbOnly,
                    out var rawDataBackupPath,
                    out var failureReason))
            {
                return evidence.Invalid(failureReason!);
            }

            return evidence with
            {
                IsValid = true,
                RawDataBackupPath = rawDataBackupPath
            };
        }

        return evidence.Invalid("The backup DataFiles format is not supported.");
    }

    internal static bool IsSafeArchiveEntryPath(string entry)
    {
        return FindUnsafeArchiveEntry(new[] { entry }) is null;
    }

    private static BackupRestoreIntegrityEvidence ValidateArchiveShape(
        BackupRestoreIntegrityEvidence evidence,
        string? dataFilesFormat)
    {
        if (dataFilesFormat is null
            && evidence.ArchiveEntries.Any(static entry =>
                !string.Equals(
                    NormalizeArchiveEntry(entry),
                    SevenZipBackupArchiveMetadataReader.MetadataEntryName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return evidence.Invalid("The archive contains an unexpected payload entry.");
        }

        return evidence with { IsValid = true };
    }

    private static bool TryResolveRawSibling(
        string archivePath,
        string? folderName,
        bool allowMissing,
        out string? rawDataBackupPath,
        out string? failureReason)
    {
        rawDataBackupPath = null;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(folderName))
        {
            failureReason = "The Raw DataFiles FolderName is missing.";
            return false;
        }

        if (Path.IsPathRooted(folderName)
            || folderName.Contains(':')
            || folderName.Contains('\\')
            || folderName.Contains('/')
            || folderName is "." or "..")
        {
            failureReason = "The Raw DataFiles FolderName is not a safe sibling directory name.";
            return false;
        }

        var archiveDirectory = Path.GetDirectoryName(archivePath)!;
        var candidate = Path.GetFullPath(Path.Combine(archiveDirectory, folderName));
        if (!string.Equals(
                Path.GetDirectoryName(candidate),
                archiveDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "The Raw DataFiles FolderName is not a sibling directory.";
            return false;
        }

        if (File.Exists(candidate))
        {
            failureReason = "The Raw DataFiles sibling directory does not exist.";
            return false;
        }

        if (!Directory.Exists(candidate))
        {
            if (allowMissing)
            {
                return true;
            }

            failureReason = "The Raw DataFiles sibling directory does not exist.";
            return false;
        }

        if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
        {
            failureReason = "The Raw DataFiles sibling directory is a reparse point.";
            return false;
        }

        rawDataBackupPath = candidate;
        return true;
    }

    private static string? FindUnsafeArchiveEntry(IEnumerable<string> entries)
    {
        foreach (var entry in entries)
        {
            var normalized = NormalizeArchiveEntry(entry).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.StartsWith("//", StringComparison.Ordinal)
                || (normalized.Length >= 2
                    && char.IsLetter(normalized[0])
                    && normalized[1] == ':'))
            {
                return entry;
            }

            var segments = normalized.Split('/');
            if (segments.Any(static segment =>
                    segment is "" or "." or ".." || segment.Contains(':')))
            {
                return entry;
            }
        }

        return null;
    }

    private static bool IsDataBackupEntry(string entry)
    {
        var normalized = NormalizeArchiveEntry(entry).TrimEnd('/');
        return string.Equals(normalized, DataBackupFolderName, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(DataBackupFolderName + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeArchiveEntry(string entry) => entry.Replace('\\', '/');

    private static ImmutableArray<string> ParseArchiveEntries(string output)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var inEntries = false;
        foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if (line.Trim() == "----------")
            {
                inEntries = true;
                continue;
            }

            if (inEntries && line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                builder.Add(line[7..]);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableHashSet<string> ParseArchiveDirectoryEntries(string output)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        string? currentPath = null;
        foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                currentPath = line[7..];
                continue;
            }

            if (currentPath is not null
                && line.StartsWith("Attributes = ", StringComparison.Ordinal)
                && line[13..].Contains('D'))
            {
                builder.Add(NormalizeArchiveEntry(currentPath).TrimEnd('/'));
                currentPath = null;
            }
        }

        return builder.ToImmutable();
    }

    private static BackupRestoreMetadata ParseMetadata(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumMetadataCharacters,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        var root = document.Root;
        var backupInformation = root?.Element("BackupInformation");
        var modeText = backupInformation?.Attribute("Mode")?.Value;
        if (root?.Name != "Backup"
            || backupInformation is null
            || !int.TryParse(modeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode)
            || mode < 0
            || (mode & ~15) != 0)
        {
            throw new XmlException("The backup metadata shape is invalid.");
        }

        var dataFiles = backupInformation.Element("DataFiles");
        var domainAccountGraphFailureReason = (mode & BackupStartPlan.BackupDomainsFlag) != 0
            ? ValidateDomainAccountGraph(root)
            : null;
        return new BackupRestoreMetadata(
            mode,
            dataFiles is not null,
            dataFiles?.Attribute("Format")?.Value,
            dataFiles?.Attribute("FolderName")?.Value,
            domainAccountGraphFailureReason);
    }

    private static string? ValidateDomainAccountGraph(XElement root)
    {
        var domainNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accountNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.DescendantsAndSelf())
        {
            switch (element.Name.LocalName)
            {
                case "Domains":
                    if (element.Name != "Domains" || element.Parent != root)
                    {
                        return "The domain/account graph is invalid: Domains must be directly under Backup.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Domain"))
                    {
                        return "The domain/account graph is invalid: Domains contains an unexpected child.";
                    }

                    if (root.Elements("Domains").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Backup contains multiple Domains containers.";
                    }

                    break;

                case "Domain":
                    if (element.Name != "Domain" || element.Parent?.Name != "Domains")
                    {
                        return "The domain/account graph is invalid: Domain is in an unexpected location.";
                    }

                    if (string.IsNullOrWhiteSpace(element.Attribute("Name")?.Value))
                    {
                        return "The domain/account graph is invalid: Domain requires a Name attribute.";
                    }

                    if (!domainNames.Add(NormalizeIdentityName(element.Attribute("Name")!.Value)))
                    {
                        return "The domain/account graph is invalid: Domains contains duplicate Domain Name values.";
                    }

                    if (element.Elements("Accounts").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Domain contains multiple Accounts containers.";
                    }

                    break;

                case "Accounts":
                    if (element.Name != "Accounts" || element.Parent?.Name != "Domain")
                    {
                        return "The domain/account graph is invalid: Accounts is in an unexpected location.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Account"))
                    {
                        return "The domain/account graph is invalid: Accounts contains an unexpected child.";
                    }

                    break;

                case "Account":
                    if (element.Name != "Account" || element.Parent?.Name != "Accounts")
                    {
                        return "The domain/account graph is invalid: Account is outside Domain/Accounts.";
                    }

                    if (string.IsNullOrWhiteSpace(element.Attribute("Name")?.Value))
                    {
                        return "The domain/account graph is invalid: Account requires a Name attribute.";
                    }

                    if (!accountNames.Add(NormalizeIdentityName(element.Attribute("Name")!.Value)))
                    {
                        return "The domain/account graph is invalid: Accounts contains duplicate Account Name values.";
                    }

                    break;

                case "FetchAccounts":
                    if (element.Name != "FetchAccounts" || element.Parent?.Name != "Account")
                    {
                        return "The domain/account graph is invalid: FetchAccounts is in an unexpected location.";
                    }

                    if (element.Parent.Elements("FetchAccounts").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Account contains multiple FetchAccounts containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "FetchAccount"))
                    {
                        return "The domain/account graph is invalid: FetchAccounts contains an unexpected child.";
                    }

                    break;

                case "Rules":
                    if (element.Name != "Rules" || element.Parent?.Name != "Account")
                    {
                        return "The domain/account graph is invalid: Rules is in an unexpected location.";
                    }

                    if (element.Parent.Elements("Rules").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Account contains multiple Rules containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Rule"))
                    {
                        return "The domain/account graph is invalid: Rules contains an unexpected child.";
                    }

                    break;

                case "Folders":
                    if (element.Name != "Folders"
                        || (element.Parent?.Name != "Account" && element.Parent?.Name != "Folder"))
                    {
                        return "The domain/account graph is invalid: Folders is in an unexpected location.";
                    }

                    if (element.Parent!.Elements("Folders").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Folder owner contains multiple Folders containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Folder"))
                    {
                        return "The domain/account graph is invalid: Folders contains an unexpected child.";
                    }

                    break;

                case "Messages":
                    if (element.Name != "Messages" || element.Parent?.Name != "Folder")
                    {
                        return "The domain/account graph is invalid: Messages is in an unexpected location.";
                    }

                    if (element.Parent.Elements("Messages").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Folder contains multiple Messages containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Message"))
                    {
                        return "The domain/account graph is invalid: Messages contains an unexpected child.";
                    }

                    break;

                case "FetchAccount":
                    if (element.Parent?.Name != "FetchAccounts")
                    {
                        return "The domain/account graph is invalid: FetchAccount is outside Account/FetchAccounts.";
                    }

                    break;

                case "FetchAccountUIDs":
                    if (element.Name != "FetchAccountUIDs" || element.Parent?.Name != "FetchAccount")
                    {
                        return "The domain/account graph is invalid: FetchAccountUIDs is in an unexpected location.";
                    }

                    if (element.Parent.Elements("FetchAccountUIDs").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: FetchAccount contains multiple FetchAccountUIDs containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "UID"))
                    {
                        return "The domain/account graph is invalid: FetchAccountUIDs contains an unexpected child.";
                    }

                    break;

                case "UID":
                    if (element.Parent?.Name != "FetchAccountUIDs")
                    {
                        return "The domain/account graph is invalid: UID is outside FetchAccount/FetchAccountUIDs.";
                    }

                    if (!HasAttributes(element, "UID", "Date"))
                    {
                        return "The domain/account graph is invalid: UID is missing a serialized attribute.";
                    }

                    break;

                case "Rule":
                    if (element.Parent?.Name != "Rules")
                    {
                        return "The domain/account graph is invalid: Rule is outside Account/Rules.";
                    }

                    break;

                case "RuleCriterias":
                    if (element.Name != "RuleCriterias" || element.Parent?.Name != "Rule")
                    {
                        return "The domain/account graph is invalid: RuleCriterias is in an unexpected location.";
                    }

                    if (element.Parent.Elements("RuleCriterias").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Rule contains multiple RuleCriterias containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Criteria"))
                    {
                        return "The domain/account graph is invalid: RuleCriterias contains an unexpected child.";
                    }

                    break;

                case "RuleActions":
                    if (element.Name != "RuleActions" || element.Parent?.Name != "Rule")
                    {
                        return "The domain/account graph is invalid: RuleActions is in an unexpected location.";
                    }

                    if (element.Parent.Elements("RuleActions").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Rule contains multiple RuleActions containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Action"))
                    {
                        return "The domain/account graph is invalid: RuleActions contains an unexpected child.";
                    }

                    break;

                case "Criteria":
                    if (element.Parent?.Name != "RuleCriterias")
                    {
                        return "The domain/account graph is invalid: Criteria is outside Rule/RuleCriterias.";
                    }

                    if (!HasAttributes(element, "MatchString", "FieldType", "MatchType", "HeaderField", "UsePredefinedField"))
                    {
                        return "The domain/account graph is invalid: Criteria is missing a serialized attribute.";
                    }

                    break;

                case "Action":
                    if (element.Parent?.Name != "RuleActions")
                    {
                        return "The domain/account graph is invalid: Action is outside Rule/RuleActions.";
                    }

                    if (!HasAttributes(element, "Type", "Subject", "Body", "FromAddress", "FromName", "IMAPFolder", "FileName", "To", "ScriptFunction", "SortOrder", "Header", "Value", "RouteID", "AbortSpamFlagged"))
                    {
                        return "The domain/account graph is invalid: Action is missing a serialized attribute.";
                    }

                    break;

                case "Folder":
                    if (element.Parent?.Name != "Folders")
                    {
                        return "The domain/account graph is invalid: Folder is outside Account/Folders.";
                    }

                    if (!HasAttributes(element, "Name", "Subscribed", "CreateTime", "CurrentUID"))
                    {
                        return "The domain/account graph is invalid: Folder is missing a serialized attribute.";
                    }

                    break;

                case "Message":
                    if (element.Parent?.Name != "Messages")
                    {
                        return "The domain/account graph is invalid: Message is outside Folder/Messages.";
                    }

                    if (!HasAttributes(element, "CreateTime", "Filename", "FromAddress", "State", "Size", "NoOfRetries", "Flags", "ID", "UID"))
                    {
                        return "The domain/account graph is invalid: Message is missing a serialized attribute.";
                    }

                    break;

                case "DomainAliases":
                    if (element.Name != "DomainAliases" || element.Parent?.Name != "Domain")
                    {
                        return "The domain/account graph is invalid: DomainAliases is in an unexpected location.";
                    }

                    if (element.Parent.Elements("DomainAliases").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Domain contains multiple DomainAliases containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "DomainAlias"))
                    {
                        return "The domain/account graph is invalid: DomainAliases contains an unexpected child.";
                    }

                    break;

                case "Aliases":
                    if (element.Name != "Aliases" || element.Parent?.Name != "Domain")
                    {
                        return "The domain/account graph is invalid: Aliases is in an unexpected location.";
                    }

                    if (element.Parent.Elements("Aliases").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Domain contains multiple Aliases containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "Alias"))
                    {
                        return "The domain/account graph is invalid: Aliases contains an unexpected child.";
                    }

                    break;

                case "DistributionLists":
                    if (element.Name != "DistributionLists" || element.Parent?.Name != "Domain")
                    {
                        return "The domain/account graph is invalid: DistributionLists is in an unexpected location.";
                    }

                    if (element.Parent.Elements("DistributionLists").Skip(1).Any())
                    {
                        return "The domain/account graph is invalid: Domain contains multiple DistributionLists containers.";
                    }

                    if (element.Elements().Any(static child => child.Name != "DistributionList"))
                    {
                        return "The domain/account graph is invalid: DistributionLists contains an unexpected child.";
                    }

                    break;

                case "DomainAlias":
                    if (element.Parent?.Name != "DomainAliases")
                    {
                        return "The domain/account graph is invalid: DomainAlias is outside Domain/DomainAliases.";
                    }

                    if (element.Attribute("Name") is null)
                    {
                        return "The domain/account graph is invalid: DomainAlias requires a Name attribute.";
                    }

                    break;

                case "Alias":
                    if (element.Parent?.Name != "Aliases")
                    {
                        return "The domain/account graph is invalid: Alias is outside Domain/Aliases.";
                    }

                    if (!HasAttributes(element, "Name", "Value", "Active"))
                    {
                        return "The domain/account graph is invalid: Alias is missing a serialized attribute.";
                    }

                    break;

                case "DistributionList":
                    if (element.Parent?.Name != "DistributionLists")
                    {
                        return "The domain/account graph is invalid: DistributionList is outside Domain/DistributionLists.";
                    }

                    if (!HasAttributes(element, "Name", "Active", "RequiresAuth", "RequiresAuthAddress", "ListMode"))
                    {
                        return "The domain/account graph is invalid: DistributionList is missing a serialized attribute.";
                    }

                    break;
            }
        }

        return null;

        static bool HasAttributes(XElement element, params string[] names)
        {
            return names.All(name => element.Attribute(name) is not null);
        }

        static string NormalizeIdentityName(string value) => value.TrimEnd(' ');
    }

    private ProcessStartInfo CreateStartInfo(
        string command,
        string archivePath,
        string? additionalArgument = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _sevenZipExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(archivePath);
        if (additionalArgument is not null)
        {
            startInfo.ArgumentList.Add(additionalArgument);
        }

        startInfo.ArgumentList.Add("-y");
        return startInfo;
    }

    private static async Task<SevenZipCommandResult> RunAsync(
        ProcessStartInfo startInfo,
        int maximumOutputCharacters,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(MaximumCommandDuration);
        var commandToken = timeoutSource.Token;
        try
        {
            if (!process.Start())
            {
                return new SevenZipCommandResult(-1, string.Empty, "The 7z process could not be started.");
            }

            var outputTask = ReadToEndBoundedAsync(
                process.StandardOutput,
                maximumOutputCharacters,
                commandToken);
            var errorTask = ReadToEndBoundedAsync(
                process.StandardError,
                maximumOutputCharacters,
                commandToken);
            await process.WaitForExitAsync(commandToken).ConfigureAwait(false);
            return new SevenZipCommandResult(
                process.ExitCode,
                await outputTask.ConfigureAwait(false),
                await errorTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit();
            return new SevenZipCommandResult(-1, string.Empty, "The 7z command timed out.");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        catch (InvalidDataException exception)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit();
            return new SevenZipCommandResult(-1, string.Empty, exception.Message);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException)
        {
            return new SevenZipCommandResult(-1, string.Empty, exception.Message);
        }
    }

    private static async Task<string> ReadToEndBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[8192];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return builder.ToString();
            }

            if (builder.Length > maximumCharacters - count)
            {
                throw new InvalidDataException("The 7z command output exceeded its bounded limit.");
            }

            builder.Append(buffer, 0, count);
        }
    }

    private sealed record BackupRestoreMetadata(
        int BackupOptions,
        bool DataFilesPresent,
        string? DataFilesFormat,
        string? RawFolderName,
        string? DomainAccountGraphFailureReason);

    private sealed record SevenZipCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}

[ComVisible(false)]
internal sealed record BackupRestoreIntegrityEvidence(string ArchivePath)
{
    internal bool IsValid { get; init; }
    internal bool ArchiveTestPassed { get; init; }
    internal bool MetadataPresent { get; init; }
    internal bool MetadataXmlValid { get; init; }
    internal int? BackupOptions { get; init; }
    internal bool BackupMessagesDbOnly { get; init; }
    internal bool DataFilesPresent { get; init; }
    internal string? DataFilesFormat { get; init; }
    internal string? RawFolderName { get; init; }
    internal string? RawDataBackupPath { get; init; }
    internal IReadOnlyList<string> ArchiveEntries { get; init; } =
        Array.Empty<string>();
    internal string? FailureReason { get; init; }

    internal BackupRestoreIntegrityEvidence Invalid(string reason) => this with
    {
        IsValid = false,
        FailureReason = reason
    };
}
