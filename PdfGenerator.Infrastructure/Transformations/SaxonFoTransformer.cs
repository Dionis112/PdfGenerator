using System.Diagnostics;
using System.Xml;
using Saxon.Api;

namespace PdfGenerator;


/// <summary>
/// <para>Transforms XML to FO with bounded snapshot-based XSLT caching; each job owns its transformer and parameters.</para>
/// <para>Преобразует XML в FO с ограниченным кэшем снимков XSLT; transformer и параметры принадлежат отдельному заданию.</para>
/// </summary>
public sealed class SaxonFoTransformer : IReportingXsltFoTransformer, IDisposable
{

    /// <summary>
    /// <para>Gets or defines the options value used by SaxonFoTransformer.</para>
    /// <para>Возвращает или определяет значение options, используемое SaxonFoTransformer.</para>
    /// </summary>
    private readonly XmlFoOptions options;

    /// <summary>
    /// <para>Gets or defines the slots value used by SaxonFoTransformer.</para>
    /// <para>Возвращает или определяет значение slots, используемое SaxonFoTransformer.</para>
    /// </summary>
    private readonly SemaphoreSlim slots;

    /// <summary>
    /// <para>Gets or defines the cache value used by SaxonFoTransformer.</para>
    /// <para>Возвращает или определяет значение cache, используемое SaxonFoTransformer.</para>
    /// </summary>
    private readonly XsltTemplateCache cache;


    /// <summary>
    /// <para>Initializes SaxonFoTransformer with an immutable snapshot of service settings.</para>
    /// <para>Создаёт SaxonFoTransformer со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public SaxonFoTransformer(XmlFoOptions? options = null)
    {
        this.options = options ?? new();
        this.options.Altova2012?.Validate();
        if (this.options.MaxConcurrency <= 0 || this.options.MaxInputBytes <= 0 || this.options.MaxOutputBytes <= 0 || this.options.MemoryBufferThresholdBytes < 0 || this.options.MaxStylesheetBytes <= 0 || this.options.TemplateCacheEntries < 0 || this.options.TemplateCacheBytes < 0 || this.options.TemplateCacheIdleTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        slots = new(this.options.MaxConcurrency);
        cache = new(this.options);
    }


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
    public Task TransformFileAsync(string xmlPath, string xsltPath, string foPath, CancellationToken cancellationToken = default)
            => TransformAsync(new(XmlSource.FromFile(xmlPath), XmlSource.FromFile(xsltPath)), foPath, cancellationToken);


    /// <summary>
    /// <para>Prepares bounded input snapshots and transforms with job-local parameters; in-process cancellation is observed around synchronous Saxon execution.</para>
    /// <para>Подготавливает ограниченные снимки входов и преобразует с параметрами задания; отмена внутри процесса проверяется вокруг синхронного выполнения Saxon.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public async Task TransformAsync(ConversionRequest request, string foPath, CancellationToken cancellationToken = default)
            => await TransformWithResultAsync(request, foPath, cancellationToken).ConfigureAwait(false);


    /// <summary>
    /// <para>Prepares bounded input snapshots and transforms with job-local parameters; in-process cancellation is observed around synchronous Saxon execution.</para>
    /// <para>Подготавливает ограниченные снимки входов и преобразует с параметрами задания; отмена внутри процесса проверяется вокруг синхронного выполнения Saxon.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task<TransformResult> TransformWithResultAsync(ConversionRequest request, string foPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(foPath);
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        ConversionStage preparationStage = ConversionStage.Input;
        try
        {
            var watch = Stopwatch.StartNew();
            long inputLimit = Math.Min(options.MaxInputBytes, request.Settings.MaxInputBytes ?? options.MaxInputBytes);
            using PreparedXmlSource xml = await PreparedXmlSource.CreateAsync(request.Xml, inputLimit, options.MemoryBufferThresholdBytes, cancellationToken).ConfigureAwait(false);
            using PreparedXmlSource xslt = await PreparedXmlSource.CreateAsync(request.Xslt, options.MaxStylesheetBytes, options.MemoryBufferThresholdBytes, cancellationToken).ConfigureAwait(false);
            TimeSpan preparationTime = watch.Elapsed;
            watch.Restart();
            preparationStage = ConversionStage.Compile;
            string? root = xslt.BaseUri is null ? null : Path.GetDirectoryName(xslt.BaseUri.LocalPath);
            LocalResourcePolicy resources = request.Resources ?? new LocalResourcePolicy(root is null ? [] : [root]);
            StylesheetSnapshot snapshot = await Task.Run(() => StylesheetSnapshot.Create(xslt, resources, options, cancellationToken), CancellationToken.None).ConfigureAwait(false);
            TimeSpan snapshotTime = watch.Elapsed;
            watch.Restart();
            CompiledStylesheet stylesheet = await cache.GetAsync(snapshot, cancellationToken).ConfigureAwait(false);
            TimeSpan compilationTime = watch.Elapsed;
            watch.Restart();
            // Hold the execution slot until synchronous Saxon actually finishes.
            IReadOnlyList<XsltExecutionDiagnostic> diagnostics = await Task.Run(() => Transform(request, xml, stylesheet, foPath, cancellationToken), CancellationToken.None).ConfigureAwait(false);
            return new(preparationTime, snapshotTime, compilationTime, watch.Elapsed)
            {
                Diagnostics = diagnostics
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (ConversionException) { throw; }
        catch (Exception ex) { throw new ConversionException(preparationStage, "Cannot prepare XML inputs or stylesheet.", ex); }
        finally
        {
            slots.Release();
        }
    }


    /// <summary>
    /// <para>Executes Transform as part of the SaxonFoTransformer conversion workflow.</para>
    /// <para>Выполняет Transform в цепочке преобразования SaxonFoTransformer.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="xml"><para>The xml argument.</para><para>Аргумент xml.</para></param>
    /// <param name="stylesheet"><para>Executable and immutable compilation diagnostics.</para><para>Исполняемый шаблон и неизменяемые диагностики компиляции.</para></param>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <returns><para>Compilation and job-local transformation diagnostics.</para><para>Диагностики компиляции и преобразования текущего задания.</para></returns>
    private IReadOnlyList<XsltExecutionDiagnostic> Transform(ConversionRequest request, PreparedXmlSource xml, CompiledStylesheet stylesheet, string foPath, CancellationToken token)
    {
        ConversionStage stage = ConversionStage.Input;
        string? temporary = null;
        var diagnostics = new SaxonDiagnosticCollector(ConversionStage.Transform);

        try
        {
            token.ThrowIfCancellationRequested();
            temporary = AtomicOutput.Prepare(foPath, new[] { request.Xml.FilePath, request.Xslt.FilePath }.OfType<string>().ToArray());
            Processor processor = stylesheet.Executable.Processor;
            using AltovaJobContext? compatibility = options.Altova2012 is null ? null : new AltovaJobContext(options.Altova2012);
            stage = ConversionStage.Transform;
            XsltTransformer transformer = NewTransformer(request, xml, stylesheet, diagnostics);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var limited = new LimitedWriteStream(output, options.MaxOutputBytes))
            {
                Serializer serializer = processor.NewSerializer(limited);
                serializer.SetOutputProperty(Serializer.METHOD, "xml");
                serializer.SetOutputProperty(Serializer.ENCODING, "utf-8");
                transformer.Run(serializer);
            }
            using (XmlReader result = OpenXml(temporary, options.MaxOutputBytes))
            {
                result.MoveToContent();
                if (result.LocalName != "root" || result.NamespaceURI != "http://www.w3.org/1999/XSL/Format")
                {
                    throw new XmlException("The stylesheet did not produce an XSL-FO root.");
                }

                while (result.Read())
                {
                }
            }
            stage = ConversionStage.Publish;
            AtomicOutput.Publish(temporary, foPath, token);
            return Array.AsReadOnly(stylesheet.Diagnostics.Concat(diagnostics.Snapshot()).ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConversionException(stage, $"XML → FO failed during {stage}.", ex)
            {
                Diagnostics = Array.AsReadOnly(stylesheet.Diagnostics.Concat(diagnostics.Snapshot()).ToArray())
            };
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary><para>Prepares the same job-local Saxon execution for FO and XDM validation.</para><para>Подготавливает одинаковое выполнение Saxon для FO и проверки XDM.</para></summary>
    /// <param name="request"><para>Job request.</para><para>Запрос задания.</para></param>
    /// <param name="xml"><para>Bounded input snapshot.</para><para>Ограниченный снимок входа.</para></param>
    /// <param name="stylesheet"><para>Shared executable.</para><para>Общий шаблон.</para></param>
    /// <param name="diagnostics"><para>Job-local collector.</para><para>Сборщик задания.</para></param>
    /// <returns><para>A new transformer.</para><para>Новый преобразователь.</para></returns>
    private XsltTransformer NewTransformer(ConversionRequest request, PreparedXmlSource xml, CompiledStylesheet stylesheet, SaxonDiagnosticCollector diagnostics)
    {
        using XmlReader input = OpenXml(xml);
        XsltTransformer transformer = stylesheet.Executable.Load();
        transformer.ErrorReporter = diagnostics.Report;
        transformer.TraceFunctionDestination = TextWriter.Null;
        transformer.MessageListener = _ => { };
        foreach (XsltParameter parameter in request.Parameters)
        {
            transformer.SetParameter(new QName(parameter.Name), new XdmAtomicValue(parameter.LexicalValue,
                XdmAtomicType.BuiltInAtomicType(new QName("http://www.w3.org/2001/XMLSchema", parameter.Type))));
        }
        transformer.InitialContextNode = stylesheet.Executable.Processor.NewDocumentBuilder().Build(input);
        transformer.ResultDocumentHandler = (_, _) => throw new XmlException("Secondary output documents are disabled.");
        return transformer;
    }

    /// <summary><para>Validation-only XDM execution using the application's snapshot, cache and resource policy.</para><para>Выполнение XDM для стенда со снимком, кэшем и политикой ресурсов приложения.</para></summary>
    /// <param name="request"><para>Validation request.</para><para>Запрос проверки.</para></param>
    /// <param name="token"><para>Cancellation token.</para><para>Токен отмены.</para></param>
    /// <returns><para>Raw XDM results before serialization.</para><para>Результаты XDM до сериализации.</para></returns>
    internal async Task<XdmValue> TransformToXdmAsync(ConversionRequest request, CancellationToken token = default)
    {
        await slots.WaitAsync(token).ConfigureAwait(false);
        ConversionStage stage = ConversionStage.Input;
        var diagnostics = new SaxonDiagnosticCollector(ConversionStage.Transform);
        try
        {
            long inputLimit = Math.Min(options.MaxInputBytes, request.Settings.MaxInputBytes ?? options.MaxInputBytes);
            using PreparedXmlSource xml = await PreparedXmlSource.CreateAsync(request.Xml, inputLimit, options.MemoryBufferThresholdBytes, token).ConfigureAwait(false);
            using PreparedXmlSource xslt = await PreparedXmlSource.CreateAsync(request.Xslt, options.MaxStylesheetBytes, options.MemoryBufferThresholdBytes, token).ConfigureAwait(false);
            stage = ConversionStage.Compile;
            string? root = xslt.BaseUri is null ? null : Path.GetDirectoryName(xslt.BaseUri.LocalPath);
            var snapshot = StylesheetSnapshot.Create(xslt, request.Resources ?? new LocalResourcePolicy(root is null ? [] : [root]), options, token);
            CompiledStylesheet stylesheet = await cache.GetAsync(snapshot, token).ConfigureAwait(false);
            stage = ConversionStage.Transform;
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using AltovaJobContext? compatibility = options.Altova2012 is null ? null : new AltovaJobContext(options.Altova2012);
                XsltTransformer transformer = NewTransformer(request, xml, stylesheet, diagnostics);
                var destination = new RawDestination();
                transformer.Run(destination);
                token.ThrowIfCancellationRequested();
                return destination.XdmValue;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (ConversionException) { throw; }
        catch (Exception error)
        {
            throw new ConversionException(stage, "XDM validation failed.", error) { Diagnostics = diagnostics.Snapshot() };
        }
        finally
        {
            slots.Release();
        }
    }

    /// <summary>
    /// <para>Executes OpenXml as part of the SaxonFoTransformer conversion workflow.</para>
    /// <para>Выполняет OpenXml в цепочке преобразования SaxonFoTransformer.</para>
    /// </summary>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <param name="limit"><para>The limit argument.</para><para>Аргумент limit.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private XmlReader OpenXml(string path, long? limit = null)
    {
        if (new FileInfo(path).Length > (limit ?? options.MaxInputBytes))
        {
            throw new IOException("XML resource exceeds the byte limit.");
        }

        return XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = limit ?? options.MaxInputBytes
        });
    }


    /// <summary>
    /// <para>Executes OpenXml as part of the SaxonFoTransformer conversion workflow.</para>
    /// <para>Выполняет OpenXml в цепочке преобразования SaxonFoTransformer.</para>
    /// </summary>
    /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private XmlReader OpenXml(PreparedXmlSource source) => XmlReader.Create(source.Stream, new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = options.MaxInputBytes,
        CloseInput = false
    }, source.BaseUri?.AbsoluteUri ?? "urn:pdfgenerator:stream");


    /// <summary>
    /// <para>Returns an immutable snapshot of cache or worker counters.</para>
    /// <para>Возвращает неизменяемый снимок счётчиков кэша или worker.</para>
    /// </summary>
    public TemplateCacheStatistics CacheStatistics => cache.Statistics;

    /// <summary>
    /// <para>Executes ClearTemplateCache as part of the SaxonFoTransformer conversion workflow.</para>
    /// <para>Выполняет ClearTemplateCache в цепочке преобразования SaxonFoTransformer.</para>
    /// </summary>
    public void ClearTemplateCache() => cache.Clear();

    /// <summary>
    /// <para>Compiles a coherent stylesheet snapshot into the bounded cache; parameter values do not participate in its key.</para>
    /// <para>Компилирует согласованный снимок шаблона в ограниченный кэш; значения параметров не входят в ключ.</para>
    /// </summary>
    /// <param name="xslt"><para>The xslt argument.</para><para>Аргумент xslt.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task WarmupAsync(XmlSource xslt, LocalResourcePolicy? resources = null, CancellationToken token = default)
    {
        using PreparedXmlSource source = await PreparedXmlSource.CreateAsync(xslt, options.MaxStylesheetBytes, options.MemoryBufferThresholdBytes, token).ConfigureAwait(false);
        string? root = xslt.BaseUri is null ? null : Path.GetDirectoryName(xslt.BaseUri.LocalPath);
        var snapshot = StylesheetSnapshot.Create(source, resources ?? new LocalResourcePolicy(root is null ? [] : [root]), options, token);
        await cache.GetAsync(snapshot, token).ConfigureAwait(false);
    }

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose()
    {
        cache.Dispose();
        slots.Dispose();
    }
}


/// <summary>
/// <para>Provides the LimitedWriteStream contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию LimitedWriteStream для преобразования PDF.</para>
/// </summary>
/// <param name="inner"><para>The inner argument.</para><para>Аргумент inner.</para></param>
/// <param name="limit"><para>The limit argument.</para><para>Аргумент limit.</para></param>
internal sealed class LimitedWriteStream(Stream inner, long limit) : Stream
{

    /// <summary>
    /// <para>Gets or defines the written value used by LimitedWriteStream.</para>
    /// <para>Возвращает или определяет значение written, используемое LimitedWriteStream.</para>
    /// </summary>
    private long written;

    /// <summary>
    /// <para>Executes Write as part of the LimitedWriteStream conversion workflow.</para>
    /// <para>Выполняет Write в цепочке преобразования LimitedWriteStream.</para>
    /// </summary>
    /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
    /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
    /// <param name="count"><para>The count argument.</para><para>Аргумент count.</para></param>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <summary>
    /// <para>Executes Write as part of the LimitedWriteStream conversion workflow.</para>
    /// <para>Выполняет Write в цепочке преобразования LimitedWriteStream.</para>
    /// </summary>
    /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (written + buffer.Length > limit)
        {
            throw new IOException("FO output exceeds the byte limit.");
        }

        inner.Write(buffer);
        written += buffer.Length;
    }

    /// <summary>
    /// <para>Executes Flush as part of the LimitedWriteStream conversion workflow.</para>
    /// <para>Выполняет Flush в цепочке преобразования LimitedWriteStream.</para>
    /// </summary>
    public override void Flush() => inner.Flush();

    /// <summary>
    /// <para>Gets or defines the CanRead value used by LimitedWriteStream.</para>
    /// <para>Возвращает или определяет значение CanRead, используемое LimitedWriteStream.</para>
    /// </summary>
    public override bool CanRead => false;

    /// <summary>
    /// <para>Gets or defines the CanSeek value used by LimitedWriteStream.</para>
    /// <para>Возвращает или определяет значение CanSeek, используемое LimitedWriteStream.</para>
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// <para>Gets or defines the CanWrite value used by LimitedWriteStream.</para>
    /// <para>Возвращает или определяет значение CanWrite, используемое LimitedWriteStream.</para>
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// <para>Gets or defines the Length value used by LimitedWriteStream.</para>
    /// <para>Возвращает или определяет значение Length, используемое LimitedWriteStream.</para>
    /// </summary>
    public override long Length => written;

    /// <summary>
    /// <para>Gets or defines the Position value used by LimitedWriteStream.</para>
    /// <para>Возвращает или определяет значение Position, используемое LimitedWriteStream.</para>
    /// </summary>
    public override long Position
    {
        get => written; set => throw new NotSupportedException();
    }

    /// <summary>
    /// <para>Executes Read as part of the LimitedWriteStream conversion workflow.</para>
    /// <para>Выполняет Read в цепочке преобразования LimitedWriteStream.</para>
    /// </summary>
    /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
    /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
    /// <param name="count"><para>The count argument.</para><para>Аргумент count.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// <para>Executes Seek as part of the LimitedWriteStream conversion workflow.</para>
    /// <para>Выполняет Seek в цепочке преобразования LimitedWriteStream.</para>
    /// </summary>
    /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
    /// <param name="origin"><para>The origin argument.</para><para>Аргумент origin.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>
    /// <para>Executes SetLength as part of the LimitedWriteStream conversion workflow.</para>
    /// <para>Выполняет SetLength в цепочке преобразования LimitedWriteStream.</para>
    /// </summary>
    /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
    public override void SetLength(long value) => throw new NotSupportedException();
}
