namespace PdfGenerator;


/// <summary>
/// <para>Provides the ConversionSettings contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ConversionSettings для преобразования PDF.</para>
/// </summary>
public sealed record ConversionSettings
{

    /// <summary>
    /// <para>Gets or defines the Timeout value used by ConversionSettings.</para>
    /// <para>Возвращает или определяет значение Timeout, используемое ConversionSettings.</para>
    /// </summary>
    public TimeSpan? Timeout
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the MaxInputBytes value used by ConversionSettings.</para>
    /// <para>Возвращает или определяет значение MaxInputBytes, используемое ConversionSettings.</para>
    /// </summary>
    public long? MaxInputBytes
    {
        get; init;
    }
}


/// <summary>
/// <para>Provides the PdfSchedulerOptions contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PdfSchedulerOptions для преобразования PDF.</para>
/// </summary>
public sealed record PdfSchedulerOptions
{
    /// <summary>
    /// <para>Immutable profile version copied into conversion results.</para>
    /// <para>Неизменяемая версия профиля, включаемая в результат преобразования.</para>
    /// </summary>
    public string? ProfileVersion
    {
        get; init;
    }

    /// <summary>
    /// <para>Maximum admitted conversion chains executing at once; covers preparation through publication.</para>
    /// <para>Максимум одновременно исполняемых цепочек преобразования; охватывает подготовку и публикацию.</para>
    /// </summary>
    public int MaxActiveJobs { get; init; } = 2;

    /// <summary>
    /// <para>Maximum additional admitted jobs waiting for an execution slot; excess requests fail immediately.</para>
    /// <para>Максимум дополнительных принятых заданий, ожидающих слот исполнения; лишние запросы отклоняются сразу.</para>
    /// </summary>
    public int MaxQueuedJobs { get; init; } = 8;

    /// <summary>
    /// <para>Maximum wait for an execution slot, independent of the job execution timeout.</para>
    /// <para>Максимальное ожидание слота исполнения, независимое от тайм-аута вычисления задания.</para>
    /// </summary>
    public TimeSpan QueueTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <para>Grace period for draining accepted jobs before requesting cancellation. In-process engines still must finish.</para>
    /// <para>Период завершения принятых заданий до запроса отмены. Движки внутри процесса всё равно должны завершиться.</para>
    /// </summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(30);
}


/// <summary>
/// <para>Provides the ConversionQueueFullException contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ConversionQueueFullException для преобразования PDF.</para>
/// </summary>
public sealed class ConversionQueueFullException() : Exception("The PDF conversion queue is full.");


/// <summary>
/// <para>Provides the ConversionOutputConflictException contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ConversionOutputConflictException для преобразования PDF.</para>
/// </summary>
public sealed class ConversionOutputConflictException() : Exception("A conversion already owns this output path.");
