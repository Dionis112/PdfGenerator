using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PdfGenerator;

string root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".");
string output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? Path.Combine(root, "artifacts/template-validation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)));
if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
{
    throw new ArgumentException("Use a new, empty output directory for a fresh run.");
}
Directory.CreateDirectory(output);
if (args.Contains("--strict-altova2012"))
{
    return await Altova2012Acceptance.Run(root, output, ValidateBank);
}
string data = Path.Combine(root, "test-data");
var cases = new List<TemplateCase>();
using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "altova/manifest.json"))))
{
    foreach (JsonElement item in manifest.RootElement.GetProperty("cases").EnumerateArray())
    {
        string id = item.GetProperty("id").GetString()!;
        cases.Add(new("altova/" + id, "altova/" + item.GetProperty("input").GetString(), "altova/" + item.GetProperty("stylesheet").GetString(),
            item.GetProperty("result").GetString()!, id.StartsWith("negative/", StringComparison.Ordinal)));
    }
}
cases.AddRange([
    new("basic", "basic/input.xml", "basic/document.xsl", "fo"),
    new("sales-1", "sales/input.xml", "sales/sales-report-1.xsl", "fo"),
    new("sales-2", "sales/input.xml", "sales/sales-report-2.xsl", "fo"),
    new("audit", "basic/input.xml", "audit/aliased-functions.xsl", "xml"),
    new("bank-standard", "bank-signing/input.xml", "bank-signing/bank-standard.xsl", "fo"),
    new("bank-altova", "bank-signing/input.xml", "bank-signing/bank-altova.xsl", "fo"),
    new("bank-altova-2012", "bank-signing/input.xml", "bank-signing/bank-altova-2012.xsl", "fo"),
    new("layout-module", "basic/input.xml", "basic/parts/layout.xsl", "module")
]);
string[] inventory = [.. Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Where(p => Path.GetExtension(p) is ".xsl" or ".xslt")
    .Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')).Order(StringComparer.Ordinal)];
string[] mapped = [.. cases.Select(c => c.Stylesheet).Distinct().Order(StringComparer.Ordinal)];
if (!inventory.SequenceEqual(mapped))
{
    throw new InvalidOperationException("Unmapped or missing stylesheets: " + string.Join(", ", inventory.Except(mapped).Concat(mapped.Except(inventory))));
}
foreach (string variant in new[] { "month-end", "empty-services", "second-client" })
{
    var document = XDocument.Load(Path.Combine(data, "bank-signing/input.xml"));
    if (variant == "month-end")
    {
        document.Root!.SetAttributeValue("issued", "2026-01-31");
    }
    if (variant == "empty-services")
    {
        document.Root!.Element("services")!.RemoveNodes();
    }
    if (variant == "second-client")
    {
        document.Root!.SetAttributeValue("id", "DEMO-BNK-2026-002");
        document.Root.Element("client")!.SetAttributeValue("name", "Примерова Мария Игоревна");
    }
    string input = Path.Combine(output, variant + ".xml");
    document.Save(input);
    cases.Add(new("bank-" + variant, input, "bank-signing/bank-standard.xsl", "fo"));
}
var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
var rows = new List<CaseResult>();
var audit = new XsltDiagnostics();
await using var worker = new WorkerXmlPdfGenerator(new() { WorkingDirectory = Path.Combine(output, "worker"), JobTimeout = TimeSpan.FromSeconds(30) }, new XmlFoOptions { TemplateCacheEntries = 0 });
foreach (TemplateCase item in cases)
{
    string xml = Path.IsPathRooted(item.Input) ? item.Input : Path.Combine(data, item.Input);
    string xsl = Path.Combine(data, item.Stylesheet);
    string folder = Path.Combine(output, item.Id.Replace('/', '-'));
    Directory.CreateDirectory(folder);
    XsltDiagnosticReport analysis = audit.AnalyzeFile(xsl);
    File.WriteAllText(Path.Combine(folder, "audit.json"), JsonSerializer.Serialize(analysis, json));
    var parameters = new List<XsltParameter>();
    if (item.Id.EndsWith("19-save-image", StringComparison.Ordinal))
    {
        string destination = Path.Combine(folder, "chart-output");
        Directory.CreateDirectory(destination);
        parameters.Add(new("output-directory", Path.GetFullPath(destination) + Path.DirectorySeparatorChar));
    }
    var request = new ConversionRequest(XmlSource.FromFile(xml), XmlSource.FromFile(xsl), parameters, item.Id, new LocalResourcePolicy([data]));
    string[] unsupported = analysis.Functions.Where(f => f.Support == XsltFunctionSupport.Unsupported).Select(f => "{" + f.NamespaceUri + "}" + f.Name).ToArray();
    var modes = new List<ModeResult>();
    foreach (string mode in item.DeclaredResult == "module" ? new[] { "compile-only" } : ["in-process", "worker"])
    {
        string pdf = Path.Combine(folder, mode + ".pdf");
        const string previous = "PREVIOUS_OUTPUT_SENTINEL";
        if (mode != "compile-only")
        {
            File.WriteAllText(pdf, previous);
        }
        try
        {
            if (mode == "compile-only")
            {
                using var transformer = new SaxonFoTransformer(new()
                {
                    TemplateCacheEntries = 0
                });
                await transformer.WarmupAsync(XmlSource.FromFile(xsl));
                modes.Add(new(mode, "module-compiled", null, null, [], true, []));
                continue;
            }
            ConversionResult result;
            if (mode == "worker")
            {
                result = await worker.GenerateAsync(request, pdf);
            }
            else
            {
                using var transformer = new SaxonFoTransformer(new()
                {
                    TemplateCacheEntries = 0
                });
                using var renderer = new EmbeddedFopRenderer();
                string fo = Path.Combine(folder, "result.fo");
                await transformer.TransformWithResultAsync(request, fo);
                result = await new XmlPdfGenerator(transformer, renderer).GenerateAsync(request, pdf);
                if (item.Id.StartsWith("bank-", StringComparison.Ordinal))
                {
                    ValidateBank(xml, fo);
                }
            }
            if (item.Id.StartsWith("bank-", StringComparison.Ordinal) && result.PageCount != 3)
            {
                throw new InvalidOperationException("Bank document must contain three pages.");
            }
            using (FileStream file = File.OpenRead(pdf))
            {
                byte[] magic = new byte[5];
                file.ReadExactly(magic);
                if (Encoding.ASCII.GetString(magic) != "%PDF-")
                {
                    throw new InvalidOperationException("Invalid PDF signature.");
                }
            }
            modes.Add(new(mode, "pdf-created", null, result.PageCount, result.Diagnostics, true, result.Warnings));
        }
        catch (ConversionException error)
        {
            bool unknownFunction = error.Stage == ConversionStage.Compile && error.Diagnostics.Any(d => !d.IsWarning)
                && error.Diagnostics.Where(d => !d.IsWarning).All(d => d.Code == "XPST0017") && unsupported.Length > 0;
            string status = unknownFunction ? (item.Negative ? "negative-prerequisite-missing" : "unsupported-functions") : "failed";
            modes.Add(new(mode, status, error.Stage, null, error.Diagnostics, File.Exists(pdf) && File.ReadAllText(pdf) == previous, []));
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(item.Id + " / " + mode + ": " + error.GetType().Name + ": " + error.Message);
            modes.Add(new(mode, "failed", null, null, [], false, []));
        }
    }
    rows.Add(new(item.Id, item.Stylesheet, item.DeclaredResult, item.Negative, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(xsl))),
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(xml))), unsupported, modes));
    Console.WriteLine(item.Id + ": " + string.Join(", ", modes.Select(m => m.Mode + "=" + m.Status)));
    File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
    {
        createdUtc = DateTimeOffset.UtcNow,
        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        saxon = typeof(Saxon.Api.Processor).Assembly.GetName().Version?.ToString(),
        cacheEntries = 0,
        resourceRoots = new[] { data },
        stylesheetCount = inventory.Length,
        inventory,
        cases = rows
    }, json));
}
var markdown = new StringBuilder("# Проверка шаблонов с текущим PdfGenerator\n\n");
markdown.AppendLine($"Шаблонов: {inventory.Length}; сценариев с дополнительными банковскими входами: {rows.Count}. Кэш шаблонов отключён. Все суммы и имена синтетические.\n");
markdown.AppendLine("| Сценарий | Объявленный результат | InProcess | Worker | Страницы | Коды |\n|---|---|---|---|---|---|");
foreach (CaseResult row in rows)
{
    markdown.AppendLine($"| {row.Id} | {row.DeclaredResult} | {row.Modes[0].Status} | {row.Modes.ElementAtOrDefault(1)?.Status ?? "не применяется"} | {row.Modes[0].Pages?.ToString(CultureInfo.InvariantCulture) ?? "-"} | {string.Join(", ", row.Modes.SelectMany(m => m.Diagnostics).Select(d => d.Code).Distinct())} |");
}
markdown.AppendLine("\n`unsupported-functions` означает ошибку компиляции отсутствующих расширений. `negative-prerequisite-missing` означает, что ожидаемая динамическая ошибка не проверена: функция отсутствует. Обычный XML не является входным форматом PDF-рендерера; результату требуется FO-обёртка. Модуль layout компилируется отдельно и выполняется через basic/document.xsl.\n");
markdown.AppendLine("Банковский стандартный вариант проверяет суммы, даты, нумерацию, выбор услуг и уникальность примечаний. Вариант Altova объединяет evaluate, distinct-nodes, generate-auto-number, reset-auto-number и add-months-to-date; его запуск не подтверждает совместимость с оригинальной Altova.\n");
markdown.AppendLine("Код завершения 0 означает завершение измерений и прохождение контрольных сценариев, а не поддержку всех функций. results.json содержит хэши входов/главных шаблонов и фактические диагностики. Эталоны оригинального движка Altova отсутствуют.");
File.WriteAllText(Path.Combine(output, "report.md"), markdown.ToString());
bool failed = rows.Any(r => r.Modes.Any(m => m.Status == "failed" || !m.PreviousOutputPreserved || m.Warnings.Any(w => w.Contains("glyph", StringComparison.OrdinalIgnoreCase)))
    || (r.Modes.Count == 2 && r.Modes[0].Status != r.Modes[1].Status));
Console.WriteLine("Report: " + Path.Combine(output, "report.md"));
return failed ? 1 : 0;

void ValidateBank(string input, string fo)
{
    var source = XDocument.Load(input);
    var document = XDocument.Load(fo);
    XNamespace ns = "http://www.w3.org/1999/XSL/Format";
    XElement[] pages = document.Root!.Elements(ns + "page-sequence").ToArray();
    if (pages.Length != 3)
    {
        throw new InvalidOperationException("Expected three page sequences.");
    }
    XElement[] rows = pages[1].Descendants(ns + "table-body").Elements(ns + "table-row").ToArray();
    if (rows.Length != 13)
    {
        throw new InvalidOperationException("Expected 12 payments plus totals.");
    }
    var issued = DateTime.ParseExact(source.Root!.Attribute("issued")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    for (int index = 0; index < 12; index++)
    {
        string[] values = rows[index].Elements(ns + "table-cell").Select(c => c.Value.Trim()).ToArray();
        decimal charge = 12000 - index * 1000;
        string[] expected = [(index + 1).ToString(CultureInfo.InvariantCulture), issued.AddMonths(index + 1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), Money(100000), Money(charge), Money(100000 + charge), Money(1100000 - index * 100000)];
        if (!values.SequenceEqual(expected))
        {
            throw new InvalidOperationException("Payment differs: " + string.Join(" | ", values));
        }
    }
    if (!rows[^1].Elements(ns + "table-cell").Select(c => c.Value.Trim()).SequenceEqual(["ИТОГО", Money(1200000), Money(78000), Money(1278000), Money(0)]))
    {
        throw new InvalidOperationException("Grand totals differ.");
    }
    string[] clauses = pages[2].Descendants(ns + "block").Select(b => b.Value).Where(t => t.StartsWith("1. Данные клиента", StringComparison.Ordinal)).ToArray();
    if (clauses.Length != 1 || document.Root.Value.Contains("Не выбранная услуга", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Clause deduplication or service selection failed.");
    }
    decimal serviceTotal = source.Root.Element("services")!.Elements("service").Where(s => (string?)s.Attribute("selected") == "true").Sum(s => decimal.Parse(s.Attribute("amount")!.Value, CultureInfo.InvariantCulture));
    if (!pages[2].Value.Contains("Итого отдельно выбранные услуги: " + Money(serviceTotal), StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Service total differs.");
    }
    if (!document.Root.Value.Contains(source.Root.Element("client")!.Attribute("name")!.Value, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Client data differs.");
    }
}
string Money(decimal amount) => amount.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")).Replace('\u00a0', ' ').Replace('\u202f', ' ');

/// <summary><para>Maps one executable fixture or supporting module.</para><para>Связывает исполняемый пример либо вспомогательный модуль с входом.</para></summary>
/// <param name="Id"><para>Scenario identifier.</para><para>Идентификатор сценария.</para></param>
/// <param name="Input"><para>Input path.</para><para>Путь входа.</para></param>
/// <param name="Stylesheet"><para>Stylesheet path.</para><para>Путь шаблона.</para></param>
/// <param name="DeclaredResult"><para>Declared output kind.</para><para>Заявленный вид результата.</para></param>
/// <param name="Negative"><para>Whether the original scenario expects a dynamic error.</para><para>Ожидает ли исходный сценарий динамическую ошибку.</para></param>
internal sealed record TemplateCase(string Id, string Input, string Stylesheet, string DeclaredResult, bool Negative = false);

/// <summary><para>Records observed application behavior for one execution mode.</para><para>Фиксирует поведение приложения в одном режиме выполнения.</para></summary>
/// <param name="Mode"><para>Execution mode.</para><para>Режим выполнения.</para></param>
/// <param name="Status"><para>Observed status.</para><para>Полученный статус.</para></param>
/// <param name="Stage"><para>Failure stage.</para><para>Этап ошибки.</para></param>
/// <param name="Pages"><para>Reported PDF pages.</para><para>Число страниц PDF.</para></param>
/// <param name="Diagnostics"><para>Safe engine diagnostics.</para><para>Безопасные диагностики движка.</para></param>
/// <param name="PreviousOutputPreserved"><para>Whether failed conversion preserved the sentinel.</para><para>Сохранила ли ошибка прежнее содержимое файла.</para></param>
/// <param name="Warnings"><para>Renderer warnings.</para><para>Предупреждения рендерера.</para></param>
internal sealed record ModeResult(string Mode, string Status, ConversionStage? Stage, int? Pages, IReadOnlyList<XsltExecutionDiagnostic> Diagnostics, bool PreviousOutputPreserved, IReadOnlyList<string> Warnings);

/// <summary><para>Captures fixture fingerprints and measured results.</para><para>Сохраняет хэши примера и измеренные результаты.</para></summary>
/// <param name="Id"><para>Scenario identifier.</para><para>Идентификатор сценария.</para></param>
/// <param name="Stylesheet"><para>Relative stylesheet path.</para><para>Относительный путь шаблона.</para></param>
/// <param name="DeclaredResult"><para>Declared result kind.</para><para>Заявленный вид результата.</para></param>
/// <param name="Negative"><para>Negative-case marker.</para><para>Признак отрицательного сценария.</para></param>
/// <param name="StylesheetSha256"><para>Stylesheet fingerprint.</para><para>Хэш шаблона.</para></param>
/// <param name="InputSha256"><para>Input fingerprint.</para><para>Хэш входа.</para></param>
/// <param name="Unsupported"><para>Functions the static audit flags as unsupported.</para><para>Функции, отмеченные аудитом как неподдерживаемые.</para></param>
/// <param name="Modes"><para>Measured results.</para><para>Результаты измерений.</para></param>
internal sealed record CaseResult(string Id, string Stylesheet, string DeclaredResult, bool Negative, string StylesheetSha256, string InputSha256, string[] Unsupported, IReadOnlyList<ModeResult> Modes);
