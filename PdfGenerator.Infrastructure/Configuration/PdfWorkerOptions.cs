namespace PdfGenerator;


/// <summary>
/// <para>Provides the PdfWorkerOptions contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PdfWorkerOptions для преобразования PDF.</para>
/// </summary>
public sealed record PdfWorkerOptions
{

    /// <summary>
    /// <para>Maximum child processes; each worker executes at most one document at a time.</para>
    /// <para>Максимум дочерних процессов; каждый worker одновременно выполняет не более одного документа.</para>
    /// </summary>
    public int MaxWorkers { get; init; } = 1;

    /// <summary>
    /// <para>Total reserved worker memory; concurrency cannot reserve more than this budget. Zero disables reservation validation.</para>
    /// <para>Общий резерв памяти worker; параллельность не может резервировать больше этого бюджета. Ноль отключает проверку резерва.</para>
    /// </summary>
    public long MemoryBudgetBytes { get; init; } = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// <para>Deadline for process startup and the compatible protocol handshake.</para>
    /// <para>Предельное время запуска процесса и согласования совместимого протокола.</para>
    /// </summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <para>Default execution deadline, enforced by the parent through process termination. A request may override it.</para>
    /// <para>Срок исполнения по умолчанию, обеспечиваемый родителем через завершение процесса. Запрос может переопределить его.</para>
    /// </summary>
    public TimeSpan JobTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// <para>Time allowed for a stop request before killing the process tree and awaiting confirmed exit.</para>
    /// <para>Время на запрос остановки до принудительного завершения дерева процессов и ожидания подтверждённого выхода.</para>
    /// </summary>
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// <para>Gets the observed working-set threshold that triggers worker termination; this is not an operating-system hard memory limit. Zero disables monitoring.</para>
    /// <para>Порог наблюдаемого working set для остановки worker; это не строгий лимит памяти ОС. Ноль отключает наблюдение.</para>
    /// </summary>
    public long MaxWorkingSetBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>
    /// <para>Completed job count after which an idle worker is stopped and replaced on its next use.</para>
    /// <para>Число завершённых заданий, после которого свободный worker останавливается и заменяется при следующем использовании.</para>
    /// </summary>
    public int RecycleAfterJobs { get; init; } = 100;

    /// <summary>
    /// <para>Consecutive startup failures that open the slot startup circuit; successful startup resets the counter.</para>
    /// <para>Число последовательных сбоев запуска, блокирующих дальнейшие старты слота; успешный запуск сбрасывает счётчик.</para>
    /// </summary>
    public int MaxStartupFailures { get; init; } = 3;

    /// <summary>
    /// <para>Delay before another startup attempt after a failure; failed documents are not retried automatically.</para>
    /// <para>Задержка перед новой попыткой запуска после сбоя; документы автоматически не повторяются.</para>
    /// </summary>
    public TimeSpan StartupFailureBackoff { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// <para>Optional local parent directory for pool, job and worker-runtime files; defaults to the system temporary directory.</para>
    /// <para>Необязательный локальный родительский каталог файлов пула, заданий и runtime worker; по умолчанию используется системный временный каталог.</para>
    /// </summary>
    public string? WorkingDirectory
    {
        get; init;
    }

    /// <summary>
    /// <para>Installed dotnet host executable used for framework-dependent worker launch.</para>
    /// <para>Исполняемый файл установленного dotnet host для запуска framework-dependent worker.</para>
    /// </summary>
    public string DotnetHostPath { get; init; } = "dotnet";
}


/// <summary>
/// <para>Provides the WorkerFailureKind contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerFailureKind для преобразования PDF.</para>
/// </summary>
public enum WorkerFailureKind
{
    /// <summary>
    /// <para>The worker failed to start or complete its handshake.</para>
    /// <para>Worker не запустился или не завершил согласование протокола.</para>
    /// </summary>
    Startup,
    /// <summary>
    /// <para>A worker message or completed-result validation violated the protocol.</para>
    /// <para>Сообщение worker или проверка готового результата нарушили протокол.</para>
    /// </summary>
    Protocol,
    /// <summary>
    /// <para>The process exited without a complete result; this does not imply an out-of-memory failure.</para>
    /// <para>Процесс завершился без готового результата; это не означает доказанный недостаток памяти.</para>
    /// </summary>
    Crash,
    /// <summary>
    /// <para>The parent terminated a job after its execution deadline.</para>
    /// <para>Родитель завершил задание после истечения срока исполнения.</para>
    /// </summary>
    Timeout,
    /// <summary>
    /// <para>The observed working set exceeded the configured threshold; this is not an operating-system hard limit.</para>
    /// <para>Наблюдаемый working set превысил настроенный порог; это не строгий лимит операционной системы.</para>
    /// </summary>
    MemoryLimit
}


/// <summary>
/// <para>Provides the PdfWorkerException contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PdfWorkerException для преобразования PDF.</para>
/// </summary>
/// <param name="kind"><para>The kind argument.</para><para>Аргумент kind.</para></param>
/// <param name="message"><para>The message argument.</para><para>Аргумент message.</para></param>
/// <param name="innerException"><para>The innerException argument.</para><para>Аргумент innerException.</para></param>
public sealed class PdfWorkerException(WorkerFailureKind kind, string message, Exception? innerException = null) : Exception(message, innerException)
{

    /// <summary>
    /// <para>Gets or defines the Kind value used by PdfWorkerException.</para>
    /// <para>Возвращает или определяет значение Kind, используемое PdfWorkerException.</para>
    /// </summary>
    public WorkerFailureKind Kind { get; } = kind;
}


/// <summary>
/// <para>Provides the WorkerPoolStatistics contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerPoolStatistics для преобразования PDF.</para>
/// </summary>
/// <param name="Starts"><para>The Starts argument.</para><para>Аргумент Starts.</para></param>
/// <param name="CompletedJobs"><para>The CompletedJobs argument.</para><para>Аргумент CompletedJobs.</para></param>
/// <param name="Recycles"><para>The Recycles argument.</para><para>Аргумент Recycles.</para></param>
/// <param name="Crashes"><para>The Crashes argument.</para><para>Аргумент Crashes.</para></param>
/// <param name="Timeouts"><para>The Timeouts argument.</para><para>Аргумент Timeouts.</para></param>
/// <param name="MemoryTerminations"><para>The MemoryTerminations argument.</para><para>Аргумент MemoryTerminations.</para></param>
public sealed record WorkerPoolStatistics(long Starts, long CompletedJobs, long Recycles, long Crashes, long Timeouts, long MemoryTerminations);
