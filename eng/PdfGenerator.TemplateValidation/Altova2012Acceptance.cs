using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PdfGenerator;
using Saxon.Api;

/// <summary><para>Strict documented-contract acceptance; original Altova baselines remain separate.</para><para>Строгая приёмка по контракту документации; эталоны оригинального Altova учитываются отдельно.</para></summary>
internal static class Altova2012Acceptance
{
    /// <summary><para>Runs semantic assertions and PDF checks in process and in the worker with cold and warm caches.</para><para>Проверяет семантику и PDF внутри процесса и в worker с холодным и прогретым кэшем.</para></summary>
    /// <param name="root"><para>Repository root.</para><para>Корень репозитория.</para></param>
    /// <param name="output"><para>Empty result directory.</para><para>Пустой каталог результатов.</para></param>
    /// <param name="validateBank"><para>Independent bank calculations.</para><para>Независимые банковские расчёты.</para></param>
    /// <returns><para>Nonzero if any declared expectation fails.</para><para>Ненулевой код при нарушении любого ожидания.</para></returns>
    internal static async Task<int> Run(string root, string output, Action<string, string> validateBank)
    {
        string data = Path.Combine(root, "test-data");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "altova/manifest.json")));
        using var expectations = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "altova/expected-properties.json")));
        var observations = new List<object>();
        var watch = Stopwatch.StartNew();
        int failures = 0;
        XPathCompiler selfCheckCompiler = new Processor().NewXPathCompiler();
        XdmNode damaged = selfCheckCompiler.Processor.NewDocumentBuilder().Build(new StringReader("<result input-count='7' unique-count='4'/>")).Children().Single();
        bool corruptionRejected = false;
        try
        {
            Verify(damaged, expectations.RootElement.GetProperty("01-distinct-nodes").GetProperty("assertions").EnumerateArray().Select(a => a.GetString()!).ToArray());
        }
        catch (InvalidOperationException)
        {
            corruptionRejected = true;
        }
        using (var disabled = new SaxonFoTransformer())
        {
            var missing = new ConversionRequest(XmlSource.FromFile(Path.Combine(data, "altova/01-distinct-nodes.xml")), XmlSource.FromFile(Path.Combine(data, "altova/01-distinct-nodes.xsl")));
            bool missingFunctionRejected = false;
            try
            {
                await disabled.TransformToXdmAsync(missing);
            }
            catch (ConversionException error)
            {
                missingFunctionRejected = error.Stage == ConversionStage.Compile && error.Diagnostics.Any(d => d.Code == "XPST0017");
            }
            if (!corruptionRejected || !missingFunctionRejected)
            {
                throw new InvalidOperationException("Acceptance guards did not reject corrupt output or a missing function.");
            }
        }
        Console.WriteLine("Acceptance guards passed: damaged result and missing function rejected.");
        foreach (int cache in new[] { 0, 32 })
        {
            var options = new XmlFoOptions { Altova2012 = new(), TemplateCacheEntries = cache };
            using var transformer = new SaxonFoTransformer(options);
            using var renderer = new EmbeddedFopRenderer();
            await using var worker = new WorkerXmlPdfGenerator(new()
            {
                WorkingDirectory = Path.Combine(output, "workers-" + cache),
                JobTimeout = TimeSpan.FromSeconds(60)
            }, options);
            foreach (JsonElement item in manifest.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("profile").GetString() is "legacy-general" or "legacy-negative"))
            {
                string id = item.GetProperty("id").GetString()!;
                JsonElement expected = expectations.RootElement.GetProperty(id);
                string xml = Path.Combine(data, "altova", item.GetProperty("input").GetString()!);
                string xsl = Path.Combine(data, "altova", item.GetProperty("stylesheet").GetString()!);
                var request = new ConversionRequest(XmlSource.FromFile(xml), XmlSource.FromFile(xsl), resources: new LocalResourcePolicy([data]));
                string folder = Path.Combine(output, "cache-" + cache, id.Replace('/', '-'));
                Directory.CreateDirectory(folder);
                var audit = new XsltDiagnostics(altova2012: options.Altova2012);
                XsltDiagnosticReport report = audit.AnalyzeFile(xsl, request.Resources!);
                File.WriteAllText(Path.Combine(folder, "audit.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                string[] assertions = expected.TryGetProperty("assertions", out JsonElement checks) ? checks.EnumerateArray().Select(c => c.GetString()!).ToArray() : [];
                string? errorCode = expected.TryGetProperty("errorCode", out JsonElement code) ? code.GetString() : null;
                for (int repeat = 0; repeat < (cache == 0 ? 1 : 2); repeat++)
                {
                    foreach (string mode in new[] { "in-process-xdm", "worker-assertions" })
                    {
                        bool passed = false;
                        string? observedCode = null;
                        try
                        {
                            if (report.HasErrors)
                            {
                                throw new InvalidOperationException("Declared profile contains unavailable functions or resources.");
                            }
                            if (mode == "in-process-xdm")
                            {
                                XdmValue value = await transformer.TransformToXdmAsync(request);
                                Verify(value, assertions);
                                File.WriteAllText(Path.Combine(folder, "result.xml"), value[0].ToString());
                                if (item.GetProperty("result").GetString() == "fo")
                                {
                                    ConversionResult pdf = await new XmlPdfGenerator(transformer, renderer).GenerateAsync(request, Path.Combine(folder, "in-process.pdf"));
                                    if (pdf.PageCount != 1)
                                    {
                                        throw new InvalidOperationException("Expected one invoice page.");
                                    }
                                }
                            }
                            else
                            {
                                ConversionRequest wrapped = Wrap(request, xsl, assertions);
                                ConversionResult pdf = await worker.GenerateAsync(wrapped, Path.Combine(folder, "worker.pdf"));
                                if (pdf.PageCount != 1)
                                {
                                    throw new InvalidOperationException("Expected one validation page.");
                                }
                            }
                            passed = errorCode is null;
                        }
                        catch (ConversionException error)
                        {
                            observedCode = string.Join(",", error.Diagnostics.Select(d => d.Code));
                            passed = errorCode is not null && error.Stage == ConversionStage.Transform && error.Diagnostics.Any(d => d.Code == errorCode)
                                && !error.Diagnostics.Any(d => d.Code == "XPST0017");
                        }
                        catch (Exception error)
                        {
                            Console.Error.WriteLine(id + ": " + error.GetType().Name + ": " + error.Message);
                        }
                        if (!passed)
                        {
                            failures++;
                        }
                        observations.Add(new
                        {
                            id,
                            cache,
                            repeat,
                            mode,
                            passed,
                            observedCode,
                            dependencies = Dependencies(xsl, data)
                        });
                        Console.WriteLine($"{id} cache={cache} repeat={repeat} {mode}: {(passed ? "passed" : "FAILED")} {observedCode}");
                    }
                }
            }
            foreach (string variant in new[] { "base", "month-end", "empty-services", "second-client" })
            {
                var input = XDocument.Load(Path.Combine(data, "bank-signing/input.xml"));
                if (variant == "month-end")
                {
                    input.Root!.SetAttributeValue("issued", "2026-01-31");
                }
                if (variant == "empty-services")
                {
                    input.Root!.Element("services")!.RemoveNodes();
                }
                if (variant == "second-client")
                {
                    input.Root!.Element("client")!.SetAttributeValue("name", "Примерова Мария Игоревна");
                }
                string folder = Path.Combine(output, "cache-" + cache, "bank-" + variant);
                Directory.CreateDirectory(folder);
                string xml = Path.Combine(folder, "input.xml");
                input.Save(xml);
                string xsl = Path.Combine(data, "bank-signing/bank-altova-2012.xsl");
                var request = new ConversionRequest(XmlSource.FromFile(xml), XmlSource.FromFile(xsl), resources: new LocalResourcePolicy([data]));
                foreach (string mode in new[] { "in-process", "worker" })
                {
                    bool passed = false;
                    try
                    {
                        string fo = Path.Combine(folder, "result.fo");
                        await transformer.TransformAsync(request, fo);
                        validateBank(xml, fo);
                        ConversionResult pdf = mode == "worker"
                            ? await worker.GenerateAsync(Wrap(request, xsl, BankAssertions(input), true), Path.Combine(folder, "worker.pdf"))
                            : await new XmlPdfGenerator(transformer, renderer).GenerateAsync(request, Path.Combine(folder, "in-process.pdf"));
                        passed = pdf.PageCount == 3 && !pdf.Warnings.Any(w => w.Contains("glyph", StringComparison.OrdinalIgnoreCase));
                    }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine("bank-" + variant + " " + mode + ": " + error.Message);
                    }
                    if (!passed)
                    {
                        failures++;
                    }
                    observations.Add(new
                    {
                        id = "bank-" + variant,
                        cache,
                        mode,
                        passed,
                        dependencies = Dependencies(xsl, data)
                    });
                    Console.WriteLine($"bank-{variant} cache={cache} {mode}: {(passed ? "passed" : "FAILED")}");
                }
            }
        }
        File.WriteAllText(Path.Combine(output, "acceptance.json"), JsonSerializer.Serialize(new
        {
            profile = Altova2012Options.ImplementationVersion,
            confirmation = "documentation-only; original Altova engine not captured",
            saxon = typeof(Processor).Assembly.GetName().Version?.ToString(),
            optimizer = "0 (SaxonCS-HE 13.0.0 adapter ignores focus/side-effect flags)",
            elapsedSeconds = watch.Elapsed.TotalSeconds,
            failures,
            corruptionRejected,
            observations
        }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }

    /// <summary><para>Checks independent XPath predicates on the original XDM result.</para><para>Проверяет независимые XPath-условия по исходному результату XDM.</para></summary>
    /// <param name="value"><para>Raw result.</para><para>Исходный результат.</para></param>
    /// <param name="assertions"><para>Expected properties.</para><para>Ожидаемые свойства.</para></param>
    internal static void Verify(XdmValue value, string[] assertions)
    {
        if (value.Count != 1 || value[0] is not XdmNode)
        {
            throw new InvalidOperationException("Expected one result node.");
        }
        XPathCompiler compiler = ((XdmNode)value[0]).Processor.NewXPathCompiler();
        compiler.DeclareNamespace("fo", "http://www.w3.org/1999/XSL/Format");
        foreach (string assertion in assertions)
        {
            XPathSelector selector = compiler.Compile(assertion).Load();
            selector.ContextItem = value[0];
            if (!selector.EffectiveBooleanValue())
            {
                throw new InvalidOperationException("Expected result property failed: " + assertion);
            }
        }
    }

    /// <summary><para>Checks the imported fixture result inside the real worker before making a validation PDF.</para><para>Проверяет результат исходного примера внутри настоящего worker до формирования проверочного PDF.</para></summary>
    /// <param name="request"><para>Original request.</para><para>Исходный запрос.</para></param>
    /// <param name="stylesheet"><para>Original module.</para><para>Исходный модуль.</para></param>
    /// <param name="assertions"><para>Expected properties.</para><para>Ожидаемые свойства.</para></param>
    /// <param name="emitOriginal"><para>Emit original FO after checking its semantics.</para><para>Вывести исходный FO после проверки его семантики.</para></param>
    /// <returns><para>Worker request.</para><para>Запрос worker.</para></returns>
    private static ConversionRequest Wrap(ConversionRequest request, string stylesheet, string[] assertions, bool emitOriginal = false)
    {
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        XNamespace fo = "http://www.w3.org/1999/XSL/Format";
        var wrapper = new XElement(xsl + "stylesheet", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "xsl", xsl), new XAttribute(XNamespace.Xmlns + "fo", fo),
            new XElement(xsl + "import", new XAttribute("href", new Uri(stylesheet).AbsoluteUri)),
            new XElement(xsl + "template", new XAttribute("match", "/"),
                new XElement(xsl + "variable", new XAttribute("name", "result"), new XElement(xsl + "apply-templates", new XAttribute("select", "/*"))),
                new XElement(xsl + "if", new XAttribute("test", "count($result/*) != 1"), new XElement(xsl + "sequence", new XAttribute("select", "error(QName('http://www.w3.org/2005/xqt-errors', 'FOER0000'), 'Expected one result')"))),
                new XElement(xsl + "for-each", new XAttribute("select", "$result/*"), assertions.Select(a => new XElement(xsl + "if", new XAttribute("test", "not(" + a + ")"),
                    new XElement(xsl + "sequence", new XAttribute("select", "error(QName('http://www.w3.org/2005/xqt-errors', 'FOER0000'), 'Fixture assertion failed')"))))),
                emitOriginal ? new XElement(xsl + "copy-of", new XAttribute("select", "$result/*")) : new XElement(fo + "root", new XElement(fo + "layout-master-set", new XElement(fo + "simple-page-master", new XAttribute("master-name", "a4"), new XAttribute("page-height", "297mm"), new XAttribute("page-width", "210mm"), new XElement(fo + "region-body"))),
                    new XElement(fo + "page-sequence", new XAttribute("master-reference", "a4"), new XElement(fo + "flow", new XAttribute("flow-name", "xsl-region-body"), new XElement(fo + "block", "Semantic assertions passed"))))));
        var wrapperUri = new Uri(Path.Combine(Path.GetDirectoryName(stylesheet)!, "__acceptance-wrapper.xsl"));
        return new ConversionRequest(request.Xml, XmlSource.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(wrapper.ToString())), wrapperUri), resources: request.Resources);
    }

    /// <summary><para>Builds numeric and client assertions evaluated inside the worker on the original bank FO.</para><para>Создаёт числовые проверки и проверки клиента внутри worker по исходному банковскому FO.</para></summary>
    /// <param name="source"><para>Bank input.</para><para>Банковский вход.</para></param>
    /// <returns><para>Independent expected properties.</para><para>Независимые ожидаемые свойства.</para></returns>
    private static string[] BankAssertions(XDocument source)
    {
        var assertions = new List<string> { "count(fo:page-sequence) = 3", "count(fo:page-sequence[2]//fo:table-body/fo:table-row) = 13" };
        var issued = DateTime.ParseExact(source.Root!.Attribute("issued")!.Value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        for (int index = 0; index < 12; index++)
        {
            decimal interest = 12000 - index * 1000;
            string[] row = [(index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), issued.AddMonths(index + 1).ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture), Money(100000), Money(interest), Money(100000 + interest), Money(1100000 - index * 100000)];
            assertions.Add("string-join(fo:page-sequence[2]//fo:table-body/fo:table-row[" + (index + 1) + "]/fo:table-cell/fo:block, '|') = '" + string.Join("|", row) + "'");
        }
        assertions.Add("string-join(fo:page-sequence[2]//fo:table-body/fo:table-row[13]/fo:table-cell/fo:block, '|') = 'ИТОГО|" + Money(1200000) + "|" + Money(78000) + "|" + Money(1278000) + "|" + Money(0) + "'");
        string client = source.Root.Element("client")!.Attribute("name")!.Value.Replace("'", "''");
        decimal services = source.Root.Element("services")!.Elements("service").Where(s => (string?)s.Attribute("selected") == "true").Sum(s => decimal.Parse(s.Attribute("amount")!.Value, System.Globalization.CultureInfo.InvariantCulture));
        assertions.Add("contains(string(.), '" + client + "') and contains(string(.), 'Итого отдельно выбранные услуги: " + Money(services) + "')");
        assertions.Add("count(fo:page-sequence[3]//fo:block[starts-with(.,'1. Данные клиента')]) = 1 and not(contains(string(.), 'Не выбранная услуга'))");
        return [.. assertions];
    }

    /// <summary><para>Formats the independent decimal expectations.</para><para>Форматирует независимые десятичные ожидания.</para></summary>
    /// <param name="value"><para>Amount.</para><para>Сумма.</para></param>
    /// <returns><para>Expected Russian amount text.</para><para>Ожидаемый русский текст суммы.</para></returns>
    private static string Money(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")).Replace('\u00a0', ' ').Replace('\u202f', ' ');

    /// <summary><para>Records hashes of the main module and all permitted transitive dependencies.</para><para>Записывает хэши главного модуля и всех разрешённых зависимостей.</para></summary>
    /// <param name="path"><para>Main stylesheet.</para><para>Главный шаблон.</para></param>
    /// <param name="data"><para>Allowed fixture root.</para><para>Разрешённый корень примеров.</para></param>
    /// <returns><para>Dependency fingerprint map.</para><para>Карта хэшей зависимостей.</para></returns>
    private static Dictionary<string, string> Dependencies(string path, string data)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        var policy = new LocalResourcePolicy([data]);
        pending.Push(path);
        while (pending.TryPop(out string? module))
        {
            string file = policy.Resolve(new Uri(module));
            if (hashes.ContainsKey(file))
            {
                continue;
            }
            hashes.Add(file, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            foreach (XElement import in XDocument.Load(file).Descendants().Where(e => e.Name.NamespaceName == "http://www.w3.org/1999/XSL/Transform" && e.Name.LocalName is "include" or "import"))
            {
                pending.Push(new Uri(new Uri(file), import.Attribute("href")!.Value).LocalPath);
            }
        }
        return hashes;
    }
}
