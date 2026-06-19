using System.Net;

namespace HMailServer.Security;

public sealed class SystemDnsReverseResolver : IDnsReverseResolver
{
    public async ValueTask<IReadOnlyList<string>> ResolveHostNamesAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        var entry = await Dns
            .GetHostEntryAsync(address)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddName(names, entry.HostName);
        foreach (var alias in entry.Aliases)
        {
            AddName(names, alias);
        }

        return names.ToArray();
    }

    private static void AddName(HashSet<string> names, string? value)
    {
        var name = NormalizeHostName(value);
        if (name.Length > 0)
        {
            names.Add(name);
        }
    }

    private static string NormalizeHostName(string? value) =>
        (value ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
}
