using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the XsltDiagnostics contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltDiagnostics для преобразования PDF.</para>
/// </summary>
public sealed class XsltDiagnostics : IXsltDiagnostics
{

    /// <summary>
    /// <para>Gets or defines the Xsl value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение Xsl, используемое XsltDiagnostics.</para>
    /// </summary>
    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>
    /// <para>Gets or defines the AltovaNamespace value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение AltovaNamespace, используемое XsltDiagnostics.</para>
    /// </summary>
    private const string AltovaNamespace = "http://www.altova.com/xslt-extensions";

    /// <summary>
    /// <para>Gets or defines the FunctionsNamespace value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение FunctionsNamespace, используемое XsltDiagnostics.</para>
    /// </summary>
    private const string FunctionsNamespace = "http://www.w3.org/2005/xpath-functions";

    /// <summary>
    /// <para>Gets or defines the TypesNamespace value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение TypesNamespace, используемое XsltDiagnostics.</para>
    /// </summary>
    private const string TypesNamespace = "http://www.w3.org/2001/XMLSchema";

    /// <summary>
    /// <para>Gets or defines the Calls value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение Calls, используемое XsltDiagnostics.</para>
    /// </summary>
    private static readonly Regex Calls = new(@"(?<![\w.\-])(?<prefix>[A-Za-z_][\w.\-]*):(?<name>[A-Za-z_][\w.\-]*)\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// <para>Gets or defines the RestrictedUnprefixedCalls value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение RestrictedUnprefixedCalls, используемое XsltDiagnostics.</para>
    /// </summary>
    private static readonly Regex RestrictedUnprefixedCalls = new(@"(?<![\w.\-:])(?<name>doc|unparsed-text|collection|uri-collection)\s*\(", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// <para>Gets or defines the maxStylesheetBytes value used by XsltDiagnostics.</para>
    /// <para>Возвращает или определяет значение maxStylesheetBytes, используемое XsltDiagnostics.</para>
    /// </summary>
    private readonly long maxStylesheetBytes;
    /// <summary><para>Selected runtime compatibility profile.</para><para>Выбранный профиль совместимости выполнения.</para></summary>
    private readonly Altova2012Options? compatibility;


    /// <summary>
    /// <para>Initializes XsltDiagnostics with an immutable snapshot of service settings.</para>
    /// <para>Создаёт XsltDiagnostics со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="maxStylesheetBytes"><para>The maxStylesheetBytes argument.</para><para>Аргумент maxStylesheetBytes.</para></param>
    /// <param name="altova2012"><para>Optional general compatibility profile.</para><para>Необязательный общий профиль совместимости.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public XsltDiagnostics(long maxStylesheetBytes = 16 * 1024 * 1024, Altova2012Options? altova2012 = null)
    {
        if (maxStylesheetBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStylesheetBytes));
        }

        this.maxStylesheetBytes = maxStylesheetBytes;
        altova2012?.Validate();
        compatibility = altova2012;
    }


    /// <summary>
    /// <para>Executes Analyze as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет Analyze в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="xslt"><para>The xslt argument.</para><para>Аргумент xslt.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public XsltDiagnosticReport Analyze(string xslt)
    {
        ArgumentNullException.ThrowIfNull(xslt);
        var context = new Context();
        if (Encoding.UTF8.GetByteCount(xslt) > maxStylesheetBytes)
        {
            context.Add("XSLT_SIZE", XsltDiagnosticSeverity.Error, "Stylesheet exceeds the diagnostic size limit.");
            return context.Report();
        }

        LoadText(xslt, "(inline)", context);
        AnalyzeLoaded(context);
        return context.Report();
    }


    /// <summary>
    /// <para>Executes AnalyzeFile as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет AnalyzeFile в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public XsltDiagnosticReport AnalyzeFile(string xsltPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xsltPath);
        string root = Path.GetDirectoryName(Path.GetFullPath(xsltPath))!;
        if (!Directory.Exists(root))
        {
            var context = new Context();
            context.Add("XSLT_READ", XsltDiagnosticSeverity.Error, "Cannot read stylesheet directory.", Path.GetFileName(xsltPath));
            return context.Report();
        }
        return AnalyzeFile(xsltPath, new LocalResourcePolicy([root]));
    }

    /// <summary><para>Inspects includes/imports with the same allowed roots as runtime.</para><para>Проверяет include/import с теми же разрешёнными корнями, что и выполнение.</para></summary>
    /// <param name="xsltPath"><para>Main stylesheet path.</para><para>Путь главного шаблона.</para></param>
    /// <param name="resources"><para>Runtime resource policy.</para><para>Политика ресурсов выполнения.</para></param>
    /// <returns><para>Static diagnostic report.</para><para>Статический отчёт диагностики.</para></returns>
    public XsltDiagnosticReport AnalyzeFile(string xsltPath, LocalResourcePolicy resources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xsltPath);
        var context = new Context();
        string rootFile = Path.GetFullPath(xsltPath);
        string rootDirectory = Path.GetDirectoryName(rootFile)!;
        LoadFile(rootFile, rootDirectory, context, resources);
        AnalyzeLoaded(context);
        return context.Report();
    }


    /// <summary>
    /// <para>Executes LoadFile as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет LoadFile в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <param name="rootDirectory"><para>The rootDirectory argument.</para><para>Аргумент rootDirectory.</para></param>
    /// <param name="resources"><para>Allowed runtime resource roots.</para><para>Разрешённые корни ресурсов выполнения.</para></param>
    /// <param name="context"><para>The context argument.</para><para>Аргумент context.</para></param>
    private void LoadFile(string path, string rootDirectory, Context context, LocalResourcePolicy resources)
    {
        string source = Path.GetRelativePath(rootDirectory, path);
        if (!context.Visited.Add(path))
        {
            return;
        }

        if (context.Visited.Count > 256)
        {
            context.Add("XSLT_MODULE_LIMIT", XsltDiagnosticSeverity.Error, "Too many stylesheet modules.", source);
            return;
        }

        try
        {
            path = resources.Resolve(new Uri(Path.GetFullPath(path)));
            if (HasSymbolicLink(path))
            {
                context.Add("XSLT_LINK", XsltDiagnosticSeverity.Error, "Symbolic links in stylesheet paths are not supported.", source);
                return;
            }

            if (new FileInfo(path).Length > maxStylesheetBytes)
            {
                context.Add("XSLT_SIZE", XsltDiagnosticSeverity.Error, "Stylesheet exceeds the diagnostic size limit.", source);
                return;
            }

            using var reader = XmlReader.Create(path, SafeReaderSettings());
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            context.Documents.Add((document, source));
            ValidateRoot(document, context, source);
            foreach (XElement module in document.Descendants().Where(e => e.Name == Xsl + "include" || e.Name == Xsl + "import"))
            {
                string? href = (string?)module.Attribute("href");
                if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(new Uri(path), href, out Uri? uri) || !uri.IsFile || uri.IsUnc)
                {
                    context.Add("XSLT_INCLUDE", XsltDiagnosticSeverity.Error, "An include/import is missing or is not a local file.", source, Line(module));
                    continue;
                }

                string modulePath;
                try
                {
                    modulePath = resources.Resolve(uri);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    context.Add("XSLT_INCLUDE", XsltDiagnosticSeverity.Error, "An include/import leaves the stylesheet directory.", source, Line(module));
                    continue;
                }

                LoadFile(modulePath, rootDirectory, context, resources);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
        {
            context.Add("XSLT_READ", XsltDiagnosticSeverity.Error, $"Cannot read or parse stylesheet: {ex.GetType().Name}.", source);
        }
    }


    /// <summary>
    /// <para>Executes LoadText as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет LoadText в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="xslt"><para>The xslt argument.</para><para>Аргумент xslt.</para></param>
    /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
    /// <param name="context"><para>The context argument.</para><para>Аргумент context.</para></param>
    private void LoadText(string xslt, string source, Context context)
    {
        try
        {
            using var input = new StringReader(xslt);
            using var reader = XmlReader.Create(input, SafeReaderSettings());
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            context.Documents.Add((document, source));
            ValidateRoot(document, context, source);
        }
        catch (XmlException)
        {
            context.Add("XSLT_PARSE", XsltDiagnosticSeverity.Error, "Cannot parse stylesheet XML.", source);
        }
    }


    /// <summary>
    /// <para>Executes SafeReaderSettings as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет SafeReaderSettings в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private XmlReaderSettings SafeReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = maxStylesheetBytes
    };


    /// <summary>
    /// <para>Executes HasSymbolicLink as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет HasSymbolicLink в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static bool HasSymbolicLink(string path)
    {
        for (FileSystemInfo? item = new FileInfo(path); item is not null; item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// <para>Executes ValidateRoot as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет ValidateRoot в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="document"><para>The document argument.</para><para>Аргумент document.</para></param>
    /// <param name="context"><para>The context argument.</para><para>Аргумент context.</para></param>
    /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
    private static void ValidateRoot(XDocument document, Context context, string source)
    {
        if (document.Root is not XElement root || (root.Name != Xsl + "stylesheet" && root.Name != Xsl + "transform" && root.Name != Xsl + "package"))
        {
            context.Add("XSLT_ROOT", XsltDiagnosticSeverity.Error, "The document root is not an XSLT stylesheet, transform or package.", source, document.Root is null ? null : Line(document.Root));
        }
    }


    /// <summary>
    /// <para>Executes AnalyzeLoaded as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет AnalyzeLoaded в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="context"><para>The context argument.</para><para>Аргумент context.</para></param>
    private void AnalyzeLoaded(Context context)
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach ((XDocument document, _) in context.Documents)
        {
            foreach (XElement function in document.Descendants(Xsl + "function"))
            {
                string? name = (string?)function.Attribute("name");
                if (name is null || !name.Contains(':', StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = name.Split(':', 2);
                XNamespace? ns = function.GetNamespaceOfPrefix(parts[0]);
                if (ns is not null)
                {
                    defined.Add(Key(ns.NamespaceName, parts[1]));
                }
            }
        }

        foreach ((XDocument document, string source) in context.Documents)
        {
            foreach (XElement element in document.Descendants())
            {
                if (element.Name == Xsl + "include" || element.Name == Xsl + "import")
                {
                    if (source == "(inline)")
                    {
                        context.Add("XSLT_INCLUDE", XsltDiagnosticSeverity.Warning, "Inline analysis cannot inspect include/import modules; use AnalyzeFile.", source, Line(element));
                    }
                }
                else if (element.Name == Xsl + "result-document")
                {
                    context.Add("XSLT_RESULT_DOCUMENT", XsltDiagnosticSeverity.Error, "Secondary output documents are disabled by the transformer.", source, Line(element));
                }
                else if (element.Name == Xsl + "import-schema")
                {
                    context.Add("XSLT_SCHEMA", XsltDiagnosticSeverity.Error, "Schema-aware XSLT is not supported by the current SaxonCS-HE provider.", source, Line(element));
                }
                else if (element.Name == Xsl + "evaluate")
                {
                    context.Add("XSLT_DYNAMIC_EVALUATION", XsltDiagnosticSeverity.Warning, "Dynamic XPath evaluation cannot be checked statically; review its expressions and resource access.", source, Line(element));
                }

                foreach (XAttribute attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
                {
                    string expression = MaskLiteralsAndComments(attribute.Value);
                    foreach (Match match in RestrictedUnprefixedCalls.Matches(expression))
                    {
                        string name = match.Groups["name"].Value;
                        context.Add("XSLT_RUNTIME_RESOURCE", XsltDiagnosticSeverity.Error,
                            $"Function {name}() requests a runtime resource blocked by the transformer.", source, Line(attribute));
                    }

                    foreach (Match match in Calls.Matches(expression))
                    {
                        string prefix = match.Groups["prefix"].Value;
                        string name = match.Groups["name"].Value;
                        string? uri = element.GetNamespaceOfPrefix(prefix)?.NamespaceName;
                        if (uri is null)
                        {
                            context.Add("XSLT_PREFIX", XsltDiagnosticSeverity.Warning, $"Unresolved function prefix '{prefix}'.", source, Line(attribute));
                            continue;
                        }

                        int? arity = CountArguments(attribute.Value, match.Index + match.Length - 1);
                        string declarationKey = Key(uri, name);
                        string key = declarationKey + "#" + arity;
                        if (!context.Functions.TryGetValue(key, out FunctionCount? count))
                        {
                            (XsltFunctionSupport support, string reason) = Classify(uri, name, defined.Contains(declarationKey), arity);
                            count = new FunctionCount(uri, name, support, reason) { ArgumentCount = arity };
                            context.Functions.Add(key, count);
                        }

                        count.Occurrences++;
                        if (uri == AltovaNamespace && name == "evaluate")
                        {
                            context.Add("XSLT_DYNAMIC_EVALUATION", XsltDiagnosticSeverity.Warning,
                                "Dynamic expressions require execution tests; stylesheet functions, schema types and current() are outside the general profile.", source, Line(attribute));
                        }
                    }
                }
            }
        }

        foreach (FunctionCount function in context.Functions.Values.Where(f => f.Support == XsltFunctionSupport.Unsupported))
        {
            context.Add("XSLT_FUNCTION_UNSUPPORTED", XsltDiagnosticSeverity.Error,
                $"Function {{{function.NamespaceUri}}}{function.Name} is not supported: {function.Explanation}");
        }

        context.Add("XSLT_STATIC_SCOPE", XsltDiagnosticSeverity.Info,
            "Static inspection cannot prove runtime compatibility; conditional and dynamically constructed calls need execution tests.");
    }


    /// <summary>
    /// <para>Executes Classify as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет Classify в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <param name="defined"><para>The defined argument.</para><para>Аргумент defined.</para></param>
    /// <param name="arity"><para>Observed argument count.</para><para>Обнаруженное число аргументов.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private (XsltFunctionSupport Support, string Reason) Classify(string uri, string name, bool defined, int? arity)
    {
        if (defined)
        {
            return (XsltFunctionSupport.Supported, "Declared as xsl:function in the inspected stylesheet modules; its behavior still needs testing.");
        }

        if (uri == AltovaNamespace)
        {
            if (compatibility is null)
            {
                return (XsltFunctionSupport.Unsupported, "AltovaXML 2012 compatibility is disabled.");
            }
            (int min, int max) = AltovaFunctionCatalog.Arity(name);
            if (min < 0 || arity is not null && (arity < min || arity > max))
            {
                return (XsltFunctionSupport.Unsupported, "This Altova function/arity is outside the selected general profile.");
            }
            return (arity is null ? XsltFunctionSupport.ReviewRequired : XsltFunctionSupport.Supported,
                Altova2012Options.ImplementationVersion + ": implemented from documentation; original Altova comparison unconfirmed.");
        }

        if (uri == FunctionsNamespace && name is "doc" or "document" or "unparsed-text" or "collection" or "uri-collection")
        {
            return (XsltFunctionSupport.Unsupported, "The transformer blocks external runtime resources.");
        }

        if (uri == FunctionsNamespace || uri == TypesNamespace || uri == Xsl.NamespaceName)
        {
            return (XsltFunctionSupport.Supported, "Standard function namespace; specific arguments and runtime behavior still need testing.");
        }

        if (uri == "http://saxon.sf.net/" || uri.StartsWith("http://exslt.org/", StringComparison.Ordinal) || uri.StartsWith("http://expath.org/", StringComparison.Ordinal))
        {
            return (XsltFunctionSupport.ReviewRequired, "Built-in extension availability depends on the SaxonCS edition and function signature.");
        }

        return (XsltFunctionSupport.Unsupported, "No matching xsl:function or registered extension function is available in this package.");
    }


    /// <summary>
    /// <para>Executes Key as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет Key в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string Key(string uri, string name) => $"{{{uri}}}{name}";

    /// <summary><para>Counts top-level arguments, excluding nested expressions, strings and comments.</para><para>Считает аргументы верхнего уровня с учётом вложений, строк и комментариев.</para></summary>
    /// <param name="expression"><para>Original expression.</para><para>Исходное выражение.</para></param>
    /// <param name="open"><para>Opening parenthesis index.</para><para>Индекс открывающей скобки.</para></param>
    /// <returns><para>Argument count, or null if incomplete.</para><para>Число аргументов либо null при неполном выражении.</para></returns>
    private static int? CountArguments(string expression, int open)
    {
        string masked = MaskLiteralsAndComments(expression);
        int depth = 0, commas = 0;
        for (int i = open + 1; i < masked.Length; i++)
        {
            char c = masked[i];
            if (c == ')' && depth == 0)
            {
                return string.IsNullOrWhiteSpace(expression[(open + 1)..i]) ? 0 : commas + 1;
            }
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                commas++;
            }
        }
        return null;
    }


    /// <summary>
    /// <para>Executes Line as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет Line в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="item"><para>The item argument.</para><para>Аргумент item.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static int? Line(XObject item) => item is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;


    /// <summary>
    /// <para>Executes MaskLiteralsAndComments as part of the XsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет MaskLiteralsAndComments в цепочке преобразования XsltDiagnostics.</para>
    /// </summary>
    /// <param name="expression"><para>The expression argument.</para><para>Аргумент expression.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string MaskLiteralsAndComments(string expression)
    {
        char[] output = expression.ToCharArray();
        char quote = '\0';
        int comments = 0;
        for (int i = 0; i < output.Length; i++)
        {
            if (comments > 0)
            {
                if (i + 1 < output.Length && expression[i] == '(' && expression[i + 1] == ':')
                {
                    comments++;
                    output[i++] = ' ';
                    output[i] = ' ';
                }
                else if (i + 1 < output.Length && expression[i] == ':' && expression[i + 1] == ')')
                {
                    comments--;
                    output[i++] = ' ';
                    output[i] = ' ';
                }
                else
                {
                    output[i] = ' ';
                }

                continue;
            }

            if (quote != '\0')
            {
                if (expression[i] == quote)
                {
                    if (i + 1 < output.Length && expression[i + 1] == quote)
                    {
                        output[i++] = ' ';
                        output[i] = ' ';
                        continue;
                    }

                    quote = '\0';
                }

                output[i] = ' ';
                continue;
            }

            if (i + 1 < output.Length && expression[i] == '(' && expression[i + 1] == ':')
            {
                comments++;
                output[i++] = ' ';
                output[i] = ' ';
            }
            else if (expression[i] is '\'' or '"')
            {
                quote = expression[i];
                output[i] = ' ';
            }
        }

        return new string(output);
    }


    /// <summary>
    /// <para>Provides the FunctionCount contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию FunctionCount для преобразования PDF.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <param name="support"><para>The support argument.</para><para>Аргумент support.</para></param>
    /// <param name="explanation"><para>The explanation argument.</para><para>Аргумент explanation.</para></param>
    private sealed class FunctionCount(string uri, string name, XsltFunctionSupport support, string explanation)
    {
        /// <summary><para>Observed arity.</para><para>Обнаруженная арность.</para></summary>
        public int? ArgumentCount
        {
            get; init;
        }

        /// <summary>
        /// <para>Gets or defines the NamespaceUri value used by FunctionCount.</para>
        /// <para>Возвращает или определяет значение NamespaceUri, используемое FunctionCount.</para>
        /// </summary>
        public string NamespaceUri { get; } = uri;

        /// <summary>
        /// <para>Gets or defines the Name value used by FunctionCount.</para>
        /// <para>Возвращает или определяет значение Name, используемое FunctionCount.</para>
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// <para>Gets or defines the Support value used by FunctionCount.</para>
        /// <para>Возвращает или определяет значение Support, используемое FunctionCount.</para>
        /// </summary>
        public XsltFunctionSupport Support { get; } = support;

        /// <summary>
        /// <para>Gets or defines the Explanation value used by FunctionCount.</para>
        /// <para>Возвращает или определяет значение Explanation, используемое FunctionCount.</para>
        /// </summary>
        public string Explanation { get; } = explanation;

        /// <summary>
        /// <para>Gets or defines the Occurrences value used by FunctionCount.</para>
        /// <para>Возвращает или определяет значение Occurrences, используемое FunctionCount.</para>
        /// </summary>
        public int Occurrences
        {
            get; set;
        }
    }


    /// <summary>
    /// <para>Provides the Context contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию Context для преобразования PDF.</para>
    /// </summary>
    private sealed class Context
    {

        /// <summary>
        /// <para>Gets or defines the Visited value used by Context.</para>
        /// <para>Возвращает или определяет значение Visited, используемое Context.</para>
        /// </summary>
        public HashSet<string> Visited { get; } = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        /// <summary>
        /// <para>Gets or defines the Documents value used by Context.</para>
        /// <para>Возвращает или определяет значение Documents, используемое Context.</para>
        /// </summary>
        public List<(XDocument Document, string Source)> Documents { get; } = [];

        /// <summary>
        /// <para>Gets or defines the Functions value used by Context.</para>
        /// <para>Возвращает или определяет значение Functions, используемое Context.</para>
        /// </summary>
        public Dictionary<string, FunctionCount> Functions { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// <para>Gets or defines the Diagnostics value used by Context.</para>
        /// <para>Возвращает или определяет значение Diagnostics, используемое Context.</para>
        /// </summary>
        public List<XsltDiagnostic> Diagnostics { get; } = [];


        /// <summary>
        /// <para>Executes Add as part of the Context conversion workflow.</para>
        /// <para>Выполняет Add в цепочке преобразования Context.</para>
        /// </summary>
        /// <param name="code"><para>The code argument.</para><para>Аргумент code.</para></param>
        /// <param name="severity"><para>The severity argument.</para><para>Аргумент severity.</para></param>
        /// <param name="message"><para>The message argument.</para><para>Аргумент message.</para></param>
        /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
        /// <param name="line"><para>The line argument.</para><para>Аргумент line.</para></param>
        public void Add(string code, XsltDiagnosticSeverity severity, string message, string? source = null, int? line = null)
                    => Diagnostics.Add(new(code, severity, message, source, line));


        /// <summary>
        /// <para>Executes Report as part of the Context conversion workflow.</para>
        /// <para>Выполняет Report в цепочке преобразования Context.</para>
        /// </summary>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        public XsltDiagnosticReport Report() => new(
                    Functions.Values.OrderBy(f => f.NamespaceUri, StringComparer.Ordinal).ThenBy(f => f.Name, StringComparer.Ordinal)
                        .Select(f => new XsltFunctionUsage(f.NamespaceUri, f.Name, f.Occurrences, f.Support, f.Explanation) { ArgumentCount = f.ArgumentCount }).ToArray(),
                    [.. Diagnostics], Documents.Count);
    }
}
