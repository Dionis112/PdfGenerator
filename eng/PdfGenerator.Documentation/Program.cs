using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

bool check = args.Contains("--check");
string root = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".");
int failures = 0;
foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(p =>
    !p.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin" or "artifacts" or ".git")))
{
    string source = File.ReadAllText(file);
    SyntaxNode tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Diagnose)).GetRoot();
    SyntaxNode[] members = tree.DescendantNodes().Where(n => n is BaseTypeDeclarationSyntax or BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or
        EnumMemberDeclarationSyntax or DelegateDeclarationSyntax or FieldDeclarationSyntax).ToArray();
    if (check)
    {
        foreach (SyntaxNode node in members)
        {
            DocumentationCommentTriviaSyntax? documentation = node.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();
            string content = documentation?.ToFullString() ?? "";
            try
            {
                var xml = XElement.Parse("<doc>" + Regex.Replace(content, @"(?m)^\s*/// ?", "") + "</doc>");
                foreach (XElement element in xml.Elements().Where(e => e.Name.LocalName is "summary" or "param" or "returns" or "exception"))
                {
                    XElement[] paragraphs = element.Elements("para").ToArray();
                    if (paragraphs.Length < 2 || paragraphs[0].Value.Any(c => c >= 'А' && c <= 'я') || !paragraphs[1].Value.Any(c => c >= 'А' && c <= 'я'))
                    {
                        Console.Error.WriteLine($"{file}: invalid English/Russian paragraphs in {element.Name}");
                        failures++;
                    }
                }
            }
            catch (System.Xml.XmlException) { Console.Error.WriteLine($"{file}: malformed XML documentation"); failures++; }
            if (!content.Contains("<summary>") || content.Split("<para>").Length < 3 || !content.Any(c => c >= 'А' && c <= 'я'))
            {
                Console.Error.WriteLine($"{file}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: missing bilingual summary");
                failures++;
            }
            SeparatedSyntaxList<ParameterSyntax>? parameters = node switch
            {
                BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
                TypeDeclarationSyntax declaration => declaration.ParameterList?.Parameters,
                DelegateDeclarationSyntax declaration => declaration.ParameterList.Parameters,
                _ => null
            };
            foreach (ParameterSyntax parameter in parameters ?? [])
            {
                if (!content.Contains($"<param name=\"{parameter.Identifier.ValueText}\">"))
                {
                    Console.Error.WriteLine($"{file}: missing param {parameter.Identifier.ValueText}");
                    failures++;
                }
            }
            if (node is MethodDeclarationSyntax declarationWithReturn && declarationWithReturn.ReturnType.ToString() != "void" && !content.Contains("<returns>"))
            {
                Console.Error.WriteLine($"{file}: missing returns for {Name(node)}");
                failures++;
            }
        }
    }
    else
    {
        SyntaxNode updated = tree.ReplaceNodes(members, (original, rewritten) =>
        {
            string name = Name(original);
            string type = original.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? name;
            (string english, string russian) = Describe(original, name, type);
            var comment = new StringBuilder("\n/// <summary>\n/// <para>" + Escape(english) + "</para>\n/// <para>" + Escape(russian) + "</para>\n/// </summary>\n");
            SeparatedSyntaxList<ParameterSyntax>? parameters = original switch
            {
                BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
                TypeDeclarationSyntax declaration => declaration.ParameterList?.Parameters,
                DelegateDeclarationSyntax declaration => declaration.ParameterList.Parameters,
                _ => null
            };
            foreach (ParameterSyntax parameter in parameters ?? [])
            {
                string p = parameter.Identifier.ValueText;
                (string en, string ru) = Parameter(p);
                comment.AppendLine($"/// <param name=\"{p}\"><para>{Escape(en)}</para><para>{Escape(ru)}</para></param>");
            }
            if (original is MethodDeclarationSyntax methodDeclaration && methodDeclaration.ReturnType.ToString() != "void")
            {
                comment.AppendLine("/// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>");
            }
            if (original is MethodDeclarationSyntax m && m.ParameterList.Parameters.Any(p => p.Type?.ToString() == "CancellationToken"))
            {
                comment.AppendLine("/// <exception cref=\"OperationCanceledException\"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>");
            }
            if (original is ConstructorDeclarationSyntax || original is MethodDeclarationSyntax { Identifier.ValueText: "GenerateAsync" or "TransformAsync" or "RenderFileAsync" })
            {
                comment.AppendLine("/// <exception cref=\"ArgumentException\"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>");
            }
            IEnumerable<SyntaxTrivia> trivia = rewritten.GetLeadingTrivia().Where(t => t.GetStructure() is not DocumentationCommentTriviaSyntax);
            return rewritten.WithLeadingTrivia(SyntaxFactory.TriviaList(trivia).AddRange(SyntaxFactory.ParseLeadingTrivia(comment.ToString())));
        });
        File.WriteAllText(file, updated.ToFullString(), new UTF8Encoding(false));
    }
}
return failures == 0 ? 0 : 1;

static string Escape(string text) => System.Security.SecurityElement.Escape(text)!;
static string Name(SyntaxNode node) => node switch
{
    BaseTypeDeclarationSyntax n => n.Identifier.ValueText,
    ConstructorDeclarationSyntax n => n.Identifier.ValueText,
    DestructorDeclarationSyntax n => n.Identifier.ValueText,
    MethodDeclarationSyntax n => n.Identifier.ValueText,
    PropertyDeclarationSyntax n => n.Identifier.ValueText,
    EnumMemberDeclarationSyntax n => n.Identifier.ValueText,
    DelegateDeclarationSyntax n => n.Identifier.ValueText,
    FieldDeclarationSyntax n => n.Declaration.Variables.First().Identifier.ValueText,
    _ => node.Kind().ToString()
};
static (string, string) Describe(SyntaxNode node, string name, string type)
{
    if (type.EndsWith("Tests") || name.EndsWith("Tests"))
    {
        return ($"Runs the {name} validation scenario.", $"Выполняет проверку сценария {name}.");
    }
    if (node is ConstructorDeclarationSyntax)
    {
        return ($"Initializes {name} with an immutable snapshot of service settings.", $"Создаёт {name} со снимком настроек сервиса.");
    }
    if (node is BaseTypeDeclarationSyntax)
    {
        return name switch
        {
            "SaxonFoTransformer" => ("Transforms XML to FO with bounded snapshot-based XSLT caching; each job owns its transformer and parameters.", "Преобразует XML в FO с ограниченным кэшем снимков XSLT; transformer и параметры принадлежат отдельному заданию."),
            "EmbeddedFopRenderer" => ("Renders FO with bundled FOP and leases a cached configuration/font factory for each document.", "Рендерит FO встроенным FOP, выделяя каждому документу фабрику из кэша конфигурации и шрифтов."),
            "WorkerXmlPdfGenerator" => ("Runs conversions in a bounded pool of bundled .NET workers; publishes verified PDFs from the parent process.", "Выполняет преобразования в ограниченном пуле встроенных .NET worker; проверенные PDF публикует родительский процесс."),
            "ScheduledXmlPdfGenerator" => ("Bounds complete conversion chains and queue waits, rejects concurrent ownership of an output path, and drains jobs on shutdown.", "Ограничивает полные цепочки преобразования и ожидание в очереди, отклоняет конфликты выходного пути и завершает задания при остановке."),
            "XmlSource" => ("Describes a file or caller-owned stream, read from its current position without seeking or closing it.", "Описывает файл или поток вызывающего кода, читаемый с текущей позиции без перемотки и закрытия."),
            "ConversionRequest" => ("Captures immutable job metadata, qualified typed parameters, resource roots and per-call limits; input streams remain caller-owned.", "Фиксирует неизменяемые данные задания, типизированные параметры QName, корни ресурсов и лимиты вызова; входные потоки принадлежат вызывающему коду."),
            "PdfProcessingProfile" => ("Owns a versioned font/configuration snapshot; dispose only after all associated jobs and services finish.", "Владеет версионированным снимком шрифтов и конфигурации; освобождать после завершения всех связанных заданий и сервисов."),
            "LocalResourcePolicy" => ("Resolves only local resources within explicitly permitted roots and rejects network paths and symbolic links.", "Разрешает локальные ресурсы внутри явно разрешённых корней, отклоняя сетевые пути и символические ссылки."),
            "FoImagePreparation" => ("Rewrites FO incrementally and prepares bounded immutable image snapshots without loading the complete document tree.", "Последовательно переписывает FO и подготавливает ограниченные неизменяемые снимки изображений без дерева всего документа."),
            "PngIccProfileRemover" => ("Removes PNG iCCP chunks without decoding pixels; validates retained CRCs and preserves transparency.", "Удаляет блоки PNG iCCP без декодирования пикселей; проверяет CRC сохранённых блоков и сохраняет прозрачность."),
            _ => ($"Provides the {name} contract or implementation for PDF conversion.", $"Предоставляет контракт или реализацию {name} для преобразования PDF.")
        };
    }
    return name switch
    {
        "GenerateAsync" or "GenerateFileAsync" => ("Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.", "Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым."),
        "TransformAsync" or "TransformFileAsync" or "TransformWithResultAsync" => ("Prepares bounded input snapshots and transforms with job-local parameters; in-process cancellation is observed around synchronous Saxon execution.", "Подготавливает ограниченные снимки входов и преобразует с параметрами задания; отмена внутри процесса проверяется вокруг синхронного выполнения Saxon."),
        "RenderFileAsync" or "RenderWithResultAsync" or "RenderWithResourcesAsync" => ("Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.", "Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла."),
        "Dispose" or "DisposeAsync" => ("Releases owned resources; caller-owned input and output streams are not closed.", "Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются."),
        "RemoveIccProfile" => ("Validates PNG chunk boundaries and retained CRCs, removes iCCP, and copies pixel chunks without decoding or re-encoding them.", "Проверяет границы блоков PNG и CRC сохранённых блоков, удаляет iCCP и копирует блоки пикселей без декодирования и перекодирования."),
        "WarmupAsync" => ("Compiles a coherent stylesheet snapshot into the bounded cache; parameter values do not participate in its key.", "Компилирует согласованный снимок шаблона в ограниченный кэш; значения параметров не входят в ключ."),
        "CacheStatistics" or "Statistics" => ("Returns an immutable snapshot of cache or worker counters.", "Возвращает неизменяемый снимок счётчиков кэша или worker."),
        "Resolve" => ("Normalizes a file URI, checks permitted roots and rejects symbolic links before returning the local path.", "Нормализует файловый URI, проверяет разрешённые корни и отклоняет символические ссылки перед возвратом локального пути."),
        "MaxWorkingSetBytes" => ("Gets the observed working-set threshold that triggers worker termination; this is not an operating-system hard memory limit. Zero disables monitoring.", "Порог наблюдаемого working set для остановки worker; это не строгий лимит памяти ОС. Ноль отключает наблюдение."),
        _ when node is PropertyDeclarationSyntax or FieldDeclarationSyntax or EnumMemberDeclarationSyntax => ($"Gets or defines the {name} value used by {type}.", $"Возвращает или определяет значение {name}, используемое {type}."),
        _ => ($"Executes {name} as part of the {type} conversion workflow.", $"Выполняет {name} в цепочке преобразования {type}.")
    };
}
static (string, string) Parameter(string name) => name switch
{
    "cancellationToken" or "token" => ("Cancellation token; synchronous in-process engines finish before their slot is released.", "Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота."),
    "request" => ("Immutable job inputs and settings; its streams remain caller-owned.", "Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду."),
    "output" => ("Output destination; caller-owned streams remain open and final copying is not atomic.", "Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно."),
    "xmlPath" => ("Local XML input path.", "Путь локального входного XML."),
    "xsltPath" => ("Local stylesheet path; relative dependencies require a permitted base directory.", "Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог."),
    "pdfPath" => ("PDF destination, atomically replaced only after successful rendering.", "Путь PDF, атомарно заменяемый после успешного рендеринга."),
    "options" or "settings" => ("Immutable configuration snapshot for this service or job.", "Неизменяемый снимок конфигурации сервиса или задания."),
    _ => ($"The {name} argument.", $"Аргумент {name}.")
};
