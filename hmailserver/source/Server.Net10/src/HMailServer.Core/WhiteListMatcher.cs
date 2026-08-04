using System.Net;
using HMailServer.Core.Abstractions;

namespace HMailServer.Core;

public static class WhiteListMatcher
{
    public static bool IsMatch(
        string? clientIpAddress,
        string? senderAddress,
        IEnumerable<WhiteListAddressAdministrationSnapshot>? addresses)
    {
        if (string.IsNullOrEmpty(clientIpAddress) || addresses is null ||
            !IPAddress.TryParse(clientIpAddress, out var clientAddress))
        {
            return false;
        }

        var clientBytes = clientAddress.GetAddressBytes();
        if (clientBytes.Length is not (4 or 16))
        {
            return false;
        }

        foreach (var address in addresses)
        {
            if (address is null ||
                !IPAddress.TryParse(address.LowerIpAddress, out var lowerAddress) ||
                !IPAddress.TryParse(address.UpperIpAddress, out var upperAddress))
            {
                continue;
            }

            var lowerBytes = lowerAddress.GetAddressBytes();
            var upperBytes = upperAddress.GetAddressBytes();
            if (lowerBytes.Length != clientBytes.Length ||
                upperBytes.Length != clientBytes.Length ||
                CompareBytes(lowerBytes, upperBytes) > 0 ||
                CompareBytes(clientBytes, lowerBytes) < 0 ||
                CompareBytes(clientBytes, upperBytes) > 0)
            {
                continue;
            }

            if (string.IsNullOrEmpty(address.EmailAddress) || address.EmailAddress == "*")
            {
                return true;
            }

            if (WildcardMatchNoCase(address.EmailAddress, senderAddress ?? string.Empty))
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareBytes(byte[] left, byte[] right)
    {
        for (var index = 0; index < left.Length; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static bool WildcardMatchNoCase(string pattern, string value)
    {
        var patternIndex = 0;
        var valueIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 (pattern[patternIndex] != '*' &&
                  char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex]))))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex;
                matchIndex = valueIndex;
                patternIndex++;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                matchIndex++;
                valueIndex = matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
