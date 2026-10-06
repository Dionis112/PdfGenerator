using System.Xml;
using Saxon.Api;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the TemplateCacheStatistics contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию TemplateCacheStatistics для преобразования PDF.</para>
/// </summary>
/// <param name="Hits"><para>The Hits argument.</para><para>Аргумент Hits.</para></param>
/// <param name="Misses"><para>The Misses argument.</para><para>Аргумент Misses.</para></param>
/// <param name="Compilations"><para>The Compilations argument.</para><para>Аргумент Compilations.</para></param>
/// <param name="Evictions"><para>The Evictions argument.</para><para>Аргумент Evictions.</para></param>
/// <param name="Entries"><para>The Entries argument.</para><para>Аргумент Entries.</para></param>
/// <param name="EstimatedBytes"><para>The EstimatedBytes argument.</para><para>Аргумент EstimatedBytes.</para></param>
public sealed record TemplateCacheStatistics(long Hits, long Misses, long Compilations, long Evictions, int Entries, long EstimatedBytes);


/// <summary>
/// <para>Provides the XsltTemplateCache contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltTemplateCache для преобразования PDF.</para>
/// </summary>
/// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
internal sealed class XsltTemplateCache(XmlFoOptions options) : IDisposable
{

    /// <summary>
    /// <para>Provides the Entry contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию Entry для преобразования PDF.</para>
    /// </summary>
    /// <param name="Task"><para>The Task argument.</para><para>Аргумент Task.</para></param>
    /// <param name="Weight"><para>The Weight argument.</para><para>Аргумент Weight.</para></param>
    private sealed record Entry(Task<CompiledStylesheet> Task, long Weight)
    {

        /// <summary>
        /// <para>Gets or defines the LastUse value used by Entry.</para>
        /// <para>Возвращает или определяет значение LastUse, используемое Entry.</para>
        /// </summary>
        public DateTime LastUse { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// <para>Gets or defines the gate value used by XsltTemplateCache.</para>
    /// <para>Возвращает или определяет значение gate, используемое XsltTemplateCache.</para>
    /// </summary>
    private readonly object gate = new();

    /// <summary>
    /// <para>Gets or defines the entries value used by XsltTemplateCache.</para>
    /// <para>Возвращает или определяет значение entries, используемое XsltTemplateCache.</para>
    /// </summary>
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /// <summary>
    /// <para>Gets or defines the hits value used by XsltTemplateCache.</para>
    /// <para>Возвращает или определяет значение hits, используемое XsltTemplateCache.</para>
    /// </summary>
    private long hits, misses, compilations, evictions, bytes;

    /// <summary>
    /// <para>Gets or defines the compileSlots value used by XsltTemplateCache.</para>
    /// <para>Возвращает или определяет значение compileSlots, используемое XsltTemplateCache.</para>
    /// </summary>
    private readonly SemaphoreSlim compileSlots = new(options.MaxConcurrency);

    /// <summary>
    /// <para>Tracks shared compilation tasks, including uncached entries and canceled waiters, until completion.</para>
    /// <para>Учитывает общие задачи компиляции, включая некэшируемые записи и отменённых ожидателей, до завершения.</para>
    /// </summary>
    private readonly HashSet<Task<CompiledStylesheet>> pending = [];

    /// <summary>
    /// <para>Prevents new compilation admission after disposal begins.</para>
    /// <para>Запрещает новую компиляцию после начала освобождения ресурсов.</para>
    /// </summary>
    private bool disposed;


    /// <summary>
    /// <para>Returns an immutable snapshot of cache or worker counters.</para>
    /// <para>Возвращает неизменяемый снимок счётчиков кэша или worker.</para>
    /// </summary>
    public TemplateCacheStatistics Statistics
    {
        get
        {
            lock (gate)
            {
                return new(hits, misses, compilations, evictions, entries.Count, bytes);
            }
        }
    }


    /// <summary>
    /// <para>Executes GetAsync as part of the XsltTemplateCache conversion workflow.</para>
    /// <para>Выполняет GetAsync в цепочке преобразования XsltTemplateCache.</para>
    /// </summary>
    /// <param name="snapshot"><para>The snapshot argument.</para><para>Аргумент snapshot.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public Task<CompiledStylesheet> GetAsync(StylesheetSnapshot snapshot, CancellationToken token)
    {
        Task<CompiledStylesheet> task;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            pending.RemoveWhere(t => t.IsCompleted);
            foreach (string key in entries.Where(p => p.Value.Task.IsCompleted &&
                (p.Value.Task.IsFaulted || DateTime.UtcNow - p.Value.LastUse > options.TemplateCacheIdleTime)).Select(p => p.Key).ToArray())
            {
                Remove(key);
            }
            if (entries.TryGetValue(snapshot.Key, out Entry? entry))
            {
                hits++;
                entry.LastUse = DateTime.UtcNow;
                snapshot.Dispose();
                task = entry.Task;
            }
            else
            {
                if (pending.Count >= Math.Max(1, options.TemplateCacheEntries) + options.MaxConcurrency)
                {
                    snapshot.Dispose();
                    throw new IOException("Shared compilation queue is full.");
                }
                misses++;
                long weight = checked(snapshot.Bytes * 8 + 512 * 1024);
                while (entries.Count > 0 && (entries.Count >= options.TemplateCacheEntries || bytes + weight > options.TemplateCacheBytes))
                {
                    string? key = entries.Where(p => p.Value.Task.IsCompleted).OrderBy(p => p.Value.LastUse).Select(p => p.Key).FirstOrDefault();
                    if (key is null)
                    {
                        break;
                    }
                    Remove(key);
                }
                task = CompileAsync(snapshot);
                pending.Add(task);
                if (options.TemplateCacheEntries > 0 && weight <= options.TemplateCacheBytes && entries.Count < options.TemplateCacheEntries && bytes + weight <= options.TemplateCacheBytes)
                {
                    entries.Add(snapshot.Key, new(task, weight));
                    bytes += weight;
                }
            }
        }
        return task.WaitAsync(token);
    }


    /// <summary>
    /// <para>Executes CompileAsync as part of the XsltTemplateCache conversion workflow.</para>
    /// <para>Выполняет CompileAsync в цепочке преобразования XsltTemplateCache.</para>
    /// </summary>
    /// <param name="snapshot"><para>The snapshot argument.</para><para>Аргумент snapshot.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private async Task<CompiledStylesheet> CompileAsync(StylesheetSnapshot snapshot)
    {
        await compileSlots.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                lock (gate)
                {
                    compilations++;
                }
                Processor processor = SaxonProcessorFactory.Create(options);
                var readers = new List<XmlReader>();
                var diagnostics = new SaxonDiagnosticCollector(ConversionStage.Compile);
                try
                {
                    processor.SetResourceResolver(request =>
                    {
                        if (request.Nature != ResourceRequest.XSLT_NATURE || !snapshot.Modules.ContainsKey(request.Uri.AbsoluteUri))
                        {
                            throw new XmlException("Resource is not in the stylesheet snapshot.");
                        }
                        XmlReader reader = snapshot.Open(request.Uri);
                        readers.Add(reader);
                        return new XmlReaderResource(processor.NewDocumentBuilder(), reader);
                    }, false);
                    using XmlReader main = snapshot.Open(snapshot.BaseUri);
                    XsltCompiler compiler = processor.NewXsltCompiler();
                    compiler.ErrorReporter = diagnostics.Report;
                    XsltExecutable executable = compiler.Compile(main);
                    // Freeze runtime policy before publishing the immutable executable to other jobs.
                    processor.SetResourceResolver(_ => throw new XmlException("External runtime resources are disabled."), false);
                    return new CompiledStylesheet(executable, diagnostics.Snapshot());
                }
                catch (Exception ex)
                {
                    throw new ConversionException(ConversionStage.Compile, "XSLT compilation failed.", ex)
                    {
                        Diagnostics = diagnostics.Snapshot()
                    };
                }
                finally { foreach (XmlReader reader in readers) { reader.Dispose(); } }
            }).ConfigureAwait(false);
        }
        finally { snapshot.Dispose(); compileSlots.Release(); }
    }


    /// <summary>
    /// <para>Executes Remove as part of the XsltTemplateCache conversion workflow.</para>
    /// <para>Выполняет Remove в цепочке преобразования XsltTemplateCache.</para>
    /// </summary>
    /// <param name="key"><para>The key argument.</para><para>Аргумент key.</para></param>
    private void Remove(string key)
    {
        bytes -= entries[key].Weight;
        entries.Remove(key);
        evictions++;
    }

    /// <summary>
    /// <para>Executes Clear as part of the XsltTemplateCache conversion workflow.</para>
    /// <para>Выполняет Clear в цепочке преобразования XsltTemplateCache.</para>
    /// </summary>
    public void Clear()
    {
        lock (gate)
        {
            foreach (string key in entries.Keys.ToArray())
            {
                Remove(key);
            }
        }
    }

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose()
    {
        Task<CompiledStylesheet>[] outstanding;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            outstanding = [.. pending];
        }
        try
        {
            Task.WhenAll(outstanding).GetAwaiter().GetResult();
        }
        catch { }
        Clear();
        compileSlots.Dispose();
    }
}
