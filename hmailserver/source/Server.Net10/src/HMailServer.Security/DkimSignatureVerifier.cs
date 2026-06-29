using System.Security.Cryptography;
using System.Text;

namespace HMailServer.Security;

public static class DkimSignatureVerifier
{
    public static async ValueTask<DkimEvaluation> VerifyAsync(
        string headerBlock,
        string body,
        string signatureHeaderValue,
        DkimSignature signature,
        IDkimTxtResolver resolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headerBlock);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(signatureHeaderValue);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(resolver);

        var keyLookup = await DkimPublicKeyLookup.LookupAsync(
            signature,
            resolver,
            cancellationToken).ConfigureAwait(false);
        if (keyLookup.Evaluation.Result != DkimResult.Neutral || keyLookup.KeyRecord is null)
        {
            return keyLookup.Evaluation;
        }

        return Verify(
            headerBlock,
            body,
            signatureHeaderValue,
            signature,
            keyLookup.KeyRecord.PublicKey);
    }

    public static DkimEvaluation Verify(
        string headerBlock,
        string body,
        string signatureHeaderValue,
        DkimSignature signature,
        string publicKeyBase64)
    {
        ArgumentNullException.ThrowIfNull(headerBlock);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(signatureHeaderValue);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKeyBase64);

        var bodyHashResult = DkimBodyHashVerifier.VerifyBodyHash(body, signature);
        if (bodyHashResult.Result != DkimResult.Neutral)
        {
            return bodyHashResult;
        }

        return VerifyHeaderSignature(
            headerBlock,
            signatureHeaderValue,
            signature,
            publicKeyBase64);
    }

    private static DkimEvaluation VerifyHeaderSignature(
        string headerBlock,
        string signatureHeaderValue,
        DkimSignature signature,
        string publicKeyBase64)
    {
        if (signature.Signature.Length == 0)
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM header signature verification failed: the b tag is empty.");
        }

        if (!TryGetHashAlgorithm(signature.Algorithm, out var hashAlgorithm))
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM header signature verification failed: unsupported DKIM hash algorithm.");
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature.Signature);
        }
        catch (FormatException)
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM header signature verification failed: the b tag is not valid base64.");
        }

        byte[] publicKeyBytes;
        try
        {
            publicKeyBytes = Convert.FromBase64String(RemoveWhitespace(publicKeyBase64));
        }
        catch (FormatException)
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM header signature verification failed: the public key is not valid base64.");
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
            if (bytesRead != publicKeyBytes.Length)
            {
                return new DkimEvaluation(
                    DkimResult.PermFail,
                    "DKIM header signature verification failed: the public key contains trailing data.");
            }

            var canonicalizedHeader = DkimCanonicalizer.CanonicalizeHeaders(
                headerBlock,
                "DKIM-Signature",
                StripSignatureFieldName(signatureHeaderValue),
                signature.SignedHeaders,
                signature.HeaderCanonicalization,
                out _);
            var canonicalizedHeaderBytes = Encoding.Latin1.GetBytes(canonicalizedHeader);

            var verified = rsa.VerifyData(
                canonicalizedHeaderBytes,
                signatureBytes,
                hashAlgorithm,
                RSASignaturePadding.Pkcs1);

            return verified
                ? new DkimEvaluation(
                    DkimResult.Pass,
                    "DKIM body hash and header signature verified.",
                    [signature.Domain])
                : new DkimEvaluation(
                    DkimResult.PermFail,
                    "DKIM header signature verification failed: the signature does not match the canonicalized headers.");
        }
        catch (CryptographicException)
        {
            return new DkimEvaluation(
                DkimResult.PermFail,
                "DKIM header signature verification failed: the public key or signature could not be evaluated.");
        }
    }

    private static bool TryGetHashAlgorithm(
        string algorithm,
        out HashAlgorithmName hashAlgorithm)
    {
        hashAlgorithm = algorithm.ToLowerInvariant() switch
        {
            "rsa-sha1" => HashAlgorithmName.SHA1,
            "rsa-sha256" => HashAlgorithmName.SHA256,
            _ => default
        };

        return hashAlgorithm.Name is not null;
    }

    private static string StripSignatureFieldName(string value)
    {
        var colonIndex = value.IndexOf(':');
        if (colonIndex <= 0)
        {
            return value;
        }

        var name = value[..colonIndex].Trim();
        return name.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase)
            ? value[(colonIndex + 1)..].TrimStart(' ', '\t')
            : value;
    }

    private static string RemoveWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!DkimCanonicalizer.IsWhitespace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
