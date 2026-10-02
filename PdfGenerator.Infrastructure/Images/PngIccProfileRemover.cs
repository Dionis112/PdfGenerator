using System.Buffers.Binary;

namespace Company.XmlFo;

/// <summary>Removes PNG iCCP chunks without decoding/re-encoding pixel data.</summary>
public static class PngIccProfileRemover
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static bool IsPng(ReadOnlySpan<byte> data) => data.StartsWith(Signature);

    /// <summary>Validates chunk boundaries and retained CRCs; discards even invalid ICC payloads/CRCs.</summary>
    public static byte[] RemoveIccProfile(ReadOnlySpan<byte> png)
    {
        if (!IsPng(png))
        {
            throw new InvalidDataException("Invalid PNG signature.");
        }

        using var output = new MemoryStream(png.Length);
        output.Write(Signature);
        int position = Signature.Length;
        bool first = true;
        bool hasImageData = false;
        while (position < png.Length)
        {
            if (png.Length - position < 12)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }

            uint length = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(position, 4));
            if (length > int.MaxValue || (long)length + 12 > png.Length - position)
            {
                throw new InvalidDataException("Invalid PNG chunk length.");
            }

            int size = (int)length;
            ReadOnlySpan<byte> chunk = png.Slice(position, size + 12);
            ReadOnlySpan<byte> type = chunk.Slice(4, 4);
            if (first && (!type.SequenceEqual("IHDR"u8) || size != 13))
            {
                throw new InvalidDataException("PNG must begin with IHDR.");
            }

            if (!first && type.SequenceEqual("IHDR"u8))
            {
                throw new InvalidDataException("Duplicate PNG header.");
            }

            first = false;
            if (type.SequenceEqual("IDAT"u8))
            {
                hasImageData = true;
            }

            if (!type.SequenceEqual("iCCP"u8))
            {
                uint expected = BinaryPrimitives.ReadUInt32BigEndian(chunk.Slice(size + 8, 4));
                if (ComputeCrc(chunk.Slice(4, size + 4)) != expected)
                {
                    throw new InvalidDataException("PNG chunk CRC mismatch.");
                }

                output.Write(chunk);
            }
            position += chunk.Length;
            if (type.SequenceEqual("IEND"u8))
            {
                if (size != 0 || !hasImageData || position != png.Length)
                {
                    throw new InvalidDataException("Invalid PNG end/data chunks.");
                }

                return output.ToArray();
            }
        }
        throw new InvalidDataException("PNG is missing IEND.");
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
        {
            crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateCrcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            }

            table[i] = crc;
        }
        return table;
    }
}
