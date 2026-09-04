using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record RestoreAccountEntry(AccountAdministrationSnapshot Account, string Password, int PasswordEncryption)
{
    public IReadOnlyList<RestoreFetchAccountEntry> FetchAccounts { get; init; } = Array.Empty<RestoreFetchAccountEntry>();

    public IReadOnlyList<RestoreRuleEntry> Rules { get; init; } = Array.Empty<RestoreRuleEntry>();

    public IReadOnlyList<RestoreFolderEntry> Folders { get; init; } = Array.Empty<RestoreFolderEntry>();
}

[ComVisible(false)]
public sealed record RestoreRuleEntry(
    RuleAdministrationSnapshot Rule,
    IReadOnlyList<RuleCriteriaAdministrationSnapshot> Criteria,
    IReadOnlyList<RuleActionAdministrationSnapshot> Actions);

[ComVisible(false)]
public sealed record RestoreFolderEntry(
    ImapFolderAdministrationSnapshot Folder,
    IReadOnlyList<RestoreFolderEntry> Children,
    IReadOnlyList<MessageAdministrationSnapshot> Messages);

[ComVisible(false)]
public sealed record RestoreFolderPermissionEntry(
    int PermissionType,
    int Rights,
    string Holder);

[ComVisible(false)]
public sealed record RestorePublicFolderEntry(
    ImapFolderAdministrationSnapshot Folder,
    IReadOnlyList<RestorePublicFolderEntry> Children,
    IReadOnlyList<MessageAdministrationSnapshot> Messages,
    IReadOnlyList<RestoreFolderPermissionEntry> Permissions);

[ComVisible(false)]
public sealed record RestoreFetchAccountEntry(
    FetchAccountAdministrationDraft Account,
    string EncryptedPassword,
    IReadOnlyList<FetchAccountUidBackupAdministrationSnapshot> Uids);

[ComVisible(false)]
public sealed record RestoreDistributionListEntry(
    DistributionListAdministrationSnapshot DistributionList,
    IReadOnlyList<DistributionListRecipientAdministrationSnapshot> Recipients);

[ComVisible(false)]
public sealed record RestoreDomainEntry(
    DomainAdministrationSnapshot Domain,
    IReadOnlyList<RestoreAccountEntry> Accounts,
    IReadOnlyList<AliasAdministrationSnapshot> Aliases,
    IReadOnlyList<RestoreDistributionListEntry> DistributionLists);

[ComVisible(false)]
public sealed record RestoreGroupEntry(
    GroupAdministrationSnapshot Group,
    IReadOnlyList<string> MemberNames);

[ComVisible(false)]
public sealed record RestoreTcpIpPortEntry(
    TcpIpPortAdministrationSnapshot Port);

[ComVisible(false)]
public sealed record RestoreBlockedAttachmentEntry(
    BlockedAttachmentAdministrationSnapshot Attachment);

public static class BackupArchiveXmlSnapshotParser
{
    public static IReadOnlyList<DomainAdministrationSnapshot> ParseDomains(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var domains = document.Descendants("Domain").Select(ParseDomain).ToArray();
        return domains;
    }

    public static IReadOnlyList<RestoreAccountEntry> ParseAccounts(string archiveXml, int domainId)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        return document.Descendants("Account")
            .Select(element => ParseAccount(element, domainId))
            .ToArray();
    }

    internal static IReadOnlyList<BackupSettingsPropertySnapshot> ParseSettingsProperties(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var properties = document.Root?.Element("Properties")?.Elements()
            ?? Enumerable.Empty<XElement>();

        return properties
            .Select(property => new BackupSettingsPropertySnapshot(
                Name: property.Name.LocalName,
                LongValue: LongAttr(property, "LongValue"),
                StringValue: property.Attribute("StringValue")?.Value ?? string.Empty))
            .ToArray();
    }

    public static IReadOnlyList<DistributionListRecipientAdministrationSnapshot> ParseDistributionListRecipients(
        string archiveXml,
        int distributionListId)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        return document.Descendants("Recipient")
            .Select(element => new DistributionListRecipientAdministrationSnapshot(
                Id: 0,
                ListId: distributionListId,
                Address: element.Attribute("Name")?.Value ?? string.Empty))
            .ToArray();
    }

    public static IReadOnlyList<AliasAdministrationSnapshot> ParseAliases(string archiveXml, int domainId)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        return document.Descendants("Alias")
            .Select(element => new AliasAdministrationSnapshot(
                Id: 0,
                DomainId: domainId,
                Name: element.Attribute("Name")?.Value ?? string.Empty,
                Value: element.Attribute("Value")?.Value ?? string.Empty,
                Active: IntAttr(element, "Active") != 0))
            .ToArray();
    }

    public static IReadOnlyList<DistributionListAdministrationSnapshot> ParseDistributionLists(
        string archiveXml,
        int domainId)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        return document.Descendants("DistributionList")
            .Select(element => new DistributionListAdministrationSnapshot(
                Id: 0,
                DomainId: domainId,
                Address: element.Attribute("Name")?.Value ?? string.Empty,
                Active: IntAttr(element, "Active") != 0,
                RequireSmtpAuth: IntAttr(element, "RequiresAuth") != 0,
                RequireSenderAddress: element.Attribute("RequiresAuthAddress")?.Value ?? string.Empty,
                Mode: IntAttr(element, "ListMode")))
            .ToArray();
    }

    public static IReadOnlyList<RestoreDomainEntry> ParseDomainEntries(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var domains = document.Root?.Element("Domains")?.Elements("Domain")
            ?? Enumerable.Empty<XElement>();

        return domains
            .Select(domain => new RestoreDomainEntry(
                ParseDomain(domain),
                domain.Element("Accounts")?.Elements("Account")
                    .Select(account => ParseAccount(account, 0))
                    .ToArray()
                    ?? Array.Empty<RestoreAccountEntry>(),
                domain.Element("Aliases")?.Elements("Alias")
                    .Select(alias => new AliasAdministrationSnapshot(
                        Id: 0,
                        DomainId: 0,
                        Name: alias.Attribute("Name")?.Value ?? string.Empty,
                        Value: alias.Attribute("Value")?.Value ?? string.Empty,
                        Active: IntAttr(alias, "Active") != 0))
                    .ToArray()
                    ?? Array.Empty<AliasAdministrationSnapshot>(),
                domain.Element("DistributionLists")?.Elements("DistributionList")
                    .Select(list => new RestoreDistributionListEntry(
                        new DistributionListAdministrationSnapshot(
                            Id: 0,
                            DomainId: 0,
                            Address: list.Attribute("Name")?.Value ?? string.Empty,
                            Active: IntAttr(list, "Active") != 0,
                            RequireSmtpAuth: IntAttr(list, "RequiresAuth") != 0,
                            RequireSenderAddress: list.Attribute("RequiresAuthAddress")?.Value ?? string.Empty,
                            Mode: IntAttr(list, "ListMode")),
                        list.Element("Recipients")?.Elements("Recipient")
                            .Select(recipient => new DistributionListRecipientAdministrationSnapshot(
                                Id: 0,
                                ListId: 0,
                                Address: recipient.Attribute("Name")?.Value ?? string.Empty))
                            .ToArray()
                            ?? Array.Empty<DistributionListRecipientAdministrationSnapshot>()))
                    .ToArray()
                    ?? Array.Empty<RestoreDistributionListEntry>()))
            .ToArray();
    }

    public static IReadOnlyList<SecurityRangeAdministrationSnapshot> ParseSecurityRanges(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var container = document.Root?.Elements("SecurityRanges").FirstOrDefault();
        if (container is null)
        {
            return Array.Empty<SecurityRangeAdministrationSnapshot>();
        }

        return container.Elements("SecurityRange")
            .Select(element => new SecurityRangeAdministrationSnapshot(
                Id: 0,
                Name: element.Attribute("Name")?.Value ?? string.Empty,
                LowerIp: element.Attribute("LowerIP")?.Value ?? string.Empty,
                UpperIp: element.Attribute("UpperIP")?.Value ?? string.Empty,
                Priority: IntAttr(element, "Priority"),
                Options: IntAttr(element, "Options"),
                Expires: element.Attribute("Expires")?.Value == "1",
                ExpiresTime: SecurityRangeDateTimeAttr(element, "ExpiresTime")))
            .ToArray();
    }

    public static IReadOnlyList<RestoreTcpIpPortEntry> ParseTcpIpPorts(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var container = document.Root?.Elements("TCPIPPorts").FirstOrDefault();
        if (container is null)
        {
            return Array.Empty<RestoreTcpIpPortEntry>();
        }

        return container.Elements("TCPIPPort")
            .Select(element => new RestoreTcpIpPortEntry(
                new TcpIpPortAdministrationSnapshot(
                    Id: 0,
                    Protocol: IntAttr(element, "PortProtocol"),
                    PortNumber: IntAttr(element, "PortNumber"),
                    Address: element.Attribute("Address")?.Value ?? string.Empty,
                    ConnectionSecurity: element.Attribute("UseSSL")?.Value == "1"
                        ? 1
                        : IntAttr(element, "ConnectionSecurity"),
                    SslCertificateId: 0)
                {
                    SslCertificateName = element.Attribute("SSLCertificateName")?.Value
                }))
            .ToArray();
    }

    public static IReadOnlyList<RestoreBlockedAttachmentEntry> ParseBlockedAttachments(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var container = document.Root?.Elements("BlockedAttachments").FirstOrDefault();
        if (container is null)
        {
            return Array.Empty<RestoreBlockedAttachmentEntry>();
        }

        return container.Elements("BlockedAttachment")
            .Select(element => new RestoreBlockedAttachmentEntry(
                new BlockedAttachmentAdministrationSnapshot(
                    Id: 0,
                    Wildcard: element.Attribute("Name")?.Value ?? string.Empty,
                    Description: element.Attribute("Description")?.Value ?? string.Empty)))
            .ToArray();
    }

    public static IReadOnlyList<RestorePublicFolderEntry> ParsePublicFolderEntries(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var folders = document.Root?.Element("PublicFolders")?.Elements("Folder")
            ?? Enumerable.Empty<XElement>();

        return folders.Select(ParsePublicFolder).ToArray();
    }

    public static IReadOnlyList<RestoreGroupEntry> ParseGroupEntries(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = ParseDocument(archiveXml);
        var containers = document.Root?.Elements("Groups").ToArray()
            ?? Array.Empty<XElement>();
        if (containers.Length > 1)
        {
            throw new InvalidDataException("The backup contains multiple Groups containers.");
        }

        var groups = containers.Length == 0
            ? Array.Empty<XElement>()
            : containers[0].Elements().ToArray();
        if (groups.Any(static element => element.Name != "Group"))
        {
            throw new InvalidDataException("The Groups container contains an unexpected child.");
        }

        var entries = groups.Select(ParseGroup).ToArray();
        if (entries
            .GroupBy(static entry => entry.Group.Name, StringComparer.OrdinalIgnoreCase)
            .Any(static group => group.Count() > 1))
        {
            throw new InvalidDataException("The backup contains duplicate group names.");
        }

        return entries;
    }

    private static RestoreAccountEntry ParseAccount(XElement element, int domainId)
    {
        var snapshot = new AccountAdministrationSnapshot(
            Id: 0,
            DomainId: domainId,
            Address: element.Attribute("Name")?.Value ?? string.Empty,
            Active: IntAttr(element, "Active") != 0,
            AdminLevel: IntAttr(element, "AdminLevel"),
            IsActiveDirectoryAccount: IntAttr(element, "ADActive") != 0,
            ActiveDirectoryDomain: element.Attribute("ADDomain")?.Value ?? string.Empty,
            ActiveDirectoryUsername: element.Attribute("ADUsername")?.Value ?? string.Empty,
            MaxSize: IntAttr(element, "MaxAccountSize"),
            LastLogonTime: DateTimeAttr(element, "LastLogonTime"),
            PersonFirstName: element.Attribute("PersonFirstName")?.Value ?? string.Empty,
            PersonLastName: element.Attribute("PersonLastName")?.Value ?? string.Empty,
            VacationMessageIsOn: IntAttr(element, "VacationMessageOn") != 0,
            VacationMessage: element.Attribute("VacationMessage")?.Value ?? string.Empty,
            VacationSubject: element.Attribute("VacationSubject")?.Value ?? string.Empty,
            VacationMessageExpires: IntAttr(element, "VacationExpires") != 0,
            VacationMessageExpiresDate: element.Attribute("VacationExpireDate")?.Value ?? string.Empty,
            VacationMessageAbortSpamFlagged: IntAttr(element, "VacationAbortSpamFlagged") != 0,
            ForwardEnabled: IntAttr(element, "ForwardEnabled") != 0,
            ForwardAddress: element.Attribute("ForwardAddress")?.Value ?? string.Empty,
            ForwardKeepOriginal: IntAttr(element, "ForwardKeepOriginal") != 0,
            ForwardAbortSpamFlagged: IntAttr(element, "ForwardAbortSpamFlagged") != 0,
            SignatureEnabled: IntAttr(element, "EnableSignature") != 0,
            SignaturePlainText: element.Attribute("SignaturePlainText")?.Value ?? string.Empty,
            SignatureHtml: element.Attribute("SignatureHTML")?.Value ?? string.Empty);
        return new RestoreAccountEntry(
            snapshot,
            element.Attribute("Password")?.Value ?? string.Empty,
            IntAttr(element, "PasswordEncryption"))
        {
            FetchAccounts = element.Element("FetchAccounts")?.Elements("FetchAccount")
                .Select(ParseFetchAccount)
                .ToArray()
                ?? Array.Empty<RestoreFetchAccountEntry>(),
            Rules = element.Element("Rules")?.Elements("Rule")
                .Select(rule => ParseRule(rule, 0))
                .ToArray()
                ?? Array.Empty<RestoreRuleEntry>(),
            Folders = element.Element("Folders")?.Elements("Folder")
                .Select(ParseFolder)
                .ToArray()
                ?? Array.Empty<RestoreFolderEntry>()
        };
    }

    private static RestoreFolderEntry ParseFolder(XElement element)
    {
        if (element.Element("Permissions") is not null)
        {
            throw new InvalidDataException(
                "Folder restore with permissions is outside the bounded message-metadata slice.");
        }

        var folder = new ImapFolderAdministrationSnapshot(
            Id: 0,
            AccountId: 0,
            ParentId: 0,
            Name: element.Attribute("Name")?.Value ?? string.Empty,
            Subscribed: IntAttr(element, "Subscribed") != 0,
            CurrentUid: IntAttr(element, "CurrentUID"),
            CreationTime: element.Attribute("CreateTime")?.Value ?? string.Empty);
        var children = element.Element("Folders")?.Elements("Folder")
            .Select(ParseFolder)
            .ToArray()
            ?? Array.Empty<RestoreFolderEntry>();
        var messages = element.Element("Messages")?.Elements("Message")
            .Select(message => new MessageAdministrationSnapshot(
                Id: 0,
                AccountId: 0,
                FolderId: 0,
                FileName: message.Attribute("Filename")?.Value ?? string.Empty,
                State: IntAttr(message, "State"),
                FromAddress: message.Attribute("FromAddress")?.Value ?? string.Empty,
                SizeBytes: LongAttr(message, "Size"),
                CurrentNumberOfTries: IntAttr(message, "NoOfRetries"),
                Flags: IntAttr(message, "Flags"),
                InternalDate: DateTimeAttr(message, "CreateTime"),
                Uid: LongAttr(message, "UID")))
            .ToArray()
            ?? Array.Empty<MessageAdministrationSnapshot>();
        return new RestoreFolderEntry(folder, children, messages);
    }

    private static RestorePublicFolderEntry ParsePublicFolder(XElement element)
    {
        var folder = new ImapFolderAdministrationSnapshot(
            Id: 0,
            AccountId: 0,
            ParentId: -1,
            Name: element.Attribute("Name")?.Value ?? string.Empty,
            Subscribed: IntAttr(element, "Subscribed") != 0,
            CurrentUid: IntAttr(element, "CurrentUID"),
            CreationTime: element.Attribute("CreateTime")?.Value ?? string.Empty);
        var children = element.Element("Folders")?.Elements("Folder")
            .Select(ParsePublicFolder)
            .ToArray()
            ?? Array.Empty<RestorePublicFolderEntry>();
        var messages = ParseFolderMessages(element);
        var permissions = ParseFolderPermissions(element);

        return new RestorePublicFolderEntry(folder, children, messages, permissions);
    }

    private static RestoreGroupEntry ParseGroup(XElement element)
    {
        var name = RequiredStringAttr(element, "Name", "a group");
        var containers = element.Elements("GroupMembers").ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidDataException($"Group '{name}' contains multiple GroupMembers containers.");
        }

        var members = containers.Length == 0
            ? Array.Empty<XElement>()
            : containers[0].Elements().ToArray();
        if (members.Any(static member => member.Name != "Member"))
        {
            throw new InvalidDataException($"Group '{name}' contains an unexpected member child.");
        }

        return new RestoreGroupEntry(
            new GroupAdministrationSnapshot(0, name),
            members.Select(member => RequiredStringAttr(member, "Name", $"group '{name}' member")).ToArray());
    }

    private static string RequiredStringAttr(XElement element, string name, string context)
    {
        var value = element.Attribute(name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"The {context} is missing a non-empty {name} attribute.");
        }

        return value;
    }

    private static IReadOnlyList<MessageAdministrationSnapshot> ParseFolderMessages(XElement element)
    {
        var messages = element.Element("Messages")?.Elements("Message")
            .Select(message => new MessageAdministrationSnapshot(
                Id: 0,
                AccountId: 0,
                FolderId: 0,
                FileName: message.Attribute("Filename")?.Value ?? string.Empty,
                State: IntAttr(message, "State"),
                FromAddress: message.Attribute("FromAddress")?.Value ?? string.Empty,
                SizeBytes: LongAttr(message, "Size"),
                CurrentNumberOfTries: IntAttr(message, "NoOfRetries"),
                Flags: IntAttr(message, "Flags"),
                InternalDate: DateTimeAttr(message, "CreateTime"),
                Uid: LongAttr(message, "UID")))
            .ToArray()
            ?? Array.Empty<MessageAdministrationSnapshot>();
        return messages;
    }

    private static IReadOnlyList<RestoreFolderPermissionEntry> ParseFolderPermissions(XElement folder)
    {
        var containers = folder.Elements("ACLs").ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidDataException("A public folder may contain only one ACLs container.");
        }

        var container = containers.SingleOrDefault();
        if (container is null)
        {
            return Array.Empty<RestoreFolderPermissionEntry>();
        }

        if (container.Elements().Any(static child => child.Name != "Permission"))
        {
            throw new InvalidDataException("The ACLs container contains an unexpected child.");
        }

        return container.Elements("Permission")
            .Select(ParseFolderPermission)
            .ToArray();
    }

    private static RestoreFolderPermissionEntry ParseFolderPermission(XElement element)
    {
        var type = StrictIntAttribute(element, "Type");
        var rights = StrictIntAttribute(element, "Rights");
        var holder = element.Attribute("Holder")?.Value;
        if (string.IsNullOrWhiteSpace(holder))
        {
            throw new InvalidDataException("An ACL permission must contain a non-empty Holder attribute.");
        }

        if (type is < 0 or > 2)
        {
            throw new InvalidDataException("An ACL permission has an unsupported legacy Type.");
        }

        if (rights is < 0 or > 2047)
        {
            throw new InvalidDataException("An ACL permission has an out-of-range legacy Rights value.");
        }

        return new RestoreFolderPermissionEntry(type, rights, holder);
    }

    private static int StrictIntAttribute(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException($"An ACL permission is missing or has an invalid {name} attribute.");
        }

        return parsed;
    }

    private static RestoreRuleEntry ParseRule(XElement element, int accountId)
    {
        var rule = new RuleAdministrationSnapshot(
            Id: 0,
            AccountId: accountId,
            Name: element.Attribute("Name")?.Value ?? string.Empty,
            Active: IntAttr(element, "Active") != 0,
            UseAnd: IntAttr(element, "UseAND") != 0,
            SortOrder: IntAttr(element, "SortOrder"));
        var criteria = element.Element("RuleCriterias")?.Elements("Criteria")
            .Select(item => new RuleCriteriaAdministrationSnapshot(
                Id: 0,
                RuleId: 0,
                MatchValue: item.Attribute("MatchString")?.Value ?? string.Empty,
                UsePredefined: IntAttr(item, "UsePredefinedField") != 0,
                PredefinedField: IntAttr(item, "FieldType"),
                MatchType: IntAttr(item, "MatchType"),
                HeaderField: item.Attribute("HeaderField")?.Value ?? string.Empty))
            .ToArray()
            ?? Array.Empty<RuleCriteriaAdministrationSnapshot>();
        var actions = element.Element("RuleActions")?.Elements("Action")
            .Select(item => new RuleActionAdministrationSnapshot(
                Id: 0,
                RuleId: 0,
                Type: IntAttr(item, "Type"),
                Subject: item.Attribute("Subject")?.Value ?? string.Empty,
                Body: item.Attribute("Body")?.Value ?? string.Empty,
                FromName: item.Attribute("FromName")?.Value ?? string.Empty,
                FromAddress: item.Attribute("FromAddress")?.Value ?? string.Empty,
                Filename: item.Attribute("FileName")?.Value ?? string.Empty,
                To: item.Attribute("To")?.Value ?? string.Empty,
                ImapFolder: item.Attribute("IMAPFolder")?.Value ?? string.Empty,
                ScriptFunction: item.Attribute("ScriptFunction")?.Value ?? string.Empty,
                HeaderName: item.Attribute("Header")?.Value ?? string.Empty,
                Value: item.Attribute("Value")?.Value ?? string.Empty,
                RouteId: IntAttr(item, "RouteID"),
                AbortSpamFlagged: IntAttr(item, "AbortSpamFlagged") != 0,
                SortOrder: IntAttr(item, "SortOrder")))
            .ToArray()
            ?? Array.Empty<RuleActionAdministrationSnapshot>();
        return new RestoreRuleEntry(rule, criteria, actions);
    }

    private static RestoreFetchAccountEntry ParseFetchAccount(XElement element)
    {
        var connectionSecurity = IntAttr(element, "ConnectionSecurity");
        if (IntAttr(element, "UseSSL") != 0)
        {
            connectionSecurity = 1;
        }

        var account = new FetchAccountAdministrationDraft(
            AccountId: 0,
            Name: element.Attribute("Name")?.Value ?? string.Empty,
            ServerAddress: element.Attribute("ServerAddress")?.Value ?? string.Empty,
            Port: IntAttr(element, "Port"),
            ServerType: IntAttr(element, "ServerType"),
            Username: element.Attribute("Username")?.Value ?? string.Empty,
            MinutesBetweenFetch: IntAttr(element, "Minutes"),
            DaysToKeepMessages: IntAttr(element, "DaysToKeep"),
            Enabled: IntAttr(element, "Active") != 0,
            ProcessMimeRecipients: IntAttr(element, "ProcessMIMERecipients") != 0,
            ProcessMimeDate: IntAttr(element, "ProcessMIMEDate") != 0,
            ConnectionSecurity: connectionSecurity,
            UseAntiSpam: IntAttr(element, "UseAntiSpam") != 0,
            UseAntiVirus: IntAttr(element, "UseAntiVirus") != 0,
            EnableRouteRecipients: IntAttr(element, "EnableRouteRecipients") != 0,
            MimeRecipientHeaders: element.Attribute("MIMERecipientHeaders")?.Value ?? string.Empty);
        var uids = element.Element("FetchAccountUIDs")?.Elements("UID")
            .Select(uid => new FetchAccountUidBackupAdministrationSnapshot(
                uid.Attribute("UID")?.Value ?? string.Empty,
                uid.Attribute("Date")?.Value ?? string.Empty))
            .ToArray()
            ?? Array.Empty<FetchAccountUidBackupAdministrationSnapshot>();
        return new RestoreFetchAccountEntry(
            account,
            element.Attribute("Password")?.Value ?? string.Empty,
            uids);
    }

    private static DateTime DateTimeAttr(XElement element, string name) =>
        DateTime.TryParse(
            element.Attribute(name)?.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : new DateTime(2026, 1, 1);

    private static DateTime SecurityRangeDateTimeAttr(XElement element, string name) =>
        DateTime.TryParse(
            element.Attribute(name)?.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : new DateTime(2001, 1, 1);

    private static DomainAdministrationSnapshot ParseDomain(XElement element)
    {
        var antiSpamOptions = IntAttr(element, "AntiSpamOptions");
        var limitationsEnabled = IntAttr(element, "LimitationsEnabled");
        return new DomainAdministrationSnapshot(
            Id: 0,
            Name: element.Attribute("Name")?.Value ?? string.Empty,
            Active: IntAttr(element, "Active") != 0,
            Postmaster: element.Attribute("Postmaster")?.Value ?? string.Empty,
            MaxMessageSize: IntAttr(element, "MaxMessageSize"),
            PlusAddressingEnabled: IntAttr(element, "UsePlusAddressing") != 0,
            PlusAddressingCharacter: element.Attribute("PlusAddressingChar")?.Value ?? "+",
            AntiSpamEnableGreylisting: (antiSpamOptions & 1) != 0,
            AdDomainName: element.Attribute("ADDomainName")?.Value ?? string.Empty,
            MaxSize: IntAttr(element, "MaxSize"),
            MaxNumberOfAccounts: IntAttr(element, "MaxNoOfAccounts"),
            MaxNumberOfAliases: IntAttr(element, "MaxNoOfAliases"),
            MaxNumberOfDistributionLists: IntAttr(element, "MaxNoOfLists"),
            MaxNumberOfAccountsEnabled: (limitationsEnabled & 1) != 0,
            MaxNumberOfAliasesEnabled: (limitationsEnabled & 2) != 0,
            MaxNumberOfDistributionListsEnabled: (limitationsEnabled & 4) != 0,
            MaxAccountSize: IntAttr(element, "MaxAccountSize"),
            SignatureEnabled: IntAttr(element, "EnableSignature") != 0,
            SignatureMethod: IntAttr(element, "SignatureMethod"),
            SignaturePlainText: element.Attribute("SignaturePlainText")?.Value ?? string.Empty,
            SignatureHtml: element.Attribute("SignatureHTML")?.Value ?? string.Empty,
            AddSignaturesToReplies: IntAttr(element, "AddSignaturesToReplies") != 0,
            AddSignaturesToLocalMail: IntAttr(element, "AddSignaturesToLocalMail") != 0,
            DkimSignEnabled: (antiSpamOptions & 2) != 0,
            DkimSelector: element.Attribute("DKIMSelector")?.Value ?? string.Empty,
            DkimPrivateKeyFile: element.Attribute("DKIMPrivateKeyFile")?.Value ?? string.Empty,
            DkimHeaderCanonicalizationMethod: (antiSpamOptions & 4) != 0 ? 1 : 2,
            DkimBodyCanonicalizationMethod: (antiSpamOptions & 8) != 0 ? 1 : 2,
            DkimSigningAlgorithm: (antiSpamOptions & 16) != 0 ? 1 : 2,
            DkimSignAliasesEnabled: (antiSpamOptions & 32) != 0);
    }

    private static int IntAttr(XElement element, string name) =>
        int.TryParse(element.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static long LongAttr(XElement element, string name) =>
        long.TryParse(element.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static XDocument ParseDocument(string archiveXml)
    {
        using var stringReader = new StringReader(archiveXml);
        using var reader = XmlReader.Create(
            stringReader,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024
            });
        return XDocument.Load(reader, LoadOptions.None);
    }
}
