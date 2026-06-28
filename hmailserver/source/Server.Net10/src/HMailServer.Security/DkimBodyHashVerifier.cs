using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Security;

public static class DkimBodyHashVerifier
{
    public static DkimEvaluation VerifyBodyHash(
        string body,
        DkimSignature signature)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(signature);

        if (signature.BodyHash.Length == 0)
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM body hash verification failed: the bh tag is empty.");
        }

        var canonicalizedBody = DkimCanonicalizer.CanonicalizeBody(
            body,
            signature.BodyCanonicalization);
        ReadOnlySpan<byte> bodyBytes = Encoding.Latin1.GetBytes(canonicalizedBody);

        if (signature.BodyLength is int bodyLength)
        {
            if (bodyLength > bodyBytes.Length)
            {
                return new DkimEvaluation(
                    DkimResult.PermFail,
                    "DKIM body hash verification failed: the l tag exceeds the canonicalized body length.");
            }

            bodyBytes = bodyBytes[..bodyLength];
        }

        var computedHash = ComputeBodyHash(signature.Algorithm, bodyBytes);
        if (computedHash.Length == 0)
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM body hash verification failed: unsupported DKIM hash algorithm.");
        }

        if (!signature.BodyHash.Equals(computedHash, StringComparison.Ordinal))
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM body hash verification failed: the bh tag does not match the canonicalized body.");
        }

        return new DkimEvaluation(
            DkimResult.Neutral,
            "DKIM body hash verified; header cryptographic verification is not evaluated in this slice.");
    }

    private static string ComputeBodyHash(
        string algorithm,
        ReadOnlySpan<byte> bodyBytes)
    {
        var hash = algorithm.ToLowerInvariant() switch
        {
            "rsa-sha1" => SHA1.HashData(bodyBytes),
            "rsa-sha256" => SHA256.HashData(bodyBytes),
            _ => []
        };

        return hash.Length == 0
            ? string.Empty
            : Convert.ToBase64String(hash);
    }
}
