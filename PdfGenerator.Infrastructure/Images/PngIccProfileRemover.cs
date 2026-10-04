using System.Buffers.Binary;

namespace PdfGenerator;


/// <summary>
/// <para>Removes PNG iCCP chunks without decoding pixels; validates retained CRCs and preserves transparency.</para>
/// <para>Удаляет блоки PNG iCCP без декодирования пикселей; проверяет CRC сохранённых блоков и сохраняет прозрачность.</para>
/// </summary>
public static class PngIccProfileRemover
{

    /// <summary>
    /// <para>Gets or defines the Signature value used by PngIccProfileRemover.</para>
    /// <para>Возвращает или определяет значение Signature, используемое PngIccProfileRemover.</para>
    /// </summary>
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// <para>Gets or defines the CrcTable value used by PngIccProfileRemover.</para>
    /// <para>Возвращает или определяет значение CrcTable, используемое PngIccProfileRemover.</para>
    /// </summary>
    private static readonly uint[] CrcTable = CreateCrcTable();


    /// <summary>
    /// <para>Executes IsPng as part of the PngIccProfileRemover conversion workflow.</para>
    /// <para>Выполняет IsPng в цепочке преобразования PngIccProfileRemover.</para>
    /// </summary>
    /// <param name="data"><para>The data argument.</para><para>Аргумент data.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static bool IsPng(ReadOnlySpan<byte> data) => data.StartsWith(Signature);


    /// <summary>
    /// <para>Validates PNG chunk boundaries and retained CRCs, removes iCCP, and copies pixel chunks without decoding or re-encoding them.</para>
    /// <para>Проверяет границы блоков PNG и CRC сохранённых блоков, удаляет iCCP и копирует блоки пикселей без декодирования и перекодирования.</para>
    /// </summary>
    /// <param name="png"><para>The png argument.</para><para>Аргумент png.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
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


    /// <summary>
    /// <para>Executes ComputeCrc as part of the PngIccProfileRemover conversion workflow.</para>
    /// <para>Выполняет ComputeCrc в цепочке преобразования PngIccProfileRemover.</para>
    /// </summary>
    /// <param name="data"><para>The data argument.</para><para>Аргумент data.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
        {
            crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        }

        return ~crc;
    }


    /// <summary>
    /// <para>Validates PNG chunk boundaries and retained CRCs, removes iCCP, and copies pixel chunks without decoding or re-encoding them.</para>
    /// <para>Проверяет границы блоков PNG и CRC сохранённых блоков, удаляет iCCP и копирует блоки пикселей без декодирования и перекодирования.</para>
    /// </summary>
    /// <param name="input"><para>The input argument.</para><para>Аргумент input.</para></param>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="maxBytes"><para>The maxBytes argument.</para><para>Аргумент maxBytes.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static void RemoveIccProfile(Stream input, Stream output, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (maxBytes < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }
        byte[] signature = new byte[8];
        input.ReadExactly(signature);
        if (!IsPng(signature))
        {
            throw new InvalidDataException("Invalid PNG signature.");
        }
        output.Write(signature);
        long consumed = 8;
        byte[] header = new byte[8];
        byte[] buffer = new byte[65536];
        byte[] crcBytes = new byte[4];
        bool first = true;
        bool hasData = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            input.ReadExactly(header);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length > maxBytes - consumed - 12)
            {
                throw new InvalidDataException("PNG exceeds its byte limit or has invalid chunk bounds.");
            }
            consumed += length + 12L;
            bool ihdr = header.AsSpan(4).SequenceEqual("IHDR"u8);
            bool end = header.AsSpan(4).SequenceEqual("IEND"u8);
            bool discard = header.AsSpan(4).SequenceEqual("iCCP"u8);
            if (first && (!ihdr || length != 13) || !first && ihdr)
            {
                throw new InvalidDataException("Invalid PNG header.");
            }
            first = false;
            hasData |= header.AsSpan(4).SequenceEqual("IDAT"u8);
            uint crc = uint.MaxValue;
            foreach (byte value in header.AsSpan(4))
            {
                crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
            }
            if (!discard)
            {
                output.Write(header);
            }
            long remaining = length;
            while (remaining != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = (int)Math.Min(remaining, buffer.Length);
                input.ReadExactly(buffer.AsSpan(0, count));
                if (!discard)
                {
                    for (int i = 0; i < count; i++)
                    {
                        crc = CrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8);
                    }
                    output.Write(buffer, 0, count);
                }
                remaining -= count;
            }
            input.ReadExactly(crcBytes);
            if (!discard)
            {
                if (~crc != BinaryPrimitives.ReadUInt32BigEndian(crcBytes))
                {
                    throw new InvalidDataException("PNG chunk CRC mismatch.");
                }
                output.Write(crcBytes);
            }
            if (end)
            {
                if (length != 0 || !hasData || input.ReadByte() != -1)
                {
                    throw new InvalidDataException("Invalid PNG end/data chunks.");
                }
                return;
            }
        }
    }


    /// <summary>
    /// <para>Executes CreateCrcTable as part of the PngIccProfileRemover conversion workflow.</para>
    /// <para>Выполняет CreateCrcTable в цепочке преобразования PngIccProfileRemover.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
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
