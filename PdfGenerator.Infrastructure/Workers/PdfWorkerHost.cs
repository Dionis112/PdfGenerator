using System.Text.Json;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the PdfWorkerHost contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PdfWorkerHost для преобразования PDF.</para>
/// </summary>
internal static class PdfWorkerHost
{
    /// <summary><para>Gets aliases registered in this isolated worker.</para><para>Псевдонимы, зарегистрированные в изолированном worker.</para></summary>
    internal static IReadOnlyDictionary<string, string>? SvgFontAliases
    {
        get; private set;
    }

    /// <summary>
    /// <para>Gets or defines the InWorker value used by PdfWorkerHost.</para>
    /// <para>Возвращает или определяет значение InWorker, используемое PdfWorkerHost.</para>
    /// </summary>
    public static bool InWorker
    {
        get; private set;
    }

    /// <summary>
    /// <para>Executes Main as part of the PdfWorkerHost conversion workflow.</para>
    /// <para>Выполняет Main в цепочке преобразования PdfWorkerHost.</para>
    /// </summary>
    /// <param name="args"><para>The args argument.</para><para>Аргумент args.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || args[0] != "--pdf-worker")
        {
            return 64;
        }
        InWorker = true;
        string root = Path.GetFullPath(args[1]);
        var owned = new LocalResourcePolicy([root]);
        string config = owned.Resolve(new Uri(Path.GetFullPath(args[2])));
        WorkerHostSettings settings = JsonSerializer.Deserialize<WorkerHostSettings>(await File.ReadAllTextAsync(config))!;
        using var transformer = new SaxonFoTransformer(settings.XmlOptions);
        using var renderer = new EmbeddedFopRenderer(settings.RendererOptions);
        var generator = new XmlPdfGenerator(transformer, renderer);
        TextWriter protocol = Console.Out;
        Console.SetOut(TextWriter.Null);
        string bundled = BundledFopRuntime.ConfigurationPath;
        SvgFontAliases = BundledFopRuntime.RegisterFonts(settings.RendererOptions.ConfigurationPath ?? bundled);
        await WorkerProtocol.WriteAsync(protocol, new("ready", Engine: WorkerProtocol.Engine), CancellationToken.None);
        while (await WorkerProtocol.ReadAsync(Console.In, CancellationToken.None) is { } message)
        {
            if (message.Kind == "stop")
            {
                return 0;
            }
            if (message.Kind == "ping")
            {
                await WorkerProtocol.WriteAsync(protocol, new("pong"), CancellationToken.None);
                continue;
            }
            if (message.Kind != "job" || message.Job is null)
            {
                return 65;
            }
            WorkerJob job = message.Job;
            try
            {
                string directory = owned.Resolve(new Uri(Path.GetFullPath(job.Directory)));
                var jobPolicy = new LocalResourcePolicy([directory]);
                string xml = jobPolicy.Resolve(new Uri(Path.Combine(directory, "input.xml")));
                string xslt = jobPolicy.Resolve(new Uri(Path.Combine(directory, "template.xsl")));
                string pdf = Path.Combine(directory, "result.pdf");
                using FileStream xmlStream = File.OpenRead(xml);
                using FileStream xsltStream = File.OpenRead(xslt);
                var request = new ConversionRequest(XmlSource.FromStream(xmlStream, job.XmlBaseUri is null ? null : new Uri(job.XmlBaseUri)),
                    XmlSource.FromStream(xsltStream, job.XsltBaseUri is null ? null : new Uri(job.XsltBaseUri)),
                    job.Parameters.Select(p => p.ToParameter()), job.JobId, new LocalResourcePolicy(job.ResourceRoots), job.Settings);
                ConversionResult result = await generator.GenerateAsync(request, pdf);
                result = result with
                {
                    EngineCounters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(new Dictionary<string, long>
                    {
                        ["xsltHits"] = transformer.CacheStatistics.Hits,
                        ["xsltMisses"] = transformer.CacheStatistics.Misses,
                        ["xsltCompilations"] = transformer.CacheStatistics.Compilations,
                        ["xsltEvictions"] = transformer.CacheStatistics.Evictions,
                        ["xsltEntries"] = transformer.CacheStatistics.Entries,
                        ["xsltEstimatedBytes"] = transformer.CacheStatistics.EstimatedBytes,
                        ["fopHits"] = renderer.CacheStatistics.Hits,
                        ["fopMisses"] = renderer.CacheStatistics.Misses,
                        ["fopEntries"] = renderer.CacheStatistics.Entries,
                        ["fopEstimatedBytes"] = renderer.CacheStatistics.EstimatedBytes,
                        ["managedHeap"] = GC.GetTotalMemory(false),
                        ["allocatedBytes"] = GC.GetTotalAllocatedBytes(),
                        ["gc0"] = GC.CollectionCount(0),
                        ["gc1"] = GC.CollectionCount(1),
                        ["gc2"] = GC.CollectionCount(2)
                    })
                };
                await WorkerProtocol.WriteAsync(protocol, new("result", Result: result), CancellationToken.None);
            }
            catch (ConversionException ex) { await WorkerProtocol.WriteAsync(protocol, new("error", Stage: ex.Stage, ErrorCode: ex.InnerException?.GetType().Name), CancellationToken.None); }
            catch (Exception ex) { await WorkerProtocol.WriteAsync(protocol, new("error", Stage: ConversionStage.Input, ErrorCode: ex.GetType().Name), CancellationToken.None); }
        }
        return 0;
    }
}

/// <summary>
/// <para>Provides the WorkerHostSettings contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerHostSettings для преобразования PDF.</para>
/// </summary>
/// <param name="XmlOptions"><para>The XmlOptions argument.</para><para>Аргумент XmlOptions.</para></param>
/// <param name="RendererOptions"><para>The RendererOptions argument.</para><para>Аргумент RendererOptions.</para></param>
internal sealed record WorkerHostSettings(XmlFoOptions XmlOptions, EmbeddedFopOptions RendererOptions);
