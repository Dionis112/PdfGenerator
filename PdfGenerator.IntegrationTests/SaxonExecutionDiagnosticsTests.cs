using System.Text.Json;
using Xunit;

namespace PdfGenerator.IntegrationTests;

/// <summary><para>Checks real Saxon diagnostics, cache reuse and worker transport.</para><para>Проверяет реальные диагностики Saxon, кэш и передачу через worker.</para></summary>
public sealed class SaxonExecutionDiagnosticsTests : IDisposable
{
    /// <summary><para>Isolated inputs and output directory.</para><para>Изолированный каталог входов и результатов.</para></summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "saxon-diagnostics-" + Guid.NewGuid().ToString("N"));

    /// <summary><para>Creates the fixture directory.</para><para>Создаёт каталог теста.</para></summary>
    public SaxonExecutionDiagnosticsTests() => Directory.CreateDirectory(directory);

    /// <summary><para>Writes a document and a complete FO-producing stylesheet.</para><para>Записывает документ и шаблон, формирующий полный FO.</para></summary>
    /// <param name="body"><para>XSLT instructions inside the flow.</para><para>Инструкции XSLT внутри потока FO.</para></param>
    /// <param name="templates"><para>Additional stylesheet templates.</para><para>Дополнительные шаблоны стиля.</para></param>
    /// <returns><para>Conversion request.</para><para>Запрос преобразования.</para></returns>
    private ConversionRequest Request(string body, string templates = "")
    {
        string xml = Path.Combine(directory, "sensitive-input.xml");
        string xslt = Path.Combine(directory, "sensitive-stylesheet.xsl");
        File.WriteAllText(xml, "<document><title>SECRET_DOCUMENT_VALUE</title></document>");
        File.WriteAllText(xslt, $$"""
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:fo="http://www.w3.org/1999/XSL/Format">
              <xsl:template match="/">
                <fo:root>
                  <fo:layout-master-set><fo:simple-page-master master-name="a4" page-width="210mm" page-height="297mm" margin="15mm"><fo:region-body/></fo:simple-page-master></fo:layout-master-set>
                  <fo:page-sequence master-reference="a4"><fo:flow flow-name="xsl-region-body" font-family="sans-serif">
                    {{body}}
                  </fo:flow></fo:page-sequence>
                </fo:root>
              </xsl:template>
              {{templates}}
            </xsl:stylesheet>
            """);
        return new(XmlSource.FromFile(xml), XmlSource.FromFile(xslt));
    }

    /// <summary><para>Checks compilation and execution failures in both execution modes.</para><para>Проверяет ошибки компиляции и выполнения в обоих режимах.</para></summary>
    /// <param name="worker"><para>Whether to use a real worker.</para><para>Использовать настоящий worker.</para></param>
    /// <param name="compile"><para>Whether the error occurs at compilation.</para><para>Возникает ли ошибка при компиляции.</para></param>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task ErrorsCarrySafeCodesAndPositionsAndPreserveOutput(bool worker, bool compile)
    {
        ConversionRequest request = Request(compile
            ? "<fo:block><xsl:value-of select='missing-function()'/></fo:block>"
            : "<fo:block><xsl:value-of select='xs:integer(/document/title)'/></fo:block>");
        string output = Path.Combine(directory, "previous.pdf");
        File.WriteAllText(output, "previous");
        using var transformer = new SaxonFoTransformer();
        using var renderer = new EmbeddedFopRenderer();
        await using var isolated = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory
        });
        IXmlPdfGenerator generator = worker ? isolated : new XmlPdfGenerator(transformer, renderer);
        ConversionException exception = await Assert.ThrowsAsync<ConversionException>(() => generator.GenerateAsync(request, output));
        ConversionStage stage = compile ? ConversionStage.Compile : ConversionStage.Transform;
        Assert.Equal(stage, exception.Stage);
        Assert.Contains(exception.Diagnostics, d => !d.IsWarning && d.Stage == stage && d.Code == (compile ? "XPST0017" : "FORG0001") && d.Line > 0);
        string serialized = JsonSerializer.Serialize(exception.Diagnostics);
        Assert.DoesNotContain("SECRET_DOCUMENT_VALUE", serialized);
        Assert.DoesNotContain("sensitive-", serialized);
        Assert.DoesNotContain(directory, serialized);
        Assert.Equal("previous", File.ReadAllText(output));
    }

    /// <summary><para>Checks immutable compilation warnings on cold and cached executions and worker results.</para><para>Проверяет неизменяемые предупреждения компиляции при первом запуске, из кэша и через worker.</para></summary>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Fact]
    public async Task CompilationWarningsSurviveCacheReuseAndWorkerSuccess()
    {
        ConversionRequest request = Request("<fo:block>Report</fo:block><xsl:variable name='unused' select='/document/title'/>");
        using var transformer = new SaxonFoTransformer();
        TransformResult first = await transformer.TransformWithResultAsync(request, Path.Combine(directory, "first.fo"));
        TransformResult second = await transformer.TransformWithResultAsync(request, Path.Combine(directory, "second.fo"));
        Assert.Contains(first.Diagnostics, d => d.IsWarning && d.Stage == ConversionStage.Compile);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(1, transformer.CacheStatistics.Compilations);
        using var renderer = new EmbeddedFopRenderer();
        ConversionResult direct = await new XmlPdfGenerator(transformer, renderer).GenerateAsync(request, Path.Combine(directory, "direct.pdf"));
        await using var worker = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory
        });
        ConversionResult remote = await worker.GenerateAsync(request, Path.Combine(directory, "worker.pdf"));
        Assert.Equal(first.Diagnostics, direct.Diagnostics);
        Assert.Equal(first.Diagnostics, remote.Diagnostics);
        Assert.Equal(1, remote.PageCount);
    }

    /// <summary><para>Verifies that failed jobs do not contaminate subsequent jobs sharing the compiled stylesheet.</para><para>Проверяет, что ошибки заданий не попадают в последующие задания с общим шаблоном.</para></summary>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Fact]
    public async Task ParallelJobsKeepRuntimeDiagnosticsSeparate()
    {
        ConversionRequest baseline = Request("<fo:block><xsl:value-of select='xs:integer(/document/title)'/></fo:block>");
        using var transformer = new SaxonFoTransformer();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            string xml = Path.Combine(directory, $"input-{index}.xml");
            File.WriteAllText(xml, $"<document><title>{(index % 2 == 0 ? "42" : "SECRET_DOCUMENT_VALUE")}</title></document>");
            var request = new ConversionRequest(XmlSource.FromFile(xml), baseline.Xslt);
            string output = Path.Combine(directory, $"output-{index}.fo");
            if (index % 2 == 0)
            {
                Assert.Empty((await transformer.TransformWithResultAsync(request, output)).Diagnostics);
            }
            else
            {
                ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformWithResultAsync(request, output));
                Assert.Contains(error.Diagnostics, d => d.Code == "FORG0001" && d.Stage == ConversionStage.Transform);
            }
        }));
        Assert.Equal(1, transformer.CacheStatistics.Compilations);
    }

    /// <summary><para>Checks that user-defined error names do not become diagnostic codes.</para><para>Проверяет, что пользовательские имена ошибок не становятся кодами диагностик.</para></summary>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Fact]
    public async Task CustomErrorCodesAreRedacted()
    {
        ConversionRequest request = Request("<fo:block><xsl:sequence select=\"error(QName('urn:private', 'DATA1234'), /document/title)\"/></fo:block>");
        using var transformer = new SaxonFoTransformer();
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformWithResultAsync(request, Path.Combine(directory, "out.fo")));
        Assert.Contains(error.Diagnostics, d => d.Code == "SAXON_ERROR");
        Assert.DoesNotContain("DATA1234", JsonSerializer.Serialize(error.Diagnostics));
    }

    /// <summary><para>Bounds runtime warnings and retains the fatal error when warnings fill the collector.</para><para>Ограничивает предупреждения выполнения и сохраняет ошибку после заполнения сборщика.</para></summary>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Fact]
    public async Task RuntimeWarningLimitStillRetainsFatalError()
    {
        const string templates = "<xsl:template match='title'><fo:block>First</fo:block></xsl:template><xsl:template match='title'><fo:block>Second</fo:block></xsl:template>";
        ConversionRequest request = Request("<xsl:apply-templates select='/document/title'/>", templates);
        File.WriteAllText(request.Xml.FilePath!, "<document>" + string.Concat(Enumerable.Repeat("<title>SECRET_DOCUMENT_VALUE</title>", 80)) + "</document>");
        using var transformer = new SaxonFoTransformer();
        TransformResult result = await transformer.TransformWithResultAsync(request, Path.Combine(directory, "warnings.fo"));
        Assert.Equal(SaxonDiagnosticCollector.Limit, result.Diagnostics.Count(d => d.Stage == ConversionStage.Transform));
        Assert.All(result.Diagnostics, d => Assert.True(d.IsWarning));
        request = Request("<xsl:apply-templates select='/document/title'/><fo:block><xsl:value-of select='xs:integer(/document/title[1])'/></fo:block>", templates);
        File.WriteAllText(request.Xml.FilePath!, "<document>" + string.Concat(Enumerable.Repeat("<title>SECRET_DOCUMENT_VALUE</title>", 80)) + "</document>");
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformWithResultAsync(request, Path.Combine(directory, "error.fo")));
        Assert.Equal(SaxonDiagnosticCollector.Limit, error.Diagnostics.Count(d => d.Stage == ConversionStage.Transform));
        Assert.Contains(error.Diagnostics, d => !d.IsWarning && d.Code == "FORG0001");
    }

    /// <summary><para>Deletes the fixture directory after all workers have stopped.</para><para>Удаляет каталог теста после остановки всех worker.</para></summary>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
