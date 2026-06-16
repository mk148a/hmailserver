using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace HMailServer.Security;

public static class LegacyBlowfishPasswordCipher
{
    private const int BlockSize = 8;
    private const int InitialPArrayLength = 18;
    private const int InitialSBoxesLength = 4 * 256;
    private const int InitialStateLength = InitialPArrayLength + InitialSBoxesLength;
    private const string LegacyKey = "THIS_KEY_IS_NOT_SECRET";
    private const string ResourceName = "HMailServer.Security.Legacy.BlowFish.h2";

    private static readonly LegacyBlowfishTransform Transform = LegacyBlowfishTransform.Create();

    public static string Encrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var input = Encoding.Latin1.GetBytes(value);
        var buffer = new byte[GetOutputLength(input.Length)];
        input.CopyTo(buffer.AsSpan());

        Transform.EncodeInPlace(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    public static bool TryDecrypt(string encrypted, out string decrypted)
    {
        decrypted = string.Empty;

        if (string.IsNullOrEmpty(encrypted))
        {
            return true;
        }

        if ((encrypted.Length & 1) != 0)
        {
            return false;
        }

        byte[] buffer;
        try
        {
            buffer = Convert.FromHexString(encrypted);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (buffer.Length == 0)
        {
            return true;
        }

        if (buffer.Length % BlockSize != 0)
        {
            return false;
        }

        Transform.DecodeInPlace(buffer);

        var terminatorIndex = Array.IndexOf(buffer, (byte)0);
        var length = terminatorIndex >= 0 ? terminatorIndex : buffer.Length;
        decrypted = Encoding.Latin1.GetString(buffer, 0, length);
        return true;
    }

    private static int GetOutputLength(int inputLength)
    {
        var remainder = inputLength % BlockSize;
        return remainder == 0 ? inputLength : inputLength + BlockSize - remainder;
    }

    private static uint[] LoadInitialState()
    {
        using var stream = typeof(LegacyBlowfishPasswordCipher).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded Blowfish table resource '{ResourceName}'.");
        using var reader = new StreamReader(stream, Encoding.ASCII);

        var source = reader.ReadToEnd();
        var values = new uint[InitialStateLength];
        var count = 0;

        for (var index = 0; index < source.Length - 1; index++)
        {
            if (source[index] != '0' || (source[index + 1] != 'x' && source[index + 1] != 'X'))
            {
                continue;
            }

            var start = index + 2;
            var end = start;
            while (end < source.Length && IsHexDigit(source[end]))
            {
                end++;
            }

            if (end == start)
            {
                continue;
            }

            if (count == values.Length)
            {
                throw new InvalidOperationException("The embedded Blowfish table contains too many values.");
            }

            values[count++] = uint.Parse(
                source.AsSpan(start, end - start),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
            index = end;
        }

        if (count != values.Length)
        {
            throw new InvalidOperationException("The embedded Blowfish table is incomplete.");
        }

        return values;
    }

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private sealed class LegacyBlowfishTransform
    {
        private readonly uint[] _pArray = new uint[InitialPArrayLength];
        private readonly uint[] _sBoxes = new uint[InitialSBoxesLength];

        private LegacyBlowfishTransform(ReadOnlySpan<uint> initialState)
        {
            initialState[..InitialPArrayLength].CopyTo(_pArray);
            initialState[InitialPArrayLength..].CopyTo(_sBoxes);
        }

        public static LegacyBlowfishTransform Create()
        {
            var transform = new LegacyBlowfishTransform(LoadInitialState());
            transform.Initialize(Encoding.ASCII.GetBytes(LegacyKey));
            return transform;
        }

        public void EncodeInPlace(Span<byte> buffer)
        {
            for (var offset = 0; offset < buffer.Length; offset += BlockSize)
            {
                var left = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..(offset + 4)]);
                var right = BinaryPrimitives.ReadUInt32LittleEndian(buffer[(offset + 4)..(offset + BlockSize)]);

                Encipher(ref left, ref right);

                BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..(offset + 4)], left);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer[(offset + 4)..(offset + BlockSize)], right);
            }
        }

        public void DecodeInPlace(Span<byte> buffer)
        {
            for (var offset = 0; offset < buffer.Length; offset += BlockSize)
            {
                var left = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..(offset + 4)]);
                var right = BinaryPrimitives.ReadUInt32LittleEndian(buffer[(offset + 4)..(offset + BlockSize)]);

                Decipher(ref left, ref right);

                BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..(offset + 4)], left);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer[(offset + 4)..(offset + BlockSize)], right);
            }
        }

        private void Initialize(ReadOnlySpan<byte> key)
        {
            var keyIndex = 0;
            for (var index = 0; index < _pArray.Length; index++)
            {
                var data =
                    ((uint)key[keyIndex] << 24) |
                    ((uint)key[(keyIndex + 1) % key.Length] << 16) |
                    ((uint)key[(keyIndex + 2) % key.Length] << 8) |
                    key[(keyIndex + 3) % key.Length];

                _pArray[index] ^= data;
                keyIndex = (keyIndex + 4) % key.Length;
            }

            uint dataLeft = 0;
            uint dataRight = 0;

            for (var index = 0; index < _pArray.Length; index += 2)
            {
                Encipher(ref dataLeft, ref dataRight);
                _pArray[index] = dataLeft;
                _pArray[index + 1] = dataRight;
            }

            for (var box = 0; box < 4; box++)
            {
                var boxOffset = box * 256;
                for (var index = 0; index < 256; index += 2)
                {
                    Encipher(ref dataLeft, ref dataRight);
                    _sBoxes[boxOffset + index] = dataLeft;
                    _sBoxes[boxOffset + index + 1] = dataRight;
                }
            }
        }

        private void Encipher(ref uint left, ref uint right)
        {
            unchecked
            {
                var xl = left ^ _pArray[0];
                var xr = right;

                xr ^= F(xl) ^ _pArray[1];
                xl ^= F(xr) ^ _pArray[2];
                xr ^= F(xl) ^ _pArray[3];
                xl ^= F(xr) ^ _pArray[4];
                xr ^= F(xl) ^ _pArray[5];
                xl ^= F(xr) ^ _pArray[6];
                xr ^= F(xl) ^ _pArray[7];
                xl ^= F(xr) ^ _pArray[8];
                xr ^= F(xl) ^ _pArray[9];
                xl ^= F(xr) ^ _pArray[10];
                xr ^= F(xl) ^ _pArray[11];
                xl ^= F(xr) ^ _pArray[12];
                xr ^= F(xl) ^ _pArray[13];
                xl ^= F(xr) ^ _pArray[14];
                xr ^= F(xl) ^ _pArray[15];
                xl ^= F(xr) ^ _pArray[16];
                xr ^= _pArray[17];

                left = xr;
                right = xl;
            }
        }

        private void Decipher(ref uint left, ref uint right)
        {
            unchecked
            {
                var xl = left ^ _pArray[17];
                var xr = right;

                xr ^= F(xl) ^ _pArray[16];
                xl ^= F(xr) ^ _pArray[15];
                xr ^= F(xl) ^ _pArray[14];
                xl ^= F(xr) ^ _pArray[13];
                xr ^= F(xl) ^ _pArray[12];
                xl ^= F(xr) ^ _pArray[11];
                xr ^= F(xl) ^ _pArray[10];
                xl ^= F(xr) ^ _pArray[9];
                xr ^= F(xl) ^ _pArray[8];
                xl ^= F(xr) ^ _pArray[7];
                xr ^= F(xl) ^ _pArray[6];
                xl ^= F(xr) ^ _pArray[5];
                xr ^= F(xl) ^ _pArray[4];
                xl ^= F(xr) ^ _pArray[3];
                xr ^= F(xl) ^ _pArray[2];
                xl ^= F(xr) ^ _pArray[1];
                xr ^= _pArray[0];

                left = xr;
                right = xl;
            }
        }

        private uint F(uint value)
        {
            unchecked
            {
                var byte0 = (int)((value >> 24) & 0xff);
                var byte1 = (int)((value >> 16) & 0xff);
                var byte2 = (int)((value >> 8) & 0xff);
                var byte3 = (int)(value & 0xff);

                return ((_sBoxes[byte0] + _sBoxes[256 + byte1]) ^ _sBoxes[512 + byte2]) + _sBoxes[768 + byte3];
            }
        }
    }
}
