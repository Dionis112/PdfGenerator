using Xunit;

namespace PdfGenerator.IntegrationTests;


/// <summary>
/// <para>Runs the CacheTests validation scenario.</para>
/// <para>Выполняет проверку сценария CacheTests.</para>
/// </summary>
public sealed class CacheTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pdf-cache-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// <para>Runs the CacheTests validation scenario.</para>
    /// <para>Выполняет проверку сценария CacheTests.</para>
    /// </summary>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public CacheTests() => Directory.CreateDirectory(directory);

    /// <summary>
    /// <para>Runs the FilePath validation scenario.</para>
    /// <para>Выполняет проверку сценария FilePath.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private string FilePath(string name) => Path.Combine(directory, name);

    /// <summary>
    /// <para>Runs the ParametersReuseCompilationAndTransitiveChangeInvalidatesIt validation scenario.</para>
    /// <para>Выполняет проверку сценария ParametersReuseCompilationAndTransitiveChangeInvalidatesIt.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task ParametersReuseCompilationAndTransitiveChangeInvalidatesIt()
    {
        File.WriteAllText(FilePath("input.xml"), "<root/>");
        File.WriteAllText(FilePath("main.xsl"), "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:include href='nested.xsl'/><xsl:param name='title'/><xsl:template match='/'><fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'><xsl:value-of select='$title'/><xsl:call-template name='value'/></fo:root></xsl:template></xsl:stylesheet>");
        File.WriteAllText(FilePath("nested.xsl"), "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:include href='leaf.xsl'/></xsl:stylesheet>");
        void Leaf(string value) => File.WriteAllText(FilePath("leaf.xsl"), $"<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:template name='value'>{value}</xsl:template></xsl:stylesheet>");
        Leaf("first");
        using var transformer = new SaxonFoTransformer();
        await Task.WhenAll(Enumerable.Range(0, 6).Select(i => transformer.TransformAsync(new(XmlSource.FromFile(FilePath("input.xml")), XmlSource.FromFile(FilePath("main.xsl")), [new("title", i)]), FilePath($"{i}.fo"))));
        Assert.Equal(1, transformer.CacheStatistics.Compilations);
        Assert.Equal(5, transformer.CacheStatistics.Hits);
        Leaf("second");
        await transformer.TransformFileAsync(FilePath("input.xml"), FilePath("main.xsl"), FilePath("changed.fo"));
        Assert.Contains("second", File.ReadAllText(FilePath("changed.fo")));
        Assert.Equal(2, transformer.CacheStatistics.Compilations);
    }

    /// <summary>
    /// <para>Runs the CacheIsBoundedAndCompilationErrorCanRecover validation scenario.</para>
    /// <para>Выполняет проверку сценария CacheIsBoundedAndCompilationErrorCanRecover.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task CacheIsBoundedAndCompilationErrorCanRecover()
    {
        File.WriteAllText(FilePath("input.xml"), "<root/>");
        using var transformer = new SaxonFoTransformer(new()
        {
            TemplateCacheEntries = 1
        });
        for (int i = 0; i < 3; i++)
        {
            string xslt = FilePath($"{i}.xsl");
            File.WriteAllText(xslt, "<broken>");
            await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(FilePath("input.xml"), xslt, FilePath("result.fo")));
            File.WriteAllText(xslt, $"<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:template match='/'><fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'>{i}</fo:root></xsl:template></xsl:stylesheet>");
            await transformer.TransformFileAsync(FilePath("input.xml"), xslt, FilePath("result.fo"));
        }
        Assert.Equal(1, transformer.CacheStatistics.Entries);
        Assert.True(transformer.CacheStatistics.Evictions >= 2);
    }

    /// <summary>
    /// <para>Runs the ConcurrentFopDocumentsReuseOneFactory validation scenario.</para>
    /// <para>Выполняет проверку сценария ConcurrentFopDocumentsReuseOneFactory.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task ConcurrentFopDocumentsReuseOneFactory()
    {
        using var renderer = new EmbeddedFopRenderer();
        string fixture = Path.Combine(AppContext.BaseDirectory, "test-data/basic/expected.fo");
        await Task.WhenAll(Enumerable.Range(0, 4).Select(i => renderer.RenderFileAsync(fixture, FilePath($"{i}.pdf"))));
        Assert.Equal(1, renderer.CacheStatistics.Misses);
        Assert.Equal(3, renderer.CacheStatistics.Hits);
        Assert.Equal(1, renderer.CacheStatistics.Entries);
    }

    /// <summary><para>Cancellation of one waiter leaves shared compilation usable by the next job.</para><para>Отмена одного ожидателя оставляет общую компиляцию доступной следующему заданию.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task CanceledCompilationWaiterDoesNotPoisonSharedEntry()
    {
        string stylesheet = FilePath("shared.xsl");
        File.WriteAllText(stylesheet, "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'>" +
            string.Concat(Enumerable.Range(0, 5000).Select(i => $"<xsl:template name='t{i}'><xsl:value-of select='{i}+1'/></xsl:template>")) + "</xsl:stylesheet>");
        var options = new XmlFoOptions();
        using var cache = new XsltTemplateCache(options);
        using PreparedXmlSource source = await PreparedXmlSource.CreateAsync(XmlSource.FromFile(stylesheet), options.MaxStylesheetBytes, options.MemoryBufferThresholdBytes, default);
        var policy = new LocalResourcePolicy([directory]);
        var first = StylesheetSnapshot.Create(source, policy, options, default);
        using var cancellation = new CancellationTokenSource();
        Task<Saxon.Api.XsltExecutable> canceled = cache.GetAsync(first, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        using PreparedXmlSource secondSource = await PreparedXmlSource.CreateAsync(XmlSource.FromFile(stylesheet), options.MaxStylesheetBytes, options.MemoryBufferThresholdBytes, default);
        var second = StylesheetSnapshot.Create(secondSource, policy, options, default);
        Assert.NotNull(await cache.GetAsync(second, default));
        Assert.Equal(1, cache.Statistics.Compilations);
        Assert.Equal(1, cache.Statistics.Hits);
    }

    /// <summary><para>Changing configuration invalidates a factory while the previous immutable snapshot remains available.</para><para>Изменение конфигурации обновляет фабрику, сохраняя прежний неизменяемый снимок.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task FactoryConfigurationChangeCreatesNewSnapshot()
    {
        string config = FilePath("fop.xconf");
        File.Copy(BundledFopRuntime.ConfigurationPath, config);
        using var renderer = new EmbeddedFopRenderer(new()
        {
            ConfigurationPath = config
        });
        string fixture = Path.Combine(AppContext.BaseDirectory, "test-data/basic/expected.fo");
        await renderer.RenderFileAsync(fixture, FilePath("first.pdf"));
        var document = System.Xml.Linq.XDocument.Load(config);
        document.Root!.Add(new System.Xml.Linq.XElement("target-resolution", 96));
        document.Save(config);
        await renderer.RenderFileAsync(fixture, FilePath("second.pdf"));
        Assert.Equal(2, renderer.CacheStatistics.Misses);
        Assert.Equal(2, renderer.CacheStatistics.Entries);
    }

    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose() => Directory.Delete(directory, true);
}
