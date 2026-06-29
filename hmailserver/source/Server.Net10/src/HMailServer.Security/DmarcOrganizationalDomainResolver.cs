using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;

namespace HMailServer.Security;

public interface IDmarcOrganizationalDomainResolver
{
    ValueTask<string?> ResolveAsync(
        string domain,
        CancellationToken cancellationToken);
}

public sealed class PublicSuffixDmarcOrganizationalDomainResolver
    : IDmarcOrganizationalDomainResolver
{
    private readonly Lazy<Task<IDomainParser?>> _parser;

    public PublicSuffixDmarcOrganizationalDomainResolver(string publicSuffixListPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicSuffixListPath);
        var path = Path.GetFullPath(publicSuffixListPath);
        _parser = new Lazy<Task<IDomainParser?>>(
            () => LoadParserAsync(path),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async ValueTask<string?> ResolveAsync(
        string domain,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedDomain = domain.Trim().TrimEnd('.');
        if (normalizedDomain.Length == 0)
        {
            return null;
        }

        var parser = await _parser.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (parser is null)
        {
            return null;
        }

        try
        {
            if (!parser.TryParse(normalizedDomain, out var domainInfo)
                || domainInfo is null
                || string.IsNullOrWhiteSpace(domainInfo.RegistrableDomain))
            {
                return null;
            }

            return domainInfo.RegistrableDomain.ToLowerInvariant();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<IDomainParser?> LoadParserAsync(string publicSuffixListPath)
    {
        try
        {
            var ruleProvider = new LocalFileRuleProvider(publicSuffixListPath);
            var loaded = await ruleProvider
                .BuildAsync(ignoreCache: false, CancellationToken.None)
                .ConfigureAwait(false);
            return loaded ? new DomainParser(ruleProvider) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
