using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PdfGenerator.TestData;

// Deterministic red RGBA square, with an intentionally unusable ICC payload.
internal static class PngFixture
{
    public static byte[] Create(bool withIcc = true, bool badProfileCrc = false)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, 16);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 16);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);
        if (withIcc)
        {
            byte[] profile = [.. Encoding.ASCII.GetBytes("Broken ICC\0\0"), .. Compress("invalid ICC payload"u8.ToArray())];
            Chunk(output, "iCCP", profile, badProfileCrc);
        }
        byte[] rows = new byte[16 * (1 + 16 * 4)];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int offset = y * 65 + 1 + x * 4;
                rows[offset] = 255;
                rows[offset + 3] = x < 8 ? (byte)255 : (byte)128;
            }
        }

        Chunk(output, "IDAT", Compress(rows));
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private static void Chunk(Stream output, string name, byte[] data, bool badCrc = false)
    {
        byte[] type = Encoding.ASCII.GetBytes(name);
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(value, (uint)data.Length);
        output.Write(value);
        output.Write(type);
        output.Write(data);
        uint crc = uint.MaxValue;
        foreach (byte b in type.Concat(data))
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(value, badCrc ? 0u : ~crc);
        output.Write(value);
    }
}
