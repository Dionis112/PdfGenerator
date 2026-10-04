namespace PdfGenerator;

/// <summary>
/// <para>Rejects writes before PDF output exceeds the byte or remaining temporary-file limit.</para>
/// <para>Отклоняет запись до превышения лимита PDF или остатка квоты временных файлов.</para>
/// </summary>
/// <param name="output"><para>Owned Java file stream.</para><para>Принадлежащий операции файловый поток Java.</para></param>
/// <param name="limit"><para>Maximum bytes to write.</para><para>Максимальное число записываемых байтов.</para></param>
internal sealed class LimitedPdfOutputStream(java.io.OutputStream output, long limit) : java.io.FilterOutputStream(output)
{
    /// <summary><para>Number of bytes already written.</para><para>Число уже записанных байтов.</para></summary>
    private long written;
    /// <summary><para>Writes one byte within the quota.</para><para>Записывает один байт в пределах квоты.</para></summary>
    /// <param name="value"><para>Byte value.</para><para>Значение байта.</para></param>
    public override void write(int value)
    {
        Reserve(1);
        @out.write(value);
    }
    /// <summary><para>Writes a byte segment within the quota.</para><para>Записывает часть массива в пределах квоты.</para></summary>
    /// <param name="buffer"><para>Source bytes.</para><para>Исходные байты.</para></param>
    /// <param name="offset"><para>First byte offset.</para><para>Смещение первого байта.</para></param>
    /// <param name="length"><para>Number of bytes.</para><para>Число байтов.</para></param>
    public override void write(byte[] buffer, int offset, int length)
    {
        Reserve(length);
        @out.write(buffer, offset, length);
    }
    /// <summary><para>Charges bytes before performing a write.</para><para>Учитывает байты до выполнения записи.</para></summary>
    /// <param name="count"><para>Requested bytes.</para><para>Запрошенное число байтов.</para></param>
    /// <exception cref="java.io.IOException"><para>The quota is exhausted.</para><para>Квота исчерпана.</para></exception>
    private void Reserve(int count)
    {
        if (count < 0 || count > limit - written)
        {
            throw new java.io.IOException("PDF output byte limit exceeded.");
        }
        written += count;
    }
}
