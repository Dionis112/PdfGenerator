namespace PdfGenerator;


/// <summary>
/// <para>Provides the IFoPdfRenderer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IFoPdfRenderer для преобразования PDF.</para>
/// </summary>
public interface IFoPdfRenderer
{

    /// <summary>
    /// <para>Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.</para>
    /// <para>Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    Task RenderFileAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default);
}
