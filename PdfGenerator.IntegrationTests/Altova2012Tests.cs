using System.Text;
using System.Xml.Linq;
using Saxon.Api;
using Xunit;

namespace PdfGenerator.IntegrationTests;

/// <summary><para>Exercises the documented general profile and its isolation boundaries.</para><para>Проверяет общий профиль по документации и границы изоляции.</para></summary>
public sealed class Altova2012Tests
{
    /// <summary><para>Creates an inline request preserving the source and stylesheet base URIs.</para><para>Создаёт запрос с сохранением базовых URI входа и шаблона.</para></summary>
    /// <param name="body"><para>Template body.</para><para>Тело шаблона.</para></param>
    /// <param name="xml"><para>Input document.</para><para>Входной документ.</para></param>
    /// <param name="attributes"><para>Extra stylesheet attributes.</para><para>Дополнительные атрибуты шаблона.</para></param>
    /// <returns><para>Conversion request.</para><para>Запрос преобразования.</para></returns>
    private static ConversionRequest Request(string body, string xml = "<root/>", string attributes = "") => new(
        XmlSource.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(xml))),
        XmlSource.FromStream(new MemoryStream(Encoding.UTF8.GetBytes($"""
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
            {attributes}><xsl:template match="/">{body}</xsl:template></xsl:stylesheet>
            """)), new Uri("file:///altova-fixture/template.xsl")), resources: new LocalResourcePolicy([]));

    /// <summary><para>Runs all general corpus fixtures with the same application execution path.</para><para>Запускает общие примеры через путь выполнения приложения.</para></summary>
    /// <param name="id"><para>Fixture identifier.</para><para>Идентификатор примера.</para></param>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Theory]
    [InlineData("01-distinct-nodes")]
    [InlineData("02-evaluate")]
    [InlineData("03-encode-for-rtf")]
    [InlineData("04-auto-number")]
    [InlineData("05-temp-folder")]
    [InlineData("06-invoice-fo")]
    public async Task GeneralCorpus(string id)
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
        });
        string data = Path.Combine(AppContext.BaseDirectory, "test-data");
        var request = new ConversionRequest(XmlSource.FromFile(Path.Combine(data, "altova", id + ".xml")),
            XmlSource.FromFile(Path.Combine(data, "altova", id + ".xsl")), resources: new LocalResourcePolicy([data]));
        XdmValue value = await transformer.TransformToXdmAsync(request);
        var result = XDocument.Parse(value[0].ToString());
        if (id == "01-distinct-nodes")
        {
            Assert.Equal("5", result.Root!.Attribute("unique-count")!.Value);
            Assert.Equal(["1", "3", "4", "5", "6"], result.Descendants("node").Select(n => n.Attribute("source-positions")!.Value));
        }
        if (id == "02-evaluate")
        {
            Assert.Equal("37.5", result.Root!.Element("total")!.Value);
            Assert.Equal("2|4|текст|7", result.Root.Element("parameters")!.Value);
            Assert.Equal(["A: Бумага", "B: Папка", "C: Образец"], result.Descendants("label").Select(n => n.Value));
            Assert.Equal("A", result.Root.Element("node-parameter")!.Value);
        }
        if (id == "04-auto-number")
        {
            Assert.Equal(["10", "15", "100", "10", "98", "", "10", "15", "10", "0", "0"], result.Descendants("operation").Select(n => n.Value));
        }
        if (id == "05-temp-folder")
        {
            string folder = result.Root!.Element("path")!.Value;
            Assert.True(Path.IsPathFullyQualified(folder));
            Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), folder);
            Assert.False(Directory.Exists(folder));
            Assert.Equal("true", result.Root.Attribute("stable")!.Value);
        }
        if (id == "06-invoice-fo")
        {
            Assert.Contains("Итого: 200.00", result.Root!.Value);
            Assert.Equal(1, result.Descendants().Count(n => n.Name.LocalName == "block" && n.Value == "Доставка включена"));
        }
    }

    /// <summary><para>Checks dynamic errors inside a supported evaluate, rather than missing-function errors.</para><para>Проверяет ошибки внутри поддержанного evaluate вместо отсутствия функции.</para></summary>
    /// <param name="id"><para>Negative fixture.</para><para>Отрицательный пример.</para></param>
    /// <param name="code"><para>Expected XPath code.</para><para>Ожидаемый код XPath.</para></param>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Theory]
    [InlineData("14-evaluate-syntax", "XPST0003")]
    [InlineData("15-evaluate-missing-parameter", "XPST0008")]
    [InlineData("16-evaluate-unknown-prefix", "XPST0081")]
    public async Task NegativeEvaluate(string id, string code)
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
        });
        string data = Path.Combine(AppContext.BaseDirectory, "test-data/altova/negative");
        var request = new ConversionRequest(XmlSource.FromFile(Path.Combine(data, id + ".xml")), XmlSource.FromFile(Path.Combine(data, id + ".xsl")));
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformToXdmAsync(request));
        Assert.Equal(ConversionStage.Transform, error.Stage);
        Assert.Contains(error.Diagnostics, d => d.Code == code);
    }

    /// <summary><para>Verifies position, size, default namespace, base URI, typed parameters and node identity before serialization.</para><para>Проверяет позицию, размер, пространство имён, URI, типы и идентичность узлов до сериализации.</para></summary>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Fact]
    public async Task EvaluateInheritsFocusAndStaticValues()
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
        });
        XdmValue value = await transformer.TransformToXdmAsync(Request("""
            <xsl:for-each select="root/item"><xsl:sequence select="a:evaluate('position(), last()')"/></xsl:for-each>
            <xsl:sequence select="a:evaluate('count(root/item)')"/>
            <xsl:sequence select="a:evaluate('static-base-uri()')"/>
            <xsl:sequence select="a:evaluate('$p1', root/item[1]) is root/item[1]"/>
            <xsl:sequence select="a:evaluate('$p1 instance of xs:decimal', xs:decimal('1.2'))"/>
            <xsl:sequence select="a:evaluate('$p1', ())"/>
            """, "<root xmlns='urn:test'><item/><item/><item/></root>", "xpath-default-namespace='urn:test'"));
        Assert.Equal(["1", "3", "2", "3", "3", "3", "3", "file:///altova-fixture/template.xsl", "true", "true"], value.Select(v => v.StringValue));
    }

    /// <summary><para>One warm executable runs concurrently without sharing counters or dynamic parameters.</para><para>Один прогретый шаблон выполняется параллельно без общих счётчиков и параметров.</para></summary>
    /// <param name="cache"><para>Template retention size.</para><para>Размер кэша шаблонов.</para></param>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public async Task ConcurrentExecutionsAreIsolated(int cache)
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            MaxConcurrency = 4,
            TemplateCacheEntries = cache,
            Altova2012 = new()
        });
        Task<XdmValue>[] tasks = Enumerable.Range(1, 12).Select(i => transformer.TransformToXdmAsync(Request("""
            <xsl:for-each select="1 to 3"><xsl:sequence select="a:generate-auto-number('s',1,1,'A')"/></xsl:for-each>
            <xsl:sequence select="a:evaluate('xs:integer($p1)', root/@id)"/>
            """, $"<root id='{i}'/>"))).ToArray();
        XdmValue[] values = await Task.WhenAll(tasks);
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(["1", "2", "3", (i + 1).ToString()], values[i].Select(v => v.StringValue));
        }
        Assert.True(cache == 0 || transformer.CacheStatistics.Hits > 0);
    }

    /// <summary><para>Disabled compatibility and arbitrary .NET calls remain unavailable.</para><para>Отключённая совместимость и произвольные вызовы .NET остаются недоступными.</para></summary>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Fact]
    public async Task ExtensionsAreOptInAndDoNotEnableReflection()
    {
        using var disabled = new SaxonFoTransformer();
        ConversionException first = await Assert.ThrowsAsync<ConversionException>(() => disabled.TransformToXdmAsync(Request("<xsl:sequence select=\"a:evaluate('1')\"/>")));
        Assert.Equal(ConversionStage.Compile, first.Stage);
        Assert.Contains(first.Diagnostics, d => d.Code == "XPST0017");
        using var enabled = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
        });
        ConversionException second = await Assert.ThrowsAsync<ConversionException>(() => enabled.TransformToXdmAsync(Request("<xsl:sequence select=\"e:GetEnvironmentVariables()\"/>", attributes: "xmlns:e='clitype:System.Environment'")));
        Assert.Equal(ConversionStage.Compile, second.Stage);
    }

    /// <summary><para>Dynamic resource access is blocked, including function lookup and text reads.</para><para>Динамический доступ к ресурсам запрещён, включая поиск функций и чтение текста.</para></summary>
    /// <param name="expression"><para>Resource expression.</para><para>Ресурсное выражение.</para></param>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Theory]
    [InlineData("doc('file:///missing-secret.xml')")]
    [InlineData("unparsed-text('file:///missing-secret.txt')")]
    [InlineData("collection('file:///missing-secret')")]
    public async Task DynamicResourcesAreBlocked(string expression)
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
        });
        string escaped = expression.Replace("'", "''");
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformToXdmAsync(Request($"<xsl:sequence select=\"a:evaluate('{escaped}')\"/>")));
        Assert.Equal(ConversionStage.Transform, error.Stage);
        Assert.DoesNotContain("missing-secret", System.Text.Json.JsonSerializer.Serialize(error.Diagnostics));
    }

    /// <summary><para>Evaluation budgets fail safely and a following job starts with fresh state.</para><para>Бюджет вычисления завершается безопасно, следующее задание получает новое состояние.</para></summary>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Fact]
    public async Task BudgetsAndRecovery()
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
            {
                MaxEvaluations = 2,
                MaxCachedExpressions = 2
            }
        });
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformToXdmAsync(Request("<xsl:for-each select='1 to 3'><xsl:sequence select=\"a:evaluate('1')\"/></xsl:for-each>")));
        Assert.Equal(ConversionStage.Transform, error.Stage);
        Assert.Equal("1", (await transformer.TransformToXdmAsync(Request("<xsl:sequence select=\"a:evaluate('1')\"/>")))[0].StringValue);
    }

    /// <summary><para>Confirms blocked reads using files that actually exist and could otherwise be read.</para><para>Проверяет запрет чтения на существующих файлах, доступных без ограничений.</para></summary>
    /// <param name="function"><para>Resource function.</para><para>Ресурсная функция.</para></param>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Theory]
    [InlineData("doc")]
    [InlineData("unparsed-text")]
    public async Task ExistingResourcesCannotBeRead(string function)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "<secret>private</secret>");
            using var transformer = new SaxonFoTransformer(new()
            {
                Altova2012 = new()
            });
            string expression = function + "(''" + new Uri(path).AbsoluteUri + "'')";
            ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformToXdmAsync(Request($"<xsl:sequence select=\"a:evaluate('{expression}')\"/>")));
            Assert.Equal(ConversionStage.Transform, error.Stage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary><para>Limits recursive and diverse expressions as well as individual length.</para><para>Ограничивает рекурсивные и различные выражения, а также длину одного выражения.</para></summary>
    /// <param name="budget"><para>Budget under test.</para><para>Проверяемый бюджет.</para></param>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Theory]
    [InlineData("depth")]
    [InlineData("cache")]
    [InlineData("length")]
    [InlineData("total")]
    public async Task DynamicExpressionBudgets(string budget)
    {
        Altova2012Options limits = budget switch
        {
            "depth" => new() { MaxEvaluationDepth = 2 },
            "cache" => new() { MaxCachedExpressions = 2 },
            "length" => new() { MaxExpressionCharacters = 1 },
            _ => new() { MaxTotalExpressionCharacters = 2 }
        };
        string body = budget == "depth"
            ? "<xsl:sequence select=\"a:evaluate('a:evaluate($p1, $p1)', 'a:evaluate($p1, $p1)')\"/>"
            : budget == "length" ? "<xsl:sequence select=\"a:evaluate('1+1')\"/>"
            : "<xsl:for-each select='1 to 3'><xsl:sequence select='a:evaluate(string(.))'/></xsl:for-each>";
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = limits
        });
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformToXdmAsync(Request(body)));
        Assert.Equal(ConversionStage.Transform, error.Stage);
        Assert.Contains(error.Diagnostics, d => d.Code == "FOER0000");
    }

    /// <summary><para>Distinct nodes retain identity and ignore attribute order while preserving meaningful whitespace.</para><para>Уникальные узлы сохраняют идентичность, не учитывают порядок атрибутов и сохраняют значимые пробелы.</para></summary>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Fact]
    public async Task DistinctPreservesIdentityAndStructure()
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            Altova2012 = new()
        });
        XdmValue result = await transformer.TransformToXdmAsync(Request("""
            <xsl:sequence select="count(a:distinct-nodes(root/item))"/>
            <xsl:sequence select="a:distinct-nodes(root/item)[1] is root/item[1]"/>
            <xsl:sequence select="a:distinct-nodes(root/item)[2] is root/item[3]"/>
            """, "<root><item a='1' b='2'><name>X</name></item><item b='2' a='1'><name>X</name></item><item a='1' b='2'><name> X </name></item></root>"));
        Assert.Equal(["2", "true", "true"], result.Select(i => i.StringValue));
    }

    /// <summary><para>The availability catalog distinguishes argument counts and dynamic-expression limitations.</para><para>Каталог доступности различает арности и ограничения динамических выражений.</para></summary>
    [Fact]
    public void DiagnosticsUseTheActualProfileAndArity()
    {
        const string text = """
            <xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:ext='http://www.altova.com/xslt-extensions'>
            <xsl:template match='/'><xsl:value-of select="ext:evaluate('1'), ext:evaluate('sum($p1)',(1,2)), ext:distinct-nodes(), ext:get-temp-folder()"/></xsl:template></xsl:stylesheet>
            """;
        XsltDiagnosticReport report = new XsltDiagnostics(altova2012: new()).Analyze(text);
        Assert.Contains(report.Functions, f => f.Name == "evaluate" && f.ArgumentCount == 1 && f.Support == XsltFunctionSupport.Supported);
        Assert.Contains(report.Functions, f => f.Name == "evaluate" && f.ArgumentCount == 2 && f.Support == XsltFunctionSupport.Supported);
        Assert.Contains(report.Functions, f => f.Name == "distinct-nodes" && f.ArgumentCount == 0 && f.Support == XsltFunctionSupport.Unsupported);
        Assert.Contains(report.Diagnostics, d => d.Code == "XSLT_DYNAMIC_EVALUATION");
        XsltDiagnosticReport missing = new XsltDiagnostics(altova2012: new()).AnalyzeFile(Path.Combine(Path.GetTempPath(), "missing-altova-" + Guid.NewGuid().ToString("N"), "missing.xsl"));
        Assert.True(missing.HasErrors);
        Assert.Contains(missing.Diagnostics, d => d.Code == "XSLT_READ");
    }

    /// <summary><para>Proves worker profile transmission, semantic assertions, process reuse and recovery after termination.</para><para>Проверяет передачу профиля worker, семантику, повторное использование и восстановление процесса.</para></summary>
    /// <returns><para>Validation task.</para><para>Задача проверки.</para></returns>
    [Fact]
    public async Task WorkerUsesProfileAndRecoversWithoutCounterLeakage()
    {
        await using var worker = new WorkerXmlPdfGenerator(xmlOptions: new()
        {
            Altova2012 = new()
        });
        const string body = """
            <xsl:variable name="numbers" as="xs:integer*"><xsl:for-each select="1 to 3"><xsl:sequence select="a:generate-auto-number('s',1,1,'A')"/></xsl:for-each></xsl:variable>
            <xsl:if test="string-join(for $n in $numbers return string($n),',') != '1,2,3' or a:evaluate('1+1') != 2">
            <xsl:sequence select="error(QName('http://www.w3.org/2005/xqt-errors','FOER0000'))"/></xsl:if>
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
            <fo:layout-master-set><fo:simple-page-master master-name="a4" page-width="210mm" page-height="297mm"><fo:region-body/></fo:simple-page-master></fo:layout-master-set>
            <fo:page-sequence master-reference="a4"><fo:flow flow-name="xsl-region-body"><fo:block>Verified</fo:block></fo:flow></fo:page-sequence></fo:root>
            """;
        for (int i = 0; i < 3; i++)
        {
            using var output = new MemoryStream();
            Assert.Equal(1, (await worker.GenerateAsync(Request(body), output)).PageCount);
            if (i == 1)
            {
                using var process = System.Diagnostics.Process.GetProcessById(Assert.Single(worker.ProcessIds));
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
        Assert.Equal(2, worker.Statistics.Starts);
        Assert.Equal(3, worker.Statistics.CompletedJobs);
    }
}
