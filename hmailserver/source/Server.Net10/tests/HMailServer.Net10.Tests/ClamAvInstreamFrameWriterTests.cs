using System.Text;
using HMailServer.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class ClamAvInstreamFrameWriterTests
{
    [TestMethod]
    public async Task WriteChunkAsync_WritesBigEndianLengthPrefix()
    {
        await using var stream = new MemoryStream();

        await ClamAvInstreamFrameWriter.WriteChunkAsync(
            stream,
            Encoding.ASCII.GetBytes("abc"),
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new byte[] { 0, 0, 0, 3, (byte)'a', (byte)'b', (byte)'c' },
            stream.ToArray());
    }

    [TestMethod]
    public async Task WriteEndAsync_WritesZeroLengthFrame()
    {
        await using var stream = new MemoryStream();

        await ClamAvInstreamFrameWriter.WriteEndAsync(stream, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, stream.ToArray());
    }
}
