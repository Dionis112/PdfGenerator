namespace PdfGenerator;

/// <summary>
/// <para>Marks a generator that independently enforces per-job timeouts by terminating its execution process.</para>
/// <para>Обозначает генератор, самостоятельно обеспечивающий тайм-аут задания завершением процесса исполнения.</para>
/// </summary>
public interface IProcessIsolatedXmlPdfGenerator : IXmlPdfGenerator
{
}


/// <summary>
/// <para>Provides the IXmlPdfGenerator contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IXmlPdfGenerator для преобразования PDF.</para>
/// </summary>
public interface IXmlPdfGenerator
{

    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="xmlPath"><para>Local XML input path.</para><para>Путь локального входного XML.</para></param>
    /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    Task GenerateFileAsync(string xmlPath, string xsltPath, string pdfPath, CancellationToken cancellationToken = default);


    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    Task<ConversionResult> GenerateAsync(ConversionRequest request, string pdfPath, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("This generator does not support requests.");


    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    Task<ConversionResult> GenerateAsync(ConversionRequest request, Stream output, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("This generator does not support streams.");
}
