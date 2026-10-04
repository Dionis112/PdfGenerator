namespace PdfGenerator;


/// <summary>
/// <para>Renders FO with bundled FOP and leases a cached configuration/font factory for each document.</para>
/// <para>Рендерит FO встроенным FOP, выделяя каждому документу фабрику из кэша конфигурации и шрифтов.</para>
/// </summary>
public sealed class EmbeddedFopRenderer : IResourceFoPdfRenderer, IDisposable
{

    /// <summary>
    /// <para>Gets or defines the options value used by EmbeddedFopRenderer.</para>
    /// <para>Возвращает или определяет значение options, используемое EmbeddedFopRenderer.</para>
    /// </summary>
    private readonly EmbeddedFopOptions options;

    /// <summary>
    /// <para>Gets or defines the slots value used by EmbeddedFopRenderer.</para>
    /// <para>Возвращает или определяет значение slots, используемое EmbeddedFopRenderer.</para>
    /// </summary>
    private readonly SemaphoreSlim slots;

    /// <summary>
    /// <para>Gets or defines the factories value used by EmbeddedFopRenderer.</para>
    /// <para>Возвращает или определяет значение factories, используемое EmbeddedFopRenderer.</para>
    /// </summary>
    private readonly FopFactoryCache factories;


    /// <summary>
    /// <para>Initializes EmbeddedFopRenderer with an immutable snapshot of service settings.</para>
    /// <para>Создаёт EmbeddedFopRenderer со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public EmbeddedFopRenderer(EmbeddedFopOptions? options = null)
    {
        this.options = options ?? new();
        if (this.options.MaxConcurrency <= 0 || this.options.MaxPdfBytes <= 0 || this.options.MaxFoBytes <= 0 || this.options.MaxImageBytes <= 0 || this.options.MaxImageBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (this.options.FactoryCacheEntries < 1 || this.options.FactoryCacheBytes < 1 || this.options.MaxImages < 1 || this.options.MaxTotalImageBytes <= 0 || this.options.MaxTemporaryBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        slots = new(this.options.MaxConcurrency);
        factories = new(this.options.FactoryCacheEntries, this.options.FactoryCacheBytes);
    }


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
    public async Task RenderFileAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default)
            => await RenderWithResultAsync(foPath, pdfPath, cancellationToken).ConfigureAwait(false);


    /// <summary>
    /// <para>Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.</para>
    /// <para>Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task<RenderResult> RenderWithResultAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default)
            => await RenderWithResourcesAsync(foPath, pdfPath, new Uri(Path.GetFullPath(foPath)),
                new LocalResourcePolicy([Path.GetDirectoryName(Path.GetFullPath(foPath))!]), cancellationToken).ConfigureAwait(false);


    /// <summary>
    /// <para>Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.</para>
    /// <para>Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task<RenderResult> RenderWithResourcesAsync(string foPath, string pdfPath, Uri? baseUri,
            LocalResourcePolicy resources, CancellationToken cancellationToken = default)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        ConversionStage stage = ConversionStage.Render;
        try
        {
            temporary = AtomicOutput.Prepare(pdfPath, foPath);
            if (!File.Exists(foPath))
            {
                throw new FileNotFoundException("FO input not found.", foPath);
            }

            var preparation = System.Diagnostics.Stopwatch.StartNew();
            using FoImagePreparation images = await FoImagePreparation.CreateAsync(foPath, Path.GetDirectoryName(temporary)!,
                new FopOptions
                {
                    MaxFoBytes = options.MaxFoBytes,
                    MaxImageBytes = options.MaxImageBytes,
                    MaxImages = options.MaxImages,
                    MaxTotalImageBytes = options.MaxTotalImageBytes,
                    MaxTemporaryBytes = options.MaxTemporaryBytes,
                    SvgFontAliases = PdfWorkerHost.SvgFontAliases
                }, cancellationToken, baseUri, resources).ConfigureAwait(false);
            preparation.Stop();
            string bundledConfig = BundledFopRuntime.ConfigurationPath;
            string configuration = options.ConfigurationPath is null ? bundledConfig : Path.GetFullPath(options.ConfigurationPath);
            cancellationToken.ThrowIfCancellationRequested();
            // FOP is synchronous. Keep the concurrency slot and resources until it actually finishes.
            long pdfLimit = Math.Min(options.MaxPdfBytes, options.MaxTemporaryBytes - images.TemporaryBytes);
            if (pdfLimit < 5)
            {
                throw new IOException("Temporary file quota leaves no room for PDF output.");
            }
            RenderResult result = (await Task.Run(() => RenderCore(images.FoPath, temporary, configuration, pdfLimit), CancellationToken.None).ConfigureAwait(false))
                with
            {
                PreparationDuration = preparation.Elapsed
            };
            cancellationToken.ThrowIfCancellationRequested();
            long size = new FileInfo(temporary).Length;
            if (size < 5 || size > options.MaxPdfBytes)
            {
                throw new IOException("Invalid PDF size.");
            }

            await using (FileStream stream = File.OpenRead(temporary))
            {
                byte[] header = new byte[5];
                await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8))
                {
                    throw new IOException("FOP did not produce a PDF.");
                }
            }
            images.Dispose();
            stage = ConversionStage.Publish;
            AtomicOutput.Publish(temporary, pdfPath, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConversionException(stage, $"FO → PDF failed during {stage}.", ex);
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            slots.Release();
        }
    }


    /// <summary>
    /// <para>Executes RenderCore as part of the EmbeddedFopRenderer conversion workflow.</para>
    /// <para>Выполняет RenderCore в цепочке преобразования EmbeddedFopRenderer.</para>
    /// </summary>
    /// <param name="fo"><para>The fo argument.</para><para>Аргумент fo.</para></param>
    /// <param name="pdf"><para>The pdf argument.</para><para>Аргумент pdf.</para></param>
    /// <param name="configuration"><para>The configuration argument.</para><para>Аргумент configuration.</para></param>
    /// <param name="pdfLimit"><para>PDF byte limit after preparation.</para><para>Лимит байтов PDF после подготовки.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private RenderResult RenderCore(string fo, string pdf, string configuration, long pdfLimit)
    {
        java.lang.System.setProperty("java.awt.headless", "true");
        var factoryTimer = System.Diagnostics.Stopwatch.StartNew();
        using FopFactoryCache.Lease lease = factories.Acquire(configuration);
        factoryTimer.Stop();
        var fopTimer = System.Diagnostics.Stopwatch.StartNew();
        org.apache.fop.apps.FopFactory factory = lease.Factory;
        org.apache.fop.apps.FOUserAgent userAgent = factory.newFOUserAgent();
        var events = new FailureListener();
        userAgent.getEventBroadcaster().addEventListener(events);
        using var file = new java.io.FileOutputStream(pdf);
        using var output = new LimitedPdfOutputStream(file, pdfLimit);
        org.apache.fop.apps.Fop fop;
        // Xerces DOM configuration performs lazy writes even while reading. Serialize only
        // document initialization; the actual transformation/rendering remains concurrent.
        lock (factory)
        {
            fop = factory.newFop("application/pdf", userAgent, output);
        }
        javax.xml.transform.Transformer transformer = javax.xml.transform.TransformerFactory.newInstance().newTransformer();
        try
        {
            transformer.transform(new javax.xml.transform.stream.StreamSource(new java.io.File(fo)),
                new javax.xml.transform.sax.SAXResult(fop.getDefaultHandler()));
        }
        finally
        {
            // A cached/preloaded image source can retain a Windows file handle even after
            // layout completes. Drain this document's session before deleting its snapshots.
            foreach (string image in Directory.EnumerateFiles(Path.GetDirectoryName(fo)!))
            {
                javax.xml.transform.Source? source = userAgent.getImageSessionContext().getSource(new Uri(image).AbsoluteUri);
                if (source is not null)
                {
                    org.apache.xmlgraphics.io.XmlSourceUtil.closeQuietly(source);
                }
            }
            factory.getImageManager().getCache().clearCache();
        }
        if (events.Failed || fop.getResults().getPageCount() == 0)
        {
            throw new IOException("FOP reported a rendering error or produced no pages.");
        }
        return new(fop.getResults().getPageCount(), events.Warnings.AsReadOnly())
        {
            FactoryDuration = factoryTimer.Elapsed,
            FopDuration = fopTimer.Elapsed
        };
    }


    /// <summary>
    /// <para>Provides the FailureListener contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию FailureListener для преобразования PDF.</para>
    /// </summary>
    private sealed class FailureListener : org.apache.fop.events.EventListener
    {

        /// <summary>
        /// <para>Gets or defines the Warnings value used by FailureListener.</para>
        /// <para>Возвращает или определяет значение Warnings, используемое FailureListener.</para>
        /// </summary>
        public List<string> Warnings { get; } = [];

        /// <summary>
        /// <para>Gets or defines the Failed value used by FailureListener.</para>
        /// <para>Возвращает или определяет значение Failed, используемое FailureListener.</para>
        /// </summary>
        public bool Failed
        {
            get; private set;
        }

        /// <summary>
        /// <para>Executes processEvent as part of the FailureListener conversion workflow.</para>
        /// <para>Выполняет processEvent в цепочке преобразования FailureListener.</para>
        /// </summary>
        /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
        public void processEvent(org.apache.fop.events.Event value)
        {
            org.apache.fop.events.model.EventSeverity severity = value.getSeverity();
            if (severity == org.apache.fop.events.model.EventSeverity.WARN)
            {
                // Report event codes only, never document or parameter values.
                string code = value.getEventID();
                if (Warnings.Count < 64 && !Warnings.Contains(code, StringComparer.Ordinal))
                {
                    Warnings.Add(code);
                }
                else if (Warnings.Count == 64 && !Warnings.Contains(code, StringComparer.Ordinal))
                {
                    Warnings[63] = "PdfGenerator.WarningsTruncated";
                }
            }
            if (severity == org.apache.fop.events.model.EventSeverity.ERROR || severity == org.apache.fop.events.model.EventSeverity.FATAL)
            {
                Failed = true;
            }
        }
    }


    /// <summary>
    /// <para>Returns an immutable snapshot of cache or worker counters.</para>
    /// <para>Возвращает неизменяемый снимок счётчиков кэша или worker.</para>
    /// </summary>
    public FopCacheStatistics CacheStatistics => factories.Statistics;

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose()
    {
        factories.Dispose();
        slots.Dispose();
    }
}
