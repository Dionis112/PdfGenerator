namespace PdfGenerator;

// Caller streams are never disposed or sought. Small inputs stay in a bounded buffer.

/// <summary>
/// <para>Provides the PreparedXmlSource contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PreparedXmlSource для преобразования PDF.</para>
/// </summary>
internal sealed class PreparedXmlSource : IDisposable
{

    /// <summary>
    /// <para>Gets or defines the Stream value used by PreparedXmlSource.</para>
    /// <para>Возвращает или определяет значение Stream, используемое PreparedXmlSource.</para>
    /// </summary>
    public Stream Stream
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the BaseUri value used by PreparedXmlSource.</para>
    /// <para>Возвращает или определяет значение BaseUri, используемое PreparedXmlSource.</para>
    /// </summary>
    public Uri? BaseUri
    {
        get;
    }


    /// <summary>
    /// <para>Initializes PreparedXmlSource with an immutable snapshot of service settings.</para>
    /// <para>Создаёт PreparedXmlSource со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="stream"><para>The stream argument.</para><para>Аргумент stream.</para></param>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    private PreparedXmlSource(Stream stream, Uri? baseUri)
    {
        Stream = stream;
        BaseUri = baseUri;
    }


    /// <summary>
    /// <para>Executes CreateAsync as part of the PreparedXmlSource conversion workflow.</para>
    /// <para>Выполняет CreateAsync в цепочке преобразования PreparedXmlSource.</para>
    /// </summary>
    /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
    /// <param name="limit"><para>The limit argument.</para><para>Аргумент limit.</para></param>
    /// <param name="threshold"><para>The threshold argument.</para><para>Аргумент threshold.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static async Task<PreparedXmlSource> CreateAsync(XmlSource source, long limit, int threshold, CancellationToken token)
    {
        Stream input = source.Stream ?? File.OpenRead(source.FilePath!);
        Stream buffer = new MemoryStream();
        try
        {
            byte[] bytes = new byte[64 * 1024];
            long length = 0;
            int read;
            while ((read = await input.ReadAsync(bytes, token).ConfigureAwait(false)) != 0)
            {
                if (read > limit - length)
                {
                    throw new IOException("XML source exceeds the byte limit.");
                }
                length += read;
                if (buffer is MemoryStream && length > threshold)
                {
                    var file = new FileStream(Path.Combine(Path.GetTempPath(), "xmlpdf-input-" + Guid.NewGuid().ToString("N")),
                        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
                    try
                    {
                        buffer.Position = 0;
                        await buffer.CopyToAsync(file, token).ConfigureAwait(false);
                    }
                    catch { file.Dispose(); throw; }
                    buffer.Dispose();
                    buffer = file;
                }
                await buffer.WriteAsync(bytes.AsMemory(0, read), token).ConfigureAwait(false);
            }
            buffer.Position = 0;
            return new(buffer, source.BaseUri);
        }
        catch { buffer.Dispose(); throw; }
        finally { if (source.FilePath is not null) { input.Dispose(); } }
    }


    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose() => Stream.Dispose();
}
