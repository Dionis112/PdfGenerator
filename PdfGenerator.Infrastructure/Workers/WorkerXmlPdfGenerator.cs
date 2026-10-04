using System.Diagnostics;

namespace PdfGenerator;


/// <summary>
/// <para>Runs conversions in a bounded pool of bundled .NET workers; publishes verified PDFs from the parent process.</para>
/// <para>Выполняет преобразования в ограниченном пуле встроенных .NET worker; проверенные PDF публикует родительский процесс.</para>
/// </summary>
public sealed class WorkerXmlPdfGenerator : IProcessIsolatedXmlPdfGenerator, IAsyncDisposable
{

    /// <summary>
    /// <para>Gets or defines the options value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение options, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly PdfWorkerOptions options;

    /// <summary>
    /// <para>Gets or defines the xmlOptions value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение xmlOptions, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly XmlFoOptions xmlOptions;

    /// <summary>
    /// <para>Gets or defines the rendererOptions value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение rendererOptions, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly EmbeddedFopOptions rendererOptions;

    /// <summary>
    /// <para>Gets or defines the root value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение root, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly string root;

    /// <summary>
    /// <para>Gets or defines the workers value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение workers, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly WorkerClient[] workers;

    /// <summary>
    /// <para>Gets or defines the available value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение available, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly SemaphoreSlim available;

    /// <summary>
    /// <para>Gets or defines the gate value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение gate, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private readonly object gate = new();

    /// <summary>
    /// <para>Gets or defines the starts value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение starts, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private long starts, completed, recycles, crashes, timeouts, memory;

    /// <summary>
    /// <para>Gets or defines the disposed value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение disposed, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    private bool disposed;

    /// <summary>
    /// <para>Initializes WorkerXmlPdfGenerator with an immutable snapshot of service settings.</para>
    /// <para>Создаёт WorkerXmlPdfGenerator со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="xmlOptions"><para>The xmlOptions argument.</para><para>Аргумент xmlOptions.</para></param>
    /// <param name="rendererOptions"><para>The rendererOptions argument.</para><para>Аргумент rendererOptions.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public WorkerXmlPdfGenerator(PdfWorkerOptions? options = null, XmlFoOptions? xmlOptions = null, EmbeddedFopOptions? rendererOptions = null)
    {
        this.options = options ?? new();
        this.xmlOptions = xmlOptions ?? new();
        this.rendererOptions = rendererOptions ?? new();
        if (this.options.MaxWorkers < 1 || this.options.StartupTimeout <= TimeSpan.Zero || this.options.JobTimeout <= TimeSpan.Zero ||
            this.options.StopGracePeriod <= TimeSpan.Zero || this.options.RecycleAfterJobs < 1 || this.options.MaxStartupFailures < 1 || this.options.MaxWorkingSetBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        if (this.options.MemoryBudgetBytes < 0 || this.options.StartupFailureBackoff < TimeSpan.Zero ||
            (this.options.MemoryBudgetBytes > 0 && (this.options.MaxWorkingSetBytes == 0 ||
            this.options.MaxWorkingSetBytes > this.options.MemoryBudgetBytes / this.options.MaxWorkers)))
        {
            throw new ArgumentException("Worker reservations exceed the total memory budget.", nameof(options));
        }
        using var transformer = new SaxonFoTransformer(this.xmlOptions);
        using var renderer = new EmbeddedFopRenderer(this.rendererOptions);
        root = Path.Combine(Path.GetFullPath(this.options.WorkingDirectory ?? Path.GetTempPath()), "xmlpdf-pool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        workers = Enumerable.Range(0, this.options.MaxWorkers).Select(_ => new WorkerClient(this.options, root, new(this.xmlOptions, this.rendererOptions))).ToArray();
        available = new(workers.Length);
    }

    /// <summary>
    /// <para>Returns an immutable snapshot of cache or worker counters.</para>
    /// <para>Возвращает неизменяемый снимок счётчиков кэша или worker.</para>
    /// </summary>
    public WorkerPoolStatistics Statistics
    {
        get
        {
            lock (gate)
            {
                return new(starts, completed, recycles, crashes, timeouts, memory);
            }
        }
    }

    /// <summary>
    /// <para>Gets or defines the ProcessIds value used by WorkerXmlPdfGenerator.</para>
    /// <para>Возвращает или определяет значение ProcessIds, используемое WorkerXmlPdfGenerator.</para>
    /// </summary>
    public IReadOnlyList<int> ProcessIds
    {
        get
        {
            lock (gate)
            {
                return workers.Select(w => w.ProcessId).OfType<int>().ToArray();
            }
        }
    }

    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="xmlPath"><para>Local XML input path.</para><para>Путь локального входного XML.</para></param>
    /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
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
    /// <param name="cancellationToken"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public Task<ConversionResult> GenerateAsync(ConversionRequest request, string pdfPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string temporary = AtomicOutput.Prepare(pdfPath, new[] { request.Xml.FilePath, request.Xslt.FilePath }.OfType<string>().ToArray());
        return GenerateCoreAsync(request, temporary, pdfPath, null, cancellationToken);
    }

    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public Task<ConversionResult> GenerateAsync(ConversionRequest request, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite || ReferenceEquals(output, request.Xml.Stream) || ReferenceEquals(output, request.Xslt.Stream))
        {
            throw new ArgumentException("Output must be a separate writable stream.");
        }
        return GenerateCoreAsync(request, Path.Combine(root, Guid.NewGuid().ToString("N") + ".pdf"), null, output, cancellationToken);
    }

    /// <summary>
    /// <para>Executes GenerateCoreAsync as part of the WorkerXmlPdfGenerator conversion workflow.</para>
    /// <para>Выполняет GenerateCoreAsync в цепочке преобразования WorkerXmlPdfGenerator.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="temporary"><para>The temporary argument.</para><para>Аргумент temporary.</para></param>
    /// <param name="target"><para>The target argument.</para><para>Аргумент target.</para></param>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="token"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private async Task<ConversionResult> GenerateCoreAsync(ConversionRequest request, string temporary, string? target, Stream? output, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var total = Stopwatch.StartNew();
        await available.WaitAsync(token).ConfigureAwait(false);
        WorkerClient worker;
        string affinity = request.Xslt.BaseUri?.AbsoluteUri ?? "stream";
        lock (gate)
        {
            worker = workers.FirstOrDefault(w => !w.Busy && w.TemplateAffinity == affinity) ?? workers.First(w => !w.Busy);
            worker.Busy = true;
            worker.TemplateAffinity = affinity;
        }
        string directory = Path.Combine(root, "job-" + Guid.NewGuid().ToString("N"));
        ConversionStage stage = ConversionStage.Input;
        try
        {
            Directory.CreateDirectory(directory);
            var preparation = Stopwatch.StartNew();
            long inputLimit = Math.Min(rendererOptions.MaxTemporaryBytes, Math.Min(xmlOptions.MaxInputBytes, request.Settings.MaxInputBytes ?? xmlOptions.MaxInputBytes));
            using (PreparedXmlSource xml = await PreparedXmlSource.CreateAsync(request.Xml, inputLimit, xmlOptions.MemoryBufferThresholdBytes, token).ConfigureAwait(false))
            await using (FileStream file = File.Create(Path.Combine(directory, "input.xml")))
            {
                await xml.Stream.CopyToAsync(file, token).ConfigureAwait(false);
            }
            long remaining = rendererOptions.MaxTemporaryBytes - new FileInfo(Path.Combine(directory, "input.xml")).Length;
            using (PreparedXmlSource xslt = await PreparedXmlSource.CreateAsync(request.Xslt, Math.Max(1, Math.Min(remaining, xmlOptions.MaxStylesheetBytes)), xmlOptions.MemoryBufferThresholdBytes, token).ConfigureAwait(false))
            await using (FileStream file = File.Create(Path.Combine(directory, "template.xsl")))
            {
                await xslt.Stream.CopyToAsync(file, token).ConfigureAwait(false);
            }
            if (Directory.EnumerateFiles(directory).Sum(p => new FileInfo(p).Length) > rendererOptions.MaxTemporaryBytes)
            {
                throw new IOException("Worker input staging exceeds its disk quota.");
            }
            preparation.Stop();
            stage = ConversionStage.Render;
            var startup = Stopwatch.StartNew();
            if (await worker.StartAsync(token).ConfigureAwait(false))
            {
                lock (gate)
                {
                    starts++;
                }
            }
            startup.Stop();
            string? resourceRoot = request.Xslt.BaseUri is null ? null : Path.GetDirectoryName(request.Xslt.BaseUri.LocalPath);
            string[] roots = request.Resources?.AllowedRoots.ToArray() ?? (resourceRoot is null ? [] : [resourceRoot]);
            var job = new WorkerJob(directory, request.JobId, request.Xml.BaseUri?.AbsoluteUri, request.Xslt.BaseUri?.AbsoluteUri, roots,
                request.Parameters.Select(p => new WorkerParameter(p.Name.Name, p.Name.Namespace, p.Type, p.LexicalValue)).ToArray(), request.Settings);
            WorkerMessage response = await worker.ExecuteAsync(job, token).ConfigureAwait(false);
            if (response.Kind == "error")
            {
                throw new ConversionException(response.Stage ?? ConversionStage.Render, "Worker conversion failed (" + response.ErrorCode + ").");
            }
            ConversionResult result = response.Result ?? throw new PdfWorkerException(WorkerFailureKind.Protocol, "Worker omitted its result.");
            string generated = Path.Combine(directory, "result.pdf");
            long size = new FileInfo(generated).Length;
            if (result.JobId != request.JobId || size != result.PdfBytes || size < 5 || size > rendererOptions.MaxPdfBytes || result.PageCount is not > 0)
            {
                throw new PdfWorkerException(WorkerFailureKind.Protocol, "Worker result validation failed.");
            }
            await using (FileStream file = File.OpenRead(generated))
            {
                byte[] header = new byte[5];
                await file.ReadExactlyAsync(header, token).ConfigureAwait(false);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8))
                {
                    throw new PdfWorkerException(WorkerFailureKind.Protocol, "Worker output is not a PDF.");
                }
            }
            var publication = Stopwatch.StartNew();
            stage = ConversionStage.Publish;
            if (target is not null)
            {
                await using (FileStream source = File.OpenRead(generated))
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    await source.CopyToAsync(file, token).ConfigureAwait(false);
                }
                AtomicOutput.Publish(temporary, target, token);
            }
            else
            {
                await using FileStream source = File.OpenRead(generated);
                await source.CopyToAsync(output!, token).ConfigureAwait(false);
            }
            lock (gate)
            {
                completed++;
            }
            var stages = new Dictionary<string, TimeSpan>(result.StageDurations)
            {
                ["workerPreparation"] = preparation.Elapsed,
                ["workerStartup"] = startup.Elapsed,
                ["publish"] = publication.Elapsed
            };
            return result with
            {
                InputPreparationDuration = preparation.Elapsed,
                PublishDuration = publication.Elapsed,
                TotalDuration = total.Elapsed,
                StageDurations = new System.Collections.ObjectModel.ReadOnlyDictionary<string, TimeSpan>(stages)
            };
        }
        catch (PdfWorkerException ex)
        {
            await worker.StopAsync().ConfigureAwait(false);
            lock (gate)
            {
                if (ex.Kind == WorkerFailureKind.Timeout)
                {
                    timeouts++;
                }
                else if (ex.Kind == WorkerFailureKind.MemoryLimit)
                {
                    memory++;
                }
                else
                {
                    crashes++;
                }
            }
            throw;
        }
        catch (OperationCanceledException) { throw; }
        catch (ConversionException) { throw; }
        catch (Exception ex) { throw new ConversionException(stage, "Worker preparation or publication failed.", ex); }
        finally
        {
            if (worker.Jobs >= options.RecycleAfterJobs)
            {
                await worker.StopAsync().ConfigureAwait(false);
                lock (gate)
                {
                    recycles++;
                }
            }
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
            lock (gate)
            {
                worker.Busy = false;
            }
            available.Release();
        }
    }

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        for (int i = 0; i < workers.Length; i++)
        {
            await available.WaitAsync().ConfigureAwait(false);
        }
        foreach (WorkerClient worker in workers)
        {
            await worker.DisposeAsync().ConfigureAwait(false);
        }
        Directory.Delete(root, true);
        available.Dispose();
    }
}
