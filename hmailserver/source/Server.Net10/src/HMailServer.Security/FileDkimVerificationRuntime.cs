using System.Text;
using HMailServer.Core.Abstractions;

namespace HMailServer.Security;

public sealed record FileDkimVerificationRuntimeOptions
{
    public const int LegacyMaximumMessageBytes = 50 * 1024 * 1024;

    public int MaximumMessageBytes { get; init; } = LegacyMaximumMessageBytes;
}

public sealed class FileDkimVerificationRuntime : IDkimVerificationRuntime
{
    private readonly IDkimTxtResolver _resolver;
    private readonly int _maximumMessageBytes;

    public FileDkimVerificationRuntime(
        IDkimTxtResolver resolver,
        FileDkimVerificationRuntimeOptions? options = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _maximumMessageBytes = (options ?? new FileDkimVerificationRuntimeOptions()).MaximumMessageBytes;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_maximumMessageBytes);
    }

    public DkimVerificationResult Verify(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var messageBytes = ReadBounded(file);
        if (messageBytes is null)
        {
            return DkimVerificationResult.Neutral;
        }

        var evaluation = DkimMessageVerifier
            .VerifyAsync(Encoding.Latin1.GetString(messageBytes), _resolver, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return evaluation.Result switch
        {
            DkimResult.Neutral => DkimVerificationResult.Neutral,
            DkimResult.Pass => DkimVerificationResult.Pass,
            DkimResult.TempFail => DkimVerificationResult.TempFail,
            DkimResult.PermFail => DkimVerificationResult.PermFail,
            _ => DkimVerificationResult.TempFail
        };
    }

    private byte[]? ReadBounded(string file)
    {
        var fileInfo = new FileInfo(file);
        if (fileInfo.Length > _maximumMessageBytes)
        {
            return null;
        }

        using var input = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        using var output = new MemoryStream(capacity: (int)fileInfo.Length);
        var buffer = new byte[81920];

        int bytesRead;
        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + bytesRead > _maximumMessageBytes)
            {
                return null;
            }

            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }
}
