namespace PdfGenerator;


/// <summary>
/// <para>Provides the IXsltFoTransformer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IXsltFoTransformer для преобразования PDF.</para>
/// </summary>
public interface IXsltFoTransformer
{

    /// <summary>
    /// <para>Prepares bounded input snapshots and transforms with job-local parameters; in-process cancellation is observed around synchronous Saxon execution.</para>
    /// <para>Подготавливает ограниченные снимки входов и преобразует с параметрами задания; отмена внутри процесса проверяется вокруг синхронного выполнения Saxon.</para>
    /// </summary>
    /// <param name="xmlPath"><para>Local XML input path.</para><para>Путь локального входного XML.</para></param>
    /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    Task TransformFileAsync(string xmlPath, string xsltPath, string foPath, CancellationToken cancellationToken = default);
}
