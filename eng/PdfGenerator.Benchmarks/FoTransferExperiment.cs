using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PdfGenerator;

/// <summary><para>Compares file-backed FO with a buffered direct stream, without changing the production pipeline.</para><para>Сравнивает файловый FO с буферизованным прямым потоком без изменения производственной цепочки.</para></summary>
internal static class FoTransferExperiment
{
    /// <summary><para>Renders identical FO through file and memory sources and records page count, timing and retained input bytes.</para><para>Рендерит одинаковый FO через файловый источник и память, записывая страницы, время и объём удерживаемого входа.</para></summary>
    /// <param name="directory"><para>Report directory.</para><para>Каталог отчёта.</para></param>
    /// <returns><para>Asynchronous experiment completion.</para><para>Асинхронное завершение эксперимента.</para></returns>
    public static async Task RunAsync(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.fo");
        string blocks = string.Concat(Enumerable.Range(0, 5000).Select(i => $"<fo:block>Строка {i}: " + string.Concat(Enumerable.Repeat("data ", 20)) + "</fo:block>"));
        await File.WriteAllTextAsync(input, "<fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'><fo:layout-master-set><fo:simple-page-master master-name='a' page-width='210mm' page-height='297mm' margin='20mm'><fo:region-body/></fo:simple-page-master></fo:layout-master-set><fo:page-sequence master-reference='a'><fo:flow flow-name='xsl-region-body' font-family='sans-serif' font-size='10pt'>" + blocks + "</fo:flow></fo:page-sequence></fo:root>", Encoding.UTF8);
        using var cache = new FopFactoryCache(1, 128L * 1024 * 1024);
        using FopFactoryCache.Lease lease = cache.Acquire(BundledFopRuntime.ConfigurationPath);
        var samples = new List<object>();
        for (int round = 0; round < 4; round++)
        {
            bool direct = round % 2 != 0;
            var timer = Stopwatch.StartNew();
            byte[]? buffer = direct ? await File.ReadAllBytesAsync(input) : null;
            using var output = new java.io.FileOutputStream(Path.Combine(directory, $"{round}.pdf"));
            org.apache.fop.apps.Fop fop = lease.Factory.newFop("application/pdf", lease.Factory.newFOUserAgent(), output);
            using java.io.ByteArrayInputStream? stream = direct ? new java.io.ByteArrayInputStream(buffer!) : null;
            javax.xml.transform.stream.StreamSource source = direct ? new javax.xml.transform.stream.StreamSource(stream) : new javax.xml.transform.stream.StreamSource(new java.io.File(input));
            source.setSystemId(new Uri(input).AbsoluteUri);
            javax.xml.transform.TransformerFactory.newInstance().newTransformer().transform(source, new javax.xml.transform.sax.SAXResult(fop.getDefaultHandler()));
            samples.Add(new
            {
                round,
                source = direct ? "bufferedStream" : "file",
                elapsedMs = timer.Elapsed.TotalMilliseconds,
                pages = fop.getResults().getPageCount(),
                retainedInputBytes = buffer?.LongLength ?? 0
            });
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "fo-transfer.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
    }
}
