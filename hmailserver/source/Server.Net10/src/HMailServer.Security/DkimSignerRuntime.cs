using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using HMailServer.Core.Abstractions;
using MimeKit;
using Microsoft.Win32.SafeHandles;

namespace HMailServer.Security;

public sealed class DkimSignerRuntime : IDkimSigner
{
    public const int LegacyMaximumMessageBytes = 10 * 1024 * 1024;
    public const int MaxPrivateKeyBytes = 1024 * 1024;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;

    private static readonly string[] RecommendedHeaders =
    [
        "From", "Sender", "Reply-To", "Subject", "Date", "Message-ID", "To", "CC",
        "MIME-Version", "Content-Type", "Content-Transfer-Encoding", "Content-ID",
        "Content-Description", "Resent-Date", "Resent-From", "Resent-Sender", "Resent-To",
        "Resent-Cc", "Resent-Message-ID", "In-Reply-To", "References", "List-Id", "List-Help",
        "List-Unsubscribe", "List-Unsubscribe-Post", "List-Subscribe", "List-Post", "List-Owner",
        "List-Archive", "X-CSA-Complaints"
    ];

    private readonly string _dataDirectory;
    private readonly IDomainAdministrationStore _domainStore;
    private readonly IDomainAliasAdministrationStore _domainAliasStore;

    public DkimSignerRuntime(
        string dataDirectory,
        IDomainAdministrationStore domainStore,
        IDomainAliasAdministrationStore domainAliasStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _dataDirectory = Path.GetFullPath(dataDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _domainStore = domainStore ?? throw new ArgumentNullException(nameof(domainStore));
        _domainAliasStore = domainAliasStore ?? throw new ArgumentNullException(nameof(domainAliasStore));
    }

    public async ValueTask<byte[]?> SignAsync(
        DeliveryQueuedMessage message,
        byte[] messageData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageData);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (messageData.Length > LegacyMaximumMessageBytes)
            {
                return null;
            }

            if (message.CurrentRetryCount != 0)
            {
                return null;
            }

            var parsed = ParseMessage(message, messageData);
            if (parsed is null || HasSameDomainSignature(parsed.HeaderBlock, parsed.FromDomain))
            {
                return null;
            }

            var domain = await FindSigningDomainAsync(parsed.FromDomain, cancellationToken).ConfigureAwait(false);
            if (domain is null
                || !domain.Active
                || !domain.DkimSignEnabled
                || (!domain.IsMainDomain && !domain.DkimSignAliasesEnabled)
                || !IsValidDomain(parsed.FromDomain)
                || !IsValidSelector(domain.DkimSelector)
                || !TryGetCanonicalization(domain.Snapshot, out var headerMethod, out var bodyMethod)
                || !TryGetHashAlgorithm(domain.DkimSigningAlgorithm, out var hashAlgorithm, out var algorithmName))
            {
                return null;
            }

            var keyPath = ResolvePrivateKeyPath(domain.DkimPrivateKeyFile);
            if (keyPath is null)
            {
                return null;
            }

            var keyData = await ReadPrivateKeyAsync(keyPath, cancellationToken).ConfigureAwait(false);
            if (keyData is null)
            {
                return null;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(Encoding.ASCII.GetString(keyData));

            var bodyHash = Convert.ToBase64String(
                ComputeHash(
                    hashAlgorithm,
                    Encoding.Latin1.GetBytes(DkimCanonicalizer.CanonicalizeBody(parsed.Body, bodyMethod))));
            var unsignedSignature = BuildSignatureValue(
                algorithmName,
                parsed.FromDomain,
                domain.DkimSelector,
                headerMethod,
                bodyMethod,
                parsed.HeaderBlock,
                bodyHash,
                string.Empty);
            var canonicalizedHeaders = DkimCanonicalizer.CanonicalizeHeaders(
                parsed.HeaderBlock,
                "DKIM-Signature",
                unsignedSignature,
                RecommendedHeaders,
                headerMethod,
                out var fieldList);
            if (fieldList.Length == 0 || !fieldList.Split(':').Any(static name => name.Equals("From", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var signature = Convert.ToBase64String(
                rsa.SignData(
                    Encoding.Latin1.GetBytes(canonicalizedHeaders),
                    hashAlgorithm,
                    RSASignaturePadding.Pkcs1));
            var signatureValue = BuildSignatureValue(
                algorithmName,
                parsed.FromDomain,
                domain.DkimSelector,
                headerMethod,
                bodyMethod,
                parsed.HeaderBlock,
                bodyHash,
                signature,
                fieldList);
            var signedMessage = "DKIM-Signature: " + signatureValue + "\r\n"
                + parsed.HeaderBlock + "\r\n\r\n" + parsed.Body;
            return Encoding.Latin1.GetBytes(signedMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async ValueTask<SigningDomain?> FindSigningDomainAsync(
        string fromDomain,
        CancellationToken cancellationToken)
    {
        var domains = await _domainStore.GetDomainsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var domain in domains)
        {
            var normalizedName = NormalizeDomain(domain.Name);
            if (normalizedName.Equals(fromDomain, StringComparison.OrdinalIgnoreCase))
            {
                return new SigningDomain(domain, IsMainDomain: true);
            }
        }

        foreach (var domain in domains)
        {
            var aliases = await _domainAliasStore
                .GetDomainAliasesAsync(domain.Id, cancellationToken)
                .ConfigureAwait(false);
            if (aliases.Any(alias => NormalizeDomain(alias.AliasName).Equals(fromDomain, StringComparison.OrdinalIgnoreCase)))
            {
                return new SigningDomain(domain, IsMainDomain: false);
            }
        }

        return null;
    }

    private string? ResolvePrivateKeyPath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)
            || configuredPath.IndexOfAny(['\r', '\n', '\0']) >= 0
            || HasTraversalComponent(configuredPath))
        {
            return null;
        }

        try
        {
            var candidate = Path.GetFullPath(
                Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(_dataDirectory, configuredPath));
            var relative = Path.GetRelativePath(_dataDirectory, candidate);
            if (relative.Equals(".", StringComparison.Ordinal)
                || relative.Equals("..", StringComparison.Ordinal)
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(relative)
                || !IsPathWithoutReparsePoints(candidate))
            {
                return null;
            }

            return candidate;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async ValueTask<byte[]?> ReadPrivateKeyAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = OpenPrivateKeyStream(path, _dataDirectory);
            if (stream is null)
            {
                return null;
            }

            if (stream.Length <= 0 || stream.Length > MaxPrivateKeyBytes)
            {
                return null;
            }

            if (stream.Length > int.MaxValue)
            {
                return null;
            }

            var data = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
            if (stream.ReadByte() >= 0)
            {
                return null;
            }

            return data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ParsedMessage? ParseMessage(
        DeliveryQueuedMessage queuedMessage,
        byte[] messageData)
    {
        var rawMessage = Encoding.Latin1.GetString(messageData);
        var normalized = NormalizeLineEndings(rawMessage);
        var separator = normalized.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        var headerBlock = normalized[..separator];
        if (!IsValidHeaderBlock(headerBlock))
        {
            return null;
        }

        string? fromDomain = null;
        try
        {
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(normalized), writable: false);
            var mimeMessage = MimeMessage.Load(stream);
            var mailbox = mimeMessage.From.Mailboxes.FirstOrDefault();
            if (mailbox is not null)
            {
                fromDomain = ExtractDomain(mailbox.Address);
            }
        }
        catch (FormatException)
        {
            return null;
        }

        if (fromDomain is null && !ContainsHeader(headerBlock, "From"))
        {
            fromDomain = ExtractDomain(queuedMessage.FromAddress);
        }

        return IsValidDomain(fromDomain)
            ? new ParsedMessage(
                headerBlock,
                normalized[(separator + 4)..],
                NormalizeDomain(fromDomain!))
            : null;
    }

    private static bool HasSameDomainSignature(string headerBlock, string domain)
    {
        foreach (var value in GetHeaderValues(headerBlock, "DKIM-Signature"))
        {
            if (DkimSignatureParser.TryParse(value, out var signature, out _)
                && signature is not null
                && signature.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSignatureValue(
        string algorithm,
        string domain,
        string selector,
        DkimCanonicalizationMethod headerMethod,
        DkimCanonicalizationMethod bodyMethod,
        string headerBlock,
        string bodyHash,
        string signature,
        string? fieldList = null)
    {
        if (fieldList is null)
        {
            DkimCanonicalizer.CanonicalizeHeaders(
                headerBlock,
                string.Empty,
                string.Empty,
                RecommendedHeaders,
                headerMethod,
                out fieldList);
        }

        var value = "v=1; a=" + algorithm + "; d=" + domain + "; s=" + selector + ";\r\n"
            + "\tc=" + ToName(headerMethod) + "/" + ToName(bodyMethod) + "; q=dns/txt;\r\n"
            + "\th=" + fieldList + ";\r\n"
            + "\tbh=" + bodyHash + ";\r\n"
            + "\tb=";
        return signature.Length == 0 ? value : value + FoldBase64(signature);
    }

    private static string FoldBase64(string value)
    {
        var builder = new StringBuilder(value.Length + value.Length / 250 * 3);
        for (var index = 0; index < value.Length; index += 250)
        {
            if (index > 0)
            {
                builder.Append("\r\n\t");
            }

            builder.Append(value, index, Math.Min(250, value.Length - index));
        }

        return builder.ToString();
    }

    private static bool TryGetCanonicalization(
        DomainAdministrationSnapshot domain,
        out DkimCanonicalizationMethod headerMethod,
        out DkimCanonicalizationMethod bodyMethod)
    {
        headerMethod = (DkimCanonicalizationMethod)domain.DkimHeaderCanonicalizationMethod;
        bodyMethod = (DkimCanonicalizationMethod)domain.DkimBodyCanonicalizationMethod;
        return headerMethod is DkimCanonicalizationMethod.Simple or DkimCanonicalizationMethod.Relaxed
            && bodyMethod is DkimCanonicalizationMethod.Simple or DkimCanonicalizationMethod.Relaxed;
    }

    private static bool TryGetHashAlgorithm(
        int configuredAlgorithm,
        out HashAlgorithmName hashAlgorithm,
        out string algorithmName)
    {
        hashAlgorithm = configuredAlgorithm switch
        {
            1 => HashAlgorithmName.SHA1,
            2 => HashAlgorithmName.SHA256,
            _ => default
        };
        algorithmName = configuredAlgorithm switch
        {
            1 => "rsa-sha1",
            2 => "rsa-sha256",
            _ => string.Empty
        };
        return algorithmName.Length > 0;
    }

    private static byte[] ComputeHash(HashAlgorithmName algorithm, byte[] data) =>
        algorithm == HashAlgorithmName.SHA1 ? SHA1.HashData(data) : SHA256.HashData(data);

    private static string ToName(DkimCanonicalizationMethod method) =>
        method == DkimCanonicalizationMethod.Simple ? "simple" : "relaxed";

    private static string? ExtractDomain(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            return null;
        }

        var at = address.LastIndexOf('@');
        return at <= 0 || at == address.Length - 1 ? null : NormalizeDomain(address[(at + 1)..]);
    }

    private static string NormalizeDomain(string value) => value.Trim().TrimEnd('.').ToLowerInvariant();

    private static bool IsValidDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.IndexOfAny(['\r', '\n', '\0', '/', '\\', ':']) >= 0
            || value.Length > 253)
        {
            return false;
        }

        var domain = NormalizeDomain(value);
        return domain.Length > 0
            && domain.Split('.').All(static label =>
                label.Length is > 0 and <= 63
                && label[0] != '-'
                && label[^1] != '-'
                && label.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-'));
    }

    private static bool IsValidSelector(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 63
        && value.IndexOfAny(['\r', '\n', '\0', ';', '=', '/', '\\', ' ', '\t']) < 0
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool HasTraversalComponent(string value) =>
        value.Split(['/', '\\'], StringSplitOptions.None).Any(static part => part == "..");

    private static bool IsPathWithoutReparsePoints(string path)
    {
        var pathRoot = Path.GetPathRoot(path);
        if (pathRoot is null || !Directory.Exists(pathRoot))
        {
            return false;
        }

        var root = Path.GetFullPath(pathRoot);
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        if (HasReparsePoint(current))
        {
            return false;
        }

        foreach (var part in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                return false;
            }

            if (HasReparsePoint(current))
            {
                return false;
            }
        }

        return File.Exists(path);
    }

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static FileStream? OpenPrivateKeyStream(string path, string approvedDataDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
        }

        var rawHandle = CreateFile(
            path,
            GenericRead,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (rawHandle == IntPtr.Zero || rawHandle == new IntPtr(-1))
        {
            return null;
        }

        var handle = new SafeFileHandle(rawHandle, ownsHandle: true);
        if (!GetFileInformationByHandle(handle, out var information)
            || (information.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0
            || information.NumberOfLinks != 1
            || !TryGetFinalPathByHandle(handle, out var finalPath)
            || !IsContainedFinalPath(approvedDataDirectory, finalPath))
        {
            handle.Dispose();
            return null;
        }

        return new FileStream(handle, FileAccess.Read, bufferSize: 4096, isAsync: false);
    }

    private static bool IsContainedFinalPath(string approvedDataDirectory, string finalPath)
    {
        var root = NormalizeFinalPath(approvedDataDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = NormalizeFinalPath(finalPath);
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFinalPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\" + path[7..];
        }

        return path.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? path[4..]
            : Path.GetFullPath(path);
    }

    private static bool TryGetFinalPathByHandle(SafeFileHandle handle, out string path)
    {
        for (var capacity = 260; capacity <= 32768; capacity *= 2)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0)
            {
                path = string.Empty;
                return false;
            }

            if (length < buffer.Capacity)
            {
                path = buffer.ToString();
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder filePath,
        uint filePathLength,
        uint flags);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    private static bool IsValidHeaderBlock(string headerBlock)
    {
        var hasField = false;
        foreach (var line in headerBlock.Split("\r\n", StringSplitOptions.None))
        {
            if (line.Length == 0)
            {
                return false;
            }

            if (line[0] is ' ' or '\t')
            {
                if (!hasField)
                {
                    return false;
                }

                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0 || !IsValidHeaderName(line[..colon]))
            {
                return false;
            }

            hasField = true;
        }

        return hasField;
    }

    private static bool IsValidHeaderName(string name) =>
        name.All(static character => char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));

    private static bool ContainsHeader(string headerBlock, string name) =>
        GetHeaderValues(headerBlock, name).Count > 0;

    private static IReadOnlyList<string> GetHeaderValues(string headerBlock, string name)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        foreach (var line in headerBlock.Split("\r\n", StringSplitOptions.None))
        {
            if (line[0] is ' ' or '\t')
            {
                current.Append("\r\n").Append(line);
                continue;
            }

            AddHeaderValue(values, current, name);
            current.Clear();
            current.Append(line);
        }

        AddHeaderValue(values, current, name);
        return values;
    }

    private static void AddHeaderValue(
        ICollection<string> values,
        StringBuilder current,
        string name)
    {
        if (current.Length == 0)
        {
            return;
        }

        var raw = current.ToString();
        var colon = raw.IndexOf(':');
        if (colon > 0 && raw[..colon].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            values.Add(raw[(colon + 1)..].TrimStart(' ', '\t'));
        }
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);

    private sealed record ParsedMessage(string HeaderBlock, string Body, string FromDomain);

    private sealed record SigningDomain(DomainAdministrationSnapshot Snapshot, bool IsMainDomain)
    {
        public bool Active => Snapshot.Active;
        public bool DkimSignEnabled => Snapshot.DkimSignEnabled;
        public bool DkimSignAliasesEnabled => Snapshot.DkimSignAliasesEnabled;
        public string DkimSelector => Snapshot.DkimSelector;
        public string DkimPrivateKeyFile => Snapshot.DkimPrivateKeyFile;
        public int DkimHeaderCanonicalizationMethod => Snapshot.DkimHeaderCanonicalizationMethod;
        public int DkimBodyCanonicalizationMethod => Snapshot.DkimBodyCanonicalizationMethod;
        public int DkimSigningAlgorithm => Snapshot.DkimSigningAlgorithm;
    }
}
