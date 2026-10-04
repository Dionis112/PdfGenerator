using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PdfGenerator.TestData;

// Deterministic red RGBA square, with an intentionally unusable ICC payload.

/// <summary>
/// <para>Provides the PngFixture contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PngFixture для преобразования PDF.</para>
/// </summary>
internal static class PngFixture
{

    /// <summary>
    /// <para>Executes Create as part of the PngFixture conversion workflow.</para>
    /// <para>Выполняет Create в цепочке преобразования PngFixture.</para>
    /// </summary>
    /// <param name="withIcc"><para>The withIcc argument.</para><para>Аргумент withIcc.</para></param>
    /// <param name="badProfileCrc"><para>The badProfileCrc argument.</para><para>Аргумент badProfileCrc.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
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


    /// <summary>
    /// <para>Executes Compress as part of the PngFixture conversion workflow.</para>
    /// <para>Выполняет Compress в цепочке преобразования PngFixture.</para>
    /// </summary>
    /// <param name="data"><para>The data argument.</para><para>Аргумент data.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }


    /// <summary>
    /// <para>Executes Chunk as part of the PngFixture conversion workflow.</para>
    /// <para>Выполняет Chunk в цепочке преобразования PngFixture.</para>
    /// </summary>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <param name="data"><para>The data argument.</para><para>Аргумент data.</para></param>
    /// <param name="badCrc"><para>The badCrc argument.</para><para>Аргумент badCrc.</para></param>
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
