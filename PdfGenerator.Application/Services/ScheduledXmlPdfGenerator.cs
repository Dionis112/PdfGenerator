using System.Collections.Concurrent;
using System.Diagnostics;

namespace PdfGenerator;


/// <summary>
/// <para>Bounds complete conversion chains and queue waits, rejects concurrent ownership of an output path, and drains jobs on shutdown.</para>
/// <para>Ограничивает полные цепочки преобразования и ожидание в очереди, отклоняет конфликты выходного пути и завершает задания при остановке.</para>
/// </summary>
public sealed class ScheduledXmlPdfGenerator : IXmlPdfGenerator, IAsyncDisposable, IDisposable
{

    /// <summary>
    /// <para>Gets or defines the Outputs value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение Outputs, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> Outputs = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// <para>Gets or defines the inner value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение inner, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly IXmlPdfGenerator inner;

    /// <summary>
    /// <para>Gets or defines the options value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение options, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly PdfSchedulerOptions options;

    /// <summary>
    /// <para>Gets or defines the admitted value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение admitted, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly SemaphoreSlim admitted;

    /// <summary>
    /// <para>Gets or defines the active value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение active, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly SemaphoreSlim active;

    /// <summary>
    /// <para>Gets or defines the shutdown value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение shutdown, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly CancellationTokenSource shutdown = new();

    /// <summary>
    /// <para>Gets or defines the gate value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение gate, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly object gate = new();

    /// <summary>
    /// <para>Gets or defines the jobs value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение jobs, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private readonly HashSet<Task> jobs = [];

    /// <summary>
    /// <para>Gets or defines the stopping value used by ScheduledXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение stopping, используемое ScheduledXmlPdfGenerator.</para>
    /// </summary>
    private bool stopping;

    /// <summary>
    /// <para>Initializes ScheduledXmlPdfGenerator with an immutable snapshot of service settings.</para>
    /// <para>Создаёт ScheduledXmlPdfGenerator со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="inner"><para>The inner argument.</para><para>Аргумент inner.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public ScheduledXmlPdfGenerator(IXmlPdfGenerator inner, PdfSchedulerOptions? options = null)
    {
        this.inner = inner;
        this.options = options ?? new();
        if (this.options.MaxActiveJobs < 1 || this.options.MaxQueuedJobs < 0 || this.options.QueueTimeout <= TimeSpan.Zero || this.options.ShutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        admitted = new(checked(this.options.MaxActiveJobs + this.options.MaxQueuedJobs));
        active = new(this.options.MaxActiveJobs);
    }

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
    public async Task GenerateFileAsync(string xmlPath, string xsltPath, string pdfPath, CancellationToken cancellationToken = default)
            => await GenerateAsync(new(XmlSource.FromFile(xmlPath), XmlSource.FromFile(xsltPath)), pdfPath, cancellationToken).ConfigureAwait(false);

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
    public Task<ConversionResult> GenerateAsync(ConversionRequest request, string pdfPath, CancellationToken cancellationToken = default)
            => Start(request, Path.GetFullPath(pdfPath), token => inner.GenerateAsync(request, pdfPath, token), cancellationToken);

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
    public Task<ConversionResult> GenerateAsync(ConversionRequest request, Stream output, CancellationToken cancellationToken = default)
            => Start(request, null, token => inner.GenerateAsync(request, output, token), cancellationToken);

    /// <summary>
    /// <para>Executes Start as part of the ScheduledXmlPdfGenerator conversion workflow.</para>
    /// <para>Выполняет Start в цепочке преобразования ScheduledXmlPdfGenerator.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <param name="execute"><para>The execute argument.</para><para>Аргумент execute.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private Task<ConversionResult> Start(ConversionRequest request, string? path, Func<CancellationToken, Task<ConversionResult>> execute, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        token.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (stopping)
            {
                throw new ObjectDisposedException(nameof(ScheduledXmlPdfGenerator));
            }
            if (!admitted.Wait(0))
            {
                throw new ConversionQueueFullException();
            }
            if (path is not null && !Outputs.TryAdd(path, 0))
            {
                admitted.Release();
                throw new ConversionOutputConflictException();
            }
            Task<ConversionResult> job = RunAsync(request, path, execute, token);
            jobs.Add(job);
            _ = job.ContinueWith(completed => { lock (gate) { jobs.Remove(completed); } }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return job;
        }
    }

    /// <summary>
    /// <para>Executes RunAsync as part of the ScheduledXmlPdfGenerator conversion workflow.</para>
    /// <para>Выполняет RunAsync в цепочке преобразования ScheduledXmlPdfGenerator.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <param name="execute"><para>The execute argument.</para><para>Аргумент execute.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private async Task<ConversionResult> RunAsync(ConversionRequest request, string? path, Func<CancellationToken, Task<ConversionResult>> execute, CancellationToken token)
    {
        var queue = Stopwatch.StartNew();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, shutdown.Token);
        bool acquired = false;
        try
        {
            if (!await active.WaitAsync(options.QueueTimeout, linked.Token).ConfigureAwait(false))
            {
                throw new TimeoutException("PDF queue wait timed out.");
            }
            acquired = true;
            queue.Stop();
            if (request.Settings.Timeout is { } timeout && inner is not IProcessIsolatedXmlPdfGenerator)
            {
                linked.CancelAfter(timeout);
            }
            try
            {
                ConversionResult result = await execute(linked.Token).ConfigureAwait(false);
                return result with
                {
                    QueueDuration = queue.Elapsed,
                    TotalDuration = result.TotalDuration + queue.Elapsed,
                    ProfileVersion = options.ProfileVersion
                };
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && !shutdown.IsCancellationRequested)
            {
                throw new TimeoutException("PDF job execution timed out.");
            }
        }
        finally
        {
            if (acquired)
            {
                active.Release();
            }
            if (path is not null)
            {
                Outputs.TryRemove(path, out _);
            }
            admitted.Release();
        }
    }

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        lock (gate)
        {
            if (stopping)
            {
                return;
            }
            stopping = true;
            pending = [.. jobs];
        }
        try
        {
            await Task.WhenAll(pending).WaitAsync(options.ShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException) { shutdown.Cancel(); try { await Task.WhenAll(pending).ConfigureAwait(false); } catch { } }
        catch { }
        shutdown.Dispose();
        admitted.Dispose();
        active.Dispose();
        if (inner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// <para>Synchronously drains the scheduler and disposes its owned generator; prefer asynchronous disposal for hosting applications.</para>
    /// <para>Синхронно завершает задания и освобождает принадлежащий генератор; в серверных приложениях предпочтительна асинхронная очистка.</para>
    /// </summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
