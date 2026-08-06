using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public sealed record RestoreAccountEntry(AccountAdministrationSnapshot Account, string Password, int PasswordEncryption);

public static class BackupArchiveXmlSnapshotParser
{
    public static IReadOnlyList<DomainAdministrationSnapshot> ParseDomains(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = XDocument.Parse(archiveXml);
        var domains = document.Descendants("Domain").Select(ParseDomain).ToArray();
        return domains;
    }

    public static IReadOnlyList<RestoreAccountEntry> ParseAccounts(string archiveXml, int domainId)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = XDocument.Parse(archiveXml);
        return document.Descendants("Account")
            .Select(element => ParseAccount(element, domainId))
            .ToArray();
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
            IntAttr(element, "PasswordEncryption"));
    }

    private static DateTime DateTimeAttr(XElement element, string name) =>
        DateTime.TryParse(
            element.Attribute(name)?.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : new DateTime(2026, 1, 1);

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
}