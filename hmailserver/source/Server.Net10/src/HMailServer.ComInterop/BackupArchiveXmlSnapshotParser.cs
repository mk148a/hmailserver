using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(false)]
public static class BackupArchiveXmlSnapshotParser
{
    public static IReadOnlyList<DomainAdministrationSnapshot> ParseDomains(string archiveXml)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveXml);
        var document = XDocument.Parse(archiveXml);
        var domains = document.Descendants("Domain").Select(ParseDomain).ToArray();
        return domains;
    }

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