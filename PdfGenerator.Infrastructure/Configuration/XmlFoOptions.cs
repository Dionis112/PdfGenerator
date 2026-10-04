namespace PdfGenerator;


/// <summary>
/// <para>Provides the XmlFoOptions contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XmlFoOptions для преобразования PDF.</para>
/// </summary>
public sealed record XmlFoOptions
{

    /// <summary>
    /// <para>Maximum simultaneous synchronous XSLT operations; a canceled operation retains its slot until the engine finishes.</para>
    /// <para>Максимум одновременных синхронных операций XSLT; отменённая операция сохраняет слот до завершения движка.</para>
    /// </summary>
    public int MaxConcurrency { get; init; } = 2;

    /// <summary>
    /// <para>Maximum XML source bytes, enforced while reading files and streams.</para>
    /// <para>Максимум байтов входного XML, проверяемый при чтении файлов и потоков.</para>
    /// </summary>
    public long MaxInputBytes { get; init; } = 16 * 1024 * 1024;

    /// <summary>
    /// <para>Maximum generated FO bytes, enforced before publishing the intermediate file.</para>
    /// <para>Максимум байтов сформированного FO, проверяемый до публикации промежуточного файла.</para>
    /// </summary>
    public long MaxOutputBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// <para>Maximum source bytes buffered in memory before spilling to a temporary file; does not limit the Saxon tree.</para>
    /// <para>Максимум байтов входа в буфере памяти до переноса во временный файл; не ограничивает дерево Saxon.</para>
    /// </summary>
    public int MemoryBufferThresholdBytes { get; init; } = 256 * 1024;

    /// <summary>
    /// <para>Maximum aggregate stylesheet snapshot bytes, including transitive include/import modules.</para>
    /// <para>Максимум суммарных байтов снимка шаблона, включая транзитивные модули include/import.</para>
    /// </summary>
    public long MaxStylesheetBytes { get; init; } = 16 * 1024 * 1024;

    /// <summary>
    /// <para>Maximum retained compiled templates; zero disables retention. Active compilations remain tracked separately.</para>
    /// <para>Максимум сохраняемых скомпилированных шаблонов; ноль отключает хранение. Активные компиляции учитываются отдельно.</para>
    /// </summary>
    public int TemplateCacheEntries { get; init; } = 32;

    /// <summary>
    /// <para>Estimated retained-template byte budget, not a hard managed-memory limit.</para>
    /// <para>Оценочный бюджет байтов сохраняемых шаблонов, а не строгий лимит managed-памяти.</para>
    /// </summary>
    public long TemplateCacheBytes { get; init; } = 128 * 1024 * 1024;

    /// <summary>
    /// <para>Idle lifetime of a compiled template; expiration is checked on cache access.</para>
    /// <para>Срок простоя скомпилированного шаблона; истечение проверяется при обращении к кэшу.</para>
    /// </summary>
    public TimeSpan TemplateCacheIdleTime { get; init; } = TimeSpan.FromMinutes(30);
}
