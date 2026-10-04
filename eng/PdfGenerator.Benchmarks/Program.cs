using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using PdfGenerator;

if (args.Length > 0 && args[0] == "--fo-transfer")
{
    await FoTransferExperiment.RunAsync(args.Length > 1 ? args[1] : "artifacts/benchmarks/fo-transfer");
    return;
}

int size = args.Length > 0 ? int.Parse(args[0]) : 10;
int concurrency = args.Length > 1 ? int.Parse(args[1]) : 1;
int iterations = args.Length > 2 ? int.Parse(args[2]) : 4;
string output = Path.GetFullPath(args.Length > 3 ? args[3] : "artifacts/benchmarks");
Directory.CreateDirectory(output);
string temporaryRoot = Path.Combine(output, ".job-temp");
Directory.CreateDirectory(temporaryRoot);
Environment.SetEnvironmentVariable("TEMP", temporaryRoot);
Environment.SetEnvironmentVariable("TMP", temporaryRoot);
Environment.SetEnvironmentVariable("TMPDIR", temporaryRoot);
string xml = Path.Combine(output, $"input-{size}.xml");
string xslt = Path.Combine(output, "benchmark.xsl");
await File.WriteAllBytesAsync(Path.Combine(output, "icc.png"), PdfGenerator.TestData.PngFixture.Create());
if (!File.Exists(xml))
{
    using FileStream file = File.Create(xml);
    using var writer = XmlWriter.Create(file, new()
    {
        Encoding = new System.Text.UTF8Encoding(false)
    });
    writer.WriteStartElement("document");
    string payload = new('x', 480);
    int rows = checked(size * 1024 * 1024 / 550);
    for (int i = 0; i < rows; i++)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("id", i.ToString());
        writer.WriteAttributeString("group", (i % 50).ToString());
        writer.WriteAttributeString("amount", "1.25");
        writer.WriteString(payload);
        writer.WriteEndElement();
    }
    writer.WriteEndElement();
}
await File.WriteAllTextAsync(xslt, """
    <xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:fo="http://www.w3.org/1999/XSL/Format" version="2.0">
    <xsl:key name="rows" match="row" use="@group"/>
    <xsl:template match="/">
    <fo:root><fo:layout-master-set><fo:simple-page-master master-name="a4" page-width="210mm" page-height="297mm" margin="15mm"><fo:region-body/></fo:simple-page-master></fo:layout-master-set>
    <fo:page-sequence master-reference="a4"><fo:flow flow-name="xsl-region-body" font-family="sans-serif" font-size="10pt">
    <fo:block>Кириллица: измерение XML</fo:block>
    <fo:block><xsl:for-each select="1 to 20"><fo:external-graphic src="icc.png" content-width="3mm"/></xsl:for-each></fo:block>
    <xsl:for-each-group select="document/row" group-by="@group"><xsl:sort select="number(current-grouping-key())" data-type="number"/>
    <fo:block><xsl:value-of select="concat(current-grouping-key(), ': ', count(key('rows', current-grouping-key())), ', ', sum(current-group()/@amount/xs:decimal(.)))"/></fo:block>
    </xsl:for-each-group>
    <fo:table table-layout="fixed" width="100%"><fo:table-column column-width="20%"/><fo:table-column column-width="80%"/>
    <fo:table-header><fo:table-row><fo:table-cell><fo:block>ID</fo:block></fo:table-cell><fo:table-cell><fo:block>Содержание</fo:block></fo:table-cell></fo:table-row></fo:table-header>
    <fo:table-body><xsl:for-each select="document/row[position() le 500]"><fo:table-row><fo:table-cell><fo:block><xsl:value-of select="@id"/></fo:block></fo:table-cell><fo:table-cell><fo:block><xsl:value-of select="substring(.,1,50)"/></fo:block></fo:table-cell></fo:table-row></xsl:for-each></fo:table-body></fo:table>
    <fo:block><fo:instream-foreign-object><svg xmlns="http://www.w3.org/2000/svg" width="100" height="30"><rect width="100" height="30" fill="green"/><text x="2" y="20" font-family="sans-serif">SVG текст</text></svg></fo:instream-foreign-object></fo:block>
    </fo:flow></fo:page-sequence></fo:root></xsl:template></xsl:stylesheet>
    """);
using var transformer = new SaxonFoTransformer(new() { MaxInputBytes = 128L * 1024 * 1024, MaxConcurrency = concurrency });
using var renderer = new EmbeddedFopRenderer(new() { MaxConcurrency = concurrency });
bool useWorker = args.Length > 4 && args[4] == "worker";
string scenario = args.Length > 5 ? args[5] : "repeated";
await using WorkerXmlPdfGenerator? worker = useWorker ? new WorkerXmlPdfGenerator(new()
{
    MaxWorkers = concurrency,
    MaxWorkingSetBytes = 1024L * 1024 * 1024,
    MemoryBudgetBytes = concurrency * 1024L * 1024 * 1024,
    WorkingDirectory = output,
    RecycleAfterJobs = scenario == "oneshot" ? 1 : 100
},
    new()
    {
        MaxInputBytes = 128L * 1024 * 1024
    }) : null;
IXmlPdfGenerator generator = worker is null ? new XmlPdfGenerator(transformer, renderer) : worker;
await using var scheduled = new ScheduledXmlPdfGenerator(generator, new() { MaxActiveJobs = concurrency, MaxQueuedJobs = concurrency * 4 });
using var process = Process.GetCurrentProcess();
long peak = 0;
long peakHeap = 0;
long peakTemporary = 0;
using var sampleCancellation = new CancellationTokenSource();
var sample = Task.Run(async () =>
{
    try
    {
        while (true)
        {
            process.Refresh();
            long workingSet = process.WorkingSet64;
            if (worker is not null)
            {
                foreach (int pid in worker.ProcessIds)
                {
                    try
                    {
                        using var child = Process.GetProcessById(pid);
                        workingSet += child.WorkingSet64;
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                }
            }
            peak = Math.Max(peak, workingSet);
            peakHeap = Math.Max(peakHeap, GC.GetTotalMemory(false));
            long temporary = 0;
            try
            {
                foreach (string path in Directory.EnumerateFiles(output, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }).Where(p => p.Contains("xmlpdf-") || p.Contains(".tmp") || p.Contains(".job-temp") || p.Contains(".png-resources-")))
                {
                    try
                    {
                        temporary += new FileInfo(path).Length;
                    }
                    catch (IOException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            peakTemporary = Math.Max(peakTemporary, temporary);
            await Task.Delay(50, sampleCancellation.Token);
        }
    }
    catch (OperationCanceledException) { }
});
long allocated = GC.GetTotalAllocatedBytes();
int[] gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
var watch = Stopwatch.StartNew();
var measurements = new List<object>();
var durations = new List<double>();
var warmDurations = new List<double>();
double firstRoundMs = 0;
for (int round = 0; round < iterations; round++)
{
    string template = xslt;
    if (scenario == "mixed" || scenario == "new")
    {
        int version = scenario == "mixed" ? round % 3 : round;
        template = Path.Combine(output, $"template-{version}.xsl");
        if (!File.Exists(template))
        {
            await File.WriteAllTextAsync(template, (await File.ReadAllTextAsync(xslt)).Replace("XML</fo:block>", $"XML {version}</fo:block>"));
        }
    }
    ConversionResult[] results = await Task.WhenAll(Enumerable.Range(0, scenario == "queued" ? concurrency * 2 : concurrency).Select(async i =>
    {
        await using FileStream inputStream = File.OpenRead(xml);
        var request = new ConversionRequest(scenario == "stream" ? XmlSource.FromStream(inputStream, new Uri(xml)) : XmlSource.FromFile(xml), XmlSource.FromFile(template));
        if (scenario == "baseline")
        {
            using var uncachedTransformer = new SaxonFoTransformer(new()
            {
                MaxInputBytes = 128L * 1024 * 1024
            });
            using var uncachedRenderer = new EmbeddedFopRenderer();
            return await new XmlPdfGenerator(uncachedTransformer, uncachedRenderer).GenerateAsync(request, Path.Combine(output, $"result-{round}-{i}.pdf"));
        }
        return await scheduled.GenerateAsync(request, Path.Combine(output, $"result-{round}-{i}.pdf"));
    }));
    foreach (ConversionResult result in results)
    {
        durations.Add(result.TotalDuration.TotalMilliseconds);
        if (round > 0)
        {
            warmDurations.Add(result.TotalDuration.TotalMilliseconds);
        }
        process.Refresh();
        measurements.Add(new
        {
            round,
            result.PageCount,
            result.PdfBytes,
            result.StageDurations,
            result.EngineCounters,
            parentWorkingSet = process.WorkingSet64,
            managedHeap = GC.GetTotalMemory(false),
            queueMs = result.QueueDuration.TotalMilliseconds,
            preparationMs = result.InputPreparationDuration.TotalMilliseconds,
            transformMs = result.TransformDuration.TotalMilliseconds,
            renderMs = result.RenderDuration.TotalMilliseconds,
            publishMs = result.PublishDuration.TotalMilliseconds,
            totalMs = result.TotalDuration.TotalMilliseconds
        });
    }
    if (round == 0)
    {
        firstRoundMs = watch.Elapsed.TotalMilliseconds;
    }
}
sampleCancellation.Cancel();
await sample;
double[] sorted = [.. durations.Order()];
double[] warm = [.. warmDurations.Order()];
using Stream fontResource = typeof(EmbeddedFopRenderer).Assembly.GetManifestResourceStream("PdfGenerator.Resources.Fonts.DejaVuSans.ttf")!;
string fontSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fontResource));
var report = new
{
    dateUtc = DateTimeOffset.UtcNow,
    scenario,
    mode = useWorker ? "worker" : "inProcess",
    templateCache = useWorker || scenario == "baseline" ? null : (object)transformer.CacheStatistics,
    fopCache = useWorker || scenario == "baseline" ? null : (object)renderer.CacheStatistics,
    workerPool = worker?.Statistics,
    framework = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    processors = Environment.ProcessorCount,
    memoryAvailable = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
    saxon = typeof(Saxon.Api.Processor).Assembly.GetName().Version?.ToString(),
    fop = "2.11",
    ikvm = typeof(java.lang.System).Assembly.GetName().Version?.ToString(),
    fontSha256,
    corpus = new
    {
        version = 2,
        rowCount = size * 1024 * 1024 / 550,
        depth = 2,
        attributesPerRow = 3,
        outputTableRows = 500,
        charactersPerOutputRow = 50,
        repeatedPng = 20,
        uniquePng = 1,
        svg = 1
    },
    limits = new
    {
        xmlBytes = 128L * 1024 * 1024,
        foBytes = 64L * 1024 * 1024,
        pdfBytes = 64L * 1024 * 1024,
        imageBytes = 32L * 1024 * 1024,
        temporaryBytes = 512L * 1024 * 1024
    },
    heapScope = useWorker ? "Parent peak; child completion counters in measurements" : "In-process peak",
    inputBytes = new FileInfo(xml).Length,
    sizeMiB = size,
    concurrency,
    iterations,
    peakWorkingSet = peak,
    peakManagedHeap = peakHeap,
    peakTemporary,
    allocatedBytes = GC.GetTotalAllocatedBytes() - allocated,
    gcCounts = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray(),
    elapsedMs = watch.Elapsed.TotalMilliseconds,
    documentsPerMinute = durations.Count / watch.Elapsed.TotalMinutes,
    p50Ms = sorted[(sorted.Length - 1) / 2],
    p95Ms = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1],
    coldRoundMs = firstRoundMs,
    warmP50Ms = warm.Length == 0 ? (double?)null : warm[(warm.Length - 1) / 2],
    warmP95Ms = warm.Length == 0 ? (double?)null : warm[(int)Math.Ceiling(warm.Length * 0.95) - 1],
    warmDocumentsPerMinute = warm.Length == 0 ? (double?)null : warm.Length * 60000 / (watch.Elapsed.TotalMilliseconds - firstRoundMs),
    measurements
};
string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(Path.Combine(output, $"report-{size}-{concurrency}-{DateTime.UtcNow:yyyyMMddHHmmss}.json"), json);
Console.WriteLine(json);
