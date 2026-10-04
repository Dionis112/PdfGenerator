using System.Text;
using System.Xml.Linq;
using PdfGenerator.TestData;
using Xunit;

namespace PdfGenerator.IntegrationTests;

/// <summary><para>Verifies rejection, cleanup and publication under resource and protocol failures.</para><para>Проверяет отказ, очистку и публикацию при ошибках ресурсов и протокола.</para></summary>
public sealed class ResourceLimitTests : IDisposable
{
    /// <summary><para>Private test directory.</para><para>Собственный каталог теста.</para></summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pdf-limits-" + Guid.NewGuid().ToString("N"));
    /// <summary><para>Creates the private test directory.</para><para>Создаёт собственный каталог теста.</para></summary>
    public ResourceLimitTests() => Directory.CreateDirectory(directory);
    /// <summary><para>Returns a bundled basic fixture path.</para><para>Возвращает путь встроенного базового примера.</para></summary>
    /// <param name="name"><para>Fixture name.</para><para>Имя примера.</para></param>
    /// <returns><para>Absolute fixture path.</para><para>Абсолютный путь примера.</para></returns>
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "test-data/basic", name);

    /// <summary><para>Rejects PDF and image preparation over their limits without replacing a previous PDF.</para><para>Отклоняет превышение лимитов PDF и подготовки изображений, сохраняя предыдущий PDF.</para></summary>
    /// <param name="kind"><para>Quota under test.</para><para>Проверяемая квота.</para></param>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Theory]
    [InlineData("pdf")]
    [InlineData("images")]
    [InlineData("disk")]
    public async Task LimitsPreservePreviousOutputAndCleanTemporaryFiles(string kind)
    {
        string fo = Path.Combine(directory, "input.fo");
        string first = Convert.ToBase64String(PngFixture.Create());
        string second = Convert.ToBase64String(PngFixture.Create(false));
        string content = XDocument.Load(Fixture("expected.fo")).Root!.ToString();
        content = content.Replace("</fo:flow>", $"<fo:block><fo:external-graphic src='data:image/png;base64,{first}'/><fo:external-graphic src='data:image/png;base64,{second}'/></fo:block></fo:flow>");
        File.WriteAllText(fo, content);
        string target = Path.Combine(directory, "result.pdf");
        File.WriteAllText(target, "previous");
        using var renderer = new EmbeddedFopRenderer(new()
        {
            MaxPdfBytes = kind == "pdf" ? 10 : 65536,
            MaxImages = kind == "images" ? 1 : 10,
            MaxTemporaryBytes = kind == "disk" ? 128 : 1024 * 1024
        });
        await Assert.ThrowsAsync<ConversionException>(() => renderer.RenderFileAsync(fo, target));
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Empty(Directory.EnumerateDirectories(directory));
        Assert.Equal(2, Directory.EnumerateFiles(directory).Count());
    }

    /// <summary><para>Terminates a worker exceeding an observed memory threshold and preserves its prior output.</para><para>Завершает worker при превышении наблюдаемого порога памяти и сохраняет предыдущий результат.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task WorkerMemoryTerminationIsDistinctAndCleansJob()
    {
        await using var worker = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory,
            MaxWorkingSetBytes = 1
        });
        string target = Path.Combine(directory, "result.pdf");
        File.WriteAllText(target, "previous");
        PdfWorkerException error = await Assert.ThrowsAsync<PdfWorkerException>(() => worker.GenerateAsync(new(XmlSource.FromFile(Fixture("input.xml")), XmlSource.FromFile(Fixture("document.xsl"))), target));
        Assert.Equal(WorkerFailureKind.MemoryLimit, error.Kind);
        Assert.Equal(1, worker.Statistics.MemoryTerminations);
        Assert.Empty(worker.ProcessIds);
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Empty(Directory.EnumerateDirectories(directory, "job-*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateDirectories(directory, "worker-runtime-*", SearchOption.AllDirectories));
    }

    /// <summary><para>Stops repeated startup attempts and rejects incompatible or oversized metadata.</para><para>Останавливает повторные попытки запуска и отклоняет несовместимые или слишком большие метаданные.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task StartupCircuitAndProtocolBoundsRejectInvalidPeers()
    {
        await using var worker = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory,
            DotnetHostPath = Path.Combine(directory, "missing-host"),
            MaxStartupFailures = 1
        });
        var request = new ConversionRequest(XmlSource.FromFile(Fixture("input.xml")), XmlSource.FromFile(Fixture("document.xsl")));
        Assert.Equal(WorkerFailureKind.Startup, (await Assert.ThrowsAsync<PdfWorkerException>(() => worker.GenerateAsync(request, new MemoryStream()))).Kind);
        Assert.Equal(WorkerFailureKind.Startup, (await Assert.ThrowsAsync<PdfWorkerException>(() => worker.GenerateAsync(request, new MemoryStream()))).Kind);
        await Assert.ThrowsAsync<InvalidDataException>(() => WorkerProtocol.ReadAsync(new StringReader("{\"Kind\":\"ready\",\"Version\":999}\n"), default));
        await Assert.ThrowsAsync<InvalidDataException>(() => WorkerProtocol.ReadAsync(new StringReader(new string('x', WorkerProtocol.MaxMessageCharacters + 1)), default));
        Assert.Empty(Directory.EnumerateFiles(directory, "worker-*.json", SearchOption.AllDirectories));
    }

    /// <summary><para>Recycles a warmed worker and renders custom SVG fonts using the immutable profile.</para><para>Перезапускает прогретый worker и рендерит пользовательские шрифты SVG с неизменяемым профилем.</para></summary>
    /// <returns><para>Asynchronous test completion.</para><para>Асинхронное завершение теста.</para></returns>
    [Fact]
    public async Task WorkerRecyclingAndCustomSvgFontsUseProfileSnapshot()
    {
        string font = Path.Combine(directory, "font.ttf");
        using (Stream resource = typeof(EmbeddedFopRenderer).Assembly.GetManifestResourceStream("PdfGenerator.Resources.Fonts.DejaVuSans.ttf")!)
        using (FileStream file = File.Create(font))
        {
            resource.CopyTo(file);
        }
        using var profile = PdfProcessingProfile.Create("svg-profile", [new(font, "Report Sans")]);
        File.Delete(font);
        await using var worker = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory,
            RecycleAfterJobs = 1
        }, profile.XmlOptions, profile.RendererOptions);
        string fo = XDocument.Load(Fixture("expected.fo")).Root!.ToString().Replace("</fo:flow>", "<fo:block><fo:instream-foreign-object><svg xmlns='http://www.w3.org/2000/svg' width='120' height='30'><text x='2' y='20' font-family='Report Sans'>Текст SVG</text></svg></fo:instream-foreign-object></fo:block></fo:flow>");
        string xslt = Path.Combine(directory, "svg.xsl");
        File.WriteAllText(xslt, "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:template match='/'>" + fo + "</xsl:template></xsl:stylesheet>", Encoding.UTF8);
        for (int i = 0; i < 2; i++)
        {
            ConversionResult result = await worker.GenerateAsync(new(XmlSource.FromFile(Fixture("input.xml")), XmlSource.FromFile(xslt)), Path.Combine(directory, $"{i}.pdf"));
            Assert.Equal(1, result.PageCount);
            Assert.Empty(worker.ProcessIds);
        }
        Assert.Equal(2, worker.Statistics.Recycles);
        Assert.Equal(2, worker.Statistics.Starts);
    }
    /// <summary><para>Deletes test-owned files after all workers have exited.</para><para>Удаляет файлы теста после завершения всех worker.</para></summary>
    public void Dispose() => Directory.Delete(directory, true);
}
