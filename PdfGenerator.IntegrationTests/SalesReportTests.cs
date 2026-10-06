using System.Text;
using System.Xml.Linq;
using Xunit;

namespace PdfGenerator.IntegrationTests;

/// <summary><para>Validates the adapted sales-report corpus through Saxon and FOP.</para><para>Проверяет адаптированный корпус отчётов через Saxon и FOP.</para></summary>
public sealed class SalesReportTests : IDisposable
{
    /// <summary><para>Isolated output directory.</para><para>Изолированный каталог результатов.</para></summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pdf-sales-" + Guid.NewGuid().ToString("N"));

    /// <summary><para>XSL-FO namespace.</para><para>Пространство имён XSL-FO.</para></summary>
    private static readonly XNamespace Fo = "http://www.w3.org/1999/XSL/Format";

    /// <summary><para>Creates the output directory.</para><para>Создаёт каталог результатов.</para></summary>
    public SalesReportTests() => Directory.CreateDirectory(directory);

    /// <summary><para>Locates a copied corpus fixture.</para><para>Возвращает путь скопированного тестового файла.</para></summary>
    /// <param name="name"><para>Fixture name.</para><para>Имя файла корпуса.</para></param>
    /// <returns><para>Absolute fixture path.</para><para>Абсолютный путь файла корпуса.</para></returns>
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "test-data/sales", name);

    /// <summary><para>Checks every row, line total and grand total in both XSLT variants.</para><para>Проверяет строки, суммы позиций и общий итог в обеих версиях XSLT.</para></summary>
    /// <param name="stylesheet"><para>Stylesheet fixture.</para><para>Тестовый шаблон.</para></param>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Theory]
    [InlineData("sales-report-1.xsl")]
    [InlineData("sales-report-2.xsl")]
    public async Task TableMatchesAllRowsAndTotals(string stylesheet)
    {
        using var transformer = new SaxonFoTransformer();
        string path = Path.Combine(directory, "result.fo");
        await transformer.TransformFileAsync(Fixture("input.xml"), Fixture(stylesheet), path);
        var document = XDocument.Load(path);
        Assert.Equal(Fo + "root", document.Root!.Name);
        string[][] rows = document.Descendants(Fo + "table-body").Elements(Fo + "table-row")
            .Select(row => row.Elements(Fo + "table-cell").Select(cell => cell.Value.Trim()).ToArray()).ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(new[] { "1", "Ноутбук Pro 15", "120", "100000", "12000000" }, rows[0]);
        Assert.Equal(new[] { "2", "Смартфон X200", "350", "25000", "8750000" }, rows[1]);
        Assert.Equal(new[] { "3", "Планшет Tab Mini", "80", "30000", "2400000" }, rows[2]);
        Assert.Equal(new[] { "4", "Наушники AirSound", "500", "7000", "3500000" }, rows[3]);
        string[] footer = document.Descendants(Fo + "table-footer").Descendants(Fo + "table-cell").Select(cell => cell.Value.Trim()).ToArray();
        Assert.Equal(new[] { "Итого:", "26650000" }, footer);
        Assert.Contains("Отчёт по продажам за 2026 год", document.Root.Value);
        Assert.Contains("Квартальный срез: Q1 2026", document.Root.Value);
        Assert.Contains("Комментарии по позициям:", document.Root.Value);
        Assert.Single(document.Descendants(Fo + "page-number"));
    }

    /// <summary><para>Renders each sales stylesheet in-process and in a real worker.</para><para>Рендерит каждый шаблон отчёта внутри процесса и в настоящем worker.</para></summary>
    /// <param name="stylesheet"><para>Stylesheet fixture.</para><para>Тестовый шаблон.</para></param>
    /// <param name="worker"><para>Whether to render in a worker.</para><para>Выполнять ли рендеринг в worker.</para></param>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Theory]
    [InlineData("sales-report-1.xsl", false)]
    [InlineData("sales-report-2.xsl", false)]
    [InlineData("sales-report-1.xsl", true)]
    [InlineData("sales-report-2.xsl", true)]
    public async Task CompleteReportsProducePdfInBothModes(string stylesheet, bool worker)
    {
        using var transformer = new SaxonFoTransformer();
        using var renderer = new EmbeddedFopRenderer();
        await using var isolated = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory
        });
        IXmlPdfGenerator generator = worker ? isolated : new XmlPdfGenerator(transformer, renderer);
        string pdf = Path.Combine(directory, "report.pdf");
        ConversionResult result = await generator.GenerateAsync(new(XmlSource.FromFile(Fixture("input.xml")), XmlSource.FromFile(Fixture(stylesheet))), pdf);
        Assert.Equal(1, result.PageCount);
        byte[] bytes = File.ReadAllBytes(pdf);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.Equal(bytes.LongLength, result.PdfBytes);
        Assert.DoesNotContain(result.Diagnostics, d => !d.IsWarning);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("glyph", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary><para>Checks the empty-report placeholder, zero total and successful rendering.</para><para>Проверяет строку пустого отчёта, нулевой итог и успешный рендеринг.</para></summary>
    /// <param name="stylesheet"><para>Stylesheet fixture.</para><para>Тестовый шаблон.</para></param>
    /// <returns><para>Asynchronous test.</para><para>Асинхронная проверка.</para></returns>
    [Theory]
    [InlineData("sales-report-1.xsl")]
    [InlineData("sales-report-2.xsl")]
    public async Task EmptyReportRendersPlaceholderAndZeroTotal(string stylesheet)
    {
        string xml = Path.Combine(directory, "empty.xml");
        File.WriteAllText(xml, "<document><title>Пустой отчёт</title><items/></document>");
        using var transformer = new SaxonFoTransformer();
        string fo = Path.Combine(directory, "empty.fo");
        await transformer.TransformFileAsync(xml, Fixture(stylesheet), fo);
        var document = XDocument.Load(fo);
        Assert.Contains("Нет позиций", Assert.Single(document.Descendants(Fo + "table-body").Elements(Fo + "table-row")).Value);
        Assert.Equal("0", document.Descendants(Fo + "table-footer").Descendants(Fo + "table-cell").Last().Value.Trim());
        using var renderer = new EmbeddedFopRenderer();
        Assert.Equal(1, (await renderer.RenderWithResultAsync(fo, Path.Combine(directory, "empty.pdf"))).PageCount);
    }

    /// <summary><para>Deletes the output directory after worker disposal.</para><para>Удаляет каталог результатов после освобождения worker.</para></summary>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
