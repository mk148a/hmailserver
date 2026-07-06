using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HMailServer.Core.Abstractions;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("F6BB0F43-EDEE-49A8-8166-672F3017426F")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceUtilities
{
    [DispId(1)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetMailServer([MarshalAs(UnmanagedType.BStr)] string emailAddress);

    [DispId(2)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool IsValidEmailAddress([MarshalAs(UnmanagedType.BStr)] string emailAddress);

    [DispId(3)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool IsValidDomainName([MarshalAs(UnmanagedType.BStr)] string domainName);

    [DispId(4)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string MD5([MarshalAs(UnmanagedType.BStr)] string input);

    [DispId(5)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string BlowfishEncrypt([MarshalAs(UnmanagedType.BStr)] string input);

    [DispId(6)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string BlowfishDecrypt([MarshalAs(UnmanagedType.BStr)] string input);

    [DispId(7)]
    void MakeDependent([MarshalAs(UnmanagedType.BStr)] string otherService);

    [DispId(8)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool ImportMessageFromFile(
        [MarshalAs(UnmanagedType.BStr)] string filename,
        int accountId);

    [DispId(9)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool EmailAllAccounts(
        [MarshalAs(UnmanagedType.BStr)] string recipientWildcard,
        [MarshalAs(UnmanagedType.BStr)] string fromAddress,
        [MarshalAs(UnmanagedType.BStr)] string fromName,
        [MarshalAs(UnmanagedType.BStr)] string subject,
        [MarshalAs(UnmanagedType.BStr)] string body);

    [DispId(10)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string GenerateGUID();

    [DispId(11)]
    void RunTestSuite([MarshalAs(UnmanagedType.BStr)] string testPassword);

    [DispId(12)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool IsLocalHost([MarshalAs(UnmanagedType.BStr)] string hostname);

    [DispId(13)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool ImportMessageFromFileToIMAPFolder(
        [MarshalAs(UnmanagedType.BStr)] string filename,
        int accountId,
        [MarshalAs(UnmanagedType.BStr)] string imapFolder);

    [DispId(14)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool IsStrongPassword(
        [MarshalAs(UnmanagedType.BStr)] string username,
        [MarshalAs(UnmanagedType.BStr)] string password);

    [DispId(15)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string SHA256([MarshalAs(UnmanagedType.BStr)] string input);

    [DispId(16)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool CriteriaMatch(
        [MarshalAs(UnmanagedType.BStr)] string matchValue,
        ComRuleMatchType matchType,
        [MarshalAs(UnmanagedType.BStr)] string testValue);

    [DispId(17)]
    long RetrieveMessageID([MarshalAs(UnmanagedType.BStr)] string filename);

    [DispId(18)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool IsValidIPAddress([MarshalAs(UnmanagedType.BStr)] string ipAddress);

    [DispId(19)]
    void PerformMaintenance(ComMaintenanceOperation operation);
}

[ComVisible(true)]
[Guid("E116DCB7-7FEC-4540-BEA1-FA1B19D05B5F")]
[ProgId("hMailServer.Utilities.1")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IInterfaceUtilities))]
public sealed class Utilities : IInterfaceUtilities
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int EInvalidArgument = unchecked((int)0x80070057);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int LegacySha256SaltLength = 6;
    private const int MaximumEmailAddressLength = 254;
    private const string LegacyShortPasswordRequiredCharacters =
        "01234567890!\"#\uFFFD%&/()=?^*_:;><,.-'\uFFFD\uFFFD+";
    private const string EmailAddressPattern =
        """^(("[^<>@\\]+")|(?!\.|.*\.(\.|@))[^<> @\\"]+)@(\[([0-9]{1,3}\.){3}[0-9]{1,3}\]|\[IPv6:(?:[A-Fa-f0-9]{1,4}:){7}[A-Fa-f0-9]{1,4}\]|(?=.{1,255}$)((?!-|\.)[a-zA-Z0-9-]{0,62}[a-zA-Z0-9])(|\.(?!-|\.)[a-zA-Z0-9-]{0,62}[a-zA-Z0-9]){1,126})$""";
    private const string DomainNamePattern =
        """^(\[([0-9]{1,3}\.){3}[0-9]{1,3}\]|\[IPv6:(?:[A-Fa-f0-9]{1,4}:){7}[A-Fa-f0-9]{1,4}\]|(?=.{1,255}$)((?!-|\.)[a-zA-Z0-9-]{0,62}[a-zA-Z0-9])(|\.(?!-|\.)[a-zA-Z0-9-]{0,62}[a-zA-Z0-9]){1,126})$""";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly Regex EmailAddressExpression =
        new(EmailAddressPattern, RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex DomainNameExpression =
        new(DomainNamePattern, RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly HashSet<string> CommonWeakPasswords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "secret",
            "password",
            "info",
            "webmaster",
            "admin",
            "12345",
            "qwerty"
        };

    private readonly Func<bool>? _isServerAdministrator;
    private readonly ILegacyBlowfishCipher? _blowfishCipher;

    public Utilities()
    {
    }

    private Utilities(
        Func<bool>? isServerAdministrator,
        ILegacyBlowfishCipher? blowfishCipher)
    {
        _isServerAdministrator = isServerAdministrator;
        _blowfishCipher = blowfishCipher;
    }

    internal static Utilities CreateForApplication(
        Func<bool> isServerAdministrator,
        ILegacyBlowfishCipher? blowfishCipher)
    {
        ArgumentNullException.ThrowIfNull(isServerAdministrator);
        return new Utilities(isServerAdministrator, blowfishCipher);
    }

    [ComVisible(false)]
    public static Utilities CreateForRuntime(ILegacyBlowfishCipher blowfishCipher)
    {
        ArgumentNullException.ThrowIfNull(blowfishCipher);
        return new Utilities(isServerAdministrator: null, blowfishCipher);
    }

    public string GetMailServer(string emailAddress) => Unavailable<string>();

    public bool IsValidEmailAddress(string emailAddress)
    {
        if (emailAddress is null || emailAddress.Length > MaximumEmailAddressLength)
        {
            return false;
        }

        return IsMatch(EmailAddressExpression, emailAddress);
    }

    public bool IsValidDomainName(string domainName) =>
        domainName is not null && IsMatch(DomainNameExpression, domainName);

    public string MD5(string input) =>
        ComputeHashHex(HashAlgorithmName.MD5, input ?? string.Empty);

    public string BlowfishEncrypt(string input) =>
        BlowfishCipher?.Encrypt(input ?? string.Empty) ?? Unavailable<string>();

    public string BlowfishDecrypt(string input)
    {
        var cipher = BlowfishCipher;
        if (cipher is null)
        {
            return Unavailable<string>();
        }

        if (!cipher.TryDecrypt(input ?? string.Empty, out var output))
        {
            throw new COMException(
                "The Blowfish ciphertext is not a valid legacy hMailServer value.",
                EInvalidArgument);
        }

        return output;
    }

    public void MakeDependent(string otherService) => UnavailableForAdministrator();

    public bool ImportMessageFromFile(string filename, int accountId) =>
        UnavailableForAdministrator<bool>();

    public bool EmailAllAccounts(
        string recipientWildcard,
        string fromAddress,
        string fromName,
        string subject,
        string body) =>
        UnavailableForAdministrator<bool>();

    public string GenerateGUID() => Guid.NewGuid().ToString("B");

    public void RunTestSuite(string testPassword) => UnavailableForAdministrator();

    public bool IsLocalHost(string hostname) => Unavailable<bool>();

    public bool ImportMessageFromFileToIMAPFolder(
        string filename,
        int accountId,
        string imapFolder) =>
        UnavailableForAdministrator<bool>();

    public bool IsStrongPassword(string username, string password)
    {
        username ??= string.Empty;
        password ??= string.Empty;

        if (username.Contains(password, StringComparison.OrdinalIgnoreCase)
            || password.Length <= 4)
        {
            return false;
        }

        if (password.Length <= 6
            && password.IndexOfAny(LegacyShortPasswordRequiredCharacters.ToCharArray()) < 0)
        {
            return false;
        }

        return !CommonWeakPasswords.Contains(password);
    }

    public string SHA256(string input)
    {
        var randomValue = Guid.NewGuid().ToString("N")[..12];
        var salt = ComputeHashHex(HashAlgorithmName.SHA256, randomValue)[..LegacySha256SaltLength];
        return salt + ComputeHashHex(HashAlgorithmName.SHA256, salt + (input ?? string.Empty));
    }

    public bool CriteriaMatch(
        string matchValue,
        ComRuleMatchType matchType,
        string testValue)
    {
        matchValue ??= string.Empty;
        testValue ??= string.Empty;

        return matchType switch
        {
            ComRuleMatchType.Equals =>
                string.Equals(matchValue, testValue, StringComparison.OrdinalIgnoreCase),
            ComRuleMatchType.Contains =>
                testValue.Contains(matchValue, StringComparison.OrdinalIgnoreCase),
            ComRuleMatchType.LessThan =>
                ParseLegacyDouble(matchValue) > ParseLegacyDouble(testValue),
            ComRuleMatchType.GreaterThan =>
                ParseLegacyDouble(matchValue) < ParseLegacyDouble(testValue),
            ComRuleMatchType.RegExMatch =>
                RegexMatches(matchValue, testValue),
            ComRuleMatchType.NotContains =>
                !testValue.Contains(matchValue, StringComparison.OrdinalIgnoreCase),
            ComRuleMatchType.NotEquals =>
                !string.Equals(matchValue, testValue, StringComparison.OrdinalIgnoreCase),
            ComRuleMatchType.Wildcard =>
                WildcardMatches(matchValue, testValue),
            _ => false
        };
    }

    public long RetrieveMessageID(string filename) => UnavailableForAdministrator<long>();

    public bool IsValidIPAddress(string ipAddress)
    {
        if (ipAddress is null)
        {
            return false;
        }

        if (ipAddress.Contains(':'))
        {
            return IPAddress.TryParse(ipAddress, out var ipv6)
                && ipv6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }

        var octets = ipAddress.Split('.');
        return octets.Length == 4
            && octets.All(
                static octet =>
                    octet.Length > 0
                    && octet.All(char.IsAsciiDigit)
                    && byte.TryParse(octet, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }

    public void PerformMaintenance(ComMaintenanceOperation operation) =>
        UnavailableForAdministrator();

    private static string ComputeHashHex(HashAlgorithmName algorithmName, string value)
    {
        var input = Encoding.UTF8.GetBytes(value);
        var output = algorithmName == HashAlgorithmName.MD5
            ? System.Security.Cryptography.MD5.HashData(input)
            : System.Security.Cryptography.SHA256.HashData(input);
        return Convert.ToHexString(output).ToLowerInvariant();
    }

    private static bool IsMatch(Regex expression, string value)
    {
        try
        {
            return expression.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static double ParseLegacyDouble(string value)
    {
        return double.TryParse(
            value.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0;
    }

    private static bool RegexMatches(string pattern, string value)
    {
        try
        {
            return Regex.IsMatch(
                value,
                $@"\A(?:{pattern})\z",
                RegexOptions.Singleline | RegexOptions.CultureInvariant,
                RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool WildcardMatches(string pattern, string value)
    {
        var expression = new StringBuilder(@"\A");
        foreach (var character in pattern)
        {
            expression.Append(
                character switch
                {
                    '*' => ".*",
                    '?' => ".",
                    _ => Regex.Escape(character.ToString())
                });
        }

        expression.Append(@"\z");

        try
        {
            return Regex.IsMatch(
                value,
                expression.ToString(),
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
                RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private void EnsureServerAdministrator()
    {
        if (_isServerAdministrator is null || !_isServerAdministrator())
        {
            throw new COMException(
                "You do not have access to this property / method. Ensure that hMailServer.Application.Authenticate() is called with proper login credentials.",
                EAccessDenied);
        }
    }

    private ILegacyBlowfishCipher? BlowfishCipher => _blowfishCipher;

    private static T Unavailable<T>()
    {
        throw new COMException(
            "This Utilities member is not implemented by the .NET 10 rewrite yet.",
            ENotImplemented);
    }

    private void UnavailableForAdministrator()
    {
        EnsureServerAdministrator();
        _ = Unavailable<object>();
    }

    private T UnavailableForAdministrator<T>()
    {
        EnsureServerAdministrator();
        return Unavailable<T>();
    }
}
