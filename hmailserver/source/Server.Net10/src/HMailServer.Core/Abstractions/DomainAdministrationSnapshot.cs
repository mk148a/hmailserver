namespace HMailServer.Core.Abstractions;

public sealed record DomainAdministrationSnapshot(
    int Id,
    string Name,
    bool Active,
    string Postmaster = "",
    int MaxMessageSize = 0,
    bool PlusAddressingEnabled = false,
    string PlusAddressingCharacter = "",
    int MaxSize = 0,
    int MaxNumberOfAccounts = 0,
    int MaxNumberOfAliases = 0,
    int MaxNumberOfDistributionLists = 0,
    bool MaxNumberOfAccountsEnabled = false,
    bool MaxNumberOfAliasesEnabled = false,
    bool MaxNumberOfDistributionListsEnabled = false,
    int MaxAccountSize = 0);
